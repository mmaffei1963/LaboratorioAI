using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryAnagrafiche" />
public class RepositoryAnagrafiche : IRepositoryAnagrafiche
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryAnagrafiche(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<IReadOnlyList<ClienteConCampioni>> ClientiConCampioniAsync(DateTime dal, CancellationToken ct = default)
    {
        // Prima i lotti del periodo, poi l'aggancio commessa → cliente sul codice contabile (come nelle altre letture).
        const string sql = """
            WITH Lotti AS (
                SELECT l.IdLotto, l.IdCommessa, l.DataRic FROM Lotto l WHERE l.DataRic >= @Dal
            )
            SELECT cl.IDCliente                                         AS IdCliente,
                   MAX(CONVERT(nvarchar(300), cl.RagioneSociale))       AS RagioneSociale,
                   MAX(CONVERT(nvarchar(30),  cl.PIVA))                 AS PartitaIva,
                   MAX(CONVERT(nvarchar(400), cl.Email))                AS Email,
                   COUNT(c.IdCampione)                                  AS NumeroCampioni,
                   MAX(lt.DataRic)                                      AS UltimoCampione
            FROM Lotti lt
                INNER JOIN Campione c  ON c.IdLotto = lt.IdLotto
                INNER JOIN commessa cm ON cm.IdCommessa = lt.IdCommessa
                INNER JOIN Clienti cl  ON CAST(cl.CODContabilita AS varchar(50)) = CAST(cm.Cod_Cliente AS varchar(50))
            GROUP BY cl.IDCliente
            ORDER BY MAX(CONVERT(nvarchar(300), cl.RagioneSociale))
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        // Classe con proprietà settabili (non record): Dapper converte i tipi del reader senza cercare un costruttore.
        var righe = await cn.QueryAsync<RigaClienteCampioni>(
            new CommandDefinition(sql, new { Dal = dal.Date }, commandTimeout: 120, cancellationToken: ct));
        return righe.Select(r => new ClienteConCampioni(r.IdCliente, r.RagioneSociale?.Trim(), r.PartitaIva?.Trim(), r.Email?.Trim(),
            r.NumeroCampioni, r.UltimoCampione)).ToList();
    }

    private sealed class RigaClienteCampioni
    {
        public int IdCliente { get; set; }
        public string? RagioneSociale { get; set; }
        public string? PartitaIva { get; set; }
        public string? Email { get; set; }
        public int NumeroCampioni { get; set; }
        public DateTime? UltimoCampione { get; set; }
    }

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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ClienteDistinto>> ClientiDistintiAsync(CancellationToken ct = default)
    {
        // Come ClientiAsync, con quello che serve a distinguere gli
        // omonimi, e senza le schede da cui non si deve partire.
        //
        // Un cliente entra nella scelta solo se e' **attivo** e ha un
        // **codice contabilita'**: in ArcLab restano schede vecchie
        // della stessa societa', spente o mai aperte davvero, e
        // produrre un pacchetto di documenti intestato a una di quelle
        // si scopre mesi dopo (11/11/2026, verificato su tre doppioni
        // di PARCO LE NAVI). Il codice contabilita' da solo non basta:
        // PARCONAVI S.P.A. ce l'ha ed e' spento.
        //
        // Gli insediamenti si contano e si pretendono **in uso**
        // (<c>NonInUso = 0</c>): una societa' i cui insediamenti sono
        // tutti chiusi non e' un cliente a cui fare la manualistica.
        //
        // NB: la colonna citta' di fatturazione e' [CittàFatt] con
        // l'accento.
        const string sql = """
            SELECT cl.IDCliente AS Id, cl.RagioneSociale AS Nome,
                   cl.PIVA AS PartitaIva, cl.[CittàFatt] AS Comune,
                   CAST(CASE WHEN NULLIF(LTRIM(RTRIM(cl.email)), '') LIKE '%@%'
                             THEN 1 ELSE 0 END AS bit) AS HaEmail,
                   (SELECT COUNT(*) FROM Insediamento i
                     WHERE i.IDCliente = cl.IDCliente AND ISNULL(i.NonInUso, 0) = 0) AS Insediamenti
            FROM Clienti cl
            WHERE cl.RagioneSociale IS NOT NULL
              AND cl.Attivo = 1
              AND NULLIF(LTRIM(RTRIM(cl.CODContabilita)), '') IS NOT NULL
              AND EXISTS (SELECT 1 FROM Insediamento i
                           WHERE i.IDCliente = cl.IDCliente AND ISNULL(i.NonInUso, 0) = 0)
            ORDER BY cl.RagioneSociale
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<ClienteDistinto>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public async Task<IReadOnlyList<VoceInsediamento>> InsediamentiAsync(
        int? idCliente = null, CancellationToken ct = default)
    {
        // Nome insediamento + città. NB: la colonna città è [CittàInsed] (con accento).
        const string sql = """
            SELECT i.IDInsediamento AS Id, i.NomeInsed AS Nome, i.[CittàInsed] AS Citta, i.IDCliente
            FROM Insediamento i
            WHERE i.NomeInsed IS NOT NULL
              AND (@IdCliente IS NULL OR i.IDCliente = @IdCliente)
            ORDER BY i.NomeInsed
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync(
            new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));
        return righe.Select(r =>
        {
            string nome = (string?)r.Nome ?? "";
            string citta = ((string?)r.Citta ?? "").Trim();
            return new VoceInsediamento((int)r.Id, citta.Length > 0 ? $"{nome} - {citta}" : nome, (int)r.IDCliente);
        }).ToList();
    }

    public async Task<DettaglioCliente?> DettaglioClienteAsync(int idCliente, CancellationToken ct = default)
    {
        // PIVA e indirizzo di fatturazione (sede legale) sono entrambi sul Cliente: l'offerta è
        // intestata al cliente, non a un insediamento/sede specifico, quindi si leggono da qui
        // direttamente, senza passare dagli insediamenti (che restano indirizzi operativi diversi).
        const string sql = """
            SELECT cl.IDCliente AS IdCliente, cl.PIVA AS PartitaIva,
                   cl.IndirizzoFatt AS Indirizzo, cl.CapFatt AS Cap,
                   cl.[CittàFatt] AS Citta, cl.ProvinciaFatt AS Provincia, cl.Email AS Email
            FROM Clienti cl
            WHERE cl.IDCliente = @IdCliente
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<DettaglioCliente>(
            new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SedeInsediamento>> SediInsediamentiAsync(int idCliente, CancellationToken ct = default)
    {
        // Le stesse colonne di InsediamentiConEmailAsync, senza comporle:
        // qui servono separate. NB: la colonna citta' e' [CittaInsed] con
        // l'accento, come nelle altre letture di questa classe.
        const string sql = """
            SELECT i.IDInsediamento AS Id, i.NomeInsed AS Nome, i.IndirizzoInsed AS Indirizzo,
                   i.CapInsed AS Cap, i.[CittàInsed] AS Comune, i.ProvinciaInsed AS Provincia,
                   i.Email AS Email
            FROM Insediamento i
            WHERE i.IDCliente = @IdCliente AND i.NomeInsed IS NOT NULL
              AND ISNULL(i.NonInUso, 0) = 0
            ORDER BY i.NomeInsed
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<SedeInsediamento>(
            new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));

        return righe.Select(r => r with
        {
            Nome = (r.Nome ?? "").Trim(),
            Indirizzo = Ripulisci(r.Indirizzo),
            Cap = Ripulisci(r.Cap),
            Comune = Ripulisci(r.Comune),
            Provincia = Ripulisci(r.Provincia),
            Email = Ripulisci(r.Email)
        }).ToList();
    }

    private static string? Ripulisci(string? v) =>
        string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    public async Task<IReadOnlyList<InsediamentoContatto>> InsediamentiConEmailAsync(int idCliente, CancellationToken ct = default)
    {
        // NB: la colonna città è [CittàInsed] (con accento), come in InsediamentiAsync.
        const string sql = """
            SELECT i.NomeInsed AS Nome, i.IndirizzoInsed AS Indirizzo, i.CapInsed AS Cap,
                   i.[CittàInsed] AS Citta, i.ProvinciaInsed AS Provincia, i.Email AS Email
            FROM Insediamento i
            WHERE i.IDCliente = @IdCliente AND i.NomeInsed IS NOT NULL
            ORDER BY i.NomeInsed
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync(
            new CommandDefinition(sql, new { IdCliente = idCliente }, cancellationToken: ct));
        return righe.Select(r =>
        {
            var parti = new[] { (string?)r.Indirizzo, string.Join(" ", new[] { (string?)r.Cap, (string?)r.Citta }.Where(s => !string.IsNullOrWhiteSpace(s))), string.IsNullOrWhiteSpace((string?)r.Provincia) ? null : $"({((string?)r.Provincia)!.Trim()})" }
                .Where(s => !string.IsNullOrWhiteSpace(s));
            var indirizzo = string.Join(" ", parti).Trim();
            return new InsediamentoContatto(
                ((string?)r.Nome ?? "").Trim(),
                indirizzo.Length > 0 ? indirizzo : null,
                string.IsNullOrWhiteSpace((string?)r.Email) ? null : ((string)r.Email).Trim());
        }).ToList();
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

    public async Task<IReadOnlyList<VoceAnagrafica>> MetodiAsync(CancellationToken ct = default)
    {
        // Solo metodi effettivamente usati (presenti in rigametodo). Etichetta: denominazione + rif. biblio.
        const string sql = """
            SELECT m.IdMetodo AS Id,
                   LTRIM(RTRIM(
                     ISNULL(NULLIF(CONVERT(nvarchar(300), m.DettagliMetodo), ''), CONVERT(nvarchar(300), p.NomeParametro))
                     + CASE WHEN m.RifBiblio IS NOT NULL THEN ' — ' + CONVERT(nvarchar(300), m.RifBiblio) ELSE '' END
                   )) AS Nome
            FROM Metodo m
                LEFT JOIN Parametro p ON p.IdParametro = m.IdParametro
            WHERE EXISTS (SELECT 1 FROM rigametodo rm WHERE rm.IdMetodo = m.IdMetodo)
              AND (m.DettagliMetodo IS NOT NULL OR p.NomeParametro IS NOT NULL OR m.RifBiblio IS NOT NULL)
            ORDER BY Nome
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, commandTimeout: 120, cancellationToken: ct))).ToList();
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

    public async Task<IReadOnlyList<VoceAnagrafica>> ImballaggiAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT IdImballaggio AS Id, LTRIM(RTRIM(CONVERT(nvarchar(300), Imballaggio))) AS Nome
            FROM Imballaggio
            WHERE Imballaggio IS NOT NULL AND LTRIM(RTRIM(CONVERT(nvarchar(300), Imballaggio))) <> ''
            ORDER BY 2
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        return (await cn.QueryAsync<VoceAnagrafica>(
            new CommandDefinition(sql, cancellationToken: ct))).ToList();
    }

    public Task<IReadOnlyList<PuntoPrelievoUsato>> PuntiPrelievoUsatiAsync(DateTime dal, int perInsediamento, CancellationToken ct = default) =>
        ValoriUsatiPerInsediamentoAsync("ca.PuntoPrelievo", dal, perInsediamento, ct);

    public Task<IReadOnlyList<PuntoPrelievoUsato>> LuoghiPrelievoUsatiAsync(DateTime dal, int perInsediamento, CancellationToken ct = default) =>
        ValoriUsatiPerInsediamentoAsync("ca.LuogoPrelievo", dal, perInsediamento, ct);

    /// <summary>Valori più frequenti di una colonna del campione per insediamento (colonna fissa nel codice, mai da input).</summary>
    private async Task<IReadOnlyList<PuntoPrelievoUsato>> ValoriUsatiPerInsediamentoAsync(string colonna, DateTime dal, int perInsediamento, CancellationToken ct)
    {
        if (colonna is not ("ca.PuntoPrelievo" or "ca.LuogoPrelievo")) throw new ArgumentException("Colonna non ammessa.", nameof(colonna));
        // Campione → Lotto → commessa → ContattoCliente → insediamento (come nelle altre letture).
        var sql = $"""
            WITH Punti AS (
                SELECT cc.IDInsediamento AS IdInsediamento,
                       LTRIM(RTRIM(CONVERT(nvarchar(200), {colonna}))) AS Punto,
                       COUNT(*) AS Volte
                FROM Campione ca
                    INNER JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
                    INNER JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
                    INNER JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
                WHERE lo.DataRic >= @Dal
                  AND {colonna} IS NOT NULL
                GROUP BY cc.IDInsediamento, LTRIM(RTRIM(CONVERT(nvarchar(200), {colonna})))
            ), Ordinati AS (
                SELECT IdInsediamento, Punto, Volte,
                       ROW_NUMBER() OVER (PARTITION BY IdInsediamento ORDER BY Volte DESC, Punto) AS N
                FROM Punti
                WHERE Punto <> '' AND Punto NOT IN ('sconosciuto', 'n.a.', 'na', '-', '--')
            )
            SELECT IdInsediamento, Punto, Volte FROM Ordinati WHERE N <= @Max
            ORDER BY IdInsediamento, Volte DESC, Punto
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<RigaPunto>(
            new CommandDefinition(sql, new { Dal = dal.Date, Max = perInsediamento }, commandTimeout: 120, cancellationToken: ct));
        return righe.Select(r => new PuntoPrelievoUsato(r.IdInsediamento, r.Punto ?? "", r.Volte)).ToList();
    }

    private sealed class RigaPunto
    {
        public int IdInsediamento { get; set; }
        public string? Punto { get; set; }
        public int Volte { get; set; }
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
