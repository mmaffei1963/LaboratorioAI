namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Invio email via SMTP (account MARE.A configurato in appsettings, sezione "Smtp"; la password
/// non è mai nel file di configurazione, solo in user-secrets/variabile d'ambiente).
/// </summary>
public interface IServizioEmail
{
    /// <summary>
    /// Invia un'email HTML ai destinatari indicati. Lancia <see cref="InvalidOperationException"/>
    /// se la configurazione SMTP è incompleta o non ci sono destinatari; lascia propagare eccezioni
    /// di invio (connessione, autenticazione) perché il chiamante decida come gestirle.
    /// </summary>
    Task InviaAsync(IEnumerable<string> destinatari, string oggetto, string corpoHtml, CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="InviaAsync(IEnumerable{string}, string, string, CancellationToken)"/>, con allegati; restituisce il
    /// Message-ID del messaggio inviato, da conservare come evidenza (10/10/2026). <paramref name="rispondiA"/>: indirizzi
    /// a cui vanno le risposte (intestazione Reply-To), perché il mittente è una casella no-reply.
    /// <paramref name="copia"/>: destinatari in copia (Cc), 06/11/2026.
    /// </summary>
    Task<string?> InviaConAllegatiAsync(IEnumerable<string> destinatari, string oggetto, string corpoHtml,
        IEnumerable<AllegatoEmail> allegati, CancellationToken ct = default, IEnumerable<string>? rispondiA = null,
        IEnumerable<string>? copia = null);
}

/// <summary>Allegato di un'email.</summary>
public sealed record AllegatoEmail(string NomeFile, byte[] Contenuto, string TipoMime);
