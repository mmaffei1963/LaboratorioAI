using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Report dei campioni con NonFatturare attivo nel periodo. Sola lettura.</summary>
public interface IRepositoryNonFatturare
{
    Task<EsitoNonFatturare> InterrogaAsync(
        CriteriNonFatturare criteri, CancellationToken ct = default);
}
