using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryOrdineAccettazione" />
/// <remarks>
/// SOLA LETTURA su ArcLab (LabAnalisiDati). Ricostruisce l'Ordine di Accettazione di un lotto
/// già ricevuto: testata (commessa/cliente/insediamento/controllo/prezzo), campioni (matrice,
/// imballaggio, punto di prelievo, T° ricevimento, urgenza) e analisi (prove → rigametodo → metodo),
/// con la frase del laboratorio esterno per le prove in subappalto.
/// </remarks>
public class RepositoryOrdineAccettazione : IRepositoryOrdineAccettazione
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryOrdineAccettazione(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    // ── Testata del lotto: lotto + commessa + cliente + insediamento + controllo ──
    private const string SqlTestata = """
        SELECT
            l.IdLotto                                  AS IdLotto,
            CONVERT(nvarchar(20),  l.CodLotto)         AS CodLotto,
            l.IdCommessa                               AS IdCommessa,
            cm.IDInsediamento                          AS IdInsediamento,
            l.DataRic                                  AS DataRicezione,
            CAST(l.OraRic AS time)                     AS OraRicezione,
            l.DataPrelievo                             AS DataPrelievo,
            cm.IDControllo                             AS IdControllo,
            CONVERT(nvarchar(300), co.Controllo)       AS Controllo,
            CAST(cm.PrezzoControllo AS decimal(18,2))  AS PrezzoCampione,
            cl.IDCliente                               AS IdClienteArcLab,
            CONVERT(nvarchar(300), cl.RagioneSociale)  AS ClienteRagioneSociale,
            CONVERT(nvarchar(300), cl.IndirizzoFatt)   AS ClienteIndirizzo,
            CONVERT(nvarchar(30),  cl.CapFatt)         AS ClienteCap,
            CONVERT(nvarchar(200), cl.[CittàFatt])     AS ClienteCitta,
            CONVERT(nvarchar(50),  cl.ProvinciaFatt)   AS ClienteProvincia,
            CONVERT(nvarchar(30),  cl.PIVA)            AS ClientePartitaIva,
            CONVERT(nvarchar(30),  cl.CodContabilita)  AS ClienteCodContabilita,
            CONVERT(nvarchar(300), i.NomeInsed)        AS Insediamento,
            CONVERT(nvarchar(300), i.IndirizzoInsed)   AS InsediamentoIndirizzo,
            CONVERT(nvarchar(30),  i.CapInsed)         AS InsediamentoCap,
            CONVERT(nvarchar(200), i.[CittàInsed])     AS InsediamentoCitta,
            CONVERT(nvarchar(50),  i.ProvinciaInsed)   AS InsediamentoProvincia,
            CONVERT(nvarchar(400), i.Email)            AS InsediamentoEmail
        FROM Lotto l
            LEFT JOIN commessa cm ON cm.IdCommessa = l.IdCommessa
            LEFT JOIN Controllo co ON co.IDControllo = cm.IDControllo
            LEFT JOIN Clienti cl ON CAST(cl.CODContabilita AS varchar(50)) = CAST(cm.Cod_Cliente AS varchar(50))
            LEFT JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
        WHERE l.IdLotto = @IdLotto
        """;

    // ── Campioni del lotto ──
    private const string SqlCampioni = """
        SELECT
            c.IdCampione                              AS IdCampione,
            CONVERT(nvarchar(20),  c.CodCampione)     AS CodCampione,
            CONVERT(nvarchar(500), c.DenomCampione)   AS Denominazione,
            CONVERT(nvarchar(300), mt.Matrice)        AS Matrice,
            CONVERT(nvarchar(300), im.Imballaggio)    AS Imballaggio,
            CONVERT(nvarchar(300), c.PuntoPrelievo)   AS PuntoPrelievo,
            CONVERT(nvarchar(300), c.LuogoPrelievo)   AS LuogoPrelievo,
            CONVERT(nvarchar(50),  c.TempRic)         AS TemperaturaRicevimento,
            CASE WHEN ISNULL(c.Urgente, 0) = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS Urgente,
            NULLIF(LTRIM(RTRIM(ISNULL(org.CognomeOrg, '') + ' ' + ISNULL(org.NomeOrg, ''))), '') AS Prelevatore
        FROM Campione c
            LEFT JOIN Matrice mt    ON mt.IdMatrice = c.IdMatrice
            LEFT JOIN Imballaggio im ON im.IdImballaggio = c.IdImballaggio
            LEFT JOIN mansione msp  ON msp.IdMansione = c.IdPrelevatore
            LEFT JOIN Organico org  ON org.IdOrganico = msp.IdOrganico
        WHERE c.IdLotto = @IdLotto
        ORDER BY c.CodCampione
        """;

    // ── Analisi (prove) di tutti i campioni del lotto ──
    private const string SqlAnalisi = """
        SELECT
            c.IdCampione                              AS IdCampione,
            -- Denominazione come in ArcLab: DettagliMetodo + " - aliquota N" (se NumeroAliquota valorizzato).
            LTRIM(RTRIM(
                ISNULL(CONVERT(nvarchar(300), m.DettagliMetodo), '') +
                CASE WHEN pr.NumeroAliquota IS NOT NULL
                          AND LTRIM(RTRIM(CONVERT(nvarchar(50), pr.NumeroAliquota))) <> ''
                     THEN ' - aliquota ' + CONVERT(nvarchar(50), pr.NumeroAliquota)
                     ELSE '' END
            ))                                        AS Denominazione,
            CONVERT(nvarchar(300), p.NomeParametro)   AS Parametro,
            CONVERT(nvarchar(300), m.RifBiblio)       AS Metodo,
            CONVERT(nvarchar(300), rm.DenominazioneRiga) AS DenominazioneRiga,
            ISNULL(m.Accreditato, 0)                  AS Accreditato,
            CONVERT(nvarchar(300), le.LabEsterno)     AS LaboratorioEsterno,
            CONVERT(nvarchar(1000), le.Frase)         AS FraseLabEsterno
        FROM prove pr
            INNER JOIN Campione c     ON c.IdCampione = pr.IdCampione
            INNER JOIN rigametodo rm  ON rm.IdRigametodo = pr.IdRigaMetodo
            INNER JOIN Metodo m       ON m.IdMetodo = rm.IdMetodo
            LEFT  JOIN Parametro p    ON p.IdParametro = m.IdParametro
            LEFT  JOIN LabEsterno le  ON le.IdLabEsterno = rm.IdLabEsterno
        WHERE c.IdLotto = @IdLotto
        ORDER BY c.CodCampione, p.NomeParametro, rm.DenominazioneRiga
        """;

    // NB: classi con proprietà settabili (non record posizionali): Dapper mappa per nome e converte
    // i tipi restituiti dal reader (es. money/float → decimal?, datetime → DateTime?), evitando
    // l'errore di "costruttore corrispondente" tipico dei record.
    private sealed class RigaTestata
    {
        public int IdLotto { get; set; }
        public string? CodLotto { get; set; }
        public int IdCommessa { get; set; }
        public int? IdInsediamento { get; set; }
        public DateTime? DataRicezione { get; set; }
        public TimeSpan? OraRicezione { get; set; }
        public DateTime? DataPrelievo { get; set; }
        public int? IdControllo { get; set; }
        public string? Controllo { get; set; }
        public decimal? PrezzoCampione { get; set; }
        public int? IdClienteArcLab { get; set; }
        public string? ClienteRagioneSociale { get; set; }
        public string? ClienteIndirizzo { get; set; }
        public string? ClienteCap { get; set; }
        public string? ClienteCitta { get; set; }
        public string? ClienteProvincia { get; set; }
        public string? ClientePartitaIva { get; set; }
        public string? ClienteCodContabilita { get; set; }
        public string? Insediamento { get; set; }
        public string? InsediamentoIndirizzo { get; set; }
        public string? InsediamentoCap { get; set; }
        public string? InsediamentoCitta { get; set; }
        public string? InsediamentoProvincia { get; set; }
        public string? InsediamentoEmail { get; set; }
    }

    private sealed class RigaCampione
    {
        public int IdCampione { get; set; }
        public string? CodCampione { get; set; }
        public string? Denominazione { get; set; }
        public string? Matrice { get; set; }
        public string? Imballaggio { get; set; }
        public string? PuntoPrelievo { get; set; }
        public string? LuogoPrelievo { get; set; }
        public string? TemperaturaRicevimento { get; set; }
        public bool Urgente { get; set; }
        public string? Prelevatore { get; set; }
    }

    // Autocontrollo dichiarato (Campione.Escludi_statistica, sì/no). Query separata e tollerante: se la
    // colonna non esiste o ha un tipo imprevisto, l'Ordine si genera lo stesso senza la riga autocontrollo.
    private const string SqlAutocontrollo = """
        SELECT c.IdCampione AS IdCampione, CONVERT(nvarchar(10), c.Escludi_statistica) AS Valore
        FROM Campione c
        WHERE c.IdLotto = @IdLotto
        """;

    // Richieste del Cliente (Campione.RichiesteCliente, 09/10/2026): se contiene «Richiesta Comunicazione anticipata dei
    // fuori limite» la comunicazione è richiesta. Tollerante come l'autocontrollo.
    private const string SqlRichiesteCliente = """
        SELECT c.IdCampione AS IdCampione, CONVERT(nvarchar(1000), c.RichiesteCliente) AS Valore
        FROM Campione c
        WHERE c.IdLotto = @IdLotto
        """;

    // Giudizio di conformità richiesto (Clienti.GiudizioConf, sì/no). Tollerante come l'autocontrollo.
    private const string SqlGiudizio = """
        SELECT CONVERT(nvarchar(10), cl.GiudizioConf) FROM Clienti cl WHERE cl.IDCliente = @IdCliente
        """;

    private sealed class RigaAutocontrollo
    {
        public int IdCampione { get; set; }
        public string? Valore { get; set; }
    }

    /// <summary>Interpreta sì/no di ArcLab (bit, -1 di Access, testo "Sì"/"SI"/"True").</summary>
    private static bool Si(string? v) =>
        (v ?? "").Trim().ToLowerInvariant() is "1" or "-1" or "true" or "si" or "sì" or "s" or "yes" or "y";

    private sealed class RigaAnalisi
    {
        public int IdCampione { get; set; }
        public string? Denominazione { get; set; }
        public string? Parametro { get; set; }
        public string? Metodo { get; set; }
        public string? DenominazioneRiga { get; set; }
        public bool Accreditato { get; set; }
        public string? LaboratorioEsterno { get; set; }
        public string? FraseLabEsterno { get; set; }
    }

    public async Task<OrdineAccettazione?> LeggiPerLottoAsync(int idLotto, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);

        var t = await cn.QueryFirstOrDefaultAsync<RigaTestata>(
            new CommandDefinition(SqlTestata, new { IdLotto = idLotto }, cancellationToken: ct));
        if (t is null) return null;

        var campioni = (await cn.QueryAsync<RigaCampione>(
            new CommandDefinition(SqlCampioni, new { IdLotto = idLotto }, cancellationToken: ct))).ToList();
        if (campioni.Count == 0) return null;

        var analisi = (await cn.QueryAsync<RigaAnalisi>(
            new CommandDefinition(SqlAnalisi, new { IdLotto = idLotto }, cancellationToken: ct))).ToList();

        Dictionary<int, bool>? autocontrollo = null;
        try
        {
            autocontrollo = (await cn.QueryAsync<RigaAutocontrollo>(
                    new CommandDefinition(SqlAutocontrollo, new { IdLotto = idLotto }, cancellationToken: ct)))
                .GroupBy(r => r.IdCampione).ToDictionary(g => g.Key, g => Si(g.First().Valore));
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            autocontrollo = null; // colonna non disponibile: niente riga autocontrollo nell'Ordine
        }

        Dictionary<int, string?>? richieste = null;
        try
        {
            richieste = (await cn.QueryAsync<RigaAutocontrollo>(
                    new CommandDefinition(SqlRichiesteCliente, new { IdLotto = idLotto }, cancellationToken: ct)))
                .GroupBy(r => r.IdCampione).ToDictionary(g => g.Key, g => g.First().Valore);
        }
        catch (Microsoft.Data.SqlClient.SqlException)
        {
            richieste = null; // campo non disponibile: niente riga sulla comunicazione anticipata
        }

        bool? giudizio = null;
        if (t.IdClienteArcLab is int idCli)
        {
            try
            {
                giudizio = Si(await cn.ExecuteScalarAsync<string?>(
                    new CommandDefinition(SqlGiudizio, new { IdCliente = idCli }, cancellationToken: ct)));
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                giudizio = null; // campo non disponibile: niente riga giudizio nell'Ordine
            }
        }

        var codLotto = (t.CodLotto ?? "").Trim();
        var perCampione = analisi.GroupBy(a => a.IdCampione).ToDictionary(g => g.Key, g => g.ToList());

        var ordine = new OrdineAccettazione
        {
            IdLotto = t.IdLotto,
            CodLotto = codLotto,
            IdCommessa = t.IdCommessa,
            IdInsediamento = t.IdInsediamento,
            DataRicezione = t.DataRicezione,
            OraRicezione = t.OraRicezione,
            DataPrelievo = t.DataPrelievo,
            IdControllo = t.IdControllo,
            Controllo = t.Controllo?.Trim(),
            PrezzoCampione = t.PrezzoCampione,
            IdClienteArcLab = t.IdClienteArcLab,
            ClienteRagioneSociale = t.ClienteRagioneSociale?.Trim(),
            ClienteIndirizzo = t.ClienteIndirizzo?.Trim(),
            ClienteCap = t.ClienteCap?.Trim(),
            ClienteCitta = t.ClienteCitta?.Trim(),
            ClienteProvincia = t.ClienteProvincia?.Trim(),
            ClientePartitaIva = t.ClientePartitaIva?.Trim(),
            ClienteCodContabilita = t.ClienteCodContabilita?.Trim(),
            Insediamento = t.Insediamento?.Trim(),
            InsediamentoIndirizzo = t.InsediamentoIndirizzo?.Trim(),
            InsediamentoCap = t.InsediamentoCap?.Trim(),
            InsediamentoCitta = t.InsediamentoCitta?.Trim(),
            InsediamentoProvincia = t.InsediamentoProvincia?.Trim(),
            InsediamentoEmail = NormalizzaEmail(t.InsediamentoEmail),
            GiudizioConformitaRichiesto = giudizio,
        };

        foreach (var c in campioni)
        {
            var codCamp = (c.CodCampione ?? "").Trim();
            bool? dichiarato = null;
            if (autocontrollo is not null && autocontrollo.TryGetValue(c.IdCampione, out var ac)) dichiarato = ac;
            var camp = new CampioneAccettazione
            {
                IdCampione = c.IdCampione,
                CodCampione = codCamp,
                Riferimento = string.IsNullOrEmpty(codCamp) ? codLotto : $"{codLotto}-{codCamp}",
                Denominazione = c.Denominazione?.Trim(),
                Matrice = c.Matrice?.Trim(),
                Imballaggio = c.Imballaggio?.Trim(),
                PuntoPrelievo = c.PuntoPrelievo?.Trim(),
                LuogoPrelievo = string.IsNullOrWhiteSpace(c.LuogoPrelievo) ? null : c.LuogoPrelievo.Trim(),
                TemperaturaRicevimento = c.TemperaturaRicevimento?.Trim(),
                Urgente = c.Urgente,
                Prelevatore = c.Prelevatore?.Trim(),
                AutocontrolloDichiarato = dichiarato,
                RichiesteClienteLette = richieste is not null,
                RichiesteCliente = richieste is not null && richieste.TryGetValue(c.IdCampione, out var rc) ? rc : null,
                IdControllo = t.IdControllo,
                Controllo = t.Controllo?.Trim(),
                Prezzo = t.PrezzoCampione,
            };

            if (perCampione.TryGetValue(c.IdCampione, out var righe))
                foreach (var a in righe)
                    camp.Analisi.Add(new AnalisiAccettazione
                    {
                        Denominazione = a.Denominazione?.Trim(),
                        Parametro = a.Parametro?.Trim(),
                        Metodo = a.Metodo?.Trim(),
                        DenominazioneRiga = a.DenominazioneRiga?.Trim(),
                        Accreditato = a.Accreditato,
                        LaboratorioEsterno = a.LaboratorioEsterno?.Trim(),
                        FraseLabEsterno = a.FraseLabEsterno?.Trim(),
                    });

            ordine.Campioni.Add(camp);
        }

        return ordine;
    }

    /// <summary>Normalizza gli indirizzi email (separatori vari → "; ").</summary>
    private static string? NormalizzaEmail(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parti = raw.Split(new[] { ';', ',', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(s => s.Trim())
                       .Where(s => s.Length > 0)
                       .Distinct(StringComparer.OrdinalIgnoreCase);
        var join = string.Join("; ", parti);
        return join.Length > 0 ? join : null;
    }

    // Filtra prima i lotti del periodo (CTE) e aggrega i conteggi campioni/parametri solo su quel
    // sottoinsieme: evita che le due sottoquery correlate girino riga per riga su tutta la tabella
    // Lotto prima di applicare il filtro data (causa del timeout su intervalli anche brevi).
    private const string SqlLottiRicevuti = """
        WITH LottiPeriodo AS (
            -- Filtri su cliente e insediamento (14/10/2026): parte del nome, come scritta dall'utente.
            SELECT TOP (2000) l.IdLotto, l.CodLotto, l.DataRic, l.IdCommessa
            FROM Lotto l
                LEFT JOIN commessa cm ON cm.IdCommessa = l.IdCommessa
                LEFT JOIN Clienti cl ON CAST(cl.CODContabilita AS varchar(50)) = CAST(cm.Cod_Cliente AS varchar(50))
                LEFT JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
            WHERE (@Dal IS NULL OR l.DataRic >= @Dal)
              AND (@Al  IS NULL OR l.DataRic <  @Al)
              AND (@Cliente IS NULL OR cl.RagioneSociale LIKE '%' + @Cliente + '%')
              AND (@IdCliente IS NULL OR cl.IDCliente = @IdCliente)
              AND (@IdInsediamento IS NULL OR cm.IDInsediamento = @IdInsediamento)
              AND (@Insediamento IS NULL OR i.NomeInsed LIKE '%' + @Insediamento + '%' OR i.IndirizzoInsed LIKE '%' + @Insediamento + '%')
            ORDER BY l.DataRic DESC, l.IdLotto DESC
        )
        SELECT
            lp.IdLotto                                  AS IdLotto,
            CONVERT(nvarchar(20),  lp.CodLotto)         AS CodLotto,
            lp.DataRic                                  AS DataRicezione,
            CONVERT(nvarchar(300), cl.RagioneSociale)   AS Cliente,
            CONVERT(nvarchar(300), i.NomeInsed)         AS Insediamento,
            CONVERT(nvarchar(200), i.[CittàInsed])      AS InsediamentoCitta,
            ISNULL(agg.NumeroCampioni, 0)                AS NumeroCampioni,
            ISNULL(agg.CampioniConParametri, 0)          AS CampioniConParametri
        FROM LottiPeriodo lp
            LEFT JOIN commessa cm ON cm.IdCommessa = lp.IdCommessa
            LEFT JOIN Clienti cl ON CAST(cl.CODContabilita AS varchar(50)) = CAST(cm.Cod_Cliente AS varchar(50))
            LEFT JOIN Insediamento i ON i.IDInsediamento = cm.IDInsediamento
            INNER JOIN (
                SELECT c.IdLotto,
                       COUNT(*) AS NumeroCampioni,
                       SUM(CASE WHEN pr.IdCampione IS NOT NULL THEN 1 ELSE 0 END) AS CampioniConParametri
                FROM Campione c
                    LEFT JOIN (SELECT DISTINCT IdCampione FROM prove) pr ON pr.IdCampione = c.IdCampione
                WHERE c.IdLotto IN (SELECT IdLotto FROM LottiPeriodo)
                GROUP BY c.IdLotto
            ) agg ON agg.IdLotto = lp.IdLotto
        ORDER BY lp.DataRic DESC, lp.IdLotto DESC
        """;

    public async Task<IReadOnlyList<LottoCandidato>> LottiRicevutiAsync(DateTime? dal, DateTime? al, CancellationToken ct = default)
        => await LottiRicevutiAsync(dal, al, new FiltroLotti(), ct);

    public async Task<IReadOnlyList<LottoCandidato>> LottiRicevutiAsync(DateTime? dal, DateTime? al, FiltroLotti filtro, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        // @Al è esclusivo: se indicato, si intende fino a fine giornata → +1 giorno lato controller.
        var righe = await cn.QueryAsync<LottoCandidato>(
            new CommandDefinition(SqlLottiRicevuti, new { Dal = dal, Al = al, filtro.IdCliente, filtro.IdInsediamento, Cliente = filtro.IdCliente is null ? Pulito(filtro.Cliente) : null, Insediamento = filtro.IdInsediamento is null ? Pulito(filtro.Insediamento) : null }, commandTimeout: 120, cancellationToken: ct));
        return righe.ToList();
    }

    public async Task<int?> CampioniDelLottoAsync(int idLotto, CancellationToken ct = default)
    {
        const string sql = """
            SELECT (SELECT COUNT(*) FROM Campione c WHERE c.IdLotto = l.IdLotto)
            FROM Lotto l WHERE l.IdLotto = @IdLotto
            """;
        using var cn = await _fabbrica.ApriAsync(ct);
        return await cn.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(sql, new { IdLotto = idLotto }, cancellationToken: ct));
    }

    /// <summary>Testo di ricerca: vuoto = nessun filtro; i caratteri jolly di LIKE vengono resi letterali.</summary>
    private static string? Pulito(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");

    // Lotti ricevuti dal giorno indicato con almeno un campione e nessun campione senza analisi (= «Pronto»).
    private const string SqlLottiPronti = """
        SELECT l.IdLotto
        FROM Lotto l
        WHERE l.DataRic >= @Dal
          AND EXISTS (SELECT 1 FROM Campione c WHERE c.IdLotto = l.IdLotto)
          AND NOT EXISTS (
                SELECT 1 FROM Campione c
                WHERE c.IdLotto = l.IdLotto
                  AND NOT EXISTS (SELECT 1 FROM prove p WHERE p.IdCampione = c.IdCampione))
        """;

    public async Task<IReadOnlyList<int>> LottiProntiAsync(DateTime dal, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = await cn.QueryAsync<int>(
            new CommandDefinition(SqlLottiPronti, new { Dal = dal.Date }, commandTimeout: 30, cancellationToken: ct));
        return righe.ToList();
    }
}
