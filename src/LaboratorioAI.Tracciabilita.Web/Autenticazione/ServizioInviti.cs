using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Autenticazione;

public sealed record EsitoInvito(bool Riuscito, string? Nome = null, IReadOnlyList<string>? Errori = null);

/// <summary>Accetta un invito: la persona sceglie la password. Usa il contesto del gestore (non è ancora entrata).</summary>
public sealed class ServizioInviti
{
    private readonly FabbricaContestoGestore _fabbrica;

    public ServizioInviti(FabbricaContestoGestore fabbrica) => _fabbrica = fabbrica;

    /// <summary>Nome della persona se il link è valido, altrimenti nulla (messaggio unico per link sbagliato o scaduto).</summary>
    public async Task<string?> NomePerInvitoAsync(string codice, CancellationToken ct = default)
    {
        await using var db = _fabbrica.Crea();
        var utente = await Trova(db, codice, ct);
        return utente?.Nome;
    }

    public async Task<EsitoInvito> ImpostaPasswordAsync(string codice, string? password, string? indirizzoIp, CancellationToken ct = default)
    {
        await using var db = _fabbrica.Crea();
        var utente = await Trova(db, codice, ct);
        if (utente is null)
            return new EsitoInvito(false, null, new[] { "Il link non è valido o è scaduto. Chiedi a MARE.A un nuovo invito." });

        var errori = RegolePassword.Valida(password ?? "", utente.Email);
        if (errori.Count > 0)
            return new EsitoInvito(false, utente.Nome, errori.ToList());

        var adesso = DateTime.UtcNow;
        utente.ImprontaPassword = ServizioAccesso.Impronta(utente, password!);
        utente.ImprontaInvito = null;
        utente.ScadenzaInvitoUtc = null;
        utente.TentativiFalliti = 0;
        utente.BloccatoFinoUtc = null;

        db.Audit.Add(new VoceAudit
        {
            IdAzienda = utente.IdAzienda,
            IdUtente = utente.IdUtente,
            Azione = "PasswordImpostata",
            Entita = "Utente",
            IdEntita = utente.IdUtente.ToString(),
            Dopo = "La persona ha scelto la password tramite invito",
            IndirizzoIp = indirizzoIp,
            DataOra = adesso
        });
        await db.SaveChangesAsync(ct);
        return new EsitoInvito(true, utente.Nome);
    }

    private static async Task<Utente?> Trova(ContestoTracciabilita db, string codice, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codice)) return null;
        var impronta = InvitoUtente.ImprontaDi(codice);
        var utente = await db.Utenti.FirstOrDefaultAsync(u => u.ImprontaInvito == impronta, ct);
        return utente is { Attivo: true } && InvitoUtente.Valido(utente.ImprontaInvito, utente.ScadenzaInvitoUtc, codice, DateTime.UtcNow)
            ? utente
            : null;
    }
}
