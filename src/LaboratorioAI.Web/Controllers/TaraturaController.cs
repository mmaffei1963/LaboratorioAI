using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Taratura termometri: anagrafica campioni di riferimento (CDR), esecuzione
/// tarature (misure + calcolo GUM), rapporto e registro. Scrittura solo sul
/// database proprio; cliente letto dall'anagrafica ArcLab (sola lettura).
/// </summary>
[RichiedePermesso(Permessi.TaraturaEsegui)]
public class TaraturaController : Controller
{
    private const string MimePdf = "application/pdf";
    private const string MimeRtf = "application/rtf";
    private const string MimeXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Mesi oltre i quali la validazione dei calcoli è considerata scaduta.</summary>
    private const int MesiValiditaValidazione = 12;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioTaratura _calcolo;
    private readonly IServizioValidazioneTaratura _validazione;
    private readonly IServizioControlliTaratura _controlli;
    private readonly IGeneratoreRapportoTaratura _generatore;
    private readonly IGeneratoreRapportoValidazione _generatoreValidazione;
    private readonly IGeneratoreEtichetteTermometri _etichette;
    private readonly IClientAnthropic _ia;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IServizioAudit _audit;

    public TaraturaController(
        ContestoLaboratorioAI ctx, IServizioTaratura calcolo, IServizioValidazioneTaratura validazione,
        IServizioControlliTaratura controlli, IGeneratoreRapportoTaratura generatore,
        IGeneratoreRapportoValidazione generatoreValidazione, IGeneratoreEtichetteTermometri etichette,
        IClientAnthropic ia, IRepositoryAnagrafiche anagrafiche, IContestoUtente utente, IServizioVersione versione,
        IServizioAudit audit)
    {
        _ctx = ctx; _calcolo = calcolo; _validazione = validazione; _controlli = controlli;
        _generatore = generatore; _generatoreValidazione = generatoreValidazione; _etichette = etichette; _ia = ia;
        _anagrafiche = anagrafiche; _utente = utente; _versione = versione; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Taratura termometri";
        return View();
    }

    // ═══════════════ Campioni di riferimento (CDR) ═════════════════
    [HttpGet("api/taratura/cdr/elenco")]
    public async Task<IActionResult> CdrElenco(CancellationToken ct)
    {
        var righe = await _ctx.CampioniRiferimento.AsNoTracking().Where(c => c.Attivo)
            .OrderBy(c => c.Identificazione)
            .Select(c => new
            {
                c.IdCampione, c.Identificazione, c.CodiceInterno, c.CentroLat, c.NumeroCertificato,
                DataScadenza = c.DataScadenza.HasValue ? c.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduto = c.DataScadenza.HasValue && c.DataScadenza.Value < DateTime.Today
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/taratura/cdr/{id:int}")]
    public async Task<IActionResult> CdrDettaglio(int id, CancellationToken ct)
    {
        var c = await _ctx.CampioniRiferimento.AsNoTracking().Include(x => x.Punti)
            .FirstOrDefaultAsync(x => x.IdCampione == id, ct);
        if (c is null) return NotFound(new { messaggio = "CDR non trovato." });
        return Json(new
        {
            c.IdCampione, c.Identificazione, c.TipoTermometro, c.CodiceInterno, c.CentroLat, c.NumeroCertificato,
            DataTaratura = c.DataTaratura, DataScadenza = c.DataScadenza,
            c.Risoluzione, c.FattoreCoperturaK, c.IncertezzaInstabilita, c.IncertezzaEstesaCertificato, c.Note,
            Punti = c.Punti.Where(p => !p.Secondario).OrderBy(p => p.Ordine).Select(p => new
            {
                p.TIndicata, p.TRiferimento, p.Differenza, p.IncertezzaEstesa, p.IncertezzaTipo
            }),
            PuntiSecondari = c.Punti.Where(p => p.Secondario).OrderBy(p => p.Ordine).Select(p => new
            {
                p.TIndicata, p.TRiferimento, p.Differenza, p.IncertezzaEstesa, p.IncertezzaTipo
            })
        });
    }

    [HttpPost("api/taratura/cdr/salva")]
    public async Task<IActionResult> CdrSalva([FromBody] RichiestaSalvaCdr req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Identificazione))
            return BadRequest(new { messaggio = "L'identificazione del CDR è obbligatoria." });
        if (req.Punti is not { Count: >= 2 })
            return BadRequest(new { messaggio = "Servono almeno due punti di certificato." });

        var ora = DateTime.UtcNow;
        CampioneRiferimento c;
        if (req.Id is int id)
        {
            c = await _ctx.CampioniRiferimento.Include(x => x.Punti).FirstOrDefaultAsync(x => x.IdCampione == id, ct)
                ?? throw new InvalidOperationException("CDR non trovato.");
            _ctx.PuntiCertificato.RemoveRange(c.Punti);
            c.Punti.Clear();
            c.DataUltimaModifica = ora;
        }
        else { c = new CampioneRiferimento { DataCreazione = ora, DataUltimaModifica = ora }; _ctx.CampioniRiferimento.Add(c); }

        c.Identificazione = req.Identificazione.Trim();
        c.TipoTermometro = Vuoto(req.TipoTermometro);
        c.CodiceInterno = Vuoto(req.CodiceInterno);
        c.CentroLat = Vuoto(req.CentroLat);
        c.NumeroCertificato = Vuoto(req.NumeroCertificato);
        c.DataTaratura = req.DataTaratura;
        c.DataScadenza = req.DataScadenza;
        c.Risoluzione = req.Risoluzione;
        c.FattoreCoperturaK = req.FattoreCoperturaK <= 0 ? 2.0 : req.FattoreCoperturaK;
        c.IncertezzaInstabilita = req.IncertezzaInstabilita < 0 ? 0 : req.IncertezzaInstabilita;
        c.IncertezzaEstesaCertificato = req.IncertezzaEstesaCertificato < 0 ? 0 : req.IncertezzaEstesaCertificato;
        c.Note = Vuoto(req.Note);

        int ordine = 1;
        foreach (var p in req.Punti)
            c.Punti.Add(new PuntoCertificato
            {
                TIndicata = p.TIndicata, TRiferimento = p.TRiferimento, Differenza = p.Differenza,
                IncertezzaEstesa = p.IncertezzaEstesa, IncertezzaTipo = p.IncertezzaTipo, Ordine = ordine++
            });

        // Taratura secondaria/interna del CDR (opzionale), per la deriva u(stCR).
        int ordineSec = 1;
        foreach (var p in req.PuntiSecondari ?? new List<PuntoCertDto>())
            c.Punti.Add(new PuntoCertificato
            {
                TIndicata = p.TIndicata, TRiferimento = p.TRiferimento, Differenza = p.Differenza,
                IncertezzaEstesa = p.IncertezzaEstesa, IncertezzaTipo = p.IncertezzaTipo,
                Ordine = ordineSec++, Secondario = true
            });

        await _ctx.SaveChangesAsync(ct);
        return Json(new { c.IdCampione });
    }

    // ═══════════════ Tarature ══════════════════════════════════════
    [HttpGet("api/taratura/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.Tarature.AsNoTracking().Where(t => t.Attivo)
            .OrderByDescending(t => t.DataTaratura)
            .Take(500)
            .Select(t => new
            {
                t.IdTaratura, t.NumeroRapporto, t.StrumentoIdentificazione, t.StrumentoCodice, t.Committente,
                DataTaratura = t.DataTaratura.ToString("dd/MM/yyyy"),
                DataScadenza = t.DataScadenza.HasValue ? t.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduta = t.DataScadenza.HasValue && t.DataScadenza.Value < DateTime.Today,
                InScadenza = t.DataScadenza.HasValue && t.DataScadenza.Value >= DateTime.Today
                    && t.DataScadenza.Value < DateTime.Today.AddDays(30)
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/taratura/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var t = await _ctx.Tarature.AsNoTracking().FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });
        var misure = JsonSerializer.Deserialize<List<RigaMisuraDto>>(t.DatiMisure, JsonOpts) ?? new();
        return Json(new
        {
            t.IdTaratura, t.IdCampioneRiferimento, t.StrumentoIdentificazione, t.StrumentoCodice,
            t.RisoluzioneStrumento, t.FattoreCopertura, t.IdClienteArcLab, t.Committente,
            t.NumeroRapporto, DataTaratura = t.DataTaratura, DataScadenza = t.DataScadenza,
            t.RiferimentoIstruzione, t.Responsabile, t.Note, Misure = misure
        });
    }

    [HttpGet("api/taratura/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Json((await _anagrafiche.ClientiAsync(ct)).Select(c => new { c.Id, c.Nome }));

    [HttpGet("api/taratura/strumenti")]
    public async Task<IActionResult> Strumenti(CancellationToken ct) =>
        Json((await _anagrafiche.StrumentiInterniAsync(ct)).Select(s => new { s.Id, s.Nome }));

    [HttpPost("api/taratura/calcola")]
    public async Task<IActionResult> Calcola([FromBody] RichiestaCalcoloTaratura req, CancellationToken ct)
    {
        var (dati, _, errore) = await ComponiAsync(req.IdCampioneRiferimento, req.RisoluzioneStrumento, req.FattoreCopertura, req.Misure, ct);
        if (errore is not null) return errore;
        return Json(_calcolo.Calcola(dati!));
    }

    [HttpPost("api/taratura/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaTaratura req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.StrumentoIdentificazione))
            return BadRequest(new { messaggio = "L'identificazione dello strumento è obbligatoria." });
        if (req.IdCampioneRiferimento <= 0)
            return BadRequest(new { messaggio = "Seleziona il campione di riferimento (CDR)." });

        var ora = DateTime.UtcNow;
        Taratura t;
        if (req.Id is int id)
        {
            t = await _ctx.Tarature.FirstOrDefaultAsync(x => x.IdTaratura == id, ct)
                ?? throw new InvalidOperationException("Taratura non trovata.");
            t.DataUltimaModifica = ora;
        }
        else { t = new Taratura { DataCreazione = ora, DataUltimaModifica = ora }; _ctx.Tarature.Add(t); }

        t.IdCampioneRiferimento = req.IdCampioneRiferimento;
        t.StrumentoIdentificazione = req.StrumentoIdentificazione.Trim();
        t.StrumentoCodice = Vuoto(req.StrumentoCodice);
        t.RisoluzioneStrumento = req.RisoluzioneStrumento;
        t.FattoreCopertura = req.FattoreCopertura <= 0 ? 2.0 : req.FattoreCopertura;
        t.IdClienteArcLab = req.IdClienteArcLab;
        t.Committente = Vuoto(req.Committente);
        t.DataTaratura = req.DataTaratura.ToDateTime(TimeOnly.MinValue);
        t.DataScadenza = req.DataScadenza?.ToDateTime(TimeOnly.MinValue) ?? req.DataTaratura.AddYears(1).ToDateTime(TimeOnly.MinValue);
        t.RiferimentoIstruzione = Vuoto(req.RiferimentoIstruzione);
        t.Responsabile = Vuoto(req.Responsabile);
        t.Note = Vuoto(req.Note);
        t.DatiMisure = JsonSerializer.Serialize(req.Misure ?? new(), JsonOpts);
        t.IdUtente = _utente.IdUtente;
        t.VersioneApplicazione = _versione.Versione;
        if (string.IsNullOrWhiteSpace(t.NumeroRapporto))
            t.NumeroRapporto = string.IsNullOrWhiteSpace(req.NumeroRapporto) ? await NumeroAsync(req.DataTaratura, ct) : req.NumeroRapporto.Trim();

        await _ctx.SaveChangesAsync(ct);

        // Taratura di un termometro CLIENTE (esterno): compare anche nell'elenco esterni dello scadenziario,
        // già segnata «da fatturare». Le tarature di strumenti interni (senza committente) non vi finiscono.
        bool esterna = t.IdClienteArcLab.HasValue || !string.IsNullOrWhiteSpace(t.Committente);
        if (esterna)
        {
            var te = await _ctx.TermometriEsterni.FirstOrDefaultAsync(x => x.IdTaraturaOrigine == t.IdTaratura, ct);
            bool nuovo = te is null;
            if (nuovo)
            {
                te = new TermometroEsterno { IdTaraturaOrigine = t.IdTaratura, DataCreazione = ora, Fatturato = false };
                _ctx.TermometriEsterni.Add(te);
            }
            te!.NumeroRapporto = int.TryParse(t.NumeroRapporto, out var nr) ? nr : te.NumeroRapporto;
            te.Tipologia = t.StrumentoIdentificazione;
            te.DataTaratura = t.DataTaratura;
            te.DataScadenza = t.DataScadenza;
            te.Cliente = t.Committente;
            te.DataUltimaModifica = ora;
            te.IdUtente = _utente.IdUtente;
            await _ctx.SaveChangesAsync(ct);
        }

        await _audit.RegistraAsync(
            AreaFunzionale.Taratura, "taratura.salvata", EsitoOperazione.Successo,
            tipoEntita: "Taratura", idEntita: t.IdTaratura.ToString(),
            dettaglio: new { t.StrumentoIdentificazione, t.NumeroRapporto, Esterna = esterna }, ct: ct);
        return Json(new { t.IdTaratura, t.NumeroRapporto });
    }

    [HttpPost("api/taratura/{id:int}/rapporto")]
    public async Task<IActionResult> Rapporto(int id, [FromBody] RichiestaRapportoTaratura? req, CancellationToken ct)
    {
        var t = await _ctx.Tarature.FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });

        var misure = JsonSerializer.Deserialize<List<RigaMisuraDto>>(t.DatiMisure, JsonOpts) ?? new();
        var (dati, cdr, errore) = await ComponiAsync(t.IdCampioneRiferimento, t.RisoluzioneStrumento, t.FattoreCopertura, misure, ct);
        if (errore is not null) return errore;

        var risultato = _calcolo.Calcola(dati!);

        // Priorità: responsabile indicato al momento; poi quello salvato sulla
        // taratura; infine l'utente corrente (escluso l'utente di sviluppo).
        var redattoDa =
            !string.IsNullOrWhiteSpace(req?.Responsabile) ? req!.Responsabile!.Trim()
            : !string.IsNullOrWhiteSpace(t.Responsabile)  ? t.Responsabile!
            : _utente.NomeCompleto == "Utente di sviluppo" ? ""
            : _utente.NomeCompleto;

        // Se indicato ora e diverso da quello salvato, aggiorna la taratura.
        if (!string.IsNullOrWhiteSpace(req?.Responsabile) &&
            !string.Equals(req!.Responsabile!.Trim(), t.Responsabile, StringComparison.Ordinal))
        {
            t.Responsabile = req.Responsabile!.Trim();
            t.DataUltimaModifica = DateTime.UtcNow;
            await _ctx.SaveChangesAsync(ct);
        }

        var datiRapporto = new DatiRapportoTaratura
        {
            NumeroRapporto = t.NumeroRapporto ?? "",
            DataTaratura = DateOnly.FromDateTime(t.DataTaratura),
            DataScadenza = t.DataScadenza is DateTime ds ? DateOnly.FromDateTime(ds) : null,
            RiferimentoIstruzione = t.RiferimentoIstruzione,
            RedattoDa = redattoDa,
            Committente = t.Committente,
            StrumentoIdentificazione = t.StrumentoIdentificazione,
            StrumentoCodice = t.StrumentoCodice,
            RisoluzioneStrumento = t.RisoluzioneStrumento,
            CdrIdentificazione = cdr!.Identificazione,
            CdrTipo = cdr.TipoTermometro,
            CdrCentroLat = cdr.CentroLat,
            CdrNumeroCertificato = cdr.NumeroCertificato,
            CdrDataTaratura = cdr.DataTaratura is DateTime cdt ? DateOnly.FromDateTime(cdt) : null,
            CdrDataScadenza = cdr.DataScadenza is DateTime cds ? DateOnly.FromDateTime(cds) : null,
            CdrRisoluzione = cdr.Risoluzione,
            CdrFattoreCopertura = cdr.FattoreCoperturaK,
            Certificato = dati!.Certificato,
            Risultato = risultato
        };

        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatore.GeneraWord(datiRapporto) : _generatore.GeneraPdf(datiRapporto);
        var estensione = word ? "rtf" : "pdf";
        var mime = word ? MimeRtf : MimePdf;
        return File(contenuto, mime, _generatore.NomeFile(datiRapporto, estensione));
    }

    // ── Etichetta termometro (Excel, formato foglio 2020, colore per anno) ──
    [HttpGet("api/taratura/{id:int}/etichetta")]
    public async Task<IActionResult> Etichetta(int id, CancellationToken ct)
    {
        var t = await _ctx.Tarature.AsNoTracking().FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });

        var riga = new LaboratorioAI.Core.Modelli.RigaEtichetta
        {
            Codice = string.IsNullOrWhiteSpace(t.StrumentoCodice) ? t.StrumentoIdentificazione : t.StrumentoCodice!,
            RigaRapportoEtichetta = "N°Rapporto taratura:",
            RigaRapportoValore = t.NumeroRapporto ?? "",
            DataTaratura = t.DataTaratura,
            Scadenza = t.DataScadenza,
            AnnoColore = t.DataTaratura.Year
        };
        var bytes = _etichette.Genera(new[] { riga });
        var nome = new string((t.StrumentoCodice ?? t.StrumentoIdentificazione ?? "termometro")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return File(bytes, MimeXlsx, $"Etichetta_{nome}.xlsx");
    }

    // ═══════════════ Validazione del motore di calcolo ═════════════
    [HttpGet("api/taratura/validazione/stato")]
    [RichiedePermesso(Permessi.TaraturaValidazione)]
    public async Task<IActionResult> ValidazioneStato(CancellationToken ct)
    {
        var ultima = await _ctx.ValidazioniCalcolo.AsNoTracking()
            .OrderByDescending(v => v.DataEsecuzione).FirstOrDefaultAsync(ct);
        var scadenza = ultima?.DataEsecuzione.AddMonths(MesiValiditaValidazione);
        return Json(new
        {
            eseguitaMai = ultima is not null,
            idEsito = ultima?.IdEsito,
            validato = ultima?.Validato,
            data = ultima?.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            operatore = ultima?.Operatore,
            scaduta = ultima is null || scadenza < DateTime.UtcNow,
            mesiValidita = MesiValiditaValidazione,
            versioneSet = SetRiferimentoTaratura.Versione
        });
    }

    [HttpPost("api/taratura/validazione/esegui")]
    [RichiedePermesso(Permessi.TaraturaValidazione)]
    public async Task<IActionResult> ValidazioneEsegui(CancellationToken ct)
    {
        var esito = _validazione.Esegui(_versione.Versione);
        var operatore = _utente.NomeCompleto == "Utente di sviluppo" ? null : _utente.NomeCompleto;

        var riga = new EsitoValidazioneTaratura
        {
            DataEsecuzione = esito.DataEsecuzione,
            Validato = esito.Validato,
            NumeroOk = esito.NumeroOk,
            NumeroKo = esito.NumeroKo,
            Tolleranza = esito.Tolleranza,
            VersioneSet = esito.VersioneSet,
            VersioneApplicazione = esito.VersioneApplicazione,
            Dettaglio = JsonSerializer.Serialize(esito, JsonOpts),
            IdUtente = _utente.IdUtente,
            Operatore = operatore
        };
        _ctx.ValidazioniCalcolo.Add(riga);
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Taratura, "taratura.validazione.eseguita",
            esito.Validato ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            tipoEntita: "EsitoValidazioneTaratura", idEntita: riga.IdEsito.ToString(),
            dettaglio: new { esito.Validato, esito.NumeroOk, esito.NumeroKo, esito.VersioneSet }, ct: ct);
        return Json(new
        {
            riga.IdEsito,
            esito.Validato,
            esito.NumeroOk,
            esito.NumeroKo,
            data = esito.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            casi = esito.Casi.Select(c => new { c.Nome, c.Descrizione, c.Ok, c.Ko, c.Validato }),
            dettagli = esito.Dettagli.Select(d => new { d.Caso, d.Punto, d.Parametro, d.Calcolato, d.Atteso, d.Scarto, d.Ok })
        });
    }

    [HttpGet("api/taratura/validazione/elenco")]
    [RichiedePermesso(Permessi.TaraturaValidazione)]
    public async Task<IActionResult> ValidazioneElenco(CancellationToken ct)
    {
        var righe = await _ctx.ValidazioniCalcolo.AsNoTracking()
            .OrderByDescending(v => v.DataEsecuzione).Take(50)
            .Select(v => new { v.IdEsito, v.DataEsecuzione, v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore })
            .ToListAsync(ct);
        return Json(righe.Select(v => new
        {
            v.IdEsito,
            data = v.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore
        }));
    }

    [HttpGet("api/taratura/validazione/{id:int}/rapporto")]
    [RichiedePermesso(Permessi.TaraturaValidazione)]
    public async Task<IActionResult> ValidazioneRapporto(int id, CancellationToken ct)
    {
        var riga = await _ctx.ValidazioniCalcolo.AsNoTracking().FirstOrDefaultAsync(v => v.IdEsito == id, ct);
        if (riga is null) return NotFound(new { messaggio = "Validazione non trovata." });

        var esito = JsonSerializer.Deserialize<EsitoValidazioneCalcoli>(riga.Dettaglio, JsonOpts)
            ?? throw new InvalidOperationException("Dettaglio validazione non interpretabile.");
        var pdf = _generatoreValidazione.GeneraPdf(esito, riga.Operatore);
        return File(pdf, MimePdf, _generatoreValidazione.NomeFile(esito));
    }

    // ═══════════════ Analisi AI anti-errore della taratura ═════════
    [HttpPost("api/taratura/{id:int}/analizza")]
    public async Task<IActionResult> Analizza(int id, [FromBody] RichiestaAnalisiTaratura? req, CancellationToken ct)
    {
        var t = await _ctx.Tarature.FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });

        var misure = JsonSerializer.Deserialize<List<RigaMisuraDto>>(t.DatiMisure, JsonOpts) ?? new();
        var (dati, cdr, errore) = await ComponiAsync(t.IdCampioneRiferimento, t.RisoluzioneStrumento, t.FattoreCopertura, misure, ct);
        if (errore is not null) return errore;

        var risultato = _calcolo.Calcola(dati!);
        var anomalie = _controlli.Controlla(dati!, risultato).Anomalie.ToList();

        // Scadenza CDR: controllo che dipende dall'anagrafica, fuori dal servizio numerico.
        if (cdr!.DataScadenza is DateTime sc && sc.Date < DateTime.Today)
            anomalie.Insert(0, new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Errore,
                Messaggio = $"Il campione di riferimento (CDR) è scaduto il {sc:dd/MM/yyyy}."
            });

        var firma = FirmaTaratura(t, risultato, anomalie);

        // Archivio-first: se esiste già un'analisi per gli stessi dati, la riuso.
        if (req?.Forza != true)
        {
            var esistente = await _ctx.AnalisiTarature.AsNoTracking()
                .Where(a => a.IdTaratura == id && a.Firma == firma)
                .OrderByDescending(a => a.DataAnalisi).FirstOrDefaultAsync(ct);
            if (esistente is not null)
                return Json(new
                {
                    esistente.Esito, esistente.Testo,
                    anomalieDeterministiche = esistente.AnomalieDeterministiche,
                    anomalie = AnomalieJson(esistente.Controlli),
                    dataAnalisi = esistente.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    daArchivio = true
                });
        }

        var prompt = ComponiPromptAnalisi(t, cdr!, risultato, anomalie);
        var risposta = await _ia.InterrogaAsync(prompt, ct);
        var (esito, testo) = SeparaVerdetto(risposta.Testo);

        _ctx.AnalisiTarature.Add(new AnalisiTaratura
        {
            IdTaratura = id,
            DataAnalisi = DateTime.UtcNow,
            Firma = firma,
            AnomalieDeterministiche = anomalie.Count > 0,
            Controlli = JsonSerializer.Serialize(anomalie, JsonOpts),
            Esito = esito,
            Testo = testo,
            ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput,
            TokenOutput = risposta.TokenOutput,
            VersioneApplicazione = _versione.Versione,
            IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Taratura, "taratura.analisi.ai", EsitoOperazione.Successo,
            tipoEntita: "Taratura", idEntita: id.ToString(),
            dettaglio: new { Esito = esito, Anomalie = anomalie.Count }, ct: ct);
        return Json(new
        {
            esito, testo,
            anomalieDeterministiche = anomalie.Count > 0,
            anomalie = anomalie.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }),
            dataAnalisi = DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
            daArchivio = false
        });
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static object AnomalieJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<object>();
        var lista = JsonSerializer.Deserialize<List<AnomaliaTaratura>>(json, JsonOpts) ?? new();
        return lista.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }).ToList();
    }

    private static string FirmaTaratura(Taratura t, RisultatoTaratura r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(t.IdCampioneRiferimento).Append('|')
          .Append(t.StrumentoIdentificazione).Append('|')
          .Append(t.RisoluzioneStrumento.ToString("F4", ci)).Append('|')
          .Append(t.FattoreCopertura.ToString("F2", ci)).Append('|')
          .Append(t.DatiMisure).Append('|').Append(anomalie.Count);
        foreach (var p in r.Punti)
            sb.Append('|').Append(p.Etichetta).Append(':')
              .Append(p.DifferenzaMedia.ToString("F6", ci)).Append(':')
              .Append(p.IncertezzaEstesa.ToString("F6", ci));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static (string? Esito, string Testo) SeparaVerdetto(string risposta)
    {
        var righe = (risposta ?? "").Replace("\r", "").Split('\n');
        var esito = righe.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))?.Trim();
        return (esito, (risposta ?? "").Trim());
    }

    private static string ComponiPromptAnalisi(Taratura t, CampioneRiferimento cdr, RisultatoTaratura r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Sei un metrologo esperto di taratura di misuratori di temperatura.");
        sb.AppendLine("Ti fornisco i dati e i risultati di una taratura di un termometro contro un campione di riferimento (CDR).");
        sb.AppendLine("Valuta se i risultati sono PLAUSIBILI e segnala eventuali ERRORI GROSSOLANI di inserimento dati");
        sb.AppendLine("(letture in unità sbagliata, valori fuori scala, correzioni anomale, incertezze implausibili,");
        sb.AppendLine("punti fuori dall'intervallo del certificato). NON rifare i calcoli e NON dare giudizi di conformità.");
        sb.AppendLine("Rispondi in italiano: la PRIMA riga è un verdetto sintetico (es. \"Coerente\" oppure \"Possibili anomalie\");");
        sb.AppendLine("poi da 2 a 4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Strumento in taratura: {t.StrumentoIdentificazione} (risoluzione {t.RisoluzioneStrumento.ToString("0.###", ci)} °C).");
        sb.AppendLine($"CDR: {cdr.Identificazione}, risoluzione {cdr.Risoluzione.ToString("0.###", ci)} °C, k={cdr.FattoreCoperturaK.ToString("0.#", ci)}"
            + (cdr.DataScadenza is DateTime s ? $", scadenza {s:dd/MM/yyyy}." : "."));
        sb.AppendLine("Risultati per punto (Tr = rif., Tw = strumento, tutte in °C):");
        foreach (var p in r.Punti)
            sb.AppendLine($"- Punto {p.Etichetta}: Tr={p.TemperaturaMediaRiferimento.ToString("0.###", ci)}, "
                + $"Tw={p.TemperaturaMediaStrumento.ToString("0.###", ci)}, "
                + $"differenza={p.DifferenzaMedia.ToString("0.###", ci)}, "
                + $"U={p.IncertezzaEstesa.ToString("0.###", ci)}, "
                + $"correzione={p.FattoreCorrezione.ToString("0.###", ci)}");
        if (anomalie.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Controlli automatici che hanno già segnalato:");
            foreach (var a in anomalie)
                sb.AppendLine($"- [{a.Livello}] {(string.IsNullOrEmpty(a.Punto) ? "" : "Punto " + a.Punto + ": ")}{a.Messaggio}");
        }
        return sb.ToString();
    }

    private async Task<(DatiTaratura? Dati, CampioneRiferimento? Cdr, IActionResult? Errore)>
        ComponiAsync(int idCdr, double res, double k, List<RigaMisuraDto>? misure, CancellationToken ct)
    {
        var cdr = await _ctx.CampioniRiferimento.AsNoTracking().Include(c => c.Punti)
            .FirstOrDefaultAsync(c => c.IdCampione == idCdr, ct);
        if (cdr is null) return (null, null, BadRequest(new { messaggio = "Campione di riferimento non trovato." }));
        if (cdr.Punti.Count < 2) return (null, cdr, BadRequest(new { messaggio = "Il CDR ha meno di due punti di certificato." }));

        var righe = (misure ?? new()).Where(m => m.Punto is not null).ToList();
        if (righe.Count == 0) return (null, cdr, BadRequest(new { messaggio = "Inserisci almeno una misura." }));

        var punti = righe.GroupBy(m => m.Punto!.Trim())
            .Select(g => new PuntoTaratura
            {
                Etichetta = g.Key,
                Letture = g.Select(m => new LetturaTaratura
                { TemperaturaNominale = m.TRiferimento, LetturaStrumento = m.LetturaStrumento }).ToList()
            }).ToList();

        var dati = new DatiTaratura
        {
            Certificato = cdr.Punti.OrderBy(p => p.Ordine).Select(p => new PuntoCertificatoCdr
            {
                TIndicata = p.TIndicata, TRiferimento = p.TRiferimento, Differenza = p.Differenza,
                IncertezzaEstesa = p.IncertezzaEstesa, IncertezzaTipo = p.IncertezzaTipo
            }).ToList(),
            RisoluzioneStrumento = res,
            FattoreCopertura = k <= 0 ? 2.0 : k,
            Punti = punti
        };
        return (dati, cdr, null);
    }

    private async Task<string> NumeroAsync(DateOnly data, CancellationToken ct)
    {
        var conteggio = await _ctx.Tarature.CountAsync(t => t.DataTaratura.Year == data.Year, ct);
        return $"{data:yy}-{conteggio + 1:000}";
    }

    private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
}

// ── DTO ────────────────────────────────────────────────────────────
public record RigaMisuraDto
{
    public string? Punto { get; init; }
    public double TRiferimento { get; init; }
    public double LetturaStrumento { get; init; }
}

public record PuntoCertDto
{
    public double TIndicata { get; init; }
    public double TRiferimento { get; init; }
    public double Differenza { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double IncertezzaTipo { get; init; }
}

public record RichiestaSalvaCdr
{
    public int? Id { get; init; }
    public string Identificazione { get; init; } = "";
    public string? TipoTermometro { get; init; }
    public string? CodiceInterno { get; init; }
    public string? CentroLat { get; init; }
    public string? NumeroCertificato { get; init; }
    public DateTime? DataTaratura { get; init; }
    public DateTime? DataScadenza { get; init; }
    public double Risoluzione { get; init; }
    public double FattoreCoperturaK { get; init; } = 2.0;
    public double IncertezzaInstabilita { get; init; }
    public double IncertezzaEstesaCertificato { get; init; }
    public string? Note { get; init; }
    public List<PuntoCertDto>? Punti { get; init; }
    public List<PuntoCertDto>? PuntiSecondari { get; init; }
}

public record RichiestaCalcoloTaratura
{
    public int IdCampioneRiferimento { get; init; }
    public double RisoluzioneStrumento { get; init; }
    public double FattoreCopertura { get; init; } = 2.0;
    public List<RigaMisuraDto>? Misure { get; init; }
}

public record RichiestaSalvaTaratura
{
    public int? Id { get; init; }
    public int IdCampioneRiferimento { get; init; }
    public string StrumentoIdentificazione { get; init; } = "";
    public string? StrumentoCodice { get; init; }
    public double RisoluzioneStrumento { get; init; }
    public double FattoreCopertura { get; init; } = 2.0;
    public int? IdClienteArcLab { get; init; }
    public string? Committente { get; init; }
    public string? NumeroRapporto { get; init; }
    public DateOnly DataTaratura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string? Responsabile { get; init; }
    public string? Note { get; init; }
    public List<RigaMisuraDto>? Misure { get; init; }
}

public record RichiestaRapportoTaratura
{
    public string? Formato { get; init; }
    public string? Responsabile { get; init; }
}

public record RichiestaAnalisiTaratura
{
    /// <summary>Se vero, rigenera l'analisi anche se ne esiste già una in archivio.</summary>
    public bool Forza { get; init; }
}
