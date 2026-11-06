using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Elenchi anagrafici usati per popolare i filtri dell'interfaccia.</summary>
public interface IRepositoryAnagrafiche
{
    Task<IReadOnlyList<VoceAnagrafica>> ClientiAsync(CancellationToken ct = default);

    /// <summary>
    /// Clienti con almeno un campione ricevuto dalla data indicata (Lotto.DataRic), con numero di campioni e data
    /// dell'ultimo: base dell'invio massivo delle richieste di approvazione delle Condizioni (10/10/2026).
    /// </summary>
    Task<IReadOnlyList<ClienteConCampioni>> ClientiConCampioniAsync(DateTime dal, CancellationToken ct = default);

    /// <summary>Insediamenti, opzionalmente filtrati per cliente.</summary>
    Task<IReadOnlyList<VoceInsediamento>> InsediamentiAsync(
        int? idCliente = null, CancellationToken ct = default);

    /// <summary>
    /// Dettaglio anagrafico di un cliente (Partita IVA, a livello Cliente) più gli indirizzi
    /// dei suoi insediamenti (a livello Insediamento: un cliente può averne più di uno).
    /// Usato per precompilare i dati del cliente in Preventivi.
    /// </summary>
    Task<DettaglioCliente?> DettaglioClienteAsync(int idCliente, CancellationToken ct = default);

    /// <summary>
    /// Insediamenti del cliente con indirizzo completo ed email (Insediamento.Email): i recapiti a cui
    /// arrivano le comunicazioni relative ai rapporti di prova, riportati in fondo a offerte/preventivi.
    /// </summary>
    Task<IReadOnlyList<InsediamentoContatto>> InsediamentiConEmailAsync(int idCliente, CancellationToken ct = default);

    Task<IReadOnlyList<VoceAnagrafica>> MatriciAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VoceAnagrafica>> ControlliAsync(CancellationToken ct = default);
    Task<IReadOnlyList<VoceAnagrafica>> LaboratoriEsterniAsync(CancellationToken ct = default);

    /// <summary>Metodi (per il filtro "campioni che applicano questo metodo").</summary>
    Task<IReadOnlyList<VoceAnagrafica>> MetodiAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> PrelevatoriAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> LuoghiPrelievoAsync(CancellationToken ct = default);

    /// <summary>Imballaggi/contenitori di ArcLab (tabella Imballaggio), per le tendine del prelievo vocale.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> ImballaggiAsync(CancellationToken ct = default);

    /// <summary>
    /// Punti di prelievo già usati per ogni insediamento dal <paramref name="dal"/> (i più frequenti,
    /// al massimo <paramref name="perInsediamento"/> per insediamento), per le tendine del prelievo vocale.
    /// </summary>
    Task<IReadOnlyList<PuntoPrelievoUsato>> PuntiPrelievoUsatiAsync(DateTime dal, int perInsediamento, CancellationToken ct = default);

    /// <summary>Luoghi di prelievo già usati per ogni insediamento (stessa logica dei punti).</summary>
    Task<IReadOnlyList<PuntoPrelievoUsato>> LuoghiPrelievoUsatiAsync(DateTime dal, int perInsediamento, CancellationToken ct = default);

    /// <summary>Strumenti interni MARE.A (tabella ArcLab Strumenti), per le tarature.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> StrumentiInterniAsync(CancellationToken ct = default);
}
