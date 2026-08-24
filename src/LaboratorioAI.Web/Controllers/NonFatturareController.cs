using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Report "Non fatturare": campioni con il flag NonFatturare attivo, per
/// prelevatore. Sola lettura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.PrefatturatoNonFatturare)]
public class NonFatturareController : Controller
{
    private const string MimeXlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRepositoryNonFatturare _repository;
    private readonly IEsportatoreNonFatturare _esportatore;
    private readonly IServizioAudit _audit;

    public NonFatturareController(
        IRepositoryNonFatturare repository, IEsportatoreNonFatturare esportatore, IServizioAudit audit)
    {
        _repository = repository;
        _esportatore = esportatore;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Non fatturare";
        return View();
    }

    [HttpPost("api/non-fatturare/interroga")]
    public async Task<IActionResult> Interroga(
        [FromBody] CriteriNonFatturare criteri, CancellationToken ct)
    {
        var esito = await _repository.InterrogaAsync(criteri, ct);

        return Json(new
        {
            esito.Totale,
            esito.Troncato,
            Campioni = esito.Campioni.Select(c => new
            {
                c.IdCampione,
                c.NumeroCampione,
                c.IdCliente,
                c.RagioneSociale,
                c.IdInsediamento,
                c.NomeInsediamento,
                c.IdControllo,
                c.Controllo,
                DataRicezione = c.DataRicezione?.ToString("dd/MM/yyyy"),
                c.Prelevatore,
                c.Ricevitore,
                c.PrezzoControllo,
                c.ContrattoHaccp
            })
        });
    }

    [HttpPost("api/non-fatturare/esporta")]
    public async Task<IActionResult> Esporta(
        [FromBody] CriteriNonFatturare criteri, CancellationToken ct)
    {
        var criteriEstesi = criteri with { Limite = 100000 };
        var esito = await _repository.InterrogaAsync(criteriEstesi, ct);

        // L'Excel rispecchia i filtri impostati a video.
        static bool Contiene(string? v, string? f) =>
            string.IsNullOrWhiteSpace(f)
            || (v ?? "").Contains(f.Trim(), StringComparison.OrdinalIgnoreCase);

        var campioni = esito.Campioni.Where(c =>
            Contiene(c.RagioneSociale, criteri.Cliente) &&
            Contiene(c.NomeInsediamento, criteri.Insediamento) &&
            (criteri.SoloHaccp is null || c.ContrattoHaccp == criteri.SoloHaccp) &&
            (string.IsNullOrWhiteSpace(criteri.Prelevatore)
                || EsportatoreNonFatturare.Prelevatore(c.Prelevatore) == criteri.Prelevatore) &&
            (string.IsNullOrWhiteSpace(criteri.Ricevitore) || (c.Ricevitore ?? "") == criteri.Ricevitore) &&
            (criteri.Controlli is not { Count: > 0 } || criteri.Controlli.Contains(c.IdControllo)))
            .ToList();

        var contenuto = _esportatore.Genera(campioni, criteriEstesi);
        await _audit.RegistraAsync(
            AreaFunzionale.Prefatturato, "nonfatturare.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { Campioni = campioni.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile(criteriEstesi));
    }
}
