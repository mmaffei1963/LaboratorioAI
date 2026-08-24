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
/// Taratura dei misuratori di incubatori/celle (Modulo Statistico 04-02):
/// esecuzione (misure + calcolo), rapporto, registro, validazione del calcolo e
/// analisi AI. Il CDR è condiviso con i termometri. Sola scrittura sul db proprio.
/// </summary>
[RichiedePermesso(Permessi.IncubatoriEsegui)]
public class IncubatoriController : Controller
{
    private const string MimePdf = "application/pdf";
    private const string MimeRtf = "application/rtf";
    private const int MesiValiditaValidazione = 12;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioTaraturaIncubatori _calcolo;
    private readonly IServizioValidazioneIncubatori _validazione;
    private readonly IServizioControlliIncubatori _controlli;
    private readonly IGeneratoreRapportoIncubatore _generatore;
    private readonly IGeneratoreRapportoValidazione _generatoreValidazione;
    private readonly IClientAnthropic _ia;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IServizioAudit _audit;

    public IncubatoriController(
        ContestoLaboratorioAI ctx, IServizioTaraturaIncubatori calcolo, IServizioValidazioneIncubatori validazione,
        IServizioControlliIncubatori controlli, IGeneratoreRapportoIncubatore generatore,
        IGeneratoreRapportoValidazione generatoreValidazione, IClientAnthropic ia,
        IRepositoryAnagrafiche anagrafiche, IContestoUtente utente, IServizioVersione versione,
        IServizioAudit audit)
    {
        _ctx = ctx; _calcolo = calcolo; _validazione = validazione; _controlli = controlli;
        _generatore = generatore; _generatoreValidazione = generatoreValidazione; _ia = ia;
        _anagrafiche = anagrafiche; _utente = utente; _versione = versione; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Taratura incubatori";
        return View();
    }

    // ── Anagrafiche per i menu ──────────────────────────────────────
    [HttpGet("api/incubatori/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Json((await _anagrafiche.ClientiAsync(ct)).Select(c => new { c.Id, c.Nome }));

    [HttpGet("api/incubatori/strumenti")]
    public async Task<IActionResult> Strumenti(CancellationToken ct) =>
        Json((await _anagrafiche.StrumentiInterniAsync(ct)).Select(s => new { s.Id, s.Nome }));

    [HttpGet("api/incubatori/cdr/elenco")]
    public async Task<IActionResult> CdrElenco(CancellationToken ct)
    {
        var righe = await _ctx.CampioniRiferimento.AsNoTracking().Where(c => c.Attivo)
            .OrderBy(c => c.Identificazione)
            .Select(c => new
            {
                c.IdCampione, c.Identificazione, c.CodiceInterno, c.CentroLat, c.NumeroCertificato,
                c.Risoluzione, c.IncertezzaInstabilita,
                DataScadenza = c.DataScadenza.HasValue ? c.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduto = c.DataScadenza.HasValue && c.DataScadenza.Value < DateTime.Today
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    // ── Registro / dettaglio ────────────────────────────────────────
    [HttpGet("api/incubatori/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.TaratureIncubatori.AsNoTracking().Where(t => t.Attivo)
            .OrderByDescending(t => t.DataTaratura).Take(500)
            .Select(t => new
            {
                t.IdTaratura, t.NumeroRapporto, t.MisuratoreIdentificazione, t.ApparecchiaturaIdentificazione,
                t.ValoreNominale,
                DataTaratura = t.DataTaratura.ToString("dd/MM/yyyy"),
                DataScadenza = t.DataScadenza.HasValue ? t.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduta = t.DataScadenza.HasValue && t.DataScadenza.Value < DateTime.Today
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/incubatori/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var t = await _ctx.TaratureIncubatori.AsNoTracking().FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });
        var misure = JsonSerializer.Deserialize<List<RigaMisuraIncubatoreDto>>(t.DatiMisure, JsonOpts) ?? new();
        return Json(new
        {
            t.IdTaratura, t.IdCampioneRiferimento,
            t.MisuratoreIdentificazione, t.MisuratoreCodice, t.MisuratoreInterno,
            t.MisuratoreIdStrumentoArcLab, t.MisuratoreIdClienteArcLab,
            t.ApparecchiaturaIdentificazione, t.ApparecchiaturaCodice, t.ApparecchiaturaInterna,
            t.ApparecchiaturaIdStrumentoArcLab, t.ApparecchiaturaIdClienteArcLab,
            t.ValoreNominale, t.Tolleranza, t.RisoluzioneStrumento, t.DispersioneMappatura,
            t.NumeroRapporto, DataTaratura = t.DataTaratura, DataScadenza = t.DataScadenza,
            t.RiferimentoIstruzione, t.Responsabile, t.Note, Misure = misure
        });
    }

    // ── Calcolo / salvataggio ───────────────────────────────────────
    [HttpPost("api/incubatori/calcola")]
    public async Task<IActionResult> Calcola([FromBody] RichiestaCalcoloIncubatore req, CancellationToken ct)
    {
        var (dati, _, errore) = await ComponiAsync(req.IdCampioneRiferimento, req.ValoreNominale, req.Tolleranza,
            req.RisoluzioneStrumento, req.DispersioneMappatura, req.Misure, ct);
        if (errore is not null) return errore;
        return Json(_calcolo.Calcola(dati!));
    }

    [HttpPost("api/incubatori/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaIncubatore req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.MisuratoreIdentificazione))
            return BadRequest(new { messaggio = "L'identificazione del misuratore è obbligatoria." });
        if (string.IsNullOrWhiteSpace(req.ApparecchiaturaIdentificazione))
            return BadRequest(new { messaggio = "L'identificazione dell'apparecchiatura è obbligatoria." });
        if (req.IdCampioneRiferimento <= 0)
            return BadRequest(new { messaggio = "Seleziona il campione di riferimento (CDR)." });

        var ora = DateTime.UtcNow;
        TaraturaIncubatore t;
        if (req.Id is int id)
        {
            t = await _ctx.TaratureIncubatori.FirstOrDefaultAsync(x => x.IdTaratura == id, ct)
                ?? throw new InvalidOperationException("Taratura non trovata.");
            t.DataUltimaModifica = ora;
        }
        else { t = new TaraturaIncubatore { DataCreazione = ora, DataUltimaModifica = ora }; _ctx.TaratureIncubatori.Add(t); }

        t.IdCampioneRiferimento = req.IdCampioneRiferimento;
        t.MisuratoreIdentificazione = req.MisuratoreIdentificazione.Trim();
        t.MisuratoreCodice = Vuoto(req.MisuratoreCodice);
        t.MisuratoreInterno = req.MisuratoreInterno;
        t.MisuratoreIdStrumentoArcLab = req.MisuratoreIdStrumentoArcLab;
        t.MisuratoreIdClienteArcLab = req.MisuratoreIdClienteArcLab;
        t.ApparecchiaturaIdentificazione = req.ApparecchiaturaIdentificazione.Trim();
        t.ApparecchiaturaCodice = Vuoto(req.ApparecchiaturaCodice);
        t.ApparecchiaturaInterna = req.ApparecchiaturaInterna;
        t.ApparecchiaturaIdStrumentoArcLab = req.ApparecchiaturaIdStrumentoArcLab;
        t.ApparecchiaturaIdClienteArcLab = req.ApparecchiaturaIdClienteArcLab;
        t.ValoreNominale = req.ValoreNominale;
        t.Tolleranza = req.Tolleranza;
        t.RisoluzioneStrumento = req.RisoluzioneStrumento;
        t.DispersioneMappatura = req.DispersioneMappatura;
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
        await _audit.RegistraAsync(
            AreaFunzionale.Incubatori, "incubatore.salvato", EsitoOperazione.Successo,
            tipoEntita: "TaraturaIncubatore", idEntita: t.IdTaratura.ToString(),
            dettaglio: new { t.MisuratoreIdentificazione, t.ApparecchiaturaIdentificazione, t.NumeroRapporto }, ct: ct);
        return Json(new { t.IdTaratura, t.NumeroRapporto });
    }

    [HttpPost("api/incubatori/{id:int}/rapporto")]
    public async Task<IActionResult> Rapporto(int id, [FromBody] RichiestaRapportoIncubatore? req, CancellationToken ct)
    {
        var t = await _ctx.TaratureIncubatori.FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });

        var misure = JsonSerializer.Deserialize<List<RigaMisuraIncubatoreDto>>(t.DatiMisure, JsonOpts) ?? new();
        var (dati, cdr, errore) = await ComponiAsync(t.IdCampioneRiferimento, t.ValoreNominale, t.Tolleranza,
            t.RisoluzioneStrumento, t.DispersioneMappatura, misure, ct);
        if (errore is not null) return errore;

        var risultato = _calcolo.Calcola(dati!);

        var redattoDa =
            !string.IsNullOrWhiteSpace(req?.Responsabile) ? req!.Responsabile!.Trim()
            : !string.IsNullOrWhiteSpace(t.Responsabile) ? t.Responsabile!
            : _utente.NomeCompleto == "Utente di sviluppo" ? ""
            : _utente.NomeCompleto;

        if (!string.IsNullOrWhiteSpace(req?.Responsabile) &&
            !string.Equals(req!.Responsabile!.Trim(), t.Responsabile, StringComparison.Ordinal))
        {
            t.Responsabile = req.Responsabile!.Trim();
            t.DataUltimaModifica = DateTime.UtcNow;
            await _ctx.SaveChangesAsync(ct);
        }

        var dr = new DatiRapportoIncubatore
        {
            NumeroRapporto = t.NumeroRapporto ?? "",
            DataTaratura = DateOnly.FromDateTime(t.DataTaratura),
            DataScadenza = t.DataScadenza is DateTime ds ? DateOnly.FromDateTime(ds) : null,
            RiferimentoIstruzione = t.RiferimentoIstruzione,
            RedattoDa = redattoDa,
            Committente = t.MisuratoreInterno && t.ApparecchiaturaInterna ? "MARE.A (interno)" : "Cliente",
            MisuratoreIdentificazione = t.MisuratoreIdentificazione,
            MisuratoreCodice = t.MisuratoreCodice,
            ApparecchiaturaIdentificazione = t.ApparecchiaturaIdentificazione,
            ApparecchiaturaCodice = t.ApparecchiaturaCodice,
            ValoreNominale = t.ValoreNominale,
            Tolleranza = t.Tolleranza,
            RisoluzioneStrumento = t.RisoluzioneStrumento,
            CdrIdentificazione = cdr!.Identificazione,
            CdrCentroLat = cdr.CentroLat,
            CdrNumeroCertificato = cdr.NumeroCertificato,
            CdrDataTaratura = cdr.DataTaratura is DateTime cdt ? DateOnly.FromDateTime(cdt) : null,
            CdrDataScadenza = cdr.DataScadenza is DateTime cds ? DateOnly.FromDateTime(cds) : null,
            CdrRisoluzione = cdr.Risoluzione,
            Risultato = risultato
        };

        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatore.GeneraWord(dr) : _generatore.GeneraPdf(dr);
        return File(contenuto, word ? MimeRtf : MimePdf, _generatore.NomeFile(dr, word ? "rtf" : "pdf"));
    }

    // ── Validazione del calcolo ─────────────────────────────────────
    [HttpGet("api/incubatori/validazione/stato")]
    [RichiedePermesso(Permessi.IncubatoriValidazione)]
    public async Task<IActionResult> ValidazioneStato(CancellationToken ct)
    {
        var ultima = await _ctx.ValidazioniCalcoloIncubatori.AsNoTracking()
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
            versioneSet = SetRiferimentoIncubatori.Versione
        });
    }

    [HttpPost("api/incubatori/validazione/esegui")]
    [RichiedePermesso(Permessi.IncubatoriValidazione)]
    public async Task<IActionResult> ValidazioneEsegui(CancellationToken ct)
    {
        var esito = _validazione.Esegui(_versione.Versione);
        var operatore = _utente.NomeCompleto == "Utente di sviluppo" ? null : _utente.NomeCompleto;

        var riga = new EsitoValidazioneIncubatore
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
        _ctx.ValidazioniCalcoloIncubatori.Add(riga);
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Incubatori, "incubatore.validazione.eseguita",
            esito.Validato ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            tipoEntita: "EsitoValidazioneIncubatore", idEntita: riga.IdEsito.ToString(),
            dettaglio: new { esito.Validato, esito.NumeroOk, esito.NumeroKo, esito.VersioneSet }, ct: ct);
        return Json(new
        {
            riga.IdEsito, esito.Validato, esito.NumeroOk, esito.NumeroKo,
            data = esito.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            casi = esito.Casi.Select(c => new { c.Nome, c.Descrizione, c.Ok, c.Ko, c.Validato }),
            dettagli = esito.Dettagli.Select(d => new { d.Caso, d.Punto, d.Parametro, d.Calcolato, d.Atteso, d.Scarto, d.Ok })
        });
    }

    [HttpGet("api/incubatori/validazione/elenco")]
    [RichiedePermesso(Permessi.IncubatoriValidazione)]
    public async Task<IActionResult> ValidazioneElenco(CancellationToken ct)
    {
        var righe = await _ctx.ValidazioniCalcoloIncubatori.AsNoTracking()
            .OrderByDescending(v => v.DataEsecuzione).Take(50)
            .Select(v => new { v.IdEsito, v.DataEsecuzione, v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore })
            .ToListAsync(ct);
        return Json(righe.Select(v => new
        {
            v.IdEsito, data = v.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore
        }));
    }

    [HttpGet("api/incubatori/validazione/{id:int}/rapporto")]
    [RichiedePermesso(Permessi.IncubatoriValidazione)]
    public async Task<IActionResult> ValidazioneRapporto(int id, CancellationToken ct)
    {
        var riga = await _ctx.ValidazioniCalcoloIncubatori.AsNoTracking().FirstOrDefaultAsync(v => v.IdEsito == id, ct);
        if (riga is null) return NotFound(new { messaggio = "Validazione non trovata." });
        var esito = JsonSerializer.Deserialize<EsitoValidazioneCalcoli>(riga.Dettaglio, JsonOpts)
            ?? throw new InvalidOperationException("Dettaglio validazione non interpretabile.");
        var pdf = _generatoreValidazione.GeneraPdf(esito, riga.Operatore);
        return File(pdf, MimePdf, _generatoreValidazione.NomeFile(esito));
    }

    // ── Analisi AI anti-errore ──────────────────────────────────────
    [HttpPost("api/incubatori/{id:int}/analizza")]
    public async Task<IActionResult> Analizza(int id, [FromBody] RichiestaAnalisiIncubatore? req, CancellationToken ct)
    {
        var t = await _ctx.TaratureIncubatori.FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });

        var misure = JsonSerializer.Deserialize<List<RigaMisuraIncubatoreDto>>(t.DatiMisure, JsonOpts) ?? new();
        var (dati, cdr, errore) = await ComponiAsync(t.IdCampioneRiferimento, t.ValoreNominale, t.Tolleranza,
            t.RisoluzioneStrumento, t.DispersioneMappatura, misure, ct);
        if (errore is not null) return errore;

        var risultato = _calcolo.Calcola(dati!);
        var anomalie = _controlli.Controlla(dati!, risultato).Anomalie.ToList();
        if (cdr!.DataScadenza is DateTime sc && sc.Date < DateTime.Today)
            anomalie.Insert(0, new AnomaliaTaratura
            {
                Livello = LivelloAnomalia.Errore,
                Messaggio = $"Il campione di riferimento (CDR) è scaduto il {sc:dd/MM/yyyy}."
            });

        var firma = FirmaTaratura(t, risultato, anomalie);

        if (req?.Forza != true)
        {
            var esistente = await _ctx.AnalisiIncubatori.AsNoTracking()
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

        _ctx.AnalisiIncubatori.Add(new AnalisiIncubatore
        {
            IdTaratura = id, DataAnalisi = DateTime.UtcNow, Firma = firma,
            AnomalieDeterministiche = anomalie.Count > 0,
            Controlli = JsonSerializer.Serialize(anomalie, JsonOpts),
            Esito = esito, Testo = testo, ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput, TokenOutput = risposta.TokenOutput,
            VersioneApplicazione = _versione.Versione, IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Incubatori, "incubatore.analisi.ai", EsitoOperazione.Successo,
            tipoEntita: "TaraturaIncubatore", idEntita: id.ToString(),
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
    private async Task<(DatiTaraturaIncubatore? Dati, CampioneRiferimento? Cdr, IActionResult? Errore)>
        ComponiAsync(int idCdr, double nominale, double tolleranza, double res, double mappa,
            List<RigaMisuraIncubatoreDto>? misure, CancellationToken ct)
    {
        var cdr = await _ctx.CampioniRiferimento.AsNoTracking().Include(c => c.Punti)
            .FirstOrDefaultAsync(c => c.IdCampione == idCdr, ct);
        if (cdr is null) return (null, null, BadRequest(new { messaggio = "Campione di riferimento non trovato." }));
        if (cdr.Punti.Count(p => !p.Secondario) < 2) return (null, cdr, BadRequest(new { messaggio = "Il CDR ha meno di due punti di certificato primario." }));

        var righe = (misure ?? new()).ToList();
        if (righe.Count == 0) return (null, cdr, BadRequest(new { messaggio = "Inserisci almeno una lettura." }));
        if (tolleranza <= 0) return (null, cdr, BadRequest(new { messaggio = "Imposta il limite di tolleranza (±)." }));

        var secondari = cdr.Punti.Where(p => p.Secondario).OrderBy(p => p.Ordine).Select(p => new PuntoCertificatoCdr
        {
            TIndicata = p.TIndicata, TRiferimento = p.TRiferimento, Differenza = p.Differenza,
            IncertezzaEstesa = p.IncertezzaEstesa, IncertezzaTipo = p.IncertezzaTipo
        }).ToList();

        var dati = new DatiTaraturaIncubatore
        {
            Certificato = cdr.Punti.Where(p => !p.Secondario).OrderBy(p => p.Ordine).Select(p => new PuntoCertificatoCdr
            {
                TIndicata = p.TIndicata, TRiferimento = p.TRiferimento, Differenza = p.Differenza,
                IncertezzaEstesa = p.IncertezzaEstesa, IncertezzaTipo = p.IncertezzaTipo
            }).ToList(),
            CertificatoSecondario = secondari.Count >= 2 ? secondari : null,
            ValoreNominale = nominale,
            Tolleranza = tolleranza,
            RisoluzioneStrumento = res <= 0 ? 0.1 : res,
            IncertezzaInstabilita = cdr.IncertezzaInstabilita,
            DispersioneMappatura = mappa,
            FattoreCopertura = cdr.FattoreCoperturaK <= 0 ? 2.0 : cdr.FattoreCoperturaK,
            Letture = righe.Select(m => new LetturaIncubatore
            {
                Posizione = string.IsNullOrWhiteSpace(m.Posizione) ? "A" : m.Posizione!.Trim(),
                TemperaturaRilevata = m.TemperaturaRilevata,
                LetturaStrumento = m.LetturaStrumento
            }).ToList()
        };
        return (dati, cdr, null);
    }

    private async Task<string> NumeroAsync(DateOnly data, CancellationToken ct)
    {
        var conteggio = await _ctx.TaratureIncubatori.CountAsync(t => t.DataTaratura.Year == data.Year, ct);
        return $"I{data:yy}-{conteggio + 1:000}";
    }

    private static object AnomalieJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<object>();
        var lista = JsonSerializer.Deserialize<List<AnomaliaTaratura>>(json, JsonOpts) ?? new();
        return lista.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }).ToList();
    }

    private static string FirmaTaratura(TaraturaIncubatore t, RisultatoTaraturaIncubatore r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(t.IdCampioneRiferimento).Append('|').Append(t.MisuratoreIdentificazione).Append('|')
          .Append(t.ValoreNominale.ToString("F4", ci)).Append('|').Append(t.Tolleranza.ToString("F4", ci)).Append('|')
          .Append(t.RisoluzioneStrumento.ToString("F4", ci)).Append('|').Append(t.DatiMisure).Append('|').Append(anomalie.Count)
          .Append('|').Append(r.IncertezzaEstesa.ToString("F6", ci)).Append('|').Append(r.FattoreCorrezione.ToString("F6", ci));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static (string? Esito, string Testo) SeparaVerdetto(string risposta)
    {
        var righe = (risposta ?? "").Replace("\r", "").Split('\n');
        var esito = righe.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))?.Trim();
        return (esito, (risposta ?? "").Trim());
    }

    private static string ComponiPromptAnalisi(TaraturaIncubatore t, CampioneRiferimento cdr, RisultatoTaraturaIncubatore r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Sei un metrologo esperto di taratura di camere termostatiche (incubatori, frigoriferi, celle).");
        sb.AppendLine("Ti fornisco i dati e i risultati di una taratura del misuratore di un incubatore/cella contro un CDR.");
        sb.AppendLine("Valuta se i risultati sono PLAUSIBILI e segnala eventuali ERRORI GROSSOLANI di inserimento");
        sb.AppendLine("(letture in unità sbagliata o fuori scala, temperatura di esercizio incoerente, incertezze implausibili).");
        sb.AppendLine("NON rifare i calcoli e NON dare giudizi di conformità.");
        sb.AppendLine("Rispondi in italiano: la PRIMA riga è un verdetto sintetico (es. \"Coerente\" o \"Possibili anomalie\"); poi 2-4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Misuratore: {t.MisuratoreIdentificazione}. Apparecchiatura: {t.ApparecchiaturaIdentificazione}.");
        sb.AppendLine($"Temperatura di esercizio: {t.ValoreNominale.ToString("0.###", ci)} ± {t.Tolleranza.ToString("0.###", ci)} °C.");
        sb.AppendLine($"CDR: {cdr.Identificazione}" + (cdr.DataScadenza is DateTime s ? $", scadenza {s:dd/MM/yyyy}." : "."));
        sb.AppendLine($"Risultati: Tr={r.TemperaturaMediaRiferimento.ToString("0.###", ci)}, "
            + $"Tw={r.TemperaturaMediaStrumento.ToString("0.###", ci)}, correzione={r.FattoreCorrezione.ToString("0.###", ci)}, "
            + $"U={r.IncertezzaEstesa.ToString("0.###", ci)} °C, allarmi {r.AllarmeMinimo.ToString("0.##", ci)}…{r.AllarmeMassimo.ToString("0.##", ci)} °C, "
            + $"{(r.Conforme ? "conforme" : "non conforme")}.");
        if (anomalie.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Controlli automatici che hanno già segnalato:");
            foreach (var a in anomalie)
                sb.AppendLine($"- [{a.Livello}] {(string.IsNullOrEmpty(a.Punto) ? "" : "Pos. " + a.Punto + ": ")}{a.Messaggio}");
        }
        return sb.ToString();
    }

    private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
}

// ── DTO ─────────────────────────────────────────────────────────────
public record RigaMisuraIncubatoreDto
{
    public string? Posizione { get; init; }
    public double TemperaturaRilevata { get; init; }
    public double LetturaStrumento { get; init; }
}

public record RichiestaCalcoloIncubatore
{
    public int IdCampioneRiferimento { get; init; }
    public double ValoreNominale { get; init; }
    public double Tolleranza { get; init; }
    public double RisoluzioneStrumento { get; init; } = 0.1;
    public double DispersioneMappatura { get; init; }
    public List<RigaMisuraIncubatoreDto>? Misure { get; init; }
}

public record RichiestaSalvaIncubatore
{
    public int? Id { get; init; }
    public int IdCampioneRiferimento { get; init; }

    public string MisuratoreIdentificazione { get; init; } = "";
    public string? MisuratoreCodice { get; init; }
    public bool MisuratoreInterno { get; init; }
    public int? MisuratoreIdStrumentoArcLab { get; init; }
    public int? MisuratoreIdClienteArcLab { get; init; }

    public string ApparecchiaturaIdentificazione { get; init; } = "";
    public string? ApparecchiaturaCodice { get; init; }
    public bool ApparecchiaturaInterna { get; init; }
    public int? ApparecchiaturaIdStrumentoArcLab { get; init; }
    public int? ApparecchiaturaIdClienteArcLab { get; init; }

    public double ValoreNominale { get; init; }
    public double Tolleranza { get; init; }
    public double RisoluzioneStrumento { get; init; } = 0.1;
    public double DispersioneMappatura { get; init; }

    public string? NumeroRapporto { get; init; }
    public DateOnly DataTaratura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string? Responsabile { get; init; }
    public string? Note { get; init; }
    public List<RigaMisuraIncubatoreDto>? Misure { get; init; }
}

public record RichiestaRapportoIncubatore
{
    public string? Formato { get; init; }
    public string? Responsabile { get; init; }
}

public record RichiestaAnalisiIncubatore
{
    public bool Forza { get; init; }
}
