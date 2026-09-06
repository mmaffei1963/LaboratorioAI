using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Orchestrazione del triage dei certificati esterni: estrazione AI dal PDF,
/// aggancio ArcLab per lotto/campione, composizione dei nomi proposti e criticità.
/// </summary>
public interface IServizioCertificatiEsterni
{
    Task<AnteprimaCertificatoEsterno> AnalizzaAsync(byte[] pdf, CancellationToken ct = default);
}
