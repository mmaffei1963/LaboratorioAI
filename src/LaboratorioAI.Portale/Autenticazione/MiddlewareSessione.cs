using LaboratorioAI.Portale.Servizi;

namespace LaboratorioAI.Portale.Autenticazione;

/// <summary>
/// Riconosce la sessione a partire dal token presentato nell'intestazione
/// della richiesta e la rende disponibile per il resto della pipeline.
///
/// Il token è opaco e verificato su database a ogni richiesta: questo
/// permette la revoca immediata di una sessione, che un token firmato
/// e autoconsistente non consentirebbe.
/// </summary>
public class MiddlewareSessione
{
    public const string IntestazioneToken = "X-Sessione-Portale";

    private readonly RequestDelegate _successivo;

    public MiddlewareSessione(RequestDelegate successivo) => _successivo = successivo;

    public async Task InvokeAsync(HttpContext contesto, IServizioAccessoPortale accesso)
    {
        var token = contesto.Request.Headers[IntestazioneToken].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(token))
        {
            var sessione = await accesso.VerificaTokenAsync(token, contesto.RequestAborted);
            if (sessione is not null)
                contesto.Items[ContestoSessione.ChiaveContesto] = sessione;
        }

        await _successivo(contesto);
    }
}

/// <summary>Richiede una sessione valida sull'azione o sul controller.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RichiedeSessioneAttribute : Attribute,
    Microsoft.AspNetCore.Mvc.Filters.IAsyncActionFilter
{
    /// <summary>Se vero, l'azione è riservata alle sessioni operatore.</summary>
    public bool SoloOperatore { get; set; }

    public async Task OnActionExecutionAsync(
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext contesto,
        Microsoft.AspNetCore.Mvc.Filters.ActionExecutionDelegate successivo)
    {
        var sessione = contesto.HttpContext.RequestServices
            .GetRequiredService<IContestoSessione>();

        if (!sessione.Autenticato)
        {
            contesto.Result = new Microsoft.AspNetCore.Mvc.JsonResult(
                new { messaggio = "Sessione scaduta. Effettuare nuovamente l'accesso." })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
            return;
        }

        if (SoloOperatore && !sessione.Operatore)
        {
            contesto.Result = new Microsoft.AspNetCore.Mvc.JsonResult(
                new { messaggio = "Funzione riservata al personale MARE.A." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await successivo();
    }
}
