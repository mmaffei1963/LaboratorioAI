namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Documenti del portale (19/11/2026): stato letto/non letto di un documento della tabella Entries di Marea per un
/// cliente. Su Marea non si scrive: lo stato è tenuto qui. Senza riga vale «letto» se il documento ha già una data di
/// scarico su Palomar (Entries.DataUltimoDownload).
/// </summary>
public class LetturaDocumentoPortale
{
    public int IdLetturaDocumentoPortale { get; set; }
    /// <summary>Entries.Id di Marea.</summary>
    public int IdEntry { get; set; }
    public int IdClienteArcLab { get; set; }
    public bool Letto { get; set; }
    public DateTime DataAggiornamentoUtc { get; set; }
    /// <summary>«scarico» o «segnato non letto».</summary>
    public string Origine { get; set; } = "";
}
