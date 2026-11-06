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
/// Rapportini di consulenza: il consulente compila al sopralluogo cosa è stato fatto e cosa sarà
/// fatturato; il PDF va al cliente (cartella Palomar, con registro di controllo) che ha 3 giorni per
/// le osservazioni. La conferma è manuale (fase 1). Anagrafiche e commesse lette da ArcLab in SOLA
/// LETTURA: nessuna scrittura sul gestionale.
/// </summary>
[RichiedePermesso(Permessi.RapportiniCompila)]
public class RapportiniController : Controller
{
    private const string ChiaveCartellaOutput = "Rapportini.CartellaOutput";
    private const string ChiaveCartellaPalomar = "Rapportini.CartellaPalomar";

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryConsulenze _arclab;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IGeneratoreRapportino _generatore;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly ServizioRegistroDocumenti _registro;
    private readonly IWebHostEnvironment _ambiente;

    public RapportiniController(ContestoLaboratorioAI ctx, IRepositoryConsulenze arclab, IRepositoryAnagrafiche anagrafiche,
        IGeneratoreRapportino generatore, IContestoUtente utente, IServizioAudit audit,
        ServizioRegistroDocumenti registro, IWebHostEnvironment ambiente)
    {
        _ctx = ctx; _arclab = arclab; _anagrafiche = anagrafiche; _generatore = generatore;
        _utente = utente; _audit = audit; _registro = registro; _ambiente = ambiente;
    }

    private bool Gestore => _utente.HaPermesso(Permessi.RapportiniGestione);

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Rapportini di consulenza";
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // Cataloghi (ArcLab, sola lettura)
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/rapportini/clienti")]
    public async Task<IActionResult> Clienti([FromQuery] string? filtro, CancellationToken ct)
    {
        var clienti = await _anagrafiche.ClientiAsync(ct);
        var f = (filtro ?? "").Trim();
        IEnumerable<VoceAnagrafica> q = clienti;
        if (f.Length > 0)
            q = int.TryParse(f, out var id)
                ? clienti.Where(c => c.Id == id || c.Nome.Contains(f, StringComparison.OrdinalIgnoreCase))
                : clienti.Where(c => c.Nome.Contains(f, StringComparison.OrdinalIgnoreCase));
        return Json(q.Take(50).Select(c => new { c.Id, Nome = c.Nome.Trim() }));
    }

    [HttpGet("api/rapportini/clienti/{idCliente:int}/insediamenti")]
    public async Task<IActionResult> Insediamenti(int idCliente, CancellationToken ct) =>
        Json(await _arclab.InsediamentiAsync(idCliente, ct));

    /// <summary>Tutte le commesse di consulenza del cliente (attive e non), con la voce proposta.</summary>
    [HttpGet("api/rapportini/clienti/{idCliente:int}/commesse")]
    public async Task<IActionResult> Commesse(int idCliente, CancellationToken ct)
    {
        var commesse = await _arclab.CommesseConsulenzaAsync(idCliente, ct);
        var fasce = await FasceVociAsync(ct);
        return Json(commesse.Select(c => new
        {
            c.IdCommessa, c.IdInsediamento, c.NomeInsediamento, c.DescrIncarico, c.PrezzoControllo,
            DataCommessa = c.DataCommessa?.ToString("yyyy-MM-dd"), c.Attiva, c.IdTipoIncarico, c.NContratto,
            VoceProposta = VoceDa(fasce, c.IdTipoIncarico),
            RiferimentoProposto = RiferimentoDaCommessa(c)
        }));
    }

    /// <summary>Organico in servizio + ultimo consulente scelto dall'utente collegato.</summary>
    [HttpGet("api/rapportini/consulenti")]
    public async Task<IActionResult> Consulenti(CancellationToken ct)
    {
        var elenco = await _arclab.ConsulentiAsync(ct);
        int? predefinito = null;
        if (_utente.IdUtente is int idu)
            predefinito = await _ctx.ConsulentiUtenti.AsNoTracking().Where(x => x.IdUtente == idu)
                .Select(x => (int?)x.IdOrganicoArcLab).FirstOrDefaultAsync(ct);
        return Json(new
        {
            predefinito,
            consulenti = elenco.Select(c => new { c.IdOrganico, c.Nome, c.CognomeOrg })
        });
    }

    /// <summary>Voci AZW selezionabili (per i rapportini senza commessa).</summary>
    [HttpGet("api/rapportini/voci")]
    public async Task<IActionResult> Voci(CancellationToken ct)
    {
        var voci = await _ctx.VociIncarico.AsNoTracking().Where(v => v.Attivo && v.Voce != "")
            .Select(v => v.Voce).Distinct().OrderBy(v => v).ToListAsync(ct);
        return Json(voci);
    }

    /// <summary>
    /// Frasi già usate per la descrizione in fattura: prima quelle del cliente, poi del consulente, poi
    /// tutte; in coda le descrizioni delle commesse di consulenza del cliente.
    /// </summary>
    [HttpGet("api/rapportini/frasi")]
    public async Task<IActionResult> Frasi([FromQuery] int? idCliente, [FromQuery] int? idOrganico, CancellationToken ct)
    {
        // Senza cliente o consulente il confronto con NULL diventava COUNT(NULL), che SQL Server rifiuta (errore 8117):
        // si usano valori che non corrispondono a nessun rapportino.
        var cli = idCliente ?? -1;
        var org = idOrganico ?? -1;
        var storico = await _ctx.Rapportini.AsNoTracking()
            .Where(r => r.Stato != StatoRapportino.Annullato)
            .GroupBy(r => r.DescrizioneFattura)
            .Select(g => new
            {
                Frase = g.Key,
                Cliente = g.Count(r => r.IdClienteArcLab == cli),
                Consulente = g.Count(r => r.IdOrganicoArcLab == org),
                Totale = g.Count(),
                Ultima = g.Max(r => r.DataIntervento)
            })
            .OrderByDescending(x => x.Cliente).ThenByDescending(x => x.Consulente)
            .ThenByDescending(x => x.Totale).ThenByDescending(x => x.Ultima)
            .Take(200).ToListAsync(ct);

        var frasi = storico.Select(x => x.Frase).ToList();
        if (idCliente is int idc)
        {
            var commesse = await _arclab.CommesseConsulenzaAsync(idc, ct);
            foreach (var d in commesse.Select(c => c.DescrIncarico?.Trim()).Where(d => !string.IsNullOrWhiteSpace(d)))
                if (!frasi.Contains(d!, StringComparer.OrdinalIgnoreCase)) frasi.Add(d!);
        }
        return Json(frasi.Select(f => f.Length > 250 ? f[..250] : f));
    }

    /// <summary>Documenti tipo attivi (per le righe di documentazione di riferimento).</summary>
    [HttpGet("api/rapportini/documenti-tipo")]
    public async Task<IActionResult> DocumentiTipo(CancellationToken ct) =>
        Json(await _ctx.DocumentiTipo.AsNoTracking().Where(x => x.Attivo)
            .OrderBy(x => x.Ordine).ThenBy(x => x.Nome)
            .Select(x => new { x.IdDocumentoTipo, x.Nome, x.Area, x.RichiamaLaboratorio, x.PianoAnalitico })
            .ToListAsync(ct));

    /// <summary>Delega operativa del cliente in essere alla data (registro di amministrazione).</summary>
    [HttpGet("api/rapportini/clienti/{idCliente:int}/delega")]
    public async Task<IActionResult> Delega(int idCliente, [FromQuery] DateTime? data, [FromQuery] int? idOrganico,
        [FromQuery] int? idInsediamento, CancellationToken ct)
    {
        var dl = idOrganico is int ido ? await DelegaValidaAsync(idCliente, ido, idInsediamento, (data ?? DateTime.Today).Date, ct) : null;
        return Json(new { presente = dl is not null, riferimento = dl is null ? null : TestoDelega(dl) });
    }

    // ═══════════════════════════════════════════════════════════════
    // Elenco e dettaglio
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/rapportini")]
    public async Task<IActionResult> Elenco([FromQuery] DateTime? dal, [FromQuery] DateTime? al, [FromQuery] int? stato,
        [FromQuery] string? testo, [FromQuery] bool soloMiei, CancellationToken ct)
    {
        var q = _ctx.Rapportini.AsNoTracking().AsQueryable();
        if (!Gestore || soloMiei)
        {
            var idu = _utente.IdUtente ?? -1;
            q = q.Where(r => r.IdUtenteCreazione == idu);
        }
        if (dal is DateTime d1) q = q.Where(r => r.DataIntervento >= d1.Date);
        if (al is DateTime d2) q = q.Where(r => r.DataIntervento <= d2.Date);
        if (stato is int s) q = q.Where(r => (int)r.Stato == s);
        if (!string.IsNullOrWhiteSpace(testo))
        {
            var t = testo.Trim();
            q = int.TryParse(t, out var n)
                ? q.Where(r => r.IdRapportino == n || r.RagioneSociale.Contains(t))
                : q.Where(r => r.RagioneSociale.Contains(t) || r.Consulente.Contains(t) || r.DescrizioneFattura.Contains(t)
                               || (r.NomeInsediamento != null && r.NomeInsediamento.Contains(t)));
        }
        var righe = await q.OrderByDescending(r => r.DataIntervento).ThenByDescending(r => r.IdRapportino)
            .Take(1000).ToListAsync(ct);
        return Json(new { gestore = Gestore, righe = righe.Select(Riepilogo) });
    }

    [HttpGet("api/rapportini/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var r = await CaricaAsync(id, true, ct);
        if (r is null || !PuoVedere(r)) return NotFound(new { messaggio = "Rapportino non trovato." });
        return Json(Completo(r));
    }

    private bool PuoVedere(Rapportino r) => Gestore || r.IdUtenteCreazione == _utente.IdUtente;

    private static object Riepilogo(Rapportino r) => new
    {
        r.IdRapportino,
        DataIntervento = r.DataIntervento.ToString("yyyy-MM-dd"),
        r.RagioneSociale, r.NomeInsediamento, r.Consulente, r.DescrizioneFattura, r.Ore,
        Modalita = r.ModalitaFatturazione.ToString(), r.Prepagato,
        r.Importo, r.ImportoCorretto, r.ImportoDaFatturare, r.SoloRendicontato,
        Stato = r.Stato.ToString(), StatoNum = (int)r.Stato,
        DataInvio = r.DataInvio is DateTime di ? DateTime.SpecifyKind(di, DateTimeKind.Utc) : (DateTime?)null,
        r.Modificabile, r.EmailInsediamento
    };

    private static object Completo(Rapportino r) => new
    {
        r.IdRapportino, r.IdClienteArcLab, r.RagioneSociale, r.IdInsediamentoArcLab, r.NomeInsediamento,
        r.IndirizzoInsediamento, r.EmailInsediamento,
        DataIntervento = r.DataIntervento.ToString("yyyy-MM-dd"),
        r.IdOrganicoArcLab, r.Consulente, r.IdCommessaArcLab, r.DescrizioneCommessa, r.PrezzoCommessa, r.Voce,
        r.DescrizioneFattura, r.Ore, Luogo = (int)r.Luogo, ModalitaImporto = (int)r.ModalitaImporto, r.TariffaOraria,
        r.Importo, r.ImportoCorretto, r.MotivoCorrezione, ModalitaFatturazione = (int)r.ModalitaFatturazione,
        r.Prepagato, DataUltimoScalo = r.DataUltimoScalo?.ToString("yyyy-MM-dd"), r.OreResiduePrima, r.OreResidueDopo,
        r.NoteInterne, Stato = r.Stato.ToString(), StatoNum = (int)r.Stato, r.Modificabile, r.SoloRendicontato,
        r.NotaContestazione, r.MotivoAnnullamento, r.NomeFilePdf, r.FilePalomar, r.ImportoDaFatturare,
        r.AttivitaSvolta, r.RiferimentoOfferta, r.RiferimentoDelega, r.FonteInformazioni, r.NotaPianoAnalitico,
        r.FollowUpAzione, r.FollowUpResponsabile, r.FollowUpTermine, r.VerificaRichiamiLaboratorio,
        Documenti = r.Documenti.Where(x => x.Attivo).OrderBy(x => x.Ordine).Select(x => new
        {
            x.IdDocumentoTipo, x.Nome, x.Revisione, DataDocumento = x.DataDocumento?.ToString("yyyy-MM-dd"),
            Azione = (int)x.Azione, Consegna = (int?)x.Consegna, x.RichiamaLaboratorio, x.PianoAnalitico
        })
    };

    /// <summary>Carica un rapportino con le righe di documentazione.</summary>
    private Task<Rapportino?> CaricaAsync(int id, bool soloLettura, CancellationToken ct)
    {
        var q = _ctx.Rapportini.Include(x => x.Documenti).AsQueryable();
        if (soloLettura) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(x => x.IdRapportino == id, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // Compilazione
    // ═══════════════════════════════════════════════════════════════

    public record RapportinoDto(
        int IdCliente, int IdInsediamento, DateTime DataIntervento, int IdOrganico, int? IdCommessa, string? Voce,
        string? DescrizioneFattura, decimal Ore, int Luogo, int ModalitaImporto, decimal? TariffaOraria, decimal? Importo,
        int ModalitaFatturazione, bool Prepagato, DateTime? DataUltimoScalo, decimal? OreResiduePrima, string? NoteInterne,
        string? AttivitaSvolta = null, string? RiferimentoOfferta = null, string? FonteInformazioni = null,
        string? NotaPianoAnalitico = null, string? FollowUpAzione = null, string? FollowUpResponsabile = null,
        string? FollowUpTermine = null, bool VerificaRichiamiLaboratorio = false, List<DocumentoDto>? Documenti = null);

    public record DocumentoDto(int? IdDocumentoTipo, string? Nome, string? Revisione, DateTime? DataDocumento, int Azione, int? Consegna);

    [HttpPost("api/rapportini")]
    public async Task<IActionResult> Crea([FromBody] RapportinoDto d, CancellationToken ct)
    {
        var r = new Rapportino
        {
            IdUtenteCreazione = _utente.IdUtente,
            DataCreazione = DateTime.UtcNow,
            Stato = StatoRapportino.Bozza
        };
        var errore = await ApplicaAsync(r, d, ct);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        r.DataUltimaModifica = DateTime.UtcNow;
        _ctx.Rapportini.Add(r);
        await RicordaConsulenteAsync(r.IdOrganicoArcLab, ct);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.crea", EsitoOperazione.Successo,
            "Rapportino", r.IdRapportino.ToString(), new { r.RagioneSociale, r.Importo, r.ModalitaFatturazione }, ct);
        return Json(Completo(r));
    }

    [HttpPut("api/rapportini/{id:int}")]
    public async Task<IActionResult> Modifica(int id, [FromBody] RapportinoDto d, CancellationToken ct)
    {
        var r = await CaricaAsync(id, false, ct);
        if (r is null || !PuoVedere(r)) return NotFound(new { messaggio = "Rapportino non trovato." });
        if (!r.Modificabile)
            return BadRequest(new { messaggio = $"Il rapportino è nello stato «{r.Stato}»: non si può più modificare." });
        // Dopo l'invio, l'importo di un rapportino contestato si cambia con "Correggi importo" (resta traccia).
        var importoPrima = r.Importo;
        var errore = await ApplicaAsync(r, d, ct);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        if (r.Stato == StatoRapportino.Contestato && r.Importo != importoPrima)
        {
            r.ImportoCorretto = r.Importo;
            r.Importo = importoPrima;
            r.MotivoCorrezione ??= "Modificato dopo contestazione";
            r.IdUtenteCorrezione = _utente.IdUtente;
            r.DataCorrezione = DateTime.UtcNow;
        }
        r.DataUltimaModifica = DateTime.UtcNow;
        await RicordaConsulenteAsync(r.IdOrganicoArcLab, ct);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.modifica", EsitoOperazione.Successo,
            "Rapportino", r.IdRapportino.ToString(), new { r.Importo, r.ImportoCorretto }, ct);
        return Json(Completo(r));
    }

    /// <summary>Valida e copia i dati, rileggendo da ArcLab cliente, insediamento, consulente e commessa.</summary>
    private async Task<string?> ApplicaAsync(Rapportino r, RapportinoDto d, CancellationToken ct)
    {
        if (d.IdCliente == 0) return "Scegli il cliente.";
        if (d.IdInsediamento == 0) return "Scegli l'insediamento.";
        if (d.IdOrganico == 0) return "Scegli il consulente.";
        if (d.DataIntervento == default) return "Indica la data dell'intervento.";
        if (d.DataIntervento.Date > DateTime.Today.AddDays(1)) return "La data dell'intervento è nel futuro.";

        var descr = (d.DescrizioneFattura ?? "").Trim();
        if (descr.Length == 0) return "Scrivi la descrizione che andrà in fattura.";
        if (descr.Length > 250) return "La descrizione per la fattura può avere al massimo 250 caratteri (limite di Palomar).";
        if (d.Ore <= 0 || d.Ore > 999) return "Indica le ore (maggiori di zero).";
        if (!Enum.IsDefined(typeof(LuogoPrestazione), d.Luogo)) return "Luogo non valido.";
        if (!Enum.IsDefined(typeof(ModalitaImporto), d.ModalitaImporto)) return "Modalità di importo non valida.";
        if (!Enum.IsDefined(typeof(ModalitaFatturazione), d.ModalitaFatturazione)) return "Modalità di fatturazione non valida.";
        var modFatt = (ModalitaFatturazione)d.ModalitaFatturazione;
        var modImp = (ModalitaImporto)d.ModalitaImporto;

        // Cliente e insediamento (ArcLab)
        var cliente = (await _anagrafiche.ClientiAsync(ct)).FirstOrDefault(c => c.Id == d.IdCliente);
        if (cliente is null) return "Cliente non trovato in ArcLab.";
        var ins = (await _arclab.InsediamentiAsync(d.IdCliente, ct)).FirstOrDefault(i => i.IdInsediamento == d.IdInsediamento);
        if (ins is null) return "L'insediamento non appartiene al cliente scelto.";

        // Consulente (ArcLab Organico in servizio; se già salvato e poi cessato, si mantiene)
        var cons = (await _arclab.ConsulentiAsync(ct)).FirstOrDefault(c => c.IdOrganico == d.IdOrganico);
        if (cons is null && r.IdOrganicoArcLab != d.IdOrganico) return "Consulente non trovato tra il personale in servizio.";

        // Commessa (facoltativa, ma obbligatoria per la convenzione)
        CommessaConsulenza? commessa = null;
        if (d.IdCommessa is int idc && idc != 0)
        {
            commessa = (await _arclab.CommesseConsulenzaAsync(d.IdCliente, ct)).FirstOrDefault(c => c.IdCommessa == idc);
            if (commessa is null) return "La commessa scelta non è una commessa di consulenza del cliente.";
        }
        if (modFatt == ModalitaFatturazione.Convenzione && commessa is null)
            return "Per una convenzione a prezzo stabilito scegli la commessa della convenzione.";

        // Importo
        decimal importo;
        decimal? tariffa = null;
        if (modImp == ModalitaImporto.Orario)
        {
            if (d.TariffaOraria is not decimal t || t <= 0) return "Indica la tariffa oraria.";
            tariffa = t;
            importo = Math.Round(t * d.Ore, 2, MidpointRounding.AwayFromZero);
        }
        else importo = Math.Round(d.Importo ?? 0, 2, MidpointRounding.AwayFromZero);
        if (importo < 0) return "L'importo non può essere negativo.";
        var soloRendicontato = d.Prepagato || modFatt == ModalitaFatturazione.Convenzione;
        if (!soloRendicontato && importo <= 0) return "Indica l'importo da fatturare (senza IVA).";

        // Voce: dalla commessa, oppure scelta dal consulente (obbligatoria se si fattura senza commessa)
        var voce = (d.Voce ?? "").Trim().ToUpperInvariant();
        if (voce.Length == 0 && commessa is not null)
            voce = VoceDa(await FasceVociAsync(ct), commessa.IdTipoIncarico) ?? "";
        if (voce.Length > 20) return "Voce non valida.";
        if (!soloRendicontato && commessa is null && voce.Length == 0) return "Senza commessa scegli la voce di fattura.";

        if (d.Prepagato && d.OreResiduePrima is decimal orp && orp < 0) return "Le ore residue non possono essere negative.";

        var attivita = (d.AttivitaSvolta ?? "").Trim();
        if (attivita.Length > 4000) return "La descrizione dell'attività svolta può avere al massimo 4000 caratteri.";
        var follow = (d.FollowUpAzione ?? "").Trim();
        var resp = (d.FollowUpResponsabile ?? "").Trim();
        if (resp.Length > 0 && resp != "Cliente" && resp != "MARE.A") return "Responsabile del follow-up non valido.";

        // Documentazione di riferimento
        var tipi = await _ctx.DocumentiTipo.AsNoTracking().ToDictionaryAsync(x => x.IdDocumentoTipo, ct);
        var righeDoc = new List<RapportinoDocumento>();
        var ordine = 0;
        foreach (var x in d.Documenti ?? new List<DocumentoDto>())
        {
            DocumentoTipo? tipo = null;
            if (x.IdDocumentoTipo is int idt && !tipi.TryGetValue(idt, out tipo)) return "Documento tipo non trovato.";
            var nome = (x.Nome ?? "").Trim();
            if (nome.Length == 0) nome = tipo?.Nome ?? "";
            if (nome.Length == 0) continue;   // riga vuota: ignorata
            if (nome.Length > 200) return "Nome del documento troppo lungo (max 200 caratteri).";
            if (!Enum.IsDefined(typeof(AzioneDocumento), x.Azione)) return $"Documento «{nome}»: indica se è stato esaminato, modificato o emesso.";
            var azione = (AzioneDocumento)x.Azione;
            ConsegnaDocumento? consegna = null;
            if (azione != AzioneDocumento.Esaminato && x.Consegna is int cg)
            {
                if (!Enum.IsDefined(typeof(ConsegnaDocumento), cg)) return $"Documento «{nome}»: consegna non valida.";
                consegna = (ConsegnaDocumento)cg;
            }
            righeDoc.Add(new RapportinoDocumento
            {
                IdDocumentoTipo = tipo?.IdDocumentoTipo,
                Nome = nome,
                Revisione = Taglia(x.Revisione?.Trim(), 30),
                DataDocumento = x.DataDocumento?.Date,
                Azione = azione,
                Consegna = consegna,
                Ordine = ++ordine,
                RichiamaLaboratorio = tipo?.RichiamaLaboratorio ?? false,
                PianoAnalitico = tipo?.PianoAnalitico ?? false
            });
        }

        // Rif. offerta/contratto: quello scritto, oppure dalla commessa
        var rifOfferta = (d.RiferimentoOfferta ?? "").Trim();
        if (rifOfferta.Length == 0 && commessa is not null) rifOfferta = RiferimentoDaCommessa(commessa) ?? "";

        // Delega operativa: SOLO dal registro per cliente (se c'è la mettiamo, se non c'è no)
        var delega = await DelegaValidaAsync(d.IdCliente, d.IdOrganico, d.IdInsediamento, d.DataIntervento.Date, ct);

        r.IdClienteArcLab = d.IdCliente;
        r.RagioneSociale = Taglia(cliente.Nome.Trim(), 150)!;
        r.IdInsediamentoArcLab = ins.IdInsediamento;
        r.NomeInsediamento = Taglia(ins.Nome, 255);
        r.IndirizzoInsediamento = Taglia(ins.Indirizzo, 400);
        r.EmailInsediamento = Taglia(ins.Email, 255);
        r.DataIntervento = d.DataIntervento.Date;
        if (cons is not null)
        {
            r.IdOrganicoArcLab = cons.IdOrganico;
            r.Consulente = Taglia(cons.Nome, 120)!;
            r.CognomeConsulente = Taglia(cons.CognomeOrg, 50);
            r.CommessaConsulente = Taglia(cons.Commessa, 100);
        }
        r.IdCommessaArcLab = commessa?.IdCommessa;
        r.DescrizioneCommessa = Taglia(commessa?.DescrIncarico?.Trim(), 255);
        r.IdTipoIncarico = commessa?.IdTipoIncarico;
        r.PrezzoCommessa = commessa?.PrezzoControllo;
        r.Voce = voce.Length == 0 ? null : voce;
        r.DescrizioneFattura = descr;
        r.Ore = d.Ore;
        r.Luogo = (LuogoPrestazione)d.Luogo;
        r.ModalitaImporto = modImp;
        r.TariffaOraria = tariffa;
        r.Importo = importo;
        r.ModalitaFatturazione = modFatt;
        r.Prepagato = d.Prepagato;
        r.DataUltimoScalo = d.Prepagato ? d.DataUltimoScalo?.Date : null;
        r.OreResiduePrima = d.Prepagato ? d.OreResiduePrima : null;
        r.NoteInterne = Taglia(d.NoteInterne?.Trim(), 1000);
        r.AttivitaSvolta = attivita.Length == 0 ? null : attivita;
        r.RiferimentoOfferta = Taglia(rifOfferta.Length == 0 ? null : rifOfferta, 300);
        r.RiferimentoDelega = delega is null ? null : Taglia(TestoDelega(delega), 300);
        r.FonteInformazioni = Taglia(string.IsNullOrWhiteSpace(d.FonteInformazioni) ? null : d.FonteInformazioni.Trim(), 1000);
        r.NotaPianoAnalitico = Taglia(string.IsNullOrWhiteSpace(d.NotaPianoAnalitico) ? null : d.NotaPianoAnalitico.Trim(), 1000);
        r.FollowUpAzione = Taglia(follow.Length == 0 ? null : follow, 1000);
        r.FollowUpResponsabile = follow.Length == 0 || resp.Length == 0 ? null : resp;
        r.FollowUpTermine = follow.Length == 0 ? null : Taglia(string.IsNullOrWhiteSpace(d.FollowUpTermine) ? null : d.FollowUpTermine.Trim(), 100);
        r.VerificaRichiamiLaboratorio = d.VerificaRichiamiLaboratorio;

        // Righe documenti: le precedenti si disattivano (nessuna cancellazione fisica), le nuove si aggiungono.
        foreach (var vecchia in r.Documenti.Where(x => x.Attivo)) vecchia.Attivo = false;
        r.Documenti.AddRange(righeDoc);
        return null;
    }

    private async Task RicordaConsulenteAsync(int idOrganico, CancellationToken ct)
    {
        if (_utente.IdUtente is not int idu || idOrganico == 0) return;
        var m = await _ctx.ConsulentiUtenti.FirstOrDefaultAsync(x => x.IdUtente == idu, ct);
        if (m is null) _ctx.ConsulentiUtenti.Add(new ConsulenteUtente { IdUtente = idu, IdOrganicoArcLab = idOrganico, DataUltimaModifica = DateTime.UtcNow });
        else if (m.IdOrganicoArcLab != idOrganico) { m.IdOrganicoArcLab = idOrganico; m.DataUltimaModifica = DateTime.UtcNow; }
    }

    // ═══════════════════════════════════════════════════════════════
    // PDF e invio
    // ═══════════════════════════════════════════════════════════════

    [HttpGet("api/rapportini/{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var r = await CaricaAsync(id, true, ct);
        if (r is null || !PuoVedere(r)) return NotFound(new { messaggio = "Rapportino non trovato." });
        var pdf = _generatore.GeneraPdf(r, await TestiAsync(ct),
            r.DataInvio is DateTime di ? DateTime.SpecifyKind(di, DateTimeKind.Utc).ToLocalTime() : null, _ambiente.WebRootPath);
        Response.Headers.ContentDisposition = $"inline; filename=\"{_generatore.NomeFile(r)}\"";
        return File(pdf, "application/pdf");
    }

    /// <summary>
    /// Genera il PDF, lo archivia e lo copia nella cartella di prelievo Palomar (con verifica), poi segna
    /// il rapportino come Inviato. Se la copia in Palomar non riesce il rapportino NON cambia stato.
    /// Ammesso da Bozza e da Contestato (reinvio dopo la correzione).
    /// </summary>
    [HttpPost("api/rapportini/{id:int}/invia")]
    public async Task<IActionResult> Invia(int id, CancellationToken ct)
    {
        var r = await CaricaAsync(id, false, ct);
        if (r is null || !PuoVedere(r)) return NotFound(new { messaggio = "Rapportino non trovato." });
        if (!r.Modificabile) return BadRequest(new { messaggio = $"Il rapportino è nello stato «{r.Stato}»: non si può inviare." });
        if (string.IsNullOrWhiteSpace(r.EmailInsediamento))
            return BadRequest(new { messaggio = "L'insediamento non ha un indirizzo email in ArcLab: il rapportino non può essere recapitato. Aggiungere l'email in ArcLab e riaprire il rapportino." });
        // Nota d'uso 2: descrizione sufficiente a ricostruire cosa è stato fatto.
        if ((r.AttivitaSvolta ?? "").Trim().Length < MinimoAttivita)
            return BadRequest(new { messaggio = $"Descrivi l'attività svolta in modo puntuale (almeno {MinimoAttivita} caratteri): documenti esaminati, modifiche proposte, eventuali non conformità." });
        // Nota d'uso 3: documenti che richiamano il Laboratorio, modificati o emessi.
        if (r.Documenti.Any(x => x.Attivo && x.RichiamaLaboratorio && x.Azione != AzioneDocumento.Esaminato) && !r.VerificaRichiamiLaboratorio)
            return BadRequest(new { messaggio = "Hai modificato o emesso un documento che può richiamare il Laboratorio: spunta la verifica che non gli attribuisce attività o obblighi esclusi dalle sue Condizioni." });

        var idEsecuzione = ServizioRegistroDocumenti.NuovaEsecuzione();
        var nome = _generatore.NomeFile(r);
        string? archivio = null;
        try
        {
            var dataConsegna = DateTime.Now;
            var pdf = _generatore.GeneraPdf(r, await TestiAsync(ct), dataConsegna, _ambiente.WebRootPath);

            var baseDir = await ConfigAsync(ChiaveCartellaOutput, ct);
            if (string.IsNullOrWhiteSpace(baseDir)) baseDir = Path.Combine(_ambiente.ContentRootPath, "App_Data", "Rapportini");
            var dir = Path.Combine(baseDir, r.DataIntervento.ToString("yyyy"), r.DataIntervento.ToString("MM"));
            Directory.CreateDirectory(dir);
            archivio = Path.Combine(dir, nome);
            await System.IO.File.WriteAllBytesAsync(archivio, pdf, ct);

            var (ok, filePalomar, errore) = ServizioRegistroDocumenti.ScriviVerificato(await ConfigAsync(ChiaveCartellaPalomar, ct), nome, pdf);
            await _registro.RegistraAsync(new RegistroDocumento
            {
                Ambito = ServizioRegistroDocumenti.AmbitoRapportino, IdEsecuzione = idEsecuzione, Origine = "invio",
                Riferimento = $"Rapportino {r.IdRapportino} — {r.RagioneSociale}",
                Esito = ok ? EsitoDocumento.Ok : EsitoDocumento.Parziale,
                Messaggio = ok ? "Consegnato a Palomar." : "NON consegnato a Palomar: " + errore,
                FileArchivio = archivio, FilePalomar = filePalomar, PalomarVerificato = ok
            }, ct);
            if (!ok)
            {
                await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoRapportino, idEsecuzione, ct);
                return BadRequest(new { messaggio = "Rapportino NON inviato: " + errore });
            }

            r.Stato = StatoRapportino.Inviato;
            r.DataInvio = dataConsegna.ToUniversalTime();
            r.NomeFilePdf = nome;
            r.PercorsoPdf = archivio;
            r.FilePalomar = filePalomar;
            r.DataUltimaModifica = DateTime.UtcNow;
            await _ctx.SaveChangesAsync(ct);
            await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.invia", EsitoOperazione.Successo,
                "Rapportino", r.IdRapportino.ToString(), new { nome, filePalomar }, ct);
            return Json(new { messaggio = "Rapportino inviato (consegnato a Palomar).", rapportino = Riepilogo(r) });
        }
        catch (Exception ex)
        {
            _ctx.ChangeTracker.Clear();
            await _registro.RegistraAsync(new RegistroDocumento
            {
                Ambito = ServizioRegistroDocumenti.AmbitoRapportino, IdEsecuzione = idEsecuzione, Origine = "invio",
                Riferimento = $"Rapportino {id}", Esito = EsitoDocumento.Errore,
                Messaggio = "Errore: " + ex.Message, FileArchivio = archivio
            }, ct);
            await _registro.NotificaProblemiAsync(ServizioRegistroDocumenti.AmbitoRapportino, idEsecuzione, ct);
            return BadRequest(new { messaggio = "Invio non riuscito: " + ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Gestione (contestazione, correzione, conferma, annullamento)
    // ═══════════════════════════════════════════════════════════════

    public record NotaDto(string? Nota);
    public record CorrezioneDto(decimal Importo, string? Motivo);
    public record IdsDto(int[]? Ids);

    [HttpPost("api/rapportini/{id:int}/contesta")]
    [RichiedePermesso(Permessi.RapportiniGestione)]
    public async Task<IActionResult> Contesta(int id, [FromBody] NotaDto d, CancellationToken ct)
    {
        var r = await _ctx.Rapportini.FirstOrDefaultAsync(x => x.IdRapportino == id, ct);
        if (r is null) return NotFound(new { messaggio = "Rapportino non trovato." });
        if (r.Stato is not (StatoRapportino.Inviato or StatoRapportino.Confermato or StatoRapportino.Rendicontato))
            return BadRequest(new { messaggio = $"Un rapportino «{r.Stato}» non si può contestare." });
        var nota = (d.Nota ?? "").Trim();
        if (nota.Length == 0) return BadRequest(new { messaggio = "Scrivi cosa ha chiesto il cliente." });
        r.Stato = StatoRapportino.Contestato;
        r.DataContestazione = DateTime.UtcNow;
        r.NotaContestazione = Taglia(nota, 1000);
        r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.contesta", EsitoOperazione.Successo,
            "Rapportino", id.ToString(), new { nota }, ct);
        return Json(Riepilogo(r));
    }

    [HttpPost("api/rapportini/{id:int}/correggi")]
    [RichiedePermesso(Permessi.RapportiniGestione)]
    public async Task<IActionResult> Correggi(int id, [FromBody] CorrezioneDto d, CancellationToken ct)
    {
        var r = await _ctx.Rapportini.FirstOrDefaultAsync(x => x.IdRapportino == id, ct);
        if (r is null) return NotFound(new { messaggio = "Rapportino non trovato." });
        if (r.Stato is not (StatoRapportino.Contestato or StatoRapportino.Inviato))
            return BadRequest(new { messaggio = $"Un rapportino «{r.Stato}» non si può correggere." });
        if (d.Importo < 0) return BadRequest(new { messaggio = "Importo non valido." });
        var motivo = (d.Motivo ?? "").Trim();
        if (motivo.Length == 0) return BadRequest(new { messaggio = "Indica il motivo della correzione." });
        var prima = r.ImportoDaFatturare;
        r.ImportoCorretto = Math.Round(d.Importo, 2, MidpointRounding.AwayFromZero);
        r.MotivoCorrezione = Taglia(motivo, 500);
        r.IdUtenteCorrezione = _utente.IdUtente;
        r.DataCorrezione = DateTime.UtcNow;
        r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.correggi", EsitoOperazione.Successo,
            "Rapportino", id.ToString(), new { Prima = prima, Dopo = r.ImportoCorretto, motivo }, ct);
        return Json(Riepilogo(r));
    }

    /// <summary>Conferma manuale (fase 1). I rapportini in convenzione o prepagati diventano "Rendicontato".</summary>
    [HttpPost("api/rapportini/conferma")]
    [RichiedePermesso(Permessi.RapportiniGestione)]
    public async Task<IActionResult> Conferma([FromBody] IdsDto d, CancellationToken ct)
    {
        var ids = (d.Ids ?? Array.Empty<int>()).Distinct().ToList();
        if (ids.Count == 0) return BadRequest(new { messaggio = "Nessun rapportino selezionato." });
        var righe = await _ctx.Rapportini.Where(x => ids.Contains(x.IdRapportino)).ToListAsync(ct);
        int confermati = 0, rendicontati = 0;
        var scartati = new List<string>();
        foreach (var r in righe)
        {
            if (r.Stato is not (StatoRapportino.Inviato or StatoRapportino.Contestato))
            {
                scartati.Add($"n. {r.IdRapportino}: stato «{r.Stato}»");
                continue;
            }
            r.Stato = r.SoloRendicontato ? StatoRapportino.Rendicontato : StatoRapportino.Confermato;
            r.DataConferma = DateTime.UtcNow;
            r.IdUtenteConferma = _utente.IdUtente;
            r.DataUltimaModifica = DateTime.UtcNow;
            if (r.SoloRendicontato) rendicontati++; else confermati++;
        }
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.conferma", EsitoOperazione.Successo,
            "Rapportino", string.Join(",", ids.Take(50)), new { confermati, rendicontati, scartati }, ct);
        var msg = $"{confermati} confermati per la fatturazione, {rendicontati} solo rendicontati";
        if (scartati.Count > 0) msg += $"; {scartati.Count} non confermabili ({string.Join("; ", scartati.Take(5))})";
        return Json(new { messaggio = msg + "." });
    }

    [HttpPost("api/rapportini/{id:int}/annulla")]
    public async Task<IActionResult> Annulla(int id, [FromBody] NotaDto d, CancellationToken ct)
    {
        var r = await _ctx.Rapportini.FirstOrDefaultAsync(x => x.IdRapportino == id, ct);
        if (r is null || !PuoVedere(r)) return NotFound(new { messaggio = "Rapportino non trovato." });
        if (r.Stato is StatoRapportino.InPrefatturato or StatoRapportino.Fatturato or StatoRapportino.Annullato)
            return BadRequest(new { messaggio = $"Un rapportino «{r.Stato}» non si può annullare." });
        if (r.Stato != StatoRapportino.Bozza && !Gestore)
            return BadRequest(new { messaggio = "Un rapportino già inviato può essere annullato solo da chi gestisce i rapportini." });
        var motivo = (d.Nota ?? "").Trim();
        if (motivo.Length == 0) return BadRequest(new { messaggio = "Indica il motivo dell'annullamento." });
        r.Stato = StatoRapportino.Annullato;
        r.DataAnnullamento = DateTime.UtcNow;
        r.MotivoAnnullamento = Taglia(motivo, 500);
        r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Consulenze, "rapportino.annulla", EsitoOperazione.Successo,
            "Rapportino", id.ToString(), new { motivo }, ct);
        return Json(Riepilogo(r));
    }

    /// <summary>Registro di controllo degli invii (consegne a Palomar).</summary>
    [HttpGet("api/rapportini/registro")]
    public async Task<IActionResult> Registro([FromQuery] int? giorni, [FromQuery] bool soloProblemi, CancellationToken ct)
    {
        var righe = await _registro.ElencoAsync(ServizioRegistroDocumenti.AmbitoRapportino, giorni ?? 30, soloProblemi, ct);
        return Json(righe.Select(r => new
        {
            r.IdRegistroDocumento, r.IdEsecuzione, DataOra = DateTime.SpecifyKind(r.DataOra, DateTimeKind.Utc),
            r.Origine, r.Riferimento, Esito = r.Esito.ToString(), r.Messaggio, r.FilePalomar, r.PalomarVerificato,
            StatoAttuale = (string?)null
        }));
    }

    // ── Helper ──
    /// <summary>Lunghezza minima della descrizione dell'attività svolta per poter inviare (nota d'uso 2).</summary>
    private const int MinimoAttivita = 80;

    private static string? RiferimentoDaCommessa(CommessaConsulenza c)
    {
        var n = c.NContratto?.Trim();
        if (string.IsNullOrWhiteSpace(n)) return null;
        return $"Contratto n. {n}" + (c.DataCommessa is DateTime dc ? $" del {dc:dd/MM/yyyy}" : "");
    }

    /// <summary>Delega in essere alla data per il cliente E il consulente (la delega nomina un consulente preciso).</summary>
    private async Task<DelegaCliente?> DelegaValidaAsync(int idCliente, int idOrganico, int? idInsediamento, DateTime data, CancellationToken ct)
    {
        var deleghe = await _ctx.DelegheClienti.AsNoTracking().Include(x => x.Insediamenti)
            .Where(x => x.IdClienteArcLab == idCliente && x.IdOrganicoArcLab == idOrganico && x.Attivo).ToListAsync(ct);
        // Per la sede del rapportino: la delega deve valere per tutte le sedi o per quella sede.
        return deleghe.Where(x => x.ValidaAl(data) && (idInsediamento is not int ii || ii == 0 || x.ValePerInsediamento(ii)))
            .OrderByDescending(x => x.DataSottoscrizione).FirstOrDefault();
    }

    private static string TestoDelega(DelegaCliente d) =>
        $"{d.Estremi.Trim()} sottoscritta il {d.DataSottoscrizione:dd/MM/yyyy}";

    /// <summary>Testi fissi del rapporto: Impostazioni documenti, con i predefiniti del modello approvato.</summary>
    private async Task<TestiRapportino> TestiAsync(CancellationToken ct)
    {
        var p = TestiRapportino.Predefiniti;
        var cfg = await _ctx.Configurazioni.AsNoTracking().Where(x => x.Chiave.StartsWith("Rapportini.Testo"))
            .ToDictionaryAsync(x => x.Chiave, x => x.Valore, ct);
        string T(string chiave, string predefinito) =>
            cfg.TryGetValue(chiave, out var v) && !string.IsNullOrWhiteSpace(v) ? v!.Trim() : predefinito;
        return new TestiRapportino(
            T("Rapportini.TestoFonteInformazioni", p.FonteInformazioni),
            T("Rapportini.TestoPianoAnalitico", p.PianoAnalitico),
            T("Rapportini.TestoModalitaConsegna", p.ModalitaConsegna),
            T("Rapportini.TestoResponsabilita", p.Responsabilita),
            T("Rapportini.TestoNonCertificazione", p.NonCertificazione),
            T("Rapportini.TestoDecisioniCliente", p.DecisioniCliente),
            T("Rapportini.TestoAnalisiLaboratorio", p.AnalisiLaboratorio),
            T("Rapportini.TestoOsservazioni", p.Osservazioni),
            T("Rapportini.TestoRendicontazione", p.Rendicontazione),
            T("Rapportini.TestoCondizioni", p.Condizioni));
    }
    private async Task<string> ConfigAsync(string chiave, CancellationToken ct)
    {
        var c = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == chiave, ct);
        return c?.Valore?.Trim() ?? "";
    }

    private Task<List<VoceIncarico>> FasceVociAsync(CancellationToken ct) =>
        _ctx.VociIncarico.AsNoTracking().Where(v => v.Attivo).ToListAsync(ct);

    private static string? VoceDa(IEnumerable<VoceIncarico> fasce, int? idTipoIncarico)
    {
        if (idTipoIncarico is not int t) return null;
        var v = fasce.FirstOrDefault(f => t >= f.IdTipoIncaricoDa && t <= f.IdTipoIncaricoA)?.Voce;
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    private static string? Taglia(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
