using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Consultazione dei lotti dell'azienda: elenco, scheda e rintracciabilità. Solo lettura.</summary>
[Authorize]
[Route("Lotti")]
public class LottiController : Controller
{
    private readonly ContestoTracciabilita _db;

    public LottiController(ContestoTracciabilita db) => _db = db;

    // Il gestore MARE.A non appartiene a un'azienda: vedrebbe i dati di tutte mescolati.
    private bool EGestore => User.HasClaim("tr:gestore", "true");

    private static string NormalizzaTipo(string? tipo) => tipo == "pf" ? "pf" : "mp";

    private IActionResult SoloAzienda() => View("SoloAzienda");

    [HttpGet("")]
    public async Task<IActionResult> Index(string? cerca, bool soloInMagazzino, string? tipo = null, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return SoloAzienda();

        // Prima si sceglie la pagina di lotti (query leggera), poi giacenza e data di arrivo si calcolano solo per quei 50:
        // calcolarle per tutti i lotti dell'archivio rendeva la pagina lenta fino al timeout.
        var q = from l in _db.Lotti.AsNoTracking()
                join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                from f in fj.DefaultIfEmpty()
                select new { l.IdLotto, l.Codice, l.Tipo, l.Stato, l.DataCreazione, Articolo = a.Descrizione, a.UnitaMisura, Fornitore = f != null ? f.RagioneSociale : null };

        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(r => r.Codice.Contains(t) || r.Articolo.Contains(t) || (r.Fornitore != null && r.Fornitore.Contains(t)));
        }
        if (soloInMagazzino)
            q = q.Where(r => _db.MovimentiLotto.Where(mv => mv.IdLotto == r.IdLotto).Sum(mv => (decimal?)mv.Quantita) > 0);
        // Materie prime (lotti arrivati dai fornitori) e prodotti finiti (lotti prodotti in azienda) si cercano separatamente.
        q = NormalizzaTipo(tipo) == "pf" ? q.Where(r => r.Tipo != TipoLotto.Ingresso) : q.Where(r => r.Tipo == TipoLotto.Ingresso);

        var m = new ModelloElencoLotti { Cerca = cerca, SoloInMagazzino = soloInMagazzino, FiltroTipo = NormalizzaTipo(tipo) };
        m.Totale = await q.CountAsync(ct);
        m.Pagina = Math.Clamp(pagina, 1, m.Pagine);
        var pag = await q.OrderByDescending(r => r.DataCreazione).ThenByDescending(r => r.IdLotto)
            .Skip((m.Pagina - 1) * m.PerPagina).Take(m.PerPagina).ToListAsync(ct);
        var ids = pag.Select(r => r.IdLotto).ToList();
        var giacenze = (await _db.MovimentiLotto.AsNoTracking().Where(mv => ids.Contains(mv.IdLotto))
                .GroupBy(mv => mv.IdLotto).Select(g => new { IdLotto = g.Key, Somma = g.Sum(x => x.Quantita) }).ToListAsync(ct))
            .ToDictionary(x => x.IdLotto, x => x.Somma);
        var arrivi = (await _db.Accettazioni.AsNoTracking().Where(x => x.IdLotto != null && ids.Contains(x.IdLotto.Value))
                .GroupBy(x => x.IdLotto!.Value).Select(g => new { IdLotto = g.Key, Data = g.Min(x => (DateOnly?)x.DataArrivo) }).ToListAsync(ct))
            .ToDictionary(x => x.IdLotto, x => x.Data);
        m.Righe = pag.Select(r => new RigaLotto
        {
            IdLotto = r.IdLotto, Codice = r.Codice, Articolo = r.Articolo, Fornitore = r.Fornitore, Tipo = r.Tipo, Stato = r.Stato,
            UnitaMisura = r.UnitaMisura, Giacenza = giacenze.TryGetValue(r.IdLotto, out var gg) ? gg : 0m,
            DataArrivo = arrivi.TryGetValue(r.IdLotto, out var da) ? da : null
        }).ToList();
        return View(m);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Scheda(int id, string? cerca, bool soloInMagazzino, string? tipo = null, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return SoloAzienda();

        var dati = await (from l in _db.Lotti.AsNoTracking()
                          where l.IdLotto == id
                          join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                          join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                          from f in fj.DefaultIfEmpty()
                          select new { l, a, Fornitore = f != null ? f.RagioneSociale : null })
            .FirstOrDefaultAsync(ct);
        if (dati is null) return NotFound();

        // Matrice con cui il lotto è arrivato; se non c'è, la categoria abituale dell'articolo.
        var idCategoria = dati.l.IdCategoria ?? dati.a.IdCategoria;
        var nomeCategoria = idCategoria is null ? null
            : await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.IdCategoria == idCategoria).Select(c => c.Nome).FirstOrDefaultAsync(ct);

        var movimenti = await _db.MovimentiLotto.AsNoTracking().Where(x => x.IdLotto == id)
            .OrderBy(x => x.DataMovimento).ThenBy(x => x.IdMovimento).ToListAsync(ct);
        var accettazioni = await _db.Accettazioni.AsNoTracking().Where(x => x.IdLotto == id)
            .OrderBy(x => x.DataArrivo).ToListAsync(ct);

        var m = new ModelloSchedaLotto
        {
            IdLotto = id,
            Cerca = cerca, SoloInMagazzino = soloInMagazzino, FiltroTipo = NormalizzaTipo(tipo), Pagina = pagina,
            Codice = dati.l.Codice,
            Articolo = dati.a.Descrizione,
            Categoria = nomeCategoria,
            Fornitore = dati.Fornitore,
            Tipo = dati.l.Tipo,
            Stato = dati.l.Stato,
            GiornoLottoInterno = dati.l.GiornoLottoInterno,
            DataScadenza = dati.l.DataScadenza,
            ETmc = dati.l.ETmc,
            MotivoBlocco = dati.l.MotivoBlocco,
            UnitaMisura = dati.a.UnitaMisura,
            Giacenza = movimenti.Sum(x => x.Quantita),
            Accettazioni = accettazioni.Select(x => new RigaAccettazioneLotto
            {
                DataArrivo = x.DataArrivo, TipoDocumento = x.TipoDocumento, NumeroDocumento = x.NumeroDocumento,
                DataDocumento = x.DataDocumento, Quantita = x.Quantita, UnitaMisura = x.UnitaMisura, Note = x.NoteReclami
            }).ToList(),
            Movimenti = movimenti.Select(x => new RigaMovimentoLotto
            {
                Data = x.DataMovimento, Tipo = x.Tipo, Quantita = x.Quantita, UnitaMisura = x.UnitaMisura, Nota = x.Nota
            }).ToList()
        };
        return View(m);
    }

    // Toglie il blocco a un lotto bloccato o richiamato. Il motivo è obbligatorio e resta nel registro attività.
    [HttpPost("{id:int}/Sblocca")]
    [ValidateAntiForgeryToken]
    [Servizi.RichiedeConfigurazione]
    public async Task<IActionResult> Sblocca(int id, string? motivo, string? cerca, bool soloInMagazzino, string? tipo = null, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return SoloAzienda();
        var lotto = await _db.Lotti.FirstOrDefaultAsync(l => l.IdLotto == id, ct);
        if (lotto is null) return NotFound();
        var rotta = RedirectToAction(nameof(Scheda), new { id, cerca, soloInMagazzino, tipo, pagina });
        if (lotto.Stato is not (StatoLotto.Bloccato or StatoLotto.Richiamato))
        {
            TempData["Messaggio"] = "Il lotto non è bloccato.";
            return rotta;
        }
        var testo = motivo?.Trim();
        if (string.IsNullOrEmpty(testo))
        {
            TempData["Messaggio"] = "Per sbloccare scrivi il motivo (per esempio «analisi negative, lotto idoneo»).";
            return rotta;
        }
        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).Select(i => (int?)i.IdInsediamento).FirstOrDefaultAsync(ct);
        var giacenza = await _db.MovimentiLotto.Where(x => x.IdLotto == id).SumAsync(x => (decimal?)x.Quantita, ct) ?? 0m;
        var prima = $"{lotto.Stato}: {lotto.MotivoBlocco}";
        lotto.Stato = giacenza > 0m ? StatoLotto.Attivo : StatoLotto.Esaurito;
        lotto.MotivoBlocco = null;
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, IdInsediamento = sede, Azione = "LottoSbloccato", Entita = "Lotto", IdEntita = id.ToString(),
            Prima = prima, Dopo = $"{lotto.Stato}. Motivo dello sblocco: {testo}",
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = "Lotto sbloccato.";
        return rotta;
    }

    [HttpGet("{id:int}/Rintracciabilita")]
    public async Task<IActionResult> Rintracciabilita(int id, string? cerca, bool soloInMagazzino, string? tipo = null, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return SoloAzienda();

        var origine = await (from l in _db.Lotti.AsNoTracking()
                             where l.IdLotto == id
                             join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                             select new { l.Codice, a.Descrizione }).FirstOrDefaultAsync(ct);
        if (origine is null) return NotFound();

        // Produzioni annullate e spedizioni annullate non contano.
        var consumi = await (from c in _db.ProduzioniConsumi.AsNoTracking()
                             join p in _db.Produzioni.AsNoTracking() on c.IdProduzione equals p.IdProduzione
                             where !p.Annullata
                             select new ArcoConsumo(p.IdProduzione, c.IdLottoIngresso, p.IdLottoProdotto, p.Giorno, c.Quantita))
            .ToListAsync(ct);
        var spedizioni = await (from r in _db.SpedizioniRighe.AsNoTracking()
                                join s in _db.Spedizioni.AsNoTracking() on r.IdSpedizione equals s.IdSpedizione
                                where !s.Annullata
                                select new ArcoSpedizione(s.IdSpedizione, r.IdLotto, s.IdCliente, s.DataSpedizione, s.NumeroDocumento, r.Quantita))
            .ToListAsync(ct);

        var motore = new MotoreRintracciabilita(consumi, spedizioni);
        var indietro = motore.Indietro(id);
        var avanti = motore.Avanti(id);

        var idLotti = indietro.Lotti.Concat(avanti.Lotti).Select(x => x.IdLotto).Distinct().ToList();
        var descr = await (from l in _db.Lotti.AsNoTracking()
                           where idLotti.Contains(l.IdLotto)
                           join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                           join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore into fj
                           from f in fj.DefaultIfEmpty()
                           select new { l.IdLotto, l.Codice, Articolo = a.Descrizione, Fornitore = f != null ? f.RagioneSociale : null })
            .ToDictionaryAsync(x => x.IdLotto, ct);

        List<RigaLottoRaggiunto> Righe(RisultatoRicerca r) => r.Lotti.Where(x => x.Livello > 0)
            .Select(x => new RigaLottoRaggiunto
            {
                IdLotto = x.IdLotto, Livello = x.Livello,
                Codice = descr.TryGetValue(x.IdLotto, out var d) ? d.Codice : "?",
                Articolo = descr.TryGetValue(x.IdLotto, out d) ? d.Articolo : "",
                Fornitore = d?.Fornitore
            }).ToList();

        var idClienti = avanti.Clienti.ToList();
        var nomiClienti = await _db.Clienti.AsNoTracking().Where(c => idClienti.Contains(c.IdCliente))
            .ToDictionaryAsync(c => c.IdCliente, c => c.RagioneSociale, ct);

        var m = new ModelloRintracciabilita
        {
            IdLotto = id, Codice = origine.Codice, Articolo = origine.Descrizione,
            Cerca = cerca, SoloInMagazzino = soloInMagazzino, FiltroTipo = NormalizzaTipo(tipo), Pagina = pagina,
            ABMonte = Righe(indietro), AValle = Righe(avanti),
            ProduzioniMonte = indietro.Produzioni.Count, ProduzioniValle = avanti.Produzioni.Count,
            Troncato = indietro.Troncato || avanti.Troncato,
            Clienti = avanti.Spedizioni.Select(s => new RigaClienteRaggiunto
            {
                Cliente = nomiClienti.GetValueOrDefault(s.IdCliente, "?"),
                Data = s.Data, NumeroDocumento = s.NumeroDocumento, Quantita = s.Quantita,
                LottoCodice = descr.TryGetValue(s.IdLotto, out var d) ? d.Codice : "?"
            }).OrderBy(x => x.Data).ToList()
        };
        return View(m);
    }
}
