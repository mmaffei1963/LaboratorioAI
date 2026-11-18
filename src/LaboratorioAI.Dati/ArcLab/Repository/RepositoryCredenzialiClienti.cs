using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <summary>Password del portale (17/11/2026): dati del cliente da ArcLab. SOLA LETTURA.</summary>
public class RepositoryCredenzialiClienti : IRepositoryCredenzialiClienti
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryCredenzialiClienti(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Sql = """
        SELECT CAST(cl.IDCliente AS int)                               AS IdCliente,
               LTRIM(RTRIM(CAST(cl.CodContabilita AS nvarchar(50))))   AS CodiceContabilita,
               LTRIM(RTRIM(CAST(cl.RagioneSociale AS nvarchar(255))))  AS RagioneSociale,
               LTRIM(RTRIM(CAST(cl.PIVA AS nvarchar(50))))             AS PartitaIva,
               CAST(cl.email AS nvarchar(400))                         AS Email
        FROM Clienti cl
        WHERE cl.IDCliente = @IdCliente;
        """;

    public async Task<CredenzialiClienteArcLab?> DatiAsync(int idCliente, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<CredenzialiClienteArcLab>(new CommandDefinition(Sql, new { IdCliente = idCliente }, cancellationToken: ct));
    }
}
