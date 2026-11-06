using ClosedXML.Excel;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Segnalatori (enti, studi, persone, consulenti, MARE.A), le loro condizioni (percentuale, ambito commessa o area,
/// continuità) e i clienti segnalati, indipendenti dal commerciale. Compensi calcolati sugli incassi.
/// Nessuna cancellazione fisica: si disattiva, si chiude o si annulla.
/// </summary>
// 21/10/2026: in sola lettura anche per chi ha provvigioni.consulta (soci, amministrazione contabile); ogni modifica
// (azioni POST) resta riservata a provvigioni.gestisci.
[RichiedeUnoDeiPermessi(Permessi.ProvvigioniGestisci, Permessi.ProvvigioniConsulta)]
public class SegnalatoriController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioProvvigioniLab _servizio;
    private readonly ServizioCompensiSegnalatori _compensi;
    private readonly IServizioAudit _audit;

    public SegnalatoriController(ContestoLaboratorioAI db, ServizioProvvigioniLab servizio, ServizioCompensiSegnalatori compensi, IServizioAudit audit)
    {
        _db = db;
        _servizio = servizio;
        _compensi = compensi;
        _audit = audit;
    }

    // ── Elenco segnalatori, condizioni e clienti segnalati ─────────────

    public async Task<IActionResult> Index(bool tutti, CancellationToken ct)
    {
        ViewData["Titolo"] = "Segnalatori";
        ViewData["PuoModificare"] = HttpContext.RequestServices.GetRequiredService<IContestoUtente>().HaPermesso(Permessi.ProvvigioniGestisci);
        ViewData["Tutti"] = tutti;
        await _servizio.InizializzaAsync(ct);
        ViewData["Eliminati"] = await _db.Segnalatori.CountAsync(s => !s.Attivo, ct);
        var segnalatori = await _db.Segnalatori.AsNoTracking().Include(s => s.Condizioni)
            .Where(s => tutti || s.Attivo)
            .OrderByDescending(s => s.Attivo).ThenBy(s => s.Nome).ToListAsync(ct);
        var segnalazioni = await _db.Segnalazioni.AsNoTracking().Where(s => s.Attivo)
            .OrderBy(s => s.CodiceCliente).ThenBy(s => s.DataInizio).ToListAsync(ct);
        var basi = await _db.BasiCalcolo.AsNoTracking().Where(b => b.Attivo).OrderBy(b => b.Nome).ToListAsync(ct);
        var clienti = await ClientiAsync(ct);
        var idCommesse = segnalazioni.Where(s => s.IdCommessa != null).Select(s => s.IdCommessa!.Value).Distinct().ToList();
        var commesse = (await _db.CommesseArcLab.AsNoTracking().Where(c => idCommesse.Contains(c.IdCommessa)).ToListAsync(ct))
            .GroupBy(c => c.IdCommessa).ToDictionary(g => g.Key, g => DescriviCommessa(g.First()));
        return View(new ModelloSegnalatori(segnalatori, segnalazioni, basi,
            clienti.ToDictionary(c => c.Codice, c => c.Nome, StringComparer.Ordinal), clienti, commesse));
    }

    /// <summary>Crea (id assente) o modifica un segnalatore.</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> Salva(int? idSegnalatore, string nome, TipoSegnalatore tipo, string? note, bool attivo, CancellationToken ct)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length == 0)
            return Torna("Il nome del segnalatore è obbligatorio.");
        Segnalatore s;
        if (idSegnalatore is null)
        {
            if (await _db.Segnalatori.AnyAsync(x => x.Nome == nome && x.Attivo, ct))
                return Torna($"Esiste già un segnalatore attivo «{nome}».");
            s = new Segnalatore { Nome = Tronca(nome, 100)!, Tipo = tipo, Note = Tronca(note, 500) };
            _db.Segnalatori.Add(s);
        }
        else
        {
            var trovato = await _db.Segnalatori.FirstOrDefaultAsync(x => x.IdSegnalatore == idSegnalatore, ct);
            if (trovato is null) return NotFound();
            s = trovato;
            s.Nome = Tronca(nome, 100)!;
            s.Tipo = tipo;
            s.Note = Tronca(note, 500);
            s.Attivo = attivo;
        }
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, idSegnalatore is null ? "provvigioni.segnalatore.aggiunta" : "provvigioni.segnalatore.modifica",
            EsitoOperazione.Successo, "Segnalatore", s.IdSegnalatore.ToString(), new { s.Nome, s.Tipo, s.Attivo }, ct);
        return Torna(idSegnalatore is null
            ? $"Segnalatore «{s.Nome}» aggiunto: ora aggiungi la sua condizione (percentuale e ambito)."
            : $"Segnalatore «{s.Nome}» aggiornato.");
    }

    /// <summary>
    /// Toglie un segnalatore dall'elenco (inserito per errore o doppio). Nessuna cancellazione fisica: diventa non attivo
    /// con le sue condizioni. Se ha clienti segnalati va prima unito a un altro segnalatore.
    /// </summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> Elimina(int idSegnalatore, CancellationToken ct)
    {
        var s = await _db.Segnalatori.Include(x => x.Condizioni).FirstOrDefaultAsync(x => x.IdSegnalatore == idSegnalatore, ct);
        if (s is null) return NotFound();
        if (s.Tipo == TipoSegnalatore.Azienda)
            return Torna("MARE.A non si può eliminare: è il segnalatore predefinito delle convenzioni «segnalate».");
        var idCondizioni = s.Condizioni.Select(c => c.IdCondizioneSegnalatore).ToList();
        var usate = await _db.Segnalazioni.CountAsync(x => x.Attivo && idCondizioni.Contains(x.IdCondizioneSegnalatore), ct);
        if (usate > 0)
            return Torna($"«{s.Nome}» ha {usate} clienti segnalati: se è un doppione usa «Unisci in…» per spostarli sull'altro, altrimenti annulla prima le segnalazioni.");
        s.Attivo = false;
        foreach (var c in s.Condizioni) c.Attivo = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.segnalatore.eliminato", EsitoOperazione.Successo,
            "Segnalatore", s.IdSegnalatore.ToString(), new { s.Nome }, ct);
        return Torna($"«{s.Nome}» eliminato dall'elenco (resta nello storico, visibile con «mostra anche gli eliminati»).");
    }

    /// <summary>
    /// Unisce un doppione in un altro segnalatore: le sue condizioni (e quindi i clienti segnalati) passano all'altro,
    /// il doppione diventa non attivo.
    /// </summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> Unisci(int idSegnalatore, int idDestinazione, CancellationToken ct)
    {
        if (idSegnalatore == idDestinazione) return Torna("Scegli un segnalatore diverso.");
        var s = await _db.Segnalatori.Include(x => x.Condizioni).FirstOrDefaultAsync(x => x.IdSegnalatore == idSegnalatore, ct);
        var d = await _db.Segnalatori.FirstOrDefaultAsync(x => x.IdSegnalatore == idDestinazione && x.Attivo, ct);
        if (s is null || d is null) return Torna("Segnalatore non valido.");
        if (s.Tipo == TipoSegnalatore.Azienda) return Torna("MARE.A non si può unire a un altro segnalatore.");
        var spostate = s.Condizioni.Count;
        foreach (var c in s.Condizioni.ToList()) c.IdSegnalatore = d.IdSegnalatore;
        s.Attivo = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.segnalatore.unito", EsitoOperazione.Successo,
            "Segnalatore", s.IdSegnalatore.ToString(), new { da = s.Nome, a = d.Nome, condizioni = spostate }, ct);
        return Torna($"«{s.Nome}» unito in «{d.Nome}»: {spostate} condizioni spostate con i loro clienti segnalati.");
    }

    /// <summary>Toglie una condizione inserita per errore: non attiva e nascosta. Se ha clienti segnalati resta, solo non più selezionabile.</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> EliminaCondizione(int idCondizione, CancellationToken ct)
    {
        var c = await _db.CondizioniSegnalatori.FirstOrDefaultAsync(x => x.IdCondizioneSegnalatore == idCondizione, ct);
        if (c is null) return NotFound();
        var usate = await _db.Segnalazioni.CountAsync(x => x.Attivo && x.IdCondizioneSegnalatore == idCondizione, ct);
        c.Attivo = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.condizione.eliminata", EsitoOperazione.Successo,
            "CondizioneSegnalatore", c.IdCondizioneSegnalatore.ToString(), new { c.Nome, usate }, ct);
        return Torna(usate > 0
            ? $"Condizione «{c.Nome}» disattivata: i {usate} clienti già segnalati con questa condizione continuano a usarla."
            : $"Condizione «{c.Nome}» eliminata.");
    }

    /// <summary>Crea (id assente) o modifica una condizione di un segnalatore.</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> SalvaCondizione(int? idCondizione, int idSegnalatore, string nome, string? percentuale,
        AmbitoCompenso ambito, int? idBaseCalcolo, int? mesiInterruzione, int? anniDurataMassima, string? note, bool attivo, CancellationToken ct)
    {
        nome = (nome ?? "").Trim();
        var pct = AccordiProvvigioniController.Decimale(percentuale);
        if (nome.Length == 0 || pct is null || pct < 0m || pct > 100m)
            return Torna("Condizione non valida: servono un nome e una percentuale fra 0 e 100.");
        if (ambito == AmbitoCompenso.Area && idBaseCalcolo is null)
            return Torna("Per una condizione «su tutta l'area» scegli l'area (base di calcolo).");
        if (!await _db.Segnalatori.AnyAsync(x => x.IdSegnalatore == idSegnalatore, ct)) return NotFound();

        CondizioneSegnalatore c;
        if (idCondizione is null)
        {
            c = new CondizioneSegnalatore { IdSegnalatore = idSegnalatore };
            _db.CondizioniSegnalatori.Add(c);
        }
        else
        {
            var trovata = await _db.CondizioniSegnalatori.FirstOrDefaultAsync(x => x.IdCondizioneSegnalatore == idCondizione, ct);
            if (trovata is null) return NotFound();
            c = trovata;
            c.Attivo = attivo;
        }
        c.Nome = Tronca(nome, 100)!;
        c.Percentuale = pct.Value;
        c.Ambito = ambito;
        c.IdBaseCalcolo = ambito == AmbitoCompenso.Area ? idBaseCalcolo : null;
        c.MesiInterruzione = mesiInterruzione is > 0 ? mesiInterruzione.Value : 24;
        c.AnniDurataMassima = anniDurataMassima is > 0 ? anniDurataMassima : null;
        c.Note = Tronca(note, 500);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, idCondizione is null ? "provvigioni.condizione.aggiunta" : "provvigioni.condizione.modifica",
            EsitoOperazione.Successo, "CondizioneSegnalatore", c.IdCondizioneSegnalatore.ToString(),
            new { c.IdSegnalatore, c.Nome, c.Percentuale, c.Ambito, c.IdBaseCalcolo, c.MesiInterruzione, c.AnniDurataMassima, c.Attivo }, ct);
        return Torna($"Condizione «{c.Nome}» salvata.");
    }

    /// <summary>
    /// Registra che un cliente è stato segnalato, con la condizione che si applica dalla data indicata.
    /// Condizione «sulla commessa» senza commessa scelta: si prende la commessa del cliente più vicina alla data
    /// (prima consulenza e formazione, poi analisi). ritorno = "convenzioni" per tornare a quella pagina.
    /// </summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> Assegna(string codiceCliente, int idCondizione, DateTime? dataInizio, int? idCommessa,
        string? note, string? ritorno, int? idCommerciale, CancellationToken ct)
    {
        codiceCliente = (codiceCliente ?? "").Trim();
        IActionResult Fine(string msg)
        {
            TempData["Messaggio"] = msg;
            return ritorno == "convenzioni"
                ? RedirectToAction("Convenzioni", "Provvigioni", new { idCommerciale })
                : RedirectToAction(nameof(Index));
        }
        if (codiceCliente.Length == 0 || !await _db.AzwFatture.AnyAsync(f => f.CodiceCliente == codiceCliente, ct))
            return Fine($"Il cliente «{codiceCliente}» non risulta tra le fatture importate da AZW.");
        var c = await _db.CondizioniSegnalatori.AsNoTracking().FirstOrDefaultAsync(x => x.IdCondizioneSegnalatore == idCondizione && x.Attivo, ct);
        if (c is null) return Fine("Condizione non valida.");
        var inizio = (dataInizio ?? DateTime.Today).Date;
        if (inizio.Year < 2000) return Fine("Data non valida.");

        var avviso = "";
        if (c.Ambito == AmbitoCompenso.Commessa && idCommessa is null)
        {
            var commesse = await _db.CommesseArcLab.AsNoTracking().Where(x => x.CodiceCliente == codiceCliente && x.DataCommessa != null).ToListAsync(ct);
            var scelta = commesse
                .OrderBy(x => x.IdTipoCommessa == 3 ? 1 : 0)
                .ThenBy(x => Math.Abs((x.DataCommessa!.Value.Date - inizio).TotalDays))
                .FirstOrDefault();
            idCommessa = scelta?.IdCommessa;
            avviso = scelta is null
                ? " Attenzione: il cliente non ha commesse in ArcLab, il compenso non si calcola finché non indichi la commessa o non usi una condizione d'area."
                : $" Commessa collegata: {DescriviCommessa(scelta)} (cambiala dalla pagina Segnalatori se non è quella giusta).";
        }

        var s = new Segnalazione
        {
            CodiceCliente = codiceCliente, IdCondizioneSegnalatore = idCondizione, DataInizio = inizio,
            IdCommessa = c.Ambito == AmbitoCompenso.Commessa ? idCommessa : null, Note = Tronca(note, 500)
        };
        _db.Segnalazioni.Add(s);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.segnalazione.aggiunta", EsitoOperazione.Successo,
            "Segnalazione", s.IdSegnalazione.ToString(), new { codiceCliente, idCondizione, inizio, s.IdCommessa }, ct);
        return Fine($"Segnalazione registrata per il cliente {codiceCliente}.{avviso}");
    }

    /// <summary>Corregge commessa, data di inizio, data di fine e note di una segnalazione.</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> ModificaSegnalazione(int idSegnalazione, DateTime dataInizio, DateTime? dataFine, int? idCommessa, string? note, CancellationToken ct)
    {
        var s = await _db.Segnalazioni.FirstOrDefaultAsync(x => x.IdSegnalazione == idSegnalazione, ct);
        if (s is null) return NotFound();
        if (dataInizio.Year < 2000 || (dataFine is DateTime df && df.Date < dataInizio.Date))
            return Torna("Date non valide.");
        var prima = new { s.DataInizio, s.DataFine, s.IdCommessa, s.Note };
        s.DataInizio = dataInizio.Date;
        s.DataFine = dataFine?.Date;
        s.IdCommessa = idCommessa;
        s.Note = Tronca(note, 500);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.segnalazione.modifica", EsitoOperazione.Successo,
            "Segnalazione", s.IdSegnalazione.ToString(), new { prima, dopo = new { s.DataInizio, s.DataFine, s.IdCommessa, s.Note } }, ct);
        return Torna($"Segnalazione del cliente {s.CodiceCliente} aggiornata.");
    }

    /// <summary>Annulla una segnalazione inserita per errore (resta nel database, non conta più).</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> Annulla(int idSegnalazione, CancellationToken ct)
    {
        var s = await _db.Segnalazioni.FirstOrDefaultAsync(x => x.IdSegnalazione == idSegnalazione, ct);
        if (s is null) return NotFound();
        s.Attivo = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.segnalazione.annullata", EsitoOperazione.Successo,
            "Segnalazione", s.IdSegnalazione.ToString(), new { s.CodiceCliente, s.IdCondizioneSegnalatore }, ct);
        return Torna($"Segnalazione del cliente {s.CodiceCliente} annullata.");
    }

    /// <summary>Commesse ArcLab di un cliente (per scegliere quella della segnalazione).</summary>
    [HttpGet]
    public async Task<IActionResult> CommesseCliente(string codice, CancellationToken ct)
    {
        codice = (codice ?? "").Trim();
        var commesse = await _db.CommesseArcLab.AsNoTracking().Where(c => c.CodiceCliente == codice)
            .OrderByDescending(c => c.DataCommessa).ToListAsync(ct);
        return Json(commesse.GroupBy(c => c.IdCommessa).Select(g => new { id = g.Key, testo = DescriviCommessa(g.First()) }));
    }

    // ── Compensi ──────────────────────────────────────────────────────

    public async Task<IActionResult> Compensi(int? idSegnalatore, CancellationToken ct)
    {
        ViewData["Titolo"] = "Compensi segnalatori";
        await _servizio.InizializzaAsync(ct);
        var dati = await DatiCompensiAsync(idSegnalatore, ct);
        return View(dati);
    }

    public async Task<IActionResult> Esporta(int? idSegnalatore, CancellationToken ct)
    {
        var d = await DatiCompensiAsync(idSegnalatore, ct);
        using var libro = new XLWorkbook();
        var ws = libro.Worksheets.Add("Trimestri");
        ws.Cell(1, 1).InsertTable(d.Trimestri.Select(t => new
        {
            Trimestre = t.Etichetta, Incassato = t.Incassato, NoteCredito = t.NoteCredito, Compenso = t.Provvigione, Stato = t.Chiuso ? "chiuso" : "aperto"
        }));
        var wr = libro.Worksheets.Add("Per segnalatore");
        wr.Cell(1, 1).InsertTable(d.PerSegnalatore.Select(r => new { Segnalatore = r.Nome, r.Clienti, Base = r.Base, Compenso = r.Compenso }));
        var wc = libro.Worksheets.Add("Per cliente");
        wc.Cell(1, 1).InsertTable(d.PerCliente.Select(r => new
        {
            Segnalatore = r.Segnalatore, Condizione = r.Condizione, Cliente = r.Cliente, RagioneSociale = r.Nome,
            Dal = r.DataInizio, Fine = r.FineCompenso, Base = r.Base, Compenso = r.Compenso
        }));
        var wm = libro.Worksheets.Add("Movimenti");
        wm.Cell(1, 1).InsertTable(d.Movimenti.Select(m => new
        {
            m.Trimestre, Segnalatore = d.NomeSegnalatoreDi(m.IdSegnalazione), Cliente = m.Cliente, Documento = m.Riferimento,
            m.Data, Tipo = m.Tipo == TipoMovimento.NotaCredito ? "Nota di credito" : "Incasso", m.Base, Aliquota = m.Aliquota, Compenso = m.Importo
        }));
        foreach (var w in libro.Worksheets) w.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.segnalatori.esportazione", EsitoOperazione.Successo,
            dettaglio: new { idSegnalatore }, ct: ct);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"compensi_segnalatori_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    private async Task<ModelloCompensi> DatiCompensiAsync(int? idSegnalatore, CancellationToken ct)
    {
        var segnalatori = await _db.Segnalatori.AsNoTracking().OrderBy(s => s.Nome).ToListAsync(ct);
        var condizioni = await _db.CondizioniSegnalatori.AsNoTracking().ToDictionaryAsync(c => c.IdCondizioneSegnalatore, ct);
        var segnalazioni = await _db.Segnalazioni.AsNoTracking().Where(s => s.Attivo).ToListAsync(ct);
        var ultimo = await _db.ImportazioniAzw.AsNoTracking().OrderByDescending(i => i.DataOra).FirstOrDefaultAsync(ct);
        var calcolo = await _compensi.CalcolaAsync(ct);
        var trimestri = await _compensi.RigheTrimestriAsync(calcolo, idSegnalatore, ct);
        var movimenti = calcolo.Movimenti(idSegnalatore).ToList();
        var nomi = (await ClientiAsync(ct)).ToDictionary(c => c.Codice, c => c.Nome, StringComparer.Ordinal);
        var nomeSeg = segnalatori.ToDictionary(s => s.IdSegnalatore, s => s.Nome);

        var perCliente = segnalazioni
            .Where(s => condizioni.ContainsKey(s.IdCondizioneSegnalatore))
            .Select(s => (S: s, C: condizioni[s.IdCondizioneSegnalatore]))
            .Where(x => idSegnalatore is null || x.C.IdSegnalatore == idSegnalatore)
            .Select(x =>
            {
                var mov = calcolo.Risultato.Movimenti.Where(m => m.IdSegnalazione == x.S.IdSegnalazione).ToList();
                return new RigaCompensoCliente(x.S.IdSegnalazione, nomeSeg.GetValueOrDefault(x.C.IdSegnalatore, "?"),
                    $"{x.C.Nome} ({x.C.Percentuale:0.##}%)", x.S.CodiceCliente, nomi.GetValueOrDefault(x.S.CodiceCliente, ""),
                    x.S.DataInizio, calcolo.Risultato.FineCompenso.TryGetValue(x.S.IdSegnalazione, out var f) ? f : null,
                    mov.Sum(m => m.Base), mov.Sum(m => m.Importo));
            })
            .OrderBy(r => r.Segnalatore).ThenByDescending(r => r.Compenso).ToList();
        var perSegnalatore = perCliente.GroupBy(r => r.Segnalatore)
            .Select(g => new RigaCompensoSegnalatore(g.Key, g.Select(r => r.Cliente).Distinct().Count(), g.Sum(r => r.Base), g.Sum(r => r.Compenso)))
            .OrderByDescending(r => r.Compenso).ThenBy(r => r.Nome).ToList();
        var segnalatoreDiSegnalazione = segnalazioni.Where(s => condizioni.ContainsKey(s.IdCondizioneSegnalatore))
            .ToDictionary(s => s.IdSegnalazione, s => nomeSeg.GetValueOrDefault(condizioni[s.IdCondizioneSegnalatore].IdSegnalatore, "?"));
        return new ModelloCompensi(segnalatori, idSegnalatore, ultimo, trimestri, perSegnalatore, perCliente, movimenti,
            calcolo.Risultato.Avvisi, nomi, segnalatoreDiSegnalazione);
    }

    // ── Utilità ───────────────────────────────────────────────────────

    private IActionResult Torna(string messaggio)
    {
        TempData["Messaggio"] = messaggio;
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<ClienteElenco>> ClientiAsync(CancellationToken ct) =>
        (await _db.AzwFatture.AsNoTracking()
            .GroupBy(f => f.CodiceCliente).Select(g => new { Codice = g.Key, Nome = g.Max(x => x.RagioneSociale) })
            .ToListAsync(ct))
        .Select(x => new ClienteElenco(x.Codice, x.Nome ?? "")).OrderBy(c => c.Nome).ToList();

    internal static string DescriviCommessa(CommessaArcLab c)
    {
        var tipo = c.IdTipoCommessa switch { 3 => "analisi", 4 => "formazione", 5 => "consulenza", _ => $"tipo {c.IdTipoCommessa}" };
        var data = c.DataCommessa is DateTime d ? d.ToString("dd/MM/yyyy") : "senza data";
        var descr = string.IsNullOrWhiteSpace(c.Descrizione) ? "" : " – " + (c.Descrizione.Length > 60 ? c.Descrizione[..60] + "…" : c.Descrizione);
        return $"n. {c.IdCommessa} ({tipo}, {data}){descr}";
    }

    private static string? Tronca(string? v, int max)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}

public sealed record ModelloSegnalatori(
    IReadOnlyList<Segnalatore> Segnalatori,
    IReadOnlyList<Segnalazione> Segnalazioni,
    IReadOnlyList<BaseCalcolo> Basi,
    IReadOnlyDictionary<string, string> NomiClienti,
    IReadOnlyList<ClienteElenco> Clienti,
    IReadOnlyDictionary<int, string> Commesse);

public sealed record RigaCompensoSegnalatore(string Nome, int Clienti, decimal Base, decimal Compenso);

public sealed record RigaCompensoCliente(int IdSegnalazione, string Segnalatore, string Condizione, string Cliente, string Nome,
    DateTime DataInizio, DateTime? FineCompenso, decimal Base, decimal Compenso);

public sealed record ModelloCompensi(
    IReadOnlyList<Segnalatore> Segnalatori,
    int? IdSegnalatore,
    ImportazioneAzw? UltimaImportazione,
    IReadOnlyList<RigaTrimestre> Trimestri,
    IReadOnlyList<RigaCompensoSegnalatore> PerSegnalatore,
    IReadOnlyList<RigaCompensoCliente> PerCliente,
    IReadOnlyList<MovimentoSegnalatore> Movimenti,
    IReadOnlyList<string> Avvisi,
    IReadOnlyDictionary<string, string> NomiClienti,
    IReadOnlyDictionary<int, string> SegnalatoreDiSegnalazione)
{
    public string NomeSegnalatoreDi(int idSegnalazione) => SegnalatoreDiSegnalazione.GetValueOrDefault(idSegnalazione, "?");
}
