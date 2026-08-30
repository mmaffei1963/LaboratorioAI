using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Accesso in sola lettura ai campioni su ArcLab.
///
/// Nessun metodo di scrittura è previsto né deve essere aggiunto:
/// ArcLab è il gestionale di riferimento e il sistema non vi scrive mai.
/// </summary>
public interface IRepositoryCampioni
{
    /// <summary>Campioni con tutte le prove verificate ma privi di certificato.</summary>
    Task<IReadOnlyList<CampioneDaValidare>> DaValidareAsync(CancellationToken ct = default);

    /// <summary>
    /// Campione con l'elenco completo delle prove. Nullo se inesistente.
    /// Con <paramref name="includiProveEscluse"/> a true include anche la riga
    /// normalmente esclusa (legionella, IdRigaMetodo 18963): serve al certificato,
    /// dove i valori di legionella devono comparire.
    /// </summary>
    Task<CampioneCompleto?> CompletoAsync(
        int idCampione, CancellationToken ct = default, bool includiProveEscluse = false);

    /// <summary>Ricerca nell'archivio storico secondo i criteri indicati.</summary>
    Task<EsitoRicerca<RisultatoRicercaCampione>> CercaAsync(
        CriteriRicercaCampioni criteri, CancellationToken ct = default);

    /// <summary>
    /// Risultati completi (dati rapporto di prova) per i campioni filtrati, solo certificati:
    /// una riga per prova. Passthrough dinamico (le colonne del SELECT sono le colonne dell'export).
    /// </summary>
    Task<IReadOnlyList<IDictionary<string, object>>> RisultatiRdpAsync(
        CriteriRicercaCampioni criteri, CancellationToken ct = default);

    /// <summary>Totali complessivi (conteggio e somma prezzo) sull'intero risultato della ricerca.</summary>
    Task<TotaliRicerca> TotaliRicercaAsync(
        CriteriRicercaCampioni criteri, CancellationToken ct = default);

    /// <summary>Identificativi tecnici a partire dai codici lotto e campione.</summary>
    Task<IdentificativiCampione?> IdentificativiAsync(
        int codiceLotto, int codiceCampione, CancellationToken ct = default);
}

/// <summary>Identificativi tecnici di un campione, per diagnosi e assistenza.</summary>
public record IdentificativiCampione
{
    public int IdLotto { get; init; }
    public int IdCampione { get; init; }
    public int? IdCommessa { get; init; }
    public int? IdControllo { get; init; }
    public int? IdCertificato { get; init; }
    public string? NumeroCertificato { get; init; }
    public DateTime? DataCertificato { get; init; }
    public bool AnalisiChiusa { get; init; }
    public int NumeroProve { get; init; }
    public int ProveVerificate { get; init; }
    public string? LaboratoriEsterni { get; init; }
}
