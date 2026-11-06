using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryLimitiArcLab" />
/// <remarks>SOLA LETTURA su ArcLab (17/10/2026).</remarks>
public class RepositoryLimitiArcLab : IRepositoryLimitiArcLab
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;
    public RepositoryLimitiArcLab(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private static readonly string Sql = $"""
        SELECT p.IdParametro, p.NomeParametro,
               ca.IdMatrice, mx.Matrice,
               cm.IDControllo AS IdControllo, co.Controllo,
               u.UnitaMisura,
               COUNT(*) AS Prove
        FROM prove pr
        INNER JOIN Campione ca      ON ca.IdCampione = pr.IdCampione
        INNER JOIN Lotto lo         ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm      ON cm.IdCommessa = lo.IdCommessa
        LEFT  JOIN Controllo co     ON co.IDControllo = cm.IDControllo
        LEFT  JOIN matrice mx       ON mx.IdMatrice = ca.IdMatrice
        INNER JOIN rigametodo rm    ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m         ON m.IdMetodo = rm.IdMetodo
        INNER JOIN Parametro p      ON p.IdParametro = m.IdParametro
        LEFT  JOIN Unitadimisura u  ON u.IdUnita = rm.IdUnita
        WHERE lo.DataRic >= DATEADD(year, -@Anni, GETDATE())
          AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
        GROUP BY p.IdParametro, p.NomeParametro, ca.IdMatrice, mx.Matrice, cm.IDControllo, co.Controllo, u.UnitaMisura
        """;

    // 18/10/2026: elenco tenuto in memoria per 6 ore (cambia poco e la query su 3 anni di prove può essere lenta).
    private static readonly SemaphoreSlim Blocco = new(1, 1);
    private static (int Anni, DateTime Quando, IReadOnlyList<ParametroUsatoArcLab> Righe)? _memoria;

    public async Task<IReadOnlyList<ParametroUsatoArcLab>> ParametriUsatiAsync(int anni = 3, CancellationToken ct = default)
    {
        if (_memoria is { } m && m.Anni == anni && DateTime.UtcNow - m.Quando < TimeSpan.FromHours(6)) return m.Righe;
        await Blocco.WaitAsync(ct);
        try
        {
            if (_memoria is { } m2 && m2.Anni == anni && DateTime.UtcNow - m2.Quando < TimeSpan.FromHours(6)) return m2.Righe;
            using var cn = await _fabbrica.ApriAsync(ct);
            var righe = (await cn.QueryAsync<ParametroUsatoArcLab>(
                new CommandDefinition(Sql, new { Anni = anni }, commandTimeout: 600, cancellationToken: ct))).ToList();
            _memoria = (anni, DateTime.UtcNow, righe);
            return righe;
        }
        finally { Blocco.Release(); }
    }

    // Fase 1b (18/10/2026): stesse condizioni di «Da Validare» (RepositoryCampioni.SqlDaValidare).
    private static readonly string SqlProveDaValidare = $"""
        WITH cand AS (
            SELECT ca.IdCampione
            FROM Campione ca
            INNER JOIN Lotto lo         ON lo.IdLotto = ca.IdLotto
            INNER JOIN commessa cm      ON cm.IdCommessa = lo.IdCommessa
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i   ON i.IDInsediamento = cc.IDInsediamento
            INNER JOIN Clienti cl       ON cl.IDCliente = i.IDCliente
            INNER JOIN prove pr         ON pr.IdCampione = ca.IdCampione
                                        AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
            WHERE (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
              AND ISNULL(ca.AnalisiChiusa, 0) = 0
            GROUP BY ca.IdCampione
            HAVING COUNT(pr.IdProva) > 0
               AND SUM(CASE WHEN pr.VerificaValore = 1 THEN 0 ELSE 1 END) = 0
        )
        SELECT pr.IdCampione, pr.IdProva, p.IdParametro, p.NomeParametro,
               ca.IdMatrice, mx.Matrice,
               lo.DataPrelievo, lo.DataRic AS DataRicezione, pr.DataInizioProva, CAST(ca.RitardoAnalisi AS int) AS RitardoAnalisi,
               CAST(pr.ValoreCalcolato AS float) AS ValoreCalcolato,
               CAST(ISNULL(pr.MaggioreDi, 0) AS bit) AS MaggioreDi, CAST(ISNULL(pr.MinoreDi, 0) AS bit) AS MinoreDi,
               u.UnitaMisura,
               CAST(CASE WHEN rm.MaxDecimali IS NULL OR rm.Arrotonda IS NULL THEN 0 ELSE 1 END AS bit) AS RegoleArrotondamento,
               rm.IdLabEsterno, le.LabEsterno, CONVERT(nvarchar(1000), le.Frase) AS FraseLabEsterno,
               CAST(ISNULL(pr.EsclusodaRdp, 0) AS bit) AS Esclusa
        FROM cand
        INNER JOIN Campione ca       ON ca.IdCampione = cand.IdCampione
        INNER JOIN Lotto lo          ON lo.IdLotto = ca.IdLotto
        LEFT  JOIN matrice mx        ON mx.IdMatrice = ca.IdMatrice
        INNER JOIN prove pr          ON pr.IdCampione = ca.IdCampione
                                     AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
        INNER JOIN rigametodo rm     ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m          ON m.IdMetodo = rm.IdMetodo
        LEFT  JOIN Parametro p       ON p.IdParametro = m.IdParametro
        LEFT  JOIN Unitadimisura u   ON u.IdUnita = rm.IdUnita
        LEFT  JOIN LabEsterno le     ON le.IdLabEsterno = rm.IdLabEsterno
        """;

    // 05/11/2026: stesse colonne, per un solo campione (anche già certificato).
    private static readonly string SqlProveCampione = $"""
        SELECT pr.IdCampione, pr.IdProva, p.IdParametro, p.NomeParametro,
               ca.IdMatrice, mx.Matrice,
               lo.DataPrelievo, lo.DataRic AS DataRicezione, pr.DataInizioProva, CAST(ca.RitardoAnalisi AS int) AS RitardoAnalisi,
               CAST(pr.ValoreCalcolato AS float) AS ValoreCalcolato,
               CAST(ISNULL(pr.MaggioreDi, 0) AS bit) AS MaggioreDi, CAST(ISNULL(pr.MinoreDi, 0) AS bit) AS MinoreDi,
               u.UnitaMisura,
               CAST(CASE WHEN rm.MaxDecimali IS NULL OR rm.Arrotonda IS NULL THEN 0 ELSE 1 END AS bit) AS RegoleArrotondamento,
               rm.IdLabEsterno, le.LabEsterno, CONVERT(nvarchar(1000), le.Frase) AS FraseLabEsterno,
               CAST(ISNULL(pr.EsclusodaRdp, 0) AS bit) AS Esclusa
        FROM Campione ca
        INNER JOIN Lotto lo          ON lo.IdLotto = ca.IdLotto
        LEFT  JOIN matrice mx        ON mx.IdMatrice = ca.IdMatrice
        INNER JOIN prove pr          ON pr.IdCampione = ca.IdCampione
                                     AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
        INNER JOIN rigametodo rm     ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m          ON m.IdMetodo = rm.IdMetodo
        LEFT  JOIN Parametro p       ON p.IdParametro = m.IdParametro
        LEFT  JOIN Unitadimisura u   ON u.IdUnita = rm.IdUnita
        LEFT  JOIN LabEsterno le     ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE ca.IdCampione = @IdCampione
        """;

    public async Task<IReadOnlyList<ProvaPerSegnalazioni>> ProveCampioneAsync(int idCampione, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ProvaPerSegnalazioni>(
            new CommandDefinition(SqlProveCampione, new { IdCampione = idCampione }, commandTimeout: 60, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ProvaPerSegnalazioni>> ProveDaValidareAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ProvaPerSegnalazioni>(
            new CommandDefinition(SqlProveDaValidare, commandTimeout: 120, cancellationToken: ct))).ToList();
    }
}
