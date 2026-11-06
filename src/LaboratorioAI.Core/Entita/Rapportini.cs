namespace LaboratorioAI.Core.Entita;

/// <summary>Stato di un rapportino di consulenza.</summary>
public enum StatoRapportino
{
    /// <summary>Compilato, modificabile, non ancora inviato al cliente.</summary>
    Bozza = 1,
    /// <summary>PDF consegnato (cartella Palomar): il cliente ha 3 giorni per le osservazioni.</summary>
    Inviato = 2,
    /// <summary>Il cliente ha chiesto chiarimenti: fermo finché non viene corretto o confermato.</summary>
    Contestato = 3,
    /// <summary>Confermato (manualmente): pronto per il prefatturato.</summary>
    Confermato = 4,
    /// <summary>Solo rendicontato (convenzione o prepagato): non va nel prefatturato.</summary>
    Rendicontato = 5,
    /// <summary>Incluso in un lotto di prefatturazione.</summary>
    InPrefatturato = 6,
    /// <summary>Fatturato (chiuso dall'amministrazione).</summary>
    Fatturato = 7,
    /// <summary>Annullato (nessuna cancellazione fisica).</summary>
    Annullato = 8
}

/// <summary>Dove si è svolta la prestazione.</summary>
public enum LuogoPrestazione
{
    PressoCliente = 1,
    PressoSede = 2,
    DaRemoto = 3
}

/// <summary>Cosa è stato fatto su un documento di riferimento.</summary>
public enum AzioneDocumento
{
    Esaminato = 1,
    Modificato = 2,
    Emesso = 3
}

/// <summary>Forma in cui un documento modificato o emesso è stato consegnato al Cliente.</summary>
public enum ConsegnaDocumento
{
    Bozza = 1,
    Definitiva = 2
}

/// <summary>Come è stato calcolato l'importo.</summary>
public enum ModalitaImporto
{
    /// <summary>Importo indicato direttamente.</summary>
    ACorpo = 1,
    /// <summary>Tariffa oraria × ore.</summary>
    Orario = 2
}

/// <summary>Come sarà fatturata la prestazione.</summary>
public enum ModalitaFatturazione
{
    AOre = 1,
    AConsuntivo = 2,
    /// <summary>Convenzione a prezzo stabilito: richiede la commessa; il sopralluogo è solo rendicontato.</summary>
    Convenzione = 3
}

/// <summary>
/// Rapportino di consulenza: compilato dal consulente al sopralluogo, inviato al cliente prima della
/// fattura. Dati anagrafici e di commessa letti da ArcLab (sola lettura) e fotografati qui.
/// </summary>
public class Rapportino
{
    public int IdRapportino { get; set; }

    // ── Cliente (snapshot ArcLab) ──
    public int IdClienteArcLab { get; set; }
    public string RagioneSociale { get; set; } = "";
    public int IdInsediamentoArcLab { get; set; }
    public string? NomeInsediamento { get; set; }
    public string? IndirizzoInsediamento { get; set; }
    public string? EmailInsediamento { get; set; }

    /// <summary>Data dell'intervento (solo data).</summary>
    public DateTime DataIntervento { get; set; }

    // ── Consulente (ArcLab Organico) ──
    public int IdOrganicoArcLab { get; set; }
    public string Consulente { get; set; } = "";
    /// <summary>Organico.CognomeOrg (serve al prefatturato).</summary>
    public string? CognomeConsulente { get; set; }
    /// <summary>Organico.commessa (codice commessa AZW della persona, serve al prefatturato).</summary>
    public string? CommessaConsulente { get; set; }

    // ── Commessa ArcLab (facoltativa) ──
    public int? IdCommessaArcLab { get; set; }
    public string? DescrizioneCommessa { get; set; }
    public int? IdTipoIncarico { get; set; }
    public decimal? PrezzoCommessa { get; set; }

    /// <summary>Voce AZW (626, HAC, AT, …): proposta dalla commessa o scelta dal consulente.</summary>
    public string? Voce { get; set; }

    /// <summary>Descrizione che va in fattura (max 250, come Prefatturato_Interscambio_Cons).</summary>
    public string DescrizioneFattura { get; set; } = "";

    /// <summary>Descrizione puntuale dell'attività svolta (stampata nel rapporto; nota d'uso 2).</summary>
    public string? AttivitaSvolta { get; set; }

    /// <summary>Rif. offerta/contratto: dalla commessa se presente, altrimenti scritto dal consulente.</summary>
    public string? RiferimentoOfferta { get; set; }

    /// <summary>Estremi della delega operativa del cliente in essere alla data (fotografati dal registro deleghe).</summary>
    public string? RiferimentoDelega { get; set; }

    /// <summary>Fonte delle informazioni: vuota = testo predefinito delle Impostazioni.</summary>
    public string? FonteInformazioni { get; set; }

    /// <summary>Note sul piano di autocontrollo analitico (frequenze, matrici…), se predisposto o revisionato.</summary>
    public string? NotaPianoAnalitico { get; set; }

    // ── Follow-up (facoltativo) ──
    public string? FollowUpAzione { get; set; }
    /// <summary>"Cliente" oppure "MARE.A".</summary>
    public string? FollowUpResponsabile { get; set; }
    public string? FollowUpTermine { get; set; }

    /// <summary>
    /// Nota d'uso 3: il consulente ha verificato che i documenti modificati/emessi che richiamano il
    /// Laboratorio non gli attribuiscano attività o obblighi esclusi dalle Condizioni del Laboratorio.
    /// </summary>
    public bool VerificaRichiamiLaboratorio { get; set; }

    /// <summary>Documentazione di riferimento (esaminati, modificati, emessi).</summary>
    public List<RapportinoDocumento> Documenti { get; set; } = new();

    public decimal Ore { get; set; }
    public LuogoPrestazione Luogo { get; set; } = LuogoPrestazione.PressoCliente;
    public ModalitaImporto ModalitaImporto { get; set; } = ModalitaImporto.ACorpo;
    public decimal? TariffaOraria { get; set; }

    /// <summary>Importo senza IVA come compilato dal consulente.</summary>
    public decimal Importo { get; set; }

    /// <summary>Importo corretto dopo una contestazione (prevale su <see cref="Importo"/>).</summary>
    public decimal? ImportoCorretto { get; set; }
    public string? MotivoCorrezione { get; set; }
    public int? IdUtenteCorrezione { get; set; }
    public DateTime? DataCorrezione { get; set; }

    public ModalitaFatturazione ModalitaFatturazione { get; set; } = ModalitaFatturazione.AOre;

    // ── Contratto prepagato ──
    public bool Prepagato { get; set; }
    public DateTime? DataUltimoScalo { get; set; }
    public decimal? OreResiduePrima { get; set; }

    /// <summary>Note interne (non stampate).</summary>
    public string? NoteInterne { get; set; }

    // ── Stato e tracciabilità ──
    public StatoRapportino Stato { get; set; } = StatoRapportino.Bozza;
    public int? IdUtenteCreazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public DateTime? DataInvio { get; set; }
    public DateTime? DataContestazione { get; set; }
    public string? NotaContestazione { get; set; }
    public DateTime? DataConferma { get; set; }
    public int? IdUtenteConferma { get; set; }
    public DateTime? DataAnnullamento { get; set; }
    public string? MotivoAnnullamento { get; set; }

    /// <summary>Lotto di prefatturazione (fase 2).</summary>
    public int? IdLottoPrefatturato { get; set; }

    // ── Documento ──
    public string? NomeFilePdf { get; set; }
    public string? PercorsoPdf { get; set; }
    public string? FilePalomar { get; set; }

    /// <summary>Importo che va in fattura: quello corretto se presente.</summary>
    public decimal ImportoDaFatturare => ImportoCorretto ?? Importo;

    /// <summary>Ore residue del prepagato dopo questa prestazione.</summary>
    public decimal? OreResidueDopo => Prepagato && OreResiduePrima is decimal p ? p - Ore : null;

    /// <summary>True se il rapportino è solo rendicontato (non va in fattura).</summary>
    public bool SoloRendicontato => Prepagato || ModalitaFatturazione == ModalitaFatturazione.Convenzione;

    /// <summary>Modificabile dal consulente.</summary>
    public bool Modificabile => Stato is StatoRapportino.Bozza or StatoRapportino.Contestato;
}

/// <summary>
/// Ultimo consulente (ArcLab Organico) scelto da un utente: ripropone il nome alla compilazione successiva.
/// </summary>
public class ConsulenteUtente
{
    public int IdConsulenteUtente { get; set; }
    public int IdUtente { get; set; }
    public int IdOrganicoArcLab { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}

/// <summary>
/// Fascia di ArcLab Tipi_Incarico.ID_Tipo_Incarico → voce AZW (riprende la query Access di prefatturazione).
/// </summary>
public class VoceIncarico
{
    public int IdVoceIncarico { get; set; }
    public int IdTipoIncaricoDa { get; set; }
    public int IdTipoIncaricoA { get; set; }
    /// <summary>Voce AZW; vuota = nessuna voce (es. 118–119, voluto).</summary>
    public string Voce { get; set; } = "";
    public bool Attivo { get; set; } = true;
}

/// <summary>Riga di documentazione di riferimento di un rapportino.</summary>
public class RapportinoDocumento
{
    public int IdRapportinoDocumento { get; set; }
    public int IdRapportino { get; set; }
    /// <summary>Documento tipo dall'elenco di amministrazione; vuoto = scritto a mano.</summary>
    public int? IdDocumentoTipo { get; set; }
    public string Nome { get; set; } = "";
    public string? Revisione { get; set; }
    public DateTime? DataDocumento { get; set; }
    public AzioneDocumento Azione { get; set; } = AzioneDocumento.Esaminato;
    /// <summary>Solo per modificato/emesso.</summary>
    public ConsegnaDocumento? Consegna { get; set; }
    public int Ordine { get; set; }
    /// <summary>Copiato dal documento tipo al salvataggio (nota d'uso 3).</summary>
    public bool RichiamaLaboratorio { get; set; }
    /// <summary>Copiato dal documento tipo: è il piano di autocontrollo analitico.</summary>
    public bool PianoAnalitico { get; set; }
    /// <summary>False = riga sostituita da una modifica successiva (nessuna cancellazione fisica).</summary>
    public bool Attivo { get; set; } = true;
}

/// <summary>Elenco (amministrazione) dei documenti che i consulenti trattano di solito.</summary>
public class DocumentoTipo
{
    public int IdDocumentoTipo { get; set; }
    public string Nome { get; set; } = "";
    /// <summary>HACCP, Sicurezza, Igiene, …</summary>
    public string? Area { get; set; }
    /// <summary>Il documento può richiamare il Laboratorio MARE.A (nota d'uso 3).</summary>
    public bool RichiamaLaboratorio { get; set; }
    /// <summary>È il piano di autocontrollo analitico (stampa la riga dedicata).</summary>
    public bool PianoAnalitico { get; set; }
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;
}

/// <summary>Stato di una delega operativa nel registro.</summary>
public enum StatoDelega
{
    /// <summary>Generata, in attesa della firma del cliente: non vale ancora.</summary>
    DaFirmare = 1,
    /// <summary>Firmata: vale dalla data di sottoscrizione.</summary>
    Sottoscritta = 2
}

/// <summary>
/// Delega operativa di un cliente a un consulente MARE.A (registro per cliente e consulente).
/// Generata dal modello Word oppure registrata a mano (deleghe firmate prima del programma).
/// </summary>
public class DelegaCliente
{
    public int IdDelegaCliente { get; set; }
    public int IdClienteArcLab { get; set; }
    public string RagioneSociale { get; set; } = "";
    /// <summary>Estremi della delega come vanno riportati nel rapporto.</summary>
    public string Estremi { get; set; } = "";
    public DateTime? DataSottoscrizione { get; set; }
    /// <summary>Fine validità (revoca/scadenza); vuota = in essere.</summary>
    public DateTime? DataFine { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public int? IdUtenteModifica { get; set; }
    public DateTime DataModifica { get; set; }

    // ── Consulente delegato (ArcLab Organico) ──
    public int? IdOrganicoArcLab { get; set; }
    public string? Consulente { get; set; }
    public string? ConsulenteQualifica { get; set; }
    public string? ConsulenteAlbo { get; set; }

    /// <summary>DaFirmare finché non si registra la firma; le deleghe registrate a mano nascono Sottoscritte.</summary>
    public StatoDelega Stato { get; set; } = StatoDelega.Sottoscritta;

    // ── Dati del documento generato (fotografia di ArcLab e di quanto scritto) ──
    public bool Generata { get; set; }
    public DateTime? DataEmissione { get; set; }
    public int? IdInsediamentoArcLab { get; set; }
    public string? SedeLegale { get; set; }
    public string? SedeLegaleComune { get; set; }
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? SedeOperativa { get; set; }
    public string? SedeOperativaComune { get; set; }
    public string? LegaleRappNome { get; set; }
    public string? LegaleRappCognome { get; set; }
    /// <summary>Rapportino da cui è stata generata (facoltativo).</summary>
    public int? IdRapportino { get; set; }
    public string? PercorsoWord { get; set; }
    public string? PercorsoPdf { get; set; }
    /// <summary>Copia firmata allegata (PDF o immagine).</summary>
    public string? PercorsoFirmata { get; set; }
    public int? IdUtenteCreazione { get; set; }

    /// <summary>Valida per tutte le sedi operative del cliente, anche quelle aggiunte in futuro.</summary>
    public bool TuttiInsediamenti { get; set; }

    /// <summary>Sedi operative per cui vale (se non vale per tutte).</summary>
    public List<DelegaInsediamento> Insediamenti { get; set; } = new();

    /// <summary>
    /// Vale per l'insediamento? Sì se è per tutti, se l'insediamento è tra quelli indicati, oppure se non ne
    /// è indicato nessuno (deleghe registrate a mano prima della gestione per sede).
    /// </summary>
    public bool ValePerInsediamento(int idInsediamento) =>
        TuttiInsediamenti || !Insediamenti.Any(i => i.Attivo)
        || Insediamenti.Any(i => i.Attivo && i.IdInsediamentoArcLab == idInsediamento);

    /// <summary>In essere alla data: firmata, attiva, sottoscritta entro la data e non terminata.</summary>
    public bool ValidaAl(DateTime data) =>
        Attivo && Stato != StatoDelega.DaFirmare && DataSottoscrizione is DateTime ds && ds.Date <= data.Date
        && (DataFine is null || DataFine.Value.Date >= data.Date);
}

/// <summary>Sede operativa (insediamento ArcLab) per cui vale una delega.</summary>
public class DelegaInsediamento
{
    public int IdDelegaInsediamento { get; set; }
    public int IdDelegaCliente { get; set; }
    public int IdInsediamentoArcLab { get; set; }
    public string Nome { get; set; } = "";
    /// <summary>Indirizzo come stampato nella delega (via, CAP).</summary>
    public string? Sede { get; set; }
    public string? Comune { get; set; }
    /// <summary>False = sede tolta da una modifica successiva (nessuna cancellazione fisica).</summary>
    public bool Attivo { get; set; } = true;
}

/// <summary>Dati del consulente che non stanno in ArcLab (qualifica, iscrizione all'Albo), per le deleghe.</summary>
public class ConsulenteDati
{
    public int IdConsulenteDati { get; set; }
    public int IdOrganicoArcLab { get; set; }
    public string Nome { get; set; } = "";
    public string? Qualifica { get; set; }
    public string? IscrizioneAlbo { get; set; }
    public int? IdUtenteModifica { get; set; }
    public DateTime DataModifica { get; set; }
}
