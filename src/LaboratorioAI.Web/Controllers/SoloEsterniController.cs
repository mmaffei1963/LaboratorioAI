using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// "Solo esterni": confronto fra i certificati esterni caricati nel portale e i certificati
/// interni/ArcLab, con stato di fatturazione. Sola lettura su LabAnalisiDati.
/// </summary>
[RichiedePermesso(Permessi.SoloEsterniVisualizza)]
public class SoloEsterniController : Controller
{
    private const string MimeXlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRepositorySoloEsterni _repository;
    private readonly IEsportatoreSoloEsterni _esportatore;
    private readonly IServizioAudit _audit;

    public SoloEsterniController(IRepositorySoloEsterni repository, IEsportatoreSoloEsterni esportatore, IServizioAudit audit)
    {
        _repository = repository;
        _esportatore = esportatore;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Certificati esterni Non Ricertificati";
        return View();
    }

    [HttpPost("api/solo-esterni/cerca")]
    public async Task<IActionResult> Cerca([FromBody] CriteriSoloEsterni criteri, CancellationToken ct)
    {
        var righe = await _repository.CercaAsync(criteri, ct);
        return Json(new
        {
            Totale = righe.Count,
            SenzaInterno = righe.Count(r => !r.HaInterno),
            NonFatturati = righe.Count(r => !r.Fatturato),
            Righe = righe.Select(r => new
            {
                r.IdEntry, r.Anno, r.Lotto, r.Campione,
                r.IdClienteArcLab, r.RagioneSociale,
                DataUpload = r.DataUpload.ToString("dd/MM/yyyy"),
                r.NomeFile, r.HaInterno,
                r.IdCampione, HaCampione = r.IdCampione.HasValue,
                r.NCertificato, r.IdCertificato,
                DataFattura = r.DataFattura?.ToString("dd/MM/yyyy"),
                r.Fatturato,
                r.IdControllo, r.Controllo, r.PrezzoControllo
            })
        });
    }

    [HttpPost("api/solo-esterni/esporta")]
    public async Task<IActionResult> Esporta([FromBody] CriteriSoloEsterni criteri, CancellationToken ct)
    {
        var righe = await _repository.CercaAsync(criteri with { Limite = 50000 }, ct);
        var contenuto = _esportatore.Genera(righe);
        await _audit.RegistraAsync(
            AreaFunzionale.SoloEsterni, "esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile());
    }
}
