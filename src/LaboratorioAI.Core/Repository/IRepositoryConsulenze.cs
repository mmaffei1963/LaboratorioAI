using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Letture ArcLab (sola lettura) per i rapportini di consulenza.</summary>
public interface IRepositoryConsulenze
{
    /// <summary>Insediamenti del cliente con indirizzo ed email.</summary>
    Task<IReadOnlyList<InsediamentoRapportino>> InsediamentiAsync(int idCliente, CancellationToken ct = default);

    /// <summary>Tutte le commesse di consulenza (tipo 5) del cliente, attive e non attive.</summary>
    Task<IReadOnlyList<CommessaConsulenza>> CommesseConsulenzaAsync(int idCliente, CancellationToken ct = default);

    /// <summary>Una commessa di consulenza per id (verifica lato server).</summary>
    Task<CommessaConsulenza?> CommessaAsync(int idCommessa, CancellationToken ct = default);

    /// <summary>Organico in servizio.</summary>
    Task<IReadOnlyList<ConsulenteArcLab>> ConsulentiAsync(CancellationToken ct = default);

    /// <summary>Descrizioni incarico (Tipi_Incarico) con il tipo commessa: base delle attività dei preventivi di consulenza.</summary>
    Task<IReadOnlyList<TipoIncaricoArcLab>> TipiIncaricoAsync(CancellationToken ct = default);

    /// <summary>Referenti (ContattoCliente) di un insediamento.</summary>
    Task<IReadOnlyList<ContattoArcLab>> ContattiAsync(int idInsediamento, CancellationToken ct = default);

    /// <summary>Dati anagrafici del cliente (Clienti) per precompilare un preventivo.</summary>
    Task<AnagraficaClienteArcLab?> AnagraficaClienteAsync(int idCliente, CancellationToken ct = default);
}
