using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Lettura da ArcLab (sola lettura) dei dati del campione a partire dal nostro
/// lotto/campione, per comporre il nome del certificato esterno.
/// </summary>
public interface IRepositoryCertificatiEsterni
{
    /// <summary>
    /// Cerca il campione ArcLab per lotto/campione (numerici). I numeri di lotto ripartono ogni anno:
    /// se è nota la data di emissione del certificato si prende il lotto ricevuto ENTRO quella data,
    /// il più vicino (evita di agganciare lo stesso numero dell'anno successivo). Senza data: il più recente.
    /// Ritorna null se non trovato.
    /// </summary>
    Task<CampioneArcLab?> CercaCampioneAsync(string lotto, string campione, DateOnly? ricevutoEntro, CancellationToken ct = default);

    /// <summary>Laboratori esterni a cui sono affidate prove del campione (rigametodo.IdLabEsterno).</summary>
    Task<IReadOnlyList<LabEsternoArcLab>> LabEsterniDelCampioneAsync(int idCampione, CancellationToken ct = default);

    /// <summary>
    /// Prove dei campioni indicati (escluse la riga tecnica e le prove escluse dal RdP), con laboratorio
    /// esterno e presenza del risultato: servono a distinguere certificati solo esterni e misti e a
    /// controllare se i valori dei misti sono stati inseriti.
    /// </summary>
    Task<IReadOnlyList<ProvaCampioneArcLab>> ProveDeiCampioniAsync(IEnumerable<int> idCampioni, CancellationToken ct = default);
}
