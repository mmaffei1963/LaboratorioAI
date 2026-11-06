using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Ricette dell'azienda e ricerca dei lotti da usare in un certo giorno.</summary>
[Authorize]
[Route("Ricette")]
public class RicetteController : Controller
{
    private readonly ContestoTracciabilita _db;

    public RicetteController(ContestoTracciabilita db) => _db = db;

    private bool EGestore => User.HasClaim("tr:gestore", "true");

    [HttpGet("")]
    public async Task<IActionResult> Index(bool tutte, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var m = new ModelloElencoRicette { MostraTutte = tutte };
        var ricette = await _db.Ricette.AsNoTracking().Where(r => tutte || r.Attiva).OrderByDescending(r => r.Attiva).ThenBy(r => r.Nome).ToListAsync(ct);
        m.NonInUso = await _db.Ricette.AsNoTracking().CountAsync(r => !r.Attiva, ct);
        foreach (var r in ricette)
        {
            var n = await (from v in _db.RicetteVersioni.AsNoTracking()
                           join g in _db.RicetteRighe.AsNoTracking() on v.IdRicettaVersione equals g.IdRicettaVersione
                           where v.IdRicetta == r.IdRicetta && v.ValidaAl == null && g.Attiva
                           select g.IdRicettaRiga).CountAsync(ct);
            m.Ricette.Add(new RigaRicettaElenco { IdRicetta = r.IdRicetta, Nome = r.Nome, Ingredienti = n, Attiva = r.Attiva });
        }
        return View(m);
    }

    [HttpPost("Nuova")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Nuova(ModelloElencoRicette modello, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var nome = modello.NuovoNome?.Trim();
        if (string.IsNullOrEmpty(nome))
        {
            TempData["Errore"] = "Scrivi il nome del prodotto.";
            return RedirectToAction(nameof(Index));
        }
        if (await _db.Ricette.AnyAsync(r => r.Nome == nome, ct))
        {
            TempData["Errore"] = "Esiste già una ricetta con questo nome.";
            return RedirectToAction(nameof(Index));
        }
        // Il prodotto finito è un articolo dell'azienda: lo creiamo con lo stesso nome della ricetta.
        var articolo = new Articolo { Descrizione = nome, Tipo = TipoArticolo.ProdottoFinito, UnitaMisura = "kg" };
        _db.Articoli.Add(articolo);
        await _db.SaveChangesAsync(ct);
        var ricetta = new Ricetta { IdArticolo = articolo.IdArticolo, Nome = nome };
        ricetta.Versioni.Add(new RicettaVersione { Numero = 1, ValidaDal = DateOnly.FromDateTime(DateTime.UtcNow) });
        _db.Ricette.Add(ricetta);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Scheda), new { id = ricetta.IdRicetta });
    }

    /// <summary>Mette una ricetta fuori uso (non compare più nell'elenco) o la rimette in uso. Non si cancella: le produzioni vecchie la conservano.</summary>
    [HttpPost("{id:int}/Stato")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Stato(int id, bool attiva, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var ricetta = await _db.Ricette.FirstOrDefaultAsync(r => r.IdRicetta == id, ct);
        if (ricetta is null) return NotFound();
        if (ricetta.Attiva != attiva)
        {
            var prima = ricetta.Attiva;
            ricetta.Attiva = attiva;
            _db.Audit.Add(new VoceAudit
            {
                IdUtente = _db.IdUtenteCorrente, Azione = attiva ? "RicettaRimessaInUso" : "RicettaMessaFuoriUso", Entita = "Ricetta", IdEntita = id.ToString(),
                Prima = $"{ricetta.Nome}; in uso={prima}", Dopo = $"{ricetta.Nome}; in uso={attiva}",
                IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }
        if (!attiva)
        {
            TempData["Messaggio"] = $"Ricetta «{ricetta.Nome}» messa fuori uso: non compare più nell'elenco. Per rivederla usa «Mostra anche le ricette fuori uso».";
            return RedirectToAction(nameof(Index));
        }
        TempData["Messaggio"] = $"Ricetta «{ricetta.Nome}» rimessa in uso.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    /// <summary>Copia una ricetta (con gli ingredienti attivi) in una nuova, con un altro nome e un nuovo prodotto finito.</summary>
    [HttpPost("{id:int}/Duplica")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplica(int id, string? nuovoNome, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var origine = await _db.Ricette.AsNoTracking().FirstOrDefaultAsync(r => r.IdRicetta == id, ct);
        if (origine is null) return NotFound();
        var nome = nuovoNome?.Trim();
        if (string.IsNullOrEmpty(nome))
        {
            TempData["Errore"] = "Scrivi il nome della nuova ricetta.";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        if (await _db.Ricette.AnyAsync(r => r.Nome == nome, ct))
        {
            TempData["Errore"] = "Esiste già una ricetta con questo nome.";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        var versioneOrigine = await _db.RicetteVersioni.AsNoTracking()
            .Where(v => v.IdRicetta == id && v.ValidaAl == null).OrderByDescending(v => v.Numero).FirstOrDefaultAsync(ct);
        var righe = versioneOrigine is null ? new List<RicettaRiga>()
            : await _db.RicetteRighe.AsNoTracking().Where(g => g.IdRicettaVersione == versioneOrigine.IdRicettaVersione && g.Attiva).OrderBy(g => g.Ordine).ToListAsync(ct);
        var idRighe = righe.Select(g => g.IdRicettaRiga).ToList();
        var extra = await _db.RicetteRigheCategorie.AsNoTracking().Where(e => idRighe.Contains(e.IdRicettaRiga)).ToListAsync(ct);
        var bio = await _db.Articoli.AsNoTracking().Where(a => a.IdArticolo == origine.IdArticolo).Select(a => a.Bio).FirstOrDefaultAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var articolo = new Articolo { Descrizione = nome, Tipo = TipoArticolo.ProdottoFinito, UnitaMisura = "kg", Bio = bio };
        _db.Articoli.Add(articolo);
        await _db.SaveChangesAsync(ct);
        var ricetta = new Ricetta { IdArticolo = articolo.IdArticolo, Nome = nome };
        var versione = new RicettaVersione
        {
            Numero = 1, ValidaDal = DateOnly.FromDateTime(DateTime.UtcNow),
            QuantitaProdottaTeorica = versioneOrigine?.QuantitaProdottaTeorica, Note = versioneOrigine?.Note
        };
        var copie = new List<(RicettaRiga Vecchia, RicettaRiga Nuova)>();
        foreach (var g in righe)
        {
            var nuova = new RicettaRiga
            {
                IdCategoria = g.IdCategoria, IdArticoloSemilavorato = g.IdArticoloSemilavorato, QuantitaTeorica = g.QuantitaTeorica,
                UnitaMisura = g.UnitaMisura, Ordine = g.Ordine, Note = g.Note, Etichetta = g.Etichetta, LottiPerFornitore = g.LottiPerFornitore
            };
            versione.Righe.Add(nuova);
            copie.Add((g, nuova));
        }
        ricetta.Versioni.Add(versione);
        _db.Ricette.Add(ricetta);
        await _db.SaveChangesAsync(ct);
        foreach (var (vecchia, nuova) in copie)
            foreach (var e in extra.Where(x => x.IdRicettaRiga == vecchia.IdRicettaRiga))
                _db.RicetteRigheCategorie.Add(new RicettaRigaCategoria { IdRicettaRiga = nuova.IdRicettaRiga, IdCategoria = e.IdCategoria });
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = _db.IdUtenteCorrente, Azione = "RicettaDuplicata", Entita = "Ricetta", IdEntita = ricetta.IdRicetta.ToString(),
            Prima = origine.Nome, Dopo = $"{nome}; ingredienti copiati: {righe.Count}", IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        TempData["Messaggio"] = $"Ricetta «{nome}» creata copiando «{origine.Nome}» ({righe.Count} ingredienti). Controlla gli ingredienti e correggi le differenze.";
        return RedirectToAction(nameof(Scheda), new { id = ricetta.IdRicetta });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Scheda(int id, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var m = await CostruisciScheda(id, ct);
        return m is null ? NotFound() : View(m);
    }

    [HttpPost("{id:int}/Ingrediente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AggiungiIngrediente(int id, ModelloSchedaRicetta modello, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var ricetta = await _db.Ricette.FirstOrDefaultAsync(r => r.IdRicetta == id, ct);
        if (ricetta is null) return NotFound();
        var scelte = modello.CategorieScelte.Distinct().ToList();
        var etichetta = modello.Etichetta?.Trim();
        if (scelte.Count == 0 || string.IsNullOrEmpty(etichetta))
        {
            TempData["Errore"] = "Scrivi il nome dell'ingrediente e scegli almeno una categoria.";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        var valide = await _db.CategorieMateriaPrima.Where(c => scelte.Contains(c.IdCategoria)).Select(c => c.IdCategoria).ToListAsync(ct);
        if (valide.Count != scelte.Count)
        {
            TempData["Errore"] = "Una delle categorie scelte non esiste.";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        await CreaRigaAsync(id, etichetta, modello.LottiPerFornitore, modello.QuantitaIngrediente, valide, null, ct);
        TempData["Messaggio"] = $"Ingrediente «{etichetta}» aggiunto.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    [HttpGet("{id:int}/Ingrediente/{idRiga:int}/Modifica")]
    public async Task<IActionResult> Modifica(int id, int idRiga, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var m = await CostruisciScheda(id, ct);
        var riga = await RigaAttivaAsync(id, idRiga, ct);
        if (m is null || riga is null) return NotFound();
        m.IdRicettaRiga = idRiga;
        m.Etichetta = riga.Etichetta;
        m.LottiPerFornitore = riga.LottiPerFornitore;
        m.QuantitaIngrediente = riga.QuantitaTeorica;
        m.CategorieScelte = await CategorieDellaRigaAsync(riga, ct);
        return View(m);
    }

    // Modificare = sostituire: la riga vecchia viene disattivata e ne nasce una nuova al suo posto.
    [HttpPost("{id:int}/Ingrediente/{idRiga:int}/Modifica")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Modifica(int id, int idRiga, ModelloSchedaRicetta modello, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var riga = await RigaAttivaAsync(id, idRiga, ct);
        if (riga is null) return NotFound();
        var scelte = modello.CategorieScelte.Distinct().ToList();
        var etichetta = modello.Etichetta?.Trim();
        var valide = await _db.CategorieMateriaPrima.Where(c => scelte.Contains(c.IdCategoria)).Select(c => c.IdCategoria).ToListAsync(ct);
        if (string.IsNullOrEmpty(etichetta) || valide.Count == 0 || valide.Count != scelte.Count)
        {
            TempData["Errore"] = "Scrivi il nome dell'ingrediente e scegli almeno una categoria.";
            return RedirectToAction(nameof(Modifica), new { id, idRiga });
        }
        await CreaRigaAsync(id, etichetta, modello.LottiPerFornitore, modello.QuantitaIngrediente, valide, riga, ct);
        TempData["Messaggio"] = $"Ingrediente «{etichetta}» modificato.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    [HttpPost("{id:int}/Riferimento")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Riferimento(int id, decimal? quantitaRiferimento, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var versione = await _db.RicetteVersioni.Where(v => v.IdRicetta == id && v.ValidaAl == null).OrderByDescending(v => v.Numero).FirstOrDefaultAsync(ct);
        if (versione is null) return NotFound();
        if (quantitaRiferimento is <= 0)
        {
            TempData["Errore"] = "I kg di riferimento devono essere maggiori di zero (oppure lasciali vuoti).";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        versione.QuantitaProdottaTeorica = quantitaRiferimento;
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = "Quantità di riferimento della ricetta salvata.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    [HttpPost("{id:int}/Ingrediente/{idRiga:int}/Rimuovi")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rimuovi(int id, int idRiga, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var riga = await RigaAttivaAsync(id, idRiga, ct);
        if (riga is null) return NotFound();
        riga.Attiva = false;
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = $"Ingrediente «{riga.Etichetta}» tolto dalla ricetta.";
        return RedirectToAction(nameof(Scheda), new { id });
    }

    private async Task<RicettaRiga?> RigaAttivaAsync(int idRicetta, int idRiga, CancellationToken ct) =>
        await (from v in _db.RicetteVersioni
               join g in _db.RicetteRighe on v.IdRicettaVersione equals g.IdRicettaVersione
               where v.IdRicetta == idRicetta && g.IdRicettaRiga == idRiga && g.Attiva
               select g).FirstOrDefaultAsync(ct);

    private async Task<List<int>> CategorieDellaRigaAsync(RicettaRiga riga, CancellationToken ct)
    {
        var l = new List<int>();
        if (riga.IdCategoria is { } c) l.Add(c);
        l.AddRange(await _db.RicetteRigheCategorie.AsNoTracking().Where(e => e.IdRicettaRiga == riga.IdRicettaRiga).Select(e => e.IdCategoria).ToListAsync(ct));
        return l;
    }

    /// <summary>Crea una riga attiva nella versione corrente; se <paramref name="sostituisce"/> è indicata la disattiva e ne prende il posto.</summary>
    private async Task CreaRigaAsync(int idRicetta, string etichetta, int lottiPerFornitore, decimal? quantita, List<int> categorie, RicettaRiga? sostituisce, CancellationToken ct)
    {
        var versione = await _db.RicetteVersioni.Where(v => v.IdRicetta == idRicetta && v.ValidaAl == null)
            .OrderByDescending(v => v.Numero).FirstAsync(ct);
        int ordine;
        if (sostituisce != null)
        {
            ordine = sostituisce.Ordine;
            sostituisce.Attiva = false;
        }
        else
        {
            ordine = (await _db.RicetteRighe.Where(g => g.IdRicettaVersione == versione.IdRicettaVersione).MaxAsync(g => (int?)g.Ordine, ct) ?? 0) + 1;
        }
        var riga = new RicettaRiga
        {
            IdRicettaVersione = versione.IdRicettaVersione, IdCategoria = categorie[0], Ordine = ordine,
            Etichetta = etichetta, LottiPerFornitore = Math.Clamp(lottiPerFornitore, 1, 10),
            QuantitaTeorica = quantita is > 0 ? quantita : null, UnitaMisura = quantita is > 0 ? "kg" : null
        };
        _db.RicetteRighe.Add(riga);
        await _db.SaveChangesAsync(ct);
        foreach (var c in categorie.Skip(1))
            _db.RicetteRigheCategorie.Add(new RicettaRigaCategoria { IdRicettaRiga = riga.IdRicettaRiga, IdCategoria = c });
        await _db.SaveChangesAsync(ct);
    }

    [HttpGet("{id:int}/Proposta")]
    public async Task<IActionResult> Proposta(int id, DateOnly? data, int[]? escludi, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var ricetta = await _db.Ricette.AsNoTracking().FirstOrDefaultAsync(r => r.IdRicetta == id, ct);
        if (ricetta is null) return NotFound();
        var giorno = data ?? DateOnly.FromDateTime(DateTime.Now);
        var esclusi = (escludi ?? Array.Empty<int>()).ToList();
        var scheda = await CostruisciScheda(id, ct);

        var righe = await (from v in _db.RicetteVersioni.AsNoTracking()
                           join g in _db.RicetteRighe.AsNoTracking() on v.IdRicettaVersione equals g.IdRicettaVersione
                           where v.IdRicetta == id && v.ValidaAl == null && g.Attiva && g.IdCategoria != null
                           orderby g.Ordine
                           select g).ToListAsync(ct);
        var extra = await _db.RicetteRigheCategorie.AsNoTracking().ToListAsync(ct);

        var imp = await _db.ImpostazioniAziende.AsNoTracking().FirstOrDefaultAsync(ct);
        var m = new ModelloProposta
        {
            IdRicetta = id, Nome = ricetta.Nome, Giorno = giorno, Esclusi = esclusi,
            ModoConsumi = imp?.ModoQuantitaConsumi ?? ModoQuantitaConsumi.SoloLotti, ConsumiObbligatori = imp?.QuantitaConsumiObbligatorie ?? false,
            QuantitaRiferimento = scheda?.QuantitaRiferimento
        };
        var vistiFornitori = new Dictionary<int, string>();
        foreach (var r in righe)
        {
            var categorie = new List<int> { r.IdCategoria!.Value };
            categorie.AddRange(extra.Where(e => e.IdRicettaRiga == r.IdRicettaRiga).Select(e => e.IdCategoria));
            var candidati = await CandidatiAsync(categorie, ct);
            var trovati = RicercaLottiPerData.Cerca(candidati, categorie, giorno, r.LottiPerFornitore, fornitoriEsclusi: esclusi);
            foreach (var f in trovati) vistiFornitori[f.IdFornitore] = f.Fornitore;
            m.Ingredienti.Add(new IngredienteProposto
            {
                Nome = r.Etichetta ?? "Ingrediente",
                QuantitaTeorica = r.QuantitaTeorica,
                LottiPerFornitore = r.LottiPerFornitore,
                Fornitori = trovati,
                Scartati = RicercaLottiPerData.FornitoriScartati(candidati, categorie, giorno).ToList()
            });
        }
        await ImpostaLottoInternoAsync(m, ricetta.IdArticolo, giorno, ct);
        // Per ricalcolare con un'esclusione serve poter rivedere anche i fornitori già esclusi.
        foreach (var e in esclusi.Where(e => !vistiFornitori.ContainsKey(e)))
        {
            var nome = await _db.Fornitori.AsNoTracking().Where(f => f.IdFornitore == e).Select(f => f.RagioneSociale).FirstOrDefaultAsync(ct);
            if (nome != null) vistiFornitori[e] = nome;
        }
        m.FornitoriPresenti = vistiFornitori.Select(k => (k.Key, k.Value)).OrderBy(x => x.Value).ToList();
        return View(m);
    }

    /// <summary>
    /// Il lotto interno del giorno secondo le impostazioni dell'azienda (giorno dell'anno, sigla sede, progressivo).
    /// Se questo prodotto è già stato prodotto quel giorno si riusa il suo lotto; altrimenti è il prossimo libero.
    /// </summary>
    private async Task<(Insediamento Sede, string Codice, int? IdLottoEsistente)?> LottoInternoAsync(int idArticolo, DateOnly giorno, CancellationToken ct)
    {
        var sede = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo).OrderBy(i => i.IdInsediamento).FirstOrDefaultAsync(ct);
        if (sede is null) return null;
        var imp = await _db.ImpostazioniAziende.AsNoTracking().FirstOrDefaultAsync(ct);
        var formato = imp?.FormatoLotto ?? FormatoLotto.GiornoDellAnno;
        var conSigla = imp?.IncludiSiglaSedeNelLotto ?? false;

        var delGiorno = await _db.Produzioni.AsNoTracking()
            .Where(p => p.IdInsediamento == sede.IdInsediamento && p.Giorno == giorno && !p.Annullata)
            .Select(p => new { p.IdArticolo, p.IdLottoProdotto }).ToListAsync(ct);
        var esistente = delGiorno.FirstOrDefault(p => p.IdArticolo == idArticolo);
        if (esistente != null)
        {
            var codice = await _db.Lotti.AsNoTracking().Where(l => l.IdLotto == esistente.IdLottoProdotto).Select(l => l.Codice).FirstAsync(ct);
            return (sede, codice, esistente.IdLottoProdotto);
        }
        var progressivo = (imp?.PiuLottiAlGiorno ?? false) ? delGiorno.Select(p => p.IdLottoProdotto).Distinct().Count() + 1 : 1;
        return (sede, GeneratoreCodiceLotto.Genera(formato, giorno, sede.Sigla, progressivo, conSigla), null);
    }

    private async Task ImpostaLottoInternoAsync(ModelloProposta m, int idArticolo, DateOnly giorno, CancellationToken ct)
    {
        if (await LottoInternoAsync(idArticolo, giorno, ct) is not { } r) return;
        m.LottoInterno = r.Codice;
        m.LottoInternoEsistente = r.IdLottoEsistente != null;
    }

    /// <summary>I lotti proposti per un giorno, uno per ogni ingrediente attivo, con la riga a cui appartengono.</summary>
    private async Task<List<(RicettaRiga Riga, IReadOnlyList<LottiDelFornitore> Fornitori)>> ProposteAsync(
        int idRicetta, DateOnly giorno, IReadOnlyCollection<int> esclusi, CancellationToken ct)
    {
        var righe = await (from v in _db.RicetteVersioni.AsNoTracking()
                           join g in _db.RicetteRighe.AsNoTracking() on v.IdRicettaVersione equals g.IdRicettaVersione
                           where v.IdRicetta == idRicetta && v.ValidaAl == null && g.Attiva && g.IdCategoria != null
                           orderby g.Ordine
                           select g).ToListAsync(ct);
        var extra = await _db.RicetteRigheCategorie.AsNoTracking().ToListAsync(ct);
        var risultato = new List<(RicettaRiga, IReadOnlyList<LottiDelFornitore>)>();
        foreach (var r in righe)
        {
            var categorie = new List<int> { r.IdCategoria!.Value };
            categorie.AddRange(extra.Where(e => e.IdRicettaRiga == r.IdRicettaRiga).Select(e => e.IdCategoria));
            var candidati = await CandidatiAsync(categorie, ct);
            risultato.Add((r, RicercaLottiPerData.Cerca(candidati, categorie, giorno, r.LottiPerFornitore, fornitoriEsclusi: esclusi.ToList())));
        }
        return risultato;
    }

    /// <summary>
    /// Registra la produzione del giorno: crea (o riusa) il lotto del prodotto finito e scrive i lotti di ingresso usati.
    /// I lotti scelti devono essere tra quelli proposti. Se coincidono con la proposta il consumo è «Confermato», altrimenti «Modificato».
    /// </summary>
    [HttpPost("{id:int}/Produci")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Produci(int id, DateOnly data, int[]? escludi, string[]? lotti, decimal? quantita, string? note, CancellationToken ct)
    {
        if (EGestore) return View("~/Views/Lotti/SoloAzienda.cshtml");
        var ricetta = await _db.Ricette.AsNoTracking().FirstOrDefaultAsync(r => r.IdRicetta == id, ct);
        if (ricetta is null) return NotFound();
        if (!ricetta.Attiva)
        {
            TempData["Errore"] = "Questa ricetta è fuori uso: rimettila in uso dalla sua scheda per registrare una produzione.";
            return RedirectToAction(nameof(Scheda), new { id });
        }
        IActionResult Indietro(string errore)
        {
            TempData["Errore"] = errore;
            return RedirectToAction(nameof(Proposta), new { id, data = data.ToString("yyyy-MM-dd"), escludi });
        }
        if (data > DateOnly.FromDateTime(DateTime.Now).AddDays(1)) return Indietro("Il giorno di produzione non può essere nel futuro.");
        if (quantita is <= 0) return Indietro("La quantità prodotta deve essere maggiore di zero (oppure lasciala vuota).");

        var proposte = await ProposteAsync(id, data, escludi ?? Array.Empty<int>(), ct);
        var ammessi = new Dictionary<int, int>();   // IdLotto → IdCategoria con cui è stato proposto
        foreach (var (_, fornitori) in proposte)
            foreach (var l in fornitori.SelectMany(f => f.Lotti))
                ammessi.TryAdd(l.IdLotto, l.IdCategoria);

        var scelti = new List<(int IdLotto, int IdCategoria)>();
        foreach (var voce in lotti ?? Array.Empty<string>())
        {
            var parti = voce.Split(':');
            if (parti.Length != 2 || !int.TryParse(parti[0], out var idLotto) || !ammessi.TryGetValue(idLotto, out var cat))
                return Indietro("Uno dei lotti scelti non è tra quelli proposti: rifai la ricerca.");
            if (scelti.All(x => x.IdLotto != idLotto)) scelti.Add((idLotto, cat));
        }
        if (scelti.Count == 0) return Indietro("Scegli almeno un lotto da registrare.");
        var origine = scelti.Count == ammessi.Count ? OrigineConsumo.Confermato : OrigineConsumo.Modificato;

        // Kg consumati per lotto, secondo la modalità scelta dall'azienda.
        // I campi si chiamano kg[<idLotto>]: li leggo a mano (il binder automatico dei dizionari interpreta male gli altri campi del modulo).
        var kg = new Dictionary<int, decimal?>();
        foreach (var chiave in Request.Form.Keys.Where(k => k.StartsWith("kg[", StringComparison.Ordinal) && k.EndsWith(']')))
        {
            if (!int.TryParse(chiave.AsSpan(3, chiave.Length - 4), out var idLottoKg)) continue;
            var valoreTesto = Request.Form[chiave].ToString().Trim().Replace(',', '.');
            if (valoreTesto.Length == 0) continue;
            if (!decimal.TryParse(valoreTesto, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var valore))
                return Indietro("I kg usati devono essere numeri (esempio 12.5).");
            kg[idLottoKg] = valore;
        }
        if (kg.Values.Any(v => v is <= 0)) return Indietro("I kg usati devono essere maggiori di zero (oppure lasciali vuoti).");
        var imp = await _db.ImpostazioniAziende.AsNoTracking().FirstOrDefaultAsync(ct);
        var modo = imp?.ModoQuantitaConsumi ?? ModoQuantitaConsumi.SoloLotti;
        var versioneRif = await _db.RicetteVersioni.AsNoTracking().Where(v => v.IdRicetta == id && v.ValidaAl == null)
            .OrderByDescending(v => v.Numero).FirstAsync(ct);
        var assegnati = new HashSet<int>();
        var ingredienti = new List<IngredienteDaConsumare>();
        foreach (var (riga, fornitori) in proposte)
        {
            var suoi = fornitori.SelectMany(f => f.Lotti).Select(l => l.IdLotto).Distinct()
                .Where(l => scelti.Any(x => x.IdLotto == l) && assegnati.Add(l)).ToList();
            if (suoi.Count > 0) ingredienti.Add(new IngredienteDaConsumare(riga.IdRicettaRiga, riga.Etichetta ?? "Ingrediente", riga.QuantitaTeorica, suoi));
        }
        var consumi = CalcoloConsumi.Calcola(modo, imp?.QuantitaConsumiObbligatorie ?? false, quantita, versioneRif.QuantitaProdottaTeorica,
            ingredienti, kg);
        if (consumi.Errori.Count > 0) return Indietro(string.Join(" ", consumi.Errori));

        if (await LottoInternoAsync(ricetta.IdArticolo, data, ct) is not { } interno)
            return Indietro("Manca una sede attiva: impossibile registrare la produzione.");

        // Doppio clic: la stessa produzione registrata da pochi secondi non si ripete.
        var soglia = DateTime.UtcNow.AddMinutes(-2);
        if (await _db.Produzioni.AnyAsync(p => p.IdInsediamento == interno.Sede.IdInsediamento && p.IdArticolo == ricetta.IdArticolo
                                               && p.Giorno == data && !p.Annullata && p.DataOra > soglia, ct))
            return Indietro("Una produzione di questo prodotto è stata appena registrata. Se serve una seconda, attendi due minuti.");

        var versione = versioneRif;
        var adesso = DateTime.UtcNow;
        var idUtente = _db.IdUtenteCorrente;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var idLottoProdotto = interno.IdLottoEsistente;
        if (idLottoProdotto is null)
        {
            var nuovo = new Lotto
            {
                IdArticolo = ricetta.IdArticolo, Codice = interno.Codice, Tipo = TipoLotto.Finito, Stato = StatoLotto.Attivo,
                GiornoLottoInterno = data, DataCreazione = adesso
            };
            _db.Lotti.Add(nuovo);
            await _db.SaveChangesAsync(ct);
            idLottoProdotto = nuovo.IdLotto;
        }
        var produzione = new Produzione
        {
            IdInsediamento = interno.Sede.IdInsediamento, IdRicettaVersione = versione.IdRicettaVersione, IdArticolo = ricetta.IdArticolo,
            IdLottoProdotto = idLottoProdotto.Value, Giorno = data, DataOra = adesso, Quantita = quantita,
            UnitaMisura = quantita is null ? null : "kg", Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), IdUtente = idUtente
        };
        _db.Produzioni.Add(produzione);
        await _db.SaveChangesAsync(ct);
        var giacenzeInsufficienti = new List<string>();
        foreach (var (idLotto, cat) in scelti)
        {
            consumi.KgPerLotto.TryGetValue(idLotto, out var kgLotto);
            _db.ProduzioniConsumi.Add(new ProduzioneConsumo
            {
                IdProduzione = produzione.IdProduzione, IdCategoria = cat, IdLottoIngresso = idLotto, Origine = origine, IdUtente = idUtente,
                Quantita = kgLotto, UnitaMisura = kgLotto is null ? null : "kg"
            });
            if (kgLotto is not > 0) continue;
            // Il magazzino cala del consumo, ma mai sotto zero: la quantità registrata nella produzione resta quella vera.
            var giacenza = Math.Max(0m, await _db.MovimentiLotto.Where(mv => mv.IdLotto == idLotto).SumAsync(mv => (decimal?)mv.Quantita, ct) ?? 0m);
            var scarico = Math.Min(kgLotto.Value, giacenza);
            if (scarico < kgLotto.Value)
                giacenzeInsufficienti.Add(await _db.Lotti.Where(l => l.IdLotto == idLotto).Select(l => l.Codice).FirstAsync(ct));
            if (scarico > 0)
                _db.MovimentiLotto.Add(new MovimentoLotto
                {
                    IdInsediamento = interno.Sede.IdInsediamento, IdLotto = idLotto, Tipo = TipoMovimento.Consumo, Quantita = -scarico,
                    UnitaMisura = "kg", DataMovimento = adesso, IdProduzione = produzione.IdProduzione,
                    Nota = $"Usato nella produzione del {data:dd/MM/yyyy}", IdUtente = idUtente, DataCreazione = adesso
                });
        }
        if (quantita is > 0)
            _db.MovimentiLotto.Add(new MovimentoLotto
            {
                IdInsediamento = interno.Sede.IdInsediamento, IdLotto = idLottoProdotto.Value, Tipo = TipoMovimento.Produzione,
                Quantita = quantita.Value, UnitaMisura = "kg", DataMovimento = adesso, IdProduzione = produzione.IdProduzione,
                Nota = $"Produzione del {data:dd/MM/yyyy}", IdUtente = idUtente, DataCreazione = adesso
            });
        _db.Audit.Add(new VoceAudit
        {
            IdUtente = idUtente, IdInsediamento = interno.Sede.IdInsediamento, Azione = "ProduzioneRegistrata", Entita = "Produzione",
            IdEntita = produzione.IdProduzione.ToString(), Dopo = $"{ricetta.Nome}, lotto {interno.Codice}, {scelti.Count} lotti di ingresso ({origine})",
            IndirizzoIp = HttpContext.Connection.RemoteIpAddress?.ToString(), DataOra = adesso
        });
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var testo = $"Produzione registrata: lotto {interno.Codice} del {data:dd/MM/yyyy}, {scelti.Count} lotti di ingresso collegati.";
        if (consumi.CalcolatiDallaRicetta) testo += " I kg degli ingredienti sono stati calcolati dalla ricetta.";
        if (giacenzeInsufficienti.Count > 0)
            testo += $" Attenzione: la giacenza non bastava per {string.Join(", ", giacenzeInsufficienti)} (il magazzino è stato portato a zero).";
        TempData["Messaggio"] = testo;
        return RedirectToAction("Rintracciabilita", "Lotti", new { id = idLottoProdotto.Value, tipo = "pf" });
    }

    /// <summary>
    /// I lotti con la loro data di impegno. Per i lotti nuovi è l'inizio dell'uso (LottoInUso); per quelli importati
    /// dal vecchio programma è la data del movimento «Storico importato». Un lotto mai messo in uso non è candidato.
    /// </summary>
    private async Task<List<LottoImpegnato>> CandidatiAsync(IReadOnlyCollection<int> categorie, CancellationToken ct)
    {
        var lotti = await (from l in _db.Lotti.AsNoTracking()
                           join a in _db.Articoli.AsNoTracking() on l.IdArticolo equals a.IdArticolo
                           join f in _db.Fornitori.AsNoTracking() on l.IdFornitore equals f.IdFornitore
                           where l.Tipo == TipoLotto.Ingresso
                           let cat = l.IdCategoria ?? a.IdCategoria
                           where cat != null && categorie.Contains(cat.Value)
                           select new { l.IdLotto, Cat = cat!.Value, f.IdFornitore, f.RagioneSociale, l.Codice, Articolo = a.Descrizione, l.DataScadenza })
                          .ToListAsync(ct);
        var ids = lotti.Select(l => l.IdLotto).ToList();
        if (ids.Count == 0) return new();

        var inUso = (await _db.LottiInUso.AsNoTracking().Where(u => ids.Contains(u.IdLotto))
                .GroupBy(u => u.IdLotto).Select(g => new { IdLotto = g.Key, Inizio = g.Min(x => x.Inizio) }).ToListAsync(ct))
            .ToDictionary(x => x.IdLotto, x => x.Inizio);
        var storico = (await _db.MovimentiLotto.AsNoTracking()
                .Where(mv => ids.Contains(mv.IdLotto) && mv.Tipo == TipoMovimento.Rettifica && mv.Nota != null && mv.Nota.StartsWith("Storico importato"))
                .GroupBy(mv => mv.IdLotto).Select(g => new { IdLotto = g.Key, Data = g.Max(x => x.DataMovimento) }).ToListAsync(ct))
            .ToDictionary(x => x.IdLotto, x => x.Data);

        var risultato = new List<LottoImpegnato>();
        foreach (var l in lotti)
        {
            DateTime? quando = inUso.TryGetValue(l.IdLotto, out var i) ? i : storico.TryGetValue(l.IdLotto, out var s) ? s : null;
            if (quando is null) continue;
            risultato.Add(new LottoImpegnato(l.IdLotto, l.Cat, l.IdFornitore, l.RagioneSociale, l.Codice, l.Articolo,
                DateOnly.FromDateTime(quando.Value), l.DataScadenza));
        }
        return risultato;
    }

    private async Task<ModelloSchedaRicetta?> CostruisciScheda(int id, CancellationToken ct)
    {
        var ricetta = await _db.Ricette.AsNoTracking().FirstOrDefaultAsync(r => r.IdRicetta == id, ct);
        if (ricetta is null) return null;
        var nomiCat = await _db.CategorieMateriaPrima.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome)
            .Select(c => new OpzioneCategoria { IdCategoria = c.IdCategoria, Nome = c.Nome }).ToListAsync(ct);
        var nomi = await _db.CategorieMateriaPrima.AsNoTracking().ToDictionaryAsync(c => c.IdCategoria, c => c.Nome, ct);
        var righe = await (from v in _db.RicetteVersioni.AsNoTracking()
                           join g in _db.RicetteRighe.AsNoTracking() on v.IdRicettaVersione equals g.IdRicettaVersione
                           where v.IdRicetta == id && v.ValidaAl == null && g.Attiva
                           orderby g.Ordine
                           select g).ToListAsync(ct);
        var extra = await _db.RicetteRigheCategorie.AsNoTracking().ToListAsync(ct);
        var m = new ModelloSchedaRicetta { IdRicetta = id, Nome = ricetta.Nome, Attiva = ricetta.Attiva, CategorieDisponibili = nomiCat };
        foreach (var r in righe)
        {
            var cat = new List<string>();
            if (r.IdCategoria is { } c1 && nomi.TryGetValue(c1, out var n1)) cat.Add(n1);
            cat.AddRange(extra.Where(e => e.IdRicettaRiga == r.IdRicettaRiga && nomi.ContainsKey(e.IdCategoria)).Select(e => nomi[e.IdCategoria]));
            m.Ingredienti.Add(new RigaIngrediente { IdRicettaRiga = r.IdRicettaRiga, Nome = r.Etichetta ?? cat.FirstOrDefault() ?? "", Categorie = cat, LottiPerFornitore = r.LottiPerFornitore, QuantitaTeorica = r.QuantitaTeorica });
        }
        m.QuantitaRiferimento = await _db.RicetteVersioni.AsNoTracking().Where(v => v.IdRicetta == id && v.ValidaAl == null)
            .OrderByDescending(v => v.Numero).Select(v => v.QuantitaProdottaTeorica).FirstOrDefaultAsync(ct);
        return m;
    }
}
