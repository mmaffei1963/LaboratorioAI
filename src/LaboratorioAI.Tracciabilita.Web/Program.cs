using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Autenticazione;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════
// LOGGING
// ═══════════════════════════════════════════════════════════════════
builder.Host.UseSerilog((contesto, servizi, config) =>
{
    config
        .ReadFrom.Configuration(contesto.Configuration)
        .ReadFrom.Services(servizi)
        .Enrich.FromLogContext();
    if (contesto.HostingEnvironment.IsDevelopment())
        config.WriteTo.Console(outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}");
});

// ═══════════════════════════════════════════════════════════════════
// DATI — database «Tracciabilita», separato da Laboratorio AI, ArcLab e AZW.
// I dati dei clienti del servizio stanno solo qui.
// ═══════════════════════════════════════════════════════════════════
builder.Services.AggiungiTracciabilitaDati(
    builder.Configuration.GetConnectionString("Tracciabilita") ?? "");

// Chi sta lavorando e per quale azienda: lo dice la sessione. Senza accesso non c'è azienda
// né gestore, quindi il database non mostra nessun dato (comportamento sicuro).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IContestoAzienda, ContestoDaUtente>();
builder.Services.AddScoped<ServizioAccesso>();
builder.Services.AddScoped<ServizioInviti>();
builder.Services.AddScoped<LaboratorioAI.Tracciabilita.Web.Servizi.IInvioEmail, LaboratorioAI.Tracciabilita.Web.Servizi.InvioEmailSmtp>();

// ═══════════════════════════════════════════════════════════════════
// ACCESSO — e-mail e password, sessione in un cookie cifrato
// ═══════════════════════════════════════════════════════════════════
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "Tracciato.Accesso";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        o.LoginPath = "/Accesso";
        o.AccessDeniedPath = "/Accesso";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = ValidatoreSessione.ValidaAsync;
    });
builder.Services.AddAuthorization(o =>
    o.AddPolicy("Gestore", p => p.RequireAuthenticatedUser().RequireClaim(Rivendicazioni.Gestore, "true")));

builder.Services.AddScoped<LaboratorioAI.Tracciabilita.Web.Servizi.PermessiUtente>();
builder.Services.AddControllersWithViews(opzioni => opzioni.Filters.Add<LaboratorioAI.Tracciabilita.Web.Servizi.FiltroSolaLettura>());
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ContestoTracciabilita>("database");

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════
// PROTEZIONI DENTRO IL DATABASE (trigger e sicurezza a livello di riga)
// Si applicano solo se richiesto in configurazione, dopo aver creato il database con la migrazione.
// La sicurezza a livello di riga resta spenta finché non si imposta Tracciabilita:AttivaSicurezzaRiga.
// ═══════════════════════════════════════════════════════════════════
if (app.Configuration.GetValue<bool>("Tracciabilita:ApplicaVincoliSql"))
{
    // Le opzioni del database sono legate a una sessione: si leggono da una sessione temporanea.
    using var sessione = app.Services.CreateScope();
    var opzioni = sessione.ServiceProvider.GetRequiredService<DbContextOptions<ContestoTracciabilita>>();
    await using var db = new ContestoTracciabilita(opzioni, new ContestoGestore());
    await VincoliSicurezzaSql.ApplicaAsync(db, app.Configuration.GetValue<bool>("Tracciabilita:AttivaSicurezzaRiga"));
    app.Logger.LogInformation("Vincoli di sicurezza del database applicati (sicurezza a livello di riga: {Stato}).",
        app.Configuration.GetValue<bool>("Tracciabilita:AttivaSicurezzaRiga") ? "attiva" : "spenta");
}

await GestoreIniziale.AssicuraAsync(app.Services, app.Configuration, app.Logger);

// ═══════════════════════════════════════════════════════════════════
// PIPELINE
// ═══════════════════════════════════════════════════════════════════
// Dietro IIS e Cloudflare: si legge l'indirizzo e il protocollo veri del visitatore (https, host pubblico),
// altrimenti i link degli inviti e il QR delle etichette partirebbero con l'indirizzo interno.
app.UseForwardedHeaders(new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedHost
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Errore");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/stato");
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Serve ai test e allo strumento delle migrazioni.
public partial class Program { }
