using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Accettazione della merce in arrivo: controlli, giudizio, lotto in ingresso, carico in magazzino ed etichetta.</summary>
[Authorize]
[Route("Accettazioni")]
public class AccettazioniController : Controller
{
    private readonly ContestoTracciabilita _db;

    public AccettazioniController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");
    private IActionResult SoloAzienda() => View("~/Views/Lotti/SoloAzienda.cshtml");
    private static string? Pulisci(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    // ── Elenco ─────────────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index(string? cerca, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var q = from a in _db.Accettazioni.AsNoTracking()
                join f in _db.Fornitori.AsNoTracking() on a.IdFornitore equals f.IdFornitore
                join ar in _db.Articoli.AsNoTracking() on a.IdArticolo equals ar.IdArticolo
                join l in _db.Lotti.AsNoTracking() on a.IdLotto equals l.IdLotto into lj
                from l in lj.DefaultIfEmpty()
                select new RigaAccettazione
                {
                    IdAccettazione = a.IdAccettazione, DataArrivo = a.DataArrivo, Fornitore = f.RagioneSociale, Articolo = ar.Descrizione,
                    Lotto = l != null ? l.Codice : null, IdLotto = a.IdLotto, Quantita = a.Quantita, UnitaMisura = a.UnitaMisura, Giudizio = a.Giudizio,
                    NumeroOrdine = _db.OrdiniRighe.Where(r => r.IdOrdineRiga == a.IdOrdineRiga)
                        .Join(_db.Ordini, r => r.IdOrdine, o => o.IdOrdine, (r, o) => o.Numero).FirstOrDefault()
                };
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(r => r.Fornitore.Contains(t) || r.Articolo.Contains(t) || (r.Lotto != null && r.Lotto.Contains(t)));
        }
        var m = new ModelloElencoAccettazioni { Cerca = cerca, Totale = await q.CountAsync(ct) };
        m.Righe = await q.OrderByDescending(r => r.DataArrivo).ThenByDescending(r => r.IdAccettazione).Take(200).ToListAsync(ct);
        return View(m);
    }

    // ── Nuova accettazione ─────────────────────────────────────────
    [HttpGet("Nuova")]
    public async Task<IActionResult> Nuova(int? idOrdineRiga, int? idFornitore, int? idArticolo, int? idCategoria, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var m = new ModelloNuovaAccettazione { DataArrivo = DateOnly.FromDateTime(DateTime.Now) };

        if (idOrdineRiga is { } ir)
        {
            var x = await (from r in _db.OrdiniRighe.AsNoTracking()
                           join o in _db.Ordini.AsNoTracking() on r.IdOrdine equals o.IdOrdine
                           where r.IdOrdineRiga == ir && r.Attiva
                           select new { r, o }).FirstOrDefaultAsync(ct);
            if (x is null) return NotFound();
            if (x.o.Stato is StatoOrdine.Annullato or StatoOrdine.Bozza)
            {
                TempData["Errore"] = "Si accetta merce solo da ordini inviati e non annullati.";
                return RedirectToAction("Scheda", "Ordini", new { id = x.o.IdOrdine });
            }
            m.IdOrdineRiga = ir; m.NumeroOrdine = x.o.Numero; m.IdFornitore = x.o.IdFornitore; m.IdArticolo = x.r.IdArticolo;
            m.IdCategoria = x.r.IdCategoria; m.Quantita = x.r.Quantita; m.UnitaMisura = x.r.UnitaMisura;
        }
        else
        {
            m.IdFornitore = idFornitore; m.IdArticolo = idArticolo; m.IdCategoria = idCategoria;
        }

        if (m.IdFornitore is not null && m.IdArticolo is not null)
        {
            await CompletaAsync(m, ct);
            m.Pronto = true;
            await CaricaControlliAsync(m, ct);
        }
        else
        {
            m.Fornitori = await _db.Fornitori.AsNoTracking().Where(f => f.Attivo).OrderBy(f => f.RagioneSociale)
                .Select(f => new OpzioneAnagrafica { Id = f.IdFornitore, Nome = f.RagioneSociale }).ToListAsync(ct);
            m.Articoli = await _db.Articoli.AsNoTracking().Where(a => a.Attivo && a.Tipo != TipoArticolo.ProdottoFinito).OrderBy(a => a.Descrizione)
                .Select(a => new OpzioneAnagrafica { Id = a.IdArticolo, Nome = a.Descrizione }).ToListAsync(ct);
            m.Categorie = await ElencoCategorieAsync(ct);
        }
        return View(m);
    }

    [HttpPost("Nuova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuova(ModelloNuovaAccettazione m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        if (m.IdFornitore is null || m.IdArticolo is null) return RedirectToAction(nameof(Nuova));
        var inseriti = m.Controlli;
        await CompletaAsync(m, ct);
        m.Pronto = true;
        await CaricaControlliAsync(m, ct);
        foreach (var c in m.Controlli)
        {
            var i = inseriti.FirstOrDefault(x => x.Controllo == c.Controllo);
            if (i != null) { c.Conforme = i.Conforme; c.Valore = Pulisci(i.Valore); }
        }

        var errori = new List<string>();
        var respinto = m.Giudizio == GiudizioAccettazione.Respinto;
        if (m.Quantita is not > 0) errori.Add("Scrivi la quantità arrivata (maggiore di zero).");
        if (string.IsNullOrWhiteSpace(m.UnitaMisura)) errori.Add("Scrivi l'unità di misura.");
        if (!respinto && string.IsNullOrWhiteSpace(m.CodiceLotto)) errori.Add("Scrivi il lotto del fornitore.");
        if (m.DataArrivo > DateOnly.FromDateTime(DateTime.Now).AddDays(1)) errori.Add("La data di arrivo non può essere nel futuro.");
        if (!respinto)
            foreach (var c in m.Controlli.Where(c => c.Obbligatorio && c.Conforme is null))
                errori.Add($"Controllo obbligatorio da compilare: {c.Nome}.");
        if (m.IdOrdineRiga is { } ir)
        {
            var stato = await (from r in _db.OrdiniRighe.AsNoTracking() join o in _db.Ordini.AsNoTracking() on r.IdOrdine equals o.IdOrdine
                               where r.IdOrdineRiga == ir select (StatoOrdine?)o.Stato).FirstOrDefaultAsync(ct);
            if (stato is null or StatoOrdine.Annullato or StatoOrdine.Bozza) errori.Add("L'ordine collegato non è valido per l'accettazione.");
        }
        if (errori.Count > 0)
        {
            foreach (var e in errori) ModelState.AddModelError("", e);
            return View(m);
        }

        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).FirstOrDefaultAsync(ct);
        if (sede is null) { ModelState.AddModelError("", "Manca una sede attiva."); return View(m); }

        var adesso = DateTime.UtcNow;
        var idUtente = _db.IdUtenteCorrente;
        var codice = Pulisci(m.CodiceLotto);
        var quantita = m.Quantita!.Value;
        var unita = m.UnitaMisura!.Trim();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var accettazione = new Accettazione
        {
            IdInsediamento = sede.IdInsediamento, IdOrdineRiga = m.IdOrdineRiga, IdFornitore = m.IdFornitore.Value, IdArticolo = m.IdArticolo.Value,
            DataArrivo = m.DataArrivo, TipoDocumento = Pulisci(m.TipoDocumento), NumeroDocumento = Pulisci(m.NumeroDocumento), DataDocumento = m.DataDocumento,
            Vettore = Pulisci(m.Vettore), Quantita = quantita, UnitaMisura = unita, Giudizio = m.Giudizio, NoteReclami = Pulisci(m.NoteReclami),
            IdUtente = idUtente, DataCreazione = adesso
        };
        _db.Accettazioni.Add(accettazione);
        await _db.SaveChangesAsync(ct);

        if (!respinto && codice is not null)
        {
            var lotto = await _db.Lotti.FirstOrDefaultAsync(l => l.IdArticolo == m.IdArticolo && l.IdFornitore == m.IdFornitore
                                                               && l.Codice == codice && l.Tipo == TipoLotto.Ingresso, ct);
            if (lotto is null)
            {
                lotto = new Lotto
                {
                    IdArticolo = m.IdArticolo.Value, IdFornitore = m.IdFornitore, IdCategoria = m.IdCategoria, Codice = codice, Tipo = TipoLotto.Ingresso,
                    Stato = StatoLotto.Attivo, DataScadenza = m.DataScadenza, ETmc = m.ETmc, DataProduzioneFornitore = m.DataProduzione,
                    EnteBio = m.Bio ? Pulisci(m.EnteBio) : null, IdAccettazione = accettazione.IdAccettazione, DataCreazione = adesso
                };
                _db.Lotti.Add(lotto);
                await _db.SaveChangesAsync(ct);
            }
            accettazione.IdLotto = lotto.IdLotto;
            var quando = m.DataArrivo == DateOnly.FromDateTime(DateTime.Now) ? adesso : m.DataArrivo.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
            _db.MovimentiLotto.Add(new MovimentoLotto
            {
                IdInsediamento = sede.IdInsediamento, IdLotto = lotto.IdLotto, Tipo = TipoMovimento.Carico, Quantita = quantita, UnitaMisura = unita,
                DataMovimento = quando, IdAccettazione = accettazione.IdAccettazione, Nota = "Accettazione in entrata", IdUtente = idUtente, DataCreazione = adesso
            });
        }
        foreach (var c in m.Controlli.Where(c => c.Conforme is not null || c.Valore is not null))
            _db.AccettazioniControlli.Add(new AccettazioneControllo
            {
                IdAccettazione = accettazione.IdAccettazione, Controllo = c.Controllo, Conforme = c.Conforme, Valore = c.Valore
            });

        // L'ordine è ricevuto quando ogni sua riga ha almeno un'accettazione.
        if (m.IdOrdineRiga is { } riga)
        {
            await _db.SaveChangesAsync(ct);
            var idOrdine = await _db.OrdiniRighe.Where(r => r.IdOrdineRiga == riga).Select(r => r.IdOrdine).FirstAsync(ct);
            var righeAttive = await _db.OrdiniRighe.Where(r => r.IdOrdine == idOrdine && r.Attiva).Select(r => r.IdOrdineRiga).ToListAsync(ct);
            var accettate = await _db.Accettazioni.Where(a => a.IdOrdineRiga != null && righeAttive.Contains(a.IdOrdineRiga.Value))
                .Select(a => a.IdOrdineRiga!.Value).Distinct().ToListAsync(ct);
            if (righeAttive.All(accettate.Contains))
            {
                var ordine = await _db.Ordini.FirstAsync(o => o.IdOrdine == idOrdine, ct);
                if (ordine.Stato == StatoOrdine.Inviato) ordine.Stato = StatoOrdine.Ricevuto;
            }
        }
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = idUtente, IdInsediamento = sede.IdInsediamento, Azione = "AccettazioneRegistrata", Entita = "Accettazione",
            IdEntita = accettazione.IdAccettazione.ToString(), Dopo = $"{m.Articolo}, lotto {codice ?? "-"}, {quantita:0.##} {unita}, {m.Giudizio}",
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = adesso
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        TempData["Messaggio"] = respinto ? "Accettazione registrata: merce respinta (nessun carico in magazzino)." : $"Accettazione registrata: lotto {codice} caricato in magazzino.";
        return RedirectToAction(nameof(Etichetta), new { id = accettazione.IdAccettazione });
    }

    // ── Etichetta e scheda di accettazione ─────────────────────────
    [HttpGet("{id:int}/Etichetta")]
    public async Task<IActionResult> Etichetta(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var a = await _db.Accettazioni.AsNoTracking().FirstOrDefaultAsync(x => x.IdAccettazione == id, ct);
        if (a is null) return NotFound();
        var m = await IntestazioneAsync(a.IdInsediamento, a.IdFornitore, a.IdArticolo, null, ct);
        m.IdAccettazione = id;
        m.Accettata = a.DataCreazione; m.TipoDocumento = a.TipoDocumento; m.DataDocumento = a.DataDocumento; m.NumeroDocumento = a.NumeroDocumento;
        m.Quantita = a.Quantita; m.UnitaMisura = a.UnitaMisura; m.Giudizio = a.Giudizio; m.Note = a.NoteReclami;
        m.NumeroOrdine = await (from r in _db.OrdiniRighe.AsNoTracking() join o in _db.Ordini.AsNoTracking() on r.IdOrdine equals o.IdOrdine
                                where r.IdOrdineRiga == a.IdOrdineRiga select o.Numero).FirstOrDefaultAsync(ct);
        if (a.IdLotto is { } il)
        {
            var l = await _db.Lotti.AsNoTracking().FirstAsync(x => x.IdLotto == il, ct);
            m.Lotto = l.Codice; m.DataScadenza = l.DataScadenza; m.ETmc = l.ETmc; m.DataProduzione = l.DataProduzioneFornitore; m.EnteBio = l.EnteBio;
            m.UrlLotto = $"{Servizi.UrlPubblico.Base(HttpContext)}/Lotti/{l.IdLotto}";
            if (l.IdCategoria is { } ic) m.Matrice = await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.IdCategoria == ic).Select(c => c.Nome).FirstOrDefaultAsync(ct);
        }
        var controlli = await _db.AccettazioniControlli.AsNoTracking().Where(c => c.IdAccettazione == id).ToListAsync(ct);
        m.Voci = controlli.Select(c => new VoceEtichetta
        {
            Gruppo = NomiControlli.Gruppo(c.Controllo), Nome = NomiControlli.Nome(c.Controllo),
            Esito = string.Join(" · ", new[] { c.Conforme is true ? "conforme" : c.Conforme is false ? "NON conforme" : null, c.Valore }.Where(t => !string.IsNullOrWhiteSpace(t)))
        }).OrderBy(v => v.Gruppo).ToList();
        return View(m);
    }

    /// <summary>La scheda di accettazione con le caselle vuote, da stampare prima dell'arrivo e compilare a mano.</summary>
    [HttpGet("SchedaVuota")]
    public async Task<IActionResult> SchedaVuota(int idOrdineRiga, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var x = await (from r in _db.OrdiniRighe.AsNoTracking() join o in _db.Ordini.AsNoTracking() on r.IdOrdine equals o.IdOrdine
                       where r.IdOrdineRiga == idOrdineRiga select new { r, o }).FirstOrDefaultAsync(ct);
        if (x is null) return NotFound();
        var m = await IntestazioneAsync(x.o.IdInsediamento, x.o.IdFornitore, x.r.IdArticolo, x.r.IdCategoria, ct);
        m.Vuota = true; m.NumeroOrdine = x.o.Numero; m.Quantita = x.r.Quantita; m.UnitaMisura = x.r.UnitaMisura;
        var tmp = new ModelloNuovaAccettazione { IdCategoria = x.r.IdCategoria ?? await _db.Articoli.AsNoTracking().Where(a => a.IdArticolo == x.r.IdArticolo).Select(a => a.IdCategoria).FirstOrDefaultAsync(ct) };
        await CaricaControlliAsync(tmp, ct);
        m.Voci = tmp.Controlli.Select(c => new VoceEtichetta { Gruppo = NomiControlli.Gruppo(c.Controllo), Nome = c.Nome, Esito = "" }).OrderBy(v => v.Gruppo).ToList();
        return View("Etichetta", m);
    }

    // ── Correzione di un'accettazione già registrata ───────────────
    // Nulla si sovrascrive di nascosto: la quantità cambia con un movimento di rettifica e ogni modifica finisce nel registro attività.
    [HttpGet("{id:int}/Correggi")]
    public async Task<IActionResult> Correggi(int id, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var a = await _db.Accettazioni.AsNoTracking().Include(x => x.Controlli).FirstOrDefaultAsync(x => x.IdAccettazione == id, ct);
        if (a is null) return NotFound();
        var m = new ModelloNuovaAccettazione
        {
            IdAccettazione = id, IdOrdineRiga = a.IdOrdineRiga, IdFornitore = a.IdFornitore, IdArticolo = a.IdArticolo, DataArrivo = a.DataArrivo,
            TipoDocumento = a.TipoDocumento, NumeroDocumento = a.NumeroDocumento, DataDocumento = a.DataDocumento, Vettore = a.Vettore,
            Quantita = a.Quantita, UnitaMisura = a.UnitaMisura, Giudizio = a.Giudizio, NoteReclami = a.NoteReclami, Respinta = a.Giudizio == GiudizioAccettazione.Respinto
        };
        if (a.IdLotto is { } il)
        {
            var l = await _db.Lotti.AsNoTracking().FirstAsync(x => x.IdLotto == il, ct);
            m.CodiceLotto = l.Codice; m.DataProduzione = l.DataProduzioneFornitore; m.DataScadenza = l.DataScadenza; m.ETmc = l.ETmc;
            m.EnteBio = l.EnteBio; m.IdCategoria = l.IdCategoria;
            if (await _db.Accettazioni.AnyAsync(x => x.IdLotto == il && x.IdAccettazione != id, ct))
            {
                m.CodiceBloccato = true;
                m.MotivoCodiceBloccato = "Questo lotto è condiviso con altre accettazioni: il codice non si cambia da qui.";
            }
        }
        await CompletaAsync(m, ct);
        await CaricaControlliAsync(m, ct);
        // Si aggiungono i controlli già registrati che non sono più nel profilo, e si riportano i valori inseriti.
        foreach (var c in a.Controlli.Where(c => m.Controlli.All(x => x.Controllo != c.Controllo)))
            m.Controlli.Add(new ControlloInput { Controllo = c.Controllo, Nome = NomiControlli.Nome(c.Controllo) });
        foreach (var c in m.Controlli)
        {
            var e = a.Controlli.FirstOrDefault(x => x.Controllo == c.Controllo);
            if (e != null) { c.Conforme = e.Conforme; c.Valore = e.Valore; }
        }
        return View(m);
    }

    [HttpPost("{id:int}/Correggi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Correggi(int id, ModelloNuovaAccettazione m, CancellationToken ct)
    {
        if (EGestore) return SoloAzienda();
        var a = await _db.Accettazioni.Include(x => x.Controlli).FirstOrDefaultAsync(x => x.IdAccettazione == id, ct);
        if (a is null) return NotFound();
        var inseriti = m.Controlli;
        m.IdAccettazione = id; m.IdFornitore = a.IdFornitore; m.IdArticolo = a.IdArticolo; m.IdOrdineRiga = a.IdOrdineRiga;
        var categoriaScelta = m.IdCategoria;
        var lotto = a.IdLotto is { } il ? await _db.Lotti.FirstOrDefaultAsync(x => x.IdLotto == il, ct) : null;
        m.Respinta = a.Giudizio == GiudizioAccettazione.Respinto;
        if (lotto != null && await _db.Accettazioni.AnyAsync(x => x.IdLotto == lotto.IdLotto && x.IdAccettazione != id, ct))
        {
            m.CodiceBloccato = true;
            m.MotivoCodiceBloccato = "Questo lotto è condiviso con altre accettazioni: il codice non si cambia da qui.";
        }
        await CompletaAsync(m, ct);
        m.IdCategoria = categoriaScelta ?? m.IdCategoria;
        await CaricaControlliAsync(m, ct);
        foreach (var c in inseriti.Where(c => m.Controlli.All(x => x.Controllo != c.Controllo)))
            m.Controlli.Add(new ControlloInput { Controllo = c.Controllo, Nome = NomiControlli.Nome(c.Controllo) });
        foreach (var c in m.Controlli)
        {
            var i = inseriti.FirstOrDefault(x => x.Controllo == c.Controllo);
            if (i != null) { c.Conforme = i.Conforme; c.Valore = Pulisci(i.Valore); }
        }

        var errori = new List<string>();
        var respinto = m.Giudizio == GiudizioAccettazione.Respinto;
        if ((a.Giudizio == GiudizioAccettazione.Respinto) != respinto)
            errori.Add("Non si passa da «respinta» ad accettata (o al contrario) con una correzione: registra una nuova accettazione.");
        if (m.Quantita is not > 0) errori.Add("Scrivi la quantità arrivata (maggiore di zero).");
        if (string.IsNullOrWhiteSpace(m.UnitaMisura)) errori.Add("Scrivi l'unità di misura.");
        if (lotto != null && string.IsNullOrWhiteSpace(m.CodiceLotto)) errori.Add("Scrivi il lotto del fornitore.");
        if (m.DataArrivo > DateOnly.FromDateTime(DateTime.Now).AddDays(1)) errori.Add("La data di arrivo non può essere nel futuro.");

        var nuovoCodice = Pulisci(m.CodiceLotto);
        var cambiaCodice = lotto != null && nuovoCodice != null && nuovoCodice != lotto.Codice;
        if (cambiaCodice)
        {
            if (m.CodiceBloccato) errori.Add(m.MotivoCodiceBloccato!);
            else if (await _db.Lotti.AnyAsync(l => l.IdLotto != lotto!.IdLotto && l.IdArticolo == lotto.IdArticolo && l.IdFornitore == lotto.IdFornitore
                                                 && l.Codice == nuovoCodice && l.Tipo == TipoLotto.Ingresso, ct))
                errori.Add($"Esiste già un lotto «{nuovoCodice}» per questo articolo e questo fornitore.");
        }
        decimal delta = 0m;
        if (lotto != null && m.Quantita is > 0)
        {
            delta = m.Quantita.Value - a.Quantita;
            if (delta != 0m)
            {
                var giacenza = await _db.MovimentiLotto.Where(mv => mv.IdLotto == lotto.IdLotto).SumAsync(mv => (decimal?)mv.Quantita, ct) ?? 0m;
                if (giacenza + delta < 0m)
                    errori.Add($"Del lotto sono già stati usati o spediti {a.Quantita - giacenza:0.##}: non puoi scendere sotto questa quantità.");
            }
        }
        if (errori.Count > 0)
        {
            foreach (var e in errori) ModelState.AddModelError("", e);
            return View(m);
        }

        var cambi = new List<string>();
        void Nota(string campo, object? prima, object? dopo)
        {
            if (!Equals(prima, dopo)) cambi.Add($"{campo}: {prima ?? "-"} → {dopo ?? "-"}");
        }
        var adesso = DateTime.UtcNow;
        var idUtente = _db.IdUtenteCorrente;
        Nota("data arrivo", a.DataArrivo, m.DataArrivo);
        Nota("tipo documento", a.TipoDocumento, Pulisci(m.TipoDocumento));
        Nota("n. documento", a.NumeroDocumento, Pulisci(m.NumeroDocumento));
        Nota("data documento", a.DataDocumento, m.DataDocumento);
        Nota("vettore", a.Vettore, Pulisci(m.Vettore));
        Nota("quantità", a.Quantita, m.Quantita);
        Nota("unità", a.UnitaMisura, m.UnitaMisura!.Trim());
        Nota("giudizio", a.Giudizio, m.Giudizio);
        Nota("note", a.NoteReclami, Pulisci(m.NoteReclami));
        a.DataArrivo = m.DataArrivo; a.TipoDocumento = Pulisci(m.TipoDocumento); a.NumeroDocumento = Pulisci(m.NumeroDocumento);
        a.DataDocumento = m.DataDocumento; a.Vettore = Pulisci(m.Vettore); a.Quantita = m.Quantita!.Value; a.UnitaMisura = m.UnitaMisura!.Trim();
        a.Giudizio = m.Giudizio; a.NoteReclami = Pulisci(m.NoteReclami);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        if (lotto != null)
        {
            Nota("lotto", lotto.Codice, nuovoCodice);
            Nota("scadenza", lotto.DataScadenza, m.DataScadenza);
            Nota("TMC", lotto.ETmc, m.ETmc);
            Nota("data produzione", lotto.DataProduzioneFornitore, m.DataProduzione);
            Nota("ente BIO", lotto.EnteBio, m.Bio ? Pulisci(m.EnteBio) : lotto.EnteBio);
            Nota("matrice", lotto.IdCategoria, m.IdCategoria);
            if (nuovoCodice != null) lotto.Codice = nuovoCodice;
            lotto.DataScadenza = m.DataScadenza; lotto.ETmc = m.ETmc; lotto.DataProduzioneFornitore = m.DataProduzione;
            if (m.Bio) lotto.EnteBio = Pulisci(m.EnteBio);
            lotto.IdCategoria = m.IdCategoria;
            if (delta != 0m)
                _db.MovimentiLotto.Add(new MovimentoLotto
                {
                    IdInsediamento = a.IdInsediamento, IdLotto = lotto.IdLotto, Tipo = TipoMovimento.Rettifica, Quantita = delta, UnitaMisura = a.UnitaMisura,
                    DataMovimento = adesso, IdAccettazione = id, Nota = $"Correzione dell'accettazione n. {id}: quantità da {a.Quantita - delta:0.##} a {a.Quantita:0.##}",
                    IdUtente = idUtente, DataCreazione = adesso
                });
        }
        foreach (var c in m.Controlli)
        {
            var e = a.Controlli.FirstOrDefault(x => x.Controllo == c.Controllo);
            if (e is null)
            {
                if (c.Conforme is null && c.Valore is null) continue;
                cambi.Add($"controllo {NomiControlli.Nome(c.Controllo)} aggiunto");
                _db.AccettazioniControlli.Add(new AccettazioneControllo { IdAccettazione = id, Controllo = c.Controllo, Conforme = c.Conforme, Valore = c.Valore });
            }
            else if (e.Conforme != c.Conforme || e.Valore != c.Valore)
            {
                cambi.Add($"controllo {NomiControlli.Nome(c.Controllo)}: {(e.Conforme is true ? "conforme" : e.Conforme is false ? "non conforme" : "-")} {e.Valore} → {(c.Conforme is true ? "conforme" : c.Conforme is false ? "non conforme" : "-")} {c.Valore}");
                e.Conforme = c.Conforme; e.Valore = c.Valore;
            }
        }
        if (cambi.Count == 0)
        {
            await tx.RollbackAsync(ct);
            TempData["Messaggio"] = "Nessuna modifica da salvare.";
            return RedirectToAction(nameof(Etichetta), new { id });
        }
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = idUtente, IdInsediamento = a.IdInsediamento, Azione = "AccettazioneCorretta", Entita = "Accettazione", IdEntita = id.ToString(),
            Dopo = string.Join("; ", cambi), IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = adesso
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Messaggio"] = $"Accettazione corretta ({cambi.Count} {(cambi.Count == 1 ? "modifica" : "modifiche")}).";
        return RedirectToAction(nameof(Etichetta), new { id });
    }

    // ── Supporto ───────────────────────────────────────────────────
    private async Task<ModelloEtichetta> IntestazioneAsync(int idSede, int idFornitore, int idArticolo, int? idCategoria, CancellationToken ct)
    {
        var az = await _db.Aziende.AsNoTracking().FirstOrDefaultAsync(ct);
        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.IdInsediamento == idSede).Select(i => i.Nome).FirstOrDefaultAsync(ct);
        var f = await _db.Fornitori.AsNoTracking().Where(x => x.IdFornitore == idFornitore).Select(x => x.RagioneSociale).FirstOrDefaultAsync(ct);
        var ar = await _db.Articoli.AsNoTracking().Where(x => x.IdArticolo == idArticolo).Select(x => new { x.Descrizione, x.IdCategoria }).FirstAsync(ct);
        var cat = idCategoria ?? ar.IdCategoria;
        var matrice = cat is { } c ? await _db.CategorieMateriaPrima.AsNoTracking().Where(x => x.IdCategoria == c).Select(x => x.Nome).FirstOrDefaultAsync(ct) : null;
        return new ModelloEtichetta { Azienda = az?.RagioneSociale ?? "", Sede = sede ?? "", Fornitore = f ?? "", Articolo = ar.Descrizione, Matrice = matrice };
    }

    private async Task CompletaAsync(ModelloNuovaAccettazione m, CancellationToken ct)
    {
        m.Fornitore = await _db.Fornitori.AsNoTracking().Where(f => f.IdFornitore == m.IdFornitore).Select(f => f.RagioneSociale).FirstOrDefaultAsync(ct) ?? "";
        var a = await _db.Articoli.AsNoTracking().Where(x => x.IdArticolo == m.IdArticolo).FirstAsync(ct);
        m.Articolo = a.Descrizione; m.Bio = a.Bio;
        m.IdCategoria ??= a.IdCategoria;
        if (string.IsNullOrWhiteSpace(m.UnitaMisura)) m.UnitaMisura = a.UnitaMisura;
        m.Matrice = m.IdCategoria is { } c ? await _db.CategorieMateriaPrima.AsNoTracking().Where(x => x.IdCategoria == c).Select(x => x.Nome).FirstOrDefaultAsync(ct) : null;
        m.Categorie = await ElencoCategorieAsync(ct);
    }

    private async Task<List<OpzioneAnagrafica>> ElencoCategorieAsync(CancellationToken ct) =>
        await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome)
            .Select(c => new OpzioneAnagrafica { Id = c.IdCategoria, Nome = c.Nome }).ToListAsync(ct);

    /// <summary>I controlli da fare dipendono dal profilo della matrice. Senza profilo si propongono i tre di base, non obbligatori.</summary>
    private async Task CaricaControlliAsync(ModelloNuovaAccettazione m, CancellationToken ct)
    {
        m.Controlli = new List<ControlloInput>();
        var idProfilo = m.IdCategoria is { } c
            ? await _db.CategorieMateriaPrima.AsNoTracking().Where(x => x.IdCategoria == c).Select(x => x.IdProfiloControllo).FirstOrDefaultAsync(ct)
            : null;
        if (idProfilo is { } p)
        {
            var righe = await _db.ProfiliControlloRighe.AsNoTracking().Where(r => r.IdProfiloControllo == p && r.Attivo).OrderBy(r => r.Controllo).ToListAsync(ct);
            m.Controlli = righe.Select(r => new ControlloInput { Controllo = r.Controllo, Nome = NomiControlli.Nome(r.Controllo), Obbligatorio = r.Obbligatorio, Istruzione = r.Istruzione }).ToList();
        }
        if (m.Controlli.Count == 0)
            m.Controlli = new[] { TipoControllo.Etichettatura, TipoControllo.Confezione, TipoControllo.Temperatura }
                .Select(t => new ControlloInput { Controllo = t, Nome = NomiControlli.Nome(t) }).ToList();
    }
}
