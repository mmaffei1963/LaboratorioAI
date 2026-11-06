using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Limiti;

/// <summary>
/// Verifica delle schede validate (18/10/2026): controlli automatici (subito, senza AI) e revisione con l'AI per fonte
/// (in coda, eseguita in background). Nessuna modifica alle schede: i rilievi si chiudono con una nota.
/// </summary>
public class ServizioVerificaLimiti
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryLimitiArcLab _arclab;

    public ServizioVerificaLimiti(ContestoLaboratorioAI ctx, IRepositoryLimitiArcLab arclab) { _ctx = ctx; _arclab = arclab; }

    /// <summary>Schede soggette a verifica: validate e da riverificare.</summary>
    public IQueryable<LimiteNormativo> SchedeInVigore() =>
        _ctx.LimitiNormativi.Where(s => s.Stato == StatoSchedaLimite.Validata || s.Stato == StatoSchedaLimite.DaRiverificare);

    public async Task<GiroVerificaLimiti> ControlliAutomaticiAsync(OperatoreLimiti op, CancellationToken ct)
    {
        var giro = new GiroVerificaLimiti
        {
            Tipo = TipoVerificaLimiti.Automatica, Stato = StatoGiroVerifica.InCorso, DataRichiesta = DateTime.UtcNow,
            DataInizio = DateTime.UtcNow, RichiestoDa = op.Nome, IdUtenteRichiesta = op.IdUtente
        };
        _ctx.GiriVerificaLimiti.Add(giro);
        await _ctx.SaveChangesAsync(ct);

        var schede = await SchedeInVigore().AsNoTracking().Include(s => s.Abbinamenti).ToListAsync(ct);
        // unità usate in ArcLab per parametro (ultimi 3 anni)
        Dictionary<int, List<string>> unita;
        string? avviso = null;
        try
        {
            unita = (await _arclab.ParametriUsatiAsync(3, ct)).GroupBy(p => p.IdParametro)
                .ToDictionary(g => g.Key, g => g.Select(p => p.UnitaMisura?.Trim()).Where(u => !string.IsNullOrEmpty(u)).Select(u => u!).Distinct().ToList());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            unita = new();
            avviso = " Unità ArcLab non lette (" + ex.Message + "): controllo delle unità non eseguito.";
        }
        var input = schede.Select(s => new SchedaDaControllare(s, s.Abbinamenti.Where(a => a.Attivo)
            .Select(a => (a.NomeParametro ?? a.IdParametroArcLab.ToString(),
                (IReadOnlyList<string>)(unita.TryGetValue(a.IdParametroArcLab, out var u) ? u : new List<string>()), a.FattoreUnita))
            .ToList())).ToList();
        var rilievi = ControlliSchede.Esegui(input);
        foreach (var r in rilievi)
            _ctx.RilieviVerificaLimiti.Add(new RilievoVerificaLimite
            {
                IdGiroVerificaLimiti = giro.IdGiroVerificaLimiti, IdLimiteNormativo = r.IdLimiteNormativo, Gravita = r.Gravita,
                Regola = r.Regola, Motivo = r.Motivo.Length > 2000 ? r.Motivo[..2000] : r.Motivo
            });
        giro.Stato = StatoGiroVerifica.Completato;
        giro.DataFine = DateTime.UtcNow;
        giro.SchedeControllate = schede.Count;
        var conRilievi = rilievi.Select(r => r.IdLimiteNormativo).Distinct().Count();
        giro.Esito = $"{schede.Count} schede controllate, {conRilievi} con rilievi " +
            $"({rilievi.Count(r => r.Gravita == GravitaRilievo.Dubbio)} dubbi, {rilievi.Count(r => r.Gravita == GravitaRilievo.Avviso)} avvisi)." + avviso;
        await _ctx.SaveChangesAsync(ct);
        return giro;
    }

    /// <summary>Mette in coda la revisione AI delle schede validate di una fonte. Null = fatto, altrimenti il motivo.</summary>
    public async Task<string?> AccodaRevisioneAsync(int idFonte, OperatoreLimiti op, CancellationToken ct)
    {
        var versioni = _ctx.VersioniFonti.Where(v => v.IdFonteNormativa == idFonte).Select(v => (int?)v.IdVersioneFonte);
        var n = await SchedeInVigore().CountAsync(s => versioni.Contains(s.IdVersioneFonte), ct);
        if (n == 0) return "Questa fonte non ha schede validate.";
        if (await _ctx.GiriVerificaLimiti.AnyAsync(g => g.IdFonteNormativa == idFonte
                && (g.Stato == StatoGiroVerifica.InCoda || g.Stato == StatoGiroVerifica.InCorso), ct))
            return "La revisione di questa fonte è già in coda o in corso.";
        _ctx.GiriVerificaLimiti.Add(new GiroVerificaLimiti
        {
            Tipo = TipoVerificaLimiti.RevisioneAi, IdFonteNormativa = idFonte, Stato = StatoGiroVerifica.InCoda,
            DataRichiesta = DateTime.UtcNow, RichiestoDa = op.Nome, IdUtenteRichiesta = op.IdUtente, Esito = $"In coda ({n} schede)"
        });
        await _ctx.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> ChiudiAsync(IEnumerable<int> ids, string? nota, OperatoreLimiti op, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nota)) return "Scrivi una nota (es. «verificato sul testo: valore corretto», «corretta la scheda»).";
        var lista = ids.Distinct().ToList();
        var rilievi = await _ctx.RilieviVerificaLimiti.Where(r => lista.Contains(r.IdRilievoVerificaLimite) && !r.Chiuso).ToListAsync(ct);
        foreach (var r in rilievi)
        {
            r.Chiuso = true; r.ChiusoDa = op.Nome; r.DataChiusura = DateTime.UtcNow;
            r.NotaChiusura = nota.Trim().Length > 1000 ? nota.Trim()[..1000] : nota.Trim();
        }
        await _ctx.SaveChangesAsync(ct);
        return null;
    }
}
