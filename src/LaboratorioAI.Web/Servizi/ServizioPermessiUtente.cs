using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Permessi effettivi di un utente (11/10/2026): l'unione dei permessi dei suoi ruoli attivi; nessuno se l'utente è
/// disattivato. Il risultato resta in memoria per un minuto; la pagina Utenti e ruoli lo invalida a ogni modifica,
/// così i cambi di ruolo valgono alla richiesta successiva senza dover uscire e rientrare.
/// </summary>
public class ServizioPermessiUtente
{
    private static readonly TimeSpan Durata = TimeSpan.FromMinutes(1);
    private static int _generazione;

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IMemoryCache _cache;

    public ServizioPermessiUtente(ContestoLaboratorioAI ctx, IMemoryCache cache)
    {
        _ctx = ctx;
        _cache = cache;
    }

    private static string Chiave(int idUtente) => $"permessi:{Volatile.Read(ref _generazione)}:{idUtente}";

    public async Task<IReadOnlyList<string>> LeggiAsync(int idUtente, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(Chiave(idUtente), out IReadOnlyList<string>? valore) && valore is not null) return valore;

        var attivo = await _ctx.Utenti.AsNoTracking().Where(u => u.IdUtente == idUtente).Select(u => (bool?)u.Attivo).FirstOrDefaultAsync(ct);
        IReadOnlyList<string> permessi = attivo == true
            ? await _ctx.UtentiRuoli.AsNoTracking()
                .Where(ur => ur.IdUtente == idUtente && ur.Ruolo.Attivo)
                .SelectMany(ur => ur.Ruolo.Permessi)
                .Select(rp => rp.Permesso.Codice)
                .Distinct()
                .ToListAsync(ct)
            : Array.Empty<string>();

        _cache.Set(Chiave(idUtente), permessi, Durata);
        return permessi;
    }

    /// <summary>Svuota la memoria per tutti gli utenti (dopo una modifica a ruoli, permessi o utenti).</summary>
    public static void Invalida() => Interlocked.Increment(ref _generazione);
}
