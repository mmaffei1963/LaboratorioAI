using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryCommesse" />
public class RepositoryCommesse : IRepositoryCommesse
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryCommesse(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<VoceAnagrafica>> TipiCommessaAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT IdTipoCommessa AS Id, TipoCommessa AS Nome
            FROM TipoCommessa
            WHERE TipoCommessa IS NOT NULL
            ORDER BY IdTipoCommessa
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // RICERCA
    // ═══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<Commessa>> CercaAsync(
        CriteriRicercaCommesse criteri, CancellationToken ct = default)
    {
        var tipo = criteri.IdTipoCommessa ?? CostantiArcLab.TipoCommessaLaboratorio;
        var diLaboratorio = tipo == CostantiArcLab.TipoCommessaLaboratorio;

        var condizioni = new List<string> { "cm.IdTipoCommessa = @IdTipo" };
        var p = new DynamicParameters();
        p.Add("IdTipo", tipo);
        p.Add("Limite", criteri.Limite);

        // Le commesse di laboratorio si considerano attive quando non hanno
        // data di scadenza incarico. Per le altre tipologie serve anche che
        // la commessa sia stata avviata, salvo richiesta di includere le concluse.
        if (diLaboratorio)
        {
            condizioni.Add("cm.DataScadenzaInc IS NULL");
        }
        else if (!criteri.IncludiConcluse)
        {
            condizioni.Add("cm.DataCommessa IS NOT NULL");
            condizioni.Add("cm.DataScadenzaInc IS NULL");
        }

        void Aggiungi(string condizione, string nome, object? valore)
        {
            if (valore is null) return;
            if (valore is string s && string.IsNullOrWhiteSpace(s)) return;
            condizioni.Add(condizione);
            p.Add(nome, valore);
        }

        Aggiungi("i.IDCliente = @IdCliente",           "IdCliente",      criteri.IdCliente);
        Aggiungi("i.IDInsediamento = @IdInsediamento", "IdInsediamento", criteri.IdInsediamento);
        Aggiungi("cm.RespCommessa LIKE @Responsabile", "Responsabile",   Contiene(criteri.ResponsabileCommessa));
        Aggiungi("cm.DescrIncarico LIKE @Descrizione", "Descrizione",    Contiene(criteri.DescrizioneIncarico));
        Aggiungi("cm.NContratto LIKE @Contratto",      "Contratto",      Contiene(criteri.NumeroContratto));

        if (diLaboratorio)
        {
            Aggiungi("cm.IDControllo = @IdControllo",  "IdControllo",   criteri.IdControllo);
            Aggiungi("co.Controllo LIKE @NomeControllo", "NomeControllo", Contiene(criteri.NomeControllo));
        }

        var sql = $"""
            SELECT TOP (@Limite)
                cm.IdCommessa,
                cm.IdTipoCommessa,
                tc.TipoCommessa,
                cl.RagioneSociale,
                i.NomeInsed        AS NomeInsediamento,
                cm.DescrIncarico   AS DescrizioneIncarico,
                cm.IDControllo     AS IdControllo,
                co.Controllo,
                cm.PrezzoControllo,
                cm.DataCommessa,
                cm.NContratto      AS NumeroContratto,
                cm.Durata,
                cm.RespCommessa    AS ResponsabileCommessa
            FROM commessa cm
            LEFT  JOIN TipoCommessa tc ON tc.IdTipoCommessa = cm.IdTipoCommessa
            LEFT  JOIN Controllo co    ON co.IDControllo = cm.IDControllo
            INNER JOIN Insediamento i  ON i.IDInsediamento = cm.IDInsediamento
            LEFT  JOIN Clienti cl
                   ON CAST(cl.CodContabilita AS VARCHAR(50)) = CAST(cm.Cod_Cliente AS VARCHAR(50))
            WHERE {string.Join("\n              AND ", condizioni)}
            ORDER BY cl.RagioneSociale, i.NomeInsed, cm.DataCommessa DESC
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<Commessa>(
            new CommandDefinition(sql, p, cancellationToken: ct))).ToList();

        static string? Contiene(string? valore) =>
            string.IsNullOrWhiteSpace(valore) ? null : $"%{valore.Trim()}%";
    }

    // ═══════════════════════════════════════════════════════════════
    // ESPLOSIONE
    // ═══════════════════════════════════════════════════════════════

    private static readonly string SqlCampioni = $"""
        SELECT
            ca.IdCampione,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            lo.DataRic        AS DataRicezione,
            lo.DataPrelievo,
            ca.DenomCampione  AS DenominazioneCampione,
            mx.Matrice,
            ca.LuogoPrelievo,
            lo.BuonoPrelievo  AS VerbalePrelievo,
            NULLIF(ca.IdCertificato, 0) AS IdCertificato,
            DATEDIFF(day, lo.DataRic, GETDATE()) AS GiorniInLaboratorio,
            (SELECT COUNT(*) FROM prove pr
              WHERE pr.IdCampione = ca.IdCampione
                AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}) AS NumeroProve
        FROM Lotto lo
        INNER JOIN Campione ca ON ca.IdLotto = lo.IdLotto
        LEFT  JOIN matrice mx  ON mx.IdMatrice = ca.IdMatrice
        WHERE lo.IdCommessa = @IdCommessa
        ORDER BY lo.DataRic DESC, lo.CodLotto DESC, ca.CodCampione
        """;

    public async Task<IReadOnlyList<CampioneDiCommessa>> CampioniAsync(
        int idCommessa, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<CampioneDiCommessa>(
            new CommandDefinition(SqlCampioni,
                new { IdCommessa = idCommessa }, cancellationToken: ct))).ToList();
    }

    /// <remarks>
    /// Il responsabile della prestazione è indicato tramite la mansione,
    /// che a sua volta rimanda alla persona in organico.
    /// La scadenza si ottiene sommando alla data di inizio la durata
    /// prevista dalla commessa.
    /// </remarks>
    private const string SqlPrestazioni = """
        SELECT
            pr.DescPres        AS Descrizione,
            pr.DataInizioPres  AS DataInizio,
            pr.DataFinePres    AS DataFine,
            CASE
                WHEN pr.DataInizioPres IS NOT NULL AND cm.Durata IS NOT NULL
                THEN DATEADD(day, cm.Durata, pr.DataInizioPres)
            END AS ScadenzaCalcolata,
            LTRIM(RTRIM(ISNULL(o.CognomeOrg, '') + ' ' + ISNULL(o.NomeOrg, ''))) AS Responsabile,
            CASE WHEN pr.Fattura IS NOT NULL AND pr.Fattura <> '' THEN 1 ELSE 0 END AS Fatturata,
            pr.DataFattura
        FROM Prestazioni pr
        INNER JOIN commessa cm ON cm.IdCommessa = pr.IdCommessa
        LEFT  JOIN mansione ms ON ms.IdMansione = pr.IdResponsabilePrestazione
        LEFT  JOIN Organico o  ON o.IdOrganico = ms.IdOrganico
        WHERE pr.IdCommessa = @IdCommessa
        ORDER BY pr.DataInizioPres
        """;

    public async Task<IReadOnlyList<PrestazioneDiCommessa>> PrestazioniAsync(
        int idCommessa, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<PrestazioneDiCommessa>(
            new CommandDefinition(SqlPrestazioni,
                new { IdCommessa = idCommessa }, cancellationToken: ct))).ToList();
    }
}
