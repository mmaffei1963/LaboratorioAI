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
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
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
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
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

    // ── 5. Lotti senza ricevitore ───────────────────────────────────
    [HttpPost("api/ricerca-errori/lotti-senza-ricevitore")]
    public async Task<IActionResult> LottiSenzaRicevitore([FromBody] RichiestaAnni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await _repo.LottiSenzaRicevitoreAsync(da, a, ct);
        return Json(new { totale = righe.Count, righe = righe.Select(ProiettaLotto) });
    }

    [HttpPost("api/ricerca-errori/lotti-senza-ricevitore/esporta")]
    public async Task<IActionResult> LottiSenzaRicevitoreEsporta([FromBody] RichiestaAnni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await _repo.LottiSenzaRicevitoreAsync(da, a, ct);
        var contenuto = _esportatore.Lotti(righe, "Lotti senza ricevitore", da, a);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "lotti.senzaricevitore.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { AnnoDa = da, AnnoA = a, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("LottiSenzaRicevitore"));
    }

    // ── 6. Lotti senza vettore ──────────────────────────────────────
    [HttpPost("api/ricerca-errori/lotti-senza-vettore")]
    public async Task<IActionResult> LottiSenzaVettore([FromBody] RichiestaAnni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await _repo.LottiSenzaVettoreAsync(da, a, ct);
        return Json(new { totale = righe.Count, righe = righe.Select(ProiettaLotto) });
    }

    [HttpPost("api/ricerca-errori/lotti-senza-vettore/esporta")]
    public async Task<IActionResult> LottiSenzaVettoreEsporta([FromBody] RichiestaAnni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await _repo.LottiSenzaVettoreAsync(da, a, ct);
        var contenuto = _esportatore.Lotti(righe, "Lotti senza vettore", da, a);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "lotti.senzavettore.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { AnnoDa = da, AnnoA = a, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("LottiSenzaVettore"));
    }

    // ── 7. Coerenza prelevatore interno → vettore + verbale ─────────
    [HttpPost("api/ricerca-errori/coerenza-prelevatore")]
    public async Task<IActionResult> CoerenzaPrelevatore([FromBody] RichiestaAnni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await _repo.CoerenzaPrelevatoreVettoreAsync(da, a, ct);
        return Json(new
        {
            totale = righe.Count,
            vettore = righe.Count(r => r.AnomaliaVettore),
            verbale = righe.Count(r => r.AnomaliaVerbale),
            righe = righe.Select(r => new
            {
                r.IdLotto, r.CodLotto, dataRic = r.DataRic?.ToString("dd/MM/yyyy"),
                r.IdCampione, r.CodCampione, r.IdPrelevatore, r.Prelevatore,
                r.IdVettore, r.Vettore, r.BuonoPrelievo, r.AnomaliaVettore, r.AnomaliaVerbale
            })
        });
    }

    [HttpPost("api/ricerca-errori/coerenza-prelevatore/esporta")]
    public async Task<IActionResult> CoerenzaPrelevatoreEsporta([FromBody] RichiestaAnni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await _repo.CoerenzaPrelevatoreVettoreAsync(da, a, ct);
        var contenuto = _esportatore.Coerenza(righe, da, a);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "coerenza.prelevatore.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { AnnoDa = da, AnnoA = a, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("CoerenzaPrelevatore"));
    }

    // ── 8. Campioni «chiusi» non pronti alla stampa ────────────────
    [HttpPost("api/ricerca-errori/campioni-stampa")]
    public async Task<IActionResult> CampioniStampa([FromBody] RichiestaCampioniStampa req, CancellationToken ct)
    {
        var righe = await _repo.CampioniProntiStampaAsync(req.Da, req.A, req.CodLotto, req.CodCampione, ct);
        return Json(new
        {
            totale = righe.Count,
            campioni = righe.Select(r => r.IdCampione).Distinct().Count(),
            righe = righe.Select(r => new
            {
                r.IdLotto, r.CodLotto, dataRic = r.DataRic?.ToString("dd/MM/yyyy"),
                r.IdCampione, r.CodCampione, r.RagioneSociale,
                r.IdProva, r.IdRigaMetodo, r.DenominazioneRiga, r.NomeParametro,
                r.VerificaValore,
                dataInizioProva = r.DataInizioProva?.ToString("dd/MM/yyyy"),
                dataFineProva = r.DataFineProva?.ToString("dd/MM/yyyy"),
                r.Arrotonda, r.MaxDecimali
            })
        });
    }

    [HttpPost("api/ricerca-errori/campioni-stampa/esporta")]
    public async Task<IActionResult> CampioniStampaEsporta([FromBody] RichiestaCampioniStampa req, CancellationToken ct)
    {
        var righe = await _repo.CampioniProntiStampaAsync(req.Da, req.A, req.CodLotto, req.CodCampione, ct);
        var contenuto = _esportatore.Stampa(righe, PeriodoStampa(req));
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "campionistampa.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { req.CodLotto, req.CodCampione, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("CampioniStampa"));
    }

    private static string PeriodoStampa(RichiestaCampioniStampa req)
    {
        if (!string.IsNullOrWhiteSpace(req.CodLotto) || !string.IsNullOrWhiteSpace(req.CodCampione))
            return $"Lotto {req.CodLotto ?? "—"} / Campione {req.CodCampione ?? "—"}";
        var da = req.Da?.ToString("dd/MM/yyyy") ?? "(ultimi 30 gg)";
        var a = req.A?.ToString("dd/MM/yyyy") ?? DateTime.Today.ToString("dd/MM/yyyy");
        return $"dal {da} al {a}";
    }

    // ── Cruscotto: esegue i controlli selezionati ──────────────────
    [HttpPost("api/ricerca-errori/cruscotto")]
    public async Task<IActionResult> Cruscotto([FromBody] RichiestaCruscotto req, CancellationToken ct)
    {
        var d = await EseguiAsync(req, ct);
        var risultati = new[]
        {
            d.Duplicati      is null ? null : new { chiave = "duplicati",     nome = "Doppioni rapporti di prova",      totale = d.Duplicati.Count },
            d.Arrotondamento is null ? null : new { chiave = "arrotondamento", nome = "Metodi senza cifre/decimali",      totale = d.Arrotondamento.Count },
            d.Margine        is null ? null : new { chiave = "margine",        nome = "Metodi con margine sotto soglia",  totale = d.Margine.Count },
            d.Doppi          is null ? null : new { chiave = "doppi",          nome = "Metodi potenzialmente doppi",      totale = d.Doppi.Count },
            d.LottiRicevitore is null ? null : new { chiave = "ricevitore",    nome = "Lotti senza ricevitore",           totale = d.LottiRicevitore.Count },
            d.LottiVettore   is null ? null : new { chiave = "vettore",        nome = "Lotti senza vettore",              totale = d.LottiVettore.Count },
            d.Coerenza       is null ? null : new { chiave = "coerenza",       nome = "Coerenza prelevatore/vettore",     totale = d.Coerenza.Count },
            d.Stampa         is null ? null : new { chiave = "stampa",         nome = "Campioni non pronti alla stampa",  totale = d.Stampa.Count }
        };
        return Json(new { annoDa = d.AnnoDa, annoA = d.AnnoA, risultati = risultati.Where(x => x is not null) });
    }

    [HttpPost("api/ricerca-errori/esporta-tutto")]
    public async Task<IActionResult> EsportaTutto([FromBody] RichiestaCruscotto req, CancellationToken ct)
    {
        var d = await EseguiAsync(req, ct);
        var contenuto = _esportatore.Tutto(d);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "cruscotto.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { d.AnnoDa, d.AnnoA }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("Cruscotto"));
    }

    /// <summary>Esegue solo i controlli selezionati con i parametri condivisi del cruscotto.</summary>
    private async Task<DatiRicercaErrori> EseguiAsync(RichiestaCruscotto req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var soglia = ClampSoglia(req.SogliaPct);
        return new DatiRicercaErrori
        {
            AnnoDa = da,
            AnnoA = a,
            SogliaPct = soglia,
            PeriodoStampa = $"anni {da}–{a}",
            Duplicati       = req.Duplicati       ? await _repo.DuplicatiRapportiAsync(da, a, ct) : null,
            Arrotondamento  = req.Arrotondamento  ? await _repo.MetodiSenzaArrotondamentoAsync(ct) : null,
            Margine         = req.Margine         ? await _repo.MetodiMargineAsync(soglia / 100m, ct) : null,
            Doppi           = req.Doppi           ? await _repo.MetodiDoppiAsync(ct) : null,
            LottiRicevitore = req.LottiRicevitore ? await _repo.LottiSenzaRicevitoreAsync(da, a, ct) : null,
            LottiVettore    = req.LottiVettore    ? await _repo.LottiSenzaVettoreAsync(da, a, ct) : null,
            Coerenza        = req.Coerenza        ? await _repo.CoerenzaPrelevatoreVettoreAsync(da, a, ct) : null,
            Stampa          = req.Stampa          ? await _repo.CampioniProntiStampaAsync(new DateOnly(da, 1, 1), new DateOnly(a, 12, 31), null, null, ct) : null
        };
    }

    // ── Helper ──────────────────────────────────────────────────────
    private static object ProiettaLotto(RigaLottoErrore r) => new
    {
        r.IdLotto, r.CodLotto, dataRic = r.DataRic?.ToString("dd/MM/yyyy"), r.IdCommessa, r.RagioneSociale
    };

    private static (int Da, int A) NormalizzaAnni(int annoDa, int annoA)
    {
        int oggi = DateTime.Today.Year;
        int da = annoDa is > 1990 and < 2100 ? annoDa : oggi - 1;
        int a = annoA is > 1990 and < 2100 ? annoA : oggi;
        return da <= a ? (da, a) : (a, da);
    }

    private static decimal ClampSoglia(decimal pct) => pct <= 0 ? 15m : (pct > 100 ? 100m : pct);
    private static decimal Frazione(decimal pct) => ClampSoglia(pct) / 100m;
}

public record RichiestaDuplicati(int AnnoDa, int AnnoA);
public record RichiestaMargine(decimal SogliaPct);
public record RichiestaAnni(int AnnoDa, int AnnoA);
public record RichiestaCampioniStampa(DateOnly? Da, DateOnly? A, string? CodLotto, string? CodCampione);

public record RichiestaCruscotto(
    bool Duplicati, bool Arrotondamento, bool Margine, bool Doppi,
    bool LottiRicevitore, bool LottiVettore, bool Coerenza, bool Stampa,
    int AnnoDa, int AnnoA, decimal SogliaPct);
