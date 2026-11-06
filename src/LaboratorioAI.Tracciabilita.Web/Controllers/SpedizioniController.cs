using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>
/// Spedizioni ai clienti: quali lotti di prodotto sono partiti, per chi e con quale documento. È la parte «a valle»
/// che permette di sapere chi avvisare in caso di richiamo. Una spedizione non si modifica: si annulla (con storno dei movimenti).
/// </summary>
[Authorize]
[Route("Spedizioni")]
public class SpedizioniController : Controller
{
    private readonly ContestoTracciabilita _db;

    public SpedizioniController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");

    [HttpGet("")]
    public async Task<IActionResult> Index(string? cerca, DateOnly? dal, DateOnly? al, int pagina = 1, CancellationToken ct = default)
    {
        if (EGestore) return SoloAzienda();

        var q = from s in _db.Spedizioni.AsNoTracking()
                join c in _db.Clienti.AsNoTracking() on s.IdCliente equals c.IdCliente
                select new { s.IdSpedizione, s.DataSpedizione, s.DataCreazione, Cliente = c.RagioneSociale, s.NumeroDocumento, s.Annullata };

        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(r => r.Cliente.Contains(t) || (r.NumeroDocumento != null && r.NumeroDocumento.Contains(t))
                || _db.SpedizioniRighe.Any(x => x.IdSpedizione == r.IdSpedizione && _db.Lotti.Any(l => l.IdLotto == x.IdLotto && l.Codice.Contains(t))));
        }
        if (dal is { } d1) q = q.Where(r => r.DataSpedizione >= d1);
        if (al is { } d2) q = q.Where(r => r.DataSpedizione <= d2);

        var m = new ModelloElencoSpedizioni { Cerca = cerca, Dal = dal, Al = al };
        m.Totale = await q.CountAsync(ct);
        m.Pagina = Math.Clamp(pagina, 1, m.Pagine);
        var pag = await q.OrderByDescending(r => r.DataSpedizione).ThenByDescending(r => r.DataCreazione)
            .Skip((m.Pagina - 1) * m.PerPagina).Take(m.PerPagina).ToListAsync(ct);

        var ids = pag.Select(r => r.IdSpedizione).ToList();
        var lotti = (await (from x in _db.SpedizioniRighe.AsNoTracking()
                            where ids.Contains(x.IdSpedizione)
                            join l in _db.Lotti.AsNoTracking() on x.IdLotto equals l.IdLotto
                            select new { x.IdSpedizione, l.Codice }).ToListAsync(ct))
            .GroupBy(x => x.IdSpedizione).ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.Codice).Distinct()));
        m.Righe = pag.Select(r => new RigaSpedizione
        {
            IdSpedizione = r.IdSpedizione, Data = r.DataSpedizione, Cliente = r.Cliente, NumeroDocumento = r.NumeroDocumento,
            Annullata = r.Annullata, Lotti = lotti.GetValueOrDefault(r.IdSpedizione, "")
        }).ToList();
        return View(m);
    }

    [HttpGet("Nuova")]
    public async Task<IActionResult> Nuova(CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        return View(await CostruisciModelloAsync(new ModelloNuovaSpedizione { Data = DateOnly.FromDateTime(DateTime.Now) }, ct));
    }

    private async Task<ModelloNuovaSpedizione> CostruisciModelloAsync(ModelloNuovaSpedizione m, CancellationToken ct)
    {
        m.Clienti = await _db.Clienti.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.RagioneSociale)
            .Select(c => new OpzioneCliente { IdCliente = c.IdCliente, Nome = c.RagioneSociale }).ToListAsync(ct);
        // Lotti di prodotto (non materie prime) ancora spedibili: attivi, creati negli ultimi 90 giorni o con giacenza.
        var da = DateTime.UtcNow.AddDays(-90);
        var candidati = await (from l in _db.Lotti.AsNoTracking()
                               where l.Tipo != TipoLotto.Ingresso && l.Stato == StatoLotto.Attivo && l.DataCreazione >= da
                               join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                               orderby l.DataCreazione descending, l.IdLotto descending
                               select new { l.IdLotto, l.Codice, Prodotto = a.Descrizione, a.UnitaMisura }).Take(200).ToListAsync(ct);
        var ids = candidati.Select(x => x.IdLotto).ToList();
        var giacenze = (await _db.MovimentiLotto.AsNoTracking().Where(mv => ids.Contains(mv.IdLotto))
                .GroupBy(mv => mv.IdLotto).Select(g => new { Id = g.Key, S = g.Sum(x => x.Quantita) }).ToListAsync(ct))
            .ToDictionary(x => x.Id, x => x.S);
        m.Lotti = candidati.Select(x => new OpzioneLottoSpedibile
        {
            IdLotto = x.IdLotto,
            Testo = $"{x.Codice} - {x.Prodotto}" + (giacenze.GetValueOrDefault(x.IdLotto) > 0 ? $" (in magazzino {giacenze[x.IdLotto]:0.##} {x.UnitaMisura})" : "")
        }).ToList();
        return m;
    }

    [HttpPost("Nuova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Registra(DateOnly data, int? idCliente, string? numeroDocumento, DateOnly? dataDocumento, string? note,
        int?[]? lotto, decimal?[]? quantita, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var documento = string.IsNullOrWhiteSpace(numeroDocumento) ? null : numeroDocumento.Trim();
        var modello = new ModelloNuovaSpedizione { Data = data, IdCliente = idCliente, NumeroDocumento = documento, DataDocumento = dataDocumento, Note = note };

        async Task<IActionResult> Errore(string testo)
        {
            TempData["Errore"] = testo;
            return View("Nuova", await CostruisciModelloAsync(modello, ct));
        }

        if (idCliente is null || !await _db.Clienti.AnyAsync(c => c.IdCliente == idCliente && c.Attivo, ct))
            return await Errore("Scegli il cliente.");
        if (data > DateOnly.FromDateTime(DateTime.Now).AddDays(1)) return await Errore("La data della spedizione non può essere nel futuro.");

        var righe = new List<(int IdLotto, decimal? Quantita)>();
        for (var i = 0; i < (lotto?.Length ?? 0); i++)
        {
            if (lotto![i] is not { } idLotto) continue;
            var q = quantita is not null && i < quantita.Length ? quantita[i] : null;
            if (q is <= 0) return await Errore("La quantità deve essere maggiore di zero (oppure lasciala vuota).");
            if (righe.Any(r => r.IdLotto == idLotto)) return await Errore("Lo stesso lotto è indicato due volte: scrivi la quantità totale in una sola riga.");
            righe.Add((idLotto, q));
        }
        if (righe.Count == 0) return await Errore("Scegli almeno un lotto.");

        var idsLotti = righe.Select(r => r.IdLotto).ToList();
        var lotti = await (from l in _db.Lotti.AsNoTracking()
                           where idsLotti.Contains(l.IdLotto)
                           join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                           select new { l.IdLotto, l.Codice, l.Tipo, l.Stato, a.UnitaMisura }).ToListAsync(ct);
        if (lotti.Count != idsLotti.Count || lotti.Any(l => l.Tipo == TipoLotto.Ingresso))
            return await Errore("Uno dei lotti scelti non può essere spedito.");
        var bloccato = lotti.FirstOrDefault(l => l.Stato is StatoLotto.Bloccato or StatoLotto.Richiamato);
        if (bloccato != null) return await Errore($"Il lotto {bloccato.Codice} è bloccato o richiamato: non si può spedire.");

        // Doppio clic o documento già registrato per lo stesso cliente.
        var soglia = DateTime.UtcNow.AddMinutes(-2);
        if (await _db.Spedizioni.AnyAsync(s => !s.Annullata && s.IdCliente == idCliente && s.DataSpedizione == data
            && (documento != null ? s.NumeroDocumento == documento : s.DataCreazione > soglia), ct))
            return await Errore(documento != null
                ? $"Per questo cliente esiste già una spedizione del {data:dd/MM/yyyy} con il documento {documento}."
                : "Una spedizione per questo cliente è stata appena registrata. Se serve una seconda, attendi due minuti.");

        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).FirstOrDefaultAsync(ct);
        if (sede is null) return await Errore("Manca una sede attiva: impossibile registrare la spedizione.");

        var adesso = DateTime.UtcNow;
        var idUtente = _db.IdUtenteCorrente;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var sped = new Spedizione
        {
            IdInsediamento = sede.IdInsediamento, IdCliente = idCliente.Value, DataSpedizione = data, NumeroDocumento = documento,
            DataDocumento = dataDocumento, Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), IdUtente = idUtente, DataCreazione = adesso
        };
        foreach (var (idLotto, q) in righe)
            sped.Righe.Add(new SpedizioneRiga { IdLotto = idLotto, Quantita = q, UnitaMisura = q is null ? null : lotti.First(l => l.IdLotto == idLotto).UnitaMisura });
        _db.Spedizioni.Add(sped);
        await _db.SaveChangesAsync(ct);

        // Se si indica la quantità il magazzino cala, ma mai sotto zero (la quantità spedita resta registrata per intero).
        var insufficienti = new List<string>();
        foreach (var (idLotto, q) in righe.Where(r => r.Quantita is > 0))
        {
            var l = lotti.First(x => x.IdLotto == idLotto);
            var giacenza = Math.Max(0m, await _db.MovimentiLotto.Where(mv => mv.IdLotto == idLotto).SumAsync(mv => (decimal?)mv.Quantita, ct) ?? 0m);
            var scarico = Math.Min(q!.Value, giacenza);
            if (scarico < q.Value) insufficienti.Add(l.Codice);
            if (scarico > 0)
                _db.MovimentiLotto.Add(new MovimentoLotto
                {
                    IdInsediamento = sede.IdInsediamento, IdLotto = idLotto, Tipo = TipoMovimento.Spedizione, Quantita = -scarico,
                    UnitaMisura = l.UnitaMisura, DataMovimento = adesso, IdSpedizione = sped.IdSpedizione,
                    Nota = $"Spedizione del {data:dd/MM/yyyy}", IdUtente = idUtente, DataCreazione = adesso
                });
        }
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = idUtente, IdInsediamento = sede.IdInsediamento, Azione = "SpedizioneRegistrata", Entita = "Spedizione",
            IdEntita = sped.IdSpedizione.ToString(), Dopo = $"Cliente {idCliente}, {righe.Count} lotti, documento {documento ?? "-"}",
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = adesso
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var testo = $"Spedizione registrata: {righe.Count} {(righe.Count == 1 ? "lotto" : "lotti")}.";
        if (insufficienti.Count > 0)
            testo += $" Attenzione: la giacenza non bastava per {string.Join(", ", insufficienti)} (il magazzino è stato portato a zero).";
        TempData["Messaggio"] = testo;
        return RedirectToAction(nameof(Scheda), new { id = sped.IdSpedizione });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Scheda(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var s = await (from x in _db.Spedizioni.AsNoTracking()
                       where x.IdSpedizione == id
                       join c in _db.Clienti.AsNoTracking() on x.IdCliente equals c.IdCliente
                       select new { x, Cliente = c.RagioneSociale }).FirstOrDefaultAsync(ct);
        if (s is null) return NotFound();
        var righe = await (from r in _db.SpedizioniRighe.AsNoTracking()
                           where r.IdSpedizione == id
                           join l in _db.Lotti.AsNoTracking() on r.IdLotto equals l.IdLotto
                           join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                           select new RigaSchedaSpedizione { IdLotto = l.IdLotto, Lotto = l.Codice, Prodotto = a.Descrizione, Quantita = r.Quantita, UnitaMisura = r.UnitaMisura })
            .ToListAsync(ct);
        return View(new ModelloSchedaSpedizione
        {
            IdSpedizione = id, Data = s.x.DataSpedizione, Cliente = s.Cliente, NumeroDocumento = s.x.NumeroDocumento,
            DataDocumento = s.x.DataDocumento, Note = s.x.Note, Annullata = s.x.Annullata, Righe = righe
        });
    }

    /// <summary>Annulla la spedizione: i movimenti di magazzino si compensano con uno storno, la spedizione resta nel registro.</summary>
    [HttpPost("{id:int}/Annulla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Annulla(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var sped = await _db.Spedizioni.FirstOrDefaultAsync(s => s.IdSpedizione == id, ct);
        if (sped is null) return NotFound();
        if (sped.Annullata)
        {
            TempData["Errore"] = "La spedizione è già annullata.";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        var adesso = DateTime.UtcNow;
        var idUtente = _db.IdUtenteCorrente;
        var movimenti = await _db.MovimentiLotto.AsNoTracking().Where(mv => mv.IdSpedizione == id && mv.Tipo == TipoMovimento.Spedizione).ToListAsync(ct);
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        sped.Annullata = true;
        foreach (var mv in movimenti)
            _db.MovimentiLotto.Add(new MovimentoLotto
            {
                IdInsediamento = mv.IdInsediamento, IdLotto = mv.IdLotto, Tipo = TipoMovimento.Storno, Quantita = -mv.Quantita, UnitaMisura = mv.UnitaMisura,
                DataMovimento = adesso, IdSpedizione = id, IdMovimentoStornato = mv.IdMovimento, Nota = "Spedizione annullata",
                IdUtente = idUtente, DataCreazione = adesso
            });
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = idUtente, IdInsediamento = sped.IdInsediamento, Azione = "SpedizioneAnnullata", Entita = "Spedizione",
            IdEntita = id.ToString(), Dopo = $"Annullata, {movimenti.Count} movimenti stornati",
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = adesso
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Messaggio"] = "Spedizione annullata: il magazzino è stato ripristinato.";
        return RedirectToAction(nameof(Scheda), new { id });
    }
}
