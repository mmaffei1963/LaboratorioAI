using System.Globalization;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti.Dvr;

/// <summary>
/// Corrispondenza fra i dati della scheda e i controlli del modello.
///
/// Le chiavi sono normalizzate con la stessa funzione applicata ai tag
/// del documento: <c>Ragione Sociale</c>, <c>ragione_sociale</c> e
/// <c>RAGIONE SOCIALE</c> conducono quindi allo stesso valore.
///
/// Aggiungere un campo al questionario significa aggiungere una voce qui
/// e un controllo con il tag corrispondente nel modello.
/// </summary>
internal static class MappaValori
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("it-IT");

    public static Dictionary<string, string> Componi(SchedaDvr s)
    {
        var mappa = new Dictionary<string, string>(StringComparer.Ordinal);

        void Aggiungi(string tag, string? valore) =>
            mappa[NormalizzazioneTesto.Chiave(tag)] = valore ?? "";

        // ── Identificazione ────────────────────────────────────────
        Aggiungi("ragione_sociale",        s.RagioneSociale);
        Aggiungi("insediamento",           s.NomeInsediamento);
        Aggiungi("indirizzo",              s.Indirizzo);
        Aggiungi("comune",                 s.Comune);
        Aggiungi("provincia",              s.Provincia);
        Aggiungi("cap",                    s.Cap);
        Aggiungi("partita_iva",            s.PartitaIva);
        Aggiungi("indirizzo_completo",     IndirizzoCompleto(s));
        Aggiungi("regione",                DescrizioneRegione(s.Regione));

        // ── Riferimenti ────────────────────────────────────────────
        Aggiungi("legale_rappresentante",  s.LegaleRappresentante);
        Aggiungi("responsabile_struttura", s.ResponsabileStruttura);
        Aggiungi("data_sopralluogo",       s.DataSopralluogo);
        Aggiungi("data_documento",         DateTime.Now.ToString("dd/MM/yyyy", Cultura));
        Aggiungi("anno_documento",         DateTime.Now.Year.ToString());

        // ── Struttura ──────────────────────────────────────────────
        Aggiungi("tipologia_struttura",    s.TipologiaStruttura);
        Aggiungi("numero_camere",          s.NumeroCamere?.ToString());
        Aggiungi("numero_piani",           s.NumeroPianiFuoriTerra?.ToString());
        Aggiungi("anno_costruzione",       s.AnnoCostruzione?.ToString());
        Aggiungi("anno_ristrutturazione",  s.AnnoUltimaRistrutturazione?.ToString());
        Aggiungi("periodo_apertura",       s.PeriodoApertura);
        Aggiungi("esercizio_stagionale",   SiNo(s.Stagionale));

        // ── Impianto idrico ────────────────────────────────────────
        Aggiungi("produzione_acqua_calda", DescrizioneProduzione(s.ProduzioneAcqua));
        Aggiungi("serbatoi_acqua_fredda",  SiNo(s.SerbatoiAccumuloAcquaFredda));
        Aggiungi("ricircolo_acqua_calda",  SiNo(s.RicircoloAcquaCalda));
        Aggiungi("temperatura_mandata",    Temperatura(s.TemperaturaMandata));
        Aggiungi("temperatura_ritorno",    Temperatura(s.TemperaturaRitorno));
        Aggiungi("trattamenti_acqua",      s.TrattamentiAcqua);

        // ── Elementi di rischio ────────────────────────────────────
        Aggiungi("torri_evaporative",      SiNo(s.TorriEvaporative));
        Aggiungi("fontane_decorative",     SiNo(s.FontaneDecorative));
        Aggiungi("piscine_idromassaggio",  SiNo(s.PiscineOIdromassaggio));
        Aggiungi("umidificatori",          SiNo(s.UmidificatoriAria));
        Aggiungi("impianti_antincendio",   SiNo(s.ImpiantiAntincendioAdUmido));
        Aggiungi("altri_elementi_rischio", s.AltriElementiRischio);
        Aggiungi("elenco_elementi_rischio", ElencoElementiRischio(s));

        // ── Campionamento ──────────────────────────────────────────
        Aggiungi("numero_punti_prelievo",  s.PuntiPrelievo.Count.ToString());
        Aggiungi("elenco_punti_prelievo",  ElencoPunti(s.PuntiPrelievo));

        // ── Note ───────────────────────────────────────────────────
        Aggiungi("note_sopralluogo",       s.NoteSopralluogo);
        Aggiungi("misure_gia_adottate",    s.MisureGiaAdottate);

        return mappa;
    }

    // ── Formattazioni ──────────────────────────────────────────────

    private static string SiNo(bool valore) => valore ? "SÌ" : "NO";

    private static string Temperatura(decimal? valore) =>
        valore is null ? "non rilevata" : $"{valore.Value.ToString("0.#", Cultura)} °C";

    private static string DescrizioneRegione(RegioneDvr regione) => regione switch
    {
        RegioneDvr.EmiliaRomagna => "Emilia-Romagna",
        RegioneDvr.Marche        => "Marche",
        _                        => ""
    };

    private static string DescrizioneProduzione(ProduzioneAcquaCalda produzione) => produzione switch
    {
        ProduzioneAcquaCalda.Istantanea            => "Produzione istantanea",
        ProduzioneAcquaCalda.AccumuloCentralizzato => "Accumulo centralizzato",
        ProduzioneAcquaCalda.AccumuloLocale        => "Accumulo locale",
        ProduzioneAcquaCalda.Misto                 => "Sistema misto",
        _                                          => "Non specificata"
    };

    private static string IndirizzoCompleto(SchedaDvr s)
    {
        var localita = string.Join(' ', new[] { s.Cap, s.Comune }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var provincia = string.IsNullOrWhiteSpace(s.Provincia) ? "" : $"({s.Provincia})";

        return string.Join(" — ", new[] { s.Indirizzo, $"{localita} {provincia}".Trim() }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    /// <summary>Elenco discorsivo degli elementi che concorrono al rischio.</summary>
    private static string ElencoElementiRischio(SchedaDvr s)
    {
        var elementi = new List<string>();

        if (s.TorriEvaporative)           elementi.Add("torri evaporative");
        if (s.FontaneDecorative)          elementi.Add("fontane decorative");
        if (s.PiscineOIdromassaggio)      elementi.Add("piscine o vasche idromassaggio");
        if (s.UmidificatoriAria)          elementi.Add("umidificatori dell'aria");
        if (s.ImpiantiAntincendioAdUmido) elementi.Add("impianti antincendio ad umido");

        if (!string.IsNullOrWhiteSpace(s.AltriElementiRischio))
            elementi.Add(s.AltriElementiRischio.Trim());

        if (elementi.Count == 0)
            return "Non sono presenti elementi impiantistici che aumentino "
                 + "significativamente il rischio di proliferazione.";

        return "Sono presenti i seguenti elementi: " + string.Join(", ", elementi) + ".";
    }

    private static string ElencoPunti(IReadOnlyList<PuntoPrelievoDvr> punti)
    {
        if (punti.Count == 0) return "Piano di campionamento da definire.";

        return string.Join("\n", punti.Select((p, i) =>
        {
            var dettagli = string.Join(", ", new[] { p.Ubicazione, p.Tipologia }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return dettagli.Length > 0
                ? $"{i + 1}. {p.Descrizione} ({dettagli})"
                : $"{i + 1}. {p.Descrizione}";
        }));
    }
}
