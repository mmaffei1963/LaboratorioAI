using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryAnagrafiche" />
public class RepositoryAnagrafiche : IRepositoryAnagrafiche
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryAnagrafiche(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<VoceAnagrafica>> ClientiAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT cl.IDCliente AS Id, cl.RagioneSociale AS Nome
            FROM Clienti cl
            WHERE cl.RagioneSociale IS NOT NULL
              AND EXISTS (SELECT 1 FROM Insediamento i WHERE i.IDCliente = cl.IDCliente)
            ORDER BY cl.RagioneSociale
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceInsediamento>> InsediamentiAsync(
        int? idCliente = null, CancellationToken ct = default)
    {
        const string sql = """
            SELECT i.IDInsediamento AS Id, i.NomeInsed AS Nome, i.IDCliente
            FROM Insediamento i
            WHERE i.NomeInsed IS NOT NULL
              AND (@IdCliente IS NULL OR i.IDCliente = @IdCliente)
            ORDER BY i.NomeInsed
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceInsediamento>(
            new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> MatriciAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT IdMatrice AS Id, Matrice AS Nome
            FROM matrice
            WHERE Matrice IS NOT NULL
            ORDER BY Matrice
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
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

    public async Task<IReadOnlyList<VoceAnagrafica>> LaboratoriEsterniAsync(CancellationToken ct = default)
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

    public async Task<IReadOnlyList<VoceAnagrafica>> StrumentiInterniAsync(CancellationToken ct = default)
    {
        // Etichetta univoca "NomeStrum - IdApparecchio": unisce Strumenti e
        // apparecchiature (legate da IdStrumento). Se lo strumento non ha un
        // apparecchio associato, resta il solo NomeStrum.
        const string sql = """
            SELECT DISTINCT
                s.IdStrumento AS Id,
                LTRIM(RTRIM(s.NomeStrum))
                    + ISNULL(' - ' + CAST(a.IdApparecchio AS varchar(20)), '') AS Nome
            FROM Strumenti s
            LEFT JOIN apparecchiature a ON a.IdStrumento = s.IdStrumento
            WHERE s.NomeStrum IS NOT NULL AND LTRIM(RTRIM(s.NomeStrum)) <> ''
            ORDER BY Nome
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<string>> PrelevatoriAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT LTRIM(RTRIM(Prelevatore))
            FROM Lotto
            WHERE Prelevatore IS NOT NULL AND LTRIM(RTRIM(Prelevatore)) <> ''
            ORDER BY 1
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<string>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<string>> LuoghiPrelievoAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT DISTINCT TOP 500 LTRIM(RTRIM(LuogoPrelievo))
            FROM Campione
            WHERE LuogoPrelievo IS NOT NULL AND LTRIM(RTRIM(LuogoPrelievo)) <> ''
            ORDER BY 1
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<string>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }
}
