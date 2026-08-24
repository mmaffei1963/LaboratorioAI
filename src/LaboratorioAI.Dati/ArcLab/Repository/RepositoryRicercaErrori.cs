using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryRicercaErrori" />
/// <remarks>SOLA LETTURA su LabAnalisiDati. Solo verifiche diagnostiche, nessuna modifica.</remarks>
public class RepositoryRicercaErrori : IRepositoryRicercaErrori
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryRicercaErrori(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // ── 1. Doppioni di parametro sui rapporti di prova ──────────────
    private const string SqlDuplicati = """
        SELECT
            q.IdCertificato, q.NCertificato, q.DataCertificato,
            q.IdCampione, q.CodCampione,
            q.IdParametro, q.NomeParametro, q.IdTipoParametro,
            q.IdRigaMetodo, q.DenominazioneRiga, q.IdProva,
            q.ValoreCalcolato, q.MaggioreDi, q.MinoreDi,
            CASE WHEN q.IdParametro = 236 THEN q.OccPar ELSE q.OccDenom END AS Occorrenze
        FROM (
            SELECT
                ce.IdCertificato, ce.NCertificato, ce.DataCertificato,
                ca.IdCampione, ca.CodCampione,
                m.IdParametro, p.NomeParametro, p.IdTipoParametro,
                pr.IdRigaMetodo,
                LTRIM(RTRIM(rm.DenominazioneRiga)) AS DenominazioneRiga,
                pr.IdProva,
                pr.ValoreCalcolato,
                ISNULL(pr.MaggioreDi, 0) AS MaggioreDi,
                ISNULL(pr.MinoreDi,   0) AS MinoreDi,
                COUNT(*)             OVER (PARTITION BY ca.IdCampione, m.IdParametro, LTRIM(RTRIM(rm.DenominazioneRiga))) AS OccDenom,
                COUNT(*)             OVER (PARTITION BY ca.IdCampione, m.IdParametro)                                     AS OccPar,
                MIN(pr.IdRigaMetodo) OVER (PARTITION BY ca.IdCampione, m.IdParametro)                                     AS MinRigaPar,
                MAX(pr.IdRigaMetodo) OVER (PARTITION BY ca.IdCampione, m.IdParametro)                                     AS MaxRigaPar
            FROM Certificato ce
                INNER JOIN Campione   ca ON ca.IdCertificato = ce.IdCertificato
                INNER JOIN prove      pr ON pr.IdCampione    = ca.IdCampione
                INNER JOIN rigametodo rm ON rm.IdRigametodo  = pr.IdRigaMetodo
                INNER JOIN Metodo     m  ON m.IdMetodo        = rm.IdMetodo
                LEFT  JOIN Parametro  p  ON p.IdParametro     = m.IdParametro
            WHERE ce.DataCertificato >= @Da
              AND ce.DataCertificato <  @A
              AND pr.IdRigaMetodo <> 18963
              AND ISNULL(p.IdTipoParametro, 0) <> 2
        ) q
        WHERE
            (q.IdParametro <> 236 AND q.OccDenom > 1)
            OR
            (q.IdParametro = 236 AND q.OccPar > 1
                AND NOT (q.OccPar = 2 AND q.MinRigaPar <> q.MaxRigaPar))
        ORDER BY Occorrenze DESC, q.IdCampione, q.IdParametro, q.DenominazioneRiga, q.IdRigaMetodo
        """;

    public async Task<IReadOnlyList<RigaDuplicatoRapporto>> DuplicatiRapportiAsync(int annoDa, int annoA, CancellationToken ct = default)
    {
        if (annoA < annoDa) (annoDa, annoA) = (annoA, annoDa);
        var da = new DateTime(annoDa, 1, 1);
        var a = new DateTime(annoA, 1, 1).AddYears(1);   // primo giorno dell'anno successivo (estremo escluso)
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaDuplicatoRapporto>(
            new CommandDefinition(SqlDuplicati, new { Da = da, A = a }, cancellationToken: ct))).ToList();
    }

    // ── 2. Metodi attivi senza cifre significative / decimali max ───
    private const string SqlSenzaArrotondamento = """
        SELECT
            m.IdMetodo, m.IdParametro, m.IdTipoMetodo,
            m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
            m.Cifre_significative AS CifreSignificative,
            m.Decimali_Max        AS DecimaliMax
        FROM metodo m
        WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
          AND (m.Cifre_significative IS NULL OR m.Decimali_Max IS NULL)
        ORDER BY m.IdParametro, m.IdMetodo
        """;

    public async Task<IReadOnlyList<RigaMetodoArrotondamento>> MetodiSenzaArrotondamentoAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoArrotondamento>(
            new CommandDefinition(SqlSenzaArrotondamento, cancellationToken: ct))).ToList();
    }

    // ── 3. Metodi attivi con margine sotto soglia (o negativo) ──────
    private const string SqlMargine = """
        SELECT
            m.IdMetodo, m.IdParametro, m.IdTipoMetodo,
            m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
            m.CostoAnoi, m.CostoMetodoEuro,
            CAST(100.0 * (m.CostoMetodoEuro - m.CostoAnoi) / NULLIF(m.CostoMetodoEuro, 0) AS DECIMAL(9,2)) AS MarginePct
        FROM metodo m
        WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
          AND m.CostoAnoi IS NOT NULL
          AND m.CostoMetodoEuro IS NOT NULL AND m.CostoMetodoEuro <> 0
          AND (m.CostoMetodoEuro - m.CostoAnoi) / m.CostoMetodoEuro < @Soglia
        ORDER BY MarginePct
        """;

    public async Task<IReadOnlyList<RigaMetodoMargine>> MetodiMargineAsync(decimal sogliaFrazione, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoMargine>(
            new CommandDefinition(SqlMargine, new { Soglia = sogliaFrazione }, cancellationToken: ct))).ToList();
    }

    // ── 4. Metodi attivi potenzialmente doppi ───────────────────────
    private const string SqlDoppi = """
        SELECT x.IdMetodo, x.IdParametro, x.IdTipoMetodo,
               x.RifMetodo, x.DettagliMetodo, x.RifBiblio, x.IdUnita, x.NMetodi
        FROM (
            SELECT m.IdMetodo, m.IdParametro, m.IdTipoMetodo,
                   m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
                   COUNT(*) OVER (PARTITION BY m.IdParametro,
                                  LOWER(LTRIM(RTRIM(m.DettagliMetodo))),
                                  LOWER(LTRIM(RTRIM(m.RifMetodo)))) AS NMetodi
            FROM metodo m
            WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
        ) x
        WHERE x.NMetodi > 1
        ORDER BY x.IdParametro, x.DettagliMetodo, x.RifMetodo, x.IdMetodo
        """;

    public async Task<IReadOnlyList<RigaMetodoDoppio>> MetodiDoppiAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoDoppio>(
            new CommandDefinition(SqlDoppi, cancellationToken: ct))).ToList();
    }
}
