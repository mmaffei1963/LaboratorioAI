using LaboratorioAI.Tracciabilita.Web.Navigazione;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Guide d'uso delle funzioni: elenco, pagina intera (stampabile) e pannello laterale.</summary>
[Authorize]
[Route("Guide")]
public class GuideController : Controller
{
    private bool EGestore => User.HasClaim("tr:gestore", "true");

    [HttpGet("")]
    public IActionResult Index() => View(Guide.Visibili(EGestore).ToList());

    [HttpGet("{codice}")]
    public IActionResult Guida(string codice)
    {
        var g = Guide.PerCodice(codice, EGestore);
        return g is null ? NotFound() : View(g);
    }

    [HttpGet("{codice}/pannello")]
    public IActionResult Pannello(string codice)
    {
        var g = Guide.PerCodice(codice, EGestore);
        return g is null ? NotFound() : PartialView("_" + g.Codice, g);
    }
}
