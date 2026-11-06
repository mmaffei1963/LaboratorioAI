using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryMetodiLabEsterno" />
public class RepositoryMetodiLabEsterno : IRepositoryMetodiLabEsterno
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryMetodiLabEsterno(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<MetodoLabEsterno>> MetodiAffidatiAsync(
        int idLabEsterno, CancellationToken ct = default)
    {
        // Stessa struttura già validata in RepositoryLaboratoriEsterni (m.RifBiblio come
        // riferimento normativo, labEstMetodo.AccrLabEsterno come flag di accreditamento
        // inserito a mano in ArcLab).
        const string sql = """
            SELECT
                lem.IdLabEstMetodo,
                lem.IdMetodo,
                m.CodMetodo,
                m.RifBiblio      AS RiferimentoNormativo,
                -- Nome del parametro come nel Rapporto di Prova: Metodo.DettagliMetodo (fallback Parametro.NomeParametro).
                COALESCE(NULLIF(LTRIM(RTRIM(CONVERT(nvarchar(300), m.DettagliMetodo))), ''), p.NomeParametro) AS NomeParametro,
                lem.AccrLabEsterno AS AccreditatoArcLab
            FROM labEstMetodo lem
            INNER JOIN Metodo m    ON m.IdMetodo = lem.IdMetodo
            LEFT  JOIN Parametro p ON p.IdParametro = m.IdParametro
            WHERE lem.IdLabEsterno = @IdLabEsterno
            ORDER BY NomeParametro, m.RifBiblio
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<MetodoLabEsterno>(
            new CommandDefinition(sql, new { IdLabEsterno = idLabEsterno }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<AnagraficaLabEsterno>> LaboratoriAsync(
        IReadOnlyCollection<int> idLabEsterni, CancellationToken ct = default)
    {
        if (idLabEsterni.Count == 0) return Array.Empty<AnagraficaLabEsterno>();
        const string sql = """
            SELECT le.IdLabEsterno,
                   CONVERT(nvarchar(300), le.LabEsterno)      AS Nome,
                   CAST(le.NumeroAccredia AS NVARCHAR(50))    AS NumeroAccredia
            FROM LabEsterno le
            WHERE le.IdLabEsterno IN @Id
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<AnagraficaLabEsterno>(
            new CommandDefinition(sql, new { Id = idLabEsterni }, cancellationToken: ct))).ToList();
    }
}
