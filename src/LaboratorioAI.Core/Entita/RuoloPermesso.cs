namespace LaboratorioAI.Core.Entita;

/// <summary>Associazione fra ruolo e permesso.</summary>
public class RuoloPermesso
{
    public int IdRuolo { get; set; }
    public Ruolo Ruolo { get; set; } = null!;

    public int IdPermesso { get; set; }
    public Permesso Permesso { get; set; } = null!;
}
