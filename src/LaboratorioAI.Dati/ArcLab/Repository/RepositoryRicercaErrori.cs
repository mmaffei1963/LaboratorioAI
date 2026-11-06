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
            m.IdMetodo, m.IdParametro, p.NomeParametro, m.IdTipoMetodo,
            m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
            m.Cifre_significative AS CifreSignificative,
            m.Decimali_Max        AS DecimaliMax
        FROM metodo m
            LEFT JOIN Parametro p ON p.IdParametro = m.IdParametro
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
            m.IdMetodo, m.IdParametro, p.NomeParametro, m.IdTipoMetodo,
            m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
            m.CostoAnoi, m.CostoMetodoEuro,
            CAST(100.0 * (m.CostoMetodoEuro - m.CostoAnoi) / NULLIF(m.CostoMetodoEuro, 0) AS DECIMAL(9,2)) AS MarginePct
        FROM metodo m
            LEFT JOIN Parametro p ON p.IdParametro = m.IdParametro
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
        SELECT x.IdMetodo, x.IdParametro, x.NomeParametro, x.IdTipoMetodo,
               x.RifMetodo, x.DettagliMetodo, x.RifBiblio, x.IdUnita, x.NMetodi
        FROM (
            SELECT m.IdMetodo, m.IdParametro, p.NomeParametro, m.IdTipoMetodo,
                   m.RifMetodo, m.DettagliMetodo, m.RifBiblio, m.IdUnita,
                   COUNT(*) OVER (PARTITION BY m.IdParametro,
                                  LOWER(LTRIM(RTRIM(m.DettagliMetodo))),
                                  LOWER(LTRIM(RTRIM(m.RifMetodo)))) AS NMetodi
            FROM metodo m
                LEFT JOIN Parametro p ON p.IdParametro = m.IdParametro
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

    // ── 9. Denominazioni dei metodi (09/10/2026) ────────────────────
    // Solo i dati: il confronto (denominazione che non cita il parametro, stessa denominazione per parametri diversi)
    // è in Core (ControlloDenominazioniMetodi), testabile senza database.
    private const string SqlDenominazioni = """
        SELECT m.IdMetodo, m.IdParametro,
               CONVERT(nvarchar(300), p.NomeParametro)  AS NomeParametro,
               CONVERT(nvarchar(500), m.DettagliMetodo) AS DettagliMetodo,
               CONVERT(nvarchar(300), m.RifBiblio)      AS RifBiblio
        FROM metodo m
            LEFT JOIN Parametro p ON p.IdParametro = m.IdParametro
        WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
        ORDER BY p.NomeParametro, m.IdMetodo
        """;

    public async Task<IReadOnlyList<RigaMetodoDenominazione>> MetodiDenominazioneAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoDenominazione>(
            new CommandDefinition(SqlDenominazioni, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // Rapporti di prova emessi nel periodo con almeno una prova del metodo (come in Consultazione: Campione.IdCertificato).
    private const string SqlRapportiMetodo = """
        SELECT rm.IdMetodo,
               COUNT(DISTINCT ce.IdCertificato) AS NRapporti,
               MAX(ce.DataCertificato)          AS UltimoRapporto
        FROM prove pr
            INNER JOIN rigametodo  rm ON rm.IdRigametodo = pr.IdRigaMetodo
            INNER JOIN Campione    ca ON ca.IdCampione = pr.IdCampione
            INNER JOIN Certificato ce ON ce.IdCertificato = ca.IdCertificato
        WHERE rm.IdMetodo IN @Ids
          AND ce.DataCertificato >= @Da AND ce.DataCertificato < @A
        GROUP BY rm.IdMetodo
        """;

    public async Task<IReadOnlyList<RigaRapportiMetodo>> RapportiPerMetodoAsync(
        IReadOnlyCollection<int> idMetodi, int annoDa, int annoA, CancellationToken ct = default)
    {
        if (idMetodi.Count == 0) return Array.Empty<RigaRapportiMetodo>();
        var (da, a) = Intervallo(annoDa, annoA);
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaRapportiMetodo>(
            new CommandDefinition(SqlRapportiMetodo, new { Ids = idMetodi.Distinct().ToList(), Da = da, A = a },
                commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
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

    // ── 8. Campioni «chiusi» non pronti alla stampa ─────────────────
    public async Task<IReadOnlyList<RigaCampioneStampa>> CampioniProntiStampaAsync(
        DateOnly? da, DateOnly? a, string? codLotto, string? codCampione, CancellationToken ct = default)
    {
        bool perChiave = !string.IsNullOrWhiteSpace(codLotto) || !string.IsNullOrWhiteSpace(codCampione);

        var cond = new List<string> { "ca.AnalisiChiusa = 1", "pr.IdRigaMetodo <> 18963" };
        var par = new DynamicParameters();

        if (perChiave)
        {
            // Ricerca per singolo lotto/campione: le date sono ignorate.
            if (!string.IsNullOrWhiteSpace(codLotto)) { cond.Add("lo.CodLotto = @CodLotto"); par.Add("CodLotto", codLotto!.Trim()); }
            if (!string.IsNullOrWhiteSpace(codCampione)) { cond.Add("ca.CodCampione = @CodCampione"); par.Add("CodCampione", codCampione!.Trim()); }
        }
        else
        {
            // Ricerca per data sul ricevimento del lotto (default: ultimi 30 giorni).
            var dda = (da ?? DateOnly.FromDateTime(DateTime.Today).AddDays(-30)).ToDateTime(TimeOnly.MinValue);
            var daa = (a ?? DateOnly.FromDateTime(DateTime.Today)).AddDays(1).ToDateTime(TimeOnly.MinValue);
            cond.Add("lo.DataRic >= @Da AND lo.DataRic < @A");
            par.Add("Da", dda); par.Add("A", daa);
        }

        // La riga è segnalata se ANCHE UNA sola condizione di completezza non è rispettata.
        cond.Add("""
            (
                ISNULL(pr.VerificaValore, 0) = 0
                OR pr.DataInizioProva IS NULL
                OR pr.DataFineProva  IS NULL
                OR rm.Arrotonda   IS NULL
                OR rm.MaxDecimali IS NULL
            )
            """);

        var sql = $"""
            SELECT
                lo.IdLotto, lo.CodLotto, lo.DataRic,
                ca.IdCampione, ca.CodCampione,
                cl.RagioneSociale,
                pr.IdProva, pr.IdRigaMetodo, rm.DenominazioneRiga, p.NomeParametro,
                pr.VerificaValore, pr.DataInizioProva, pr.DataFineProva,
                rm.Arrotonda, rm.MaxDecimali
            FROM Campione ca
                INNER JOIN Lotto           lo ON lo.IdLotto        = ca.IdLotto
                LEFT  JOIN commessa        cm ON cm.IdCommessa     = lo.IdCommessa
                LEFT  JOIN ContattoCliente cc ON cc.IDContatto     = cm.IDContatto
                LEFT  JOIN Insediamento    i  ON i.IDInsediamento  = cc.IDInsediamento
                LEFT  JOIN Clienti         cl ON cl.IDCliente      = i.IDCliente
                INNER JOIN prove           pr ON pr.IdCampione     = ca.IdCampione
                INNER JOIN rigametodo      rm ON rm.IdRigametodo   = pr.IdRigaMetodo
                INNER JOIN metodo          m  ON m.IdMetodo        = rm.IdMetodo
                LEFT  JOIN Parametro       p  ON p.IdParametro     = m.IdParametro
            WHERE {string.Join("\n              AND ", cond)}
            ORDER BY lo.DataRic DESC, lo.CodLotto, ca.CodCampione, pr.IdProva
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaCampioneStampa>(
            new CommandDefinition(sql, par, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }

    // ── 10. Prezzi commesse ArcLab ↔ listini Marea (28/10/2026) ────
    // Solo clienti ArcLab attivi e con codice contabilità (anche per le righe di Marea): gli altri non vanno nel portale.
    public async Task<(IReadOnlyList<CommessaPrezzoArcLab> Commesse, IReadOnlyList<ListinoMarea> Listini)> PrezziCommesseMareaAsync(CancellationToken ct = default)
    {
        var sql = $"""
            SELECT CAST(cm.IdCommessa AS int) AS IdCommessa, CAST(i.IDCliente AS int) AS IdCliente, cl.RagioneSociale,
                   CAST(cm.IDInsediamento AS int) AS IdInsediamento, i.NomeInsed AS Insediamento,
                   CAST(cm.IDControllo AS int) AS IdControllo, c.Controllo,
                   CAST(cm.PrezzoControllo AS decimal(18, 2)) AS Prezzo,
                   CAST(cm.NContratto AS int) AS NContratto, cm.DescrIncarico,
                   cm.DataCommessa, cm.DataUltimoRinnovo, uso.UltimoCampione
            FROM commessa cm
                INNER JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
                INNER JOIN Clienti cl     ON cl.IDCliente = i.IDCliente
                                          AND cl.Attivo = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL
                LEFT  JOIN Controllo c    ON c.IdControllo = cm.IDControllo
                OUTER APPLY (SELECT MAX(lo.DataRic) AS UltimoCampione FROM Lotto lo WHERE lo.IdCommessa = cm.IdCommessa) AS uso
            WHERE cm.IdTipoCommessa = {CostantiArcLab.TipoCommessaLaboratorio}
              AND cm.DataScadenzaInc IS NULL
              AND cm.IDControllo IS NOT NULL
              AND cm.PrezzoControllo > 0;

            SELECT CAST(mc.IDArcLab AS int) AS IdClienteArcLab, mc.Nome AS Cliente, CAST(l.IDControllo AS int) AS IdControllo,
                   l.DescrizioneIncarico, CAST(l.Prezzo AS decimal(18, 2)) AS Prezzo, l.UltimaModifica,
                   CAST(l.IDContratto AS int) AS IdContratto
            FROM [Marea].dbo.Listini l
                INNER JOIN [Marea].dbo.Clienti mc ON mc.Id = l.IDCliente
                INNER JOIN Clienti cl ON cl.IDCliente = mc.IDArcLab AND cl.Attivo = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL
            WHERE mc.IDArcLab IS NOT NULL
              AND ISNULL(mc.Obsoleto, 0) = 0
              AND l.IDControllo IS NOT NULL
              AND l.Prezzo IS NOT NULL;
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, commandTimeout: TimeoutDiagnostica, cancellationToken: ct));
        var commesse = (await multi.ReadAsync<CommessaPrezzoArcLab>()).ToList();
        var listini = (await multi.ReadAsync<ListinoMarea>()).ToList();
        return (commesse, listini);
    }

    // ── 11. Commesse ArcLab ↔ Marea oltre ai prezzi (28/10/2026) ───
    public async Task<DatiCommesseMarea> CommesseMareaAsync(CancellationToken ct = default)
    {
        var attive = $"cm.IdTipoCommessa = {CostantiArcLab.TipoCommessaLaboratorio} AND cm.DataScadenzaInc IS NULL AND cm.IDControllo IS NOT NULL";
        var sql = $"""
            SELECT CAST(cm.IdCommessa AS int) AS IdCommessa, CAST(i.IDCliente AS int) AS IdCliente, cl.RagioneSociale,
                   CAST(cm.IDInsediamento AS int) AS IdInsediamento, i.NomeInsed AS Insediamento,
                   CAST(cm.IDControllo AS int) AS IdControllo, c.Controllo,
                   CAST(cm.NContratto AS int) AS NContratto, cm.DescrIncarico,
                   cm.DataCommessa, cm.DataUltimoRinnovo, uso.UltimoCampione
            FROM commessa cm
                INNER JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
                INNER JOIN Clienti cl     ON cl.IDCliente = i.IDCliente
                                          AND cl.Attivo = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL
                LEFT  JOIN Controllo c    ON c.IdControllo = cm.IDControllo
                OUTER APPLY (SELECT MAX(lo.DataRic) AS UltimoCampione FROM Lotto lo WHERE lo.IdCommessa = cm.IdCommessa) AS uso
            WHERE {attive};

            SELECT DISTINCT CAST(rm.IdControllo AS int) AS IdControllo, p.NomeParametro, rm.DenominazioneRiga
            FROM rigametodo rm
                INNER JOIN metodo m    ON m.IdMetodo = rm.IdMetodo
                LEFT  JOIN Parametro p ON p.IdParametro = m.IdParametro
            WHERE rm.IdRigametodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
              AND rm.IdControllo IN (SELECT cm.IDControllo FROM commessa cm WHERE {attive});

            SELECT CAST(IdControllo AS int) AS IdControllo, Controllo FROM Controllo;

            SELECT CAST(mco.IDCommessa AS int) AS IdCommessa, mco.DescCommessa, CAST(mco.IDClienteArcLab AS int) AS IdClienteArcLab,
                   CAST(mco.IDInsediamentoArcLab AS int) AS IdInsediamentoArcLab, mco.Parametri
            FROM [Marea].dbo.Commesse mco
                INNER JOIN Clienti cl ON cl.IDCliente = mco.IDClienteArcLab AND cl.Attivo = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL
            WHERE ISNULL(mco.Obsoleto, 0) = 0;

            SELECT CAST(mc.IDArcLab AS int) AS IdClienteArcLab, mc.Nome AS Cliente, CAST(l.IDControllo AS int) AS IdControllo,
                   l.DescrizioneIncarico, CAST(ISNULL(l.Prezzo, 0) AS decimal(18, 2)) AS Prezzo, l.UltimaModifica,
                   CAST(l.IDContratto AS int) AS IdContratto
            FROM [Marea].dbo.Listini l
                INNER JOIN [Marea].dbo.Clienti mc ON mc.Id = l.IDCliente
                INNER JOIN Clienti cl ON cl.IDCliente = mc.IDArcLab AND cl.Attivo = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL
            WHERE mc.IDArcLab IS NOT NULL
              AND ISNULL(mc.Obsoleto, 0) = 0
              AND l.IDControllo IS NOT NULL;

            -- clienti non obsoleti nel portale ma non attivi in ArcLab (o senza codice contabilità, inesistenti, non collegati)
            SELECT CAST(mc.Id AS int) AS IdMarea, mc.Nome, CAST(mc.CodContabilita AS nvarchar(50)) AS CodContabilitaMarea,
                   CAST(mc.IDArcLab AS int) AS IdArcLab, cl.RagioneSociale AS RagioneSocialeArcLab,
                   CAST(CASE WHEN mc.IDArcLab IS NULL THEN 'non collegato ad ArcLab (IDArcLab vuoto)'
                             WHEN cl.IDCliente IS NULL THEN 'cliente non trovato in ArcLab'
                             WHEN ISNULL(cl.Attivo, 0) <> 1 THEN 'non attivo in ArcLab'
                             ELSE 'senza codice contabilità in ArcLab' END AS nvarchar(100)) AS Motivo,
                   CAST((SELECT COUNT(*) FROM [Marea].dbo.Listini l WHERE l.IDCliente = mc.Id) AS int) AS Listini,
                   CAST((SELECT COUNT(*) FROM [Marea].dbo.Commesse mco WHERE mco.IDClienteArcLab = mc.IDArcLab AND ISNULL(mco.Obsoleto, 0) = 0) AS int) AS Commesse,
                   mc.PortaleUltimoAccesso AS UltimoAccessoPortale
            FROM [Marea].dbo.Clienti mc
                LEFT JOIN Clienti cl ON cl.IDCliente = mc.IDArcLab
            WHERE ISNULL(mc.Obsoleto, 0) = 0
              AND NOT (ISNULL(cl.Attivo, 0) = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL);
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, commandTimeout: TimeoutDiagnostica, cancellationToken: ct));
        var commesse = (await multi.ReadAsync<CommessaArcLab>()).ToList();
        var parametri = (await multi.ReadAsync<ParametroControlloArcLab>()).ToList();
        var controlli = (await multi.ReadAsync<NomeControlloArcLab>()).ToList();
        var commesseMarea = (await multi.ReadAsync<CommessaMarea>()).ToList();
        var listini = (await multi.ReadAsync<ListinoMarea>()).ToList();
        var clientiNonAttivi = (await multi.ReadAsync<ClienteMareaNonAttivo>()).ToList();
        return new DatiCommesseMarea(commesse, parametri, controlli, commesseMarea, listini, clientiNonAttivi);
    }

    // ── 12. Commesse attive senza numero di contratto (28/10/2026) ──
    public async Task<IReadOnlyList<CommessaSenzaContratto>> CommesseSenzaContrattoAsync(CancellationToken ct = default)
    {
        var sql = $"""
            SELECT CAST(cm.IdCommessa AS int) AS IdCommessa, CAST(cm.CodCommessa AS nvarchar(50)) AS CodCommessa,
                   CAST(i.IDCliente AS int) AS IdCliente, cl.RagioneSociale,
                   LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))) AS CodContabilita,
                   i.NomeInsed AS Insediamento, CAST(cm.IDControllo AS int) AS IdControllo, c.Controllo, cm.DescrIncarico,
                   CAST(cm.PrezzoControllo AS decimal(18, 2)) AS Prezzo, cm.DataCommessa,
                   uso.UltimoCampione, CAST(ISNULL(uso.Lotti, 0) AS int) AS Lotti
            FROM commessa cm
                INNER JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
                INNER JOIN Clienti cl     ON cl.IDCliente = i.IDCliente
                                          AND cl.Attivo = 1 AND NULLIF(LTRIM(RTRIM(CAST(cl.CODContabilita AS nvarchar(50)))), '') IS NOT NULL
                LEFT  JOIN Controllo c    ON c.IdControllo = cm.IDControllo
                OUTER APPLY (SELECT MAX(lo.DataRic) AS UltimoCampione, COUNT(*) AS Lotti FROM Lotto lo WHERE lo.IdCommessa = cm.IdCommessa) AS uso
            WHERE cm.IdTipoCommessa = {CostantiArcLab.TipoCommessaLaboratorio}
              AND cm.DataScadenzaInc IS NULL
              AND cm.NContratto IS NULL
            ORDER BY uso.UltimoCampione DESC, cl.RagioneSociale;
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<CommessaSenzaContratto>(new CommandDefinition(sql, commandTimeout: TimeoutDiagnostica, cancellationToken: ct))).ToList();
    }
}
