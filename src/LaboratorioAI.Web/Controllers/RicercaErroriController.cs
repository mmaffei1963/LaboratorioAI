using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
    private readonly ContestoLaboratorioAI _db;
    private readonly IContestoUtente _utente;

    public RicercaErroriController(
        IRepositoryRicercaErrori repo, IEsportatoreRicercaErrori esportatore, IServizioAudit audit,
        ContestoLaboratorioAI db, IContestoUtente utente)
    {
        _repo = repo;
        _esportatore = esportatore;
        _audit = audit;
        _db = db;
        _utente = utente;
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

    // ── 9. Denominazioni dei metodi (09/10/2026) ────────────────────
    // Metodi attivi la cui denominazione (Metodo.DettagliMetodo, stampata nel Rapporto di Prova) non cita il parametro o
    // è usata per parametri diversi. ArcLab in sola lettura; le verifiche «è corretta» stanno nel database di LabAI.
    [HttpPost("api/ricerca-errori/denominazioni")]
    public async Task<IActionResult> Denominazioni([FromBody] RichiestaDenominazioni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await DenominazioniAsync(da, a, req.AncheVerificate, ct);
        return Json(new
        {
            totale = righe.Count,
            metodi = righe.Select(r => r.IdMetodo).Distinct().Count(),
            conRapporti = righe.Where(r => r.NRapporti > 0).Select(r => r.IdMetodo).Distinct().Count(),
            annoDa = da, annoA = a,
            righe = righe.Select(r => new
            {
                r.Tipo, r.Motivo, r.IdMetodo, r.IdParametro, r.NomeParametro, r.DettagliMetodo, r.RifBiblio,
                r.NRapporti, ultimoRapporto = r.UltimoRapporto?.ToString("dd/MM/yyyy"),
                r.Verificata, r.VerificataDa, verificataIl = r.VerificataIl?.ToLocalTime().ToString("dd/MM/yyyy"), r.NoteVerifica
            })
        });
    }

    [HttpPost("api/ricerca-errori/denominazioni/esporta")]
    public async Task<IActionResult> DenominazioniEsporta([FromBody] RichiestaDenominazioni req, CancellationToken ct)
    {
        var (da, a) = NormalizzaAnni(req.AnnoDa, req.AnnoA);
        var righe = await DenominazioniAsync(da, a, req.AncheVerificate, ct);
        var contenuto = _esportatore.Denominazioni(righe, da, a);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "denominazioni.esportazione.excel", EsitoOperazione.Successo,
            dettaglio: new { AnnoDa = da, AnnoA = a, req.AncheVerificate, Righe = righe.Count }, ct: ct);
        return File(contenuto, MimeXlsx, _esportatore.NomeFile("Denominazioni"));
    }

    /// <summary>Segna una segnalazione come corretta (con nota) o annulla la verifica. Nessuna cancellazione.</summary>
    [HttpPost("api/ricerca-errori/denominazioni/verifica")]
    public async Task<IActionResult> DenominazioneVerifica([FromBody] RichiestaVerificaDenominazione req, CancellationToken ct)
    {
        if (req.Tipo is not (ControlloDenominazioniMetodi.TipoParametroAssente or ControlloDenominazioniMetodi.TipoDenominazioneCondivisa))
            return BadRequest(new { messaggio = "Tipo di segnalazione non valido." });
        var note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        if (note?.Length > 500) return BadRequest(new { messaggio = "Nota troppo lunga (massimo 500 caratteri)." });

        var esistenti = await _db.DenominazioniMetodoVerificate
            .Where(x => x.IdMetodoArcLab == req.IdMetodo && x.Tipo == req.Tipo && x.Attivo).ToListAsync(ct);
        var adesso = DateTime.UtcNow;
        foreach (var x in esistenti) { x.Attivo = false; x.DataCancellazione = adesso; }

        if (req.Corretta)
        {
            // La verifica vale per la denominazione e il parametro di oggi: si rileggono da ArcLab.
            var metodo = (await _repo.MetodiDenominazioneAsync(ct)).FirstOrDefault(m => m.IdMetodo == req.IdMetodo);
            if (metodo is null) return NotFound(new { messaggio = "Metodo non trovato tra quelli attivi in ArcLab." });
            _db.DenominazioniMetodoVerificate.Add(new DenominazioneMetodoVerificata
            {
                IdMetodoArcLab = req.IdMetodo, Tipo = req.Tipo,
                DenominazioneVerificata = Taglia(metodo.DettagliMetodo?.Trim(), 500),
                ParametroVerificato = Taglia(metodo.NomeParametro?.Trim(), 300),
                Note = note, IdUtente = _utente.IdUtente,
                NomeUtente = Taglia(_utente.NomeCompleto ?? _utente.Email, 200),
                DataCreazione = adesso
            });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori,
            req.Corretta ? "denominazione.verificata" : "denominazione.verifica-annullata", EsitoOperazione.Successo,
            tipoEntita: "Metodo", idEntita: req.IdMetodo.ToString(), dettaglio: new { req.IdMetodo, req.Tipo, Note = note }, ct: ct);
        return Json(new { ok = true });
    }

    private async Task<List<RigaDenominazioneSospetta>> DenominazioniAsync(int da, int a, bool ancheVerificate, CancellationToken ct)
    {
        var anomalie = ControlloDenominazioniMetodi.Analizza(await _repo.MetodiDenominazioneAsync(ct));
        var verifiche = await _db.DenominazioniMetodoVerificate.AsNoTracking().Where(x => x.Attivo).ToListAsync(ct);
        var rapporti = (await _repo.RapportiPerMetodoAsync(anomalie.Select(x => x.Metodo.IdMetodo).Distinct().ToList(), da, a, ct))
            .ToDictionary(x => x.IdMetodo);

        var righe = new List<RigaDenominazioneSospetta>();
        foreach (var x in anomalie)
        {
            // Una verifica vale solo se denominazione e parametro sono ancora quelli verificati.
            var v = verifiche.FirstOrDefault(y => y.IdMetodoArcLab == x.Metodo.IdMetodo && y.Tipo == x.Tipo
                && string.Equals(y.DenominazioneVerificata, Taglia(x.Metodo.DettagliMetodo?.Trim(), 500), StringComparison.Ordinal)
                && string.Equals(y.ParametroVerificato, Taglia(x.Metodo.NomeParametro?.Trim(), 300), StringComparison.Ordinal));
            if (v is not null && !ancheVerificate) continue;
            rapporti.TryGetValue(x.Metodo.IdMetodo, out var r);
            righe.Add(new RigaDenominazioneSospetta
            {
                Tipo = x.Tipo, Motivo = x.Motivo, IdMetodo = x.Metodo.IdMetodo, IdParametro = x.Metodo.IdParametro,
                NomeParametro = x.Metodo.NomeParametro?.Trim(), DettagliMetodo = x.Metodo.DettagliMetodo?.Trim(),
                RifBiblio = x.Metodo.RifBiblio?.Trim(), NRapporti = r?.NRapporti ?? 0, UltimoRapporto = r?.UltimoRapporto,
                Verificata = v is not null, VerificataDa = v?.NomeUtente, VerificataIl = v?.DataCreazione, NoteVerifica = v?.Note
            });
        }
        // Prima le non verificate con rapporti emessi (le più urgenti), poi per parametro.
        return righe.OrderBy(r => r.Verificata).ThenByDescending(r => r.NRapporti > 0)
            .ThenBy(r => r.NomeParametro).ThenBy(r => r.IdMetodo).ToList();
    }

    private static string? Taglia(string? s, int n) => s is null ? null : s.Length <= n ? s : s[..n];

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

    // ── 10. Prezzi commesse ArcLab ↔ listini Marea (28/10/2026) ────
    [HttpGet("api/ricerca-errori/prezzi-marea")]
    public async Task<IActionResult> PrezziMarea(CancellationToken ct)
    {
        var righe = await PrezziMareaAsync(ct);
        return Json(new
        {
            totale = righe.Count,
            casi = righe.SelectMany(r => r.Caso.Split("; ")).GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count()),
            righe = righe.Select(r => new
            {
                r.Caso, r.IdCliente, r.Cliente, r.Insediamento, r.IdControllo, r.Controllo, r.IdCommessa,
                r.PrezzoArcLab, r.PrezzoMarea, r.Differenza, ultimaModificaMarea = r.UltimaModificaMarea?.ToString("dd/MM/yyyy HH:mm"),
                dataCommessa = r.DataCommessa?.ToString("dd/MM/yyyy"), dataUltimoRinnovo = r.DataUltimoRinnovo?.ToString("dd/MM/yyyy"), ultimoCampione = r.UltimoCampione?.ToString("dd/MM/yyyy")
            })
        });
    }

    [HttpPost("api/ricerca-errori/prezzi-marea/esporta")]
    public async Task<IActionResult> PrezziMareaEsporta(CancellationToken ct)
    {
        var righe = await PrezziMareaAsync(ct);
        using var libro = new ClosedXML.Excel.XLWorkbook();
        var ws = libro.AddWorksheet("Prezzi ArcLab-Marea");
        string[] h = ["Caso", "Id cliente", "Cliente", "Insediamento", "Id controllo", "Controllo", "Id commessa", "Prezzo ArcLab", "Prezzo Marea", "Differenza", "Ultima modifica Marea",
            "Data commessa ArcLab", "Ultimo rinnovo ArcLab", "Ultimo campione"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var n = 2;
        foreach (var r in righe)
        {
            ws.Cell(n, 1).Value = r.Caso; ws.Cell(n, 2).Value = r.IdCliente; ws.Cell(n, 3).Value = r.Cliente; ws.Cell(n, 4).Value = r.Insediamento;
            ws.Cell(n, 5).Value = r.IdControllo; ws.Cell(n, 6).Value = r.Controllo;
            if (r.IdCommessa is int ic) ws.Cell(n, 7).Value = ic;
            if (r.PrezzoArcLab is decimal pa) ws.Cell(n, 8).Value = pa;
            ws.Cell(n, 9).Value = r.PrezzoMarea;
            if (r.Differenza is decimal d) ws.Cell(n, 10).Value = d;
            if (r.UltimaModificaMarea is DateTime um) ws.Cell(n, 11).Value = um;
            if (r.DataCommessa is DateTime dc) ws.Cell(n, 12).Value = dc;
            if (r.DataUltimoRinnovo is DateTime dr) ws.Cell(n, 13).Value = dr;
            if (r.UltimoCampione is DateTime uc) ws.Cell(n, 14).Value = uc;
            n++;
        }
        ws.Column(8).Style.NumberFormat.Format = "#,##0.00"; ws.Column(10).Style.NumberFormat.Format = "#,##0.00;[Red]-#,##0.00";
        ws.Column(11).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        ws.Columns(12, 14).Style.DateFormat.Format = "dd/MM/yyyy";
        if (n > 2) ws.Range(1, 1, n - 1, h.Length).SetAutoFilter();
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "prezzimarea.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { Righe = righe.Count }, ct: ct);
        return File(ms.ToArray(), MimeXlsx, _esportatore.NomeFile("PrezziArcLabMarea"));
    }

    private async Task<List<RigaPrezzoMarea>> PrezziMareaAsync(CancellationToken ct)
    {
        var (commesse, listini) = await _repo.PrezziCommesseMareaAsync(ct);
        return ConfrontoPrezziMarea.Confronta(commesse, listini);
    }

    // ── 11. Commesse ArcLab ↔ Marea oltre ai prezzi (28/10/2026) ───
    [HttpGet("api/ricerca-errori/commesse-marea")]
    public async Task<IActionResult> CommesseMarea(CancellationToken ct)
    {
        var righe = await CommesseMareaAsync(ct);
        return Json(new
        {
            totale = righe.Count,
            casi = righe.GroupBy(r => r.Caso).ToDictionary(g => g.Key, g => g.Count()),
            righe = righe.Select(r => new
            {
                r.Caso, r.IdCliente, r.Cliente, r.Insediamento, r.IdControllo, r.Controllo, r.IdCommessaArcLab, r.IdCommessaMarea, r.Dettaglio,
                dataCommessa = r.DataCommessa?.ToString("dd/MM/yyyy"), dataUltimoRinnovo = r.DataUltimoRinnovo?.ToString("dd/MM/yyyy"), ultimoCampione = r.UltimoCampione?.ToString("dd/MM/yyyy"),
                ultimaModificaMarea = r.UltimaModificaMarea?.ToString("dd/MM/yyyy HH:mm")
            })
        });
    }

    [HttpPost("api/ricerca-errori/commesse-marea/esporta")]
    public async Task<IActionResult> CommesseMareaEsporta(CancellationToken ct)
    {
        var righe = await CommesseMareaAsync(ct);
        using var libro = new ClosedXML.Excel.XLWorkbook();
        var ws = libro.AddWorksheet("Commesse ArcLab-Marea");
        string[] h = ["Caso", "Id cliente", "Cliente", "Insediamento", "Id controllo", "Controllo", "Commessa ArcLab", "Commessa Marea", "Dettaglio",
            "Data commessa ArcLab", "Ultimo rinnovo ArcLab", "Ultima modifica listino Marea", "Ultimo campione"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var n = 2;
        foreach (var r in righe)
        {
            ws.Cell(n, 1).Value = r.Caso;
            if (r.IdCliente is int ic) ws.Cell(n, 2).Value = ic;
            ws.Cell(n, 3).Value = r.Cliente; ws.Cell(n, 4).Value = r.Insediamento;
            if (r.IdControllo is int ictrl) ws.Cell(n, 5).Value = ictrl;
            ws.Cell(n, 6).Value = r.Controllo;
            if (r.IdCommessaArcLab is int ca) ws.Cell(n, 7).Value = ca;
            if (r.IdCommessaMarea is int cm) ws.Cell(n, 8).Value = cm;
            ws.Cell(n, 9).Value = r.Dettaglio;
            if (r.DataCommessa is DateTime dc) ws.Cell(n, 10).Value = dc;
            if (r.DataUltimoRinnovo is DateTime dr) ws.Cell(n, 11).Value = dr;
            if (r.UltimaModificaMarea is DateTime um) ws.Cell(n, 12).Value = um;
            if (r.UltimoCampione is DateTime uc) ws.Cell(n, 13).Value = uc;
            n++;
        }
        if (n > 2) ws.Range(1, 1, n - 1, h.Length).SetAutoFilter();
        ws.Columns(10, 11).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Column(12).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
        ws.Column(13).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Columns(1, 8).AdjustToContents();
        ws.Columns(10, 13).AdjustToContents();
        ws.Column(9).Width = 80;
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "commessemarea.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { Righe = righe.Count }, ct: ct);
        return File(ms.ToArray(), MimeXlsx, _esportatore.NomeFile("CommesseArcLabMarea"));
    }

    private async Task<List<RigaCommessaMarea>> CommesseMareaAsync(CancellationToken ct)
    {
        var d = await _repo.CommesseMareaAsync(ct);
        return ConfrontoCommesseMarea.Confronta(d.Commesse, d.Parametri, d.Controlli, d.CommesseMarea, d.Listini, d.ClientiNonAttivi);
    }

    // ── 12. Commesse attive senza numero di contratto (28/10/2026) ──
    [HttpGet("api/ricerca-errori/senza-contratto")]
    public async Task<IActionResult> SenzaContratto(CancellationToken ct)
    {
        var righe = await _repo.CommesseSenzaContrattoAsync(ct);
        var unAnnoFa = DateTime.Today.AddYears(-1);
        return Json(new
        {
            totale = righe.Count,
            ultimoAnno = righe.Count(r => r.UltimoCampione >= unAnnoFa),
            maiUsate = righe.Count(r => r.UltimoCampione is null),
            righe = righe.Select(r => new
            {
                r.IdCommessa, r.CodCommessa, r.IdCliente, r.RagioneSociale, r.CodContabilita, r.Insediamento, r.IdControllo, r.Controllo,
                r.DescrIncarico, r.Prezzo, dataCommessa = r.DataCommessa?.ToString("dd/MM/yyyy"),
                ultimoCampione = r.UltimoCampione?.ToString("dd/MM/yyyy"), ultimoCampioneIso = r.UltimoCampione?.ToString("yyyy-MM-dd"), r.Lotti
            })
        });
    }

    [HttpPost("api/ricerca-errori/senza-contratto/esporta")]
    public async Task<IActionResult> SenzaContrattoEsporta(CancellationToken ct)
    {
        var righe = await _repo.CommesseSenzaContrattoAsync(ct);
        using var libro = new ClosedXML.Excel.XLWorkbook();
        var ws = libro.AddWorksheet("Commesse senza contratto");
        string[] h = ["Id commessa", "Cod. commessa", "Id cliente", "Cliente", "Cod. contabilità", "Insediamento", "Id controllo", "Controllo",
            "Descrizione incarico", "Prezzo", "Data commessa", "Ultimo campione", "Lotti"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var n = 2;
        foreach (var r in righe)
        {
            ws.Cell(n, 1).Value = r.IdCommessa; ws.Cell(n, 2).Value = r.CodCommessa; ws.Cell(n, 3).Value = r.IdCliente;
            ws.Cell(n, 4).Value = r.RagioneSociale; ws.Cell(n, 5).Value = r.CodContabilita; ws.Cell(n, 6).Value = r.Insediamento;
            if (r.IdControllo is int ic) ws.Cell(n, 7).Value = ic;
            ws.Cell(n, 8).Value = r.Controllo; ws.Cell(n, 9).Value = r.DescrIncarico?.Trim();
            if (r.Prezzo is decimal p) ws.Cell(n, 10).Value = p;
            if (r.DataCommessa is DateTime dc) ws.Cell(n, 11).Value = dc;
            if (r.UltimoCampione is DateTime uc) ws.Cell(n, 12).Value = uc;
            ws.Cell(n, 13).Value = r.Lotti;
            n++;
        }
        ws.Column(10).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns(11, 12).Style.DateFormat.Format = "dd/MM/yyyy";
        if (n > 2) ws.Range(1, 1, n - 1, h.Length).SetAutoFilter();
        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await _audit.RegistraAsync(AreaFunzionale.RicercaErrori, "senzacontratto.esportazione.excel",
            EsitoOperazione.Successo, dettaglio: new { Righe = righe.Count }, ct: ct);
        return File(ms.ToArray(), MimeXlsx, _esportatore.NomeFile("CommesseSenzaContratto"));
    }

    // ── Cruscotto: esegue i controlli selezionati ──────────────────
    [HttpPost("api/ricerca-errori/cruscotto")]
    public async Task<IActionResult> Cruscotto([FromBody] RichiestaCruscotto req, CancellationToken ct)
    {
        var d = await EseguiAsync(req, ct);
        // 28/10/2026: commesse attive con prezzo diverso da quello del listino Marea (conteggio).
        int? prezziDiversi = req.PrezziMarea
            ? (await PrezziMareaAsync(ct)).Count(r => r.Caso.Contains(ConfrontoPrezziMarea.PrezzoDiverso))
            : null;
        int? commesseDiverse = req.CommesseMarea ? (await CommesseMareaAsync(ct)).Count : null;
        int? senzaContratto = req.SenzaContratto ? (await _repo.CommesseSenzaContrattoAsync(ct)).Count : null;
        var risultati = new[]
        {
            d.Duplicati      is null ? null : new { chiave = "duplicati",     nome = "Doppioni rapporti di prova",      totale = d.Duplicati.Count },
            d.Arrotondamento is null ? null : new { chiave = "arrotondamento", nome = "Metodi senza cifre/decimali",      totale = d.Arrotondamento.Count },
            d.Margine        is null ? null : new { chiave = "margine",        nome = "Metodi con margine sotto soglia",  totale = d.Margine.Count },
            d.Doppi          is null ? null : new { chiave = "doppi",          nome = "Metodi potenzialmente doppi",      totale = d.Doppi.Count },
            d.LottiRicevitore is null ? null : new { chiave = "ricevitore",    nome = "Lotti senza ricevitore",           totale = d.LottiRicevitore.Count },
            d.LottiVettore   is null ? null : new { chiave = "vettore",        nome = "Lotti senza vettore",              totale = d.LottiVettore.Count },
            d.Coerenza       is null ? null : new { chiave = "coerenza",       nome = "Coerenza prelevatore/vettore",     totale = d.Coerenza.Count },
            d.Stampa         is null ? null : new { chiave = "stampa",         nome = "Campioni non pronti alla stampa",  totale = d.Stampa.Count },
            prezziDiversi    is not int pd ? null : new { chiave = "prezzimarea", nome = "Commesse con prezzo diverso da Marea", totale = pd },
            commesseDiverse  is not int cd ? null : new { chiave = "commessemarea", nome = "Altre differenze commesse ArcLab/Marea", totale = cd },
            senzaContratto   is not int sc ? null : new { chiave = "senzacontratto", nome = "Commesse attive senza numero di contratto", totale = sc }
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
public record RichiestaDenominazioni(int AnnoDa, int AnnoA, bool AncheVerificate);
public record RichiestaVerificaDenominazione(int IdMetodo, string Tipo, bool Corretta, string? Note);
public record RichiestaMargine(decimal SogliaPct);
public record RichiestaAnni(int AnnoDa, int AnnoA);
public record RichiestaCampioniStampa(DateOnly? Da, DateOnly? A, string? CodLotto, string? CodCampione);

public record RichiestaCruscotto(
    bool Duplicati, bool Arrotondamento, bool Margine, bool Doppi,
    bool LottiRicevitore, bool LottiVettore, bool Coerenza, bool Stampa,
    int AnnoDa, int AnnoA, decimal SogliaPct, bool PrezziMarea = false, bool CommesseMarea = false, bool SenzaContratto = false);
