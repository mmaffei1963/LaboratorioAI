using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryLaboratoriEsterni" />
public class RepositoryLaboratoriEsterni : IRepositoryLaboratoriEsterni
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryLaboratoriEsterni(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<VoceAnagrafica>> LaboratoriAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT le.IdLabEsterno AS Id, le.LabEsterno AS Nome
            FROM LabEsterno le
            WHERE le.LabEsterno IS NOT NULL
              AND EXISTS (SELECT 1 FROM rigametodo rm WHERE rm.IdLabEsterno = le.IdLabEsterno)
            ORDER BY le.LabEsterno
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceLaboratorioEsterno>> LaboratoriConFraseAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT le.IdLabEsterno AS Id, le.LabEsterno AS Nome, le.Frase
            FROM LabEsterno le
            WHERE le.LabEsterno IS NOT NULL
            ORDER BY le.LabEsterno
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceLaboratorioEsterno>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // INVIO CAMPIONI (06/11/2026) — dalla query Access «campioni per lab esterno»
    // ═══════════════════════════════════════════════════════════════

    // Data analisi = data ricevimento + ritardo analisi (come in Access). Escluse: campioni certificati, prove esitate
    // (valore inserito o verificata), prove escluse dal RdP e la riga di servizio (annullate).
    private static readonly string SqlProveDaInviare = $"""
        SELECT x.* FROM (
            SELECT DISTINCT ca.IdCampione, pr.IdProva,
                   CONCAT(LTRIM(RTRIM(i.NomeInsed)), ' - ', LTRIM(RTRIM(i.IndirizzoInsed)), ' ', LTRIM(RTRIM(i.[CittàInsed])), ' ',
                          LTRIM(RTRIM(i.CapInsed)), ' ( ', LTRIM(RTRIM(i.ProvinciaInsed)), ' )') AS ClienteInsediamento,
                   CONCAT(lo.CodLotto, '/', ca.CodCampione)            AS NumeroCampione,
                   CONVERT(nvarchar(500), ca.DenomCampione)            AS DenominazioneCampione,
                   CONVERT(nvarchar(500), rm.DenominazioneRiga)        AS Prova,
                   CONVERT(nvarchar(500), m.RifBiblio)                 AS RifBiblio,
                   CONVERT(nvarchar(500), m.RifMetodo)                 AS RifMetodo,
                   DATEADD(day, CAST(ISNULL(ca.RitardoAnalisi, 0) AS int), CAST(lo.DataRic AS date)) AS DataAnalisi,
                   CONVERT(nvarchar(50), ca.TempPrelievo)              AS TemperaturaPrelievo,
                   lo.DataRic                                          AS DataRicezione,
                   CAST(lo.OraRic AS time)                             AS OraRicezione,
                   CONVERT(nvarchar(300), le.LabEsterno)               AS LabEsterno
            FROM Campione ca
            INNER JOIN Lotto lo             ON lo.IdLotto = ca.IdLotto
            INNER JOIN prove pr             ON pr.IdCampione = ca.IdCampione
                                            AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
            INNER JOIN rigametodo rm        ON rm.IdRigametodo = pr.IdRigaMetodo
            INNER JOIN metodo m             ON m.IdMetodo = rm.IdMetodo
            INNER JOIN LabEsterno le        ON le.IdLabEsterno = rm.IdLabEsterno
            LEFT  JOIN commessa cm          ON cm.IdCommessa = lo.IdCommessa
            LEFT  JOIN ContattoCliente cc   ON cc.IDContatto = cm.IDContatto
            LEFT  JOIN Insediamento i       ON i.IDInsediamento = cc.IDInsediamento
            WHERE rm.IdLabEsterno = @IdLab
              AND (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
              AND ISNULL(pr.EsclusodaRdp, 0) = 0
              AND pr.ValoreCalcolato IS NULL
              AND ISNULL(pr.VerificaValore, 0) = 0
              AND lo.DataRic IS NOT NULL
        ) x
        WHERE x.DataAnalisi >= @Dal AND x.DataAnalisi <= @Al
        ORDER BY x.DataAnalisi, x.NumeroCampione, x.Prova
        """;

    public async Task<IReadOnlyList<ProvaDaInviare>> ProveDaInviareAsync(int idLabEsterno, DateTime dal, DateTime al, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ProvaDaInviare>(new CommandDefinition(SqlProveDaInviare,
            new { IdLab = idLabEsterno, Dal = dal.Date, Al = al.Date }, commandTimeout: 90, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // INTERROGAZIONE
    // ═══════════════════════════════════════════════════════════════

    public async Task<EsitoLaboratoriEsterni> InterrogaAsync(
        CriteriLaboratoriEsterni criteri, CancellationToken ct = default)
    {
        var (dove, p) = ComponiFiltri(criteri);

        using var cn = await _fabbrica.ApriAsync(ct);

        var prove = (await cn.QueryAsync<ProvaEsterna>(
            new CommandDefinition(SqlProve(dove), p, cancellationToken: ct))).ToList();

        var riepilogo = (await cn.QueryAsync<RiepilogoMetodoEsterno>(
            new CommandDefinition(SqlRiepilogo(dove), p, cancellationToken: ct))).ToList();

        return new EsitoLaboratoriEsterni
        {
            Prove = prove,
            Riepilogo = riepilogo,
            Troncato = prove.Count >= criteri.Limite
        };
    }

    /// <remarks>
    /// La data di analisi è calcolata sommando alla data di ricezione il
    /// ritardo previsto per il campione: rappresenta il giorno in cui il
    /// campione deve essere consegnato al laboratorio esterno, non quello
    /// in cui l'analisi viene effettivamente avviata.
    /// </remarks>
    private static string SqlProve(string dove) => $"""
        WITH {CostantiArcLab.CteEstPortale}
        SELECT TOP (@Limite)
            ca.IdCampione,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            cl.RagioneSociale,
            i.NomeInsed       AS NomeInsediamento,
            lo.DataRic        AS DataRicezione,
            DATEADD(day, ISNULL(ca.RitardoAnalisi, 0), lo.DataRic) AS DataAnalisi,
            lo.DataPrelievo,
            ca.DenomCampione  AS DenominazioneCampione,
            mx.Matrice,
            rm.DenominazioneRiga,
            m.RifBiblio       AS RiferimentoNormativo,
            le.LabEsterno     AS LaboratorioEsterno,
            -- Accreditato presso il laboratorio che ha eseguito la prova (rm.IdLabEsterno), non presso
            -- un qualsiasi laboratorio a cui è affidato lo stesso metodo (corretto l'08/10/2026).
            CASE WHEN EXISTS (SELECT 1 FROM labEstMetodo lem
                              WHERE lem.IdMetodo = m.IdMetodo
                                AND lem.IdLabEsterno = rm.IdLabEsterno
                                AND lem.AccrLabEsterno = 1) THEN 1 ELSE 0 END AS AccreditatoEsterno,
            NULLIF(ca.IdCertificato, 0) AS IdCertificato,
            CASE WHEN ep.Anno IS NOT NULL THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS EsternoPresente
        {DalleTabelle()}
        WHERE {dove}
        ORDER BY DATEADD(day, ISNULL(ca.RitardoAnalisi, 0), lo.DataRic), lo.CodLotto
        """;

    private static string SqlRiepilogo(string dove) => $"""
        WITH {CostantiArcLab.CteEstPortale}
        SELECT
            le.LabEsterno        AS LaboratorioEsterno,
            mx.Matrice,
            rm.DenominazioneRiga,
            m.RifBiblio          AS RiferimentoNormativo,
            COUNT(*)             AS NumeroProve,
            SUM(CASE WHEN ca.IdCertificato IS NOT NULL AND ca.IdCertificato <> 0
                     THEN 1 ELSE 0 END) AS NumeroCertificate
        {DalleTabelle()}
        WHERE {dove}
        GROUP BY le.LabEsterno, mx.Matrice, rm.DenominazioneRiga, m.RifBiblio
        ORDER BY le.LabEsterno, COUNT(*) DESC
        """;

    private static string DalleTabelle() => """
        FROM prove pr
        INNER JOIN rigametodo rm      ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m           ON m.IdMetodo = rm.IdMetodo
        INNER JOIN LabEsterno le      ON le.IdLabEsterno = rm.IdLabEsterno
        INNER JOIN Campione ca        ON ca.IdCampione = pr.IdCampione
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
        INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
        LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        """ + "\n        " + CostantiArcLab.JoinEstPortale;

    private static (string Dove, DynamicParameters Parametri) ComponiFiltri(
        CriteriLaboratoriEsterni c)
    {
        var condizioni = new List<string>
        {
            "rm.IdLabEsterno IS NOT NULL",
            "DATEADD(day, ISNULL(ca.RitardoAnalisi, 0), lo.DataRic) BETWEEN @DataDa AND @DataA"
        };

        var p = new DynamicParameters();
        p.Add("DataDa", c.DataDa.ToDateTime(TimeOnly.MinValue));
        p.Add("DataA",  c.DataA.ToDateTime(TimeOnly.MaxValue));
        p.Add("Limite", c.Limite);

        void Aggiungi(string condizione, string nome, object? valore)
        {
            if (valore is null) return;
            condizioni.Add(condizione);
            p.Add(nome, valore);
        }

        Aggiungi("rm.IdLabEsterno = @IdLab",           "IdLab",          c.IdLaboratorioEsterno);
        Aggiungi("ca.IdMatrice = @IdMatrice",          "IdMatrice",      c.IdMatrice);
        Aggiungi("i.IDCliente = @IdCliente",           "IdCliente",      c.IdCliente);
        Aggiungi("i.IDInsediamento = @IdInsediamento", "IdInsediamento", c.IdInsediamento);

        // Numero lotto (o "lotto/campione"): CodLotto/CodCampione sono testuali,
        // TRY_CONVERT evita l'errore di conversione sui valori non numerici.
        if (!string.IsNullOrWhiteSpace(c.NumeroLotto))
        {
            var parti = c.NumeroLotto.Split(new[] { '/', '-' },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (int.TryParse(parti[0], out var lotto))
            {
                condizioni.Add("TRY_CONVERT(int, lo.CodLotto) = @Lotto");
                p.Add("Lotto", lotto);

                if (parti.Length > 1 && int.TryParse(parti[1], out var campione))
                {
                    condizioni.Add("TRY_CONVERT(int, ca.CodCampione) = @Campione");
                    p.Add("Campione", campione);
                }
            }
        }

        // "Solo spediti in originale": senza nostro certificato MA con certificato esterno nel portale.
        if (c.SoloNonCertificati)
            condizioni.Add("(ca.IdCertificato IS NULL OR ca.IdCertificato = 0) AND ep.Anno IS NOT NULL");

        return (string.Join("\n          AND ", condizioni), p);
    }
}
