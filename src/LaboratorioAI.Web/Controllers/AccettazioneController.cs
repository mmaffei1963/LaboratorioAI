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
/// Accettazione campioni fuori sede (consulenti/clienti). Scrive in tabelle di staging
/// (schema «accettazione»), MAI su ArcLab; da qui genera il tracciato XML per Palomar.
/// </summary>
[RichiedePermesso(Permessi.AccettazioneEsegui)]
public class AccettazioneController : Controller
{
    private const string MimeXml = "application/xml";

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IRepositoryAccettazione _rif;
    private readonly IGeneratoreTracciatoAccettazione _tracciato;
    private readonly IGeneratoreDocumentiAccettazione _documenti;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public AccettazioneController(
        ContestoLaboratorioAI ctx, IRepositoryAnagrafiche anagrafiche, IRepositoryAccettazione rif,
        IGeneratoreTracciatoAccettazione tracciato, IGeneratoreDocumentiAccettazione documenti, IContestoUtente utente,
        IServizioAudit audit)
    {
        _ctx = ctx; _anagrafiche = anagrafiche; _rif = rif; _tracciato = tracciato; _documenti = documenti; _utente = utente; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Accettazione campioni";
        return View();
    }

    // ── Anagrafiche di riferimento (ArcLab, sola lettura) ───────────
    [HttpGet("api/accettazione/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) => Json(await _anagrafiche.ClientiAsync(ct));

    [HttpGet("api/accettazione/insediamenti")]
    public async Task<IActionResult> Insediamenti([FromQuery] int idCliente, CancellationToken ct) =>
        Json(await _rif.InsediamentiPerClienteAsync(idCliente, ct));

    [HttpGet("api/accettazione/commesse")]
    public async Task<IActionResult> Commesse([FromQuery] int idInsediamento, CancellationToken ct) =>
        Json(await _rif.CommessePerInsediamentoAsync(idInsediamento, ct));

    [HttpGet("api/accettazione/tecniche-prelievo")]
    public async Task<IActionResult> TecnichePrelievo(CancellationToken ct) => Json(await _rif.TecnichePrelievoAsync(ct));

    [HttpGet("api/accettazione/imballaggi")]
    public async Task<IActionResult> Imballaggi(CancellationToken ct) => Json(await _rif.ImballaggiAsync(ct));

    [HttpGet("api/accettazione/matrici-tecnica")]
    public async Task<IActionResult> MatriciTecnica(CancellationToken ct) => Json(await _rif.MatriciTecnicaAsync(ct));

    [HttpGet("api/accettazione/laboratori-esterni")]
    public async Task<IActionResult> LaboratoriEsterni(CancellationToken ct) => Json(await _rif.LaboratoriEsterniAsync(ct));

    [HttpGet("api/accettazione/matrici")]
    public async Task<IActionResult> Matrici(CancellationToken ct) => Json(await _anagrafiche.MatriciAsync(ct));

    [HttpGet("api/accettazione/controlli")]
    public async Task<IActionResult> Controlli(CancellationToken ct) => Json(await _anagrafiche.ControlliAsync(ct));

    [HttpGet("api/accettazione/operatori")]
    public async Task<IActionResult> Operatori(CancellationToken ct) => Json(await _rif.OperatoriAsync(ct));

    [HttpGet("api/accettazione/termometri")]
    public async Task<IActionResult> Termometri(CancellationToken ct) => Json(await _rif.TermometriAsync(ct));

    /// <summary>Righe metodo (rigametodo) di un controllo, da caricare come prove — maschera Campione2.</summary>
    [HttpGet("api/accettazione/righe-controllo")]
    public async Task<IActionResult> RigheControllo([FromQuery] int idControllo, CancellationToken ct) =>
        Json(await _rif.RigheMetodoPerControlloAsync(idControllo, ct));

    // ── Registro e dettaglio ────────────────────────────────────────
    [HttpGet("api/accettazione/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.AccettazioneLotti.AsNoTracking()
            .Where(l => l.Attivo && l.DataCancellazione == null)
            .OrderByDescending(l => l.DataCreazione).Take(300)
            .Select(l => new
            {
                l.IdAccettazioneLotto, l.CodiceLocale, l.CodLotto, l.VerbalePrelievo, Stato = l.Stato.ToString(),
                l.Cliente, l.Commessa, l.DataConferimento, l.Origine,
                NumeroCampioni = l.Campioni.Count, l.DataTrasmissione
            }).ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/accettazione/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var l = await _ctx.AccettazioneLotti.AsNoTracking()
            .Include(x => x.Campioni).ThenInclude(c => c.Prove)
            .FirstOrDefaultAsync(x => x.IdAccettazioneLotto == id, ct);
        if (l is null) return NotFound();
        return Json(new
        {
            l.IdAccettazioneLotto, l.CodiceLocale, l.CodLotto, Stato = l.Stato.ToString(),
            l.IdClienteArcLab, l.Cliente, l.IdInsediamentoArcLab, l.Insediamento,
            l.IdCommessaArcLab, l.Commessa, l.DataConferimento,
            l.IdPrelevatore, l.Prelevatore, l.Vettore, l.VerbalePrelievo,
            l.IdRicevitore, l.Ricevitore, l.Note, l.Origine,
            Campioni = l.Campioni.OrderBy(c => c.Progressivo).Select(c => new
            {
                c.Progressivo, c.IdMatriceArcLab, c.Matrice, c.Descrizione,
                c.EtichettaCliente, c.SiglaCliente, c.Identificativo, c.Imballaggio,
                c.ProceduraPrelievo, c.CameraLegionella, c.DataPrevistaConsegna, c.RitardoAnalisiGiorni,
                c.DataPrelievo, c.OraPrelievo, c.PuntoPrelievo, c.LuogoPrelievo, c.Temperatura, c.TempRicezione,
                c.IdTermometroArcLab, c.Termometro, c.CondizioniTrasporto, c.Note,
                c.IdLabEsternoArcLab, c.LabEsterno, c.DataInvioLab, c.NoteLabEsterno,
                c.ConsegnaPrevistaLabEstGiorni, c.CertificatoNonRichiesto,
                Prove = c.Prove.Select(p => new { p.IdControlloArcLab, p.Controllo, p.IdMetodoArcLab, p.IdRigaMetodoArcLab, p.Metodo, p.Parametro, p.LaboratorioEsterno, p.Note })
            })
        });
    }

    // ── Salvataggio (upsert con campioni e prove) ───────────────────
    [HttpPost("api/accettazione/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaAccettazione req, CancellationToken ct)
    {
        var ora = DateTime.UtcNow;
        AccettazioneLotto l;
        if (req.IdAccettazioneLotto is int id and > 0)
        {
            l = await _ctx.AccettazioneLotti.Include(x => x.Campioni).ThenInclude(c => c.Prove)
                    .FirstOrDefaultAsync(x => x.IdAccettazioneLotto == id, ct)
                ?? throw new InvalidOperationException("Accettazione non trovata.");
            if (l.Stato is StatoAccettazione.Trasmesso or StatoAccettazione.Importato)
                return BadRequest(new { messaggio = "Accettazione già trasmessa: non modificabile." });
            _ctx.AccettazioneProve.RemoveRange(l.Campioni.SelectMany(c => c.Prove));
            _ctx.AccettazioneCampioni.RemoveRange(l.Campioni);
            l.Campioni.Clear();
            l.DataUltimaModifica = ora;
        }
        else
        {
            l = new AccettazioneLotto { DataCreazione = ora, DataUltimaModifica = ora, Stato = StatoAccettazione.Bozza };
            if (!string.IsNullOrWhiteSpace(req.CodiceLocale)) l.CodiceLocale = req.CodiceLocale!.Trim();
            _ctx.AccettazioneLotti.Add(l);
        }

        // ── Regole prelevatore / vettore / verbale ──
        bool prelevatoreCliente = ClienteOTerzaParte(req.Prelevatore);
        bool interno = req.IdPrelevatore.HasValue && !prelevatoreCliente;
        if (!interno && !prelevatoreCliente)
            return BadRequest(new { messaggio = "Indicare il prelevatore." });
        // Il verbale non è più obbligatorio: se il prelevatore è interno e non è indicato,
        // viene assegnato automaticamente = CodLotto (l'accettazione stessa fa da verbale).

        var ricezione = req.DataOraRicezione ?? DateTime.Now;

        // ── Validazione campioni ──
        var campioni = req.Campioni ?? new();
        for (int i = 0; i < campioni.Count; i++)
        {
            var c = campioni[i]; int n = i + 1;
            if (string.IsNullOrWhiteSpace(c.Matrice)) return BadRequest(new { messaggio = $"Campione n°{n}: la matrice è obbligatoria." });
            if (string.IsNullOrWhiteSpace(c.Descrizione)) return BadRequest(new { messaggio = $"Campione n°{n}: la denominazione è obbligatoria." });
            if (string.IsNullOrWhiteSpace(c.Imballaggio)) return BadRequest(new { messaggio = $"Campione n°{n}: l'imballaggio è obbligatorio." });
            if (!c.Temperatura.HasValue) return BadRequest(new { messaggio = $"Campione n°{n}: la temperatura di prelievo è obbligatoria." });
            if (!c.DataPrelievo.HasValue) return BadRequest(new { messaggio = $"Campione n°{n}: la data/ora di prelievo è obbligatoria." });
        }

        l.IdClienteArcLab = req.IdClienteArcLab; l.Cliente = req.Cliente;
        l.IdInsediamentoArcLab = req.IdInsediamentoArcLab; l.Insediamento = req.Insediamento;
        l.IdCommessaArcLab = req.IdCommessaArcLab; l.Commessa = req.Commessa;
        l.DataConferimento = ricezione;
        l.IdPrelevatore = interno ? req.IdPrelevatore : null;
        l.Prelevatore = req.Prelevatore;
        l.Vettore = interno ? req.Prelevatore : "Cliente";                 // automatico
        l.VerbalePrelievo = interno ? req.VerbalePrelievo?.Trim() : null;   // vietato se cliente/terza parte
        l.IdRicevitore = req.IdRicevitore; l.Ricevitore = req.Ricevitore;
        l.Note = req.Note;
        l.Origine = string.IsNullOrWhiteSpace(req.Origine) ? "consulente" : req.Origine;
        l.IdUtente = _utente.IdUtente;
        if (req.Stato is int s && Enum.IsDefined(typeof(StatoAccettazione), s)) l.Stato = (StatoAccettazione)s;

        // Termometri da ArcLab: il neg contiene "463", il pos "446".
        var termometri = await _rif.TermometriAsync(ct);
        var termNeg = termometri.FirstOrDefault(t => (t.Nome ?? "").Contains("463"));
        var termPos = termometri.FirstOrDefault(t => (t.Nome ?? "").Contains("446"));

        int prog = 1;
        foreach (var cDto in campioni)
        {
            var term = TermometroScelto(interno, cDto.TempRicezione, termNeg, termPos);
            var c = new AccettazioneCampione
            {
                Progressivo = cDto.Progressivo > 0 ? cDto.Progressivo : prog,
                IdMatriceArcLab = cDto.IdMatriceArcLab, Matrice = cDto.Matrice,
                Descrizione = cDto.Descrizione, Imballaggio = cDto.Imballaggio,
                EtichettaCliente = cDto.EtichettaCliente, SiglaCliente = cDto.SiglaCliente,
                Identificativo = cDto.Identificativo, ProceduraPrelievo = cDto.ProceduraPrelievo,
                CameraLegionella = cDto.CameraLegionella, DataPrevistaConsegna = cDto.DataPrevistaConsegna,
                RitardoAnalisiGiorni = cDto.RitardoAnalisiGiorni,
                DataPrelievo = cDto.DataPrelievo,
                OraPrelievo = cDto.DataPrelievo?.ToString("HH:mm"),
                PuntoPrelievo = cDto.PuntoPrelievo,
                LuogoPrelievo = cDto.LuogoPrelievo,
                Temperatura = cDto.Temperatura,
                TempRicezione = cDto.TempRicezione,
                CondizioniTrasporto = Condizioni(cDto.TempRicezione),      // automatico
                IdTermometroArcLab = term?.Id,
                Termometro = term?.Nome,                                   // automatico
                Note = cDto.Note,
                IdLabEsternoArcLab = cDto.IdLabEsternoArcLab, LabEsterno = cDto.LabEsterno,
                DataInvioLab = cDto.DataInvioLab, NoteLabEsterno = cDto.NoteLabEsterno,
                ConsegnaPrevistaLabEstGiorni = cDto.ConsegnaPrevistaLabEstGiorni,
                CertificatoNonRichiesto = cDto.CertificatoNonRichiesto
            };
            foreach (var pDto in cDto.Prove ?? new())
                c.Prove.Add(new AccettazioneProva
                {
                    IdControlloArcLab = pDto.IdControlloArcLab, Controllo = pDto.Controllo,
                    IdMetodoArcLab = pDto.IdMetodoArcLab, IdRigaMetodoArcLab = pDto.IdRigaMetodoArcLab,
                    Metodo = pDto.Metodo, Parametro = pDto.Parametro,
                    LaboratorioEsterno = pDto.LaboratorioEsterno, Note = pDto.Note
                });
            l.Campioni.Add(c);
            prog++;
        }

        // Il CodLotto NON è più assegnato qui: lo genera Palomar all'import, in continuità con
        // il flusso legionella, agganciandolo tramite il nostro CodiceLocale.
        // Il contatore interno viene riusato per il NUMERO DI VERBALE (nostro documento):
        // se il prelevatore è interno e il verbale non è indicato, ne assegniamo uno progressivo.
        if (interno && string.IsNullOrWhiteSpace(l.VerbalePrelievo))
        {
            // La strategia di retry (EnableRetryOnFailure) non consente transazioni manuali:
            // vanno eseguite dentro l'execution strategy come unità ritentabile.
            var strategy = _ctx.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
                var cont = await _ctx.ContatoriAccettazione
                    .FromSqlRaw("SELECT * FROM [accettazione].[Contatori] WITH (UPDLOCK, HOLDLOCK) WHERE Id = 1")
                    .FirstAsync(ct);
                cont.ProssimoValore += 1;
                l.VerbalePrelievo = FormattaVerbale(cont.ProssimoValore);
                await _ctx.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        else
        {
            await _ctx.SaveChangesAsync(ct);
        }
        await _audit.RegistraAsync(
            AreaFunzionale.Accettazione, "accettazione.salvata", EsitoOperazione.Successo,
            tipoEntita: "AccettazioneLotto", idEntita: l.IdAccettazioneLotto.ToString(),
            dettaglio: new { l.CodiceLocale, l.VerbalePrelievo, Stato = l.Stato.ToString(), Campioni = l.Campioni.Count }, ct: ct);
        return Json(new { l.IdAccettazioneLotto, l.CodiceLocale, l.CodLotto, l.VerbalePrelievo, Stato = l.Stato.ToString() });
    }

    /// <summary>Numero di verbale interno: VP-000001, VP-000002, …</summary>
    private static string FormattaVerbale(long n) => $"VP-{n:D6}";

    [HttpPost("api/accettazione/{id:int}/elimina")]
    public async Task<IActionResult> Elimina(int id, CancellationToken ct)
    {
        var l = await _ctx.AccettazioneLotti.FirstOrDefaultAsync(x => x.IdAccettazioneLotto == id, ct);
        if (l is null) return NotFound();
        if (l.Stato is StatoAccettazione.Trasmesso or StatoAccettazione.Importato)
            return BadRequest(new { messaggio = "Accettazione già trasmessa: non eliminabile." });
        l.Attivo = false; l.DataCancellazione = DateTime.UtcNow; l.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Accettazione, "accettazione.eliminata", EsitoOperazione.Successo,
            tipoEntita: "AccettazioneLotto", idEntita: id.ToString(),
            dettaglio: new { l.CodiceLocale }, ct: ct);
        return Ok();
    }

    // ── Generazione tracciato XML per Palomar ───────────────────────
    [HttpPost("api/accettazione/{id:int}/tracciato")]
    [RichiedePermesso(Permessi.AccettazioneTrasmetti)]
    public async Task<IActionResult> Tracciato(int id, CancellationToken ct)
    {
        var l = await _ctx.AccettazioneLotti.Include(x => x.Campioni).ThenInclude(c => c.Prove)
            .FirstOrDefaultAsync(x => x.IdAccettazioneLotto == id, ct);
        if (l is null) return NotFound(new { messaggio = "Accettazione non trovata." });
        if (l.Campioni.Count == 0) return BadRequest(new { messaggio = "Nessun campione: aggiungine almeno uno." });

        var bytes = _tracciato.GeneraXml(l);
        var nome = _tracciato.NomeFile(l);

        l.Stato = StatoAccettazione.Trasmesso;
        l.DataTrasmissione = DateTime.UtcNow;
        l.NomeFileTracciato = nome;
        l.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Accettazione, "accettazione.tracciato.xml", EsitoOperazione.Successo,
            tipoEntita: "AccettazioneLotto", idEntita: id.ToString(),
            dettaglio: new { l.CodiceLocale, NomeFile = nome, Campioni = l.Campioni.Count }, ct: ct);
        return File(bytes, MimeXml, nome);
    }

    // ── Documenti PDF (richiesta lab esterno, catena custodia, etichette, scheda ricezione) ──
    private const string MimePdf = "application/pdf";

    private async Task<AccettazioneLotto?> CaricaLottoAsync(int id, CancellationToken ct) =>
        await _ctx.AccettazioneLotti.AsNoTracking()
            .Include(x => x.Campioni).ThenInclude(c => c.Prove)
            .FirstOrDefaultAsync(x => x.IdAccettazioneLotto == id, ct);

    [HttpPost("api/accettazione/{id:int}/campione/{prog:int}/pdf/{tipo}")]
    public async Task<IActionResult> PdfCampione(int id, int prog, string tipo, CancellationToken ct)
    {
        var l = await CaricaLottoAsync(id, ct);
        if (l is null) return NotFound(new { messaggio = "Accettazione non trovata." });
        var c = l.Campioni.FirstOrDefault(x => x.Progressivo == prog);
        if (c is null) return NotFound(new { messaggio = "Campione non trovato." });
        var m = await _rif.IntestazioneAsync(ct);
        var lab = c.IdLabEsternoArcLab.HasValue ? await _rif.LabEsternoAsync(c.IdLabEsternoArcLab.Value, ct) : null;
        byte[] bytes;
        switch (tipo)
        {
            case "richiesta": bytes = _documenti.RichiestaAnalisi(l, c, m, lab); break;
            case "catena":
                var dest = l.IdInsediamentoArcLab.HasValue ? await _rif.IndirizzoDestinatarioAsync(l.IdInsediamentoArcLab.Value, ct) : null;
                bytes = _documenti.CatenaCustodia(l, c, m, lab, dest); break;
            case "etichetta": bytes = _documenti.Etichette(l, new[] { c }); break;
            case "scheda": bytes = _documenti.SchedaRicezione(l, new[] { c }); break;
            default: return BadRequest(new { messaggio = "Tipo documento non riconosciuto." });
        }
        await _audit.RegistraAsync(
            AreaFunzionale.Accettazione, "accettazione.pdf.campione", EsitoOperazione.Successo,
            tipoEntita: "AccettazioneCampione", idEntita: $"{id}/{prog}",
            dettaglio: new { Tipo = tipo }, ct: ct);
        return File(bytes, MimePdf, _documenti.NomeFile(tipo, l, prog));
    }

    [HttpPost("api/accettazione/{id:int}/pdf/{tipo}")]
    public async Task<IActionResult> PdfLotto(int id, string tipo, CancellationToken ct)
    {
        var l = await CaricaLottoAsync(id, ct);
        if (l is null) return NotFound(new { messaggio = "Accettazione non trovata." });
        var campioni = l.Campioni.OrderBy(c => c.Progressivo).ToList();
        if (campioni.Count == 0) return BadRequest(new { messaggio = "Nessun campione nel lotto." });
        byte[]? bytes = tipo switch
        {
            "etichette" => _documenti.Etichette(l, campioni),
            "schede" => _documenti.SchedaRicezione(l, campioni),
            _ => null
        };
        if (bytes is null) return BadRequest(new { messaggio = "Tipo documento non riconosciuto." });
        await _audit.RegistraAsync(
            AreaFunzionale.Accettazione, "accettazione.pdf.lotto", EsitoOperazione.Successo,
            tipoEntita: "AccettazioneLotto", idEntita: id.ToString(),
            dettaglio: new { Tipo = tipo, Campioni = campioni.Count }, ct: ct);
        return File(bytes, MimePdf, _documenti.NomeFile(tipo, l, null));
    }

    // ── Verifica di importabilità: i riferimenti ArcLab esistono ancora? ──
    [HttpPost("api/accettazione/{id:int}/verifica")]
    public async Task<IActionResult> Verifica(int id, CancellationToken ct)
    {
        var l = await CaricaLottoAsync(id, ct);
        if (l is null) return NotFound(new { messaggio = "Accettazione non trovata." });

        var rif = new RiferimentiDaVerificare
        {
            IdCliente = l.IdClienteArcLab,
            IdInsediamento = l.IdInsediamentoArcLab,
            IdCommessa = l.IdCommessaArcLab,
            IdPrelevatore = l.IdPrelevatore,
            IdRicevitore = l.IdRicevitore,
            Matrici = l.Campioni.Where(c => c.IdMatriceArcLab.HasValue).Select(c => c.IdMatriceArcLab!.Value).Distinct().ToList(),
            Termometri = l.Campioni.Where(c => c.IdTermometroArcLab.HasValue).Select(c => c.IdTermometroArcLab!.Value).Distinct().ToList(),
            LabEsterni = l.Campioni.Where(c => c.IdLabEsternoArcLab.HasValue).Select(c => c.IdLabEsternoArcLab!.Value).Distinct().ToList(),
            RigheMetodo = l.Campioni.SelectMany(c => c.Prove).Where(p => p.IdRigaMetodoArcLab.HasValue)
                           .Select(p => p.IdRigaMetodoArcLab!.Value).Distinct().ToList()
        };
        var esito = await _rif.VerificaImportabilitaAsync(rif, ct);
        return Json(esito);
    }

    // ── Regole automatiche (autorevoli lato server) ─────────────────
    private static bool ClienteOTerzaParte(string? prelevatore)
    {
        var p = (prelevatore ?? "").Trim().ToLowerInvariant().Replace("_", " ");
        return p is "cliente" || p.Contains("terza parte");
    }

    /// <summary>Prova a leggere la temperatura di ricezione come numero (null se "Ambiente" o vuoto).</summary>
    private static double? NumeroTemp(string? tempRicezione)
    {
        var t = (tempRicezione ?? "").Trim().Replace("+", "").Replace(",", ".");
        return double.TryParse(t, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static string? Condizioni(string? tempRicezione)
    {
        var n = NumeroTemp(tempRicezione);
        if (n is null) return "Temp. ambiente";
        return n < 0 ? "Surgelato" : "Refrigerato";
    }

    /// <summary>Termometro scelto dalla lista ArcLab secondo prelevatore e temp. ricezione.</summary>
    private static LaboratorioAI.Core.Modelli.VoceAnagrafica? TermometroScelto(
        bool interno, string? tempRicezione,
        LaboratorioAI.Core.Modelli.VoceAnagrafica? neg, LaboratorioAI.Core.Modelli.VoceAnagrafica? pos)
    {
        var n = NumeroTemp(tempRicezione);
        if (n is null) return null;                    // temp. ambiente → nessun termometro
        if (!interno) return neg;                      // cliente/terza parte → sempre "463"
        return n < 0 ? neg : pos;
    }
}

// ── DTO ─────────────────────────────────────────────────────────────
public record RichiestaSalvaAccettazione
{
    public int? IdAccettazioneLotto { get; init; }
    public string? CodiceLocale { get; init; }
    public int? Stato { get; init; }
    public int? IdClienteArcLab { get; init; }
    public string? Cliente { get; init; }
    public int? IdInsediamentoArcLab { get; init; }
    public string? Insediamento { get; init; }
    public int? IdCommessaArcLab { get; init; }
    public string? Commessa { get; init; }
    public DateOnly? DataConferimento { get; init; }
    public int? IdPrelevatore { get; init; }
    public string? Prelevatore { get; init; }
    public string? VerbalePrelievo { get; init; }
    public int? IdRicevitore { get; init; }
    public string? Ricevitore { get; init; }
    public string? Note { get; init; }
    public string? Origine { get; init; }
    public DateTime? DataOraRicezione { get; init; }
    public List<CampioneDto>? Campioni { get; init; }
}

public record CampioneDto
{
    public int Progressivo { get; init; }
    public int? IdMatriceArcLab { get; init; }
    public string? Matrice { get; init; }
    public string? Descrizione { get; init; }
    public string? EtichettaCliente { get; init; }
    public string? SiglaCliente { get; init; }
    public string? Identificativo { get; init; }
    public string? Imballaggio { get; init; }
    public string? ProceduraPrelievo { get; init; }
    public string? CameraLegionella { get; init; }
    public DateTime? DataPrevistaConsegna { get; init; }
    public int? RitardoAnalisiGiorni { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    public string? LuogoPrelievo { get; init; }
    public double? Temperatura { get; init; }
    public string? TempRicezione { get; init; }
    public string? Note { get; init; }
    // Laboratorio esterno
    public int? IdLabEsternoArcLab { get; init; }
    public string? LabEsterno { get; init; }
    public DateTime? DataInvioLab { get; init; }
    public string? NoteLabEsterno { get; init; }
    public int? ConsegnaPrevistaLabEstGiorni { get; init; }
    public bool CertificatoNonRichiesto { get; init; }
    public List<ProvaDto>? Prove { get; init; }
}

public record ProvaDto
{
    public int? IdControlloArcLab { get; init; }
    public string? Controllo { get; init; }
    public int? IdMetodoArcLab { get; init; }
    public int? IdRigaMetodoArcLab { get; init; }
    public string? Metodo { get; init; }
    public string? Parametro { get; init; }
    public string? LaboratorioEsterno { get; init; }
    public string? Note { get; init; }
}
