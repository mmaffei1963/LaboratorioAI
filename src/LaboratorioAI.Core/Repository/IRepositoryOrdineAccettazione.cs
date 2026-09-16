using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Lettura (SOLA LETTURA) da ArcLab dei dati necessari a generare l'Ordine di Accettazione
/// di un lotto già ricevuto. Nessuna scrittura su ArcLab.
/// </summary>
public interface IRepositoryOrdineAccettazione
{
    /// <summary>
    /// Costruisce l'Ordine di Accettazione completo (testata + campioni + analisi) per un IdLotto.
    /// Ritorna null se il lotto non esiste o non ha campioni.
    /// </summary>
    Task<OrdineAccettazione?> LeggiPerLottoAsync(int idLotto, CancellationToken ct = default);

    /// <summary>
    /// Lotti candidati alla generazione dell'Ordine di Accettazione (ricevuti in ArcLab),
    /// filtrabili per intervallo di date di ricezione. Per popolare l'elenco/il bottone.
    /// </summary>
    Task<IReadOnlyList<LottoCandidato>> LottiRicevutiAsync(DateTime? dal, DateTime? al, CancellationToken ct = default);
}

/// <summary>Riga di elenco di un lotto ricevuto, per la scelta in interfaccia.</summary>
public record LottoCandidato
{
    public int IdLotto { get; init; }
    public string CodLotto { get; init; } = "";
    public DateTime? DataRicezione { get; init; }
    public string? Cliente { get; init; }
    public int NumeroCampioni { get; init; }
    /// <summary>Campioni con almeno una prova (parametri) caricata.</summary>
    public int CampioniConParametri { get; init; }

    /// <summary>Pronto = tutti i campioni hanno i parametri caricati.</summary>
    public bool Pronto => NumeroCampioni > 0 && CampioniConParametri >= NumeroCampioni;
}
