using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Repository;

/// <summary>Lettura delle sigle dei laboratori esterni (tabella certificati.SigleLaboratoriEsterni).</summary>
public interface IRepositorySigleLaboratori
{
    /// <summary>Sigle attive, una per laboratorio ArcLab.</summary>
    Task<IReadOnlyList<SiglaLaboratorioEsterno>> ElencoAttiveAsync(CancellationToken ct = default);
}
