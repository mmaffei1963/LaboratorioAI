using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryCertificatiEsterni" />
/// <remarks>
/// SOLA LETTURA su LabAnalisiDati. Aggancia il campione tramite lotto/campione
/// (Campione → Lotto → commessa → ContattoCliente → Insediamento → Clienti), come
/// nelle query esistenti (RepositoryCampioni).
/// </remarks>
public class RepositoryCertificatiEsterni : IRepositoryCertificatiEsterni
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryCertificatiEsterni(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private const string Sql = """
        SELECT TOP 1
            ca.IdCampione,
            YEAR(lo.DataRic)                     AS Anno,
            CAST(lo.CodLotto AS varchar(20))     AS Lotto,
            CAST(ca.CodCampione AS varchar(20))  AS Campione,
            cl.RagioneSociale,
            i.IDInsediamento                     AS IdInsediamento,
            i.NomeInsed                          AS NomeInsediamento,
            ca.DenomProdotto                     AS DenominazioneProdotto,
            mx.Matrice,
            ca.IdMatrice,
            lo.DataRic                           AS DataRicezione
        FROM Campione ca
            INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
            INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
            INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
            LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        WHERE TRY_CAST(lo.CodLotto AS int)    = @Lotto
          AND TRY_CAST(ca.CodCampione AS int) = @Campione
          AND (@Entro IS NULL OR lo.DataRic < @Entro)
        ORDER BY lo.DataRic DESC
        """;

    private const string SqlLab = """
        SELECT DISTINCT le.IdLabEsterno, le.LabEsterno AS Nome
        FROM prove pr
            INNER JOIN rigametodo rm ON rm.IdRigametodo = pr.IdRigaMetodo
            INNER JOIN LabEsterno le ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE pr.IdCampione = @IdCampione
        """;

    public async Task<IReadOnlyList<LabEsternoArcLab>> LabEsterniDelCampioneAsync(int idCampione, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<LabEsternoArcLab>(
            new CommandDefinition(SqlLab, new { IdCampione = idCampione }, commandTimeout: 60, cancellationToken: ct));
        return righe.ToList();
    }

    // Risultato inserito = valore presente oppure prova già verificata (risultati non numerici).
    private static readonly string SqlProve = $"""
        SELECT pr.IdProva, pr.IdCampione,
               p.NomeParametro AS Parametro,
               NULLIF(rm.IdLabEsterno, 0) AS IdLabEsterno,
               le.LabEsterno,
               CAST(CASE WHEN pr.ValoreCalcolato IS NOT NULL OR ISNULL(pr.VerificaValore, 0) = 1 THEN 1 ELSE 0 END AS bit) AS ValoreInserito
        FROM prove pr
            INNER JOIN rigametodo rm ON rm.IdRigametodo = pr.IdRigaMetodo
            INNER JOIN Metodo m      ON m.IdMetodo = rm.IdMetodo
            LEFT  JOIN Parametro p   ON p.IdParametro = m.IdParametro
            LEFT  JOIN LabEsterno le ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE pr.IdCampione IN @Ids
          AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
          AND ISNULL(pr.EsclusodaRdp, 0) = 0
        ORDER BY pr.IdCampione, pr.IdProva
        """;

    public async Task<IReadOnlyList<ProvaCampioneArcLab>> ProveDeiCampioniAsync(IEnumerable<int> idCampioni, CancellationToken ct = default)
    {
        var ids = idCampioni.Where(i => i != 0).Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<ProvaCampioneArcLab>();
        var tutte = new List<ProvaCampioneArcLab>();
        using var cn = await _fabbrica.ApriAsync(ct);
        foreach (var blocco in ids.Chunk(500))
        {
            var righe = await cn.QueryAsync<ProvaCampioneArcLab>(
                new CommandDefinition(SqlProve, new { Ids = blocco }, commandTimeout: 60, cancellationToken: ct));
            tutte.AddRange(righe);
        }
        return tutte;
    }

    public async Task<CampioneArcLab?> CercaCampioneAsync(string lotto, string campione, DateOnly? ricevutoEntro, CancellationToken ct = default)
    {
        if (!int.TryParse(lotto, out var lottoN) || !int.TryParse(campione, out var campioneN))
            return null;

        var p = new DynamicParameters();
        p.Add("Lotto", lottoN);
        p.Add("Campione", campioneN);
        // Limite esclusivo = giorno successivo all'emissione (DataRic può avere l'ora).
        p.Add("Entro", ricevutoEntro?.AddDays(1).ToDateTime(TimeOnly.MinValue), System.Data.DbType.DateTime);

        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<CampioneArcLab>(
            new CommandDefinition(Sql, p, commandTimeout: 60, cancellationToken: ct));
    }
}
