using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;

/// <summary>Una voce della check list che non viene dai modelli, con le sue opzioni.</summary>
/// <param name="Codice">Come la chiama il programma, e come la ritrova il generatore.</param>
/// <param name="Etichetta">Come la legge il consulente.</param>
/// <param name="Tipo">Tendina, si'/no, numero, scelta multipla.</param>
/// <param name="Passo">In quale schermata compare.</param>
/// <param name="Predefinito">Valore proposto all'apertura; null = nessuno.</param>
/// <param name="Opzioni">Valore interno e testo mostrato. Vuoto per numeri e si'/no.</param>
public sealed record VoceGovernante(
    string Codice,
    string Etichetta,
    TipoCampoChecklist Tipo,
    string Passo,
    string? Predefinito,
    params (string Valore, string Testo)[] Opzioni);

/// <summary>
/// Le domande che governano i documenti (10/11/2026).
///
/// Non stanno nei modelli, perche' non scrivono niente: le legge il
/// motore per decidere quali documenti escono e quali parti restano.
/// Sono le piu' importanti della check list — da loro dipende perfino
/// quanti documenti il pacchetto contiene — ed e' per questo che il
/// programma non le puo' ricavare leggendo i modelli come fa per le
/// altre: nei modelli non c'e' niente da leggere.
///
/// I valori non sono scelti: sono quelli che <c>contesto()</c> confronta
/// in motore.js. Cambiarne uno qui senza cambiarlo li' rompe la
/// generazione in silenzio — il blocco resta o sparisce dal documento e
/// nessuno se ne accorge.
///
/// Una sola eccezione: la quarta modalita' della videosorveglianza, la
/// sola visione. Il motore la riconosce per esclusione e non la nomina
/// mai, quindi il nome SOLO_VISIONE l'abbiamo scelto noi; da concordare
/// con Vaccarini perche' i due programmi dicano la stessa parola.
///
/// Il sostituto del legale rappresentante non e' qui: non e' una
/// domanda con una tendina, e' una scelta fra gli autorizzati gia'
/// inseriti, e sta nella schermata del passo 3.
/// </summary>
public static class VociGovernanti
{
    public static readonly IReadOnlyList<VoceGovernante> Privacy =
    [
        // ── 1. Cliente, sedi, data e revisione ─────────────────────
        new("CONTRATTO_FORNITURA", "Il cliente richiede il contratto di fornitura servizi",
            TipoCampoChecklist.SiNo, "CLIENTE", "NO"),
        new("ASSISTENZA_ANNUALE", "Assistenza annuale compresa nel contratto",
            TipoCampoChecklist.SiNo, "CLIENTE", "SI"),

        // ── 2. Struttura e finalita' ───────────────────────────────
        new("VIDEO", "Videosorveglianza", TipoCampoChecklist.Tendina, "STRUTTURA", "NESSUNA",
            ("NESSUNA", "nessuna"),
            ("VISIONE_REG", "visione in tempo reale e registrazione"),
            ("SOLO_VISIONE", "sola visione in tempo reale"),
            ("SOLO_REG", "sola registrazione")),

        new("PISCINA", "Telecamera piscina", TipoCampoChecklist.Tendina, "STRUTTURA", "NESSUNA",
            ("NESSUNA", "nessuna"),
            ("VISIONE_REG", "visione in tempo reale e registrazione"),
            ("SOLO_VISIONE", "sola visione in tempo reale")),

        new("VIDEO_ARCHIVIAZIONE", "Dove si conservano le registrazioni",
            TipoCampoChecklist.Tendina, "STRUTTURA", "DVR",
            ("DVR", "su Digital Video Recorder (DVR) presso la struttura"),
            ("CLOUD", "sul cloud del fornitore dell'impianto")),

        new("DURATA_CONSERVAZIONE_IMMAGINI", "Per quante ore si conservano le immagini",
            TipoCampoChecklist.Numero, "STRUTTURA", "24"),

        new("SALA", "Servizio di sala", TipoCampoChecklist.Tendina, "STRUTTURA", "NESSUNO",
            ("CAMERIERI_SALA", "camerieri di sala"),
            ("CAPO_SALA", "capo sala"),
            ("NESSUNO", "nessuno")),

        new("PIANI", "Servizi ai piani", TipoCampoChecklist.Tendina, "STRUTTURA", "INTERNE_NOMINATE",
            ("INTERNE_NOMINATE", "cameriere interne, autorizzate al trattamento"),
            ("INTERNE_NON_NOMINATE", "cameriere interne, non autorizzate"),
            ("DITTA_ESTERNA", "ditta esterna")),

        new("GUARDIANO", "Guardiano notturno", TipoCampoChecklist.Tendina, "STRUTTURA", "NESSUNO",
            ("CON_SEGRETERIA", "presente, con mansioni di segreteria"),
            ("SENZA_SEGRETERIA", "presente, senza mansioni di segreteria"),
            ("NESSUNO", "nessuno")),

        new("CAPO_CHEF", "Capo chef", TipoCampoChecklist.SiNo, "STRUTTURA", "NO"),

        new("FINALITA_STATISTICHE", "Finalità: elaborazioni statistiche",
            TipoCampoChecklist.SiNo, "STRUTTURA", "NO"),
        new("GESTIONALE_CLIENTI", "Finalità: gestionale clienti",
            TipoCampoChecklist.SiNo, "STRUTTURA", "NO"),
        new("FINALITA_COMUNICAZIONE_PRESENZA", "Finalità: comunicazione della presenza a terzi",
            TipoCampoChecklist.SiNo, "STRUTTURA", "NO"),
        new("FINALITA_FOTO_VIDEO", "Finalità: foto e video della struttura",
            TipoCampoChecklist.SiNo, "STRUTTURA", "NO"),
        new("FINALITA_MARKETING", "Finalità: marketing e newsletter",
            TipoCampoChecklist.SiNo, "STRUTTURA", "NO"),

        new("MANUTENTORI", "Quanti manutentori interni (0 = nessuno)",
            TipoCampoChecklist.Numero, "STRUTTURA", "0"),
        new("IMPEGNO_DITTA_PIANI", "Quante lettere di impegno alla ditta dei piani",
            TipoCampoChecklist.Numero, "STRUTTURA", "0"),

        // ── 3. Autorizzati ─────────────────────────────────────────
        new("FORMAZIONE_DURATA_ORE", "Ore di formazione degli autorizzati",
            TipoCampoChecklist.Numero, "AUTORIZZATI", "2"),

        // ── 5. Sistemi, archivi e misure ───────────────────────────
        new("CRM_PRESENTE", "Il cliente usa gestionali per i clienti",
            TipoCampoChecklist.SiNo, "SISTEMI", "NO"),
        new("WIFI_PRESENTE", "La struttura ha una rete wifi per gli ospiti",
            TipoCampoChecklist.SiNo, "SISTEMI", "NO"),

        new("BACKUP_TIPI", "Tipi di backup in uso", TipoCampoChecklist.SceltaMultipla, "SISTEMI", null,
            ("SERVER", "su server"),
            ("DISPOSITIVI", "su dispositivi esterni"),
            ("CLOUD", "su cloud")),

        new("TEMPO_RIPRISTINO_ORE", "Entro quante ore si ripristinano i dati",
            TipoCampoChecklist.Numero, "SISTEMI", "48")
    ];

    /// <summary>Le voci che governano un tipo di manuale; vuoto se non ne ha.</summary>
    public static IReadOnlyList<VoceGovernante> Per(string codiceTipo) =>
        codiceTipo == "PRIVACY" ? Privacy : [];

    /// <summary>
    /// I codici che non vanno letti dai modelli: li governa questa
    /// tabella. Tre di loro compaiono anche nei modelli come campi di
    /// testo — ore di conservazione, ore di formazione, ore di
    /// ripristino — e senza questo l'estrazione li ricreerebbe come
    /// testo libero, perdendo tipo e valore predefinito.
    /// </summary>
    public static IReadOnlySet<string> Codici(string codiceTipo) =>
        Per(codiceTipo).Select(v => v.Codice).ToHashSet(StringComparer.Ordinal);
}
