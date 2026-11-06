using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.Accredia;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;
using LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;
using LaboratorioAI.Dati.Pdf;
using LaboratorioAI.Dati.MonitorTemperature;
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

// Font resolver per i PDF (PDFsharp cross-platform): font incorporati, così la generazione
// PDF funziona anche su server privi di font di sistema. Idempotente.
LaboratorioAI.Documenti.FontResolverLabAI.Registra();

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

// Modulo Monitor Temperature (Econorma): sola lettura salvo MappaSonde. MAI MigrateAsync su questo contesto:
// lo schema del database MonitorTemperature resta di competenza del programma di acquisizione Econorma.
builder.Services.AddDbContext<ContestoMonitorTemperature>(opzioni =>
    opzioni.UseSqlServer(builder.Configuration.GetConnectionString("MonitorTemperature")));

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
builder.Services.AddScoped<IAuthorizationHandler, GestoreUnoDeiPermessi>();
// 11/10/2026: permessi riletti dal database a ogni richiesta (cambi di ruolo con effetto immediato).
builder.Services.AddScoped<ServizioPermessiUtente>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation, TrasformazionePermessi>();

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
// Condizioni Generali e firme: registro delle evidenze a impronte concatenate (09/10/2026).
builder.Services.AddScoped<LaboratorioAI.Dati.LaboratorioAI.IRegistroEvidenzeApprovazioni, LaboratorioAI.Dati.LaboratorioAI.RegistroEvidenzeApprovazioni>();
builder.Services.AddScoped<ServizioRegistroDocumenti>();
builder.Services.AddSingleton<IServizioEmail, LaboratorioAI.Dati.Email.ServizioEmailSmtp>();

// â”€â”€ Accesso in sola lettura ad ArcLab â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSingleton<IFabbricaConnessioniArcLab, FabbricaConnessioniArcLab>();
builder.Services.AddScoped<IRepositoryCampioni, RepositoryCampioni>();
builder.Services.AddScoped<IRepositoryCertificati, RepositoryCertificati>();
builder.Services.AddScoped<IRepositoryAnagrafiche, RepositoryAnagrafiche>();
builder.Services.AddScoped<IRepositoryMetodi, RepositoryMetodi>();
builder.Services.AddScoped<IRepositoryCommesse, RepositoryCommesse>();
builder.Services.AddScoped<IRepositoryConsulenze, RepositoryConsulenze>();
builder.Services.AddScoped<IRepositoryLaboratoriEsterni, RepositoryLaboratoriEsterni>();
builder.Services.AddScoped<IRepositoryMetodiLabEsterno, RepositoryMetodiLabEsterno>();
builder.Services.AddScoped<IRepositoryTamponiContratto, RepositoryTamponiContratto>();
builder.Services.AddScoped<IRepositorySoloEsterni, RepositorySoloEsterni>();
builder.Services.AddScoped<IRepositoryCertificatiEsterni, RepositoryCertificatiEsterni>();
builder.Services.AddScoped<IArchivioEstrazioniCertificato, ArchivioEstrazioniCertificato>();
builder.Services.AddScoped<IRepositorySigleLaboratori, RepositorySigleLaboratori>();
builder.Services.AddScoped<IRepositoryControlli, RepositoryControlli>();
// Limiti normativi (17/10/2026): schede e fonti, download dei testi ufficiali, estrazione AI in background.
builder.Services.AddScoped<IRepositoryLimitiArcLab, RepositoryLimitiArcLab>();
builder.Services.AddHttpClient<LaboratorioAI.Dati.Limiti.ServizioLimitiNormativi>(http => http.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddScoped<LaboratorioAI.IA.EstrattoreLimitiNormativi>();
builder.Services.AddScoped<LaboratorioAI.Dati.Limiti.ServizioVerificaLimiti>();
builder.Services.AddScoped<LaboratorioAI.IA.RevisoreLimitiNormativi>();
builder.Services.AddScoped<LaboratorioAI.Dati.Limiti.ServizioCategorieLimiti>();
builder.Services.AddScoped<LaboratorioAI.Dati.Limiti.ServizioSegnalazioniLimiti>();
builder.Services.AddScoped<IRepositoryGiudiziArcLab, RepositoryGiudiziArcLab>();
builder.Services.AddScoped<LaboratorioAI.Dati.Validazione.ServizioValidazioneGiudizio>();
builder.Services.AddScoped<LaboratorioAI.Dati.Provvigioni.ServizioSpettanzeConsulenti>();
builder.Services.AddScoped<LaboratorioAI.IA.ProponiCategorieMatrici>();
builder.Services.AddHostedService<LaboratorioAI.Web.Servizi.ServizioEstrazioneLimiti>();
builder.Services.AddScoped<IRepositoryRicercaErrori, RepositoryRicercaErrori>();
builder.Services.AddScoped<IRepositoryNonFatturare, RepositoryNonFatturare>();
builder.Services.AddScoped<LaboratorioAI.Dati.Provvigioni.ImportatoreAzw>();
builder.Services.AddHostedService<LaboratorioAI.Web.Servizi.ImportazioneNotturnaAzw>();
builder.Services.AddScoped<LaboratorioAI.Dati.Provvigioni.ServizioProvvigioniLab>();
builder.Services.AddScoped<LaboratorioAI.Dati.Direzione.ServizioRendimentoCommerciali>();
builder.Services.AddScoped<LaboratorioAI.Dati.Direzione.ServizioConsuntivi>();
builder.Services.AddScoped<LaboratorioAI.Dati.Direzione.ServizioCompensiSoci>();
builder.Services.AddScoped<LaboratorioAI.Dati.ArcLab.Repository.RepositoryRitardiCampioni>();
builder.Services.AddScoped<LaboratorioAI.Dati.CicloAnalitico.ServizioRitardiCampioni>();
builder.Services.AddScoped<LaboratorioAI.Web.Servizi.ImportatoreCompensiSociExcel>();
builder.Services.AddScoped<LaboratorioAI.Dati.Clienti.ServizioClientiDaSeguire>();
// Cambi di gestione (12/10/2026): subentri e verifica delle P.IVA sul servizio europeo VIES.
builder.Services.AddScoped<LaboratorioAI.Dati.Clienti.ServizioSubentri>();
builder.Services.AddHttpClient<LaboratorioAI.Dati.Clienti.ClientVies>(http => http.Timeout = TimeSpan.FromSeconds(20));
// Contatori della home (08/10/2026): conteggi leggeri, tenuti in memoria 2 minuti.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<LaboratorioAI.Web.Navigazione.ServizioContatoriHome>();
builder.Services.AddScoped<LaboratorioAI.Dati.Provvigioni.ServizioCompensiSegnalatori>();
builder.Services.AddScoped<LaboratorioAI.Dati.Consulenza.ServizioPreventiviConsulenza>();
builder.Services.AddScoped<IRepositoryStatistiche, RepositoryStatistiche>();
builder.Services.AddScoped<IRepositoryArcLabSonde, RepositoryArcLabSonde>();
builder.Services.AddScoped<RepositoryMonitorSonde>();   // 03/11/2026: seriali attivi e limiti
builder.Services.AddSingleton<IEsportatoreMonitorTemperature, EsportatoreMonitorTemperature>();

// â”€â”€ Generazione documenti â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
builder.Services.AddSingleton<IEsportatoreLaboratoriEsterni, EsportatoreLaboratoriEsterni>();
builder.Services.AddSingleton<IEsportatoreTamponiContratto, EsportatoreTamponiContratto>();
builder.Services.AddSingleton<IEsportatoreSoloEsterni, EsportatoreSoloEsterni>();
builder.Services.AddSingleton<IEsportatoreRicercaErrori, EsportatoreRicercaErrori>();
builder.Services.AddSingleton<IEsportatoreRisultatiRdp, EsportatoreRisultatiRdp>();
builder.Services.AddSingleton<IEsportatoreNonFatturare, EsportatoreNonFatturare>();
builder.Services.AddSingleton<IEsportatoreProvvigioni, EsportatoreProvvigioni>();
builder.Services.AddSingleton<IEsportatoreClientiDaSeguire, EsportatoreClientiDaSeguire>();
builder.Services.AddSingleton<IEsportatoreStatistiche, EsportatoreStatistiche>();
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
// Condizioni e firme, fase 2 (10/10/2026): Ricevuta PDF delle approvazioni, generata e inviata in automatico.
builder.Services.AddSingleton<IGeneratoreRicevutaApprovazione, GeneratoreRicevutaApprovazione>();
builder.Services.AddHostedService<LaboratorioAI.Web.Servizi.ServizioRicevuteApprovazione>();
// Invio massivo dei link (10/10/2026): operazioni condivise e invio in coda con tetto giornaliero.
builder.Services.AddScoped<LaboratorioAI.Web.Servizi.ServizioApprovazioni>();
// Inviti al cliente a indicare il Firmatario e promemoria (11/10/2026).
builder.Services.AddScoped<LaboratorioAI.Web.Servizi.ServizioInvitiFirmatario>();
builder.Services.AddHostedService<LaboratorioAI.Web.Servizi.ServizioInvioLinkMassivo>();
// «Chiedi alla guida» (11/10/2026): dopo 12 mesi svuota il testo delle domande registrate.
builder.Services.AddHostedService<LaboratorioAI.Web.Servizi.ServizioScadenzaDomandeGuida>();
builder.Services.AddSingleton<IGeneratoreRapportino, GeneratoreRapportino>();
builder.Services.AddSingleton<IGeneratoreDelega, GeneratoreDelega>();

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
    // 17/10/2026: il tempo massimo è per chiamata, dentro ClientAnthropic (TimeoutSecondi o quello del lavoro lungo).
    // Margine di sicurezza: nessuna chiamata può comunque durare più di 15 minuti.
    http.Timeout = TimeSpan.FromSeconds(Math.Max(opzioni.TimeoutSecondi, 900));
});

builder.Services.AddScoped<IServizioAnalisi, ServizioAnalisi>();
builder.Services.AddScoped<IEstrattoreSchedaNutrizionale, EstrattoreSchedaNutrizionale>();
builder.Services.AddScoped<IEstrattoreCertificatoEsterno, EstrattoreCertificatoEsterno>();
builder.Services.AddScoped<IServizioCertificatiEsterni, ServizioCertificatiEsterni>();
builder.Services.AddScoped<IServizioPreventivi, ServizioPreventivi>();
builder.Services.AddScoped<LaboratorioAI.Documenti.Manualistica.ServizioModelliManualistica>();
builder.Services.AddScoped<LaboratorioAI.Documenti.Manualistica.ServizioVociChecklist>();

// ── Accreditamento ACCREDIA laboratori esterni ─────────────────────────────
// Timeout esplicito (invece del default di 100s): con più laboratori controllati in sequenza,
// uno solo lento o che non risponde non deve tenere impegnato l'intero controllo per minuti.
//
// Il server chiama sempre certificati.accredia.it direttamente: dal 29/09/2026 non ci riesce
// (probabile blocco lato ACCREDIA sull'IP del server, ospitato fuori dalla rete aziendale) e il
// controllo notturno diretto va disattivato con "AccreditamentiEsterni:ControlloAutomaticoNotturnoAttivo"
// = false (vedi ControlloAccreditamentiEsterni). Il canale effettivo è invece l'importazione dei
// risultati raccolti da strumenti/AgenteAccredia (vedi AccrediaAgenteController), che usa un
// IClientAccredia diverso (ClientAccrediaDaImportazione) costruito al volo solo per quella
// richiesta — non serve un'altra registrazione qui.
builder.Services.AddHttpClient<IClientAccredia, ClientAccredia>(http =>
{
    http.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<IServizioAccreditamentiEsterni, ServizioAccreditamentiEsterni>();
builder.Services.AddScoped<IServizioVerificaMetodiEsterni, ServizioVerificaMetodiEsterni>();
// Accreditamenti «da sistemare» (discrepanze verifica/ArcLab, numeri ACCREDIA diversi, controllo fermo) — 08/10/2026.
builder.Services.AddScoped<LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioAnomalieAccreditamenti>();
builder.Services.AddScoped<LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioLaboratoriMonitorati>();
builder.Services.AddSingleton<IEstrattoreTestoPdf, EstrattoreTestoPdf>();
builder.Services.AddSingleton<ControlloAccreditamentiEsterni>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ControlloAccreditamentiEsterni>());

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
        // 11/10/2026: al passaggio ai ruoli per mestiere, in sviluppo l'utente di sviluppo resta Amministratore.
        var amministratoriAggiuntivi = app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("AutenticazioneSviluppo:Abilitato")
            && app.Configuration["AutenticazioneSviluppo:Email"] is { Length: > 0 } emailSviluppo
            ? new[] { emailSviluppo } : Array.Empty<string>();
        var riepilogoRuoli = await Popolamento.EseguiAsync(ctx, amministratoriAggiuntivi);
        if (riepilogoRuoli is not null) log.LogWarning("{Riepilogo}", riepilogoRuoli);
        await PopolamentoAccreditamentiEsterni.EseguiAsync(ctx);
        await PopolamentoManualistica.EseguiAsync(ctx);
        await PopolamentoTermometriEsterni.EseguiAsync(ctx);   // 03/11/2026: tipo dei termometri dalla tipologia
        await PopolamentoComuni.EseguiAsync(ctx);              // 04/11/2026: elenco ISTAT dei comuni
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
    name: "aree",
    pattern: "{area:exists}/{controller=Monitor}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHealthChecks("/stato").AllowAnonymous();

app.Run();

