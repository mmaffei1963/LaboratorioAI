using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>Singola facoltà applicativa, verificata sugli endpoint.</summary>
public class Permesso
{
    public int IdPermesso { get; set; }

    /// <summary>Codice nel formato <c>area.funzione</c>. Vedi <see cref="Costanti.Permessi"/>.</summary>
    public string Codice { get; set; } = null!;

    public string Descrizione { get; set; } = null!;
    public AreaFunzionale Area { get; set; }

    public ICollection<RuoloPermesso> Ruoli { get; set; } = new List<RuoloPermesso>();
}
