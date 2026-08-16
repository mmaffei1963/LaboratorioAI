using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Esecuzione e archiviazione delle analisi AI.
///
/// Il servizio verifica sempre l'archivio prima di interrogare il modello:
/// un'analisi già eseguita viene restituita senza costo aggiuntivo, salvo
/// richiesta esplicita di riesecuzione.
/// </summary>
public interface IServizioAnalisi
{
    /// <summary>Esegue o recupera l'analisi di un campione.</summary>
    Task<EsitoAnalisi> AnalizzaAsync(RichiestaAnalisi richiesta, CancellationToken ct = default);

    /// <summary>Analizza più campioni, riusando le analisi già in archivio.</summary>
    Task<EsitoAnalisiMassiva> AnalizzaMassivoAsync(
        IReadOnlyList<int> idCampioni,
        IProgress<int>? avanzamento = null,
        CancellationToken ct = default);

    /// <summary>Ultima analisi archiviata per ciascun campione indicato.</summary>
    Task<IReadOnlyDictionary<int, SintesiAnalisi>> ArchivioAsync(
        IReadOnlyList<int> idCampioni, CancellationToken ct = default);

    /// <summary>Analisi archiviata più recente di un campione. Nulla se assente.</summary>
    Task<EsitoAnalisi?> UltimaAsync(int idCampione, CancellationToken ct = default);

    /// <summary>Storico completo delle analisi di un campione.</summary>
    Task<IReadOnlyList<SintesiAnalisi>> StoricoAsync(int idCampione, CancellationToken ct = default);
}
