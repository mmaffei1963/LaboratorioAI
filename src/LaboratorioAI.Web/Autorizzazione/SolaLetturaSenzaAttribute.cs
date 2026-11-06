using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>
/// Sola lettura per chi non ha il permesso indicato (11/10/2026). Le pagine si possono aprire (GET), ma ogni modifica
/// (POST, PUT, DELETE…) richiede il permesso; in più la pagina viene marcata «sola lettura» (ViewData["SolaLettura"]):
/// il layout disattiva i moduli di modifica e ne nasconde i pulsanti. Esempio: i soci consultano le provvigioni.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SolaLetturaSenzaAttribute : Attribute, IAuthorizationFilter, IActionFilter
{
    private readonly string _permesso;

    public SolaLetturaSenzaAttribute(string permesso) => _permesso = permesso;

    private bool Consentito(HttpContext http) => http.User.HasClaim(TipiClaim.Permesso, _permesso);

    public void OnAuthorization(AuthorizationFilterContext contesto)
    {
        var metodo = contesto.HttpContext.Request.Method;
        if (HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) || Consentito(contesto.HttpContext)) return;
        contesto.Result = new ForbidResult();
    }

    public void OnActionExecuting(ActionExecutingContext contesto)
    {
        if (!Consentito(contesto.HttpContext) && contesto.Controller is Controller c) c.ViewData["SolaLettura"] = true;
    }

    public void OnActionExecuted(ActionExecutedContext contesto) { }
}
