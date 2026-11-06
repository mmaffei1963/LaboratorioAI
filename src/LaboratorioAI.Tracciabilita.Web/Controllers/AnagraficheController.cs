using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Agenda fornitori e agenda prodotti (articoli con i loro fornitori). Nulla si cancella: si toglie dall'uso.</summary>
[Authorize]
[Route("Anagrafiche")]
public class AnagraficheController : Controller
{
    private readonly ContestoTracciabilita _db;

    public AnagraficheController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");
    private static string? Pulisci(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    // ── Fornitori ──────────────────────────────────────────────────
    [HttpGet("Fornitori")]
    public async Task<IActionResult> Fornitori(string? cerca, bool conNonAttivi, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var q = _db.Fornitori.AsNoTracking().AsQueryable();
        if (!conNonAttivi) q = q.Where(f => f.Attivo);
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(f => f.RagioneSociale.Contains(t) || (f.Citta != null && f.Citta.Contains(t)) || (f.PartitaIva != null && f.PartitaIva.Contains(t)));
        }
        var m = new ModelloElencoFornitori { Cerca = cerca, ConNonAttivi = conNonAttivi, Totale = await q.CountAsync(ct) };
        m.Righe = await q.OrderBy(f => f.RagioneSociale).Take(300)
            .Select(f => new RigaFornitore { IdFornitore = f.IdFornitore, RagioneSociale = f.RagioneSociale, Citta = f.Citta, Telefono = f.Telefono, Email = f.Email, Attivo = f.Attivo })
            .ToListAsync(ct);
        return View(m);
    }

    [HttpGet("Fornitori/Nuovo")]
    public IActionResult NuovoFornitore() => EGestore ? SoloAzienda() : View("ModificaFornitore", new ModelloFornitore());

    [HttpGet("Fornitori/{id:int}")]
    public async Task<IActionResult> ModificaFornitore(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var f = await _db.Fornitori.AsNoTracking().FirstOrDefaultAsync(x => x.IdFornitore == id, ct);
        if (f is null) return NotFound();
        return View(new ModelloFornitore
        {
            IdFornitore = f.IdFornitore, RagioneSociale = f.RagioneSociale, PartitaIva = f.PartitaIva, Indirizzo = f.Indirizzo, Cap = f.Cap,
            Citta = f.Citta, Provincia = f.Provincia, Telefono = f.Telefono, Email = f.Email, Note = f.Note, Attivo = f.Attivo
        });
    }

    [HttpPost("Fornitori/Salva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaFornitore(ModelloFornitore m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        if (!ModelState.IsValid) return View("ModificaFornitore", m);
        var nome = m.RagioneSociale!.Trim();
        if (await _db.Fornitori.AnyAsync(f => f.RagioneSociale == nome && f.IdFornitore != m.IdFornitore, ct))
        {
            ModelState.AddModelError(nameof(m.RagioneSociale), "Esiste già un fornitore con questo nome.");
            return View("ModificaFornitore", m);
        }
        Fornitore f;
        if (m.IdFornitore == 0) { f = new Fornitore(); _db.Fornitori.Add(f); }
        else
        {
            f = await _db.Fornitori.FirstOrDefaultAsync(x => x.IdFornitore == m.IdFornitore, ct) ?? throw new InvalidOperationException("Fornitore non trovato.");
        }
        f.RagioneSociale = nome; f.PartitaIva = Pulisci(m.PartitaIva); f.Indirizzo = Pulisci(m.Indirizzo); f.Cap = Pulisci(m.Cap);
        f.Citta = Pulisci(m.Citta); f.Provincia = Pulisci(m.Provincia); f.Telefono = Pulisci(m.Telefono); f.Email = Pulisci(m.Email);
        f.Note = Pulisci(m.Note); f.Attivo = m.Attivo;
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = $"Fornitore «{f.RagioneSociale}» salvato.";
        return RedirectToAction(nameof(Fornitori));
    }

    // ── Articoli ───────────────────────────────────────────────────
    [HttpGet("Articoli")]
    public async Task<IActionResult> Articoli(string? cerca, bool conNonAttivi, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var q = from a in _db.Articoli.AsNoTracking()
                join c in _db.CategorieMateriaPrima.AsNoTracking() on a.IdCategoria equals c.IdCategoria into cj
                from c in cj.DefaultIfEmpty()
                select new { a, Categoria = c != null ? c.Nome : null };
        if (!conNonAttivi) q = q.Where(x => x.a.Attivo);
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(x => x.a.Descrizione.Contains(t) || (x.a.Codice != null && x.a.Codice.Contains(t)) || (x.Categoria != null && x.Categoria.Contains(t)));
        }
        var m = new ModelloElencoArticoli { Cerca = cerca, ConNonAttivi = conNonAttivi, Totale = await q.CountAsync(ct) };
        var righe = await q.OrderBy(x => x.a.Descrizione).Take(300).ToListAsync(ct);
        var ids = righe.Select(r => r.a.IdArticolo).ToList();
        var legami = await (from l in _db.ArticoliFornitori.AsNoTracking()
                            join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore
                            where ids.Contains(l.IdArticolo) && l.Attivo
                            select new { l.IdArticolo, f.RagioneSociale }).ToListAsync(ct);
        m.Righe = righe.Select(r => new RigaArticolo
        {
            IdArticolo = r.a.IdArticolo, Descrizione = r.a.Descrizione, Codice = r.a.Codice, Tipo = r.a.Tipo, Categoria = r.Categoria,
            UnitaMisura = r.a.UnitaMisura, Attivo = r.a.Attivo,
            Fornitori = string.Join(", ", legami.Where(l => l.IdArticolo == r.a.IdArticolo).Select(l => l.RagioneSociale).OrderBy(n => n))
        }).ToList();
        return View(m);
    }

    [HttpGet("Articoli/Nuovo")]
    public async Task<IActionResult> NuovoArticolo(CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = new ModelloArticolo();
        await CaricaOpzioniAsync(m, ct);
        return View("ModificaArticolo", m);
    }

    [HttpGet("Articoli/{id:int}")]
    public async Task<IActionResult> ModificaArticolo(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var a = await _db.Articoli.AsNoTracking().FirstOrDefaultAsync(x => x.IdArticolo == id, ct);
        if (a is null) return NotFound();
        var m = new ModelloArticolo
        {
            IdArticolo = a.IdArticolo, Descrizione = a.Descrizione, Codice = a.Codice, Tipo = a.Tipo, IdCategoria = a.IdCategoria,
            UnitaMisura = a.UnitaMisura, Bio = a.Bio, Note = a.Note, Attivo = a.Attivo,
            FornitoriScelti = await _db.ArticoliFornitori.AsNoTracking().Where(l => l.IdArticolo == id && l.Attivo).Select(l => l.IdFornitore).ToListAsync(ct)
        };
        await CaricaOpzioniAsync(m, ct);
        return View(m);
    }

    [HttpPost("Articoli/Salva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaArticolo(ModelloArticolo m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        if (!ModelState.IsValid) { await CaricaOpzioniAsync(m, ct); return View("ModificaArticolo", m); }
        var desc = m.Descrizione!.Trim();
        if (await _db.Articoli.AnyAsync(a => a.Descrizione == desc && a.IdArticolo != m.IdArticolo, ct))
        {
            ModelState.AddModelError(nameof(m.Descrizione), "Esiste già un articolo con questa descrizione.");
            await CaricaOpzioniAsync(m, ct);
            return View("ModificaArticolo", m);
        }
        Articolo a;
        if (m.IdArticolo == 0) { a = new Articolo(); _db.Articoli.Add(a); }
        else a = await _db.Articoli.FirstOrDefaultAsync(x => x.IdArticolo == m.IdArticolo, ct) ?? throw new InvalidOperationException("Articolo non trovato.");
        a.Descrizione = desc; a.Codice = Pulisci(m.Codice); a.Tipo = m.Tipo; a.IdCategoria = m.IdCategoria;
        a.UnitaMisura = m.UnitaMisura!.Trim(); a.Bio = m.Bio; a.Note = Pulisci(m.Note); a.Attivo = m.Attivo;
        await _db.SaveChangesAsync(ct);

        // Legami articolo-fornitore: si aggiungono o si riattivano; quelli tolti restano come non attivi.
        var scelti = m.FornitoriScelti.Distinct().ToList();
        var esistenti = await _db.ArticoliFornitori.Where(l => l.IdArticolo == a.IdArticolo).ToListAsync(ct);
        foreach (var l in esistenti) l.Attivo = scelti.Contains(l.IdFornitore);
        foreach (var idF in scelti.Where(x => esistenti.All(l => l.IdFornitore != x)))
            _db.ArticoliFornitori.Add(new ArticoloFornitore { IdArticolo = a.IdArticolo, IdFornitore = idF });
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = $"Articolo «{a.Descrizione}» salvato.";
        return RedirectToAction(nameof(Articoli));
    }

    private async Task CaricaOpzioniAsync(ModelloArticolo m, CancellationToken ct)
    {
        m.Categorie = await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome)
            .Select(c => new OpzioneAnagrafica { Id = c.IdCategoria, Nome = c.Nome }).ToListAsync(ct);
        m.Fornitori = await _db.Fornitori.AsNoTracking().Where(f => f.Attivo).OrderBy(f => f.RagioneSociale)
            .Select(f => new OpzioneAnagrafica { Id = f.IdFornitore, Nome = f.RagioneSociale }).ToListAsync(ct);
    }
}
