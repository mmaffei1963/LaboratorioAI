using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <summary>
/// Documenti del portale (19/11/2026): tabella Entries del database Marea (portale Palomar), letta con la connessione
/// ArcLab sullo stesso server. SOLA LETTURA. Regola fissa: i certificati senza DataFirma (non firmati digitalmente) e i
/// documenti bloccati non escono mai.
/// </summary>
public class RepositoryDocumentiPortale : IRepositoryDocumentiPortale
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryDocumentiPortale(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Colonne = """
        SELECT e.Id, e.DataUpload, CAST(e.Tipo AS nvarchar(200)) AS Tipo, CAST(e.Titolo AS nvarchar(400)) AS Titolo,
               CAST(e.Percorso AS nvarchar(400)) AS Percorso, CAST(e.NomeFile AS nvarchar(400)) AS NomeFile,
               e.ValidoFinoA, e.DataUltimoDownload, e.IDInsediamento AS IdInsediamento,
               CAST(i.Insediamento AS nvarchar(255)) AS Insediamento, e.IsPositivo,
               CAST(e.NomeCampione AS nvarchar(500)) AS NomeCampione, e.DataPrelievo,
               CAST(e.VerbalePrelievo AS nvarchar(100)) AS VerbalePrelievo, CAST(e.LuogoPrelievo AS nvarchar(255)) AS LuogoPrelievo,
               CAST(e.PuntoPrelievo AS nvarchar(255)) AS PuntoPrelievo, CAST(ISNULL(e.ByCliente, 0) AS bit) AS ByCliente,
               CAST(e.Filtri AS nvarchar(400)) AS Filtri, e.DataFirma, c.IDArcLab AS IdClienteArcLab
        FROM [Marea].dbo.Entries e
        INNER JOIN [Marea].dbo.Clienti c     ON c.Id = e.IDCliente
        LEFT JOIN [Marea].dbo.Insediamenti i ON i.Id = e.IDInsediamento
        WHERE ISNULL(e.IsBloccato, 0) = 0
          AND NOT (e.Tipo LIKE 'Certificato%' AND e.DataFirma IS NULL)
        """;

    public async Task<IReadOnlyList<DocumentoMarea>> DocumentiAsync(int idClienteArcLab, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var sql = Colonne + "\n  AND c.IDArcLab = @IdCliente\nORDER BY e.DataUpload DESC\nOPTION (RECOMPILE);";
        return (await cn.QueryAsync<DocumentoMarea>(new CommandDefinition(sql, new { IdCliente = idClienteArcLab },
            commandTimeout: 60, cancellationToken: ct))).ToList();
    }

    public async Task<DocumentoMarea?> DocumentoAsync(int idEntry, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<DocumentoMarea>(new CommandDefinition(Colonne + "\n  AND e.Id = @Id;",
            new { Id = idEntry }, cancellationToken: ct));
    }
}
