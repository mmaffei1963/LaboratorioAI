using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryConsulenze" />
/// <remarks>Solo SELECT su ArcLab: nessuna scrittura.</remarks>
public class RepositoryConsulenze : IRepositoryConsulenze
{
    private const int TipoCommessaConsulenza = 5;

    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryConsulenze(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<InsediamentoRapportino>> InsediamentiAsync(int idCliente, CancellationToken ct = default)
    {
        // NB: la colonna città è [CittàInsed] (con accento).
        const string sql = """
            SELECT i.IDInsediamento AS IdInsediamento, i.NomeInsed AS Nome,
                   i.IndirizzoInsed AS Via, i.CapInsed AS Cap, i.[CittàInsed] AS Citta,
                   i.ProvinciaInsed AS Provincia, i.Email AS Email
            FROM Insediamento i
            WHERE i.IDCliente = @IdCliente AND i.NomeInsed IS NOT NULL
            ORDER BY i.NomeInsed
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync(new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));
        return righe.Select(r =>
        {
            var parti = new[]
            {
                (string?)r.Via,
                string.Join(" ", new[] { (string?)r.Cap, (string?)r.Citta }.Where(s => !string.IsNullOrWhiteSpace(s))),
                string.IsNullOrWhiteSpace((string?)r.Provincia) ? null : $"({((string)r.Provincia).Trim()})"
            }.Where(s => !string.IsNullOrWhiteSpace(s));
            var indirizzo = string.Join(" ", parti).Trim();
            return new InsediamentoRapportino
            {
                IdInsediamento = (int)r.IdInsediamento,
                Nome = ((string?)r.Nome ?? "").Trim(),
                Indirizzo = indirizzo.Length > 0 ? indirizzo : null,
                Email = string.IsNullOrWhiteSpace((string?)r.Email) ? null : ((string)r.Email).Trim(),
                Via = ((string?)r.Via)?.Trim(),
                Cap = ((string?)r.Cap)?.Trim(),
                Citta = ((string?)r.Citta)?.Trim(),
                Provincia = ((string?)r.Provincia)?.Trim()
            };
        }).ToList();
    }

    private const string SqlCommesse = """
        SELECT cm.IdCommessa, cm.IDInsediamento AS IdInsediamento, i.NomeInsed AS NomeInsediamento,
               cm.DescrIncarico, CAST(cm.PrezzoControllo AS decimal(10,2)) AS PrezzoControllo,
               cm.DataCommessa, cm.DataScadenzaInc, CAST(cm.NContratto AS NVARCHAR(100)) AS NContratto,
               (SELECT TOP 1 ti.ID_Tipo_Incarico FROM Tipi_Incarico ti
                 WHERE ti.DescrIncarico = cm.DescrIncarico ORDER BY ti.ID_Tipo_Incarico) AS IdTipoIncarico
        FROM commessa cm
        INNER JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
        """;

    public async Task<IReadOnlyList<CommessaConsulenza>> CommesseConsulenzaAsync(int idCliente, CancellationToken ct = default)
    {
        var sql = SqlCommesse + """

            WHERE cm.IdTipoCommessa = @Tipo AND i.IDCliente = @IdCliente
            ORDER BY CASE WHEN cm.DataScadenzaInc IS NULL THEN 0 ELSE 1 END, cm.DataCommessa DESC, cm.IdCommessa DESC
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<CommessaConsulenza>(new CommandDefinition(sql,
            new { Tipo = TipoCommessaConsulenza, IdCliente = idCliente }, cancellationToken: ct))).ToList();
    }

    public async Task<CommessaConsulenza?> CommessaAsync(int idCommessa, CancellationToken ct = default)
    {
        var sql = SqlCommesse + """

            WHERE cm.IdTipoCommessa = @Tipo AND cm.IdCommessa = @IdCommessa
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<CommessaConsulenza>(new CommandDefinition(sql,
            new { Tipo = TipoCommessaConsulenza, IdCommessa = idCommessa }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ConsulenteArcLab>> ConsulentiAsync(CancellationToken ct = default)
    {
        // "In servizio" = Organico.FineProf non impostata (come in RepositoryAccettazione).
        const string sql = """
            SELECT o.IdOrganico, LTRIM(RTRIM(o.CognomeOrg)) AS CognomeOrg, LTRIM(RTRIM(o.NomeOrg)) AS NomeOrg,
                   o.commessa AS Commessa
            FROM Organico o
            WHERE o.FineProf IS NULL AND (o.CognomeOrg IS NOT NULL OR o.NomeOrg IS NOT NULL)
            ORDER BY o.CognomeOrg, o.NomeOrg
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ConsulenteArcLab>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<TipoIncaricoArcLab>> TipiIncaricoAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT ti.ID_Tipo_Incarico AS IdTipoIncarico, ti.IDTipoCommessa AS IdTipoCommessa,
                   LTRIM(RTRIM(ti.DescrIncarico)) AS Descrizione
            FROM Tipi_Incarico ti
            WHERE ti.DescrIncarico IS NOT NULL AND LTRIM(RTRIM(ti.DescrIncarico)) <> ''
            ORDER BY ti.ID_Tipo_Incarico
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<TipoIncaricoArcLab>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<ContattoArcLab>> ContattiAsync(int idInsediamento, CancellationToken ct = default)
    {
        const string sql = """
            SELECT c.IDContatto AS IdContatto, LTRIM(RTRIM(c.NomeContatto)) AS Nome, LTRIM(RTRIM(c.CognomeContatto)) AS Cognome,
                   c.MansioneContatto AS Mansione, c.TelContatto AS Telefono, c.CellulareContatto AS Cellulare, c.Email AS Email
            FROM ContattoCliente c
            WHERE c.IDInsediamento = @IdInsediamento
            ORDER BY c.CognomeContatto, c.NomeContatto
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ContattoArcLab>(new CommandDefinition(sql, new { IdInsediamento = idInsediamento }, cancellationToken: ct))).ToList();
    }

    public async Task<AnagraficaClienteArcLab?> AnagraficaClienteAsync(int idCliente, CancellationToken ct = default)
    {
        // NB: colonna città con accento: [CittàFatt].
        const string sql = """
            SELECT c.IDCliente AS IdCliente, LTRIM(RTRIM(c.CodiceCliente)) AS CodiceCliente, c.RagioneSociale,
                   c.PIVA AS PartitaIva, c.CodFisc AS CodiceFiscale, c.IndirizzoFatt AS Indirizzo, c.CapFatt AS Cap,
                   c.[CittàFatt] AS Citta, c.ProvinciaFatt AS Provincia, c.email AS Email
            FROM Clienti c
            WHERE c.IDCliente = @IdCliente
            """;
        // Codici cliente AZW usati nelle commesse delle sue sedi: sono quelli che collegano ArcLab ad AZW.
        const string sqlCodici = """
            SELECT LTRIM(RTRIM(co.Cod_Cliente)) AS Codice
            FROM commessa co
            JOIN Insediamento i ON i.IDInsediamento = co.IDInsediamento
            WHERE i.IDCliente = @IdCliente AND co.Cod_Cliente IS NOT NULL AND LTRIM(RTRIM(co.Cod_Cliente)) <> ''
            GROUP BY LTRIM(RTRIM(co.Cod_Cliente))
            ORDER BY MAX(co.DataCommessa) DESC
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        var c = await cn.QueryFirstOrDefaultAsync<AnagraficaClienteArcLab>(new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));
        if (c is null) return null;
        c.CodiciCommesse = (await cn.QueryAsync<string>(new CommandDefinition(sqlCodici, new { IdCliente = idCliente }, cancellationToken: ct))).ToList();
        return c;
    }
}
