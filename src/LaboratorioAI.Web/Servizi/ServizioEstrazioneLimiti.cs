using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.IA;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Esegue in background le estrazioni dei limiti con l'AI messe in coda dalla pagina «Limiti normativi» (17/10/2026).
/// Una lettura di un testo di legge può durare diversi minuti: oltre i 100 secondi di Cloudflare (errore 524) se fatta
/// nella richiesta web. La pagina controlla lo stato ogni pochi secondi. Un'estrazione rimasta «in corso» per oltre
/// 20 minuti (es. riavvio del sito) passa a «Errore» e si può rilanciare.
/// </summary>
public sealed class ServizioEstrazioneLimiti : BackgroundService
{
    private static readonly TimeSpan Intervallo = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan Abbandonata = TimeSpan.FromMinutes(20);

    private readonly IServiceProvider _servizi;
    private readonly ILogger<ServizioEstrazioneLimiti> _log;

    public ServizioEstrazioneLimiti(IServiceProvider servizi, ILogger<ServizioEstrazioneLimiti> log)
    {
        _servizi = servizi; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await GiroAsync(ct); await RevisioneAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Estrazione limiti normativi: giro non riuscito.");
            }
            await Task.Delay(Intervallo, ct);
        }
    }

    /// <summary>Revisione AI delle schede validate (18/10/2026): un giro in coda alla volta.</summary>
    private async Task RevisioneAsync(CancellationToken ct)
    {
        int idGiro;
        TipoVerificaLimiti tipo;
        using (var ambito = _servizi.CreateScope())
        {
            var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
            var limite = DateTime.UtcNow - TimeSpan.FromMinutes(90);
            foreach (var g in await ctx.GiriVerificaLimiti.Where(g => g.Stato == StatoGiroVerifica.InCorso && g.DataInizio < limite).ToListAsync(ct))
            {
                g.Stato = StatoGiroVerifica.Errore;
                g.Esito = "Revisione interrotta (sito riavviato o tempo scaduto): rilanciarla.";
            }
            await ctx.SaveChangesAsync(ct);
            var prossimo = await ctx.GiriVerificaLimiti.Where(g => g.Stato == StatoGiroVerifica.InCoda
                    && (g.Tipo == TipoVerificaLimiti.RevisioneAi || g.Tipo == TipoVerificaLimiti.AbbinamentoMatrici))
                .OrderBy(g => g.IdGiroVerificaLimiti).FirstOrDefaultAsync(ct);
            if (prossimo is null) return;
            prossimo.Stato = StatoGiroVerifica.InCorso;
            prossimo.DataInizio = DateTime.UtcNow;
            prossimo.Esito = prossimo.Tipo == TipoVerificaLimiti.AbbinamentoMatrici ? "Proposta in corso…" : "Revisione in corso…";
            await ctx.SaveChangesAsync(ct);
            idGiro = prossimo.IdGiroVerificaLimiti;
            tipo = prossimo.Tipo;
        }
        using var lavoro = _servizi.CreateScope();
        try
        {
            if (tipo == TipoVerificaLimiti.AbbinamentoMatrici)
                await lavoro.ServiceProvider.GetRequiredService<ProponiCategorieMatrici>().EseguiAsync(idGiro, ct);
            else
                await lavoro.ServiceProvider.GetRequiredService<RevisoreLimitiNormativi>().EseguiAsync(idGiro, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogError(ex, "Revisione limiti giro {Id} non riuscita.", idGiro);
            using var ambito = _servizi.CreateScope();
            var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
            var g = await ctx.GiriVerificaLimiti.FirstAsync(x => x.IdGiroVerificaLimiti == idGiro, CancellationToken.None);
            g.Stato = StatoGiroVerifica.Errore;
            var m = (tipo == TipoVerificaLimiti.AbbinamentoMatrici ? "Proposta non riuscita: " : "Revisione non riuscita: ") + ex.Message;
            g.Esito = m.Length > 2000 ? m[..2000] : m;
            await ctx.SaveChangesAsync(CancellationToken.None);
        }
    }

    private async Task GiroAsync(CancellationToken ct)
    {
        int? id;
        List<FamigliaNormativa>? famiglie;
        using (var ambito = _servizi.CreateScope())
        {
            var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
            var limite = DateTime.UtcNow - Abbandonata;
            var ferme = await ctx.VersioniFonti.Where(v => v.StatoEstrazione == StatoEstrazioneLimiti.InCorso
                && v.DataEstrazione != null && v.DataEstrazione < limite).ToListAsync(ct);
            foreach (var v in ferme)
            {
                v.StatoEstrazione = StatoEstrazioneLimiti.Errore;
                v.EsitoEstrazione = "Estrazione interrotta (sito riavviato o tempo scaduto): rilanciarla.";
            }
            if (ferme.Count > 0) await ctx.SaveChangesAsync(ct);

            var prossima = await ctx.VersioniFonti
                .Where(v => v.StatoEstrazione == StatoEstrazioneLimiti.InCorso && v.DataEstrazione == null)
                .OrderBy(v => v.IdVersioneFonte).FirstOrDefaultAsync(ct);
            if (prossima is null) return;
            famiglie = LaboratorioAI.Core.Limiti.RegoleLimiti.FamiglieDaCoda(prossima.EsitoEstrazione);
            prossima.DataEstrazione = DateTime.UtcNow;
            prossima.EsitoEstrazione = "L'AI sta leggendo il testo…";
            await ctx.SaveChangesAsync(ct);
            id = prossima.IdVersioneFonte;
        }

        using var ambitoLavoro = _servizi.CreateScope();
        try
        {
            var estrattore = ambitoLavoro.ServiceProvider.GetRequiredService<EstrattoreLimitiNormativi>();
            var esito = await estrattore.EstraiAsync(id.Value, famiglie, ct);
            _log.LogInformation("Estrazione limiti versione {Id}: {Esito}", id, esito.Messaggio);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogError(ex, "Estrazione limiti versione {Id} non riuscita.", id);
            using var ambito = _servizi.CreateScope();
            var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
            var v = await ctx.VersioniFonti.FirstAsync(x => x.IdVersioneFonte == id, CancellationToken.None);
            v.StatoEstrazione = StatoEstrazioneLimiti.Errore;
            var m = "Estrazione non riuscita: " + ex.Message;
            v.EsitoEstrazione = m.Length > 2000 ? m[..2000] : m;
            await ctx.SaveChangesAsync(CancellationToken.None);
        }
    }
}
