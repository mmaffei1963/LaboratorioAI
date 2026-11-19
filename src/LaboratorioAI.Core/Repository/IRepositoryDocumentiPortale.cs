using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>Documenti del portale (19/11/2026): tabella Entries di Marea, SOLA LETTURA.</summary>
public interface IRepositoryDocumentiPortale
{
    /// <summary>
    /// Documenti del cliente ArcLab (tramite Marea.Clienti.IDArcLab), esclusi i bloccati e i certificati senza firma
    /// digitale (DataFirma vuota).
    /// </summary>
    Task<IReadOnlyList<DocumentoMarea>> DocumentiAsync(int idClienteArcLab, CancellationToken ct = default);

    /// <summary>Un documento con le stesse esclusioni, per lo scarico; null se non esiste o non è visibile.</summary>
    Task<DocumentoMarea?> DocumentoAsync(int idEntry, CancellationToken ct = default);
}
