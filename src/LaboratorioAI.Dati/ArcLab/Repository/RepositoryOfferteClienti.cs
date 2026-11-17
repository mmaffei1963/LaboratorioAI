using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <summary>
/// Offerte sul portale (17/11/2026): commesse di laboratorio attive da ArcLab. SOLA LETTURA.
/// Insediamento della commessa: commessa.IDInsediamento, o quello del contatto se manca.
/// </summary>
public class RepositoryOfferteClienti : IRepositoryOfferteClienti
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryOfferteClienti(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string SqlCommesse = """
        SELECT CAST(cl.IDCliente AS int)                               AS IdCliente,
               LTRIM(RTRIM(CAST(cl.RagioneSociale AS nvarchar(255))))  AS RagioneSociale,
               LTRIM(RTRIM(CAST(cl.CodContabilita AS nvarchar(50))))   AS CodContabilita,
               CAST(i.IDInsediamento AS int)                           AS IdInsediamento,
               LTRIM(RTRIM(CAST(i.NomeInsed AS nvarchar(255))))        AS Insediamento,
               CAST(cm.IdCommessa AS int)                              AS IdCommessa,
               CAST(cm.IDControllo AS int)                             AS IdControllo,
               CAST(co.Controllo AS nvarchar(255))                     AS Controllo,
               CAST(cm.DescrIncarico AS nvarchar(max))                 AS DescrIncarico,
               CAST(ROUND(cm.PrezzoControllo, 2) AS decimal(18, 2))    AS Prezzo,
               CAST(cm.DataCommessa AS datetime)                       AS DataCommessa,
               CAST(ISNULL(cm.NonFatturare, 0) AS bit)                 AS NonFatturare
        FROM commessa cm
        LEFT JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i    ON i.IDInsediamento = COALESCE(cm.IDInsediamento, cc.IDInsediamento)
        INNER JOIN Clienti cl        ON cl.IDCliente = i.IDCliente
        LEFT JOIN Controllo co       ON co.IDControllo = cm.IDControllo
        WHERE cm.IdTipoCommessa = 3
          -- 17/11/2026: attiva = senza scadenza o con scadenza futura; insediamento in uso.
          AND (cm.DataScadenzaInc IS NULL OR cm.DataScadenzaInc > CAST(GETDATE() AS date))
          AND ISNULL(cm.Chiuso, 0) = 0
          AND ISNULL(i.NonInUso, 0) = 0
          AND cm.IDControllo IS NOT NULL
          AND cm.PrezzoControllo > 0
          AND (@IdCliente IS NULL OR cl.IDCliente = @IdCliente)
          -- Solo clienti attivi con codice contabilità (offerte e anomalie); nell'elenco di tutti esclusa MARE.A (MIRKO1).
          AND ISNULL(cl.Attivo, 0) = 1
          AND NULLIF(LTRIM(RTRIM(CAST(cl.CodContabilita AS nvarchar(50)))), '') IS NOT NULL
          AND (@IdCliente IS NOT NULL OR CAST(cl.CodContabilita AS nvarchar(50)) <> 'MIRKO1')
        ORDER BY cl.IDCliente, cm.IDControllo, i.NomeInsed;
        """;

    private const string SqlCliente = """
        SELECT CAST(cl.IDCliente AS int)                               AS IdCliente,
               LTRIM(RTRIM(CAST(cl.RagioneSociale AS nvarchar(255))))  AS RagioneSociale,
               LTRIM(RTRIM(CAST(cl.CodContabilita AS nvarchar(50))))   AS CodContabilita,
               LTRIM(RTRIM(CAST(cl.PIVA AS nvarchar(50))))             AS PartitaIva,
               LTRIM(RTRIM(CAST(cl.IndirizzoFatt AS nvarchar(255))))   AS Indirizzo,
               LTRIM(RTRIM(CAST(cl.CapFatt AS nvarchar(20))))          AS Cap,
               LTRIM(RTRIM(CAST(cl.[CittàFatt] AS nvarchar(100))))     AS Citta,
               LTRIM(RTRIM(CAST(cl.ProvinciaFatt AS nvarchar(10))))    AS Provincia
        FROM Clienti cl
        WHERE cl.IDCliente = @IdCliente;
        """;

    public async Task<IReadOnlyList<CommessaOffertaArcLab>> CommesseAsync(int? idCliente, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<CommessaOffertaArcLab>(new CommandDefinition(SqlCommesse, new { IdCliente = idCliente },
            commandTimeout: 120, cancellationToken: ct))).ToList();
    }

    public async Task<ClienteOffertaArcLab?> ClienteAsync(int idCliente, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<ClienteOffertaArcLab>(new CommandDefinition(SqlCliente, new { IdCliente = idCliente },
            cancellationToken: ct));
    }
}
