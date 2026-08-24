using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryScadenziario" />
///
/// <remarks>
/// SOLA LETTURA. Traduce in T-SQL la query Access dello scadenziario tarature:
/// piani attivi con IdAttività 25/26, apparecchiature in uso, scadenza = ultima
/// taratura + FreqGiorni, entro il preavviso o mai eseguita.
///
/// NOTA DA VERIFICARE: in Access «UltimiInterventi» è una query salvata che calcola
/// MAX(DataInt) per IdPianoInt. Qui è ricalcolata sulla tabella base degli interventi:
/// se in ArcLab (SQL Server) la tabella NON si chiama «Interventi» o la colonna data
/// non è «DataInt», va corretto il nome nella sottoquery indicata sotto.
/// </remarks>
public class RepositoryScadenziario : IRepositoryScadenziario
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryScadenziario(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private sealed record Riga(
        int IdApparecchio, int? IdStrumento, string Strumento, string? Attivita, string? NomePiano,
        DateTime? UltimaTaratura, DateTime? Scadenza, int? FreqGiorni);

    public async Task<IReadOnlyList<VoceScadenziarioInterno>> ScadenziarioInterniAsync(
        int giorniPreavviso = 30, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                pi.IdApparecchio                                                       AS IdApparecchio,
                s.IdStrumento                                                          AS IdStrumento,
                LTRIM(RTRIM(ISNULL(s.NomeStrum,'') + ' [' + ISNULL(a.NSerieApp,'') + '] ' + ISNULL(a.DenApp,''))) AS Strumento,
                att.[Attività]                                                         AS Attivita,
                pi.NomePiano                                                           AS NomePiano,
                ui.MaxDiDataInt                                                        AS UltimaTaratura,
                DATEADD(day, pi.FreqGiorni, ui.MaxDiDataInt)                           AS Scadenza,
                pi.FreqGiorni                                                          AS FreqGiorni
            FROM PianoInterventi pi
                INNER JOIN apparecchiature a ON a.IdApparecchio = pi.IdApparecchio
                INNER JOIN Strumenti s       ON s.IdStrumento   = a.IdStrumento
                LEFT JOIN [Attività] att     ON att.[IdAttività] = pi.[IdAttività]
                /* «UltimiInterventi» di Access = MAX(DataInt) per piano su SchedeIntervento */
                LEFT JOIN (
                    SELECT IdPianoInt, MAX(DataInt) AS MaxDiDataInt
                    FROM SchedeIntervento
                    GROUP BY IdPianoInt
                ) ui ON ui.IdPianoInt = pi.IdPianoInt
            WHERE pi.Attivo = 1
              AND pi.[IdAttività] IN (25, 26)
              AND a.NonInUso = 0
              AND pi.FreqGiorni IS NOT NULL
              AND ( DATEADD(day, pi.FreqGiorni, ui.MaxDiDataInt) <= DATEADD(day, @Giorni, CAST(GETDATE() AS date))
                    OR ui.MaxDiDataInt IS NULL )
            ORDER BY Scadenza
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<Riga>(
            new CommandDefinition(sql, new { Giorni = giorniPreavviso }, cancellationToken: ct));

        return righe.Select(r => new VoceScadenziarioInterno
        {
            IdApparecchio = r.IdApparecchio,
            IdStrumento = r.IdStrumento,
            Strumento = r.Strumento,
            Attivita = r.Attivita,
            NomePiano = r.NomePiano,
            UltimaTaratura = r.UltimaTaratura,
            Scadenza = r.Scadenza,
            FreqGiorni = r.FreqGiorni,
            Gruppo = ClassificatoreStrumento.Classifica(r.Strumento)
        }).ToList();
    }
}
