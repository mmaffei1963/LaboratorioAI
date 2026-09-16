using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Import;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Scadenziario tarature: strumenti interni da ArcLab (raggruppati per tipo),
/// archivio/scadenziario dei termometri clienti (esterni), generatore etichette.
/// </summary>
[RichiedePermesso(Permessi.ScadenziarioVisualizza)]
public class ScadenziarioController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryScadenziario _repo;
    private readonly IImportatoreTermometriEsterni _import;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public ScadenziarioController(
        ContestoLaboratorioAI ctx, IRepositoryScadenziario repo, IImportatoreTermometriEsterni import,
        IRepositoryAnagrafiche anagrafiche, IContestoUtente utente, IServizioAudit audit)
    {
        _ctx = ctx; _repo = repo; _import = import; _anagrafiche = anagrafiche; _utente = utente; _audit = audit;
    }

    [HttpGet("api/scadenziario/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Json(await _anagrafiche.ClientiAsync(ct));

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Scadenziario tarature";
        return View();
    }

    // ── Interni (ArcLab), raggruppati per tipo ──────────────────────
    [HttpGet("api/scadenziario/interni")]
    public async Task<IActionResult> Interni([FromQuery] int giorni = 30, CancellationToken ct = default)
    {
        var voci = await _repo.ScadenziarioInterniAsync(giorni, ct);
        var gruppi = voci
            .GroupBy(v => v.Gruppo)
            .OrderBy(g => (int)g.Key)
            .Select(g => new
            {
                gruppo = ClassificatoreStrumento.Etichetta(g.Key),
                voci = g.OrderBy(v => v.Scadenza ?? DateTime.MaxValue).Select(v => new
                {
                    v.IdApparecchio, v.IdStrumento, v.Strumento, v.Attivita, v.NomePiano,
                    v.UltimaTaratura, v.Scadenza, v.FreqGiorni, v.GiorniAllaScadenza
                })
            });
        return Json(gruppi);
    }

    // ── Esterni (clienti) ───────────────────────────────────────────
    [HttpGet("api/scadenziario/esterni")]
    public async Task<IActionResult> Esterni(CancellationToken ct)
    {
        var righe = await _ctx.TermometriEsterni.AsNoTracking()
            .Where(t => t.Attivo && t.DataCancellazione == null)
            .OrderByDescending(t => t.DataScadenza)
            .Select(t => new
            {
                t.IdTermometroEsterno, t.NumeroRapporto, t.Tipologia, t.DataTaratura, t.DataScadenza,
                t.Cliente, t.Riferimento, t.Note, t.DataConsegna, t.OperatoreConsegna, t.RifFattura, t.Fatturato,
                t.EscludiScadenze
            }).ToListAsync(ct);
        return Json(righe);
    }

    [HttpPost("api/scadenziario/esterni")]
    [RichiedePermesso(Permessi.ScadenziarioGestione)]
    public async Task<IActionResult> SalvaEsterno([FromBody] TermometroEsternoDto req, CancellationToken ct)
    {
        var t = req.IdTermometroEsterno is int id and > 0
            ? await _ctx.TermometriEsterni.FirstOrDefaultAsync(x => x.IdTermometroEsterno == id, ct)
            : null;
        if (t is null) { t = new TermometroEsterno { DataCreazione = DateTime.UtcNow }; _ctx.TermometriEsterni.Add(t); }
        t.NumeroRapporto = req.NumeroRapporto; t.Tipologia = req.Tipologia;
        t.DataTaratura = req.DataTaratura; t.DataScadenza = req.DataScadenza;
        t.Cliente = req.Cliente; t.Riferimento = req.Riferimento; t.Note = req.Note;
        t.DataConsegna = req.DataConsegna; t.OperatoreConsegna = req.OperatoreConsegna; t.RifFattura = req.RifFattura;
        t.Fatturato = req.Fatturato;
        t.EscludiScadenze = req.EscludiScadenze;
        t.DataUltimaModifica = DateTime.UtcNow; t.IdUtente = _utente.IdUtente;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Scadenziario, "scadenziario.esterno.salvato", EsitoOperazione.Successo,
            tipoEntita: "TermometroEsterno", idEntita: t.IdTermometroEsterno.ToString(),
            dettaglio: new { t.NumeroRapporto, t.Cliente, t.Fatturato }, ct: ct);
        return Json(new { t.IdTermometroEsterno });
    }

    [HttpPost("api/scadenziario/esterni/{id:int}/elimina")]
    [RichiedePermesso(Permessi.ScadenziarioGestione)]
    public async Task<IActionResult> EliminaEsterno(int id, CancellationToken ct)
    {
        var t = await _ctx.TermometriEsterni.FirstOrDefaultAsync(x => x.IdTermometroEsterno == id, ct);
        if (t is null) return NotFound();
        t.Attivo = false; t.DataCancellazione = DateTime.UtcNow; t.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Scadenziario, "scadenziario.esterno.eliminato", EsitoOperazione.Successo,
            tipoEntita: "TermometroEsterno", idEntita: id.ToString(), ct: ct);
        return Ok();
    }

    /// <summary>Svuota l'archivio importato (righe non provenienti da una taratura in app), per reimportare pulito.</summary>
    [HttpPost("api/scadenziario/esterni/svuota-importati")]
    [RichiedePermesso(Permessi.ScadenziarioGestione)]
    public async Task<IActionResult> SvuotaImportati(CancellationToken ct)
    {
        var righe = await _ctx.TermometriEsterni
            .Where(t => t.IdTaraturaOrigine == null && t.DataCancellazione == null).ToListAsync(ct);
        var ora = DateTime.UtcNow;
        foreach (var t in righe) { t.Attivo = false; t.DataCancellazione = ora; t.DataUltimaModifica = ora; }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Scadenziario, "scadenziario.esterni.svuotati", EsitoOperazione.Successo,
            dettaglio: new { Rimossi = righe.Count }, ct: ct);
        return Json(new { rimossi = righe.Count });
    }

    [HttpPost("api/scadenziario/esterni/importa")]
    [RichiedePermesso(Permessi.ScadenziarioGestione)]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> ImportaEsterni(IFormFile? file, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file caricato." });
        IReadOnlyList<TermometroEsterno> righe;
        try
        {
            await using var s = file.OpenReadStream();
            using var ms = new MemoryStream(); await s.CopyToAsync(ms, ct); ms.Position = 0;
            righe = _import.Leggi(ms);
        }
        catch (Exception ex) { return BadRequest(new { messaggio = "File non leggibile: " + ex.Message }); }

        static DateTime Q(TermometroEsterno t) => t.DataTaratura ?? DateTime.MinValue;

        // a parità di N° rapporto tieni la riga del file con data più recente
        var perNumero = new Dictionary<int, TermometroEsterno>();
        var senzaNumero = new List<TermometroEsterno>();
        foreach (var r in righe)
        {
            if (r.NumeroRapporto is int n)
            {
                if (!perNumero.TryGetValue(n, out var cur) || Q(r) > Q(cur)) perNumero[n] = r;
            }
            else senzaNumero.Add(r);
        }

        var numeri = perNumero.Keys.ToList();
        var esistenti = await _ctx.TermometriEsterni
            .Where(t => t.NumeroRapporto != null && numeri.Contains(t.NumeroRapporto.Value)).ToListAsync(ct);
        var mappa = esistenti.GroupBy(t => t.NumeroRapporto!.Value).ToDictionary(g => g.Key, g => g.First());

        int aggiunte = 0, aggiornate = 0;
        void Copia(TermometroEsterno d, TermometroEsterno s)
        {
            d.Tipologia = s.Tipologia; d.DataTaratura = s.DataTaratura; d.DataScadenza = s.DataScadenza;
            d.Cliente = s.Cliente; d.Riferimento = s.Riferimento; d.Note = s.Note;
            d.DataConsegna = s.DataConsegna; d.OperatoreConsegna = s.OperatoreConsegna; d.RifFattura = s.RifFattura;
        }
        foreach (var (numero, r) in perNumero)
        {
            if (mappa.TryGetValue(numero, out var ex))
            {
                if (Q(r) > Q(ex)) { Copia(ex, r); ex.DataUltimaModifica = DateTime.UtcNow; aggiornate++; }
            }
            else
            {
                r.DataCreazione = DateTime.UtcNow; r.DataUltimaModifica = DateTime.UtcNow; r.IdUtente = _utente.IdUtente;
                _ctx.TermometriEsterni.Add(r); aggiunte++;
            }
        }
        foreach (var r in senzaNumero)
        {
            r.DataCreazione = DateTime.UtcNow; r.DataUltimaModifica = DateTime.UtcNow; r.IdUtente = _utente.IdUtente;
            _ctx.TermometriEsterni.Add(r); aggiunte++;
        }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Scadenziario, "scadenziario.esterni.importati", EsitoOperazione.Successo,
            dettaglio: new { Aggiunte = aggiunte, Aggiornate = aggiornate, Lette = righe.Count, File = file.FileName }, ct: ct);
        return Json(new { aggiunte, aggiornate, lette = righe.Count });
    }

}

public record TermometroEsternoDto
{
    public int? IdTermometroEsterno { get; init; }
    public int? NumeroRapporto { get; init; }
    public string? Tipologia { get; init; }
    public DateTime? DataTaratura { get; init; }
    public DateTime? DataScadenza { get; init; }
    public string? Cliente { get; init; }
    public string? Riferimento { get; init; }
    public string? Note { get; init; }
    public DateTime? DataConsegna { get; init; }
    public string? OperatoreConsegna { get; init; }
    public string? RifFattura { get; init; }
    public bool Fatturato { get; init; }
    public bool EscludiScadenze { get; init; }
}

