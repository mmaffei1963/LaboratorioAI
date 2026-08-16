using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
