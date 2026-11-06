using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Autenticazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Servizi;

/// <summary>
/// Cosa può fare l'utente collegato, in base al suo ruolo nell'azienda (si rilegge dal database a ogni richiesta,
/// così un cambio di ruolo vale subito). Il ruolo più alto tra quelli assegnati è quello che conta:
/// Consultazione = sola lettura; Ricevimento, Produzione = lavoro di ogni giorno; Qualità e Amministratore =
/// anche impostazioni, categorie e controlli, sblocco dei lotti.
/// </summary>
public class PermessiUtente
{
    private readonly ContestoTracciabilita _db;
    private readonly IHttpContextAccessor _http;
    private RuoloAzienda? _ruolo;
    private bool _letto;

    public PermessiUtente(ContestoTracciabilita db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    /// <summary>Il ruolo più alto dell'utente; vuoto per il gestore e per chi non ha nessun ruolo assegnato.</summary>
    public async Task<RuoloAzienda?> RuoloAsync(CancellationToken ct = default)
    {
        if (_letto) return _ruolo;
        _letto = true;
        var utente = _http.HttpContext?.User;
        if (utente?.Identity?.IsAuthenticated != true || utente.HasClaim(Rivendicazioni.Gestore, "true")) return null;
        if (!int.TryParse(utente.FindFirst(Rivendicazioni.IdUtente)?.Value, out var idUtente)) return null;
        var ruoli = await _db.UtentiInsediamenti.AsNoTracking().Where(r => r.IdUtente == idUtente).Select(r => r.Ruolo).ToListAsync(ct);
        _ruolo = ruoli.Count == 0 ? null : ruoli.Max();
        return _ruolo;
    }

    /// <summary>Può registrare e correggere (tutti tranne la sola consultazione).</summary>
    public async Task<bool> PuoModificareAsync(CancellationToken ct = default) =>
        await RuoloAsync(ct) is { } r && r >= RuoloAzienda.Ricevimento;

    /// <summary>Può cambiare le impostazioni, le categorie e i controlli, e sbloccare i lotti.</summary>
    public async Task<bool> PuoConfigurareAsync(CancellationToken ct = default) =>
        await RuoloAsync(ct) is { } r && r >= RuoloAzienda.Qualita;
}

/// <summary>Da mettere sulle azioni riservate a Qualità e Amministratore azienda.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RichiedeConfigurazioneAttribute : Attribute, IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext contesto)
    {
        var http = contesto.HttpContext;
        if (http.User.Identity?.IsAuthenticated != true) return;      // ci pensa [Authorize]
        if (http.User.HasClaim(Rivendicazioni.Gestore, "true")) return; // il gestore non lavora sui dati dell'azienda
        var permessi = http.RequestServices.GetRequiredService<PermessiUtente>();
        if (!await permessi.PuoConfigurareAsync(http.RequestAborted))
            contesto.Result = new RedirectToActionResult("NonAutorizzato", "Home", new { cosa = "configurazione" });
    }
}

/// <summary>Chi ha solo il ruolo di consultazione (o nessun ruolo) può guardare ma non modificare nulla.</summary>
public sealed class FiltroSolaLettura : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext contesto, ActionExecutionDelegate avanti)
    {
        var http = contesto.HttpContext;
        var metodo = http.Request.Method;
        var scrittura = !(HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || HttpMethods.IsOptions(metodo));
        if (!scrittura || http.User.Identity?.IsAuthenticated != true || http.User.HasClaim(Rivendicazioni.Gestore, "true"))
        {
            await avanti();
            return;
        }
        var controller = contesto.RouteData.Values["controller"] as string;
        if (controller is "Account")
        {
            await avanti();
            return;
        }
        var permessi = http.RequestServices.GetRequiredService<PermessiUtente>();
        if (await permessi.PuoModificareAsync(http.RequestAborted))
        {
            await avanti();
            return;
        }
        contesto.Result = new RedirectToActionResult("NonAutorizzato", "Home", new { cosa = "modifica" });
    }
}
