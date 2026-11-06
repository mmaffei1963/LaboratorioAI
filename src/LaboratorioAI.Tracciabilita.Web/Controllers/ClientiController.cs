using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Agenda clienti: i destinatari delle spedizioni. Nulla si cancella: si toglie dall'uso.</summary>
[Authorize]
[Route("Clienti")]
public class ClientiController : Controller
{
    private readonly ContestoTracciabilita _db;

    public ClientiController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");
    private static string? Pulisci(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    [HttpGet("")]
    public async Task<IActionResult> Index(string? cerca, bool conNonAttivi, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var q = _db.Clienti.AsNoTracking().AsQueryable();
        if (!conNonAttivi) q = q.Where(c => c.Attivo);
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(c => c.RagioneSociale.Contains(t) || (c.Citta != null && c.Citta.Contains(t)) || (c.PartitaIva != null && c.PartitaIva.Contains(t)));
        }
        var m = new ModelloElencoClienti { Cerca = cerca, ConNonAttivi = conNonAttivi, Totale = await q.CountAsync(ct) };
        m.Righe = await q.OrderBy(c => c.RagioneSociale).Take(300)
            .Select(c => new RigaCliente { IdCliente = c.IdCliente, RagioneSociale = c.RagioneSociale, Citta = c.Citta, Telefono = c.Telefono, Email = c.Email, Attivo = c.Attivo })
            .ToListAsync(ct);
        return View(m);
    }

    [HttpGet("Nuovo")]
    public IActionResult Nuovo() => EGestore ? SoloAzienda() : View("Modifica", new ModelloCliente());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Modifica(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var c = await _db.Clienti.AsNoTracking().FirstOrDefaultAsync(x => x.IdCliente == id, ct);
        if (c is null) return NotFound();
        return View(new ModelloCliente
        {
            IdCliente = c.IdCliente, RagioneSociale = c.RagioneSociale, PartitaIva = c.PartitaIva, Indirizzo = c.Indirizzo, Cap = c.Cap,
            Citta = c.Citta, Provincia = c.Provincia, Telefono = c.Telefono, Email = c.Email, Note = c.Note, Attivo = c.Attivo
        });
    }

    [HttpPost("Salva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salva(ModelloCliente m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        if (!ModelState.IsValid) return View("Modifica", m);
        var nome = m.RagioneSociale!.Trim();
        if (await _db.Clienti.AnyAsync(c => c.RagioneSociale == nome && c.IdCliente != m.IdCliente, ct))
        {
            ModelState.AddModelError(nameof(m.RagioneSociale), "Esiste già un cliente con questo nome.");
            return View("Modifica", m);
        }
        Cliente c;
        if (m.IdCliente == 0) { c = new Cliente(); _db.Clienti.Add(c); }
        else c = await _db.Clienti.FirstOrDefaultAsync(x => x.IdCliente == m.IdCliente, ct) ?? throw new InvalidOperationException("Cliente non trovato.");
        c.RagioneSociale = nome; c.PartitaIva = Pulisci(m.PartitaIva); c.Indirizzo = Pulisci(m.Indirizzo); c.Cap = Pulisci(m.Cap);
        c.Citta = Pulisci(m.Citta); c.Provincia = Pulisci(m.Provincia); c.Telefono = Pulisci(m.Telefono); c.Email = Pulisci(m.Email);
        c.Note = Pulisci(m.Note); c.Attivo = m.Attivo;
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = $"Cliente «{c.RagioneSociale}» salvato.";
        return RedirectToAction(nameof(Index));
    }
}
