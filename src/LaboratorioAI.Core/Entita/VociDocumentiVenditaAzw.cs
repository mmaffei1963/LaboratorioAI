namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Voce di un documento di vendita AZW (10/11/2026), per la ricerca delle fatture per descrizione. In AZW
/// (TABFST01 testata, TABFST02 righe) il testo della fattura è spezzato: una riga con quantità e prezzo seguita da
/// righe di solo testo (TIPRIG = 1). Qui ogni voce riunisce la riga con il prezzo e le righe di testo che la seguono.
/// Tabella di lavoro riscritta a ogni importazione da AZW (sola lettura su AZW). Dal 2019: FE fatture, CE note di credito.
/// </summary>
public class VoceDocumentoVenditaAzw
{
    public int IdVoceDocumentoVenditaAzw { get; set; }
    /// <summary>FE = fattura elettronica, CE = nota di credito elettronica.</summary>
    public string TipoDocumento { get; set; } = null!;
    public int AnnoDocumento { get; set; }
    public string NumeroDocumento { get; set; } = null!;
    public DateTime DataDocumento { get; set; }
    public string CodiceCliente { get; set; } = null!;
    public string? RagioneSociale { get; set; }
    /// <summary>Numero della prima riga AZW della voce.</summary>
    public int NumeroRiga { get; set; }
    public string? CodiceArticolo { get; set; }
    /// <summary>Testo completo della voce (riga con il prezzo + righe di testo successive).</summary>
    public string Descrizione { get; set; } = "";
    public decimal Quantita { get; set; }
    public decimal Prezzo { get; set; }
    /// <summary>Sconti della riga, es. «10» o «10+5».</summary>
    public string? Sconto { get; set; }
    /// <summary>Imponibile della voce; negativo per le note di credito.</summary>
    public decimal Importo { get; set; }
    public string? Centro { get; set; }
    public string? Voce { get; set; }
    public string? Commessa { get; set; }
}
