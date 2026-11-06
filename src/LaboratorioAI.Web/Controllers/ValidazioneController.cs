using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.ValidazioneVisualizza)]
public class ValidazioneController : Controller
{
    private readonly IRepositoryCampioni _campioni;
    private readonly IServizioAnalisi _analisi;
    private readonly IServizioAudit _audit;

    public ValidazioneController(IRepositoryCampioni campioni, IServizioAnalisi analisi, IServizioAudit audit)
    {
        _campioni = campioni;
        _analisi = analisi;
        _audit = audit;
    }

    /// <param name="campione">IdCampione da aprire subito (collegamento dalla maschera di ArcLab).</param>
    public IActionResult Index(int? campione)
    {
        ViewData["Titolo"] = "Da Validare";
        ViewData["CampioneIniziale"] = campione;
        return View();
    }

    // ═══════════════════════════════════════════════════════════════
    // API
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Elenco campioni da validare, arricchito con lo stato dell'archivio analisi.</summary>
    [HttpGet("api/validazione/campioni")]
    public async Task<IActionResult> Campioni(CancellationToken ct)
    {
        var campioni = await _campioni.DaValidareAsync(ct);
        var archivio = await _analisi.ArchivioAsync(
            campioni.Select(c => c.IdCampione).ToList(), ct);

        var risultato = campioni.Select(c => new
        {
            c.IdCampione,
            c.NumeroCampione,
            c.RagioneSociale,
            c.NomeInsediamento,
            c.DenominazioneCampione,
            c.Matrice,
            DataRicezione = c.DataRicezione?.ToString("dd/MM/yyyy"),
            c.GiorniInLaboratorio,
            c.NumeroProve,
            c.Operatori,
            Anzianita = c.Anzianita.ToString(),
            Analisi = archivio.TryGetValue(c.IdCampione, out var a)
                ? new
                {
                    a.IdAnalisi,
                    Data = a.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    Giudizio = a.Giudizio.ToString(),
                    a.EseguitaDa
                }
                : null
        });

        return Json(risultato);
    }

    /// <summary>
    /// Colori e segnalazioni dei limiti normativi (fase 1b): confronto con le schede limite validate e controlli
    /// tecnici. Separato dal giudizio AI. In cache 2 minuti; ?aggiorna=true ricalcola.
    /// </summary>
    [HttpGet("api/validazione/segnalazioni")]
    public async Task<IActionResult> Segnalazioni([FromServices] LaboratorioAI.Dati.Limiti.ServizioSegnalazioniLimiti servizio,
        [FromServices] ILogger<ValidazioneController> log, bool aggiorna, CancellationToken ct)
    {
        try
        {
            var esiti = await servizio.CalcolaAsync(aggiorna, ct);
            return Json(esiti.ToDictionary(e => e.Key.ToString(), e => new
            {
                // 26/10/2026: verde senza nessun confronto con una scheda = «Nessun limite di legge» (azzurro).
                Colore = e.Value.Colore == LaboratorioAI.Core.Limiti.ColoreRilievo.Verde && !e.Value.Confrontato
                    ? "azzurro" : e.Value.Colore.ToString().ToLowerInvariant(),
                Rilievi = e.Value.Rilievi.Select(r => new
                {
                    Colore = r.Colore.ToString().ToLowerInvariant(), r.Regola, r.Testo, r.IdProva, r.IdParametro, r.CodiceScheda
                })
            }));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Segnalazioni limiti non calcolate");
            return StatusCode(500, new { messaggio = "Segnalazioni dei limiti non disponibili: " + ex.Message });
        }
    }

    // ── Validazione da LaboratorioAI: giudizio + chiusura analisi da riportare in ArcLab (18/10/2026) ──

    private static object Riga(LaboratorioAI.Core.Entita.GiudizioDaRiportare g) => new
    {
        id = g.IdGiudizioDaRiportare, g.IdCampione, g.IdGiudizio, g.CodGiudizio, g.GiudizioTesto, g.Automatico,
        stato = g.Stato.ToString(), g.ValidatoDa, DataValidazione = g.DataValidazione.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
        DataRiportato = g.DataRiportato?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), g.ColoreLimiti
    };

    [HttpGet("api/validazione/campione/{idCampione:int}/giudizio")]
    public async Task<IActionResult> Giudizio(int idCampione, [FromServices] LaboratorioAI.Dati.Validazione.ServizioValidazioneGiudizio servizio,
        [FromServices] IContestoUtente utente, CancellationToken ct)
    {
        var s = await servizio.SchedaAsync(idCampione, ct);
        return Json(new
        {
            arcLab = s.ArcLab is null ? null : new
            {
                s.ArcLab.AnalisiChiusa, s.ArcLab.GiudizioConf, s.ArcLab.NumeroCampione, s.ArcLab.ProveNonVerificate,
                giudizi = s.ArcLab.Giudizi.Select(g => new { g.IdGiudizio, g.CodGiudizio, g.Giudizio })
            },
            proposti = s.Proposti.Select(g => new { g.IdGiudizio, g.CodGiudizio, g.Giudizio, g.GiudizioConformita }),
            validazione = s.Validazione is null ? null : Riga(s.Validazione),
            s.ColoreLimiti, s.MotivoBlocco,
            puoValidare = utente.HaPermesso(Permessi.ValidazioneConferma),
            senzaGiudizio = LaboratorioAI.Core.Validazione.RegoleGiudizio.SenzaGiudizio
        });
    }

    [HttpGet("api/validazione/validati")]
    public async Task<IActionResult> Validati([FromServices] LaboratorioAI.Dati.Validazione.ServizioValidazioneGiudizio servizio, CancellationToken ct)
    {
        try { return Json((await servizio.ElencoAsync(ct)).Select(Riga)); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return StatusCode(500, new { messaggio = "Stato delle validazioni non disponibile: " + ex.Message }); }
    }

    public record RichiestaValida(int IdCampione, int? IdGiudizio, bool ConfermaColore);

    [HttpPost("api/validazione/valida")]
    [RichiedePermesso(Permessi.ValidazioneConferma)]
    public async Task<IActionResult> Valida([FromBody] RichiestaValida r, [FromServices] LaboratorioAI.Dati.Validazione.ServizioValidazioneGiudizio servizio,
        [FromServices] IContestoUtente utente, CancellationToken ct)
    {
        var op = new LaboratorioAI.Dati.Limiti.OperatoreLimiti(utente.IdUtente ?? 0, utente.NomeCompleto ?? utente.Email ?? "?");
        var esito = await servizio.ValidaAsync(r.IdCampione, r.IdGiudizio, r.ConfermaColore, op, ct);
        if (esito.Riuscita)
            await _audit.RegistraAsync(AreaFunzionale.Validazione, "campione.validato", EsitoOperazione.Successo,
                tipoEntita: "Campione", idEntita: r.IdCampione.ToString(), dettaglio: new { r.IdGiudizio, r.ConfermaColore, esito.Messaggio }, ct: ct);
        return Json(new { riuscita = esito.Riuscita, messaggio = esito.Messaggio, chiedeConferma = esito.ChiedeConferma });
    }

    public record RichiestaAnnulla(int Id, string? Nota);

    [HttpPost("api/validazione/annulla")]
    [RichiedePermesso(Permessi.ValidazioneConferma)]
    public async Task<IActionResult> Annulla([FromBody] RichiestaAnnulla r, [FromServices] LaboratorioAI.Dati.Validazione.ServizioValidazioneGiudizio servizio,
        [FromServices] IContestoUtente utente, CancellationToken ct)
    {
        var op = new LaboratorioAI.Dati.Limiti.OperatoreLimiti(utente.IdUtente ?? 0, utente.NomeCompleto ?? utente.Email ?? "?");
        var esito = await servizio.AnnullaAsync(r.Id, r.Nota, op, ct);
        if (esito.Riuscita)
            await _audit.RegistraAsync(AreaFunzionale.Validazione, "campione.validazione-annullata", EsitoOperazione.Successo,
                tipoEntita: "GiudizioDaRiportare", idEntita: r.Id.ToString(), dettaglio: new { r.Nota }, ct: ct);
        return Json(new { riuscita = esito.Riuscita, messaggio = esito.Messaggio });
    }

    /// <summary>Dettaglio del campione con le prove.</summary>
    [HttpGet("api/validazione/campione/{idCampione:int}")]
    public async Task<IActionResult> Campione(int idCampione, CancellationToken ct)
    {
        var campione = await _campioni.CompletoAsync(idCampione, ct);
        if (campione is null) return NotFound(new { messaggio = "Campione non trovato." });

        var ultima = await _analisi.UltimaAsync(idCampione, ct);

        return Json(new
        {
            Campione = new
            {
                campione.Campione.IdCampione,
                campione.Campione.NumeroCampione,
                campione.Campione.RagioneSociale,
                campione.Campione.NomeInsediamento,
                campione.Campione.DenominazioneCampione,
                campione.Campione.Matrice,
                campione.Campione.Controllo,
                campione.Campione.LuogoPrelievo,
                campione.Campione.PuntoPrelievo,
                DataRicezione = campione.Campione.DataRicezione?.ToString("dd/MM/yyyy"),
                DataPrelievo  = campione.Campione.DataPrelievo?.ToString("dd/MM/yyyy"),
                campione.Campione.GiorniInLaboratorio
            },
            Prove = campione.Prove.Select(p => new
            {
                p.IdProva,
                Parametro = p.DenominazioneRiga ?? p.Parametro,
                p.RiferimentoNormativo,
                Valore = p.ValoreFormattato,
                Intervallo = p.IntervalloFormattato,
                p.UnitaMisura,
                p.VerificaValore,
                p.LaboratorioEsterno,
                p.Accreditato,
                p.Note
            }),
            AnalisiArchiviata = ultima is null ? null : new
            {
                ultima.IdAnalisi,
                Data = ultima.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                Giudizio = ultima.Giudizio.ToString(),
                ultima.EseguitaDa
            }
        });
    }

    /// <summary>Esegue o recupera l'analisi AI di un campione.</summary>
    [HttpPost("api/validazione/analizza")]
    [RichiedePermesso(Permessi.ValidazioneAnalizza)]
    public async Task<IActionResult> Analizza(
        [FromBody] RichiestaAnalisi richiesta, CancellationToken ct)
    {
        try
        {
            var esito = await _analisi.AnalizzaAsync(richiesta, ct);

            if (!esito.DaArchivio)
                await _audit.RegistraAsync(
                    AreaFunzionale.Validazione, "analisi.eseguita", EsitoOperazione.Successo,
                    tipoEntita: "Analisi", idEntita: esito.IdAnalisi.ToString(),
                    dettaglio: new { Giudizio = esito.Giudizio.ToString(), esito.NumeroProve }, ct: ct);
            return Json(new
            {
                esito.IdAnalisi,
                esito.Testo,
                Giudizio = esito.Giudizio.ToString(),
                Data = esito.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                esito.NumeroProve,
                esito.DaArchivio,
                esito.EseguitaDa
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { messaggio = ex.Message });
        }
    }

    /// <summary>Analisi massiva su più campioni.</summary>
    [HttpPost("api/validazione/analizza-massivo")]
    [RichiedePermesso(Permessi.ValidazioneAnalizzaLotto)]
    public async Task<IActionResult> AnalizzaMassivo(
        [FromBody] RichiestaMassiva richiesta, CancellationToken ct)
    {
        if (richiesta.IdCampioni.Count == 0)
            return BadRequest(new { messaggio = "Nessun campione selezionato." });

        var esito = await _analisi.AnalizzaMassivoAsync(richiesta.IdCampioni, null, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.Validazione, "analisi.massiva.eseguita", EsitoOperazione.Successo,
            dettaglio: new { Campioni = richiesta.IdCampioni.Count, esito.Conformi, esito.NonConformi, esito.Borderline, esito.NonRiusciti }, ct: ct);
        return Json(new
        {
            esito.Conformi,
            esito.NonConformi,
            esito.Borderline,
            esito.NonRiusciti,
            Righe = esito.Righe.Select(r => new
            {
                r.IdCampione,
                r.NumeroCampione,
                r.RagioneSociale,
                r.DenominazioneCampione,
                Giudizio = r.Giudizio.ToString(),
                r.DaArchivio,
                r.Errore
            })
        });
    }

    /// <summary>Storico delle analisi di un campione.</summary>
    [HttpGet("api/validazione/storico/{idCampione:int}")]
    public async Task<IActionResult> Storico(int idCampione, CancellationToken ct)
    {
        var storico = await _analisi.StoricoAsync(idCampione, ct);

        return Json(storico.Select(s => new
        {
            s.IdAnalisi,
            Data = s.DataAnalisi.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            Giudizio = s.Giudizio.ToString(),
            s.EseguitaDa
        }));
    }

    public record RichiestaMassiva(List<int> IdCampioni);
}
