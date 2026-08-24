using System.Text;
using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryCampioni" />
public class RepositoryCampioni : IRepositoryCampioni
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryCampioni(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // ═══════════════════════════════════════════════════════════════
    // CAMPIONI DA VALIDARE
    // ═══════════════════════════════════════════════════════════════

    private static readonly string SqlDaValidare = $"""
        SELECT
            ca.IdCampione,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            cl.RagioneSociale,
            i.NomeInsed        AS NomeInsediamento,
            ca.DenomCampione   AS DenominazioneCampione,
            mx.Matrice,
            lo.DataRic         AS DataRicezione,
            DATEDIFF(day, lo.DataRic, GETDATE()) AS GiorniInLaboratorio,
            COUNT(pr.IdProva)  AS NumeroProve,
            STUFF((
                SELECT ', ' + o.OperatoreFine
                FROM (
                    SELECT DISTINCT LTRIM(RTRIM(pr2.OperatoreFine)) AS OperatoreFine
                    FROM prove pr2
                    WHERE pr2.IdCampione = ca.IdCampione
                      AND pr2.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
                      AND NULLIF(LTRIM(RTRIM(pr2.OperatoreFine)), '') IS NOT NULL
                ) o
                ORDER BY o.OperatoreFine
                FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS Operatori
        FROM Campione ca
        INNER JOIN Lotto lo         ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm      ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i   ON i.IDInsediamento = cc.IDInsediamento
        INNER JOIN Clienti cl       ON cl.IDCliente = i.IDCliente
        LEFT  JOIN matrice mx       ON mx.IdMatrice = ca.IdMatrice
        INNER JOIN prove pr         ON pr.IdCampione = ca.IdCampione
                                    AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
        WHERE (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
          AND ISNULL(ca.AnalisiChiusa, 0) = 0
        GROUP BY
            ca.IdCampione, lo.CodLotto, ca.CodCampione, cl.RagioneSociale,
            i.NomeInsed, ca.DenomCampione, mx.Matrice, lo.DataRic
        HAVING COUNT(pr.IdProva) > 0
           AND SUM(CASE WHEN pr.VerificaValore = 1 THEN 0 ELSE 1 END) = 0
        ORDER BY lo.DataRic ASC
        """;

    public async Task<IReadOnlyList<CampioneDaValidare>> DaValidareAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<CampioneDaValidare>(
            new CommandDefinition(SqlDaValidare, cancellationToken: ct));
        return righe.ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // CAMPIONE COMPLETO
    // ═══════════════════════════════════════════════════════════════

    private const string SqlDettaglio = """
        SELECT TOP 1
            ca.IdCampione,
            ca.IdLotto,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            cl.RagioneSociale,
            i.NomeInsed        AS NomeInsediamento,
            ca.DenomCampione   AS DenominazioneCampione,
            ca.DenomProdotto   AS DenominazioneProdotto,
            mx.Matrice,
            co.Controllo,
            ca.LuogoPrelievo,
            ca.PuntoPrelievo,
            lo.BuonoPrelievo   AS VerbalePrelievo,
            lo.DataRic         AS DataRicezione,
            lo.DataPrelievo,
            NULLIF(ca.IdCertificato, 0) AS IdCertificato,
            DATEDIFF(day, lo.DataRic, GETDATE()) AS GiorniInLaboratorio
        FROM Campione ca
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        LEFT  JOIN Controllo co       ON co.IDControllo = cm.IDControllo
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
        INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
        LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        WHERE ca.IdCampione = @IdCampione
        """;

    private static readonly string SqlProve = $"""
        SELECT
            pr.IdProva,
            pr.IdRigaMetodo,
            p.NomeParametro     AS Parametro,
            rm.DenominazioneRiga,
            m.RifBiblio         AS RiferimentoNormativo,
            pr.ValoreCalcolato,
            ISNULL(pr.MaggioreDi, 0) AS MaggioreDi,
            ISNULL(pr.MinoreDi,   0) AS MinoreDi,
            pr.LimiteInferiore,
            pr.LimiteSuperiore,
            u.UnitaMisura,
            pr.VerificaValore,
            le.LabEsterno       AS LaboratorioEsterno,
            CAST(le.NumeroAccredia AS NVARCHAR(50)) AS NumeroLaboratorioEsterno,
            ISNULL(m.Accreditato, 0) AS Accreditato,
            -- Accreditamento del laboratorio esterno: casella AccrLabEsterno
            -- della tabella labEstMetodo, legata al metodo da IdMetodo.
            CASE WHEN EXISTS (SELECT 1 FROM labEstMetodo lem
                              WHERE lem.IdMetodo = m.IdMetodo
                                AND lem.AccrLabEsterno = 1) THEN 1 ELSE 0 END AS AccreditatoEsterno,
            pr.Note
        FROM prove pr
        INNER JOIN rigametodo rm    ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m         ON m.IdMetodo = rm.IdMetodo
        LEFT  JOIN Parametro p      ON p.IdParametro = m.IdParametro
        LEFT  JOIN Unitadimisura u  ON u.IdUnita = rm.IdUnita
        LEFT  JOIN LabEsterno le    ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE pr.IdCampione = @IdCampione
          AND (@IncludiProveEscluse = 1
               OR pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere})
        ORDER BY rm.DenominazioneRiga
        """;

    public async Task<CampioneCompleto?> CompletoAsync(
        int idCampione, CancellationToken ct = default, bool includiProveEscluse = false)
    {
        using var cn = await _fabbrica.ApriAsync(ct);

        var dettaglio = await cn.QueryFirstOrDefaultAsync<DettaglioCampione>(
            new CommandDefinition(SqlDettaglio, new { IdCampione = idCampione }, cancellationToken: ct));

        if (dettaglio is null) return null;

        var prove = await cn.QueryAsync<ProvaCampione>(
            new CommandDefinition(SqlProve,
                new { IdCampione = idCampione, IncludiProveEscluse = includiProveEscluse ? 1 : 0 },
                cancellationToken: ct));

        return new CampioneCompleto { Campione = dettaglio, Prove = prove.ToList() };
    }

    // ═══════════════════════════════════════════════════════════════
    // RICERCA
    // ═══════════════════════════════════════════════════════════════

    public async Task<EsitoRicerca<RisultatoRicercaCampione>> CercaAsync(
        CriteriRicercaCampioni criteri, CancellationToken ct = default)
    {
        var (dove, parametri) = ComponiFiltri(criteri);

        var sql = $"""
            WITH {CostantiArcLab.CteEstPortale}
            SELECT TOP (@Limite)
                ca.IdCampione,
                CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
                cl.RagioneSociale,
                cl.CODContabilita  AS CodContabilita,
                LTRIM(RTRIM(COALESCE(
                    NULLIF(CAST(cl.email AS nvarchar(400)), ''),
                    NULLIF(CAST(i.Email  AS nvarchar(400)), '')))) AS Email,
                i.NomeInsed        AS NomeInsediamento,
                ca.DenomCampione   AS DenominazioneCampione,
                mx.Matrice,
                cm.IDControllo     AS IdControllo,
                co.Controllo,
                ca.LuogoPrelievo,
                ca.PuntoPrelievo,
                lo.BuonoPrelievo   AS VerbalePrelievo,
                LTRIM(RTRIM(ISNULL(org.CognomeOrg, '') + ' ' + ISNULL(org.NomeOrg, ''))) AS Prelevatore,
                cm.PrezzoControllo,
                lo.DataRic         AS DataRicezione,
                lo.DataPrelievo,
                NULLIF(ca.IdCertificato, 0) AS IdCertificato,
                DATEDIFF(day, lo.DataRic, GETDATE()) AS GiorniInLaboratorio,
                (SELECT COUNT(*) FROM prove pr
                  WHERE pr.IdCampione = ca.IdCampione
                    AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) AS NumeroProve,
                CASE WHEN ep.Anno IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS EsternoPresente
            FROM Campione ca
            INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
            INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
            LEFT  JOIN Controllo co       ON co.IDControllo = cm.IDControllo
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
            LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
            LEFT  JOIN mansione msp       ON msp.IdMansione = ca.IdPrelevatore
            LEFT  JOIN Organico org       ON org.IdOrganico = msp.IdOrganico
            LEFT  JOIN mansione msr       ON msr.IdMansione = lo.IdRicevitore
            LEFT  JOIN Organico orgr      ON orgr.IdOrganico = msr.IdOrganico
            {CostantiArcLab.JoinEstPortale}
            WHERE {dove}
            ORDER BY lo.DataRic DESC, lo.CodLotto DESC
            """;

        parametri.Add("Limite", criteri.Limite);

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = (await cn.QueryAsync<RisultatoRicercaCampione>(
            new CommandDefinition(sql, parametri, cancellationToken: ct))).ToList();

        return new EsitoRicerca<RisultatoRicercaCampione>
        {
            Elementi = righe,
            Troncato = righe.Count >= criteri.Limite
        };
    }

    public async Task<TotaliRicerca> TotaliRicercaAsync(
        CriteriRicercaCampioni criteri, CancellationToken ct = default)
    {
        var (dove, parametri) = ComponiFiltri(criteri);

        var sql = $"""
            WITH {CostantiArcLab.CteEstPortale}
            SELECT COUNT(*) AS Totale, SUM(cm.PrezzoControllo) AS TotalePrezzo
            FROM Campione ca
            INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
            INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
            LEFT  JOIN Controllo co       ON co.IDControllo = cm.IDControllo
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
            LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
            LEFT  JOIN mansione msp       ON msp.IdMansione = ca.IdPrelevatore
            LEFT  JOIN Organico org       ON org.IdOrganico = msp.IdOrganico
            LEFT  JOIN mansione msr       ON msr.IdMansione = lo.IdRicevitore
            LEFT  JOIN Organico orgr      ON orgr.IdOrganico = msr.IdOrganico
            {CostantiArcLab.JoinEstPortale}
            WHERE {dove}
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<TotaliRicerca>(
            new CommandDefinition(sql, parametri, cancellationToken: ct)) ?? new TotaliRicerca();
    }

    /// <summary>
    /// Compone dinamicamente la clausola WHERE. Ogni filtro è parametrizzato:
    /// nessuna concatenazione di valori nella stringa SQL.
    /// </summary>
    private static (string Dove, DynamicParameters Parametri) ComponiFiltri(CriteriRicercaCampioni c)
    {
        var condizioni = new List<string> { "1 = 1" };
        var p = new DynamicParameters();

        void Aggiungi(string condizione, string nome, object? valore)
        {
            if (valore is null) return;
            if (valore is string s && string.IsNullOrWhiteSpace(s)) return;
            condizioni.Add(condizione);
            p.Add(nome, valore);
        }

        Aggiungi("lo.DataPrelievo >= @PrelievoDa",  "PrelievoDa",  c.PrelievoDa?.ToDateTime(TimeOnly.MinValue));
        Aggiungi("lo.DataPrelievo <= @PrelievoA",   "PrelievoA",   c.PrelievoA?.ToDateTime(TimeOnly.MaxValue));
        Aggiungi("lo.DataRic >= @RicezioneDa",      "RicezioneDa", c.RicezioneDa?.ToDateTime(TimeOnly.MinValue));
        Aggiungi("lo.DataRic <= @RicezioneA",       "RicezioneA",  c.RicezioneA?.ToDateTime(TimeOnly.MaxValue));

        Aggiungi("i.IDCliente = @IdCliente",             "IdCliente",      c.IdCliente);
        Aggiungi("i.IDInsediamento = @IdInsediamento",   "IdInsediamento", c.IdInsediamento);
        Aggiungi("ca.IdMatrice = @IdMatrice",            "IdMatrice",      c.IdMatrice);
        Aggiungi("cm.IDControllo = @IdControllo",        "IdControllo",    c.IdControllo);

        Aggiungi("ca.DenomCampione LIKE @Denominazione", "Denominazione", Contiene(c.DenominazioneCampione));
        Aggiungi("lo.BuonoPrelievo LIKE @Verbale",       "Verbale",       Contiene(c.VerbalePrelievo));
        Aggiungi("LTRIM(RTRIM(ISNULL(org.CognomeOrg, '') + ' ' + ISNULL(org.NomeOrg, ''))) LIKE @Prelevatore",
                 "Prelevatore", Contiene(c.Prelevatore));
        Aggiungi("LTRIM(RTRIM(ISNULL(orgr.CognomeOrg, '') + ' ' + ISNULL(orgr.NomeOrg, ''))) LIKE @Ricevitore",
                 "Ricevitore", Contiene(c.Ricevitore));
        Aggiungi("ca.LuogoPrelievo LIKE @Luogo",         "Luogo",         Contiene(c.LuogoPrelievo));
        Aggiungi("ca.PuntoPrelievo LIKE @Punto",         "Punto",         Contiene(c.PuntoPrelievo));

        // Email dell'amministratore: cercata su cliente e insediamento.
        if (!string.IsNullOrWhiteSpace(c.Email))
        {
            condizioni.Add("(CAST(cl.email AS nvarchar(400)) LIKE @Email OR CAST(i.Email AS nvarchar(400)) LIKE @Email)");
            p.Add("Email", $"%{c.Email.Trim()}%");
        }

        // Codice LIM: accetta "4530", "4530/01" oppure "4530-01"
        if (!string.IsNullOrWhiteSpace(c.CodiceLim))
        {
            var parti = c.CodiceLim.Split(new[] { '/', '-' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (int.TryParse(parti[0], out var lotto))
            {
                // CodLotto/CodCampione sono colonne testuali con valori anche non
                // numerici: TRY_CONVERT evita l'errore di conversione (245).
                condizioni.Add("TRY_CONVERT(int, lo.CodLotto) = @CodLotto");
                p.Add("CodLotto", lotto);

                if (parti.Length > 1 && int.TryParse(parti[1], out var campione))
                {
                    condizioni.Add("TRY_CONVERT(int, ca.CodCampione) = @CodCampione");
                    p.Add("CodCampione", campione);
                }
            }
        }

        if (c.IdLaboratorioEsterno is not null)
        {
            condizioni.Add($"""
                EXISTS (SELECT 1
                        FROM prove pr2
                        INNER JOIN rigametodo rm2 ON rm2.IdRigametodo = pr2.IdRigaMetodo
                        WHERE pr2.IdCampione = ca.IdCampione
                          AND rm2.IdLabEsterno = @IdLabEsterno)
                """);
            p.Add("IdLabEsterno", c.IdLaboratorioEsterno);
        }

        // Stato: replica la logica della proprietà calcolata sul modello
        switch (c.Stato)
        {
            case StatoCampione.Certificato:
                condizioni.Add("ca.IdCertificato IS NOT NULL AND ca.IdCertificato <> 0");
                break;

            case StatoCampione.InCorso:
                condizioni.Add($"""
                    (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
                    AND NOT (
                        ( DATEDIFF(day, lo.DataRic, GETDATE()) > {CostantiArcLab.GiorniPerSpeditoOriginale}
                          AND NOT EXISTS (SELECT 1 FROM prove pr3
                                          WHERE pr3.IdCampione = ca.IdCampione
                                            AND pr3.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) )
                        OR ep.Anno IS NOT NULL
                    )
                    """);
                break;

            case StatoCampione.SpeditoOriginale:
                condizioni.Add($"""
                    (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
                    AND (
                        ( DATEDIFF(day, lo.DataRic, GETDATE()) > {CostantiArcLab.GiorniPerSpeditoOriginale}
                          AND NOT EXISTS (SELECT 1 FROM prove pr4
                                          WHERE pr4.IdCampione = ca.IdCampione
                                            AND pr4.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) )
                        OR ep.Anno IS NOT NULL
                    )
                    """);
                break;
        }

        return (string.Join("\n              AND ", condizioni), p);

        static string? Contiene(string? valore) =>
            string.IsNullOrWhiteSpace(valore) ? null : $"%{valore.Trim()}%";
    }

    // ═══════════════════════════════════════════════════════════════
    // IDENTIFICATIVI TECNICI
    // ═══════════════════════════════════════════════════════════════

    private static readonly string SqlIdentificativi = $"""
        SELECT TOP 1
            lo.IdLotto,
            ca.IdCampione,
            lo.IdCommessa,
            cm.IDControllo   AS IdControllo,
            NULLIF(ca.IdCertificato, 0) AS IdCertificato,
            ce.NCertificato  AS NumeroCertificato,
            ce.DataCertificato,
            ISNULL(ca.AnalisiChiusa, 0) AS AnalisiChiusa,
            (SELECT COUNT(*) FROM prove pr
              WHERE pr.IdCampione = ca.IdCampione
                AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) AS NumeroProve,
            (SELECT COUNT(*) FROM prove pr
              WHERE pr.IdCampione = ca.IdCampione
                AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
                AND pr.VerificaValore = 1) AS ProveVerificate,
            STUFF((
               SELECT DISTINCT ', ' + le.LabEsterno
               FROM prove pr
               INNER JOIN rigametodo rm ON rm.IdRigametodo = pr.IdRigaMetodo
               INNER JOIN LabEsterno le ON le.IdLabEsterno = rm.IdLabEsterno
              WHERE pr.IdCampione = ca.IdCampione
              FOR XML PATH(''), TYPE
            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS LaboratoriEsterni
        FROM Lotto lo
        INNER JOIN Campione ca   ON ca.IdLotto = lo.IdLotto
        INNER JOIN commessa cm   ON cm.IdCommessa = lo.IdCommessa
        LEFT  JOIN Certificato ce ON ce.IdCertificato = ca.IdCertificato
        WHERE TRY_CONVERT(int, lo.CodLotto) = @CodLotto
          AND TRY_CONVERT(int, ca.CodCampione) = @CodCampione
        ORDER BY lo.DataRic DESC
        """;

    public async Task<IdentificativiCampione?> IdentificativiAsync(
        int codiceLotto, int codiceCampione, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<IdentificativiCampione>(
            new CommandDefinition(SqlIdentificativi,
                new { CodLotto = codiceLotto, CodCampione = codiceCampione },
                cancellationToken: ct));
    }
}
