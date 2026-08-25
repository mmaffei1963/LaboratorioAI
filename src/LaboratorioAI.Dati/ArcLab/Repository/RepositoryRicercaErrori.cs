using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryRicercaErrori" />
/// <remarks>SOLA LETTURA su LabAnalisiDati. Solo verifiche diagnostiche, nessuna modifica.</remarks>
public class RepositoryRicercaErrori : IRepositoryRicercaErrori
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    /// <summary>Timeout comando esteso: sono verifiche diagnostiche occasionali su molti anni.</summary>
    private const int TimeoutDiagnostica = 300;   // secondi

    public RepositoryRicercaErrori(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // ── 1. Doppioni di parametro sui rapporti di prova ──────────────
    // Ottimizzata: il join pesante (certificato→campione→prove→metodo) viene letto UNA sola
    // volta e materializzato in #b; poi i gruppi duplicati si trovano con GROUP BY (che consente
    // COUNT(DISTINCT) per l'eccezione del valore energetico 236) e si rilegge il solo dettaglio
    // segnalato. Evita il doppio ordinamento a finestra sull'intero set.
    private const string SqlDuplicati = """
        SET NOCOUNT ON;

        SELECT
            ce.IdCertificato, ce.NCertificato, ce.DataCertificato,
            ca.IdCampione, ca.CodCampione,
            m.IdParametro, p.NomeParametro, p.IdTipoParametro,
            pr.IdRigaMetodo,
            LTRIM(RTRIM(rm.DenominazioneRiga)) AS DenominazioneRiga,
            pr.IdProva, pr.ValoreCalcolato,
            ISNULL(pr.MaggioreDi, 0) AS MaggioreDi,
            ISNULL(pr.MinoreDi,   0) AS MinoreDi
        INTO #b
        FROM Certificato ce
            INNER JOIN Campione   ca ON ca.IdCertificato = ce.IdCertificato
            INNER JOIN prove      pr ON pr.IdCampione    = ca.IdCampione
            INNER JOIN rigametodo rm ON rm.IdRigametodo  = pr.IdRigaMetodo
            INNER JOIN Metodo     m  ON m.IdMetodo        = rm.IdMetodo
            LEFT  JOIN Parametro  p  ON p.IdParametro     = m.IdParametro
        WHERE ce.DataCertificato >= @Da
          AND ce.DataCertificato <  @A
          AND pr.IdRigaMetodo <> 18963
          AND ISNULL(p.IdTipoParametro, 0) <> 2;

        ;WITH flag_denom AS (
            -- Tutti i parametri tranne il 236: stesso parametro + stessa denominazione ripetuti.
            SELECT IdCampione,
                   ISNULL(IdParametro, -1)      AS Par,
                   ISNULL(DenominazioneRiga,'') AS Den,
                   COUNT(*)                     AS Occ
            FROM #b
            WHERE ISNULL(IdParametro, -1) <> 236
            GROUP BY IdCampione, ISNULL(IdParametro, -1), ISNULL(DenominazioneRiga, '')
            HAVING COUNT(*) > 1
        ),
        flag_236 AS (
            -- Valore energetico: segnala se >2 occorrenze, oppure 2 ma non con due righe diverse (kJ/kcal).
            SELECT IdCampione, IdParametro, COUNT(*) AS Occ
            FROM #b
            WHERE IdParametro = 236
            GROUP BY IdCampione, IdParametro
            HAVING COUNT(*) > 1
               AND NOT (COUNT(*) = 2 AND COUNT(DISTINCT IdRigaMetodo) = 2)
        )
        SELECT
            b.IdCertificato, b.NCertificato, b.DataCertificato,
            b.IdCampione, b.CodCampione,
            b.IdParametro, b.NomeParametro, b.IdTipoParametro,
            b.IdRigaMetodo, b.DenominazioneRiga, b.IdProva,
            b.ValoreCalcolato, b.MaggioreDi, b.MinoreDi,
            ISNULL(fd.Occ, f2.Occ) AS Occorrenze
        FROM #b b
            LEFT JOIN flag_denom fd
                ON fd.IdCampione = b.IdCampione
               AND fd.Par        = ISNULL(b.IdParametro, -1)
               AND fd.Den        = ISNULL(b.DenominazioneRiga, '')
            LEFT JOIN flag_236 f2
                ON b.IdParametro = 236
               AND f2.IdCampione = b.IdCampione
               AND f2.IdParametro = b.IdParametro
        WHERE fd.IdCampione IS NOT NULL OR f2.IdCampione IS NOT NULL
        ORDER BY Occorrenze DESC, b.IdCampione, b.IdParametro, b.DenominazioneRiga, b.IdRigaMetodo;

        DROP TABLE #b;
        """;

    public async Task<IReadOnlyList<RigaDuplicatoRapporto>> DuplicatiRapportiAsync(int annoDa, int annoA, CancellationToken ct = default)
    {
        if (annoA < annoDa) (annoDa, annoA) = (annoA, annoDa);
        var da = new DateTime(annoDa, 1, 1);
        var a = new DateTime(annoA, 1, 1).AddYears(1);   // primo giorno dell'anno successivo (estremo escluso)
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaDuplicatoRapporto>(
            new CommandDefinition(SqlDuplicati, new { Da = da, A = a }, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // ── 2. Metodi attivi senza cifre significative / decimali max ───
    private const string SqlSenzaArrotondamento = """
        SELECT
            m.IdMetodo, m.IdParametro, m.IdTipoMetodo,
            m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
            m.Cifre_significative AS CifreSignificative,
            m.Decimali_Max        AS DecimaliMax
        FROM metodo m
        WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
          AND (m.Cifre_significative IS NULL OR m.Decimali_Max IS NULL)
        ORDER BY m.IdParametro, m.IdMetodo
        """;

    public async Task<IReadOnlyList<RigaMetodoArrotondamento>> MetodiSenzaArrotondamentoAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoArrotondamento>(
            new CommandDefinition(SqlSenzaArrotondamento, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // ── 3. Metodi attivi con margine sotto soglia (o negativo) ──────
    private const string SqlMargine = """
        SELECT
            m.IdMetodo, m.IdParametro, m.IdTipoMetodo,
            m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
            m.CostoAnoi, m.CostoMetodoEuro,
            CAST(100.0 * (m.CostoMetodoEuro - m.CostoAnoi) / NULLIF(m.CostoMetodoEuro, 0) AS DECIMAL(9,2)) AS MarginePct
        FROM metodo m
        WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
          AND m.CostoAnoi IS NOT NULL
          AND m.CostoMetodoEuro IS NOT NULL AND m.CostoMetodoEuro <> 0
          AND (m.CostoMetodoEuro - m.CostoAnoi) / m.CostoMetodoEuro < @Soglia
        ORDER BY MarginePct
        """;

    public async Task<IReadOnlyList<RigaMetodoMargine>> MetodiMargineAsync(decimal sogliaFrazione, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoMargine>(
            new CommandDefinition(SqlMargine, new { Soglia = sogliaFrazione }, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // ── 4. Metodi attivi potenzialmente doppi ───────────────────────
    private const string SqlDoppi = """
        SELECT x.IdMetodo, x.IdParametro, x.IdTipoMetodo,
               x.RifMetodo, x.DettagliMetodo, x.RifBiblio, x.IdUnita, x.NMetodi
        FROM (
            SELECT m.IdMetodo, m.IdParametro, m.IdTipoMetodo,
                   m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
                   COUNT(*) OVER (PARTITION BY m.IdParametro,
                                  LOWER(LTRIM(RTRIM(m.DettagliMetodo))),
                                  LOWER(LTRIM(RTRIM(m.RifMetodo)))) AS NMetodi
            FROM metodo m
            WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
        ) x
        WHERE x.NMetodi > 1
        ORDER BY x.IdParametro, x.DettagliMetodo, x.RifMetodo, x.IdMetodo
        """;

    public async Task<IReadOnlyList<RigaMetodoDoppio>> MetodiDoppiAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoDoppio>(
            new CommandDefinition(SqlDoppi, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // ── 5/6. Lotti con riferimento mancante (ricevitore / vettore) ──
    // Campo variabile (@Colonna) non parametrizzabile: si compone la clausola con nome fisso.
    private const string SqlLottiSenza = """
        SELECT lo.IdLotto, lo.CodLotto, lo.DataRic, lo.IdCommessa, cl.RagioneSociale
        FROM Lotto lo
            LEFT JOIN commessa        cm ON cm.IdCommessa    = lo.IdCommessa
            LEFT JOIN ContattoCliente cc ON cc.IDContatto    = cm.IDContatto
            LEFT JOIN Insediamento    i  ON i.IDInsediamento = cc.IDInsediamento
            LEFT JOIN Clienti         cl ON cl.IDCliente     = i.IDCliente
        WHERE lo.DataRic >= @Da AND lo.DataRic < @A
          AND {0} IS NULL
        ORDER BY lo.DataRic DESC, lo.CodLotto
        """;

    public Task<IReadOnlyList<RigaLottoErrore>> LottiSenzaRicevitoreAsync(int annoDa, int annoA, CancellationToken ct = default)
        => LottiSenzaAsync("lo.IdRicevitore", annoDa, annoA, ct);

    public Task<IReadOnlyList<RigaLottoErrore>> LottiSenzaVettoreAsync(int annoDa, int annoA, CancellationToken ct = default)
        => LottiSenzaAsync("lo.IdVettore", annoDa, annoA, ct);

    private async Task<IReadOnlyList<RigaLottoErrore>> LottiSenzaAsync(string colonna, int annoDa, int annoA, CancellationToken ct)
    {
        var (da, a) = Intervallo(annoDa, annoA);
        var sql = string.Format(SqlLottiSenza, colonna);   // colonna: valore interno costante, non input utente
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaLottoErrore>(
            new CommandDefinition(sql, new { Da = da, A = a }, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // ── 7. Coerenza di categoria prelevatore/vettore + verbale ──────
    // Categoria decisa dal NOME (come ClienteOTerzaParte in Accettazione): "cliente" o
    // "terza parte" = esterno ('E'); ogni altro nome = interno ('I').
    // Regola: prelevatore e vettore devono essere della STESSA categoria. Inoltre, se il
    // prelevatore è cliente/terza parte, il verbale (Lotto.BuonoPrelievo) non deve esserci.
    private const string SqlCoerenza = """
        SELECT
            x.IdLotto, x.CodLotto, x.DataRic,
            x.IdCampione, x.CodCampione,
            x.IdPrelevatore, x.Prelevatore,
            x.IdVettore, x.Vettore, x.BuonoPrelievo,
            CASE WHEN x.TipoVett IS NOT NULL AND x.TipoVett <> x.TipoPrel
                 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS AnomaliaVettore,
            CASE WHEN x.TipoPrel = 'E' AND x.BuonoPrelievo IS NOT NULL AND x.BuonoPrelievo <> ''
                 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS AnomaliaVerbale
        FROM (
            SELECT
                lo.IdLotto, lo.CodLotto, lo.DataRic,
                ca.IdCampione, ca.CodCampione,
                ca.IdPrelevatore,
                LTRIM(RTRIM(ISNULL(op.CognomeOrg, '') + ' ' + ISNULL(op.NomeOrg, ''))) AS Prelevatore,
                lo.IdVettore,
                LTRIM(RTRIM(ISNULL(ov.CognomeOrg, '') + ' ' + ISNULL(ov.NomeOrg, ''))) AS Vettore,
                LTRIM(RTRIM(CONVERT(nvarchar(50), lo.BuonoPrelievo))) AS BuonoPrelievo,
                CASE WHEN REPLACE(LOWER(LTRIM(RTRIM(ISNULL(op.CognomeOrg,'') + ' ' + ISNULL(op.NomeOrg,'')))), '_', ' ') = 'cliente'
                       OR REPLACE(LOWER(ISNULL(op.CognomeOrg,'') + ' ' + ISNULL(op.NomeOrg,'')), '_', ' ') LIKE '%terza parte%'
                     THEN 'E' ELSE 'I' END AS TipoPrel,
                CASE WHEN lo.IdVettore IS NULL THEN NULL
                     WHEN REPLACE(LOWER(LTRIM(RTRIM(ISNULL(ov.CognomeOrg,'') + ' ' + ISNULL(ov.NomeOrg,'')))), '_', ' ') = 'cliente'
                       OR REPLACE(LOWER(ISNULL(ov.CognomeOrg,'') + ' ' + ISNULL(ov.NomeOrg,'')), '_', ' ') LIKE '%terza parte%'
                     THEN 'E' ELSE 'I' END AS TipoVett
            FROM Campione ca
                INNER JOIN Lotto    lo ON lo.IdLotto     = ca.IdLotto
                LEFT  JOIN mansione mp ON mp.IdMansione  = ca.IdPrelevatore
                LEFT  JOIN Organico op ON op.IdOrganico  = mp.IdOrganico
                LEFT  JOIN mansione mv ON mv.IdMansione  = lo.IdVettore
                LEFT  JOIN Organico ov ON ov.IdOrganico  = mv.IdOrganico
            WHERE lo.DataRic >= @Da AND lo.DataRic < @A
              AND ca.IdPrelevatore IS NOT NULL
              AND LTRIM(RTRIM(ISNULL(op.CognomeOrg,'') + ' ' + ISNULL(op.NomeOrg,''))) <> ''
        ) x
        WHERE
            (x.TipoVett IS NOT NULL AND x.TipoVett <> x.TipoPrel)
            OR
            (x.TipoPrel = 'E' AND x.BuonoPrelievo IS NOT NULL AND x.BuonoPrelievo <> '')
        ORDER BY x.DataRic DESC, x.CodLotto, x.CodCampione
        """;

    public async Task<IReadOnlyList<RigaCoerenzaPrelevatore>> CoerenzaPrelevatoreVettoreAsync(int annoDa, int annoA, CancellationToken ct = default)
    {
        var (da, a) = Intervallo(annoDa, annoA);
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaCoerenzaPrelevatore>(
            new CommandDefinition(SqlCoerenza, new { Da = da, A = a }, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    private static (DateTime Da, DateTime A) Intervallo(int annoDa, int annoA)
    {
        if (annoA < annoDa) (annoDa, annoA) = (annoA, annoDa);
        return (new DateTime(annoDa, 1, 1), new DateTime(annoA, 1, 1).AddYears(1));
    }
}
