using System.Data.OleDb;
using Dapper;
using LaboratorioAI.Dati.ArcLab;
using System.Globalization;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LaboratorioAI.Dati.Provvigioni;

/// <summary>
/// Importa da AZW (Access, contabilità) tutte le fatture ai clienti (con la quota di laboratorio), gli incassi e le note di credito
/// nelle tabelle di lavoro <c>provv.Azw*</c>. AZW è letto SOLO in lettura: il file viene prima copiato
/// in una cartella temporanea e letto dalla copia, così non si crea alcun file di blocco accanto
/// all'originale e non si legge un file in aggiornamento. Il database proprio è riscritto in una sola
/// transazione: o l'importazione riesce tutta o resta la precedente.
///
/// Configurazione: <c>Provvigioni:PercorsoAzw</c> (percorso di rete UNC alla copia notturna, non una lettera di unità).
/// Richiede il provider OLE DB ACE a 64 bit sul computer che esegue l'importazione.
/// </summary>
public sealed class ImportatoreAzw
{
    private static readonly string[] VociPerimetro = { "ANA", "ANE", "AMB", "LEV", "LEC", "LEG" };
    private const string CentroPerimetro = "04";
    private const decimal AliquotaIvaStima = 1.22m;

    private readonly ContestoLaboratorioAI _db;
    private readonly IConfiguration _config;
    private readonly IFabbricaConnessioniArcLab _arcLab;

    public ImportatoreAzw(ContestoLaboratorioAI db, IConfiguration config, IFabbricaConnessioniArcLab arcLab)
    {
        _db = db;
        _config = config;
        _arcLab = arcLab;
    }

    /// <summary>Una sola importazione alla volta (pagine di gestione e pagine personali, 19/10/2026).</summary>
    private static readonly SemaphoreSlim Blocco = new(1, 1);

    /// <summary>
    /// Per le pagine personali (commerciali e consulenti): importa solo se l'ultima importazione riuscita è più vecchia di
    /// <paramref name="intervallo"/> e se non ce n'è già una in corso. Restituisce il messaggio da mostrare.
    /// </summary>
    public async Task<(bool Riuscita, string Messaggio)> AggiornaSeVecchiaAsync(TimeSpan intervallo, CancellationToken ct = default)
    {
        var ultima = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).OrderByDescending(i => i.DataOra).FirstOrDefaultAsync(ct);
        if (ultima is not null && DateTime.UtcNow - ultima.DataOra < intervallo)
            return (true, $"Dati già aggiornati alle {ultima.DataOra.ToLocalTime():HH:mm} del {ultima.DataOra.ToLocalTime():dd/MM}: riprova più tardi.");
        if (Blocco.CurrentCount == 0) return (false, "C'è già un aggiornamento in corso: riprova tra qualche minuto.");
        var esito = await ImportaAsync(ct);
        return esito.Riuscita
            ? (true, $"Dati aggiornati da AZW: {esito.Fatture} fatture, {esito.Incassi} incassi." + (esito.Messaggio is { Length: > 0 } m ? " Attenzione: " + m : ""))
            : (false, "Aggiornamento non riuscito: " + esito.Messaggio);
    }

    public async Task<ImportazioneAzw> ImportaAsync(CancellationToken ct = default)
    {
        await Blocco.WaitAsync(ct);
        try { return await ImportaInternoAsync(ct); }
        finally { Blocco.Release(); }
    }

    private async Task<ImportazioneAzw> ImportaInternoAsync(CancellationToken ct)
    {
        var percorso = _config["Provvigioni:PercorsoAzw"];
        var esito = new ImportazioneAzw { DataOra = DateTime.UtcNow, PercorsoFile = percorso ?? "(non configurato)" };

        string? copia = null;
        try
        {
            if (string.IsNullOrWhiteSpace(percorso)) throw new InvalidOperationException("Impostazione Provvigioni:PercorsoAzw mancante.");
            if (!File.Exists(percorso)) throw new FileNotFoundException("File AZW non trovato.", percorso);
            esito.DataModificaFile = File.GetLastWriteTimeUtc(percorso);

            copia = Path.Combine(Path.GetTempPath(), $"azw_{Guid.NewGuid():N}.accdb");
            File.Copy(percorso, copia, overwrite: true);

            await ServizioSpettanzeConsulenti.PopolaAsync(_db, ct);
            var fornitoriConsulenti = await _db.ConsulentiPercentuale.AsNoTracking()
                .Where(c => c.CodiceFornitore != null).Select(c => c.CodiceFornitore!).ToListAsync(ct);
            var dati = LeggiDaAccess(copia, new DateTime(2017, 1, 1), fornitoriConsulenti);
            var commesse = await LeggiCommesseArcLabAsync(ct);
            var campioni = await LeggiCampioniMensiliArcLabAsync(ct);
            var insediamenti = await LeggiInsediamentiArcLabAsync(ct);
            var campioniIns = await LeggiCampioniInsediamentiArcLabAsync(ct);
            var prestazioniIns = await LeggiPrestazioniInsediamentiArcLabAsync(ct);

            // 22/10/2026: la contabilità analitica non si riscrive se il file AZW è più vecchio di quanto già importato.
            var direzione = dati.Direzione;
            string? motivoDirezione = null;
            if (direzione is not null)
            {
                var righeAttuali = await _db.RegistrazioniAnalitiche.CountAsync(ct);
                var ultimaAttuale = righeAttuali == 0 ? null : await _db.RegistrazioniAnalitiche.MaxAsync(r => (DateTime?)r.DataRegistrazione, ct);
                var ultimaNuova = direzione.Registrazioni.Count == 0 ? (DateTime?)null : direzione.Registrazioni.Max(r => r.DataRegistrazione);
                motivoDirezione = ControlloContabilitaAnalitica.Verifica(ultimaAttuale, righeAttuali, ultimaNuova, direzione.Registrazioni.Count);
                if (motivoDirezione is not null) direzione = null;
            }

            // SqlServerRetryingExecutionStrategy: una transazione manuale va eseguita come unità ripetibile.
            var strategia = _db.Database.CreateExecutionStrategy();
            await strategia.ExecuteAsync(async () =>
            {
                _db.ChangeTracker.Clear();
                await using var tx = await _db.Database.BeginTransactionAsync(ct);
                await _db.AzwIncassi.ExecuteDeleteAsync(ct);
                await _db.AzwNoteCredito.ExecuteDeleteAsync(ct);
                await _db.AzwFatture.ExecuteDeleteAsync(ct);
                await _db.AzwRigheFattura.ExecuteDeleteAsync(ct);
                await _db.AzwRigheNoteCredito.ExecuteDeleteAsync(ct);
                await _db.AzwFattureConsulenti.ExecuteDeleteAsync(ct);
                await _db.CommesseArcLab.ExecuteDeleteAsync(ct);
                await _db.CampioniMensiliArcLab.ExecuteDeleteAsync(ct);
                await _db.InsediamentiArcLab.ExecuteDeleteAsync(ct);
                await _db.CampioniInsediamentiArcLab.ExecuteDeleteAsync(ct);
                await _db.PrestazioniInsediamentiArcLab.ExecuteDeleteAsync(ct);
                await _db.AzwClienti.ExecuteDeleteAsync(ct);
                if (dati.Trasferte is not null) await _db.TrasferteAzw.ExecuteDeleteAsync(ct);
                // Cruscotto Direzione (13/10/2026): si riscrive solo se la contabilità analitica è stata letta.
                if (direzione is not null)
                {
                    await _db.MovimentiAnalitici.ExecuteDeleteAsync(ct);
                    await _db.RegistrazioniAnalitiche.ExecuteDeleteAsync(ct);
                    await _db.CentriCostoAzw.ExecuteDeleteAsync(ct);
                    await _db.CommesseAzw.ExecuteDeleteAsync(ct);
                    await _db.FatturatoClienti.ExecuteDeleteAsync(ct);
                }

                _db.ChangeTracker.AutoDetectChangesEnabled = false;
                _db.AzwFatture.AddRange(dati.Fatture);
                _db.AzwIncassi.AddRange(dati.Incassi);
                _db.AzwNoteCredito.AddRange(dati.NoteCredito);
                _db.AzwRigheFattura.AddRange(dati.RigheFattura);
                _db.AzwRigheNoteCredito.AddRange(dati.RigheNote);
                _db.AzwFattureConsulenti.AddRange(dati.FattureConsulenti);
                _db.CommesseArcLab.AddRange(commesse);
                _db.CampioniMensiliArcLab.AddRange(campioni);
                _db.InsediamentiArcLab.AddRange(insediamenti);
                _db.CampioniInsediamentiArcLab.AddRange(campioniIns);
                _db.PrestazioniInsediamentiArcLab.AddRange(prestazioniIns);
                _db.AzwClienti.AddRange(dati.Anagrafiche);
                if (dati.Trasferte is not null) _db.TrasferteAzw.AddRange(dati.Trasferte);
                if (direzione is not null)
                {
                    _db.MovimentiAnalitici.AddRange(direzione.Movimenti);
                    _db.RegistrazioniAnalitiche.AddRange(direzione.Registrazioni);
                    _db.CentriCostoAzw.AddRange(direzione.Centri);
                    _db.CommesseAzw.AddRange(direzione.Commesse);
                    _db.FatturatoClienti.AddRange(direzione.Fatturato);
                    await AllineaClassificazioniAsync(direzione.Voci, ct);
                }
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.AutoDetectChangesEnabled = true;

                esito.Riuscita = true;
                esito.Fatture = dati.Fatture.Count;
                esito.Incassi = dati.Incassi.Count;
                esito.NoteCredito = dati.NoteCredito.Count;
                if (dati.ErroreDirezione is not null)
                    esito.Messaggio = "Importazione riuscita, ma contabilità analitica (cruscotto Direzione) non letta: " + dati.ErroreDirezione;
                if (motivoDirezione is not null)
                    esito.Messaggio = "Importazione riuscita, ma contabilità analitica NON aggiornata (restano i dati già importati): " + motivoDirezione;
                if (dati.ErroreTrasferte is not null)
                    esito.Messaggio = ((esito.Messaggio ?? "Importazione riuscita") + "; trasferte non lette: " + dati.ErroreTrasferte) is var m && m.Length > 1900 ? m[..1900] : m;
                _db.ImportazioniAzw.Add(esito);
                await _db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            esito.Riuscita = false;
            esito.Messaggio = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
            _db.ChangeTracker.Clear();
            _db.ImportazioniAzw.Add(esito);
            await _db.SaveChangesAsync(ct);
        }
        finally
        {
            if (copia is not null) { try { File.Delete(copia); } catch { /* file temporaneo */ } }
        }
        return esito;
    }

    // ── Lettura da ArcLab (sola lettura) ──────────────────────────────

    /// <summary>
    /// Commesse di consulenza (5) e formazione (4) con un riferimento numerico in <c>CodCommessa</c>
    /// (= <c>mansione.IdMansione</c> del commerciale, «rif. del cliente» in maschera) e il tipo di rapporto
    /// (<c>TipoStampa</c>: 1 continuativo, 2 occasionale, 3 terza parte), più le commesse di analisi (3) dal 2017,
    /// con il riferimento se presente (voce «contratti di analisi»). ArcLab è letto, mai scritto.
    /// </summary>
    private async Task<List<CommessaArcLab>> LeggiCommesseArcLabAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT c.IdCommessa,
                   LTRIM(RTRIM(c.Cod_Cliente))           AS CodiceCliente,
                   c.IdTipoCommessa,
                   c.TipoStampa                          AS TipoRapporto,
                   c.DataCommessa,
                   LEFT(c.DescrIncarico, 500)            AS Descrizione,
                   CASE WHEN c.IdTipoCommessa IN (3, 4, 5) THEN TRY_CONVERT(int, c.CodCommessa) END AS IdMansioneRiferimento,
                   (SELECT TOP 1 ti.ID_Tipo_Incarico FROM dbo.Tipi_Incarico ti
                     WHERE ti.DescrIncarico = c.DescrIncarico ORDER BY ti.ID_Tipo_Incarico) AS IdTipoIncarico
            FROM dbo.commessa c
            WHERE ((c.IdTipoCommessa IN (4, 5) AND TRY_CONVERT(int, c.CodCommessa) IS NOT NULL)
                   OR (c.IdTipoCommessa = 3 AND c.DataCommessa >= '20170101'))
              AND c.Cod_Cliente IS NOT NULL AND LTRIM(RTRIM(c.Cod_Cliente)) <> ''
            """;
        using var cn = await _arcLab.ApriAsync(ct);
        var righe = await cn.QueryAsync<CommessaArcLab>(new CommandDefinition(sql, cancellationToken: ct, commandTimeout: 120));
        return righe.ToList();
    }

    /// <summary>
    /// Campioni di laboratorio (commesse tipo 3) per cliente, mese e controllo dagli ultimi quattro anni, per la pagina
    /// «Clienti da seguire». ArcLab è letto, mai scritto.
    /// </summary>
    private async Task<List<CampioneMensileArcLab>> LeggiCampioniMensiliArcLabAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT LEFT(LTRIM(RTRIM(CAST(cl.CODContabilita AS varchar(50)))), 20) AS CodiceCliente,
                   YEAR(lo.DataRic) AS Anno, MONTH(lo.DataRic) AS Mese,
                   cm.IDControllo AS IdControllo,
                   LEFT(MAX(CAST(co.Controllo AS nvarchar(400))), 200) AS Controllo,
                   COUNT(*) AS Campioni,
                   CAST(SUM(ISNULL(cm.PrezzoControllo, 0)) AS decimal(14, 2)) AS ValoreListino,
                   MAX(lo.DataRic) AS UltimoCampione
            FROM dbo.Campione ca
            JOIN dbo.Lotto lo           ON lo.IdLotto = ca.IdLotto
            JOIN dbo.commessa cm        ON cm.IdCommessa = lo.IdCommessa
            LEFT JOIN dbo.Controllo co  ON co.IDControllo = cm.IDControllo
            JOIN dbo.ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            JOIN dbo.Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
            JOIN dbo.Clienti cl         ON cl.IDCliente = i.IDCliente
            WHERE cm.IdTipoCommessa = 3 AND lo.DataRic >= @Dal AND lo.DataRic IS NOT NULL
              AND cl.CODContabilita IS NOT NULL AND LTRIM(RTRIM(CAST(cl.CODContabilita AS varchar(50)))) <> ''
            GROUP BY LEFT(LTRIM(RTRIM(CAST(cl.CODContabilita AS varchar(50)))), 20), YEAR(lo.DataRic), MONTH(lo.DataRic), cm.IDControllo
            """;
        var dal = new DateTime(DateTime.Today.Year - 4, 1, 1);
        using var cn = await _arcLab.ApriAsync(ct);
        var righe = await cn.QueryAsync<CampioneMensileArcLab>(new CommandDefinition(sql, new { Dal = dal }, cancellationToken: ct, commandTimeout: 300));
        return righe.ToList();
    }

    /// <summary>
    /// Cambi di gestione (12/10/2026): tutti gli insediamenti ArcLab con il loro cliente (anche quelli senza campioni:
    /// il nuovo gestore può non averne ancora). ArcLab è letto, mai scritto.
    /// </summary>
    private async Task<List<InsediamentoArcLab>> LeggiInsediamentiArcLabAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT i.IDInsediamento                                                        AS IdInsediamento,
                   i.IDCliente                                                             AS IdClienteArcLab,
                   NULLIF(LEFT(LTRIM(RTRIM(CAST(cl.CODContabilita AS varchar(50)))), 20), '') AS CodiceCliente,
                   LEFT(ISNULL(CAST(cl.RagioneSociale AS nvarchar(400)), ''), 200)         AS RagioneSociale,
                   NULLIF(LEFT(LTRIM(RTRIM(CAST(cl.PIVA AS nvarchar(50)))), 30), '')        AS PartitaIva,
                   LEFT(CAST(i.NomeInsed AS nvarchar(400)), 255)                           AS NomeInsediamento,
                   LEFT(CAST(i.IndirizzoInsed AS nvarchar(400)), 255)                      AS Indirizzo,
                   LEFT(CAST(i.[CittàInsed] AS nvarchar(200)), 100)                        AS Citta
            FROM dbo.Insediamento i
            JOIN dbo.Clienti cl ON cl.IDCliente = i.IDCliente
            """;
        using var cn = await _arcLab.ApriAsync(ct);
        var righe = await cn.QueryAsync<InsediamentoArcLab>(new CommandDefinition(sql, cancellationToken: ct, commandTimeout: 300));
        return righe.ToList();
    }

    /// <summary>Campioni di laboratorio (commesse tipo 3) per insediamento e mese, dagli ultimi quattro anni.</summary>
    private async Task<List<CampioneInsediamentoArcLab>> LeggiCampioniInsediamentiArcLabAsync(CancellationToken ct)
    {
        var testo = await _db.ImpostazioniAndamento.AsNoTracking().OrderBy(x => x.IdImpostazioniAndamento)
            .Select(x => x.TestoControlloLegionella).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(testo)) testo = "Legionella";
        const string sql = """
            SELECT cc.IDInsediamento AS IdInsediamento,
                   YEAR(lo.DataRic) AS Anno, MONTH(lo.DataRic) AS Mese,
                   COUNT(*) AS Campioni,
                   SUM(CASE WHEN CAST(co.Controllo AS nvarchar(400)) LIKE @Testo THEN 1 ELSE 0 END) AS CampioniLegionella,
                   CAST(SUM(ISNULL(cm.PrezzoControllo, 0)) AS decimal(14, 2)) AS Valore,
                   MIN(lo.DataRic) AS PrimoCampione,
                   MAX(lo.DataRic) AS UltimoCampione
            FROM dbo.Campione ca
            JOIN dbo.Lotto lo           ON lo.IdLotto = ca.IdLotto
            JOIN dbo.commessa cm        ON cm.IdCommessa = lo.IdCommessa
            LEFT JOIN dbo.Controllo co  ON co.IDControllo = cm.IDControllo
            JOIN dbo.ContattoCliente cc ON cc.IDContatto = cm.IDContatto
            WHERE cm.IdTipoCommessa = 3 AND lo.DataRic >= @Dal AND lo.DataRic IS NOT NULL AND cc.IDInsediamento IS NOT NULL
            GROUP BY cc.IDInsediamento, YEAR(lo.DataRic), MONTH(lo.DataRic)
            """;
        var dal = new DateTime(DateTime.Today.Year - 4, 1, 1);
        using var cn = await _arcLab.ApriAsync(ct);
        var righe = await cn.QueryAsync<CampioneInsediamentoArcLab>(new CommandDefinition(sql,
            new { Dal = dal, Testo = "%" + testo.Trim() + "%" }, cancellationToken: ct, commandTimeout: 300));
        return righe.ToList();
    }

    /// <summary>
    /// Cambi di gestione estesi alla consulenza (13/10/2026): prestazioni di consulenza (5) e formazione (4) per sede,
    /// mese e tipo di incarico, dagli ultimi quattro anni. ArcLab è letto, mai scritto.
    /// </summary>
    private async Task<List<PrestazioneInsediamentoArcLab>> LeggiPrestazioniInsediamentiArcLabAsync(CancellationToken ct)
    {
        const string sql = """
            WITH TI AS (
                SELECT DescrIncarico, MIN(ID_Tipo_Incarico) AS IdTipoIncarico
                FROM dbo.Tipi_Incarico GROUP BY DescrIncarico
            ), P AS (
                SELECT cc.IDInsediamento, cm.IdTipoCommessa, ti.IdTipoIncarico,
                       COALESCE(pr.DataFattura, pr.DataFinePres, pr.DataInizioPres) AS DataPrestazione,
                       ISNULL(cm.PrezzoControllo, 0) AS Valore
                FROM dbo.Prestazioni pr
                JOIN dbo.commessa cm        ON cm.IdCommessa = pr.IdCommessa
                JOIN dbo.ContattoCliente cc ON cc.IDContatto = cm.IDContatto
                LEFT JOIN TI ti             ON ti.DescrIncarico = cm.DescrIncarico
                WHERE cm.IdTipoCommessa IN (4, 5) AND cc.IDInsediamento IS NOT NULL
            )
            SELECT IDInsediamento AS IdInsediamento, YEAR(DataPrestazione) AS Anno, MONTH(DataPrestazione) AS Mese,
                   IdTipoCommessa, IdTipoIncarico, COUNT(*) AS Prestazioni,
                   CAST(SUM(Valore) AS decimal(14, 2)) AS Valore,
                   MIN(DataPrestazione) AS PrimaPrestazione, MAX(DataPrestazione) AS UltimaPrestazione
            FROM P
            WHERE DataPrestazione >= @Dal AND DataPrestazione <= @Fino
            GROUP BY IDInsediamento, YEAR(DataPrestazione), MONTH(DataPrestazione), IdTipoCommessa, IdTipoIncarico
            """;
        var dal = new DateTime(DateTime.Today.Year - 4, 1, 1);
        using var cn = await _arcLab.ApriAsync(ct);
        var righe = await cn.QueryAsync<PrestazioneInsediamentoArcLab>(new CommandDefinition(sql,
            new { Dal = dal, Fino = DateTime.Today.AddYears(1) }, cancellationToken: ct, commandTimeout: 300));
        return righe.ToList();
    }

    // ── Lettura da Access ─────────────────────────────────────────────

    private sealed record DatiAzw(List<AzwFattura> Fatture, List<AzwIncasso> Incassi, List<AzwNotaCredito> NoteCredito,
        List<AzwRigaFattura> RigheFattura, List<AzwRigaNotaCredito> RigheNote, List<AzwCliente> Anagrafiche)
    {
        public DatiDirezione? Direzione { get; init; }
        public string? ErroreDirezione { get; init; }
        public List<AzwFatturaConsulente> FattureConsulenti { get; init; } = [];
        /// <summary>22/10/2026: trasferte (Viaggi); null se la tabella non si legge (l'importazione prosegue).</summary>
        public List<TrasfertaAzw>? Trasferte { get; init; }
        public string? ErroreTrasferte { get; init; }
    }

    private sealed record DatiDirezione(List<MovimentoAnaliticoMensile> Movimenti, List<(string Codice, string? Descrizione, bool Ricavo)> Voci,
        List<CentroCostoAzw> Centri, List<CommessaAzw> Commesse, List<FatturatoClienteMensile> Fatturato)
    {
        /// <summary>Consuntivo per area (20/10/2026): righe senza somme.</summary>
        public List<RegistrazioneAnalitica> Registrazioni { get; init; } = [];
    }

    /// <summary>
    /// Voci AZW nella tabella delle classificazioni: le nuove si aggiungono con la classificazione proposta (regole di
    /// Power BI) e «da verificare»; di quelle esistenti si aggiornano solo descrizione e tipo ricavo/costo, mai la
    /// classificazione scelta da una persona.
    /// </summary>
    private async Task AllineaClassificazioniAsync(List<(string Codice, string? Descrizione, bool Ricavo)> voci, CancellationToken ct)
    {
        var esistenti = await _db.ClassificazioniVoci.ToDictionaryAsync(x => x.CodiceVoce, StringComparer.OrdinalIgnoreCase, ct);
        var primaVolta = esistenti.Count == 0;
        var ora = DateTime.UtcNow;
        foreach (var (codice, descrizione, ricavo) in voci)
        {
            if (esistenti.TryGetValue(codice, out var c))
            {
                if (c.Descrizione != descrizione || c.Ricavo != ricavo)
                {
                    c.Descrizione = descrizione; c.Ricavo = ricavo; c.DataUltimaModifica = ora;
                    if (ricavo) { c.Macrocategoria = Core.Direzione.ClassificatoreVociAzw.MacroRicavo; c.TipoCosto = null; c.MateriaPrima = false; }
                }
                continue;
            }
            var (tipo, macro, mp) = Core.Direzione.ClassificatoreVociAzw.Proponi(codice, descrizione, ricavo);
            var nuova = new ClassificazioneVoce
            {
                CodiceVoce = codice, Descrizione = descrizione, Ricavo = ricavo, TipoCosto = tipo, Macrocategoria = macro,
                MateriaPrima = mp, DaVerificare = !primaVolta, DataCreazione = ora, DataUltimaModifica = ora
            };
            _db.ClassificazioniVoci.Add(nuova);
            esistenti[codice] = nuova;
        }
    }

    /// <summary>
    /// Contabilità analitica per il cruscotto Direzione: tabella <c>dati_importati_preconsuntivo</c> (ricaricata ogni notte
    /// dalla macro Access <c>mcrAggiornaDBF</c>, la stessa di Power BI), sommata per mese, centro, voce, commessa e conto;
    /// più voci (VOCICOST), centri di costo (CENTCOST) e commesse (COMMESSE).
    /// </summary>
    private static DatiDirezione LeggiDirezione(OleDbConnection conn)
    {
        var movimenti = new List<MovimentoAnaliticoMensile>();
        const string sql =
            "SELECT YEAR(DATREG) AS A, MONTH(DATREG) AS M, CENTRO, VOCE, COMMESSA, CONTO, SUM(IMPORTO_SEGNO) AS I, COUNT(*) AS N " +
            "FROM dati_importati_preconsuntivo WHERE DATREG IS NOT NULL " +
            "GROUP BY YEAR(DATREG), MONTH(DATREG), CENTRO, VOCE, COMMESSA, CONTO";
        foreach (var r in Leggi(conn, sql))
        {
            var importo = Math.Round(Decimale(r[6]), 2);
            if (importo == 0m) continue;
            movimenti.Add(new MovimentoAnaliticoMensile
            {
                Anno = Convert.ToInt32(r[0], CultureInfo.InvariantCulture), Mese = Convert.ToInt32(r[1], CultureInfo.InvariantCulture),
                Centro = Taglia(Testo(r[2]), 10)!, Voce = Taglia(Testo(r[3]), 10)!, Commessa = Taglia(Testo(r[4]), 20)!,
                Conto = Taglia(Testo(r[5]), 20)!, Importo = importo, Righe = Convert.ToInt32(r[7], CultureInfo.InvariantCulture)
            });
        }
        var voci = Leggi(conn, "SELECT CODICE, [DESC], TIPO FROM VOCICOST")
            .Where(r => Testo(r[0]).Length > 0)
            .Select(r => (Taglia(Testo(r[0]), 10)!, Taglia(NullSeVuoto(r[1]), 100), Testo(r[2]).Equals("R", StringComparison.OrdinalIgnoreCase)))
            .GroupBy(x => x.Item1, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        var centri = Leggi(conn, "SELECT CODICE, [DESC] FROM CENTCOST").Where(r => Testo(r[0]).Length > 0)
            .Select(r => new CentroCostoAzw { Codice = Taglia(Testo(r[0]), 10)!, Descrizione = Taglia(NullSeVuoto(r[1]), 100) }).ToList();
        var commesse = Leggi(conn, "SELECT CODICE, [DESC] FROM COMMESSE").Where(r => Testo(r[0]).Length > 0)
            .Select(r => new CommessaAzw { Codice = Taglia(Testo(r[0]), 20)!, Descrizione = Taglia(NullSeVuoto(r[1]), 100) }).ToList();
        // Fatturato per cliente: righe di ricavo (voci di tipo R) della contabilità analitica sul documento del cliente.
        var vociRicavo = voci.Where(v => v.Item3).Select(v => v.Item1).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fatturato = new Dictionary<(int, int, string, string, string, string), decimal>();
        const string sqlFatt =
            "SELECT YEAR(t.DATREG) AS A, MONTH(t.DATREG) AS M, IIF(t.CLFR='C', t.CODCF, '') AS C, d.CENTRO, d.VOCE, d.COMMESSA, " +
            "SUM(IIF(d.SEGNO='D', -d.IMPORTO, d.IMPORTO)) AS I " +
            "FROM TESTANA AS t INNER JOIN DETTANA AS d ON (t.NUMREG = d.NUMREG AND t.ANNREG = d.ANNREG) " +
            "WHERE t.DATREG IS NOT NULL " +
            "GROUP BY YEAR(t.DATREG), MONTH(t.DATREG), IIF(t.CLFR='C', t.CODCF, ''), d.CENTRO, d.VOCE, d.COMMESSA";
        foreach (var r in Leggi(conn, sqlFatt))
        {
            var voce = Taglia(Testo(r[4]), 10)!;
            if (!vociRicavo.Contains(voce)) continue;
            var chiave = (Convert.ToInt32(r[0], CultureInfo.InvariantCulture), Convert.ToInt32(r[1], CultureInfo.InvariantCulture),
                Taglia(Testo(r[2]), 20)!, Taglia(Testo(r[3]), 10)!, voce, Taglia(Testo(r[5]), 20)!);
            fatturato[chiave] = (fatturato.TryGetValue(chiave, out var x) ? x : 0m) + Decimale(r[6]);
        }
        var righeFatt = fatturato.Where(f => Math.Round(f.Value, 2) != 0m).Select(f => new FatturatoClienteMensile
        {
            Anno = f.Key.Item1, Mese = f.Key.Item2, CodiceCliente = f.Key.Item3, Centro = f.Key.Item4, Voce = f.Key.Item5,
            Commessa = f.Key.Item6, Importo = Math.Round(f.Value, 2)
        }).ToList();
        // Consuntivo per area (20/10/2026): le stesse righe, una per una (periodo a giorni e dettaglio registrazioni).
        var registrazioni = new List<RegistrazioneAnalitica>();
        const string sqlReg =
            "SELECT DATREG, ANNREG, NUMREG, TIPDOC, NUMDOC, CENTRO, VOCE, COMMESSA, CONTO, DESCRIGA, IMPORTO_SEGNO " +
            "FROM dati_importati_preconsuntivo WHERE DATREG IS NOT NULL";
        foreach (var r in Leggi(conn, sqlReg))
        {
            var d = Data(r[0]);
            var importo = Math.Round(Decimale(r[10]), 2);
            if (d is null || importo == 0m) continue;
            int.TryParse(Numero(r[1]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var annoReg);
            int.TryParse(Numero(r[2]), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numReg);
            registrazioni.Add(new RegistrazioneAnalitica
            {
                DataRegistrazione = d.Value.Date, AnnoRegistrazione = annoReg, NumeroRegistrazione = numReg,
                TipoDocumento = Taglia(NullSeVuoto(r[3]), 5), NumeroDocumento = Taglia(NullSeVuoto(Numero(r[4])), 20),
                Centro = Taglia(Testo(r[5]), 10)!, Voce = Taglia(Testo(r[6]), 10)!, Commessa = Taglia(Testo(r[7]), 20)!,
                Conto = Taglia(Testo(r[8]), 20)!, Descrizione = Taglia(NullSeVuoto(r[9]), 200), Importo = importo
            });
        }
        return new DatiDirezione(movimenti, voci, centri, commesse, righeFatt) { Registrazioni = registrazioni };
    }

    private static DatiAzw LeggiDaAccess(string file, DateTime dal, IReadOnlyCollection<string> fornitoriConsulenti)
    {
        var cs = $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={file};Mode=Read;";
        using var conn = new OleDbConnection(cs);
        conn.Open();

        var data = $"#{dal:yyyy-MM-dd}#";

        // Venditore corrente e ragione sociale del cliente.
        var clienti = new Dictionary<string, (string? Rag, string? Vet)>(StringComparer.Ordinal);
        // Anagrafica completa (per i preventivi di consulenza: SDI, PEC, codice fiscale, indirizzo).
        var anagrafiche = new List<AzwCliente>();
        // ZONA = sigla del responsabile cliente (tabella delle sigle in Amministrazione → Responsabili clienti).
        // Se in questa copia di AZW il campo ZONA non esistesse, l'importazione prosegue senza (zona vuota).
        const string sqlAnagrafica = "SELECT CODCF, RAGSOC, CODVET, PARTIVA, CODFISC, INDIR, CAP, LOCAL, PROV, EMAIL, PEC_FE, CODSDI, TEL{0} FROM ANACF WHERE CLFR='C'";
        List<object[]> righeAnacf;
        try { righeAnacf = Leggi(conn, string.Format(sqlAnagrafica, ", ZONA")).ToList(); }
        catch (OleDbException) { righeAnacf = Leggi(conn, string.Format(sqlAnagrafica, ", NULL AS ZONA")).ToList(); }
        foreach (var r in righeAnacf)
        {
            clienti[Testo(r[0])] = (NullSeVuoto(r[1]), NullSeVuoto(r[2]));
            anagrafiche.Add(new AzwCliente
            {
                CodiceCliente = Testo(r[0]), RagioneSociale = Taglia(NullSeVuoto(r[1]), 200), PartitaIva = Taglia(NullSeVuoto(r[3]), 20),
                CodiceFiscale = Taglia(NullSeVuoto(r[4]), 20), Indirizzo = Taglia(NullSeVuoto(r[5]), 255), Cap = Taglia(NullSeVuoto(r[6]), 10),
                Citta = Taglia(NullSeVuoto(r[7]), 100), Provincia = Taglia(NullSeVuoto(r[8]), 4), Email = Taglia(NullSeVuoto(r[9]), 250),
                Pec = Taglia(NullSeVuoto(r[10]), 250), CodiceSdi = Taglia(NullSeVuoto(r[11]), 10), Telefono = Taglia(NullSeVuoto(r[12]), 50),
                Zona = Taglia(NullSeVuoto(r[13])?.ToUpperInvariant(), 20),
                CodiceVenditore = Taglia(NullSeVuoto(r[2])?.ToUpperInvariant(), 20)
            });
        }

        // Fatture: importi per documento, centro di costo e voce. Da qui si ricavano il totale, la quota di
        // laboratorio (perimetro storico) e le righe che servono alle basi di calcolo configurabili.
        var fatture = new Dictionary<string, AzwFattura>(StringComparer.Ordinal);
        var righe = new Dictionary<(string Chiave, string Centro, string Voce, string Commessa), decimal>();
        // COMMESSA della riga (19/10/2026) = chi ha svolto il lavoro: serve alle spettanze dei consulenti.
        var sqlFatture =
            "SELECT t.CODCF, t.ANNDOC, t.NUMDOC, t.DATREG, d.CENTRO, d.VOCE, SUM(d.IMPORTO) AS N, d.COMMESSA " +
            "FROM TESTANA AS t INNER JOIN DETTANA AS d ON (t.NUMREG = d.NUMREG AND t.ANNREG = d.ANNREG) " +
            $"WHERE t.CLFR='C' AND t.TIPDOC IN ('FE','F2','F5') AND d.SEGNO='A' AND t.DATREG >= {data} " +
            "GROUP BY t.CODCF, t.ANNDOC, t.NUMDOC, t.DATREG, d.CENTRO, d.VOCE, d.COMMESSA";
        foreach (var r in Leggi(conn, sqlFatture))
        {
            var cod = Testo(r[0]);
            var data_ = Data(r[3]);
            if (data_ is null) continue;
            var chiave = $"{cod}|{Numero(r[1])}|{Numero(r[2])}";
            var centro = Testo(r[4]);
            var voce = Testo(r[5]);
            var importo = Decimale(r[6]);
            var diPerimetro = centro == CentroPerimetro && VociPerimetro.Contains(voce);

            if (!fatture.TryGetValue(chiave, out var f))
            {
                clienti.TryGetValue(cod, out var cli);
                f = new AzwFattura
                {
                    Chiave = chiave, CodiceCliente = cod, RagioneSociale = cli.Rag, CodiceVenditore = cli.Vet,
                    DataFattura = data_.Value.Date
                };
                fatture[chiave] = f;
            }
            f.NettoTotale += importo;
            if (diPerimetro) f.NettoPerimetro += importo;
            var commessa = Testo(r[7]).ToUpperInvariant();
            righe[(chiave, centro, voce, commessa)] = righe.GetValueOrDefault((chiave, centro, voce, commessa)) + importo;
        }
        var righeFattura = righe.Where(x => x.Value != 0m)
            .Select(x => new AzwRigaFattura
            {
                ChiaveFattura = x.Key.Chiave, Centro = x.Key.Centro, Voce = x.Key.Voce,
                Commessa = x.Key.Commessa.Length == 0 ? null : Taglia(x.Key.Commessa, 20), Netto = x.Value
            })
            .ToList();

        // Partitario: lordo delle fatture e incassi (causale 140 in avere). La causale 140 in dare è uno storno
        // dell'incasso (pagamento in eccesso restituito, incasso annullato): entra come incasso negativo.
        // Gli insoluti in AZW non hanno una causale: la fattura resta aperta, senza incasso, e non genera provvigione.
        var lordi = new Dictionary<string, decimal>(StringComparer.Ordinal);
        var incassi = new List<AzwIncasso>();
        var sqlPartitario =
            "SELECT CODCF, ANNPAR, NUMPAR, SEGNO, TIPDOC, CODCAU, DATPAR, IMPTOT FROM MOVPAR " +
            "WHERE (SEGNO='D' AND TIPDOC IN ('FE','F2','F5')) OR Val(CODCAU & '')=140";
        foreach (var r in Leggi(conn, sqlPartitario))
        {
            var chiave = $"{Testo(r[0])}|{Numero(r[1])}|{Numero(r[2])}";
            if (!fatture.ContainsKey(chiave)) continue;
            var segno = Testo(r[3]);
            var importo = Decimale(r[7]);
            var causale140 = Testo(r[5]).Trim() == "140";
            if (segno == "D" && !causale140) lordi[chiave] = lordi.GetValueOrDefault(chiave) + importo;
            else if (causale140)
            {
                var d = Data(r[6]);
                if (d is null) continue;   // date non valide nel partitario: scartate
                incassi.Add(new AzwIncasso
                {
                    ChiaveFattura = chiave, DataIncasso = d.Value.Date, ImportoLordo = segno == "D" ? -importo : importo
                });
            }
        }
        foreach (var f in fatture.Values)
        {
            if (lordi.TryGetValue(f.Chiave, out var l) && l > 0m) f.Lordo = l;
            else { f.Lordo = Math.Round(f.NettoTotale * AliquotaIvaStima, 2); f.LordoStimato = true; }
        }

        // Note di credito: importi per nota, centro di costo e voce (quota di laboratorio, totale e righe).
        var noteTot = new Dictionary<(string Cliente, string Rif, DateTime Data), AzwNotaCredito>();
        var righeN = new Dictionary<(string Cliente, string Rif, DateTime Data, string Centro, string Voce, string Commessa), decimal>();
        var sqlNote =
            "SELECT t.CODCF, t.ANNDOC, t.NUMDOC, t.DATREG, d.CENTRO, d.VOCE, SUM(d.IMPORTO) AS N, d.COMMESSA " +
            "FROM TESTANA AS t INNER JOIN DETTANA AS d ON (t.NUMREG = d.NUMREG AND t.ANNREG = d.ANNREG) " +
            $"WHERE t.CLFR='C' AND t.TIPDOC IN ('CE','C1') AND d.SEGNO='D' AND t.DATREG >= {data} " +
            "GROUP BY t.CODCF, t.ANNDOC, t.NUMDOC, t.DATREG, d.CENTRO, d.VOCE, d.COMMESSA";
        foreach (var r in Leggi(conn, sqlNote))
        {
            var d = Data(r[3]);
            if (d is null) continue;
            var cod = Testo(r[0]);
            var rif = $"NC {Numero(r[2])}/{Numero(r[1])}";
            var centro = Testo(r[4]);
            var voce = Testo(r[5]);
            var importo = Decimale(r[6]);
            var k = (cod, rif, d.Value.Date);
            if (!noteTot.TryGetValue(k, out var n))
            {
                n = new AzwNotaCredito { CodiceCliente = cod, Riferimento = rif, Data = d.Value.Date };
                noteTot[k] = n;
            }
            n.NettoTotale += importo;
            if (centro == CentroPerimetro && VociPerimetro.Contains(voce)) n.NettoPerimetro += importo;
            var commN = Testo(r[7]).ToUpperInvariant();
            righeN[(cod, rif, d.Value.Date, centro, voce, commN)] = righeN.GetValueOrDefault((cod, rif, d.Value.Date, centro, voce, commN)) + importo;
        }
        var note = noteTot.Values.Where(n => n.NettoTotale > 0m || n.NettoPerimetro > 0m).ToList();
        var righeNote = righeN.Where(x => x.Value != 0m)
            .Select(x => new AzwRigaNotaCredito
            {
                CodiceCliente = x.Key.Cliente, Riferimento = x.Key.Rif, Data = x.Key.Data,
                Centro = x.Key.Centro, Voce = x.Key.Voce,
                Commessa = x.Key.Commessa.Length == 0 ? null : Taglia(x.Key.Commessa, 20), Netto = x.Value
            }).ToList();

        // Spettanze consulenti (19/10/2026): fatture dei consulenti a MARE.A (fatture fornitore), cioè gli acconti.
        var fattureConsulenti = new List<AzwFatturaConsulente>();
        var fornitori = fornitoriConsulenti.Where(f => f.Length > 0 && f.All(c => char.IsLetterOrDigit(c))).Distinct().ToList();
        if (fornitori.Count > 0)
        {
            var elenco = string.Join(",", fornitori.Select(f => $"'{f}'"));
            var sqlForn =
                "SELECT t.CODCF, t.ANNDOC, t.NUMDOC, t.ANNREG, t.NUMREG, t.DATREG, d.CENTRO, d.VOCE, d.SEGNO, SUM(d.IMPORTO) AS N " +
                "FROM TESTANA AS t INNER JOIN DETTANA AS d ON (t.NUMREG = d.NUMREG AND t.ANNREG = d.ANNREG) " +
                $"WHERE t.CLFR='F' AND t.CODCF IN ({elenco}) AND t.DATREG >= {data} " +
                "GROUP BY t.CODCF, t.ANNDOC, t.NUMDOC, t.ANNREG, t.NUMREG, t.DATREG, d.CENTRO, d.VOCE, d.SEGNO";
            foreach (var r in Leggi(conn, sqlForn))
            {
                var d = Data(r[5]);
                if (d is null) continue;
                var num = Numero(r[2]);
                var doc = num.Length > 0 ? $"{num}/{Numero(r[1])}" : $"reg. {Numero(r[4])}/{Numero(r[3])}";
                var imp = Decimale(r[9]) * (Testo(r[8]) == "A" ? -1m : 1m);
                if (imp == 0m) continue;
                fattureConsulenti.Add(new AzwFatturaConsulente
                {
                    CodiceFornitore = Testo(r[0]), Documento = Taglia(doc, 60)!, DataRegistrazione = d.Value.Date,
                    Centro = Taglia(Testo(r[6]), 10)!, Voce = Taglia(Testo(r[7]), 10)!, Importo = imp
                });
            }
        }

        // Cruscotto Direzione: se la tabella manca o non si legge, il resto dell'importazione procede lo stesso.
        DatiDirezione? direzione = null; string? erroreDirezione = null;
        try { direzione = LeggiDirezione(conn); }
        catch (Exception ex) when (ex is OleDbException or InvalidOperationException or FormatException or InvalidCastException) { erroreDirezione = ex.Message; }

        // Trasferte (22/10/2026): come per la Direzione, un errore qui non blocca il resto.
        List<TrasfertaAzw>? trasferte = null; string? erroreTrasferte = null;
        try { trasferte = LeggiTrasferte(conn); }
        catch (Exception ex) when (ex is OleDbException or InvalidOperationException or FormatException or InvalidCastException) { erroreTrasferte = ex.Message; }

        return new DatiAzw(fatture.Values.ToList(), incassi, note, righeFattura, righeNote, anagrafiche)
        {
            FattureConsulenti = fattureConsulenti,
            Direzione = direzione,
            ErroreDirezione = erroreDirezione,
            Trasferte = trasferte,
            ErroreTrasferte = erroreTrasferte
        };
    }

    /// <summary>
    /// Trasferte (22/10/2026): tabella Viaggi (collegata in AZW_07 a AZW_07_Dati.accdb) con MEZZI e Operatori_commerciale,
    /// come la query «Trasferte» della maschera ConteggioKm. Sola lettura.
    /// </summary>
    private static List<TrasfertaAzw> LeggiTrasferte(OleDbConnection conn)
    {
        const string sql =
            "SELECT v.IDViaggi, v.Operatore, o.Operatore AS NomeOperatore, v.[Data], v.Distanza, v.[Costo Auto Kmetrico], " +
            "v.[Costo Autostrada], v.Vitto_Alloggio, v.Costi_Diversi, v.Mezzo, me.Mezzo AS NomeMezzo, me.TipoMezzo, v.[Note] " +
            "FROM (Viaggi AS v LEFT JOIN Operatori_commerciale AS o ON v.Operatore = o.IDOperatore) " +
            "LEFT JOIN MEZZI AS me ON v.Mezzo = me.ID";
        var r = new List<TrasfertaAzw>();
        foreach (var x in Leggi(conn, sql))
        {
            if (Data(x[3]) is not DateTime d) continue;
            r.Add(new TrasfertaAzw
            {
                IdViaggioAzw = Convert.ToInt32(x[0], CultureInfo.InvariantCulture),
                IdOperatoreAzw = x[1] is DBNull or null ? null : Convert.ToInt32(x[1], CultureInfo.InvariantCulture),
                Operatore = Taglia(NullSeVuoto(x[2]), 150), Data = d.Date, Km = Decimale(x[4]), CostoKm = Decimale(x[5]),
                Autostrada = Decimale(x[6]), VittoAlloggio = Decimale(x[7]), CostiDiversi = Decimale(x[8]),
                IdMezzoAzw = x[9] is DBNull or null ? null : Convert.ToInt32(x[9], CultureInfo.InvariantCulture),
                Mezzo = Taglia(NullSeVuoto(x[10]), 100),
                TipoMezzo = x[11] is DBNull or null ? null : Convert.ToInt32(x[11], CultureInfo.InvariantCulture),
                Note = Taglia(NullSeVuoto(x[12]), 1000)
            });
        }
        return r;
    }

    private static IEnumerable<object[]> Leggi(OleDbConnection conn, string sql)
    {
        using var cmd = new OleDbCommand(sql, conn) { CommandTimeout = 300 };
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            var riga = new object[rd.FieldCount];
            rd.GetValues(riga);
            yield return riga;
        }
    }

    // ── Conversioni tolleranti (Access restituisce DBNull, double, testo con spazi…) ──

    private static string Testo(object o) => o is DBNull or null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture)!.Trim();
    private static string? NullSeVuoto(object o) { var t = Testo(o); return t.Length == 0 ? null : t; }
    private static string? Taglia(string? s, int n) => s is null || s.Length <= n ? s : s[..n];

    /// <summary>Numero di documento normalizzato: 105, 105.0 e "105" diventano "105"; testi come "105/B" restano invariati.</summary>
    private static string Numero(object o)
    {
        if (o is DBNull or null) return "";
        if (o is double dd) return ((long)Math.Round(dd)).ToString(CultureInfo.InvariantCulture);
        if (o is decimal dm) return ((long)Math.Round(dm)).ToString(CultureInfo.InvariantCulture);
        var t = Convert.ToString(o, CultureInfo.InvariantCulture)!.Trim();
        if (t.EndsWith(".0", StringComparison.Ordinal)) t = t[..^2];
        return t;
    }

    private static decimal Decimale(object o)
    {
        if (o is DBNull or null) return 0m;
        if (o is decimal d) return d;
        if (o is double db) return (decimal)db;
        return decimal.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static DateTime? Data(object o)
    {
        if (o is DateTime d) return d;
        return DateTime.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.None, out var v) ? v : null;
    }
}
