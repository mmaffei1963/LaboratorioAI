using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Provvigioni dei commerciali: importazione da AZW (sola lettura) e anteprima del calcolo.
/// Dati retributivi: accesso riservato a chi ha il permesso <c>provvigioni.gestisci</c>.
/// </summary>
// 11/10/2026: i soci (provvigioni.consulta) vedono tutto in sola lettura; le modifiche restano a provvigioni.gestisci.
[RichiedeUnoDeiPermessi(Permessi.ProvvigioniGestisci, Permessi.ProvvigioniConsulta)]
[SolaLetturaSenza(Permessi.ProvvigioniGestisci)]
public class ProvvigioniController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ImportatoreAzw _importatore;
    private readonly ServizioProvvigioniLab _servizio;
    private readonly IServizioAudit _audit;
    private readonly IServizioVersione _versione;
    private readonly IEsportatoreProvvigioni _esportatore;

    public ProvvigioniController(ContestoLaboratorioAI db, ImportatoreAzw importatore,
        ServizioProvvigioniLab servizio, IServizioAudit audit, IServizioVersione versione, IEsportatoreProvvigioni esportatore)
    {
        _versione = versione;
        _esportatore = esportatore;
        _db = db;
        _importatore = importatore;
        _servizio = servizio;
        _audit = audit;
    }

    public async Task<IActionResult> Index(int? idCommerciale, int? annoDett, int? baseDett, string[]? voci, CancellationToken ct)
    {
        ViewData["Titolo"] = "Provvigioni";
        await _servizio.InizializzaAsync(ct);

        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).ToListAsync(ct);
        var scelto = commerciali.FirstOrDefault(c => c.IdCommerciale == idCommerciale) ?? commerciali.FirstOrDefault();
        var ultimo = await _db.ImportazioniAzw.AsNoTracking().OrderByDescending(i => i.DataOra).FirstOrDefaultAsync(ct);

        RisultatoProvvigioniLab? risultato = null;
        if (scelto is not null && ultimo is { Riuscita: true })
            risultato = await _servizio.CalcolaAsync(scelto.IdCommerciale, ct: ct);

        var nomi = new Dictionary<string, string>(StringComparer.Ordinal);
        if (risultato is not null)
        {
            var codici = risultato.Movimenti.Select(m => m.Cliente).Distinct().ToList();
            nomi = await NomiClientiAsync(codici, ct);
        }
        var calcoloAttivo = scelto is not null && await _servizio.CalcoloAttivoAsync(scelto.IdCommerciale, ct);
        var righe = risultato is not null && scelto is not null
            ? await _servizio.RigheTrimestriAsync(scelto.IdCommerciale, risultato, ct)
            : new List<RigaTrimestre>();
        var utenti = await _db.Utenti.AsNoTracking().Where(u => u.Attivo).OrderBy(u => u.NomeCompleto)
            .Select(u => new UtenteElenco(u.IdUtente, u.NomeCompleto, u.Email)).ToListAsync(ct);
        var dettaglio = risultato is not null && scelto is not null
            ? await _servizio.DettaglioPerVoceAsync(scelto.IdCommerciale, risultato, annoDett, baseDett, voci, ct)
            : null;
        ViewData["SegnalatiDa"] = await SegnalatiDaAsync(ct);
        ViewData["Eccezioni"] = scelto is null ? new List<EccezioneAreaCommerciale>()
            : await _db.EccezioniArea.AsNoTracking().Where(e => e.IdCommerciale == scelto.IdCommerciale && e.Attivo)
                .OrderBy(e => e.CodiceCliente).ToListAsync(ct);
        ViewData["Basi"] = await _db.BasiCalcolo.AsNoTracking().Where(b => b.Attivo).OrderBy(b => b.Nome).ToListAsync(ct);
        ViewData["Clienti"] = await ClientiAsync(ct);
        if (ViewData["Eccezioni"] is List<EccezioneAreaCommerciale> ecc && ecc.Count > 0)
            foreach (var kv in await NomiClientiAsync(ecc.Select(e => e.CodiceCliente).Where(c => !nomi.ContainsKey(c)).ToList(), ct))
                nomi[kv.Key] = kv.Value;
        return View(new ModelloProvvigioni(commerciali, scelto, ultimo, risultato, nomi, righe, utenti, calcoloAttivo, dettaglio));
    }

    /// <summary>Tutte le tabelle della pagina in un file Excel (trimestri, clienti, dettaglio per voce col filtro scelto, movimenti, premio).</summary>
    public async Task<IActionResult> Esporta(int idCommerciale, int? annoDett, int? baseDett, string[]? voci, CancellationToken ct)
    {
        var c = await _db.Commerciali.AsNoTracking().FirstOrDefaultAsync(x => x.IdCommerciale == idCommerciale, ct);
        if (c is null) return NotFound();
        var risultato = await _servizio.CalcolaAsync(idCommerciale, null, ct);
        var righe = await _servizio.RigheTrimestriAsync(idCommerciale, risultato, ct);
        var nomi = await NomiClientiAsync(risultato.Movimenti.Select(m => m.Cliente).Distinct().ToList(), ct);
        var dettaglio = await _servizio.DettaglioPerVoceAsync(idCommerciale, risultato, annoDett, baseDett, voci, ct);
        var convenzioni = await _db.Convenzioni.AsNoTracking().Where(x => x.IdCommerciale == idCommerciale).ToListAsync(ct);
        foreach (var kv in await NomiClientiAsync(convenzioni.Select(x => x.CodiceCliente).Where(x => !nomi.ContainsKey(x)).Distinct().ToList(), ct))
            nomi[kv.Key] = kv.Value;
        var file = _esportatore.Genera(c.Nome, righe, risultato, nomi, dettaglio, convenzioni);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.esportazione.excel", EsitoOperazione.Successo,
            "Commerciale", idCommerciale.ToString(), new { annoDett, baseDett, voci }, ct);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", _esportatore.NomeFile(c.Nome));
    }

    [HttpPost]
    public async Task<IActionResult> Importa(CancellationToken ct)
    {
        var esito = await _importatore.ImportaAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.importazione.azw",
            esito.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            dettaglio: new { esito.Fatture, esito.Incassi, esito.NoteCredito, esito.Messaggio }, ct: ct);
        var nuove = esito.Riuscita ? await _servizio.SincronizzaConvenzioniAsync(ct) : 0;
        TempData["Messaggio"] = esito.Riuscita
            ? $"Importazione riuscita: {esito.Fatture} fatture, {esito.Incassi} incassi, {esito.NoteCredito} note di credito."
              + (nuove > 0 ? $" Nuove convenzioni da ArcLab: {nuove} (da controllare nella schermata Convenzioni)." : "")
              + (esito.Messaggio is { Length: > 0 } avviso ? " Attenzione: " + avviso : "")
            : $"Importazione fallita: {esito.Messaggio}";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Per cliente, chi l'ha segnalato (segnalazioni attive, anche chiuse: «Nome» o «Nome; Nome»).</summary>
    private async Task<Dictionary<string, string>> SegnalatiDaAsync(CancellationToken ct) =>
        (await (from x in _db.Segnalazioni.AsNoTracking()
                join c in _db.CondizioniSegnalatori.AsNoTracking() on x.IdCondizioneSegnalatore equals c.IdCondizioneSegnalatore
                join sg in _db.Segnalatori.AsNoTracking() on c.IdSegnalatore equals sg.IdSegnalatore
                where x.Attivo
                select new { x.CodiceCliente, Testo = sg.Nome + " – " + c.Nome }).ToListAsync(ct))
        .GroupBy(x => x.CodiceCliente, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => string.Join("; ", g.Select(x => x.Testo).Distinct()), StringComparer.Ordinal);

    /// <summary>Condizioni attive dei segnalatori attivi, per le tendine («MARE.A – Segnalazione interna (0%)»).</summary>
    private async Task<List<CondizioneElenco>> CondizioniElencoAsync(CancellationToken ct) =>
        (await (from c in _db.CondizioniSegnalatori.AsNoTracking()
                join sg in _db.Segnalatori.AsNoTracking() on c.IdSegnalatore equals sg.IdSegnalatore
                where c.Attivo && sg.Attivo
                select new { c.IdCondizioneSegnalatore, Segnalatore = sg.Nome, c.Nome, c.Percentuale }).ToListAsync(ct))
        .OrderBy(x => x.Segnalatore == ServizioProvvigioniLab.SegnalatoreAzienda ? 0 : 1).ThenBy(x => x.Segnalatore).ThenBy(x => x.Nome)
        .Select(x => new CondizioneElenco(x.IdCondizioneSegnalatore, x.Segnalatore, $"{x.Segnalatore} – {x.Nome} ({x.Percentuale:0.##}%)"))
        .ToList();

    private async Task<List<ClienteElenco>> ClientiAsync(CancellationToken ct) =>
        (await _db.AzwFatture.AsNoTracking()
            .GroupBy(f => f.CodiceCliente).Select(g => new { Codice = g.Key, Nome = g.Max(x => x.RagioneSociale) })
            .ToListAsync(ct))
        .Select(x => new ClienteElenco(x.Codice, x.Nome ?? "")).OrderBy(c => c.Nome).ToList();

    // ── Eccezioni «intera area» ────────────────────────────────────────

    /// <summary>Per un cliente del commerciale la provvigione si calcola su tutta l'area invece che sulle sole voci delle sue commesse.</summary>
    [HttpPost]
    public async Task<IActionResult> EccezioneAggiungi(int idCommerciale, string codiceCliente, int idBaseCalcolo, DateTime dataInizio, string? motivo, CancellationToken ct)
    {
        codiceCliente = (codiceCliente ?? "").Trim();
        motivo = Tronca(motivo, 500);
        if (codiceCliente.Length == 0 || dataInizio.Year < 2000 || motivo is null)
        {
            TempData["Messaggio"] = "Eccezione non registrata: servono cliente, area, data di inizio e motivo.";
            return RedirectToAction(nameof(Index), new { idCommerciale });
        }
        if (!await _db.AzwFatture.AnyAsync(f => f.CodiceCliente == codiceCliente, ct) || !await _db.BasiCalcolo.AnyAsync(b => b.IdBaseCalcolo == idBaseCalcolo, ct))
        {
            TempData["Messaggio"] = "Cliente o area non validi.";
            return RedirectToAction(nameof(Index), new { idCommerciale });
        }
        var e = new EccezioneAreaCommerciale
        {
            IdCommerciale = idCommerciale, CodiceCliente = codiceCliente, IdBaseCalcolo = idBaseCalcolo,
            DataInizio = dataInizio.Date, Motivo = motivo, CreataDa = User.Identity?.Name
        };
        _db.EccezioniArea.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.eccezione.aggiunta", EsitoOperazione.Successo,
            "EccezioneAreaCommerciale", e.IdEccezioneArea.ToString(), new { idCommerciale, codiceCliente, idBaseCalcolo, dataInizio, motivo }, ct);
        TempData["Messaggio"] = $"Eccezione registrata: per il cliente {codiceCliente} conta tutta l'area dal {dataInizio:dd/MM/yyyy}.";
        return RedirectToAction(nameof(Index), new { idCommerciale });
    }

    /// <summary>Chiude l'eccezione da una data (resta nello storico; i periodi già chiusi non cambiano).</summary>
    [HttpPost]
    public async Task<IActionResult> EccezioneChiudi(int idEccezione, DateTime dataFine, CancellationToken ct)
    {
        var e = await _db.EccezioniArea.FirstOrDefaultAsync(x => x.IdEccezioneArea == idEccezione, ct);
        if (e is null) return NotFound();
        if (dataFine.Date < e.DataInizio)
        {
            // Inserita per errore: si annulla.
            e.Attivo = false;
        }
        else e.DataFine = dataFine.Date;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.eccezione.chiusura", EsitoOperazione.Successo,
            "EccezioneAreaCommerciale", e.IdEccezioneArea.ToString(), new { e.CodiceCliente, dataFine, e.Attivo }, ct);
        TempData["Messaggio"] = e.Attivo ? "Eccezione chiusa." : "Eccezione annullata (data di fine prima dell'inizio).";
        return RedirectToAction(nameof(Index), new { idCommerciale = e.IdCommerciale });
    }

    private async Task<Dictionary<string, string>> NomiClientiAsync(IReadOnlyCollection<string> codici, CancellationToken ct) =>
        await _db.AzwFatture.AsNoTracking().Where(f => codici.Contains(f.CodiceCliente))
            .GroupBy(f => f.CodiceCliente).Select(g => new { Codice = g.Key, Nome = g.Max(x => x.RagioneSociale) })
            .ToDictionaryAsync(x => x.Codice, x => x.Nome ?? "", ct);

    /// <summary>Collega il commerciale a un utente applicativo: quell'utente vedrà solo i propri dati in «Le mie provvigioni».</summary>
    [HttpPost]
    public async Task<IActionResult> CollegaUtente(int idCommerciale, int? idUtente, CancellationToken ct)
    {
        var c = await _db.Commerciali.FirstOrDefaultAsync(x => x.IdCommerciale == idCommerciale, ct);
        if (c is null) return NotFound();
        // Un utente corrisponde a un solo commerciale: il collegamento precedente, se c'è, viene tolto.
        if (idUtente is not null)
        {
            var altri = await _db.Commerciali.Where(x => x.IdUtente == idUtente && x.IdCommerciale != idCommerciale).ToListAsync(ct);
            foreach (var altro in altri) altro.IdUtente = null;
        }
        c.IdUtente = idUtente;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.commerciale.collegamento", EsitoOperazione.Successo,
            "Commerciale", idCommerciale.ToString(), new { idUtente }, ct);
        TempData["Messaggio"] = idUtente is null ? "Collegamento rimosso." : "Utente collegato al commerciale.";
        return RedirectToAction(nameof(Index), new { idCommerciale });
    }

    /// <summary>
    /// Codice della persona in ArcLab (mansione.IdMansione) usato come «rif. del cliente» (commessa.CodCommessa):
    /// riconosce le commesse di consulenza e formazione del commerciale.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImpostaRiferimentoArcLab(int idCommerciale, int? idMansioneArcLab, CancellationToken ct)
    {
        var c = await _db.Commerciali.FirstOrDefaultAsync(x => x.IdCommerciale == idCommerciale, ct);
        if (c is null) return NotFound();
        c.IdMansioneArcLab = idMansioneArcLab;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.commerciale.riferimento", EsitoOperazione.Successo,
            "Commerciale", idCommerciale.ToString(), new { idMansioneArcLab }, ct);
        TempData["Messaggio"] = "Codice di riferimento ArcLab salvato.";
        return RedirectToAction(nameof(Index), new { idCommerciale });
    }

    /// <summary>Chiude il trimestre per tutti i commerciali: i movimenti vengono salvati e non cambiano più.</summary>
    [HttpPost]
    [RichiedePermesso(Permessi.ProvvigioniChiudi)]
    public async Task<IActionResult> ChiudiTrimestre(int anno, int numero, int? idCommerciale, CancellationToken ct)
    {
        try
        {
            var n = await _servizio.ChiudiTrimestreAsync(anno, numero, null, _versione.Versione, ct: ct);
            await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.trimestre.chiusura", EsitoOperazione.Successo,
                "Trimestre", $"{anno}-T{numero}", new { Movimenti = n }, ct);
            TempData["Messaggio"] = $"Trimestre {anno}-T{numero} chiuso: {n} movimenti congelati.";
        }
        catch (InvalidOperationException ex)
        {
            await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.trimestre.chiusura", EsitoOperazione.Errore,
                "Trimestre", $"{anno}-T{numero}", new { ex.Message }, ct);
            TempData["Messaggio"] = "Chiusura non eseguita: " + ex.Message;
        }
        return RedirectToAction(nameof(Index), new { idCommerciale });
    }

    // ── Convenzioni pluriennali (accordo Carloni) ──────────────────────

    public async Task<IActionResult> Convenzioni(int? idCommerciale, CancellationToken ct)
    {
        ViewData["Titolo"] = "Convenzioni";
        await _servizio.InizializzaAsync(ct);
        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).ToListAsync(ct);
        var scelto = commerciali.FirstOrDefault(c => c.IdCommerciale == idCommerciale)
            ?? commerciali.FirstOrDefault(c => c.Nome.Contains("Carloni")) ?? commerciali.FirstOrDefault();

        var voci = scelto is null
            ? new List<ConvenzioneProvvigioni>()
            : await _db.Convenzioni.AsNoTracking().Where(v => v.IdCommerciale == scelto.IdCommerciale && v.Attivo)
                .OrderBy(v => v.CodiceCliente).ThenBy(v => v.DataInizio).ToListAsync(ct);

        var nomi = await NomiClientiAsync(voci.Select(v => v.CodiceCliente).ToList(), ct);
        var righeClienti = await _db.AzwFatture.AsNoTracking()
            .GroupBy(f => f.CodiceCliente).Select(g => new { Codice = g.Key, Nome = g.Max(x => x.RagioneSociale) })
            .ToListAsync(ct);
        var clienti = righeClienti.Select(x => new ClienteElenco(x.Codice, x.Nome ?? "")).OrderBy(c => c.Nome).ToList();

        // Controllo incrociato fra codice venditore AZW e riferimento ArcLab.
        ControlloClienti? controllo = null;
        if (scelto is not null)
        {
            controllo = await _servizio.ControllaClientiAsync(scelto.IdCommerciale, ct);
            var altri = controllo.SoloCodiceVenditore.Concat(controllo.SoloRiferimentoArcLab).Where(x => !nomi.ContainsKey(x)).ToList();
            foreach (var kv in await NomiClientiAsync(altri, ct)) nomi[kv.Key] = kv.Value;
        }
        ViewData["SegnalatiDa"] = await SegnalatiDaAsync(ct);
        ViewData["Condizioni"] = await CondizioniElencoAsync(ct);
        return View(new ModelloConvenzioni(scelto, voci, nomi, clienti, controllo));
    }

    [HttpPost]
    public async Task<IActionResult> ConvenzioneAggiungi(int idCommerciale, string codiceCliente, DateTime dataInizio,
        OrigineConvenzione origine, string? descrizione, CancellationToken ct)
    {
        codiceCliente = (codiceCliente ?? "").Trim();
        if (codiceCliente.Length == 0 || dataInizio.Year < 2000)
        {
            TempData["Messaggio"] = "Dati non validi: servono cliente e data di inizio della convenzione.";
            return RedirectToAction(nameof(Convenzioni), new { idCommerciale });
        }
        if (!await _db.AzwFatture.AnyAsync(f => f.CodiceCliente == codiceCliente, ct))
        {
            TempData["Messaggio"] = $"Il cliente {codiceCliente} non risulta tra le fatture importate da AZW.";
            return RedirectToAction(nameof(Convenzioni), new { idCommerciale });
        }
        if (await _db.Convenzioni.AnyAsync(v => v.IdCommerciale == idCommerciale && v.CodiceCliente == codiceCliente
                && v.DataInizio == dataInizio.Date && v.Attivo, ct))
        {
            TempData["Messaggio"] = "Questa convenzione è già registrata.";
            return RedirectToAction(nameof(Convenzioni), new { idCommerciale });
        }

        var c = new ConvenzioneProvvigioni
        {
            IdCommerciale = idCommerciale, CodiceCliente = codiceCliente, DataInizio = dataInizio.Date,
            Origine = origine, Descrizione = Tronca(descrizione, 200),
            Fonte = FonteConvenzione.Manuale, OrigineConfermata = true
        };
        _db.Convenzioni.Add(c);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.convenzione.aggiunta", EsitoOperazione.Successo,
            "ConvenzioneProvvigioni", c.IdConvenzione.ToString(), new { codiceCliente, dataInizio, origine }, ct);
        TempData["Messaggio"] = "Convenzione registrata.";
        return RedirectToAction(nameof(Convenzioni), new { idCommerciale });
    }

    /// <summary>Toglie la convenzione (nessuna cancellazione fisica: resta ma non è più attiva).</summary>
    [HttpPost]
    public async Task<IActionResult> ConvenzioneRimuovi(int idConvenzione, CancellationToken ct)
    {
        var c = await _db.Convenzioni.FirstOrDefaultAsync(x => x.IdConvenzione == idConvenzione, ct);
        if (c is null) return NotFound();
        c.Attivo = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.convenzione.rimozione", EsitoOperazione.Successo,
            "ConvenzioneProvvigioni", c.IdConvenzione.ToString(), new { c.CodiceCliente, c.DataInizio }, ct);
        return RedirectToAction(nameof(Convenzioni), new { idCommerciale = c.IdCommerciale });
    }

    /// <summary>Corregge data di inizio e origine di una convenzione: da qui in poi l'origine è confermata.</summary>
    [HttpPost]
    public async Task<IActionResult> ConvenzioneModifica(int idConvenzione, DateTime dataInizio, OrigineConvenzione origine, CancellationToken ct)
    {
        var c = await _db.Convenzioni.FirstOrDefaultAsync(x => x.IdConvenzione == idConvenzione, ct);
        if (c is null) return NotFound();
        if (dataInizio.Year < 2000)
        {
            TempData["Messaggio"] = "Data non valida.";
            return RedirectToAction(nameof(Convenzioni), new { idCommerciale = c.IdCommerciale });
        }
        var prima = new { c.DataInizio, c.Origine };
        c.DataInizio = dataInizio.Date;
        c.Origine = origine;
        c.OrigineConfermata = true;
        c.DataDaVerificare = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.convenzione.modifica", EsitoOperazione.Successo,
            "ConvenzioneProvvigioni", c.IdConvenzione.ToString(), new { prima, dopo = new { c.DataInizio, c.Origine } }, ct);
        TempData["Messaggio"] = "Convenzione aggiornata e confermata.";
        return RedirectToAction(nameof(Convenzioni), new { idCommerciale = c.IdCommerciale });
    }

    /// <summary>Crea le convenzioni mancanti dalle commesse continuative di ArcLab (non tocca quelle esistenti).</summary>
    [HttpPost]
    public async Task<IActionResult> ConvenzioniSincronizza(int idCommerciale, CancellationToken ct)
    {
        var n = await _servizio.SincronizzaConvenzioniAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.convenzioni.sincronizzazione", EsitoOperazione.Successo,
            dettaglio: new { Create = n }, ct: ct);
        TempData["Messaggio"] = n > 0 ? $"Create {n} convenzioni dalle commesse continuative di ArcLab." : "Nessuna nuova convenzione da ArcLab.";
        return RedirectToAction(nameof(Convenzioni), new { idCommerciale });
    }

    // ── Elenco "in sviluppo" (voce 3, 2% a obiettivo raggiunto) ─────────

    public async Task<IActionResult> Sviluppo(int? idCommerciale, int? anno, CancellationToken ct)
    {
        ViewData["Titolo"] = "Elenco in sviluppo";
        await _servizio.InizializzaAsync(ct);
        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).ToListAsync(ct);
        var scelto = commerciali.FirstOrDefault(c => c.IdCommerciale == idCommerciale) ?? commerciali.FirstOrDefault();
        var annoScelto = anno ?? DateTime.Today.Year;
        var aggiunto = DateTime.Today.Month > 9 && anno is null ? DateTime.Today.Year + 1 : annoScelto;

        var voci = scelto is null
            ? new List<VoceElencoSviluppo>()
            : await _db.ElencoSviluppo.AsNoTracking()
                .Where(v => v.IdCommerciale == scelto.IdCommerciale && v.Anno == aggiunto && v.Attivo)
                .OrderBy(v => v.CodiceCliente).ToListAsync(ct);

        var nomi = await NomiClientiAsync(voci.Select(v => v.CodiceCliente).ToList(), ct);
        var righeClienti = await _db.AzwFatture.AsNoTracking()
            .GroupBy(f => f.CodiceCliente).Select(g => new { Codice = g.Key, Nome = g.Max(x => x.RagioneSociale) })
            .ToListAsync(ct);
        var clienti = righeClienti.Select(x => new ClienteElenco(x.Codice, x.Nome ?? "")).OrderBy(c => c.Nome).ToList();

        return View(new ModelloSviluppo(commerciali, scelto, aggiunto, voci, nomi, clienti));
    }

    [HttpPost]
    public async Task<IActionResult> SviluppoAggiungi(int idCommerciale, int anno, int trimestre, string codiceCliente,
        decimal obiettivo, string? motivo, string? azione, string? note, CancellationToken ct)
    {
        codiceCliente = (codiceCliente ?? "").Trim();
        if (codiceCliente.Length == 0 || obiettivo <= 0 || trimestre is < 1 or > 4)
        {
            TempData["Messaggio"] = "Dati non validi: servono codice cliente, trimestre (1-4) e obiettivo maggiore di zero.";
            return RedirectToAction(nameof(Sviluppo), new { idCommerciale, anno });
        }
        if (!await _db.AzwFatture.AnyAsync(f => f.CodiceCliente == codiceCliente, ct))
        {
            TempData["Messaggio"] = $"Il cliente {codiceCliente} non risulta tra le fatture di laboratorio importate da AZW.";
            return RedirectToAction(nameof(Sviluppo), new { idCommerciale, anno });
        }
        if (await _db.ElencoSviluppo.AnyAsync(v => v.IdCommerciale == idCommerciale && v.Anno == anno && v.CodiceCliente == codiceCliente && v.Attivo, ct))
        {
            TempData["Messaggio"] = $"Il cliente {codiceCliente} è già nell'elenco {anno}.";
            return RedirectToAction(nameof(Sviluppo), new { idCommerciale, anno });
        }

        var voce = new VoceElencoSviluppo
        {
            IdCommerciale = idCommerciale, CodiceCliente = codiceCliente, Anno = anno,
            ValidoDal = new DateTime(anno, (trimestre - 1) * 3 + 1, 1), Obiettivo = obiettivo,
            Motivo = motivo, AzionePrevista = azione, Note = note
        };
        _db.ElencoSviluppo.Add(voce);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.sviluppo.aggiunta", EsitoOperazione.Successo,
            "VoceElencoSviluppo", voce.IdElencoSviluppo.ToString(), new { codiceCliente, anno, trimestre, obiettivo }, ct);
        TempData["Messaggio"] = "Cliente aggiunto all'elenco (in attesa di approvazione).";
        return RedirectToAction(nameof(Sviluppo), new { idCommerciale, anno });
    }

    [HttpPost]
    public async Task<IActionResult> SviluppoApprova(int idElencoSviluppo, CancellationToken ct)
    {
        var v = await _db.ElencoSviluppo.FirstOrDefaultAsync(x => x.IdElencoSviluppo == idElencoSviluppo, ct);
        if (v is null) return NotFound();
        v.ApprovatoDa = User.Identity?.Name ?? "utente";
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.sviluppo.approvazione", EsitoOperazione.Successo,
            "VoceElencoSviluppo", v.IdElencoSviluppo.ToString(), new { v.CodiceCliente, v.Anno, v.Obiettivo }, ct);
        return RedirectToAction(nameof(Sviluppo), new { idCommerciale = v.IdCommerciale, anno = v.Anno });
    }

    /// <summary>Toglie il cliente dall'elenco (nessuna cancellazione fisica: la voce resta, non più attiva).</summary>
    [HttpPost]
    public async Task<IActionResult> SviluppoRimuovi(int idElencoSviluppo, CancellationToken ct)
    {
        var v = await _db.ElencoSviluppo.FirstOrDefaultAsync(x => x.IdElencoSviluppo == idElencoSviluppo, ct);
        if (v is null) return NotFound();
        v.Attivo = false;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.sviluppo.rimozione", EsitoOperazione.Successo,
            "VoceElencoSviluppo", v.IdElencoSviluppo.ToString(), new { v.CodiceCliente, v.Anno }, ct);
        return RedirectToAction(nameof(Sviluppo), new { idCommerciale = v.IdCommerciale, anno = v.Anno });
    }

    /// <summary>
    /// Carica il file Excel «elenco_in_sviluppo» (foglio «Elenco in sviluppo», intestazione a riga 4):
    /// A = trimestre (es. 2027-T1), B = CODCF, E = motivo, F = azione, J = obiettivo, N = note.
    /// Le righe importate restano in attesa di approvazione. I clienti già in elenco per l'anno sono saltati.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SviluppoImporta(int idCommerciale, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Messaggio"] = "Seleziona un file Excel.";
            return RedirectToAction(nameof(Sviluppo), new { idCommerciale });
        }

        int aggiunte = 0, saltate = 0;
        int? annoUltimo = null;
        try
        {
            await using var flusso = file.OpenReadStream();
            using var libro = new ClosedXML.Excel.XLWorkbook(flusso);
            var foglio = libro.Worksheets.FirstOrDefault(w => w.Name == "Elenco in sviluppo") ?? libro.Worksheet(1);
            var esistenti = await _db.ElencoSviluppo.Where(v => v.IdCommerciale == idCommerciale && v.Attivo)
                .Select(v => v.Anno + "|" + v.CodiceCliente).ToListAsync(ct);
            var presenti = new HashSet<string>(esistenti, StringComparer.Ordinal);

            for (var riga = 5; riga <= foglio.LastRowUsed()?.RowNumber(); riga++)
            {
                var trimestre = foglio.Cell(riga, 1).GetFormattedString().Trim();
                var codice = foglio.Cell(riga, 2).GetFormattedString().Trim();
                if (codice.Length == 0 || trimestre.Length == 0) continue;
                if (!foglio.Cell(riga, 10).TryGetValue<decimal>(out var obiettivo) || obiettivo <= 0) { saltate++; continue; }

                // "2027-T1"
                var parti = trimestre.Split("-T", StringSplitOptions.TrimEntries);
                if (parti.Length != 2 || !int.TryParse(parti[0], out var anno) || !int.TryParse(parti[1], out var tr) || tr is < 1 or > 4)
                { saltate++; continue; }
                if (!presenti.Add(anno + "|" + codice)) { saltate++; continue; }

                _db.ElencoSviluppo.Add(new VoceElencoSviluppo
                {
                    IdCommerciale = idCommerciale, CodiceCliente = codice, Anno = anno,
                    ValidoDal = new DateTime(anno, (tr - 1) * 3 + 1, 1), Obiettivo = obiettivo,
                    Motivo = Tronca(foglio.Cell(riga, 5).GetFormattedString(), 50),
                    AzionePrevista = Tronca(foglio.Cell(riga, 6).GetFormattedString(), 100),
                    Note = Tronca(foglio.Cell(riga, 14).GetFormattedString(), 1000)
                });
                annoUltimo = anno;
                aggiunte++;
            }
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["Messaggio"] = "Impossibile leggere il file: " + ex.Message;
            return RedirectToAction(nameof(Sviluppo), new { idCommerciale });
        }

        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.sviluppo.importazione", EsitoOperazione.Successo,
            dettaglio: new { aggiunte, saltate, file = file.FileName }, ct: ct);
        TempData["Messaggio"] = $"Importate {aggiunte} righe in attesa di approvazione; saltate {saltate} (già presenti o non valide).";
        return RedirectToAction(nameof(Sviluppo), new { idCommerciale, anno = annoUltimo });
    }

    private static string? Tronca(string? v, int max)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}

public sealed record ModelloProvvigioni(
    IReadOnlyList<Commerciale> Commerciali,
    Commerciale? Scelto,
    ImportazioneAzw? UltimaImportazione,
    RisultatoProvvigioniLab? Risultato,
    IReadOnlyDictionary<string, string> NomiClienti,
    IReadOnlyList<RigaTrimestre> Trimestri,
    IReadOnlyList<UtenteElenco> Utenti,
    bool CalcoloAttivo,
    DettaglioVoci? Dettaglio);

public sealed record UtenteElenco(int IdUtente, string Nome, string Email);

public sealed record ModelloSviluppo(
    IReadOnlyList<Commerciale> Commerciali,
    Commerciale? Scelto,
    int Anno,
    IReadOnlyList<VoceElencoSviluppo> Voci,
    IReadOnlyDictionary<string, string> NomiClienti,
    IReadOnlyList<ClienteElenco> Clienti);

public sealed record ClienteElenco(string Codice, string Nome);

public sealed record CondizioneElenco(int IdCondizione, string Segnalatore, string Testo);

public sealed record ModelloConvenzioni(
    Commerciale? Scelto,
    IReadOnlyList<ConvenzioneProvvigioni> Voci,
    IReadOnlyDictionary<string, string> NomiClienti,
    IReadOnlyList<ClienteElenco> Clienti,
    ControlloClienti? Controllo);
