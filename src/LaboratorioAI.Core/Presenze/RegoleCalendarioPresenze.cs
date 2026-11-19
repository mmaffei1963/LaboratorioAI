#nullable enable
using System;
using System.Collections.Generic;

namespace LaboratorioAI.Core.Presenze;

/// <summary>Esito del controllo di una richiesta sul calendario aziendale: giorni da inserire, ore (solo chiusure) o errore da mostrare.</summary>
public sealed record EsitoCalendario(IReadOnlyList<DateOnly> Giorni, double? Ore, string? Errore)
{
    public bool Valido => Errore is null;
}

/// <summary>Regole del calendario aziendale (festività e chiusure), come nell'app di Vaccarini.</summary>
public static class RegoleCalendarioPresenze
{
    public const int MaxDescrizione = 80;
    public const int MaxGiorni = 62;

    /// <summary>
    /// Controlla la richiesta. Per le festività si inseriscono tutti i giorni; per le chiusure di più giorni si saltano sabato e domenica
    /// (una chiusura di un solo giorno vale anche nel fine settimana). Le ore valgono solo per le chiusure (vuoto = tutta la giornata).
    /// </summary>
    public static EsitoCalendario Pianifica(string? tipo, DateOnly dal, DateOnly al, string? oreTesto)
    {
        static EsitoCalendario Errore(string m) => new(Array.Empty<DateOnly>(), null, m);

        if (tipo is not (VoceCalendario.Festivita or VoceCalendario.Chiusura)) return Errore("Tipo non valido.");
        if (al < dal || al.DayNumber - dal.DayNumber > MaxGiorni) return Errore("Intervallo di date non valido (massimo 2 mesi).");

        double? ore = null;
        if (tipo == VoceCalendario.Chiusura && !string.IsNullOrWhiteSpace(oreTesto))
        {
            try { ore = ParsingPresenze.ParseOre(oreTesto); }
            catch (Exception e) when (e is FormatException or OverflowException) { ore = -1; }
            if (ore is null || ore <= 0 || ore > 24) return Errore("Ore non valide (es. 4 oppure 4:30).");
        }

        var giorni = new List<DateOnly>();
        for (var d = dal; d <= al; d = d.AddDays(1))
            if (tipo == VoceCalendario.Festivita || dal == al || d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                giorni.Add(d);
        if (giorni.Count == 0) return Errore("Nessun giorno feriale nel periodo indicato.");
        return new EsitoCalendario(giorni, ore, null);
    }

    /// <summary>Descrizione scelta dall'utente (al massimo 80 caratteri) oppure quella predefinita del tipo.</summary>
    public static string Descrizione(string tipo, string? testo)
    {
        var pulita = string.Join(' ', (testo ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (pulita.Length > MaxDescrizione) pulita = pulita.Substring(0, MaxDescrizione);
        if (pulita.Length > 0) return pulita;
        return tipo == VoceCalendario.Chiusura ? "Chiusura aziendale" : "Festività";
    }
}
