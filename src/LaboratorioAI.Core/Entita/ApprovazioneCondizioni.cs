namespace LaboratorioAI.Core.Entita;

// ═══════════════════════════════════════════════════════════════════
// Approvazione delle Condizioni Generali di Fornitura (09/10/2026)
// Specifica funzionale v0.4: una approvazione per cliente e per revisione delle Condizioni,
// con approvazione specifica delle clausole vessatorie (artt. 1341-1342 c.c.).
// Schema «contratti» del database LaboratorioAI. Nessuna cancellazione fisica. ArcLab in sola lettura.
// ═══════════════════════════════════════════════════════════════════

/// <summary>Stato di una revisione delle Condizioni.</summary>
public enum StatoRevisioneCondizioni
{
    /// <summary>In preparazione: PDF e clausole modificabili.</summary>
    Bozza = 1,

    /// <summary>Congelata: PDF e clausole non si toccano più, l'impronta è calcolata. Si può chiedere l'approvazione.</summary>
    Pubblicata = 2
}

/// <summary>Stato di una richiesta di approvazione rivolta a un cliente.</summary>
public enum StatoRichiestaApprovazione
{
    DaInviare = 1,
    Inviata = 2,
    Aperta = 3,
    /// <summary>Pagina firmata a mano caricata, in attesa della verifica del commerciale (modalità C).</summary>
    InVerifica = 4,
    Approvata = 5,
    Rifiutata = 6,
    Annullata = 7
}

/// <summary>Modalità con cui è stata perfezionata l'approvazione (Specifica §3).</summary>
public enum ModalitaApprovazione
{
    /// <summary>A – caselle dal Portale.</summary>
    Caselle = 1,
    /// <summary>A – caselle con conferma OTP via e-mail.</summary>
    CaselleOtp = 2,
    /// <summary>B – pagina firmata con firma digitale del cliente.</summary>
    FirmaDigitale = 3,
    /// <summary>C – pagina firmata a mano e timbrata.</summary>
    Autografa = 4,
    /// <summary>D – firma grafica con mouse o dito.</summary>
    FirmaGrafica = 5
}

/// <summary>
/// Revisione delle Condizioni Generali di Fornitura (es. «Mod. Com. 05 Rev. 19 del 16/10/2026»). Il PDF è conservato
/// nel database, così è compreso nel backup e non può essere sostituito su disco; alla pubblicazione si calcola e
/// congela l'impronta SHA-256, che la Ricevuta cita.
/// </summary>
public class RevisioneCondizioni
{
    public int IdRevisioneCondizioni { get; set; }

    /// <summary>Riferimento stampato nei documenti (es. «Mod. Com. 05 Rev. 19 del 16/10/2026»).</summary>
    public string Codice { get; set; } = "";

    public int NumeroRevisione { get; set; }
    public DateTime? DataRevisione { get; set; }
    public DateTime? DataEntrataVigore { get; set; }

    public StatoRevisioneCondizioni Stato { get; set; } = StatoRevisioneCondizioni.Bozza;

    /// <summary>Se vero, chi aveva approvato la revisione precedente deve approvare di nuovo (Specifica §11.1).</summary>
    public bool RichiedeNuovaApprovazione { get; set; } = true;

    /// <summary>Testo «Cosa cambia rispetto alla revisione precedente», mostrato al cliente.</summary>
    public string? TestoCosaCambia { get; set; }

    public string? NomeFilePdf { get; set; }
    public byte[]? ContenutoPdf { get; set; }
    public long? DimensionePdf { get; set; }

    /// <summary>Impronta SHA-256 del PDF (64 caratteri esadecimali), congelata alla pubblicazione.</summary>
    public string? ImprontaPdf { get; set; }

    public DateTime? DataPubblicazione { get; set; }
    public int? IdUtentePubblicazione { get; set; }
    public string? NomeUtentePubblicazione { get; set; }

    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public int? IdUtenteCreazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public List<ClausolaCondizioni> Clausole { get; set; } = new();
}

/// <summary>Clausola da approvare specificamente (Casella 2), per revisione.</summary>
public class ClausolaCondizioni
{
    public int IdClausolaCondizioni { get; set; }
    public int IdRevisioneCondizioni { get; set; }
    public RevisioneCondizioni? Revisione { get; set; }

    public int Ordine { get; set; }

    /// <summary>Numero dell'articolo (es. «4.2»).</summary>
    public string Articolo { get; set; } = "";

    /// <summary>Contenuto breve, come compare nell'elenco della Casella 2.</summary>
    public string ContenutoBreve { get; set; } = "";

    /// <summary>Testo integrale dell'articolo, per il pannello «leggi».</summary>
    public string? TestoIntegrale { get; set; }

    /// <summary>Falso se tolta dall'elenco mentre la revisione era in bozza (nessuna cancellazione fisica).</summary>
    public bool Attivo { get; set; } = true;
}

/// <summary>
/// Firmatario designato dal commerciale per un cliente ArcLab: la persona che approva le Condizioni.
/// Un solo Firmatario attivo per cliente; il cambio lascia il precedente con Attivo = false (resta nel registro).
/// </summary>
public class FirmatarioCliente
{
    public int IdFirmatarioCliente { get; set; }

    /// <summary>Clienti.IDCliente di ArcLab (sola lettura, nessuna vera FK).</summary>
    public int IdClienteArcLab { get; set; }
    public string? ClienteRagioneSociale { get; set; }
    public string? ClientePartitaIva { get; set; }

    public string Nome { get; set; } = "";
    public string Cognome { get; set; } = "";
    public string? Qualifica { get; set; }
    /// <summary>E-mail nominativa (non caselle generiche): qui arriva il link personale.</summary>
    public string Email { get; set; } = "";
    public string? CodiceFiscale { get; set; }
    public string? Note { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime DataDesignazione { get; set; }
    public int? IdUtenteDesignazione { get; set; }
    public string? NomeUtenteDesignazione { get; set; }
    public DateTime? DataCancellazione { get; set; }
}

/// <summary>
/// Richiesta di approvazione di una revisione delle Condizioni (ed eventualmente di un'Offerta) rivolta a un cliente.
/// Il link personale è un codice casuale consegnato solo per e-mail: nel database se ne conserva solo l'impronta.
/// </summary>
public class RichiestaApprovazione
{
    public int IdRichiestaApprovazione { get; set; }

    public int IdClienteArcLab { get; set; }
    public string? ClienteRagioneSociale { get; set; }
    public string? ClientePartitaIva { get; set; }

    public int IdRevisioneCondizioni { get; set; }
    public RevisioneCondizioni? Revisione { get; set; }

    /// <summary>Offerta da accettare nello stesso passaggio (facoltativa, Specifica §1.3).</summary>
    public int? IdPreventivo { get; set; }

    public int? IdFirmatarioCliente { get; set; }
    public FirmatarioCliente? Firmatario { get; set; }

    public StatoRichiestaApprovazione Stato { get; set; } = StatoRichiestaApprovazione.DaInviare;
    public ModalitaApprovazione? Modalita { get; set; }

    /// <summary>Impronta SHA-256 del codice del link personale (il codice in chiaro non si conserva).</summary>
    public string? ImprontaCodiceLink { get; set; }
    public DateTime? ScadenzaLink { get; set; }

    public DateTime? DataInvio { get; set; }
    public DateTime? DataApertura { get; set; }
    public DateTime? DataPerfezionamento { get; set; }
    public int NumeroPromemoria { get; set; }
    public DateTime? DataUltimoPromemoria { get; set; }

    // ── Fase 2 (10/10/2026): documenti mostrati, dati dichiarati, Ricevuta ──
    /// <summary>Copia congelata dell'Offerta in PDF, generata all'invio del link: è esattamente ciò che il cliente vede.</summary>
    public byte[]? OffertaPdf { get; set; }
    public string? OffertaImpronta { get; set; }
    /// <summary>Riferimento stampato dell'Offerta (es. «2026/012 del 05/10/2026»).</summary>
    public string? OffertaRiferimento { get; set; }

    /// <summary>Dati dichiarati dal Firmatario nella pagina (Casella 3), che possono differire da quelli designati.</summary>
    public string? NomeDichiarato { get; set; }
    public string? CognomeDichiarato { get; set; }
    public string? QualificaDichiarata { get; set; }

    /// <summary>Impronta SHA-256 dei testi delle tre caselle esattamente come mostrati e approvati.</summary>
    public string? ImprontaTesti { get; set; }

    /// <summary>Numero della Ricevuta (es. «RA-2026-0001»).</summary>
    public string? NumeroRicevuta { get; set; }
    public byte[]? RicevutaPdf { get; set; }
    public string? RicevutaImpronta { get; set; }
    public DateTime? DataInvioRicevuta { get; set; }
    /// <summary>Ultimo errore nell'invio della Ricevuta (si ritenta).</summary>
    public string? ErroreInvioRicevuta { get; set; }

    /// <summary>Invio massivo (10/10/2026): data in cui la richiesta è stata messa in coda per l'invio automatico del link.</summary>
    public DateTime? InvioAccodato { get; set; }
    /// <summary>Ultimo errore dell'invio automatico del link (la richiesta resta in coda e si ritenta).</summary>
    public string? ErroreInvioLink { get; set; }

    public string? Note { get; set; }
    public int? IdUtenteCreazione { get; set; }
    public string? NomeUtenteCreazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}

/// <summary>
/// Firma grafica tracciata con mouse o dito (modalità D, 10/10/2026). Due per approvazione: riquadro 1 = accettazione
/// delle Condizioni (e dell'Offerta), riquadro 2 = approvazione specifica delle clausole. Si conservano l'immagine e il
/// tracciato del gesto; nessun confronto automatico con firme di riferimento (non è un dato biometrico trattato).
/// </summary>
public class FirmaGraficaApprovazione
{
    public int IdFirmaGraficaApprovazione { get; set; }
    public int IdRichiestaApprovazione { get; set; }
    /// <summary>1 = accettazione, 2 = approvazione specifica.</summary>
    public int Riquadro { get; set; }
    public byte[] ImmaginePng { get; set; } = Array.Empty<byte>();
    /// <summary>Tracciato in JSON: tratti di punti [x, y, ms, pressione].</summary>
    public string Tracciato { get; set; } = "";
    /// <summary>Tipo di puntatore: mouse, touch o pen.</summary>
    public string? Puntatore { get; set; }
    public int Larghezza { get; set; }
    public int Altezza { get; set; }
    /// <summary>SHA-256 di immagine e tracciato.</summary>
    public string Impronta { get; set; } = "";
    public DateTime DataUtc { get; set; }
}

/// <summary>
/// Riga del registro delle evidenze (Specifica §8): tabella a sola aggiunta con impronte concatenate. Ogni riga
/// contiene l'impronta della precedente, così una modifica o una cancellazione rompe la catena e si vede.
/// UPDATE e DELETE sono impediti anche da un trigger sul database.
/// </summary>
public class EventoApprovazione
{
    public long IdEventoApprovazione { get; set; }

    public int? IdRichiestaApprovazione { get; set; }
    public int? IdClienteArcLab { get; set; }

    /// <summary>Tipo di evento (es. «revisione.pubblicata», «richiesta.creata», «documento.aperto», «approvazione.perfezionata»).</summary>
    public string Tipo { get; set; } = "";

    public DateTime DataUtc { get; set; }

    /// <summary>Dettaglio in JSON (testi mostrati, impronte, scelte del Firmatario…).</summary>
    public string? Dati { get; set; }

    public string? NomeUtente { get; set; }
    public string? IndirizzoIp { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>Impronta della riga precedente (64 zeri per la prima riga).</summary>
    public string ImprontaPrecedente { get; set; } = "";

    /// <summary>Impronta di questa riga (vedi <see cref="Servizi.CatenaEvidenze"/>).</summary>
    public string Impronta { get; set; } = "";
}

/// <summary>Stato di un invito al cliente a indicare il Firmatario (11/10/2026).</summary>
public enum StatoInvitoFirmatario
{
    DaInviare = 1,
    Inviato = 2,
    /// <summary>Il cliente ha aperto la pagina.</summary>
    Aperto = 3,
    /// <summary>Il cliente ha indicato il Firmatario: LaboratorioAI lo designa e gli invia il link di approvazione.</summary>
    Indicato = 4,
    /// <summary>Firmatario designato e link di approvazione inviato.</summary>
    Elaborato = 5,
    /// <summary>Non più necessario (es. Firmatario designato nel frattempo dal commerciale).</summary>
    Annullato = 6
}

/// <summary>
/// Invito a indicare il Firmatario (11/10/2026): e-mail all'indirizzo aziendale del cliente in anagrafica ArcLab
/// (Clienti.Email, letto di solito dall'amministrazione) con un link personale a una pagina del Portale dove si indicano
/// nome, cognome, qualifica ed e-mail nominativa di chi approverà le Condizioni. Vale la prima risposta. LaboratorioAI
/// designa il Firmatario e gli invia in automatico il link di approvazione. Nel database resta solo l'impronta del codice.
/// </summary>
public class InvitoFirmatario
{
    public int IdInvitoFirmatario { get; set; }

    public int IdClienteArcLab { get; set; }
    public string? ClienteRagioneSociale { get; set; }
    public string? ClientePartitaIva { get; set; }

    /// <summary>Indirizzi aziendali a cui è stato mandato l'invito, separati da «;».</summary>
    public string EmailDestinatari { get; set; } = "";

    /// <summary>Revisione delle Condizioni in vigore quando l'invito è stato preparato.</summary>
    public int IdRevisioneCondizioni { get; set; }

    public StatoInvitoFirmatario Stato { get; set; } = StatoInvitoFirmatario.DaInviare;

    public string? ImprontaCodiceLink { get; set; }
    public DateTime? ScadenzaLink { get; set; }

    public DateTime? InvioAccodato { get; set; }
    public DateTime? DataInvio { get; set; }
    public DateTime? DataApertura { get; set; }
    public int NumeroPromemoria { get; set; }
    public DateTime? DataUltimoPromemoria { get; set; }
    /// <summary>Ultimo errore d'invio (resta in coda e si ritenta).</summary>
    public string? ErroreInvio { get; set; }

    // ── Risposta del cliente ──
    public string? NomeIndicato { get; set; }
    public string? CognomeIndicato { get; set; }
    public string? QualificaIndicata { get; set; }
    public string? EmailIndicata { get; set; }
    public DateTime? DataIndicazione { get; set; }

    // ── Elaborazione in LaboratorioAI ──
    public int? IdFirmatarioCliente { get; set; }
    public int? IdRichiestaApprovazione { get; set; }
    public DateTime? DataElaborazione { get; set; }
    public string? ErroreElaborazione { get; set; }

    public string? Note { get; set; }
    public int? IdUtenteCreazione { get; set; }
    public string? NomeUtenteCreazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}
