namespace LaboratorioAI.Core.Entita;

/// <summary>Esito di un'operazione su un documento (emissione Ordine di Accettazione, rinomina certificato esterno).</summary>
public enum EsitoDocumento
{
    /// <summary>Tutto a posto: documento generato/rinominato e presente nella cartella Palomar.</summary>
    Ok = 1,
    /// <summary>Ordine già emesso in precedenza: nessuna nuova emissione (guardia anti-doppione).</summary>
    GiaEmesso = 2,
    /// <summary>Lotto con campioni senza parametri: escluso.</summary>
    NonPronto = 3,
    /// <summary>Operazione fallita: nessun documento prodotto.</summary>
    Errore = 4,
    /// <summary>Documento prodotto ma qualcosa non è andato (es. non arrivato nella cartella Palomar).</summary>
    Parziale = 5,
    /// <summary>Riga di annullamento (12/10/2026): annulla la riga indicata in <see cref="RegistroDocumento.IdRegistroCollegato"/>; il messaggio è il motivo.</summary>
    Annullato = 6
}

/// <summary>
/// Tipo di certificato esterno (12/10/2026). Solo esterni: tutte le prove del campione sono di laboratori esterni,
/// il certificato va a Palomar che compila ArcLab (fatturato interscambio). Misto: il campione ha anche prove
/// fatte da noi; il certificato va nella cartella Palomar INTERNI_ESTERNI (solo portale, nessun campo ArcLab)
/// e i valori si inseriscono a mano nel certificato MARE.A.
/// </summary>
public enum TipoCertificatoEsterno
{
    SoloEsterni = 1,
    Misto = 2
}

/// <summary>
/// Registro di controllo (append-only, mai modificato né cancellato) delle operazioni sui documenti
/// destinati a Palomar: per ogni Ordine di Accettazione emesso e ogni certificato esterno rinominato
/// dice cosa è partito, cosa no e se il file è davvero nella cartella di prelievo Palomar.
/// Le righe di una stessa esecuzione (es. emissione massiva) condividono <see cref="IdEsecuzione"/>.
/// </summary>
public class RegistroDocumento
{
    public int IdRegistroDocumento { get; set; }

    /// <summary>"OrdineAccettazione" oppure "CertificatoEsterno".</summary>
    public string Ambito { get; set; } = "";

    /// <summary>Identificativo dell'esecuzione (una emissione massiva, un "Salva tutti").</summary>
    public string IdEsecuzione { get; set; } = "";

    /// <summary>Data e ora UTC.</summary>
    public DateTime DataOra { get; set; }

    public int? IdUtente { get; set; }

    /// <summary>manuale, massiva, upload, cartella, riprova…</summary>
    public string? Origine { get; set; }

    /// <summary>Riferimento leggibile: n. accettazione (CodLotto) o nome del certificato.</summary>
    public string? Riferimento { get; set; }

    /// <summary>Lotto ArcLab (solo Ordini di Accettazione).</summary>
    public int? IdLotto { get; set; }

    public EsitoDocumento Esito { get; set; }

    public string? Messaggio { get; set; }

    /// <summary>File di partenza (es. PDF originale del laboratorio esterno).</summary>
    public string? FileOrigine { get; set; }

    /// <summary>Copia di archivio scritta.</summary>
    public string? FileArchivio { get; set; }

    /// <summary>Percorso del file nella cartella di prelievo Palomar.</summary>
    public string? FilePalomar { get; set; }

    /// <summary>True se dopo la scrittura il file risultava presente nella cartella Palomar con la dimensione attesa.</summary>
    public bool PalomarVerificato { get; set; }

    /// <summary>Certificati esterni: solo esterni o misto (null per le righe precedenti al 12/10/2026 e per gli altri ambiti).</summary>
    public TipoCertificatoEsterno? Tipo { get; set; }

    /// <summary>Certificati esterni: campione ArcLab agganciato (per controllare l'inserimento dei valori dei misti).</summary>
    public int? IdCampioneArcLab { get; set; }

    /// <summary>Certificati esterni: laboratorio esterno ArcLab del certificato.</summary>
    public int? IdLabEsternoArcLab { get; set; }

    /// <summary>Solo per le righe <see cref="EsitoDocumento.Annullato"/>: la riga annullata. Il registro resta append-only.</summary>
    public int? IdRegistroCollegato { get; set; }
}
