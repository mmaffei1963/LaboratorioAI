using LaboratorioAI.Tracciabilita.Core.Sicurezza;

namespace LaboratorioAI.Tracciabilita.Web.Autenticazione;

/// <summary>
/// Chi sta lavorando e per quale azienda, ricavato dalla sessione. Senza accesso non c'è azienda
/// né gestore, quindi il database non mostra nessun dato.
/// </summary>
public sealed class ContestoDaUtente : IContestoAzienda
{
    private readonly IHttpContextAccessor _http;

    public ContestoDaUtente(IHttpContextAccessor http) => _http = http;

    private System.Security.Claims.ClaimsPrincipal? Utente =>
        _http.HttpContext?.User is { Identity.IsAuthenticated: true } u ? u : null;

    private int? Numero(string tipo) =>
        int.TryParse(Utente?.FindFirst(tipo)?.Value, out var n) ? n : null;

    public int? IdAzienda => Numero(Rivendicazioni.IdAzienda);
    public int? IdUtente => Numero(Rivendicazioni.IdUtente);

    // La scelta dell'insediamento arriva con la consegna dei ruoli per insediamento.
    public int? IdInsediamento => null;

    public bool Gestore => Utente?.FindFirst(Rivendicazioni.Gestore)?.Value == "true";
}
