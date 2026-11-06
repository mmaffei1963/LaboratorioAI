using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Tarature;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Rinnovo delle tarature dei termometri clienti nello scadenziario (03/11/2026): proposta delle tarature che una
/// nuova potrebbe rinnovare (stesso cliente e stessa tipologia, ancora in uso) e registrazione di quelle spuntate.
/// </summary>
internal static class RinnovoTermometriEsterni
{
    public static async Task<IReadOnlyList<object>> CandidatiAsync(ContestoLaboratorioAI ctx, TermometroEsterno nuova, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nuova.Cliente)) return Array.Empty<object>();
        var archivio = await ctx.TermometriEsterni.AsNoTracking()
            .Where(t => t.Attivo && t.DataCancellazione == null && t.DataRinnovo == null && !t.EscludiScadenze
                        && t.IdTermometroEsterno != nuova.IdTermometroEsterno)
            .ToListAsync(ct);
        return RinnoviTermometri.Candidati(nuova, archivio)
            .Select(t => (object)new { t.IdTermometroEsterno, t.NumeroRapporto, t.Tipologia, t.Cliente, t.Riferimento, t.DataTaratura, t.DataScadenza })
            .ToList();
    }

    /// <summary>Segna come rinnovate dalla <paramref name="nuova"/> le tarature indicate (solo quelle ancora in uso).</summary>
    public static async Task<int> RinnovaAsync(ContestoLaboratorioAI ctx, TermometroEsterno nuova, IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().Where(i => i != nuova.IdTermometroEsterno).ToList();
        var vecchie = await ctx.TermometriEsterni
            .Where(t => lista.Contains(t.IdTermometroEsterno) && t.DataRinnovo == null && t.DataCancellazione == null)
            .ToListAsync(ct);
        var ora = DateTime.UtcNow;
        foreach (var t in vecchie)
        {
            t.DataRinnovo = nuova.DataTaratura ?? DateTime.Today;
            t.IdTermometroRinnovo = nuova.IdTermometroEsterno;
            t.DataUltimaModifica = ora;
        }
        await ctx.SaveChangesAsync(ct);
        return vecchie.Count;
    }
}

public record RichiestaRinnovo
{
    public List<int>? Ids { get; init; }
}

public record RichiestaStatoEsterno
{
    /// <summary>«inuso», «rinnovata» o «dismessa».</summary>
    public string? Stato { get; init; }
}
