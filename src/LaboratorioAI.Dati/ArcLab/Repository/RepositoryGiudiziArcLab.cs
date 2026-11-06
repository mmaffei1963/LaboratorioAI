using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryGiudiziArcLab" />
/// <remarks>SOLA LETTURA su ArcLab (18/10/2026).</remarks>
public class RepositoryGiudiziArcLab : IRepositoryGiudiziArcLab
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;
    public RepositoryGiudiziArcLab(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private static readonly string SqlStati = $"""
        SELECT ca.IdCampione, CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione, ca.IdMatrice,
               CAST(ca.AnalisiChiusa AS bit) AS AnalisiChiusa, CAST(ISNULL(cl.GiudizioConf, 0) AS bit) AS GiudizioConf, ca.IdCertificato,
               (SELECT COUNT(*) FROM prove pr WHERE pr.IdCampione = ca.IdCampione AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) AS Prove,
               (SELECT COUNT(*) FROM prove pr WHERE pr.IdCampione = ca.IdCampione AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
                    AND ISNULL(pr.VerificaValore, 0) <> 1) AS ProveNonVerificate
        FROM Campione ca
        LEFT JOIN Lotto lo             ON lo.IdLotto = ca.IdLotto
        LEFT JOIN commessa cm          ON cm.IdCommessa = lo.IdCommessa
        LEFT JOIN ContattoCliente cc   ON cc.IDContatto = cm.IDContatto
        LEFT JOIN Insediamento i       ON i.IDInsediamento = cc.IDInsediamento
        LEFT JOIN Clienti cl           ON cl.IDCliente = i.IDCliente
        WHERE ca.IdCampione IN @Ids;
        SELECT gc.IdCampione, t.IdGiudizio, t.CodGiudizio, t.Giudizio, CAST(t.Giudizio_conformita AS bit) AS GiudizioConformita
        FROM GiudCamp gc
        INNER JOIN TipoGiudizi t ON t.IdGiudizio = gc.IdGiudizio
        WHERE gc.IdCampione IN @Ids AND gc.IdGiudizio IS NOT NULL
        ORDER BY gc.IdGiudCamp;
        """;

    public async Task<StatoCampioneArcLab?> StatoAsync(int idCampione, CancellationToken ct = default) =>
        (await StatiAsync([idCampione], ct)).FirstOrDefault();

    public async Task<IReadOnlyList<StatoCampioneArcLab>> StatiAsync(IReadOnlyCollection<int> idCampioni, CancellationToken ct = default)
    {
        if (idCampioni.Count == 0) return [];
        var risultato = new List<StatoCampioneArcLab>();
        using var cn = await _fabbrica.ApriAsync(ct);
        foreach (var blocco in idCampioni.Distinct().Chunk(500))
        {
            using var multi = await cn.QueryMultipleAsync(new CommandDefinition(SqlStati, new { Ids = blocco }, commandTimeout: 60, cancellationToken: ct));
            var stati = (await multi.ReadAsync<StatoCampioneArcLab>()).ToList();
            var giudizi = (await multi.ReadAsync<(int IdCampione, int IdGiudizio, string CodGiudizio, string Giudizio, bool GiudizioConformita)>()).ToList();
            foreach (var s in stati)
                s.Giudizi = giudizi.Where(g => g.IdCampione == s.IdCampione)
                    .Select(g => new GiudizioArcLab { IdGiudizio = g.IdGiudizio, CodGiudizio = g.CodGiudizio, Giudizio = g.Giudizio, GiudizioConformita = g.GiudizioConformita })
                    .ToList();
            risultato.AddRange(stati);
        }
        return risultato;
    }

    public async Task<IReadOnlyList<GiudizioArcLab>> GiudiziMatriceAsync(int idMatrice, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<GiudizioArcLab>(new CommandDefinition("""
            SELECT DISTINCT t.IdGiudizio, t.CodGiudizio, t.Giudizio, CAST(t.Giudizio_conformita AS bit) AS GiudizioConformita
            FROM GiudizioMatrice gm
            INNER JOIN TipoGiudizi t ON t.IdGiudizio = gm.IdGiudizio
            WHERE gm.IdMatrice = @IdMatrice AND t.Sospeso = 0
            ORDER BY t.CodGiudizio
            """, new { IdMatrice = idMatrice }, commandTimeout: 30, cancellationToken: ct))).ToList();
    }

    public async Task<GiudizioArcLab?> GiudizioAsync(int idGiudizio, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<GiudizioArcLab>(new CommandDefinition("""
            SELECT t.IdGiudizio, t.CodGiudizio, t.Giudizio, CAST(t.Giudizio_conformita AS bit) AS GiudizioConformita
            FROM TipoGiudizi t WHERE t.IdGiudizio = @IdGiudizio
            """, new { IdGiudizio = idGiudizio }, commandTimeout: 30, cancellationToken: ct));
    }
}
