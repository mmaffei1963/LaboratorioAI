using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Fonte in SOLA LETTURA dei dati di ArcLab (clienti, insediamenti, punti di
/// prelievo storici, analisi piscina già svolte) per la generazione dei
/// manuali/DVR piscine. Nessun metodo di questa interfaccia scrive su ArcLab.
///
/// Chiavi: IdInsediamento e IdCliente sono INT in ArcLab.
/// </summary>
public interface IRepositoryPiscine
{
    /// <summary>Anagrafica dell'insediamento con il cliente collegato. Null se inesistente.</summary>
    Task<AnagraficaPiscina?> LeggiAnagraficaAsync(int idInsediamento, CancellationToken ct = default);

    /// <summary>Insediamenti di un cliente, per la scelta del sito.</summary>
    Task<IReadOnlyList<AnagraficaPiscina>> LeggiInsediamentiClienteAsync(
        int idCliente, CancellationToken ct = default);

    /// <summary>Punti di prelievo storici dell'insediamento (da analisi piscina già svolte).</summary>
    Task<IReadOnlyList<PuntoPrelievo>> LeggiPuntiPrelievoStoriciAsync(
        int idInsediamento, CancellationToken ct = default);

    /// <summary>Ricerca insediamenti per testo libero (ragione sociale, comune, P.IVA).</summary>
    Task<IReadOnlyList<AnagraficaPiscina>> CercaInsediamentiAsync(
        string testo, CancellationToken ct = default);

    /// <summary>
    /// Analisi di piscina già svolte per l'insediamento (matrici 213/214),
    /// dalla più recente. Per precaricare esiti storici e confronto con Tabella A.
    /// </summary>
    Task<IReadOnlyList<AnalisiStoricaPiscina>> LeggiAnalisiStoricheAsync(
        int idInsediamento, CancellationToken ct = default);
}
