namespace LaboratorioAI.Core.Entita;

/// <summary>Stato di un certificato esterno nel flusso di import verso ArcLab.</summary>
public enum StatoCertificatoEsterno { DaImportare = 1, Importato = 2 }

/// <summary>
/// Certificato di un laboratorio esterno da importare in ArcLab. Staging: NON scrive su ArcLab.
/// È ArcLab (via processo dedicato) a leggere questi record e a creare il certificato nativo,
/// collegandolo al campione. Idempotenza sul CodiceLocale.
///
/// Popolato al momento della conferma/caricamento del certificato esterno, quando sono già noti
/// il campione ArcLab agganciato, il numero e la data del certificato.
/// </summary>
public class CertificatoEsternoDaImportare
{
    public int IdCertificatoEsterno { get; set; }

    /// <summary>Chiave locale stabile (GUID) per l'idempotenza dell'import.</summary>
    public string CodiceLocale { get; set; } = Guid.NewGuid().ToString("N");

    public StatoCertificatoEsterno Stato { get; set; } = StatoCertificatoEsterno.DaImportare;

    /// <summary>Campione ArcLab a cui collegare il certificato (Campione.IdCampione).</summary>
    public int IdCampioneArcLab { get; set; }

    /// <summary>Numero del certificato/RdP esterno (finirà in certificato.NCertificato).</summary>
    public string NumeroCertificato { get; set; } = "";

    /// <summary>Data di emissione del certificato esterno (certificato.DataCertificato).</summary>
    public DateTime DataCertificato { get; set; }

    /// <summary>Data di caricamento/spedizione (certificato.DataSped).</summary>
    public DateTime DataSped { get; set; }

    /// <summary>Nome del file rinominato (solo tracciabilità; il file non va in ArcLab).</summary>
    public string? NomeFile { get; set; }

    /// <summary>Laboratorio esterno emittente (acronimo/ragione sociale), per tracciabilità.</summary>
    public string? Laboratorio { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }
}
