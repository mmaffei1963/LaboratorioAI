using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.ArcLab;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Controllo pubblicazione certificati (16/11/2026): confronto tra i certificati spediti in ArcLab (DataSped) e quelli
/// caricati sul portale clienti (Marea.Entries: DataUpload, DataFirma). Sola lettura su entrambi i database.
/// </summary>
[RichiedePermesso(Permessi.CertificatiControlloPubblicazione)]
public class ControlloPubblicazioneController : Controller
{
    private const int MassimoGiorni = 186;
    private readonly RepositoryControlloPubblicazione _repository;
    private readonly IServizioAudit _audit;

    public ControlloPubblicazioneController(IFabbricaConnessioniArcLab fabbrica, IServizioAudit audit)
    {
        _repository = new RepositoryControlloPubblicazione(fabbrica);
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Controllo pubblicazione certificati";
        return View();
    }

    private static string? Verifica(DateOnly dal, DateOnly al)
    {
        if (al < dal) return "La data «al» è precedente alla data «dal».";
        if (al.DayNumber - dal.DayNumber > MassimoGiorni) return "Periodo troppo lungo: al massimo 6 mesi per volta.";
        return null;
    }

    [HttpGet("api/controllo-pubblicazione/cerca")]
    public async Task<IActionResult> Cerca([FromQuery] DateOnly dal, [FromQuery] DateOnly al, [FromQuery] bool escludiEsterni = true, CancellationToken ct = default)
    {
        if (Verifica(dal, al) is string errore) return BadRequest(new { messaggio = errore });
        var esito = await _repository.ConfrontaAsync(dal, al, escludiEsterni, ct: ct);
        return Ok(new
        {
            dateDiverse = esito.DateDiverse.Select(d => new
            {
                d.NCert, d.DataSped, d.DataUpload, d.DataFirma, d.GiorniUpload, d.GiorniFirma, d.Tipo, d.NomeFile,
                firmatoDopo = d.FirmatoDopoPubblicazione
            }),
            esito.SoloArcLab, esito.SoloPortale, esito.Doppioni
        });
    }

    [HttpGet("api/controllo-pubblicazione/excel")]
    public async Task<IActionResult> Excel([FromQuery] DateOnly dal, [FromQuery] DateOnly al, [FromQuery] bool escludiEsterni = true, CancellationToken ct = default)
    {
        if (Verifica(dal, al) is string errore) return BadRequest(new { messaggio = errore });
        var esito = await _repository.ConfrontaAsync(dal, al, escludiEsterni, ct: ct);
        await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "controllopubblicazione.excel", EsitoOperazione.Successo,
            dettaglio: new { dal, al, escludiEsterni, esito.DateDiverse.Count, SoloArcLab = esito.SoloArcLab.Count, SoloPortale = esito.SoloPortale.Count }, ct: ct);
        var file = EsportatoreControlloPubblicazione.Genera(esito, dal, al, escludiEsterni);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Controllo_pubblicazione_{dal:yyyyMMdd}-{al:yyyyMMdd}.xlsx");
    }
}
