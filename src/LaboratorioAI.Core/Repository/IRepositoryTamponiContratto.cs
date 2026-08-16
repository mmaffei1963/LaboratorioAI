using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Report dei tamponi (IdControllo 180/203) fatturabili in un periodo, con
/// verifica del contratto di consulenza HACCP che li comprende. Sola lettura.
/// </summary>
public interface IRepositoryTamponiContratto
{
    /// <summary>Dettaglio per tampone e riepilogo per insediamento nel periodo indicato.</summary>
    Task<EsitoTamponiContratto> InterrogaAsync(
        CriteriTamponiContratto criteri, CancellationToken ct = default);
}
