using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using LaboratorioAI.Tracciabilita.Web.Servizi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Ordini ai fornitori e quadro consegne. Un ordine inviato resta in «da consegnare» finché la merce non viene accettata.</summary>
[Authorize]
[Route("Ordini")]
public class OrdiniController : Controller
{
    private readonly ContestoTracciabilita _db;
    private readonly IInvioEmail _email;

    public OrdiniController(ContestoTracciabilita db, IInvioEmail email)
    {
        _db = db; _email = email;
    }

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");
    private static string? Pulisci(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    // ── Quadro consegne ────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index(string? vista, string? cerca, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        vista = vista is "bozze" or "ricevuti" or "tutti" ? vista : "da-consegnare";
        var q = from o in _db.Ordini.AsNoTracking()
                join f in _db.Fornitori.AsNoTracking() on o.IdFornitore equals f.IdFornitore
                select new { o, f.RagioneSociale };
        q = vista switch
        {
            "bozze" => q.Where(x => x.o.Stato == StatoOrdine.Bozza),
            "ricevuti" => q.Where(x => x.o.Stato == StatoOrdine.Ricevuto),
            "tutti" => q,
            _ => q.Where(x => x.o.Stato == StatoOrdine.Inviato)
        };
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(x => x.o.Numero.Contains(t) || x.RagioneSociale.Contains(t));
        }
        var ordini = await q.OrderByDescending(x => x.o.DataOrdine).ThenByDescending(x => x.o.IdOrdine).Take(300).ToListAsync(ct);
        var ids = ordini.Select(x => x.o.IdOrdine).ToList();
        var righe = await (from r in _db.OrdiniRighe.AsNoTracking()
                           join a in _db.Articoli.AsNoTracking() on r.IdArticolo equals a.IdArticolo
                           where ids.Contains(r.IdOrdine) && r.Attiva
                           select new { r.IdOrdine, a.Descrizione }).ToListAsync(ct);
        var m = new ModelloQuadroConsegne { Vista = vista, Cerca = cerca };
        m.Righe = ordini.Select(x => new RigaQuadro
        {
            IdOrdine = x.o.IdOrdine, Numero = x.o.Numero, DataOrdine = x.o.DataOrdine, DataConsegnaPrevista = x.o.DataConsegnaPrevista,
            Fornitore = x.RagioneSociale, Stato = x.o.Stato,
            Articoli = string.Join(", ", righe.Where(r => r.IdOrdine == x.o.IdOrdine).Select(r => r.Descrizione))
        }).ToList();
        return View(m);
    }

    // ── Nuovo ordine ───────────────────────────────────────────────
    [HttpGet("Nuovo")]
    public async Task<IActionResult> Nuovo(int? idFornitore, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = new ModelloNuovoOrdine { IdFornitore = idFornitore };
        await CaricaNuovoAsync(m, ct);
        return View(m);
    }

    [HttpPost("Nuovo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuovo(ModelloNuovoOrdine m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        if (!ModelState.IsValid) { await CaricaNuovoAsync(m, ct); return View(m); }
        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).FirstOrDefaultAsync(ct);
        if (sede is null)
        {
            ModelState.AddModelError("", "Manca una sede attiva.");
            await CaricaNuovoAsync(m, ct); return View(m);
        }
        var numeri = await _db.Ordini.AsNoTracking().Select(o => o.Numero).ToListAsync(ct);
        var numero = NumerazioneOrdini.Prossimo(numeri, m.NumeroIniziale ?? 1);
        var ordine = new Ordine
        {
            IdInsediamento = sede.IdInsediamento, Numero = numero.ToString(), DataOrdine = DateOnly.FromDateTime(DateTime.Now),
            DataConsegnaPrevista = m.DataConsegnaPrevista, IdFornitore = m.IdFornitore!.Value, Stato = StatoOrdine.Bozza,
            Note = Pulisci(m.Note), IdUtente = _db.IdUtenteCorrente, DataCreazione = DateTime.UtcNow
        };
        _db.Ordini.Add(ordine);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Un altro ordine ha appena preso questo numero. Riprova.");
            await CaricaNuovoAsync(m, ct); return View(m);
        }
        Registra("OrdineCreato", ordine.IdOrdine, $"Ordine n. {ordine.Numero}");
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Scheda), new { id = ordine.IdOrdine });
    }

    private async Task CaricaNuovoAsync(ModelloNuovoOrdine m, CancellationToken ct)
    {
        m.Fornitori = await _db.Fornitori.AsNoTracking().Where(f => f.Attivo).OrderBy(f => f.RagioneSociale)
            .Select(f => new OpzioneAnagrafica { Id = f.IdFornitore, Nome = f.RagioneSociale }).ToListAsync(ct);
        var numeri = await _db.Ordini.AsNoTracking().Select(o => o.Numero).ToListAsync(ct);
        m.PrimoOrdine = numeri.Count == 0;
        m.NumeroProposto = NumerazioneOrdini.Prossimo(numeri, m.NumeroIniziale ?? 1);
    }

    // ── Scheda ordine ──────────────────────────────────────────────
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Scheda(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = await CostruisciSchedaAsync(id, ct);
        return m is null ? NotFound() : View(m);
    }

    [HttpPost("{id:int}/Riga")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AggiungiRiga(int id, ModelloSchedaOrdine m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var ordine = await _db.Ordini.FirstOrDefaultAsync(o => o.IdOrdine == id, ct);
        if (ordine is null) return NotFound();
        if (ordine.Stato != StatoOrdine.Bozza) return Errore(id, "L'ordine è già stato inviato: le righe non si modificano più.");
        var articolo = m.IdArticolo is { } ia ? await _db.Articoli.AsNoTracking().FirstOrDefaultAsync(a => a.IdArticolo == ia, ct) : null;
        if (articolo is null || m.Quantita is not > 0) return Errore(id, "Scegli l'articolo e scrivi una quantità maggiore di zero.");
        var unita = Pulisci(m.UnitaMisura) ?? articolo.UnitaMisura;
        _db.OrdiniRighe.Add(new OrdineRiga
        {
            IdOrdine = id, IdArticolo = articolo.IdArticolo, Quantita = m.Quantita!.Value, UnitaMisura = unita,
            Colli = m.Colli is > 0 ? m.Colli : null, IdCategoria = m.IdCategoria ?? articolo.IdCategoria, Note = Pulisci(m.NoteRiga)
        });
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Scheda), new { id });
    }

    [HttpPost("{id:int}/Riga/{idRiga:int}/Togli")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogliRiga(int id, int idRiga, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var ordine = await _db.Ordini.AsNoTracking().FirstOrDefaultAsync(o => o.IdOrdine == id, ct);
        if (ordine is null) return NotFound();
        if (ordine.Stato != StatoOrdine.Bozza) return Errore(id, "L'ordine è già stato inviato: le righe non si modificano più.");
        var riga = await _db.OrdiniRighe.FirstOrDefaultAsync(r => r.IdOrdineRiga == idRiga && r.IdOrdine == id, ct);
        if (riga is null) return NotFound();
        riga.Attiva = false;
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Scheda), new { id });
    }

    // ── Stampa, invio, stati ───────────────────────────────────────
    [HttpGet("{id:int}/Stampa")]
    public async Task<IActionResult> Stampa(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = await CostruisciStampaAsync(id, ct);
        return m is null ? NotFound() : View(m);
    }

    [HttpPost("{id:int}/Invia")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invia(int id, string? destinatario, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var ordine = await _db.Ordini.FirstOrDefaultAsync(o => o.IdOrdine == id, ct);
        if (ordine is null) return NotFound();
        if (ordine.Stato is StatoOrdine.Annullato or StatoOrdine.Ricevuto) return Errore(id, "Questo ordine non si può più inviare.");
        var a = Pulisci(destinatario);
        if (a is null || !System.Net.Mail.MailAddress.TryCreate(a, out _)) return Errore(id, "Scrivi un indirizzo e-mail valido del fornitore.");
        var stampa = await CostruisciStampaAsync(id, ct);
        if (stampa is null || stampa.Righe.Count == 0) return Errore(id, "L'ordine non ha righe: aggiungi almeno un articolo.");

        var rispondiA = await _db.Utenti.AsNoTracking().Where(u => u.IdUtente == _db.IdUtenteCorrente).Select(u => u.Email).FirstOrDefaultAsync(ct);
        var esito = await _email.InviaAsync(a, CorpoOrdine.Oggetto(stampa), CorpoOrdine.Html(stampa), stampa.Azienda, rispondiA, ct);
        if (!esito.Riuscito) return Errore(id, esito.Errore ?? "Invio non riuscito.");

        ordine.Stato = StatoOrdine.Inviato;
        Registra("OrdineInviato", id, $"Ordine n. {ordine.Numero} inviato a {a}");
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = $"Ordine inviato a {a}.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    /// <summary>Per l'ordine dato per telefono o stampato e consegnato a mano.</summary>
    [HttpPost("{id:int}/SegnaInviato")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SegnaInviato(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var ordine = await _db.Ordini.FirstOrDefaultAsync(o => o.IdOrdine == id, ct);
        if (ordine is null) return NotFound();
        if (ordine.Stato != StatoOrdine.Bozza) return Errore(id, "L'ordine non è in bozza.");
        if (!await _db.OrdiniRighe.AnyAsync(r => r.IdOrdine == id && r.Attiva, ct)) return Errore(id, "L'ordine non ha righe: aggiungi almeno un articolo.");
        ordine.Stato = StatoOrdine.Inviato;
        Registra("OrdineInviato", id, $"Ordine n. {ordine.Numero} segnato come inviato");
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = "Ordine segnato come inviato: ora è nel quadro consegne.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    [HttpPost("{id:int}/Annulla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Annulla(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var ordine = await _db.Ordini.FirstOrDefaultAsync(o => o.IdOrdine == id, ct);
        if (ordine is null) return NotFound();
        if (ordine.Stato is not (StatoOrdine.Bozza or StatoOrdine.Inviato)) return Errore(id, "Questo ordine non si può annullare.");
        ordine.Stato = StatoOrdine.Annullato;
        Registra("OrdineAnnullato", id, $"Ordine n. {ordine.Numero}");
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = "Ordine annullato.";
        return RedirectToAction(nameof(Index));
    }

    // ── Supporto ───────────────────────────────────────────────────
    private IActionResult Errore(int id, string testo)
    {
        TempData["Errore"] = testo;
        return RedirectToAction(nameof(Scheda), new { id });
    }

    private void Registra(string azione, int idOrdine, string dettaglio) =>
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, Azione = azione, Entita = "Ordine", IdEntita = idOrdine.ToString(), Dopo = dettaglio,
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
        });

    private async Task<List<RigaOrdineVista>> RigheAsync(int idOrdine, CancellationToken ct) =>
        await (from r in _db.OrdiniRighe.AsNoTracking()
               join a in _db.Articoli.AsNoTracking() on r.IdArticolo equals a.IdArticolo
               join c in _db.CategorieMateriaPrima.AsNoTracking() on r.IdCategoria equals c.IdCategoria into cj
               from c in cj.DefaultIfEmpty()
               where r.IdOrdine == idOrdine && r.Attiva
               orderby r.IdOrdineRiga
               select new RigaOrdineVista
               {
                   IdOrdineRiga = r.IdOrdineRiga, Articolo = a.Descrizione, Matrice = c != null ? c.Nome : null,
                   Quantita = r.Quantita, UnitaMisura = r.UnitaMisura, Colli = r.Colli, Note = r.Note
               }).ToListAsync(ct);

    private async Task<ModelloSchedaOrdine?> CostruisciSchedaAsync(int id, CancellationToken ct)
    {
        var x = await (from o in _db.Ordini.AsNoTracking()
                       join f in _db.Fornitori.AsNoTracking() on o.IdFornitore equals f.IdFornitore
                       where o.IdOrdine == id
                       select new { o, f.RagioneSociale, f.Email, f.IdFornitore }).FirstOrDefaultAsync(ct);
        if (x is null) return null;
        var delFornitore = await _db.ArticoliFornitori.AsNoTracking().Where(l => l.IdFornitore == x.IdFornitore && l.Attivo).Select(l => l.IdArticolo).ToListAsync(ct);
        var articoli = await _db.Articoli.AsNoTracking().Where(a => a.Attivo && a.Tipo != TipoArticolo.ProdottoFinito).OrderBy(a => a.Descrizione)
            .Select(a => new OpzioneArticoloOrdine { IdArticolo = a.IdArticolo, Descrizione = a.Descrizione, UnitaMisura = a.UnitaMisura, IdCategoria = a.IdCategoria }).ToListAsync(ct);
        foreach (var a in articoli) a.DelFornitore = delFornitore.Contains(a.IdArticolo);
        return new ModelloSchedaOrdine
        {
            IdOrdine = id, Numero = x.o.Numero, DataOrdine = x.o.DataOrdine, DataConsegnaPrevista = x.o.DataConsegnaPrevista, Stato = x.o.Stato,
            Note = x.o.Note, Fornitore = x.RagioneSociale, EmailFornitore = x.Email, Righe = await RigheAsync(id, ct),
            Articoli = articoli.OrderByDescending(a => a.DelFornitore).ThenBy(a => a.Descrizione).ToList(),
            Categorie = await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome)
                .Select(c => new OpzioneAnagrafica { Id = c.IdCategoria, Nome = c.Nome }).ToListAsync(ct)
        };
    }

    private async Task<ModelloStampaOrdine?> CostruisciStampaAsync(int id, CancellationToken ct)
    {
        var x = await (from o in _db.Ordini.AsNoTracking()
                       join f in _db.Fornitori.AsNoTracking() on o.IdFornitore equals f.IdFornitore
                       join s in _db.Insediamenti.AsNoTracking() on o.IdInsediamento equals s.IdInsediamento
                       where o.IdOrdine == id
                       select new { o, f, s }).FirstOrDefaultAsync(ct);
        if (x is null) return null;
        var az = await _db.Aziende.AsNoTracking().FirstOrDefaultAsync(ct);
        string? indirizzoFornitore = string.Join(", ", new[] { x.f.Indirizzo, string.Join(" ", new[] { x.f.Cap, x.f.Citta, x.f.Provincia }.Where(t => !string.IsNullOrWhiteSpace(t))) }
            .Where(t => !string.IsNullOrWhiteSpace(t)));
        return new ModelloStampaOrdine
        {
            Azienda = az?.RagioneSociale ?? "", PartitaIvaAzienda = az?.PartitaIva, Sede = x.s.Nome, IndirizzoSede = x.s.Indirizzo,
            Numero = x.o.Numero, DataOrdine = x.o.DataOrdine, DataConsegnaPrevista = x.o.DataConsegnaPrevista, Note = x.o.Note,
            Fornitore = x.f.RagioneSociale, IndirizzoFornitore = indirizzoFornitore == "" ? null : indirizzoFornitore, TelefonoFornitore = x.f.Telefono,
            Righe = await RigheAsync(id, ct)
        };
    }
}
