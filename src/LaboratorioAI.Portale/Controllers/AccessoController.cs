using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Portale.Autenticazione;
using LaboratorioAI.Portale.Servizi;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

[ApiController]
[Route("api/accesso")]
public class AccessoController : ControllerBase
{
    private readonly IServizioAccessoPortale _accesso;
    private readonly IContestoSessione _sessione;

    public AccessoController(IServizioAccessoPortale accesso, IContestoSessione sessione)
    {
        _accesso = accesso;
        _sessione = sessione;
    }

    /// <summary>
    /// Verifica le credenziali e apre una sessione.
    ///
    /// Le credenziali viaggiano esclusivamente nel corpo della richiesta:
    /// mai in query string, dove finirebbero nei log del server web,
    /// nella cronologia del browser e nelle intestazioni di provenienza.
    /// </summary>
    [HttpPost("entra")]
    public async Task<IActionResult> Entra(
        [FromBody] RichiestaAccesso richiesta, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(richiesta.CodiceContabilita) ||
            string.IsNullOrWhiteSpace(richiesta.Credenziale))
        {
            return BadRequest(new { messaggio = "Indicare codice cliente e credenziale." });
        }

        var (esito, sessione, tokenVerifica) = await _accesso.AccediAsync(
            richiesta.CodiceContabilita,
            richiesta.Credenziale,
            richiesta.TokenDispositivo,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.FirstOrDefault(),
            ct);

        // 18/11/2026: credenziali corrette da un browser nuovo → codice inviato all'email del cliente.
        if (esito == EsitoAccesso.Riuscito && tokenVerifica is not null)
            return Ok(new
            {
                Verifica = true,
                TokenVerifica = tokenVerifica,
                Messaggio = "Per sicurezza, al primo accesso da questo browser abbiamo inviato un codice all'indirizzo email " +
                            "del cliente registrato presso MARE.A. Inseriscilo qui sotto (vale 15 minuti)."
            });

        return esito switch
        {
            EsitoAccesso.Riuscito => Ok(new
            {
                sessione!.Token,
                sessione.RagioneSociale,
                sessione.Operatore,
                Scadenza = sessione.Scadenza.ToLocalTime()
            }),

            EsitoAccesso.TroppiTentativi => StatusCode(429, new
            {
                messaggio = "Troppi tentativi di accesso. Riprovare fra qualche minuto."
            }),

            // Risposta identica sia che il codice non esista sia che la
            // credenziale sia errata: distinguere i due casi permetterebbe
            // di verificare quali codici cliente siano validi.
            _ => Unauthorized(new
            {
                messaggio = "Codice cliente o credenziale non corretti."
            })
        };
    }

    /// <summary>Conferma del codice ricevuto per email al primo accesso da un browser nuovo (18/11/2026).</summary>
    [HttpPost("verifica")]
    public async Task<IActionResult> Verifica([FromBody] RichiestaVerifica richiesta, CancellationToken ct)
    {
        var (esito, sessione, tokenDispositivo, messaggio) = await _accesso.ConfermaVerificaAsync(
            richiesta.TokenVerifica ?? "", richiesta.Codice ?? "", richiesta.RicordaBrowser,
            HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.FirstOrDefault(), ct);
        if (esito != EsitoAccesso.Riuscito || sessione is null)
            return Unauthorized(new { messaggio });
        return Ok(new
        {
            sessione.Token,
            sessione.RagioneSociale,
            sessione.Operatore,
            Scadenza = sessione.Scadenza.ToLocalTime(),
            TokenDispositivo = tokenDispositivo
        });
    }

    [HttpPost("esci")]
    public async Task<IActionResult> Esci(CancellationToken ct)
    {
        var token = Request.Headers[MiddlewareSessione.IntestazioneToken].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(token))
            await _accesso.RevocaAsync(token, ct);

        return Ok();
    }

    /// <summary>Stato della sessione corrente, per il ripristino dopo un ricaricamento.</summary>
    [HttpGet("stato")]
    public IActionResult Stato()
    {
        if (!_sessione.Autenticato)
            return Unauthorized(new { messaggio = "Nessuna sessione attiva." });

        return Ok(new
        {
            _sessione.Sessione!.RagioneSociale,
            _sessione.Operatore,
            Scadenza = _sessione.Sessione.Scadenza.ToLocalTime()
        });
    }

    /// <remarks>
    /// Il nome del campo credenziale è fra quelli intercettati dal filtro
    /// di log, così il valore non compare nemmeno in caso di eccezione.
    /// </remarks>
    public record RichiestaAccesso(string CodiceContabilita, string Credenziale, string? TokenDispositivo = null);

    /// <remarks>I nomi contengono «token»: il filtro dei log li omette.</remarks>
    public record RichiestaVerifica(string? TokenVerifica, string? Codice, bool RicordaBrowser);
}
