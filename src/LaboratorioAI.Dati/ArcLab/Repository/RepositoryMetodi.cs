using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryMetodi" />
public class RepositoryMetodi : IRepositoryMetodi
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryMetodi(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // ═══════════════════════════════════════════════════════════════
    // ELENCHI
    // ═══════════════════════════════════════════════════════════════

    /// <remarks>
    /// La sottoquery su ProceduraMetodo è distinta perché un metodo può
    /// avere più procedure: senza DISTINCT il join moltiplicherebbe le righe.
    /// </remarks>
    // Una riga per metodo (OUTER APPLY TOP 1): evita l'esplosione di righe dei LEFT JOIN
    // su rigametodo/labEstMetodo, che rendeva la query lentissima con conseguente timeout.
    private const string SqlMetodiAttivi = """
        SELECT
            m.IdMetodo,
            m.RifBiblio          AS RiferimentoNormativo,
            p.NomeParametro,
            rm.DenominazioneRiga,
            m.Acronimo,
            m.RifMetodo,
            m.DettagliMetodo,
            rm.UnitaMisura,
            ISNULL(m.Accreditato, 0)      AS Accreditato,
            le.LabEsterno        AS LaboratorioEsterno,
            ISNULL(lem.AccrLabEsterno, 0) AS AccreditatoEsterno
        FROM Metodo m
        LEFT JOIN Parametro p ON p.IdParametro = m.IdParametro
        OUTER APPLY (
            SELECT TOP 1 r.DenominazioneRiga, u.UnitaMisura
            FROM rigametodo r
            LEFT JOIN Unitadimisura u ON u.IdUnita = r.IdUnita
            WHERE r.IdMetodo = m.IdMetodo
            ORDER BY r.IdRigametodo
        ) rm
        OUTER APPLY (
            SELECT TOP 1 x.AccrLabEsterno, x.IdLabEsterno
            FROM labEstMetodo x
            WHERE x.IdMetodo = m.IdMetodo
            ORDER BY x.IdLabEstMetodo   -- sempre lo stesso laboratorio se il metodo ne ha più di uno
        ) lem
        LEFT JOIN LabEsterno le ON le.IdLabEsterno = lem.IdLabEsterno
        WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
          AND (m.RifBiblio IS NOT NULL OR p.NomeParametro IS NOT NULL)
        ORDER BY m.RifBiblio
        """;

    public async Task<IReadOnlyList<MetodoRicercabile>> MetodiAttiviAsync(CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<MetodoRicercabile>(
            new CommandDefinition(SqlMetodiAttivi, commandTimeout: 120, cancellationToken: ct));

        // Un metodo può comparire più volte per via delle diverse righe metodo:
        // si tiene la prima occorrenza, che è quella con la denominazione più comune.
        return righe
            .GroupBy(m => m.IdMetodo)
            .Select(g => g.First())
            .ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> ControlliAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT IDControllo AS Id, Controllo AS Nome
            FROM Controllo
            WHERE Controllo IS NOT NULL AND Attivo = 1
            ORDER BY Controllo
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // METODI → CONTROLLI
    // ═══════════════════════════════════════════════════════════════

    public async Task<IReadOnlyList<ControlloTrovato>> ControlliPerMetodiAsync(
        IReadOnlyList<int> idMetodi,
        LogicaRicercaControlli logica,
        CancellationToken ct = default)
    {
        if (idMetodi.Count == 0) return [];

        // La clausola HAVING traduce la logica richiesta:
        //   Tutti       → il controllo contiene tutti i metodi indicati
        //   Esattamente → li contiene tutti e non ne ha altri
        //   AlmenoUno   → nessun vincolo oltre alla presenza nell'IN
        var having = logica switch
        {
            LogicaRicercaControlli.Tutti =>
                $"HAVING COUNT(DISTINCT rm.IdMetodo) = {idMetodi.Count}",

            LogicaRicercaControlli.Esattamente =>
                $"""
                 HAVING COUNT(DISTINCT rm.IdMetodo) = {idMetodi.Count}
                    AND (SELECT COUNT(DISTINCT rm3.IdMetodo)
                           FROM rigametodo rm3
                          WHERE rm3.IdControllo = co.IDControllo) = {idMetodi.Count}
                 """,

            _ => ""
        };

        var sql = $"""
            SELECT
                co.IDControllo AS IdControllo,
                co.Controllo,
                ISNULL(co.Attivo, 0) AS Attivo,
                (SELECT COUNT(DISTINCT rm2.IdMetodo)
                   FROM rigametodo rm2
                  WHERE rm2.IdControllo = co.IDControllo) AS NumeroMetodi
            FROM Controllo co
            INNER JOIN rigametodo rm ON rm.IdControllo = co.IDControllo
            WHERE rm.IdMetodo IN @IdMetodi
            GROUP BY co.IDControllo, co.Controllo, co.Attivo
            {having}
            ORDER BY co.Controllo
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ControlloTrovato>(
            new CommandDefinition(sql, new { IdMetodi = idMetodi }, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // CONTROLLO → METODI
    // ═══════════════════════════════════════════════════════════════

    private const string SqlMetodiDelControllo = """
        SELECT
            co.IDControllo       AS IdControllo,
            co.Controllo,
            m.IdMetodo,
            rm.DenominazioneRiga,
            m.RifBiblio          AS RiferimentoNormativo,
            p.NomeParametro,
            ISNULL(m.Accreditato, 0)      AS Accreditato,
            le.LabEsterno        AS LaboratorioEsterno,
            -- Laboratorio della riga del controllo (rm.IdLabEsterno) e accreditamento per quella coppia
            -- metodo + laboratorio (corretto l'08/10/2026: prima qualsiasi laboratorio del metodo).
            CASE WHEN EXISTS (SELECT 1 FROM labEstMetodo lem
                              WHERE lem.IdMetodo = m.IdMetodo
                                AND lem.IdLabEsterno = rm.IdLabEsterno
                                AND lem.AccrLabEsterno = 1) THEN 1 ELSE 0 END AS AccreditatoEsterno,
            CASE WHEN pm.IdMetodi IS NOT NULL THEN 1 ELSE 0 END AS Attivo
        FROM rigametodo rm
        INNER JOIN Controllo co    ON co.IDControllo = rm.IdControllo
        INNER JOIN Metodo m        ON m.IdMetodo = rm.IdMetodo
        LEFT  JOIN Parametro p     ON p.IdParametro = m.IdParametro
        LEFT  JOIN LabEsterno le   ON le.IdLabEsterno = rm.IdLabEsterno
        LEFT  JOIN (
            SELECT DISTINCT IdMetodi
            FROM ProceduraMetodo
            WHERE Attivo = 1
        ) pm ON pm.IdMetodi = m.IdMetodo
        WHERE rm.IdControllo = @IdControllo
        ORDER BY rm.DenominazioneRiga
        """;

    public async Task<IReadOnlyList<MetodoDelControllo>> MetodiDelControlloAsync(
        int idControllo, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<MetodoDelControllo>(
            new CommandDefinition(SqlMetodiDelControllo,
                new { IdControllo = idControllo }, cancellationToken: ct))).ToList();
    }

    // ═══════════════════════════════════════════════════════════════
    // PREZZI
    // ═══════════════════════════════════════════════════════════════

    /// <remarks>
    /// Il collegamento fra commessa e cliente passa da Cod_Cliente e
    /// CodContabilita, che possono avere tipi diversi: il CAST evita
    /// errori di conversione implicita.
    ///
    /// Sono considerate solo le commesse senza data di scadenza incarico,
    /// cioè quelle attualmente in vigore, con un cliente effettivamente
    /// valorizzato (alcune commesse molto datate hanno Cod_Cliente NULL —
    /// prassi diversa in anni passati — e, restando senza scadenza,
    /// resterebbero altrimenti per sempre nel calcolo), con quel cliente
    /// ancora attivo (Clienti.Attivo = 1) e con l'insediamento della
    /// commessa non segnato come non più in uso (Insediamento.NonInUso):
    /// un cliente cessato o un insediamento dismesso non devono influenzare
    /// il prezzo minimo/massimo mostrato come attuale.
    ///
    /// Esclusa anche la commessa con CodContabilita "MIRKO1": è MARE.A
    /// stessa (cliente interno/di test), non un cliente reale.
    /// </remarks>
    private const string SqlPrezzi = """
        WITH Attive AS (
            SELECT cm.PrezzoControllo, cm.Cod_Cliente
            FROM commessa cm
            LEFT JOIN Insediamento ins ON ins.IDInsediamento = cm.IDInsediamento
            WHERE cm.IDControllo = @IdControllo
              AND cm.DataScadenzaInc IS NULL
              AND cm.PrezzoControllo IS NOT NULL
              AND cm.PrezzoControllo > 0
              AND cm.Cod_Cliente IS NOT NULL
              AND ISNULL(ins.NonInUso, 0) = 0
              AND CAST(cm.Cod_Cliente AS VARCHAR(50)) <> 'MIRKO1'
              AND EXISTS (
                    SELECT 1 FROM Clienti cl
                    WHERE CAST(cl.CodContabilita AS VARCHAR(50)) = CAST(cm.Cod_Cliente AS VARCHAR(50))
                      AND cl.Attivo = 1
              )
        ),
        Estremi AS (
            SELECT MIN(PrezzoControllo) AS Minimo,
                   MAX(PrezzoControllo) AS Massimo
            FROM Attive
        )
        SELECT
            e.Minimo  AS PrezzoMinimo,
            e.Massimo AS PrezzoMassimo,
            (SELECT COUNT(DISTINCT a.Cod_Cliente) FROM Attive a
              WHERE a.PrezzoControllo = e.Minimo)  AS ClientiAlMinimo,
            (SELECT COUNT(DISTINCT a.Cod_Cliente) FROM Attive a
              WHERE a.PrezzoControllo = e.Massimo) AS ClientiAlMassimo,
            (SELECT COUNT(DISTINCT a.Cod_Cliente) FROM Attive a) AS ClientiTotali,
            (SELECT TOP 1 cl.RagioneSociale
               FROM Attive a
               LEFT JOIN Clienti cl
                 ON CAST(cl.CodContabilita AS VARCHAR(50)) = CAST(a.Cod_Cliente AS VARCHAR(50))
              WHERE a.PrezzoControllo = e.Minimo
              ORDER BY cl.RagioneSociale) AS ClientePrezzoMinimo,
            (SELECT TOP 1 cl.RagioneSociale
               FROM Attive a
               LEFT JOIN Clienti cl
                 ON CAST(cl.CodContabilita AS VARCHAR(50)) = CAST(a.Cod_Cliente AS VARCHAR(50))
              WHERE a.PrezzoControllo = e.Massimo
              ORDER BY cl.RagioneSociale) AS ClientePrezzoMassimo
        FROM Estremi e
        """;

    public async Task<PrezziControllo> PrezziControlloAsync(
        int idControllo, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);

        var prezzi = await cn.QueryFirstOrDefaultAsync<PrezziControllo>(
            new CommandDefinition(SqlPrezzi,
                new { IdControllo = idControllo }, cancellationToken: ct));

        return prezzi ?? new PrezziControllo();
    }
}
