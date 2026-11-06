namespace LaboratorioAI.Core.Entita;

// «Clienti da seguire»: andamento del fatturato (AZW) e dei campioni (ArcLab) per cliente, registro delle azioni del
// commerciale, assegnazioni dei clienti e soglie. Schema «clienti». ArcLab e AZW restano in sola lettura: qui si
// tengono solo una copia di lavoro dei campioni e i dati inseriti in LabAI.

/// <summary>
/// Copia di lavoro dei campioni di laboratorio ArcLab (commesse tipo 3) riassunti per cliente, mese e controllo.
/// Si riscrive a ogni importazione AZW, nella stessa transazione.
/// </summary>
public class CampioneMensileArcLab
{
    public int IdCampioneMensileArcLab { get; set; }

    /// <summary><c>Clienti.CODContabilita</c> di ArcLab = <c>CODCF</c> di AZW.</summary>
    public string CodiceCliente { get; set; } = null!;
    public int Anno { get; set; }
    public int Mese { get; set; }
    public int? IdControllo { get; set; }
    public string? Controllo { get; set; }
    public int Campioni { get; set; }

    /// <summary>Somma del prezzo del controllo sulla commessa per i campioni del mese (valore a listino, non fatturato).</summary>
    public decimal ValoreListino { get; set; }

    /// <summary>Data di ricezione dell'ultimo campione del mese.</summary>
    public DateTime UltimoCampione { get; set; }
}

/// <summary>Soglie e parametri della pagina «Clienti da seguire». Una sola riga, modificabile da chi ha <c>clienti.gestisci</c>.</summary>
public class ImpostazioniAndamento
{
    public int IdImpostazioniAndamento { get; set; }

    /// <summary>Fatturato di laboratorio minimo nei 12 mesi precedenti per avere il semaforo (€).</summary>
    public decimal MinimoFatturato { get; set; } = 500m;

    /// <summary>Variazione percentuale (es. −20) uguale o peggiore della quale il cliente è rosso.</summary>
    public decimal SogliaRosso { get; set; } = -20m;

    /// <summary>Variazione percentuale (es. −10) uguale o peggiore della quale il cliente è giallo.</summary>
    public decimal SogliaGiallo { get; set; } = -10m;

    /// <summary>Valore a listino delle analisi perse oltre il quale il cliente in calo è giallo (€).</summary>
    public decimal ValoreAnalisiPerse { get; set; } = 300m;

    /// <summary>Campioni negli stessi tre mesi dell'anno prima oltre i quali un cliente fermo negli ultimi tre mesi è giallo.</summary>
    public int CampioniTrimestreMinimo { get; set; } = 3;

    /// <summary>Legionella: giorni di tolleranza oltre la data attesa del prossimo campionamento.</summary>
    public int LegionellaTolleranzaGiorni { get; set; } = 30;

    /// <summary>Legionella: giorni di preavviso per l'elenco «da ricordare».</summary>
    public int LegionellaPreavvisoGiorni { get; set; } = 30;

    /// <summary>
    /// Legionella: variazione percentuale dei campioni (es. −20) uguale o peggiore della quale il cliente è «in calo»
    /// (ultimi 12 mesi contro i 12 precedenti). Separata dalla soglia del rosso dall'08/10/2026.
    /// </summary>
    public decimal LegionellaSogliaCalo { get; set; } = -20m;

    /// <summary>«Solo consulenza»: altri servizi negli ultimi 12 mesi almeno (€).</summary>
    public decimal SoloConsulenzaMinimo { get; set; } = 1000m;

    /// <summary>«Solo consulenza»: analisi negli ultimi 12 mesi meno di (€).</summary>
    public decimal SoloConsulenzaLabMassimo { get; set; } = 500m;

    /// <summary>Testo che riconosce i controlli di Legionella nel nome del controllo ArcLab.</summary>
    public string TestoControlloLegionella { get; set; } = "Legionella";

    /// <summary>Voci AZW del fatturato Legionella, separate da punto e virgola.</summary>
    public string VociLegionella { get; set; } = "LEV;LEC;LEG";

    /// <summary>Base di calcolo (provv.BasiCalcolo) che definisce il fatturato di laboratorio; vuota = «Area analisi».</summary>
    public int? IdBaseCalcoloLaboratorio { get; set; }

    public DateTime DataUltimaModifica { get; set; }
    public string? UltimaModificaDa { get; set; }
}

public enum TipoAzioneCliente
{
    Telefonata = 1,
    Visita = 2,
    Email = 3,
    Offerta = 4,
    Altro = 9
}

public enum EsitoAzioneCliente
{
    DaDefinire = 0,
    Positivo = 1,
    InTrattativa = 2,
    Negativo = 3,
    NonRaggiunto = 4
}

/// <summary>Azione del commerciale su un cliente (registro). Non si cancella: si annulla.</summary>
public class AzioneCliente
{
    public int IdAzioneCliente { get; set; }
    public string CodiceCliente { get; set; } = null!;

    /// <summary>Giorno dell'azione (data di calendario, senza ora).</summary>
    public DateTime DataAzione { get; set; }
    public TipoAzioneCliente Tipo { get; set; }
    public EsitoAzioneCliente Esito { get; set; }
    public string? Note { get; set; }

    /// <summary>Giorno in cui ricontattare il cliente (promemoria), se previsto.</summary>
    public DateTime? ProssimoContatto { get; set; }

    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }
    public DateTime DataCreazione { get; set; }
    public bool Annullata { get; set; }
    public string? AnnullataDa { get; set; }
    public DateTime? DataAnnullamento { get; set; }
}

public enum StatoAssegnazioneCliente
{
    Proposta = 1,
    Approvata = 2,
    Rifiutata = 3,
    Chiusa = 4
}

/// <summary>
/// Assegnazione di un cliente MARE.A esistente a un commerciale (proposta dal commerciale, approvata da Mirko).
/// È la base della regola della crescita del 30% delle provvigioni 2027.
/// </summary>
public class AssegnazioneCliente
{
    public int IdAssegnazioneCliente { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public int IdCommerciale { get; set; }
    public StatoAssegnazioneCliente Stato { get; set; } = StatoAssegnazioneCliente.Proposta;
    public string? Motivo { get; set; }

    /// <summary>Fatturato di laboratorio degli ultimi 12 mesi al momento della proposta (€), per memoria.</summary>
    public decimal? FatturatoLabAllaProposta { get; set; }

    public DateTime DataProposta { get; set; }
    public string? PropostaDa { get; set; }
    public DateTime? DataDecisione { get; set; }
    public string? DecisaDa { get; set; }
    public string? NotaDecisione { get; set; }

    /// <summary>Primo giorno di validità (data di calendario), di norma il giorno dell'approvazione.</summary>
    public DateTime? DataInizio { get; set; }

    /// <summary>Ultimo giorno di validità se l'assegnazione è chiusa.</summary>
    public DateTime? DataFine { get; set; }
}

/// <summary>Informazioni inserite in LabAI su un cliente (una riga per cliente).</summary>
public class AttributiCliente
{
    public int IdAttributiCliente { get; set; }
    public string CodiceCliente { get; set; } = null!;

    /// <summary>Intermediario: studio, società di consulenza o manutenzione che campiona per conto dei propri clienti.</summary>
    public bool Intermediario { get; set; }
    public string? Note { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public string? UltimaModificaDa { get; set; }
}

// ── Cambi di gestione (subentri, 12/10/2026) ──────────────────────────

/// <summary>
/// Copia di lavoro degli insediamenti ArcLab con il loro cliente, per riconoscere i cambi di gestione (stesso posto,
/// nuovo cliente). Si riscrive a ogni importazione AZW, nella stessa transazione. ArcLab resta in sola lettura.
/// </summary>
public class InsediamentoArcLab
{
    public int IdInsediamentoArcLab { get; set; }

    /// <summary><c>Insediamento.IDInsediamento</c> di ArcLab.</summary>
    public int IdInsediamento { get; set; }

    /// <summary><c>Clienti.IDCliente</c> di ArcLab.</summary>
    public int IdClienteArcLab { get; set; }

    /// <summary><c>Clienti.CODContabilita</c> = codice cliente AZW.</summary>
    public string? CodiceCliente { get; set; }
    public string RagioneSociale { get; set; } = "";
    public string? PartitaIva { get; set; }
    public string? NomeInsediamento { get; set; }
    public string? Indirizzo { get; set; }
    public string? Citta { get; set; }
    public string? Telefono { get; set; }
}

/// <summary>Campioni di laboratorio (commesse tipo 3) per insediamento e mese, ultimi quattro anni. Copia di lavoro.</summary>
public class CampioneInsediamentoArcLab
{
    public int IdCampioneInsediamentoArcLab { get; set; }
    public int IdInsediamento { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }
    public int Campioni { get; set; }
    public int CampioniLegionella { get; set; }

    /// <summary>Valore a listino (prezzo del controllo sulla commessa), non fatturato.</summary>
    public decimal Valore { get; set; }
    public DateTime PrimoCampione { get; set; }
    public DateTime UltimoCampione { get; set; }
}

/// <summary>
/// Decisione di una persona su un cambio di gestione: conferma o scarto di un abbinamento proposto, abbinamento manuale
/// a un altro insediamento, oppure «nessun subentro» (insediamento successivo vuoto). Non si cancella: si annulla.
/// </summary>
public class SubentroInsediamento
{
    public int IdSubentroInsediamento { get; set; }

    /// <summary>Insediamento ArcLab che non manda più campioni.</summary>
    public int IdInsediamentoPrecedente { get; set; }

    /// <summary>Insediamento ArcLab del nuovo gestore; vuoto = «nessun subentro» (chiuso o passato ad altri).</summary>
    public int? IdInsediamentoSuccessivo { get; set; }

    /// <summary>Codici cliente al momento della decisione, per leggibilità.</summary>
    public string? CodiceClientePrecedente { get; set; }
    public string? CodiceClienteSuccessivo { get; set; }

    /// <summary>Vero = abbinamento confermato; falso = abbinamento scartato.</summary>
    public bool Confermato { get; set; }

    /// <summary>Vero se l'abbinamento l'ha scelto la persona (non era proposto dal programma).</summary>
    public bool Manuale { get; set; }
    public string? Note { get; set; }

    public DateTime DataDecisione { get; set; }
    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }

    public bool Annullata { get; set; }
    public DateTime? DataAnnullamento { get; set; }
    public string? AnnullataDa { get; set; }
}

/// <summary>
/// Esito della verifica di una P.IVA sul servizio europeo VIES. «Non valida» non vuol dire per forza cessata:
/// in VIES compaiono solo le imprese abilitate agli scambi con l'estero.
/// </summary>
public class VerificaPartitaIva
{
    public int IdVerificaPartitaIva { get; set; }
    public string Paese { get; set; } = "IT";
    public string PartitaIva { get; set; } = "";

    /// <summary>Vero = valida in VIES; falso = non trovata; vuoto = verifica non riuscita (servizio non disponibile).</summary>
    public bool? Valida { get; set; }
    public string? Nome { get; set; }
    public string? Indirizzo { get; set; }
    public string? Errore { get; set; }
    public DateTime DataVerifica { get; set; }
}

/// <summary>
/// Prestazioni di consulenza (commesse tipo 5) e formazione (tipo 4) per insediamento, mese e tipo di incarico, ultimi
/// quattro anni (13/10/2026, cambi di gestione estesi alla consulenza). Copia di lavoro riscritta a ogni importazione AZW.
/// Data della prestazione = data di fattura, altrimenti fine o inizio prestazione.
/// </summary>
public class PrestazioneInsediamentoArcLab
{
    public int IdPrestazioneInsediamentoArcLab { get; set; }
    public int IdInsediamento { get; set; }
    public int Anno { get; set; }
    public int Mese { get; set; }
    public int IdTipoCommessa { get; set; }

    /// <summary><c>Tipi_Incarico.ID_Tipo_Incarico</c> (dalla descrizione incarico della commessa); vuoto se non trovato.</summary>
    public int? IdTipoIncarico { get; set; }
    public int Prestazioni { get; set; }

    /// <summary>Prezzo della commessa per le prestazioni del mese (valore indicativo, non fatturato).</summary>
    public decimal Valore { get; set; }
    public DateTime PrimaPrestazione { get; set; }
    public DateTime UltimaPrestazione { get; set; }
}

/// <summary>Perché un cliente è stato tolto dalla lista dei clienti da seguire (14/10/2026).</summary>
public enum MotivoUscitaCliente
{
    /// <summary>Acquisito da un altro nostro cliente: lo storico si somma a quello del cliente che l'ha assorbito.</summary>
    AcquisitoDaCliente = 1,
    CessataAttivita = 2,
    /// <summary>Passato alla concorrenza o rapporto chiuso.</summary>
    NonRecuperabile = 3,
    Altro = 9
}

/// <summary>
/// Cliente tolto dalla lista dei clienti da seguire (14/10/2026). Non si cancella: «Rimetti in lista» annulla la riga.
/// Se il cliente torna a fatturare o a mandare campioni dopo il mese di uscita ricompare da solo, con un avviso.
/// </summary>
public class ClienteTolto
{
    public int IdClienteTolto { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public MotivoUscitaCliente Motivo { get; set; }
    /// <summary>Cliente che l'ha acquisito (solo per <see cref="MotivoUscitaCliente.AcquisitoDaCliente"/>).</summary>
    public string? CodiceClienteSuccessore { get; set; }
    public string? Note { get; set; }
    /// <summary>Giorno dell'uscita (di norma quello in cui il commerciale la registra).</summary>
    public DateTime DataUscita { get; set; }
    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }
    public DateTime DataRegistrazione { get; set; }
    public bool Annullata { get; set; }
    public DateTime? DataAnnullamento { get; set; }
    public string? AnnullataDa { get; set; }
}
