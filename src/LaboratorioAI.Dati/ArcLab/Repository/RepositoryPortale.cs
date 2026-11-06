using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryPortale" />
public class RepositoryPortale : IRepositoryPortale
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryPortale(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // ═══════════════════════════════════════════════════════════════
    // ACCESSO
    // ═══════════════════════════════════════════════════════════════

    /// <remarks>
    /// La credenziale viene letta e restituita al solo servizio di
    /// autenticazione, che la confronta in memoria e la scarta.
    /// Non deve essere esposta da nessun altro punto del sistema.
    /// </remarks>
    private const string SqlPerAccesso = """
        SELECT TOP 1
            cl.IDCliente        AS IdCliente,
            cl.CodContabilita   AS CodiceContabilita,
            cl.RagioneSociale,
            cl.PIVA             AS Credenziale
        FROM Clienti cl
        WHERE LTRIM(RTRIM(cl.CodContabilita)) = @Codice
          AND cl.RagioneSociale IS NOT NULL
        """;

    public async Task<(ClientePortale? Cliente, string? Credenziale)> CercaPerAccessoAsync(
        string codiceContabilita, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);

        var riga = await cn.QueryFirstOrDefaultAsync<RigaAccesso>(
            new CommandDefinition(SqlPerAccesso,
                new { Codice = codiceContabilita.Trim() }, cancellationToken: ct));

        if (riga is null) return (null, null);

        return (new ClientePortale
        {
            IdCliente = riga.IdCliente,
            CodiceContabilita = riga.CodiceContabilita ?? "",
            RagioneSociale = riga.RagioneSociale ?? ""
        }, riga.Credenziale);
    }

    private class RigaAccesso
    {
        public int IdCliente { get; set; }
        public string? CodiceContabilita { get; set; }
        public string? RagioneSociale { get; set; }
        public string? Credenziale { get; set; }
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> ClientiAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT cl.IDCliente AS Id, cl.RagioneSociale AS Nome
            FROM Clienti cl
            WHERE cl.RagioneSociale IS NOT NULL
              AND EXISTS (SELECT 1 FROM Insediamento i WHERE i.IDCliente = cl.IDCliente)
            ORDER BY cl.RagioneSociale
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // FILTRI
    // ═══════════════════════════════════════════════════════════════

    public async Task<FiltriPortale> FiltriAsync(int? idCliente, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);

        const string sqlInsediamenti = """
            SELECT i.IDInsediamento AS Id, i.NomeInsed AS Nome, i.IDCliente
            FROM Insediamento i
            WHERE i.NomeInsed IS NOT NULL
              AND (@IdCliente IS NULL OR i.IDCliente = @IdCliente)
            ORDER BY i.NomeInsed
            """;

        // Matrici e controlli effettivamente usati dal cliente:
        // proporre l'intero catalogo sarebbe fuorviante
        const string sqlMatrici = """
            SELECT DISTINCT mx.IdMatrice AS Id, mx.Matrice AS Nome
            FROM matrice mx
            INNER JOIN Campione ca        ON ca.IdMatrice = mx.IdMatrice
            INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
            INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            WHERE mx.Matrice IS NOT NULL
              AND (@IdCliente IS NULL OR i.IDCliente = @IdCliente)
            ORDER BY mx.Matrice
            """;

        const string sqlControlli = """
            SELECT DISTINCT co.IDControllo AS Id, co.Controllo AS Nome
            FROM Controllo co
            INNER JOIN commessa cm        ON cm.IDControllo = co.IDControllo
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            WHERE co.Controllo IS NOT NULL
              AND (@IdCliente IS NULL OR i.IDCliente = @IdCliente)
            ORDER BY co.Controllo
            """;

        var parametri = new { IdCliente = idCliente };

        var insediamenti = await cn.QueryAsync<VoceInsediamento>(
            new CommandDefinition(sqlInsediamenti, parametri, cancellationToken: ct));
        var matrici = await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sqlMatrici, parametri, cancellationToken: ct));
        var controlli = await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sqlControlli, parametri, cancellationToken: ct));

        return new FiltriPortale
        {
            Insediamenti = insediamenti.ToList(),
            Matrici = matrici.ToList(),
            Controlli = controlli.ToList()
        };
    }

    // ═══════════════════════════════════════════════════════════════
    // RICERCA CAMPIONI
    // ═══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<CampionePortale>> CercaCampioniAsync(
        int? idCliente, CriteriPortale criteri, CancellationToken ct = default)
    {
        var condizioni = new List<string> { "1 = 1" };
        var p = new DynamicParameters();
        p.Add("Limite", criteri.Limite);

        // Il vincolo sul cliente proviene dalla sessione, mai dalla richiesta
        if (idCliente is not null)
        {
            condizioni.Add("i.IDCliente = @IdCliente");
            p.Add("IdCliente", idCliente);
        }

        void Aggiungi(string condizione, string nome, object? valore)
        {
            if (valore is null) return;
            if (valore is string s && string.IsNullOrWhiteSpace(s)) return;
            condizioni.Add(condizione);
            p.Add(nome, valore);
        }

        Aggiungi("lo.DataRic >= @DataDa", "DataDa", criteri.DataDa?.ToDateTime(TimeOnly.MinValue));
        Aggiungi("lo.DataRic <= @DataA",  "DataA",  criteri.DataA?.ToDateTime(TimeOnly.MaxValue));

        Aggiungi("i.IDInsediamento = @IdInsediamento", "IdInsediamento", criteri.IdInsediamento);
        Aggiungi("ca.IdMatrice = @IdMatrice",          "IdMatrice",      criteri.IdMatrice);
        Aggiungi("cm.IDControllo = @IdControllo",      "IdControllo",    criteri.IdControllo);

        Aggiungi("ca.LuogoPrelievo LIKE @Luogo",   "Luogo",   Contiene(criteri.LuogoPrelievo));
        Aggiungi("ca.PuntoPrelievo LIKE @Punto",   "Punto",   Contiene(criteri.PuntoPrelievo));
        Aggiungi("lo.BuonoPrelievo LIKE @Verbale", "Verbale", Contiene(criteri.VerbalePrelievo));

        if (!string.IsNullOrWhiteSpace(criteri.TestoLibero))
        {
            condizioni.Add("""
                (ca.DenomCampione LIKE @Testo
                 OR ca.LuogoPrelievo LIKE @Testo
                 OR ca.PuntoPrelievo LIKE @Testo)
                """);
            p.Add("Testo", Contiene(criteri.TestoLibero));
        }

        switch (criteri.Stato)
        {
            case StatoCampione.Certificato:
                condizioni.Add("ca.IdCertificato IS NOT NULL AND ca.IdCertificato <> 0");
                break;

            case StatoCampione.InCorso:
                condizioni.Add($"""
                    (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
                    AND NOT (
                        DATEDIFF(day, lo.DataRic, GETDATE()) > {CostantiArcLab.GiorniPerSpeditoOriginale}
                        AND NOT EXISTS (SELECT 1 FROM prove pr2
                                        WHERE pr2.IdCampione = ca.IdCampione
                                          AND pr2.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere})
                    )
                    """);
                break;

            case StatoCampione.SpeditoOriginale:
                condizioni.Add($"""
                    (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
                    AND DATEDIFF(day, lo.DataRic, GETDATE()) > {CostantiArcLab.GiorniPerSpeditoOriginale}
                    AND NOT EXISTS (SELECT 1 FROM prove pr3
                                    WHERE pr3.IdCampione = ca.IdCampione
                                      AND pr3.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere})
                    """);
                break;
        }

        var sql = $"""
            SELECT TOP (@Limite)
                ca.IdCampione,
                CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
                cl.RagioneSociale,
                i.NomeInsed       AS NomeInsediamento,
                ca.DenomCampione  AS DenominazioneCampione,
                mx.Matrice,
                co.Controllo,
                ca.LuogoPrelievo,
                ca.PuntoPrelievo,
                lo.BuonoPrelievo  AS VerbalePrelievo,
                lo.DataRic        AS DataRicezione,
                lo.DataPrelievo,
                NULLIF(ca.IdCertificato, 0) AS IdCertificato,
                DATEDIFF(day, lo.DataRic, GETDATE()) AS GiorniInLaboratorio,
                (SELECT COUNT(*) FROM prove pr
                  WHERE pr.IdCampione = ca.IdCampione
                    AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) AS NumeroProve
            FROM Campione ca
            INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
            INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
            LEFT  JOIN Controllo co       ON co.IDControllo = cm.IDControllo
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
            LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
            WHERE {string.Join("\n              AND ", condizioni)}
            ORDER BY lo.DataRic DESC, lo.CodLotto DESC
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<CampionePortale>(
            new CommandDefinition(sql, p, cancellationToken: ct))).ToList();

        static string? Contiene(string? valore) =>
            string.IsNullOrWhiteSpace(valore) ? null : $"%{valore.Trim()}%";
    }

    // ═══════════════════════════════════════════════════════════════
    // PROVE
    // ═══════════════════════════════════════════════════════════════

    /// <remarks>
    /// Il vincolo sul cliente è nella query stessa: anche indovinando un
    /// identificativo di campione altrui, la richiesta non restituisce nulla.
    /// </remarks>
    private static readonly string SqlProve = $"""
        SELECT
            pr.IdProva,
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
            -- Badge «A» (accreditato MARE.A) solo per le prove eseguite da MARE.A: una prova in subappalto
            -- non è coperta dal nostro accreditamento (Condizioni Generali, Art. 8.2). Corretto l'08/10/2026.
            CASE WHEN rm.IdLabEsterno IS NULL THEN ISNULL(m.Accreditato, 0) ELSE CAST(0 AS bit) END AS Accreditato,
            pr.Note
        FROM prove pr
        INNER JOIN rigametodo rm      ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m           ON m.IdMetodo = rm.IdMetodo
        INNER JOIN Campione ca        ON ca.IdCampione = pr.IdCampione
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
        LEFT  JOIN Parametro p        ON p.IdParametro = m.IdParametro
        LEFT  JOIN Unitadimisura u    ON u.IdUnita = rm.IdUnita
        LEFT  JOIN LabEsterno le      ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE pr.IdCampione = @IdCampione
          AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
          AND (@IdCliente IS NULL OR i.IDCliente = @IdCliente)
        ORDER BY rm.DenominazioneRiga
        """;

    public async Task<IReadOnlyList<ProvaCampione>> ProveAsync(
        int idCampione, int? idCliente, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ProvaCampione>(
            new CommandDefinition(SqlProve,
                new { IdCampione = idCampione, IdCliente = idCliente },
                cancellationToken: ct))).ToList();
    }
}
