using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Elenchi anagrafici usati per popolare i filtri dell'interfaccia.</summary>
public interface IRepositoryAnagrafiche
{
    Task<IReadOnlyList<VoceAnagrafica>> ClientiAsync(CancellationToken ct = default);

    /// <summary>Insediamenti, opzionalmente filtrati per cliente.</summary>
    Task<IReadOnlyList<VoceInsediamento>> InsediamentiAsync(
        int? idCliente = null, CancellationToken ct = default);

    Task<IReadOnlyList<VoceAnagrafica>> MatriciAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VoceAnagrafica>> ControlliAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VoceAnagrafica>> LaboratoriEsterniAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> PrelevatoriAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> LuoghiPrelievoAsync(CancellationToken ct = default);

    /// <summary>Strumenti interni MARE.A (tabella ArcLab Strumenti), per le tarature.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> StrumentiInterniAsync(CancellationToken ct = default);
}
