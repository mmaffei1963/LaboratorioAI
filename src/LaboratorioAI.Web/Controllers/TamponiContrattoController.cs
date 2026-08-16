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
/// Report "Tamponi in contratto": tamponi fatturabili nel periodo con verifica
/// del contratto di consulenza HACCP che li comprende. Sola lettura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.PrefatturatoTamponi)]
public class TamponiContrattoController : Controller
{
    private const string MimeXlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRepositoryTamponiContratto _repository;
    private readonly IEsportatoreTamponiContratto _esportatore;
    private readonly IServizioAudit _audit;

    public TamponiContrattoController(
        IRepositoryTamponiContratto repository,
        IEsportatoreTamponiContratto esportatore,
        IServizioAudit audit)
    {
        _repository = repository;
        _esportatore = esportatore;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Tamponi in contratto";
        return View();
    }

    [HttpPost("api/tamponi-contratto/interroga")]
    public async Task<IActionResult> Interroga(
        [FromBody] CriteriTamponiContratto criteri, CancellationToken ct)
    {
        var esito = await _repository.InterrogaAsync(criteri, ct);

        return Json(new
        {
            esito.TotaleTamponi,
            esito.InContratto,
            esito.FuoriContratto,
            esito.InsediamentiDistinti,
            esito.Troncato,
            Riepilogo = esito.Riepilogo.Select(r => new
            {
                r.IdCliente,
                r.IdInsediamento,
                r.RagioneSociale,
                r.NomeInsediamento,
                r.ContrattoHaccp,
                r.TamponiInclusi,
                r.TamponiFatti,
                r.Differenza,
                r.Prelevatori
            }),
            Tamponi = esito.Tamponi.Select(t => new
            {
                t.IdCliente,
                t.IdInsediamento,
                t.NumeroCampione,
                t.RagioneSociale,
                t.NomeInsediamento,
                t.IdControllo,
                t.Controllo,
                t.NumeroCertificato,
                DataCertificato = t.DataCertificato?.ToString("dd/MM/yyyy"),
                t.Prelevatore,
                t.Ricevitore,
                t.NonFatturare,
                t.ContrattoHaccp,
                t.TamponiInclusi
            })
        });
    }

    [HttpPost("api/tamponi-contratto/esporta")]
    public async Task<IActionResult> Esporta(
        [FromBody] CriteriTamponiContratto criteri, CancellationToken ct)
    {
        // L'esportazione non è limitata come la visualizzazione a schermo
        var criteriEstesi = criteri with { Limite = 50000 };

        var esito = await _repository.InterrogaAsync(criteriEstesi, ct);

        // L'Excel riflette i filtri impostati a video (HACCP, prelevatore, insediamento)
        var tamponi = esito.Tamponi.Where(t =>
            (criteri.SoloHaccp is null || t.ContrattoHaccp == criteri.SoloHaccp) &&
            (criteri.IdInsediamento is null || t.IdInsediamento == criteri.IdInsediamento) &&
            (string.IsNullOrWhiteSpace(criteri.Prelevatore) || t.Prelevatore == criteri.Prelevatore) &&
            (string.IsNullOrWhiteSpace(criteri.Ricevitore) || t.Ricevitore == criteri.Ricevitore))
            .ToList();

        var esitoFiltrato = esito with
        {
            Tamponi = tamponi,
            Riepilogo = RiepilogoInsediamentoTamponi.Da(tamponi)
        };

        var contenuto = _esportatore.Genera(esitoFiltrato, criteriEstesi);

        await _audit.RegistraAsync(
            AreaFunzionale.Prefatturato,
            "tamponi.esportazione.excel",
            EsitoOperazione.Successo,
            dettaglio: new
            {
                Dal = criteri.DataDa.ToString("yyyy-MM-dd"),
                Al = criteri.DataA.ToString("yyyy-MM-dd"),
                Tamponi = esitoFiltrato.TotaleTamponi
            },
            ct: ct);

        return File(contenuto, MimeXlsx, _esportatore.NomeFile(criteriEstesi));
    }
}
