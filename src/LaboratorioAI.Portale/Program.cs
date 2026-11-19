using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
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
// Offerte (17/11/2026): offerta di laboratorio del cliente dalle commesse ArcLab (sola lettura).
builder.Services.AddScoped<IRepositoryOfferteClienti, RepositoryOfferteClienti>();
builder.Services.AddScoped<IRepositoryControlli, RepositoryControlli>();
builder.Services.AddScoped<ServizioOfferteClienti>();
// Password del portale (17/11/2026): password personale, recupero via email, ripristino dell'operatore.
builder.Services.AddScoped<IRepositoryCredenzialiClienti, RepositoryCredenzialiClienti>();
builder.Services.AddScoped<ServizioPasswordPortale>();
// Ordini di accettazione (18/11/2026): stesso repository e stesso generatore dell'anteprima di LaboratorioAI.
builder.Services.AddScoped<IRepositoryOrdineAccettazione, RepositoryOrdineAccettazione>();
builder.Services.AddScoped<IRepositoryAnagrafiche, RepositoryAnagrafiche>();
builder.Services.AddSingleton<IGeneratoreOrdineAccettazione, GeneratoreOrdineAccettazione>();
builder.Services.AddScoped<ServizioOrdiniPortale>();
// Documenti (19/11/2026): documenti del cliente da Marea.Entries (sola lettura).
builder.Services.AddScoped<IRepositoryDocumentiPortale, RepositoryDocumentiPortale>();
builder.Services.AddScoped<ServizioDocumentiPortale>();
// Accesso operatori con Microsoft 365 (19/11/2026), al posto del codice operatore con password.
AccessoOperatore365.Configura(builder);
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
// 18/11/2026: per impostazione predefinita ASP.NET Core si fida solo dei proxy locali e ignorava le intestazioni di
// Cloudflare: si dichiarano fidati gli indirizzi di Cloudflare (e le reti interne, per un eventuale proxy in sede).
// Si prende solo l'ultimo indirizzo aggiunto (quello visto da Cloudflare): gli altri possono essere falsificati.
var opzioniInoltro = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
{
    ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                     | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};
foreach (var rete in ReteFidata.Cloudflare.Concat(ReteFidata.Interne))
    opzioniInoltro.KnownNetworks.Add(ReteFidata.Leggi(rete));
app.UseForwardedHeaders(opzioniInoltro);

// 18/11/2026: intestazioni di sicurezza. Il portale usa solo risorse proprie: niente script esterni, niente inserimento
// in pagine di altri siti, niente sniffing dei tipi di file.
app.Use(async (contesto, successivo) =>
{
    var h = contesto.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "same-origin";
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";
    h["Content-Security-Policy"] =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; " +
        "connect-src 'self'; font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
    await successivo();
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
app.UseAuthentication(); // 19/11/2026: solo per il rientro dal login Microsoft degli operatori

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

/// <summary>Reti fidate per le intestazioni X-Forwarded-* (18/11/2026). Elenco di Cloudflare: https://www.cloudflare.com/ips/</summary>
static class ReteFidata
{
    public static readonly string[] Cloudflare =
    [
        "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18", "108.162.192.0/18",
        "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17", "162.158.0.0/15", "104.16.0.0/13",
        "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
        "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32", "2a06:98c0::/29", "2c0f:f248::/32"
    ];

    public static readonly string[] Interne = ["10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16"];

    public static Microsoft.AspNetCore.HttpOverrides.IPNetwork Leggi(string cidr)
    {
        var parti = cidr.Split('/');
        return new Microsoft.AspNetCore.HttpOverrides.IPNetwork(System.Net.IPAddress.Parse(parti[0]), int.Parse(parti[1]));
    }
}
