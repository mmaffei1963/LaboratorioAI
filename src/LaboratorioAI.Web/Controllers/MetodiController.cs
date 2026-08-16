using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.MetodiConsulta)]
public class MetodiController : Controller
{
    private readonly IRepositoryMetodi _metodi;
    private readonly IContestoUtente _utente;

    public MetodiController(IRepositoryMetodi metodi, IContestoUtente utente)
    {
        _metodi = metodi;
        _utente = utente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Metodi e Controlli";
        return View();
    }

    [HttpGet("api/metodi/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var metodi = await _metodi.MetodiAttiviAsync(ct);

        return Json(metodi.Select(m => new
        {
            m.IdMetodo,
            m.RiferimentoNormativo,
            m.NomeParametro,
            m.DenominazioneRiga,
            m.Accreditato,
            m.LaboratorioEsterno,
            m.AccreditatoEsterno,
            m.TestoRicerca
        }));
    }

    [HttpGet("api/metodi/controlli")]
    public async Task<IActionResult> Controlli(CancellationToken ct) =>
        Json(await _metodi.ControlliAsync(ct));

    /// <summary>Controlli che soddisfano la logica indicata sui metodi selezionati.</summary>
    [HttpPost("api/metodi/cerca-controlli")]
    public async Task<IActionResult> CercaControlli(
        [FromBody] RichiestaControlli richiesta, CancellationToken ct)
    {
        if (richiesta.IdMetodi.Count == 0)
            return BadRequest(new { messaggio = "Selezionare almeno un metodo." });

        var logica = Enum.TryParse<LogicaRicercaControlli>(richiesta.Logica, true, out var l)
            ? l : LogicaRicercaControlli.Tutti;

        var controlli = await _metodi.ControlliPerMetodiAsync(richiesta.IdMetodi, logica, ct);

        return Json(controlli);
    }

    /// <summary>
    /// Metodi che compongono un controllo. Il quadro dei prezzi è incluso
    /// solo per gli utenti che dispongono del permesso relativo.
    /// </summary>
    [HttpGet("api/metodi/controllo/{idControllo:int}")]
    public async Task<IActionResult> Controllo(int idControllo, CancellationToken ct)
    {
        var metodi = await _metodi.MetodiDelControlloAsync(idControllo, ct);

        if (metodi.Count == 0)
            return NotFound(new { messaggio = "Controllo inesistente o privo di metodi." });

        var prezzi = _utente.HaPermesso(Permessi.MetodiPrezzi)
            ? await _metodi.PrezziControlloAsync(idControllo, ct)
            : null;

        return Json(new
        {
            IdControllo = idControllo,
            Controllo = metodi[0].Controllo,
            Metodi = metodi.Select(m => new
            {
                m.IdMetodo,
                m.DenominazioneRiga,
                m.RiferimentoNormativo,
                m.NomeParametro,
                m.Accreditato,
                m.LaboratorioEsterno,
                m.AccreditatoEsterno,
                m.Attivo
            }),
            Prezzi = prezzi is null || !prezzi.Disponibili ? null : new
            {
                prezzi.PrezzoMinimo,
                prezzi.ClientePrezzoMinimo,
                prezzi.ClientiAlMinimo,
                prezzi.PrezzoMassimo,
                prezzi.ClientePrezzoMassimo,
                prezzi.ClientiAlMassimo,
                prezzi.ClientiTotali,
                prezzi.PrezzoUnico
            }
        });
    }

    public record RichiestaControlli(List<int> IdMetodi, string Logica);
}
