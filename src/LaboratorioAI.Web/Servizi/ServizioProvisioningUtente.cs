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
/// Un utente che accede per la prima volta viene creato automaticamente e
/// assegnato al ruolo Amministratore, coerentemente con la decisione 5.1
/// del documento di analisi (nella prima release tutti vedono tutto).
/// </summary>
public class ServizioProvisioningUtente
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ILogger<ServizioProvisioningUtente> _log;

    public ServizioProvisioningUtente(
        ContestoLaboratorioAI ctx,
        ILogger<ServizioProvisioningUtente> log)
    {
        _ctx = ctx;
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

        if (utente is null)
        {
            utente = await CreaAsync(objectId.Value, email, nome, ct);
            _log.LogInformation("Primo accesso di {Email}: utente creato (Id {Id}).", email, utente.IdUtente);
        }
        else
        {
            // Allinea i dati anagrafici, che su Entra possono cambiare
            utente.Email = email;
            utente.NomeCompleto = nome;
        }

        if (!utente.Attivo)
        {
            _log.LogWarning("Accesso rifiutato: utente {Email} disattivato.", email);
            return;
        }

        utente.DataUltimoAccesso = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        identita.AddClaim(new Claim(TipiClaim.IdUtente, utente.IdUtente.ToString()));

        foreach (var permesso in await LeggiPermessiAsync(utente.IdUtente, ct))
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

        var amministratore = await _ctx.Ruoli
            .FirstOrDefaultAsync(r => r.Codice == Ruoli.Amministratore, ct);

        if (amministratore is not null)
        {
            _ctx.UtentiRuoli.Add(new UtenteRuolo
            {
                IdUtente = utente.IdUtente,
                IdRuolo = amministratore.IdRuolo,
                DataAssegnazione = DateTime.UtcNow
            });
            await _ctx.SaveChangesAsync(ct);
        }

        return utente;
    }

    private async Task<List<string>> LeggiPermessiAsync(int idUtente, CancellationToken ct) =>
        await _ctx.UtentiRuoli
            .Where(ur => ur.IdUtente == idUtente)
            .SelectMany(ur => ur.Ruolo.Permessi)
            .Select(rp => rp.Permesso.Codice)
            .Distinct()
            .ToListAsync(ct);
}
