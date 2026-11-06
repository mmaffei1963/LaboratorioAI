using System.Text.RegularExpressions;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Condizioni Generali di Fornitura e firme dei clienti — Fase 1 (09/10/2026, Specifica v0.4):
/// revisioni delle Condizioni con PDF congelato e clausole da approvare, Firmatari dei clienti, richieste di
/// approvazione e registro delle evidenze a impronte concatenate. La pagina di firma (Portale) arriva con la Fase 2.
/// Dati dei clienti letti da ArcLab in sola lettura. Nessuna cancellazione fisica.
/// </summary>
[RichiedePermesso(Permessi.CondizioniFirmatari)]
public class CondizioniController : Controller
{
    private const long MaxPdf = 15_000_000;

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRegistroEvidenzeApprovazioni _registro;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly ServizioApprovazioni _approvazioni;
    private readonly IConfiguration _config;

    public CondizioniController(ContestoLaboratorioAI ctx, IRegistroEvidenzeApprovazioni registro,
        IRepositoryAnagrafiche anagrafiche, IContestoUtente utente, IServizioAudit audit, ServizioApprovazioni approvazioni,
        IConfiguration config)
    {
        _ctx = ctx; _registro = registro; _anagrafiche = anagrafiche; _utente = utente; _audit = audit; _approvazioni = approvazioni;
        _config = config;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Condizioni e firme";
        return View();
    }

    private OperatoreApprovazioni Operatore => new(_utente.IdUtente, _utente.NomeCompleto, _utente.IndirizzoIp, Agente);

    private string? Agente => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private Task<EventoApprovazione> RegistraAsync(string tipo, int? idRichiesta, int? idCliente, object? dati, CancellationToken ct) =>
        _registro.AggiungiAsync(tipo, idRichiesta, idCliente, dati, _utente.NomeCompleto, _utente.IndirizzoIp, Agente, ct);

    // ═══════════════════════════════════════════════════════════════
    // Revisioni delle Condizioni e clausole (permesso condizioni.gestione)
    // ═══════════════════════════════════════════════════════════════

    // Riferimento della revisione (10/10/2026): non si scrive a mano, si compone da numero e data nello stesso
    // formato della voce «Revisione Condizioni Generali» di Impostazioni documenti (es. «Mod. Com. 05 Rev. 18 del 16/04/25»).
    private const string ChiaveRevisioneImpostazioni = "Preventivi.RevisioneCondizioni";
    private const string PrefissoPredefinito = "Mod. Com. 05";
    private static readonly Regex RiferimentoRevisione = new(@"^(?<prefisso>.*?)\s*Rev\.?\s*(?<numero>\d+)(?:\s+del\s+(?<data>\d{1,2}/\d{1,2}/\d{2,4}))?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private async Task<string?> RiferimentoImpostazioniAsync(CancellationToken ct) =>
        (await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == ChiaveRevisioneImpostazioni, ct))?.Valore?.Trim();

    private static string Prefisso(string? riferimentoImpostazioni)
    {
        var m = RiferimentoRevisione.Match(riferimentoImpostazioni ?? "");
        var p = m.Success ? m.Groups["prefisso"].Value.Trim() : "";
        return p.Length > 0 ? p : PrefissoPredefinito;
    }

    /// <summary>«Mod. Com. 05 Rev. 19 del 16/10/26»: stesso formato delle Impostazioni documenti.</summary>
    private static string ComponiRiferimento(string prefisso, int numero, DateTime? data) =>
        $"{prefisso} Rev. {numero}" + (data is DateTime d ? $" del {d:dd/MM/yy}" : "");

    /// <summary>
    /// Proposta per una revisione nuova: prefisso, numero e data letti da Impostazioni documenti; se quella revisione
    /// è già in elenco, numero successivo all'ultimo e data da indicare.
    /// </summary>
    [HttpGet("api/condizioni/revisioni/proposta")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> Proposta(CancellationToken ct)
    {
        var impostazioni = await RiferimentoImpostazioniAsync(ct);
        var prefisso = Prefisso(impostazioni);
        var m = RiferimentoRevisione.Match(impostazioni ?? "");
        int? numero = m.Success ? int.Parse(m.Groups["numero"].Value) : null;
        DateTime? data = m.Success && m.Groups["data"].Success
            && DateTime.TryParseExact(m.Groups["data"].Value, new[] { "dd/MM/yy", "d/M/yy", "dd/MM/yyyy", "d/M/yyyy" },
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d) ? d : null;
        var esistenti = await _ctx.RevisioniCondizioni.AsNoTracking().Where(x => x.Attivo).Select(x => x.NumeroRevisione).ToListAsync(ct);
        if (numero is null || esistenti.Contains(numero.Value))
        {
            numero = (esistenti.Count > 0 ? esistenti.Max() : (numero ?? 0)) + 1;
            data = null;
        }
        return Json(new { prefisso, riferimentoImpostazioni = impostazioni, numero, data = data?.ToString("yyyy-MM-dd") });
    }

    [HttpGet("api/condizioni/revisioni")]
    public async Task<IActionResult> Revisioni(CancellationToken ct)
    {
        var righe = await _ctx.RevisioniCondizioni.AsNoTracking().Where(r => r.Attivo)
            .OrderByDescending(r => r.NumeroRevisione).ThenByDescending(r => r.IdRevisioneCondizioni)
            .Select(r => new
            {
                r.IdRevisioneCondizioni, r.Codice, r.NumeroRevisione,
                DataRevisione = r.DataRevisione.HasValue ? r.DataRevisione.Value.ToString("dd/MM/yyyy") : null,
                DataEntrataVigore = r.DataEntrataVigore.HasValue ? r.DataEntrataVigore.Value.ToString("dd/MM/yyyy") : null,
                Stato = r.Stato.ToString(), r.RichiedeNuovaApprovazione, r.NomeFilePdf, r.DimensionePdf, r.ImprontaPdf,
                DataPubblicazione = r.DataPubblicazione.HasValue ? r.DataPubblicazione.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : null,
                r.NomeUtentePubblicazione,
                Clausole = r.Clausole.Count(c => c.Attivo)
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/condizioni/revisioni/{id:int}")]
    public async Task<IActionResult> Revisione(int id, CancellationToken ct)
    {
        var r = await _ctx.RevisioniCondizioni.AsNoTracking().Include(x => x.Clausole)
            .FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct);
        if (r is null) return NotFound(new { messaggio = "Revisione non trovata." });
        return Json(new
        {
            r.IdRevisioneCondizioni, r.Codice, r.NumeroRevisione,
            DataRevisione = r.DataRevisione?.ToString("yyyy-MM-dd"),
            DataEntrataVigore = r.DataEntrataVigore?.ToString("yyyy-MM-dd"),
            Stato = r.Stato.ToString(), r.RichiedeNuovaApprovazione, r.TestoCosaCambia, r.Note,
            r.NomeFilePdf, r.DimensionePdf, r.ImprontaPdf,
            Clausole = r.Clausole.Where(c => c.Attivo).OrderBy(c => c.Ordine)
                .Select(c => new { c.IdClausolaCondizioni, c.Articolo, c.ContenutoBreve, c.TestoIntegrale })
        });
    }

    /// <summary>Clausole predefinite della Rev. 19, per precompilare una revisione nuova.</summary>
    [HttpGet("api/condizioni/clausole-predefinite")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public IActionResult ClausolePredefinite() =>
        Json(ClausoleCondizioniPredefinite.Rev19.Select(c => new { articolo = c.Articolo, contenutoBreve = c.ContenutoBreve }));

    [HttpPost("api/condizioni/revisioni/salva")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> SalvaRevisione([FromBody] RichiestaSalvaRevisione req, CancellationToken ct)
    {
        if (req.NumeroRevisione <= 0) return BadRequest(new { messaggio = "Indica il numero di revisione." });
        if (req.DataRevisione is null) return BadRequest(new { messaggio = "Indica la data della revisione: fa parte del riferimento stampato." });
        var clausole = (req.Clausole ?? new()).Where(c => !string.IsNullOrWhiteSpace(c.Articolo) || !string.IsNullOrWhiteSpace(c.ContenutoBreve)).ToList();
        if (clausole.Any(c => string.IsNullOrWhiteSpace(c.Articolo) || string.IsNullOrWhiteSpace(c.ContenutoBreve)))
            return BadRequest(new { messaggio = "Ogni clausola deve avere numero di articolo e contenuto breve." });

        var ora = DateTime.UtcNow;
        RevisioneCondizioni r;
        if (req.Id is int id)
        {
            r = await _ctx.RevisioniCondizioni.Include(x => x.Clausole).FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct)
                ?? throw new InvalidOperationException("Revisione non trovata.");
            if (r.Stato != StatoRevisioneCondizioni.Bozza)
                return BadRequest(new { messaggio = "La revisione è pubblicata: non si può più modificare. Per cambiarla crea una revisione nuova." });
        }
        else
        {
            r = new RevisioneCondizioni { DataCreazione = ora, IdUtenteCreazione = _utente.IdUtente };
            _ctx.RevisioniCondizioni.Add(r);
        }

        r.Codice = ComponiRiferimento(Prefisso(await RiferimentoImpostazioniAsync(ct)), req.NumeroRevisione, req.DataRevisione);
        r.NumeroRevisione = req.NumeroRevisione;
        r.DataRevisione = req.DataRevisione?.Date;
        r.DataEntrataVigore = req.DataEntrataVigore?.Date;
        r.RichiedeNuovaApprovazione = req.RichiedeNuovaApprovazione;
        r.TestoCosaCambia = string.IsNullOrWhiteSpace(req.TestoCosaCambia) ? null : req.TestoCosaCambia.Trim();
        r.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();
        r.DataUltimaModifica = ora;

        // Clausole: aggiorna in posizione, aggiunge le nuove, disattiva quelle tolte (nessuna cancellazione fisica).
        var attive = r.Clausole.Where(c => c.Attivo).OrderBy(c => c.Ordine).ToList();
        for (var i = 0; i < clausole.Count; i++)
        {
            var c = clausole[i];
            var esistente = c.Id is int idc ? attive.FirstOrDefault(x => x.IdClausolaCondizioni == idc) : null;
            if (esistente is null) { esistente = new ClausolaCondizioni(); r.Clausole.Add(esistente); }
            else attive.Remove(esistente);
            esistente.Ordine = i + 1;
            esistente.Articolo = c.Articolo!.Trim();
            esistente.ContenutoBreve = c.ContenutoBreve!.Trim();
            esistente.TestoIntegrale = string.IsNullOrWhiteSpace(c.TestoIntegrale) ? null : c.TestoIntegrale.Trim();
            esistente.Attivo = true;
        }
        foreach (var tolta in attive) tolta.Attivo = false;

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Contratti, "condizioni.revisione.salvata", EsitoOperazione.Successo,
            tipoEntita: "RevisioneCondizioni", idEntita: r.IdRevisioneCondizioni.ToString(),
            dettaglio: new { r.Codice, Clausole = clausole.Count }, ct: ct);
        return Json(new { idRevisioneCondizioni = r.IdRevisioneCondizioni, messaggio = "Revisione salvata (bozza)." });
    }

    [HttpPost("api/condizioni/revisioni/{id:int}/pdf")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    [RequestSizeLimit(MaxPdf + 100_000)]
    public async Task<IActionResult> CaricaPdf(int id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Scegli il PDF delle Condizioni." });
        if (file.Length > MaxPdf) return BadRequest(new { messaggio = "Il PDF supera i 15 MB." });
        var r = await _ctx.RevisioniCondizioni.FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct);
        if (r is null) return NotFound(new { messaggio = "Revisione non trovata." });
        if (r.Stato != StatoRevisioneCondizioni.Bozza) return BadRequest(new { messaggio = "La revisione è pubblicata: il PDF non si può più sostituire." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var dati = ms.ToArray();
        if (dati.Length < 5 || dati[0] != (byte)'%' || dati[1] != (byte)'P' || dati[2] != (byte)'D' || dati[3] != (byte)'F')
            return BadRequest(new { messaggio = "Il file non è un PDF." });

        r.ContenutoPdf = dati;
        r.DimensionePdf = dati.Length;
        r.NomeFilePdf = Path.GetFileName(file.FileName);
        r.ImprontaPdf = CatenaEvidenze.Sha256(dati);
        r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Json(new { messaggio = "PDF caricato.", r.NomeFilePdf, r.DimensionePdf, r.ImprontaPdf });
    }

    [HttpGet("api/condizioni/revisioni/{id:int}/pdf")]
    public async Task<IActionResult> ScaricaPdf(int id, CancellationToken ct)
    {
        var r = await _ctx.RevisioniCondizioni.AsNoTracking()
            .Where(x => x.IdRevisioneCondizioni == id && x.Attivo)
            .Select(x => new { x.ContenutoPdf, x.NomeFilePdf }).FirstOrDefaultAsync(ct);
        if (r?.ContenutoPdf is null) return NotFound(new { messaggio = "PDF non presente." });
        return File(r.ContenutoPdf, "application/pdf", r.NomeFilePdf ?? $"Condizioni_{id}.pdf");
    }

    /// <summary>Congela la revisione: da qui PDF e clausole non cambiano più e si possono chiedere le approvazioni.</summary>
    [HttpPost("api/condizioni/revisioni/{id:int}/pubblica")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> Pubblica(int id, CancellationToken ct)
    {
        var r = await _ctx.RevisioniCondizioni.Include(x => x.Clausole).FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct);
        if (r is null) return NotFound(new { messaggio = "Revisione non trovata." });
        if (r.Stato != StatoRevisioneCondizioni.Bozza) return BadRequest(new { messaggio = "La revisione è già pubblicata." });
        var mancanti = new List<string>();
        if (r.ContenutoPdf is null) mancanti.Add("il PDF");
        if (r.DataEntrataVigore is null) mancanti.Add("la data di entrata in vigore");
        if (!r.Clausole.Any(c => c.Attivo)) mancanti.Add("almeno una clausola da approvare");
        if (mancanti.Count > 0) return BadRequest(new { messaggio = "Per pubblicare manca " + string.Join(", ", mancanti) + "." });
        if (await _ctx.RevisioniCondizioni.AnyAsync(x => x.Attivo && x.Stato == StatoRevisioneCondizioni.Pubblicata
                && x.NumeroRevisione == r.NumeroRevisione && x.IdRevisioneCondizioni != id, ct))
            return BadRequest(new { messaggio = $"Esiste già una Rev. {r.NumeroRevisione} pubblicata." });

        r.ImprontaPdf = CatenaEvidenze.Sha256(r.ContenutoPdf!);
        r.Stato = StatoRevisioneCondizioni.Pubblicata;
        r.DataPubblicazione = DateTime.UtcNow;
        r.IdUtentePubblicazione = _utente.IdUtente;
        r.NomeUtentePubblicazione = _utente.NomeCompleto;
        r.DataUltimaModifica = r.DataPubblicazione.Value;

        // L'evento salva anche il cambio di stato, nella stessa transazione. Nel registro restano il PDF (impronta)
        // e l'elenco esatto delle clausole pubblicate.
        await RegistraAsync("revisione.pubblicata", null, null, new
        {
            r.IdRevisioneCondizioni, r.Codice, r.NumeroRevisione,
            DataEntrataVigore = r.DataEntrataVigore?.ToString("yyyy-MM-dd"),
            r.RichiedeNuovaApprovazione, r.NomeFilePdf, r.ImprontaPdf,
            Clausole = r.Clausole.Where(c => c.Attivo).OrderBy(c => c.Ordine).Select(c => new { c.Articolo, c.ContenutoBreve })
        }, ct);
        var impostazioni = await RiferimentoImpostazioniAsync(ct);
        var avviso = string.Equals(impostazioni, r.Codice, StringComparison.OrdinalIgnoreCase) ? ""
            : $" Nota: in Impostazioni documenti la revisione delle Condizioni è ancora «{impostazioni ?? "non impostata"}». Offerte e Ordini di Accettazione citano quella finché non la cambi in «{r.Codice}», di solito alla data di entrata in vigore.";
        return Json(new { messaggio = $"{r.Codice} pubblicata. Impronta del PDF: {r.ImprontaPdf}." + avviso });
    }

    /// <summary>Nuova revisione in bozza che copia le clausole di una esistente (numero + 1).</summary>
    [HttpPost("api/condizioni/revisioni/{id:int}/copia")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> Copia(int id, CancellationToken ct)
    {
        var r = await _ctx.RevisioniCondizioni.AsNoTracking().Include(x => x.Clausole).FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct);
        if (r is null) return NotFound(new { messaggio = "Revisione non trovata." });
        var ora = DateTime.UtcNow;
        var nuova = new RevisioneCondizioni
        {
            Codice = ComponiRiferimento(Prefisso(await RiferimentoImpostazioniAsync(ct)), r.NumeroRevisione + 1, null),
            NumeroRevisione = r.NumeroRevisione + 1,
            RichiedeNuovaApprovazione = true,
            DataCreazione = ora, DataUltimaModifica = ora, IdUtenteCreazione = _utente.IdUtente,
            Clausole = r.Clausole.Where(c => c.Attivo).OrderBy(c => c.Ordine).Select((c, i) => new ClausolaCondizioni
            {
                Ordine = i + 1, Articolo = c.Articolo, ContenutoBreve = c.ContenutoBreve, TestoIntegrale = c.TestoIntegrale
            }).ToList()
        };
        _ctx.RevisioniCondizioni.Add(nuova);
        await _ctx.SaveChangesAsync(ct);
        return Json(new { idRevisioneCondizioni = nuova.IdRevisioneCondizioni, messaggio = "Creata una nuova revisione in bozza con le stesse clausole." });
    }

    /// <summary>
    /// Ritira una revisione pubblicata per errore (10/10/2026): possibile solo se non ha richieste di approvazione
    /// (annullate escluse). Resta nel database e nel registro delle evidenze con il motivo; il numero torna libero.
    /// </summary>
    [HttpPost("api/condizioni/revisioni/{id:int}/ritira")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> Ritira(int id, [FromBody] RichiestaMotivo req, CancellationToken ct)
    {
        var motivo = req?.Motivo?.Trim();
        if (string.IsNullOrEmpty(motivo)) return BadRequest(new { messaggio = "Indica il motivo." });
        var r = await _ctx.RevisioniCondizioni.FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct);
        if (r is null) return NotFound(new { messaggio = "Revisione non trovata." });
        if (r.Stato != StatoRevisioneCondizioni.Pubblicata) return BadRequest(new { messaggio = "Solo una revisione pubblicata si ritira; una bozza si elimina." });
        var richieste = await _ctx.RichiesteApprovazione.CountAsync(x => x.IdRevisioneCondizioni == id && x.Stato != StatoRichiestaApprovazione.Annullata, ct);
        if (richieste > 0)
            return BadRequest(new { messaggio = $"La revisione ha {richieste} richieste di approvazione: annullale prima (le approvate non si possono annullare, e allora la revisione non si ritira)." });
        r.Attivo = false;
        r.DataUltimaModifica = DateTime.UtcNow;
        await RegistraAsync("revisione.ritirata", null, null, new { r.IdRevisioneCondizioni, r.Codice, r.ImprontaPdf, Motivo = motivo }, ct);
        return Json(new { messaggio = $"{r.Codice} ritirata." });
    }

    /// <summary>Elimina una revisione ancora in bozza (Attivo = false). Le pubblicate non si eliminano.</summary>
    [HttpPost("api/condizioni/revisioni/{id:int}/elimina")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> EliminaRevisione(int id, CancellationToken ct)
    {
        var r = await _ctx.RevisioniCondizioni.FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == id && x.Attivo, ct);
        if (r is null) return NotFound(new { messaggio = "Revisione non trovata." });
        if (r.Stato != StatoRevisioneCondizioni.Bozza) return BadRequest(new { messaggio = "Una revisione pubblicata non si elimina." });
        r.Attivo = false;
        r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return Json(new { messaggio = "Bozza eliminata." });
    }

    // ═══════════════════════════════════════════════════════════════
    // Clienti e Firmatari (permesso condizioni.firmatari)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/condizioni/clienti")]
    public async Task<IActionResult> Clienti([FromQuery] string? filtro, CancellationToken ct)
    {
        var f = (filtro ?? "").Trim();
        if (f.Length < 2) return Json(Array.Empty<object>());
        // Ricerca tollerante e ordinata per pertinenza (15/10/2026): «marea», «mare-a» trovano «MARE.A S.R.L.» anche tra tanti «MARE…».
        var clienti = RicercaNomi.Cerca(await _anagrafiche.ClientiAsync(ct), c => c.Nome, f, 40);
        return Json(clienti.Select(c => new { c.Id, c.Nome }));
    }

    /// <summary>Scheda del cliente: dati ArcLab, Firmatario attivo e precedenti, richieste, offerte.</summary>
    [HttpGet("api/condizioni/clienti/{idCliente:int}")]
    public async Task<IActionResult> Cliente(int idCliente, CancellationToken ct)
    {
        var dettaglio = await _anagrafiche.DettaglioClienteAsync(idCliente, ct);
        if (dettaglio is null) return NotFound(new { messaggio = "Cliente non trovato in ArcLab." });
        var nome = (await _anagrafiche.ClientiAsync(ct)).FirstOrDefault(c => c.Id == idCliente)?.Nome;

        var firmatari = await _ctx.FirmatariClienti.AsNoTracking().Where(x => x.IdClienteArcLab == idCliente)
            .OrderByDescending(x => x.Attivo).ThenByDescending(x => x.DataDesignazione).ToListAsync(ct);
        var richieste = await Proietta(_ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.IdClienteArcLab == idCliente)).ToListAsync(ct);
        var offerte = await _ctx.Preventivi.AsNoTracking()
            .Where(p => p.IdClienteArcLab == idCliente && p.Attivo && p.Numero != null)
            .OrderByDescending(p => p.DataOfferta)
            .Select(p => new { p.IdPreventivo, p.Anno, p.Numero, DataOfferta = p.DataOfferta.HasValue ? p.DataOfferta.Value.ToString("dd/MM/yyyy") : null, Stato = p.Stato.ToString(), p.Oggetto })
            .Take(20).ToListAsync(ct);

        return Json(new
        {
            idCliente, nome, dettaglio.PartitaIva, dettaglio.Email,
            firmatari = firmatari.Select(x => new
            {
                x.IdFirmatarioCliente, x.Nome, x.Cognome, x.Qualifica, x.Email, x.CodiceFiscale, x.Note, x.Attivo,
                DataDesignazione = x.DataDesignazione.ToLocalTime().ToString("dd/MM/yyyy"), x.NomeUtenteDesignazione
            }),
            richieste = richieste.Select(FormatoRichiesta),
            offerte,
            inviti = (await _ctx.InvitiFirmatario.AsNoTracking().Where(x => x.IdClienteArcLab == idCliente)
                .OrderByDescending(x => x.IdInvitoFirmatario).Take(10).ToListAsync(ct)).Select(FormatoInvito)
        });
    }

    [HttpPost("api/condizioni/firmatari/salva")]
    public async Task<IActionResult> SalvaFirmatario([FromBody] RichiestaFirmatario req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nome) || string.IsNullOrWhiteSpace(req.Cognome))
            return BadRequest(new { messaggio = "Nome e cognome del Firmatario sono obbligatori." });
        var email = (req.Email ?? "").Trim();
        if (!ServizioApprovazioni.FormatoEmail.IsMatch(email)) return BadRequest(new { messaggio = "Indirizzo e-mail non valido." });
        if (ServizioApprovazioni.EmailGenerica(email) && !req.ConfermaEmailGenerica)
            return Json(new { richiedeConferma = true, messaggio = $"«{email}» sembra una casella generica: il link personale deve arrivare a un indirizzo nominativo del Firmatario. Confermi comunque?" });

        var dettaglio = await _anagrafiche.DettaglioClienteAsync(req.IdCliente, ct);
        if (dettaglio is null) return BadRequest(new { messaggio = "Cliente non trovato in ArcLab." });
        var nome = (await _anagrafiche.ClientiAsync(ct)).FirstOrDefault(c => c.Id == req.IdCliente)?.Nome;

        var (f, riassegnate) = await _approvazioni.DesignaFirmatarioAsync(req.IdCliente, nome, dettaglio.PartitaIva,
            req.Nome, req.Cognome, req.Qualifica, email, req.CodiceFiscale, req.Note, Operatore, ct);
        return Json(new
        {
            messaggio = $"Firmatario designato: {f.Nome} {f.Cognome}." + (riassegnate > 0 ? $" {riassegnate} richiesta/e aperte passate a lui." : ""),
            idFirmatarioCliente = f.IdFirmatarioCliente
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // Richieste di approvazione
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Riga dell'elenco richieste, senza i PDF (che restano nel database).</summary>
    private sealed record RigaRichiesta(int IdRichiestaApprovazione, int IdClienteArcLab, string? ClienteRagioneSociale, string? ClientePartitaIva,
        string? Revisione, int? IdPreventivo, string? OffertaRiferimento, string? FirmatarioNome, string? FirmatarioCognome, string? FirmatarioEmail,
        StatoRichiestaApprovazione Stato, ModalitaApprovazione? Modalita, DateTime DataCreazione, DateTime? DataInvio, DateTime? DataApertura,
        DateTime? DataPerfezionamento, int NumeroPromemoria, string? Note, string? NumeroRicevuta, bool HaRicevuta, DateTime? DataInvioRicevuta,
        string? ErroreInvioRicevuta, string? NomeDichiarato, string? CognomeDichiarato);

    private static IQueryable<RigaRichiesta> Proietta(IQueryable<RichiestaApprovazione> q) =>
        q.OrderByDescending(x => x.DataCreazione).Select(x => new RigaRichiesta(
            x.IdRichiestaApprovazione, x.IdClienteArcLab, x.ClienteRagioneSociale, x.ClientePartitaIva,
            x.Revisione!.Codice, x.IdPreventivo, x.OffertaRiferimento,
            x.Firmatario != null ? x.Firmatario.Nome : null, x.Firmatario != null ? x.Firmatario.Cognome : null, x.Firmatario != null ? x.Firmatario.Email : null,
            x.Stato, x.Modalita, x.DataCreazione, x.DataInvio, x.DataApertura, x.DataPerfezionamento, x.NumeroPromemoria, x.Note,
            x.NumeroRicevuta, x.RicevutaPdf != null, x.DataInvioRicevuta, x.ErroreInvioRicevuta, x.NomeDichiarato, x.CognomeDichiarato));

    private static string? Locale(DateTime? utc, string formato) =>
        utc is DateTime d ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToLocalTime().ToString(formato) : null;

    private static object FormatoRichiesta(RigaRichiesta x) => new
    {
        x.IdRichiestaApprovazione, x.IdClienteArcLab, x.ClienteRagioneSociale, x.ClientePartitaIva,
        x.Revisione, x.IdPreventivo, x.OffertaRiferimento,
        Firmatario = x.FirmatarioNome is null ? null : $"{x.FirmatarioNome} {x.FirmatarioCognome}",
        x.FirmatarioEmail,
        Stato = x.Stato.ToString(), Modalita = x.Modalita?.ToString(),
        DataCreazione = Locale(x.DataCreazione, "dd/MM/yyyy"),
        DataInvio = Locale(x.DataInvio, "dd/MM/yyyy"),
        DataApertura = Locale(x.DataApertura, "dd/MM/yyyy HH:mm"),
        DataPerfezionamento = Locale(x.DataPerfezionamento, "dd/MM/yyyy HH:mm"),
        GiorniAttesa = x.DataInvio.HasValue && x.Stato is StatoRichiestaApprovazione.Inviata or StatoRichiestaApprovazione.Aperta
            ? (int)(DateTime.UtcNow - x.DataInvio.Value).TotalDays : (int?)null,
        x.NumeroPromemoria, x.Note, x.NumeroRicevuta, x.HaRicevuta,
        DataInvioRicevuta = Locale(x.DataInvioRicevuta, "dd/MM/yyyy HH:mm"), x.ErroreInvioRicevuta,
        Dichiarante = x.NomeDichiarato is null ? null : $"{x.NomeDichiarato} {x.CognomeDichiarato}"
    };

    [HttpGet("api/condizioni/richieste")]
    public async Task<IActionResult> Richieste([FromQuery] string? stato, [FromQuery] string? filtro, CancellationToken ct)
    {
        var q = _ctx.RichiesteApprovazione.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(stato) && Enum.TryParse<StatoRichiestaApprovazione>(stato, out var s)) q = q.Where(x => x.Stato == s);
        else if (stato != "tutte") q = q.Where(x => x.Stato != StatoRichiestaApprovazione.Annullata);
        if (!string.IsNullOrWhiteSpace(filtro)) { var f = filtro.Trim(); q = q.Where(x => x.ClienteRagioneSociale!.Contains(f)); }
        var righe = await Proietta(q).Take(1000).ToListAsync(ct);
        return Json(righe.Select(FormatoRichiesta));
    }

    /// <summary>
    /// Crea la richiesta di approvazione per un cliente: revisione pubblicata (la più recente se non indicata),
    /// Firmatario attivo, Offerta facoltativa. L'invio del link arriva con la Fase 2.
    /// </summary>
    [HttpPost("api/condizioni/richieste/crea")]
    public async Task<IActionResult> CreaRichiesta([FromBody] RichiestaCreaApprovazione req, CancellationToken ct)
    {
        var e = await _approvazioni.CreaRichiestaAsync(req.IdCliente, req.IdRevisione, req.IdPreventivo, req.Note, Operatore, ct);
        return e.Riuscito
            ? Json(new { messaggio = e.Messaggio + " Ora puoi inviare il link al Firmatario.", idRichiestaApprovazione = e.Id })
            : BadRequest(new { messaggio = e.Messaggio });
    }

    /// <summary>Invia (o reinvia) al Firmatario il link personale alla pagina di approvazione sul Portale.</summary>
    [HttpPost("api/condizioni/richieste/{id:int}/invia")]
    public async Task<IActionResult> InviaLink(int id, CancellationToken ct)
    {
        var e = await _approvazioni.InviaLinkAsync(id, Operatore, ct);
        return e.Riuscito ? Json(new { messaggio = e.Messaggio }) : BadRequest(new { messaggio = e.Messaggio });
    }

    /// <summary>Ricevuta PDF di una richiesta approvata.</summary>
    [HttpGet("api/condizioni/richieste/{id:int}/ricevuta")]
    public async Task<IActionResult> Ricevuta(int id, CancellationToken ct)
    {
        var r = await _ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.IdRichiestaApprovazione == id)
            .Select(x => new { x.RicevutaPdf, x.NumeroRicevuta }).FirstOrDefaultAsync(ct);
        if (r?.RicevutaPdf is null) return NotFound(new { messaggio = "Ricevuta non ancora generata." });
        return File(r.RicevutaPdf, "application/pdf", $"Ricevuta_{r.NumeroRicevuta}.pdf");
    }

    [HttpPost("api/condizioni/richieste/{id:int}/annulla")]
    public async Task<IActionResult> AnnullaRichiesta(int id, [FromBody] RichiestaMotivo req, CancellationToken ct)
    {
        var motivo = req?.Motivo?.Trim();
        if (string.IsNullOrEmpty(motivo)) return BadRequest(new { messaggio = "Indica il motivo." });
        var r = await _ctx.RichiesteApprovazione.FirstOrDefaultAsync(x => x.IdRichiestaApprovazione == id, ct);
        if (r is null) return NotFound(new { messaggio = "Richiesta non trovata." });
        if (r.Stato is StatoRichiestaApprovazione.Approvata or StatoRichiestaApprovazione.Annullata)
            return BadRequest(new { messaggio = "Una richiesta approvata o già annullata non si annulla." });
        r.Stato = StatoRichiestaApprovazione.Annullata;
        r.ImprontaCodiceLink = null;
        r.ScadenzaLink = null;
        r.DataUltimaModifica = DateTime.UtcNow;
        await RegistraAsync("richiesta.annullata", r.IdRichiestaApprovazione, r.IdClienteArcLab, new { Motivo = motivo }, ct);
        return Json(new { messaggio = "Richiesta annullata." });
    }

    // ═══════════════════════════════════════════════════════════════
    // Invio massivo (10/10/2026): elenco Excel dei clienti con campioni negli ultimi 12 mesi,
    // anteprima del file compilato, preparazione (Firmatari + richieste) e invio in coda.
    // ═══════════════════════════════════════════════════════════════

    private const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly string[] Qualifiche = { "Legale rappresentante", "Amministratore", "Procuratore", "Responsabile qualità delegato" };
    private static readonly string[] Intestazioni =
        { "ID cliente", "Ragione sociale", "P. IVA", "Campioni 12 mesi", "Ultimo campione", "E-mail in anagrafica", "Stato approvazione", "Nome", "Cognome", "Qualifica", "E-mail Firmatario", "Note" };

    /// <summary>Stato dell'approvazione della revisione corrente per ogni cliente (ultima richiesta non annullata).</summary>
    private async Task<Dictionary<int, StatoRichiestaApprovazione>> StatiPerClienteAsync(int? idRevisione, CancellationToken ct)
    {
        if (idRevisione is null) return new();
        var righe = await _ctx.RichiesteApprovazione.AsNoTracking()
            .Where(x => x.IdRevisioneCondizioni == idRevisione && x.Stato != StatoRichiestaApprovazione.Annullata && x.Stato != StatoRichiestaApprovazione.Rifiutata)
            .Select(x => new { x.IdClienteArcLab, x.Stato, x.IdRichiestaApprovazione }).ToListAsync(ct);
        return righe.GroupBy(x => x.IdClienteArcLab).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IdRichiestaApprovazione).First().Stato);
    }

    [HttpGet("api/condizioni/massivo/elenco")]
    public async Task<IActionResult> ElencoMassivo([FromQuery] int? mesi, CancellationToken ct)
    {
        var dal = DateTime.Today.AddMonths(-Math.Clamp(mesi ?? 12, 1, 60));
        var clienti = await _anagrafiche.ClientiConCampioniAsync(dal, ct);
        var revisione = await _approvazioni.RevisioneCorrenteAsync(ct);
        var stati = await StatiPerClienteAsync(revisione?.IdRevisioneCondizioni, ct);
        var firmatari = (await _ctx.FirmatariClienti.AsNoTracking().Where(x => x.Attivo).ToListAsync(ct))
            .GroupBy(x => x.IdClienteArcLab).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataDesignazione).First());

        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Firmatari");
        for (var c = 0; c < Intestazioni.Length; c++) ws.Cell(1, c + 1).Value = Intestazioni[c];
        var riga = 2;
        foreach (var cl in clienti)
        {
            firmatari.TryGetValue(cl.IdCliente, out var f);
            ws.Cell(riga, 1).Value = cl.IdCliente;
            ws.Cell(riga, 2).Value = cl.RagioneSociale ?? "";
            ws.Cell(riga, 3).Value = cl.PartitaIva ?? "";
            ws.Cell(riga, 4).Value = cl.NumeroCampioni;
            if (cl.UltimoCampione is DateTime u) { ws.Cell(riga, 5).Value = u; ws.Cell(riga, 5).Style.DateFormat.Format = "dd/mm/yyyy"; }
            ws.Cell(riga, 6).Value = cl.Email ?? "";
            ws.Cell(riga, 7).Value = stati.TryGetValue(cl.IdCliente, out var st) ? st switch
            {
                StatoRichiestaApprovazione.Approvata => "approvata",
                StatoRichiestaApprovazione.DaInviare => "da inviare",
                StatoRichiestaApprovazione.Inviata or StatoRichiestaApprovazione.Aperta => "in attesa",
                _ => st.ToString()
            } : "";
            ws.Cell(riga, 8).Value = f?.Nome ?? "";
            ws.Cell(riga, 9).Value = f?.Cognome ?? "";
            ws.Cell(riga, 10).Value = f?.Qualifica ?? "";
            ws.Cell(riga, 11).Value = f?.Email ?? "";
            riga++;
        }
        var intestazione = ws.Range(1, 1, 1, Intestazioni.Length);
        intestazione.Style.Font.Bold = true;
        intestazione.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#EEEEEE");
        if (riga > 2)
        {
            ws.Range(2, 1, riga - 1, 7).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#F7F7F7");
            ws.Range(2, 8, riga - 1, 12).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#FFF8E1");
            ws.Range(2, 10, riga - 1, 10).CreateDataValidation().List("\"" + string.Join(",", Qualifiche) + "\"", true);
        }
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(8.0, 60.0);
        var istr = wb.Worksheets.Add("Istruzioni");
        var testi = new[]
        {
            "Elenco dei clienti con campioni ricevuti dal " + dal.ToString("dd/MM/yyyy") + " (ArcLab, sola lettura).",
            "Compilare le colonne gialle: Nome, Cognome, Qualifica ed E-mail del Firmatario (legale rappresentante o delegato).",
            "L'e-mail deve essere nominativa (non info@, qualita@ ecc.): lì arriva il link personale per approvare le Condizioni.",
            "Le righe senza e-mail vengono saltate. Non modificare la colonna «ID cliente».",
            "Poi ricaricare il file in LaboratorioAI → Condizioni e firme → Invio massivo: prima si vede un'anteprima, nulla parte senza conferma.",
            "Revisione delle Condizioni in vigore per le richieste: " + (revisione?.Codice ?? "NESSUNA REVISIONE PUBBLICATA")
        };
        for (var i = 0; i < testi.Length; i++) istr.Cell(i + 1, 1).Value = testi[i];
        istr.Column(1).Width = 130;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), MimeXlsx, $"Firmatari_clienti_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    /// <summary>Riga dell'anteprima dell'invio massivo.</summary>
    public sealed record RigaMassivo(int Riga, int IdCliente, string? RagioneSociale, string? PartitaIva, string? Nome, string? Cognome,
        string? Qualifica, string? Email, string Esito, string Messaggio, bool Designa, bool Crea, bool Inclusa, bool EmailGenerica);

    [HttpPost("api/condizioni/massivo/anteprima")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> AnteprimaMassivo(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { messaggio = "Scegli il file Excel compilato." });
        var revisione = await _approvazioni.RevisioneCorrenteAsync(ct);
        if (revisione is null) return BadRequest(new { messaggio = "Non c'è una revisione delle Condizioni pubblicata." });

        List<(int Riga, string Id, string Nome, string Cognome, string Qualifica, string Email)> lette = new();
        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            ms.Position = 0;
            using var wb = new ClosedXML.Excel.XLWorkbook(ms);
            var ws = wb.Worksheets.First();
            var colonne = ws.Row(1).CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
            int Col(string nome) => colonne.TryGetValue(nome, out var n) ? n : throw new InvalidOperationException($"Nel file manca la colonna «{nome}».");
            int cId = Col("ID cliente"), cNome = Col("Nome"), cCognome = Col("Cognome"), cQualifica = Col("Qualifica"), cEmail = Col("E-mail Firmatario");
            foreach (var r in ws.RowsUsed().Where(r => r.RowNumber() > 1))
                lette.Add((r.RowNumber(), r.Cell(cId).GetString().Trim(), r.Cell(cNome).GetString().Trim(), r.Cell(cCognome).GetString().Trim(),
                    r.Cell(cQualifica).GetString().Trim(), r.Cell(cEmail).GetString().Trim()));
        }
        catch (InvalidOperationException ex) { return BadRequest(new { messaggio = ex.Message }); }
        catch (Exception ex) when (ex is not OperationCanceledException) { return BadRequest(new { messaggio = "File Excel non leggibile: " + ex.Message }); }

        var anagrafica = (await _anagrafiche.ClientiAsync(ct)).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Nome);
        var conCampioni = (await _anagrafiche.ClientiConCampioniAsync(DateTime.Today.AddMonths(-13), ct)).ToDictionary(c => c.IdCliente);
        var stati = await StatiPerClienteAsync(revisione.IdRevisioneCondizioni, ct);
        var firmatari = (await _ctx.FirmatariClienti.AsNoTracking().Where(x => x.Attivo).ToListAsync(ct))
            .GroupBy(x => x.IdClienteArcLab).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataDesignazione).First());

        var visti = new HashSet<int>();
        var righe = new List<RigaMassivo>();
        foreach (var l in lette)
        {
            RigaMassivo Esito(int id, string esito, string msg, bool designa = false, bool crea = false, bool inclusa = false, bool generica = false) =>
                new(l.Riga, id, anagrafica.GetValueOrDefault(id), conCampioni.TryGetValue(id, out var cc) ? cc.PartitaIva : null,
                    l.Nome, l.Cognome, l.Qualifica, l.Email, esito, msg, designa, crea, inclusa, generica);
            if (!int.TryParse(l.Id, out var idCliente)) { righe.Add(Esito(0, "errore", "ID cliente mancante o non numerico")); continue; }
            if (!anagrafica.ContainsKey(idCliente)) { righe.Add(Esito(idCliente, "errore", "cliente non trovato in ArcLab")); continue; }
            if (!visti.Add(idCliente)) { righe.Add(Esito(idCliente, "errore", "cliente ripetuto: vale solo la prima riga")); continue; }
            stati.TryGetValue(idCliente, out var stato);
            if (stato == StatoRichiestaApprovazione.Approvata) { righe.Add(Esito(idCliente, "salta", $"ha già approvato {revisione.Codice}")); continue; }
            if (string.IsNullOrWhiteSpace(l.Email)) { righe.Add(Esito(idCliente, "salta", "senza e-mail del Firmatario")); continue; }
            if (!ServizioApprovazioni.FormatoEmail.IsMatch(l.Email)) { righe.Add(Esito(idCliente, "errore", "e-mail non valida")); continue; }
            if (string.IsNullOrWhiteSpace(l.Nome) || string.IsNullOrWhiteSpace(l.Cognome)) { righe.Add(Esito(idCliente, "errore", "manca nome o cognome")); continue; }
            var generica = ServizioApprovazioni.EmailGenerica(l.Email);
            firmatari.TryGetValue(idCliente, out var f);
            var invariato = f is not null && string.Equals(f.Email, l.Email, StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.Nome, l.Nome, StringComparison.OrdinalIgnoreCase) && string.Equals(f.Cognome, l.Cognome, StringComparison.OrdinalIgnoreCase)
                && string.Equals(f.Qualifica ?? "", l.Qualifica, StringComparison.OrdinalIgnoreCase);
            var haRichiesta = stato is StatoRichiestaApprovazione.DaInviare or StatoRichiestaApprovazione.Inviata or StatoRichiestaApprovazione.Aperta or StatoRichiestaApprovazione.InVerifica;
            var esito = f is null ? "nuovo" : invariato ? "invariato" : "cambia";
            var messaggio = (f is null ? "nuovo Firmatario" : invariato ? "Firmatario invariato" : $"cambia Firmatario (era {f.Nome} {f.Cognome}, {f.Email})")
                + (haRichiesta ? " · richiesta già presente" : " · nuova richiesta")
                + (generica ? " · ATTENZIONE: e-mail generica" : "");
            if (invariato && haRichiesta) { righe.Add(Esito(idCliente, "salta", "Firmatario invariato e richiesta già presente")); continue; }
            righe.Add(Esito(idCliente, esito, messaggio, designa: !invariato, crea: !haRichiesta, inclusa: !generica, generica: generica));
        }

        return Json(new
        {
            revisione = revisione.Codice,
            totale = righe.Count,
            daPreparare = righe.Count(r => r.Designa || r.Crea),
            saltate = righe.Count(r => r.Esito == "salta"),
            errori = righe.Count(r => r.Esito == "errore"),
            generiche = righe.Count(r => r.EmailGenerica && (r.Designa || r.Crea)),
            righe
        });
    }

    public sealed record RichiestaPreparaMassivo(List<RigaPrepara>? Righe);
    public sealed record RigaPrepara(int IdCliente, string? Nome, string? Cognome, string? Qualifica, string? Email);

    /// <summary>
    /// Applica le righe confermate dell'anteprima: designa i Firmatari nuovi o cambiati e crea le richieste mancanti sulla
    /// revisione corrente. Non invia nulla: l'invio parte con «Metti in coda l'invio».
    /// </summary>
    [HttpPost("api/condizioni/massivo/prepara")]
    public async Task<IActionResult> PreparaMassivo([FromBody] RichiestaPreparaMassivo req, CancellationToken ct)
    {
        var righe = req?.Righe ?? new();
        if (righe.Count == 0) return BadRequest(new { messaggio = "Nessuna riga da preparare." });
        var revisione = await _approvazioni.RevisioneCorrenteAsync(ct);
        if (revisione is null) return BadRequest(new { messaggio = "Non c'è una revisione delle Condizioni pubblicata." });
        var anagrafica = (await _anagrafiche.ClientiAsync(ct)).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First().Nome);
        var conCampioni = (await _anagrafiche.ClientiConCampioniAsync(DateTime.Today.AddMonths(-13), ct)).ToDictionary(c => c.IdCliente);

        int designati = 0, create = 0; var errori = new List<string>();
        foreach (var r in righe.GroupBy(x => x.IdCliente).Select(g => g.First()))
        {
            try
            {
                var email = (r.Email ?? "").Trim();
                if (!anagrafica.TryGetValue(r.IdCliente, out var rs)) { errori.Add($"{r.IdCliente}: cliente non trovato"); continue; }
                if (!ServizioApprovazioni.FormatoEmail.IsMatch(email) || string.IsNullOrWhiteSpace(r.Nome) || string.IsNullOrWhiteSpace(r.Cognome))
                { errori.Add($"{rs}: dati del Firmatario incompleti"); continue; }

                var f = await _ctx.FirmatariClienti.AsNoTracking().Where(x => x.IdClienteArcLab == r.IdCliente && x.Attivo)
                    .OrderByDescending(x => x.DataDesignazione).FirstOrDefaultAsync(ct);
                var invariato = f is not null && string.Equals(f.Email, email, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(f.Nome, r.Nome!.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(f.Cognome, r.Cognome!.Trim(), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(f.Qualifica ?? "", (r.Qualifica ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
                if (!invariato)
                {
                    var piva = conCampioni.TryGetValue(r.IdCliente, out var cc) ? cc.PartitaIva : (await _anagrafiche.DettaglioClienteAsync(r.IdCliente, ct))?.PartitaIva;
                    await _approvazioni.DesignaFirmatarioAsync(r.IdCliente, rs, piva, r.Nome!, r.Cognome!, r.Qualifica, email, null, "invio massivo", Operatore, ct);
                    designati++;
                }
                var e = await _approvazioni.CreaRichiestaAsync(r.IdCliente, revisione.IdRevisioneCondizioni, null, "invio massivo", Operatore, ct);
                if (e.Riuscito) create++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errori.Add($"{r.IdCliente}: {ex.Message}");
                _ctx.ChangeTracker.Clear();
            }
        }
        await _audit.RegistraAsync(AreaFunzionale.Contratti, "condizioni.massivo.preparato", EsitoOperazione.Successo,
            dettaglio: new { Righe = righe.Count, Designati = designati, Create = create, Errori = errori.Count }, ct: ct);
        return Json(new
        {
            designati, create, errori,
            messaggio = $"Preparato: {designati} Firmatari designati, {create} richieste create" + (errori.Count > 0 ? $", {errori.Count} errori." : ".") +
                " Ora puoi mettere in coda l'invio."
        });
    }

    /// <summary>Mette in coda per l'invio automatico tutte le richieste «da inviare» della revisione corrente.</summary>
    [HttpPost("api/condizioni/massivo/accoda")]
    [RichiedePermesso(Permessi.CondizioniGestione)] // 10/10/2026: preparano i commerciali, il via lo dà il Responsabile
    public async Task<IActionResult> AccodaMassivo(CancellationToken ct)
    {
        var revisione = await _approvazioni.RevisioneCorrenteAsync(ct);
        if (revisione is null) return BadRequest(new { messaggio = "Non c'è una revisione delle Condizioni pubblicata." });
        var ora = DateTime.UtcNow;
        var daInviare = await _ctx.RichiesteApprovazione.Where(x => x.IdRevisioneCondizioni == revisione.IdRevisioneCondizioni
            && x.Stato == StatoRichiestaApprovazione.DaInviare && x.InvioAccodato == null && x.IdFirmatarioCliente != null).ToListAsync(ct);
        foreach (var r in daInviare) { r.InvioAccodato = ora; r.ErroreInvioLink = null; }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Contratti, "condizioni.massivo.accodato", EsitoOperazione.Successo,
            dettaglio: new { Revisione = revisione.Codice, Richieste = daInviare.Count }, ct: ct);
        return Json(new { messaggio = $"{daInviare.Count} richieste messe in coda: partono una alla volta, al massimo {LimiteGiornaliero} e-mail al giorno." });
    }

    [HttpPost("api/condizioni/massivo/ferma")]
    [RichiedePermesso(Permessi.CondizioniGestione)] // 10/10/2026: preparano i commerciali, il via lo dà il Responsabile
    public async Task<IActionResult> FermaMassivo(CancellationToken ct)
    {
        var inCoda = await _ctx.RichiesteApprovazione.Where(x => x.InvioAccodato != null).ToListAsync(ct);
        foreach (var r in inCoda) r.InvioAccodato = null;
        var invitiInCoda = await _ctx.InvitiFirmatario.Where(x => x.InvioAccodato != null).ToListAsync(ct);
        foreach (var i in invitiInCoda) i.InvioAccodato = null;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Contratti, "condizioni.massivo.fermato", EsitoOperazione.Successo,
            dettaglio: new { Richieste = inCoda.Count, Inviti = invitiInCoda.Count }, ct: ct);
        return Json(new { messaggio = $"Invio fermato: {inCoda.Count} richieste e {invitiInCoda.Count} inviti tolti dalla coda (restano «da inviare»)." });
    }

    private int LimiteGiornaliero => _config.GetValue("Condizioni:InviiMassimiGiorno", 1000);

    // ═══════════════════════════════════════════════════════════════
    // Inviti a indicare il Firmatario (11/10/2026): e-mail all'indirizzo aziendale del cliente (ArcLab, sola lettura)
    // ═══════════════════════════════════════════════════════════════

    private static object FormatoInvito(InvitoFirmatario i) => new
    {
        i.IdInvitoFirmatario, i.ClienteRagioneSociale, i.EmailDestinatari, Stato = i.Stato.ToString(), InCoda = i.InvioAccodato != null,
        DataInvio = i.DataInvio?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), DataApertura = i.DataApertura?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
        DataIndicazione = i.DataIndicazione?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), i.NumeroPromemoria,
        Indicato = i.NomeIndicato is null ? null : $"{i.NomeIndicato} {i.CognomeIndicato}, {i.QualificaIndicata} <{i.EmailIndicata}>",
        Errore = i.ErroreInvio ?? i.ErroreElaborazione, i.Note
    };

    /// <summary>Situazione dei clienti con campioni negli ultimi 12 mesi rispetto alla revisione in vigore (conteggi).</summary>
    [HttpGet("api/condizioni/inviti/situazione")]
    public async Task<IActionResult> SituazioneInviti([FromServices] ServizioInvitiFirmatario inviti, CancellationToken ct)
    {
        var (revisione, righe) = await inviti.SituazioneAsync(ct);
        var ultimi = await _ctx.InvitiFirmatario.AsNoTracking()
            .Where(x => x.Stato == StatoInvitoFirmatario.Indicato || x.Stato == StatoInvitoFirmatario.Elaborato || x.ErroreInvio != null || x.ErroreElaborazione != null)
            .OrderByDescending(x => x.DataUltimaModifica).Take(30).ToListAsync(ct);
        return Json(new
        {
            revisione = revisione?.Codice,
            totale = righe.Count,
            conteggi = righe.GroupBy(r => r.Situazione).Select(g => new { situazione = g.Key, numero = g.Count() }).OrderByDescending(x => x.numero),
            ultimi = ultimi.Select(FormatoInvito)
        });
    }

    [HttpGet("api/condizioni/inviti/excel")]
    public async Task<IActionResult> ExcelInviti([FromServices] ServizioInvitiFirmatario inviti, CancellationToken ct)
    {
        var (revisione, righe) = await inviti.SituazioneAsync(ct);
        string[] intestazioni = { "ID cliente", "Ragione sociale", "P. IVA", "E-mail aziendale (ArcLab)", "Campioni 12 mesi", "Ultimo campione", "Situazione", "Dettaglio" };
        using var wb = new ClosedXML.Excel.XLWorkbook();
        var ws = wb.Worksheets.Add("Situazione");
        for (var c = 0; c < intestazioni.Length; c++) ws.Cell(1, c + 1).Value = intestazioni[c];
        var riga = 2;
        foreach (var r in righe.OrderBy(x => x.Situazione).ThenBy(x => x.RagioneSociale))
        {
            ws.Cell(riga, 1).Value = r.IdCliente;
            ws.Cell(riga, 2).Value = r.RagioneSociale ?? "";
            ws.Cell(riga, 3).Value = r.PartitaIva ?? "";
            ws.Cell(riga, 4).Value = r.EmailAziendale ?? "";
            ws.Cell(riga, 5).Value = r.NumeroCampioni;
            if (r.UltimoCampione is DateTime u) { ws.Cell(riga, 6).Value = u; ws.Cell(riga, 6).Style.DateFormat.Format = "dd/mm/yyyy"; }
            ws.Cell(riga, 7).Value = r.Situazione;
            ws.Cell(riga, 8).Value = r.Dettaglio ?? "";
            riga++;
        }
        var testa = ws.Range(1, 1, 1, intestazioni.Length);
        testa.Style.Font.Bold = true;
        testa.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#EEEEEE");
        if (riga > 2) ws.Range(1, 1, riga - 1, intestazioni.Length).SetAutoFilter();
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(riga, 300));
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Situazione_Firmatari_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>Crea gli inviti «da inviare» per i clienti senza Firmatario e con e-mail aziendale. Non invia nulla.</summary>
    [HttpPost("api/condizioni/inviti/prepara")]
    public async Task<IActionResult> PreparaInviti([FromServices] ServizioInvitiFirmatario inviti, CancellationToken ct)
    {
        var (creati, senzaEmail, errore) = await inviti.PreparaAsync(Operatore, ct);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        await _audit.RegistraAsync(AreaFunzionale.Contratti, "condizioni.inviti.preparati", EsitoOperazione.Successo,
            dettaglio: new { Creati = creati, SenzaEmail = senzaEmail }, ct: ct);
        return Json(new { messaggio = $"Preparati {creati} inviti. {senzaEmail} clienti non hanno un'e-mail aziendale valida in ArcLab (vedi l'Excel)." +
            (creati > 0 ? " Ora si possono mettere in coda." : "") });
    }

    [HttpPost("api/condizioni/inviti/accoda")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> AccodaInviti(CancellationToken ct)
    {
        var ora = DateTime.UtcNow;
        var daInviare = await _ctx.InvitiFirmatario.Where(x => x.Stato == StatoInvitoFirmatario.DaInviare && x.InvioAccodato == null).ToListAsync(ct);
        foreach (var i in daInviare) { i.InvioAccodato = ora; i.ErroreInvio = null; }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Contratti, "condizioni.inviti.accodati", EsitoOperazione.Successo,
            dettaglio: new { Inviti = daInviare.Count }, ct: ct);
        return Json(new { messaggio = $"{daInviare.Count} inviti messi in coda: partono nei giorni feriali dalle 8 alle 18, al massimo {LimiteGiornaliero} e-mail al giorno in tutto." });
    }

    public sealed record RichiestaInvitoCliente(int IdCliente, string? Email);

    /// <summary>
    /// Invia subito l'invito a un singolo cliente (dalla scheda cliente): all'e-mail aziendale di ArcLab o a quella indicata.
    /// </summary>
    [HttpPost("api/condizioni/inviti/cliente")]
    public async Task<IActionResult> InvitaCliente([FromBody] RichiestaInvitoCliente req, [FromServices] ServizioInvitiFirmatario inviti, CancellationToken ct)
    {
        var revisione = await _approvazioni.RevisioneCorrenteAsync(ct);
        if (revisione is null) return BadRequest(new { messaggio = "Non c'è una revisione delle Condizioni pubblicata." });
        var dettaglio = await _anagrafiche.DettaglioClienteAsync(req.IdCliente, ct);
        if (dettaglio is null) return BadRequest(new { messaggio = "Cliente non trovato in ArcLab." });
        if (await _ctx.FirmatariClienti.AnyAsync(x => x.IdClienteArcLab == req.IdCliente && x.Attivo, ct))
            return BadRequest(new { messaggio = "Il cliente ha già un Firmatario: crea la richiesta di approvazione." });
        var destinatari = IndirizziEmail.Elenco(string.IsNullOrWhiteSpace(req.Email) ? dettaglio.Email : req.Email);
        if (destinatari.Count == 0) return BadRequest(new { messaggio = "Indica almeno un indirizzo e-mail valido." });

        var ora = DateTime.UtcNow;
        var nome = (await _anagrafiche.ClientiAsync(ct)).FirstOrDefault(c => c.Id == req.IdCliente)?.Nome;
        var aperti = await _ctx.InvitiFirmatario.Where(x => x.IdClienteArcLab == req.IdCliente
            && (x.Stato == StatoInvitoFirmatario.DaInviare || x.Stato == StatoInvitoFirmatario.Inviato || x.Stato == StatoInvitoFirmatario.Aperto)).ToListAsync(ct);
        foreach (var a in aperti)
        {
            a.Stato = StatoInvitoFirmatario.Annullato; a.InvioAccodato = null; a.ImprontaCodiceLink = null; a.ScadenzaLink = null;
            a.Note = "Sostituito da un nuovo invito"; a.DataUltimaModifica = ora;
        }
        var invito = new InvitoFirmatario
        {
            IdClienteArcLab = req.IdCliente, ClienteRagioneSociale = nome, ClientePartitaIva = dettaglio.PartitaIva,
            EmailDestinatari = string.Join("; ", destinatari), IdRevisioneCondizioni = revisione.IdRevisioneCondizioni,
            Stato = StatoInvitoFirmatario.DaInviare, IdUtenteCreazione = _utente.IdUtente, NomeUtenteCreazione = _utente.NomeCompleto,
            DataCreazione = ora, DataUltimaModifica = ora
        };
        _ctx.InvitiFirmatario.Add(invito);
        await _ctx.SaveChangesAsync(ct);
        var esito = await inviti.InviaAsync(invito.IdInvitoFirmatario, false, Operatore, ct);
        if (!esito.Riuscito)
        {
            invito.ErroreInvio = esito.Messaggio.Length > 1000 ? esito.Messaggio[..1000] : esito.Messaggio;
            await _ctx.SaveChangesAsync(ct);
            return BadRequest(new { messaggio = esito.Messaggio });
        }
        await _ctx.SaveChangesAsync(ct);
        return Json(new { messaggio = esito.Messaggio });
    }

    [HttpGet("api/condizioni/massivo/stato")]
    public async Task<IActionResult> StatoMassivo(CancellationToken ct)
    {
        var inCoda = await _ctx.RichiesteApprovazione.CountAsync(x => x.InvioAccodato != null, ct)
                   + await _ctx.InvitiFirmatario.CountAsync(x => x.InvioAccodato != null, ct);
        var inviatiOggi = await ServizioInvioLinkMassivo.InviatiOggiAsync(_ctx, ct);
        var errori = await _ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.InvioAccodato != null && x.ErroreInvioLink != null)
            .Select(x => new { x.ClienteRagioneSociale, x.ErroreInvioLink }).Take(50).ToListAsync(ct);
        return Json(new { inCoda, inviatiOggi, limite = LimiteGiornaliero, errori });
    }

    // ═══════════════════════════════════════════════════════════════
    // Registro delle evidenze
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/condizioni/registro")]
    public async Task<IActionResult> Registro([FromQuery] int? idCliente, CancellationToken ct)
    {
        var q = _ctx.EventiApprovazione.AsNoTracking();
        if (idCliente is int c) q = q.Where(x => x.IdClienteArcLab == c);
        var righe = await q.OrderByDescending(x => x.IdEventoApprovazione).Take(500).ToListAsync(ct);
        return Json(righe.Select(x => new
        {
            x.IdEventoApprovazione, x.Tipo, x.IdRichiestaApprovazione, x.IdClienteArcLab,
            Data = DateTime.SpecifyKind(x.DataUtc, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"),
            x.NomeUtente, x.IndirizzoIp, x.Dati, x.Impronta
        }));
    }

    [HttpGet("api/condizioni/registro/verifica")]
    [RichiedePermesso(Permessi.CondizioniGestione)]
    public async Task<IActionResult> VerificaRegistro(CancellationToken ct)
    {
        var esito = await _registro.VerificaAsync(ct);
        return Json(new
        {
            integra = esito.Integra, righe = esito.RigheControllate, esito.PrimaRigaErrata, esito.Motivo,
            messaggio = esito.Integra
                ? $"Registro integro: {esito.RigheControllate} righe verificate, catena delle impronte corretta."
                : $"ATTENZIONE: catena interrotta alla riga {esito.PrimaRigaErrata} — {esito.Motivo}."
        });
    }
}

public record RichiestaClausola
{
    public int? Id { get; init; }
    public string? Articolo { get; init; }
    public string? ContenutoBreve { get; init; }
    public string? TestoIntegrale { get; init; }
}

public record RichiestaSalvaRevisione
{
    public int? Id { get; init; }
    public string? Codice { get; init; }
    public int NumeroRevisione { get; init; }
    public DateTime? DataRevisione { get; init; }
    public DateTime? DataEntrataVigore { get; init; }
    public bool RichiedeNuovaApprovazione { get; init; } = true;
    public string? TestoCosaCambia { get; init; }
    public string? Note { get; init; }
    public List<RichiestaClausola>? Clausole { get; init; }
}

public record RichiestaFirmatario
{
    public int IdCliente { get; init; }
    public string? Nome { get; init; }
    public string? Cognome { get; init; }
    public string? Qualifica { get; init; }
    public string? Email { get; init; }
    public string? CodiceFiscale { get; init; }
    public string? Note { get; init; }
    public bool ConfermaEmailGenerica { get; init; }
}

public record RichiestaCreaApprovazione
{
    public int IdCliente { get; init; }
    public int? IdRevisione { get; init; }
    public int? IdPreventivo { get; init; }
    public string? Note { get; init; }
}

public record RichiestaMotivo
{
    public string? Motivo { get; init; }
}
