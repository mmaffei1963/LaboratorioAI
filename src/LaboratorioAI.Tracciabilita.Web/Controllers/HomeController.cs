using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    /// <summary>Pagina mostrata quando il ruolo dell'utente non consente l'operazione.</summary>
    [Route("Home/NonAutorizzato")]
    public IActionResult NonAutorizzato(string? cosa) { ViewData["Cosa"] = cosa; return View(); }

    [Route("Home/Errore")]
    public IActionResult Errore() => View();
}
