using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryDvr" />
public class RepositoryDvr : IRepositoryDvr
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryDvr(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    /// <remarks>
    /// Le strutture soggette a valutazione sono riconosciute dalla presenza
    /// di controlli il cui nome richiama la legionella: è un criterio
    /// empirico ma è l'unico ricavabile dai dati di ArcLab.
    /// </remarks>
    private const string SqlStrutture = """
        SELECT DISTINCT
            i.IDCliente,
            i.IDInsediamento,
            cl.RagioneSociale,
            i.NomeInsed        AS NomeInsediamento,
            i.IndirizzoInsed   AS Indirizzo,
            i.CittaInsed       AS Comune,
            i.ProvinciaInsed   AS Provincia,
            i.CapInsed         AS Cap,
            cl.PIVA            AS PartitaIva,
            i.Email,
            i.Telefono,
            (SELECT MAX(lo.DataRic)
               FROM Lotto lo
               INNER JOIN commessa cm2        ON cm2.IdCommessa = lo.IdCommessa
               INNER JOIN ContattoCliente cc2 ON cc2.IDContatto = cm2.IDContatto
              WHERE cc2.IDInsediamento = i.IDInsediamento) AS UltimoCampionamento
        FROM Insediamento i
        INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
        INNER JOIN ContattoCliente cc ON cc.IDInsediamento = i.IDInsediamento
        INNER JOIN commessa cm        ON cm.IDContatto = cc.IDContatto
        INNER JOIN Controllo co       ON co.IDControllo = cm.IDControllo
        WHERE co.Controllo LIKE '%legionell%'
          AND i.NomeInsed IS NOT NULL
          AND (@Filtro IS NULL
               OR cl.RagioneSociale LIKE @Ricerca
               OR i.NomeInsed LIKE @Ricerca
               OR i.CittaInsed LIKE @Ricerca)
        ORDER BY cl.RagioneSociale, i.NomeInsed
        """;

    public async Task<IReadOnlyList<StrutturaDvr>> StruttureAsync(
        string? filtro = null, CancellationToken ct = default)
    {
        var pulito = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim();

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<StrutturaDvr>(
            new CommandDefinition(SqlStrutture,
                new { Filtro = pulito, Ricerca = $"%{pulito}%" },
                cancellationToken: ct))).ToList();
    }

    public async Task<StrutturaDvr?> StrutturaAsync(
        int idInsediamento, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1
                i.IDCliente,
                i.IDInsediamento,
                cl.RagioneSociale,
                i.NomeInsed      AS NomeInsediamento,
                i.IndirizzoInsed AS Indirizzo,
                i.CittaInsed     AS Comune,
                i.ProvinciaInsed AS Provincia,
                i.CapInsed       AS Cap,
                cl.PIVA          AS PartitaIva,
                i.Email,
                i.Telefono
            FROM Insediamento i
            INNER JOIN Clienti cl ON cl.IDCliente = i.IDCliente
            WHERE i.IDInsediamento = @Id
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<StrutturaDvr>(
            new CommandDefinition(sql, new { Id = idInsediamento }, cancellationToken: ct));
    }

    /// <remarks>
    /// Si guardano gli ultimi due anni: i punti più vecchi appartengono
    /// spesso a configurazioni impiantistiche superate.
    /// </remarks>
    private const string SqlPuntiStorici = """
        SELECT DISTINCT
            ca.LuogoPrelievo AS Descrizione,
            ca.PuntoPrelievo AS Ubicazione,
            mx.Matrice       AS Tipologia
        FROM Campione ca
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN Controllo co       ON co.IDControllo = cm.IDControllo
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        WHERE cc.IDInsediamento = @Id
          AND co.Controllo LIKE '%legionell%'
          AND lo.DataRic >= DATEADD(year, -2, GETDATE())
          AND ca.LuogoPrelievo IS NOT NULL
        ORDER BY ca.LuogoPrelievo
        """;

    public async Task<IReadOnlyList<PuntoPrelievoDvr>> PuntiStoriciAsync(
        int idInsediamento, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<PuntoPrelievoDvr>(
            new CommandDefinition(SqlPuntiStorici,
                new { Id = idInsediamento }, cancellationToken: ct))).ToList();
    }
}
