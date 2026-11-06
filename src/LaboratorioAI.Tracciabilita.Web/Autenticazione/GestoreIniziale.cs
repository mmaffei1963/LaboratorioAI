using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Autenticazione;

/// <summary>
/// Crea il primo utente gestore (personale MARE.A) se non ne esiste nessuno.
/// E-mail e password vanno in <c>user-secrets</c> (sviluppo) o nel file di produzione non versionato:
/// mai nel codice né nei file del repository.
/// </summary>
public static class GestoreIniziale
{
    public static async Task AssicuraAsync(IServiceProvider servizi, IConfiguration configurazione, ILogger log)
    {
        try
        {
            using var sessione = servizi.CreateScope();
            var fabbrica = sessione.ServiceProvider.GetRequiredService<FabbricaContestoGestore>();
            await using var db = fabbrica.Crea();

            if (await db.Utenti.AnyAsync(u => u.EGestore)) return;

            var email = configurazione["Tracciabilita:GestoreIniziale:Email"]?.Trim();
            var password = configurazione["Tracciabilita:GestoreIniziale:Password"];
            var nome = configurazione["Tracciabilita:GestoreIniziale:Nome"]?.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            {
                log.LogWarning("Non esiste nessun utente gestore. Impostare Tracciabilita:GestoreIniziale:Email e :Password (user-secrets) e riavviare.");
                return;
            }

            var errori = RegolePassword.Valida(password, email);
            if (errori.Count > 0)
            {
                log.LogError("Password del gestore iniziale non accettata: {Motivi}", string.Join(" ", errori));
                return;
            }

            var utente = new Utente
            {
                Email = email,
                Nome = string.IsNullOrWhiteSpace(nome) ? "Gestore MARE.A" : nome,
                EGestore = true,
                Attivo = true
            };
            utente.ImprontaPassword = ServizioAccesso.Impronta(utente, password);
            db.Utenti.Add(utente);
            await db.SaveChangesAsync();

            db.Audit.Add(new VoceAudit
            {
                IdUtente = utente.IdUtente,
                Azione = "UtenteCreato",
                Entita = "Utente",
                IdEntita = utente.IdUtente.ToString(),
                Dopo = "Primo utente gestore creato all'avvio"
            });
            await db.SaveChangesAsync();

            log.LogInformation("Creato il primo utente gestore ({Email}).", email);
        }
        catch (Exception e)
        {
            log.LogError(e, "Impossibile verificare l'utente gestore: il database è aggiornato? (dotnet ef database update)");
        }
    }
}
