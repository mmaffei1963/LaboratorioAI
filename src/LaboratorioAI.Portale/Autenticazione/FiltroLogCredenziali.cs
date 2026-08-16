using Serilog.Core;
using Serilog.Events;

namespace LaboratorioAI.Portale.Autenticazione;

/// <summary>
/// Impedisce che le credenziali finiscano nei log.
///
/// Interviene su qualunque proprietà il cui nome richiami una credenziale,
/// sostituendone il valore. Vale per tutti i livelli di log, compresi
/// gli errori: è proprio in caso di eccezione che il rischio è maggiore,
/// perché la serializzazione automatica degli oggetti tende a includere
/// ogni campo.
/// </summary>
public class FiltroLogCredenziali : ILogEventEnricher
{
    private static readonly string[] NomiSensibili =
    [
        "credenziale", "password", "piva", "pwd", "secret",
        "chiaveapi", "apikey", "token"
    ];

    private const string Sostituzione = "[omesso]";

    public void Enrich(LogEvent evento, ILogEventPropertyFactory fabbrica)
    {
        foreach (var proprieta in evento.Properties.ToList())
        {
            if (!NomeSensibile(proprieta.Key)) continue;

            evento.AddOrUpdateProperty(
                fabbrica.CreateProperty(proprieta.Key, Sostituzione));
        }
    }

    private static bool NomeSensibile(string nome) =>
        NomiSensibili.Any(s => nome.Contains(s, StringComparison.OrdinalIgnoreCase));
}
