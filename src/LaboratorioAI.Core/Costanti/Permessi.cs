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
    /// <summary>Validare il campione (giudizio + chiusura analisi da riportare in ArcLab), 18/10/2026.</summary>
    public const string ValidazioneConferma      = "validazione.conferma";

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

    /// <summary>Gestione del controllo accreditamento ACCREDIA (conferma nuove revisioni elenco prove).</summary>
    public const string LaboratoriEsterniAccreditamento = "labesterni.accreditamento";
    /// <summary>06/11/2026: inviare ai laboratori esterni l'Excel dei campioni spediti e gestirne gli indirizzi e-mail.</summary>
    public const string LaboratoriEsterniInvio = "labesterni.invio";

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

    // ── Scadenziario tarature ──────────────────────────────────────
    public const string ScadenziarioVisualizza = "scadenziario.visualizza";
    public const string ScadenziarioGestione   = "scadenziario.gestione";

    // ── Accettazione campioni (fuori sede) ─────────────────────────
    public const string AccettazioneEsegui    = "accettazione.esegui";
    public const string AccettazioneTrasmetti  = "accettazione.trasmetti";

    // ── Ordine di Accettazione (documento generato da lotti ArcLab) ──
    public const string OrdiniAccettazioneGestione = "ordiniaccettazione.gestione";

    // ── Solo esterni (confronto certificati esterni/interni + fatturazione) ──
    public const string SoloEsterniVisualizza = "soloesterni.visualizza";

    // ── Certificati esterni (triage e rinomina per Palomar) ──
    public const string CertificatiEsterniGestione = "certificatiesterni.gestione";

    // ── Proposta controlli (consulenti/clienti → import supervisionato) ──
    public const string ControlliProponi = "controlli.proponi";
    public const string ControlliApprova = "controlli.approva";

    // ── Preventivi / offerte commerciali ───────────────────────────
    public const string PreventiviGestione = "preventivi.gestione";
    /// <summary>Approvazione dei preventivi che applicano uno sconto.</summary>
    public const string PreventiviApprova  = "preventivi.approva";
    /// <summary>05/11/2026: richieste di analisi dei clienti privati (modulo Mod. Com. 05B da firmare al banco).</summary>
    public const string RichiestePrivatiGestione = "richiesteprivati.gestione";

    // ── Preventivi / contratti di consulenza ───────────────────────
    /// <summary>Comporre i preventivi di consulenza e formazione; gestire attività e pacchetti di settore.</summary>
    public const string PreventiviConsulenza = "preventiviconsulenza.gestione";

    // ── Ricerca errori (diagnostica sola lettura su ArcLab) ─────────
    public const string RicercaErroriVisualizza = "ricercaerrori.visualizza";

    // ── Monitor Temperature (Econorma, integrato come Area) ─────────
    public const string MonitorTemperatureVisualizza = "monitortemperature.visualizza";
    /// <summary>03/11/2026: copiare i limiti di ArcLab nelle apparecchiature del monitoraggio (cambia gli allarmi).</summary>
    public const string MonitorTemperatureLimiti = "monitortemperature.limiti";

    // ── Statistiche (ricerca trasversale a livello di singola prova) ──
    public const string StatisticheVisualizza = "statistiche.visualizza";

    // ── Rapportini di consulenza ───────────────────────────────────
    /// <summary>Il consulente compila e invia i propri rapportini.</summary>
    public const string RapportiniCompila = "rapportini.compila";
    /// <summary>Gestione di tutti i rapportini: contestazione, correzione importi, conferma, annullamento.</summary>
    public const string RapportiniGestione = "rapportini.gestione";

    // ── Provvigioni commerciali ────────────────────────────────────
    /// <summary>Calcolo, elenco "in sviluppo", eccezioni. Dati retributivi: vista completa su tutti i commerciali.</summary>
    public const string ProvvigioniGestisci = "provvigioni.gestisci";

    /// <summary>Il commerciale vede solo i propri clienti e le proprie provvigioni.</summary>
    public const string ProvvigioniProprie  = "provvigioni.proprie";

    /// <summary>Chiusura del trimestre (congela i movimenti).</summary>
    public const string ProvvigioniChiudi   = "provvigioni.chiudi";

    /// <summary>Provvigioni e accordi in sola lettura (soci, 11/10/2026): vede tutto, non modifica nulla.</summary>
    public const string ProvvigioniConsulta = "provvigioni.consulta";

    /// <summary>Spettanze dei consulenti a percentuale: il consulente vede solo le proprie (19/10/2026).</summary>
    public const string SpettanzeProprie = "spettanze.proprie";

    // ── Clienti da seguire ─────────────────────────────────────────
    /// <summary>Andamento dei clienti (fatturato e campioni), registro azioni, proposta di assegnazione.</summary>
    public const string ClientiSeguire = "clienti.seguire";
    /// <summary>Soglie della pagina e approvazione delle assegnazioni dei clienti ai commerciali.</summary>
    public const string ClientiGestisci = "clienti.gestisci";
    /// <summary>Cambi di gestione (12/10/2026): vedere e confermare i subentri (stesso posto, nuovo cliente). Senza importi.</summary>
    public const string ClientiSubentri = "clienti.subentri";

    // 13/10/2026: cruscotto Direzione (soci e direzione di laboratorio): costi, margini, rendimento commerciali, territorio.
    public const string DirezioneConsulta = "direzione.consulta";
    public const string DirezioneGestisci = "direzione.gestisci";
    /// <summary>20/10/2026: consuntivo dei costi e dei ricavi per area (con gli stipendi voce per voce): solo i soci.</summary>
    public const string ConsuntiviConsulta = "consuntivi.consulta";
    /// <summary>10/11/2026: ricerca delle fatture di vendita AZW per testo della fattura (soci e amministrazione contabile).</summary>
    public const string RicercaFattureConsulta = "ricercafatture.consulta";
    /// <summary>21/10/2026: compensi dei soci e rimborsi spese (riservato ai soci; serve anche essere un socio attivo).</summary>
    public const string CompensiSociConsulta = "compensisoci.consulta";
    /// <summary>21/10/2026: modificare parametri, dati dei soci, premi area laboratorio, chiudere l'anno (solo il socio «gestore»).</summary>
    public const string CompensiSociGestisci = "compensisoci.gestisci";

    // ── Limiti normativi (17/10/2026, procedura limiti normativi Rev. 0) ──
    /// <summary>Consultare schede limite e fonti (sola lettura).</summary>
    public const string LimitiConsulta = "limiti.consulta";
    /// <summary>Gestire fonti, estrarre con l'AI, proporre, validare, correggere e respingere le schede (Mirko Maffei, Bertozzi, Qualità, Giulia Simoncelli). Non valida chi ha proposto o modificato la scheda.</summary>
    public const string LimitiValida = "limiti.valida";

    // ── Amministrazione ────────────────────────────────────────────
    // ── Condizioni Generali e firme (09/10/2026) ──────────────────
    /// <summary>Revisioni delle Condizioni Generali e clausole da approvare: caricare, modificare, pubblicare.</summary>
    public const string CondizioniGestione = "condizioni.gestione";
    /// <summary>Firmatari dei clienti e richieste di approvazione delle Condizioni (commerciali).</summary>
    public const string CondizioniFirmatari = "condizioni.firmatari";

    public const string AmministrazioneUtenti         = "amministrazione.utenti";
    public const string AmministrazioneConfigurazioni = "amministrazione.configurazioni";
    public const string AmministrazioneAudit          = "amministrazione.audit";
    /// <summary>Elenco delle domande fatte a «Chiedi alla guida», con voti e costi (11/10/2026).</summary>
    public const string GuideDomande                  = "guide.domande";

    // ── Manualistica (01/11/2026) ──────────────────────────────────
    /// <summary>Vedere compilazioni e documenti prodotti.</summary>
    public const string ManualisticaConsulta          = "manualistica.consulta";
    /// <summary>Compilare la check list, generare e inviare i documenti (ruolo Consulente).</summary>
    public const string ManualisticaGenera            = "manualistica.genera";
    /// <summary>Caricare le revisioni dei modelli documentali.</summary>
    public const string ManualisticaModelliGestisci   = "manualistica.modelli.gestisci";
    /// <summary>Approvare una revisione: servono due approvatori diversi, mai l'autore.</summary>
    public const string ManualisticaModelliApprova    = "manualistica.modelli.approva";
    /// <summary>Pannello di controllo: predefiniti, prezzi, voci delle tendine.</summary>
    public const string ManualisticaImpostazioni      = "manualistica.impostazioni";
    /// <summary>Indicare chi approva i modelli di ciascun tipo di manuale (03/11/2026).</summary>
    public const string ManualisticaApprovatori       = "manualistica.approvatori";

    /// <summary>Elenco completo, usato per popolare la tabella dei permessi.</summary>
    public static readonly IReadOnlyList<string> Tutti = new[]
    {
        ValidazioneVisualizza, ValidazioneAnalizza, ValidazioneAnalizzaLotto, ValidazioneConferma,
        ConsultazioneCampioni, ConsultazioneCertificati, ConsultazioneClienti, ConsultazioneRisultati,
        MetodiConsulta, MetodiPrezzi,
        CommesseConsulta, CommessePrezzi,
        LaboratoriEsterniConsulta, LaboratoriEsterniEsporta, LaboratoriEsterniAccreditamento, LaboratoriEsterniInvio,
        DvrGenera, DvrLotto, DvrRegistro,
        PiscineGenera, PiscineRegistro,
        PrefatturatoTamponi, PrefatturatoNonFatturare,
        NutrizionaleMateriePrime, NutrizionaleRicette,
        TaraturaEsegui, TaraturaRegistro, TaraturaValidazione,
        IncubatoriEsegui, IncubatoriValidazione,
        MappaturaEsegui, MappaturaValidazione,
        BilanceEsegui, BilanceValidazione,
        TempLogEsegui, TempLogMappatura, TempLogValidazione,
        ScadenziarioVisualizza, ScadenziarioGestione,
        AccettazioneEsegui, AccettazioneTrasmetti,
        OrdiniAccettazioneGestione,
        SoloEsterniVisualizza,
        CertificatiEsterniGestione,
        ControlliProponi, ControlliApprova,
        PreventiviGestione, PreventiviApprova, RichiestePrivatiGestione,
        PreventiviConsulenza,
        RicercaErroriVisualizza,
        MonitorTemperatureVisualizza, MonitorTemperatureLimiti,
        StatisticheVisualizza,
        ProvvigioniGestisci, ProvvigioniProprie, ProvvigioniChiudi, ProvvigioniConsulta, SpettanzeProprie,
        RapportiniCompila, RapportiniGestione,
        ClientiSeguire, ClientiGestisci, ClientiSubentri,
        DirezioneConsulta, DirezioneGestisci, ConsuntiviConsulta, RicercaFattureConsulta, CompensiSociConsulta, CompensiSociGestisci,
        LimitiConsulta, LimitiValida,
        CondizioniGestione, CondizioniFirmatari,
        AmministrazioneUtenti, AmministrazioneConfigurazioni, AmministrazioneAudit,
        GuideDomande,
        ManualisticaConsulta, ManualisticaGenera, ManualisticaModelliGestisci,
        ManualisticaModelliApprova, ManualisticaImpostazioni, ManualisticaApprovatori
    };
}

/// <summary>Codici dei ruoli predefiniti.</summary>
public static class Ruoli
{
    public const string Amministratore = "amministratore";
    public const string Responsabile   = "responsabile";
    public const string Operatore      = "operatore";
    public const string Consultazione  = "consultazione";

    // 11/10/2026: ruoli per mestiere (tabella «Ruoli e permessi di LaboratorioAI» approvata da Mirko Maffei).
    // «operatore» resta nel database ma disattivato.
    public const string Qualita              = "qualita";
    public const string ResponsabileQualita  = "responsabile-qualita";
    public const string Tecnico              = "tecnico";
    public const string Campionatore         = "campionatore";
    public const string Accettazione         = "accettazione";
    public const string Commerciale          = "commerciale";
    public const string Consulente           = "consulente";
    public const string Socio                = "socio";
    /// <summary>13/10/2026: soci e direzione di laboratorio, unici a vedere il cruscotto Direzione.</summary>
    public const string Direzione            = "direzione";
    /// <summary>21/10/2026: amministrazione contabile (Silvia Migani, Federica Volponi): pagine di Amministrazione in sola lettura.</summary>
    public const string AmministrazioneContabile = "amministrazione-contabile";
}
