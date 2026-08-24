using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// "Ricerca errori": verifiche diagnostiche in sola lettura su ArcLab (doppioni nei rapporti
/// di prova, metodi senza arrotondamento, margine sotto soglia, metodi doppi). Nessuna scrittura.
/// </summary>
[RichiedePermesso(Permessi.RicercaErroriVisualizza)]
public class RicercaErroriController : Controller
{
    private const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IRepositoryRicercaErrori _repo;
    private readonly IEsportatoreRicercaErrori _esportatore;
    private readonly IServizioAudit _audit;

    public RicercaErroriController(
        IRepositoryRicercaErrori repo, IEsportatoreRicercaErrori esportatore, IServizioAudit audit)
    {
        _repo = repo;
        _esportatore = esportatore;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Ricerca errori";
        return View();
    }

    // ── 1. Doppioni sui rapporti di prova ───────────────────────────
    [HttpPost("api/ricerca-errori/duplicati")]
    public async Task<IActionResult> Duplicati([FromBody] RichiestaDuplicati req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req);
        var righe = await _repo.DuplicatiRapportiAsync(da, a, ct);
        return Json(new
        {
            totale = righe.Count,
            campioni = righe.Select(r => r.IdCampione).Distinct().Count(),
            righe = righe.Select(r => new
            {
                r.IdCertificato, r.NCertificato,
                dataCertificato = r.DataCertificato?.ToString("dd/MM/yyyy"),
                r.IdCampione, r.CodCampione, r.IdParametro, r.NomeParametro, r.IdTipoParametro,
                r.IdRigaMetodo, r.DenominazioneRiga, r.IdProva, r.ValoreCalcolato,
                r.MaggioreDi, r.MinoreDi, r.Occorrenze
            })
        });
    }

    [HttpPost("api/ricerca-errori/duplicati/esporta")]
    public async Task<IActionResult> DuplicatiEsporta([FromBody] RichiestaDuplicati req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req);
        var righe = await _repo.DuplicatiRapportiAsync(da, a, ct);
        var contenuto = _esportatore.Duplicati(righe, da, a);
        await _audit.RegistraAsync(
            AreaFunzionale.RicercaErrori, "duplicati.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { AnnoDa = da, AnnoA = a, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("Duplicati"));
    }

    // ── 2. Metodi senza cifre significative / decimali max ──────────
    [HttpGet("api/ricerca-errori/arrotondamento")]
    public async Task<IActionResult> Arrotondamento(CancellationToken ct)
    {
        var righe = await _repo.MetodiSenzaArrotondamentoAsync(ct);
        return Json(new { totale = righe.Count, righe });
    }

    [HttpPost("api/ricerca-errori/arrotondamento/esporta")]
    public async Task<IActionResult> ArrotondamentoEsporta(CancellationToken ct)
    {
        var righe = await _repo.MetodiSenzaArrotondamentoAsync(ct);
        var contenuto = _esportatore.Arrotondamento(righe);
        await _audit.RegistraAsync(
            AreaFunzionale.RicercaErrori, "arrotondamento.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("Arrotondamento"));
    }

    // ── 3. Metodi con margine sotto soglia ──────────────────────────
    [HttpPost("api/ricerca-errori/margine")]
    public async Task<IActionResult> Margine([FromBody] RichiestaMargine req, CancellationToken ct)
    {
        var righe = await _repo.MetodiMargineAsync(Frazione(req.SogliaPct), ct);
        return Json(new { totale = righe.Count, righe });
    }

    [HttpPost("api/ricerca-errori/margine/esporta")]
    public async Task<IActionResult> MargineEsporta([FromBody] RichiestaMargine req, CancellationToken ct)
    {
        var soglia = ClampSoglia(req.SogliaPct);
        var righe = await _repo.MetodiMargineAsync(soglia / 100m, ct);
        var contenuto = _esportatore.Margine(righe, soglia);
        await _audit.RegistraAsync(
            AreaFunzionale.RicercaErrori, "margine.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { SogliaPct = soglia, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("Margine"));
    }

    // ── 4. Metodi potenzialmente doppi ──────────────────────────────
    [HttpGet("api/ricerca-errori/doppi")]
    public async Task<IActionResult> Doppi(CancellationToken ct)
    {
        var righe = await _repo.MetodiDoppiAsync(ct);
        return Json(new
        {
            totale = righe.Count,
            gruppi = righe.GroupBy(r => new { r.IdParametro, r.DettagliMetodo, r.RifMetodo }).Count(),
            righe
        });
    }

    [HttpPost("api/ricerca-errori/doppi/esporta")]
    public async Task<IActionResult> DoppiEsporta(CancellationToken ct)
    {
        var righe = await _repo.MetodiDoppiAsync(ct);
        var contenuto = _esportatore.Doppi(righe);
        await _audit.RegistraAsync(
            AreaFunzionale.RicercaErrori, "doppi.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("MetodiDoppi"));
    }

    // ── Helper ──────────────────────────────────────────────────────
    private static (int Da, int A) NormalizzaAnni(RichiestaDuplicati req)
    {
        int oggi = DateTime.Today.Year;
        int da = req.AnnoDa is > 1990 and < 2100 ? req.AnnoDa : oggi - 1;
        int a = req.AnnoA is > 1990 and < 2100 ? req.AnnoA : oggi;
        return da <= a ? (da, a) : (a, da);
    }

    private static decimal ClampSoglia(decimal pct) => pct <= 0 ? 15m : (pct > 100 ? 100m : pct);
    private static decimal Frazione(decimal pct) => ClampSoglia(pct) / 100m;
}

public record RichiestaDuplicati(int AnnoDa, int AnnoA);
public record RichiestaMargine(decimal SogliaPct);
