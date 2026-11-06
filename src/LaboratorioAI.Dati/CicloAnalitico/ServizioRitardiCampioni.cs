using System.Text.Json;
using LaboratorioAI.Core.CicloAnalitico;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.ArcLab.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.CicloAnalitico;

/// <summary>
/// Avviso campioni in ritardo (22/10/2026): impostazioni (giorni dell'avviso e soglie per tipo, in app.Configurazioni),
/// campioni tolti dall'elenco e calcolo con <see cref="MotoreRitardiCampioni"/> sui dati ArcLab (sola lettura).
/// </summary>
public class ServizioRitardiCampioni
{
    public const string ChiaveImpostazioni = "CicloAnalitico.AvvisoRitardi";
    private readonly ContestoLaboratorioAI _ctx;
    private readonly RepositoryRitardiCampioni _repo;

    public ServizioRitardiCampioni(ContestoLaboratorioAI ctx, RepositoryRitardiCampioni repo)
    {
        _ctx = ctx; _repo = repo;
    }

    public async Task<ImpostazioniRitardi> ImpostazioniAsync(CancellationToken ct = default)
    {
        var c = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(x => x.Chiave == ChiaveImpostazioni, ct);
        if (string.IsNullOrWhiteSpace(c?.Valore)) return new ImpostazioniRitardi();
        try { return JsonSerializer.Deserialize<ImpostazioniRitardi>(c.Valore!) ?? new ImpostazioniRitardi(); }
        catch (JsonException) { return new ImpostazioniRitardi(); }
    }

    public async Task SalvaImpostazioniAsync(ImpostazioniRitardi imp, int? idUtente, CancellationToken ct = default)
    {
        var c = await _ctx.Configurazioni.FirstOrDefaultAsync(x => x.Chiave == ChiaveImpostazioni, ct);
        if (c is null)
        {
            c = new Configurazione
            {
                Chiave = ChiaveImpostazioni, Categoria = "Ciclo analitico",
                Descrizione = "Avviso campioni in ritardo: giorni della settimana e soglie in giorni per tipo (JSON)."
            };
            _ctx.Configurazioni.Add(c);
        }
        c.Valore = JsonSerializer.Serialize(imp);
        c.DataModifica = DateTime.UtcNow;
        c.IdUtenteModifica = idUtente;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<HashSet<int>> IdNascostiAsync(CancellationToken ct = default) =>
        (await _ctx.CampioniNascostiRitardi.AsNoTracking().Where(x => x.Attivo).Select(x => x.IdCampioneArcLab).ToListAsync(ct)).ToHashSet();

    public Task<List<CampioneNascostoRitardi>> NascostiAsync(CancellationToken ct = default) =>
        _ctx.CampioniNascostiRitardi.AsNoTracking().Where(x => x.Attivo).OrderByDescending(x => x.DataNascosto).ToListAsync(ct);

    /// <summary>Campioni in ritardo (senza i nascosti), calcolati oggi.</summary>
    public async Task<List<CampioneInRitardo>> InRitardoAsync(CancellationToken ct = default)
    {
        var imp = await ImpostazioniAsync(ct);
        var nascosti = await IdNascostiAsync(ct);
        var prove = await _repo.ProveInAttesaAsync(ct);
        return MotoreRitardiCampioni.Calcola(prove, imp, DateTime.Today, nascosti.Contains);
    }

    public async Task NascondiAsync(int idCampione, string numero, string motivo, string chi, CancellationToken ct = default)
    {
        if (await _ctx.CampioniNascostiRitardi.AnyAsync(x => x.IdCampioneArcLab == idCampione && x.Attivo, ct)) return;
        _ctx.CampioniNascostiRitardi.Add(new CampioneNascostoRitardi
        {
            IdCampioneArcLab = idCampione, NumeroCampione = numero.Length > 30 ? numero[..30] : numero,
            Motivo = motivo.Length > 500 ? motivo[..500] : motivo, NascostoDa = chi, DataNascosto = DateTime.UtcNow
        });
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task RipristinaAsync(int idNascosto, string chi, CancellationToken ct = default)
    {
        var x = await _ctx.CampioniNascostiRitardi.FirstOrDefaultAsync(n => n.IdCampioneNascostoRitardi == idNascosto && n.Attivo, ct);
        if (x is null) return;
        x.Attivo = false; x.RipristinatoDa = chi; x.DataRipristino = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}
