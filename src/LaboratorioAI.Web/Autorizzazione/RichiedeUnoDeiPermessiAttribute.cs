using Microsoft.AspNetCore.Authorization;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>
/// Accesso consentito a chi ha almeno uno dei permessi indicati (11/10/2026), es. provvigioni: chi le gestisce e chi le
/// consulta in sola lettura. <code>[RichiedeUnoDeiPermessi(Permessi.ProvvigioniGestisci, Permessi.ProvvigioniConsulta)]</code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RichiedeUnoDeiPermessiAttribute : AuthorizeAttribute
{
    public const string Prefisso = "uno:";

    public RichiedeUnoDeiPermessiAttribute(params string[] codici) => Policy = Prefisso + string.Join('|', codici);
}

/// <summary>Requisito soddisfatto da almeno uno dei permessi elencati.</summary>
public class RequisitoUnoDeiPermessi : IAuthorizationRequirement
{
    public IReadOnlyList<string> Codici { get; }

    public RequisitoUnoDeiPermessi(IEnumerable<string> codici) => Codici = codici.ToList();
}

public class GestoreUnoDeiPermessi : AuthorizationHandler<RequisitoUnoDeiPermessi>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext contesto, RequisitoUnoDeiPermessi requisito)
    {
        if (requisito.Codici.Any(c => contesto.User.HasClaim(TipiClaim.Permesso, c))) contesto.Succeed(requisito);
        return Task.CompletedTask;
    }
}
