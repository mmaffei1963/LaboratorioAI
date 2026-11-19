#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LaboratorioAI.Core.Presenze;

/// <summary>
/// Regole di calcolo delle presenze MARE.A (portate 1:1 dall'app «Presenze» di Vaccarini, calcoli.py).
///
/// - Ogni timbratura è arrotondata al quarto d'ora più vicino (8:07 → 8:00, 8:08 → 8:15).
/// - Le ore lavorate sono la somma delle coppie Entrata → Uscita (la pausa pranzo si timbra).
/// - Il confronto avviene con le ore da contratto del lavoratore per giorno della settimana.
/// - saldo = ore lavorate + assenze + chiusura aziendale − ore da contratto;
///   saldo &gt; 0 → straordinario; saldo &lt; 0 → ore non lavorate (da giustificare),
///   ma mai per oggi e per i giorni futuri.
///
/// Funzioni pure: nessun database, nessun orologio (la data di «oggi» si passa dal chiamante).
/// </summary>
public static class MotoreCalcoloPresenze
{
    /// <summary>Arrotondamento delle timbrature, in minuti.</summary>
    public const int ArrotondamentoMinuti = 15;

    public const string Entrata = "E";
    public const string Uscita = "U";

    public static readonly string[] GiorniBrevi = { "LUN", "MAR", "MER", "GIO", "VEN", "SAB", "DOM" };
    public static readonly string[] GiorniLunghi = { "Lunedì", "Martedì", "Mercoledì", "Giovedì", "Venerdì", "Sabato", "Domenica" };
    public static readonly string[] Mesi =
    {
        "GENNAIO", "FEBBRAIO", "MARZO", "APRILE", "MAGGIO", "GIUGNO", "LUGLIO",
        "AGOSTO", "SETTEMBRE", "OTTOBRE", "NOVEMBRE", "DICEMBRE",
    };

    // ------------------------------------------------------------------ orari

    /// <summary>"08:07" → minuti dalla mezzanotte arrotondati al quarto d'ora più vicino (7 min giù, 8 min su).</summary>
    public static int Arrotonda(string hhmm)
    {
        var parti = hhmm.Split(':');
        if (parti.Length < 2)
            throw new FormatException($"Orario non valido: {hhmm}");
        var totale = int.Parse(parti[0], CultureInfo.InvariantCulture) * 60
                     + int.Parse(parti[1], CultureInfo.InvariantCulture);
        var q = ArrotondamentoMinuti;
        return (totale + q / 2) / q * q;
    }

    /// <summary>Minuti dalla mezzanotte → "HH:mm" (1440 → "24:00").</summary>
    public static string MinutiInHhmm(int minuti) =>
        $"{minuti / 60:00}:{minuti % 60:00}";

    /// <summary>6.5 → "6:30"; negativi con il segno ("-0:15"); null → "".</summary>
    public static string OreFmt(double? ore)
    {
        if (ore is null) return "";
        var segno = ore.Value < 0 ? "-" : "";
        var minuti = (long)Math.Round(Math.Abs(ore.Value) * 60, MidpointRounding.ToEven);
        return $"{segno}{minuti / 60}:{minuti % 60:00}";
    }

    // ------------------------------------------------------------------ festività

    /// <summary>Domenica di Pasqua (algoritmo gregoriano anonimo).</summary>
    public static DateOnly Pasqua(int anno)
    {
        var a = anno % 19;
        var b = anno / 100;
        var c = anno % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var mese = (h + l - 7 * m + 114) / 31;
        var giorno = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(anno, mese, giorno);
    }

    /// <summary>
    /// Festività nazionali italiane, in ordine di data. Il patrono di ogni sede va aggiunto a parte
    /// (Cattolica: San Pio V, 30 aprile; Pesaro: San Terenzio, 24 settembre).
    /// San Francesco (4 ottobre) è festa nazionale dal 2026 (legge 8 ottobre 2025, n. 151).
    /// </summary>
    public static IReadOnlyList<FestivitaNazionale> FestivitaNazionali(int anno)
    {
        var pasqua = Pasqua(anno);
        var elenco = new List<FestivitaNazionale>
        {
            new(new DateOnly(anno, 1, 1), "Capodanno"),
            new(new DateOnly(anno, 1, 6), "Epifania"),
            new(pasqua, "Pasqua"),
            new(pasqua.AddDays(1), "Lunedì dell'Angelo"),
            new(new DateOnly(anno, 4, 25), "Festa della Liberazione"),
            new(new DateOnly(anno, 5, 1), "Festa del Lavoro"),
            new(new DateOnly(anno, 6, 2), "Festa della Repubblica"),
            new(new DateOnly(anno, 8, 15), "Ferragosto"),
            new(new DateOnly(anno, 11, 1), "Ognissanti"),
            new(new DateOnly(anno, 12, 8), "Immacolata Concezione"),
            new(new DateOnly(anno, 12, 25), "Natale"),
            new(new DateOnly(anno, 12, 26), "Santo Stefano"),
        };
        if (anno >= 2026)
            elenco.Add(new FestivitaNazionale(new DateOnly(anno, 10, 4), "San Francesco d'Assisi"));
        return elenco
            .OrderBy(x => x.Giorno)
            .ThenBy(x => x.Descrizione, StringComparer.Ordinal)
            .ToList();
    }

    // ------------------------------------------------------------------ giorni

    /// <summary>Indice del giorno della settimana: 0 = lunedì … 6 = domenica.</summary>
    public static int IndiceGiornoSettimana(DateOnly giorno) =>
        ((int)giorno.DayOfWeek + 6) % 7;

    public static bool EWeekend(DateOnly giorno) =>
        giorno.DayOfWeek == DayOfWeek.Saturday || giorno.DayOfWeek == DayOfWeek.Sunday;

    /// <summary>Ore da contratto del lavoratore nel giorno della settimana di <paramref name="giorno"/>.</summary>
    public static double OreContratto(LavoratorePresenze lavoratore, DateOnly giorno) =>
        lavoratore.OreContrattoPerGiorno[IndiceGiornoSettimana(giorno)];

    public static IEnumerable<DateOnly> GiorniMese(int anno, int mese)
    {
        var d = new DateOnly(anno, mese, 1);
        while (d.Month == mese)
        {
            yield return d;
            d = d.AddDays(1);
        }
    }

    /// <summary>
    /// Giorni con ore da contratto &gt; 0, esclusi festivi e chiusure aziendali
    /// (per inserire assenze su più giorni).
    /// </summary>
    public static IEnumerable<DateOnly> GiorniLavorativi(
        LavoratorePresenze lavoratore, DateOnly dal, DateOnly al,
        IReadOnlyDictionary<DateOnly, VoceCalendario> calendario)
    {
        var d = dal;
        while (d <= al)
        {
            if (OreContratto(lavoratore, d) > 0 && !calendario.ContainsKey(d))
                yield return d;
            d = d.AddDays(1);
        }
    }

    // ------------------------------------------------------------------ calcolo

    /// <summary>
    /// Abbina le timbrature (già ordinate per orario) in coppie Entrata → Uscita.
    /// Entrate senza uscita, uscite senza entrata e uscite prima dell'entrata diventano anomalie.
    /// </summary>
    public static RisultatoCoppie AccoppiaTimbrature(IEnumerable<TimbraturaGiorno> timbrature)
    {
        var coppie = new List<CoppiaMinuti>();
        var anomalie = new List<string>();
        TimbraturaGiorno? entrata = null;
        foreach (var t in timbrature)
        {
            if (t.Tipo == Entrata)
            {
                if (entrata is not null)
                    anomalie.Add($"Entrata {t.Ora} senza uscita precedente");
                entrata = t;
            }
            else
            {
                if (entrata is null)
                {
                    anomalie.Add($"Uscita {t.Ora} senza entrata");
                    continue;
                }
                var e = Arrotonda(entrata.Ora);
                var u = Arrotonda(t.Ora);
                if (u < e)
                    anomalie.Add($"Uscita {t.Ora} precedente all'entrata {entrata.Ora}");
                else
                    coppie.Add(new CoppiaMinuti(e, u));
                entrata = null;
            }
        }
        return new RisultatoCoppie(coppie, anomalie, entrata is not null);
    }

    /// <summary>Calcolo di un giorno per un lavoratore.</summary>
    /// <param name="lavoratore">Lavoratore con le ore da contratto.</param>
    /// <param name="giorno">Giorno da calcolare.</param>
    /// <param name="timbrature">Timbrature non annullate del giorno (in qualsiasi ordine: si ordinano per orario).</param>
    /// <param name="assenze">Assenze non annullate del giorno.</param>
    /// <param name="calendario">Voce del calendario aziendale di quel giorno (per la sede del lavoratore), oppure null.</param>
    /// <param name="oggi">Data di oggi (Europe/Rome).</param>
    /// <param name="inizio">Data da cui l'app conta le ore (prima, nessuna ora da contratto).</param>
    public static CalcoloGiorno CalcolaGiorno(
        LavoratorePresenze lavoratore, DateOnly giorno,
        IReadOnlyList<TimbraturaGiorno> timbrature, IReadOnlyList<AssenzaGiorno> assenze,
        VoceCalendario? calendario, DateOnly oggi, DateOnly? inizio = null)
    {
        var festivo = calendario is not null && calendario.Tipo == VoceCalendario.Festivita;
        var contratto = festivo ? 0.0 : OreContratto(lavoratore, giorno);
        if (lavoratore.AttivoDal is DateOnly attivoDal && giorno < attivoDal)
            contratto = 0.0;
        // prima della data di inizio conteggi (avvio dell'app) non si calcolano ore mancanti
        var primaInizio = inizio is DateOnly avvio && giorno < avvio;
        if (primaInizio)
            contratto = 0.0;

        var ordinate = timbrature.OrderBy(t => t.Ora, StringComparer.Ordinal).ToList();
        var abbinate = AccoppiaTimbrature(ordinate);
        var anomalie = new List<string>(abbinate.Anomalie);
        var minutiLavorati = 0;
        foreach (var c in abbinate.Coppie)
            minutiLavorati += c.Uscita - c.Entrata;
        var lavorate = minutiLavorati / 60.0;

        var chiusura = 0.0;
        if (calendario is not null && calendario.Tipo == VoceCalendario.Chiusura && contratto > 0)
            chiusura = calendario.Ore is null ? contratto : Math.Min(calendario.Ore.Value, contratto);

        var oreAssenze = 0.0;
        foreach (var a in assenze)
            oreAssenze += a.Ore;

        var saldo = lavorate + oreAssenze + chiusura - contratto;

        var inCorso = giorno == oggi && abbinate.Aperta;
        var futuro = giorno > oggi;
        if (abbinate.Aperta && !inCorso)
            anomalie.Add("Manca la timbratura di uscita");

        var straordinario = Math.Max(saldo, 0.0);
        // oggi (giornata non ancora conclusa) e nei giorni futuri non si contano ore mancanti
        var nonLavorate = (futuro || giorno == oggi) ? 0.0 : Math.Max(-saldo, 0.0);

        return new CalcoloGiorno
        {
            Data = giorno,
            Contratto = contratto,
            Lavorate = lavorate,
            Coppie = abbinate.Coppie,
            Assenze = assenze,
            OreAssenze = oreAssenze,
            Chiusura = chiusura,
            Festivo = festivo,
            FestivoDescrizione = festivo ? (calendario!.Descrizione ?? "") : "",
            ChiusuraDescrizione = calendario is not null && calendario.Tipo == VoceCalendario.Chiusura
                ? (calendario.Descrizione ?? "")
                : "",
            Weekend = EWeekend(giorno),
            Straordinario = Arrotonda2(straordinario),
            NonLavorate = Arrotonda2(nonLavorate),
            Anomalie = anomalie,
            InCorso = inCorso,
            Futuro = futuro,
            PrimaInizio = primaInizio,
            Timbrature = timbrature,
        };
    }

    /// <summary>
    /// Calcoli giornalieri di un lavoratore nel periodo [dal, al].
    /// I dati arrivano già filtrati (niente timbrature o assenze annullate).
    /// </summary>
    public static IReadOnlyList<CalcoloGiorno> CalcolaPeriodo(
        LavoratorePresenze lavoratore, DateOnly dal, DateOnly al,
        ILookup<DateOnly, TimbraturaGiorno> timbrature,
        ILookup<DateOnly, AssenzaGiorno> assenze,
        IReadOnlyDictionary<DateOnly, VoceCalendario> calendario,
        DateOnly oggi, DateOnly? inizio)
    {
        var risultato = new List<CalcoloGiorno>();
        for (var d = dal; d <= al; d = d.AddDays(1))
        {
            calendario.TryGetValue(d, out var voce);
            risultato.Add(CalcolaGiorno(
                lavoratore, d, timbrature[d].ToList(), assenze[d].ToList(), voce, oggi, inizio));
        }
        return risultato;
    }

    /// <summary>Totali di un periodo. Le ore da contratto dei giorni futuri non si contano.</summary>
    public static TotaliPeriodo CalcolaTotali(IEnumerable<CalcoloGiorno> giorni)
    {
        double contratto = 0, lavorate = 0, straordinario = 0, nonLavorate = 0, chiusura = 0;
        var causali = new Dictionary<string, double>();
        var conAnomalie = 0;
        foreach (var g in giorni)
        {
            if (!g.Futuro)
                contratto += g.Contratto;
            lavorate += g.Lavorate;
            straordinario += g.Straordinario;
            nonLavorate += g.NonLavorate;
            chiusura += g.Chiusura;
            foreach (var a in g.Assenze)
                causali[a.Causale] = (causali.TryGetValue(a.Causale, out var prima) ? prima : 0.0) + a.Ore;
            if (g.Anomalie.Count > 0)
                conAnomalie++;
        }
        return new TotaliPeriodo
        {
            Contratto = contratto,
            Lavorate = lavorate,
            Straordinario = straordinario,
            NonLavorate = nonLavorate,
            Chiusura = chiusura,
            Causali = causali,
            GiorniConAnomalie = conAnomalie,
        };
    }

    // ------------------------------------------------------------------ utilità

    /// <summary>Arrotonda a 2 decimali (come round(x, 2) dell'originale).</summary>
    internal static double Arrotonda2(double x) =>
        Math.Round(x, 2, MidpointRounding.ToEven);
}
