using Dapper;
using LaboratorioAI.Dati.MonitorTemperature.Models;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.MonitorTemperature;

// 03/11/2026: seriali attivi delle sonde e limiti delle apparecchiature del monitoraggio (schema temperature,
// le stesse tabelle usate da EconormaAcquisizione). ArcLab non è mai scritto: i limiti si leggono da ArcLab
// (RepositoryArcLabSonde) e si copiano in temperature.Apparecchiature solo dopo conferma.

/// <summary>Seriale fisico attivo di una sonda, come lo registra EconormaAcquisizione.</summary>
public record SerialeAttivoSonda(string CodiceSonda, string SerialeSonda, DateTime DataInizio);

/// <summary>Limiti di un'apparecchiatura del monitoraggio (quelli usati per gli allarmi e dal Monitor controllo).</summary>
public record LimitiApparecchiatura(int IdApparecchiatura, string CodiceApparecchiatura, string? Descrizione, decimal? LimiteMin, decimal? LimiteMax);

/// <summary>Confronto dei limiti di un'apparecchiatura: monitoraggio contro ArcLab.</summary>
public record DifferenzaLimiti(
    string CodiceApparecchiatura, string Descrizione, string Sonde,
    decimal? MinMonitor, decimal? MaxMonitor, decimal? MinArcLab, decimal? MaxArcLab,
    EsitoConfrontoLimiti Esito);

public enum EsitoConfrontoLimiti
{
    /// <summary>Limiti diversi: si possono allineare.</summary>
    Diversi,
    Uguali,
    /// <summary>In ArcLab le sonde della stessa apparecchiatura hanno limiti diversi: non si aggiorna.</summary>
    Conflitto,
    /// <summary>In ArcLab l'apparecchiatura non ha limiti.</summary>
    SenzaLimitiArcLab,
    /// <summary>L'apparecchiatura non è (attiva) nel monitoraggio.</summary>
    NonNelMonitor
}

public static class ConfrontoLimitiSonde
{
    /// <summary>Codice dell'apparecchiatura nel monitoraggio: «ARC» + Id dell'apparecchiatura associata in ArcLab.</summary>
    public static string Codice(int idApparecchioAssociato) => $"ARC{idApparecchioAssociato}";

    private static decimal? R(decimal? v) => v is decimal d ? Math.Round(d, 2) : null;

    /// <summary>Un confronto per apparecchiatura ArcLab con sonde attive (stessa regola della vecchia «Configurazione valore nominale»).</summary>
    public static IReadOnlyList<DifferenzaLimiti> Confronta(IEnumerable<SondaArcLab> arcLab, IEnumerable<LimitiApparecchiatura> monitor)
    {
        var perCodice = monitor.GroupBy(m => m.CodiceApparecchiatura, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var esito = new List<DifferenzaLimiti>();
        foreach (var g in arcLab.GroupBy(x => x.IdApparecchioAssociato).OrderBy(g => g.Key))
        {
            var codice = Codice(g.Key);
            var mins = g.Where(x => x.LimInfInt.HasValue).Select(x => R(x.LimInfInt)!.Value).Distinct().ToList();
            var maxs = g.Where(x => x.LimSupInt.HasValue).Select(x => R(x.LimSupInt)!.Value).Distinct().ToList();
            var sonde = string.Join(", ", g.Select(x => CodiceSonda(x.NomeSonda)).Distinct().OrderBy(x => x.Length).ThenBy(x => x));
            perCodice.TryGetValue(codice, out var m);
            decimal? minA = mins.Count == 1 ? mins[0] : null, maxA = maxs.Count == 1 ? maxs[0] : null;
            EsitoConfrontoLimiti e;
            if (m is null) e = EsitoConfrontoLimiti.NonNelMonitor;
            else if (mins.Count > 1 || maxs.Count > 1) e = EsitoConfrontoLimiti.Conflitto;
            else if (minA is null && maxA is null) e = EsitoConfrontoLimiti.SenzaLimitiArcLab;
            else if ((minA is not null && minA != R(m.LimiteMin)) || (maxA is not null && maxA != R(m.LimiteMax))) e = EsitoConfrontoLimiti.Diversi;
            else e = EsitoConfrontoLimiti.Uguali;
            esito.Add(new DifferenzaLimiti(codice, m?.Descrizione ?? g.First().NomeApparecchiatura, sonde,
                R(m?.LimiteMin), R(m?.LimiteMax), minA, maxA, e));
        }
        return esito;
    }

    /// <summary>«Econorma FT 105\RF - Sonda T10» → «T10».</summary>
    public static string CodiceSonda(string? nomeSonda)
    {
        if (string.IsNullOrWhiteSpace(nomeSonda)) return "";
        const string testo = "Sonda ";
        var p = nomeSonda.LastIndexOf(testo, StringComparison.OrdinalIgnoreCase);
        var c = (p < 0 ? nomeSonda : nomeSonda[(p + testo.Length)..]).Trim().ToUpperInvariant();
        return c.StartsWith('T') && int.TryParse(c[1..], out var n) ? $"T{n}" : c;
    }
}

public class RepositoryMonitorSonde
{
    private readonly ContestoMonitorTemperature _ctx;
    public RepositoryMonitorSonde(ContestoMonitorTemperature ctx) => _ctx = ctx;

    /// <summary>Seriale attivo per codice sonda (temperature.StoricoSerialiSonde, scritto da EconormaAcquisizione).</summary>
    public async Task<IReadOnlyDictionary<string, SerialeAttivoSonda>> SerialiAttiviAsync()
    {
        const string sql = """
            SELECT s.CodiceSonda, st.SerialeSonda, st.DataInizio
            FROM temperature.StoricoSerialiSonde st
            JOIN temperature.Sonde s ON s.IdSonda = st.IdSonda
            WHERE st.Attiva = 1
            ORDER BY st.DataInizio DESC, st.IdStoricoSeriale DESC
            """;
        var cn = _ctx.Database.GetDbConnection();
        var righe = await cn.QueryAsync<SerialeAttivoSonda>(sql);
        var d = new Dictionary<string, SerialeAttivoSonda>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in righe)
            d.TryAdd(ConfrontoLimitiSonde.CodiceSonda("Sonda " + r.CodiceSonda), r);   // il più recente vince
        return d;
    }

    /// <summary>Apparecchiature attive del monitoraggio con i loro limiti.</summary>
    public async Task<IReadOnlyList<LimitiApparecchiatura>> ApparecchiatureAsync()
    {
        const string sql = """
            SELECT IdApparecchiatura, CodiceApparecchiatura, Descrizione,
                   CAST(LimiteMin AS decimal(9,3)) AS LimiteMin, CAST(LimiteMax AS decimal(9,3)) AS LimiteMax
            FROM temperature.Apparecchiature
            WHERE Attiva = 1
            """;
        var cn = _ctx.Database.GetDbConnection();
        return (await cn.QueryAsync<LimitiApparecchiatura>(sql)).ToList();
    }

    /// <summary>
    /// Copia i limiti di ArcLab nelle apparecchiature indicate (una transazione). Un limite assente in ArcLab
    /// lascia quello attuale. Restituisce il numero di apparecchiature aggiornate.
    /// </summary>
    public async Task<int> AggiornaLimitiAsync(IEnumerable<DifferenzaLimiti> daAggiornare)
    {
        var lista = daAggiornare.Where(d => d.Esito == EsitoConfrontoLimiti.Diversi).ToList();
        if (lista.Count == 0) return 0;
        var cn = _ctx.Database.GetDbConnection();
        if (cn.State != System.Data.ConnectionState.Open) await cn.OpenAsync();
        await using var tr = await cn.BeginTransactionAsync();
        var n = 0;
        foreach (var d in lista)
            n += await cn.ExecuteAsync("""
                UPDATE temperature.Apparecchiature
                SET LimiteMin = COALESCE(@Min, LimiteMin), LimiteMax = COALESCE(@Max, LimiteMax)
                WHERE CodiceApparecchiatura = @Codice AND Attiva = 1
                """, new { Min = d.MinArcLab, Max = d.MaxArcLab, Codice = d.CodiceApparecchiatura }, tr);
        await tr.CommitAsync();
        return n;
    }
}
