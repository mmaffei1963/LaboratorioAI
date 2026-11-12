using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Interrogazione della struttura di metodi e controlli su ArcLab.</summary>
public interface IRepositoryMetodi
{
    /// <summary>
    /// Metodi selezionabili nella ricerca.
    /// Restituisce solo quelli con almeno una procedura attiva: i metodi
    /// dismessi non devono comparire fra i suggerimenti.
    /// </summary>
    Task<IReadOnlyList<MetodoRicercabile>> MetodiAttiviAsync(CancellationToken ct = default);

    /// <summary>Elenco dei controlli attivi.</summary>
    Task<IReadOnlyList<VoceAnagrafica>> ControlliAsync(CancellationToken ct = default);

    /// <summary>Controlli che soddisfano la logica indicata sui metodi selezionati.</summary>
    Task<IReadOnlyList<ControlloTrovato>> ControlliPerMetodiAsync(
        IReadOnlyList<int> idMetodi,
        LogicaRicercaControlli logica,
        CancellationToken ct = default);

    /// <summary>Metodi che compongono un controllo.</summary>
    Task<IReadOnlyList<MetodoDelControllo>> MetodiDelControlloAsync(
        int idControllo, CancellationToken ct = default);

    /// <summary>Quadro dei prezzi di un controllo sulle commesse in vigore.</summary>
    Task<PrezziControllo> PrezziControlloAsync(
        int idControllo, CancellationToken ct = default);

    /// <summary>12/11/2026: matrici di ArcLab (per la vista «Controlli per matrice»).</summary>
    Task<IReadOnlyList<VoceAnagrafica>> MatriciAsync(CancellationToken ct = default);

    /// <summary>
    /// 12/11/2026: campioni ricevuti nel periodo con una delle matrici indicate, raggruppati per commessa. Solo commesse
    /// di laboratorio attive (senza scadenza incarico), con cliente, esclusa MARE.A stessa (MIRKO1).
    /// </summary>
    Task<IReadOnlyList<CampioniCommessaMatrice>> CampioniPerMatriciAsync(
        IReadOnlyList<int> idMatrici, DateTime dal, DateTime al, CancellationToken ct = default);
}
