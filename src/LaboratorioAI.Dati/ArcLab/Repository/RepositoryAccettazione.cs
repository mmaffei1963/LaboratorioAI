using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryAccettazione" />
/// <remarks>SOLA LETTURA su ArcLab. Fornisce le liste di riferimento per l'accettazione fuori sede.</remarks>
public class RepositoryAccettazione : IRepositoryAccettazione
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryAccettazione(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<VoceAnagrafica>> InsediamentiPerClienteAsync(int idCliente, CancellationToken ct = default)
    {
        // Nome insediamento + città. NB: la colonna città si chiama [CittàInsed]
        // (con accento), non "CittaInsed" → va tra parentesi quadre.
        const string sql = """
            SELECT i.IDInsediamento AS Id,
                   i.NomeInsed       AS Nome,
                   i.[CittàInsed]    AS Citta
            FROM Insediamento i
            WHERE i.NomeInsed IS NOT NULL AND i.IDCliente = @IdCliente
            ORDER BY i.NomeInsed
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync(
            new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));
        return righe.Select(r =>
        {
            string nome = (string?)r.Nome ?? "";
            string citta = ((string?)r.Citta ?? "").Trim();
            return new VoceAnagrafica((int)r.Id, citta.Length > 0 ? $"{nome} - {citta}" : nome);
        }).ToList();
    }

    public async Task<IReadOnlyList<VoceCommessa>> CommessePerInsediamentoAsync(int idInsediamento, CancellationToken ct = default)
    {
        // Solo le commesse dello specifico insediamento selezionato (non di tutto il cliente),
        // e solo quelle con un controllo associato. Espongo IdControllo, nome e prezzo.
        const string sql = """
            SELECT cm.IdCommessa       AS Id,
                   cm.IDControllo       AS IdControllo,
                   co.Controllo         AS Controllo,
                   cm.PrezzoControllo   AS Prezzo
            FROM commessa cm
                LEFT JOIN Controllo co ON co.IDControllo = cm.IDControllo
            WHERE cm.IDInsediamento = @IdInsediamento
              AND cm.IDControllo IS NOT NULL AND cm.IDControllo <> 0
            ORDER BY cm.IdCommessa DESC
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<VoceCommessa>(
            new CommandDefinition(sql, new { IdInsediamento = idInsediamento }, cancellationToken: ct));
        return righe.Select(r =>
        {
            var etichetta = $"{(string.IsNullOrWhiteSpace(r.Controllo) ? "Controllo" : r.Controllo)} [id {r.IdControllo}]";
            if (r.Prezzo.HasValue) etichetta += $" — € {r.Prezzo.Value:N2}";
            return r with { Nome = etichetta };
        }).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> TecnichePrelievoAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT t.IdTecnica AS Id, t.Tecnica AS Nome
            FROM TecnicaPrelievo t
            WHERE t.Tecnica IS NOT NULL
            ORDER BY t.Tecnica
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> MatriciTecnicaAsync(CancellationToken ct = default)
    {
        // La procedura di prelievo deriva dalla matrice: Matrice.Id_IOCampionamento → TecnicaPrelievo.
        const string sql = """
            SELECT mx.IdMatrice AS Id, t.Tecnica AS Nome
            FROM matrice mx
                INNER JOIN TecnicaPrelievo t ON t.IdTecnica = mx.Id_IOCampionamento
            WHERE mx.Matrice IS NOT NULL AND t.Tecnica IS NOT NULL
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> ImballaggiAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT i.IdImballaggio AS Id, i.Imballaggio AS Nome
            FROM Imballaggio i
            WHERE i.Imballaggio IS NOT NULL
            ORDER BY i.Imballaggio
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> LaboratoriEsterniAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT le.IdLabEsterno AS Id,
                   LTRIM(RTRIM(ISNULL(le.LabEsterno, '') + ' ' + ISNULL(le.IndirizzoLabEsterno, ''))) AS Nome
            FROM LabEsterno le
            WHERE le.LabEsterno IS NOT NULL
            ORDER BY le.LabEsterno
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<string?> CodContabilitaAsync(int idCliente, CancellationToken ct = default)
    {
        const string sql = "SELECT CONVERT(nvarchar(30), cl.CodContabilita) FROM Clienti cl WHERE cl.IDCliente = @Id";
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(sql, new { Id = idCliente }, cancellationToken: ct));
    }

    public async Task<IntestazioneMarea> IntestazioneAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP 1
                   CONVERT(nvarchar(200), m.DirettoreLaboratorio) AS Direttore,
                   CONVERT(nvarchar(200), m.[Città])              AS Citta,
                   CONVERT(nvarchar(200), m.MailRisultatiEst)     AS Mail
            FROM Marchi m
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryFirstOrDefaultAsync<IntestazioneMarea>(new CommandDefinition(sql, cancellationToken: ct)))
               ?? new IntestazioneMarea(null, null, null);
    }

    public async Task<LabEsternoInfo?> LabEsternoAsync(int idLabEsterno, CancellationToken ct = default)
    {
        const string sql = """
            SELECT le.IdLabEsterno AS Id,
                   CONVERT(nvarchar(300), le.LabEsterno)          AS Nome,
                   CONVERT(nvarchar(400), le.IndirizzoLabEsterno)  AS Indirizzo
            FROM LabEsterno le
            WHERE le.IdLabEsterno = @Id
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<LabEsternoInfo>(
            new CommandDefinition(sql, new { Id = idLabEsterno }, cancellationToken: ct));
    }

    public async Task<IndirizzoDestinatario?> IndirizzoDestinatarioAsync(int idInsediamento, CancellationToken ct = default)
    {
        const string sql = """
            SELECT cl.RagioneSociale                     AS RagioneSociale,
                   CONVERT(nvarchar(300), i.NomeInsed)   AS Insediamento,
                   CONVERT(nvarchar(300), i.IndirizzoInsed) AS Indirizzo,
                   CONVERT(nvarchar(30),  i.CapInsed)    AS Cap,
                   CONVERT(nvarchar(200), i.[CittàInsed]) AS Citta,
                   CONVERT(nvarchar(50),  i.ProvinciaInsed) AS Provincia
            FROM Insediamento i
                INNER JOIN Clienti cl ON cl.IDCliente = i.IDCliente
            WHERE i.IDInsediamento = @Id
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        var r = await cn.QueryFirstOrDefaultAsync(new CommandDefinition(sql, new { Id = idInsediamento }, cancellationToken: ct));
        if (r is null) return null;
        string indir = string.Join(" ", new[] {
            (string?)r.Indirizzo, (string?)r.Cap, (string?)r.Citta,
            string.IsNullOrWhiteSpace((string?)r.Provincia) ? null : $"({((string?)r.Provincia)!.Trim()})"
        }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        return new IndirizzoDestinatario((string?)r.RagioneSociale, (string?)r.Insediamento, indir);
    }

    public async Task<IReadOnlyList<RigaMetodoControllo>> RigheMetodoPerControlloAsync(int idControllo, CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                rm.IdRigametodo       AS IdRigametodo,
                rm.IdMetodo           AS IdMetodo,
                rm.IdControllo        AS IdControllo,
                rm.DenominazioneRiga  AS DenominazioneRiga,
                p.NomeParametro       AS Parametro,
                le.LabEsterno         AS LaboratorioEsterno
            FROM rigametodo rm
                INNER JOIN Metodo m       ON m.IdMetodo = rm.IdMetodo
                LEFT  JOIN Parametro p    ON p.IdParametro = m.IdParametro
                LEFT  JOIN LabEsterno le  ON le.IdLabEsterno = rm.IdLabEsterno
            WHERE rm.IdControllo = @IdControllo
            ORDER BY rm.DenominazioneRiga
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<RigaMetodoControllo>(
            new CommandDefinition(sql, new { IdControllo = idControllo }, cancellationToken: ct))).ToList();
    }

    public async Task<EsitoVerificaImportabilita> VerificaImportabilitaAsync(RiferimentiDaVerificare r, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var problemi = new List<string>();

        async Task Singolo(int? id, string sql, string descr)
        {
            if (id is null) return;
            var ok = await cn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
            if (ok is null) problemi.Add($"{descr} (id {id}) non esiste più in ArcLab.");
        }
        async Task Molti(IReadOnlyList<int> ids, string sql, string descr)
        {
            if (ids.Count == 0) return;
            var esistenti = (await cn.QueryAsync<int>(new CommandDefinition(sql, new { ids }, cancellationToken: ct))).ToHashSet();
            foreach (var id in ids.Distinct())
                if (!esistenti.Contains(id)) problemi.Add($"{descr} (id {id}) non esiste più in ArcLab.");
        }

        await Singolo(r.IdCliente, "SELECT 1 FROM Clienti WHERE IDCliente = @id", "Cliente");
        await Singolo(r.IdInsediamento, "SELECT 1 FROM Insediamento WHERE IDInsediamento = @id", "Insediamento");
        await Singolo(r.IdCommessa, "SELECT 1 FROM commessa WHERE IdCommessa = @id", "Commessa");
        await Singolo(r.IdPrelevatore, "SELECT 1 FROM mansione WHERE IdMansione = @id", "Prelevatore");
        await Singolo(r.IdRicevitore, "SELECT 1 FROM mansione WHERE IdMansione = @id", "Ricevitore");
        await Molti(r.Matrici, "SELECT IdMatrice FROM matrice WHERE IdMatrice IN @ids", "Matrice");
        await Molti(r.Termometri, "SELECT ID FROM TermometriRicevimento WHERE ID IN @ids", "Termometro");
        await Molti(r.LabEsterni, "SELECT IdLabEsterno FROM LabEsterno WHERE IdLabEsterno IN @ids", "Laboratorio esterno");
        await Molti(r.RigheMetodo, "SELECT IdRigametodo FROM rigametodo WHERE IdRigametodo IN @ids", "Riga metodo (prova)");

        return new EsitoVerificaImportabilita(problemi.Count == 0, problemi);
    }

    public async Task<IReadOnlyList<VoceOperatore>> OperatoriAsync(CancellationToken ct = default)
    {
        // Attività: 1 = prelevatore, 2 = ricevitore, 4 = vettore.
        // "Attivo" = Organico.FineProf IS NULL (fine rapporto non impostata).
        const string sql = """
            SELECT m.IdMansione AS Id,
                   LTRIM(RTRIM(ISNULL(o.CognomeOrg, '') + ' ' + ISNULL(o.NomeOrg, ''))) AS Nome,
                   m.[IdAttività] AS Attivita
            FROM mansione m
                INNER JOIN Organico o ON o.IdOrganico = m.IdOrganico
            WHERE m.[IdAttività] IN (1, 2, 4)
              AND (o.CognomeOrg IS NOT NULL OR o.NomeOrg IS NOT NULL)
              AND o.FineProf IS NULL   -- solo personale ancora in servizio
            ORDER BY Nome
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceOperatore>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceAnagrafica>> TermometriAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT t.ID AS Id, t.Termometro AS Nome
            FROM TermometriRicevimento t
            WHERE t.Termometro IS NOT NULL
            ORDER BY t.Termometro
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }
}
