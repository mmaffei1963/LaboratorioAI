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
          AND (@Anno IS NULL OR YEAR(lo.DataRic) = @Anno)
        ORDER BY lo.DataRic DESC
        """;

    public async Task<CampioneArcLab?> CercaCampioneAsync(string lotto, string campione, int? anno, CancellationToken ct = default)
    {
        if (!int.TryParse(lotto, out var lottoN) || !int.TryParse(campione, out var campioneN))
            return null;

        var p = new DynamicParameters();
        p.Add("Lotto", lottoN);
        p.Add("Campione", campioneN);
        p.Add("Anno", anno);

        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<CampioneArcLab>(
            new CommandDefinition(Sql, p, commandTimeout: 60, cancellationToken: ct));
    }
}
