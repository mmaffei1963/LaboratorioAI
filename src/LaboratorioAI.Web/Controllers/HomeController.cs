using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using LaboratorioAI.Web.Navigazione;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

public class HomeController : Controller
{
    private readonly IContestoUtente _utente;
    private readonly ServizioContatoriHome _contatori;
    private readonly ContestoLaboratorioAI _db;
    private readonly ILogger<HomeController> _log;

    public HomeController(IContestoUtente utente, ServizioContatoriHome contatori, ContestoLaboratorioAI db, ILogger<HomeController> log)
    {
        _utente = utente;
        _contatori = contatori;
        _db = db;
        _log = log;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["NomeUtente"] = _utente.NomeCompleto;
        // Preferiti dell'utente (stellina): solo indirizzi; la pagina mostra quelli che l'utente vede ancora.
        // Se non si possono leggere (es. migrazione non ancora applicata) la home si apre lo stesso, senza preferiti.
        var preferiti = new List<string>();
        if (_utente.IdUtente is int idu)
        {
            try
            {
                preferiti = await _db.PreferitiUtenti.AsNoTracking().Where(p => p.IdUtente == idu && p.Attivo)
                    .OrderBy(p => p.Ordine).ThenBy(p => p.IdPreferitoUtente).Select(p => p.Url).ToListAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "Preferiti della home non letti: la home si apre senza.");
            }
        }
        ViewData["Preferiti"] = preferiti;
        return View();
    }

    public sealed record RichiestaPreferito(string? Url, bool Preferito);

    /// <summary>
    /// Mette o toglie la stellina su una funzione per l'utente corrente. Si accettano solo indirizzi di voci di menu
    /// che l'utente vede. Togliere la stella disattiva la riga (nessuna cancellazione fisica).
    /// </summary>
    [HttpPost("api/home/preferiti")]
    public async Task<IActionResult> Preferito([FromBody] RichiestaPreferito req, CancellationToken ct)
    {
        if (_utente.IdUtente is not int idu) return Unauthorized();
        var url = MenuApplicazione.Visibili(_utente.HaPermesso).SelectMany(g => g.Voci)
            .Select(v => v.Url).FirstOrDefault(u => string.Equals(u, req.Url, StringComparison.OrdinalIgnoreCase));
        if (url is null) return BadRequest(new { messaggio = "Funzione non trovata." });

        var riga = await _db.PreferitiUtenti.FirstOrDefaultAsync(p => p.IdUtente == idu && p.Url == url, ct);
        var adesso = DateTime.UtcNow;
        if (riga is null)
        {
            if (!req.Preferito) return Json(new { ok = true });
            var ordine = await _db.PreferitiUtenti.Where(p => p.IdUtente == idu).MaxAsync(p => (int?)p.Ordine, ct) ?? 0;
            _db.PreferitiUtenti.Add(new PreferitoUtente
            {
                IdUtente = idu, Url = url, Ordine = ordine + 1, Attivo = true, DataCreazione = adesso, DataUltimaModifica = adesso
            });
        }
        else if (riga.Attivo != req.Preferito)
        {
            riga.Attivo = req.Preferito;
            if (req.Preferito)
                riga.Ordine = (await _db.PreferitiUtenti.Where(p => p.IdUtente == idu).MaxAsync(p => (int?)p.Ordine, ct) ?? 0) + 1;
            riga.DataUltimaModifica = adesso;
        }
        await _db.SaveChangesAsync(ct);
        return Json(new { ok = true });
    }

    /// <summary>
    /// Contatori della home («cose da fare»), chiamati dalla pagina dopo l'apertura (js/moduli/home.js):
    /// la home non aspetta le query. Solo le voci che l'utente vede; quelli non calcolabili sono omessi.
    /// </summary>
    [HttpGet("api/home/contatori")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Contatori(CancellationToken ct) =>
        Json(await _contatori.CalcolaAsync(_utente, ct));

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult Errore() => View();

    [AllowAnonymous]
    public IActionResult AccessoNegato() => View();
}
