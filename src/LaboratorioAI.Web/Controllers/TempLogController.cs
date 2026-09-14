using System.Text.Json;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Monitoraggio TempLog Econorma: carica il file notturno dei registratori di temperatura,
/// applica i limiti di taratura (letti da ArcLab tramite la mappatura canale→strumento),
/// evidenzia le escursioni sopra/sotto ed elenca i canali da sistemare. Nessuna alterazione dei dati.
/// </summary>
[RichiedePermesso(Permessi.TempLogEsegui)]
public class TempLogController : Controller
{
    private const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string MimePdf = "application/pdf";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly ILettoreFileTempLog _lettore;
    private readonly ILettoreDbTempLog _lettoreDb;
    private readonly IServizioMonitoraggioTempLog _monitor;
    private readonly IRepositoryTempLog _repo;
    private readonly IGeneratoreReportTempLog _report;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IConfiguration _config;
    private readonly IServizioAudit _audit;

    public TempLogController(
        ContestoLaboratorioAI ctx, ILettoreFileTempLog lettore, ILettoreDbTempLog lettoreDb,
        IServizioMonitoraggioTempLog monitor, IRepositoryTempLog repo, IGeneratoreReportTempLog report,
        IContestoUtente utente, IServizioVersione versione, IConfiguration config, IServizioAudit audit)
    {
        _ctx = ctx; _lettore = lettore; _lettoreDb = lettoreDb; _monitor = monitor; _repo = repo; _report = report;
        _utente = utente; _versione = versione; _config = config; _audit = audit;
    }

    private string CartellaPredefinita => _config["TempLog:CartellaScarico"]
        ?? @"\\SRVMAREA\marea\ArcLab\ECONORMA_DATI\DATI_LETTURE";

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Monitoraggio Econorma";
        return View();
    }

    // ── Caricamento e monitoraggio del file notturno ────────────────
    [HttpPost("api/templog/monitora")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Monitora(IFormFile? file, [FromForm] bool salva = false, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { messaggio = "Nessun file caricato." });

        FileTempLog contenuto;
        try
        {
            await using var s = file.OpenReadStream();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, ct);
            ms.Position = 0;
            contenuto = _lettore.Leggi(ms, file.FileName);
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = "File non leggibile: " + ex.Message });
        }

        var limiti = await RisolviLimitiAsync(contenuto.Canali, ct);
        var risultato = _monitor.Monitora(contenuto, limiti);

        if (salva)
        {
            _ctx.CaricamentiTempLog.Add(new CaricamentoTempLog
            {
                NomeFile = risultato.NomeFile,
                DataDal = risultato.DataDal,
                DataAl = risultato.DataAl,
                NumeroCanaliMonitorati = risultato.NumeroCanaliMonitorati,
                NumeroCanaliConEscursioni = risultato.NumeroCanaliConEscursioni,
                NumeroEscursioni = risultato.Escursioni.Count,
                TuttoConforme = risultato.TuttoConforme,
                EscursioniJson = JsonSerializer.Serialize(risultato.Escursioni, JsonOpts),
                DaSistemare = string.Join(", ", risultato.DaSistemare),
                IdUtente = _utente.IdUtente,
                VersioneApplicazione = _versione.Versione
            });
            await _ctx.SaveChangesAsync(ct);
        }

        return Json(risultato);
    }

    // ── Monitoraggio dal DB MonitorTemperature (misuratori Econorma) ─
    [HttpPost("api/templog/monitora-db")]
    public async Task<IActionResult> MonitoraDb([FromBody] RichiestaMonitoraDb req, CancellationToken ct)
    {
        var fonte = string.Equals(req.Fonte, "Misure1", StringComparison.OrdinalIgnoreCase)
            ? FonteTempLogDb.Misure1 : FonteTempLogDb.Misure;
        // Default: ultime 24 ore. Presets 24/48 h o intervallo esplicito dal client.
        var al = req.Al ?? DateTime.Now;
        var dal = req.Dal ?? al.AddHours(-(req.Ore is int o && o > 0 ? o : 24));

        FileTempLog contenuto;
        try { contenuto = await _lettoreDb.LeggiAsync(fonte, dal, al, ct); }
        catch (Exception ex) { return BadRequest(new { messaggio = "Lettura MonitorTemperature non riuscita: " + ex.Message }); }

        if (contenuto.Canali.Count == 0)
            return Json(_monitor.Monitora(contenuto, new List<LimitiCanale>()));

        // Se i limiti (Apparecchiature via AssegnazioneSonde) non sono leggibili, mostra comunque le temperature.
        IReadOnlyList<LimitiCanale> limiti;
        try { limiti = await _lettoreDb.LimitiAsync(contenuto.Canali, ct); }
        catch { limiti = new List<LimitiCanale>(); }
        var risultato = _monitor.Monitora(contenuto, limiti);
        if (req.Salva) await SalvaCaricamentoAsync(risultato, "MonitorTemperature/" + fonte, ct);
        return Json(risultato);
    }

    // ── Export report (Excel / PDF) ─────────────────────────────────
    [HttpPost("api/templog/report")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Report(IFormFile? file, [FromQuery] string formato = "pdf", CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file caricato." });
        FileTempLog contenuto;
        try
        {
            await using var s = file.OpenReadStream();
            using var ms = new MemoryStream(); await s.CopyToAsync(ms, ct); ms.Position = 0;
            contenuto = _lettore.Leggi(ms, file.FileName);
        }
        catch (Exception ex) { return BadRequest(new { messaggio = "File non leggibile: " + ex.Message }); }

        var risultato = _monitor.Monitora(contenuto, await RisolviLimitiAsync(contenuto.Canali, ct));
        bool excel = string.Equals(formato, "excel", StringComparison.OrdinalIgnoreCase);
        var bytes = excel ? _report.GeneraExcel(risultato) : _report.GeneraPdf(risultato);
        return File(bytes, excel ? MimeXlsx : MimePdf, _report.NomeFile(risultato, excel ? "xlsx" : "pdf"));
    }

    // ── Export PDF mirati (dal risultato già calcolato lato client) ──
    [HttpPost("api/templog/pdf/grafici")]
    [RequestSizeLimit(30_000_000)]
    public IActionResult PdfGrafici([FromBody] RisultatoMonitoraggio? r)
    {
        if (r is null || r.Canali.Count == 0) return BadRequest(new { messaggio = "Nessun dato da esportare." });
        return File(_report.GeneraPdfGrafici(r), MimePdf, _report.NomeFile(r, "grafici.pdf"));
    }

    [HttpPost("api/templog/pdf/canale")]
    [RequestSizeLimit(30_000_000)]
    public IActionResult PdfCanale([FromBody] RichiestaPdfCanale? req)
    {
        var r = req?.Risultato;
        if (r is null || r.Canali.Count == 0) return BadRequest(new { messaggio = "Nessun dato da esportare." });
        if (req!.Indice < 0 || req.Indice >= r.Canali.Count) return BadRequest(new { messaggio = "Canale non valido." });
        var canale = r.Canali[req.Indice].Canale;
        return File(_report.GeneraPdfCanale(r, req.Indice), MimePdf, _report.NomeFile(r, $"canale_{canale}.pdf"));
    }

    [HttpPost("api/templog/pdf/valori")]
    [RequestSizeLimit(30_000_000)]
    public IActionResult PdfValori([FromBody] RisultatoMonitoraggio? r)
    {
        if (r is null || r.Canali.Count == 0) return BadRequest(new { messaggio = "Nessun dato da esportare." });
        return File(_report.GeneraPdfValori(r), MimePdf, _report.NomeFile(r, "valori.pdf"));
    }

    // ── Caricamento da cartella di rete (ricorda l'ultima usata) ────
    [HttpGet("api/templog/cartella")]
    public async Task<IActionResult> Cartella([FromQuery] string? percorso, CancellationToken ct)
    {
        // Propone SEMPRE la cartella predefinita configurata (Econorma DATI_LETTURE);
        // l'utente può comunque digitare un altro percorso ed elencarlo manualmente.
        var cartella = !string.IsNullOrWhiteSpace(percorso) ? percorso! : CartellaPredefinita;
        try
        {
            var files = Directory.Exists(cartella)
                ? new DirectoryInfo(cartella).GetFiles("*.xls*")
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(50)
                    .Select(f => new { f.Name, Data = f.LastWriteTime, f.Length, Percorso = f.FullName }).ToList()
                : null;
            return Json(new { cartella, accessibile = files != null, files });
        }
        catch (Exception ex) { return Json(new { cartella, accessibile = false, errore = ex.Message, files = (object?)null }); }
    }

    [HttpPost("api/templog/carica-cartella")]
    public async Task<IActionResult> CaricaDaCartella([FromBody] RichiestaCaricaCartella req, [FromQuery] bool salva = false, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.Cartella) || string.IsNullOrWhiteSpace(req.NomeFile))
            return BadRequest(new { messaggio = "Cartella e file obbligatori." });
        var percorso = Path.Combine(req.Cartella, req.NomeFile);
        if (!System.IO.File.Exists(percorso)) return NotFound(new { messaggio = "File non trovato: " + percorso });

        FileTempLog contenuto;
        try
        {
            await using var fs = System.IO.File.OpenRead(percorso);
            using var ms = new MemoryStream(); await fs.CopyToAsync(ms, ct); ms.Position = 0;
            contenuto = _lettore.Leggi(ms, req.NomeFile);
        }
        catch (Exception ex) { return BadRequest(new { messaggio = "File non leggibile: " + ex.Message }); }

        var risultato = _monitor.Monitora(contenuto, await RisolviLimitiAsync(contenuto.Canali, ct));
        if (salva) await SalvaCaricamentoAsync(risultato, req.Cartella, ct);
        else await RicordaCartellaAsync(req.Cartella, ct);
        return Json(risultato);
    }

    private async Task RicordaCartellaAsync(string cartella, CancellationToken ct)
    {
        // registra solo la cartella (senza esito) per riproporla, evitando duplicati inutili
        var ultima = await _ctx.CaricamentiTempLog.OrderByDescending(c => c.DataCreazione)
            .Select(c => c.CartellaOrigine).FirstOrDefaultAsync(ct);
        if (string.Equals(ultima, cartella, StringComparison.OrdinalIgnoreCase)) return;
        _ctx.CaricamentiTempLog.Add(new CaricamentoTempLog { CartellaOrigine = cartella, IdUtente = _utente.IdUtente });
        await _ctx.SaveChangesAsync(ct);
    }

    private async Task SalvaCaricamentoAsync(RisultatoMonitoraggio r, string? cartella, CancellationToken ct)
    {
        _ctx.CaricamentiTempLog.Add(new CaricamentoTempLog
        {
            NomeFile = r.NomeFile, CartellaOrigine = cartella, DataDal = r.DataDal, DataAl = r.DataAl,
            NumeroCanaliMonitorati = r.NumeroCanaliMonitorati, NumeroCanaliConEscursioni = r.NumeroCanaliConEscursioni,
            NumeroEscursioni = r.Escursioni.Count, TuttoConforme = r.TuttoConforme,
            EscursioniJson = JsonSerializer.Serialize(r.Escursioni, JsonOpts),
            DaSistemare = string.Join(", ", r.DaSistemare),
            IdUtente = _utente.IdUtente, VersioneApplicazione = _versione.Versione
        });
        await _ctx.SaveChangesAsync(ct);
    }

    // ── Validazione (revisione umana) ───────────────────────────────
    private static readonly string[] EsitiValidi = { "Conforme", "Dubbio", "NonConforme" };

    /// <summary>Analizza il file e lo registra già validato con l'esito indicato (validatore = utente collegato).</summary>
    [HttpPost("api/templog/valida-corrente")]
    [RichiedePermesso(Permessi.TempLogValidazione)]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> ValidaCorrente(IFormFile? file, [FromForm] string? esito, [FromForm] string? note,
        [FromForm] string? cartella, CancellationToken ct = default)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file caricato." });
        if (!EsitiValidi.Contains(esito)) return BadRequest(new { messaggio = "Esito non valido." });

        FileTempLog contenuto;
        try
        {
            await using var s = file.OpenReadStream();
            using var ms = new MemoryStream(); await s.CopyToAsync(ms, ct); ms.Position = 0;
            contenuto = _lettore.Leggi(ms, file.FileName);
        }
        catch (Exception ex) { return BadRequest(new { messaggio = "File non leggibile: " + ex.Message }); }

        var risultato = _monitor.Monitora(contenuto, await RisolviLimitiAsync(contenuto.Canali, ct));
        var c = NuovoCaricamento(risultato, cartella);
        c.EsitoValidazione = esito; c.Validatore = NomeValidatore(); c.DataValidazione = DateTime.UtcNow;
        c.NoteValidazione = string.IsNullOrWhiteSpace(note) ? null : note!.Trim();
        _ctx.CaricamentiTempLog.Add(c);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.TempLog, "templog.validazione.corrente", EsitoOperazione.Successo,
            tipoEntita: "CaricamentoTempLog", idEntita: c.IdCaricamento.ToString(),
            dettaglio: new { c.NomeFile, c.EsitoValidazione }, ct: ct);
        return Json(new { c.IdCaricamento, c.EsitoValidazione, c.Validatore, c.DataValidazione });
    }

    [HttpGet("api/templog/registro")]
    public async Task<IActionResult> Registro(CancellationToken ct)
    {
        var righe = await _ctx.CaricamentiTempLog.AsNoTracking()
            .Where(c => c.NomeFile != null)
            .OrderByDescending(c => c.DataCreazione).Take(200)
            .Select(c => new
            {
                c.IdCaricamento, c.NomeFile, c.DataDal, c.DataCreazione,
                c.NumeroCanaliConEscursioni, c.NumeroEscursioni, c.TuttoConforme,
                c.EsitoValidazione, c.Validatore, c.DataValidazione, c.NoteValidazione
            }).ToListAsync(ct);
        return Json(righe);
    }

    [HttpPost("api/templog/valida")]
    [RichiedePermesso(Permessi.TempLogValidazione)]
    public async Task<IActionResult> Valida([FromBody] RichiestaValidazioneTempLog req, CancellationToken ct)
    {
        if (req.Ids is null || req.Ids.Count == 0) return BadRequest(new { messaggio = "Nessun caricamento selezionato." });
        if (!EsitiValidi.Contains(req.Esito)) return BadRequest(new { messaggio = "Esito non valido." });

        var righe = await _ctx.CaricamentiTempLog.Where(c => req.Ids.Contains(c.IdCaricamento)).ToListAsync(ct);
        var validatore = NomeValidatore();
        var ora = DateTime.UtcNow;
        foreach (var c in righe)
        {
            c.EsitoValidazione = req.Esito; c.Validatore = validatore; c.DataValidazione = ora;
            if (!string.IsNullOrWhiteSpace(req.Note)) c.NoteValidazione = req.Note!.Trim();
        }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.TempLog, "templog.validazione.multipla", EsitoOperazione.Successo,
            dettaglio: new { Validati = righe.Count, req.Esito }, ct: ct);
        return Json(new { validati = righe.Count });
    }

    private string NomeValidatore() => _utente.NomeCompleto ?? "";

    private CaricamentoTempLog NuovoCaricamento(RisultatoMonitoraggio r, string? cartella) => new()
    {
        NomeFile = r.NomeFile, CartellaOrigine = cartella, DataDal = r.DataDal, DataAl = r.DataAl,
        NumeroCanaliMonitorati = r.NumeroCanaliMonitorati, NumeroCanaliConEscursioni = r.NumeroCanaliConEscursioni,
        NumeroEscursioni = r.Escursioni.Count, TuttoConforme = r.TuttoConforme,
        EscursioniJson = JsonSerializer.Serialize(r.Escursioni, JsonOpts),
        DaSistemare = string.Join(", ", r.DaSistemare),
        IdUtente = _utente.IdUtente, VersioneApplicazione = _versione.Versione
    };

    /// <summary>Costruisce i limiti per ogni canale del file: mappatura → IdStrumento → limiti ArcLab (riletti).</summary>
    private async Task<IReadOnlyList<LimitiCanale>> RisolviLimitiAsync(IReadOnlyList<string> canali, CancellationToken ct)
    {
        var mappe = await _ctx.MappatureCanaliTempLog.AsNoTracking()
            .Where(m => m.Attivo && m.DataCancellazione == null)
            .ToListAsync(ct);
        var perCanale = mappe
            .GroupBy(m => m.Canale, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var ids = perCanale.Values.Select(m => m.IdStrumento).Distinct().ToList();
        var limitiArc = (await _repo.LimitiPerStrumentiAsync(ids, ct))
            .GroupBy(x => x.IdStrumento).ToDictionary(g => g.Key, g => g.First());

        var risultato = new List<LimitiCanale>();
        foreach (var canale in canali)
        {
            if (!perCanale.TryGetValue(canale, out var m))
            {
                risultato.Add(new LimitiCanale { Canale = canale, MotivoMancanza = "Canale non mappato ad alcuno strumento." });
                continue;
            }
            limitiArc.TryGetValue(m.IdStrumento, out var lim);
            risultato.Add(new LimitiCanale
            {
                Canale = canale,
                IdStrumento = m.IdStrumento,
                Sonda = m.Sonda,
                Ambiente = lim?.Ambiente,
                VN = lim?.VN,
                Min = lim?.Min,
                Max = lim?.Max,
                MotivoMancanza = (lim is null) ? "Strumento non trovato in ArcLab." : lim.MotivoMancanza
            });
        }
        return risultato;
    }

    // ── Mappatura canale ↔ strumento ────────────────────────────────
    [HttpGet("api/templog/mappatura")]
    public async Task<IActionResult> Mappatura(CancellationToken ct)
    {
        var righe = await _ctx.MappatureCanaliTempLog.AsNoTracking()
            .Where(m => m.DataCancellazione == null)
            .OrderBy(m => m.Canale)
            .Select(m => new { m.IdMappatura, m.Canale, m.IdStrumento, m.Sonda, m.Note, m.Attivo, m.ValidaDal, m.ValidaAl })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpPost("api/templog/mappatura")]
    [RichiedePermesso(Permessi.TempLogMappatura)]
    public async Task<IActionResult> SalvaMappatura([FromBody] RichiestaMappaturaTempLog req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Canale) || req.IdStrumento <= 0)
            return BadRequest(new { messaggio = "Canale e IdStrumento sono obbligatori." });

        var m = req.IdMappatura is int id and > 0
            ? await _ctx.MappatureCanaliTempLog.FirstOrDefaultAsync(x => x.IdMappatura == id, ct)
            : null;
        if (m is null)
        {
            m = new MappaturaCanaleTempLog { DataCreazione = DateTime.UtcNow };
            _ctx.MappatureCanaliTempLog.Add(m);
        }
        m.Canale = req.Canale.Trim().ToUpperInvariant();
        m.IdStrumento = req.IdStrumento;
        m.Sonda = string.IsNullOrWhiteSpace(req.Sonda) ? null : req.Sonda!.Trim();
        m.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note!.Trim();
        m.Attivo = req.Attivo;
        m.DataUltimaModifica = DateTime.UtcNow;
        m.IdUtente = _utente.IdUtente;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.TempLog, "templog.mappatura.salvata", EsitoOperazione.Successo,
            tipoEntita: "MappaturaCanaleTempLog", idEntita: m.IdMappatura.ToString(),
            dettaglio: new { m.Canale, m.IdStrumento }, ct: ct);
        return Json(new { m.IdMappatura });
    }

    [HttpPost("api/templog/mappatura/{id:int}/disattiva")]
    [RichiedePermesso(Permessi.TempLogMappatura)]
    public async Task<IActionResult> DisattivaMappatura(int id, CancellationToken ct)
    {
        var m = await _ctx.MappatureCanaliTempLog.FirstOrDefaultAsync(x => x.IdMappatura == id, ct);
        if (m is null) return NotFound();
        m.Attivo = false;
        m.DataCancellazione = DateTime.UtcNow;
        m.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.TempLog, "templog.mappatura.disattivata", EsitoOperazione.Successo,
            tipoEntita: "MappaturaCanaleTempLog", idEntita: id.ToString(),
            dettaglio: new { m.Canale }, ct: ct);
        return Ok();
    }

    // ── Sincronizzazione da ArcLab (proposta mappatura) ─────────────
    [HttpGet("api/templog/sonde")]
    [RichiedePermesso(Permessi.TempLogMappatura)]
    public async Task<IActionResult> Sonde(CancellationToken ct)
    {
        var sonde = await _repo.SondeEconormaAsync(ct);
        _ctx.SincronizzazioniSondeTempLog.Add(new SincronizzazioneSondeTempLog
        {
            DataSincronizzazione = DateTime.UtcNow, NumeroSonde = sonde.Count, IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);

        var esistenti = await _ctx.MappatureCanaliTempLog.AsNoTracking()
            .Where(m => m.Attivo && m.DataCancellazione == null)
            .Select(m => m.Canale).ToListAsync(ct);
        var set = new HashSet<string>(esistenti, StringComparer.OrdinalIgnoreCase);
        return Json(sonde.Select(s => new
        {
            s.CanaleIpotesi, s.IdStrumento, s.Sonda, s.Ambiente, s.VN, s.Min, s.Max, s.SondaNonInUso,
            GiaMappato = s.CanaleIpotesi != null && set.Contains(s.CanaleIpotesi),
            LimitiOk = s.Min.HasValue && s.Max.HasValue
        }));
    }

    [HttpGet("api/templog/sonde/report")]
    [RichiedePermesso(Permessi.TempLogMappatura)]
    public async Task<IActionResult> ReportSonde([FromQuery] string formato = "pdf", CancellationToken ct = default)
    {
        var sonde = await _repo.SondeEconormaAsync(ct);
        var ultima = await _ctx.SincronizzazioniSondeTempLog.AsNoTracking()
            .OrderByDescending(s => s.DataSincronizzazione).Select(s => (DateTime?)s.DataSincronizzazione).FirstOrDefaultAsync(ct);
        bool excel = string.Equals(formato, "excel", StringComparison.OrdinalIgnoreCase);
        var bytes = excel ? _report.GeneraSondeExcel(sonde, ultima) : _report.GeneraSondePdf(sonde, ultima);
        var nome = $"Sonde_Econorma_{DateTime.Now:yyyyMMdd}." + (excel ? "xlsx" : "pdf");
        return File(bytes, excel ? MimeXlsx : MimePdf, nome);
    }

    /// <summary>Crea automaticamente le mappature mancanti dalla proposta ArcLab (CanaleIpotesi → IdStrumento).</summary>
    [HttpPost("api/templog/mappatura/proponi")]
    [RichiedePermesso(Permessi.TempLogMappatura)]
    public async Task<IActionResult> ProponiMappatura(CancellationToken ct)
    {
        var sonde = await _repo.SondeEconormaAsync(ct);
        var esistenti = await _ctx.MappatureCanaliTempLog
            .Where(m => m.Attivo && m.DataCancellazione == null).Select(m => m.Canale).ToListAsync(ct);
        var set = new HashSet<string>(esistenti, StringComparer.OrdinalIgnoreCase);

        int aggiunte = 0;
        foreach (var s in sonde)
        {
            if (string.IsNullOrWhiteSpace(s.CanaleIpotesi) || s.SondaNonInUso || set.Contains(s.CanaleIpotesi!)) continue;
            _ctx.MappatureCanaliTempLog.Add(new MappaturaCanaleTempLog
            {
                Canale = s.CanaleIpotesi!.ToUpperInvariant(),
                IdStrumento = s.IdStrumento,
                Sonda = s.Sonda,
                Attivo = true,
                DataCreazione = DateTime.UtcNow,
                DataUltimaModifica = DateTime.UtcNow,
                IdUtente = _utente.IdUtente
            });
            set.Add(s.CanaleIpotesi!);
            aggiunte++;
        }
        if (aggiunte > 0) await _ctx.SaveChangesAsync(ct);
        return Json(new { aggiunte });
    }
}

public record RichiestaValidazioneTempLog
{
    public List<int> Ids { get; init; } = new();
    public string Esito { get; init; } = "";
    public string? Note { get; init; }
}

public record RichiestaCaricaCartella
{
    public string Cartella { get; init; } = "";
    public string NomeFile { get; init; } = "";
}

public record RichiestaMonitoraDb
{
    /// <summary>"Misure" (misuratore 1) o "Misure1" (misuratore 2).</summary>
    public string? Fonte { get; init; }
    /// <summary>Finestra rapida in ore (24/48). Ignorata se Dal/Al sono valorizzati.</summary>
    public int? Ore { get; init; }
    public DateTime? Dal { get; init; }
    public DateTime? Al { get; init; }
    public bool Salva { get; init; }
}

public record RichiestaPdfCanale
{
    public RisultatoMonitoraggio? Risultato { get; init; }
    public int Indice { get; init; }
}

public record RichiestaMappaturaTempLog
{
    public int? IdMappatura { get; init; }
    public string Canale { get; init; } = "";
    public int IdStrumento { get; init; }
    public string? Sonda { get; init; }
    public string? Note { get; init; }
    public bool Attivo { get; init; } = true;
}
