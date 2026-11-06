using System.Security.Claims;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Allinea l'utente applicativo all'identità Entra ID al momento dell'accesso
/// e arricchisce l'identità con i claim di permesso.
///
/// 11/10/2026 (ruoli per mestiere): chi entra per la prima volta riceve il ruolo «Consultazione», non più
/// «Amministratore». Se l'Amministratore lo aveva registrato in anticipo con la sua e-mail, al primo accesso il
/// collegamento con l'account Microsoft si completa e la persona trova già i suoi ruoli. I permessi vengono poi
/// riletti dal database a ogni richiesta (<see cref="TrasformazionePermessi"/>), così una modifica vale subito.
/// </summary>
public class ServizioProvisioningUtente
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioPermessiUtente _permessi;
    private readonly ILogger<ServizioProvisioningUtente> _log;

    public ServizioProvisioningUtente(
        ContestoLaboratorioAI ctx,
        ServizioPermessiUtente permessi,
        ILogger<ServizioProvisioningUtente> log)
    {
        _ctx = ctx;
        _permessi = permessi;
        _log = log;
    }

    public async Task AllineaAsync(ClaimsIdentity identita, CancellationToken ct = default)
    {
        var objectId = LeggiObjectId(identita);
        if (objectId is null)
        {
            _log.LogWarning("Token privo del claim oid: impossibile allineare l'utente.");
            return;
        }

        var email = identita.FindFirst("preferred_username")?.Value
                    ?? identita.FindFirst(ClaimTypes.Upn)?.Value
                    ?? identita.FindFirst(ClaimTypes.Email)?.Value
                    ?? string.Empty;

        var nome = identita.FindFirst("name")?.Value ?? email;

        var utente = await _ctx.Utenti
            .FirstOrDefaultAsync(u => u.ObjectIdEntra == objectId, ct);

        if (utente is null && email.Length > 0)
        {
            // Persona registrata in anticipo dall'Amministratore: si collega al primo accesso.
            var chiave = email.Trim().ToLower();
            utente = await _ctx.Utenti.FirstOrDefaultAsync(u => u.ObjectIdEntra == null && u.Email.ToLower() == chiave, ct);
            if (utente is not null)
            {
                utente.ObjectIdEntra = objectId;
                utente.NomeCompleto = nome;
                _log.LogInformation("Primo accesso di {Email}: collegato all'utente registrato in anticipo (Id {Id}).", email, utente.IdUtente);
            }
        }

        if (utente is null)
        {
            utente = await CreaAsync(objectId.Value, email, nome, ct);
            _log.LogInformation("Primo accesso di {Email}: utente creato con ruolo Consultazione (Id {Id}).", email, utente.IdUtente);
        }
        else
        {
            // Allinea i dati anagrafici, che su Entra possono cambiare (l'e-mail solo se non è già di un altro utente).
            if (!string.Equals(utente.Email, email, StringComparison.OrdinalIgnoreCase) && email.Length > 0
                && !await _ctx.Utenti.AnyAsync(u => u.IdUtente != utente.IdUtente && u.Email == email, ct))
                utente.Email = email;
            utente.NomeCompleto = nome;
        }

        if (!utente.Attivo)
        {
            await _ctx.SaveChangesAsync(ct);
            _log.LogWarning("Accesso rifiutato: utente {Email} disattivato.", email);
            return;
        }

        utente.DataUltimoAccesso = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        identita.AddClaim(new Claim(TipiClaim.IdUtente, utente.IdUtente.ToString()));

        foreach (var permesso in await _permessi.LeggiAsync(utente.IdUtente, ct))
        {
            identita.AddClaim(new Claim(TipiClaim.Permesso, permesso));
        }
    }

    private static Guid? LeggiObjectId(ClaimsIdentity identita)
    {
        var valore = identita.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value
                     ?? identita.FindFirst("oid")?.Value;

        return Guid.TryParse(valore, out var guid) ? guid : null;
    }

    private async Task<Utente> CreaAsync(Guid objectId, string email, string nome, CancellationToken ct)
    {
        var utente = new Utente
        {
            ObjectIdEntra = objectId,
            Email = email,
            NomeCompleto = nome,
            Attivo = true,
            DataCreazione = DateTime.UtcNow
        };

        _ctx.Utenti.Add(utente);
        await _ctx.SaveChangesAsync(ct);

        var ruoloBase = await _ctx.Ruoli
            .FirstOrDefaultAsync(r => r.Codice == Ruoli.Consultazione, ct);

        if (ruoloBase is not null)
        {
            _ctx.UtentiRuoli.Add(new UtenteRuolo
            {
                IdUtente = utente.IdUtente,
                IdRuolo = ruoloBase.IdRuolo,
                DataAssegnazione = DateTime.UtcNow
            });
            await _ctx.SaveChangesAsync(ct);
        }

        return utente;
    }
}
