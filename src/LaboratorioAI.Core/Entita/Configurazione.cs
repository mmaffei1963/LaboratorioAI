namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Parametro applicativo modificabile senza rilascio.
///
/// Non contiene segreti: chiavi API e credenziali risiedono nella
/// configurazione protetta dell'ambiente, non in questa tabella.
/// </summary>
public class Configurazione
{
    public int IdConfigurazione { get; set; }

    public string Chiave { get; set; } = null!;
    public string? Valore { get; set; }
    public string? Descrizione { get; set; }

    /// <summary>Raggruppamento per la presentazione nell'interfaccia.</summary>
    public string? Categoria { get; set; }

    public DateTime DataModifica { get; set; }
    public int? IdUtenteModifica { get; set; }
}
