using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Amministrazione → Voci preventivi. Gestione delle voci di listino accessori e delle
/// condizioni di pagamento proponibili nelle offerte: elenco, aggiunta, modifica.
///
/// Come in tutto il resto dell'applicativo, nessuna voce viene mai cancellata fisicamente:
/// una voce non più da proporre si disattiva (Attivo = false) e resta comunque leggibile
/// nei preventivi già emessi che la referenziano, ma sparisce dagli elenchi di scelta.
/// </summary>
[RichiedePermesso(Permessi.AmministrazioneConfigurazioni)]
public class VociPreventivoController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioAudit _audit;

    public VociPreventivoController(ContestoLaboratorioAI ctx, IServizioAudit audit)
    {
        _ctx = ctx; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Voci preventivi";
        return View();
    }

    // ── Voci accessorie ──────────────────────────────────────────────

    [HttpGet("api/voci-preventivo/accessori")]
    public async Task<IActionResult> ElencoAccessori(CancellationToken ct)
    {
        var voci = await _ctx.ListinoAccessori.AsNoTracking()
            .OrderBy(v => v.Codice)
            .Select(v => new
            {
                v.IdVoceAccessorio, v.Codice, v.Descrizione, v.Prezzo, v.UnitaMisura, v.Attivo
            })
            .ToListAsync(ct);
        return Json(voci);
    }

    public record VoceAccessorioDto(int IdVoceAccessorio, string Codice, string Descrizione, decimal Prezzo, string? UnitaMisura, bool Attivo);

    [HttpPost("api/voci-preventivo/accessori")]
    public async Task<IActionResult> SalvaAccessorio([FromBody] VoceAccessorioDto v, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(v.Codice) || string.IsNullOrWhiteSpace(v.Descrizione))
            return BadRequest(new { messaggio = "Codice e descrizione sono obbligatori." });

        VoceListinoAccessorio riga;
        if (v.IdVoceAccessorio > 0)
        {
            riga = await _ctx.ListinoAccessori.FirstOrDefaultAsync(x => x.IdVoceAccessorio == v.IdVoceAccessorio, ct)
                ?? throw new InvalidOperationException("Voce non trovata.");
        }
        else
        {
            riga = new VoceListinoAccessorio();
            _ctx.ListinoAccessori.Add(riga);
        }
        riga.Codice = v.Codice.Trim();
        riga.Descrizione = v.Descrizione.Trim();
        riga.Prezzo = v.Prezzo;
        riga.UnitaMisura = string.IsNullOrWhiteSpace(v.UnitaMisura) ? null : v.UnitaMisura.Trim();
        riga.Attivo = v.Attivo;

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "voci-preventivo.accessorio.salvato",
            tipoEntita: "VoceListinoAccessorio", idEntita: riga.IdVoceAccessorio.ToString(),
            dettaglio: new { riga.Codice, riga.Descrizione, riga.Attivo }, ct: ct);
        return Json(new { ok = true, id = riga.IdVoceAccessorio });
    }

    // ── Metodi di pagamento ──────────────────────────────────────────

    [HttpGet("api/voci-preventivo/pagamento")]
    public async Task<IActionResult> ElencoPagamento(CancellationToken ct)
    {
        var voci = await _ctx.MetodiPagamento.AsNoTracking()
            .OrderBy(v => v.Ordine).ThenBy(v => v.Descrizione)
            .Select(v => new { v.IdMetodoPagamento, v.Descrizione, v.Ordine, v.Attivo })
            .ToListAsync(ct);
        return Json(voci);
    }

    public record MetodoPagamentoDto(int IdMetodoPagamento, string Descrizione, int Ordine, bool Attivo);

    [HttpPost("api/voci-preventivo/pagamento")]
    public async Task<IActionResult> SalvaPagamento([FromBody] MetodoPagamentoDto v, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(v.Descrizione))
            return BadRequest(new { messaggio = "La descrizione è obbligatoria." });

        MetodoPagamento riga;
        if (v.IdMetodoPagamento > 0)
        {
            riga = await _ctx.MetodiPagamento.FirstOrDefaultAsync(x => x.IdMetodoPagamento == v.IdMetodoPagamento, ct)
                ?? throw new InvalidOperationException("Voce non trovata.");
        }
        else
        {
            riga = new MetodoPagamento();
            _ctx.MetodiPagamento.Add(riga);
        }
        riga.Descrizione = v.Descrizione.Trim();
        riga.Ordine = v.Ordine;
        riga.Attivo = v.Attivo;

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "voci-preventivo.pagamento.salvato",
            tipoEntita: "MetodoPagamento", idEntita: riga.IdMetodoPagamento.ToString(),
            dettaglio: new { riga.Descrizione, riga.Attivo }, ct: ct);
        return Json(new { ok = true, id = riga.IdMetodoPagamento });
    }
}
