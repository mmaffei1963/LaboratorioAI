using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Portale.Autenticazione;
using LaboratorioAI.Portale.Servizi;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════
// LOGGING
// Il filtro sulle credenziali è applicato a tutti i log, di ogni livello.
// ═══════════════════════════════════════════════════════════════════
builder.Host.UseSerilog((contesto, servizi, config) =>
{
    config
        .ReadFrom.Configuration(contesto.Configuration)
        .ReadFrom.Services(servizi)
        .Enrich.FromLogContext()
        .Enrich.With(new FiltroLogCredenziali());
    // 13/10/2026: in sviluppo i messaggi anche nella finestra (es. "Now listening on http://localhost:5081"),
    // come la Web; in produzione solo il file di log.
    if (contesto.HostingEnvironment.IsDevelopment())
        config.WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
});

// ═══════════════════════════════════════════════════════════════════
// DATI
// ═══════════════════════════════════════════════════════════════════
builder.Services.AddDbContext<ContestoLaboratorioAI>(opzioni =>
    opzioni.UseSqlServer(
        builder.Configuration.GetConnectionString("LaboratorioAI"),
        sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

builder.Services.AddSingleton<IFabbricaConnessioniArcLab, FabbricaConnessioniArcLab>();
builder.Services.AddScoped<IRepositoryPortale, RepositoryPortale>();
// Approvazione delle Condizioni Generali (pagina /Firma, 10/10/2026): registro delle evidenze a impronte concatenate.
builder.Services.AddScoped<IRegistroEvidenzeApprovazioni, RegistroEvidenzeApprovazioni>();
// Invio del codice OTP delle Condizioni (15/10/2026): stessa configurazione Smtp della Web app.
builder.Services.AddSingleton<IServizioEmail, LaboratorioAI.Dati.Email.ServizioEmailSmtp>();

// ═══════════════════════════════════════════════════════════════════
// SESSIONI
// ═══════════════════════════════════════════════════════════════════
builder.Services.Configure<OpzioniPortale>(
    builder.Configuration.GetSection(OpzioniPortale.Sezione));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContestoSessione, ContestoSessione>();
builder.Services.AddScoped<IServizioAccessoPortale, ServizioAccessoPortale>();

builder.Services.AddHostedService<PuliziaSessioni>();

builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ContestoLaboratorioAI>("database");

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════
// PIPELINE
// ═══════════════════════════════════════════════════════════════════

// Il portale è esposto su internet: HSTS e reindirizzamento sono attivi
// in ogni ambiente diverso dallo sviluppo.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Errore");
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Cloudflare inoltra l'indirizzo reale del visitatore nelle intestazioni:
// senza questa configurazione la protezione da tentativi ripetuti
// vedrebbe un unico indirizzo per tutti.
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
});

app.UseSerilogRequestLogging(opzioni =>
{
    // Il codice del link personale di /Firma e /Indica non deve finire nei log: il percorso è registrato mascherato (10-11/10/2026).
    opzioni.MessageTemplate = "{RequestMethod} {PercorsoRegistrato} → {StatusCode} in {Elapsed:0} ms";
    opzioni.EnrichDiagnosticContext = (diagnostica, http) =>
        diagnostica.Set("PercorsoRegistrato", System.Text.RegularExpressions.Regex.Replace(
            http.Request.Path.Value ?? "", @"^/(Firma|Indica)/[^/]+", "/$1/***", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
});

app.UseStaticFiles();
app.UseRouting();

app.UseMiddleware<MiddlewareSessione>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/stato");

app.Run();


/// <summary>
/// Rimuove periodicamente le sessioni scadute da tempo.
/// Senza questa pulizia la tabella crescerebbe indefinitamente.
/// </summary>
public class PuliziaSessioni : BackgroundService
{
    private readonly IServiceProvider _servizi;
    private readonly ILogger<PuliziaSessioni> _log;

    public PuliziaSessioni(IServiceProvider servizi, ILogger<PuliziaSessioni> log)
    {
        _servizi = servizi;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ambito = _servizi.CreateScope();
                var accesso = ambito.ServiceProvider.GetRequiredService<IServizioAccessoPortale>();

                var rimosse = await accesso.PuliziaAsync(ct);
                if (rimosse > 0)
                    _log.LogInformation("Rimosse {Numero} sessioni scadute.", rimosse);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Pulizia delle sessioni non riuscita.");
            }

            await Task.Delay(TimeSpan.FromHours(6), ct);
        }
    }
}
