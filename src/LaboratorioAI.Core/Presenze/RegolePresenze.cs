#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LaboratorioAI.Core.Presenze;

/// <summary>
/// Fuso orario delle presenze: gli istanti si salvano in UTC, il giorno e l'ora di una timbratura sono quelli di
/// Europe/Rome (su Windows può chiamarsi «W. Europe Standard Time»).
/// </summary>
public static class FusoPresenze
{
    private static readonly TimeZoneInfo Roma = Trova();

    private static TimeZoneInfo Trova()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        }
    }

    /// <summary>Data e ora locali di un istante UTC.</summary>
    public static DateTime Locale(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Roma);

    public static DateOnly GiornoLocale(DateTime utc) => DateOnly.FromDateTime(Locale(utc));

    /// <summary>Ora locale nel formato «HH:mm».</summary>
    public static string OraLocale(DateTime utc) => Locale(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Istante UTC di un giorno e di un'ora locale «HH:mm». Nulla se l'ora non esiste (salto dell'ora legale, in primavera).
    /// Se l'ora capita due volte (fine dell'ora legale, in autunno) vale la prima.
    /// </summary>
    public static DateTime? ToUtc(DateOnly giorno, string ora)
    {
        if (!ParsingPresenze.TryValidaOra(ora, out var o)) throw new ArgumentException("Ora non valida.", nameof(ora));
        var locale = new DateTime(giorno.Year, giorno.Month, giorno.Day,
            int.Parse(o[..2], CultureInfo.InvariantCulture), int.Parse(o[3..], CultureInfo.InvariantCulture), 0,
            DateTimeKind.Unspecified);
        if (Roma.IsInvalidTime(locale)) return null;
        var scostamento = Roma.IsAmbiguousTime(locale)
            ? Roma.GetAmbiguousTimeOffsets(locale).Max()
            : Roma.GetUtcOffset(locale);
        return DateTime.SpecifyKind(locale - scostamento, DateTimeKind.Utc);
    }

    /// <summary>L'istante, azzerati i secondi (le timbrature si salvano al minuto, come nell'app originale).</summary>
    public static DateTime AlMinuto(DateTime utc) =>
        new(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
}

/// <summary>Un'assenza da inserire: giorno e ore.</summary>
public sealed record PianoAssenza(DateOnly Giorno, double Ore);

/// <summary>Esito della pianificazione delle assenze: o l'elenco dei giorni, o un errore da mostrare.</summary>
public sealed record EsitoPianoAssenze(IReadOnlyList<PianoAssenza> Giorni, string? Note, string? Errore)
{
    public bool Valido => Errore is null;
}

/// <summary>Patrono di una sede (festa locale).</summary>
public sealed record PatronoSede(string Sede, int Mese, int Giorno, string Descrizione);

/// <summary>Regole di inserimento di timbrature e assenze, portate dall'app di Vaccarini (app.py).</summary>
public static class RegolePresenze
{
    public const int MaxLuogo = 120;
    public const int MaxNote = 200;
    /// <summary>Un'assenza copre al massimo due mesi (62 giorni di distanza tra primo e ultimo giorno).</summary>
    public const int MaxGiorniAssenza = 62;

    /// <summary>Patroni delle sedi. Da confermare con il consulente del lavoro come riposo retribuito.</summary>
    public static IReadOnlyList<PatronoSede> Patroni { get; } = new List<PatronoSede>
    {
        new("Cattolica", 4, 30, "San Pio V (patrono di Cattolica)"),
        new("Pesaro", 9, 24, "San Terenzio (patrono di Pesaro)"),
    };

    /// <summary>Il patrono della sede nell'anno, se la sede ne ha uno.</summary>
    public static FestivitaNazionale? Patrono(string nomeSede, int anno)
    {
        var p = Patroni.FirstOrDefault(x => string.Equals(x.Sede, nomeSede, StringComparison.OrdinalIgnoreCase));
        return p is null ? null : new FestivitaNazionale(new DateOnly(anno, p.Mese, p.Giorno), p.Descrizione);
    }

    /// <summary>Testo ripulito e tagliato alla lunghezza massima; vuoto = nulla.</summary>
    public static string? NormalizzaTesto(string? testo, int massimo)
    {
        var t = (testo ?? "").Trim();
        if (t.Length == 0) return null;
        return t.Length > massimo ? t[..massimo] : t;
    }

    public static string? ValidaTipo(string? tipo) =>
        tipo is "E" or "U" ? null : "Tipo di timbratura non valido.";

    public static string? ValidaMotivo(string? motivo) =>
        string.IsNullOrWhiteSpace(motivo) ? "Indica il motivo della correzione." : null;

    public static string? ValidaOra(string? ora, out string oraValida) =>
        ParsingPresenze.TryValidaOra(ora, out oraValida) ? null : "Orario non valido (usa HH:MM).";

    /// <summary>
    /// Pianifica le assenze: controlla i dati e ricava i giorni. Con «giornata intera» o più giorni si saltano festivi,
    /// chiusure e giorni senza ore da contratto; l'assenza a ore di un solo giorno vale anche in un giorno senza contratto.
    /// </summary>
    public static EsitoPianoAssenze PianificaAssenze(
        LavoratorePresenze lavoratore, string? causale, string? note, DateOnly dal, DateOnly al,
        bool intera, string? oreTesto, IReadOnlyDictionary<DateOnly, VoceCalendario> calendario)
    {
        static EsitoPianoAssenze Errore(string messaggio) => new(Array.Empty<PianoAssenza>(), null, messaggio);

        if (!CausaliPresenze.Esiste(causale)) return Errore("Causale non valida.");
        var nota = NormalizzaTesto(note, MaxNote);
        if (causale == CausaliPresenze.Altro && nota is null)
            return Errore("Per la causale 'Altro' descrivi il motivo nelle note.");
        if (al < dal || al.DayNumber - dal.DayNumber > MaxGiorniAssenza)
            return Errore("Intervallo di date non valido (massimo 2 mesi).");

        double? oreFisse = null;
        if (!intera)
        {
            try { oreFisse = ParsingPresenze.ParseOre(oreTesto); }
            catch (Exception e) when (e is FormatException or OverflowException) { oreFisse = -1; }
            if (oreFisse is null || oreFisse <= 0 || oreFisse > 24)
                return Errore("Indica le ore (es. 2 oppure 2:30).");
        }

        var giorni = intera || dal != al
            ? MotoreCalcoloPresenze.GiorniLavorativi(lavoratore, dal, al, calendario).ToList()
            : new List<DateOnly> { dal };
        if (giorni.Count == 0) return Errore("Nessun giorno lavorativo nel periodo indicato.");

        var piano = giorni
            .Select(d => new PianoAssenza(d, intera ? MotoreCalcoloPresenze.OreContratto(lavoratore, d) : oreFisse!.Value))
            .ToList();
        return new EsitoPianoAssenze(piano, nota, null);
    }
}
