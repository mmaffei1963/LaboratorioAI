using Dapper;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Dati.ArcLab.Repository;

/// <inheritdoc cref="IRepositoryStatistiche" />
///
/// <remarks>
/// Origine: query Access fornita da Mirko (29/09/2026), trasformata in T-SQL
/// parametrizzato. Grana della riga: una prova (parametro analizzato) di un
/// campione — non il campione stesso, perché un campione ha più prove.
///
/// Semplificazioni rispetto alla query Access originale:
/// - GROUP BY senza funzioni di aggregazione → in Access equivale a un
///   semplice SELECT (con effetto DISTINCT); qui è una SELECT piana con
///   IdProva incluso, che è già univoco per riga.
/// - HAVING (che in Access filtrava, non aggregava) → tradotto in WHERE.
/// - Il join a GiudizioMatrice/GiudCamp (giudizio di matrice) è stato
///   omesso: nella query originale non era né filtrato né mostrato con un
///   testo leggibile (solo IdGiudizio), e trattandosi di un INNER JOIN
///   avrebbe escluso le prove dei campioni privi di giudizio.
/// - Il filtro "matrice.IdMatrice &lt;&gt; 61" presente nella query originale
///   è stato rimosso su richiesta (non più intenzionale).
///
/// Struttura in due passaggi (importante per le prestazioni): prima si
/// individuano i CAMPIONI che rispettano periodo/cliente/insediamento/
/// commessa/matrice/certificato/numero campione (CTE CampioniFiltrati),
/// POI si espande sulle prove (prove/rigametodo/Metodo/Parametro), che è
/// la parte "pesante" perché ogni campione ha più prove. Fatto nell'ordine
/// inverso (come nella prima versione), su un periodo di qualche settimana
/// la query attraversava le prove dell'intero laboratorio prima di
/// scartare la quasi totalità con il filtro su descrizione incarico,
/// causando timeout (110-120s, poi anche oltre con periodi più ampi).
/// </remarks>
public class RepositoryStatistiche : IRepositoryStatistiche
{
    private readonly IFabbricaConnessioniArcLab _fabbrica;

    public RepositoryStatistiche(IFabbricaConnessioniArcLab fabbrica) => _fabbrica = fabbrica;

    public async Task<EsitoStatistiche> InterrogaAsync(
        CriteriStatistiche criteri, CancellationToken ct = default)
    {
        var (doveCampione, doveProva, parametri) = ComponiFiltri(criteri);
        parametri.Add("Limite", criteri.Limite);

        var sql = $"""
            WITH CampioniFiltrati AS (
                SELECT
                    ca.IdCampione,
                    CAST(lo.CodLotto AS VARCHAR) + '/' + CAST(ca.CodCampione AS VARCHAR) AS NumeroCampione,
                    cl.IDCliente        AS IdCliente,
                    cl.RagioneSociale   AS Cliente,
                    ins.IDInsediamento  AS IdInsediamento,
                    ins.NomeInsed       AS Insediamento,
                    ins.[CittàInsed]    AS Citta,
                    ins.ProvinciaInsed  AS Provincia,
                    ins.CapInsed        AS Cap,
                    ins.IndirizzoInsed  AS Indirizzo,
                    cm.IdCommessa       AS IdCommessa,
                    cm.DescrIncarico    AS Commessa,
                    ce.NCertificato,
                    ca.DenomCampione    AS DenominazioneCampione,
                    mx.Matrice,
                    lo.DataRic          AS DataRicezione,
                    lo.DataPrelievo,
                    ca.PuntoPrelievo,
                    CONVERT(NVARCHAR(50), ca.TempPrelievo) AS TempPrelievo,
                    ca.LottoProd        AS LottoProduzione,
                    ca.DataProd         AS DataProduzione,
                    ca.LavorazProd      AS Lavorazione
                FROM commessa cm
                -- Il periodo (obbligatorio) è filtrato QUI, sulla sola Lotto, prima di
                -- raggiungere le altre tabelle: così SQL Server restringe subito i lotti
                -- invece di trascinarsi dietro l'intero periodo fino in fondo alla query.
                INNER JOIN (
                    SELECT IdLotto, IdCommessa, CodLotto, DataRic, DataPrelievo
                    FROM Lotto
                    WHERE DataRic >= @DataRicDa AND DataRic <= @DataRicA
                ) lo                            ON cm.IdCommessa = lo.IdCommessa
                INNER JOIN Campione ca         ON lo.IdLotto = ca.IdLotto
                INNER JOIN ContattoCliente cc  ON cm.IDContatto = cc.IDContatto
                INNER JOIN Insediamento ins    ON cc.IDInsediamento = ins.IDInsediamento
                INNER JOIN Clienti cl          ON ins.IDCliente = cl.IDCliente
                LEFT  JOIN matrice mx          ON mx.IdMatrice = ca.IdMatrice
                LEFT  JOIN certificato ce      ON ce.IdCertificato = ca.IdCertificato
                WHERE {doveCampione}
            )
            -- Solo ORA, sul (di norma molto più piccolo) elenco di campioni già filtrato,
            -- si espande sulle prove: qui un campione può comparire più volte, una per prova.
            SELECT TOP (@Limite)
                cf.IdCampione,
                pr.IdProva,
                cf.NumeroCampione,
                cf.IdCliente,
                cf.Cliente,
                cf.IdInsediamento,
                cf.Insediamento,
                cf.Citta,
                cf.Provincia,
                cf.Cap,
                cf.Indirizzo,
                cf.IdCommessa,
                cf.Commessa,
                cf.NCertificato,
                cf.DenominazioneCampione,
                cf.Matrice,
                cf.DataRicezione,
                cf.DataPrelievo,
                cf.PuntoPrelievo,
                cf.TempPrelievo,
                cf.LottoProduzione,
                cf.DataProduzione,
                cf.Lavorazione,
                par.NomeParametro   AS Parametro,
                m.TitoloMetodo      AS Metodo,
                m.RifBiblio         AS RiferimentoNormativo,
                -- Accreditato MARE.A solo per le prove eseguite da MARE.A: una prova in subappalto non è coperta
                -- dal nostro accreditamento (Art. 8.2). Corretto l'08/10/2026.
                CASE WHEN rm.IdLabEsterno IS NULL THEN ISNULL(m.Accreditato, 0) ELSE 0 END AS Accreditato,
                pr.ValoreCalcolato,
                ISNULL(pr.MinoreDi, 0) AS MinoreDi,
                u.UnitaMisura
            FROM CampioniFiltrati cf
            INNER JOIN prove pr            ON pr.IdCampione = cf.IdCampione
            INNER JOIN rigametodo rm       ON rm.IdRigametodo = pr.IdRigaMetodo
            INNER JOIN Metodo m            ON m.IdMetodo = rm.IdMetodo
            LEFT  JOIN Parametro par       ON par.IdParametro = m.IdParametro
            LEFT  JOIN Unitadimisura u     ON u.IdUnita = rm.IdUnita
            WHERE {doveProva}
            ORDER BY cf.DataRicezione, cf.DataPrelievo
            OPTION (RECOMPILE)
            """;

        using var cn = await _fabbrica.ApriAsync(ct);
        var righe = (await cn.QueryAsync<RigaStatistica>(
            new CommandDefinition(sql, parametri, commandTimeout: 180, cancellationToken: ct))).ToList();

        return new EsitoStatistiche
        {
            Righe = righe,
            Troncato = righe.Count >= criteri.Limite
        };
    }

    private static (string DoveCampione, string DoveProva, DynamicParameters Parametri) ComponiFiltri(
        CriteriStatistiche c)
    {
        var condCampione = new List<string> { "1 = 1" };
        var condProva = new List<string> { "1 = 1" };
        var p = new DynamicParameters();
        p.Add("DataRicDa", c.DataRicDa.ToDateTime(TimeOnly.MinValue));
        p.Add("DataRicA", c.DataRicA.ToDateTime(TimeOnly.MaxValue));

        void Aggiungi(List<string> lista, string condizione, string nome, object? valore)
        {
            if (valore is null) return;
            if (valore is string s && string.IsNullOrWhiteSpace(s)) return;
            lista.Add(condizione);
            p.Add(nome, valore);
        }

        // ── Filtri a livello di campione (applicati PRIMA di espandere sulle prove) ──
        Aggiungi(condCampione, "lo.DataPrelievo >= @PrelievoDa", "PrelievoDa", c.DataPrelievoDa?.ToDateTime(TimeOnly.MinValue));
        Aggiungi(condCampione, "lo.DataPrelievo <= @PrelievoA", "PrelievoA", c.DataPrelievoA?.ToDateTime(TimeOnly.MaxValue));

        Aggiungi(condCampione, "cl.RagioneSociale LIKE @Cliente", "Cliente", Contiene(c.Cliente));
        Aggiungi(condCampione, "ins.NomeInsed LIKE @Insediamento", "Insediamento", Contiene(c.Insediamento));
        Aggiungi(condCampione, "ins.[CittàInsed] LIKE @Citta", "Citta", Contiene(c.Citta));
        Aggiungi(condCampione, "ins.ProvinciaInsed LIKE @Provincia", "Provincia", Contiene(c.Provincia));
        Aggiungi(condCampione, "ins.CapInsed LIKE @Cap", "Cap", Contiene(c.Cap));
        Aggiungi(condCampione, "cm.DescrIncarico LIKE @DescrIncarico", "DescrIncarico", Contiene(c.DescrIncarico));
        Aggiungi(condCampione, "ca.DenomCampione LIKE @Denominazione", "Denominazione", Contiene(c.DenominazioneCampione));
        Aggiungi(condCampione, "mx.Matrice LIKE @Matrice", "Matrice", Contiene(c.Matrice));
        Aggiungi(condCampione, "ce.NCertificato LIKE @NCertificato", "NCertificato", Contiene(c.NCertificato));

        // Numero campione: accetta "4530" oppure "4530/01" (come in Consultazione)
        if (!string.IsNullOrWhiteSpace(c.NumeroCampione))
        {
            var parti = c.NumeroCampione.Split(new[] { '/', '-' },
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (int.TryParse(parti[0], out var lotto))
            {
                condCampione.Add("TRY_CONVERT(int, lo.CodLotto) = @CodLotto");
                p.Add("CodLotto", lotto);

                if (parti.Length > 1 && int.TryParse(parti[1], out var campione))
                {
                    condCampione.Add("TRY_CONVERT(int, ca.CodCampione) = @CodCampione");
                    p.Add("CodCampione", campione);
                }
            }
        }

        // ── Filtri a livello di prova (applicati DOPO, sul set già ristretto) ──
        Aggiungi(condProva, "pr.ValoreCalcolato >= @ValoreDa", "ValoreDa", c.ValoreCalcolatoDa);
        Aggiungi(condProva, "pr.ValoreCalcolato <= @ValoreA", "ValoreA", c.ValoreCalcolatoA);

        if (c.Accreditato is not null)
        {
            condProva.Add("(CASE WHEN rm.IdLabEsterno IS NULL THEN ISNULL(m.Accreditato, 0) ELSE 0 END) = @Accreditato");
            p.Add("Accreditato", c.Accreditato.Value ? 1 : 0);
        }

        // Metodo: parametro, titolo metodo, riferimento normativo o denominazione riga
        if (!string.IsNullOrWhiteSpace(c.Metodo))
        {
            condProva.Add("""
                (m.TitoloMetodo LIKE @Metodo
                 OR m.DescrMetodo LIKE @Metodo
                 OR m.RifBiblio LIKE @Metodo
                 OR par.NomeParametro LIKE @Metodo
                 OR rm.DenominazioneRiga LIKE @Metodo)
                """);
            p.Add("Metodo", $"%{c.Metodo.Trim()}%");
        }

        return (
            string.Join("\n                  AND ", condCampione),
            string.Join("\n                  AND ", condProva),
            p);

        static string? Contiene(string? valore) =>
            string.IsNullOrWhiteSpace(valore) ? null : $"%{valore.Trim()}%";
    }
}
