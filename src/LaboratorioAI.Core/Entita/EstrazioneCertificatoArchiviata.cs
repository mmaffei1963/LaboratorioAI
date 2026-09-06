namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Estrazione AI di un certificato esterno, archiviata come cache per impronta del
/// PDF (SHA-256): evita di ripetere la chiamata AI per lo stesso file. Ogni
/// esecuzione è un record, nessuna sovrascrittura.
/// </summary>
public class EstrazioneCertificatoArchiviata
{
    public int IdEstrazione { get; set; }

    /// <summary>Impronta SHA-256 (hex) del contenuto del PDF.</summary>
    public string Hash { get; set; } = null!;

    public string? NomeFileOriginale { get; set; }

    /// <summary>JSON dell'estrazione (EstrazioneCertificatoEsterno) conservato integralmente.</summary>
    public string EstrazioneJson { get; set; } = null!;

    public string? ModelloAi { get; set; }
    public DateTime DataCreazione { get; set; }
    public int? IdUtente { get; set; }
}
