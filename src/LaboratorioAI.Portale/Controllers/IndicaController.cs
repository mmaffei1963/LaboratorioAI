using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Controllers;

/// <summary>
/// Pagina «Indica il Firmatario» (11/10/2026), raggiunta con il link dell'invito mandato all'e-mail aziendale del
/// cliente. Nessun accesso con credenziali: il link è un codice casuale di 256 bit di cui il database conserva solo
/// l'impronta. Vale la prima risposta; LaboratorioAI Web designa il Firmatario e gli invia il link di approvazione.
/// Ogni passo va nel registro delle evidenze.
/// </summary>
[Route("Indica")]
public class IndicaController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRegistroEvidenzeApprovazioni _registro;

    public IndicaController(ContestoLaboratorioAI ctx, IRegistroEvidenzeApprovazioni registro)
    {
        _ctx = ctx; _registro = registro;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Agente => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    private async Task<InvitoFirmatario?> TrovaAsync(string codice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codice) || codice.Length < 30 || codice.Length > 100) return null;
        var impronta = CatenaEvidenze.Sha256(codice.Trim());
        var i = await _ctx.InvitiFirmatario.FirstOrDefaultAsync(x => x.ImprontaCodiceLink == impronta, ct);
        if (i is null || i.ScadenzaLink is null || i.ScadenzaLink < DateTime.UtcNow) return null;
        return i.Stato is StatoInvitoFirmatario.Annullato or StatoInvitoFirmatario.DaInviare ? null : i;
    }

    private Task<EventoApprovazione> RegistraAsync(InvitoFirmatario i, string tipo, object? dati, CancellationToken ct) =>
        _registro.AggiungiAsync(tipo, null, i.IdClienteArcLab, dati, "Cliente (pagina Indica il Firmatario)", Ip, Agente, ct);

    [HttpGet("{codice}")]
    public async Task<IActionResult> Index(string codice, CancellationToken ct)
    {
        var i = await TrovaAsync(codice, ct);
        if (i is null) return View("~/Views/Firma/NonValido.cshtml");
        if (i.Stato == StatoInvitoFirmatario.Inviato)
        {
            i.Stato = StatoInvitoFirmatario.Aperto;
            i.DataApertura = DateTime.UtcNow;
            i.DataUltimaModifica = i.DataApertura.Value;
        }
        if (i.Stato == StatoInvitoFirmatario.Aperto)
            await RegistraAsync(i, "invito.aperto", new { i.IdInvitoFirmatario }, ct);
        return View(new ModelloIndica(codice.Trim(), i.ClienteRagioneSociale ?? "", i.ClientePartitaIva,
            i.Stato >= StatoInvitoFirmatario.Indicato,
            i.Stato >= StatoInvitoFirmatario.Indicato ? $"{i.NomeIndicato} {i.CognomeIndicato} ({i.EmailIndicata})" : null,
            i.DataIndicazione?.ToLocalTime()));
    }

    public sealed record RichiestaIndica(string? Nome, string? Cognome, string? Qualifica, string? Email, bool ConfermaGenerica);

    [HttpPost("{codice}")]
    public async Task<IActionResult> Conferma(string codice, [FromBody] RichiestaIndica req, CancellationToken ct)
    {
        var i = await TrovaAsync(codice, ct);
        if (i is null) return BadRequest(new { messaggio = "Il link non è più valido. Scriveteci a info@mare-a.com o chiamate lo 0541 830442." });
        if (i.Stato >= StatoInvitoFirmatario.Indicato)
            return Conflict(new { messaggio = $"Il Firmatario è già stato indicato: {i.NomeIndicato} {i.CognomeIndicato} ({i.EmailIndicata}). Per cambiarlo scriveteci rispondendo all'e-mail." });

        string T(string? s, int max) => (s ?? "").Trim() is var t && t.Length > max ? t[..max] : (s ?? "").Trim();
        var nome = T(req.Nome, 100); var cognome = T(req.Cognome, 100); var qualifica = T(req.Qualifica, 150); var email = T(req.Email, 256);
        if (nome.Length == 0 || cognome.Length == 0 || qualifica.Length == 0)
            return BadRequest(new { messaggio = "Indicate nome, cognome e qualifica." });
        if (!IndirizziEmail.Formato.IsMatch(email)) return BadRequest(new { messaggio = "L'indirizzo e-mail non è valido." });
        if (IndirizziEmail.Generica(email) && !req.ConfermaGenerica)
            return Ok(new { richiedeConferma = true, messaggio = $"«{email}» sembra una casella condivisa. Il link per l'approvazione è personale: è meglio un indirizzo nominativo della persona indicata. Volete usarlo comunque?" });

        var ora = DateTime.UtcNow;
        i.NomeIndicato = nome; i.CognomeIndicato = cognome; i.QualificaIndicata = qualifica; i.EmailIndicata = email;
        i.DataIndicazione = ora; i.Stato = StatoInvitoFirmatario.Indicato; i.DataUltimaModifica = ora;
        await RegistraAsync(i, "firmatario.indicato", new
        {
            i.IdInvitoFirmatario, i.ClienteRagioneSociale, i.ClientePartitaIva, Nome = nome, Cognome = cognome, Qualifica = qualifica, Email = email,
            EmailGenerica = IndirizziEmail.Generica(email)
        }, ct);
        return Ok(new { messaggio = $"Grazie. Entro pochi minuti {nome} {cognome} riceverà all'indirizzo {email} il link per approvare le Condizioni Generali." });
    }
}

public sealed record ModelloIndica(string Codice, string RagioneSociale, string? PartitaIva, bool GiaIndicato, string? Indicato, DateTime? DataIndicazione);
