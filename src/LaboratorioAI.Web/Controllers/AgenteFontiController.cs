using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Limiti;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Riceve i testi delle fonti normative scaricati da <c>strumenti/AgenteFonti</c> sul PC dell'ufficio (17/10/2026):
/// EUR-Lex tramite Cellar (ultimo testo consolidato) e Normattiva tramite le API Open Data. Stesso schema
/// dell'agente ACCREDIA (<see cref="AccrediaAgenteController"/>): nessun login, solo il segreto condiviso
/// nell'intestazione «X-Agente-Segreto» (LimitiNormativi:AgenteSegreto, se manca quello dell'agente ACCREDIA).
/// </summary>
[AllowAnonymous]
public class AgenteFontiController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioLimitiNormativi _limiti;
    private readonly IConfiguration _config;
    private readonly IServizioAudit _audit;

    private readonly IWebHostEnvironment _ambiente;

    public AgenteFontiController(ContestoLaboratorioAI ctx, ServizioLimitiNormativi limiti, IConfiguration config, IServizioAudit audit,
        IWebHostEnvironment ambiente)
    {
        _ctx = ctx; _limiti = limiti; _config = config; _audit = audit; _ambiente = ambiente;
    }

    private bool SegretoValido()
    {
        var atteso = _config["LimitiNormativi:AgenteSegreto"];
        if (string.IsNullOrWhiteSpace(atteso)) atteso = _config["AccreditamentiEsterni:AgenteSegreto"];
        // In sviluppo, senza segreto configurato, basta che la chiamata arrivi dallo stesso PC (localhost).
        if (string.IsNullOrWhiteSpace(atteso) && _ambiente.IsDevelopment())
            return HttpContext.Connection.RemoteIpAddress is { } ip && System.Net.IPAddress.IsLoopback(ip);
        return !string.IsNullOrWhiteSpace(atteso) && Request.Headers["X-Agente-Segreto"].ToString() == atteso;
    }

    /// <summary>Fonti attive con un canale automatico (Cellar o Normattiva).</summary>
    [HttpGet("api/fonti-agente/fonti")]
    public async Task<IActionResult> Fonti(CancellationToken ct)
    {
        if (!SegretoValido()) return Unauthorized();
        var fonti = await _ctx.FontiNormative.AsNoTracking().Where(f => f.Attiva).OrderBy(f => f.IdFonteNormativa).ToListAsync(ct);
        return Json(fonti.Select(f => (f, c: TestoFonti.CanaleAutomatico(f.Url)))
            .Where(x => x.c.Canale is not null)
            .Select(x => new { id = x.f.IdFonteNormativa, titolo = x.f.Titolo, canale = x.c.Canale, codice = x.c.Codice }));
    }

    public record TestoAgente(string? NomeFile, string? TipoContenuto, string ContenutoBase64, DateTime? DataAggiornamento, string? Nota);

    [HttpPost("api/fonti-agente/fonti/{id:int}/testo")]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> Testo(int id, [FromBody] TestoAgente t, CancellationToken ct)
    {
        if (!SegretoValido()) return Unauthorized();
        byte[] dati;
        try { dati = Convert.FromBase64String(t.ContenutoBase64); }
        catch (FormatException) { return BadRequest(new { messaggio = "Contenuto non valido." }); }
        var data = t.DataAggiornamento is DateTime d ? DateTime.SpecifyKind(d.Date, DateTimeKind.Utc) : (DateTime?)null;
        var e = await _limiti.RiceviDaAgenteAsync(id, dati, t.NomeFile, t.TipoContenuto, data, ct);
        await _audit.RegistraAsync(AreaFunzionale.LimitiNormativi, "limiti.fonte.agente",
            e.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore, "FonteNormativa", id.ToString(),
            new { t.NomeFile, t.Nota, e.Messaggio, e.IdVersioneFonte, e.Nuova }, ct);
        return Json(new { riuscita = e.Riuscita, messaggio = e.Messaggio, nuova = e.Nuova, idVersione = e.IdVersioneFonte });
    }
}
