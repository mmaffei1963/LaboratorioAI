namespace LaboratorioAI.Core.Entita;

// ═══════════════════════════════════════════════════════════════════
// Modulo Manualistica (schema «man», 01/11/2026)
//
// Genera la documentazione che il consulente consegna al cliente —
// privacy, e in seguito piscine, legionellosi, HACCP, sicurezza —
// partendo da una check list e da modelli Word marcati con controlli
// contenuto. Il modulo nasce generico: ogni tabella porta il tipo di
// manuale, così i tipi successivi non richiedono modifiche di schema.
//
// Riferimenti: docs\Privacy\MODELLO_DATI_Manualistica.md,
// docs\Privacy\INVENTARIO_Motore_Manualistica.md.
//
// Nessuna scrittura su ArcLab né su AZW: i codici ArcLab sono solo
// riferimenti logici, senza vincolo referenziale.
// ═══════════════════════════════════════════════════════════════════

// ── Enumerazioni ───────────────────────────────────────────────────

/// <summary>Stato di una revisione di un modello documentale.</summary>
public enum StatoRevisioneModello
{
    /// <summary>Caricata, non ancora mandata in approvazione.</summary>
    Bozza = 1,
    /// <summary>In attesa delle due approvazioni.</summary>
    InApprovazione = 2,
    /// <summary>Approvata: è la revisione da cui si generano i documenti.</summary>
    Approvato = 3,
    /// <summary>Superata da una revisione più recente.</summary>
    Obsoleto = 4
}

/// <summary>Esito dell'esame di una revisione da parte di un approvatore.</summary>
public enum EsitoApprovazioneModello
{
    Approvata = 1,
    Respinta = 2
}

/// <summary>Come si compila una voce della check list.</summary>
public enum TipoCampoChecklist
{
    Testo = 1,
    Data = 2,
    Tendina = 3,
    SiNo = 4,
    Numero = 5,
    SceltaMultipla = 6
}

/// <summary>Stato di una compilazione.</summary>
public enum StatoCompilazione
{
    /// <summary>In lavorazione, nessun documento prodotto.</summary>
    Bozza = 1,
    /// <summary>Documenti generati, non ancora inviati.</summary>
    Generata = 2,
    /// <summary>In attesa che il Legale Rappresentante firmi con il codice.</summary>
    InAttesaFirma = 3,
    /// <summary>Consegnata al cliente.</summary>
    Inviata = 4,
    /// <summary>Annullata (nessuna cancellazione fisica).</summary>
    Annullata = 5
}

/// <summary>Chi firma.</summary>
public enum RuoloFirmaManualistica
{
    Consulente = 1,
    LegaleRappresentante = 2
}

/// <summary>Come è stata apposta la firma.</summary>
public enum ModoFirmaManualistica
{
    /// <summary>Disegnata nel riquadro o apposta a mano sul cartaceo.</summary>
    Autografa = 1,
    /// <summary>Apposta dal cliente dopo conferma con codice ricevuto per e-mail.</summary>
    Codice = 2
}

/// <summary>Formato di un documento prodotto.</summary>
public enum FormatoManualeGenerato
{
    Docx = 1,
    Pdf = 2
}

/// <summary>Che cosa è stato spedito.</summary>
public enum TipoInvioManualistica
{
    /// <summary>Al cliente: solo PDF, eventualmente con ritardo.</summary>
    Cliente = 1,
    /// <summary>Al consulente: Word, subito.</summary>
    Consulente = 2,
    /// <summary>Alla segreteria, perché inserisca il cliente nei gestionali.</summary>
    NuovoCliente = 3,
    /// <summary>Promemoria quotidiano al cliente che non ha ancora firmato.</summary>
    PromemoriaCodice = 4,
    /// <summary>Avviso al consulente: i giorni di attesa della firma sono scaduti.</summary>
    AvvisoCodiceScaduto = 5
}

/// <summary>Stato di un invio. Sta nel database: deve sopravvivere a un riavvio.</summary>
public enum StatoInvioManualistica
{
    /// <summary>Programmato per un'ora futura.</summary>
    Programmato = 1,
    /// <summary>Fermo finché il Legale Rappresentante non firma.</summary>
    InAttesaFirma = 2,
    Inviato = 3,
    Annullato = 4,
    Errore = 5
}

/// <summary>Modalità di pagamento del contratto di fornitura servizi.</summary>
public enum ModalitaPagamentoContratto
{
    Bonifico = 1,
    RicevutaBancaria = 2,
    SceltoDalCliente = 3
}

/// <summary>Risposta a un consenso dell'informativa nel contratto.</summary>
public enum SceltaConsenso
{
    Si = 1,
    No = 2,
    /// <summary>Lasciato in bianco: lo sceglie il cliente firmando.</summary>
    SceglieIlCliente = 3
}

// ── Modelli controllati ────────────────────────────────────────────

/// <summary>Tipo di manuale: PRIVACY, PISCINE, LEGIONELLA, HACCP, SICUREZZA.</summary>
public class TipoManuale
{
    public int IdTipoManuale { get; set; }
    public string Codice { get; set; } = null!;
    public string Nome { get; set; } = null!;
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

/// <summary>
/// Un documento del pacchetto. Non contiene il file: contiene la sua
/// identità e la regola con cui entra nel pacchetto.
/// </summary>
public class ModelloDocumento
{
    public int IdModelloDocumento { get; set; }
    public int IdTipoManuale { get; set; }
    public TipoManuale? TipoManuale { get; set; }

    /// <summary>REGISTRO, NOMINE, RESP_WEB_AGENCY…: corrisponde al codice usato dal motore.</summary>
    public string Codice { get; set; } = null!;
    public string Nome { get; set; } = null!;

    /// <summary>Condizione di inclusione: null = sempre. Es. «VIDEO_O_PISCINA», «!CONTRATTO_FORNITURA».</summary>
    public string? CondizioneInclusione { get; set; }

    /// <summary>Macro attività a cui il modello si applica; null = tutte.</summary>
    public string? CodiceAttivita { get; set; }

    /// <summary>Regione a cui il modello si applica; null = tutte. Sigla di due lettere (ER, MA…).</summary>
    public string? SiglaRegione { get; set; }

    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;

    /// <summary>
    /// Se il modello deve contenere marcatori. Vero per i documenti che
    /// il motore compila; falso per i modelli di servizio, come la carta
    /// intestata del riepilogo, che sono impaginazione e basta.
    ///
    /// Serve a distinguere «questo file non ha marcatori perché non ne ha
    /// bisogno» da «questo file ha perso i controlli contenuto salvandolo»,
    /// che è un guasto grave e silenzioso (04/11/2026).
    /// </summary>
    public bool RichiedeMarcatori { get; set; } = true;

    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }

    public List<RevisioneModello> Revisioni { get; set; } = new();
}

/// <summary>
/// Una revisione del modello: qui vive il file .docx marcato.
/// Sta nel database e non in cartella perché si deve poter dire, a
/// distanza di anni, con quale versione è stato prodotto un documento.
/// </summary>
public class RevisioneModello
{
    public int IdRevisioneModello { get; set; }
    public int IdModelloDocumento { get; set; }
    public ModelloDocumento? ModelloDocumento { get; set; }

    public int NumeroRevisione { get; set; }
    public StatoRevisioneModello Stato { get; set; } = StatoRevisioneModello.Bozza;

    /// <summary>Il modello marcato.</summary>
    public byte[] ContenutoDocx { get; set; } = null!;
    public string ImprontaSha256 { get; set; } = null!;
    public string? NomeFileOriginale { get; set; }

    /// <summary>Da quando la revisione è efficace.</summary>
    public DateTime? DataEfficacia { get; set; }
    public string? NoteRevisione { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }

    public List<ApprovazioneRevisione> Approvazioni { get; set; } = new();
}

/// <summary>
/// Approvazione di una revisione: chi l'ha esaminata, com'è andata e
/// quando. Dal 02/11/2026 ne basta una. La stessa persona non può
/// esaminare due volte la stessa revisione (vincolo nel database).
/// </summary>
public class ApprovazioneRevisione
{
    public int IdApprovazioneRevisione { get; set; }
    public int IdRevisioneModello { get; set; }
    public RevisioneModello? RevisioneModello { get; set; }

    /// <summary>UPN dell'approvatore (Entra ID).</summary>
    public string Approvatore { get; set; } = null!;
    public EsitoApprovazioneModello Esito { get; set; }
    public DateTime DataUtc { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Chi può approvare i modelli di un tipo di manuale (03/11/2026).
///
/// La privacy, le piscine e la legionellosi sono materie diverse e le
/// approva chi le conosce: questa tabella dice, per ogni tipo, di chi è
/// la firma. Chi ha il permesso di approvare vede i pulsanti solo per i
/// tipi in cui è indicato; se per un tipo non c'è nessuno, i suoi
/// modelli non si approvano e la pagina lo dice.
///
/// L'elenco lo tiene un amministratore.
/// </summary>
public class ApprovatoreTipoManuale
{
    public int IdApprovatoreTipoManuale { get; set; }

    public int IdTipoManuale { get; set; }
    public TipoManuale? TipoManuale { get; set; }

    public int IdUtente { get; set; }
    public Utente? Utente { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

// ── Check list configurabile ───────────────────────────────────────

/// <summary>Una domanda della check list.</summary>
public class VoceChecklist
{
    public int IdVoceChecklist { get; set; }
    public int IdTipoManuale { get; set; }
    public TipoManuale? TipoManuale { get; set; }

    /// <summary>Corrisponde al tag del modello, senza prefisso.</summary>
    public string Codice { get; set; } = null!;
    public string Etichetta { get; set; } = null!;

    /// <summary>Il passo della check list in cui compare.</summary>
    public string? Gruppo { get; set; }

    public TipoCampoChecklist TipoCampo { get; set; }
    public bool Obbligatoria { get; set; }
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }

    public List<OpzioneVoce> Opzioni { get; set; } = new();
}

/// <summary>
/// Una voce di tendina. Le voci scritte a mano dal consulente possono
/// entrare qui per le compilazioni successive; una voce superata si
/// disattiva, non si cancella, perché i documenti già prodotti devono
/// restare spiegabili.
/// </summary>
public class OpzioneVoce
{
    public int IdOpzioneVoce { get; set; }
    public int IdVoceChecklist { get; set; }
    public VoceChecklist? VoceChecklist { get; set; }

    public string Valore { get; set; } = null!;
    public string? Etichetta { get; set; }

    /// <summary>
    /// Il modello a cui questa voce appartiene; null = vale per tutti.
    ///
    /// Serve perché alcune tendine cambiano da documento a documento, e
    /// per progetto: nella lettera alla web agency fra gli interessati
    /// c'è «PROSPECT», in quella all'Experience fra i trattamenti ci
    /// sono «Parchi tematici» e «Strutture termali». Unendole, il
    /// consulente che scrive una lettera si vedrebbe offrire i
    /// trattamenti di un'altra (verificato sui 32 modelli, 04/11/2026:
    /// otto campi si comportano così, tutti nelle lettere di nomina).
    /// </summary>
    public int? IdModelloDocumento { get; set; }
    public ModelloDocumento? ModelloDocumento { get; set; }

    /// <summary>Proposta all'apertura di una nuova compilazione.</summary>
    public bool Predefinita { get; set; }

    /// <summary>Nata da una voce scritta a mano in una compilazione.</summary>
    public bool DaCompilazione { get; set; }
    public int? IdCompilazioneOrigine { get; set; }

    public string? ApprovataDa { get; set; }
    public DateTime? ApprovataIlUtc { get; set; }

    public int Ordine { get; set; }

    /// <summary>false = obsoleta: sparisce dalle tendine, resta nei documenti prodotti.</summary>
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

/// <summary>
/// Funzione aziendale. L'ordine è quello con cui escono le lettere di
/// nomina: Amministrazione, Front Office, Capo chef, Guardiano notturno,
/// Cameriera ai piani, Cameriere di sala, Capo sala.
/// </summary>
public class FunzioneAziendale
{
    public int IdFunzioneAziendale { get; set; }
    public int IdTipoManuale { get; set; }
    public TipoManuale? TipoManuale { get; set; }

    public string Codice { get; set; } = null!;
    public string Nome { get; set; } = null!;

    /// <summary>Amministrazione e Front Office sono sempre presenti.</summary>
    public bool Facoltativa { get; set; } = true;

    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

/// <summary>
/// Qualifica dell'autorizzato. Solo Legale Rappresentante, Socio e
/// Coadiuvante familiare richiedono il nominativo: per i Lavoratori non
/// si chiede e non si salva, si scrive a mano sul documento stampato.
/// </summary>
public class QualificaAutorizzato
{
    public int IdQualifica { get; set; }
    public string Nome { get; set; } = null!;
    public bool RichiedeNominativo { get; set; }
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

// ── Il lavoro sul cliente ──────────────────────────────────────────

/// <summary>
/// Testata di una compilazione. I dati del cliente sono congelati al
/// momento della compilazione: il documento consegnato deve restare
/// leggibile anche se l'anagrafica cambia.
/// </summary>
public class Compilazione
{
    public int IdCompilazione { get; set; }
    public int IdTipoManuale { get; set; }
    public TipoManuale? TipoManuale { get; set; }

    // ── Titolare del trattamento (snapshot) ────────────────────────
    /// <summary>Riferimento logico ad ArcLab; null se il cliente è stato inserito a mano.</summary>
    public string? CodiceClienteArcLab { get; set; }
    public string RagioneSociale { get; set; } = null!;
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }

    public string? SedeLegaleIndirizzo { get; set; }
    public string? SedeLegaleCap { get; set; }
    public string? SedeLegaleComune { get; set; }
    public string? SedeLegaleProvincia { get; set; }

    public string? Email { get; set; }
    public string? Pec { get; set; }
    public string? Telefono { get; set; }

    public string? CodiceAteco { get; set; }
    public string? AttivitaEsercitata { get; set; }
    public string? SitiWeb { get; set; }

    // ── Fatturazione e referente ───────────────────────────────────
    public string? CodiceUnivocoFatturazione { get; set; }
    public string? ReferenteNome { get; set; }
    public string? ReferenteCognome { get; set; }
    public bool ReferenteComeLegaleRappresentante { get; set; } = true;

    /// <summary>Cliente non ancora nei gestionali: alla generazione parte l'avviso alla segreteria.</summary>
    public bool NuovoCliente { get; set; }

    // ── Documenti ──────────────────────────────────────────────────
    public DateTime DataDocumenti { get; set; }
    public int NumeroRevisioneDocumenti { get; set; }

    /// <summary>Tendine aperte al testo libero quando nessuna voce va bene.</summary>
    public bool CompilazioneManuale { get; set; }

    public string? EmailCliente { get; set; }
    public string? EmailConsulente { get; set; }

    /// <summary>Le note della check list vanno anche al cliente, in PDF.</summary>
    public bool NoteAlCliente { get; set; }

    public StatoCompilazione Stato { get; set; } = StatoCompilazione.Bozza;
    public string? MotivoAnnullamento { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
    public DateTime? ModificatoIlUtc { get; set; }
    public string? ModificatoDa { get; set; }

    public List<SedeCompilazione> Sedi { get; set; } = new();
    public List<ValoreCompilazione> Valori { get; set; } = new();
    public List<AutorizzatoCompilazione> Autorizzati { get; set; } = new();
    public List<ResponsabileCompilazione> Responsabili { get; set; } = new();
    public List<NotaCompilazione> Note { get; set; } = new();
    public List<FirmaCompilazione> Firme { get; set; } = new();
    public List<ManualeGenerato> Documenti { get; set; } = new();
}

/// <summary>Una sede del trattamento.</summary>
public class SedeCompilazione
{
    public int IdSedeCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    /// <summary>Riferimento logico all'insediamento ArcLab; null se inserita a mano.</summary>
    public string? CodiceInsediamentoArcLab { get; set; }

    /// <summary>Nome dell'attività: con più sedi precede l'indirizzo nelle intestazioni.</summary>
    public string? Nome { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Comune { get; set; }
    public string? Provincia { get; set; }
    public int Ordine { get; set; }
}

/// <summary>Una risposta della check list.</summary>
public class ValoreCompilazione
{
    public int IdValoreCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public string CodiceVoce { get; set; } = null!;
    public string? Valore { get; set; }

    /// <summary>Testo scritto dal consulente, non scelto da una tendina.</summary>
    public bool ScrittoAMano { get; set; }
}

/// <summary>
/// Un autorizzato o un gruppo di autorizzati identici. Ogni unità
/// produce una lettera di nomina. Nome e cognome solo per chi li
/// richiede: per i Lavoratori restano null.
/// </summary>
public class AutorizzatoCompilazione
{
    public int IdAutorizzatoCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public int IdQualifica { get; set; }
    public QualificaAutorizzato? Qualifica { get; set; }

    public string? Nome { get; set; }
    public string? Cognome { get; set; }

    /// <summary>Quanti autorizzati identici. Per chi richiede il nominativo è sempre 1.</summary>
    public int Quantita { get; set; } = 1;

    /// <summary>Accesso alle immagini della videosorveglianza, se diverso dal calcolato.</summary>
    public string? AccessoImmagini { get; set; }

    public int Ordine { get; set; }

    public List<AutorizzatoFunzione> Funzioni { get; set; } = new();
}

/// <summary>
/// Funzione svolta da un autorizzato. Chi ne ha più di una riceve una
/// sola lettera di nomina, con tutti gli ambiti.
/// </summary>
public class AutorizzatoFunzione
{
    public int IdAutorizzatoFunzione { get; set; }
    public int IdAutorizzatoCompilazione { get; set; }
    public AutorizzatoCompilazione? Autorizzato { get; set; }

    public int IdFunzioneAziendale { get; set; }
    public FunzioneAziendale? Funzione { get; set; }
}

/// <summary>
/// Una lettera di nomina a Responsabile esterno. Più righe dello stesso
/// modello = più lettere (per esempio due web agency). I dati del
/// Responsabile non si salvano: li compila a mano il Responsabile.
/// </summary>
public class ResponsabileCompilazione
{
    public int IdResponsabileCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    /// <summary>Codice del modello: RESP_WEB_AGENCY, RESP_PAGHE…</summary>
    public string CodiceModello { get; set; } = null!;
    public int Ordine { get; set; }

    public List<SceltaResponsabile> Scelte { get; set; } = new();
}

/// <summary>Una scelta fatta dentro una lettera di nomina a Responsabile.</summary>
public class SceltaResponsabile
{
    public int IdSceltaResponsabile { get; set; }
    public int IdResponsabileCompilazione { get; set; }
    public ResponsabileCompilazione? Responsabile { get; set; }

    /// <summary>Codice del campo nella lettera, con l'indice di posizione: RESP_TRATTAMENTO_1…</summary>
    public string CodiceCampo { get; set; } = null!;
    public string? Valore { get; set; }
}

/// <summary>
/// Nota libera di una sezione della check list. Finisce in un documento
/// a parte, sempre al consulente e al cliente solo se richiesto.
/// Può contenere dati personali o valutazioni interne.
/// </summary>
public class NotaCompilazione
{
    public int IdNotaCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    /// <summary>Riferimento «passo › sezione».</summary>
    public string Sezione { get; set; } = null!;
    public string Testo { get; set; } = null!;

    public bool Attivo { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

/// <summary>
/// Contratto di fornitura servizi. Tabella a parte perché facoltativo e
/// perché contiene l'IBAN del cliente, da proteggere come il resto dei
/// dati della compilazione.
/// </summary>
public class ContrattoFornitura
{
    public int IdContrattoFornitura { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public decimal PrezzoDocumentazione { get; set; } = 300m;
    public decimal PrezzoAssistenza { get; set; } = 200m;
    public bool AssistenzaAnnuale { get; set; } = true;

    public ModalitaPagamentoContratto Pagamento { get; set; } = ModalitaPagamentoContratto.SceltoDalCliente;
    public string? RibaIstituto { get; set; }
    public string? RibaAgenzia { get; set; }
    /// <summary>ABI e CAB si ricavano dall'IBAN, non si salvano.</summary>
    public string? RibaIban { get; set; }

    public string? Sdi { get; set; }
    public string? Telefono { get; set; }
    public string? EmailDocumentazione { get; set; }

    public SceltaConsenso ConsensoProfilazione { get; set; } = SceltaConsenso.SceglieIlCliente;
    public SceltaConsenso ConsensoPresenza { get; set; } = SceltaConsenso.SceglieIlCliente;
    public SceltaConsenso ConsensoFotoVideo { get; set; } = SceltaConsenso.SceglieIlCliente;
    public SceltaConsenso ConsensoPromozionali { get; set; } = SceltaConsenso.SceglieIlCliente;

    /// <summary>Riferimento e firmatario per MARE.A: congelati, devono coincidere con la firma apposta.</summary>
    public string? ConsulenteNome { get; set; }
    public string? ConsulenteCellulare { get; set; }
    public string? ConsulenteEmail { get; set; }

    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

/// <summary>Intestatario del contratto, quando è diverso dal titolare della documentazione.</summary>
public class IntestatarioContratto
{
    public int IdIntestatarioContratto { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public string? PartitaIva { get; set; }
    public string RagioneSociale { get; set; } = null!;
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Comune { get; set; }
    public string? Provincia { get; set; }
    public string? CodiceFiscale { get; set; }

    /// <summary>Riferimento logico alla copia AZW; null se inserito a mano.</summary>
    public string? CodiceClienteAzw { get; set; }
}

// ── Firme ──────────────────────────────────────────────────────────

/// <summary>
/// Firma del consulente, legata all'utente collegato. Accesso al solo
/// interessato e agli amministratori; ogni uso in un documento va
/// registrato in log.Audit.
/// </summary>
public class FirmaConsulente
{
    public int IdFirmaConsulente { get; set; }

    /// <summary>UPN dell'utente (Entra ID).</summary>
    public string UpnUtente { get; set; } = null!;

    public byte[] ImmaginePng { get; set; } = null!;
    public string? Cellulare { get; set; }

    public bool Attiva { get; set; } = true;
    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}

/// <summary>
/// Firma usata in una compilazione. L'immagine si congela: il documento
/// prodotto deve restare uguale a sé stesso anche se l'archivio cambia.
/// Si salva solo l'immagine, nessun dato di pressione, velocità o tempi:
/// non è firma grafometrica e non si trattano dati biometrici.
/// </summary>
public class FirmaCompilazione
{
    public int IdFirmaCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public RuoloFirmaManualistica Ruolo { get; set; }
    public ModoFirmaManualistica Modo { get; set; } = ModoFirmaManualistica.Autografa;

    public string? NomeFirmatario { get; set; }
    public byte[]? ImmaginePng { get; set; }
    public string? ImprontaSha256 { get; set; }

    public DateTime AcquisitaIlUtc { get; set; }
    public string? AcquisitaDa { get; set; }
}

// ── Produzione e consegna ──────────────────────────────────────────

/// <summary>
/// Un documento prodotto. Il file sta sul server, come per DVR e
/// piscine: qui restano percorso e impronta.
/// </summary>
public class ManualeGenerato
{
    public int IdManualeGenerato { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public string CodiceModello { get; set; } = null!;

    /// <summary>Per le lettere ripetute: 1 di N.</summary>
    public int NumeroLettera { get; set; } = 1;

    public string NomeFile { get; set; } = null!;
    public FormatoManualeGenerato Formato { get; set; }
    public string? PercorsoArchivio { get; set; }
    public string? ImprontaSha256 { get; set; }

    public DateTime GeneratoIlUtc { get; set; }
    public string? GeneratoDa { get; set; }

    public List<ManualeGeneratoRevisione> Revisioni { get; set; } = new();
}

/// <summary>
/// Legame fra documento prodotto e revisione del modello usata.
/// È la tabella che risponde alla domanda «con quale versione del
/// modello è stato fatto questo documento?».
/// </summary>
public class ManualeGeneratoRevisione
{
    public int IdManualeGeneratoRevisione { get; set; }
    public int IdManualeGenerato { get; set; }
    public ManualeGenerato? ManualeGenerato { get; set; }

    public int IdRevisioneModello { get; set; }
    public RevisioneModello? RevisioneModello { get; set; }
}

/// <summary>
/// Un invio. Lo stato sta nel database e non in memoria: l'invio
/// programmato deve sopravvivere a un riavvio dell'applicazione.
/// </summary>
public class InvioDocumenti
{
    public int IdInvioDocumenti { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public TipoInvioManualistica Tipo { get; set; }
    public string Destinatari { get; set; } = null!;
    public string? Formati { get; set; }

    /// <summary>Non spedire prima di quest'ora. Il ritardo predefinito al cliente è di sei ore.</summary>
    public DateTime? InviareDopoUtc { get; set; }

    public StatoInvioManualistica Stato { get; set; } = StatoInvioManualistica.Programmato;
    public DateTime? InviatoIlUtc { get; set; }
    public string? AnnullatoDa { get; set; }
    public DateTime? AnnullatoIlUtc { get; set; }

    /// <summary>Identificativo del messaggio restituito da Brevo.</summary>
    public string? IdMessaggio { get; set; }
    public string? Esito { get; set; }

    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }
}
