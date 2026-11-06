using Dapper;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryControlli" />
/// <remarks>SOLA LETTURA su ArcLab. Fornisce il catalogo dei metodi per la proposta di controlli.</remarks>
public class RepositoryControlli : IRepositoryControlli
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryControlli(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<VoceMetodo>> MetodiCatalogoAsync(string? filtro, CancellationToken ct = default)
    {
        // Ricerca per METODO attivo (ProceduraMetodo.Attivo), descrizione = NomeParametro / RifMetodo.
        // I campi di riga (denominazione, unità, lab esterno, arrotondamento) da un rigametodo
        // rappresentativo del metodo (il più recente).
        const string sql = """
            SELECT TOP (2000)
                m.IdMetodo AS Id,
                LTRIM(RTRIM(ISNULL(p.NomeParametro, '') + ' / ' + ISNULL(m.RifMetodo, ''))) AS Descrizione,
                m.Acronimo,
                m.RifBiblio,
                m.DettagliMetodo,
                m.CostoMetodoEuro AS Costo,
                rm.DenominazioneRiga,
                rm.IdUnita,
                u.UnitaMisura,
                rm.IdLabEsterno,
                le.LabEsterno,
                rm.Arrotonda   AS CifreSignificative,
                rm.MaxDecimali
            FROM metodo m
                INNER JOIN Parametro p ON p.IdParametro = m.IdParametro
                OUTER APPLY (SELECT TOP 1 r.DenominazioneRiga, r.IdUnita, r.IdLabEsterno, r.Arrotonda, r.MaxDecimali
                             FROM rigametodo r WHERE r.IdMetodo = m.IdMetodo
                             ORDER BY r.IdRigametodo DESC) rm
                LEFT JOIN Unitadimisura u ON u.IdUnita = rm.IdUnita
                LEFT JOIN LabEsterno le    ON le.IdLabEsterno = rm.IdLabEsterno
            WHERE EXISTS (SELECT 1 FROM ProceduraMetodo pm WHERE pm.IdMetodi = m.IdMetodo AND pm.Attivo = 1)
              AND (@Filtro IS NULL
                   OR p.NomeParametro LIKE @Filtro
                   OR m.RifMetodo     LIKE @Filtro
                   OR m.RifBiblio     LIKE @Filtro)
            ORDER BY Descrizione
            """;
        var f = string.IsNullOrWhiteSpace(filtro) ? null : $"%{filtro.Trim()}%";
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceMetodo>(new CommandDefinition(sql, new { Filtro = f }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceControlloEsistente>> ControlliConStessiMetodiAsync(IReadOnlyList<int> idMetodi, int? escludiIdControllo = null, CancellationToken ct = default)
    {
        var ids = idMetodi.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<VoceControlloEsistente>();

        // Controlli il cui insieme DISTINTO di metodi coincide esattamente con quello proposto:
        // contengono tutti i metodi proposti (COUNT distinti = N) e non ne hanno altri (totale distinti = N).
        // Esclude l'eventuale controllo che si sta modificando.
        const string sql = """
            SELECT c.IdControllo, c.Controllo
            FROM Controllo c
                INNER JOIN rigametodo rm ON rm.IdControllo = c.IdControllo
            WHERE rm.IdMetodo IN @Ids
              AND (@Escludi IS NULL OR c.IdControllo <> @Escludi)
            GROUP BY c.IdControllo, c.Controllo
            HAVING COUNT(DISTINCT rm.IdMetodo) = @N
               AND (SELECT COUNT(DISTINCT r2.IdMetodo) FROM rigametodo r2 WHERE r2.IdControllo = c.IdControllo) = @N
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceControlloEsistente>(
            new CommandDefinition(sql, new { Ids = ids, N = ids.Count, Escludi = escludiIdControllo }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<string>> SigleDisponibiliAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT LTRIM(RTRIM(Sigla_metodo)) FROM Sigla_Metodo WHERE ISNULL(superato,0) = 0 AND Sigla_metodo IS NOT NULL";
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<string>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<string?> NomeControlloAsync(int idControllo, CancellationToken ct = default)
    {
        const string sql = "SELECT Controllo FROM Controllo WHERE IdControllo = @Id";
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(sql, new { Id = idControllo }, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<VoceImport>> StatiImportAsync(IReadOnlyList<string> codiciLocali, CancellationToken ct = default)
    {
        var codici = codiciLocali?.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList() ?? new List<string>();
        if (codici.Count == 0) return Array.Empty<VoceImport>();

        // Il registro potrebbe non esistere in ambienti non ancora predisposti: la guardia OBJECT_ID evita l'errore.
        const string sql = """
            IF OBJECT_ID('dbo.ImportDaLaboratorioAI') IS NULL
                SELECT CAST(NULL AS VARCHAR(40)) AS CodiceLocale, CAST(NULL AS INT) AS IdControllo WHERE 1 = 0;
            ELSE
                SELECT CodiceLocale, IdEntitaArcLab AS IdControllo
                FROM dbo.ImportDaLaboratorioAI
                WHERE TipoEntita = 'PropostaControllo' AND CodiceLocale IN @Codici;
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceImport>(new CommandDefinition(sql, new { Codici = codici }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceControlloEsistente>> ControlliAttiviAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT IdControllo, Controllo FROM Controllo WHERE Attivo = 1 AND Controllo IS NOT NULL ORDER BY Controllo";
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceControlloEsistente>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceMetodo>> RigheDiControlloAsync(int idControllo, CancellationToken ct = default)
    {
        const string sql = """
            SELECT rm.IdMetodo AS Id,
                   LTRIM(RTRIM(ISNULL(p.NomeParametro, '') + ' / ' + ISNULL(m.RifMetodo, ''))) AS Descrizione,
                   m.Acronimo,
                   rm.DenominazioneRiga,
                   m.RifBiblio,
                   m.DettagliMetodo,
                   m.CostoMetodoEuro AS Costo,
                   rm.IdUnita, u.UnitaMisura,
                   rm.IdLabEsterno, le.LabEsterno,
                   rm.Arrotonda AS CifreSignificative, rm.MaxDecimali,
                   ISNULL(m.Accreditato, 0) AS Accreditato,
                   le.Frase AS LabEsternoFrase
            FROM rigametodo rm
                INNER JOIN metodo m       ON m.IdMetodo = rm.IdMetodo
                LEFT  JOIN Parametro p    ON p.IdParametro = m.IdParametro
                LEFT  JOIN Unitadimisura u ON u.IdUnita = rm.IdUnita
                LEFT  JOIN LabEsterno le   ON le.IdLabEsterno = rm.IdLabEsterno
            WHERE rm.IdControllo = @IdControllo
            ORDER BY rm.DenominazioneRiga
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceMetodo>(new CommandDefinition(sql, new { IdControllo = idControllo }, cancellationToken: ct))).ToList();
    }
}
