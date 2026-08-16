using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.CommesseConsulta)]
public class CommesseController : Controller
{
    private readonly IRepositoryCommesse _commesse;
    private readonly IContestoUtente _utente;

    public CommesseController(IRepositoryCommesse commesse, IContestoUtente utente)
    {
        _commesse = commesse;
        _utente = utente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Commesse";
        return View();
    }

    [HttpGet("api/commesse/tipi")]
    public async Task<IActionResult> Tipi(CancellationToken ct) =>
        Json(await _commesse.TipiCommessaAsync(ct));

    [HttpPost("api/commesse/cerca")]
    public async Task<IActionResult> Cerca(
        [FromBody] CriteriRicercaCommesse criteri, CancellationToken ct)
    {
        var righe = await _commesse.CercaAsync(criteri, ct);

        // Gli importi sono informazione commerciale: si omettono a chi non è abilitato
        var mostraPrezzi = _utente.HaPermesso(Permessi.CommessePrezzi);

        return Json(new
        {
            Totale = righe.Count,
            MostraPrezzi = mostraPrezzi,
            Commesse = righe.Select(c => new
            {
                c.IdCommessa,
                c.IdTipoCommessa,
                c.TipoCommessa,
                c.RagioneSociale,
                c.NomeInsediamento,
                c.DescrizioneIncarico,
                c.IdControllo,
                c.Controllo,
                PrezzoControllo = mostraPrezzi ? c.PrezzoControllo : null,
                DataCommessa = c.DataCommessa?.ToString("dd/MM/yyyy"),
                c.NumeroContratto,
                c.Durata,
                c.ResponsabileCommessa,
                c.DiLaboratorio
            })
        });
    }

    [HttpGet("api/commesse/{idCommessa:int}/campioni")]
    public async Task<IActionResult> Campioni(int idCommessa, CancellationToken ct)
    {
        var righe = await _commesse.CampioniAsync(idCommessa, ct);

        return Json(righe.Select(c => new
        {
            c.IdCampione,
            c.NumeroCampione,
            DataRicezione = c.DataRicezione?.ToString("dd/MM/yyyy"),
            DataPrelievo  = c.DataPrelievo?.ToString("dd/MM/yyyy"),
            c.DenominazioneCampione,
            c.Matrice,
            c.LuogoPrelievo,
            c.VerbalePrelievo,
            c.NumeroProve,
            Stato = c.Stato.ToString()
        }));
    }

    [HttpGet("api/commesse/{idCommessa:int}/prestazioni")]
    public async Task<IActionResult> Prestazioni(int idCommessa, CancellationToken ct)
    {
        var righe = await _commesse.PrestazioniAsync(idCommessa, ct);

        return Json(righe.Select(p => new
        {
            p.Descrizione,
            DataInizio        = p.DataInizio?.ToString("dd/MM/yyyy"),
            DataFine          = p.DataFine?.ToString("dd/MM/yyyy"),
            ScadenzaCalcolata = p.ScadenzaCalcolata?.ToString("dd/MM/yyyy"),
            p.Responsabile,
            p.Fatturata,
            DataFattura = p.DataFattura?.ToString("dd/MM/yyyy"),
            p.InRitardo
        }));
    }
}
