using System.Security.Claims;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Web.Autorizzazione;

namespace LaboratorioAI.Web.Servizi;

/// <summary>Implementazione di <see cref="IContestoUtente"/> basata su HttpContext.</summary>
public class ContestoUtenteHttp : IContestoUtente
{
    private readonly IHttpContextAccessor _accessor;

    public ContestoUtenteHttp(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool Autenticato => Principal?.Identity?.IsAuthenticated == true;

    public int? IdUtente
    {
        get
        {
            var valore = Principal?.FindFirst(TipiClaim.IdUtente)?.Value;
            return int.TryParse(valore, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirst(ClaimTypes.Upn)?.Value
        ?? Principal?.FindFirst("preferred_username")?.Value
        ?? Principal?.FindFirst(ClaimTypes.Email)?.Value;

    public string? NomeCompleto =>
        Principal?.FindFirst("name")?.Value
        ?? Principal?.Identity?.Name;

    public string? IndirizzoIp =>
        _accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool HaPermesso(string codicePermesso) =>
        Principal?.HasClaim(TipiClaim.Permesso, codicePermesso) == true;
}
