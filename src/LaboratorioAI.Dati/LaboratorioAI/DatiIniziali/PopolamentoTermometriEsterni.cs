using LaboratorioAI.Core.Tarature;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;

/// <summary>
/// Scadenziario termometri clienti (03/11/2026): per le righe senza tipo lo si ricava dalla tipologia scritta a mano
/// (SONDA → a sonda, PIROMETRO o INFRAROSSI → pirometro, …). Gira a ogni avvio ma tocca solo le righe ancora senza
/// tipo e con una tipologia riconoscibile; le altre restano da completare a mano.
/// </summary>
public static class PopolamentoTermometriEsterni
{
    public static async Task<int> EseguiAsync(ContestoLaboratorioAI ctx, CancellationToken ct = default)
    {
        var righe = await ctx.TermometriEsterni
            .Where(t => t.TipoTermometro == TipoTermometro.NonIndicato && t.Tipologia != null && t.Tipologia != "")
            .ToListAsync(ct);
        var n = 0;
        foreach (var t in righe)
        {
            var tipo = RinnoviTermometri.TipoDaTipologia(t.Tipologia);
            if (tipo == TipoTermometro.NonIndicato) continue;
            t.TipoTermometro = tipo;
            n++;
        }
        if (n > 0) await ctx.SaveChangesAsync(ct);
        return n;
    }
}
