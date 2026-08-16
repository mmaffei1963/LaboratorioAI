using LaboratorioAI.Core.Servizi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

public class HomeController : Controller
{
    private readonly IContestoUtente _utente;

    public HomeController(IContestoUtente utente) => _utente = utente;

    public IActionResult Index()
    {
        ViewData["NomeUtente"] = _utente.NomeCompleto;
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Errore() => View();

    [AllowAnonymous]
    public IActionResult AccessoNegato() => View();
}
