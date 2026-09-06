using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Triage e rinomina dei certificati (Rapporti di Prova) dei laboratori esterni:
/// estrazione AI → aggancio ArcLab (sola lettura) → nome proposto + criticità →
/// conferma avviso → deposito nella cartella di prelievo Palomar e smistamento.
/// Due sorgenti: caricamento dal browser e cartella di rete (PDF già sul server).
/// </summary>
[RichiedePermesso(Permessi.CertificatiEsterniGestione)]
public class CertificatiEsterniController : Controller
{
    private readonly IServizioCertificatiEsterni _servizio;
    private readonly IConfiguration _config;
    private readonly IServizioAudit _audit;

    public CertificatiEsterniController(IServizioCertificatiEsterni servizio, IConfiguration config, IServizioAudit audit)
    {
        _servizio = servizio;
        _config = config;
        _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Certificati esterni";
        ViewData["CartellaArrivo"] = _config["CertificatiEsterni:CartellaArrivo"] ?? "";
        ViewData["CartellaPalomar"] = _config["CertificatiEsterni:CartellaPalomar"] ?? "";
        ViewData["CartellaArchivio"] = _config["CertificatiEsterni:CartellaArchivio"] ?? "";
        return View();
    }

    // ── Cartella di rete (PDF originali) ────────────────────────────
    [HttpGet("api/certificati-esterni/cartella")]
    public IActionResult Cartella([FromQuery] string? percorso)
    {
        var cartella = string.IsNullOrWhiteSpace(percorso)
            ? (_config["CertificatiEsterni:CartellaArrivo"] ?? "")
            : percorso!;
        try
        {
            var files = Directory.Exists(cartella)
                ? new DirectoryInfo(cartella).GetFiles("*.pdf")
                    .OrderByDescending(f => f.LastWriteTime).Take(200)
                    .Select(f => new { f.Name, Percorso = f.FullName }).ToList()
                : null;
            return Json(new { cartella, accessibile = files != null, files });
        }
        catch (Exception ex)
        {
            return Json(new { cartella, accessibile = false, errore = ex.Message, files = (object?)null });
        }
    }

    // ── Analisi ─────────────────────────────────────────────────────
    [HttpPost("api/certificati-esterni/analizza")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Analizza(IFormFile? file, [FromForm] bool rianalizza, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file caricato." });
        var pdf = await LeggiAsync(file, ct);
        try { return Json(await _servizio.AnalizzaAsync(pdf, file.FileName, rianalizza, ct)); }
        catch (Exception ex) { return BadRequest(new { messaggio = "Analisi non riuscita: " + ex.Message }); }
    }

    [HttpPost("api/certificati-esterni/analizza-percorso")]
    public async Task<IActionResult> AnalizzaPercorso([FromBody] RichiestaPercorso req, CancellationToken ct)
    {
        if (!ValidoPdf(req.Percorso)) return BadRequest(new { messaggio = "File non trovato o non è un PDF." });
        var pdf = await System.IO.File.ReadAllBytesAsync(req.Percorso, ct);
        try { return Json(await _servizio.AnalizzaAsync(pdf, Path.GetFileName(req.Percorso), req.Rianalizza, ct)); }
        catch (Exception ex) { return BadRequest(new { messaggio = "Analisi non riuscita: " + ex.Message }); }
    }

    // ── Conferma (scrittura file rinominato) ────────────────────────
    [HttpPost("api/certificati-esterni/conferma")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Conferma(IFormFile? file, [FromForm] string? nomeFinale,
        [FromForm] string? labAcronimo, [FromForm] string? data,
        [FromForm] string? cartellaPalomar, [FromForm] string? cartellaArchivio, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file." });
        if (string.IsNullOrWhiteSpace(nomeFinale)) return BadRequest(new { messaggio = "Nome file mancante." });

        var nome = PulisciNomeFile(nomeFinale!) + ".pdf";
        var pdf = await LeggiAsync(file, ct);
        var percorsi = new List<string>();
        try
        {
            await SalvaRinominatoAsync(pdf, nome, labAcronimo, data, cartellaPalomar, cartellaArchivio, percorsi, ct);
            if (percorsi.Count == 0)
                return BadRequest(new { messaggio = "Nessuna cartella di destinazione configurata." });
            await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "certificatiesterni.salva", EsitoOperazione.Successo,
                tipoEntita: "CertificatoEsterno", idEntita: nome, dettaglio: new { nome, percorsi, sorgente = "upload" }, ct: ct);
            return Json(new { nome, percorsi });
        }
        catch (Exception ex) { return BadRequest(new { messaggio = "Salvataggio non riuscito: " + ex.Message }); }
    }

    [HttpPost("api/certificati-esterni/conferma-percorso")]
    public async Task<IActionResult> ConfermaPercorso([FromBody] RichiestaConfermaPercorso req, CancellationToken ct)
    {
        if (!ValidoPdf(req.Percorso)) return BadRequest(new { messaggio = "File originale non trovato." });
        if (string.IsNullOrWhiteSpace(req.NomeFinale)) return BadRequest(new { messaggio = "Nome file mancante." });

        var nome = PulisciNomeFile(req.NomeFinale) + ".pdf";
        var pdf = await System.IO.File.ReadAllBytesAsync(req.Percorso, ct);
        var percorsi = new List<string>();
        try
        {
            await SalvaRinominatoAsync(pdf, nome, req.LabAcronimo, req.Data, req.CartellaPalomar, req.CartellaArchivio, percorsi, ct);
            if (percorsi.Count == 0)
                return BadRequest(new { messaggio = "Nessuna cartella di destinazione configurata." });

            // Sposta l'originale in "processati" (best-effort: se non riesce, il salvataggio resta valido).
            string? originaleSpostato = null, avviso = null;
            try
            {
                var dirOrig = Path.GetDirectoryName(req.Percorso)!;
                var processati = Path.Combine(dirOrig, "processati");
                Directory.CreateDirectory(processati);
                var destOrig = Path.Combine(processati, Path.GetFileName(req.Percorso));
                if (System.IO.File.Exists(destOrig)) System.IO.File.Delete(destOrig);
                System.IO.File.Move(req.Percorso, destOrig);
                originaleSpostato = destOrig;
            }
            catch (Exception exMove)
            {
                avviso = "Salvato, ma originale non spostato (permessi cartella?): " + exMove.Message;
            }

            await _audit.RegistraAsync(AreaFunzionale.CertificatiEsterni, "certificatiesterni.salva", EsitoOperazione.Successo,
                tipoEntita: "CertificatoEsterno", idEntita: nome,
                dettaglio: new { nome, percorsi, originaleSpostato, sorgente = "cartella" }, ct: ct);
            return Json(new { nome, percorsi, originaleSpostato, avviso });
        }
        catch (Exception ex) { return BadRequest(new { messaggio = "Salvataggio non riuscito: " + ex.Message }); }
    }

    // ── Helper ──────────────────────────────────────────────────────
    private async Task SalvaRinominatoAsync(byte[] pdf, string nome, string? labAcronimo, string? data,
        string? palomarOverride, string? archivioOverride, List<string> percorsi, CancellationToken ct)
    {
        var palomar = string.IsNullOrWhiteSpace(palomarOverride) ? _config["CertificatiEsterni:CartellaPalomar"] : palomarOverride;
        if (!string.IsNullOrWhiteSpace(palomar))
        {
            Directory.CreateDirectory(palomar);
            var dest = Path.Combine(palomar, nome);
            await System.IO.File.WriteAllBytesAsync(dest, pdf, ct);
            percorsi.Add(dest);
        }

        var archivio = string.IsNullOrWhiteSpace(archivioOverride) ? _config["CertificatiEsterni:CartellaArchivio"] : archivioOverride;
        if (!string.IsNullOrWhiteSpace(archivio))
        {
            var lab = string.IsNullOrWhiteSpace(labAcronimo) ? "ALTRO" : PulisciNomeFile(labAcronimo!);
            var giorno = string.IsNullOrWhiteSpace(data) ? DateTime.Now.ToString("yyyy-MM-dd") : PulisciNomeFile(data!);
            var dir = Path.Combine(archivio, lab, giorno);
            Directory.CreateDirectory(dir);
            var dest = Path.Combine(dir, nome);
            await System.IO.File.WriteAllBytesAsync(dest, pdf, ct);
            percorsi.Add(dest);
        }
    }

    private static async Task<byte[]> LeggiAsync(IFormFile file, CancellationToken ct)
    {
        await using var s = file.OpenReadStream();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static bool ValidoPdf(string? percorso) =>
        !string.IsNullOrWhiteSpace(percorso)
        && percorso!.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
        && System.IO.File.Exists(percorso);

    private static string PulisciNomeFile(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
        return s.Trim().Trim('.');
    }
}

public record RichiestaPercorso
{
    public string Percorso { get; init; } = "";
    public bool Rianalizza { get; init; }
}

public record RichiestaConfermaPercorso
{
    public string Percorso { get; init; } = "";
    public string NomeFinale { get; init; } = "";
    public string? LabAcronimo { get; init; }
    public string? Data { get; init; }
    public string? CartellaPalomar { get; init; }
    public string? CartellaArchivio { get; init; }
}
