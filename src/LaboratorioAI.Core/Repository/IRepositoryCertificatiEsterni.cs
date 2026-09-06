using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Repository;

/// <summary>
/// Lettura da ArcLab (sola lettura) dei dati del campione a partire dal nostro
/// lotto/campione, per comporre il nome del certificato esterno.
/// </summary>
public interface IRepositoryCertificatiEsterni
{
    /// <summary>
    /// Cerca il campione ArcLab per lotto/campione (numerici). L'anno è opzionale:
    /// se assente si prende il più recente. Ritorna null se non trovato.
    /// </summary>
    Task<CampioneArcLab?> CercaCampioneAsync(string lotto, string campione, int? anno, CancellationToken ct = default);
}
