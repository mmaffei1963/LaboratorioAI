using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// 21/10/2026: pulsante «Aggiorna AZW» nella barra in alto, per tutti gli utenti autenticati (nessun permesso
/// dedicato: basta l'accesso all'applicazione, regola di autorizzazione predefinita). AZW è letto SOLO in lettura
/// (copia temporanea del file); stessa regola delle pagine personali: al massimo un'importazione ogni 15 minuti
/// e una sola alla volta. Dopo un'importazione riuscita si sincronizzano le convenzioni, come negli altri moduli.
/// </summary>
public class AzwController : Controller
{
    private static readonly TimeSpan Intervallo = TimeSpan.FromMinutes(15);

    /// <summary>Data e ora dell'ultima importazione riuscita (per il suggerimento sul pulsante).</summary>
    [HttpGet("api/azw/stato")]
    public async Task<IActionResult> Stato([FromServices] ContestoLaboratorioAI db, CancellationToken ct)
    {
        var ultima = await db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).Select(i => (DateTime?)i.DataOra).FirstOrDefaultAsync(ct);
        return Json(new
        {
            UltimoAggiornamento = ultima is DateTime u ? DateTime.SpecifyKind(u, DateTimeKind.Utc) : (DateTime?)null,
            MinutiIntervallo = (int)Intervallo.TotalMinutes
        });
    }

    [HttpPost("api/azw/aggiorna")]
    public async Task<IActionResult> Aggiorna([FromServices] ImportatoreAzw importatore, [FromServices] ServizioProvvigioniLab provvigioni,
        [FromServices] IServizioAudit audit, CancellationToken ct)
    {
        var (ok, msg) = await importatore.AggiornaSeVecchiaAsync(Intervallo, ct);
        var importato = ok && msg.StartsWith("Dati aggiornati", StringComparison.Ordinal);
        if (importato) await provvigioni.SincronizzaConvenzioniAsync(ct);
        await audit.RegistraAsync(AreaFunzionale.Amministrazione, "azw.aggiorna",
            ok ? EsitoOperazione.Successo : EsitoOperazione.Errore, dettaglio: new { msg }, ct: ct);
        return Json(new { Riuscita = ok, Importato = importato, Messaggio = msg });
    }
}
