using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Cache delle estrazioni AI dei certificati esterni, per impronta (hash) del PDF:
/// evita di ripetere la chiamata AI per lo stesso file. Nessuna sovrascrittura.
/// </summary>
public interface IArchivioEstrazioniCertificato
{
    /// <summary>Estrazione archiviata più recente per l'hash indicato, o null.</summary>
    Task<EstrazioneCertificatoEsterno?> LeggiPerHashAsync(string hash, CancellationToken ct = default);

    /// <summary>Archivia una nuova estrazione (record aggiuntivo, non sovrascrive).</summary>
    Task SalvaAsync(string hash, string? nomeFileOriginale, EstrazioneCertificatoEsterno estrazione,
        string? modelloAi, CancellationToken ct = default);
}
