using LaboratorioAI.Core.Servizi;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MailKit.Security;
using MimeKit;

namespace LaboratorioAI.Dati.Email;

/// <inheritdoc />
/// <remarks>In LaboratorioAI.Dati dal 15/10/2026, così la usano sia la Web app sia il Portale (codice OTP delle Condizioni).</remarks>
public class ServizioEmailSmtp : IServizioEmail
{
    private readonly IConfiguration _config;
    private readonly ILogger<ServizioEmailSmtp> _log;

    public ServizioEmailSmtp(IConfiguration config, ILogger<ServizioEmailSmtp> log)
    {
        _config = config;
        _log = log;
    }

    public Task InviaAsync(IEnumerable<string> destinatari, string oggetto, string corpoHtml, CancellationToken ct = default) =>
        InviaConAllegatiAsync(destinatari, oggetto, corpoHtml, Array.Empty<AllegatoEmail>(), ct);

    public async Task<string?> InviaConAllegatiAsync(IEnumerable<string> destinatari, string oggetto, string corpoHtml,
        IEnumerable<AllegatoEmail> allegati, CancellationToken ct = default, IEnumerable<string>? rispondiA = null,
        IEnumerable<string>? copia = null)
    {
        var smtp = _config.GetSection("Smtp");
        var host = smtp["Host"];
        var porta = int.TryParse(smtp["Porta"], out var p) ? p : 587;   // senza GetValue: in Dati non c'è il pacchetto Binder
        var utente = smtp["Utente"];
        var password = smtp["Password"];
        // Con provider come Brevo il login SMTP (Utente) è spesso diverso dall'indirizzo che i
        // destinatari vedono come mittente, che deve essere un sender verificato sul provider:
        // MittenteEmail lo separa; se assente si usa Utente (caso Office365, dove coincidevano).
        var mittenteEmail = string.IsNullOrWhiteSpace(smtp["MittenteEmail"]) ? utente : smtp["MittenteEmail"];
        var nomeMittente = string.IsNullOrWhiteSpace(smtp["NomeMittente"]) ? "Laboratorio AI" : smtp["NomeMittente"]!;

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(utente) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "Configurazione SMTP incompleta (Smtp:Host / Smtp:Utente / Smtp:Password): impossibile inviare l'email. " +
                "In sviluppo la password si imposta con 'dotnet user-secrets set Smtp:Password ...', in produzione con la variabile d'ambiente Smtp__Password.");

        var dest = destinatari.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct().ToList();
        if (dest.Count == 0)
            throw new InvalidOperationException("Nessun destinatario configurato per questa email.");

        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(nomeMittente, mittenteEmail!));
        foreach (var d in dest) msg.To.Add(MailboxAddress.Parse(d));
        foreach (var c in (copia ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase).Where(x => !dest.Contains(x, StringComparer.OrdinalIgnoreCase)))
            msg.Cc.Add(MailboxAddress.Parse(c));
        foreach (var r in (rispondiA ?? Array.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            msg.ReplyTo.Add(MailboxAddress.Parse(r));
        msg.Subject = oggetto;
        var corpo = new BodyBuilder { HtmlBody = corpoHtml };
        foreach (var a in allegati) corpo.Attachments.Add(a.NomeFile, a.Contenuto, ContentType.Parse(a.TipoMime));
        msg.Body = corpo.ToMessageBody();
        msg.MessageId = MimeKit.Utils.MimeUtils.GenerateMessageId();

        // Porta 465 = SSL/TLS implicito fin dall'inizio della connessione (es. Aruba
        // smtps.aruba.it:465); qualunque altra porta (tipicamente 587) = STARTTLS, cioè
        // connessione in chiaro che passa a TLS dopo l'handshake iniziale.
        var opzioniSicurezza = porta == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(host, porta, opzioniSicurezza, ct);
            await client.AuthenticateAsync(utente, password, ct);
            await client.SendAsync(msg, ct);
        }
        finally
        {
            if (client.IsConnected) await client.DisconnectAsync(true, ct);
        }

        _log.LogInformation("Email inviata a {Destinatari}: {Oggetto}", string.Join(", ", dest), oggetto);
        return msg.MessageId;
    }
}
