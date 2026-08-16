using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Accesso ai dati del portale clienti.
///
/// Tutte le interrogazioni sono vincolate al cliente della sessione:
/// l'identificativo non proviene mai dalla richiesta ma sempre dalla
/// sessione verificata lato server.
/// </summary>
public interface IRepositoryPortale
{
    /// <summary>
    /// Cerca il cliente per codice contabilità e restituisce anche la
    /// credenziale registrata su ArcLab, per il solo confronto in memoria.
    /// </summary>
    Task<(ClientePortale? Cliente, string? Credenziale)> CercaPerAccessoAsync(
        string codiceContabilita, CancellationToken ct = default);

    /// <summary>Elenco clienti, disponibile alle sole sessioni operatore.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> ClientiAsync(CancellationToken ct = default);

    /// <summary>Valori disponibili per i filtri, limitati al cliente indicato.</summary>
    Task<FiltriPortale> FiltriAsync(int? idCliente, CancellationToken ct = default);

    /// <summary>Campioni del cliente indicato secondo i criteri.</summary>
    Task<IReadOnlyList<CampionePortale>> CercaCampioniAsync(
        int? idCliente, CriteriPortale criteri, CancellationToken ct = default);

    /// <summary>
    /// Prove di un campione, con verifica di appartenenza al cliente.
    /// Restituisce elenco vuoto se il campione non appartiene al cliente indicato.
    /// </summary>
    Task<IReadOnlyList<ProvaCampione>> ProveAsync(
        int idCampione, int? idCliente, CancellationToken ct = default);
}

/// <summary>Valori selezionabili nei filtri del portale.</summary>
public record FiltriPortale
{
    public required IReadOnlyList<VoceInsediamento> Insediamenti { get; init; }
    public required IReadOnlyList<VoceAnagrafica> Matrici { get; init; }
    public required IReadOnlyList<VoceAnagrafica> Controlli { get; init; }
}
