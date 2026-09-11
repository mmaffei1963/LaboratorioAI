using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Interrogazione delle prove affidate a laboratori esterni.</summary>
public interface IRepositoryLaboratoriEsterni
{
    /// <summary>Laboratori esterni a cui risulta assegnato almeno un metodo.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> LaboratoriAsync(CancellationToken ct = default);

    /// <summary>
    /// Tutti i laboratori esterni con la relativa frase (LabEsterno.Frase), da usare
    /// nei preventivi per comporre i metodi in subappalto a campo libero.
    /// </summary>
    Task<IReadOnlyList<VoceLaboratorioEsterno>> LaboratoriConFraseAsync(CancellationToken ct = default);

    /// <summary>Prove e riepilogo per metodo nel periodo indicato.</summary>
    Task<EsitoLaboratoriEsterni> InterrogaAsync(
        CriteriLaboratoriEsterni criteri, CancellationToken ct = default);
}
