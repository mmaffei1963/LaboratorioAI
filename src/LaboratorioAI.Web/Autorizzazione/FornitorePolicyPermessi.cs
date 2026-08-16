using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>
/// Genera al volo una policy per ogni codice di permesso richiesto.
///
/// Evita di dover dichiarare esplicitamente una policy per ciascuno dei
/// permessi: negli endpoint basta scrivere
/// <c>[Authorize(Policy = Permessi.ValidazioneAnalizza)]</c>
/// e la policy corrispondente viene costruita automaticamente.
/// </summary>
public class FornitorePolicyPermessi : DefaultAuthorizationPolicyProvider
{
    public FornitorePolicyPermessi(IOptions<AuthorizationOptions> opzioni)
        : base(opzioni) { }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string nomePolicy)
    {
        var esistente = await base.GetPolicyAsync(nomePolicy);
        if (esistente is not null) return esistente;

        // I codici permesso hanno forma "area.funzione"
        if (!nomePolicy.Contains('.')) return null;

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RequisitoPermesso(nomePolicy))
            .Build();
    }
}
