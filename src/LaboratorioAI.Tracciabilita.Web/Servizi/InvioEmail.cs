using System.Net;
using System.Net.Mail;

namespace LaboratorioAI.Tracciabilita.Web.Servizi;

public sealed record EsitoInvio(bool Riuscito, string? Errore);

/// <summary>Invio di una e-mail. Chi chiama non sa quale servizio c'è dietro.</summary>
public interface IInvioEmail
{
    Task<EsitoInvio> InviaAsync(string destinatario, string oggetto, string corpoHtml, string? nomeMittente, string? rispondiA, CancellationToken ct);
}

/// <summary>
/// Posta in uscita via SMTP (Brevo: smtp-relay.brevo.com, porta 587). Impostazioni nella sezione «Posta» di appsettings:
/// Host, Porta, Utente, MittenteEmail, MittenteNome. La password SMTP sta nei segreti utente (mai nel codice):
/// <c>dotnet user-secrets set "Posta:Password" "…"</c>. Il mittente deve essere un indirizzo verificato.
/// </summary>
public sealed class InvioEmailSmtp : IInvioEmail
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<InvioEmailSmtp> _log;

    public InvioEmailSmtp(IConfiguration cfg, ILogger<InvioEmailSmtp> log)
    {
        _cfg = cfg; _log = log;
    }

    public async Task<EsitoInvio> InviaAsync(string destinatario, string oggetto, string corpoHtml, string? nomeMittente, string? rispondiA, CancellationToken ct)
    {
        var host = _cfg["Posta:Host"];
        var utente = _cfg["Posta:Utente"];
        var password = _cfg["Posta:Password"];
        var mittente = _cfg["Posta:MittenteEmail"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(utente) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(mittente))
            return new EsitoInvio(false, "L'invio e-mail non è ancora configurato (server, utente, password o indirizzo mittente mancanti).");
        var porta = int.TryParse(_cfg["Posta:Porta"], out var p) ? p : 587;

        try
        {
            using var messaggio = new MailMessage
            {
                From = new MailAddress(mittente, string.IsNullOrWhiteSpace(nomeMittente) ? (_cfg["Posta:MittenteNome"] ?? "Tracciato") : nomeMittente),
                Subject = oggetto, Body = corpoHtml, IsBodyHtml = true
            };
            messaggio.To.Add(destinatario);
            if (!string.IsNullOrWhiteSpace(rispondiA)) messaggio.ReplyToList.Add(rispondiA);

            using var client = new SmtpClient(host, porta)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(utente, password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20000
            };
            await client.SendMailAsync(messaggio, ct);
            return new EsitoInvio(true, null);
        }
        catch (SmtpException e)
        {
            _log.LogWarning(e, "Invio e-mail rifiutato dal server ({Stato})", e.StatusCode);
            return new EsitoInvio(false, e.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.ClientNotPermitted or SmtpStatusCode.GeneralFailure
                ? "Il server di posta ha rifiutato l'invio: controlla utente, password SMTP e mittente verificato."
                : $"Invio non riuscito ({e.StatusCode}).");
        }
        catch (Exception e) when (e is InvalidOperationException or FormatException or TaskCanceledException or OperationCanceledException)
        {
            _log.LogWarning(e, "Invio e-mail non riuscito");
            return new EsitoInvio(false, "Non riesco a inviare l'e-mail: controlla gli indirizzi e riprova.");
        }
    }
}
