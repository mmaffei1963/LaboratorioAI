using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Preventivi/offerte commerciali. Compone le offerte a partire dai controlli e metodi ArcLab
/// (sola lettura) e le salva nello schema «preventivi». MAI scrittura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.PreventiviGestione)]
public class PreventiviController : Controller
{
    private const string ChiaveRevisione = "Preventivi.RevisioneCondizioni";
    private const string RevisioneDefault = "Mod. Com. 05 Rev. 18 del 16/04/25";

    /// <summary>Revisione corrente delle Condizioni Generali (da Configurazioni, con fallback al default).</summary>
    private async Task<string> RevisioneCorrenteAsync(CancellationToken ct)
    {
        var c = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == ChiaveRevisione, ct);
        return string.IsNullOrWhiteSpace(c?.Valore) ? RevisioneDefault : c!.Valore!;
    }

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioPreventivi _servizio;
    private readonly IRepositoryControlli _catalogo;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IRepositoryLaboratoriEsterni _labEsterni;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IGeneratoreOffertaPreventivo _offerta;
    private readonly IWebHostEnvironment _ambiente;

    public PreventiviController(
        ContestoLaboratorioAI ctx, IServizioPreventivi servizio, IRepositoryControlli catalogo,
        IRepositoryAnagrafiche anagrafiche, IRepositoryLaboratoriEsterni labEsterni,
        IContestoUtente utente, IServizioAudit audit, IGeneratoreOffertaPreventivo offerta,
        IWebHostEnvironment ambiente)
    {
        _ctx = ctx; _servizio = servizio; _catalogo = catalogo; _anagrafiche = anagrafiche;
        _labEsterni = labEsterni; _utente = utente; _audit = audit; _offerta = offerta; _ambiente = ambiente;
    }

    private const string ModelloCartaIntestata = "docx/Carta_intestata_Marea.docx";

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Preventivi";
        ViewData["PuoApprovare"] = _utente.HaPermesso(Permessi.PreventiviApprova);
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // Cataloghi (ArcLab, sola lettura) e listino
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Ricerca clienti ArcLab per ragione sociale (anagrafica mista: se non trovato si digita libero).</summary>
    [HttpGet("api/preventivi/clienti")]
    public async Task<IActionResult> Clienti([FromQuery] string? filtro, CancellationToken ct)
    {
        var clienti = await _anagrafiche.ClientiAsync(ct);
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var f = filtro.Trim();
            clienti = clienti
                .Where(c => c.Nome.Contains(f, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        return Json(clienti.Take(50).Select(c => new { c.Id, c.Nome }));
    }

    /// <summary>Controlli attivi da aggiungere al preventivo.</summary>
    [HttpGet("api/preventivi/controlli")]
    public async Task<IActionResult> Controlli(CancellationToken ct) =>
        Json(await _catalogo.ControlliAttiviAsync(ct));

    /// <summary>Verifica se un metodo è già presente tra i nostri (prima di inserirlo a campo libero).</summary>
    [HttpGet("api/preventivi/metodi")]
    public async Task<IActionResult> Metodi([FromQuery] string? filtro, CancellationToken ct) =>
        Json(await _servizio.CercaMetodiNostriAsync(filtro ?? "", ct));

    /// <summary>Laboratori esterni (dai nostri) con la frase da riportare in offerta per il subappalto.</summary>
    [HttpGet("api/preventivi/lab-esterni")]
    public async Task<IActionResult> LabEsterni(CancellationToken ct) =>
        Json(await _labEsterni.LaboratoriConFraseAsync(ct));

    /// <summary>Voci accessorie da listino (giudizio conformità, conservazione, ecc.).</summary>
    [HttpGet("api/preventivi/accessori")]
    public async Task<IActionResult> Accessori(CancellationToken ct) =>
        Json(await _ctx.ListinoAccessori.AsNoTracking()
            .Where(v => v.Attivo)
            .OrderBy(v => v.Descrizione)
            .Select(v => new { v.IdVoceAccessorio, v.Codice, v.Descrizione, v.Prezzo, v.UnitaMisura })
            .ToListAsync(ct));

    /// <summary>Impostazioni predefinite del modulo (revisione Condizioni Generali corrente).</summary>
    [HttpGet("api/preventivi/impostazioni")]
    public async Task<IActionResult> Impostazioni(CancellationToken ct) =>
        Json(new { RevisioneCondizioni = await RevisioneCorrenteAsync(ct) });

    /// <summary>Compone una riga da un controllo esistente (prezzo base a cascata + snapshot metodi).</summary>
    [HttpPost("api/preventivi/riga-controllo")]
    public async Task<IActionResult> RigaControllo([FromBody] RichiestaRigaControllo req, CancellationToken ct)
    {
        if (req.IdControllo <= 0) return BadRequest(new { messaggio = "Controllo non valido." });
        var riga = await _servizio.ComponiRigaControlloAsync(
            req.IdControllo, req.NomeControllo, req.IdClienteArcLab, req.ScontoPercentuale, ct);
        return Json(ProiettaRiga(riga));
    }

    // ═══════════════════════════════════════════════════════════════
    // Registro e dettaglio
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/preventivi/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.Preventivi.AsNoTracking()
            .Where(p => p.Attivo && p.DataCancellazione == null)
            .OrderByDescending(p => p.DataCreazione).Take(300)
            .Select(p => new
            {
                p.IdPreventivo, p.Numero, p.Anno, Stato = p.Stato.ToString(),
                p.ClienteRagioneSociale, p.Oggetto, p.DataOfferta,
                p.TotaleImponibile, p.RichiedeApprovazione,
                NumeroRighe = p.Righe.Count
            }).ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/preventivi/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var p = await _ctx.Preventivi.AsNoTracking()
            .Include(x => x.Righe).ThenInclude(r => r.Metodi)
            .FirstOrDefaultAsync(x => x.IdPreventivo == id, ct);
        if (p is null) return NotFound();

        return Json(new
        {
            p.IdPreventivo, p.Numero, p.Anno, Stato = (int)p.Stato, StatoTesto = p.Stato.ToString(),
            p.IdClienteArcLab, p.ClienteRagioneSociale, p.ClientePartitaIva, p.ClienteIndirizzo,
            p.ClienteCap, p.ClienteCitta, p.ClienteProvincia, p.ClienteEmail,
            p.Oggetto, p.DataOfferta, p.ValiditaGiorni, p.CondizioniPagamento, p.RevisioneCondizioni,
            p.TotaleImponibile, p.AliquotaIva, p.RichiedeApprovazione, p.Note,
            Righe = p.Righe.OrderBy(r => r.Progressivo).Select(ProiettaRiga)
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // Salvataggio / approvazione / eliminazione
    // ═══════════════════════════════════════════════════════════════

    [HttpPost("api/preventivi/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaPreventivo req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ClienteRagioneSociale))
            return BadRequest(new { messaggio = "Indicare il cliente (ragione sociale)." });
        var righe = req.Righe ?? new();
        if (righe.Count == 0)
            return BadRequest(new { messaggio = "Aggiungi almeno una riga al preventivo." });

        var ora = DateTime.UtcNow;
        Preventivo p;
        if (req.IdPreventivo is int id and > 0)
        {
            p = await _ctx.Preventivi.Include(x => x.Righe).ThenInclude(r => r.Metodi)
                    .FirstOrDefaultAsync(x => x.IdPreventivo == id, ct)
                ?? throw new InvalidOperationException("Preventivo non trovato.");
            if (p.Stato is StatoPreventivo.Inviato or StatoPreventivo.Accettato)
                return BadRequest(new { messaggio = "Preventivo già inviato/accettato: non modificabile." });
            foreach (var r in p.Righe) _ctx.RigheMetodoPreventivo.RemoveRange(r.Metodi);
            _ctx.RighePreventivo.RemoveRange(p.Righe);
            p.Righe.Clear();
            p.DataUltimaModifica = ora;
        }
        else
        {
            p = new Preventivo { DataCreazione = ora, DataUltimaModifica = ora, Stato = StatoPreventivo.Bozza };
            _ctx.Preventivi.Add(p);
        }

        // Testata
        p.IdClienteArcLab = req.IdClienteArcLab;
        p.ClienteRagioneSociale = req.ClienteRagioneSociale!.Trim();
        p.ClientePartitaIva = Pulisci(req.ClientePartitaIva);
        p.ClienteIndirizzo = Pulisci(req.ClienteIndirizzo);
        p.ClienteCap = Pulisci(req.ClienteCap);
        p.ClienteCitta = Pulisci(req.ClienteCitta);
        p.ClienteProvincia = Pulisci(req.ClienteProvincia);
        p.ClienteEmail = Pulisci(req.ClienteEmail);
        p.Oggetto = Pulisci(req.Oggetto);
        p.DataOfferta = req.DataOfferta?.Date ?? p.DataOfferta ?? DateTime.Today;
        p.ValiditaGiorni = req.ValiditaGiorni is int v && v > 0 ? v : 60;
        p.CondizioniPagamento = Pulisci(req.CondizioniPagamento);
        p.RevisioneCondizioni = string.IsNullOrWhiteSpace(req.RevisioneCondizioni)
            ? await RevisioneCorrenteAsync(ct) : req.RevisioneCondizioni!.Trim();
        p.AliquotaIva = req.AliquotaIva is decimal iva && iva >= 0 ? iva : 22m;
        p.Note = Pulisci(req.Note);
        p.IdUtenteCreatore ??= _utente.IdUtente;

        // Aggiorna la revisione corrente predefinita (per le prossime offerte), se richiesto.
        if (req.ImpostaRevisionePredefinita && !string.IsNullOrWhiteSpace(p.RevisioneCondizioni))
        {
            var cfg = await _ctx.Configurazioni.FirstOrDefaultAsync(x => x.Chiave == ChiaveRevisione, ct);
            if (cfg is null)
            {
                _ctx.Configurazioni.Add(new Configurazione
                {
                    Chiave = ChiaveRevisione,
                    Valore = p.RevisioneCondizioni,
                    Descrizione = "Revisione corrente delle Condizioni Generali di Fornitura per i preventivi",
                    Categoria = "Preventivi",
                    DataModifica = ora,
                    IdUtenteModifica = _utente.IdUtente
                });
            }
            else
            {
                cfg.Valore = p.RevisioneCondizioni;
                cfg.DataModifica = ora;
                cfg.IdUtenteModifica = _utente.IdUtente;
            }
        }

        // Righe (+ metodi)
        var prog = 1;
        foreach (var rd in righe)
        {
            var riga = new RigaPreventivo
            {
                Progressivo = rd.Progressivo > 0 ? rd.Progressivo : prog++,
                Tipo = (TipoRigaPreventivo)rd.Tipo,
                IdControlloArcLab = rd.IdControlloArcLab,
                IdPropostaControllo = rd.IdPropostaControllo,
                IdVoceAccessorio = rd.IdVoceAccessorio,
                Descrizione = rd.Descrizione,
                OriginePrezzo = (OriginePrezzoPreventivo)rd.OriginePrezzo,
                PrezzoBase = rd.PrezzoBase,
                ScontoPercentuale = rd.ScontoPercentuale,
                Quantita = rd.Quantita < 1 ? 1 : rd.Quantita,
                Note = rd.Note
            };
            var progM = 1;
            foreach (var md in rd.Metodi ?? new())
                riga.Metodi.Add(new RigaMetodoPreventivo
                {
                    Progressivo = md.Progressivo > 0 ? md.Progressivo : progM++,
                    IdMetodoArcLab = md.IdMetodoArcLab,
                    Libero = md.IdMetodoArcLab is null,
                    Descrizione = md.Descrizione,
                    DenominazioneRiga = md.DenominazioneRiga,
                    RiferimentoNormativo = md.RiferimentoNormativo,
                    UnitaMisura = md.UnitaMisura,
                    CostoMetodoEuro = md.CostoMetodoEuro,
                    Accreditato = md.Accreditato,
                    CifreSignificative = md.CifreSignificative,
                    MaxDecimali = md.MaxDecimali,
                    IdLabEsternoArcLab = md.IdLabEsternoArcLab,
                    LabEsterno = md.LabEsterno,
                    LabEsternoFrase = md.LabEsternoFrase
                });
            p.Righe.Add(riga);
        }

        // Prezzi finali, totale e flag approvazione (calcolo server-side, non ci si fida del client).
        _servizio.Ricalcola(p);

        // Stato richiesto (senza mai superare l'approvazione se serve e non è stata concessa).
        if (req.Stato is int s && Enum.IsDefined(typeof(StatoPreventivo), s))
        {
            var richiesto = (StatoPreventivo)s;
            if (p.RichiedeApprovazione && richiesto is StatoPreventivo.Inviato or StatoPreventivo.Accettato
                && p.Stato is not StatoPreventivo.Approvato)
                richiesto = StatoPreventivo.DaApprovare;
            p.Stato = richiesto;
        }

        // Numerazione progressiva per anno alla prima assegnazione.
        if (p.Numero is null)
        {
            var anno = (p.DataOfferta ?? DateTime.Today).Year;
            var max = await _ctx.Preventivi.Where(x => x.Anno == anno).MaxAsync(x => (int?)x.Numero, ct) ?? 0;
            p.Anno = anno;
            p.Numero = max + 1;
        }

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Preventivi, "preventivo.salvato", EsitoOperazione.Successo,
            tipoEntita: "Preventivo", idEntita: p.IdPreventivo.ToString(),
            dettaglio: new { p.Numero, p.Anno, p.ClienteRagioneSociale, Righe = p.Righe.Count, p.TotaleImponibile, Stato = p.Stato.ToString() }, ct: ct);

        return Json(new { p.IdPreventivo, p.Numero, p.Anno, Stato = (int)p.Stato, StatoTesto = p.Stato.ToString(), p.TotaleImponibile, p.RichiedeApprovazione });
    }

    /// <summary>Genera l'offerta in Word (RTF) o PDF a partire dal preventivo salvato.</summary>
    [HttpGet("api/preventivi/{id:int}/offerta")]
    public async Task<IActionResult> Offerta(int id, [FromQuery] string? formato, CancellationToken ct)
    {
        var p = await _ctx.Preventivi.AsNoTracking()
            .Include(x => x.Righe).ThenInclude(r => r.Metodi)
            .FirstOrDefaultAsync(x => x.IdPreventivo == id, ct);
        if (p is null) return NotFound();

        var pdf = string.Equals(formato, "pdf", StringComparison.OrdinalIgnoreCase);
        byte[] contenuto;
        string estensione, mime;
        if (pdf)
        {
            contenuto = _offerta.GeneraPdf(p);
            estensione = "pdf";
            mime = "application/pdf";
        }
        else
        {
            var modello = Path.Combine(_ambiente.WebRootPath, ModelloCartaIntestata);
            if (!System.IO.File.Exists(modello))
                return NotFound(new { messaggio = "Modello di carta intestata non trovato: " + ModelloCartaIntestata });
            contenuto = _offerta.GeneraWord(p, modello);
            estensione = "docx";
            mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        }
        await _audit.RegistraAsync(
            AreaFunzionale.Preventivi, "preventivo.offerta", EsitoOperazione.Successo,
            tipoEntita: "Preventivo", idEntita: id.ToString(),
            dettaglio: new { p.Numero, p.Anno, Formato = estensione }, ct: ct);
        return File(contenuto, mime, _offerta.NomeFile(p, estensione));
    }

    [HttpPost("api/preventivi/{id:int}/approva")]
    [RichiedePermesso(Permessi.PreventiviApprova)]
    public async Task<IActionResult> Approva(int id, CancellationToken ct)
    {
        var p = await _ctx.Preventivi.FirstOrDefaultAsync(x => x.IdPreventivo == id, ct);
        if (p is null) return NotFound();
        p.Stato = StatoPreventivo.Approvato;
        p.IdUtenteApprovatore = _utente.IdUtente;
        p.DataApprovazione = DateTime.UtcNow;
        p.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Preventivi, "preventivo.approvato", EsitoOperazione.Successo,
            tipoEntita: "Preventivo", idEntita: id.ToString(),
            dettaglio: new { p.Numero, p.Anno }, ct: ct);
        return Json(new { Stato = (int)p.Stato, StatoTesto = p.Stato.ToString() });
    }

    [HttpPost("api/preventivi/{id:int}/elimina")]
    public async Task<IActionResult> Elimina(int id, CancellationToken ct)
    {
        var p = await _ctx.Preventivi.FirstOrDefaultAsync(x => x.IdPreventivo == id, ct);
        if (p is null) return NotFound();
        if (p.Stato is StatoPreventivo.Inviato or StatoPreventivo.Accettato)
            return BadRequest(new { messaggio = "Preventivo già inviato/accettato: non eliminabile." });
        p.Attivo = false; p.DataCancellazione = DateTime.UtcNow; p.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Preventivi, "preventivo.eliminato", EsitoOperazione.Successo,
            tipoEntita: "Preventivo", idEntita: id.ToString(),
            dettaglio: new { p.Numero, p.Anno }, ct: ct);
        return Ok();
    }

    // ── Helpers ──
    private static string? Pulisci(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static object ProiettaRiga(RigaPreventivo r) => new
    {
        r.Progressivo, Tipo = (int)r.Tipo,
        r.IdControlloArcLab, r.IdPropostaControllo, r.IdVoceAccessorio,
        r.Descrizione, OriginePrezzo = (int)r.OriginePrezzo,
        r.PrezzoBase, r.ScontoPercentuale, r.Quantita, r.PrezzoFinale, r.Note,
        Metodi = r.Metodi.OrderBy(m => m.Progressivo).Select(m => new
        {
            m.Progressivo, m.IdMetodoArcLab, m.Libero, m.Descrizione, m.DenominazioneRiga,
            m.RiferimentoNormativo, m.UnitaMisura, m.CostoMetodoEuro, m.Accreditato,
            m.CifreSignificative, m.MaxDecimali, m.IdLabEsternoArcLab, m.LabEsterno, m.LabEsternoFrase
        })
    };
}

// ═══════════════════════════════════════════════════════════════
// DTO
// ═══════════════════════════════════════════════════════════════

public record RichiestaRigaControllo
{
    public int IdControllo { get; init; }
    public string? NomeControllo { get; init; }
    public int? IdClienteArcLab { get; init; }
    public decimal ScontoPercentuale { get; init; }
}

public record RichiestaSalvaPreventivo
{
    public int? IdPreventivo { get; init; }
    public int? Stato { get; init; }

    public int? IdClienteArcLab { get; init; }
    public string? ClienteRagioneSociale { get; init; }
    public string? ClientePartitaIva { get; init; }
    public string? ClienteIndirizzo { get; init; }
    public string? ClienteCap { get; init; }
    public string? ClienteCitta { get; init; }
    public string? ClienteProvincia { get; init; }
    public string? ClienteEmail { get; init; }

    public string? Oggetto { get; init; }
    public DateTime? DataOfferta { get; init; }
    public int? ValiditaGiorni { get; init; }
    public string? CondizioniPagamento { get; init; }
    public string? RevisioneCondizioni { get; init; }
    public bool ImpostaRevisionePredefinita { get; init; }
    public decimal? AliquotaIva { get; init; }
    public string? Note { get; init; }

    public List<RigaPreventivoDto>? Righe { get; init; }
}

public record RigaPreventivoDto
{
    public int Progressivo { get; init; }
    public int Tipo { get; init; }
    public int? IdControlloArcLab { get; init; }
    public int? IdPropostaControllo { get; init; }
    public int? IdVoceAccessorio { get; init; }
    public string? Descrizione { get; init; }
    public int OriginePrezzo { get; init; }
    public decimal PrezzoBase { get; init; }
    public decimal ScontoPercentuale { get; init; }
    public int Quantita { get; init; }
    public string? Note { get; init; }
    public List<RigaMetodoPreventivoDto>? Metodi { get; init; }
}

public record RigaMetodoPreventivoDto
{
    public int Progressivo { get; init; }
    public int? IdMetodoArcLab { get; init; }
    public string? Descrizione { get; init; }
    public string? DenominazioneRiga { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public string? UnitaMisura { get; init; }
    public decimal? CostoMetodoEuro { get; init; }
    public bool Accreditato { get; init; }
    public int? CifreSignificative { get; init; }
    public int? MaxDecimali { get; init; }
    public int? IdLabEsternoArcLab { get; init; }
    public string? LabEsterno { get; init; }
    public string? LabEsternoFrase { get; init; }
}
