using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Invio massivo dei link di approvazione (10/10/2026). Le richieste messe in coda dalla pagina «Condizioni e firme»
/// (RichiestaApprovazione.InvioAccodato) partono una alla volta, con una pausa tra un'e-mail e l'altra e un tetto
/// giornaliero (Condizioni:InviiMassimiGiorno, predefinito 1000; piano Brevo Starter 20.000/mese): quello che
/// avanza parte il giorno dopo. Un invio non riuscito resta in coda e si ritenta dopo 30 minuti.
/// 11/10/2026: lo stesso giro elabora le risposte agli inviti a indicare il Firmatario, invia gli inviti in coda e i
/// promemoria (Condizioni:PromemoriaGiorni, predefinito 7 e 14 giorni); inviti e promemoria solo nei feriali 8-18.
/// </summary>
public sealed class ServizioInvioLinkMassivo : BackgroundService
{
    private static readonly TimeSpan Intervallo = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PausaTraInvii = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan AttesaDopoErrore = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _servizi;
    private readonly IConfiguration _config;
    private readonly ILogger<ServizioInvioLinkMassivo> _log;

    public ServizioInvioLinkMassivo(IServiceProvider servizi, IConfiguration config, ILogger<ServizioInvioLinkMassivo> log)
    {
        _servizi = servizi; _config = config; _log = log;
    }

    public int LimiteGiornaliero => _config.GetValue("Condizioni:InviiMassimiGiorno", 1000);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(40), ct);
        while (!ct.IsCancellationRequested)
        {
            try { await GiroAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Invio massivo dei link: giro non riuscito.");
            }
            await Task.Delay(Intervallo, ct);
        }
    }

    /// <summary>Inizio della giornata di oggi (ora italiana) in UTC.</summary>
    public static DateTime InizioOggiUtc() => DateTime.Today.ToUniversalTime();

    /// <summary>E-mail automatiche partite oggi (link, inviti e promemoria): contano tutte nel tetto giornaliero.</summary>
    public static async Task<int> InviatiOggiAsync(ContestoLaboratorioAI ctx, CancellationToken ct)
    {
        var oggi = InizioOggiUtc();
        return await ctx.RichiesteApprovazione.CountAsync(x => x.DataInvio >= oggi, ct)
             + await ctx.RichiesteApprovazione.CountAsync(x => x.DataUltimoPromemoria >= oggi, ct)
             + await ctx.InvitiFirmatario.CountAsync(x => x.DataInvio >= oggi, ct)
             + await ctx.InvitiFirmatario.CountAsync(x => x.DataUltimoPromemoria >= oggi, ct);
    }

    /// <summary>Inviti e promemoria partono solo nei giorni feriali, dalle 8 alle 18 (ora italiana).</summary>
    private static bool OrarioUfficio()
    {
        var ora = DateTime.Now;
        return ora.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && ora.Hour >= 8 && ora.Hour < 18;
    }

    private static readonly OperatoreApprovazioni Operatore = new(null, "LaboratorioAI (invio automatico)", null, null);

    private async Task GiroAsync(CancellationToken ct)
    {
        // 1) Risposte dei clienti agli inviti: Firmatario designato e link inviato subito (fuori dal tetto giornaliero).
        await ElaboraIndicazioniAsync(ct);

        using var ambito = _servizi.CreateScope();
        var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
        var disponibili = LimiteGiornaliero - await InviatiOggiAsync(ctx, ct);
        if (disponibili <= 0) return;

        // 2) Link di approvazione in coda.
        disponibili -= await InviaRichiesteInCodaAsync(ctx, disponibili, ct);
        if (disponibili <= 0 || !OrarioUfficio()) return;

        // 3) Inviti a indicare il Firmatario in coda.
        disponibili -= await InviaInvitiInCodaAsync(ctx, disponibili, ct);
        if (disponibili <= 0) return;

        // 4) Promemoria (inviti senza risposta e richieste non ancora approvate).
        await PromemoriaAsync(ctx, disponibili, ct);
    }

    private async Task ElaboraIndicazioniAsync(CancellationToken ct)
    {
        using var ambito = _servizi.CreateScope();
        var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
        var limiteErrore = DateTime.UtcNow - AttesaDopoErrore;
        var ids = await ctx.InvitiFirmatario.AsNoTracking()
            .Where(x => x.Stato == StatoInvitoFirmatario.Indicato && (x.ErroreElaborazione == null || x.DataUltimaModifica < limiteErrore))
            .OrderBy(x => x.DataIndicazione).Select(x => x.IdInvitoFirmatario).Take(50).ToListAsync(ct);
        foreach (var id in ids)
        {
            using var ambitoUno = _servizi.CreateScope();
            try
            {
                var esito = await ambitoUno.ServiceProvider.GetRequiredService<ServizioInvitiFirmatario>().ElaboraAsync(id, Operatore, ct);
                if (!esito.Riuscito) _log.LogWarning("Invito {Id}: Firmatario indicato ma link non inviato: {Motivo}", id, esito.Messaggio);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Invito {Id}: elaborazione della risposta non riuscita.", id);
                using var ambitoErrore = _servizi.CreateScope();
                var c = ambitoErrore.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
                var x = await c.InvitiFirmatario.FirstAsync(q => q.IdInvitoFirmatario == id, ct);
                x.ErroreElaborazione = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                x.DataUltimaModifica = DateTime.UtcNow;
                await c.SaveChangesAsync(ct);
            }
        }
    }

    private async Task<int> InviaRichiesteInCodaAsync(ContestoLaboratorioAI ctx, int disponibili, CancellationToken ct)
    {
        var limiteErrore = DateTime.UtcNow - AttesaDopoErrore;
        var ids = await ctx.RichiesteApprovazione.AsNoTracking()
            .Where(x => x.InvioAccodato != null && (x.ErroreInvioLink == null || x.DataUltimaModifica < limiteErrore))
            .OrderBy(x => x.InvioAccodato).ThenBy(x => x.IdRichiestaApprovazione)
            .Select(x => new { x.IdRichiestaApprovazione, x.Stato }).Take(disponibili).ToListAsync(ct);
        var inviati = 0;
        foreach (var r in ids)
        {
            using var ambitoInvio = _servizi.CreateScope();
            var ctxInvio = ambitoInvio.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
            if (r.Stato != StatoRichiestaApprovazione.DaInviare)
            {
                // Annullata, già inviata a mano o approvata nel frattempo: esce dalla coda.
                var x = await ctxInvio.RichiesteApprovazione.FirstAsync(q => q.IdRichiestaApprovazione == r.IdRichiestaApprovazione, ct);
                x.InvioAccodato = null; x.ErroreInvioLink = null;
                await ctxInvio.SaveChangesAsync(ct);
                continue;
            }
            var esito = await ambitoInvio.ServiceProvider.GetRequiredService<ServizioApprovazioni>()
                .InviaLinkAsync(r.IdRichiestaApprovazione, Operatore, ct);
            if (!esito.Riuscito)
            {
                _log.LogWarning("Invio massivo: richiesta {Id} non inviata: {Motivo}", r.IdRichiestaApprovazione, esito.Messaggio);
                ctxInvio.ChangeTracker.Clear();
                var x = await ctxInvio.RichiesteApprovazione.FirstAsync(q => q.IdRichiestaApprovazione == r.IdRichiestaApprovazione, ct);
                x.ErroreInvioLink = esito.Messaggio.Length > 1000 ? esito.Messaggio[..1000] : esito.Messaggio;
                x.DataUltimaModifica = DateTime.UtcNow;
                await ctxInvio.SaveChangesAsync(ct);
            }
            else
            {
                inviati++;
                var x = await ctxInvio.RichiesteApprovazione.FirstAsync(q => q.IdRichiestaApprovazione == r.IdRichiestaApprovazione, ct);
                if (x.ErroreInvioLink is not null) { x.ErroreInvioLink = null; await ctxInvio.SaveChangesAsync(ct); }
            }
            await Task.Delay(PausaTraInvii, ct);
        }
        return inviati;
    }

    private async Task<int> InviaInvitiInCodaAsync(ContestoLaboratorioAI ctx, int disponibili, CancellationToken ct)
    {
        var limiteErrore = DateTime.UtcNow - AttesaDopoErrore;
        var ids = await ctx.InvitiFirmatario.AsNoTracking()
            .Where(x => x.InvioAccodato != null && (x.ErroreInvio == null || x.DataUltimaModifica < limiteErrore))
            .OrderBy(x => x.InvioAccodato).ThenBy(x => x.IdInvitoFirmatario)
            .Select(x => new { x.IdInvitoFirmatario, x.Stato }).Take(disponibili).ToListAsync(ct);
        var inviati = 0;
        foreach (var i in ids)
        {
            using var ambitoInvio = _servizi.CreateScope();
            var ctxInvio = ambitoInvio.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
            if (i.Stato != StatoInvitoFirmatario.DaInviare)
            {
                var x = await ctxInvio.InvitiFirmatario.FirstAsync(q => q.IdInvitoFirmatario == i.IdInvitoFirmatario, ct);
                x.InvioAccodato = null; x.ErroreInvio = null;
                await ctxInvio.SaveChangesAsync(ct);
                continue;
            }
            var esito = await ambitoInvio.ServiceProvider.GetRequiredService<ServizioInvitiFirmatario>()
                .InviaAsync(i.IdInvitoFirmatario, false, Operatore, ct);
            if (!esito.Riuscito)
            {
                _log.LogWarning("Invito {Id} non inviato: {Motivo}", i.IdInvitoFirmatario, esito.Messaggio);
                ctxInvio.ChangeTracker.Clear();
                var x = await ctxInvio.InvitiFirmatario.FirstAsync(q => q.IdInvitoFirmatario == i.IdInvitoFirmatario, ct);
                x.ErroreInvio = esito.Messaggio.Length > 1000 ? esito.Messaggio[..1000] : esito.Messaggio;
                x.DataUltimaModifica = DateTime.UtcNow;
                await ctxInvio.SaveChangesAsync(ct);
            }
            else inviati++;
            await Task.Delay(PausaTraInvii, ct);
        }
        return inviati;
    }

    private async Task PromemoriaAsync(ContestoLaboratorioAI ctx, int disponibili, CancellationToken ct)
    {
        using var ambitoGiorni = _servizi.CreateScope();
        var giorni = ambitoGiorni.ServiceProvider.GetRequiredService<ServizioInvitiFirmatario>().GiorniPromemoria;
        if (giorni.Count == 0) return;
        var ora = DateTime.UtcNow;
        var limiteErrore = ora - AttesaDopoErrore;

        // Scadenza del promemoria n = data del primo invio + giorni[n].
        var inviti = (await ctx.InvitiFirmatario.AsNoTracking()
                .Where(x => (x.Stato == StatoInvitoFirmatario.Inviato || x.Stato == StatoInvitoFirmatario.Aperto)
                    && x.DataInvio != null && x.NumeroPromemoria < giorni.Count && (x.ErroreInvio == null || x.DataUltimaModifica < limiteErrore))
                .Select(x => new { x.IdInvitoFirmatario, x.DataInvio, x.NumeroPromemoria }).ToListAsync(ct))
            .Where(x => x.DataInvio!.Value.AddDays(giorni[x.NumeroPromemoria]) <= ora).Take(disponibili).ToList();
        foreach (var i in inviti)
        {
            using var a = _servizi.CreateScope();
            var esito = await a.ServiceProvider.GetRequiredService<ServizioInvitiFirmatario>().InviaAsync(i.IdInvitoFirmatario, true, Operatore, ct);
            if (!esito.Riuscito) _log.LogWarning("Promemoria invito {Id} non inviato: {Motivo}", i.IdInvitoFirmatario, esito.Messaggio);
            disponibili--;
            await Task.Delay(PausaTraInvii, ct);
        }
        if (disponibili <= 0) return;

        var richieste = (await ctx.RichiesteApprovazione.AsNoTracking()
                .Where(x => (x.Stato == StatoRichiestaApprovazione.Inviata || x.Stato == StatoRichiestaApprovazione.Aperta)
                    && x.DataInvio != null && x.NumeroPromemoria < giorni.Count && (x.ErroreInvioLink == null || x.DataUltimaModifica < limiteErrore))
                .Select(x => new { x.IdRichiestaApprovazione, x.DataInvio, x.NumeroPromemoria, x.DataApertura }).ToListAsync(ct))
            // Chi ha aperto la pagina nelle ultime 24 ore potrebbe starla compilando: si aspetta.
            .Where(x => x.DataInvio!.Value.AddDays(giorni[x.NumeroPromemoria]) <= ora && (x.DataApertura is null || x.DataApertura < ora.AddDays(-1)))
            .Take(disponibili).ToList();
        foreach (var r in richieste)
        {
            using var a = _servizi.CreateScope();
            var esito = await a.ServiceProvider.GetRequiredService<ServizioApprovazioni>().InviaLinkAsync(r.IdRichiestaApprovazione, Operatore, ct, promemoria: true);
            if (!esito.Riuscito) _log.LogWarning("Promemoria richiesta {Id} non inviato: {Motivo}", r.IdRichiestaApprovazione, esito.Messaggio);
            await Task.Delay(PausaTraInvii, ct);
        }
    }
}
