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
/// Mappatura di apparecchiature termostatiche (Modulo 04-03): rilievo della
/// temperatura del CDR nei ripiani, calcolo con test statistici, rapporto,
/// registro, validazione del calcolo e analisi AI. CDR condiviso coi termometri.
/// </summary>
[RichiedePermesso(Permessi.MappaturaEsegui)]
public class MappaturaController : Controller
{
    private const string MimePdf = "application/pdf";
    private const string MimeRtf = "application/rtf";
    private const int MesiValiditaValidazione = 12;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioMappaturaIncubatori _calcolo;
    private readonly IServizioValidazioneMappatura _validazione;
    private readonly IServizioControlliMappatura _controlli;
    private readonly IGeneratoreRapportoMappatura _generatore;
    private readonly IGeneratoreRapportoValidazione _generatoreValidazione;
    private readonly IClientAnthropic _ia;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly IServizioAudit _audit;

    public MappaturaController(
        ContestoLaboratorioAI ctx, IServizioMappaturaIncubatori calcolo, IServizioValidazioneMappatura validazione,
        IServizioControlliMappatura controlli, IGeneratoreRapportoMappatura generatore,
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
        ViewData["Titolo"] = "Mappatura incubatori";
        return View();
    }

    [HttpGet("api/mappatura/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Json((await _anagrafiche.ClientiAsync(ct)).Select(c => new { c.Id, c.Nome }));

    [HttpGet("api/mappatura/strumenti")]
    public async Task<IActionResult> Strumenti(CancellationToken ct) =>
        Json((await _anagrafiche.StrumentiInterniAsync(ct)).Select(s => new { s.Id, s.Nome }));

    [HttpGet("api/mappatura/cdr/elenco")]
    public async Task<IActionResult> CdrElenco(CancellationToken ct)
    {
        var righe = await _ctx.CampioniRiferimento.AsNoTracking().Where(c => c.Attivo)
            .OrderBy(c => c.Identificazione)
            .Select(c => new
            {
                c.IdCampione, c.Identificazione, c.CodiceInterno, c.NumeroCertificato, c.Risoluzione,
                DataScadenza = c.DataScadenza.HasValue ? c.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduto = c.DataScadenza.HasValue && c.DataScadenza.Value < DateTime.Today
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/mappatura/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.Mappature.AsNoTracking().Where(t => t.Attivo)
            .OrderByDescending(t => t.DataMappatura).Take(500)
            .Select(t => new
            {
                t.IdMappatura, t.NumeroRapporto, t.ApparecchiaturaIdentificazione, t.ValoreNominale,
                DataMappatura = t.DataMappatura.ToString("dd/MM/yyyy"),
                DataScadenza = t.DataScadenza.HasValue ? t.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduta = t.DataScadenza.HasValue && t.DataScadenza.Value < DateTime.Today
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/mappatura/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var t = await _ctx.Mappature.AsNoTracking().FirstOrDefaultAsync(x => x.IdMappatura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Mappatura non trovata." });
        var misure = JsonSerializer.Deserialize<List<RigaMisuraMappaturaDto>>(t.DatiMisure, JsonOpts) ?? new();
        return Json(new
        {
            t.IdMappatura, t.IdCampioneRiferimento,
            t.ApparecchiaturaIdentificazione, t.ApparecchiaturaCodice, t.ApparecchiaturaInterna,
            t.ApparecchiaturaIdStrumentoArcLab, t.ApparecchiaturaIdClienteArcLab,
            t.ValoreNominale, t.Tolleranza, t.RisoluzioneCdr,
            t.NumeroRapporto, DataMappatura = t.DataMappatura, DataScadenza = t.DataScadenza,
            t.RiferimentoIstruzione, t.Responsabile, t.Note, Misure = misure
        });
    }

    [HttpPost("api/mappatura/calcola")]
    public async Task<IActionResult> Calcola([FromBody] RichiestaCalcoloMappatura req, CancellationToken ct)
    {
        var (dati, _, errore) = await ComponiAsync(req.IdCampioneRiferimento, req.ValoreNominale, req.Tolleranza,
            req.RisoluzioneCdr, req.Misure, ct);
        if (errore is not null) return errore;
        return Json(_calcolo.Calcola(dati!));
    }

    [HttpPost("api/mappatura/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaMappatura req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ApparecchiaturaIdentificazione))
            return BadRequest(new { messaggio = "L'identificazione dell'apparecchiatura è obbligatoria." });
        if (req.IdCampioneRiferimento <= 0)
            return BadRequest(new { messaggio = "Seleziona il campione di riferimento (CDR)." });

        var ora = DateTime.UtcNow;
        TaraturaMappatura t;
        if (req.Id is int id)
        {
            t = await _ctx.Mappature.FirstOrDefaultAsync(x => x.IdMappatura == id, ct)
                ?? throw new InvalidOperationException("Mappatura non trovata.");
            t.DataUltimaModifica = ora;
        }
        else { t = new TaraturaMappatura { DataCreazione = ora, DataUltimaModifica = ora }; _ctx.Mappature.Add(t); }

        t.IdCampioneRiferimento = req.IdCampioneRiferimento;
        t.ApparecchiaturaIdentificazione = req.ApparecchiaturaIdentificazione.Trim();
        t.ApparecchiaturaCodice = Vuoto(req.ApparecchiaturaCodice);
        t.ApparecchiaturaInterna = req.ApparecchiaturaInterna;
        t.ApparecchiaturaIdStrumentoArcLab = req.ApparecchiaturaIdStrumentoArcLab;
        t.ApparecchiaturaIdClienteArcLab = req.ApparecchiaturaIdClienteArcLab;
        t.ValoreNominale = req.ValoreNominale;
        t.Tolleranza = req.Tolleranza;
        t.RisoluzioneCdr = req.RisoluzioneCdr <= 0 ? 0.01 : req.RisoluzioneCdr;
        t.DataMappatura = req.DataMappatura.ToDateTime(TimeOnly.MinValue);
        t.DataScadenza = req.DataScadenza?.ToDateTime(TimeOnly.MinValue) ?? req.DataMappatura.AddYears(1).ToDateTime(TimeOnly.MinValue);
        t.RiferimentoIstruzione = Vuoto(req.RiferimentoIstruzione);
        t.Responsabile = Vuoto(req.Responsabile);
        t.Note = Vuoto(req.Note);
        t.DatiMisure = JsonSerializer.Serialize(req.Misure ?? new(), JsonOpts);
        t.IdUtente = _utente.IdUtente;
        t.VersioneApplicazione = _versione.Versione;
        if (string.IsNullOrWhiteSpace(t.NumeroRapporto))
            t.NumeroRapporto = string.IsNullOrWhiteSpace(req.NumeroRapporto) ? await NumeroAsync(req.DataMappatura, ct) : req.NumeroRapporto.Trim();

        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Mappatura, "mappatura.salvata", EsitoOperazione.Successo,
            tipoEntita: "TaraturaMappatura", idEntita: t.IdMappatura.ToString(),
            dettaglio: new { t.ApparecchiaturaIdentificazione, t.NumeroRapporto }, ct: ct);
        return Json(new { t.IdMappatura, t.NumeroRapporto });
    }

    [HttpPost("api/mappatura/{id:int}/rapporto")]
    public async Task<IActionResult> Rapporto(int id, [FromBody] RichiestaRapportoMappatura? req, CancellationToken ct)
    {
        var t = await _ctx.Mappature.FirstOrDefaultAsync(x => x.IdMappatura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Mappatura non trovata." });

        var misure = JsonSerializer.Deserialize<List<RigaMisuraMappaturaDto>>(t.DatiMisure, JsonOpts) ?? new();
        var (dati, cdr, errore) = await ComponiAsync(t.IdCampioneRiferimento, t.ValoreNominale, t.Tolleranza, t.RisoluzioneCdr, misure, ct);
        if (errore is not null) return errore;

        var risultato = _calcolo.Calcola(dati!);
        var redattoDa =
            !string.IsNullOrWhiteSpace(req?.Responsabile) ? req!.Responsabile!.Trim()
            : !string.IsNullOrWhiteSpace(t.Responsabile) ? t.Responsabile!
            : _utente.NomeCompleto == "Utente di sviluppo" ? "" : _utente.NomeCompleto;

        if (!string.IsNullOrWhiteSpace(req?.Responsabile) &&
            !string.Equals(req!.Responsabile!.Trim(), t.Responsabile, StringComparison.Ordinal))
        {
            t.Responsabile = req.Responsabile!.Trim();
            t.DataUltimaModifica = DateTime.UtcNow;
            await _ctx.SaveChangesAsync(ct);
        }

        var dr = new DatiRapportoMappatura
        {
            NumeroRapporto = t.NumeroRapporto ?? "",
            DataMappatura = DateOnly.FromDateTime(t.DataMappatura),
            DataScadenza = t.DataScadenza is DateTime ds ? DateOnly.FromDateTime(ds) : null,
            RiferimentoIstruzione = t.RiferimentoIstruzione,
            RedattoDa = redattoDa,
            Committente = t.ApparecchiaturaInterna ? "MARE.A (interno)" : "Cliente",
            ApparecchiaturaIdentificazione = t.ApparecchiaturaIdentificazione,
            ApparecchiaturaCodice = t.ApparecchiaturaCodice,
            ValoreNominale = t.ValoreNominale,
            Tolleranza = t.Tolleranza,
            CdrIdentificazione = cdr!.Identificazione,
            CdrNumeroCertificato = cdr.NumeroCertificato,
            CdrDataTaratura = cdr.DataTaratura is DateTime cdt ? DateOnly.FromDateTime(cdt) : null,
            CdrDataScadenza = cdr.DataScadenza is DateTime cds ? DateOnly.FromDateTime(cds) : null,
            Risultato = risultato
        };

        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatore.GeneraWord(dr) : _generatore.GeneraPdf(dr);
        return File(contenuto, word ? MimeRtf : MimePdf, _generatore.NomeFile(dr, word ? "rtf" : "pdf"));
    }

    // ── Validazione del calcolo ─────────────────────────────────────
    [HttpGet("api/mappatura/validazione/stato")]
    [RichiedePermesso(Permessi.MappaturaValidazione)]
    public async Task<IActionResult> ValidazioneStato(CancellationToken ct)
    {
        var ultima = await _ctx.ValidazioniCalcoloMappatura.AsNoTracking()
            .OrderByDescending(v => v.DataEsecuzione).FirstOrDefaultAsync(ct);
        var scadenza = ultima?.DataEsecuzione.AddMonths(MesiValiditaValidazione);
        return Json(new
        {
            eseguitaMai = ultima is not null, idEsito = ultima?.IdEsito, validato = ultima?.Validato,
            data = ultima?.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), operatore = ultima?.Operatore,
            scaduta = ultima is null || scadenza < DateTime.UtcNow,
            mesiValidita = MesiValiditaValidazione, versioneSet = SetRiferimentoMappatura.Versione
        });
    }

    [HttpPost("api/mappatura/validazione/esegui")]
    [RichiedePermesso(Permessi.MappaturaValidazione)]
    public async Task<IActionResult> ValidazioneEsegui(CancellationToken ct)
    {
        var esito = _validazione.Esegui(_versione.Versione);
        var operatore = _utente.NomeCompleto == "Utente di sviluppo" ? null : _utente.NomeCompleto;
        var riga = new EsitoValidazioneMappatura
        {
            DataEsecuzione = esito.DataEsecuzione, Validato = esito.Validato, NumeroOk = esito.NumeroOk,
            NumeroKo = esito.NumeroKo, Tolleranza = esito.Tolleranza, VersioneSet = esito.VersioneSet,
            VersioneApplicazione = esito.VersioneApplicazione, Dettaglio = JsonSerializer.Serialize(esito, JsonOpts),
            IdUtente = _utente.IdUtente, Operatore = operatore
        };
        _ctx.ValidazioniCalcoloMappatura.Add(riga);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(
            AreaFunzionale.Mappatura, "mappatura.validazione.eseguita",
            esito.Validato ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            tipoEntita: "EsitoValidazioneMappatura", idEntita: riga.IdEsito.ToString(),
            dettaglio: new { esito.Validato, esito.NumeroOk, esito.NumeroKo, esito.VersioneSet }, ct: ct);
        return Json(new
        {
            riga.IdEsito, esito.Validato, esito.NumeroOk, esito.NumeroKo,
            data = esito.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            casi = esito.Casi.Select(c => new { c.Nome, c.Descrizione, c.Ok, c.Ko, c.Validato }),
            dettagli = esito.Dettagli.Select(d => new { d.Caso, d.Punto, d.Parametro, d.Calcolato, d.Atteso, d.Scarto, d.Ok })
        });
    }

    [HttpGet("api/mappatura/validazione/elenco")]
    [RichiedePermesso(Permessi.MappaturaValidazione)]
    public async Task<IActionResult> ValidazioneElenco(CancellationToken ct)
    {
        var righe = await _ctx.ValidazioniCalcoloMappatura.AsNoTracking()
            .OrderByDescending(v => v.DataEsecuzione).Take(50)
            .Select(v => new { v.IdEsito, v.DataEsecuzione, v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore })
            .ToListAsync(ct);
        return Json(righe.Select(v => new
        {
            v.IdEsito, data = v.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore
        }));
    }

    [HttpGet("api/mappatura/validazione/{id:int}/rapporto")]
    [RichiedePermesso(Permessi.MappaturaValidazione)]
    public async Task<IActionResult> ValidazioneRapporto(int id, CancellationToken ct)
    {
        var riga = await _ctx.ValidazioniCalcoloMappatura.AsNoTracking().FirstOrDefaultAsync(v => v.IdEsito == id, ct);
        if (riga is null) return NotFound(new { messaggio = "Validazione non trovata." });
        var esito = JsonSerializer.Deserialize<EsitoValidazioneCalcoli>(riga.Dettaglio, JsonOpts)
            ?? throw new InvalidOperationException("Dettaglio validazione non interpretabile.");
        var pdf = _generatoreValidazione.GeneraPdf(esito, riga.Operatore);
        return File(pdf, MimePdf, _generatoreValidazione.NomeFile(esito));
    }

    // ── Analisi AI ──────────────────────────────────────────────────
    [HttpPost("api/mappatura/{id:int}/analizza")]
    public async Task<IActionResult> Analizza(int id, [FromBody] RichiestaAnalisiMappatura? req, CancellationToken ct)
    {
        var t = await _ctx.Mappature.FirstOrDefaultAsync(x => x.IdMappatura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Mappatura non trovata." });

        var misure = JsonSerializer.Deserialize<List<RigaMisuraMappaturaDto>>(t.DatiMisure, JsonOpts) ?? new();
        var (dati, cdr, errore) = await ComponiAsync(t.IdCampioneRiferimento, t.ValoreNominale, t.Tolleranza, t.RisoluzioneCdr, misure, ct);
        if (errore is not null) return errore;

        var risultato = _calcolo.Calcola(dati!);
        var anomalie = _controlli.Controlla(dati!, risultato).Anomalie.ToList();
        if (cdr!.DataScadenza is DateTime sc && sc.Date < DateTime.Today)
            anomalie.Insert(0, new AnomaliaTaratura { Livello = LivelloAnomalia.Errore, Messaggio = $"Il CDR è scaduto il {sc:dd/MM/yyyy}." });

        var firma = Firma(t, risultato, anomalie);
        if (req?.Forza != true)
        {
            var esistente = await _ctx.AnalisiMappature.AsNoTracking()
                .Where(a => a.IdMappatura == id && a.Firma == firma)
                .OrderByDescending(a => a.DataAnalisi).FirstOrDefaultAsync(ct);
            if (esistente is not null)
                return Json(new
                {
                    esistente.Esito, esistente.Testo, anomalieDeterministiche = esistente.AnomalieDeterministiche,
                    anomalie = AnomalieJson(esistente.Controlli),
                    dataAnalisi = esistente.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), daArchivio = true
                });
        }

        var prompt = ComponiPrompt(t, cdr!, risultato, anomalie);
        var risposta = await _ia.InterrogaAsync(prompt, ct);
        var (esito, testo) = SeparaVerdetto(risposta.Testo);

        _ctx.AnalisiMappature.Add(new AnalisiMappatura
        {
            IdMappatura = id, DataAnalisi = DateTime.UtcNow, Firma = firma,
            AnomalieDeterministiche = anomalie.Count > 0, Controlli = JsonSerializer.Serialize(anomalie, JsonOpts),
            Esito = esito, Testo = testo, ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput, TokenOutput = risposta.TokenOutput,
            VersioneApplicazione = _versione.Versione, IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Mappatura, "mappatura.analisi.ai", EsitoOperazione.Successo,
            tipoEntita: "TaraturaMappatura", idEntita: id.ToString(),
            dettaglio: new { Esito = esito, Anomalie = anomalie.Count }, ct: ct);
        return Json(new
        {
            esito, testo, anomalieDeterministiche = anomalie.Count > 0,
            anomalie = anomalie.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }),
            dataAnalisi = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), daArchivio = false
        });
    }

    // ── Helper ─────────────────────────────────────────────────────
    private async Task<(DatiMappatura? Dati, CampioneRiferimento? Cdr, IActionResult? Errore)>
        ComponiAsync(int idCdr, double nominale, double tolleranza, double res, List<RigaMisuraMappaturaDto>? misure, CancellationToken ct)
    {
        var cdr = await _ctx.CampioniRiferimento.AsNoTracking().Include(c => c.Punti)
            .FirstOrDefaultAsync(c => c.IdCampione == idCdr, ct);
        if (cdr is null) return (null, null, BadRequest(new { messaggio = "Campione di riferimento non trovato." }));
        if (cdr.Punti.Count(p => !p.Secondario) < 2) return (null, cdr, BadRequest(new { messaggio = "Il CDR ha meno di due punti di certificato primario." }));

        var righe = (misure ?? new()).Where(m => !string.IsNullOrWhiteSpace(m.Posizione)).ToList();
        if (righe.Count == 0) return (null, cdr, BadRequest(new { messaggio = "Inserisci almeno una lettura." }));
        if (tolleranza <= 0) return (null, cdr, BadRequest(new { messaggio = "Imposta il limite di tolleranza (±)." }));

        var secondari = cdr.Punti.Where(p => p.Secondario).OrderBy(p => p.Ordine).Select(Mappa).ToList();
        var dati = new DatiMappatura
        {
            Certificato = cdr.Punti.Where(p => !p.Secondario).OrderBy(p => p.Ordine).Select(Mappa).ToList(),
            CertificatoSecondario = secondari.Count >= 2 ? secondari : null,
            ValoreNominale = nominale,
            Tolleranza = tolleranza,
            RisoluzioneCdr = res <= 0 ? 0.01 : res,
            IncertezzaEstesaCertificato = cdr.IncertezzaEstesaCertificato,
            IncertezzaInstabilita = cdr.IncertezzaInstabilita,
            FattoreCopertura = cdr.FattoreCoperturaK <= 0 ? 2.0 : cdr.FattoreCoperturaK,
            Letture = righe.Select(m => new LetturaMappatura
            { Posizione = m.Posizione!.Trim(), TemperaturaRilevata = m.TemperaturaRilevata }).ToList()
        };
        return (dati, cdr, null);
    }

    private static PuntoCertificatoCdr Mappa(PuntoCertificato p) => new()
    {
        TIndicata = p.TIndicata, TRiferimento = p.TRiferimento, Differenza = p.Differenza,
        IncertezzaEstesa = p.IncertezzaEstesa, IncertezzaTipo = p.IncertezzaTipo
    };

    private async Task<string> NumeroAsync(DateOnly data, CancellationToken ct)
    {
        var conteggio = await _ctx.Mappature.CountAsync(t => t.DataMappatura.Year == data.Year, ct);
        return $"M{data:yy}-{conteggio + 1:000}";
    }

    private static object AnomalieJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<object>();
        var lista = JsonSerializer.Deserialize<List<AnomaliaTaratura>>(json, JsonOpts) ?? new();
        return lista.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }).ToList();
    }

    private static string Firma(TaraturaMappatura t, RisultatoMappatura r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(t.IdCampioneRiferimento).Append('|').Append(t.ApparecchiaturaIdentificazione).Append('|')
          .Append(t.ValoreNominale.ToString("F4", ci)).Append('|').Append(t.Tolleranza.ToString("F4", ci)).Append('|')
          .Append(t.DatiMisure).Append('|').Append(anomalie.Count).Append('|')
          .Append(r.IncertezzaEstesa.ToString("F6", ci)).Append('|').Append(r.TemperaturaMedia.ToString("F6", ci));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static (string? Esito, string Testo) SeparaVerdetto(string risposta)
    {
        var righe = (risposta ?? "").Replace("\r", "").Split('\n');
        return (righe.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))?.Trim(), (risposta ?? "").Trim());
    }

    private static string ComponiPrompt(TaraturaMappatura t, CampioneRiferimento cdr, RisultatoMappatura r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Sei un metrologo esperto di mappatura di camere termostatiche (incubatori, frigoriferi, celle).");
        sb.AppendLine("Ti fornisco i dati e i risultati di una mappatura. Valuta se sono PLAUSIBILI e segnala ERRORI");
        sb.AppendLine("GROSSOLANI di inserimento (letture fuori scala/unità, temperatura di esercizio incoerente).");
        sb.AppendLine("NON rifare i calcoli. Rispondi in italiano: prima riga verdetto sintetico, poi 2-4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Apparecchiatura: {t.ApparecchiaturaIdentificazione}. CDR: {cdr.Identificazione}.");
        sb.AppendLine($"Temperatura di esercizio: {t.ValoreNominale.ToString("0.###", ci)} ± {t.Tolleranza.ToString("0.###", ci)} °C.");
        sb.AppendLine($"Risultati: Tr={r.TemperaturaMedia.ToString("0.###", ci)}, U={r.IncertezzaEstesa.ToString("0.###", ci)} °C, "
            + $"intervallo ammesso {r.IntervalloMinimo.ToString("0.##", ci)}…{r.IntervalloMassimo.ToString("0.##", ci)} °C.");
        sb.AppendLine("Medie per ripiano: " + string.Join(", ", r.Ripiani.Select(x => $"{x.Posizione}={x.Media.ToString("0.###", ci)}{(x.Conforme ? "" : " (fuori)")}")));
        sb.AppendLine($"Giudizio test: {r.Giudizio}");
        if (anomalie.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Controlli automatici:");
            foreach (var a in anomalie) sb.AppendLine($"- [{a.Livello}] {(string.IsNullOrEmpty(a.Punto) ? "" : "Pos. " + a.Punto + ": ")}{a.Messaggio}");
        }
        return sb.ToString();
    }

    private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
}

// ── DTO ─────────────────────────────────────────────────────────────
public record RigaMisuraMappaturaDto
{
    public string? Posizione { get; init; }
    public double TemperaturaRilevata { get; init; }
}

public record RichiestaCalcoloMappatura
{
    public int IdCampioneRiferimento { get; init; }
    public double ValoreNominale { get; init; }
    public double Tolleranza { get; init; }
    public double RisoluzioneCdr { get; init; } = 0.01;
    public List<RigaMisuraMappaturaDto>? Misure { get; init; }
}

public record RichiestaSalvaMappatura
{
    public int? Id { get; init; }
    public int IdCampioneRiferimento { get; init; }
    public string ApparecchiaturaIdentificazione { get; init; } = "";
    public string? ApparecchiaturaCodice { get; init; }
    public bool ApparecchiaturaInterna { get; init; }
    public int? ApparecchiaturaIdStrumentoArcLab { get; init; }
    public int? ApparecchiaturaIdClienteArcLab { get; init; }
    public double ValoreNominale { get; init; }
    public double Tolleranza { get; init; }
    public double RisoluzioneCdr { get; init; } = 0.01;
    public string? NumeroRapporto { get; init; }
    public DateOnly DataMappatura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string? Responsabile { get; init; }
    public string? Note { get; init; }
    public List<RigaMisuraMappaturaDto>? Misure { get; init; }
}

public record RichiestaRapportoMappatura
{
    public string? Formato { get; init; }
    public string? Responsabile { get; init; }
}

public record RichiestaAnalisiMappatura
{
    public bool Forza { get; init; }
}
