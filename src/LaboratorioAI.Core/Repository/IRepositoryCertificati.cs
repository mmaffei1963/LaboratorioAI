using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Accesso in sola lettura ai certificati emessi.</summary>
public interface IRepositoryCertificati
{
    /// <summary>Ricerca libera su numero certificato, cliente o denominazione campione.</summary>
    Task<IReadOnlyList<RisultatoRicercaCertificato>> CercaAsync(
        string testo, int limite = 100, CancellationToken ct = default);

    /// <summary>Certificato con dati del campione e prove. Nullo se inesistente.</summary>
    Task<CertificatoCompleto?> CompletoAsync(int idCertificato, CancellationToken ct = default);
}
