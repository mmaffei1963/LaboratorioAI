using System.Security.Claims;
using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Autenticazione;

public enum EsitoAccesso
{
    Riuscito,
    CredenzialiNonValide,
    Bloccato,
    ServizioNonAttivo
}

public sealed record RisultatoAccesso(EsitoAccesso Esito, ClaimsPrincipal? Utente = null, int MinutiDiBlocco = 0);

/// <summary>
/// Verifica e-mail e password. Regole: messaggi uguali per «e-mail sconosciuta» e «password sbagliata»,
/// tempo di risposta simile, blocco temporaneo dopo troppi tentativi, ogni esito scritto nel registro attività.
/// Lavora con il contesto del gestore perché prima dell'accesso non si sa a quale azienda appartiene l'utente.
/// </summary>
public sealed class ServizioAccesso
{
    private static readonly PasswordHasher<Utente> _hasher = new();

    // Impronta di una password inesistente: si verifica anche quando l'e-mail non c'è, per non svelarlo dai tempi.
    private static readonly string _improntaFittizia = _hasher.HashPassword(new Utente(), Guid.NewGuid().ToString("N"));

    private readonly FabbricaContestoGestore _fabbrica;
    private readonly ILogger<ServizioAccesso> _log;

    public ServizioAccesso(FabbricaContestoGestore fabbrica, ILogger<ServizioAccesso> log)
    {
        _fabbrica = fabbrica;
        _log = log;
    }

    public static string Impronta(Utente utente, string password) => _hasher.HashPassword(utente, password);

    public async Task<RisultatoAccesso> AccediAsync(string? email, string? password, string? indirizzoIp, CancellationToken ct = default)
    {
        var emailPulita = (email ?? "").Trim();
        await using var db = _fabbrica.Crea();
        var adesso = DateTime.UtcNow;

        var utente = emailPulita.Length == 0
            ? null
            : await db.Utenti.FirstOrDefaultAsync(u => u.Email == emailPulita, ct);

        if (utente is null || !utente.Attivo || string.IsNullOrEmpty(utente.ImprontaPassword))
        {
            _hasher.VerifyHashedPassword(new Utente(), _improntaFittizia, password ?? "");
            await ScriviAuditAsync(db, null, utente?.IdUtente, "AccessoNegato", "Utente non riconosciuto o non attivo", indirizzoIp, adesso, ct);
            return new RisultatoAccesso(EsitoAccesso.CredenzialiNonValide);
        }

        if (PoliticaBloccoAccesso.EBloccato(utente.BloccatoFinoUtc, adesso))
        {
            await ScriviAuditAsync(db, utente.IdAzienda, utente.IdUtente, "AccessoBloccato", "Tentativo durante il blocco temporaneo", indirizzoIp, adesso, ct);
            return new RisultatoAccesso(EsitoAccesso.Bloccato, null, PoliticaBloccoAccesso.MinutiRimasti(utente.BloccatoFinoUtc, adesso));
        }

        var verifica = _hasher.VerifyHashedPassword(utente, utente.ImprontaPassword, password ?? "");
        if (verifica == PasswordVerificationResult.Failed)
        {
            var (tentativi, bloccatoFino) = PoliticaBloccoAccesso.DopoFallimento(utente.TentativiFalliti, adesso);
            utente.TentativiFalliti = tentativi;
            utente.BloccatoFinoUtc = bloccatoFino;
            await ScriviAuditAsync(db, utente.IdAzienda, utente.IdUtente,
                bloccatoFino is null ? "AccessoNegato" : "AccessoBloccato",
                bloccatoFino is null ? "Password non corretta" : "Troppi tentativi: accesso sospeso", indirizzoIp, adesso, ct);
            _log.LogWarning("Accesso negato per l'utente {IdUtente}.", utente.IdUtente);
            return bloccatoFino is null
                ? new RisultatoAccesso(EsitoAccesso.CredenzialiNonValide)
                : new RisultatoAccesso(EsitoAccesso.Bloccato, null, PoliticaBloccoAccesso.MinutiRimasti(bloccatoFino, adesso));
        }

        // Password giusta. Il servizio dell'azienda deve essere attivo (o in chiusura: sola lettura per scaricare i dati).
        if (!utente.EGestore)
        {
            var azienda = utente.IdAzienda is { } id ? await db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == id, ct) : null;
            if (azienda is null)
            {
                await ScriviAuditAsync(db, utente.IdAzienda, utente.IdUtente, "AccessoNegato", "Utente senza azienda", indirizzoIp, adesso, ct);
                return new RisultatoAccesso(EsitoAccesso.CredenzialiNonValide);
            }
            if (azienda.Stato is StatoServizio.Sospeso or StatoServizio.Chiuso)
            {
                await ScriviAuditAsync(db, utente.IdAzienda, utente.IdUtente, "AccessoNegato", $"Servizio non attivo ({azienda.Stato})", indirizzoIp, adesso, ct);
                return new RisultatoAccesso(EsitoAccesso.ServizioNonAttivo);
            }
        }

        if (verifica == PasswordVerificationResult.SuccessRehashNeeded)
            utente.ImprontaPassword = _hasher.HashPassword(utente, password!);

        (utente.TentativiFalliti, utente.BloccatoFinoUtc) = PoliticaBloccoAccesso.DopoSuccesso();
        utente.UltimoAccesso = adesso;
        await ScriviAuditAsync(db, utente.IdAzienda, utente.IdUtente, "Accesso", "Accesso riuscito", indirizzoIp, adesso, ct);

        return new RisultatoAccesso(EsitoAccesso.Riuscito, CreaIdentita(utente));
    }

    public static ClaimsPrincipal CreaIdentita(Utente utente)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, utente.IdUtente.ToString()),
            new(ClaimTypes.Name, utente.Nome),
            new(ClaimTypes.Email, utente.Email),
            new(Rivendicazioni.IdUtente, utente.IdUtente.ToString()),
            new(Rivendicazioni.Gestore, utente.EGestore ? "true" : "false"),
        };
        if (utente.IdAzienda is { } idAzienda)
            claims.Add(new Claim(Rivendicazioni.IdAzienda, idAzienda.ToString()));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>Scrive la voce nel registro e salva insieme alle altre modifiche dell'utente.</summary>
    private static async Task ScriviAuditAsync(ContestoTracciabilita db, int? idAzienda, int? idUtente, string azione,
        string dettaglio, string? ip, DateTime adesso, CancellationToken ct)
    {
        db.Audit.Add(new VoceAudit
        {
            IdAzienda = idAzienda,
            IdUtente = idUtente,
            Azione = azione,
            Entita = "Utente",
            IdEntita = idUtente?.ToString(),
            Dopo = dettaglio,
            IndirizzoIp = ip,
            DataOra = adesso
        });
        await db.SaveChangesAsync(ct);
    }
}
