namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Informazioni sull'utente che ha originato la richiesta in corso.
///
/// Astrazione volutamente indipendente da ASP.NET: i servizi di dominio
/// non devono conoscere HttpContext.
/// </summary>
public interface IContestoUtente
{
    /// <summary>Identificativo applicativo. Nullo se la richiesta non è autenticata.</summary>
    int? IdUtente { get; }

    string? Email { get; }
    string? NomeCompleto { get; }
    string? IndirizzoIp { get; }

    bool Autenticato { get; }

    /// <summary>Verifica il possesso di un permesso. Vedi <see cref="Costanti.Permessi"/>.</summary>
    bool HaPermesso(string codicePermesso);
}
