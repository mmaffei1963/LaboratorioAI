using System.Security.Claims;
using System.Text.Encodings.Web;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.Web.Autenticazione;

public class OpzioniAutenticazioneSviluppo
{
    public const string Sezione = "AutenticazioneSviluppo";
    public const string Schema = "Sviluppo";

    public bool Abilitato { get; set; }
    public string Email { get; set; } = "sviluppo@mare-a.com";
    public string NomeCompleto { get; set; } = "Utente di sviluppo";
    public Guid ObjectId { get; set; } = new("00000000-0000-0000-0000-0000000DE7E5");

    public void Verifica(IWebHostEnvironment ambiente)
    {
        if (!Abilitato || ambiente.IsDevelopment()) return;

        throw new InvalidOperationException(
            $"AVVIO INTERROTTO. L'accesso di sviluppo e' attivo ma l'ambiente e' " +
            $"'{ambiente.EnvironmentName}', non 'Development'. In questa configurazione " +
            $"chiunque raggiunga l'applicazione vi accederebbe come '{NomeCompleto}' " +
            $"con tutti i permessi.");
    }
}

public class GestoreAutenticazioneSviluppo
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly OpzioniAutenticazioneSviluppo _sviluppo;
    private readonly ServizioProvisioningUtente _provisioning;

    public GestoreAutenticazioneSviluppo(
        IOptionsMonitor<AuthenticationSchemeOptions> opzioni,
        ILoggerFactory registro,
        UrlEncoder codificatore,
        IOptions<OpzioniAutenticazioneSviluppo> sviluppo,
        ServizioProvisioningUtente provisioning)
        : base(opzioni, registro, codificatore)
    {
        _sviluppo = sviluppo.Value;
        _provisioning = provisioning;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identita = new ClaimsIdentity(
        [
            new Claim("oid", _sviluppo.ObjectId.ToString()),
            new Claim("preferred_username", _sviluppo.Email),
            new Claim("name", _sviluppo.NomeCompleto),
            new Claim(ClaimTypes.Name, _sviluppo.NomeCompleto)
        ], OpzioniAutenticazioneSviluppo.Schema);

        await _provisioning.AllineaAsync(identita, Context.RequestAborted);

        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identita), OpzioniAutenticazioneSviluppo.Schema));
    }
}
