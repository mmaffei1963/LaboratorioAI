using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Ricevute di approvazione delle Condizioni Generali (fase 2, 10/10/2026). Il Portale registra l'approvazione;
/// ogni minuto questo servizio genera la Ricevuta PDF delle approvazioni nuove e la invia per e-mail al Firmatario e
/// all'indirizzo del cliente in anagrafica ArcLab. Così il Portale non ha bisogno né del generatore PDF né della
/// password SMTP. In caso di errore di invio si ritenta ogni 30 minuti.
/// </summary>
public sealed class ServizioRicevuteApprovazione : BackgroundService
{
    private static readonly TimeSpan Intervallo = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan AttesaDopoErrore = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _servizi;
    private readonly IWebHostEnvironment _ambiente;
    private readonly ILogger<ServizioRicevuteApprovazione> _log;

    public ServizioRicevuteApprovazione(IServiceProvider servizi, IWebHostEnvironment ambiente, ILogger<ServizioRicevuteApprovazione> log)
    {
        _servizi = servizi; _ambiente = ambiente; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), ct);   // lascia finire l'avvio (migrazioni)
        while (!ct.IsCancellationRequested)
        {
            try { await GiroAsync(ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Ricevute di approvazione: giro non riuscito.");
            }
            await Task.Delay(Intervallo, ct);
        }
    }

    private async Task GiroAsync(CancellationToken ct)
    {
        using var ambito = _servizi.CreateScope();
        var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
        var limite = DateTime.UtcNow - AttesaDopoErrore;
        var ids = await ctx.RichiesteApprovazione.AsNoTracking()
            .Where(x => x.Stato == StatoRichiestaApprovazione.Approvata && (x.RicevutaPdf == null || x.DataInvioRicevuta == null)
                && (x.ErroreInvioRicevuta == null || x.DataUltimaModifica < limite))
            .OrderBy(x => x.DataPerfezionamento).Select(x => x.IdRichiestaApprovazione).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            using var ambitoRichiesta = _servizi.CreateScope();
            await ElaboraAsync(ambitoRichiesta.ServiceProvider, id, ct);
        }
    }

    private async Task ElaboraAsync(IServiceProvider sp, int id, CancellationToken ct)
    {
        var ctx = sp.GetRequiredService<ContestoLaboratorioAI>();
        var registro = sp.GetRequiredService<IRegistroEvidenzeApprovazioni>();
        var r = await ctx.RichiesteApprovazione.Include(x => x.Revisione).Include(x => x.Firmatario)
            .FirstAsync(x => x.IdRichiestaApprovazione == id, ct);

        if (r.RicevutaPdf is null)
        {
            try { await GeneraAsync(ctx, registro, sp.GetRequiredService<IGeneratoreRicevutaApprovazione>(), r, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Ricevuta della richiesta {Id} non generata.", id);
                ctx.ChangeTracker.Clear();
                var rr = await ctx.RichiesteApprovazione.FirstAsync(x => x.IdRichiestaApprovazione == id, ct);
                rr.ErroreInvioRicevuta = Taglia("Generazione: " + ex.Message);
                rr.DataUltimaModifica = DateTime.UtcNow;
                await ctx.SaveChangesAsync(ct);
                return;
            }
        }

        if (r.DataInvioRicevuta is null)
            await InviaAsync(sp, ctx, registro, r, ct);
    }

    private async Task GeneraAsync(ContestoLaboratorioAI ctx, IRegistroEvidenzeApprovazioni registro,
        IGeneratoreRicevutaApprovazione generatore, RichiestaApprovazione r, CancellationToken ct)
    {
        var eventi = await ctx.EventiApprovazione.AsNoTracking().Where(x => x.IdRichiestaApprovazione == r.IdRichiestaApprovazione)
            .OrderBy(x => x.IdEventoApprovazione).ToListAsync(ct);
        var perfezionamento = eventi.LastOrDefault(x => x.Tipo == "approvazione.perfezionata")
            ?? throw new InvalidOperationException("evento di perfezionamento non trovato nel registro");
        using var dati = JsonDocument.Parse(perfezionamento.Dati ?? "{}");
        var testi = dati.RootElement.GetProperty("Testi");
        var dichiarati = dati.RootElement.GetProperty("Dichiarati");
        DateTime? ora(int n) => eventi.LastOrDefault(x => x.Tipo == $"casella{n}.spuntata" && x.IdEventoApprovazione < perfezionamento.IdEventoApprovazione)?.DataUtc is DateTime d
            ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime() : null;

        var firme = await ctx.FirmeGraficheApprovazione.AsNoTracking().Where(x => x.IdRichiestaApprovazione == r.IdRichiestaApprovazione).ToListAsync(ct);
        var puntatore = firme.FirstOrDefault()?.Puntatore switch { "touch" => "dito", "pen" => "penna", "mouse" => "mouse", var p => p };
        // Firma con codice OTP (dal 15/10/2026): dati dall'evento di perfezionamento.
        string? firmaOtp = null;
        if (dati.RootElement.TryGetProperty("Otp", out var o) && o.ValueKind == JsonValueKind.Object)
        {
            static string Locale(JsonElement e) => e.TryGetDateTime(out var d)
                ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy 'alle' HH:mm:ss") : "—";
            firmaOtp = $"Approvazione firmata elettronicamente con codice monouso (OTP) inviato per {o.GetProperty("Canale").GetString()} " +
                $"all'indirizzo {o.GetProperty("A").GetString()} il {Locale(o.GetProperty("Inviato"))} e inserito dal Firmatario il {Locale(o.GetProperty("Verificato"))}.";
        }
        var modalita = r.Modalita switch
        {
            ModalitaApprovazione.CaselleOtp => "Caselle dal Portale clienti e firma elettronica con codice OTP via e-mail",
            ModalitaApprovazione.FirmaGrafica => $"Caselle dal Portale clienti e firma grafica ({puntatore ?? "mouse o dito"})",
            _ => "Caselle dal Portale clienti, con doppia conferma"
        };

        // Numero progressivo per anno: RA-2026-0001.
        var anno = (r.DataPerfezionamento ?? DateTime.UtcNow).Year;
        var prefisso = $"RA-{anno}-";
        var ultimi = await ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.NumeroRicevuta != null && x.NumeroRicevuta.StartsWith(prefisso))
            .Select(x => x.NumeroRicevuta!).ToListAsync(ct);
        var progressivo = ultimi.Select(x => int.TryParse(x[prefisso.Length..], out var n) ? n : 0).DefaultIfEmpty(0).Max() + 1;
        var numero = $"{prefisso}{progressivo:0000}";

        var pdf = generatore.GeneraPdf(new DatiRicevutaApprovazione
        {
            NumeroRicevuta = numero,
            RagioneSociale = r.ClienteRagioneSociale, PartitaIva = r.ClientePartitaIva,
            NomeCognome = $"{dichiarati.GetProperty("Nome").GetString()} {dichiarati.GetProperty("Cognome").GetString()}",
            Qualifica = dichiarati.GetProperty("Qualifica").GetString() ?? "",
            Email = dichiarati.GetProperty("Email").GetString() ?? "",
            Revisione = r.Revisione!.Codice, ImprontaCondizioni = r.Revisione.ImprontaPdf,
            Offerta = r.OffertaPdf is null ? null : r.OffertaRiferimento, ImprontaOfferta = r.OffertaImpronta,
            DataApprovazione = DateTime.SpecifyKind(r.DataPerfezionamento ?? perfezionamento.DataUtc, DateTimeKind.Utc).ToLocalTime(),
            Modalita = modalita, IndirizzoIp = perfezionamento.IndirizzoIp,
            Casella1 = testi.GetProperty("Casella1").GetString() ?? "", OraCasella1 = ora(1),
            Casella2Introduzione = testi.GetProperty("Casella2").GetString() ?? "",
            Clausole = testi.GetProperty("Clausole").EnumerateArray().Select(x => x.GetString() ?? "").ToList(), OraCasella2 = ora(2),
            Casella3 = testi.GetProperty("Casella3").GetString() ?? "", OraCasella3 = ora(3),
            ImprontaTesti = r.ImprontaTesti ?? "",
            Firma1 = firme.FirstOrDefault(x => x.Riquadro == 1)?.ImmaginePng,
            Firma2 = firme.FirstOrDefault(x => x.Riquadro == 2)?.ImmaginePng,
            FirmaOtp = firmaOtp
        }, _ambiente.WebRootPath);

        r.NumeroRicevuta = numero;
        r.RicevutaPdf = pdf;
        r.RicevutaImpronta = CatenaEvidenze.Sha256(pdf);
        r.ErroreInvioRicevuta = null;
        r.DataUltimaModifica = DateTime.UtcNow;
        await registro.AggiungiAsync("ricevuta.generata", r.IdRichiestaApprovazione, r.IdClienteArcLab,
            new { NumeroRicevuta = numero, Impronta = r.RicevutaImpronta }, "LaboratorioAI (automatico)", null, null, ct);
    }

    private async Task InviaAsync(IServiceProvider sp, ContestoLaboratorioAI ctx, IRegistroEvidenzeApprovazioni registro,
        RichiestaApprovazione r, CancellationToken ct)
    {
        var email = sp.GetRequiredService<IServizioEmail>();
        var anagrafiche = sp.GetRequiredService<IRepositoryAnagrafiche>();
        var destinatari = new List<string>();
        if (!string.IsNullOrWhiteSpace(r.Firmatario?.Email)) destinatari.Add(r.Firmatario.Email.Trim());
        try
        {
            var cliente = await anagrafiche.DettaglioClienteAsync(r.IdClienteArcLab, ct);
            if (!string.IsNullOrWhiteSpace(cliente?.Email))
                destinatari.AddRange(cliente.Email.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains('@')));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "Ricevuta {Numero}: e-mail del cliente non letta da ArcLab, invio solo al Firmatario.", r.NumeroRicevuta);
        }
        destinatari = destinatari.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var oggetto = $"MARE.A – Ricevuta di approvazione delle Condizioni Generali {r.Revisione!.Codice} (n. {r.NumeroRicevuta})";
        var corpo = $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1a1a1a;max-width:640px">
            <p>Gentile Cliente,</p>
            <p>confermiamo che il {DateTime.SpecifyKind(r.DataPerfezionamento!.Value, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy 'alle' HH:mm}
            {System.Net.WebUtility.HtmlEncode($"{r.NomeDichiarato} {r.CognomeDichiarato}")} ha approvato per {System.Net.WebUtility.HtmlEncode(r.ClienteRagioneSociale)}
            le Condizioni Generali di Fornitura {System.Net.WebUtility.HtmlEncode(r.Revisione.Codice)}{(r.OffertaPdf is null ? "" : $" e accettato l'Offerta n. {System.Net.WebUtility.HtmlEncode(r.OffertaRiferimento)}")}.</p>
            <p>In allegato la Ricevuta n. {r.NumeroRicevuta}{(r.OffertaPdf is null ? "" : " e copia dell'Offerta accettata")}. Le Condizioni sono sempre disponibili su www.mare-a.com/condizioni e nel Portale clienti.</p>
            <p>Cordiali saluti<br>MARE.A s.r.l. – Laboratorio di analisi</p>
            </div>
            """;
        var allegati = new List<AllegatoEmail> { new($"Ricevuta_{r.NumeroRicevuta}.pdf", r.RicevutaPdf!, "application/pdf") };
        if (r.OffertaPdf is not null) allegati.Add(new($"Offerta_{(r.OffertaRiferimento ?? "").Split(' ')[0].Replace('/', '-')}.pdf", r.OffertaPdf, "application/pdf"));

        try
        {
            var messageId = await email.InviaConAllegatiAsync(destinatari, oggetto, corpo, allegati, ct,
                ServizioApprovazioni.RispondiA(sp.GetRequiredService<IConfiguration>()));
            r.DataInvioRicevuta = DateTime.UtcNow;
            r.ErroreInvioRicevuta = null;
            r.DataUltimaModifica = r.DataInvioRicevuta.Value;
            await registro.AggiungiAsync("ricevuta.inviata", r.IdRichiestaApprovazione, r.IdClienteArcLab,
                new { r.NumeroRicevuta, Destinatari = destinatari, MessageId = messageId }, "LaboratorioAI (automatico)", null, null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Ricevuta {Numero} non inviata.", r.NumeroRicevuta);
            r.ErroreInvioRicevuta = Taglia("Invio: " + ex.Message);
            r.DataUltimaModifica = DateTime.UtcNow;
            await ctx.SaveChangesAsync(ct);
        }
    }

    private static string Taglia(string s) => s.Length <= 1000 ? s : s[..1000];
}
