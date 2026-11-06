using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Documenti;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

[RichiedePermesso(Permessi.LaboratoriEsterniConsulta)]
public class LaboratoriEsterniController : Controller
{
    private readonly IRepositoryLaboratoriEsterni _laboratori;
    private readonly IEsportatoreLaboratoriEsterni _esportatore;
    private readonly IServizioAudit _audit;
    private readonly IServizioAccreditamentiEsterni _accreditamenti;
    private readonly IContestoUtente _utente;
    private readonly IConfiguration _config;
    private readonly ControlloAccreditamentiEsterni _controlloAccreditamenti;
    private readonly IServizioVerificaMetodiEsterni _verificaMetodi;

    public LaboratoriEsterniController(
        IRepositoryLaboratoriEsterni laboratori,
        IEsportatoreLaboratoriEsterni esportatore,
        IServizioAudit audit,
        IServizioAccreditamentiEsterni accreditamenti,
        IContestoUtente utente,
        IConfiguration config,
        ControlloAccreditamentiEsterni controlloAccreditamenti,
        IServizioVerificaMetodiEsterni verificaMetodi)
    {
        _laboratori = laboratori;
        _esportatore = esportatore;
        _audit = audit;
        _accreditamenti = accreditamenti;
        _utente = utente;
        _config = config;
        _controlloAccreditamenti = controlloAccreditamenti;
        _verificaMetodi = verificaMetodi;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Laboratori Esterni";
        return View();
    }

    [HttpGet("api/laboratori-esterni/elenco")]
    public async Task<IActionResult> Elenco(CancellationToken ct) =>
        Json(await _laboratori.LaboratoriAsync(ct));

    [HttpPost("api/laboratori-esterni/interroga")]
    public async Task<IActionResult> Interroga(
        [FromBody] CriteriLaboratoriEsterni criteri, CancellationToken ct)
    {
        var esito = await _laboratori.InterrogaAsync(criteri, ct);

        return Json(new
        {
            esito.TotaleProve,
            esito.Certificate,
            esito.InCorso,
            esito.LaboratoriDistinti,
            esito.Troncato,
            Riepilogo = esito.Riepilogo.Select(r => new
            {
                r.LaboratorioEsterno,
                r.Matrice,
                r.DenominazioneRiga,
                r.RiferimentoNormativo,
                r.NumeroProve,
                r.NumeroCertificate
            }),
            Prove = esito.Prove.Select(p => new
            {
                p.IdCampione,
                p.NumeroCampione,
                p.RagioneSociale,
                p.NomeInsediamento,
                DataRicezione = p.DataRicezione?.ToString("dd/MM/yyyy"),
                DataAnalisi   = p.DataAnalisi?.ToString("dd/MM/yyyy"),
                p.DenominazioneCampione,
                p.Matrice,
                p.DenominazioneRiga,
                p.RiferimentoNormativo,
                p.LaboratorioEsterno,
                p.AccreditatoEsterno,
                Stato = p.Stato.ToString()
            })
        });
    }

    /// <summary>Esporta l'interrogazione corrente in formato Excel.</summary>
    [HttpPost("api/laboratori-esterni/esporta")]
    [RichiedePermesso(Permessi.LaboratoriEsterniEsporta)]
    public async Task<IActionResult> Esporta(
        [FromBody] CriteriLaboratoriEsterni criteri, CancellationToken ct)
    {
        // L'esportazione non è limitata come la visualizzazione a schermo
        var criteriEstesi = criteri with { Limite = 20000 };

        var esito = await _laboratori.InterrogaAsync(criteriEstesi, ct);
        var contenuto = _esportatore.Genera(esito, criteriEstesi);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "esportazione.excel",
            EsitoOperazione.Successo,
            dettaglio: new
            {
                Dal = criteri.DataDa.ToString("yyyy-MM-dd"),
                Al = criteri.DataA.ToString("yyyy-MM-dd"),
                Prove = esito.TotaleProve
            },
            ct: ct);

        return File(
            contenuto,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _esportatore.NomeFile(criteriEstesi));
    }

    // ── Accreditamento ACCREDIA laboratori esterni ─────────────────────────

    /// <summary>
    /// Avvia subito un controllo completo (stessa logica del job notturno) invece di aspettare
    /// le 3 di notte — comodo per verificare che tutto funzioni dopo una modifica. Non aspetta il
    /// completamento sulla stessa richiesta: ACCREDIA può essere lenta e con più laboratori il
    /// tempo totale può superare il timeout dell'edge davanti al sito (Cloudflare, errore 524).
    /// La pagina segue l'avanzamento interrogando <see cref="AccreditamentiTestStato"/>.
    /// </summary>
    [HttpPost("api/laboratori-esterni/accreditamenti/test")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public IActionResult AccreditamentiTest()
    {
        var avviato = _controlloAccreditamenti.AvviaControlloManuale();
        return Json(new { avviato });
    }

    /// <summary>
    /// Stato del controllo manuale avviato con <see cref="AccreditamentiTest"/>: la pagina lo
    /// interroga a intervalli finché non risulta concluso. La prima chiamata che osserva un
    /// completamento non ancora notificato registra l'esito nell'audit trail (con l'utente della
    /// richiesta corrente, dato che il controllo gira in background senza un utente associato).
    /// </summary>
    [HttpGet("api/laboratori-esterni/accreditamenti/test/stato")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> AccreditamentiTestStato(CancellationToken ct)
    {
        var stato = _controlloAccreditamenti.OttieniStatoControlloManuale();

        if (_controlloAccreditamenti.ProvaConsumaCompletamentoManualeDaNotificare())
        {
            var erroreEsecuzione = stato.Stato == StatoEsecuzioneControlloManuale.Errore;

            await _audit.RegistraAsync(
                AreaFunzionale.LaboratoriEsterni,
                "accreditamento.controllo-manuale",
                erroreEsecuzione ? EsitoOperazione.Errore : EsitoOperazione.Successo,
                dettaglio: erroreEsecuzione ? new { Errore = stato.UltimoErrore } : (object?)stato.UltimoRiepilogo,
                ct: ct);
        }

        return Json(new
        {
            Stato = stato.Stato.ToString(),
            stato.UltimoRiepilogo,
            UltimaEsecuzione = stato.UltimaEsecuzioneUtc?.ToString("o"),
            stato.UltimoErrore,
            AvviatoAlle = stato.AvviatoAlleUtc?.ToString("o")
        });
    }

    [HttpGet("api/laboratori-esterni/accreditamenti/elenco")]
    public async Task<IActionResult> AccreditamentiElenco(CancellationToken ct) =>
        Json((await _accreditamenti.ElencoAsync(ct)).Select(a => new
        {
            a.IdAccreditamento,
            a.IdLabEsternoArcLab,
            a.NomeLaboratorio,
            a.NumeroAccreditamento,
            a.Sede,
            a.UltimaRevisioneConfermata,
            a.Note,
            StatoAccredia = a.StatoAccredia?.ToString(),
            DataStatoAccredia = a.DataStatoAccredia?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        }));

    /// <summary>
    /// Accreditamenti da sistemare (sola lettura): metodi verificati in LabAI con esito diverso dal flag di ArcLab,
    /// numeri ACCREDIA di ArcLab diversi da quelli monitorati, controllo ACCREDIA fermo da troppi giorni.
    /// </summary>
    [HttpGet("api/laboratori-esterni/accreditamenti/da-sistemare")]
    public async Task<IActionResult> AccreditamentiDaSistemare(
        [FromServices] LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioAnomalieAccreditamenti servizio, CancellationToken ct)
    {
        var a = await servizio.CalcolaAsync(ct);
        return Json(new
        {
            a.Totale,
            Discrepanze = a.Discrepanze.Select(d => new
            {
                d.IdAccreditamento, d.Laboratorio, d.IdLabEstMetodo, d.CodMetodo, d.RiferimentoNormativo, d.NomeParametro,
                Verifica = d.Verifica.ToString(), d.FlagArcLab,
                DataVerifica = d.DataVerificaUtc?.ToLocalTime().ToString("dd/MM/yyyy"), d.Note
            }),
            NumeriDiversi = a.NumeriDiversi,
            StatiNonValidi = a.StatiNonValidi.Select(s => new
            {
                s.IdAccreditamento, s.Laboratorio, s.Numero, s.Sede, Stato = s.Stato.ToString(),
                DataStato = s.DataStatoUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                Prove = s.Prove.Select(p => new
                {
                    p.Matrice, p.Prova, p.Norma, p.Tecnica,
                    Rilevata = p.DataRilevazioneUtc.ToLocalTime().ToString("dd/MM/yyyy"),
                    Metodi = p.Metodi.Select(m => new { m.CodMetodo, m.NomeParametro, m.RiferimentoNormativo })
                })
            }),
            Controllo = new
            {
                UltimoControllo = a.Controllo.UltimoControlloUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                a.Controllo.Modo, a.Controllo.Fermo, a.Controllo.GiorniMassimi
            }
        });
    }

    [HttpGet("api/laboratori-esterni/accreditamenti/da-confermare")]
    public async Task<IActionResult> AccreditamentiDaConfermare(CancellationToken ct) =>
        Json((await _accreditamenti.RevisioniDaConfermareAsync(ct)).Select(r => new
        {
            r.IdRevisione,
            r.IdAccreditamento,
            r.NumeroRevisione,
            DataEmissione = r.DataEmissione?.ToString("dd/MM/yyyy"),
            DataRilevazione = r.DataRilevazione.ToString("dd/MM/yyyy HH:mm"),
            r.HashFileSha256
        }));

    [HttpGet("api/laboratori-esterni/accreditamenti/{idAccreditamento:int}/storico")]
    public async Task<IActionResult> AccreditamentiStorico(int idAccreditamento, CancellationToken ct) =>
        Json((await _accreditamenti.StoricoRevisioniAsync(idAccreditamento, ct)).Select(r => new
        {
            r.IdRevisione,
            r.NumeroRevisione,
            DataEmissione = r.DataEmissione?.ToString("dd/MM/yyyy"),
            DataRilevazione = r.DataRilevazione.ToString("dd/MM/yyyy HH:mm"),
            r.Confermata,
            DataConferma = r.DataConferma?.ToString("dd/MM/yyyy HH:mm"),
            r.NoteConferma
        }));

    /// <summary>Scarica il PDF di una revisione (rilevata o confermata) per la verifica manuale.</summary>
    [HttpGet("api/laboratori-esterni/accreditamenti/revisioni/{idRevisione:int}/pdf")]
    public async Task<IActionResult> AccreditamentiScaricaPdf(int idRevisione, CancellationToken ct)
    {
        // Lo storico non espone un metodo "per id revisione": cerchiamo fra tutti i laboratori.
        var elenco = await _accreditamenti.ElencoAsync(ct);
        foreach (var lab in elenco)
        {
            var storico = await _accreditamenti.StoricoRevisioniAsync(lab.IdAccreditamento, ct);
            var revisione = storico.FirstOrDefault(r => r.IdRevisione == idRevisione);
            if (revisione is null || revisione.PercorsoFile is null)
                continue;

            var cartellaBase = _config["AccreditamentiEsterni:CartellaArchivio"];
            if (string.IsNullOrWhiteSpace(cartellaBase))
                return Problem("Cartella archivio accreditamenti non configurata.");

            var percorsoCompleto = Path.Combine(cartellaBase, revisione.PercorsoFile);
            if (!System.IO.File.Exists(percorsoCompleto))
                return NotFound();

            var bytes = await System.IO.File.ReadAllBytesAsync(percorsoCompleto, ct);
            return File(bytes, "application/pdf",
                $"{lab.NomeLaboratorio}_Revisione{revisione.NumeroRevisione}.pdf");
        }

        return NotFound();
    }

    // ── Gestione dei laboratori monitorati (08/10/2026) ─────────────

    /// <summary>Tutti i laboratori monitorati, anche disattivati, per la maschera di gestione.</summary>
    [HttpGet("api/laboratori-esterni/accreditamenti/tutti")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> AccreditamentiTutti(
        [FromServices] LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioLaboratoriMonitorati servizio, CancellationToken ct) =>
        Json((await servizio.TuttiAsync(ct)).Select(a => new
        {
            a.IdAccreditamento, a.IdLabEsternoArcLab, a.NomeLaboratorio, a.NumeroAccreditamento, a.Sede,
            a.UltimaRevisioneConfermata, a.Note, a.Attivo,
            StatoAccredia = a.StatoAccredia?.ToString(),
            DataStatoAccredia = a.DataStatoAccredia?.ToLocalTime().ToString("dd/MM/yyyy HH:mm")
        }));

    /// <summary>Aggiunge o modifica un laboratorio monitorato (numero, sede, note, attivo). Solo database di LabAI.</summary>
    [HttpPost("api/laboratori-esterni/accreditamenti/laboratorio")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> AccreditamentiSalvaLaboratorio(
        [FromBody] LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.DatiLaboratorioMonitorato dati,
        [FromServices] LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioLaboratoriMonitorati servizio, CancellationToken ct)
    {
        var (lab, errore) = await servizio.SalvaAsync(dati, _utente.IdUtente, ct);
        if (lab is null) return BadRequest(new { messaggio = errore });
        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            dati.IdAccreditamento is null ? "accreditamento.laboratorio.aggiunto" : "accreditamento.laboratorio.modificato",
            EsitoOperazione.Successo,
            tipoEntita: nameof(AccreditamentoLabEsterno),
            idEntita: lab.IdAccreditamento.ToString(),
            dettaglio: new { lab.NomeLaboratorio, lab.IdLabEsternoArcLab, lab.NumeroAccreditamento, lab.Sede, lab.Attivo, lab.Note },
            ct: ct);
        return Json(new { ok = true, lab.IdAccreditamento });
    }

    /// <summary>Conferma una revisione rilevata come elenco prove ufficiale del laboratorio.</summary>
    [HttpPost("api/laboratori-esterni/accreditamenti/revisioni/{idRevisione:int}/conferma")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> AccreditamentiConferma(
        int idRevisione, [FromBody] ConfermaRevisioneRichiesta? richiesta, CancellationToken ct)
    {
        var idUtente = _utente.IdUtente
            ?? throw new InvalidOperationException("Utente non autenticato.");

        await _accreditamenti.ConfermaRevisioneAsync(idRevisione, idUtente, richiesta?.Note, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "accreditamento.conferma",
            EsitoOperazione.Successo,
            tipoEntita: nameof(RevisioneAccreditamentoEsterno),
            idEntita: idRevisione.ToString(),
            dettaglio: new { richiesta?.Note },
            ct: ct);

        return Json(new { ok = true });
    }

    public record ConfermaRevisioneRichiesta(string? Note);

    /// <summary>
    /// Ri-estrae il testo dai PDF già archiviati per le revisioni con TestoEstratto ancora nullo
    /// (rilevate prima che fosse disponibile il fallback pdftotext). Non scarica nulla da ACCREDIA:
    /// rilegge solo i file già presenti nella cartella archivio. Comando una tantum, richiamabile
    /// più volte senza effetti collaterali finché ci sono revisioni da riprocessare.
    /// </summary>
    [HttpPost("api/laboratori-esterni/accreditamenti/riestrai-testo-mancante")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> AccreditamentiRiestraiTestoMancante(CancellationToken ct)
    {
        var risultati = await _accreditamenti.RiestraiTestoMancanteAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "accreditamento.riestrazione-testo",
            EsitoOperazione.Successo,
            dettaglio: risultati,
            ct: ct);

        return Json(risultati.Select(r => new
        {
            r.IdRevisione,
            r.NomeLaboratorio,
            r.NumeroRevisione,
            Esito = r.Esito.ToString(),
            r.CaratteriEstratti,
            r.MessaggioErrore
        }));
    }

    /// <summary>
    /// Individua le revisioni con PDF mancante o corrotto (download interrotto: file assente,
    /// troppo piccolo, o senza intestazione "%PDF") e lo riscarica da ACCREDIA. Aggiorna solo
    /// file/hash/testo estratto della revisione: non tocca conferma, note o storico.
    /// </summary>
    [HttpPost("api/laboratori-esterni/accreditamenti/riscarica-pdf-corrotti")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> AccreditamentiRiscaricaPdfCorrotti(CancellationToken ct)
    {
        var risultati = await _accreditamenti.RiscaricaPdfCorrottiAsync(ct);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "accreditamento.riscarico-pdf-corrotti",
            EsitoOperazione.Successo,
            dettaglio: risultati,
            ct: ct);

        return Json(risultati.Select(r => new
        {
            r.IdRevisione,
            r.NomeLaboratorio,
            r.NumeroRevisione,
            Esito = r.Esito.ToString(),
            r.CaratteriEstratti,
            r.MessaggioErrore
        }));
    }

    // ── Verifica metodi vs elenco prove ACCREDIA ────────────────────────────

    [HttpGet("api/laboratori-esterni/accreditamenti/{idAccreditamento:int}/metodi")]
    public async Task<IActionResult> MetodiDaVerificare(int idAccreditamento, CancellationToken ct)
    {
        var metodi = await _verificaMetodi.ElencoAsync(idAccreditamento, ct);

        // Revisione confermata di questo laboratorio: serve per il link "Apri PDF" nel pannello.
        var storico = await _accreditamenti.StoricoRevisioniAsync(idAccreditamento, ct);
        var confermata = storico.FirstOrDefault(r => r.Confermata);

        return Json(new
        {
            IdRevisioneConfermata = confermata?.IdRevisione,
            NumeroRevisioneConfermata = confermata?.NumeroRevisione,
            Metodi = metodi.Select(m => new
            {
                m.IdLabEstMetodo,
                m.CodMetodo,
                m.RiferimentoNormativo,
                m.NomeParametro,
                m.AccreditatoArcLab,
                Stato = m.Stato.ToString(),
                m.Note,
                m.SuggerimentoTesto,
                m.PunteggioSuggerimento
            })
        });
    }

    /// <summary>Classifica un metodo (Accreditato/NonAccreditato/DaVerificare) dopo il controllo manuale sul PDF.</summary>
    [HttpPost("api/laboratori-esterni/accreditamenti/{idAccreditamento:int}/metodi/{idLabEstMetodo:int}/verifica")]
    [RichiedePermesso(Permessi.LaboratoriEsterniAccreditamento)]
    public async Task<IActionResult> VerificaMetodo(
        int idAccreditamento, int idLabEstMetodo,
        [FromBody] VerificaMetodoRichiesta richiesta, CancellationToken ct)
    {
        var idUtente = _utente.IdUtente
            ?? throw new InvalidOperationException("Utente non autenticato.");

        if (!Enum.TryParse<StatoVerificaMetodo>(richiesta.Stato, out var stato))
            return BadRequest(new { messaggio = $"Stato non valido: {richiesta.Stato}" });

        await _verificaMetodi.SalvaVerificaAsync(idAccreditamento, idLabEstMetodo, stato, idUtente, richiesta.Note, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "accreditamento.verifica-metodo",
            EsitoOperazione.Successo,
            tipoEntita: nameof(VerificaMetodoAccreditamento),
            idEntita: $"{idAccreditamento}/{idLabEstMetodo}",
            dettaglio: new { richiesta.Stato, richiesta.Note },
            ct: ct);

        return Json(new { ok = true });
    }

    public record VerificaMetodoRichiesta(string Stato, string? Note);
}
