using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// «Chiedi alla guida» (11/10/2026): una volta al giorno svuota testo e persona delle domande più vecchie di 12 mesi.
/// La riga resta (data, pagina, voto, costo) per i conteggi: nessuna cancellazione fisica.
/// </summary>
public sealed class ServizioScadenzaDomandeGuida : BackgroundService
{
    private readonly IServiceProvider _servizi;
    private readonly ILogger<ServizioScadenzaDomandeGuida> _log;

    public ServizioScadenzaDomandeGuida(IServiceProvider servizi, ILogger<ServizioScadenzaDomandeGuida> log)
    {
        _servizi = servizi; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), ct);   // lascia finire l'avvio (migrazioni)
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ambito = _servizi.CreateScope();
                var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
                var limite = DateTime.UtcNow.AddMonths(-12);
                var adesso = DateTime.UtcNow;
                var n = await ctx.DomandeGuida
                    .Where(x => x.DataOra < limite && x.DataAnonimizzazione == null)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(x => x.Domanda, "")
                        .SetProperty(x => x.Risposta, (string?)null)
                        .SetProperty(x => x.Errore, (string?)null)
                        .SetProperty(x => x.IdUtente, (int?)null)
                        .SetProperty(x => x.EmailUtente, (string?)null)
                        .SetProperty(x => x.NomeUtente, (string?)null)
                        .SetProperty(x => x.DataAnonimizzazione, adesso), ct);
                if (n > 0) _log.LogInformation("Chiedi alla guida: svuotate {Numero} domande più vecchie di 12 mesi.", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Chiedi alla guida: scadenza delle domande non riuscita.");
            }
            await Task.Delay(TimeSpan.FromHours(24), ct);
        }
    }
}
