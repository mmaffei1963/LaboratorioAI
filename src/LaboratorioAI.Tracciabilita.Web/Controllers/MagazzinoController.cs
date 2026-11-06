using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>
/// Materie prime in magazzino e loro impegno. «Mettere in uso» un lotto ne registra l'inizio (l'impegno del vecchio programma):
/// il lotto resta in uso finché si mette in uso il successivo della stessa matrice o lo si segna esaurito.
/// </summary>
[Authorize]
[Route("Magazzino")]
public class MagazzinoController : Controller
{
    private readonly ContestoTracciabilita _db;

    public MagazzinoController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");

    [HttpGet("")]
    public async Task<IActionResult> Index(string? vista, string? cerca, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        vista = vista is "in-uso" or "tutti" ? vista : "da-impegnare";

        var q = from l in _db.Lotti.AsNoTracking()
                join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                from f in fj.DefaultIfEmpty()
                where l.Tipo == TipoLotto.Ingresso
                select new RigaMagazzino
                {
                    IdLotto = l.IdLotto, Codice = l.Codice, Articolo = a.Descrizione, Fornitore = f != null ? f.RagioneSociale : null,
                    IdCategoria = l.IdCategoria ?? a.IdCategoria, UnitaMisura = a.UnitaMisura, DataScadenza = l.DataScadenza,
                    Giacenza = _db.MovimentiLotto.Where(m => m.IdLotto == l.IdLotto).Sum(m => (decimal?)m.Quantita) ?? 0m,
                    DataArrivo = _db.Accettazioni.Where(x => x.IdLotto == l.IdLotto).Min(x => (DateOnly?)x.DataArrivo),
                    InUsoDal = _db.LottiInUso.Where(u => u.IdLotto == l.IdLotto).Min(u => (DateTime?)u.Inizio),
                    InUsoAperto = _db.LottiInUso.Any(u => u.IdLotto == l.IdLotto && u.Fine == null)
                };
        q = q.Where(r => r.Giacenza > 0);
        q = vista switch
        {
            "in-uso" => q.Where(r => r.InUsoAperto),
            "tutti" => q,
            _ => q.Where(r => r.InUsoDal == null)
        };
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(r => r.Codice.Contains(t) || r.Articolo.Contains(t) || (r.Fornitore != null && r.Fornitore.Contains(t)));
        }
        var righe = await q.OrderBy(r => r.Articolo).ThenBy(r => r.DataArrivo).ToListAsync(ct);
        var nomi = await _db.CategorieMateriaPrima.AsNoTracking().ToDictionaryAsync(c => c.IdCategoria, c => c.Nome, ct);
        foreach (var r in righe) r.Matrice = r.IdCategoria is { } ic && nomi.TryGetValue(ic, out var n) ? n : null;
        var m = new ModelloMagazzino
        {
            Vista = vista, Cerca = cerca, Righe = righe,
            Rimanenze = righe.GroupBy(r => new { r.Fornitore, r.Articolo, r.UnitaMisura })
                .Select(g => new RimanenzaArticolo { Fornitore = g.Key.Fornitore, Articolo = g.Key.Articolo, UnitaMisura = g.Key.UnitaMisura, Quantita = g.Sum(x => x.Giacenza) })
                .OrderBy(x => x.Articolo).ToList()
        };
        return View(m);
    }

    [HttpPost("{id:int}/MettiInUso")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MettiInUso(int id, DateOnly? data, string? vista, string? cerca, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        IActionResult Indietro(string? errore = null, string? messaggio = null)
        {
            if (errore != null) TempData["Errore"] = errore;
            if (messaggio != null) TempData["Messaggio"] = messaggio;
            return RedirectToAction(nameof(Index), new { vista, cerca });
        }
        var lotto = await _db.Lotti.FirstOrDefaultAsync(l => l.IdLotto == id && l.Tipo == TipoLotto.Ingresso, ct);
        if (lotto is null) return NotFound();
        var categoria = lotto.IdCategoria ?? await _db.Articoli.AsNoTracking().Where(a => a.IdArticolo == lotto.IdArticolo).Select(a => a.IdCategoria).FirstOrDefaultAsync(ct);
        if (categoria is null) return Indietro("Il lotto non ha una matrice: assegnala all'articolo prima di metterlo in uso.");
        var giacenza = await _db.MovimentiLotto.Where(m => m.IdLotto == id).SumAsync(m => (decimal?)m.Quantita, ct) ?? 0m;
        if (giacenza <= 0) return Indietro("Il lotto non ha giacenza: non si può mettere in uso.");
        if (await _db.LottiInUso.AnyAsync(u => u.IdLotto == id && u.Fine == null, ct)) return Indietro("Il lotto è già in uso.");
        if (lotto.Stato is StatoLotto.Bloccato or StatoLotto.Richiamato) return Indietro("Il lotto è bloccato o richiamato: non si può usare.");

        var giorno = data ?? DateOnly.FromDateTime(DateTime.Now);
        if (giorno > DateOnly.FromDateTime(DateTime.Now)) return Indietro("La data di impegno non può essere nel futuro.");
        var inizio = giorno == DateOnly.FromDateTime(DateTime.Now) ? DateTime.UtcNow : giorno.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).FirstOrDefaultAsync(ct);
        if (sede is null) return Indietro("Manca una sede attiva.");

        // Il lotto della stessa matrice che era in uso si chiude: ne resta uno solo alla volta.
        var aperti = await _db.LottiInUso.Where(u => u.IdInsediamento == sede.IdInsediamento && u.IdCategoria == categoria && u.Fine == null).ToListAsync(ct);
        foreach (var u in aperti) u.Fine = inizio < u.Inizio ? u.Inizio : inizio;
        _db.LottiInUso.Add(new LottoInUso { IdInsediamento = sede.IdInsediamento, IdCategoria = categoria.Value, IdLotto = id, Inizio = inizio, IdUtente = _db.IdUtenteCorrente });
        Registra("LottoMessoInUso", id, $"Lotto {lotto.Codice} in uso dal {giorno:dd/MM/yyyy}");
        await _db.SaveChangesAsync(ct);
        return Indietro(messaggio: $"Lotto {lotto.Codice} messo in uso.");
    }

    [HttpPost("{id:int}/Esaurito")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Esaurito(int id, string? vista, string? cerca, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var lotto = await _db.Lotti.FirstOrDefaultAsync(l => l.IdLotto == id && l.Tipo == TipoLotto.Ingresso, ct);
        if (lotto is null) return NotFound();
        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).FirstOrDefaultAsync(ct);
        if (sede is null) { TempData["Errore"] = "Manca una sede attiva."; return RedirectToAction(nameof(Index), new { vista, cerca }); }

        var adesso = DateTime.UtcNow;
        var giacenza = await _db.MovimentiLotto.Where(m => m.IdLotto == id).SumAsync(m => (decimal?)m.Quantita, ct) ?? 0m;
        var unita = await _db.Articoli.AsNoTracking().Where(a => a.IdArticolo == lotto.IdArticolo).Select(a => a.UnitaMisura).FirstAsync(ct);
        if (giacenza > 0)
            _db.MovimentiLotto.Add(new MovimentoLotto
            {
                IdInsediamento = sede.IdInsediamento, IdLotto = id, Tipo = TipoMovimento.Rettifica, Quantita = -giacenza, UnitaMisura = unita,
                DataMovimento = adesso, Nota = "Lotto segnato come esaurito", IdUtente = _db.IdUtenteCorrente, DataCreazione = adesso
            });
        foreach (var u in await _db.LottiInUso.Where(u => u.IdLotto == id && u.Fine == null).ToListAsync(ct)) u.Fine = adesso;
        lotto.Stato = StatoLotto.Esaurito;
        Registra("LottoEsaurito", id, $"Lotto {lotto.Codice} esaurito (residuo {giacenza:0.##})");
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = $"Lotto {lotto.Codice} segnato come esaurito.";
        return RedirectToAction(nameof(Index), new { vista, cerca });
    }

    private void Registra(string azione, int idLotto, string dettaglio) =>
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, Azione = azione, Entita = "Lotto", IdEntita = idLotto.ToString(), Dopo = dettaglio,
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
        });
}
