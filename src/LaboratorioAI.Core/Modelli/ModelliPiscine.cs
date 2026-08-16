// LaboratorioAI.Core/Modelli/ModelliPiscine.cs
//
// Modello dati per la generazione dei manuali/DVR degli impianti natatori.
// Le proprietà sono raggruppate per ORIGINE del dato, così da rispecchiare il
// flusso già in uso per i DVR Legionellosi:
//   - Anagrafica  -> ArcLab (SQL Server), sola lettura
//   - Sopralluogo -> questionario compilato dal consulente
//   - Config      -> OpzioniPiscine / dati forniti da MARE.A (limiti, metodi)
//   - Documento   -> DB LaboratorioAI (metadati e questionario salvato)
//
// Ogni proprietà riporta, in commento, il tag del controllo contenuto del
// modello Word a cui si mappa (cfr. Dizionario_Tag_Modello_Piscine).

namespace LaboratorioAI.Core.Modelli;

/// <summary>Collocazione dell'impianto: pilota le sezioni condizionate se_impianto_*.</summary>
public enum CollocazioneImpianto { Coperto, Scoperto, Misto }

/// <summary>Sistema di ricircolo dell'acqua.</summary>
public enum SistemaRicircolo { Sfioro, Bordo, Misto }

/// <summary>Esito della verifica di conformità di bocchettoni e griglie di fondo.</summary>
public enum StatoConformitaGriglie { Conformi, NonConformi, DaVerificare }

/// <summary>Trattamento di disinfezione dell'acqua.</summary>
public enum TrattamentoAcqua { Cloro, Sale, OssigenoAttivo, UvOzono, Altro }

/// <summary>Presenza dell'assistente bagnanti: pilota la sezione se_assenza_bagnino.</summary>
public enum PresenzaBagnino { Continuativo, FasceOrarie, Assente }

/// <summary>Tipologia della struttura ricettiva.</summary>
public enum TipoStruttura { Albergo, VillaggioTuristico, Residence, Agriturismo, Altro }

/// <summary>Tipologia di utenza prevalente della vasca.</summary>
public enum TipoUtenza { Adulti, Bambini, Mista }

/// <summary>Categoria della piscina secondo la classificazione normativa.</summary>
public enum CategoriaPiscina
{
    /// <summary>A — utenza pubblica / uso collettivo.</summary>
    A,
    /// <summary>B — condominiali / alberghiere entro le soglie.</summary>
    B,
    /// <summary>C — usi speciali (curative, termali).</summary>
    C
}

/// <summary>
/// Dati anagrafici dell'insediamento (il sito fisico che ospita la piscina) e
/// del cliente collegato, letti da ArcLab in SOLA LETTURA. Nessuna proprietà
/// viene mai riscritta verso il gestionale (regola invariante).
///
/// In ArcLab: <c>Clienti</c> (chiave <c>IdCliente</c>) contiene ragione sociale
/// e dati fiscali; <c>Insediamenti</c> (chiave <c>IdInsediamento</c>, collegata
/// al cliente tramite <c>IdCliente</c>) contiene indirizzo e provincia. Un
/// cliente può avere più insediamenti: il DVR piscina è riferito a un
/// insediamento specifico.
/// </summary>
public record AnagraficaPiscina
{
    /// <summary>Chiave del cliente in ArcLab (tabella <c>Clienti</c>), int.</summary>
    public required int IdCliente { get; init; }

    /// <summary>
    /// Chiave dell'insediamento in ArcLab (tabella <c>Insediamenti</c>), int.
    /// Identifica il sito fisico con la piscina ed è il riferimento del DVR.
    /// </summary>
    public required int IdInsediamento { get; init; }

    /// <summary>Ragione sociale del cliente (<c>Clienti</c>). Tag: <c>ragione_sociale</c>.</summary>
    public required string RagioneSociale { get; init; }

    /// <summary>Partita IVA o codice fiscale del cliente (<c>Clienti</c>). Tag: <c>partita_iva</c>.</summary>
    public string? PartitaIva { get; init; }

    /// <summary>Codice contabilità del cliente (<c>Clienti.CODContabilita</c>). Usato nel nome file.</summary>
    public string? CodContabilita { get; init; }

    /// <summary>Codice fiscale del cliente (<c>Clienti.CodFisc</c>).</summary>
    public string? CodiceFiscale { get; init; }

    /// <summary>Sede legale (indirizzo di fatturazione del cliente).</summary>
    public string? SedeLegale { get; init; }

    /// <summary>Cliente stagionale (<c>Clienti.Stagionale</c>).</summary>
    public bool? Stagionale { get; init; }

    /// <summary>Indirizzo dell'insediamento (<c>Insediamenti</c>). Tag: <c>indirizzo</c>.</summary>
    public string? Indirizzo { get; init; }

    /// <summary>Comune dell'insediamento (<c>Insediamenti</c>).</summary>
    public string? Comune { get; init; }

    /// <summary>
    /// Provincia dell'insediamento — campo <c>ProvinciaInsed</c> (<c>Insediamenti</c>).
    /// Da qui si deriva la regione. Atteso come sigla (es. "RN"); il resolver
    /// accetta anche il nome esteso.
    /// </summary>
    public string? Provincia { get; init; }

    /// <summary>Riferimento pratica/commessa. Tag: <c>rif_pratica</c>.</summary>
    public string? RiferimentoPratica { get; init; }

    /// <summary>Comune e provincia uniti per il modello. Tag: <c>comune_provincia</c>.</summary>
    public string? ComuneProvincia =>
        string.IsNullOrWhiteSpace(Comune) ? Provincia
        : string.IsNullOrWhiteSpace(Provincia) ? Comune
        : $"{Comune} ({Provincia})";

    /// <summary>
    /// Regione derivata dalla provincia dell'insediamento. Tag: <c>regione</c>.
    /// Pilota il richiamo del regolamento regionale nel modello.
    /// </summary>
    public string? Regione => ProvinceRegioni.RegioneDa(Provincia);
}

/// <summary>Singola vasca dell'impianto. Alimenta la tabella variabile <c>tab_vasche</c>.</summary>
public record VascaPiscina
{
    /// <summary>Denominazione della vasca. Colonna <c>{denominazione}</c>.</summary>
    public required string Denominazione { get; init; }

    /// <summary>Tipo di vasca (nuoto, bambini, idromassaggio...). Colonna <c>{tipo}</c>.</summary>
    public string? Tipo { get; init; }

    /// <summary>Dimensioni (lunghezza × larghezza). Colonna <c>{dimensioni}</c>.</summary>
    public string? Dimensioni { get; init; }

    /// <summary>Profondità minima/massima in metri. Colonna <c>{profondita}</c>.</summary>
    public string? Profondita { get; init; }

    /// <summary>Volume d'acqua in m³. Colonna <c>{volume}</c>.</summary>
    public string? Volume { get; init; }

    /// <summary>Temperatura di esercizio in °C. Colonna <c>{temperatura}</c>.</summary>
    public string? Temperatura { get; init; }

    /// <summary>Capienza massima bagnanti. Colonna <c>{capienza}</c>.</summary>
    public string? Capienza { get; init; }

    // ── Struttura ────────────────────────────────────────────────────
    public string? Forma { get; init; }
    public string? Superficie { get; init; }
    public string? VariazioneBatimetrica { get; init; }
    public string? AttrezzatureAccessorie { get; init; }
    public string? Struttura { get; init; }
    public string? Rivestimento { get; init; }

    // ── Idraulica / impianti tecnologici ─────────────────────────────
    public string? Pompe { get; init; }
    public string? Filtri { get; init; }
    public string? Prefiltri { get; init; }
    public string? Immissione { get; init; }
    public string? Ripresa { get; init; }
    public string? Ricircolo { get; init; }
    public string? Riscaldamento { get; init; }
    public string? RicambioAcqua { get; init; }

    // ── Impianto tecnico dettagliato (manuale, pag. 37) ──────────────
    public string? DelimitazioneArea { get; init; }
    public string? PompeNumero { get; init; }
    public string? PompePotenza { get; init; }
    public string? PompePortata { get; init; }
    public string? FiltriNumero { get; init; }
    public string? FiltriDiametro { get; init; }
    public string? FiltriTipologia { get; init; }
    public string? ContrLavaggiModalita { get; init; }
    public string? ContrLavaggiFrequenza { get; init; }
    public string? PrefiltriNumero { get; init; }
    public string? TemperaturaEsercizio { get; init; }
    public string? ContatoreDedicato { get; init; }
    public string? Ispezioni { get; init; }

    // ── Circolazione (manuale, pag. 38) ──────────────────────────────
    public string? PompeRiserva { get; init; }          // "Sì"/"No"
    public string? BocchetteParete { get; init; }        // n°
    public string? BocchetteFondo { get; init; }         // n°
    public string? VascaCompenso { get; init; }          // cubatura m³

    // ── Trattamento ──────────────────────────────────────────────────
    public string? SistemaDosaggio { get; init; }
    public string? PrincipioAttivo { get; init; }
    public string? SistemaAutomaticoAnalisi { get; init; }
}

/// <summary>
/// Punto di campionamento. Alimenta <c>tab_punti_prelievo</c>; i punti storici
/// sono precaricabili da ArcLab, come per i DVR Legionellosi.
/// </summary>
public record PuntoPrelievo
{
    /// <summary>Denominazione del punto. Colonna <c>{punto}</c>.</summary>
    public required string Punto { get; init; }

    /// <summary>Ubicazione fisica del punto. Colonna <c>{ubicazione}</c>.</summary>
    public string? Ubicazione { get; init; }

    /// <summary>Frequenza di campionamento. Colonna <c>{frequenza}</c>.</summary>
    public string? Frequenza { get; init; }

    /// <summary>Esito storico più recente. Colonna <c>{esito_storico}</c>.</summary>
    public string? EsitoStorico { get; init; }
}

/// <summary>
/// Parametro di controllo dell'acqua. Alimenta <c>tab_parametri_controllo</c>.
/// I valori (limite, frequenza, metodo) sono forniti da MARE.A e NON desunti.
/// </summary>
public record ParametroControllo
{
    /// <summary>Denominazione del parametro. Colonna <c>{parametro}</c>.</summary>
    public required string Parametro { get; init; }

    /// <summary>Unità di misura. Colonna <c>{unita_misura}</c>.</summary>
    public string? UnitaMisura { get; init; }

    /// <summary>Valore limite applicabile. Colonna <c>{limite}</c>.</summary>
    public string? Limite { get; init; }

    /// <summary>Frequenza di controllo. Colonna <c>{frequenza}</c>.</summary>
    public string? Frequenza { get; init; }

    /// <summary>Metodo analitico di riferimento. Colonna <c>{metodo}</c>.</summary>
    public string? Metodo { get; init; }
}

/// <summary>Voce dello scadenzario manutentivo. Alimenta <c>tab_manutenzione</c>.</summary>
public record VoceManutenzione
{
    /// <summary>Attività manutentiva. Colonna <c>{attivita}</c>.</summary>
    public required string Attivita { get; init; }

    /// <summary>Frequenza dell'attività. Colonna <c>{frequenza}</c>.</summary>
    public string? Frequenza { get; init; }

    /// <summary>Responsabile dell'attività. Colonna <c>{responsabile}</c>.</summary>
    public string? Responsabile { get; init; }

    /// <summary>Prossima scadenza. Colonna <c>{prossima_scadenza}</c>.</summary>
    public DateOnly? ProssimaScadenza { get; init; }
}

/// <summary>
/// Checklist delle misure di prevenzione (sez. 4 del modello). Ogni proprietà
/// corrisponde a una casella di controllo <c>chk_*</c>.
/// </summary>
public record ChecklistMisure
{
    /// <summary>Tag: <c>chk_griglie</c>.</summary>
    public bool Griglie { get; init; }

    /// <summary>Tag: <c>chk_cartelli</c>.</summary>
    public bool Cartelli { get; init; }

    /// <summary>Tag: <c>chk_salvagente</c>.</summary>
    public bool Salvagente { get; init; }

    /// <summary>Tag: <c>chk_pronto_soccorso</c>.</summary>
    public bool ProntoSoccorso { get; init; }

    /// <summary>Tag: <c>chk_dae</c>.</summary>
    public bool Dae { get; init; }

    /// <summary>Tag: <c>chk_regolamento</c>.</summary>
    public bool Regolamento { get; init; }

    /// <summary>Tag: <c>chk_registro</c>.</summary>
    public bool Registro { get; init; }

    /// <summary>Tag: <c>chk_manutenzione</c>.</summary>
    public bool Manutenzione { get; init; }

    /// <summary>Tag: <c>chk_formazione</c>.</summary>
    public bool Formazione { get; init; }
}

/// <summary>
/// Dati completi del questionario piscine, sorgente per la generazione del
/// documento Word. Compone l'anagrafica (ArcLab), il rilievo di sopralluogo,
/// la configurazione parametri (MARE.A) e i metadati del documento.
/// </summary>
public record SchedaPiscina
{
    // ---- Origine: ArcLab (sola lettura) -------------------------------------

    /// <summary>Dati anagrafici letti da ArcLab.</summary>
    public required AnagraficaPiscina Anagrafica { get; init; }

    // ---- Origine: questionario di sopralluogo -------------------------------

    /// <summary>Tag: <c>tipo_struttura</c>.</summary>
    public TipoStruttura? TipoStruttura { get; init; }

    /// <summary>Tag: <c>numero_vasche</c>.</summary>
    public int? NumeroVasche { get; init; }

    /// <summary>Tag: <c>collocazione_impianto</c>.</summary>
    public CollocazioneImpianto? Collocazione { get; init; }

    /// <summary>Tag: <c>tipo_utenza</c>.</summary>
    public TipoUtenza? TipoUtenza { get; init; }

    /// <summary>Tag: <c>sistema_ricircolo</c>.</summary>
    public SistemaRicircolo? SistemaRicircolo { get; init; }

    /// <summary>Tag: <c>griglie_conformi</c>.</summary>
    public StatoConformitaGriglie? GriglieConformi { get; init; }

    /// <summary>Tag: <c>trattamento_acqua</c>.</summary>
    public TrattamentoAcqua? TrattamentoAcqua { get; init; }

    /// <summary>Tag: <c>presenza_idromassaggio</c>. Pilota <c>se_vasca_idromassaggio</c>.</summary>
    public bool PresenzaIdromassaggio { get; init; }

    /// <summary>Tag: <c>presenza_bagnino</c>.</summary>
    public PresenzaBagnino? PresenzaBagnino { get; init; }

    /// <summary>Tag: <c>presenza_dae</c>.</summary>
    public bool PresenzaDae { get; init; }

    /// <summary>Tag: <c>responsabile_piscina</c>.</summary>
    public string? ResponsabilePiscina { get; init; }

    /// <summary>Tag: <c>addetto_impianti</c>.</summary>
    public string? AddettoImpianti { get; init; }

    // ---- Figure aggiuntive (norma / manuale) --------------------------------
    public string? ResponsabileSicurezzaBagnanti { get; init; }
    public string? AssistenteBagnanti { get; init; }
    public string? Gestore { get; init; }

    /// <summary>Datore di lavoro (nome e cognome insieme). Manuale: <c>fig_datore</c>.</summary>
    public string? DatoreLavoro { get; init; }

    // ---- Anagrafica estesa (manuale di autocontrollo) -----------------------
    public string? CodiceAteco { get; init; }
    public string? AttivitaEsercitata { get; init; }
    public string? PeriodoEsercizio { get; init; }
    public int? AnnoCostruzione { get; init; }

    // ---- Classificazione e autorizzazioni -----------------------------------
    /// <summary>Categoria (A / B / C) ai sensi della classificazione normativa.</summary>
    public CategoriaPiscina? Categoria { get; init; }

    /// <summary>Gruppo (es. a1, a2, b2…) secondo la disciplina regionale.</summary>
    public string? Gruppo { get; init; }

    public string? AutorizzazioneSanitaria { get; init; }
    public DateOnly? DataAutorizzazioneSanitaria { get; init; }
    public string? AutorizzazioneScarico { get; init; }
    public string? FonteApprovvigionamento { get; init; }

    // ---- Caratteristiche tecniche funzionali e vigilanza (manuale) ----------
    /// <summary>Tipologia strutturale (Tipologia 1-4). Manuale: <c>tipologia_strutturale</c>.</summary>
    public string? TipologiaStrutturale { get; init; }
    /// <summary>Tipo di utilizzazione (Tipo A-F). Manuale: <c>tipo_utilizzazione</c>.</summary>
    public string? TipoUtilizzazione { get; init; }
    /// <summary>Contatore acqua su collettore di mandata (Presente/Non presente).</summary>
    public string? ContatoreImmissione { get; init; }
    /// <summary>Registrazione degli ingressi alle vasche.</summary>
    public string? RegistrazioneIngressi { get; init; }
    /// <summary>Caratteristica vasca ai fini assistente bagnanti: superficie (&lt;/&gt; 100 m²).</summary>
    public string? CaratteristicheVascaSuperficie { get; init; }
    /// <summary>Caratteristica vasca: profondità (≤/&gt; 140 cm).</summary>
    public string? CaratteristicheVascaProfondita { get; init; }
    /// <summary>Lati del bordo vasca liberi da ostacoli che ne precludano la visuale.</summary>
    public string? LatiBordoLiberi { get; init; }
    /// <summary>Modalità di vigilanza dei bagnanti in vasca.</summary>
    public string? VigilanzaBagnanti { get; init; }
    /// <summary>Scelta relativa alla figura di assistente bagnanti (frase intera).</summary>
    public string? AssistenteBagnantiScelta { get; init; }
    /// <summary>Presenza di istruttori di nuoto in struttura (frase intera).</summary>
    public string? IstruttoriNuoto { get; init; }

    // ---- HACCP: analisi dei punti critici (manuale, pag. 44+) ---------------
    public string? PhPericolo { get; init; }
    public string? PhAzionePreventiva { get; init; }
    public string? PhMonitoraggio { get; init; }
    public string? CloroLiberoPericolo { get; init; }
    public string? CloroLiberoAzionePreventiva { get; init; }
    public string? CloroLiberoMonitoraggio { get; init; }
    public string? CloroCombinatoPericolo { get; init; }
    public string? PompeAzioneCorrettiva { get; init; }
    public string? FiltriAzioneCorrettiva { get; init; }

    // ---- Sostanze chimiche per il trattamento (manuale) ---------------------
    public string? DisinfettanteProdotto { get; init; }
    public string? DisinfettanteDosaggio { get; init; }
    public string? ReintegroCloroProdotto { get; init; }
    public string? ReintegroCloroModalita { get; init; }
    public string? FlocculanteProdotto { get; init; }
    public string? FlocculanteModalita { get; init; }
    public string? CorrettorePhProdotto { get; init; }
    public string? CorrettorePhModalita { get; init; }
    public string? AntialgheProdotto { get; init; }
    public string? AntialgheModalita { get; init; }

    // ---- Servizi igienici (manuale, pag. 53) --------------------------------
    public string? ServiziCaratterizzazione { get; init; }
    public string? ServiziDistinzione { get; init; }
    public string? ServiziNumeroDocce { get; init; }
    public string? ServiziNumeroLavabo { get; init; }

    // ---- Servizi, docce, lavapiedi, pronto soccorso, accesso ----------------
    public string? PassaggioAccesso { get; init; }
    public string? LavapiedNumero { get; init; }
    public string? LavapiedRilascio { get; init; }
    public string? LavapiedSoluzione { get; init; }
    public string? AntimicFrequenza { get; init; }
    public string? AntimicProcedura { get; init; }
    public string? DocceCaratterizzazione { get; init; }
    public string? DocceComposizione { get; init; }
    public string? DocceLinee { get; init; }
    public string? DocceMiscelazione { get; init; }
    public string? ProntoSoccorso { get; init; }
    public string? AccessibilitaMezzi { get; init; }
    public string? PsAzionePreventiva { get; init; }
    public string? PsLuogoConservazione { get; init; }
    public string? PsUbicazione { get; init; }

    /// <summary>Addetto alle pulizie (unico per tutta la matrice pulizie del manuale).</summary>
    public string? AddettoPulizie { get; init; }

    // ---- Campi specifici del Registro tecnico funzionale (Marche) -----------
    public string? MarcheGruppoStruttura { get; init; }
    public string? MarcheCarVascaSup { get; init; }        // < 240 / > 240 m²
    public string? MarcheCarVascaProf { get; init; }       // ≤ 160 / > 160 cm
    public string? MarchePostiLetto { get; init; }         // < 120 / > 120
    public string? MarcheImmissioneN { get; init; }
    public string? MarcheImmissioneTipo { get; init; }
    public string? MarcheRipresaSup { get; init; }
    public string? MarcheRipresaImmN { get; init; }
    public string? MarcheRipresaImmTipo { get; init; }
    public string? MarcheFiltriModello { get; init; }
    public string? MarcheFiltriAltezza { get; init; }
    public string? MarcheFiltriPortata { get; init; }
    public string? MarchePompeModello { get; init; }
    public string? MarchePompeHp { get; init; }
    public string? MarchePompeRiserva { get; init; }
    public string? MarcheCurva { get; init; }
    public string? MarcheNeutralizzanteProd { get; init; }
    public string? MarcheNeutralizzanteMod { get; init; }
    public string? MarcheSistemaAuto { get; init; }
    public string? DittaOrdRag { get; init; }
    public string? DittaOrdSede { get; init; }
    public string? DittaOrdComune { get; init; }
    public string? DittaOrdPiva { get; init; }
    public string? DittaStrRag { get; init; }
    public string? DittaStrSede { get; init; }
    public string? DittaStrComune { get; init; }
    public string? DittaStrPiva { get; init; }
    // Gestore (dati anagrafici, Marche) e vasca autonoma Marche
    public string? MarcheGestoreNascita { get; init; }
    public string? MarcheGestoreLuogo { get; init; }
    public string? MarcheGestoreResidenza { get; init; }
    public string? MarcheGestoreComune { get; init; }
    public string? MarcheGestoreCf { get; init; }
    public string? MarcheBagnantiMax { get; init; }
    public string? MarcheStrutturaVasca { get; init; }
    public string? MarcheRivestimento { get; init; }
    public string? MarcheDimensioni { get; init; }
    public string? MarcheProfondita { get; init; }
    public string? MarcheSuperficie { get; init; }
    public string? MarcheVolume { get; init; }
    public string? MarcheFiltriTipo { get; init; }
    public string? MarcheFiltriN { get; init; }
    public string? MarcheFiltriDiam { get; init; }
    public string? MarchePompeKw { get; init; }
    public string? MarchePompeN { get; init; }
    public string? MarcheManutInterno { get; init; }

    /// <summary>
    /// Certificati/rapporti di prova dell'acqua di piscina (da ArcLab, ultimi 5
    /// anni). Non salvati nella scheda: rivalorizzati in fase di generazione. Se
    /// presenti, il manuale include la relativa tabella dei certificati.
    /// </summary>
    public IReadOnlyList<AnalisiStoricaPiscina> AnalisiStoriche { get; init; } = [];

    // ---- Regolamento interno ------------------------------------------------
    public string? OrarioDalle { get; init; }
    public string? OrarioAlle { get; init; }
    public string? UbicazioneServizi { get; init; }

    /// <summary>Tag: <c>rischio_residuo</c>.</summary>
    public string? RischioResiduo { get; init; }

    /// <summary>Vasche dell'impianto. Tabella <c>tab_vasche</c>.</summary>
    public IReadOnlyList<VascaPiscina> Vasche { get; init; } = [];

    /// <summary>
    /// Punti di prelievo. Tabella <c>tab_punti_prelievo</c>. Precaricabili da
    /// ArcLab e integrabili in sopralluogo.
    /// </summary>
    public IReadOnlyList<PuntoPrelievo> PuntiPrelievo { get; init; } = [];

    /// <summary>Checklist delle misure di prevenzione.</summary>
    public ChecklistMisure Misure { get; init; } = new();

    // ---- Origine: configurazione OpzioniPiscine / MARE.A --------------------

    /// <summary>Parametri di controllo. Tabella <c>tab_parametri_controllo</c>.</summary>
    public IReadOnlyList<ParametroControllo> ParametriControllo { get; init; } = [];

    /// <summary>Scadenzario manutentivo. Tabella <c>tab_manutenzione</c>.</summary>
    public IReadOnlyList<VoceManutenzione> Manutenzione { get; init; } = [];

    // ---- Origine: DB LaboratorioAI (metadati del documento) -----------------

    /// <summary>Tag: <c>data_valutazione</c>.</summary>
    public DateOnly DataValutazione { get; init; }

    /// <summary>Tag: <c>revisione</c>.</summary>
    public string? Revisione { get; init; }

    /// <summary>Tag: <c>valutatore</c>.</summary>
    public string? Valutatore { get; init; }

    // ---- Condizioni derivate per i controlli-blocco se_ ---------------------
    // Espongono in modo esplicito la logica che il generatore usa per mantenere
    // o rimuovere le sezioni condizionate, evitando di duplicarla nel generatore.

    /// <summary>Vero se va mantenuta la sezione <c>se_impianto_scoperto</c>.</summary>
    public bool ImpiantoScoperto =>
        Collocazione is CollocazioneImpianto.Scoperto or CollocazioneImpianto.Misto;

    /// <summary>Vero se va mantenuta la sezione <c>se_impianto_coperto</c>.</summary>
    public bool ImpiantoCoperto =>
        Collocazione is CollocazioneImpianto.Coperto or CollocazioneImpianto.Misto;

    /// <summary>Vero se va mantenuta la sezione <c>se_vasca_idromassaggio</c>.</summary>
    public bool HaVascaIdromassaggio => PresenzaIdromassaggio;

    /// <summary>Vero se va mantenuta la sezione <c>se_assenza_bagnino</c>.</summary>
    public bool AssenzaBagnino => PresenzaBagnino is Modelli.PresenzaBagnino.Assente;
}

/// <summary>
/// Analisi di piscina già svolta per l'insediamento, letta da ArcLab in sola
/// lettura. Selezionata per matrice: 214 «acqua di piscina - in vasca» e 213
/// «acqua di piscina - immissione in vasca». Serve a precaricare gli esiti
/// storici nel DVR e il confronto con i limiti di Tabella A.
/// </summary>
public record AnalisiStoricaPiscina
{
    public int IdCampione { get; init; }
    public string? NumeroCampione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public DateTime? DataRicezione { get; init; }

    /// <summary>Matrice ArcLab: distingue immissione da acqua di vasca.</summary>
    public string? Matrice { get; init; }

    public string? PuntoPrelievo { get; init; }
    public string? LuogoPrelievo { get; init; }
    public int? IdCertificato { get; init; }

    /// <summary>Numero del certificato/rapporto di prova, se emesso.</summary>
    public string? NumeroCertificato { get; init; }

    /// <summary>Data di emissione del certificato.</summary>
    public DateTime? DataCertificato { get; init; }
}
