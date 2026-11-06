using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Consulenza;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Preventivi/contratti di consulenza e formazione (serie C). Le attività sono le descrizioni incarico di ArcLab
/// (sola lettura); anagrafica cliente da ArcLab con SDI e PEC da AZW, oppure inserita dal consulente.
/// Il contratto rinvia alle Condizioni Generali di consulenza; dal contratto accettato si compila la commessa in ArcLab.
/// Nessuna scrittura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.PreventiviConsulenza)]
public class PreventiviConsulenzaController : Controller
{
    private const string ModelloCartaIntestata = "docx/Carta_intestata_Marea.docx";

    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioPreventiviConsulenza _servizio;
    private readonly IRepositoryConsulenze _arclab;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IWebHostEnvironment _ambiente;
    private readonly IRepositoryControlli _controlli;

    public PreventiviConsulenzaController(ContestoLaboratorioAI db, ServizioPreventiviConsulenza servizio, IRepositoryConsulenze arclab,
        IRepositoryAnagrafiche anagrafiche, IContestoUtente utente, IServizioAudit audit, IWebHostEnvironment ambiente,
        IRepositoryControlli controlli)
    {
        _db = db; _servizio = servizio; _arclab = arclab; _anagrafiche = anagrafiche; _utente = utente; _audit = audit; _ambiente = ambiente;
        _controlli = controlli;
    }

    // ── Elenco ────────────────────────────────────────────────────────

    public async Task<IActionResult> Index(int? anno, string? stato, CancellationToken ct)
    {
        ViewData["Titolo"] = "Preventivi consulenza";
        var a = anno ?? DateTime.Today.Year;
        var q = _db.PreventiviConsulenza.AsNoTracking().Include(p => p.Blocchi).Where(p => p.Attivo && p.Anno == a);
        if (Enum.TryParse<StatoPreventivoConsulenza>(stato, out var st)) q = q.Where(p => p.Stato == st);
        var elenco = await q.OrderByDescending(p => p.Numero).ToListAsync(ct);
        var anni = await _db.PreventiviConsulenza.AsNoTracking().Where(p => p.Attivo).Select(p => p.Anno).Distinct().OrderByDescending(x => x).ToListAsync(ct);
        if (!anni.Contains(a)) anni.Insert(0, a);
        ViewData["SenzaAttivita"] = !await _db.AttivitaConsulenza.AnyAsync(ct);
        // 29/10/2026: redattore = utente che ha creato il preventivo (IdUtenteConsulente è impostato alla creazione e non cambia).
        var idRedattori = elenco.Where(p => p.IdUtenteConsulente != null).Select(p => p.IdUtenteConsulente!.Value).Distinct().ToList();
        var redattori = await _db.Utenti.AsNoTracking().Where(u => idRedattori.Contains(u.IdUtente))
            .ToDictionaryAsync(u => u.IdUtente, u => u.NomeCompleto, ct);
        return View(new ModelloElencoPreventiviConsulenza(elenco, a, anni, stato) { Redattori = redattori });
    }

    [HttpPost]
    public async Task<IActionResult> Nuovo(CancellationToken ct)
    {
        var anno = DateTime.Today.Year;
        int? idOrganico = _utente.IdUtente is int idu
            ? await _db.ConsulentiUtenti.AsNoTracking().Where(x => x.IdUtente == idu).Select(x => (int?)x.IdOrganicoArcLab).FirstOrDefaultAsync(ct)
            : null;
        string? nomeConsulente = _utente.NomeCompleto;
        if (idOrganico is int io)
            nomeConsulente = (await _arclab.ConsulentiAsync(ct)).FirstOrDefault(c => c.IdOrganico == io)?.Nome ?? nomeConsulente;

        var p = new PreventivoConsulenza
        {
            Anno = anno, Numero = await _servizio.ProssimoNumeroAsync(anno, ct), DataOfferta = DateTime.Today,
            RevisioneCondizioni = await _servizio.RevisioneCondizioniAsync(ct),
            IdUtenteConsulente = _utente.IdUtente, IdOrganicoConsulente = idOrganico, ConsulenteNome = Tronca(nomeConsulente, 100),
            CondizioniPagamento = await _db.MetodiPagamento.AsNoTracking().Where(m => m.Attivo).OrderBy(m => m.Ordine).Select(m => m.Descrizione).FirstOrDefaultAsync(ct),
            DataCreazione = DateTime.UtcNow, DataUltimaModifica = DateTime.UtcNow
        };
        _db.PreventiviConsulenza.Add(p);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.crea", EsitoOperazione.Successo,
            "PreventivoConsulenza", p.IdPreventivoConsulenza.ToString(), new { p.Anno, p.Numero }, ct);
        return RedirectToAction(nameof(Modifica), new { id = p.IdPreventivoConsulenza });
    }

    // ── Composizione ─────────────────────────────────────────────────

    public async Task<IActionResult> Modifica(int id, CancellationToken ct)
    {
        var p = await CaricaAsync(id, true, ct);
        if (p is null) return NotFound();
        ViewData["Titolo"] = "Preventivo " + p.NumeroCompleto;

        await _servizio.AssicuraPersonalizzataAsync(ct);   // 29/10/2026
        var attivita = await _db.AttivitaConsulenza.AsNoTracking().Include(a => a.Contenuti)
            .OrderBy(a => a.IdTipoCommessa).ThenBy(a => a.Ordine).ToListAsync(ct);
        var pacchetti = await _db.PacchettiConsulenza.AsNoTracking().Where(x => x.Attivo).OrderBy(x => x.Nome).ToListAsync(ct);
        var pagamenti = await _db.MetodiPagamento.AsNoTracking().Where(m => m.Attivo).OrderBy(m => m.Ordine).Select(m => m.Descrizione).ToListAsync(ct);
        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).ToListAsync(ct);
        IReadOnlyList<ConsulenteArcLab> consulenti = Array.Empty<ConsulenteArcLab>();
        IReadOnlyList<InsediamentoRapportino> sedi = Array.Empty<InsediamentoRapportino>();
        IReadOnlyList<ContattoArcLab> contatti = Array.Empty<ContattoArcLab>();
        string? erroreArcLab = null;
        try
        {
            consulenti = await _arclab.ConsulentiAsync(ct);
            if (p.IdClienteArcLab is int idc) sedi = await _arclab.InsediamentiAsync(idc, ct);
            if (p.IdInsediamentoArcLab is int idi) contatti = await _arclab.ContattiAsync(idi, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            erroreArcLab = "ArcLab non raggiungibile: " + ex.Message;
        }
        var commerciale = p.IdCommerciale is int ic ? commerciali.FirstOrDefault(c => c.IdCommerciale == ic) : null;
        var commesse = ServizioPreventiviConsulenza.CommesseDaCompilare(p, commerciale?.IdMansioneArcLab);
        var commesseAnalisi = ServizioPreventiviConsulenza.CommesseAnalisiDaCompilare(p);
        return View(new ModelloPreventivoConsulenza(p, attivita, pacchetti, pagamenti, commerciali, consulenti, sedi, contatti, commesse, commesseAnalisi, erroreArcLab));
    }

    /// <summary>
    /// Salva tutto il modulo e poi esegue l'azione richiesta: aggiungere un'attività o un pacchetto, togliere una riga,
    /// caricare i dati del cliente, della sede o del referente da ArcLab.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Salva(int id, FormPreventivoConsulenza f, string? azione, CancellationToken ct)
    {
        var p = await CaricaAsync(id, false, ct);
        if (p is null) return NotFound();
        if (!Modificabile(p))
        {
            TempData["Messaggio"] = "Il preventivo è " + p.Stato.ToString().ToLowerInvariant() + ": non si modifica più.";
            return RedirectToAction(nameof(Modifica), new { id });
        }
        var organicoPrima = p.IdOrganicoConsulente;
        AggiornaDaModulo(p, f);
        if (p.IdOrganicoConsulente is int io && io != organicoPrima)
        {
            try { p.ConsulenteNome = Tronca((await _arclab.ConsulentiAsync(ct)).FirstOrDefault(c => c.IdOrganico == io)?.Nome, 100) ?? p.ConsulenteNome; }
            catch (Exception ex) when (ex is not OperationCanceledException) { /* ArcLab non raggiungibile: resta il nome precedente */ }
        }
        var ancora = "";
        string? messaggio = null;
        azione ??= "salva";
        var progressivoPrima = p.Righe.Select(x => x.Progressivo).DefaultIfEmpty(0).Max();

        if (azione == "aggiungi" && f.IdAttivitaNuova is int ida)
        {
            var a = await _db.AttivitaConsulenza.AsNoTracking().Include(x => x.Contenuti).FirstOrDefaultAsync(x => x.IdAttivitaConsulenza == ida, ct);
            if (a is not null)
            {
                var r = ServizioPreventiviConsulenza.NuovaRiga(a);
                r.Progressivo = p.Righe.Select(x => x.Progressivo).DefaultIfEmpty(0).Max() + 1;
                p.Righe.Add(r);
                ancora = "#riga-" + r.Progressivo;
            }
        }
        else if (azione == "pacchetto" && f.IdPacchetto is int idp)
        {
            var (n, na, errore) = await _servizio.ApplicaPacchettoAsync(p, idp, ct);
            messaggio = $"Aggiunte dal pacchetto {n} attività e {na} analisi: controlla contenuti e prezzi." + (errore is null ? "" : " " + errore);
            ancora = "#attivita";
        }
        else if (azione == "aggiungi-blocco")
        {
            var b = ServizioPreventiviConsulenza.AggiungiBlocco(p);
            messaggio = $"Aggiunto il blocco {b.Numero}: scrivi titolo e descrizione e assegna le attività con il menu «Blocco» di ogni riga (le nuove attività vanno nell'ultimo blocco).";
            ancora = "#blocchi";
        }
        else if (azione.StartsWith("togli-blocco:") && int.TryParse(azione[13..], out var nbt))
        {
            var b = ServizioPreventiviConsulenza.TogliBlocco(p, nbt);
            if (b is not null && b.IdBloccoPreventivoConsulenza > 0) _db.BlocchiPreventivoConsulenza.Remove(b);
            if (b is not null) messaggio = $"Tolto il blocco {nbt}: le sue attività sono passate al blocco precedente.";
            ancora = "#blocchi";
        }
        else if (azione.StartsWith("rimuovi:") && int.TryParse(azione[8..], out var prog))
        {
            var r = p.Righe.FirstOrDefault(x => x.Progressivo == prog);
            if (r is not null) { p.Righe.Remove(r); _db.RighePreventivoConsulenza.Remove(r); }
            ancora = "#attivita";
        }
        else if (azione == "aggiungi-controllo")
        {
            ancora = "#analisi";
            var idc = f.IdControlloNuovo ?? IdDaTestoControllo(f.ControlloCercato);
            if (idc is int idCtrl)
            {
                try
                {
                    var an = await _servizio.ComponiAnalisiAsync(new[] { idCtrl }, null, p.IdClienteArcLab, ModalitaAnalisiConsulenza.APagamento, null, null, ct);
                    ServizioPreventiviConsulenza.AggiungiAnalisi(p, an);
                    messaggio = $"Aggiunta l'analisi «{an.Descrizione}» (ID {idCtrl}).";
                }
                catch (Exception ex) when (ex is not OperationCanceledException) { messaggio = "ArcLab non raggiungibile: " + ex.Message; }
            }
            else messaggio = "Scegli il controllo dall'elenco (scrivi almeno 2 lettere del nome o l'ID).";
        }
        else if (azione.StartsWith("rimuovi-analisi:") && int.TryParse(azione[16..], out var progA))
        {
            var a = p.Analisi.FirstOrDefault(x => x.Progressivo == progA);
            if (a is not null) { p.Analisi.Remove(a); _db.AnalisiPreventiviConsulenza.Remove(a); }
            ancora = "#analisi";
        }
        else if (azione == "raggruppa")
        {
            ancora = "#analisi";
            var scelte = (f.Analisi ?? new()).Where(x => x.Selezionata).Select(x => x.Progressivo).ToList();
            var prima = p.Analisi.ToList();
            var g = ServizioPreventiviConsulenza.Raggruppa(p, scelte, f.NomeGruppo);
            if (g is null) messaggio = "Spunta almeno due analisi da raggruppare.";
            else
            {
                foreach (var a in prima.Where(a => !p.Analisi.Contains(a))) _db.AnalisiPreventiviConsulenza.Remove(a);
                messaggio = $"Raggruppate {scelte.Count} analisi in «{g.Descrizione}» ({AnalisiConsulenza.Riferimento(g.IdControlloArcLab, g.IdControlliOrigine)}).";
            }
        }
        else if (azione == "prezzi-analisi")
        {
            ancora = "#analisi";
            try { messaggio = $"Prezzi delle analisi ricalcolati: {await _servizio.AggiornaPrezziAnalisiAsync(p, ct)} cambiati (quelli scritti a mano restano)."; }
            catch (Exception ex) when (ex is not OperationCanceledException) { messaggio = "ArcLab non raggiungibile: " + ex.Message; }
        }
        else if (azione == "cliente")
        {
            ancora = "#cliente";
            var (idCli, avviso) = f.IdClienteScelto is int scelto ? (scelto, null) : await RisolviClienteAsync(f.ClienteCercato, ct);
            if (idCli is int idc)
            {
                messaggio = await CaricaClienteAsync(p, idc, ct);
                // Con una sola sede in ArcLab la si imposta subito.
                var sedi = await _arclab.InsediamentiAsync(idc, ct);
                if (sedi.Count == 1)
                {
                    ImpostaSede(p, sedi[0]);
                    messaggio += " Sede impostata (il cliente ne ha una sola): scegli il referente.";
                }
                else if (sedi.Count == 0)
                    messaggio += " In ArcLab il cliente non ha sedi: compila a mano nome e indirizzo della sede.";
                if (p.Analisi.Count > 0)
                {
                    try
                    {
                        var cambiate = await _servizio.AggiornaPrezziAnalisiAsync(p, ct);
                        if (cambiate > 0) messaggio += $" Prezzi di {cambiate} analisi aggiornati con quelli di commessa del cliente.";
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException) { /* restano i prezzi precedenti */ }
                }
            }
            else messaggio = avviso;
        }
        else if (azione == "sede" && p.IdClienteArcLab is int idCl && f.IdInsediamentoScelto is int idIns)
        {
            var s = (await _arclab.InsediamentiAsync(idCl, ct)).FirstOrDefault(x => x.IdInsediamento == idIns);
            if (s is not null) ImpostaSede(p, s);
            ancora = "#cliente";
        }
        else if (azione == "referente" && p.IdInsediamentoArcLab is int idS && f.IdContattoScelto is int idCo)
        {
            var c = (await _arclab.ContattiAsync(idS, ct)).FirstOrDefault(x => x.IdContatto == idCo);
            if (c is not null)
            {
                p.IdContattoArcLab = c.IdContatto;
                p.ReferenteNome = Tronca(c.NomeCompleto, 100);
                p.ReferenteMansione = Tronca(c.Mansione, 100);
                p.ReferenteTelefono = Tronca(string.IsNullOrWhiteSpace(c.Telefono) ? c.Cellulare : c.Telefono, 30);
                p.ReferenteEmail = Tronca(c.Email, 250);
            }
            ancora = "#cliente";
        }

        p.CommercialeNome = p.IdCommerciale is int idCom
            ? Tronca(await _db.Commerciali.AsNoTracking().Where(c => c.IdCommerciale == idCom).Select(c => c.Nome).FirstOrDefaultAsync(ct), 100)
            : null;
        // 29/10/2026: le attività appena aggiunte (singole o da pacchetto) vanno nell'ultimo blocco
        if (p.Blocchi.Count > 0)
        {
            var ultimo = p.Blocchi.Max(x => x.Numero);
            foreach (var r in p.Righe.Where(r => r.Progressivo > progressivoPrima)) r.Blocco = ultimo;
        }
        ServizioPreventiviConsulenza.Ricalcola(p);
        p.DataUltimaModifica = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = messaggio ?? "Salvato.";
        return Redirect(Url.Action(nameof(Modifica), new { id })! + ancora);
    }


    private static void ImpostaSede(PreventivoConsulenza p, InsediamentoRapportino s)
    {
        p.IdInsediamentoArcLab = s.IdInsediamento;
        p.SedeNome = Tronca(s.Nome, 255);
        p.SedeIndirizzo = Tronca(s.Indirizzo, 500);
        if (string.IsNullOrWhiteSpace(p.Email)) p.Email = Tronca(s.Email, 250);
        p.IdContattoArcLab = null;
    }

    /// <summary>
    /// Trova il cliente ArcLab dal testo scritto nella ricerca: prima il nome identico
    /// (senza badare a maiuscole e spazi), poi l'unico nome che lo contiene.
    /// </summary>
    private async Task<(int? Id, string Avviso)> RisolviClienteAsync(string? testo, CancellationToken ct)
    {
        var t = (testo ?? "").Trim();
        var m = System.Text.RegularExpressions.Regex.Match(t, @"\(n\.\s*(\d+)\)\s*$");
        if (m.Success) return (int.Parse(m.Groups[1].Value), "");
        if (t.Length < 2) return (null, "Scrivi almeno 2 lettere della ragione sociale nella ricerca cliente e scegli dall'elenco.");
        var clienti = await _anagrafiche.ClientiAsync(ct);
        var uguali = clienti.Where(c => string.Equals(c.Nome?.Trim(), t, StringComparison.OrdinalIgnoreCase)).ToList();
        if (uguali.Count == 1) return (uguali[0].Id, "");
        var simili = uguali.Count > 1 ? uguali
            : clienti.Where(c => (c.Nome ?? "").Contains(t, StringComparison.OrdinalIgnoreCase)).ToList();
        if (simili.Count == 1) return (simili[0].Id, "");
        if (simili.Count == 0) return (null, $"Nessun cliente ArcLab contiene \"{t}\". Se è un cliente nuovo compila i campi a mano.");
        var nomi = string.Join("; ", simili.Take(8).Select(c => $"{c.Nome?.Trim()} (n. {c.Id})"));
        return (null, $"Ci sono {simili.Count} clienti che corrispondono a \"{t}\": {nomi}{(simili.Count > 8 ? "; …" : "")}. Scegli quello giusto dall'elenco a tendina e premi di nuovo il pulsante.");
    }

    private async Task<string> CaricaClienteAsync(PreventivoConsulenza p, int idCliente, CancellationToken ct)
    {
        var c = await _arclab.AnagraficaClienteAsync(idCliente, ct);
        if (c is null) return "Cliente non trovato in ArcLab.";
        p.IdClienteArcLab = c.IdCliente;
        p.CodiceClienteAzw = Tronca(c.CodiceCliente, 20);
        p.RagioneSociale = Tronca(c.RagioneSociale, 200);
        p.PartitaIva = Tronca(c.PartitaIva, 20);
        p.CodiceFiscale = Tronca(c.CodiceFiscale, 20);
        p.Indirizzo = Tronca(c.Indirizzo, 255);
        p.Cap = Tronca(c.Cap, 10);
        p.Citta = Tronca(c.Citta, 100);
        p.Provincia = Tronca(c.Provincia, 4);
        p.Email = Tronca(c.Email, 250);
        p.Telefono = null;
        p.IdInsediamentoArcLab = null; p.SedeNome = null; p.SedeIndirizzo = null;
        p.IdContattoArcLab = null; p.ReferenteNome = null; p.ReferenteMansione = null; p.ReferenteTelefono = null; p.ReferenteEmail = null;

        // SDI, PEC e dati mancanti da AZW (anagrafica clienti ANACF, copiata da Importa in Provvigioni).
        // Collegamento: codice cliente ArcLab, poi codici AZW delle sue commesse, poi partita IVA, poi codice fiscale.
        if (!await _db.AzwClienti.AnyAsync(ct))
            return "Dati del cliente caricati da ArcLab. L'anagrafica AZW non è ancora stata importata: vai in Amministrazione → Provvigioni, premi Importa e poi ricarica il cliente (oppure inserisci SDI e PEC a mano).";
        var (azw, codici) = await LaboratorioAI.Dati.Consulenza.AnagraficaAzw.TrovaAsync(_db, c, ct);
        if (azw is null)
            return "Dati del cliente caricati da ArcLab. In AZW non l'ho trovato (codici " + (codici.Count == 0 ? "—" : string.Join(", ", codici)) + ", P.IVA " + (c.PartitaIva ?? "—") + "): SDI e PEC vanno inseriti a mano.";
        p.CodiceClienteAzw = Tronca(azw.CodiceCliente, 20);
        p.ZonaCliente = azw.Zona;
        string? avvisoZona = null;
        if (string.IsNullOrWhiteSpace(azw.Zona)) { p.ResponsabileCliente = null; avvisoZona = " In AZW il cliente non ha la zona: scrivi a mano il responsabile cliente."; }
        else
        {
            var resp = await _db.ResponsabiliCliente.AsNoTracking().FirstOrDefaultAsync(x => x.Sigla == azw.Zona && x.Attivo, ct);
            p.ResponsabileCliente = Tronca(resp?.Nome, 100);
            if (string.IsNullOrWhiteSpace(p.ResponsabileCliente))
                avvisoZona = $" La zona AZW «{azw.Zona}» non ha ancora un nome in Amministrazione → Responsabili clienti: scrivi a mano il responsabile cliente o completa la tabella.";
        }
        p.Pec = azw.Pec;
        p.CodiceSdi = azw.CodiceSdi;
        p.Telefono = Tronca(azw.Telefono, 50);   // 29/10/2026
        p.PartitaIva ??= azw.PartitaIva;
        p.CodiceFiscale ??= azw.CodiceFiscale;
        p.Indirizzo ??= azw.Indirizzo; p.Cap ??= azw.Cap; p.Citta ??= azw.Citta; p.Provincia ??= azw.Provincia;
        p.Email ??= azw.Email;
        var mancanti = new List<string>();
        if (string.IsNullOrWhiteSpace(azw.CodiceSdi)) mancanti.Add("codice SDI");
        if (string.IsNullOrWhiteSpace(azw.Pec)) mancanti.Add("PEC");
        return $"Dati del cliente caricati da ArcLab e AZW (codice AZW {azw.CodiceCliente})."
            + (mancanti.Count > 0 ? " In AZW manca: " + string.Join(" e ", mancanti) + ", da inserire a mano." : "")
            + (avvisoZona ?? $" Responsabile cliente: {p.ResponsabileCliente}.")
            + " Scegli ora la sede del servizio.";
    }

    private static void AggiornaDaModulo(PreventivoConsulenza p, FormPreventivoConsulenza f)
    {
        p.RagioneSociale = Tronca(f.RagioneSociale, 200);
        p.PartitaIva = Tronca(f.PartitaIva, 20);
        p.CodiceFiscale = Tronca(f.CodiceFiscale, 20);
        p.Indirizzo = Tronca(f.Indirizzo, 255);
        p.Cap = Tronca(f.Cap, 10);
        p.Citta = Tronca(f.Citta, 100);
        p.Provincia = Tronca(f.Provincia?.ToUpperInvariant(), 4);
        p.Email = Tronca(f.Email, 250);
        p.Pec = Tronca(f.Pec, 250);
        p.CodiceSdi = Tronca(f.CodiceSdi?.ToUpperInvariant(), 10);
        p.Telefono = Tronca(f.Telefono, 50);
        p.SedeNome = Tronca(f.SedeNome, 255);
        p.SedeIndirizzo = Tronca(f.SedeIndirizzo, 500);
        p.ReferenteNome = Tronca(f.ReferenteNome, 100);
        p.ReferenteMansione = Tronca(f.ReferenteMansione, 100);
        p.ReferenteTelefono = Tronca(f.ReferenteTelefono, 30);
        p.ReferenteEmail = Tronca(f.ReferenteEmail, 250);
        p.SettoreAttivita = Tronca(f.SettoreAttivita, 100);
        p.Oggetto = Tronca(f.Oggetto, 500);
        p.DataOfferta = f.DataOfferta ?? p.DataOfferta;
        p.ValiditaGiorni = f.ValiditaGiorni is > 0 ? f.ValiditaGiorni.Value : 30;
        p.DataInizio = f.DataInizio;
        p.DurataAnni = f.DurataAnni is > 0 ? f.DurataAnni.Value : 2;
        p.NumeroRate = f.NumeroRate is > 0 ? f.NumeroRate.Value : 1;
        p.CondizioniPagamento = Tronca(f.CondizioniPagamento, 500);
        p.IdOrganicoConsulente = f.IdOrganicoConsulente ?? p.IdOrganicoConsulente;
        p.IdCommerciale = f.IdCommerciale;
        p.ResponsabileCliente = Tronca(f.ResponsabileCliente, 100);
        p.Note = Tronca(f.Note, 2000);
        // 29/10/2026: blocchi di quotazione (titolo e descrizione liberi)
        foreach (var bf in f.Blocchi ?? new List<BloccoFormConsulenza>())
        {
            var b = p.Blocchi.FirstOrDefault(x => x.Numero == bf.Numero);
            if (b is null) continue;
            b.Titolo = Tronca(bf.Titolo, 200);
            b.Descrizione = string.IsNullOrWhiteSpace(bf.Descrizione) ? null : Tronca(bf.Descrizione, 4000);
        }

        foreach (var rf in f.Righe ?? new List<RigaFormConsulenza>())
        {
            var r = p.Righe.FirstOrDefault(x => x.Progressivo == rf.Progressivo);
            if (r is null) continue;
            r.DescrizioneLibera = Tronca(rf.DescrizioneLibera, 1000);
            r.TestoOfferta = string.IsNullOrWhiteSpace(rf.TestoOfferta) ? null : rf.TestoOfferta.Trim();
            r.TestoSuccessivi = string.IsNullOrWhiteSpace(rf.TestoSuccessivi) ? null : rf.TestoSuccessivi.Trim();
            var scelti = (rf.Contenuti ?? new List<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToList();
            r.Contenuti = scelti.Count > 0 ? Tronca(string.Join('\n', scelti), 4000) : null;
            r.ModalitaPrezzo = rf.ModalitaPrezzo ?? r.ModalitaPrezzo;
            r.Quantita = AccordiProvvigioniController.Decimale(rf.Quantita) is decimal q && q > 0 ? q : 1m;
            r.Prezzo = AccordiProvvigioniController.Decimale(rf.Prezzo) ?? 0m;
            r.PrezzoSuccessivi = r.ModalitaPrezzo == ModalitaPrezzoConsulenza.Annuale ? AccordiProvvigioniController.Decimale(rf.PrezzoSuccessivi) : null;
            r.Informativa = rf.Informativa;
            if (rf.Blocco is int nb && p.Blocchi.Any(x => x.Numero == nb)) r.Blocco = nb;
            // 29/10/2026: attività personalizzata, titolo e unità liberi
            if (r.Personalizzata)
            {
                if (!string.IsNullOrWhiteSpace(rf.DescrizioneAttivita)) r.DescrizioneAttivita = Tronca(rf.DescrizioneAttivita, 255)!;
                r.UnitaMisura = Tronca(rf.UnitaMisura, 30);
            }
        }

        foreach (var af in f.Analisi ?? new List<AnalisiFormConsulenza>())
        {
            var a = p.Analisi.FirstOrDefault(x => x.Progressivo == af.Progressivo);
            if (a is null) continue;
            if (!string.IsNullOrWhiteSpace(af.Descrizione)) a.Descrizione = Tronca(af.Descrizione, 300)!;
            a.Modalita = af.Modalita ?? a.Modalita;
            a.QuantitaAnnua = AccordiProvvigioniController.Decimale(af.QuantitaAnnua) is decimal q && q > 0 ? q : null;
            a.Note = Tronca(af.Note, 500);
            // Prezzo vuoto = torna al prezzo calcolato (commessa del cliente o listino); diverso dal calcolato = scritto a mano.
            var prezzo = AccordiProvvigioniController.Decimale(af.Prezzo);
            if (prezzo is null)
            {
                if (a.OriginePrezzo == OriginePrezzoPreventivo.Manuale) { a.Prezzo = a.PrezzoBase; a.OriginePrezzo = OriginePrezzoPreventivo.Listino; }
            }
            else if (prezzo.Value != a.Prezzo)
            {
                a.Prezzo = prezzo.Value;
                a.OriginePrezzo = prezzo.Value == a.PrezzoBase ? OriginePrezzoPreventivo.Listino : OriginePrezzoPreventivo.Manuale;
            }
        }
    }

    /// <summary>Id del controllo dal testo scelto nella ricerca: "Nome (ID 123)" oppure solo il numero.</summary>
    internal static int? IdDaTestoControllo(string? testo)
    {
        var t = (testo ?? "").Trim();
        var m = System.Text.RegularExpressions.Regex.Match(t, @"\(ID\s*(\d+)\)\s*$");
        if (m.Success) return int.Parse(m.Groups[1].Value);
        return int.TryParse(t, out var n) && n > 0 ? n : null;
    }

    // ── Stato e documento ────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CambiaStato(int id, StatoPreventivoConsulenza stato, CancellationToken ct)
    {
        var p = await CaricaAsync(id, false, ct);
        if (p is null) return NotFound();
        var prima = p.Stato;
        if (stato == StatoPreventivoConsulenza.Accettato)
        {
            if (p.Righe.All(r => r.Informativa))
            {
                TempData["Messaggio"] = "Serve almeno un'attività (non solo prezzi «per conoscenza») per accettare il contratto.";
                return RedirectToAction(nameof(Modifica), new { id });
            }
            p.DataAccettazione = DateTime.Today;
            p.DataInizio ??= DateTime.Today;
        }
        p.Stato = stato;
        p.DataUltimaModifica = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.stato", EsitoOperazione.Successo,
            "PreventivoConsulenza", id.ToString(), new { prima, dopo = stato }, ct);
        TempData["Messaggio"] = stato == StatoPreventivoConsulenza.Accettato
            ? "Contratto accettato: in fondo alla pagina trovi i dati per compilare le commesse in ArcLab."
            : "Stato aggiornato.";
        return Redirect(Url.Action(nameof(Modifica), new { id })! + (stato == StatoPreventivoConsulenza.Accettato ? "#commessa" : ""));
    }

    public async Task<IActionResult> Documento(int id, CancellationToken ct)
    {
        var p = await CaricaAsync(id, true, ct);
        if (p is null) return NotFound();
        var modello = Path.Combine(_ambiente.WebRootPath, ModelloCartaIntestata);
        if (!System.IO.File.Exists(modello)) return NotFound("Modello di carta intestata non trovato: " + ModelloCartaIntestata);
        var contenuto = ContrattoConsulenzaDocx.Genera(p, modello);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.documento", EsitoOperazione.Successo,
            "PreventivoConsulenza", id.ToString(), new { p.Anno, p.Numero }, ct);
        return File(contenuto, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ContrattoConsulenzaDocx.NomeFile(p));
    }

    /// <summary>Contratto in PDF (stesso contenuto del Word, su carta intestata).</summary>
    public async Task<IActionResult> DocumentoPdf(int id, CancellationToken ct)
    {
        var p = await CaricaAsync(id, true, ct);
        if (p is null) return NotFound();
        var modello = Path.Combine(_ambiente.WebRootPath, ModelloCartaIntestata);
        if (!System.IO.File.Exists(modello)) return NotFound("Modello di carta intestata non trovato: " + ModelloCartaIntestata);
        var contenuto = ContrattoConsulenzaDocx.GeneraPdf(p, modello, _ambiente.WebRootPath);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.documento.pdf", EsitoOperazione.Successo,
            "PreventivoConsulenza", id.ToString(), new { p.Anno, p.Numero }, ct);
        return File(contenuto, "application/pdf", ContrattoConsulenzaDocx.NomeFilePdf(p));
    }

    /// <summary>Clienti ArcLab per la ricerca nel modulo.</summary>
    [HttpGet]
    public async Task<IActionResult> Clienti(string? filtro, CancellationToken ct)
    {
        var clienti = await _anagrafiche.ClientiAsync(ct);
        if (!string.IsNullOrWhiteSpace(filtro))
            clienti = clienti.Where(c => c.Nome.Contains(filtro.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        return Json(clienti.Take(30).Select(c => new { id = c.Id, nome = (c.Nome ?? "").Trim() }));
    }

    /// <summary>
    /// Controlli ArcLab attivi per la ricerca nelle analisi. Senza filtro restituisce tutti i controlli attivi
    /// (la pagina li carica una volta e filtra mentre si scrive); con il filtro, quelli che contengono tutte le parole o l'ID.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Controlli(string? filtro, CancellationToken ct)
    {
        var t = (filtro ?? "").Trim();
        IEnumerable<VoceControlloEsistente> trovati = await _controlli.ControlliAttiviAsync(ct);
        if (t.Length > 0)
        {
            var parole = t.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            trovati = trovati.Where(c => parole.All(w => (c.Controllo ?? "").Contains(w, StringComparison.OrdinalIgnoreCase)
                                                        || c.IdControllo.ToString() == w));
        }
        return Json(trovati.OrderBy(c => (c.Controllo ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(c => new { id = c.IdControllo, controllo = (c.Controllo ?? "").Trim(), nome = $"{(c.Controllo ?? "").Trim()} (ID {c.IdControllo})" }));
    }

    /// <summary>Parametri e metodi di un controllo ArcLab, per vederli prima di inserirlo.</summary>
    [HttpGet]
    public async Task<IActionResult> MetodiControllo(int id, CancellationToken ct) =>
        Json((await _controlli.RigheDiControlloAsync(id, ct)).Select(m => new
        {
            denominazione = m.DettagliMetodo ?? m.Descrizione ?? m.DenominazioneRiga, metodo = m.RifBiblio, accreditato = m.Accreditato,
            labEsterno = m.LabEsterno, costo = m.Costo
        }));

    // ── Amministrazione: attività ────────────────────────────────────

    public async Task<IActionResult> Attivita(CancellationToken ct)
    {
        ViewData["Titolo"] = "Attività di consulenza";
        var elenco = await _db.AttivitaConsulenza.AsNoTracking().Include(a => a.Contenuti)
            .OrderByDescending(a => a.Attivo).ThenBy(a => a.IdTipoCommessa).ThenBy(a => a.Ordine).ToListAsync(ct);
        return View(elenco);
    }

    [HttpPost]
    public async Task<IActionResult> Sincronizza(CancellationToken ct)
    {
        try
        {
            var tipi = await _arclab.TipiIncaricoAsync(ct);
            var (create, disattivate) = await _servizio.SincronizzaAsync(tipi, ct);
            await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.attivita.sincronizza", EsitoOperazione.Successo,
                dettaglio: new { create, disattivate, totale = tipi.Count }, ct: ct);
            TempData["Messaggio"] = $"Allineato con ArcLab: {tipi.Count} descrizioni incarico, {create} nuove, {disattivate} non più presenti.";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["Messaggio"] = "Sincronizzazione non riuscita: " + ex.Message;
        }
        return RedirectToAction(nameof(Attivita));
    }

    public async Task<IActionResult> AttivitaModifica(int id, CancellationToken ct)
    {
        var a = await _db.AttivitaConsulenza.AsNoTracking().Include(x => x.Contenuti).FirstOrDefaultAsync(x => x.IdAttivitaConsulenza == id, ct);
        if (a is null) return NotFound();
        ViewData["Titolo"] = a.Descrizione;
        return View(a);
    }

    [HttpPost]
    public async Task<IActionResult> AttivitaSalva(int id, ModalitaPrezzoConsulenza modalitaPrezzo, string? unitaMisura, string? testoOfferta,
        string? testoSuccessivi, string? prezzoProposto, string? prezzoSuccessiviProposto, bool attivo,
        List<ContenutoForm>? contenuti, string? nuovoContenuto, CancellationToken ct)
    {
        var a = await _db.AttivitaConsulenza.Include(x => x.Contenuti).FirstOrDefaultAsync(x => x.IdAttivitaConsulenza == id, ct);
        if (a is null) return NotFound();
        a.ModalitaPrezzo = modalitaPrezzo;
        a.UnitaMisura = Tronca(unitaMisura, 30);
        a.TestoOfferta = string.IsNullOrWhiteSpace(testoOfferta) ? null : testoOfferta.Trim();
        a.TestoSuccessivi = string.IsNullOrWhiteSpace(testoSuccessivi) ? null : testoSuccessivi.Trim();
        a.PrezzoProposto = AccordiProvvigioniController.Decimale(prezzoProposto);
        a.PrezzoSuccessiviProposto = AccordiProvvigioniController.Decimale(prezzoSuccessiviProposto);
        a.Attivo = attivo;
        foreach (var cf in contenuti ?? new List<ContenutoForm>())
        {
            var c = a.Contenuti.FirstOrDefault(x => x.IdContenutoAttivita == cf.IdContenutoAttivita);
            if (c is null) continue;
            if (!string.IsNullOrWhiteSpace(cf.Testo)) c.Testo = Tronca(cf.Testo, 500)!;
            c.Predefinito = cf.Predefinito;
            c.Attivo = cf.Attivo;
        }
        if (!string.IsNullOrWhiteSpace(nuovoContenuto))
        {
            var ordine = a.Contenuti.Select(c => c.Ordine).DefaultIfEmpty(0).Max() + 1;
            a.Contenuti.Add(new ContenutoAttivita { Chiave = $"v{ordine}", Testo = Tronca(nuovoContenuto, 500)!, Predefinito = false, Ordine = ordine });
        }
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.attivita.modifica", EsitoOperazione.Successo,
            "AttivitaConsulenza", id.ToString(), new { a.Descrizione, a.ModalitaPrezzo, a.PrezzoProposto, a.Attivo }, ct);
        TempData["Messaggio"] = "Attività salvata.";
        return RedirectToAction(nameof(AttivitaModifica), new { id });
    }

    // ── Amministrazione: pacchetti di settore ────────────────────────

    public async Task<IActionResult> Pacchetti(CancellationToken ct)
    {
        ViewData["Titolo"] = "Pacchetti di settore";
        var elenco = await _db.PacchettiConsulenza.AsNoTracking().Include(p => p.Righe).Include(p => p.Analisi)
            .OrderByDescending(p => p.Attivo).ThenBy(p => p.Nome).ToListAsync(ct);
        return View(elenco);
    }

    [HttpPost]
    public async Task<IActionResult> NuovoPacchetto(string nome, CancellationToken ct)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length == 0) { TempData["Messaggio"] = "Scrivi il nome del pacchetto."; return RedirectToAction(nameof(Pacchetti)); }
        var p = new PacchettoConsulenza { Nome = Tronca(nome, 100)!, DataUltimaModifica = DateTime.UtcNow, UltimaModificaDa = Tronca(_utente.NomeCompleto, 100) };
        _db.PacchettiConsulenza.Add(p);
        await _db.SaveChangesAsync(ct);
        return RedirectToAction(nameof(Pacchetto), new { id = p.IdPacchettoConsulenza });
    }

    /// <summary>Excel per la revisione dei pacchetti in riunione: analisi generiche da sostituire con i controlli ArcLab.</summary>
    public async Task<IActionResult> EsportaRevisionePacchetti(CancellationToken ct)
    {
        var pacchetti = await _db.PacchettiConsulenza.AsNoTracking().Include(x => x.Righe).Include(x => x.Analisi).ToListAsync(ct);
        var attivita = await _db.AttivitaConsulenza.AsNoTracking().Include(a => a.Contenuti).ToDictionaryAsync(a => a.IdAttivitaConsulenza, ct);
        IReadOnlyList<(int, string)> controlli;
        try
        {
            controlli = (await _controlli.ControlliAttiviAsync(ct)).Select(c => (c.IdControllo, (c.Controllo ?? "").Trim())).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["Messaggio"] = "ArcLab non raggiungibile, Excel non generato: " + ex.Message;
            return RedirectToAction(nameof(Pacchetti));
        }
        var file = RevisionePacchettiExcel.Genera(pacchetti, attivita, controlli);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.pacchetti.esporta", EsitoOperazione.Successo, ct: ct);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", RevisionePacchettiExcel.NomeFile());
    }

    /// <summary>
    /// Importa l'Excel di revisione compilato: per ogni riga con l'ID del controllo aggiunge l'analisi al pacchetto
    /// (senza doppioni) e, se richiesto, toglie la vecchia riga di analisi generica.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImportaRevisionePacchetti(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) { TempData["Messaggio"] = "Scegli il file Excel compilato."; return RedirectToAction(nameof(Pacchetti)); }
        IReadOnlyList<RevisionePacchettiExcel.RigaRevisione> righe;
        Dictionary<int, string> nomi;
        try
        {
            await using (var s = file.OpenReadStream()) righe = RevisionePacchettiExcel.Leggi(s);
            nomi = (await _controlli.ControlliAttiviAsync(ct)).GroupBy(c => c.IdControllo).ToDictionary(g => g.Key, g => (g.First().Controllo ?? "").Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            TempData["Messaggio"] = "Importazione non riuscita: " + ex.Message;
            return RedirectToAction(nameof(Pacchetti));
        }
        var ids = righe.Select(r => r.IdPacchetto).Distinct().ToList();
        var pacchetti = await _db.PacchettiConsulenza.Include(x => x.Righe).Include(x => x.Analisi)
            .Where(x => ids.Contains(x.IdPacchettoConsulenza)).ToDictionaryAsync(x => x.IdPacchettoConsulenza, ct);
        int aggiunte = 0, doppie = 0, tolte = 0;
        var errori = new List<string>();
        var daTogliere = new List<(PacchettoConsulenza, int)>();
        foreach (var r in righe)
        {
            if (!pacchetti.TryGetValue(r.IdPacchetto, out var p)) { errori.Add($"riga {r.Riga}: pacchetto non trovato"); continue; }
            var idc = RevisionePacchettiExcel.IdDaTesto(r.IdScelti);
            var mancanti = idc.Where(i => !nomi.ContainsKey(i)).ToList();
            if (idc.Count == 0 || mancanti.Count > 0)
            {
                errori.Add($"riga {r.Riga} ({p.Nome}): " + (idc.Count == 0 ? $"ID «{r.IdScelti}» non valido" : "controlli non attivi in ArcLab: " + string.Join(", ", mancanti)));
                continue;
            }
            var chiave = string.Join('-', idc.OrderBy(x => x));
            if (p.Analisi.Any(a => string.Join('-', a.IdControlli().OrderBy(x => x)) == chiave)) doppie++;
            else
            {
                p.Analisi.Add(new AnalisiPacchettoConsulenza
                {
                    IdControlloArcLab = idc.Count == 1 ? idc[0] : null,
                    IdControlliOrigine = idc.Count > 1 ? string.Join('-', idc) : null,
                    Descrizione = Tronca(!string.IsNullOrWhiteSpace(r.Nome) ? r.Nome : string.Join(" + ", idc.Select(i => nomi[i])), 300)!,
                    Modalita = r.Modalita, QuantitaAnnua = r.QuantitaAnnua, Prezzo = r.Prezzo, Note = Tronca(r.Note, 500),
                    Ordine = p.Analisi.Select(a => a.Ordine).DefaultIfEmpty(0).Max() + 1
                });
                aggiunte++;
            }
            if (r.TogliGenerica && r.IdRigaGenerica is int irg) daTogliere.Add((p, irg));
        }
        foreach (var (p, irg) in daTogliere.Distinct())
            if (p.Righe.FirstOrDefault(x => x.IdRigaPacchetto == irg) is { } rg) { p.Righe.Remove(rg); _db.RighePacchettoConsulenza.Remove(rg); tolte++; }
        foreach (var p in pacchetti.Values) { p.DataUltimaModifica = DateTime.UtcNow; p.UltimaModificaDa = Tronca(_utente.NomeCompleto, 100); }
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.pacchetti.importa", EsitoOperazione.Successo,
            dettaglio: new { file = file.FileName, aggiunte, doppie, tolte, errori = errori.Count }, ct: ct);
        TempData["Messaggio"] = $"Importazione: {aggiunte} analisi aggiunte, {tolte} righe generiche tolte" +
            (doppie > 0 ? $", {doppie} già presenti" : "") + "." +
            (errori.Count > 0 ? " Da controllare: " + string.Join("; ", errori.Take(15)) + (errori.Count > 15 ? "; …" : "") : "");
        return RedirectToAction(nameof(Pacchetti));
    }

    /// <summary>Crea un nuovo pacchetto copiando attività e analisi di uno esistente.</summary>
    [HttpPost]
    public async Task<IActionResult> DuplicaPacchetto(int id, string? nome, CancellationToken ct)
    {
        var o = await _db.PacchettiConsulenza.AsNoTracking().Include(x => x.Righe).Include(x => x.Analisi)
            .FirstOrDefaultAsync(x => x.IdPacchettoConsulenza == id, ct);
        if (o is null) return NotFound();
        var n = new PacchettoConsulenza
        {
            Nome = Tronca(string.IsNullOrWhiteSpace(nome) ? o.Nome + " (copia)" : nome, 100)!,
            Descrizione = Tronca($"Copia di «{o.Nome}»" + (string.IsNullOrWhiteSpace(o.Descrizione) ? "" : " – " + o.Descrizione), 500),
            DataUltimaModifica = DateTime.UtcNow, UltimaModificaDa = Tronca(_utente.NomeCompleto, 100)
        };
        foreach (var r in o.Righe.OrderBy(r => r.Ordine))
            n.Righe.Add(new RigaPacchettoConsulenza
            {
                IdAttivitaConsulenza = r.IdAttivitaConsulenza, Ordine = r.Ordine, DescrizioneLibera = r.DescrizioneLibera, Contenuti = r.Contenuti,
                ModalitaPrezzo = r.ModalitaPrezzo, Quantita = r.Quantita, Prezzo = r.Prezzo, PrezzoSuccessivi = r.PrezzoSuccessivi, Informativa = r.Informativa
            });
        foreach (var a in o.Analisi.OrderBy(a => a.Ordine))
            n.Analisi.Add(new AnalisiPacchettoConsulenza
            {
                Ordine = a.Ordine, IdControlloArcLab = a.IdControlloArcLab, IdControlliOrigine = a.IdControlliOrigine, Descrizione = a.Descrizione,
                Modalita = a.Modalita, QuantitaAnnua = a.QuantitaAnnua, Prezzo = a.Prezzo, Note = a.Note
            });
        _db.PacchettiConsulenza.Add(n);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.pacchetto.duplica", EsitoOperazione.Successo,
            "PacchettoConsulenza", n.IdPacchettoConsulenza.ToString(), new { origine = id, n.Nome }, ct);
        TempData["Messaggio"] = $"Creato «{n.Nome}» copiando «{o.Nome}»: modificalo e salva.";
        return RedirectToAction(nameof(Pacchetto), new { id = n.IdPacchettoConsulenza });
    }

    public async Task<IActionResult> Pacchetto(int id, CancellationToken ct)
    {
        var p = await _db.PacchettiConsulenza.AsNoTracking().Include(x => x.Righe).Include(x => x.Analisi)
            .FirstOrDefaultAsync(x => x.IdPacchettoConsulenza == id, ct);
        if (p is null) return NotFound();
        ViewData["Titolo"] = "Pacchetto " + p.Nome;
        var attivita = await _db.AttivitaConsulenza.AsNoTracking().Include(a => a.Contenuti)
            .OrderBy(a => a.IdTipoCommessa).ThenBy(a => a.Ordine).ToListAsync(ct);
        // Parametri e prezzo di listino di ogni controllo, letti da ArcLab per vederli nel pacchetto.
        var metodi = new Dictionary<int, IReadOnlyList<VoceMetodo>>();
        string? errore = null;
        try
        {
            foreach (var idc in p.Analisi.SelectMany(a => a.IdControlli()).Distinct())
                metodi[idc] = await _controlli.RigheDiControlloAsync(idc, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { errore = "ArcLab non raggiungibile: parametri non mostrati (" + ex.Message + ")"; }
        return View(new ModelloPacchettoConsulenza(p, attivita, metodi, errore));
    }

    [HttpPost]
    public async Task<IActionResult> PacchettoSalva(int id, string nome, string? descrizione, bool attivo, List<RigaPacchettoForm>? righe,
        int? idAttivitaNuova, int? rimuovi, List<AnalisiPacchettoForm>? analisi, string? azione, string? controlloCercato, int? idControlloNuovo,
        string? nomeGruppo, CancellationToken ct)
    {
        var p = await _db.PacchettiConsulenza.Include(x => x.Righe).Include(x => x.Analisi).FirstOrDefaultAsync(x => x.IdPacchettoConsulenza == id, ct);
        if (p is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(nome)) p.Nome = Tronca(nome, 100)!;
        p.Descrizione = Tronca(descrizione, 500);
        p.Attivo = attivo;
        foreach (var rf in righe ?? new List<RigaPacchettoForm>())
        {
            var r = p.Righe.FirstOrDefault(x => x.IdRigaPacchetto == rf.IdRigaPacchetto);
            if (r is null) continue;
            r.DescrizioneLibera = Tronca(rf.DescrizioneLibera, 500);
            r.Contenuti = rf.Contenuti is { Count: > 0 } ? Tronca(string.Join(';', rf.Contenuti), 1000) : null;
            r.Quantita = AccordiProvvigioniController.Decimale(rf.Quantita) is decimal q && q > 0 ? q : 1m;
            r.Prezzo = AccordiProvvigioniController.Decimale(rf.Prezzo) ?? 0m;
            r.PrezzoSuccessivi = AccordiProvvigioniController.Decimale(rf.PrezzoSuccessivi);
            r.Informativa = rf.Informativa;
        }
        if (rimuovi is int idr && p.Righe.FirstOrDefault(x => x.IdRigaPacchetto == idr) is { } daTogliere)
        { p.Righe.Remove(daTogliere); _db.RighePacchettoConsulenza.Remove(daTogliere); }
        if (idAttivitaNuova is int ida && await _db.AttivitaConsulenza.AnyAsync(a => a.IdAttivitaConsulenza == ida, ct))
            p.Righe.Add(new RigaPacchettoConsulenza { IdAttivitaConsulenza = ida, Ordine = p.Righe.Select(r => r.Ordine).DefaultIfEmpty(0).Max() + 1 });

        // Analisi di laboratorio
        foreach (var af in analisi ?? new List<AnalisiPacchettoForm>())
        {
            var a = p.Analisi.FirstOrDefault(x => x.IdAnalisiPacchetto == af.IdAnalisiPacchetto);
            if (a is null) continue;
            if (!string.IsNullOrWhiteSpace(af.Descrizione)) a.Descrizione = Tronca(af.Descrizione, 300)!;
            a.Modalita = af.Modalita ?? a.Modalita;
            a.QuantitaAnnua = AccordiProvvigioniController.Decimale(af.QuantitaAnnua) is decimal q && q > 0 ? q : null;
            a.Prezzo = AccordiProvvigioniController.Decimale(af.Prezzo) is decimal pr && pr > 0 ? pr : null;
            a.Note = Tronca(af.Note, 500);
            a.Ordine = af.Ordine ?? a.Ordine;
        }
        string? messaggio = null;
        var ancora = "";
        azione ??= "";
        if (azione == "aggiungi-controllo")
        {
            ancora = "#analisi";
            var idc = idControlloNuovo ?? IdDaTestoControllo(controlloCercato);
            string? nomeCtrl = null;
            if (idc is int idCtrl)
            {
                try { nomeCtrl = await _controlli.NomeControlloAsync(idCtrl, ct); }
                catch (Exception ex) when (ex is not OperationCanceledException) { messaggio = "ArcLab non raggiungibile: " + ex.Message; }
            }
            if (idc is int idOk && !string.IsNullOrWhiteSpace(nomeCtrl))
            {
                p.Analisi.Add(new AnalisiPacchettoConsulenza
                {
                    IdControlloArcLab = idOk, Descrizione = Tronca(nomeCtrl, 300)!, Modalita = ModalitaAnalisiConsulenza.APagamento,
                    Ordine = p.Analisi.Select(x => x.Ordine).DefaultIfEmpty(0).Max() + 1
                });
                messaggio = $"Aggiunto il controllo «{nomeCtrl!.Trim()}» (ID {idOk}).";
            }
            else messaggio ??= "Controllo non trovato in ArcLab: sceglilo dall'elenco (scrivi almeno 2 lettere del nome o l'ID).";
        }
        else if (azione.StartsWith("rimuovi-analisi:") && int.TryParse(azione[16..], out var idRa)
                 && p.Analisi.FirstOrDefault(x => x.IdAnalisiPacchetto == idRa) is { } ra)
        {
            p.Analisi.Remove(ra); _db.AnalisiPacchettiConsulenza.Remove(ra);
            ancora = "#analisi";
        }
        else if (azione == "raggruppa")
        {
            ancora = "#analisi";
            var sel = p.Analisi.Where(a => (analisi ?? new()).Any(f => f.Selezionata && f.IdAnalisiPacchetto == a.IdAnalisiPacchetto))
                .OrderBy(a => a.Ordine).ToList();
            if (sel.Count < 2) messaggio = "Spunta almeno due analisi da raggruppare.";
            else
            {
                var ids = sel.SelectMany(a => a.IdControlli()).Distinct().ToList();
                var prezzi = sel.Select(a => a.Prezzo).ToList();
                var g = new AnalisiPacchettoConsulenza
                {
                    Ordine = sel[0].Ordine, IdControlliOrigine = string.Join('-', ids),
                    Descrizione = Tronca(string.IsNullOrWhiteSpace(nomeGruppo) ? string.Join(" + ", sel.Select(a => a.Descrizione)) : nomeGruppo, 300)!,
                    Modalita = sel[0].Modalita, QuantitaAnnua = sel[0].QuantitaAnnua,
                    Prezzo = prezzi.All(x => x is > 0) ? prezzi.Sum() : null,
                    Note = Tronca(string.Join("; ", sel.Select(a => a.Note).Where(x => !string.IsNullOrWhiteSpace(x))), 500)
                };
                foreach (var a in sel) { p.Analisi.Remove(a); _db.AnalisiPacchettiConsulenza.Remove(a); }
                p.Analisi.Add(g);
                messaggio = $"Raggruppati {sel.Count} righe in «{g.Descrizione}» (ID {g.IdControlliOrigine}). Indica il prezzo del gruppo se è diverso dalla somma.";
            }
        }
        else if (azione.StartsWith("separa:") && int.TryParse(azione[7..], out var idSep)
                 && p.Analisi.FirstOrDefault(x => x.IdAnalisiPacchetto == idSep) is { IdControlliOrigine: not null } gs)
        {
            ancora = "#analisi";
            var o = gs.Ordine;
            foreach (var idc in gs.IdControlli())
            {
                string nomeC;
                try { nomeC = (await _controlli.NomeControlloAsync(idc, ct))?.Trim() ?? $"Controllo {idc}"; }
                catch (Exception ex) when (ex is not OperationCanceledException) { nomeC = $"Controllo {idc}"; }
                p.Analisi.Add(new AnalisiPacchettoConsulenza
                {
                    IdControlloArcLab = idc, Descrizione = Tronca(nomeC, 300)!, Modalita = gs.Modalita, QuantitaAnnua = gs.QuantitaAnnua, Ordine = o
                });
            }
            p.Analisi.Remove(gs); _db.AnalisiPacchettiConsulenza.Remove(gs);
            messaggio = "Gruppo separato nei controlli d'origine.";
        }

        p.DataUltimaModifica = DateTime.UtcNow;
        p.UltimaModificaDa = Tronca(_utente.NomeCompleto, 100);
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.pacchetto.modifica", EsitoOperazione.Successo,
            "PacchettoConsulenza", id.ToString(), new { p.Nome, righe = p.Righe.Count, analisi = p.Analisi.Count, p.Attivo, azione }, ct);
        TempData["Messaggio"] = messaggio ?? "Pacchetto salvato.";
        return Redirect(Url.Action(nameof(Pacchetto), new { id })! + ancora);
    }

    // ── Amministrazione: responsabili cliente (zone AZW) ─────────────

    public async Task<IActionResult> Responsabili(CancellationToken ct)
    {
        ViewData["Titolo"] = "Responsabili clienti";
        var elenco = await _db.ResponsabiliCliente.AsNoTracking().OrderByDescending(x => x.Attivo).ThenBy(x => x.Sigla).ToListAsync(ct);
        var clientiPerZona = await _db.AzwClienti.AsNoTracking().Where(x => x.Zona != null).GroupBy(x => x.Zona!)
            .Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N, ct);
        return View(new ModelloResponsabiliCliente(elenco, clientiPerZona));
    }

    /// <summary>Salva nomi ed e-mail delle sigle; con <paramref name="nuovaSigla"/> ne aggiunge una.</summary>
    [HttpPost]
    public async Task<IActionResult> ResponsabiliSalva(List<ResponsabileForm>? righe, string? nuovaSigla, string? nuovoNome, CancellationToken ct)
    {
        var tutti = await _db.ResponsabiliCliente.ToListAsync(ct);
        foreach (var f in righe ?? new List<ResponsabileForm>())
        {
            var r = tutti.FirstOrDefault(x => x.IdResponsabileCliente == f.IdResponsabileCliente);
            if (r is null) continue;
            var nome = Tronca(f.Nome, 100); var email = Tronca(f.Email, 250);
            if (r.Nome == nome && r.Email == email && r.Attivo == f.Attivo) continue;
            r.Nome = nome; r.Email = email; r.Attivo = f.Attivo; r.DataUltimaModifica = DateTime.UtcNow;
        }
        var sigla = Tronca(nuovaSigla?.ToUpperInvariant(), 20);
        string messaggio = "Salvato.";
        if (sigla is not null)
        {
            if (tutti.Any(x => x.Sigla == sigla)) messaggio = $"La sigla «{sigla}» c'è già.";
            else
            {
                _db.ResponsabiliCliente.Add(new ResponsabileCliente { Sigla = sigla, Nome = Tronca(nuovoNome, 100), DataUltimaModifica = DateTime.UtcNow });
                messaggio = $"Aggiunta la sigla «{sigla}».";
            }
        }
        await _db.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "preventivoconsulenza.responsabili.modifica", EsitoOperazione.Successo, ct: ct);
        TempData["Messaggio"] = messaggio;
        return RedirectToAction(nameof(Responsabili));
    }

    /// <summary>Aggiunge (senza nome) le sigle di zona presenti nell'anagrafica AZW importata e non ancora in tabella.</summary>
    [HttpPost]
    public async Task<IActionResult> ResponsabiliDaAzw(CancellationToken ct)
    {
        var zone = await _db.AzwClienti.AsNoTracking().Where(x => x.Zona != null && x.Zona != "").Select(x => x.Zona!).Distinct().ToListAsync(ct);
        var esistenti = await _db.ResponsabiliCliente.Select(x => x.Sigla).ToListAsync(ct);
        var nuove = zone.Where(z => !esistenti.Contains(z, StringComparer.OrdinalIgnoreCase)).OrderBy(z => z).ToList();
        foreach (var z in nuove) _db.ResponsabiliCliente.Add(new ResponsabileCliente { Sigla = Tronca(z, 20)!, DataUltimaModifica = DateTime.UtcNow });
        await _db.SaveChangesAsync(ct);
        TempData["Messaggio"] = zone.Count == 0
            ? "Nell'anagrafica AZW importata non ci sono zone: rifai «Importa» in Amministrazione → Provvigioni (ora legge anche il campo ZONA)."
            : $"Sigle trovate in AZW: {zone.Count}; aggiunte {nuove.Count}" + (nuove.Count > 0 ? ": " + string.Join(", ", nuove) + ". Scrivi i nomi e salva." : ".");
        return RedirectToAction(nameof(Responsabili));
    }

    // ── Utilità ──────────────────────────────────────────────────────

    private async Task<PreventivoConsulenza?> CaricaAsync(int id, bool sola, CancellationToken ct)
    {
        var q = _db.PreventiviConsulenza.Include(p => p.Righe).Include(p => p.Blocchi).Include(p => p.Analisi).ThenInclude(a => a.Metodi)
            .AsSplitQuery().Where(p => p.IdPreventivoConsulenza == id && p.Attivo);
        if (sola) q = q.AsNoTracking();
        var p = await q.FirstOrDefaultAsync(ct);
        if (p is not null)
        {
            p.Righe = p.Righe.OrderBy(r => r.Progressivo).ToList();
            p.Blocchi = p.Blocchi.OrderBy(b => b.Numero).ToList();
            p.Analisi = p.Analisi.OrderBy(a => a.Progressivo).ToList();
        }
        return p;
    }

    internal static bool Modificabile(PreventivoConsulenza p) =>
        p.Stato is StatoPreventivoConsulenza.Bozza or StatoPreventivoConsulenza.Inviato;

    private static string? Tronca(string? v, int max)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}

public sealed class FormPreventivoConsulenza
{
    public string? RagioneSociale { get; set; }
    public string? PartitaIva { get; set; }
    public string? CodiceFiscale { get; set; }
    public string? Indirizzo { get; set; }
    public string? Cap { get; set; }
    public string? Citta { get; set; }
    public string? Provincia { get; set; }
    public string? Email { get; set; }
    public string? Pec { get; set; }
    public string? CodiceSdi { get; set; }
    /// <summary>29/10/2026: telefono del committente (da AZW, modificabile).</summary>
    public string? Telefono { get; set; }
    public string? SedeNome { get; set; }
    public string? SedeIndirizzo { get; set; }
    public string? ReferenteNome { get; set; }
    public string? ReferenteMansione { get; set; }
    public string? ReferenteTelefono { get; set; }
    public string? ReferenteEmail { get; set; }
    public string? SettoreAttivita { get; set; }
    public string? Oggetto { get; set; }
    public DateTime? DataOfferta { get; set; }
    public int? ValiditaGiorni { get; set; }
    public DateTime? DataInizio { get; set; }
    public int? DurataAnni { get; set; }
    public int? NumeroRate { get; set; }
    public string? CondizioniPagamento { get; set; }
    public int? IdOrganicoConsulente { get; set; }
    public string? ConsulenteNome { get; set; }
    public int? IdCommerciale { get; set; }
    public string? ResponsabileCliente { get; set; }
    public string? Note { get; set; }
    public List<RigaFormConsulenza>? Righe { get; set; }
    /// <summary>29/10/2026: blocchi di quotazione.</summary>
    public List<BloccoFormConsulenza>? Blocchi { get; set; }
    public List<AnalisiFormConsulenza>? Analisi { get; set; }
    public string? ControlloCercato { get; set; }
    public int? IdControlloNuovo { get; set; }
    public string? NomeGruppo { get; set; }
    public int? IdAttivitaNuova { get; set; }
    public int? IdPacchetto { get; set; }
    public int? IdClienteScelto { get; set; }
    public string? ClienteCercato { get; set; }
    public int? IdInsediamentoScelto { get; set; }
    public int? IdContattoScelto { get; set; }
}

public sealed class RigaFormConsulenza
{
    public int Progressivo { get; set; }
    public string? DescrizioneLibera { get; set; }
    public string? TestoOfferta { get; set; }
    public string? TestoSuccessivi { get; set; }
    public List<string>? Contenuti { get; set; }
    public ModalitaPrezzoConsulenza? ModalitaPrezzo { get; set; }
    public string? Quantita { get; set; }
    public string? Prezzo { get; set; }
    public string? PrezzoSuccessivi { get; set; }
    public bool Informativa { get; set; }
    /// <summary>29/10/2026: numero del blocco di quotazione.</summary>
    public int? Blocco { get; set; }
    /// <summary>29/10/2026: solo per l'attività personalizzata.</summary>
    public string? DescrizioneAttivita { get; set; }
    public string? UnitaMisura { get; set; }
}

public sealed class BloccoFormConsulenza
{
    public int Numero { get; set; }
    public string? Titolo { get; set; }
    public string? Descrizione { get; set; }
}

public sealed class AnalisiFormConsulenza
{
    public int Progressivo { get; set; }
    public string? Descrizione { get; set; }
    public ModalitaAnalisiConsulenza? Modalita { get; set; }
    public string? QuantitaAnnua { get; set; }
    public string? Prezzo { get; set; }
    public string? Note { get; set; }
    public bool Selezionata { get; set; }
}

public sealed class AnalisiPacchettoForm
{
    public int IdAnalisiPacchetto { get; set; }
    public int? Ordine { get; set; }
    public string? Descrizione { get; set; }
    public ModalitaAnalisiConsulenza? Modalita { get; set; }
    public string? QuantitaAnnua { get; set; }
    public string? Prezzo { get; set; }
    public string? Note { get; set; }
    public bool Selezionata { get; set; }
}

public sealed class ResponsabileForm
{
    public int IdResponsabileCliente { get; set; }
    public string? Nome { get; set; }
    public string? Email { get; set; }
    public bool Attivo { get; set; }
}

public sealed record ModelloResponsabiliCliente(IReadOnlyList<ResponsabileCliente> Elenco, IReadOnlyDictionary<string, int> ClientiPerZona);

public sealed class ContenutoForm
{
    public int IdContenutoAttivita { get; set; }
    public string? Testo { get; set; }
    public bool Predefinito { get; set; }
    public bool Attivo { get; set; }
}

public sealed class RigaPacchettoForm
{
    public int IdRigaPacchetto { get; set; }
    public string? DescrizioneLibera { get; set; }
    public List<string>? Contenuti { get; set; }
    public string? Quantita { get; set; }
    public string? Prezzo { get; set; }
    public string? PrezzoSuccessivi { get; set; }
    public bool Informativa { get; set; }
}

public sealed record ModelloElencoPreventiviConsulenza(IReadOnlyList<PreventivoConsulenza> Elenco, int Anno, IReadOnlyList<int> Anni, string? Stato)
{
    /// <summary>29/10/2026: nome del redattore (chi ha creato il preventivo) per IdUtente.</summary>
    public IReadOnlyDictionary<int, string> Redattori { get; init; } = new Dictionary<int, string>();
}

public sealed record ModelloPreventivoConsulenza(
    PreventivoConsulenza Preventivo,
    IReadOnlyList<AttivitaConsulenza> Attivita,
    IReadOnlyList<PacchettoConsulenza> Pacchetti,
    IReadOnlyList<string> Pagamenti,
    IReadOnlyList<Commerciale> Commerciali,
    IReadOnlyList<ConsulenteArcLab> Consulenti,
    IReadOnlyList<InsediamentoRapportino> Sedi,
    IReadOnlyList<ContattoArcLab> Contatti,
    IReadOnlyList<CommessaDaCompilare> Commesse,
    IReadOnlyList<CommessaAnalisiDaCompilare> CommesseAnalisi,
    string? ErroreArcLab);

public sealed record ModelloPacchettoConsulenza(PacchettoConsulenza Pacchetto, IReadOnlyList<AttivitaConsulenza> Attivita,
    IReadOnlyDictionary<int, IReadOnlyList<VoceMetodo>> Metodi, string? ErroreArcLab);
