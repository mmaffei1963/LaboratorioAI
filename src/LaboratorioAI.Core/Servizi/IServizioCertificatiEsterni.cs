using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Orchestrazione del triage dei certificati esterni: estrazione AI dal PDF,
/// aggancio ArcLab per lotto/campione, composizione dei nomi proposti e criticità.
/// </summary>
public interface IServizioCertificatiEsterni
{
    /// <param name="pdf">Contenuto del certificato PDF.</param>
    /// <param name="nomeFileOriginale">Nome del file caricato (per l'archivio).</param>
    /// <param name="forza">Se true ignora la cache e riesegue l'estrazione AI.</param>
    Task<AnteprimaCertificatoEsterno> AnalizzaAsync(byte[] pdf, string? nomeFileOriginale = null,
        bool forza = false, CancellationToken ct = default);
}
