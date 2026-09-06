using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Visore del registro attività (audit trail): consultazione delle operazioni
/// tracciate (log.Audit), con filtri per area, utente, azione e periodo.
/// </summary>
[RichiedePermesso(Permessi.AmministrazioneAudit)]
public class AuditController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;

    public AuditController(ContestoLaboratorioAI ctx) => _ctx = ctx;

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Registro attività";
        return View();
    }

    [HttpGet("api/audit")]
    public async Task<IActionResult> Elenco([FromQuery] int? area, [FromQuery] string? utente,
        [FromQuery] string? azione, [FromQuery] DateOnly? dal, [FromQuery] DateOnly? al,
        [FromQuery] int limite, CancellationToken ct)
    {
        var q = _ctx.Audit.AsNoTracking().AsQueryable();
        if (area is int a) q = q.Where(v => v.Area == (AreaFunzionale)a);
        if (!string.IsNullOrWhiteSpace(utente)) q = q.Where(v => v.EmailUtente != null && v.EmailUtente.Contains(utente));
        if (!string.IsNullOrWhiteSpace(azione)) q = q.Where(v => v.Azione.Contains(azione));
        if (dal is DateOnly d1) q = q.Where(v => v.DataOra >= d1.ToDateTime(TimeOnly.MinValue));
        if (al is DateOnly d2) q = q.Where(v => v.DataOra < d2.ToDateTime(TimeOnly.MinValue).AddDays(1));

        var take = limite is > 0 and <= 1000 ? limite : 200;
        var righe = await q.OrderByDescending(v => v.DataOra).Take(take).ToListAsync(ct);
        return Json(righe.Select(v => new
        {
            v.DataOra,
            v.EmailUtente,
            Area = v.Area.ToString(),
            v.Azione,
            v.TipoEntita,
            v.IdEntita,
            Esito = v.Esito.ToString(),
            v.IndirizzoIp,
            v.Dettaglio
        }));
    }
}
