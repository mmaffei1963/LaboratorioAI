using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryCertificati" />
public class RepositoryCertificati : IRepositoryCertificati
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;
    private readonly IRepositoryCampioni _campioni;

    public RepositoryCertificati(
        IFabbricaConnessioniArcLab fabbrica,
        IRepositoryCampioni campioni)
    {
        _fabbrica = fabbrica;
        _campioni = campioni;
    }

    private const string SqlCerca = """
        SELECT TOP (@Limite)
            ce.IdCertificato,
            ca.IdCampione,
            ce.NCertificato   AS NumeroCertificato,
            ce.DataCertificato,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            cl.RagioneSociale,
            ca.DenomCampione  AS DenominazioneCampione,
            mx.Matrice,
            DATEDIFF(day, lo.DataRic, ce.DataCertificato) AS GiorniRisposta
        FROM Certificato ce
        INNER JOIN Campione ca        ON ca.IdCertificato = ce.IdCertificato
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
        INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
        LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        WHERE @Testo IS NULL
           OR ce.NCertificato   LIKE @Filtro
           OR cl.RagioneSociale LIKE @Filtro
           OR ca.DenomCampione  LIKE @Filtro
           OR CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) LIKE @Filtro
        ORDER BY ce.DataCertificato DESC
        """;

    public async Task<IReadOnlyList<RisultatoRicercaCertificato>> CercaAsync(
        string testo, int limite = 100, CancellationToken ct = default)
    {
        var pulito = string.IsNullOrWhiteSpace(testo) ? null : testo.Trim();

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<RisultatoRicercaCertificato>(
            new CommandDefinition(SqlCerca,
                new { Testo = pulito, Filtro = $"%{pulito}%", Limite = limite },
                cancellationToken: ct));

        return righe.ToList();
    }

    private const string SqlIntestazione = """
        SELECT TOP 1
            ce.IdCertificato,
            ca.IdCampione,
            ce.NCertificato   AS NumeroCertificato,
            ce.DataCertificato,
            CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
            cl.RagioneSociale,
            ca.DenomCampione  AS DenominazioneCampione,
            mx.Matrice,
            DATEDIFF(day, lo.DataRic, ce.DataCertificato) AS GiorniRisposta
        FROM Certificato ce
        INNER JOIN Campione ca        ON ca.IdCertificato = ce.IdCertificato
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
        INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
        LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        WHERE ce.IdCertificato = @IdCertificato
        """;

    public async Task<CertificatoCompleto?> CompletoAsync(
        int idCertificato, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);

        var intestazione = await cn.QueryFirstOrDefaultAsync<RisultatoRicercaCertificato>(
            new CommandDefinition(SqlIntestazione,
                new { IdCertificato = idCertificato }, cancellationToken: ct));

        if (intestazione is null) return null;

        // Il dettaglio del campione e le prove sono identici a quelli
        // di un campione qualsiasi: si riusa il repository dei campioni.
        // includiProveEscluse: true → nel certificato compaiono anche i valori
        // di legionella (riga normalmente esclusa dagli elenchi e dai conteggi).
        var campione = await _campioni.CompletoAsync(
            intestazione.IdCampione, ct, includiProveEscluse: true);
        if (campione is null) return null;

        return new CertificatoCompleto
        {
            Certificato = intestazione,
            Campione = campione.Campione,
            Prove = campione.Prove
        };
    }
}
