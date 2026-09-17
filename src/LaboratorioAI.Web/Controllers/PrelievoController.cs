using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// "Prelievo vocale": pagina mobile per raccogliere in campo i dati del prelievo (lotto → campioni)
/// dettandoli a voce, con controllo dei campi obbligatori e riassunto esportabile da digitare poi
/// in ArcLab. Tutto lato client (offline dopo il primo caricamento): nessuna scrittura server.
/// </summary>
[RichiedePermesso(Permessi.AccettazioneEsegui)]
public class PrelievoController : Controller
{
    public IActionResult Index()
    {
        ViewData["Titolo"] = "Prelievo vocale";
        return View();
    }
}
