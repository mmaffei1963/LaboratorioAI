using Microsoft.AspNetCore.Authorization;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>
/// Verifica il requisito confrontandolo con i claim di permesso
/// presenti nell'identità dell'utente.
/// </summary>
public class GestorePermessi : AuthorizationHandler<RequisitoPermesso>
{
    private readonly ILogger<GestorePermessi> _log;

    public GestorePermessi(ILogger<GestorePermessi> log) => _log = log;

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contesto,
        RequisitoPermesso requisito)
    {
        var concesso = contesto.User.HasClaim(TipiClaim.Permesso, requisito.Codice);

        if (concesso)
        {
            contesto.Succeed(requisito);
        }
        else if (contesto.User.Identity?.IsAuthenticated == true)
        {
            // Utente autenticato ma privo del permesso: interessa registrarlo
            _log.LogWarning(
                "Accesso negato. Utente {Utente}, permesso richiesto {Permesso}",
                contesto.User.Identity.Name, requisito.Codice);
        }

        return Task.CompletedTask;
    }
}
