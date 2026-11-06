using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Consulenza;

/// <summary>
/// Trova il cliente nell'anagrafica AZW (ANACF, copia in provv.AzwClienti) partendo dal cliente ArcLab.
/// Collegamento: codice cliente ArcLab, poi codici AZW delle sue commesse, poi partita IVA, poi codice fiscale.
/// 29/10/2026: estratto dai preventivi di consulenza per usarlo anche nei preventivi analisi (telefono).
/// </summary>
public static class AnagraficaAzw
{
    private static string SoloCifre(string? s) => new((s ?? "").Where(char.IsDigit).ToArray());

    /// <returns>Il cliente AZW (null se non trovato) e i codici provati.</returns>
    public static async Task<(AzwCliente? Cliente, List<string> Codici)> TrovaAsync(ContestoLaboratorioAI db, AnagraficaClienteArcLab c, CancellationToken ct = default)
    {
        var codici = new List<string>();
        if (!string.IsNullOrWhiteSpace(c.CodiceCliente)) codici.Add(c.CodiceCliente.Trim());
        codici.AddRange(c.CodiciCommesse.Where(x => !codici.Contains(x)));
        AzwCliente? azw = null;
        foreach (var cod in codici)
        {
            azw = await db.AzwClienti.AsNoTracking().FirstOrDefaultAsync(x => x.CodiceCliente == cod, ct);
            if (azw is not null) return (azw, codici);
        }
        var piva = SoloCifre(c.PartitaIva);
        if (piva.Length >= 11)
        {
            var ultime = piva[^11..];
            azw = (await db.AzwClienti.AsNoTracking().Where(x => x.PartitaIva != null && x.PartitaIva.Contains(ultime)).ToListAsync(ct))
                .FirstOrDefault(x => SoloCifre(x.PartitaIva).EndsWith(ultime));
            if (azw is not null) return (azw, codici);
        }
        var cf = (c.CodiceFiscale ?? "").Trim().ToUpperInvariant();
        if (cf.Length >= 11)
            azw = await db.AzwClienti.AsNoTracking().FirstOrDefaultAsync(x => x.CodiceFiscale != null && x.CodiceFiscale.ToUpper() == cf, ct);
        return (azw, codici);
    }
}
