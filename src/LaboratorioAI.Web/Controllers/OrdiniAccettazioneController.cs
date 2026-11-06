using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Ordine di Accettazione: documento generato da LaboratorioAI leggendo i lotti già ricevuti in
/// ArcLab (LabAnalisiDati, SOLA LETTURA). Riepilogo campioni + prezzi + allegato tecnico, su carta
/// intestata (Word) o PDF. Dal 14/10/2026 gli Ordini per il Portale li genera Palomar da ArcLab: qui restano
/// l'anteprima e l'Ordine da firmare per i clienti privati (archivio, accettazione.OrdiniPrivati, registro).
/// MAI scrittura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.OrdiniAccettazioneGestione)]
public class OrdiniAccettazioneController : Controller
{
    private const string ModelloCartaIntestata = "docx/Carta_intestata_Marea.docx";
    private const string ChiaveRevisione = "Preventivi.RevisioneCondizioni";      // condivisa con le offerte
    private const string RevisioneDefault = "Mod. Com. 05 Rev. 19";
    private const string ChiaveModuloOA = "OrdineAccettazione.ModuloRevisione";
    private const string ModuloDefault = "Mod. Acc. 01 Rev. 0 del 16/09/2026";
    private const string ChiaveCartella = "OrdineAccettazione.CartellaOutput";     // se vuota → App_Data/OrdiniAccettazione

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryOrdineAccettazione _repo;
    private readonly IGeneratoreOrdineAccettazione _generatore;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IRepositoryAccettazione _riferimenti;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IWebHostEnvironment _ambiente;
    private readonly ServizioRegistroDocumenti _registro;

    public OrdiniAccettazioneController(
        ContestoLaboratorioAI ctx, IRepositoryOrdineAccettazione repo, IGeneratoreOrdineAccettazione generatore,
        IRepositoryAnagrafiche anagrafiche, IRepositoryAccettazione riferimenti, IContestoUtente utente, IServizioAudit audit,
        IWebHostEnvironment ambiente, ServizioRegistroDocumenti registro)
    {
        _ctx = ctx; _repo = repo; _generatore = generatore; _anagrafiche = anagrafiche; _riferimenti = riferimenti;
        _utente = utente; _audit = audit; _ambiente = ambiente; _registro = registro;
    }

    /// <summary>
    /// Insediamenti del cliente con indirizzo ed email, per il richiamo ai recapiti in fondo al
    /// documento. Solo se il cliente è in archivio ArcLab (IdClienteArcLab valorizzato).
    /// </summary>
    private Task<IReadOnlyList<InsediamentoContatto>> InsediamentiAsync(OrdineAccettazione ordine, CancellationToken ct) =>
        ordine.IdClienteArcLab is int idCliente
            ? _anagrafiche.InsediamentiConEmailAsync(idCliente, ct)
            : Task.FromResult<IReadOnlyList<InsediamentoContatto>>(Array.Empty<InsediamentoContatto>());

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Ordini di Accettazione";
        return View();
    }

    private async Task<string> ConfigAsync(string chiave, string predefinito, CancellationToken ct)
    {
        var c = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == chiave, ct);
        return string.IsNullOrWhiteSpace(c?.Valore) ? predefinito : c!.Valore!;
    }

    private Task<string> RevisioneCorrenteAsync(CancellationToken ct) => ConfigAsync(ChiaveRevisione, RevisioneDefault, ct);
    private Task<string> ModuloCorrenteAsync(CancellationToken ct) => ConfigAsync(ChiaveModuloOA, ModuloDefault, ct);

    /// <summary>Popola i campi di intestazione dipendenti dalla configurazione (revisione + modulo).</summary>
    private async Task ApplicaImpostazioniAsync(OrdineAccettazione ordine, CancellationToken ct)
    {
        ordine.RevisioneCondizioni = await RevisioneCorrenteAsync(ct);
        ordine.RiferimentoModulo = await ModuloCorrenteAsync(ct);
    }

    // Le impostazioni (revisione Condizioni condivisa, Mod. Acc., email errori, cartella) si modificano
    // in Amministrazione → Impostazioni documenti (ImpostazioniController). Qui vengono solo lette.

    // ── Elenco lotti candidati (con flag "già emesso") ──────────────
    [HttpGet("api/ordini-accettazione/lotti")]
    public async Task<IActionResult> Lotti([FromQuery] DateTime? dal, [FromQuery] DateTime? al, [FromQuery] int? idCliente, [FromQuery] int? idInsediamento,
        [FromQuery] string? cliente, [FromQuery] string? insediamento, CancellationToken ct)
    {
        // @al inclusivo sul giorno → esclusivo il giorno dopo.
        var alEsclusivo = al?.Date.AddDays(1);
        var lotti = await _repo.LottiRicevutiAsync(dal?.Date, alEsclusivo, new FiltroLotti(idCliente, idInsediamento, cliente, insediamento), ct);

        var ids = lotti.Select(l => l.IdLotto).ToList();
        // Ordini da firmare già stampati per i privati (gli Ordini del Portale li genera Palomar da ArcLab).
        var privati = await _ctx.OrdiniPrivati.AsNoTracking()
            .Where(o => ids.Contains(o.IdLotto) && !o.Annullato)
            .Select(o => new { o.IdLotto, o.IdOrdinePrivato, o.Stampe, o.DataStampa, o.Consegna, o.EmailRapporto, o.FirmatoIl })
            .ToDictionaryAsync(o => o.IdLotto, ct);

        return Json(lotti.Select(l =>
        {
            privati.TryGetValue(l.IdLotto, out var p);
            return new
            {
                l.IdLotto,
                l.CodLotto,
                DataRicezione = l.DataRicezione?.ToString("yyyy-MM-dd"),
                l.Cliente,
                Insediamento = l.Insediamento?.Trim(),
                InsediamentoCitta = l.InsediamentoCitta?.Trim(),
                l.NumeroCampioni,
                l.CampioniConParametri,
                l.Pronto,
                Privato = p is null ? null : new
                {
                    id = p.IdOrdinePrivato, p.Stampe,
                    dataStampa = DateTime.SpecifyKind(p.DataStampa, DateTimeKind.Utc),
                    consegna = p.Consegna.ToString(), email = p.EmailRapporto,
                    firmatoIl = p.FirmatoIl?.ToString("yyyy-MM-dd")
                }
            };
        }));
    }

    // ── Tendine dei filtri (ArcLab, sola lettura) ──
    [HttpGet("api/ordini-accettazione/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) => Json(await _anagrafiche.ClientiAsync(ct));

    [HttpGet("api/ordini-accettazione/insediamenti")]
    public async Task<IActionResult> Insediamenti([FromQuery] int idCliente, CancellationToken ct) =>
        Json(await _riferimenti.InsediamentiPerClienteAsync(idCliente, ct));

    // ── Generazione/anteprima del documento (Word o PDF), senza registrare l'emissione ──
    [HttpGet("api/ordini-accettazione/{idLotto:int}/documento")]
    public async Task<IActionResult> Documento(int idLotto, [FromQuery] string? formato, CancellationToken ct)
    {
        var ordine = await _repo.LeggiPerLottoAsync(idLotto, ct);
        if (ordine is null)
            return NotFound(new { messaggio = $"Lotto {idLotto} non trovato o senza campioni in ArcLab." });
        if (!ordine.Pronto)
            return BadRequest(new { messaggio = $"Lotto {ordine.CodLotto}: {ordine.CampioniSenzaParametri} campione/i senza parametri caricati. Documento non generabile." });
        await ApplicaImpostazioniAsync(ordine, ct);
        var insediamenti = await InsediamentiAsync(ordine, ct);

        var pdf = string.Equals(formato, "pdf", StringComparison.OrdinalIgnoreCase);
        byte[] contenuto;
        string estensione, mime;
        if (pdf)
        {
            contenuto = _generatore.GeneraPdf(ordine, _ambiente.WebRootPath, insediamenti);
            estensione = "pdf";
            mime = "application/pdf";
        }
        else
        {
            var modello = Path.Combine(_ambiente.WebRootPath, ModelloCartaIntestata);
            if (!System.IO.File.Exists(modello))
                return NotFound(new { messaggio = "Modello di carta intestata non trovato: " + ModelloCartaIntestata });
            contenuto = _generatore.GeneraWord(ordine, modello, insediamenti);
            estensione = "docx";
            mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }

        await _audit.RegistraAsync(AreaFunzionale.Accettazione, "ordineaccettazione.documento",
            EsitoOperazione.Successo, tipoEntita: "Lotto", idEntita: idLotto.ToString(),
            dettaglio: new { ordine.CodLotto, Formato = estensione }, ct: ct);

        return File(contenuto, mime, _generatore.NomeFile(ordine, estensione));
    }

    // ═══════════════════════════════════════════════════════════════
    //  Ordini da firmare per i clienti privati (14/10/2026)
    //  Gli Ordini per il Portale li genera Palomar da ArcLab: qui non si emettono più né si copiano in
    //  Palomar. Si stampa solo la versione da firmare per i privati (pulsante «Stampa da firmare» di ArcLab).
    // ═══════════════════════════════════════════════════════════════

    private const string ChiaveVerificaPrivati = "OrdineAccettazione.TestoVerificaPrivati";
    private const string ChiaveCondizioniPrivati = "OrdineAccettazione.TestoCondizioniPrivati";
    private const string ChiaveFirmaPrivati = "OrdineAccettazione.TestoFirmaPrivati";

    /// <summary>Pagina aperta dal pulsante di ArcLab: e-mail e consegna del Rapporto di Prova, poi stampa.</summary>
    [HttpGet("/OrdiniAccettazione/Privato/{idLotto:int}")]
    public async Task<IActionResult> Privato(int idLotto, CancellationToken ct)
    {
        ViewData["Titolo"] = "Ordine da firmare";
        var ordine = await _repo.LeggiPerLottoAsync(idLotto, ct);
        var esistente = await _ctx.OrdiniPrivati.AsNoTracking().FirstOrDefaultAsync(o => o.IdLotto == idLotto && !o.Annullato, ct);
        string? emailProposta = esistente?.EmailRapporto;
        if (emailProposta is null && ordine?.IdClienteArcLab is int idc)
            emailProposta = await _ctx.OrdiniPrivati.AsNoTracking()
                .Where(o => o.IdClienteArcLab == idc && !o.Annullato && o.EmailRapporto != null)
                .OrderByDescending(o => o.DataStampa).Select(o => o.EmailRapporto).FirstOrDefaultAsync(ct);
        string? motivo = ordine is null ? await MotivoNonTrovatoAsync(idLotto, ct) : null;
        return View("~/Views/OrdiniAccettazione/Privato.cshtml", new ModelloOrdinePrivato(idLotto, ordine, esistente, emailProposta, motivo));
    }

    public record RichiestaPrivato(string? Email, ConsegnaRapportoPrivato Consegna, bool Firmato);

    /// <summary>Genera e archivia la versione da firmare, la registra e restituisce l'indirizzo del PDF da stampare.</summary>
    [HttpPost("api/ordini-accettazione/{idLotto:int}/privato")]
    public async Task<IActionResult> StampaPrivato(int idLotto, [FromBody] RichiestaPrivato req, CancellationToken ct)
    {
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email is not null && !IndirizziEmail.Formato.IsMatch(email))
            return BadRequest(new { messaggio = "L'indirizzo e-mail non è valido." });
        var consegna = Enum.IsDefined(req.Consegna) ? req.Consegna : ConsegnaRapportoPrivato.AMano;
        if (consegna != ConsegnaRapportoPrivato.AMano && email is null)
            return BadRequest(new { messaggio = "Per l'invio per e-mail serve l'indirizzo (oppure scegli «a mano»)." });

        var ordine = await _repo.LeggiPerLottoAsync(idLotto, ct);
        if (ordine is null) return NotFound(new { messaggio = await MotivoNonTrovatoAsync(idLotto, ct) });
        if (!ordine.Pronto)
            return BadRequest(new { messaggio = $"Lotto {ordine.CodLotto}: {ordine.CampioniSenzaParametri} campione/i senza parametri caricati. Carica i parametri in ArcLab e riprova." });
        await ApplicaImpostazioniAsync(ordine, ct);
        ordine.Firma = new FirmaPrivato
        {
            EmailRapporto = email,
            Consegna = consegna,
            TestoVerifica = await ConfigAsync(ChiaveVerificaPrivati, TestiCondizioniAccettazione.VerificaPrivati, ct),
            TestoCondizioni = await ConfigAsync(ChiaveCondizioniPrivati, TestiCondizioniAccettazione.CondizioniPrivati, ct),
            TestoFirma = await ConfigAsync(ChiaveFirmaPrivati, TestiCondizioniAccettazione.FirmaPrivati, ct),
            PerMarea = _utente.NomeCompleto
        };

        var pdf = _generatore.GeneraPdf(ordine, _ambiente.WebRootPath, await InsediamentiAsync(ordine, ct));
        var nomeFile = Path.GetFileNameWithoutExtension(_generatore.NomeFile(ordine, "pdf")) + "_privato.pdf";
        var percorso = await SalvaPdfAsync(ordine, nomeFile, pdf, ct);

        var r = await _ctx.OrdiniPrivati.FirstOrDefaultAsync(o => o.IdLotto == idLotto, ct);
        if (r is null) { r = new OrdinePrivato { IdLotto = idLotto }; _ctx.OrdiniPrivati.Add(r); }
        r.Annullato = false;
        r.CodLotto = Tronca(ordine.CodLotto, 20);
        r.IdClienteArcLab = ordine.IdClienteArcLab;
        r.Cliente = Tronca(ordine.ClienteRagioneSociale, 300);
        r.NumeroCampioni = ordine.Campioni.Count;
        r.DataRicezione = ordine.DataRicezione;
        r.EmailRapporto = email;
        r.Consegna = consegna;
        if (req.Firmato) r.FirmatoIl ??= DateTime.Today;
        r.DataStampa = DateTime.UtcNow;
        r.Stampe++;
        r.IdUtente = _utente.IdUtente;
        r.NomeUtente = Tronca(_utente.NomeCompleto ?? _utente.Email, 100);
        r.NomeFile = nomeFile;
        r.PercorsoFile = percorso;
        r.Hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pdf));
        await _ctx.SaveChangesAsync(ct);

        await _registro.RegistraAsync(new RegistroDocumento
        {
            Ambito = ServizioRegistroDocumenti.AmbitoOrdine,
            IdEsecuzione = ServizioRegistroDocumenti.NuovaEsecuzione(),
            Origine = "privato",
            Riferimento = RiferimentoLotto(ordine.CodLotto, ordine.ClienteRagioneSociale, idLotto),
            IdLotto = idLotto,
            Esito = EsitoDocumento.Ok,
            Messaggio = $"Ordine da firmare stampato per cliente privato (Rapporto di Prova: {NomeConsegna(consegna)}" + (email is null ? "" : $", {email}") + ").",
            FileArchivio = percorso
        }, ct);
        await _audit.RegistraAsync(AreaFunzionale.Accettazione, "ordineaccettazione.privato", EsitoOperazione.Successo,
            tipoEntita: "Lotto", idEntita: idLotto.ToString(), dettaglio: new { ordine.CodLotto, consegna, email, r.Stampe }, ct: ct);

        return Json(new { id = r.IdOrdinePrivato, url = $"/api/ordini-accettazione/privati/{r.IdOrdinePrivato}/pdf", stampe = r.Stampe });
    }

    /// <summary>PDF archiviato, aperto nel browser (non scaricato) per stamparlo subito.</summary>
    [HttpGet("api/ordini-accettazione/privati/{id:int}/pdf")]
    public async Task<IActionResult> PdfPrivato(int id, CancellationToken ct)
    {
        var r = await _ctx.OrdiniPrivati.AsNoTracking().FirstOrDefaultAsync(o => o.IdOrdinePrivato == id, ct);
        if (r?.PercorsoFile is null || !System.IO.File.Exists(r.PercorsoFile)) return NotFound("PDF non trovato in archivio.");
        Response.Headers.ContentDisposition = $"inline; filename=\"{r.NomeFile}\"";
        return File(await System.IO.File.ReadAllBytesAsync(r.PercorsoFile, ct), "application/pdf");
    }

    /// <summary>Ordini dei privati per il registro: ricerca per numero di lotto o per cliente.</summary>
    [HttpGet("api/ordini-accettazione/privati")]
    public async Task<IActionResult> ElencoPrivati([FromQuery] string? cerca, [FromQuery] int? giorni, CancellationToken ct)
    {
        var q = _ctx.OrdiniPrivati.AsNoTracking().Where(o => !o.Annullato);
        if (!string.IsNullOrWhiteSpace(cerca))
        {
            var t = cerca.Trim();
            q = q.Where(o => (o.CodLotto != null && o.CodLotto.Contains(t)) || (o.Cliente != null && o.Cliente.Contains(t)) || (o.EmailRapporto != null && o.EmailRapporto.Contains(t)));
        }
        else
        {
            var dal = DateTime.UtcNow.AddDays(-(giorni is > 0 and <= 3650 ? giorni.Value : 90));
            q = q.Where(o => o.DataStampa >= dal);
        }
        var righe = await q.OrderByDescending(o => o.DataStampa).Take(500).ToListAsync(ct);
        return Json(righe.Select(o => new
        {
            id = o.IdOrdinePrivato, o.IdLotto, o.CodLotto, o.Cliente, o.NumeroCampioni,
            dataRicezione = o.DataRicezione?.ToString("yyyy-MM-dd"),
            email = o.EmailRapporto,
            consegna = o.Consegna.ToString(),
            // Sezioni del registro: chi riceve il Rapporto per e-mail (anche «entrambi») e chi lo ritira a mano.
            perEmail = o.Consegna != ConsegnaRapportoPrivato.AMano && o.EmailRapporto != null,
            firmatoIl = o.FirmatoIl?.ToString("yyyy-MM-dd"),
            o.Note,
            dataStampa = DateTime.SpecifyKind(o.DataStampa, DateTimeKind.Utc),
            o.Stampe,
            utente = o.NomeUtente
        }));
    }

    public record ModificaPrivato(string? Email, ConsegnaRapportoPrivato Consegna, DateTime? FirmatoIl, string? Note);

    /// <summary>Correzioni dopo la stampa: e-mail data più tardi, modalità, data della firma, nota.</summary>
    [HttpPost("api/ordini-accettazione/privati/{id:int}")]
    public async Task<IActionResult> ModificaOrdinePrivato(int id, [FromBody] ModificaPrivato req, CancellationToken ct)
    {
        var r = await _ctx.OrdiniPrivati.FirstOrDefaultAsync(o => o.IdOrdinePrivato == id, ct);
        if (r is null) return NotFound(new { messaggio = "Ordine non trovato." });
        var email = string.IsNullOrWhiteSpace(req.Email) ? null : req.Email.Trim();
        if (email is not null && !IndirizziEmail.Formato.IsMatch(email))
            return BadRequest(new { messaggio = "L'indirizzo e-mail non è valido." });
        var prima = new { r.EmailRapporto, r.Consegna, r.FirmatoIl, r.Note };
        r.EmailRapporto = email;
        r.Consegna = Enum.IsDefined(req.Consegna) ? req.Consegna : r.Consegna;
        if (r.Consegna != ConsegnaRapportoPrivato.AMano && email is null) r.Consegna = ConsegnaRapportoPrivato.AMano;
        r.FirmatoIl = req.FirmatoIl?.Date;
        r.Note = Tronca(req.Note, 1000);
        r.DataUltimaModifica = DateTime.UtcNow;
        r.UltimaModificaDa = Tronca(_utente.NomeCompleto ?? _utente.Email, 100);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Accettazione, "ordineaccettazione.privato.modifica", EsitoOperazione.Successo,
            tipoEntita: "OrdinePrivato", idEntita: id.ToString(), dettaglio: new { prima, dopo = new { r.EmailRapporto, r.Consegna, r.FirmatoIl, r.Note } }, ct: ct);
        return Json(new { messaggio = "Salvato." });
    }

    /// <summary>Perché il lotto non è leggibile: inesistente in ArcLab oppure ancora senza campioni salvati.</summary>
    private async Task<string> MotivoNonTrovatoAsync(int idLotto, CancellationToken ct) =>
        await _repo.CampioniDelLottoAsync(idLotto, ct) is null
            ? $"Il lotto con identificativo {idLotto} non esiste nel database ArcLab letto da LaboratorioAI."
            : $"Il lotto (identificativo {idLotto}) esiste in ArcLab ma non ha ancora campioni salvati: inserisci e salva i campioni, poi riprova.";

    private static string NomeConsegna(ConsegnaRapportoPrivato c) => c switch
    {
        ConsegnaRapportoPrivato.Email => "per e-mail",
        ConsegnaRapportoPrivato.Entrambi => "per e-mail e a mano",
        _ => "ritiro a mano"
    };

    private static string? Tronca(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim()[..max] : s.Trim());

    // ── Registro di controllo (cosa è partito e cosa no) ──
    [HttpGet("api/ordini-accettazione/registro")]
    public async Task<IActionResult> Registro([FromQuery] int? giorni, [FromQuery] bool soloProblemi, CancellationToken ct)
    {
        var righe = await _registro.ElencoAsync(ServizioRegistroDocumenti.AmbitoOrdine, giorni ?? 7, soloProblemi, ct);

        // Stato attuale della consegna per i lotti citati: una riga "Parziale" già risolta da un
        // tentativo successivo viene segnalata come risolta.
        var ids = righe.Where(r => r.IdLotto.HasValue).Select(r => r.IdLotto!.Value).Distinct().ToList();
        var stati = await _ctx.OrdiniAccettazioneEmessi.AsNoTracking()
            .Where(e => ids.Contains(e.IdLotto))
            .Select(e => new { e.IdLotto, e.StatoInvio })
            .ToDictionaryAsync(e => e.IdLotto, e => e.StatoInvio, ct);

        return Json(righe.Select(r => new
        {
            r.IdRegistroDocumento,
            r.IdEsecuzione,
            DataOra = DateTime.SpecifyKind(r.DataOra, DateTimeKind.Utc),
            r.Origine,
            r.Riferimento,
            r.IdLotto,
            Esito = r.Esito.ToString(),
            r.Messaggio,
            r.FilePalomar,
            r.PalomarVerificato,
            StatoAttuale = r.IdLotto is int il && stati.TryGetValue(il, out var s) ? s.ToString() : null
        }));
    }

    private static string RiferimentoLotto(string? codLotto, string? cliente, int idLotto) =>
        $"{(string.IsNullOrWhiteSpace(codLotto) ? "lotto " + idLotto : codLotto)}{(string.IsNullOrWhiteSpace(cliente) ? "" : " — " + cliente)}";

    /// <summary>Archivia il PDF su disco in &lt;cartella&gt;/AAAA/MM/ e ritorna il percorso completo.</summary>
    private async Task<string> SalvaPdfAsync(OrdineAccettazione ordine, string nomeFile, byte[] pdf, CancellationToken ct)
    {
        var baseDir = await ConfigAsync(ChiaveCartella, "", ct);
        if (string.IsNullOrWhiteSpace(baseDir))
            baseDir = Path.Combine(_ambiente.ContentRootPath, "App_Data", "OrdiniAccettazione");

        var data = ordine.DataRicezione ?? DateTime.Today;
        var dir = Path.Combine(baseDir, data.ToString("yyyy"), data.ToString("MM"));
        Directory.CreateDirectory(dir);

        var percorso = Path.Combine(dir, nomeFile);
        await System.IO.File.WriteAllBytesAsync(percorso, pdf, ct);
        return percorso;
    }
}

/// <summary>Pagina «Ordine da firmare» di un lotto (14/10/2026).</summary>
public sealed record ModelloOrdinePrivato(int IdLotto, OrdineAccettazione? Ordine, OrdinePrivato? Esistente, string? EmailProposta, string? Motivo = null);
