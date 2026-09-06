using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Triage e rinomina dei certificati (Rapporti di Prova) dei laboratori esterni:
/// estrazione AI → aggancio ArcLab (sola lettura) → nome proposto + criticità →
/// conferma avviso → deposito nella cartella di prelievo Palomar e smistamento.
/// </summary>
[RichiedePermesso(Permessi.SoloEsterniVisualizza)]
public class CertificatiEsterniController : Controller
{
    private readonly IServizioCertificatiEsterni _servizio;
    private readonly IConfiguration _config;

    public CertificatiEsterniController(IServizioCertificatiEsterni servizio, IConfiguration config)
    {
        _servizio = servizio;
        _config = config;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Certificati esterni";
        return View();
    }

    /// <summary>Analizza il PDF: estrazione + aggancio ArcLab + nomi proposti + criticità.</summary>
    [HttpPost("api/certificati-esterni/analizza")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Analizza(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file caricato." });
        var pdf = await LeggiAsync(file, ct);
        try
        {
            return Json(await _servizio.AnalizzaAsync(pdf, ct));
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = "Analisi non riuscita: " + ex.Message });
        }
    }

    /// <summary>Salva il file rinominato nella cartella di prelievo Palomar e nell'archivio smistato.</summary>
    [HttpPost("api/certificati-esterni/conferma")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Conferma(IFormFile? file, [FromForm] string? nomeFinale,
        [FromForm] string? labAcronimo, [FromForm] string? data, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Nessun file." });
        if (string.IsNullOrWhiteSpace(nomeFinale)) return BadRequest(new { messaggio = "Nome file mancante." });

        var nome = PulisciNomeFile(nomeFinale!) + ".pdf";
        var pdf = await LeggiAsync(file, ct);
        var percorsi = new List<string>();

        try
        {
            var palomar = _config["CertificatiEsterni:CartellaPalomar"];
            if (!string.IsNullOrWhiteSpace(palomar))
            {
                Directory.CreateDirectory(palomar);
                var dest = Path.Combine(palomar, nome);
                await System.IO.File.WriteAllBytesAsync(dest, pdf, ct);
                percorsi.Add(dest);
            }

            var archivio = _config["CertificatiEsterni:CartellaArchivio"];
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

            if (percorsi.Count == 0)
                return BadRequest(new { messaggio = "Nessuna cartella configurata (CertificatiEsterni:CartellaPalomar / CartellaArchivio)." });

            return Json(new { nome, percorsi });
        }
        catch (Exception ex)
        {
            return BadRequest(new { messaggio = "Salvataggio non riuscito: " + ex.Message });
        }
    }

    private static async Task<byte[]> LeggiAsync(IFormFile file, CancellationToken ct)
    {
        await using var s = file.OpenReadStream();
        using var ms = new MemoryStream();
        await s.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private static string PulisciNomeFile(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '-');
        return s.Trim().Trim('.');
    }
}
