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

public sealed record ModelloSpettanze(IReadOnlyList<ConsulentePercentuale> Consulenti, RisultatoSpettanze? Risultato, int Anno,
    IReadOnlyList<ContrattoConsulente> Contratti, IReadOnlyList<VociGruppoSpettanze> Gruppi, IReadOnlyList<Utente> Utenti,
    IReadOnlyList<AnnoSpettanzeChiuso> AnniChiusi, bool PuoGestire, bool PuoChiudere);

/// <summary>
/// Spettanze dei consulenti a percentuale (19/10/2026): calcolo sugli incassi AZW, acconti (fatture del consulente),
/// conguaglio annuale, chiusura che congela, configurazione (consulenti, contratti, gruppi di voci). Dati retributivi.
/// </summary>
[RichiedeUnoDeiPermessi(Permessi.ProvvigioniGestisci, Permessi.ProvvigioniConsulta)]
public class SpettanzeConsulentiController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioSpettanzeConsulenti _servizio;
    private readonly IServizioAudit _audit;
    private readonly IContestoUtente _utente;

    public SpettanzeConsulentiController(ContestoLaboratorioAI db, ServizioSpettanzeConsulenti servizio, IServizioAudit audit, IContestoUtente utente)
    {
        _db = db; _servizio = servizio; _audit = audit; _utente = utente;
    }

    private string Operatore => _utente.NomeCompleto ?? _utente.Email ?? "?";

    /// <summary>22/10/2026: filtro della vista da querystring (fDal/fAl = date fatture, iDal/iAl = date incassi).</summary>
    public static FiltroSpettanze? Filtro(DateTime? fDal, DateTime? fAl, DateTime? iDal, DateTime? iAl)
    {
        var f = new FiltroSpettanze(fDal?.Date, fAl?.Date, iDal?.Date, iAl?.Date);
        return f.Attivo ? f : null;
    }

    public async Task<IActionResult> Index(int? id, int? anno, DateTime? fDal, DateTime? fAl, DateTime? iDal, DateTime? iAl, CancellationToken ct)
    {
        ViewData["Titolo"] = "Spettanze consulenti";
        await ServizioSpettanzeConsulenti.PopolaAsync(_db, ct);
        var consulenti = await _db.ConsulentiPercentuale.AsNoTracking().OrderByDescending(c => c.Attivo).ThenBy(c => c.Nome).ToListAsync(ct);
        var annoScelto = anno ?? DateTime.Today.Year;
        var scelto = consulenti.FirstOrDefault(c => c.IdConsulentePercentuale == id) ?? consulenti.FirstOrDefault(c => c.Attivo);
        var risultato = scelto is null ? null : await _servizio.CalcolaAsync(scelto.IdConsulentePercentuale, annoScelto, Filtro(fDal, fAl, iDal, iAl), ct);
        var contratti = scelto is null ? new List<ContrattoConsulente>()
            : await _db.ContrattiConsulenti.AsNoTracking().Where(k => k.IdConsulentePercentuale == scelto.IdConsulentePercentuale).OrderBy(k => k.ValidoDal).ToListAsync(ct);
        var chiusi = scelto is null ? new List<AnnoSpettanzeChiuso>()
            : await _db.AnniSpettanzeChiusi.AsNoTracking().Where(a => a.IdConsulentePercentuale == scelto.IdConsulentePercentuale).OrderByDescending(a => a.Anno).ToListAsync(ct);
        var gruppi = await _db.VociGruppiSpettanze.AsNoTracking().OrderBy(g => g.Gruppo).ToListAsync(ct);
        var utenti = await _db.Utenti.AsNoTracking().Where(u => u.Attivo).OrderBy(u => u.NomeCompleto).ToListAsync(ct);
        return View(new ModelloSpettanze(consulenti, risultato, annoScelto, contratti, gruppi, utenti, chiusi,
            _utente.HaPermesso(Permessi.ProvvigioniGestisci), _utente.HaPermesso(Permessi.ProvvigioniChiudi)));
    }

    public async Task<IActionResult> Esporta(int id, int anno, DateTime? fDal, DateTime? fAl, DateTime? iDal, DateTime? iAl, CancellationToken ct)
    {
        var r = await _servizio.CalcolaAsync(id, anno, Filtro(fDal, fAl, iDal, iAl), ct);
        using var libro = LibroExcel(r);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.esportazione", EsitoOperazione.Successo,
            tipoEntita: "ConsulentePercentuale", idEntita: id.ToString(), dettaglio: new { anno, fDal, fAl, iDal, iAl }, ct: ct);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"spettanze_{r.Consulente.CodiceZona}_{anno}.xlsx");
    }

    /// <summary>Excel delle spettanze (usato anche dalla vista del consulente).</summary>
    public static XLWorkbook LibroExcel(RisultatoSpettanze r)
    {
        var libro = new XLWorkbook();
        var nomi = MotoreSpettanzeConsulenti.NomiVoci;
        var ws = libro.Worksheets.Add("Riepilogo");
        ws.Cell(1, 1).Value = $"Spettanze {r.Consulente.Nome} — anno {r.Anno}" + (r.Filtrato ? " (vista filtrata)" : r.Chiuso is null ? " (calcolo aperto)" : " (anno chiuso)");
        ws.Cell(1, 1).Style.Font.Bold = true;
        if (r.Filtrato) ws.Cell(2, 1).Value = string.Join(" ", r.Avvisi.Take(1));
        var riga = 3;
        ws.Cell(riga, 1).Value = "Mese";
        var col = 2;
        foreach (var l in nomi.Keys) ws.Cell(riga, col++).Value = nomi[l];
        ws.Cell(riga, col++).Value = "Maturato"; ws.Cell(riga, col++).Value = "Acconti (sue fatture)"; ws.Cell(riga, col).Value = "Docenze a ore (a parte)";
        ws.Row(riga).Style.Font.Bold = true;
        foreach (var m in r.Mesi)
        {
            riga++; col = 1;
            ws.Cell(riga, col++).Value = new DateTime(m.AnnoMese > 0 ? m.AnnoMese : r.Anno, m.Mese, 1).ToString(r.Filtrato ? "MMMM yyyy" : "MMMM", System.Globalization.CultureInfo.GetCultureInfo("it-IT"));
            foreach (var l in nomi.Keys) ws.Cell(riga, col++).Value = m.PerVoce[l];
            ws.Cell(riga, col++).Value = m.Maturato; ws.Cell(riga, col++).Value = m.Acconti; ws.Cell(riga, col).Value = m.DocenzeOrarie;
        }
        riga += 2;
        ws.Cell(riga, 1).Value = "Maturato"; ws.Cell(riga++, 2).Value = r.Maturato;
        if (!r.Filtrato)
        {
            ws.Cell(riga, 1).Value = "Acconti"; ws.Cell(riga++, 2).Value = r.Acconti;
            ws.Cell(riga, 1).Value = "Conguaglio (maturato − acconti)"; ws.Cell(riga++, 2).Value = r.Conguaglio;
        }
        ws.Cell(riga, 1).Value = "Docenze a ore (fatturate a parte)"; ws.Cell(riga, 2).Value = r.DocenzeOrarie;
        ws.Range(4, 2, riga, 2 + nomi.Count + 3).Style.NumberFormat.Format = "#,##0.00";

        var wa = libro.Worksheets.Add("Per anno fattura");
        wa.Cell(1, 1).InsertTable(r.PerAnnoFattura.Select(x => new
        {
            AnnoFattura = x.AnnoFattura?.ToString() ?? "non noto", BaseIncassata = x.Base,
            a = x.PerVoce["a"], b = x.PerVoce["b"], c_ = x.PerVoce["c"], d = x.PerVoce["d"], e = x.PerVoce["e"], f = x.PerVoce["f"], g = x.PerVoce["g"], Totale = x.Totale
        }));
        var wc = libro.Worksheets.Add("Clienti");
        wc.Cell(1, 1).InsertTable(r.Clienti.Select(c => new
        {
            Cliente = c.Cliente, RagioneSociale = c.RagioneSociale, Zona = c.Zona, Base = c.Base,
            a = c.PerVoce["a"], b = c.PerVoce["b"], c_ = c.PerVoce["c"], d = c.PerVoce["d"], e = c.PerVoce["e"], f = c.PerVoce["f"], g = c.PerVoce["g"], Totale = c.Totale
        }));
        var wm = libro.Worksheets.Add("Movimenti");
        wm.Cell(1, 1).InsertTable(r.Movimenti.Select(m => new
        {
            m.Data, DataFattura = m.DataFattura, Voce = nomi.GetValueOrDefault(m.Voce, m.Voce), m.Cliente, m.Documento, VoceAzw = m.VoceAzw, m.Commessa, m.Origine,
            m.Base, Percentuale = m.Percentuale, Spettanza = m.Importo
        }));
        var wf = libro.Worksheets.Add("Sue fatture");
        wf.Cell(1, 1).InsertTable(r.Fatture.Select(f => new
        {
            f.Data, f.Documento, f.Centro, f.Voce, f.Importo, Tipo = f.DocenzaOraria ? "Docenza a ore" : "Acconto"
        }));
        foreach (var w in libro.Worksheets) w.Columns().AdjustToContents();
        return libro;
    }

    /// <summary>Stessa importazione da AZW della pagina Provvigioni (vale per commerciali, segnalatori e consulenti).</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> Importa(int? id, int anno, [FromServices] ImportatoreAzw importatore,
        [FromServices] ServizioProvvigioniLab provvigioni, CancellationToken ct)
    {
        var esito = await importatore.ImportaAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.importazione.azw",
            esito.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            dettaglio: new { esito.Fatture, esito.Incassi, esito.NoteCredito, esito.Messaggio, da = "Spettanze consulenti" }, ct: ct);
        if (esito.Riuscita) await provvigioni.SincronizzaConvenzioniAsync(ct);
        TempData[esito.Riuscita ? "Messaggio" : "Errore"] = esito.Riuscita
            ? $"Importazione da AZW riuscita: {esito.Fatture} fatture, {esito.Incassi} incassi, {esito.NoteCredito} note di credito."
            : $"Importazione fallita: {esito.Messaggio}";
        return RedirectToAction(nameof(Index), new { id, anno });
    }

    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniChiudi)]
    public async Task<IActionResult> Chiudi(int id, int anno, string? note, CancellationToken ct)
    {
        var err = await _servizio.ChiudiAsync(id, anno, Operatore, note, ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.chiusura-anno", err is null ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            tipoEntita: "ConsulentePercentuale", idEntita: id.ToString(), dettaglio: new { anno, errore = err }, ct: ct);
        TempData[err is null ? "Messaggio" : "Errore"] = err ?? $"Anno {anno} chiuso: il calcolo è congelato.";
        return RedirectToAction(nameof(Index), new { id, anno });
    }

    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> SalvaConsulente(ConsulentePercentuale dati, int anno, CancellationToken ct)
    {
        var err = await _servizio.SalvaConsulenteAsync(dati, ct);
        if (err is null)
            await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.consulente.salvato", EsitoOperazione.Successo,
                tipoEntita: "ConsulentePercentuale", idEntita: dati.IdConsulentePercentuale.ToString(),
                dettaglio: new { dati.Nome, dati.CodiceZona, dati.CodiceCommessa, dati.CodiceFornitore, dati.CentriDocenzeOrarie, dati.IdUtente, dati.Attivo }, ct: ct);
        TempData[err is null ? "Messaggio" : "Errore"] = err ?? "Consulente salvato.";
        return RedirectToAction(nameof(Index), new { id = dati.IdConsulentePercentuale > 0 ? dati.IdConsulentePercentuale : (int?)null, anno });
    }

    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> SalvaContratto(int idContrattoConsulente, int idConsulentePercentuale, DateTime validoDal, DateTime? validoAl,
        string? riferimento, string? percConsulenza, string? percCampionamento, string? percCampionamentoEsterne, string? percAnalisiClienti, string? percPrimoAnnoServizi,
        string? percFormazioneAltri, string? percDocenza, string? percTarature, int mesiPrimoAnno, bool annullato, string? note, int anno, CancellationToken ct)
    {
        // percentuali come testo: «50», «7,5» o «7.5» indipendentemente dalla lingua del server
        static decimal? P(string? s) => decimal.TryParse((s ?? "").Trim().Replace(',', '.'), System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
        var dati = new ContrattoConsulente
        {
            IdContrattoConsulente = idContrattoConsulente, IdConsulentePercentuale = idConsulentePercentuale, ValidoDal = validoDal, ValidoAl = validoAl,
            Riferimento = riferimento, PercConsulenza = P(percConsulenza) ?? 0m, PercCampionamento = P(percCampionamento) ?? 0m,
            PercCampionamentoEsterne = P(percCampionamentoEsterne),
            PercAnalisiClienti = P(percAnalisiClienti) ?? 0m, PercPrimoAnnoServizi = P(percPrimoAnnoServizi) ?? 0m,
            PercFormazioneAltri = P(percFormazioneAltri) ?? 0m, PercDocenza = P(percDocenza) ?? 0m, PercTarature = P(percTarature),
            MesiPrimoAnno = mesiPrimoAnno, Annullato = annullato, Note = note
        };
        var err = await _servizio.SalvaContrattoAsync(dati, Operatore, ct);
        if (err is null)
            await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.contratto.salvato", EsitoOperazione.Successo,
                tipoEntita: "ContrattoConsulente", idEntita: dati.IdContrattoConsulente.ToString(), dettaglio: dati, ct: ct);
        TempData[err is null ? "Messaggio" : "Errore"] = err ?? "Contratto salvato.";
        return RedirectToAction(nameof(Index), new { id = dati.IdConsulentePercentuale, anno });
    }

    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniGestisci)]
    public async Task<IActionResult> SalvaGruppo(GruppoVociSpettanze gruppo, string voci, int? id, int anno, CancellationToken ct)
    {
        await _servizio.SalvaGruppoAsync(gruppo, voci, Operatore, ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.gruppo-voci.salvato", EsitoOperazione.Successo,
            dettaglio: new { gruppo = gruppo.ToString(), voci }, ct: ct);
        TempData["Messaggio"] = "Voci aggiornate.";
        return RedirectToAction(nameof(Index), new { id, anno });
    }
}

/// <summary>«Le mie spettanze»: il consulente vede solo i propri dati (consulente collegato all'utente connesso).</summary>
[RichiedePermesso(Permessi.SpettanzeProprie)]
public class MieSpettanzeController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioSpettanzeConsulenti _servizio;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public MieSpettanzeController(ContestoLaboratorioAI db, ServizioSpettanzeConsulenti servizio, IContestoUtente utente, IServizioAudit audit)
    {
        _db = db; _servizio = servizio; _utente = utente; _audit = audit;
    }

    private async Task<ConsulentePercentuale?> MioAsync(CancellationToken ct) =>
        _utente.IdUtente is int u ? await _db.ConsulentiPercentuale.AsNoTracking().FirstOrDefaultAsync(c => c.IdUtente == u && c.Attivo, ct) : null;

    public async Task<IActionResult> Index(int? anno, DateTime? fDal, DateTime? fAl, DateTime? iDal, DateTime? iAl, CancellationToken ct)
    {
        ViewData["Titolo"] = "Le mie spettanze";
        var c = await MioAsync(ct);
        var a = anno ?? DateTime.Today.Year;
        var r = c is null ? null : await _servizio.CalcolaAsync(c.IdConsulentePercentuale, a, SpettanzeConsulentiController.Filtro(fDal, fAl, iDal, iAl), ct);
        return View(new ModelloSpettanze(c is null ? [] : [c], r, a, [], [], [], [], false, false));
    }

    /// <summary>Aggiornamento da AZW richiesto dal consulente (finché non è attiva l'importazione notturna): al massimo ogni 15 minuti.</summary>
    [HttpPost]
    public async Task<IActionResult> Aggiorna(int anno, [FromServices] ImportatoreAzw importatore, CancellationToken ct)
    {
        var (ok, msg) = await importatore.AggiornaSeVecchiaAsync(TimeSpan.FromMinutes(15), ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.proprie.aggiorna-azw", ok ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            dettaglio: new { msg }, ct: ct);
        TempData[ok ? "Messaggio" : "Errore"] = msg;
        return RedirectToAction(nameof(Index), new { anno });
    }

    public async Task<IActionResult> Esporta(int anno, DateTime? fDal, DateTime? fAl, DateTime? iDal, DateTime? iAl, CancellationToken ct)
    {
        var c = await MioAsync(ct);
        if (c is null) return NotFound();
        var r = await _servizio.CalcolaAsync(c.IdConsulentePercentuale, anno, SpettanzeConsulentiController.Filtro(fDal, fAl, iDal, iAl), ct);
        using var libro = SpettanzeConsulentiController.LibroExcel(r);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "spettanze.proprie.esportazione", EsitoOperazione.Successo, dettaglio: new { anno }, ct: ct);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"mie_spettanze_{anno}.xlsx");
    }
}
