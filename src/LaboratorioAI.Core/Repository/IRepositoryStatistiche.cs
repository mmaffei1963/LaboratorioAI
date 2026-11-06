using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Ricerca trasversale a livello di prova (parametro analizzato). Sola lettura su ArcLab.</summary>
public interface IRepositoryStatistiche
{
    Task<EsitoStatistiche> InterrogaAsync(CriteriStatistiche criteri, CancellationToken ct = default);
}
