namespace LaboratorioAI.Core.Costanti;

/// <summary>
/// Codici dei permessi applicativi.
///
/// I permessi sono granulari per funzione, non per ruolo: introdurre una
/// differenziazione futura significherà togliere un permesso a un ruolo,
/// non modificare il codice dei controlli.
///
/// Nella prima release tutti gli operatori appartengono al ruolo
/// Amministratore e dispongono quindi di tutti i permessi.
/// </summary>
public static class Permessi
{
    // ── Validazione ────────────────────────────────────────────────
    public const string ValidazioneVisualizza    = "validazione.visualizza";
    public const string ValidazioneAnalizza      = "validazione.analizza";
    public const string ValidazioneAnalizzaLotto = "validazione.analizza_lotto";

    // ── Consultazione ──────────────────────────────────────────────
    public const string ConsultazioneCampioni    = "consultazione.campioni";
    public const string ConsultazioneCertificati = "consultazione.certificati";
    public const string ConsultazioneClienti     = "consultazione.clienti";
    public const string ConsultazioneRisultati   = "consultazione.risultati";

    // ── Metodi e controlli ─────────────────────────────────────────
    public const string MetodiConsulta = "metodi.consulta";

    /// <summary>Visibilità dei prezzi sui controlli. Informazione commerciale sensibile.</summary>
    public const string MetodiPrezzi = "metodi.prezzi";

    // ── Commesse ───────────────────────────────────────────────────
    public const string CommesseConsulta = "commesse.consulta";

    /// <summary>Visibilità degli importi di commessa. Informazione commerciale sensibile.</summary>
    public const string CommessePrezzi = "commesse.prezzi";

    // ── Laboratori esterni ─────────────────────────────────────────
    public const string LaboratoriEsterniConsulta = "labesterni.consulta";
    public const string LaboratoriEsterniEsporta  = "labesterni.esporta";

    // ── DVR Legionella ─────────────────────────────────────────────
    public const string DvrGenera   = "dvr.genera";
    public const string DvrLotto    = "dvr.lotto";
    public const string DvrRegistro = "dvr.registro";

    // ── DVR Piscine ────────────────────────────────────────────────
    public const string PiscineGenera   = "piscine.genera";
    public const string PiscineRegistro = "piscine.registro";

    // ── Prefatturato: tamponi in contratto ─────────────────────────
    public const string PrefatturatoTamponi = "prefatturato.tamponi";

    // ── Prefatturato: campioni non-fatturare ───────────────────────
    public const string PrefatturatoNonFatturare = "prefatturato.nonfatturare";

    // ── Nutrizionale (report ed etichette) ─────────────────────────
    public const string NutrizionaleMateriePrime = "nutrizionale.materieprime";
    public const string NutrizionaleRicette      = "nutrizionale.ricette";

    // ── Taratura termometri ────────────────────────────────────────
    public const string TaraturaEsegui     = "taratura.esegui";
    public const string TaraturaRegistro   = "taratura.registro";
    public const string TaraturaValidazione = "taratura.validazione";

    // ── Taratura incubatori/celle ──────────────────────────────────
    public const string IncubatoriEsegui     = "incubatori.esegui";
    public const string IncubatoriValidazione = "incubatori.validazione";

    // ── Mappatura apparecchiature termostatiche ────────────────────
    public const string MappaturaEsegui     = "mappatura.esegui";
    public const string MappaturaValidazione = "mappatura.validazione";

    // ── Taratura bilance ───────────────────────────────────────────
    public const string BilanceEsegui     = "bilance.esegui";
    public const string BilanceValidazione = "bilance.validazione";

    // ── Monitoraggio TempLog Econorma ──────────────────────────────
    public const string TempLogEsegui   = "templog.esegui";
    public const string TempLogMappatura = "templog.mappatura";
    public const string TempLogValidazione = "templog.validazione";

    // ── Amministrazione ────────────────────────────────────────────
    public const string AmministrazioneUtenti         = "amministrazione.utenti";
    public const string AmministrazioneConfigurazioni = "amministrazione.configurazioni";
    public const string AmministrazioneAudit          = "amministrazione.audit";

    /// <summary>Elenco completo, usato per popolare la tabella dei permessi.</summary>
    public static readonly IReadOnlyList<string> Tutti = new[]
    {
        ValidazioneVisualizza, ValidazioneAnalizza, ValidazioneAnalizzaLotto,
        ConsultazioneCampioni, ConsultazioneCertificati, ConsultazioneClienti, ConsultazioneRisultati,
        MetodiConsulta, MetodiPrezzi,
        CommesseConsulta, CommessePrezzi,
        LaboratoriEsterniConsulta, LaboratoriEsterniEsporta,
        DvrGenera, DvrLotto, DvrRegistro,
        PiscineGenera, PiscineRegistro,
        PrefatturatoTamponi, PrefatturatoNonFatturare,
        NutrizionaleMateriePrime, NutrizionaleRicette,
        TaraturaEsegui, TaraturaRegistro, TaraturaValidazione,
        IncubatoriEsegui, IncubatoriValidazione,
        MappaturaEsegui, MappaturaValidazione,
        BilanceEsegui, BilanceValidazione,
        TempLogEsegui, TempLogMappatura, TempLogValidazione,
        AmministrazioneUtenti, AmministrazioneConfigurazioni, AmministrazioneAudit
    };
}

/// <summary>Codici dei ruoli predefiniti.</summary>
public static class Ruoli
{
    public const string Amministratore = "amministratore";
    public const string Responsabile   = "responsabile";
    public const string Operatore      = "operatore";
    public const string Consultazione  = "consultazione";
}
