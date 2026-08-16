using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
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
/// Taratura bilance (Modulo 45, EURAMET cg-18): anagrafica pesiere, esecuzione
/// (eccentricità, ripetibilità, linearità + budget d'incertezza), rapporto,
/// registro, validazione del calcolo e analisi AI.
/// </summary>
[RichiedePermesso(Permessi.BilanceEsegui)]
public class BilanceController : Controller
{
    private const string MimePdf = "application/pdf";
    private const string MimeRtf = "application/rtf";
    private const int MesiValiditaValidazione = 12;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IServizioBilancia _calcolo;
    private readonly IServizioValidazioneBilancia _validazione;
    private readonly IServizioControlliBilancia _controlli;
    private readonly IGeneratoreRapportoBilancia _generatore;
    private readonly IGeneratoreRapportoValidazione _generatoreValidazione;
    private readonly IClientAnthropic _ia;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;

    public BilanceController(
        ContestoLaboratorioAI ctx, IServizioBilancia calcolo, IServizioValidazioneBilancia validazione,
        IServizioControlliBilancia controlli, IGeneratoreRapportoBilancia generatore,
        IGeneratoreRapportoValidazione generatoreValidazione, IClientAnthropic ia,
        IRepositoryAnagrafiche anagrafiche, IContestoUtente utente, IServizioVersione versione)
    {
        _ctx = ctx; _calcolo = calcolo; _validazione = validazione; _controlli = controlli;
        _generatore = generatore; _generatoreValidazione = generatoreValidazione; _ia = ia;
        _anagrafiche = anagrafiche; _utente = utente; _versione = versione;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Taratura bilance";
        return View();
    }

    [HttpGet("api/bilance/clienti")]
    public async Task<IActionResult> Clienti(CancellationToken ct) =>
        Json((await _anagrafiche.ClientiAsync(ct)).Select(c => new { c.Id, c.Nome }));

    [HttpGet("api/bilance/strumenti")]
    public async Task<IActionResult> Strumenti(CancellationToken ct) =>
        Json((await _anagrafiche.StrumentiInterniAsync(ct)).Select(s => new { s.Id, s.Nome }));

    // ── Pesiere ─────────────────────────────────────────────────────
    [HttpGet("api/bilance/pesiere")]
    public async Task<IActionResult> Pesiere(CancellationToken ct)
    {
        var righe = await _ctx.Pesiere.AsNoTracking().Where(p => p.Attivo)
            .OrderBy(p => p.Identificazione)
            .Select(p => new { p.IdPesiera, p.Identificazione, Masse = p.Masse.Count })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/bilance/pesiera/{id:int}")]
    public async Task<IActionResult> Pesiera(int id, CancellationToken ct)
    {
        var p = await _ctx.Pesiere.AsNoTracking().Include(x => x.Masse)
            .FirstOrDefaultAsync(x => x.IdPesiera == id, ct);
        if (p is null) return NotFound(new { messaggio = "Pesiera non trovata." });
        return Json(new
        {
            p.IdPesiera, p.Identificazione, p.Note,
            Masse = p.Masse.OrderBy(m => m.Ordine).Select(m => new
            {
                m.ValoreNominale, m.ValoreConvenzionale, m.IncertezzaEstesa, m.IncertezzaTipo, m.Mpe, m.ClasseOiml,
                m.CertificatoLat, DataCertificato = m.DataCertificato, DataScadenza = m.DataScadenza, m.CentroLat
            })
        });
    }

    [HttpPost("api/bilance/pesiera/salva")]
    public async Task<IActionResult> PesieraSalva([FromBody] RichiestaSalvaPesiera req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Identificazione))
            return BadRequest(new { messaggio = "L'identificazione della pesiera è obbligatoria." });
        if (req.Masse is not { Count: >= 1 })
            return BadRequest(new { messaggio = "Inserisci almeno una massa." });

        var ora = DateTime.UtcNow;
        Pesiera p;
        if (req.Id is int id)
        {
            p = await _ctx.Pesiere.Include(x => x.Masse).FirstOrDefaultAsync(x => x.IdPesiera == id, ct)
                ?? throw new InvalidOperationException("Pesiera non trovata.");
            _ctx.MasseCampione.RemoveRange(p.Masse);
            p.Masse.Clear();
            p.DataUltimaModifica = ora;
        }
        else { p = new Pesiera { DataCreazione = ora, DataUltimaModifica = ora }; _ctx.Pesiere.Add(p); }

        p.Identificazione = req.Identificazione.Trim();
        p.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        int ordine = 1;
        foreach (var m in req.Masse)
            p.Masse.Add(new MassaCampione
            {
                ValoreNominale = m.ValoreNominale, ValoreConvenzionale = m.ValoreConvenzionale,
                IncertezzaEstesa = m.IncertezzaEstesa, IncertezzaTipo = m.IncertezzaTipo,
                Mpe = m.Mpe, ClasseOiml = string.IsNullOrWhiteSpace(m.ClasseOiml) ? null : m.ClasseOiml.Trim(),
                CertificatoLat = string.IsNullOrWhiteSpace(m.CertificatoLat) ? null : m.CertificatoLat.Trim(),
                DataCertificato = m.DataCertificato, DataScadenza = m.DataScadenza, CentroLat = m.CentroLat,
                Ordine = ordine++
            });

        await _ctx.SaveChangesAsync(ct);
        return Json(new { p.IdPesiera });
    }

    // ── Registro / dettaglio tarature ───────────────────────────────
    [HttpGet("api/bilance/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var righe = await _ctx.TaratureBilance.AsNoTracking().Where(t => t.Attivo)
            .OrderByDescending(t => t.DataTaratura).Take(500)
            .Select(t => new
            {
                t.IdTaratura, t.NumeroRapporto, t.BilanciaIdentificazione, t.Portata,
                DataTaratura = t.DataTaratura.ToString("dd/MM/yyyy"),
                DataScadenza = t.DataScadenza.HasValue ? t.DataScadenza.Value.ToString("dd/MM/yyyy") : null,
                Scaduta = t.DataScadenza.HasValue && t.DataScadenza.Value < DateTime.Today
            })
            .ToListAsync(ct);
        return Json(righe);
    }

    [HttpGet("api/bilance/{id:int}")]
    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        var t = await _ctx.TaratureBilance.AsNoTracking().FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });
        var prove = JsonSerializer.Deserialize<ProveBilanciaDto>(t.DatiProve, JsonOpts) ?? new();
        return Json(new
        {
            t.IdTaratura, t.IdPesiera,
            t.BilanciaIdentificazione, t.BilanciaModello, t.BilanciaInterna,
            t.BilanciaIdStrumentoArcLab, t.BilanciaIdClienteArcLab,
            t.Portata, t.UnitaFormato, t.CoefficienteSensibilitaTermica, t.MaxIncertezzaAmmessa,
            t.TemperaturaTaratura, t.TemperaturaUtilizzo,
            t.CampoTaratura, t.TempoStabilizzazione, t.TempoIntervalloLettura, t.MaxVariazioneTemperatura, t.Tipo, t.Sda, t.TermometroUtilizzato,
            t.NumeroRapporto, DataTaratura = t.DataTaratura, DataScadenza = t.DataScadenza,
            t.RiferimentoIstruzione, t.Responsabile, t.Note, Prove = prove
        });
    }

    [HttpPost("api/bilance/calcola")]
    public IActionResult Calcola([FromBody] RichiestaCalcoloBilancia req)
    {
        var dati = Componi(req);
        return Json(_calcolo.Calcola(dati));
    }

    [HttpPost("api/bilance/salva")]
    public async Task<IActionResult> Salva([FromBody] RichiestaSalvaBilancia req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.BilanciaIdentificazione))
            return BadRequest(new { messaggio = "L'identificazione della bilancia è obbligatoria." });

        var ora = DateTime.UtcNow;
        TaraturaBilancia t;
        if (req.Id is int id)
        {
            t = await _ctx.TaratureBilance.FirstOrDefaultAsync(x => x.IdTaratura == id, ct)
                ?? throw new InvalidOperationException("Taratura non trovata.");
            t.DataUltimaModifica = ora;
        }
        else { t = new TaraturaBilancia { DataCreazione = ora, DataUltimaModifica = ora }; _ctx.TaratureBilance.Add(t); }

        t.IdPesiera = req.IdPesiera;
        t.BilanciaIdentificazione = req.BilanciaIdentificazione.Trim();
        t.BilanciaModello = Vuoto(req.BilanciaModello);
        t.BilanciaInterna = req.BilanciaInterna;
        t.BilanciaIdStrumentoArcLab = req.BilanciaIdStrumentoArcLab;
        t.BilanciaIdClienteArcLab = req.BilanciaIdClienteArcLab;
        t.Portata = req.Portata;
        t.UnitaFormato = req.UnitaFormato;
        t.CoefficienteSensibilitaTermica = req.CoefficienteSensibilitaTermica;
        t.MaxIncertezzaAmmessa = req.MaxIncertezzaAmmessa <= 0 ? 0.02 : req.MaxIncertezzaAmmessa;
        t.TemperaturaTaratura = req.TemperaturaTaratura;
        t.TemperaturaUtilizzo = req.TemperaturaUtilizzo;
        t.CampoTaratura = req.CampoTaratura;
        t.TempoStabilizzazione = req.TempoStabilizzazione;
        t.TempoIntervalloLettura = req.TempoIntervalloLettura;
        t.MaxVariazioneTemperatura = req.MaxVariazioneTemperatura;
        t.Tipo = Vuoto(req.Tipo);
        t.Sda = Vuoto(req.Sda);
        t.TermometroUtilizzato = Vuoto(req.TermometroUtilizzato);
        t.DataTaratura = req.DataTaratura.ToDateTime(TimeOnly.MinValue);
        t.DataScadenza = req.DataScadenza?.ToDateTime(TimeOnly.MinValue) ?? req.DataTaratura.AddYears(1).ToDateTime(TimeOnly.MinValue);
        t.RiferimentoIstruzione = Vuoto(req.RiferimentoIstruzione);
        t.Responsabile = Vuoto(req.Responsabile);
        t.Note = Vuoto(req.Note);
        t.DatiProve = JsonSerializer.Serialize(req.Prove ?? new(), JsonOpts);
        t.IdUtente = _utente.IdUtente;
        t.VersioneApplicazione = _versione.Versione;
        if (string.IsNullOrWhiteSpace(t.NumeroRapporto))
            t.NumeroRapporto = string.IsNullOrWhiteSpace(req.NumeroRapporto) ? await NumeroAsync(req.DataTaratura, ct) : req.NumeroRapporto.Trim();

        await _ctx.SaveChangesAsync(ct);
        return Json(new { t.IdTaratura, t.NumeroRapporto });
    }

    [HttpPost("api/bilance/{id:int}/rapporto")]
    public async Task<IActionResult> Rapporto(int id, [FromBody] RichiestaRapportoBilancia? req, CancellationToken ct)
    {
        var t = await _ctx.TaratureBilance.FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });
        var prove = JsonSerializer.Deserialize<ProveBilanciaDto>(t.DatiProve, JsonOpts) ?? new();
        var dati = Componi(t, prove);
        var risultato = _calcolo.Calcola(dati);

        var redattoDa =
            !string.IsNullOrWhiteSpace(req?.Responsabile) ? req!.Responsabile!.Trim()
            : !string.IsNullOrWhiteSpace(t.Responsabile) ? t.Responsabile!
            : _utente.NomeCompleto == "Utente di sviluppo" ? "" : _utente.NomeCompleto;
        if (!string.IsNullOrWhiteSpace(req?.Responsabile) && !string.Equals(req!.Responsabile!.Trim(), t.Responsabile, StringComparison.Ordinal))
        {
            t.Responsabile = req.Responsabile!.Trim(); t.DataUltimaModifica = DateTime.UtcNow; await _ctx.SaveChangesAsync(ct);
        }

        string? pesiera = null;
        var righePesiera = new List<RigaPesieraRapporto>();
        if (t.IdPesiera is int pid)
        {
            pesiera = await _ctx.Pesiere.AsNoTracking().Where(p => p.IdPesiera == pid)
                .Select(p => p.Identificazione).FirstOrDefaultAsync(ct);
            righePesiera = await _ctx.MasseCampione.AsNoTracking()
                .Where(m => m.IdPesiera == pid).OrderBy(m => m.Ordine).ThenBy(m => m.ValoreNominale)
                .Select(m => new RigaPesieraRapporto
                {
                    ValoreNominale = m.ValoreNominale,
                    ValoreConvenzionale = m.ValoreConvenzionale,
                    IncertezzaEstesa = m.IncertezzaEstesa,
                    IncertezzaTipo = m.IncertezzaTipo,
                    CertificatoLat = m.CertificatoLat,
                    DataCertificato = m.DataCertificato,
                    DataScadenza = m.DataScadenza,
                    CentroLat = m.CentroLat
                }).ToListAsync(ct);
        }

        var dr = new DatiRapportoBilancia
        {
            NumeroRapporto = t.NumeroRapporto ?? "",
            DataTaratura = DateOnly.FromDateTime(t.DataTaratura),
            DataScadenza = t.DataScadenza is DateTime ds ? DateOnly.FromDateTime(ds) : null,
            RiferimentoIstruzione = t.RiferimentoIstruzione,
            RedattoDa = redattoDa,
            Committente = t.BilanciaInterna ? "MARE.A (interno)" : "Cliente",
            BilanciaIdentificazione = t.BilanciaIdentificazione,
            BilanciaModello = t.BilanciaModello,
            Portata = t.Portata,
            UnitaFormato = t.UnitaFormato,
            CoefficienteSensibilitaTermica = t.CoefficienteSensibilitaTermica,
            TemperaturaTaratura = t.TemperaturaTaratura,
            TemperaturaUtilizzo = t.TemperaturaUtilizzo,
            CampoTaratura = t.CampoTaratura > 0 ? t.CampoTaratura : t.Portata,
            TempoStabilizzazione = t.TempoStabilizzazione,
            TempoIntervalloLettura = t.TempoIntervalloLettura,
            MaxVariazioneTemperatura = t.MaxVariazioneTemperatura,
            Tipo = t.Tipo,
            Sda = t.Sda,
            Operatore = redattoDa,
            PesieraIdentificazione = pesiera,
            Pesiera = righePesiera,
            Eccentricita = dati.Eccentricita,
            Ripetibilita = dati.Ripetibilita,
            TermometroUtilizzato = t.TermometroUtilizzato,
            Risultato = risultato
        };

        bool word = string.Equals(req?.Formato, "word", StringComparison.OrdinalIgnoreCase);
        var contenuto = word ? _generatore.GeneraWord(dr) : _generatore.GeneraPdf(dr);
        return File(contenuto, word ? MimeRtf : MimePdf, _generatore.NomeFile(dr, word ? "rtf" : "pdf"));
    }

    // ── Validazione ─────────────────────────────────────────────────
    [HttpGet("api/bilance/validazione/stato")]
    [RichiedePermesso(Permessi.BilanceValidazione)]
    public async Task<IActionResult> ValidazioneStato(CancellationToken ct)
    {
        var ultima = await _ctx.ValidazioniCalcoloBilance.AsNoTracking().OrderByDescending(v => v.DataEsecuzione).FirstOrDefaultAsync(ct);
        var scadenza = ultima?.DataEsecuzione.AddMonths(MesiValiditaValidazione);
        return Json(new
        {
            eseguitaMai = ultima is not null, idEsito = ultima?.IdEsito, validato = ultima?.Validato,
            data = ultima?.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), operatore = ultima?.Operatore,
            scaduta = ultima is null || scadenza < DateTime.UtcNow, mesiValidita = MesiValiditaValidazione, versioneSet = SetRiferimentoBilancia.Versione
        });
    }

    [HttpPost("api/bilance/validazione/esegui")]
    [RichiedePermesso(Permessi.BilanceValidazione)]
    public async Task<IActionResult> ValidazioneEsegui(CancellationToken ct)
    {
        var esito = _validazione.Esegui(_versione.Versione);
        var operatore = _utente.NomeCompleto == "Utente di sviluppo" ? null : _utente.NomeCompleto;
        var riga = new EsitoValidazioneBilancia
        {
            DataEsecuzione = esito.DataEsecuzione, Validato = esito.Validato, NumeroOk = esito.NumeroOk, NumeroKo = esito.NumeroKo,
            Tolleranza = esito.Tolleranza, VersioneSet = esito.VersioneSet, VersioneApplicazione = esito.VersioneApplicazione,
            Dettaglio = JsonSerializer.Serialize(esito, JsonOpts), IdUtente = _utente.IdUtente, Operatore = operatore
        };
        _ctx.ValidazioniCalcoloBilance.Add(riga);
        await _ctx.SaveChangesAsync(ct);
        return Json(new
        {
            riga.IdEsito, esito.Validato, esito.NumeroOk, esito.NumeroKo,
            data = esito.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            casi = esito.Casi.Select(c => new { c.Nome, c.Descrizione, c.Ok, c.Ko, c.Validato }),
            dettagli = esito.Dettagli.Select(dt => new { dt.Caso, dt.Punto, dt.Parametro, dt.Calcolato, dt.Atteso, dt.Scarto, dt.Ok })
        });
    }

    [HttpGet("api/bilance/validazione/elenco")]
    [RichiedePermesso(Permessi.BilanceValidazione)]
    public async Task<IActionResult> ValidazioneElenco(CancellationToken ct)
    {
        var righe = await _ctx.ValidazioniCalcoloBilance.AsNoTracking().OrderByDescending(v => v.DataEsecuzione).Take(50)
            .Select(v => new { v.IdEsito, v.DataEsecuzione, v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore })
            .ToListAsync(ct);
        return Json(righe.Select(v => new
        {
            v.IdEsito, data = v.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            v.Validato, v.NumeroOk, v.NumeroKo, v.VersioneSet, v.VersioneApplicazione, v.Operatore
        }));
    }

    [HttpGet("api/bilance/validazione/{id:int}/rapporto")]
    [RichiedePermesso(Permessi.BilanceValidazione)]
    public async Task<IActionResult> ValidazioneRapporto(int id, CancellationToken ct)
    {
        var riga = await _ctx.ValidazioniCalcoloBilance.AsNoTracking().FirstOrDefaultAsync(v => v.IdEsito == id, ct);
        if (riga is null) return NotFound(new { messaggio = "Validazione non trovata." });
        var esito = JsonSerializer.Deserialize<EsitoValidazioneCalcoli>(riga.Dettaglio, JsonOpts)
            ?? throw new InvalidOperationException("Dettaglio validazione non interpretabile.");
        return File(_generatoreValidazione.GeneraPdf(esito, riga.Operatore), MimePdf, _generatoreValidazione.NomeFile(esito));
    }

    // ── Analisi AI ──────────────────────────────────────────────────
    [HttpPost("api/bilance/{id:int}/analizza")]
    public async Task<IActionResult> Analizza(int id, [FromBody] RichiestaAnalisiBilancia? req, CancellationToken ct)
    {
        var t = await _ctx.TaratureBilance.FirstOrDefaultAsync(x => x.IdTaratura == id, ct);
        if (t is null) return NotFound(new { messaggio = "Taratura non trovata." });
        var prove = JsonSerializer.Deserialize<ProveBilanciaDto>(t.DatiProve, JsonOpts) ?? new();
        var dati = Componi(t, prove);
        var risultato = _calcolo.Calcola(dati);
        var anomalie = _controlli.Controlla(dati, risultato).Anomalie.ToList();

        var firma = Firma(t, risultato, anomalie);
        if (req?.Forza != true)
        {
            var esistente = await _ctx.AnalisiBilance.AsNoTracking()
                .Where(a => a.IdTaratura == id && a.Firma == firma).OrderByDescending(a => a.DataAnalisi).FirstOrDefaultAsync(ct);
            if (esistente is not null)
                return Json(new
                {
                    esistente.Esito, esistente.Testo, anomalieDeterministiche = esistente.AnomalieDeterministiche,
                    anomalie = AnomalieJson(esistente.Controlli),
                    dataAnalisi = esistente.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), daArchivio = true
                });
        }

        var prompt = ComponiPrompt(t, risultato, anomalie);
        var risposta = await _ia.InterrogaAsync(prompt, ct);
        var (esito, testo) = SeparaVerdetto(risposta.Testo);
        _ctx.AnalisiBilance.Add(new AnalisiBilancia
        {
            IdTaratura = id, DataAnalisi = DateTime.UtcNow, Firma = firma, AnomalieDeterministiche = anomalie.Count > 0,
            Controlli = JsonSerializer.Serialize(anomalie, JsonOpts), Esito = esito, Testo = testo, ModelloAi = risposta.Modello,
            TokenInput = risposta.TokenInput, TokenOutput = risposta.TokenOutput, VersioneApplicazione = _versione.Versione, IdUtente = _utente.IdUtente
        });
        await _ctx.SaveChangesAsync(ct);
        return Json(new
        {
            esito, testo, anomalieDeterministiche = anomalie.Count > 0,
            anomalie = anomalie.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }),
            dataAnalisi = DateTime.Now.ToString("dd/MM/yyyy HH:mm"), daArchivio = false
        });
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static DatiBilancia Componi(RichiestaCalcoloBilancia r) => Costruisci(
        r.Portata, r.UnitaFormato, r.CoefficienteSensibilitaTermica, r.MaxIncertezzaAmmessa,
        r.TemperaturaTaratura, r.TemperaturaUtilizzo, r.Prove ?? new());

    private static DatiBilancia Componi(TaraturaBilancia t, ProveBilanciaDto prove) => Costruisci(
        t.Portata, t.UnitaFormato, t.CoefficienteSensibilitaTermica, t.MaxIncertezzaAmmessa,
        t.TemperaturaTaratura, t.TemperaturaUtilizzo, prove);

    private static DatiBilancia Costruisci(double portata, double uf, double coeff, double maxU,
        double tTar, double tUso, ProveBilanciaDto prove) => new()
    {
        Portata = portata, UnitaFormato = uf, CoefficienteSensibilitaTermica = coeff,
        MaxIncertezzaAmmessa = maxU <= 0 ? 0.02 : maxU, TemperaturaTaratura = tTar, TemperaturaUtilizzo = tUso,
        Eccentricita = new ProvaEccentricita
        {
            CaricoNominale = prove.Eccentricita?.CaricoNominale ?? 0,
            ValoreConvenzionale = prove.Eccentricita?.ValoreConvenzionale ?? 0,
            Letture = prove.Eccentricita?.Letture ?? new List<double>()
        },
        Ripetibilita = prove.Ripetibilita ?? new List<double>(),
        Linearita = (prove.Linearita ?? new()).Select(p => new PuntoLinearita
        {
            CaricoNominale = p.CaricoNominale, ValoreConvenzionale = p.ValoreConvenzionale,
            Lettura = p.Lettura, IncertezzaMassa = p.IncertezzaMassa, MpeMassa = p.MpeMassa
        }).ToList()
    };

    private async Task<string> NumeroAsync(DateOnly data, CancellationToken ct)
    {
        var conteggio = await _ctx.TaratureBilance.CountAsync(t => t.DataTaratura.Year == data.Year, ct);
        return $"B{data:yy}-{conteggio + 1:000}";
    }

    private static object AnomalieJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<object>();
        var lista = JsonSerializer.Deserialize<List<AnomaliaTaratura>>(json, JsonOpts) ?? new();
        return lista.Select(a => new { livello = a.Livello.ToString(), a.Punto, a.Messaggio }).ToList();
    }

    private static string Firma(TaraturaBilancia t, RisultatoBilancia r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(t.BilanciaIdentificazione).Append('|').Append(t.Portata.ToString("F4", ci)).Append('|')
          .Append(t.UnitaFormato.ToString("F6", ci)).Append('|').Append(t.DatiProve).Append('|').Append(anomalie.Count)
          .Append('|').Append(r.IncertezzaEstesaMax.ToString("F8", ci));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static (string? Esito, string Testo) SeparaVerdetto(string risposta)
    {
        var righe = (risposta ?? "").Replace("\r", "").Split('\n');
        return (righe.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r))?.Trim(), (risposta ?? "").Trim());
    }

    private static string ComponiPrompt(TaraturaBilancia t, RisultatoBilancia r, List<AnomaliaTaratura> anomalie)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine("Sei un metrologo esperto di taratura di bilance analitiche secondo EURAMET cg-18.");
        sb.AppendLine("Ti fornisco i risultati di una taratura. Valuta se sono PLAUSIBILI e segnala ERRORI GROSSOLANI");
        sb.AppendLine("(letture o valori di massa incoerenti, errori d'indicazione anomali, eccentricità non superata).");
        sb.AppendLine("NON rifare i calcoli. Rispondi in italiano: prima riga verdetto sintetico, poi 2-4 punti brevi.");
        sb.AppendLine();
        sb.AppendLine($"Bilancia: {t.BilanciaIdentificazione}, portata {t.Portata.ToString("0.###", ci)} g, risoluzione {t.UnitaFormato.ToString("0.####", ci)} g.");
        sb.AppendLine($"Eccentricità: scostamento max {r.EccentricitaScostamentoMax.ToString("0.#####", ci)} g ({(r.EccentricitaConforme ? "conforme" : "non conforme")}).");
        sb.AppendLine($"Ripetibilità: scarto tipo {r.RipetibilitaScartoTipo.ToString("0.######", ci)} g.");
        sb.AppendLine("Errori d'indicazione per carico:");
        foreach (var p in r.Punti)
            sb.AppendLine($"- {p.CaricoNominale.ToString("0.#", ci)} g: errore {p.Errore.ToString("0.#####", ci)} g, U {p.IncertezzaEstesa.ToString("0.#####", ci)} g");
        if (anomalie.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Controlli automatici:");
            foreach (var a in anomalie) sb.AppendLine($"- [{a.Livello}] {(string.IsNullOrEmpty(a.Punto) ? "" : a.Punto + ": ")}{a.Messaggio}");
        }
        return sb.ToString();
    }

    private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s!.Trim();
}

// ── DTO ─────────────────────────────────────────────────────────────
public record MassaDto
{
    public double ValoreNominale { get; init; }
    public double ValoreConvenzionale { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double IncertezzaTipo { get; init; }
    public double Mpe { get; init; }
    public string? ClasseOiml { get; init; }
    public string? CertificatoLat { get; init; }
    public DateTime? DataCertificato { get; init; }
    public DateTime? DataScadenza { get; init; }
    public int? CentroLat { get; init; }
}

public record RichiestaSalvaPesiera
{
    public int? Id { get; init; }
    public string Identificazione { get; init; } = "";
    public string? Note { get; init; }
    public List<MassaDto>? Masse { get; init; }
}

public record EccentricitaDto
{
    public double CaricoNominale { get; init; }
    public double ValoreConvenzionale { get; init; }
    public List<double>? Letture { get; init; }
}

public record PuntoLinearitaDto
{
    public double CaricoNominale { get; init; }
    public double ValoreConvenzionale { get; init; }
    public double Lettura { get; init; }
    public double IncertezzaMassa { get; init; }
    public double MpeMassa { get; init; }
}

public record ProveBilanciaDto
{
    public EccentricitaDto? Eccentricita { get; init; }
    public List<double>? Ripetibilita { get; init; }
    public List<PuntoLinearitaDto>? Linearita { get; init; }
}

public record RichiestaCalcoloBilancia
{
    public double Portata { get; init; }
    public double UnitaFormato { get; init; }
    public double CoefficienteSensibilitaTermica { get; init; }
    public double MaxIncertezzaAmmessa { get; init; } = 0.02;
    public double TemperaturaTaratura { get; init; }
    public double TemperaturaUtilizzo { get; init; }
    public ProveBilanciaDto? Prove { get; init; }
}

public record RichiestaSalvaBilancia
{
    public int? Id { get; init; }
    public int? IdPesiera { get; init; }
    public string BilanciaIdentificazione { get; init; } = "";
    public string? BilanciaModello { get; init; }
    public bool BilanciaInterna { get; init; }
    public int? BilanciaIdStrumentoArcLab { get; init; }
    public int? BilanciaIdClienteArcLab { get; init; }
    public double Portata { get; init; }
    public double UnitaFormato { get; init; }
    public double CoefficienteSensibilitaTermica { get; init; }
    public double MaxIncertezzaAmmessa { get; init; } = 0.02;
    public double TemperaturaTaratura { get; init; }
    public double TemperaturaUtilizzo { get; init; }
    public double CampoTaratura { get; init; }
    public double TempoStabilizzazione { get; init; }
    public double TempoIntervalloLettura { get; init; }
    public double MaxVariazioneTemperatura { get; init; }
    public string? Tipo { get; init; }
    public string? Sda { get; init; }
    public string? TermometroUtilizzato { get; init; }
    public string? NumeroRapporto { get; init; }
    public DateOnly DataTaratura { get; init; }
    public DateOnly? DataScadenza { get; init; }
    public string? RiferimentoIstruzione { get; init; }
    public string? Responsabile { get; init; }
    public string? Note { get; init; }
    public ProveBilanciaDto? Prove { get; init; }
}

public record RichiestaRapportoBilancia
{
    public string? Formato { get; init; }
    public string? Responsabile { get; init; }
}

public record RichiestaAnalisiBilancia
{
    public bool Forza { get; init; }
}
