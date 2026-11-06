using Dapper;
using LaboratorioAI.Core.CicloAnalitico;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <summary>
/// Avviso campioni in ritardo (22/10/2026): prove dei campioni non ancora usciti, cioè senza rapporto di prova
/// (IdCertificato vuoto) e non chiusi (AnalisiChiusa), come in «Da Validare». Sola lettura su ArcLab.
/// </summary>
public class RepositoryRitardiCampioni
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;
    public RepositoryRitardiCampioni(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    private static readonly string Sql = $"""
        SELECT ca.IdCampione,
               CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
               cl.RagioneSociale, mx.Matrice, co.Controllo, ca.DenomCampione AS DenominazioneCampione,
               lo.DataRic AS DataRicezione, CAST(ca.RitardoAnalisi AS int) AS RitardoAnalisi,
               p.NomeParametro,
               CAST(CASE WHEN pr.VerificaValore = 1 THEN 1 ELSE 0 END AS bit) AS Verificata,
               rm.IdLabEsterno, le.LabEsterno,
               CAST(ISNULL(pr.EsclusodaRdp, 0) AS bit) AS Esclusa
        FROM Campione ca
        INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
        INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
        LEFT  JOIN Controllo co       ON co.IDControllo = cm.IDControllo
        INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
        INNER JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
        INNER JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
        LEFT  JOIN matrice mx         ON mx.IdMatrice = ca.IdMatrice
        INNER JOIN prove pr           ON pr.IdCampione = ca.IdCampione
                                      AND pr.IdRigaMetodo <> {CostantiArcLab.IdRigaMetodoDaEscludere}
        INNER JOIN rigametodo rm      ON rm.IdRigametodo = pr.IdRigaMetodo
        INNER JOIN Metodo m           ON m.IdMetodo = rm.IdMetodo
        LEFT  JOIN Parametro p        ON p.IdParametro = m.IdParametro
        LEFT  JOIN LabEsterno le      ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE (ca.IdCertificato IS NULL OR ca.IdCertificato = 0)
          AND ISNULL(ca.AnalisiChiusa, 0) = 0
          AND lo.DataRic IS NOT NULL
          AND lo.DataRic <= GETDATE()
        """;

    public async Task<IReadOnlyList<ProvaInAttesa>> ProveInAttesaAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ProvaInAttesa>(new CommandDefinition(Sql, commandTimeout: 120, cancellationToken: ct))).ToList();
    }
}
