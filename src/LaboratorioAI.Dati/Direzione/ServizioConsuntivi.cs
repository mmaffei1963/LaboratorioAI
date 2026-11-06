using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Direzione;

/// <summary>
/// Consuntivo dei costi e dei ricavi per area (20/10/2026). Legge le registrazioni della contabilità analitica importate
/// da AZW (<c>dir.RegistrazioniAnalitiche</c>) e le passa a <see cref="MotoreConsuntivo"/>. Solo lettura.
/// </summary>
public class ServizioConsuntivi
{
    private readonly ContestoLaboratorioAI _ctx;

    public ServizioConsuntivi(ContestoLaboratorioAI ctx) => _ctx = ctx;

    /// <summary>Periodo predefinito: dal 1° gennaio alla fine del mese scorso (a gennaio: tutto l'anno prima).</summary>
    public static (DateTime Dal, DateTime Al) PeriodoPredefinito(DateTime oggi)
    {
        var fineMesePrecedente = new DateTime(oggi.Year, oggi.Month, 1).AddDays(-1);
        return (new DateTime(fineMesePrecedente.Year, 1, 1), fineMesePrecedente);
    }

    public async Task<RisultatoConsuntivo> CalcolaAsync(DateTime dal, DateTime al, CancellationToken ct = default)
    {
        var (dalP, _) = MotoreConsuntivo.PeriodoPrecedente(dal.Date, al.Date);
        var limite = al.Date;
        var righe = await _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= dalP && r.DataRegistrazione <= limite)
            .Select(r => new RigaConsuntivo(r.DataRegistrazione, r.Centro, r.Voce, r.Importo))
            .ToListAsync(ct);
        var voci = (await _ctx.ClassificazioniVoci.AsNoTracking().Select(v => new { v.CodiceVoce, v.Descrizione, v.Ricavo }).ToListAsync(ct))
            .GroupBy(v => v.CodiceVoce.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (g.First().Descrizione ?? "", g.First().Ricavo), StringComparer.OrdinalIgnoreCase);
        var centri = (await _ctx.CentriCostoAzw.AsNoTracking().ToListAsync(ct))
            .GroupBy(c => c.Codice.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First().Descrizione, StringComparer.OrdinalIgnoreCase);
        return MotoreConsuntivo.Calcola(righe, dal, al, voci, centri);
    }

    /// <summary>Registrazioni di una voce (e di un'area, se indicata) nel periodo, per il dettaglio.</summary>
    public async Task<List<RegistrazioneAnalitica>> DettaglioAsync(DateTime dal, DateTime al, string voce, string? area, CancellationToken ct = default)
    {
        var d = dal.Date; var a = al.Date;
        var q = _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= d && r.DataRegistrazione <= a && r.Voce == voce);
        if (area is not null)
        {
            var centri = MotoreConsuntivo.Accorpamenti.Where(x => x.Value == area).Select(x => x.Key).Append(area).ToList();
            q = q.Where(r => centri.Contains(r.Centro));
        }
        return await q.OrderBy(r => r.DataRegistrazione).ThenBy(r => r.NumeroRegistrazione).ToListAsync(ct);
    }

    /// <summary>Tutte le registrazioni del periodo (foglio Excel).</summary>
    public Task<List<RegistrazioneAnalitica>> RegistrazioniAsync(DateTime dal, DateTime al, CancellationToken ct = default)
    {
        var d = dal.Date; var a = al.Date;
        return _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= d && r.DataRegistrazione <= a)
            .OrderBy(r => r.DataRegistrazione).ThenBy(r => r.NumeroRegistrazione).ToListAsync(ct);
    }

    /// <summary>Data dell'ultima registrazione importata e dell'ultima importazione riuscita (UTC).</summary>
    /// <remarks>22/10/2026: anche l'avviso dell'ultima importazione (es. contabilità analitica non aggiornata).</remarks>
    public async Task<(DateTime? UltimaRegistrazione, DateTime? UltimaImportazione, string? Avviso)> StatoDatiAsync(CancellationToken ct = default)
    {
        var reg = await _ctx.RegistrazioniAnalitiche.AsNoTracking().MaxAsync(r => (DateTime?)r.DataRegistrazione, ct);
        var ultima = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).OrderByDescending(i => i.DataOra)
            .Select(i => new { i.DataOra, i.Messaggio }).FirstOrDefaultAsync(ct);
        return (reg, ultima?.DataOra, ultima?.Messaggio);
    }
}
