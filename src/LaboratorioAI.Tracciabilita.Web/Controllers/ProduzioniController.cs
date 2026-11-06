using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Elenco delle produzioni registrate e dettaglio dei lotti di ingresso usati. Solo lettura.</summary>
[Authorize]
[Route("Produzioni")]
public class ProduzioniController : Controller
{
    private readonly ContestoTracciabilita _db;

    public ProduzioniController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");

    [HttpGet("")]
    public async Task<IActionResult> Index(string? cerca, DateOnly? dal, DateOnly? al, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");

        var q = from p in _db.Produzioni.AsNoTracking()
                join a in _db.Articoli.AsNoTracking() on p.IdArticolo equals a.IdArticolo
                join l in _db.Lotti.AsNoTracking() on p.IdLottoProdotto equals l.IdLotto
                select new { p.IdProduzione, p.Giorno, p.DataOra, Prodotto = a.Descrizione, p.IdLottoProdotto, Lotto = l.Codice, p.Quantita, p.UnitaMisura, p.Annullata };

        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(r => r.Prodotto.Contains(t) || r.Lotto.Contains(t));
        }
        if (dal is { } d1) q = q.Where(r => r.Giorno >= d1);
        if (al is { } d2) q = q.Where(r => r.Giorno <= d2);

        var m = new ModelloElencoProduzioni { Cerca = cerca, Dal = dal, Al = al };
        m.Totale = await q.CountAsync(ct);
        m.Pagina = Math.Clamp(pagina, 1, m.Pagine);
        var pag = await q.OrderByDescending(r => r.Giorno).ThenByDescending(r => r.DataOra)
            .Skip((m.Pagina - 1) * m.PerPagina).Take(m.PerPagina).ToListAsync(ct);

        var ids = pag.Select(r => r.IdProduzione).ToList();
        var usati = (await _db.ProduzioniConsumi.AsNoTracking().Where(c => ids.Contains(c.IdProduzione))
                .GroupBy(c => c.IdProduzione).Select(g => new { Id = g.Key, N = g.Count() }).ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.N);
        m.Righe = pag.Select(r => new RigaProduzione
        {
            IdProduzione = r.IdProduzione, Giorno = r.Giorno, Prodotto = r.Prodotto, IdLottoProdotto = r.IdLottoProdotto, LottoProdotto = r.Lotto,
            Quantita = r.Quantita, UnitaMisura = r.UnitaMisura, Annullata = r.Annullata, LottiUsati = usati.TryGetValue(r.IdProduzione, out var n) ? n : 0
        }).ToList();
        return View(m);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Scheda(int id, string? cerca, DateOnly? dal, DateOnly? al, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");

        var p = await (from x in _db.Produzioni.AsNoTracking()
                       where x.IdProduzione == id
                       join a in _db.Articoli.AsNoTracking() on x.IdArticolo equals a.IdArticolo
                       join l in _db.Lotti.AsNoTracking() on x.IdLottoProdotto equals l.IdLotto
                       select new { x, Prodotto = a.Descrizione, Lotto = l.Codice }).FirstOrDefaultAsync(ct);
        if (p is null) return NotFound();

        var consumi = await (from c in _db.ProduzioniConsumi.AsNoTracking()
                             where c.IdProduzione == id
                             join l in _db.Lotti.AsNoTracking() on c.IdLottoIngresso equals l.IdLotto
                             join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                             join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                             from f in fj.DefaultIfEmpty()
                             select new { c.IdCategoria, c.IdLottoIngresso, Lotto = l.Codice, Articolo = a.Descrizione,
                                          Fornitore = f != null ? f.RagioneSociale : null, c.Quantita, c.UnitaMisura, c.Origine })
            .ToListAsync(ct);
        var nomiCat = await _db.CategorieMateriaPrima.AsNoTracking().ToDictionaryAsync(c => c.IdCategoria, c => c.Nome, ct);

        var m = new ModelloSchedaProduzione
        {
            IdProduzione = id, Giorno = p.x.Giorno, DataOra = p.x.DataOra, Prodotto = p.Prodotto, IdLottoProdotto = p.x.IdLottoProdotto,
            LottoProdotto = p.Lotto, Quantita = p.x.Quantita, UnitaMisura = p.x.UnitaMisura, Note = p.x.Note, Annullata = p.x.Annullata,
            Cerca = cerca, Dal = dal, Al = al, Pagina = pagina,
            Consumi = consumi.Select(c => new RigaConsumoProduzione
            {
                Ingrediente = c.IdCategoria is { } k && nomiCat.TryGetValue(k, out var nome) ? nome : "",
                IdLotto = c.IdLottoIngresso, Lotto = c.Lotto, Articolo = c.Articolo, Fornitore = c.Fornitore,
                Quantita = c.Quantita, UnitaMisura = c.UnitaMisura, Origine = c.Origine
            }).OrderBy(c => c.Ingrediente).ThenBy(c => c.Lotto).ToList()
        };
        return View(m);
    }
}
