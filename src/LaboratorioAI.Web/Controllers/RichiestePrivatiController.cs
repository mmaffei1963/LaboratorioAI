using System.Text.Json;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Limiti;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Richieste di analisi dei clienti privati (05/11/2026): maschera che raccoglie i dati del modulo Mod. Com. 05B e lo
/// stampa compilato o in bianco. Da ArcLab si legge soltanto (matrici, contenitori, controlli e prezzi delle commesse
/// del cliente «Spettabile cliente», cod. contabilità MAF2, metodi). MAI scrittura su ArcLab.
/// </summary>
[RichiedePermesso(Permessi.RichiestePrivatiGestione)]
[TabelleRichiestePrivati]
public class RichiestePrivatiController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IRepositoryCommesse _commesse;
    private readonly IRepositoryControlli _controlli;
    private readonly ServizioSegnalazioniLimiti _limiti;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IWebHostEnvironment _ambiente;

    public RichiestePrivatiController(
        ContestoLaboratorioAI ctx, IRepositoryAnagrafiche anagrafiche, IRepositoryCommesse commesse,
        IRepositoryControlli controlli, ServizioSegnalazioniLimiti limiti, IContestoUtente utente,
        IServizioAudit audit, IWebHostEnvironment ambiente)
    {
        _ctx = ctx; _anagrafiche = anagrafiche; _commesse = commesse; _controlli = controlli;
        _limiti = limiti; _utente = utente; _audit = audit; _ambiente = ambiente;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Richieste analisi privati";
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // Cataloghi (ArcLab, sola lettura)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/richieste-privati/catalogo")]
    public async Task<IActionResult> Catalogo(CancellationToken ct)
    {
        var controlli = await _commesse.ControlliCommesseClienteAsync(RegoleRichiestePrivati.CodiceClientePrivati, ct);
        return Ok(new
        {
            matrici = await _anagrafiche.MatriciAsync(ct),
            contenitori = await _anagrafiche.ImballaggiAsync(ct),
            controlli = controlli.Select(c => new
            {
                c.IdCommessa, c.IdControllo, c.Controllo, c.CodCommessa, Prezzo = c.Prezzo,
                PrezzoIvaInclusa = RegoleRichiestePrivati.IvaInclusa(c.Prezzo, AliquotaIva)
            }),
            supplementoGiudizio = RegoleRichiestePrivati.SupplementoGiudizioPredefinito,
            aliquotaIva = AliquotaIva,
            codiceCliente = RegoleRichiestePrivati.CodiceClientePrivati
        });
    }

    private const decimal AliquotaIva = 22m;

    /// <summary>Metodi del controllo e tempi proposti (10 o 20 giorni lavorativi).</summary>
    [HttpGet("api/richieste-privati/controlli/{idControllo:int}")]
    public async Task<IActionResult> Controllo(int idControllo, CancellationToken ct)
    {
        var nome = await _controlli.NomeControlloAsync(idControllo, ct);
        var metodi = await MetodiAsync(idControllo, ct);
        return Ok(new { idControllo, nome, tempi = RegoleRichiestePrivati.TempiControllo(nome, metodi), metodi });
    }

    /// <summary>Norme con limiti validati per la matrice (tendina del giudizio di conformità).</summary>
    [HttpGet("api/richieste-privati/norme")]
    public async Task<IActionResult> Norme([FromQuery] int? idMatrice, [FromQuery] string? matrice, CancellationToken ct) =>
        Ok(await _limiti.NormePerMatriceAsync(idMatrice, matrice, ct));

    // ═══════════════════════════════════════════════════════════════
    // Registro e dettaglio
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/richieste-privati/elenco")]
    public async Task<IActionResult> Elenco([FromQuery] string? testo, CancellationToken ct)
    {
        var q = _ctx.RichiesteAnalisiPrivati.AsNoTracking().Where(r => r.Attivo);
        if (!string.IsNullOrWhiteSpace(testo))
        {
            var t = testo.Trim();
            q = int.TryParse(t, out var n)
                ? q.Where(r => r.Numero == n || r.NomeCognome.Contains(t))
                : q.Where(r => r.NomeCognome.Contains(t) || (r.CodiceFiscale != null && r.CodiceFiscale.Contains(t))
                               || (r.DescrizioneCampione != null && r.DescrizioneCampione.Contains(t)));
        }
        var righe = await q.OrderByDescending(r => r.Anno).ThenByDescending(r => r.Numero).Take(300)
            .Select(r => new { r.IdRichiestaAnalisiPrivato, r.Anno, r.Numero, r.NomeCognome, r.DescrizioneCampione,
                               r.TotaleIvaInclusa, r.GiudizioRichiesto, r.DataCreazione })
            .ToListAsync(ct);
        return Ok(righe.Select(r => new
        {
            r.IdRichiestaAnalisiPrivato, NumeroTesto = RegoleRichiestePrivati.NumeroFormattato(r.Anno, r.Numero),
            r.NomeCognome, r.DescrizioneCampione, r.TotaleIvaInclusa, r.GiudizioRichiesto, r.DataCreazione
        }));
    }

    [HttpGet("api/richieste-privati/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var r = await _ctx.RichiesteAnalisiPrivati.AsNoTracking().Include(x => x.Righe)
            .FirstOrDefaultAsync(x => x.IdRichiestaAnalisiPrivato == id && x.Attivo, ct);
        if (r is null) return NotFound();
        return Ok(new
        {
            r.IdRichiestaAnalisiPrivato, NumeroTesto = RegoleRichiestePrivati.NumeroFormattato(r.Anno, r.Numero),
            r.NomeCognome, r.CodiceFiscale, r.Indirizzo, r.Telefono, r.Email, r.RitiroInLaboratorio, Pagamento = (int)r.Pagamento,
            r.IdMatriceArcLab, r.DescrizioneCampione, r.PuntoPrelievo, r.DataOraPrelievo, r.IdImballaggioArcLab, r.Contenitore,
            r.ContenitoreDelLaboratorio, r.TemperaturaRicevimento, r.TempiConsegnaGiorni, r.GiudizioRichiesto, r.NormaGiudizio,
            r.ComunicazioneFuoriLimite, r.ConsensoComunicazioni, Iva = r.TotaleIvaInclusa - r.TotaleImponibile,
            r.SupplementoGiudizio, r.TotaleImponibile, r.TotaleIvaInclusa, r.Note,
            Righe = r.Righe.OrderBy(x => x.Progressivo).Select(x => new
            {
                x.IdControlloArcLab, x.IdCommessaArcLab, x.Descrizione, x.PrezzoImponibile, x.PrezzoIvaInclusa, x.TempiGiorni,
                Metodi = ModuloRichiestaPrivatiDocx.Metodi(x)
            })
        });
    }

    // ═══════════════════════════════════════════════════════════════
    // Salvataggio
    // ═══════════════════════════════════════════════════════════════

    [HttpPost("api/richieste-privati/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaPrivato req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.NomeCognome))
            return BadRequest(new { messaggio = "Indicare nome e cognome del cliente." });
        var righeReq = (req.Righe ?? new()).Where(x => x.IdControllo > 0).ToList();
        if (righeReq.Count == 0)
            return BadRequest(new { messaggio = "Scegliere almeno un controllo." });
        if (req.GiudizioRichiesto && string.IsNullOrWhiteSpace(req.NormaGiudizio))
            return BadRequest(new { messaggio = "Con il giudizio di conformità va indicata la norma o i limiti da applicare (Art. 5.2)." });
        if (req.TempiConsegnaGiorni is int tg && (tg < 1 || tg > 120))
            return BadRequest(new { messaggio = "Tempi di consegna non validi." });

        var ora = DateTime.UtcNow;
        RichiestaAnalisiPrivato r;
        if (req.IdRichiestaAnalisiPrivato is int id and > 0)
        {
            r = await _ctx.RichiesteAnalisiPrivati.Include(x => x.Righe)
                    .FirstOrDefaultAsync(x => x.IdRichiestaAnalisiPrivato == id && x.Attivo, ct)
                ?? throw new InvalidOperationException("Richiesta non trovata.");
            r.DataUltimaModifica = ora;
        }
        else
        {
            var anno = DateTime.Today.Year;
            var max = await _ctx.RichiesteAnalisiPrivati.Where(x => x.Anno == anno).MaxAsync(x => (int?)x.Numero, ct) ?? 0;
            r = new RichiestaAnalisiPrivato
            {
                Anno = anno, Numero = max + 1, DataCreazione = ora, DataUltimaModifica = ora,
                IdUtenteCreatore = _utente.IdUtente, AliquotaIva = AliquotaIva
            };
            _ctx.RichiesteAnalisiPrivati.Add(r);
        }

        // 1. Cliente
        r.NomeCognome = req.NomeCognome!.Trim();
        r.CodiceFiscale = Pulisci(req.CodiceFiscale)?.ToUpperInvariant();
        r.Indirizzo = Pulisci(req.Indirizzo);
        r.Telefono = Pulisci(req.Telefono);
        r.Email = Pulisci(req.Email);
        r.RitiroInLaboratorio = req.RitiroInLaboratorio;
        r.Pagamento = req.Pagamento == (int)PagamentoPrivato.AllaConsegnaRapporto
            ? PagamentoPrivato.AllaConsegnaRapporto : PagamentoPrivato.AllaConsegnaCampione;

        // 2. Campione
        r.IdMatriceArcLab = req.IdMatriceArcLab;
        r.DescrizioneCampione = Pulisci(req.DescrizioneCampione);
        r.PuntoPrelievo = Pulisci(req.PuntoPrelievo);
        r.DataOraPrelievo = req.DataOraPrelievo?.UtcDateTime;
        r.IdImballaggioArcLab = req.IdImballaggioArcLab;
        r.Contenitore = Pulisci(req.Contenitore);
        r.ContenitoreDelLaboratorio = req.ContenitoreDelLaboratorio;
        r.TemperaturaRicevimento = req.TemperaturaRicevimento;

        // 3. Prove: le righe già salvate restano come sono (prezzo e metodi del momento della firma);
        //    quelle nuove si compongono da ArcLab con il prezzo della commessa MAF2.
        var catalogo = (await _commesse.ControlliCommesseClienteAsync(RegoleRichiestePrivati.CodiceClientePrivati, ct))
            .ToDictionary(c => c.IdCommessa);
        var esistenti = r.Righe.ToList();
        var nuove = new List<RigaRichiestaAnalisiPrivato>();
        foreach (var rr in righeReq)
        {
            var gia = esistenti.FirstOrDefault(x => x.IdControlloArcLab == rr.IdControllo && x.IdCommessaArcLab == rr.IdCommessa
                                                    && !nuove.Contains(x));
            if (gia is not null) { nuove.Add(gia); continue; }
            if (rr.IdCommessa is not int ic || !catalogo.TryGetValue(ic, out var voce) || voce.IdControllo != rr.IdControllo)
                return BadRequest(new { messaggio = $"Il controllo {rr.IdControllo} non è tra le commesse in vigore di «Spettabile cliente» ({RegoleRichiestePrivati.CodiceClientePrivati})." });
            var metodi = await MetodiAsync(rr.IdControllo, ct);
            nuove.Add(new RigaRichiestaAnalisiPrivato
            {
                IdControlloArcLab = rr.IdControllo, IdCommessaArcLab = ic, Descrizione = voce.Controllo,
                PrezzoImponibile = voce.Prezzo, TempiGiorni = RegoleRichiestePrivati.TempiControllo(voce.Controllo, metodi),
                MetodiJson = JsonSerializer.Serialize(metodi)
            });
        }
        foreach (var vecchia in esistenti.Where(x => !nuove.Contains(x)))
        {
            r.Righe.Remove(vecchia);
            _ctx.RigheRichiestaAnalisiPrivati.Remove(vecchia);   // riga di dettaglio, come nei preventivi
        }
        var progressivo = 1;
        foreach (var n in nuove)
        {
            n.Progressivo = progressivo++;
            if (!r.Righe.Contains(n)) r.Righe.Add(n);
        }

        r.TempiConsegnaGiorni = req.TempiConsegnaGiorni ?? RegoleRichiestePrivati.TempiRichiesta(nuove.Select(x => x.TempiGiorni));
        r.GiudizioRichiesto = req.GiudizioRichiesto;
        r.ComunicazioneFuoriLimite = req.ComunicazioneFuoriLimite;
        r.ConsensoComunicazioni = req.ConsensoComunicazioni;
        r.NormaGiudizio = req.GiudizioRichiesto ? Pulisci(req.NormaGiudizio) : null;
        r.SupplementoGiudizio = req.SupplementoGiudizio is decimal s and >= 0 ? s : RegoleRichiestePrivati.SupplementoGiudizioPredefinito;
        r.Note = Pulisci(req.Note);
        RegoleRichiestePrivati.Ricalcola(r);

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Preventivi, "richiestaprivato.salvata", EsitoOperazione.Successo,
            tipoEntita: "RichiestaAnalisiPrivato", idEntita: r.IdRichiestaAnalisiPrivato.ToString(),
            dettaglio: new { r.Anno, r.Numero, Righe = r.Righe.Count, r.TotaleIvaInclusa }, ct: ct);
        return Ok(new { r.IdRichiestaAnalisiPrivato, NumeroTesto = RegoleRichiestePrivati.NumeroFormattato(r.Anno, r.Numero), r.TotaleIvaInclusa });
    }

    [HttpPost("api/richieste-privati/{id:int}/elimina")]
    public async Task<IActionResult> Elimina(int id, CancellationToken ct)
    {
        var r = await _ctx.RichiesteAnalisiPrivati.FirstOrDefaultAsync(x => x.IdRichiestaAnalisiPrivato == id && x.Attivo, ct);
        if (r is null) return NotFound();
        r.Attivo = false; r.DataCancellazione = DateTime.UtcNow; r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Preventivi, "richiestaprivato.eliminata", EsitoOperazione.Successo,
            tipoEntita: "RichiestaAnalisiPrivato", idEntita: id.ToString(), dettaglio: new { r.Anno, r.Numero }, ct: ct);
        return Ok();
    }

    // ═══════════════════════════════════════════════════════════════
    // Modulo Word (compilato o in bianco)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/richieste-privati/modulo")]
    public async Task<IActionResult> Modulo([FromQuery] int? id, CancellationToken ct)
    {
        // 06/11/2026: Ordine di Accettazione per i privati (Mod. Com. 05B), stesso aspetto degli Ordini di Accettazione.
        var modello = Path.Combine(_ambiente.WebRootPath, OrdinePrivatoDocx.PercorsoModello);
        if (!System.IO.File.Exists(modello))
            return NotFound(new { messaggio = "Carta intestata non trovata: " + OrdinePrivatoDocx.PercorsoModello });
        RichiestaAnalisiPrivato? r = null;
        if (id is int i)
        {
            r = await _ctx.RichiesteAnalisiPrivati.AsNoTracking().Include(x => x.Righe)
                .FirstOrDefaultAsync(x => x.IdRichiestaAnalisiPrivato == i && x.Attivo, ct);
            if (r is null) return NotFound();
        }
        // Ordine in bianco: Allegato 1 con i controlli delle commesse di Spettabile cliente (MAF2) e le loro prove.
        List<OrdinePrivatoDocx.ControlloAllegato>? allegato = null;
        if (r is null)
        {
            allegato = new();
            var voci = (await _commesse.ControlliCommesseClienteAsync(RegoleRichiestePrivati.CodiceClientePrivati, ct))
                .GroupBy(c => c.IdControllo).Select(g => g.OrderBy(c => c.Prezzo).First())
                .OrderBy(c => c.Controllo, StringComparer.CurrentCultureIgnoreCase);
            foreach (var v in voci)
            {
                var metodi = await MetodiAsync(v.IdControllo, ct);
                allegato.Add(new(v.IdControllo, v.Controllo, RegoleRichiestePrivati.IvaInclusa(v.Prezzo, AliquotaIva),
                    RegoleRichiestePrivati.TempiControllo(v.Controllo, metodi), metodi));
            }
        }
        var contenuto = OrdinePrivatoDocx.GeneraWord(OrdinePrivatoDocx.DaRichiesta(r, FusoItalia(), _utente.NomeCompleto, allegato), modello);
        await _audit.RegistraAsync(AreaFunzionale.Preventivi, "richiestaprivato.modulo", EsitoOperazione.Successo,
            tipoEntita: "RichiestaAnalisiPrivato", idEntita: id?.ToString() ?? "bianco", ct: ct);
        return File(contenuto, "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            OrdinePrivatoDocx.NomeFile(r));
    }

    // ── Helpers ──

    private async Task<List<MetodoRichiestaPrivato>> MetodiAsync(int idControllo, CancellationToken ct) =>
        (await _controlli.RigheDiControlloAsync(idControllo, ct))
            .Select(m => new MetodoRichiestaPrivato(
                (m.DettagliMetodo ?? m.Descrizione ?? m.DenominazioneRiga ?? "—").Trim(),
                m.RifBiblio?.Trim(), m.Accreditato, m.IdLabEsterno, m.LabEsterno?.Trim(), m.LabEsternoFrase?.Trim()))
            .ToList();

    private static string? Pulisci(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static TimeZoneInfo FusoItalia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
        catch (TimeZoneNotFoundException)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
            catch (TimeZoneNotFoundException) { return TimeZoneInfo.Local; }
        }
    }
}

public record RichiestaSalvaPrivato
{
    public int? IdRichiestaAnalisiPrivato { get; init; }
    public string? NomeCognome { get; init; }
    public string? CodiceFiscale { get; init; }
    public string? Indirizzo { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    public bool RitiroInLaboratorio { get; init; }
    public int Pagamento { get; init; } = 1;
    public int? IdMatriceArcLab { get; init; }
    public string? DescrizioneCampione { get; init; }
    public string? PuntoPrelievo { get; init; }
    public DateTimeOffset? DataOraPrelievo { get; init; }
    public int? IdImballaggioArcLab { get; init; }
    public string? Contenitore { get; init; }
    public bool? ContenitoreDelLaboratorio { get; init; }
    public decimal? TemperaturaRicevimento { get; init; }
    public int? TempiConsegnaGiorni { get; init; }
    public bool GiudizioRichiesto { get; init; }
    public bool ComunicazioneFuoriLimite { get; init; }
    public bool? ConsensoComunicazioni { get; init; }
    public string? NormaGiudizio { get; init; }
    public decimal? SupplementoGiudizio { get; init; }
    public string? Note { get; init; }
    public List<RigaSalvaPrivato>? Righe { get; init; }
}

public record RigaSalvaPrivato
{
    public int IdControllo { get; init; }
    public int? IdCommessa { get; init; }
}

/// <summary>
/// Se le tabelle delle richieste non esistono ancora (migrazione non applicata) restituisce un messaggio chiaro
/// invece del generico «Errore 500» (errore SQL 208, oggetto non valido).
/// </summary>
public sealed class TabelleRichiestePrivatiAttribute : Microsoft.AspNetCore.Mvc.Filters.ExceptionFilterAttribute
{
    public override void OnException(Microsoft.AspNetCore.Mvc.Filters.ExceptionContext context)
    {
        var testo = context.Exception.ToString();
        if (testo.Contains("RichiesteAnalisiPrivati", StringComparison.OrdinalIgnoreCase) && testo.Contains("208"))
        {
            context.Result = new ObjectResult(new
            {
                messaggio = "Le tabelle delle richieste dei privati non esistono ancora nel database: va creata e applicata la migrazione «RichiesteAnalisiPrivati»."
            }) { StatusCode = 500 };
            context.ExceptionHandled = true;
        }
    }
}
