using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryPiscine" />
public class RepositoryPiscine : IRepositoryPiscine
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryPiscine(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // Matrici ArcLab delle acque di piscina.
    private const int IdMatriceInVasca    = 214; // "acqua di piscina - in vasca"
    private const int IdMatriceImmissione = 213; // "acqua di piscina - immissione in vasca"

    // Alias fissi: la mappatura dei record dipende dagli alias, non dai nomi reali.
    private const string SqlAnagrafica = """
        SELECT
            c.IDCliente        AS IdCliente,
            i.IDInsediamento   AS IdInsediamento,
            c.RagioneSociale   AS RagioneSociale,
            c.PIVA             AS PartitaIva,
            c.CODContabilita   AS CodContabilita,
            c.CodFisc          AS CodiceFiscale,
            c.Stagionale       AS Stagionale,
            CONCAT(c.IndirizzoFatt, ', ', c.[CittàFatt], ' (', c.ProvinciaFatt, ')') AS SedeLegale,
            i.IndirizzoInsed   AS Indirizzo,
            i.[CittàInsed]     AS Comune,
            i.ProvinciaInsed   AS Provincia
        FROM Insediamento i
        INNER JOIN Clienti c ON c.IDCliente = i.IDCliente
        """;

    public async Task<AnagraficaPiscina?> LeggiAnagraficaAsync(
        int idInsediamento, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<AnagraficaPiscina>(
            new CommandDefinition($"{SqlAnagrafica} WHERE i.IDInsediamento = @Id",
                new { Id = idInsediamento }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<AnagraficaPiscina>> LeggiInsediamentiClienteAsync(
        int idCliente, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<AnagraficaPiscina>(
            new CommandDefinition($"{SqlAnagrafica} WHERE i.IDCliente = @Id ORDER BY i.[CittàInsed]",
                new { Id = idCliente }, cancellationToken: ct));
        return righe.ToList();
    }

    public async Task<IReadOnlyList<AnagraficaPiscina>> CercaInsediamentiAsync(
        string testo, CancellationToken ct = default)
    {
        var filtro = $"%{(testo ?? string.Empty).Trim()}%";
        var sql = $"""
            {SqlAnagrafica}
            WHERE c.RagioneSociale LIKE @F OR i.[CittàInsed] LIKE @F OR c.PIVA LIKE @F
            ORDER BY c.RagioneSociale, i.[CittàInsed]
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<AnagraficaPiscina>(
            new CommandDefinition(sql, new { F = filtro }, cancellationToken: ct));
        return righe.ToList();
    }

    private const string SqlPuntiStorici = """
        SELECT DISTINCT
            LTRIM(RTRIM(ca.PuntoPrelievo)) AS Punto,
            LTRIM(RTRIM(ca.LuogoPrelievo)) AS Ubicazione
        FROM Campione ca
        INNER JOIN Lotto lo    ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm ON cm.IdCommessa = lo.IdCommessa
        WHERE cm.IDInsediamento = @Id
          AND ca.IdMatrice IN (@Vasca, @Immissione)
          AND NULLIF(LTRIM(RTRIM(ca.PuntoPrelievo)), '') IS NOT NULL
        ORDER BY Punto
        """;

    public async Task<IReadOnlyList<PuntoPrelievo>> LeggiPuntiPrelievoStoriciAsync(
        int idInsediamento, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<PuntoPrelievo>(
            new CommandDefinition(SqlPuntiStorici,
                new { Id = idInsediamento, Vasca = IdMatriceInVasca, Immissione = IdMatriceImmissione },
                cancellationToken: ct));
        return righe.ToList();
    }

    private const string SqlAnalisiStoriche = """
        SELECT
            ca.IdCampione,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            lo.DataPrelievo,
            lo.DataRic        AS DataRicezione,
            mx.Matrice,
            ca.PuntoPrelievo,
            ca.LuogoPrelievo,
            NULLIF(ca.IdCertificato, 0) AS IdCertificato,
            ce.NCertificato   AS NumeroCertificato,
            ce.DataCertificato
        FROM Campione ca
        INNER JOIN Lotto lo    ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm ON cm.IdCommessa = lo.IdCommessa
        LEFT  JOIN matrice mx  ON mx.IdMatrice = ca.IdMatrice
        LEFT  JOIN Certificato ce ON ce.IdCertificato = ca.IdCertificato
        WHERE cm.IDInsediamento = @Id
          AND ca.IdMatrice IN (@Vasca, @Immissione)
          AND lo.DataRic >= DATEADD(year, -5, GETDATE())
        ORDER BY lo.DataRic DESC, lo.CodLotto DESC
        """;

    public async Task<IReadOnlyList<AnalisiStoricaPiscina>> LeggiAnalisiStoricheAsync(
        int idInsediamento, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<AnalisiStoricaPiscina>(
            new CommandDefinition(SqlAnalisiStoriche,
                new { Id = idInsediamento, Vasca = IdMatriceInVasca, Immissione = IdMatriceImmissione },
                cancellationToken: ct));
        return righe.ToList();
    }
}
