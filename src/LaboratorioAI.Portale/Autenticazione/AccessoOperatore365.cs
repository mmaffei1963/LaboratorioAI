using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Identity.Web;

namespace LaboratorioAI.Portale.Autenticazione;

/// <summary>
/// Accesso operatori MARE.A con Microsoft 365 (19/11/2026), al posto del codice operatore con password condivisa.
/// Stessa registrazione Entra ID di LaboratorioAI (sezione «EntraId» della configurazione, copiata dalla Web; sul
/// portale serve in più l'indirizzo di ritorno https://portale.mare-a.com/signin-oidc). Il login Microsoft serve solo
/// a sapere chi è la persona: subito dopo si apre la normale sessione del portale e il cookie di Microsoft si cancella.
/// I clienti non sono toccati.
/// Senza «EntraId:ClientId» configurato il pulsante non funziona (in sviluppo fa un accesso di sviluppo senza Microsoft).
/// </summary>
public static class AccessoOperatore365
{
    public const string SezioneEntra = "EntraId";

    public static bool Configurato(IConfiguration configurazione)
    {
        var id = configurazione[SezioneEntra + ":ClientId"];
        return !string.IsNullOrWhiteSpace(id) && !id.StartsWith("DA_IMPOSTARE", StringComparison.OrdinalIgnoreCase);
    }

    public static void Configura(WebApplicationBuilder builder)
    {
        if (!Configurato(builder.Configuration))
        {
            builder.Services.AddAuthentication();
            return;
        }

        builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, SezioneEntra);
        // Il cookie di Microsoft serve solo per il rientro dal login: breve e cancellato subito dopo.
        builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, o =>
        {
            o.Cookie.Name = "LabAIPortale.Operatore";
            o.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            o.SlidingExpiration = false;
        });
    }
}
