using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Anomalie offerte (17/11/2026): stesso controllo in insediamenti diversi dello stesso cliente con prezzo uguale e
/// descrizione diversa (B) o prezzo diverso e descrizione uguale (D). Servono per sistemare le commesse in ArcLab prima
/// che il cliente veda l'offerta sul portale. Sola lettura su ArcLab; stesso permesso di Ricerca errori.
/// </summary>
[RichiedePermesso(Permessi.RicercaErroriVisualizza)]
public class AnomalieOfferteController : Controller
{
    private readonly ServizioOfferteClienti _servizio;

    public AnomalieOfferteController(ServizioOfferteClienti servizio) => _servizio = servizio;

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Anomalie offerte";
        return View();
    }

    [HttpGet("api/anomalie-offerte")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var anomalie = await _servizio.AnomalieAsync(ct);
        return Ok(anomalie.Select(a => new
        {
            a.Caso, a.Cliente, a.CodContabilita, a.IdCliente, a.IdControllo, a.Controllo, a.Descrizione,
            Commesse = a.Commesse.Select(c => new
            {
                c.IdCommessa, c.Insediamento, c.Incarico, c.Prezzo, DataCommessa = c.DataCommessa?.ToString("dd/MM/yyyy"), c.NonFatturare
            })
        }));
    }

    [HttpGet("api/anomalie-offerte/excel")]
    public async Task<IActionResult> Excel(CancellationToken ct)
    {
        var file = EsportatoreAnomalieOfferte.Genera(await _servizio.AnomalieAsync(ct));
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Anomalie_offerte_{DateTime.Today:yyyyMMdd}.xlsx");
    }
}
