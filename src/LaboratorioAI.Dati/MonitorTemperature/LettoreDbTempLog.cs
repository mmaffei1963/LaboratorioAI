using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace LaboratorioAI.Dati.MonitorTemperature;

/// <inheritdoc cref="ILettoreDbTempLog" />
/// <remarks>Sola lettura sul database Econorma MonitorTemperature (connessione dedicata).</remarks>
public class LettoreDbTempLog : ILettoreDbTempLog
{
    private readonly string _conn;

    public LettoreDbTempLog(IConfiguration config)
    {
        _conn = config.GetConnectionString("MonitorTemperature")
            ?? throw new InvalidOperationException("Stringa di connessione 'MonitorTemperature' non configurata.");
    }

    private sealed record RigaMisura(string Canale, DateTime DataOra, decimal? Valore);

    public async Task<FileTempLog> LeggiAsync(FonteTempLogDb fonte, DateTime dal, DateTime al, CancellationToken ct = default)
    {
        // Misure: temperatura grezza corretta con l'offset della sonda.
        // Misure1: temperatura grezza corretta con l'offset salvato per misura (OffsetApplicato).
        var (tabella, valore, etichetta) = fonte == FonteTempLogDb.Misure1
            ? ("dbo.Misure1", "(m.TemperaturaGrezza + m.OffsetApplicato)", "Misuratore 2 (Misure1)")
            : ("dbo.Misure", "(m.TemperaturaGrezza + s.OffsetTemperatura)", "Misuratore 1 (Misure)");

        var sql = $"""
            SELECT s.CodiceSonda AS Canale, m.DataOra, CAST({valore} AS decimal(9,3)) AS Valore
            FROM {tabella} m
                INNER JOIN dbo.Sonde s ON s.IdSonda = m.IdSonda
            WHERE m.DataOra >= @Dal AND m.DataOra < @Al AND s.Attiva = 1
            ORDER BY s.CodiceSonda, m.DataOra
            """;

        using var cn = new SqlConnection(_conn);
        var righe = (await cn.QueryAsync<RigaMisura>(
            new CommandDefinition(sql, new { Dal = dal, Al = al }, cancellationToken: ct))).ToList();

        var canali = righe.Select(r => r.Canale).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

        var letture = righe
            .GroupBy(r => r.Canale, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<LetturaTempLog>)g.OrderBy(x => x.DataOra)
                    .Select(x => new LetturaTempLog { Ora = x.DataOra.ToString("dd/MM HH:mm"), Valore = (double?)x.Valore })
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        return new FileTempLog
        {
            DataDal = righe.Count > 0 ? righe.Min(r => r.DataOra) : dal,
            DataAl = righe.Count > 0 ? righe.Max(r => r.DataOra) : al,
            NomeFile = $"{etichetta} · {dal:dd/MM/yyyy HH:mm}–{al:dd/MM/yyyy HH:mm}",
            Canali = canali,
            Letture = letture
        };
    }

    private sealed record RigaLimite(string Canale, double? VN, double? Min, double? Max, string? Ambiente, int? IdApparecchioAmbiente);

    public async Task<IReadOnlyList<LimitiCanale>> LimitiAsync(IReadOnlyList<string> canali, CancellationToken ct = default)
    {
        var lista = canali?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                    ?? new List<string>();
        if (lista.Count == 0) return Array.Empty<LimitiCanale>();

        // Assegnazione corrente = quella aperta (DataFine IS NULL); in mancanza, la più recente per DataInizio.
        const string sql = """
            SELECT s.CodiceSonda                  AS Canale,
                   CAST(a.ValoreNominale AS float) AS VN,
                   CAST(a.LimiteMin AS float)      AS Min,
                   CAST(a.LimiteMax AS float)      AS Max,
                   a.Descrizione                  AS Ambiente,
                   a.IdApparecchiatura            AS IdApparecchioAmbiente
            FROM dbo.Sonde s
                OUTER APPLY (
                    SELECT TOP 1 asg.IdApparecchiatura
                    FROM dbo.AssegnazioneSonde asg
                    WHERE asg.IdSonda = s.IdSonda
                    ORDER BY CASE WHEN asg.DataFine IS NULL THEN 0 ELSE 1 END, asg.DataInizio DESC
                ) cur
                LEFT JOIN dbo.Apparecchiature a ON a.IdApparecchiatura = cur.IdApparecchiatura
            WHERE s.CodiceSonda IN @Canali AND s.Attiva = 1
            """;

        using var cn = new SqlConnection(_conn);
        var righe = (await cn.QueryAsync<RigaLimite>(
            new CommandDefinition(sql, new { Canali = lista }, cancellationToken: ct))).ToList();
        var perCanale = righe.ToDictionary(r => r.Canale, r => r, StringComparer.OrdinalIgnoreCase);

        var risultato = new List<LimitiCanale>();
        foreach (var canale in lista)
        {
            perCanale.TryGetValue(canale, out var r);
            risultato.Add(new LimitiCanale
            {
                Canale = canale,
                Ambiente = r?.Ambiente,
                IdApparecchioAmbiente = r?.IdApparecchioAmbiente,
                VN = r?.VN,
                Min = r?.Min,
                Max = r?.Max,
                MotivoMancanza = (r?.Min is null || r?.Max is null)
                    ? "Limiti non impostati sull'apparecchiatura assegnata (MonitorTemperature)." : null
            });
        }
        return risultato;
    }
}
