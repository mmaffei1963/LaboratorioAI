using LaboratorioAI.Tracciabilita.Web.Autenticazione;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

public class AccountController : Controller
{
    private readonly ServizioAccesso _accesso;
    private readonly ServizioInviti _inviti;

    public AccountController(ServizioAccesso accesso, ServizioInviti inviti)
    {
        _accesso = accesso;
        _inviti = inviti;
    }

    [HttpGet("/Invito/{codice}")]
    public async Task<IActionResult> Invito(string codice, CancellationToken ct)
    {
        var nome = await _inviti.NomePerInvitoAsync(codice, ct);
        if (nome is null) return View("InvitoNonValido");
        return View(new ModelloImpostaPassword { Codice = codice, Nome = nome });
    }

    [HttpPost("/Invito/{codice}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invito(string codice, ModelloImpostaPassword modello, CancellationToken ct)
    {
        modello.Codice = codice;
        if (!ModelState.IsValid)
        {
            modello.Nome = await _inviti.NomePerInvitoAsync(codice, ct);
            return modello.Nome is null ? View("InvitoNonValido") : View(modello);
        }

        var esito = await _inviti.ImpostaPasswordAsync(codice, modello.Password, HttpContext.Connection.RemoteIpAddress?.ToString(), ct);
        if (esito.Riuscito)
        {
            TempData["Messaggio"] = "Password impostata. Ora puoi accedere.";
            return RedirectToAction("Accesso");
        }

        if (esito.Nome is null) return View("InvitoNonValido");
        foreach (var errore in esito.Errori ?? Array.Empty<string>())
            ModelState.AddModelError(string.Empty, errore);
        modello.Nome = esito.Nome;
        modello.Password = modello.Conferma = null;
        return View(modello);
    }

    [HttpGet("/Accesso")]
    public IActionResult Accesso(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        return View(new ModelloAccesso { ReturnUrl = returnUrl });
    }

    [HttpPost("/Accesso")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accesso(ModelloAccesso modello, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(modello);

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var esito = await _accesso.AccediAsync(modello.Email, modello.Password, ip, ct);

        switch (esito.Esito)
        {
            case EsitoAccesso.Riuscito:
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, esito.Utente!,
                    new AuthenticationProperties { IsPersistent = false });
                return !string.IsNullOrEmpty(modello.ReturnUrl) && Url.IsLocalUrl(modello.ReturnUrl)
                    ? LocalRedirect(modello.ReturnUrl)
                    : RedirectToAction("Index", "Home");

            case EsitoAccesso.Bloccato:
                ModelState.AddModelError(string.Empty,
                    $"Troppi tentativi. Riprova tra {Math.Max(1, esito.MinutiDiBlocco)} minuti.");
                break;

            case EsitoAccesso.ServizioNonAttivo:
                ModelState.AddModelError(string.Empty,
                    "Il servizio per la tua azienda non è attivo. Contatta MARE.A.");
                break;

            default:
                ModelState.AddModelError(string.Empty, "E-mail o password non corretta.");
                break;
        }

        modello.Password = null;   // la password non torna mai nella pagina
        return View(modello);
    }

    [Authorize]
    [HttpPost("/Esci")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Esci()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Accesso");
    }
}
