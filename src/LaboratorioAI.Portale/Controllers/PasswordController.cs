using LaboratorioAI.Portale.Autenticazione;
using LaboratorioAI.Portale.Servizi;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Password del portale (17/11/2026): cambio dall'area riservata, recupero con codice via email (dalla pagina di accesso,
/// senza sessione), stato e ripristino delle credenziali originali per l'operatore. Le password viaggiano solo nel corpo.
/// </summary>
[ApiController]
[Route("api/password")]
public class PasswordController : ControllerBase
{
    private readonly ServizioPasswordPortale _servizio;
    private readonly IContestoSessione _sessione;

    public PasswordController(ServizioPasswordPortale servizio, IContestoSessione sessione)
    {
        _servizio = servizio;
        _sessione = sessione;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Agente => Request.Headers.UserAgent.FirstOrDefault();

    private IActionResult Risposta(ServizioPasswordPortale.Esito e) =>
        e.Riuscito ? Ok(new { messaggio = e.Messaggio }) : BadRequest(new { messaggio = e.Messaggio });

    [HttpPost("cambia")]
    [RichiedeSessione]
    public async Task<IActionResult> Cambia([FromBody] RichiestaCambio r, CancellationToken ct)
    {
        if (_sessione.Operatore || _sessione.IdCliente is not int id)
            return BadRequest(new { messaggio = "Il cambio password è per gli accessi dei clienti." });
        return Risposta(await _servizio.CambiaAsync(id, r.PasswordAttuale, r.PasswordNuova, r.PasswordRipetuta, Ip, Agente, ct));
    }

    [HttpPost("recupero/richiedi")]
    public async Task<IActionResult> Richiedi([FromBody] RichiestaCodice r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.CodiceContabilita))
            return BadRequest(new { messaggio = "Indica il codice cliente." });
        await _servizio.RichiediCodiceAsync(r.CodiceContabilita, Ip, Agente, ct);
        // Stessa risposta in ogni caso: non rivela quali codici cliente esistono o hanno un'email.
        return Ok(new
        {
            messaggio = "Se il codice cliente è corretto e ha un indirizzo email registrato presso MARE.A, riceverai a breve un codice " +
                        "valido 15 minuti. Se non arriva, controlla la posta indesiderata o contatta MARE.A al numero 0541 830442."
        });
    }

    [HttpPost("recupero/conferma")]
    public async Task<IActionResult> Conferma([FromBody] RichiestaConferma r, CancellationToken ct) =>
        Risposta(await _servizio.ConfermaRecuperoAsync(r.CodiceContabilita, r.TokenRecupero, r.PasswordNuova, r.PasswordRipetuta, Ip, Agente, ct));

    [HttpGet("stato")]
    [RichiedeSessione(SoloOperatore = true)]
    public async Task<IActionResult> Stato(int idCliente, CancellationToken ct)
    {
        var (personale, dal) = await _servizio.StatoAsync(idCliente, ct);
        return Ok(new { personale, dal = dal?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") });
    }

    [HttpPost("ripristina")]
    [RichiedeSessione(SoloOperatore = true)]
    public async Task<IActionResult> Ripristina([FromBody] RichiestaRipristino r, CancellationToken ct) =>
        Risposta(await _servizio.RipristinaAsync(r.IdCliente, Ip, Agente, ct));

    // I nomi dei campi contengono «password»: il filtro dei log li omette.
    public record RichiestaCambio(string? PasswordAttuale, string? PasswordNuova, string? PasswordRipetuta);
    public record RichiestaCodice(string? CodiceContabilita);
    public record RichiestaConferma(string? CodiceContabilita, string? TokenRecupero, string? PasswordNuova, string? PasswordRipetuta);
    public record RichiestaRipristino(int IdCliente);
}
