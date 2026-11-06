using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Accredia;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Riceve i risultati del controllo ACCREDIA raccolti da <c>strumenti/AgenteAccredia</c>, un
/// piccolo programma eseguito su un PC con accesso diretto a certificati.accredia.it (il server
/// di produzione non ci riesce — vedi il commento in cima a <see cref="ControlloAccreditamentiEsterni"/>).
///
/// Non è un controller per utenti: nessun cookie di sessione, nessun <c>[RichiedePermesso]</c>.
/// L'unica protezione è un segreto condiviso nell'intestazione "X-Agente-Segreto", verificato a
/// mano su ogni azione (stesso schema già usato dal relay rimosso). Chiamato solo da un programma
/// non presidiato: niente da mostrare a un utente, quindi niente da proteggere con un login.
/// </summary>
[AllowAnonymous] // la FallbackPolicy globale (RequireAuthenticatedUser) altrimenti blocca
                 // la richiesta con un redirect al login PRIMA che il controller possa
                 // controllare il segreto condiviso — vedi il commento della classe sopra.
public class AccrediaAgenteController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly IServizioAccreditamentiEsterni _accreditamenti;
    private readonly IEstrattoreTestoPdf _estrattoreTesto;
    private readonly IConfiguration _config;
    private readonly ILogger<ServizioAccreditamentiEsterni> _logServizio;
    private readonly ControlloAccreditamentiEsterni _controllo;
    private readonly IServizioAudit _audit;

    public AccrediaAgenteController(
        ContestoLaboratorioAI db,
        IServizioAccreditamentiEsterni accreditamenti,
        IEstrattoreTestoPdf estrattoreTesto,
        IConfiguration config,
        ILogger<ServizioAccreditamentiEsterni> logServizio,
        ControlloAccreditamentiEsterni controllo,
        IServizioAudit audit)
    {
        _db = db;
        _accreditamenti = accreditamenti;
        _estrattoreTesto = estrattoreTesto;
        _config = config;
        _logServizio = logServizio;
        _controllo = controllo;
        _audit = audit;
    }

    /// <summary>Elenco dei laboratori attivi da controllare: numero/sede, così l'agente sa chi interrogare su ACCREDIA.</summary>
    [HttpGet("api/accredia-agente/laboratori")]
    public async Task<IActionResult> Laboratori(CancellationToken ct)
    {
        if (!SegretoValido()) return Unauthorized();

        var elenco = await _accreditamenti.ElencoAsync(ct);
        return Json(elenco.Select(a => new
        {
            a.IdAccreditamento,
            a.NumeroAccreditamento,
            a.Sede,
            a.NomeLaboratorio
        }));
    }

    /// <summary>
    /// Riceve l'esito del controllo (già eseguito dall'agente) per tutti i laboratori e lo
    /// elabora con la stessa identica logica del controllo diretto: confronto con l'ultima
    /// revisione nota, salvataggio del PDF ed estrazione testo per le novità, riepilogo ed
    /// eventuale email. Costruisce al volo un <see cref="ServizioAccreditamentiEsterni"/> con un
    /// <see cref="ClientAccrediaDaImportazione"/> al posto del client HTTP normale, così la logica
    /// di <c>ControllaTuttiAsync</c> non viene duplicata.
    /// </summary>
    [HttpPost("api/accredia-agente/importa")]
    public async Task<IActionResult> Importa([FromBody] List<VoceImportazioneAccredia> voci, CancellationToken ct)
    {
        if (!SegretoValido()) return Unauthorized();

        var client = new ClientAccrediaDaImportazione(voci);
        var servizio = new ServizioAccreditamentiEsterni(_db, client, _estrattoreTesto, _config, _logServizio);

        var risultati = (await servizio.ControllaTuttiAsync(ct)).ToList();
        var riepilogo = await _controllo.ElaboraRisultatiAsync(risultati, ct);

        // Stato nella banca dati ACCREDIA (accreditato / sospeso / non trovato), dall'08/10/2026.
        // Con le prove sospese (righe con asterisco) dei laboratori con sospensione attiva, dall'08/10/2026.
        var esitoStato = await new ServizioStatoAccreditamenti(_db).AggiornaAsync(voci, ct);
        var variazioni = esitoStato.Variazioni;
        var mailStato = await _controllo.InviaEmailVariazioniStatoAsync(esitoStato, ct);

        await _audit.RegistraAsync(
            AreaFunzionale.LaboratoriEsterni,
            "accreditamento.controllo-da-agente",
            EsitoOperazione.Successo,
            dettaglio: new
            {
                riepilogo, LaboratoriRicevuti = voci.Count,
                StatiRicevuti = voci.Count(v => v.StatoAccreditamento is not null),
                VariazioniStato = variazioni.Select(v => new { v.Laboratorio, v.Numero, v.Sede, Prima = v.Prima?.ToString(), Ora = v.Ora.ToString() }),
                ProveSospeseRicevute = voci.Where(v => v.ProveSospese is not null).Sum(v => v.ProveSospese!.Count),
                VariazioniProveSospese = esitoStato.VariazioniProve.Select(v => new
                    { v.Laboratorio, v.Numero, v.Sede, Esito = v.Nuova ? "sospesa" : "sospensione terminata", v.Matrice, v.Prova, v.Norma }),
                MailStato = mailStato
            },
            ct: ct);

        return Json(riepilogo);
    }

    private bool SegretoValido()
    {
        var atteso = _config["AccreditamentiEsterni:AgenteSegreto"];
        return !string.IsNullOrWhiteSpace(atteso)
            && Request.Headers["X-Agente-Segreto"].ToString() == atteso;
    }
}
