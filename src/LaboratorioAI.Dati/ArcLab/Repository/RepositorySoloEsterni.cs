using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositorySoloEsterni" />
/// <remarks>
/// SOLA LETTURA su LabAnalisiDati. Confronta i certificati esterni caricati nel portale
/// (dbo.Entries, Tipo "Certificato Esterno") con i certificati interni caricati e con la
/// tabella Certificato di ArcLab, agganciando per anno + lotto/campione + cliente.
/// Il nome file ha struttura GG-MM-AA-lotto-campione-…; NCertificato ha struttura lotto-campione-codice.
/// </remarks>
public class RepositorySoloEsterni : IRepositorySoloEsterni
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositorySoloEsterni(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Sql = """
        WITH Est AS (
            SELECT en.Id AS IdEntry, en.IDInsediamento, en.IDCliente AS IdClienteEntry, en.DataUpload, en.NomeFile,
                   2000 + CAST(SUBSTRING(en.NomeFile, 7, 2) AS int) AS Anno,
                   SUBSTRING(en.NomeFile, 10, 4000) AS Resto
            FROM [Marea].dbo.Entries en
            WHERE en.Tipo LIKE '%Certificato Esterno%'
              AND en.NomeFile LIKE '[0-9][0-9]-[0-9][0-9]-[0-9][0-9]-%'
              AND (@DataDa IS NULL OR en.DataUpload >= @DataDa)
              AND (@DataA  IS NULL OR en.DataUpload < DATEADD(day, 1, @DataA))
        ),
        EstP AS (
            SELECT IdEntry, IDInsediamento, IdClienteEntry, DataUpload, NomeFile, Anno,
                   LEFT(Resto, CHARINDEX('-', Resto) - 1) AS Lotto,
                   LEFT(STUFF(Resto, 1, CHARINDEX('-', Resto), ''),
                        CHARINDEX('-', STUFF(Resto, 1, CHARINDEX('-', Resto), '') + '-') - 1) AS Campione
            FROM Est WHERE CHARINDEX('-', Resto) > 0
        ),
        Intn AS (
            SELECT DISTINCT
                2000 + CAST(SUBSTRING(NomeFile, 7, 2) AS int) AS Anno,
                LEFT(SUBSTRING(NomeFile,10,4000), CHARINDEX('-', SUBSTRING(NomeFile,10,4000)) - 1) AS Lotto,
                LEFT(STUFF(SUBSTRING(NomeFile,10,4000), 1, CHARINDEX('-', SUBSTRING(NomeFile,10,4000)), ''),
                     CHARINDEX('-', STUFF(SUBSTRING(NomeFile,10,4000), 1, CHARINDEX('-', SUBSTRING(NomeFile,10,4000)), '') + '-') - 1) AS Campione
            FROM [Marea].dbo.Entries
            WHERE Tipo LIKE '%Certificato%' AND Tipo NOT LIKE '%Esterno%' AND Tipo NOT LIKE '%Legionella%'
              AND NomeFile LIKE '[0-9][0-9]-[0-9][0-9]-[0-9][0-9]-%'
              AND CHARINDEX('-', SUBSTRING(NomeFile,10,4000)) > 0
        ),
        Camp AS (
            -- Il corrispondente si cerca sul CAMPIONE (esiste sempre), non sul certificato.
            SELECT ca.IdCampione,
                   YEAR(lo.DataRic) AS Anno,
                   TRY_CAST(lo.CodLotto AS int) AS LottoN,
                   ca.CodCampione AS CampioneN,
                   cm.IdCommessa, cm.IDControllo, co.Controllo, cm.PrezzoControllo,
                   LTRIM(RTRIM(CONVERT(nvarchar(50), cl.CODContabilita))) AS CodContab,
                   ce.NCertificato, ce.IdCertificato, ce.DataFattura
            FROM dbo.Campione ca
                INNER JOIN dbo.Lotto lo    ON lo.IdLotto = ca.IdLotto
                INNER JOIN dbo.commessa cm ON cm.IdCommessa = lo.IdCommessa
                LEFT  JOIN dbo.Controllo co ON co.IDControllo = cm.IDControllo
                INNER JOIN dbo.Clienti cl  ON LTRIM(RTRIM(CONVERT(nvarchar(50), cl.CODContabilita))) = LTRIM(RTRIM(CONVERT(nvarchar(50), cm.Cod_Cliente)))
                LEFT  JOIN dbo.Certificato ce ON ce.IdCertificato = ca.IdCertificato AND ISNULL(ca.IdCertificato, 0) <> 0
        )
        SELECT TOP (@Limite)
            e.IdEntry, e.Anno, e.Lotto, e.Campione,
            mc.IDArcLab AS IdClienteArcLab,
            mc.Nome     AS RagioneSociale,
            e.DataUpload, e.NomeFile,
            CASE WHEN it.Anno IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS HaInterno,
            c.IdCampione, c.NCertificato, c.IdCertificato, c.DataFattura,
            c.IdCommessa, c.IDControllo AS IdControllo, c.Controllo, c.PrezzoControllo
        FROM EstP e
            LEFT JOIN [Marea].dbo.Clienti mc ON mc.Id = e.IdClienteEntry
            LEFT JOIN Intn it ON it.Anno = e.Anno AND it.Lotto = e.Lotto AND it.Campione = e.Campione
            OUTER APPLY (
                SELECT TOP 1 cc.IdCampione, cc.NCertificato, cc.IdCertificato, cc.DataFattura,
                       cc.IdCommessa, cc.IDControllo, cc.Controllo, cc.PrezzoControllo
                FROM Camp cc
                WHERE cc.Anno = e.Anno
                  AND cc.LottoN    = TRY_CAST(e.Lotto AS int)
                  AND cc.CampioneN = TRY_CAST(e.Campione AS int)
                  AND (mc.CodContabilita IS NULL
                       OR cc.CodContab = LTRIM(RTRIM(CONVERT(nvarchar(50), mc.CodContabilita))) COLLATE DATABASE_DEFAULT)
            ) c
        WHERE (@RagFiltro IS NULL OR mc.Nome LIKE @RagFiltro COLLATE DATABASE_DEFAULT)
          AND (@SoloSenzaInterno = 0 OR it.Anno IS NULL)
          AND (@StatoFattura IS NULL
               OR (@StatoFattura = 'fatturati'    AND c.DataFattura IS NOT NULL)
               OR (@StatoFattura = 'nonfatturati' AND c.DataFattura IS NULL))
          AND (@StatoCampione IS NULL
               OR (@StatoCampione = 'con'   AND c.IdCampione IS NOT NULL)
               OR (@StatoCampione = 'senza' AND c.IdCampione IS NULL))
        ORDER BY e.Anno DESC, e.Lotto, e.Campione
        """;

    public async Task<IReadOnlyList<RigaSoloEsterni>> CercaAsync(CriteriSoloEsterni criteri, CancellationToken ct = default)
    {
        // Mese intero: se anno+mese sono impostati, hanno la precedenza sull'intervallo.
        DateTime? da = criteri.DataDa is DateOnly d1 ? d1.ToDateTime(TimeOnly.MinValue) : null;
        DateTime? a  = criteri.DataA  is DateOnly d2 ? d2.ToDateTime(TimeOnly.MinValue) : null;
        if (criteri.Anno is int y)
        {
            if (criteri.Mese is int m && m is >= 1 and <= 12)
            {
                da = new DateTime(y, m, 1);
                a = da.Value.AddMonths(1).AddDays(-1);
            }
            else   // solo anno = intero anno
            {
                da = new DateTime(y, 1, 1);
                a = new DateTime(y, 12, 31);
            }
        }

        var rag = string.IsNullOrWhiteSpace(criteri.RagioneSociale) ? null : $"%{criteri.RagioneSociale.Trim()}%";
        var stato = criteri.StatoFattura is "fatturati" or "nonfatturati" ? criteri.StatoFattura : null;
        var statoCamp = criteri.StatoCampione is "con" or "senza" ? criteri.StatoCampione : null;

        var p = new DynamicParameters();
        p.Add("DataDa", da);
        p.Add("DataA", a);
        p.Add("RagFiltro", rag);
        p.Add("SoloSenzaInterno", criteri.SoloSenzaInterno ? 1 : 0);
        p.Add("StatoFattura", stato);
        p.Add("StatoCampione", statoCamp);
        p.Add("Limite", criteri.Limite <= 0 ? 1000 : criteri.Limite);

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaSoloEsterni>(new CommandDefinition(Sql, p, cancellationToken: ct))).ToList();
    }
}
