using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryNonFatturare" />
///
/// <remarks>
/// Campioni con NonFatturare attivo sulla commessa, nel periodo (data di
/// ricezione), con prelevatore (Campione.IdPrelevatore → Organico) e flag del
/// contratto di consulenza HACCP dell'insediamento. Sola lettura su ArcLab.
/// </remarks>
public class RepositoryNonFatturare : IRepositoryNonFatturare
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryNonFatturare(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Sql = """
        WITH Contratti AS (
            SELECT DISTINCT ins.IDInsediamento AS IdInsediamento
            FROM commessa cm2
            INNER JOIN ContattoCliente cc2 ON cm2.IDContatto = cc2.IDContatto
            INNER JOIN Insediamento ins    ON cc2.IDInsediamento = ins.IDInsediamento
            INNER JOIN Prestazioni pr      ON cm2.IdCommessa = pr.IdCommessa
            WHERE cm2.IdTipoCommessa = 5
              AND cm2.DescrIncarico LIKE 'Gestione ed assistenza autocontrol%HACCP%'
        )
        SELECT TOP (@Limite)
            ca.IdCampione,
            CAST(lo.CodLotto AS VARCHAR(20)) + '/' + CAST(ca.CodCampione AS VARCHAR(20)) AS NumeroCampione,
            cl.IDCliente        AS IdCliente,
            cl.RagioneSociale,
            ins.IDInsediamento  AS IdInsediamento,
            ins.NomeInsed       AS NomeInsediamento,
            cm.IDControllo      AS IdControllo,
            co.Controllo,
            lo.DataRic          AS DataRicezione,
            LTRIM(RTRIM(ISNULL(org.CognomeOrg, '') + ' ' + ISNULL(org.NomeOrg, ''))) AS Prelevatore,
            LTRIM(RTRIM(ISNULL(orgr.CognomeOrg, '') + ' ' + ISNULL(orgr.NomeOrg, ''))) AS Ricevitore,
            cm.PrezzoControllo,
            CASE WHEN h.IdInsediamento IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS ContrattoHaccp
        FROM commessa cm
        INNER JOIN Lotto lo            ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN Campione ca         ON lo.IdLotto = ca.IdLotto
        INNER JOIN ContattoCliente cc  ON cm.IDContatto = cc.IDContatto
        INNER JOIN Insediamento ins    ON cc.IDInsediamento = ins.IDInsediamento
        INNER JOIN Clienti cl          ON ins.IDCliente = cl.IDCliente
        LEFT  JOIN Controllo co        ON cm.IDControllo = co.IdControllo
        LEFT  JOIN mansione msp        ON msp.IdMansione = ca.IdPrelevatore
        LEFT  JOIN Organico org        ON org.IdOrganico = msp.IdOrganico
        LEFT  JOIN mansione msr        ON msr.IdMansione = lo.IdRicevitore
        LEFT  JOIN Organico orgr       ON orgr.IdOrganico = msr.IdOrganico
        LEFT  JOIN Contratti h         ON h.IdInsediamento = ins.IDInsediamento
        WHERE ISNULL(cm.NonFatturare, 0) = 1
          AND lo.DataRic BETWEEN @DataDa AND @DataA
        ORDER BY Prelevatore, cl.RagioneSociale, ins.NomeInsed
        """;

    public async Task<EsitoNonFatturare> InterrogaAsync(
        CriteriNonFatturare criteri, CancellationToken ct = default)
    {
        var parametri = new
        {
            DataDa = criteri.DataDa.ToDateTime(TimeOnly.MinValue),
            DataA  = criteri.DataA.ToDateTime(TimeOnly.MaxValue),
            criteri.Limite
        };

        using var cn = await _fabbrica.ApriAsync(ct);
        var campioni = (await cn.QueryAsync<CampioneNonFatturare>(
            new CommandDefinition(Sql, parametri, cancellationToken: ct))).ToList();

        return new EsitoNonFatturare
        {
            Campioni = campioni,
            Troncato = campioni.Count >= criteri.Limite
        };
    }
}
