namespace LaboratorioAI.Core.Entita;

// Limiti normativi per la validazione dei Rapporti di Prova (fase 1a, 17/10/2026).
// Procedura «Gestione dei limiti normativi e delle soglie di riferimento» (PG __ Rev. 0 del 16/10/2026):
// le schede si ricavano da fonti ufficiali (proposte dall'AI o inserite a mano) e producono segnalazioni
// solo dopo la validazione di una persona autorizzata. Nessuna cancellazione fisica: le versioni restano.

/// <summary>Famiglia normativa (Allegato 1 della procedura).</summary>
public enum FamigliaNormativa
{
    Legionella = 1,
    AcqueConsumoUmano = 2,
    Molluschi = 3,
    Alimenti = 4,
    Piscine = 5,
    Scarichi = 6,
    SuperficiAria = 7,
    Altro = 9
}

/// <summary>Ente o banca dati da cui proviene il testo.</summary>
public enum TipoFonteNormativa
{
    EurLex = 1,
    Normattiva = 2,
    GazzettaUfficiale = 3,
    MinisteroSalute = 4,
    BollettinoRegionale = 5,
    Altro = 9
}

/// <summary>Tipo di limite: decide il colore della segnalazione (§ 6.7 della procedura).</summary>
public enum TipoLimiteNormativo
{
    /// <summary>Limite di sicurezza: superamento → rosso.</summary>
    Sicurezza = 1,
    /// <summary>Criterio di igiene di processo (es. Reg. 2073 cap. 2) → arancione.</summary>
    IgieneProcesso = 2,
    /// <summary>Parametro indicatore (es. acque D.Lgs. 18/2023) → arancione.</summary>
    Indicatore = 3,
    /// <summary>Linea guida, soglia di intervento (es. Legionella 2015) → arancione.</summary>
    LineaGuida = 4,
    /// <summary>Riferimento interno o concordato con il Cliente → arancione.</summary>
    Riferimento = 5
}

/// <summary>Come si confronta il risultato con il limite.</summary>
public enum OperatoreLimite
{
    /// <summary>Valore massimo ammesso (≤ Valore): superamento se il risultato è maggiore.</summary>
    ValoreMassimo = 1,
    /// <summary>Deve essere minore di Valore: superamento se il risultato è maggiore o uguale.</summary>
    MinoreDi = 2,
    /// <summary>Valore minimo ammesso (≥ Valore).</summary>
    ValoreMinimo = 3,
    /// <summary>Intervallo ammesso Valore – ValoreMax (es. pH).</summary>
    Intervallo = 4,
    /// <summary>Assente nella quantità indicata in Unità (es. «25 g»): presente (1) = superamento.</summary>
    AssenteIn = 5
}

/// <summary>Regola di valutazione (§ 6.8): valore puntuale salvo che la norma indichi diversamente.</summary>
public enum RegolaValutazioneLimite
{
    Puntuale = 1,
    /// <summary>Piano di campionamento a più unità (n, c, m, M), come nel Reg. 2073.</summary>
    PianoCampionamento = 2
}

/// <summary>Stato della scheda limite (§ 4 e 6.4 della procedura).</summary>
public enum StatoSchedaLimite
{
    Proposta = 1,
    Validata = 2,
    DaRiverificare = 3,
    Respinta = 4,
    Superata = 5
}

/// <summary>Stato dell'estrazione AI di una versione della fonte.</summary>
public enum StatoEstrazioneLimiti
{
    Nessuna = 0,
    InCorso = 1,
    Completata = 2,
    Errore = 3
}

/// <summary>Fonte ufficiale di una famiglia normativa (es. D.Lgs. 18/2023 su Normattiva).</summary>
public class FonteNormativa
{
    public int IdFonteNormativa { get; set; }
    public FamigliaNormativa Famiglia { get; set; }
    public string Titolo { get; set; } = "";
    public string? Ente { get; set; }
    public TipoFonteNormativa TipoFonte { get; set; }
    /// <summary>Indirizzo ufficiale da cui scaricare il testo vigente.</summary>
    public string? Url { get; set; }
    public string? Note { get; set; }
    public bool Attiva { get; set; } = true;
    public DateTime? DataUltimaVerifica { get; set; }
    public string? EsitoUltimaVerifica { get; set; }
    public DateTime DataCreazione { get; set; }
    public string? CreataDa { get; set; }
    public DateTime? DataUltimaModifica { get; set; }
    public string? UltimaModificaDa { get; set; }
}

/// <summary>Una versione del testo di una fonte, così come è stata letta (copia conservata con impronta).</summary>
public class VersioneFonte
{
    public int IdVersioneFonte { get; set; }
    public int IdFonteNormativa { get; set; }
    public FonteNormativa? Fonte { get; set; }
    /// <summary>Data di aggiornamento dichiarata dalla fonte («testo consolidato al», «vigente al»).</summary>
    public DateTime? DataAggiornamentoDichiarata { get; set; }
    public DateTime DataConsultazione { get; set; }
    /// <summary>«Scaricata» dall'indirizzo ufficiale oppure «Caricata» a mano.</summary>
    public string Origine { get; set; } = "Scaricata";
    public string? NomeFile { get; set; }
    public string? TipoContenuto { get; set; }
    public byte[]? ContenutoOriginale { get; set; }
    /// <summary>Testo estratto (dal PDF o dall'HTML), quello letto dall'AI e usato per controllare le citazioni.</summary>
    public string TestoEstratto { get; set; } = "";
    /// <summary>SHA-256 del testo estratto normalizzato: cambia solo se cambia il contenuto.</summary>
    public string Impronta { get; set; } = "";
    public string? CaricataDa { get; set; }
    public StatoEstrazioneLimiti StatoEstrazione { get; set; }
    public DateTime? DataEstrazione { get; set; }
    public string? EsitoEstrazione { get; set; }
    public int SchedeProposte { get; set; }
    public int SchedeScartate { get; set; }
}

/// <summary>
/// Scheda limite: una riga per versione. CodiceScheda (es. «LEG-001») lega le versioni della stessa scheda;
/// una scheda Validata non si modifica: una correzione crea una nuova versione «Proposta».
/// </summary>
public class LimiteNormativo
{
    public int IdLimiteNormativo { get; set; }
    public string CodiceScheda { get; set; } = "";
    public int Versione { get; set; } = 1;
    /// <summary>Versione precedente della stessa scheda (null per la prima).</summary>
    public int? IdLimitePrecedente { get; set; }
    public int? IdVersioneFonte { get; set; }
    public VersioneFonte? VersioneFonte { get; set; }
    public FamigliaNormativa Famiglia { get; set; }

    /// <summary>Parametro come scritto nella norma.</summary>
    public string Parametro { get; set; } = "";
    /// <summary>Matrice o categoria come scritta nella norma.</summary>
    public string? MatriceCategoria { get; set; }
    public OperatoreLimite Operatore { get; set; }
    public decimal? Valore { get; set; }
    public decimal? ValoreMax { get; set; }
    public string? Unita { get; set; }
    public int? N { get; set; }
    public int? C { get; set; }
    public decimal? M { get; set; }
    public decimal? MMaiuscola { get; set; }
    public TipoLimiteNormativo TipoLimite { get; set; }
    public RegolaValutazioneLimite RegolaValutazione { get; set; } = RegolaValutazioneLimite.Puntuale;
    public string? Condizioni { get; set; }
    /// <summary>Norma, articolo, allegato, punto.</summary>
    public string? Riferimento { get; set; }
    /// <summary>Frase esatta della norma da cui è tratto il limite.</summary>
    public string? Citazione { get; set; }
    /// <summary>Testo mostrato nella segnalazione (es. «Soglia 1.000 UFC/L superata – intervento previsto»).</summary>
    public string? TestoSegnalazione { get; set; }
    public DateTime? ValidoDal { get; set; }
    public DateTime? ValidoAl { get; set; }
    public StatoSchedaLimite Stato { get; set; } = StatoSchedaLimite.Proposta;
    public string? Note { get; set; }

    /// <summary>«AI» oppure il nome di chi l'ha inserita a mano.</summary>
    public string PropostaDa { get; set; } = "";
    public int? IdUtenteProposta { get; set; }
    public DateTime DataProposta { get; set; }
    public string? ModificataDa { get; set; }
    public int? IdUtenteModifica { get; set; }
    public DateTime? DataModifica { get; set; }
    public string? ValidataDa { get; set; }
    public int? IdUtenteValidazione { get; set; }
    public DateTime? DataValidazione { get; set; }
    public string? NoteValidazione { get; set; }

    public List<AbbinamentoLimite> Abbinamenti { get; set; } = new();
}

/// <summary>Collegamento della scheda ai parametri (e facoltativamente matrici o controlli) di ArcLab.</summary>
public class AbbinamentoLimite
{
    public int IdAbbinamentoLimite { get; set; }
    public int IdLimiteNormativo { get; set; }
    public int IdParametroArcLab { get; set; }
    public string? NomeParametro { get; set; }
    public int? IdMatriceArcLab { get; set; }
    public string? NomeMatrice { get; set; }
    public int? IdControlloArcLab { get; set; }
    public string? NomeControllo { get; set; }
    /// <summary>Fattore per portare il risultato ArcLab nell'unità della scheda (null = stessa unità).</summary>
    public decimal? FattoreUnita { get; set; }
    public bool Attivo { get; set; } = true;
}

// ── Verifica delle schede validate (18/10/2026): controlli automatici e revisione con l'AI ──

public enum TipoVerificaLimiti { Automatica = 1, RevisioneAi = 2, /** Proposta AI degli abbinamenti matrici → categorie (fase 1b). */ AbbinamentoMatrici = 3 }

public enum StatoGiroVerifica { InCoda = 1, InCorso = 2, Completato = 3, Errore = 4 }

/// <summary>Gravità di un rilievo: Conforme si registra solo nella revisione AI (prova che la scheda è stata vista).</summary>
public enum GravitaRilievo { Conforme = 0, Avviso = 1, Dubbio = 2, Errato = 3 }

/// <summary>Un giro di verifica: controlli automatici su tutte le schede validate, o revisione AI di una fonte.</summary>
public class GiroVerificaLimiti
{
    public int IdGiroVerificaLimiti { get; set; }
    public TipoVerificaLimiti Tipo { get; set; }
    public int? IdFonteNormativa { get; set; }
    public StatoGiroVerifica Stato { get; set; }
    public DateTime DataRichiesta { get; set; }
    public DateTime? DataInizio { get; set; }
    public DateTime? DataFine { get; set; }
    public string RichiestoDa { get; set; } = "";
    public int? IdUtenteRichiesta { get; set; }
    public string? Modello { get; set; }
    public int SchedeControllate { get; set; }
    public string? Esito { get; set; }
}

/// <summary>Esito della verifica su una scheda. Si «chiude» con una nota (confermata, corretta, respinta…).</summary>
public class RilievoVerificaLimite
{
    public int IdRilievoVerificaLimite { get; set; }
    public int IdGiroVerificaLimiti { get; set; }
    public int IdLimiteNormativo { get; set; }
    public GravitaRilievo Gravita { get; set; }
    /// <summary>Regola automatica (es. «valore-citazione») oppure «revisione-ai».</summary>
    public string Regola { get; set; } = "";
    public string Motivo { get; set; } = "";
    /// <summary>Revisione AI: frase della norma indicata dal revisore.</summary>
    public string? Citazione { get; set; }
    /// <summary>Revisione AI: la frase indicata dal revisore è stata trovata nel testo salvato?</summary>
    public bool? CitazioneTrovata { get; set; }
    public bool Chiuso { get; set; }
    public string? ChiusoDa { get; set; }
    public DateTime? DataChiusura { get; set; }
    public string? NotaChiusura { get; set; }
}

// ── Fase 1b (18/10/2026): categorie normative e abbinamento delle matrici ArcLab ──

/// <summary>
/// Categoria della norma (es. «Alimenti pronti che costituiscono terreno favorevole alla crescita di L. monocytogenes»).
/// Nasce dal testo «Matrice / categoria» delle schede; le schede si collegano per Chiave (testo normalizzato).
/// </summary>
public class CategoriaNormativa
{
    public int IdCategoriaNormativa { get; set; }
    public FamigliaNormativa Famiglia { get; set; }
    public string Nome { get; set; } = "";
    /// <summary>Testo normalizzato e compattato: uguale per le schede della stessa categoria.</summary>
    public string Chiave { get; set; } = "";
    public bool Attiva { get; set; } = true;
    public DateTime DataCreazione { get; set; }
}

public enum StatoAbbinamentoMatrice { Proposto = 1, Confermato = 2, Respinto = 3 }

/// <summary>
/// Abbinamento di una matrice ArcLab a una categoria normativa (oppure, con IdCategoriaNormativa null e Confermato,
/// «nessuna categoria»: la matrice non ha limiti della famiglia). Proposto dall'AI o inserito a mano; vale se Confermato.
/// </summary>
public class AbbinamentoMatriceCategoria
{
    public int IdAbbinamentoMatriceCategoria { get; set; }
    public int IdMatriceArcLab { get; set; }
    public string NomeMatrice { get; set; } = "";
    public int? IdCategoriaNormativa { get; set; }
    public StatoAbbinamentoMatrice Stato { get; set; }
    public string PropostoDa { get; set; } = "";
    public DateTime DataProposta { get; set; }
    public string? Nota { get; set; }
    public string? ConfermatoDa { get; set; }
    public int? IdUtenteConferma { get; set; }
    public DateTime? DataConferma { get; set; }
}

/// <summary>
/// Tempo massimo tra ricezione del campione e inizio della prova (fase 1b, 18/10/2026). Riga per famiglia
/// (IdParametroArcLab nullo) o per singolo parametro ArcLab, che prevale. Nessuna cancellazione: si disattiva.
/// </summary>
public class TempoMassimoProva
{
    public int IdTempoMassimoProva { get; set; }
    public FamigliaNormativa Famiglia { get; set; }
    public int? IdParametroArcLab { get; set; }
    public string? NomeParametro { get; set; }
    public int Ore { get; set; }
    public bool Attivo { get; set; } = true;
    public string? Nota { get; set; }
    public DateTime DataCreazione { get; set; }
    public string ModificatoDa { get; set; } = "";
    public int? IdUtenteModifica { get; set; }
    public DateTime DataModifica { get; set; }
}

/// <summary>
/// 22/10/2026: campione tolto dall'elenco dei campioni in ritardo («non mostrare più»), con il motivo.
/// Nessuna cancellazione: «mostra di nuovo» lo disattiva.
/// </summary>
public class CampioneNascostoRitardi
{
    public int IdCampioneNascostoRitardi { get; set; }
    /// <summary>Campione.IdCampione di ArcLab (solo il riferimento: ArcLab non si tocca).</summary>
    public int IdCampioneArcLab { get; set; }
    public string NumeroCampione { get; set; } = "";
    public string Motivo { get; set; } = "";
    public string NascostoDa { get; set; } = "";
    public DateTime DataNascosto { get; set; }
    public bool Attivo { get; set; } = true;
    public string? RipristinatoDa { get; set; }
    public DateTime? DataRipristino { get; set; }
}
