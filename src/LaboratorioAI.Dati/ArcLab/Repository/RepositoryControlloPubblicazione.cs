using Dapper;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <summary>
/// Controllo pubblicazione certificati (16/11/2026). SOLA LETTURA su LabAnalisiDati e Marea (stesso server):
/// le tabelle temporanee (#) stanno in tempdb e spariscono con la connessione. I testi dei due database hanno regole
/// di confronto diverse (SQL_Latin1_General_CP1_CI_AS e Latin1_General_CI_AS): si allineano con COLLATE.
/// </summary>
public class RepositoryControlloPubblicazione
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryControlloPubblicazione(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Sql = """
        SET NOCOUNT ON;

        -- certificati ArcLab con data spedizione (più un margine per trovare la controparte vicino ai bordi)
        SELECT ce.IdCertificato,
               LTRIM(RTRIM(ce.NCertificato)) COLLATE Latin1_General_CI_AS AS NCert,
               CAST(ce.DataSped AS date) AS DataSped, ce.IdTipoCertificato
        INTO #c
        FROM dbo.certificato ce
        WHERE ce.DataSped >= DATEADD(day, -@Margine, @Dal) AND ce.DataSped < DATEADD(day, @Margine + 1, @Al)
          AND LEN(LTRIM(RTRIM(ce.NCertificato))) >= 8;

        -- certificati caricati sul portale, senza gli Allegati A.
        -- 16/11/2026: Chiave = il numero certificato ricavato dal nome regolare «gg-mm-aa-LOTTO-CC-xxAaa-...»
        -- (dal 10° carattere fino al terzo trattino o al punto): permette un abbinamento veloce per uguaglianza.
        SELECT en.Id AS IdEntry,
               en.Tipo COLLATE Latin1_General_CI_AS AS Tipo,
               ISNULL(en.Titolo, '') COLLATE Latin1_General_CI_AS AS Titolo,
               ISNULL(en.NomeFile, '') COLLATE Latin1_General_CI_AS AS NomeFile,
               en.DataUpload, en.DataFirma,
               CAST(CASE WHEN en.NomeFile LIKE '[0-9][0-9]-[0-9][0-9]-[0-9][0-9]-%' AND k.d2 > 0
                         THEN LEFT(k.resto, CASE WHEN k.d3 > 0 THEN k.d3 - 1
                                                 WHEN CHARINDEX('.', k.resto, k.d2 + 1) > 0 THEN CHARINDEX('.', k.resto, k.d2 + 1) - 1
                                                 ELSE LEN(k.resto) END)
                    END AS nvarchar(60)) COLLATE Latin1_General_CI_AS AS Chiave
        INTO #e
        FROM [Marea].dbo.Entries en
        CROSS APPLY (SELECT SUBSTRING(ISNULL(en.NomeFile, ''), 10, 200) AS resto) r
        CROSS APPLY (SELECT r.resto,
                            CHARINDEX('-', r.resto, CHARINDEX('-', r.resto) + 1) AS d2,
                            CHARINDEX('-', r.resto, CHARINDEX('-', r.resto, CHARINDEX('-', r.resto) + 1) + 1) AS d3) k
        WHERE en.Tipo LIKE '%Certificato%'
          AND en.Tipo NOT LIKE '%Allegato%'
          AND en.DataUpload >= DATEADD(day, -@Margine, @Dal) AND en.DataUpload < DATEADD(day, @Margine + 1, @Al);

        -- abbinamenti: prima per uguaglianza sulla chiave (veloce), poi, solo per i file rimasti senza abbinamento,
        -- cercando il numero certificato dentro il nome file o il titolo
        SELECT c.IdCertificato, c.NCert, c.DataSped, c.IdTipoCertificato,
               e.IdEntry, e.Tipo, e.Titolo, e.NomeFile, e.DataUpload, e.DataFirma
        INTO #m
        FROM #c c
        JOIN #e e ON e.Chiave = c.NCert;

        INSERT INTO #m
        SELECT c.IdCertificato, c.NCert, c.DataSped, c.IdTipoCertificato,
               e.IdEntry, e.Tipo, e.Titolo, e.NomeFile, e.DataUpload, e.DataFirma
        FROM #e e
        JOIN #c c ON CHARINDEX(c.NCert, e.NomeFile) > 0 OR CHARINDEX(c.NCert, e.Titolo) > 0
        WHERE NOT EXISTS (SELECT 1 FROM #m m WHERE m.IdEntry = e.IdEntry);

        -- 1) date non coincidenti
        SELECT NCert, CAST(DataSped AS datetime) AS DataSped, DataUpload, DataFirma,
               DATEDIFF(day, DataSped, DataUpload) AS GiorniUpload, DATEDIFF(day, DataSped, DataFirma) AS GiorniFirma,
               Tipo, NomeFile
        FROM #m
        WHERE (DataSped BETWEEN @Dal AND @Al OR CAST(DataUpload AS date) BETWEEN @Dal AND @Al)
          AND (CAST(DataUpload AS date) <> DataSped OR DataFirma IS NULL OR CAST(DataFirma AS date) <> DataSped)
        ORDER BY DataSped, NCert;

        -- 2) spediti in ArcLab ma non sul portale
        SELECT c.NCert, CAST(c.DataSped AS datetime) AS DataSped, CAST(c.IdTipoCertificato AS int) AS IdTipoCertificato
        FROM #c c
        WHERE c.DataSped BETWEEN @Dal AND @Al
          AND NOT EXISTS (SELECT 1 FROM #m m WHERE m.IdCertificato = c.IdCertificato)
        ORDER BY c.DataSped, c.NCert;

        -- 3) sul portale ma non spediti in ArcLab (facoltativo: senza i certificati esterni con il nome regolare)
        -- 16/11/2026: prima si selezionano i soli file non abbinati, poi si cerca il certificato ArcLab solo tra quelli
        -- dell'anno del periodo e del precedente (il numero finisce con «A» + anno a due cifre): prima si scorreva
        -- l'intero archivio per ogni file e in produzione la pagina superava il tempo massimo (errore 524).
        SELECT e.IdEntry, e.Tipo, e.NomeFile, e.Titolo, e.DataUpload, e.DataFirma
        INTO #u
        FROM #e e
        WHERE CAST(e.DataUpload AS date) BETWEEN @Dal AND @Al
          AND NOT EXISTS (SELECT 1 FROM #m m WHERE m.IdEntry = e.IdEntry)
          AND (@EscludiEsterniRegolari = 0
               OR NOT (e.Tipo LIKE '%Esterno%' AND e.NomeFile LIKE '[0-9][0-9]-[0-9][0-9]-[0-9][0-9]-%'));

        SELECT IdCertificato, LTRIM(RTRIM(NCertificato)) COLLATE Latin1_General_CI_AS AS NCert, DataSped
        INTO #a
        FROM dbo.certificato
        WHERE EXISTS (SELECT 1 FROM #u)
          AND LEN(LTRIM(RTRIM(NCertificato))) >= 8
          AND (RTRIM(NCertificato) LIKE '%A' + RIGHT(CAST(YEAR(@Al) AS varchar(4)), 2)
               OR RTRIM(NCertificato) LIKE '%A' + RIGHT(CAST(YEAR(@Al) - 1 AS varchar(4)), 2));

        SELECT u.Tipo, u.NomeFile, u.Titolo, u.DataUpload, u.DataFirma,
               x.NCert AS CertificatoArcLab, x.DataSped AS DataSpedArcLab
        FROM #u u
        OUTER APPLY (SELECT TOP 1 a.NCert, a.DataSped FROM #a a
                     WHERE CHARINDEX(a.NCert, u.NomeFile) > 0 OR CHARINDEX(a.NCert, u.Titolo) > 0) x
        ORDER BY u.DataUpload;

        -- 4) caricati più volte (la versione inglese non conta come doppione)
        SELECT NCert, CAST(DataSped AS datetime) AS DataSped, COUNT(*) AS Caricamenti,
               SUM(CASE WHEN NomeFile LIKE '%(ENG)%' THEN 1 ELSE 0 END) AS DiCuiInglese,
               MIN(DataUpload) AS PrimoUpload, MAX(DataUpload) AS UltimoUpload
        FROM #m
        WHERE DataSped BETWEEN @Dal AND @Al
        GROUP BY NCert, DataSped
        HAVING COUNT(*) - SUM(CASE WHEN NomeFile LIKE '%(ENG)%' THEN 1 ELSE 0 END) > 1
        ORDER BY DataSped;

        DROP TABLE #a; DROP TABLE #u; DROP TABLE #m; DROP TABLE #e; DROP TABLE #c;
        """;

    public async Task<EsitoControlloPubblicazione> ConfrontaAsync(DateOnly dal, DateOnly al, bool escludiEsterniRegolari,
        int margineGiorni = 15, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(Sql, new
        {
            Dal = dal.ToDateTime(TimeOnly.MinValue),
            Al = al.ToDateTime(TimeOnly.MinValue),
            Margine = margineGiorni,
            EscludiEsterniRegolari = escludiEsterniRegolari ? 1 : 0
        }, commandTimeout: 300, cancellationToken: ct));
        var dateDiverse = (await multi.ReadAsync<DateDiversePubblicazione>()).ToList();
        var soloArcLab = (await multi.ReadAsync<SoloArcLabPubblicazione>()).ToList();
        var soloPortale = (await multi.ReadAsync<SoloPortalePubblicazione>()).ToList();
        var doppioni = (await multi.ReadAsync<DoppioniPubblicazione>()).ToList();
        return new EsitoControlloPubblicazione(dateDiverse, soloArcLab, soloPortale, doppioni);
    }
}
