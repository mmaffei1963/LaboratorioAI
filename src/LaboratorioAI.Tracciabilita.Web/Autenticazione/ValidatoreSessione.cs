using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Autenticazione;

/// <summary>
/// A ogni richiesta controlla che l'utente sia ancora attivo e che il servizio della sua azienda lo consenta:
/// se si disattiva un utente o si sospende un'azienda, la sessione smette di funzionare subito.
/// </summary>
public static class ValidatoreSessione
{
    public static async Task ValidaAsync(CookieValidatePrincipalContext contesto)
    {
        var principal = contesto.Principal;
        var idUtente = int.TryParse(principal?.FindFirst(Rivendicazioni.IdUtente)?.Value, out var u) ? u : (int?)null;
        var idAziendaSessione = int.TryParse(principal?.FindFirst(Rivendicazioni.IdAzienda)?.Value, out var a) ? a : (int?)null;

        if (idUtente is null) { await Rifiuta(contesto); return; }

        var fabbrica = contesto.HttpContext.RequestServices.GetRequiredService<FabbricaContestoGestore>();
        await using var db = fabbrica.Crea();

        var utente = await db.Utenti.AsNoTracking()
            .Where(x => x.IdUtente == idUtente)
            .Select(x => new { x.Attivo, x.EGestore, x.IdAzienda })
            .FirstOrDefaultAsync(contesto.HttpContext.RequestAborted);

        if (utente is null || !utente.Attivo || utente.IdAzienda != idAziendaSessione ||
            (principal!.FindFirst(Rivendicazioni.Gestore)?.Value == "true") != utente.EGestore)
        {
            await Rifiuta(contesto);
            return;
        }

        if (!utente.EGestore)
        {
            var stato = await db.Aziende.AsNoTracking()
                .Where(x => x.IdAzienda == utente.IdAzienda)
                .Select(x => (StatoServizio?)x.Stato)
                .FirstOrDefaultAsync(contesto.HttpContext.RequestAborted);

            if (stato is null or StatoServizio.Sospeso or StatoServizio.Chiuso)
                await Rifiuta(contesto);
        }
    }

    private static async Task Rifiuta(CookieValidatePrincipalContext contesto)
    {
        contesto.RejectPrincipal();
        await contesto.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
