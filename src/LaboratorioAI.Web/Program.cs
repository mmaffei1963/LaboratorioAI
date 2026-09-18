using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;
using LaboratorioAI.Documenti;
using LaboratorioAI.Documenti.Dvr;
using LaboratorioAI.Documenti.Piscine;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Autenticazione;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Segreti fuori dal versionamento e dal publish (es. chiave Anthropic): file opzionale
// creato UNA VOLTA sul server; sopravvive ai deploy perché non è nel pacchetto pubblicato.
// In sviluppo restano gli user-secrets; questo file, se assente, viene ignorato.
builder.Configuration.AddJsonFile("appsettings.secrets.json", optional: true, reloadOnChange: true);

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// LOGGING
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
builder.Host.UseSerilog((contesto, servizi, config) => config
    .ReadFrom.Configuration(contesto.Configuration)
    .ReadFrom.Services(servizi)
    .Enrich.FromLogContext());

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// ACCESSO AI DATI
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
builder.Services.AddDbContext<ContestoLaboratorioAI>(opzioni =>
    opzioni.UseSqlServer(
        builder.Configuration.GetConnectionString("LaboratorioAI"),
        sql =>
        {
            sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            sql.CommandTimeout(120);
        }));

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// AUTENTICAZIONE â€” Entra ID
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
builder.Services.AddScoped<ServizioProvisioningUtente>();

var opzioniSviluppo = builder.Configuration
    .GetSection(OpzioniAutenticazioneSviluppo.Sezione)
    .Get<OpzioniAutenticazioneSviluppo>() ?? new OpzioniAutenticazioneSviluppo();

opzioniSviluppo.Verifica(builder.Environment);

builder.Services.Configure<OpzioniAutenticazioneSviluppo>(
    builder.Configuration.GetSection(OpzioniAutenticazioneSviluppo.Sezione));

if (opzioniSviluppo.Abilitato)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine();
    Console.WriteLine("  ============================================================");
    Console.WriteLine("   ACCESSO DI SVILUPPO ATTIVO - nessuna credenziale richiesta");
    Console.WriteLine("   Utente: " + opzioniSviluppo.NomeCompleto);
    Console.WriteLine("  ============================================================");
    Console.WriteLine();
    Console.ResetColor();

    builder.Services
        .AddAuthentication(OpzioniAutenticazioneSviluppo.Schema)
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
                   GestoreAutenticazioneSviluppo>(
            OpzioniAutenticazioneSviluppo.Schema, _ => { });
}
else
{
    builder.Services
        .AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "EntraId");
}

// All'emissione del token: allinea l'utente applicativo e aggiunge i
// claim di permesso. I claim vengono conservati nel cookie di sessione,
// quindi una modifica ai permessi ha effetto al successivo accesso.
builder.Services.Configure<OpenIdConnectOptions>(
    OpenIdConnectDefaults.AuthenticationScheme, opzioni =>
{
    var precedente = opzioni.Events.OnTokenValidated;

    opzioni.Events.OnTokenValidated = async contesto =>
    {
        if (precedente is not null) await precedente(contesto);

        var provisioning = contesto.HttpContext.RequestServices
            .GetRequiredService<ServizioProvisioningUtente>();

        if (contesto.Principal?.Identity is System.Security.Claims.ClaimsIdentity identita)
        {
            await provisioning.AllineaAsync(identita);
        }
    };
});

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// AUTORIZZAZIONE â€” a permessi
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
builder.Services.AddSingleton<IAuthorizationPolicyProvider, FornitorePolicyPermessi>();
builder.Services.AddScoped<IAuthorizationHandler, GestorePermessi>();

builder.Services.AddAuthorization(opzioni =>
{
    // Nessuna pagina Ã¨ raggiungibile senza autenticazione,
    // salvo esplicita eccezione con [AllowAnonymous].
    opzioni.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// SERVIZI APPLICATIVI
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IServizioVersione, ServizioVersione>();
builder.Services.AddScoped<IContestoUtente, ContestoUtenteHttp>();
builder.Services.AddScoped<IServizioAudit, ServizioAudit>();

// â”€â”€ Accesso in sola lettura ad ArcLab â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSingleton<IFabbricaConnessioniArcLab, FabbricaConnessioniArcLab>();
builder.Services.AddScoped<IRepositoryCampioni, RepositoryCampioni>();
builder.Services.AddScoped<IRepositoryCertificati, RepositoryCertificati>();
builder.Services.AddScoped<IRepositoryAnagrafiche, RepositoryAnagrafiche>();
builder.Services.AddScoped<IRepositoryMetodi, RepositoryMetodi>();
builder.Services.AddScoped<IRepositoryCommesse, RepositoryCommesse>();
builder.Services.AddScoped<IRepositoryLaboratoriEsterni, RepositoryLaboratoriEsterni>();
builder.Services.AddScoped<IRepositoryTamponiContratto, RepositoryTamponiContratto>();
builder.Services.AddScoped<IRepositorySoloEsterni, RepositorySoloEsterni>();
builder.Services.AddScoped<IRepositoryCertificatiEsterni, RepositoryCertificatiEsterni>();
builder.Services.AddScoped<IArchivioEstrazioniCertificato, ArchivioEstrazioniCertificato>();
builder.Services.AddScoped<IRepositoryControlli, RepositoryControlli>();
builder.Services.AddScoped<IRepositoryRicercaErrori, RepositoryRicercaErrori>();
builder.Services.AddScoped<IRepositoryNonFatturare, RepositoryNonFatturare>();

// â”€â”€ Generazione documenti â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSingleton<IEsportatoreLaboratoriEsterni, EsportatoreLaboratoriEsterni>();
builder.Services.AddSingleton<IEsportatoreTamponiContratto, EsportatoreTamponiContratto>();
builder.Services.AddSingleton<IEsportatoreSoloEsterni, EsportatoreSoloEsterni>();
builder.Services.AddSingleton<IEsportatoreRicercaErrori, EsportatoreRicercaErrori>();
builder.Services.AddSingleton<IEsportatoreRisultatiRdp, EsportatoreRisultatiRdp>();
builder.Services.AddSingleton<IEsportatoreNonFatturare, EsportatoreNonFatturare>();
builder.Services.AddSingleton<IEsportatoreConsultazione, EsportatoreConsultazione>();
builder.Services.AddSingleton<IEsportatoreCertificato, EsportatoreCertificato>();

// â”€â”€ DVR Legionellosi â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.Configure<OpzioniDvr>(builder.Configuration.GetSection(OpzioniDvr.Sezione));
builder.Services.AddScoped<IRepositoryDvr, RepositoryDvr>();
builder.Services.AddScoped<IGeneratoreDvr, GeneratoreDvr>();

// ── DVR Piscine ─────────────────────────────────────────────────────────
builder.Services.Configure<OpzioniPiscine>(builder.Configuration.GetSection(OpzioniPiscine.Sezione));
builder.Services.AddScoped<IRepositoryPiscine, RepositoryPiscine>();
builder.Services.AddScoped<IGeneratorePiscine>(sp =>
    new GeneratorePiscine(sp.GetRequiredService<IOptions<OpzioniPiscine>>().Value));
builder.Services.AddSingleton<IServizioLottiDvr, ServizioLottiDvr>();

// ── Nutrizionale (report ed etichette) ──────────────────────────────────
builder.Services.AddSingleton<IServizioCalcoloNutrizionale, ServizioCalcoloNutrizionale>();
builder.Services.AddSingleton<IServizioEtichettaUsa, ServizioEtichettaUsa>();
builder.Services.AddSingleton<IServizioEtichettaCanada, ServizioEtichettaCanada>();
builder.Services.AddSingleton<IGeneratoreRapportoNutrizionale, GeneratoreRapportoNutrizionale>();
builder.Services.AddSingleton<IEsportatoreRapportoNutrizionale, EsportatoreRapportoNutrizionale>();
builder.Services.AddSingleton<IGeneratoreEtichettaUsa, GeneratoreEtichettaUsa>();
builder.Services.AddSingleton<IGeneratoreEtichettaCanada, GeneratoreEtichettaCanada>();

// ── Taratura termometri ─────────────────────────────────────────────────
builder.Services.AddSingleton<IServizioTaratura, ServizioTaratura>();
builder.Services.AddSingleton<IServizioValidazioneTaratura, ServizioValidazioneTaratura>();
builder.Services.AddSingleton<IServizioControlliTaratura, ServizioControlliTaratura>();
builder.Services.AddSingleton<IGeneratoreRapportoTaratura, GeneratoreRapportoTaratura>();
builder.Services.AddSingleton<IGeneratoreRapportoValidazione, GeneratoreRapportoValidazione>();
builder.Services.AddSingleton<IServizioTaraturaIncubatori, ServizioTaraturaIncubatori>();
builder.Services.AddSingleton<IServizioValidazioneIncubatori, ServizioValidazioneIncubatori>();
builder.Services.AddSingleton<IServizioControlliIncubatori, ServizioControlliIncubatori>();
builder.Services.AddSingleton<IGeneratoreRapportoIncubatore, GeneratoreRapportoIncubatore>();
builder.Services.AddSingleton<IServizioMappaturaIncubatori, ServizioMappaturaIncubatori>();
builder.Services.AddSingleton<IServizioValidazioneMappatura, ServizioValidazioneMappatura>();
builder.Services.AddSingleton<IServizioControlliMappatura, ServizioControlliMappatura>();
builder.Services.AddSingleton<IGeneratoreRapportoMappatura, GeneratoreRapportoMappatura>();
builder.Services.AddSingleton<IServizioBilancia, ServizioBilancia>();
builder.Services.AddSingleton<IServizioValidazioneBilancia, ServizioValidazioneBilancia>();
builder.Services.AddSingleton<IServizioControlliBilancia, ServizioControlliBilancia>();
builder.Services.AddSingleton<IGeneratoreRapportoBilancia, GeneratoreRapportoBilancia>();

// ── Monitoraggio TempLog Econorma ──────────────────────────────
builder.Services.AddSingleton<IServizioMonitoraggioTempLog, ServizioMonitoraggioTempLog>();
builder.Services.AddScoped<LaboratorioAI.Core.Servizi.ILettoreDbTempLog, LaboratorioAI.Dati.MonitorTemperature.LettoreDbTempLog>();
builder.Services.AddSingleton<ILettoreFileTempLog, LaboratorioAI.Dati.Import.LettoreFileTempLog>();
builder.Services.AddScoped<IRepositoryTempLog, RepositoryTempLog>();
builder.Services.AddSingleton<IGeneratoreReportTempLog, GeneratoreReportTempLog>();

// ── Scadenziario tarature ──────────────────────────────────────
builder.Services.AddScoped<IRepositoryScadenziario, RepositoryScadenziario>();
builder.Services.AddScoped<LaboratorioAI.Dati.Import.IImportatoreTermometriEsterni, LaboratorioAI.Dati.Import.ImportatoreTermometriEsterni>();
builder.Services.AddSingleton<IGeneratoreEtichetteTermometri, GeneratoreEtichetteTermometri>();

// ── Accettazione campioni fuori sede ───────────────────────────
builder.Services.AddScoped<IRepositoryAccettazione, RepositoryAccettazione>();
builder.Services.AddSingleton<IGeneratoreTracciatoAccettazione, GeneratoreTracciatoAccettazione>();
builder.Services.AddSingleton<IGeneratoreDocumentiAccettazione, GeneratoreDocumentiAccettazione>();
builder.Services.AddSingleton<IGeneratoreTracciatoControllo, GeneratoreTracciatoControllo>();
builder.Services.AddSingleton<IGeneratoreOffertaPreventivo, GeneratoreOffertaPreventivo>();
builder.Services.AddScoped<IRepositoryOrdineAccettazione, RepositoryOrdineAccettazione>();
builder.Services.AddSingleton<IGeneratoreOrdineAccettazione, GeneratoreOrdineAccettazione>();

builder.Services.AddScoped<LaboratorioAI.Dati.LaboratorioAI.Import.IImportatoreUsda,
    LaboratorioAI.Dati.LaboratorioAI.Import.ImportatoreUsda>();
builder.Services.AddScoped<LaboratorioAI.Dati.LaboratorioAI.Import.IImportatoreCnf,
    LaboratorioAI.Dati.LaboratorioAI.Import.ImportatoreCnf>();

// â”€â”€ Analisi AI â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.Configure<OpzioniAnthropic>(
    builder.Configuration.GetSection(OpzioniAnthropic.Sezione));

builder.Services.AddHttpClient<IClientAnthropic, ClientAnthropic>((servizi, http) =>
{
    var opzioni = servizi.GetRequiredService<
        Microsoft.Extensions.Options.IOptions<OpzioniAnthropic>>().Value;
    http.Timeout = TimeSpan.FromSeconds(opzioni.TimeoutSecondi);
});

builder.Services.AddScoped<IServizioAnalisi, ServizioAnalisi>();
builder.Services.AddScoped<IEstrattoreSchedaNutrizionale, EstrattoreSchedaNutrizionale>();
builder.Services.AddScoped<IEstrattoreCertificatoEsterno, EstrattoreCertificatoEsterno>();
builder.Services.AddScoped<IServizioCertificatiEsterni, ServizioCertificatiEsterni>();
builder.Services.AddScoped<IServizioPreventivi, ServizioPreventivi>();

builder.Services.AddControllersWithViews()
    .AddMicrosoftIdentityUI();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ContestoLaboratorioAI>("database");

var app = builder.Build();

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// MIGRAZIONI E POPOLAMENTO INIZIALE
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
using (var ambito = app.Services.CreateScope())
{
    var ctx = ambito.ServiceProvider.GetRequiredService<ContestoLaboratorioAI>();
    var log = ambito.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await ctx.Database.MigrateAsync();
        await Popolamento.EseguiAsync(ctx);
        log.LogInformation("Database allineato e popolamento iniziale completato.");
    }
    catch (Exception ex)
    {
        log.LogCritical(ex, "Allineamento del database non riuscito. L'applicazione non puÃ² avviarsi.");
        throw;
    }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// PIPELINE
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Errore");
    app.UseHsts();
}

app.UseSerilogRequestLogging(opzioni =>
{
    opzioni.MessageTemplate = "{RequestMethod} {RequestPath} â†’ {StatusCode} in {Elapsed:0} ms";
});

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/stato").AllowAnonymous();

app.Run();

