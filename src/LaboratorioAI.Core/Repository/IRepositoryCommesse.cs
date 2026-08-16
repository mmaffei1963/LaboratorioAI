using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Interrogazione delle commesse su ArcLab.</summary>
public interface IRepositoryCommesse
{
    Task<IReadOnlyList<VoceAnagrafica>> TipiCommessaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Commessa>> CercaAsync(
        CriteriRicercaCommesse criteri, CancellationToken ct = default);

    /// <summary>Campioni collegati a una commessa di laboratorio.</summary>
    Task<IReadOnlyList<CampioneDiCommessa>> CampioniAsync(
        int idCommessa, CancellationToken ct = default);

    /// <summary>Prestazioni erogate nell'ambito di una commessa di consulenza.</summary>
    Task<IReadOnlyList<PrestazioneDiCommessa>> PrestazioniAsync(
        int idCommessa, CancellationToken ct = default);
}
