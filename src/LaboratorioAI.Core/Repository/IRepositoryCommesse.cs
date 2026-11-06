using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Interrogazione delle commesse su ArcLab.</summary>
public interface IRepositoryCommesse
{
    Task<IReadOnlyList<VoceAnagrafica>> TipiCommessaAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Commessa>> CercaAsync(
        CriteriRicercaCommesse criteri, CancellationToken ct = default);

    /// <summary>
    /// Prezzo del controllo applicato al cliente sulla commessa di laboratorio in vigore
    /// (DataScadenzaInc IS NULL). Null se il cliente non ha una commessa attiva su quel controllo.
    /// Usato come prezzo base preferenziale nei preventivi.
    /// </summary>
    /// <param name="idInsediamento">05/11/2026: se indicato, solo le commesse di quell'insediamento.</param>
    Task<decimal?> PrezzoControlloClienteAsync(
        int idCliente, int idControllo, CancellationToken ct = default, int? idInsediamento = null);

    /// <summary>
    /// 27/10/2026: per ogni controllo, prezzo minimo e massimo nelle commesse di laboratorio ancora attive
    /// (DataScadenzaInc IS NULL, prezzo &gt; 0) di tutti i clienti, e quante sono. Per orientare il prezzo nei preventivi.
    /// </summary>
    Task<IReadOnlyList<PrezziControlloCommesse>> PrezziControlliCommesseAttiveAsync(CancellationToken ct = default);

    /// <summary>
    /// 05/11/2026: controlli e prezzi delle commesse di laboratorio in vigore del cliente con questo codice
    /// contabilità (Clienti.CodContabilita), es. «MAF2» = Spettabile cliente, per le richieste dei privati.
    /// </summary>
    Task<IReadOnlyList<ControlloCommessaCliente>> ControlliCommesseClienteAsync(string codContabilita, CancellationToken ct = default);

    /// <summary>Campioni collegati a una commessa di laboratorio.</summary>
    Task<IReadOnlyList<CampioneDiCommessa>> CampioniAsync(
        int idCommessa, CancellationToken ct = default);

    /// <summary>Prestazioni erogate nell'ambito di una commessa di consulenza.</summary>
    Task<IReadOnlyList<PrestazioneDiCommessa>> PrestazioniAsync(
        int idCommessa, CancellationToken ct = default);
}

/// <summary>Controllo di una commessa di laboratorio in vigore, con il suo prezzo (senza IVA).</summary>
public record ControlloCommessaCliente(int IdCommessa, int IdControllo, string Controllo, decimal Prezzo, string? CodCommessa);

/// <summary>Prezzo minimo e massimo di un controllo nelle commesse di laboratorio attive (27/10/2026).</summary>
public record PrezziControlloCommesse(int IdControllo, decimal Minimo, decimal Massimo, int Commesse);
