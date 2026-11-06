using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using LaboratorioAI.Tracciabilita.Web.Servizi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>
/// Categorie di materie prime (le «matrici») e profili di controllo: cosa si controlla all'arrivo di ogni categoria.
/// Nulla si cancella: categorie e profili si mettono «fuori uso», i controlli si tolgono dal profilo (restano nello storico).
/// Riservato a Qualità e Amministratore azienda.
/// </summary>
[Authorize]
[RichiedeConfigurazione]
[Route("Anagrafiche")]
public class CategorieController : Controller
{
    private readonly ContestoTracciabilita _db;
    public CategorieController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");
    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    private void Registra(string azione, string entita, int id, string? prima, string? dopo) =>
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, Azione = azione, Entita = entita, IdEntita = id.ToString(),
            Prima = prima, Dopo = dopo, IndirizzoIp = Ip, DataOra = DateTime.UtcNow
        });

    // ── Categorie ──────────────────────────────────────────────────
    [HttpGet("Categorie")]
    public async Task<IActionResult> Categorie(string? cerca, bool tutte, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var q = _db.CategorieMateriaPrima.AsNoTracking().AsQueryable();
        if (!tutte) q = q.Where(c => c.Attivo);
        if (!string.IsNullOrWhiteSpace(cerca)) { var t = cerca.Trim(); q = q.Where(c => c.Nome.Contains(t)); }
        var categorie = await q.OrderBy(c => c.Nome).ToListAsync(ct);
        var profili = await _db.ProfiliControllo.AsNoTracking().ToDictionaryAsync(p => p.IdProfiloControllo, p => p.Nome, ct);
        var articoli = await _db.Articoli.AsNoTracking().Where(a => a.Attivo && a.IdCategoria != null)
            .GroupBy(a => a.IdCategoria!.Value).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N, ct);
        return View(new ModelloElencoCategorie
        {
            Cerca = cerca, Tutte = tutte,
            Righe = categorie.Select(c => new RigaCategoria
            {
                IdCategoria = c.IdCategoria, Nome = c.Nome, Attivo = c.Attivo,
                Profilo = c.IdProfiloControllo is { } p ? profili.GetValueOrDefault(p) : null,
                Articoli = articoli.GetValueOrDefault(c.IdCategoria)
            }).ToList()
        });
    }

    [HttpGet("Categorie/Nuova")]
    public async Task<IActionResult> NuovaCategoria(CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = new ModelloCategoria();
        await CaricaOpzioniCategoria(m, ct);
        return View("ModificaCategoria", m);
    }

    [HttpGet("Categorie/{id:int}")]
    public async Task<IActionResult> ModificaCategoria(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var c = await _db.CategorieMateriaPrima.AsNoTracking().FirstOrDefaultAsync(x => x.IdCategoria == id, ct);
        if (c is null) return NotFound();
        var m = new ModelloCategoria { IdCategoria = id, Nome = c.Nome, IdProfiloControllo = c.IdProfiloControllo, Note = c.Note, Attivo = c.Attivo };
        await CaricaOpzioniCategoria(m, ct);
        return View(m);
    }

    [HttpPost("Categorie/Salva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaCategoria(ModelloCategoria m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var nome = m.Nome?.Trim();
        if (!string.IsNullOrEmpty(nome) &&
            await _db.CategorieMateriaPrima.AnyAsync(c => c.IdCategoria != m.IdCategoria && c.Nome == nome, ct))
            ModelState.AddModelError(nameof(m.Nome), "Esiste già una categoria con questo nome.");
        if (m.IdProfiloControllo is { } p && !await _db.ProfiliControllo.AnyAsync(x => x.IdProfiloControllo == p, ct))
            ModelState.AddModelError(nameof(m.IdProfiloControllo), "Il profilo scelto non esiste.");
        if (!ModelState.IsValid)
        {
            await CaricaOpzioniCategoria(m, ct);
            return View("ModificaCategoria", m);
        }

        CategoriaMateriaPrima c;
        string? prima = null;
        if (m.IdCategoria == 0)
        {
            c = new CategoriaMateriaPrima();
            _db.CategorieMateriaPrima.Add(c);
        }
        else
        {
            c = await _db.CategorieMateriaPrima.FirstOrDefaultAsync(x => x.IdCategoria == m.IdCategoria, ct) ?? throw new InvalidOperationException("Categoria non trovata.");
            prima = $"{c.Nome}; profilo {c.IdProfiloControllo}; in uso={c.Attivo}";
        }
        c.Nome = nome!; c.IdProfiloControllo = m.IdProfiloControllo; c.Note = string.IsNullOrWhiteSpace(m.Note) ? null : m.Note.Trim(); c.Attivo = m.Attivo;
        await _db.SaveChangesAsync(ct);
        Registra(m.IdCategoria == 0 ? "CategoriaCreata" : "CategoriaModificata", "Categoria", c.IdCategoria, prima, $"{c.Nome}; profilo {c.IdProfiloControllo}; in uso={c.Attivo}");
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = m.IdCategoria == 0 ? $"Categoria «{c.Nome}» creata." : $"Categoria «{c.Nome}» salvata.";
        return RedirectToAction(nameof(Categorie), new { tutte = !c.Attivo });
    }

    private async Task CaricaOpzioniCategoria(ModelloCategoria m, CancellationToken ct)
    {
        m.Profili = await _db.ProfiliControllo.AsNoTracking().Where(p => p.Attivo || p.IdProfiloControllo == m.IdProfiloControllo)
            .OrderBy(p => p.Nome).Select(p => new OpzioneProfilo { Id = p.IdProfiloControllo, Nome = p.Nome }).ToListAsync(ct);
        m.ArticoliAttivi = m.IdCategoria == 0 ? 0 : await _db.Articoli.AsNoTracking().CountAsync(a => a.Attivo && a.IdCategoria == m.IdCategoria, ct);
    }

    // ── Profili di controllo ───────────────────────────────────────
    [HttpGet("Profili")]
    public async Task<IActionResult> Profili(bool tutti, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var profili = await _db.ProfiliControllo.AsNoTracking().Where(p => tutti || p.Attivo).Include(p => p.Righe).OrderBy(p => p.Nome).ToListAsync(ct);
        var categorie = await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.Attivo && c.IdProfiloControllo != null)
            .GroupBy(c => c.IdProfiloControllo!.Value).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N, ct);
        ViewData["Tutti"] = tutti;
        return View(profili.Select(p => new RigaProfilo
        {
            IdProfiloControllo = p.IdProfiloControllo, Nome = p.Nome, Attivo = p.Attivo,
            Categorie = categorie.GetValueOrDefault(p.IdProfiloControllo),
            Controlli = string.Join(", ", p.Righe.Where(r => r.Attivo).OrderBy(r => r.Controllo).Select(r => NomiControlli.Nome(r.Controllo)))
        }).ToList());
    }

    [HttpGet("Profili/Nuovo")]
    public IActionResult NuovoProfilo()
    {
        if (EGestore) return SoloAzienda();
        var m = new ModelloProfilo { Controlli = ElencoControlli(null) };
        foreach (var c in m.Controlli.Where(c => c.Controllo is TipoControllo.Etichettatura or TipoControllo.Confezione or TipoControllo.Temperatura)) c.Presente = true;
        return View("ModificaProfilo", m);
    }

    [HttpGet("Profili/{id:int}")]
    public async Task<IActionResult> ModificaProfilo(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var p = await _db.ProfiliControllo.AsNoTracking().Include(x => x.Righe).FirstOrDefaultAsync(x => x.IdProfiloControllo == id, ct);
        if (p is null) return NotFound();
        return View(new ModelloProfilo
        {
            IdProfiloControllo = id, Nome = p.Nome, Attivo = p.Attivo, Controlli = ElencoControlli(p.Righe),
            CategorieAttive = await _db.CategorieMateriaPrima.AsNoTracking().CountAsync(c => c.Attivo && c.IdProfiloControllo == id, ct)
        });
    }

    [HttpPost("Profili/Salva")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaProfilo(ModelloProfilo m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var nome = m.Nome?.Trim();
        // I nomi dei controlli non viaggiano nel modulo: si rimettono.
        foreach (var c in m.Controlli) c.Nome = NomiControlli.Nome(c.Controllo);
        if (!string.IsNullOrEmpty(nome) && await _db.ProfiliControllo.AnyAsync(p => p.IdProfiloControllo != m.IdProfiloControllo && p.Nome == nome, ct))
            ModelState.AddModelError(nameof(m.Nome), "Esiste già un profilo con questo nome.");
        if (m.Controlli.Count(c => c.Presente) == 0)
            ModelState.AddModelError("", "Scegli almeno un controllo.");
        if (m.IdProfiloControllo != 0 && !m.Attivo &&
            await _db.CategorieMateriaPrima.AnyAsync(c => c.Attivo && c.IdProfiloControllo == m.IdProfiloControllo, ct))
            ModelState.AddModelError("", "Il profilo è usato da categorie in uso: abbinale prima a un altro profilo.");
        if (!ModelState.IsValid)
        {
            m.CategorieAttive = m.IdProfiloControllo == 0 ? 0 : await _db.CategorieMateriaPrima.AsNoTracking().CountAsync(c => c.Attivo && c.IdProfiloControllo == m.IdProfiloControllo, ct);
            return View("ModificaProfilo", m);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        ProfiloControllo p;
        string? prima = null;
        if (m.IdProfiloControllo == 0)
        {
            p = new ProfiloControllo();
            _db.ProfiliControllo.Add(p);
        }
        else
        {
            p = await _db.ProfiliControllo.Include(x => x.Righe).FirstOrDefaultAsync(x => x.IdProfiloControllo == m.IdProfiloControllo, ct) ?? throw new InvalidOperationException("Profilo non trovato.");
            prima = $"{p.Nome}; in uso={p.Attivo}; controlli: {Riassunto(p.Righe)}";
        }
        p.Nome = nome!; p.Attivo = m.Attivo;
        foreach (var c in m.Controlli)
        {
            var riga = p.Righe.FirstOrDefault(r => r.Controllo == c.Controllo);
            var istruzione = string.IsNullOrWhiteSpace(c.Istruzione) ? null : c.Istruzione.Trim();
            if (riga is null)
            {
                if (c.Presente) p.Righe.Add(new ProfiloControlloRiga { Controllo = c.Controllo, Obbligatorio = c.Obbligatorio, Istruzione = istruzione, Attivo = true });
            }
            else
            {
                riga.Attivo = c.Presente;
                riga.Obbligatorio = c.Obbligatorio;   // si salva anche per i controlli tolti: così la pagina li rimostra come li hai lasciati
                riga.Istruzione = istruzione;
            }
        }
        await _db.SaveChangesAsync(ct);
        Registra(m.IdProfiloControllo == 0 ? "ProfiloCreato" : "ProfiloModificato", "ProfiloControllo", p.IdProfiloControllo, prima,
            $"{p.Nome}; in uso={p.Attivo}; controlli: {Riassunto(p.Righe)}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Messaggio"] = $"Profilo «{p.Nome}» salvato. Vale dalle prossime accettazioni; quelle già registrate non cambiano.";
        return RedirectToAction(nameof(Profili));
    }

    private static string Riassunto(IEnumerable<ProfiloControlloRiga> righe) =>
        string.Join(", ", righe.Where(r => r.Attivo).OrderBy(r => r.Controllo).Select(r => NomiControlli.Nome(r.Controllo) + (r.Obbligatorio ? "" : " (facoltativo)")));

    /// <summary>Tutti i controlli possibili, con quelli già nel profilo spuntati.</summary>
    private static List<RigaControlloProfilo> ElencoControlli(IEnumerable<ProfiloControlloRiga>? righe)
    {
        var esistenti = righe?.ToList() ?? new List<ProfiloControlloRiga>();
        return Enum.GetValues<TipoControllo>().Select(t =>
        {
            var r = esistenti.FirstOrDefault(x => x.Controllo == t);
            return new RigaControlloProfilo
            {
                Controllo = t, Nome = NomiControlli.Nome(t), Presente = r is { Attivo: true },
                Obbligatorio = r is { Attivo: true } && r.Obbligatorio, Istruzione = r?.Istruzione
            };
        }).ToList();
    }
}
