using System.Security.Claims;
using LaboratorioAI.Portale.Autenticazione;
using LaboratorioAI.Portale.Servizi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Accesso operatori MARE.A con Microsoft 365 (19/11/2026). /operatore/accedi manda al login Microsoft;
/// /operatore/rientro controlla che la persona sia un utente attivo di LaboratorioAI, apre la sessione del portale e
/// torna alla pagina con il token nel frammento dell'indirizzo (#operatore=…), che non arriva al server né nei log
/// e che la pagina cancella subito.
/// </summary>
[Route("operatore")]
public class OperatoreController : Controller
{
    private readonly IServizioAccessoPortale _accesso;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _ambiente;

    public OperatoreController(IServizioAccessoPortale accesso, IConfiguration config, IWebHostEnvironment ambiente)
    {
        _accesso = accesso; _config = config; _ambiente = ambiente;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Agente => Request.Headers.UserAgent.FirstOrDefault();

    [HttpGet("accedi")]
    public async Task<IActionResult> Accedi(CancellationToken ct)
    {
        if (!AccessoOperatore365.Configurato(_config))
        {
            if (!_ambiente.IsDevelopment()) return Redirect("/#operatore-errore=configurazione");
            // Sviluppo: lo stesso «Utente di sviluppo» dell'accesso di sviluppo di LaboratorioAI (deve essere attivo con
            // un ruolo nel database di test: basta aver aperto LaboratorioAI in sviluppo almeno una volta).
            var sessioneSviluppo = await _accesso.AccediOperatore365Async(
                new Guid("00000000-0000-0000-0000-0000000DE7E5"), "sviluppo@mare-a.com", "Utente di sviluppo", Ip, Agente, ct);
            return Redirect(sessioneSviluppo is null ? "/#operatore-errore=non-abilitato" : "/#operatore=" + sessioneSviluppo.Token);
        }
        return Challenge(new AuthenticationProperties { RedirectUri = "/operatore/rientro" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("rientro")]
    public async Task<IActionResult> Rientro(CancellationToken ct)
    {
        if (!AccessoOperatore365.Configurato(_config)) return Redirect("/#operatore-errore=configurazione");
        var esito = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        // Il cookie Microsoft non serve più: da qui vale solo la sessione del portale.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!esito.Succeeded || esito.Principal is null) return Redirect("/#operatore-errore=accesso");

        var p = esito.Principal;
        var oidTesto = p.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier") ?? p.FindFirstValue("oid");
        Guid? oid = Guid.TryParse(oidTesto, out var g) ? g : null;
        var email = p.FindFirstValue("preferred_username") ?? p.FindFirstValue(ClaimTypes.Email) ?? p.FindFirstValue(ClaimTypes.Upn);
        var nome = p.FindFirstValue("name") ?? p.Identity?.Name;

        var sessione = await _accesso.AccediOperatore365Async(oid, email, nome, Ip, Agente, ct);
        return Redirect(sessione is null ? "/#operatore-errore=non-abilitato" : "/#operatore=" + sessione.Token);
    }
}
