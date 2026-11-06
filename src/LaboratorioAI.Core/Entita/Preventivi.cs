namespace LaboratorioAI.Core.Entita;

/// <summary>Stato di avanzamento di un preventivo/offerta.</summary>
public enum StatoPreventivo
{
    Bozza = 0,
    DaApprovare = 1,
    Approvato = 2,
    Inviato = 3,
    Accettato = 4,
    Rifiutato = 5
}

/// <summary>Tipo di riga di un preventivo.</summary>
public enum TipoRigaPreventivo
{
    /// <summary>Controllo già presente in ArcLab (riferito per IdControllo).</summary>
    ControlloEsistente = 0,

    /// <summary>Controllo non ancora presente: riferito a una PropostaControllo di staging (import supervisionato).</summary>
    PropostaControllo = 1,

    /// <summary>Voce accessoria da listino (giudizio conformità, conservazione, ecc.).</summary>
    Accessorio = 2,

    /// <summary>
    /// Controllo/metodi non presenti in ArcLab, comunicati da un laboratorio in subappalto:
    /// composti a campo libero dal commerciale con prezzo concordato. In offerta risultano
    /// identici ai nostri; i metodi riportano sempre il riferimento al laboratorio esterno.
    /// </summary>
    ControlloLibero = 3,

    /// <summary>
    /// Due o più controlli raggruppati dal commerciale in un'unica riga con un unico prezzo
    /// (es. un pacchetto commerciale). La descrizione è libera; gli Id dei controlli d'origine
    /// restano tracciati in <see cref="RigaPreventivo.IdControlliOrigine"/>. I metodi sono
    /// l'unione di quelli dei controlli raggruppati.
    /// </summary>
    GruppoControlli = 4
}

/// <summary>Come è stato determinato il prezzo base di una riga.</summary>
public enum OriginePrezzoPreventivo
{
    /// <summary>Prezzo di commessa già applicato al cliente (commessa.PrezzoControllo).</summary>
    Commessa = 0,

    /// <summary>Somma dei CostoMetodoEuro dei metodi del controllo (listino grezzo).</summary>
    Listino = 1,

    /// <summary>Prezzo impostato manualmente dal commerciale.</summary>
    Manuale = 2
}

/// <summary>
/// Preventivo/offerta composto dal commerciale a partire dai controlli e metodi ArcLab.
///
/// Non scrive su ArcLab: i dati dei controlli/prezzi sono letti in sola lettura e ne viene
/// conservata una copia (snapshot) sulle righe, così l'offerta resta stabile nel tempo anche
/// se i cataloghi cambiano. L'anagrafica cliente è mista: se il cliente è già in ArcLab si
/// riferisce con <see cref="IdClienteArcLab"/>, altrimenti si compilano i campi ex novo.
/// </summary>
public class Preventivo
{
    public int IdPreventivo { get; set; }

    /// <summary>Numero progressivo nell'anno (es. 1, 2, 3…). Formattato in "Anno/NNN" per il documento.</summary>
    public int? Numero { get; set; }
    public int? Anno { get; set; }

    public StatoPreventivo Stato { get; set; } = StatoPreventivo.Bozza;

    // ── Cliente (anagrafica mista) ─────────────────────────────────
    /// <summary>Se valorizzato, il cliente è quello ArcLab con questo Id (sola lettura da ArcLab).</summary>
    public int? IdClienteArcLab { get; set; }
    /// <summary>Ragione sociale (snapshot da ArcLab oppure inserita ex novo). Sempre valorizzata.</summary>
    public string? ClienteRagioneSociale { get; set; }
    public string? ClientePartitaIva { get; set; }
    public string? ClienteIndirizzo { get; set; }
    public string? ClienteCap { get; set; }
    public string? ClienteCitta { get; set; }
    public string? ClienteProvincia { get; set; }
    public string? ClienteEmail { get; set; }
    /// <summary>21/10/2026: telefono del cliente (stampato nell'offerta; per «Spett.le cliente» spazio da compilare a mano).</summary>
    public string? ClienteTelefono { get; set; }
    /// <summary>21/10/2026: solo per «Spett.le cliente» (privati senza partita IVA): nome e cognome del cliente,
    /// stampato nella riga «Nome cliente» della prima pagina. L'intestazione resta «Spett.le cliente».</summary>
    public string? ClienteNome { get; set; }

    // ── Offerta ────────────────────────────────────────────────────
    public string? Oggetto { get; set; }
    /// <summary>Data dell'offerta (giorno, non UTC-time). Default = data creazione.</summary>
    public DateTime? DataOfferta { get; set; }
    /// <summary>Giorni di validità dell'offerta dalla data offerta.</summary>
    public int ValiditaGiorni { get; set; } = 365; // 365 = 12 mesi con rinnovo tacito (Condizioni Rev. 19, Art. 3.3)
    /// <summary>
    /// 21/10/2026: se vero l'offerta dura 12 mesi SENZA rinnovo tacito (in deroga all'Art. 3.3 delle Condizioni).
    /// Falso (predefinito, anche per le offerte già esistenti) = 12 mesi con rinnovo tacito annuale.
    /// </summary>
    public bool SenzaRinnovoTacito { get; set; }
    /// <summary>21/10/2026: solo per «Spett.le cliente» (privati, clienti occasionali): l'importo è già stato pagato.</summary>
    public bool GiaPagato { get; set; }
    /// <summary>Condizioni di pagamento scelte (testo libero o codice).</summary>
    public string? CondizioniPagamento { get; set; }
    /// <summary>Revisione delle Condizioni Generali richiamata (es. "Mod. Com. 05 Rev. 18 del 16/04/25").</summary>
    public string? RevisioneCondizioni { get; set; }

    // ── Importi ────────────────────────────────────────────────────
    public decimal TotaleImponibile { get; set; }
    public decimal AliquotaIva { get; set; } = 22m;

    /// <summary>
    /// Se vero, l'offerta include il riepilogo promozionale "Il nostro lavoro" (testo fisso
    /// MARE.A: esperienza, accreditamento, reperibilità) prima delle Condizioni Generali.
    /// </summary>
    public bool RiepilogoPromozionale { get; set; }

    /// <summary>
    /// 06/11/2026 (Zatterin): se vero, nell'offerta compare «Insediamento: …» sotto i controlli che ne hanno uno.
    /// Predefinito no: l'insediamento serve a prendere il prezzo giusto, non sempre è utile al cliente.
    /// </summary>
    public bool MostraInsediamenti { get; set; }

    // ── Approvazione (richiesta solo se presente uno sconto) ───────
    /// <summary>Vero se una qualunque riga ha uno sconto &gt; 0: l'invio richiede approvazione.</summary>
    public bool RichiedeApprovazione { get; set; }
    public int? IdUtenteApprovatore { get; set; }
    public DateTime? DataApprovazione { get; set; }

    public string? Note { get; set; }

    /// <summary>26/10/2026: preventivo da cui questo è stato duplicato (null = creato da zero).</summary>
    public int? IdPreventivoOrigine { get; set; }

    // ── Tracciabilità ──────────────────────────────────────────────
    public int? IdUtenteCreatore { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public List<RigaPreventivo> Righe { get; set; } = new();
}

/// <summary>Riga di un preventivo: un controllo (esistente o proposto) oppure una voce accessoria.</summary>
public class RigaPreventivo
{
    public int IdRigaPreventivo { get; set; }
    public int IdPreventivo { get; set; }
    public Preventivo? Preventivo { get; set; }

    public int Progressivo { get; set; }
    public TipoRigaPreventivo Tipo { get; set; }

    /// <summary>Controllo ArcLab (se Tipo = ControlloEsistente).</summary>
    public int? IdControlloArcLab { get; set; }
    /// <summary>Proposta di controllo locale (se Tipo = PropostaControllo).</summary>
    public int? IdPropostaControllo { get; set; }
    /// <summary>Voce di listino accessori (se Tipo = Accessorio).</summary>
    public int? IdVoceAccessorio { get; set; }

    /// <summary>Descrizione mostrata in offerta (snapshot del nome controllo/accessorio).</summary>
    public string? Descrizione { get; set; }

    /// <summary>
    /// Solo per Tipo = GruppoControlli: gli Id ArcLab (o i riferimenti locali <see cref="RifLocale"/>
    /// dei controlli d'origine privi di Id ArcLab) raggruppati in questa riga, separati da "-"
    /// (es. "123-456-789" oppure "123-L00002"), stampati in offerta accanto alla descrizione.
    /// </summary>
    public string? IdControlliOrigine { get; set; }

    /// <summary>
    /// Solo per Tipo = ControlloLibero: riferimento locale (es. "L00001") assegnato alla creazione,
    /// perché un controllo in subappalto a campo libero non ha un Id di catalogo ArcLab ma deve
    /// comunque restare sempre identificabile in offerta, anche quando viene raggruppato con altri
    /// controlli (in tal caso il riferimento confluisce in <see cref="IdControlliOrigine"/> del gruppo).
    /// </summary>
    public string? RifLocale { get; set; }

    /// <summary>
    /// 05/11/2026 (richiesta di Zatterin): insediamento del cliente a cui si riferisce la riga, facoltativo. Le commesse
    /// ArcLab sono per insediamento, con prezzi propri: se indicato, il prezzo di commessa si prende da quella sede.
    /// </summary>
    public int? IdInsediamentoArcLab { get; set; }
    public string? Insediamento { get; set; }

    public OriginePrezzoPreventivo OriginePrezzo { get; set; }
    /// <summary>Prezzo base: prezzo di commessa oppure somma CostoMetodoEuro (snapshot).</summary>
    public decimal PrezzoBase { get; set; }
    /// <summary>Sconto percentuale sul prezzo base (0 = nessuno sconto).</summary>
    public decimal ScontoPercentuale { get; set; }
    /// <summary>Quantità (utile per accessori, es. n° giudizi di conformità o giorni di conservazione).</summary>
    public int Quantita { get; set; } = 1;
    /// <summary>Prezzo finale di riga = PrezzoBase * Quantita * (1 - ScontoPercentuale/100).</summary>
    public decimal PrezzoFinale { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Dettaglio dei metodi/parametri che compongono la riga (snapshot).
    /// Per i controlli esistenti è la copia di quelli ArcLab al momento dell'inserimento;
    /// per i controlli liberi/subappalto sono inseriti a mano dal commerciale.
    /// Serve a rendere l'offerta stabile nel tempo e i metodi liberi indistinguibili dai nostri.
    /// </summary>
    public List<RigaMetodoPreventivo> Metodi { get; set; } = new();
}

/// <summary>
/// Metodo/parametro che compone una riga di preventivo (snapshot). Riporta gli stessi
/// campi dei nostri metodi ArcLab, così i metodi liberi/in subappalto sono indistinguibili
/// da quelli interni. Per le prove in subappalto contiene sempre il riferimento al
/// laboratorio esterno e la relativa frase da stampare in offerta.
/// </summary>
public class RigaMetodoPreventivo
{
    public int IdRigaMetodoPreventivo { get; set; }
    public int IdRigaPreventivo { get; set; }
    public RigaPreventivo? Riga { get; set; }

    public int Progressivo { get; set; }

    /// <summary>Metodo ArcLab di origine (null se inserito a campo libero).</summary>
    public int? IdMetodoArcLab { get; set; }
    /// <summary>Vero se il metodo è inserito a mano (non presente in ArcLab).</summary>
    public bool Libero { get; set; }

    /// <summary>Parametro / descrizione metodo (es. "Piombo (Pb)").</summary>
    public string? Descrizione { get; set; }
    /// <summary>Denominazione della riga (rigametodo.DenominazioneRiga).</summary>
    public string? DenominazioneRiga { get; set; }
    /// <summary>
    /// Denominazione ufficiale del metodo (metodo.DettagliMetodo), la stessa usata nei Rapporti di
    /// Prova e nell'Ordine di Accettazione (AnalisiAccettazione.Denominazione). A differenza di OA
    /// non riporta l'aliquota: in offerta non esiste ancora una prova/aliquota reale.
    /// </summary>
    public string? Denominazione { get; set; }
    /// <summary>Riferimento normativo / metodo (metodo.RifBiblio).</summary>
    public string? RiferimentoNormativo { get; set; }
    public string? UnitaMisura { get; set; }
    /// <summary>Costo del metodo (snapshot di metodo.CostoMetodoEuro; per l'esterno = costo concordato).</summary>
    public decimal? CostoMetodoEuro { get; set; }
    /// <summary>Il metodo è accreditato presso MARE.A.</summary>
    public bool Accreditato { get; set; }
    public int? CifreSignificative { get; set; }
    public int? MaxDecimali { get; set; }

    // ── Laboratorio esterno (subappalto) ───────────────────────────
    /// <summary>Laboratorio esterno ArcLab (null se metodo interno o lab esterno libero).</summary>
    public int? IdLabEsternoArcLab { get; set; }
    /// <summary>Nome del laboratorio esterno (LabEsterno.LabEsterno).</summary>
    public string? LabEsterno { get; set; }
    /// <summary>Frase da riportare in offerta per l'analisi in subappalto (LabEsterno.Frase).</summary>
    public string? LabEsternoFrase { get; set; }
}

/// <summary>
/// Voce del listino accessori, precaricata dalle Condizioni Generali di Fornitura.
/// Es. giudizio di conformità, riemissione RdP, conservazione campione, campionamento.
/// </summary>
public class VoceListinoAccessorio
{
    public int IdVoceAccessorio { get; set; }
    public string Codice { get; set; } = "";
    public string Descrizione { get; set; } = "";
    public decimal Prezzo { get; set; }
    /// <summary>Unità di riferimento del prezzo (es. "cad", "giorno").</summary>
    public string? UnitaMisura { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Condizione di pagamento proponibile in offerta (es. "Bonifico bancario vista fattura").
/// Gestita in Amministrazione: nessuna cancellazione fisica, solo <see cref="Attivo"/>.
/// </summary>
public class MetodoPagamento
{
    public int IdMetodoPagamento { get; set; }
    public string Descrizione { get; set; } = "";
    /// <summary>Ordine di visualizzazione nell'elenco (crescente).</summary>
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
}
