using System.Security.Claims;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Authentication;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>
/// A ogni richiesta sostituisce i claim di permesso salvati nel cookie con quelli attuali del database (11/10/2026).
/// Prima i permessi restavano quelli del momento dell'accesso; ora un cambio di ruolo, o la disattivazione di un
/// utente, ha effetto subito (al massimo dopo un minuto di memoria, azzerata dalla pagina Utenti e ruoli).
/// </summary>
public class TrasformazionePermessi : IClaimsTransformation
{
    private readonly ServizioPermessiUtente _permessi;

    public TrasformazionePermessi(ServizioPermessiUtente permessi) => _permessi = permessi;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identita = principal.Identities.FirstOrDefault(i => i.HasClaim(c => c.Type == TipiClaim.IdUtente));
        if (identita is null || !int.TryParse(identita.FindFirst(TipiClaim.IdUtente)?.Value, out var idUtente)) return principal;

        var attuali = await _permessi.LeggiAsync(idUtente);
        var copia = principal.Clone();
        var id = copia.Identities.First(i => i.HasClaim(c => c.Type == TipiClaim.IdUtente));
        foreach (var c in id.FindAll(TipiClaim.Permesso).ToList()) id.RemoveClaim(c);
        foreach (var p in attuali) id.AddClaim(new Claim(TipiClaim.Permesso, p));
        return copia;
    }
}
