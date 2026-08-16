using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryTempLog" />
///
/// <remarks>
/// SOLA LETTURA. Risolve, per ogni sonda Econorma, l'ambiente monitorato (dal testo
/// Associato1 che termina con "Cod: &lt;IdApparecchio&gt;") e i limiti dal PianoInterventi
/// di quell'ambiente con NomePiano "TARATURA" (mai "MAPPATURA"); match CodPiano tollerante
/// ai separatori. Min = LimInfInt, Max = LimSupInt, VN = ValTeorico.
/// </remarks>
public class RepositoryTempLog : IRepositoryTempLog
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryTempLog(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Base = """
        SELECT
            'T' + RIGHT('0' + ch.Numero, 2)                     AS CanaleIpotesi,
            s.IdStrumento                                       AS IdStrumento,
            LTRIM(RTRIM(s.NomeStrum))                           AS Sonda,
            aSonda.IdApparecchio                                AS IdApparecchioSonda,
            aSonda.NonInUso                                     AS SondaNonInUso,
            amb.NomeAmbiente                                    AS Ambiente,
            amb.IdApparecchioAmbiente                           AS IdApparecchioAmbiente,
            pi.CodPiano                                         AS CodPiano,
            pi.NomePiano                                        AS NomePiano,
            pi.ValTeorico                                       AS VN,
            pi.LimInfInt                                        AS [Min],
            pi.LimSupInt                                        AS [Max]
        FROM Strumenti s
            INNER JOIN apparecchiature aSonda ON aSonda.IdStrumento = s.IdStrumento
            CROSS APPLY (
                SELECT LTRIM(LEFT(coda, CHARINDEX(' ', coda + ' ') - 1)) AS Numero
                FROM (SELECT SUBSTRING(s.NomeStrum, PATINDEX('%Sonda T%', s.NomeStrum) + 7, 10) AS coda) t
            ) ch
            CROSS APPLY (
                SELECT
                    TRY_CONVERT(int, LTRIM(RTRIM(RIGHT(aSonda.Associato1,
                        LEN(aSonda.Associato1) - CHARINDEX('Cod:', aSonda.Associato1) - 3)))) AS IdApparecchioAmbiente,
                    CASE WHEN CHARINDEX(' -', aSonda.Associato1) > 0
                         THEN LTRIM(RTRIM(LEFT(aSonda.Associato1, CHARINDEX(' -', aSonda.Associato1) - 1)))
                         ELSE aSonda.Associato1 END AS NomeAmbiente
            ) amb
            OUTER APPLY (
                SELECT TOP 1 p.CodPiano, p.NomePiano, p.ValTeorico, p.LimInfInt, p.LimSupInt
                FROM PianoInterventi p
                WHERE p.IdApparecchio = amb.IdApparecchioAmbiente
                  AND p.Attivo = 1
                  AND p.NomePiano LIKE '%TARATURA%'
                  AND p.NomePiano NOT LIKE '%MAPPATURA%'
                ORDER BY CASE
                    WHEN REPLACE(REPLACE(REPLACE(p.CodPiano,' ',''),'-',''),'.','') LIKE '%IO0402%'
                    THEN 0 ELSE 1 END
            ) pi
        WHERE s.NomeStrum LIKE '%Econorma%Sonda T%'
        """;

    public async Task<IReadOnlyList<SondaEconorma>> SondeEconormaAsync(CancellationToken ct = default)
    {
        const string sql = Base + "\nORDER BY CanaleIpotesi";
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<SondaEconorma>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<LimitiTaratura>> LimitiPerStrumentiAsync(
        IReadOnlyList<int> idStrumenti, CancellationToken ct = default)
    {
        if (idStrumenti.Count == 0) return new List<LimitiTaratura>();

        const string sql = Base + "\n  AND s.IdStrumento IN @Ids";
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<SondaEconorma>(
            new CommandDefinition(sql, new { Ids = idStrumenti }, cancellationToken: ct));

        return righe.Select(r => new LimitiTaratura
        {
            IdStrumento = r.IdStrumento,
            Ambiente = r.Ambiente,
            VN = r.VN,
            Min = r.Min,
            Max = r.Max,
            MotivoMancanza = r.Ambiente is null ? "Sonda senza ambiente associato in ArcLab."
                : (r.Min is null || r.Max is null) ? "Piano di taratura senza limiti (LimInf/LimSup)."
                : null
        }).ToList();
    }
}
