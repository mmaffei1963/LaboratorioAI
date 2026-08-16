using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Dati anagrafici delle strutture per la compilazione dei DVR.</summary>
public interface IRepositoryDvr
{
    /// <summary>
    /// Strutture che risultano soggette a controllo legionella,
    /// individuate dalla presenza di commesse con controlli pertinenti.
    /// </summary>
    Task<IReadOnlyList<StrutturaDvr>> StruttureAsync(
        string? filtro = null, CancellationToken ct = default);

    /// <summary>Dati anagrafici di una struttura, per il precaricamento della scheda.</summary>
    Task<StrutturaDvr?> StrutturaAsync(int idInsediamento, CancellationToken ct = default);

    /// <summary>
    /// Punti di prelievo già utilizzati per la struttura.
    /// Propone il piano di campionamento sulla base dello storico.
    /// </summary>
    Task<IReadOnlyList<PuntoPrelievoDvr>> PuntiStoriciAsync(
        int idInsediamento, CancellationToken ct = default);
}

/// <summary>Struttura anagrafica utilizzabile come base per un DVR.</summary>
public record StrutturaDvr
{
    public int IdCliente { get; init; }
    public int IdInsediamento { get; init; }
    public string RagioneSociale { get; init; } = "";
    public string? NomeInsediamento { get; init; }
    public string? Indirizzo { get; init; }
    public string? Comune { get; init; }
    public string? Provincia { get; init; }
    public string? Cap { get; init; }
    public string? PartitaIva { get; init; }
    public string? Email { get; init; }
    public string? Telefono { get; init; }

    /// <summary>Ultimo campionamento legionella registrato per la struttura.</summary>
    public DateTime? UltimoCampionamento { get; init; }

    /// <summary>
    /// Regione dedotta dalla provincia dell'insediamento.
    /// Determina il modello documentale proposto.
    /// </summary>
    public RegioneDvr? RegionePresunta => Provincia?.ToUpperInvariant() switch
    {
        "BO" or "FE" or "FC" or "MO" or "PR" or "PC" or "RA" or "RE" or "RN"
            => RegioneDvr.EmiliaRomagna,
        "AN" or "AP" or "FM" or "MC" or "PU"
            => RegioneDvr.Marche,
        _   => null
    };
}
