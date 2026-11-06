using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// 21/10/2026: legge un importo dal modulo accettando sia «16554.31» (campi numerici del browser) sia «16.554,31» o
/// «16554,31» (scritto all'italiana), indipendentemente dalla lingua del server. Si usa con <see cref="DecAttribute"/>.
/// </summary>
public sealed class DecimaleFlessibileBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext ctx)
    {
        var valore = ctx.ValueProvider.GetValue(ctx.ModelName);
        if (valore == ValueProviderResult.None) return Task.CompletedTask;
        ctx.ModelState.SetModelValue(ctx.ModelName, valore);
        var s = (valore.FirstValue ?? "").Trim().Replace(" ", "").Replace("€", "");
        var nullable = Nullable.GetUnderlyingType(ctx.ModelType) is not null;
        if (s.Length == 0)
        {
            ctx.Result = ModelBindingResult.Success(nullable ? null : 0m);
            return Task.CompletedTask;
        }
        if (s.Contains(',') && s.Contains('.')) s = s.Replace(".", "").Replace(',', '.');
        else s = s.Replace(',', '.');
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            ctx.Result = ModelBindingResult.Success(d);
        else
        {
            ctx.ModelState.TryAddModelError(ctx.ModelName, $"«{valore.FirstValue}» non è un numero.");
            ctx.Result = ModelBindingResult.Failed();
        }
        return Task.CompletedTask;
    }
}

/// <summary>Importo letto con <see cref="DecimaleFlessibileBinder"/>.</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class DecAttribute : ModelBinderAttribute
{
    public DecAttribute() : base(typeof(DecimaleFlessibileBinder)) { }
}
