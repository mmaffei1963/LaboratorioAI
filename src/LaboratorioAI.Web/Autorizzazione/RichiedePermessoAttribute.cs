using Microsoft.AspNetCore.Authorization;

namespace LaboratorioAI.Web.Autorizzazione;

/// <summary>
/// Scorciatoia leggibile per la protezione di controller e azioni.
/// <code>[RichiedePermesso(Permessi.ValidazioneAnalizza)]</code>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RichiedePermessoAttribute : AuthorizeAttribute
{
    public RichiedePermessoAttribute(string codicePermesso) => Policy = codicePermesso;
}
