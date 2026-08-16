using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryTamponiContratto" />
///
/// <remarks>
/// Traduzione in T-SQL della logica di prefatturazione dei tamponi, in SOLA
/// LETTURA: individua i tamponi (IdControllo 180/203) fatturabili nel periodo
/// (stessi filtri della prefatturazione: scadenza incasso, non-fatturare,
/// certificato non fatturato, vettore presente, campione non già fatturato) e
/// aggancia, per Cliente + Insediamento, l'eventuale contratto di consulenza
/// HACCP (commessa tipo 5) con il numero di tamponi inclusi (DurataPres delle
/// prestazioni dell'anno in corso). Nessun INSERT su ArcLab.
/// </remarks>
public class RepositoryTamponiContratto : IRepositoryTamponiContratto
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryTamponiContratto(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // {0} = condizione sullo stato di fatturazione (data fattura del certificato).
    // Il frammento è generato internamente da un enum, non da input utente.
    private const string SqlModello = """
        WITH Tamponi AS (
            SELECT
                ca.IdCampione,
                CAST(lo.CodLotto AS VARCHAR(20)) + '/' + CAST(ca.CodCampione AS VARCHAR(20)) AS NumeroCampione,
                cl.CODContabilita   AS CodContabilita,
                cl.IDCliente        AS IdCliente,
                cl.RagioneSociale,
                ins.IDInsediamento  AS IdInsediamento,
                ins.NomeInsed       AS NomeInsediamento,
                cm.IDControllo      AS IdControllo,
                co.Controllo,
                cert.NCertificato   AS NumeroCertificato,
                cert.DataCertificato,
                LTRIM(RTRIM(ISNULL(org.CognomeOrg, '') + ' ' + ISNULL(org.NomeOrg, ''))) AS Prelevatore,
                LTRIM(RTRIM(ISNULL(orgr.CognomeOrg, '') + ' ' + ISNULL(orgr.NomeOrg, ''))) AS Ricevitore,
                CAST(ISNULL(cm.NonFatturare, 0) AS bit) AS NonFatturare
            FROM commessa cm
            INNER JOIN Lotto lo            ON cm.IdCommessa = lo.IdCommessa
            INNER JOIN Campione ca         ON lo.IdLotto = ca.IdLotto
            INNER JOIN Certificato cert    ON ca.IdCertificato = cert.IdCertificato
            INNER JOIN ContattoCliente cc  ON cm.IDContatto = cc.IDContatto
            INNER JOIN Insediamento ins    ON cc.IDInsediamento = ins.IDInsediamento
            INNER JOIN Clienti cl          ON ins.IDCliente = cl.IDCliente
            INNER JOIN Controllo co        ON cm.IDControllo = co.IdControllo
            LEFT  JOIN mansione msp        ON msp.IdMansione = ca.IdPrelevatore
            LEFT  JOIN Organico org        ON org.IdOrganico = msp.IdOrganico
            LEFT  JOIN mansione msr        ON msr.IdMansione = lo.IdRicevitore
            LEFT  JOIN Organico orgr       ON orgr.IdOrganico = msr.IdOrganico
            -- Stessi criteri della prefatturazione ArcLab, così l'elenco
            -- coincide con l'estrazione del gestionale.
            WHERE cm.IDControllo IN @Controlli
              AND (cm.DataScadenzaInc >= DATEADD(day, -60, CAST(GETDATE() AS date))
                   OR cm.DataScadenzaInc IS NULL)
              AND ISNULL(cm.NonFatturare, 0) = 0
              {0}
              AND cert.DataCertificato BETWEEN @DataDa AND @DataA
              AND lo.idvettore IS NOT NULL
              AND ca.Fatturare IS NULL
        ),
        Contratti AS (
            -- Il contratto HACCP è "attivo" se sulla commessa di consulenza
            -- (tipo 5, "Gestione ed assistenza autocontrol[l]o HACCP") esistono
            -- prestazioni: la sola voce a listino non basta. Il LIKE con una sola
            -- "l" copre sia "autocontrolo" (refuso in ArcLab) sia "autocontrollo".
            -- N. Tamponi = ULTIMO valore compilato di DurataPres (il più recente
            -- non nullo); DataPrestazione = data della prestazione più recente.
            SELECT
                x.IdInsediamento,
                MAX(x.DataPrestazione)                          AS DataPrestazione,
                MAX(CASE WHEN x.rnValore = 1 THEN x.Durata END) AS TamponiInclusi
            FROM (
                SELECT
                    ins.IDInsediamento AS IdInsediamento,
                    pr.DataInizioPres  AS DataPrestazione,
                    TRY_CONVERT(int, pr.DurataPres) AS Durata,
                    ROW_NUMBER() OVER (
                        PARTITION BY ins.IDInsediamento
                        ORDER BY CASE WHEN pr.DurataPres IS NULL THEN 1 ELSE 0 END,
                                 pr.DataInizioPres DESC) AS rnValore
                FROM commessa cm2
                INNER JOIN ContattoCliente cc2 ON cm2.IDContatto = cc2.IDContatto
                INNER JOIN Insediamento ins    ON cc2.IDInsediamento = ins.IDInsediamento
                INNER JOIN Prestazioni pr      ON cm2.IdCommessa = pr.IdCommessa
                WHERE cm2.IdTipoCommessa = 5
                  AND cm2.DescrIncarico LIKE 'Gestione ed assistenza autocontrol%HACCP%'
            ) x
            GROUP BY x.IdInsediamento
        )
        SELECT TOP (@Limite)
            t.IdCampione, t.NumeroCampione, t.CodContabilita, t.IdCliente, t.RagioneSociale,
            t.IdInsediamento, t.NomeInsediamento, t.IdControllo, t.Controllo,
            t.NumeroCertificato, t.DataCertificato, t.Prelevatore, t.Ricevitore, t.NonFatturare,
            CASE WHEN c.IdInsediamento IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS ContrattoHaccp,
            c.TamponiInclusi,
            c.DataPrestazione
        FROM Tamponi t
        LEFT JOIN Contratti c ON t.IdInsediamento = c.IdInsediamento
        ORDER BY t.RagioneSociale, t.NomeInsediamento, t.DataCertificato
        """;

    public async Task<EsitoTamponiContratto> InterrogaAsync(
        CriteriTamponiContratto criteri, CancellationToken ct = default)
    {
        var controlli = criteri.Controlli is { Count: > 0 }
            ? criteri.Controlli
            : new[] { 180, 203, 2513 };

        // Condizione sullo stato di fatturazione (agisce solo sulla data fattura).
        var condizioneFattura = criteri.Fatturazione switch
        {
            FiltroFatturazione.Fatturati => "AND cert.DataFattura IS NOT NULL",
            FiltroFatturazione.Tutti     => "",
            _                            => "AND cert.DataFattura IS NULL"
        };

        var sql = string.Format(SqlModello, condizioneFattura);

        var parametri = new
        {
            DataDa = criteri.DataDa.ToDateTime(TimeOnly.MinValue),
            DataA  = criteri.DataA.ToDateTime(TimeOnly.MaxValue),
            Controlli = controlli,
            criteri.Limite
        };

        using var cn = await _fabbrica.ApriAsync(ct);

        var tamponi = (await cn.QueryAsync<TamponeContratto>(
            new CommandDefinition(sql, parametri, cancellationToken: ct))).ToList();

        // Il riepilogo per insediamento si ricava dal dettaglio, senza una
        // seconda interrogazione: conteggio dei tamponi fatti e confronto con
        // quelli inclusi nel contratto.
        return new EsitoTamponiContratto
        {
            Tamponi = tamponi,
            Riepilogo = RiepilogoInsediamentoTamponi.Da(tamponi),
            Troncato = tamponi.Count >= criteri.Limite
        };
    }
}
