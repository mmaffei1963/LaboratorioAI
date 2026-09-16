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
            CONVERT(nvarchar(300), cl.RagioneSociale)  AS ClienteRagioneSociale,
            CONVERT(nvarchar(300), cl.IndirizzoFatt)   AS ClienteIndirizzo,
            CONVERT(nvarchar(30),  cl.CapFatt)         AS ClienteCap,
            CONVERT(nvarchar(200), cl.[CittàFatt])     AS ClienteCitta,
            CONVERT(nvarchar(50),  cl.ProvinciaFatt)   AS ClienteProvincia,
            CONVERT(nvarchar(30),  cl.PIVA)            AS ClientePartitaIva,
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
            CONVERT(nvarchar(50),  c.TempRic)         AS TemperaturaRicevimento,
            CASE WHEN ISNULL(c.Urgente, 0) = 1 THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END AS Urgente
        FROM Campione c
            LEFT JOIN Matrice mt    ON mt.IdMatrice = c.IdMatrice
            LEFT JOIN Imballaggio im ON im.IdImballaggio = c.IdImballaggio
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
        public string? ClienteRagioneSociale { get; set; }
        public string? ClienteIndirizzo { get; set; }
        public string? ClienteCap { get; set; }
        public string? ClienteCitta { get; set; }
        public string? ClienteProvincia { get; set; }
        public string? ClientePartitaIva { get; set; }
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
        public string? TemperaturaRicevimento { get; set; }
        public bool Urgente { get; set; }
    }

    private sealed class RigaAnalisi
    {
        public int IdCampione { get; set; }
        public string? Denominazione { get; set; }
        public string? Parametro { get; set; }
        public string? Metodo { get; set; }
        public string? DenominazioneRiga { get; set; }
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
            ClienteRagioneSociale = t.ClienteRagioneSociale?.Trim(),
            ClienteIndirizzo = t.ClienteIndirizzo?.Trim(),
            ClienteCap = t.ClienteCap?.Trim(),
            ClienteCitta = t.ClienteCitta?.Trim(),
            ClienteProvincia = t.ClienteProvincia?.Trim(),
            ClientePartitaIva = t.ClientePartitaIva?.Trim(),
            Insediamento = t.Insediamento?.Trim(),
            InsediamentoIndirizzo = t.InsediamentoIndirizzo?.Trim(),
            InsediamentoCap = t.InsediamentoCap?.Trim(),
            InsediamentoCitta = t.InsediamentoCitta?.Trim(),
            InsediamentoProvincia = t.InsediamentoProvincia?.Trim(),
            InsediamentoEmail = NormalizzaEmail(t.InsediamentoEmail),
        };

        foreach (var c in campioni)
        {
            var codCamp = (c.CodCampione ?? "").Trim();
            var camp = new CampioneAccettazione
            {
                IdCampione = c.IdCampione,
                CodCampione = codCamp,
                Riferimento = string.IsNullOrEmpty(codCamp) ? codLotto : $"{codLotto}-{codCamp}",
                Denominazione = c.Denominazione?.Trim(),
                Matrice = c.Matrice?.Trim(),
                Imballaggio = c.Imballaggio?.Trim(),
                PuntoPrelievo = c.PuntoPrelievo?.Trim(),
                TemperaturaRicevimento = c.TemperaturaRicevimento?.Trim(),
                Urgente = c.Urgente,
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

    private const string SqlLottiRicevuti = """
        SELECT
            l.IdLotto                                  AS IdLotto,
            CONVERT(nvarchar(20),  l.CodLotto)         AS CodLotto,
            l.DataRic                                  AS DataRicezione,
            CONVERT(nvarchar(300), cl.RagioneSociale)  AS Cliente,
            (SELECT COUNT(*) FROM Campione c WHERE c.IdLotto = l.IdLotto) AS NumeroCampioni,
            (SELECT COUNT(*) FROM Campione c
              WHERE c.IdLotto = l.IdLotto
                AND EXISTS (SELECT 1 FROM prove pr WHERE pr.IdCampione = c.IdCampione)) AS CampioniConParametri
        FROM Lotto l
            LEFT JOIN commessa cm ON cm.IdCommessa = l.IdCommessa
            LEFT JOIN Clienti cl ON CAST(cl.CODContabilita AS varchar(50)) = CAST(cm.Cod_Cliente AS varchar(50))
        WHERE (@Dal IS NULL OR l.DataRic >= @Dal)
          AND (@Al  IS NULL OR l.DataRic <  @Al)
          AND EXISTS (SELECT 1 FROM Campione c WHERE c.IdLotto = l.IdLotto)
        ORDER BY l.DataRic DESC, l.IdLotto DESC
        """;

    public async Task<IReadOnlyList<LottoCandidato>> LottiRicevutiAsync(DateTime? dal, DateTime? al, CancellationToken ct = default)
    {
        using var cn = await _fabbrica.ApriAsync(ct);
        // @Al è esclusivo: se indicato, si intende fino a fine giornata → +1 giorno lato controller.
        var righe = await cn.QueryAsync<LottoCandidato>(
            new CommandDefinition(SqlLottiRicevuti, new { Dal = dal, Al = al }, commandTimeout: 120, cancellationToken: ct));
        return righe.ToList();
    }
}
