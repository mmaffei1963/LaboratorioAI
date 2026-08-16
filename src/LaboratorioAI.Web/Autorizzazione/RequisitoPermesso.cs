using Microsoft.AspNetCore.Authorization;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>Requisito di autorizzazione basato sul possesso di un permesso.</summary>
public class RequisitoPermesso : IAuthorizationRequirement
{
    public string Codice { get; }

    public RequisitoPermesso(string codice) => Codice = codice;
}
