using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Limiti;

/// <summary>Chi esegue l'operazione (dall'utente collegato).</summary>
public record OperatoreLimiti(int IdUtente, string Nome);

/// <summary>Dati di una scheda inseriti o corretti a mano.</summary>
public class DatiScheda
{
    public FamigliaNormativa Famiglia { get; set; }
    public int? IdVersioneFonte { get; set; }
    public string Parametro { get; set; } = "";
    public string? MatriceCategoria { get; set; }
    public OperatoreLimite Operatore { get; set; }
    public decimal? Valore { get; set; }
    public decimal? ValoreMax { get; set; }
    public string? Unita { get; set; }
    public int? N { get; set; }
    public int? C { get; set; }
    public decimal? M { get; set; }
    public decimal? MMaiuscola { get; set; }
    public TipoLimiteNormativo TipoLimite { get; set; }
    public RegolaValutazioneLimite RegolaValutazione { get; set; } = RegolaValutazioneLimite.Puntuale;
    public string? Condizioni { get; set; }
    public string? Riferimento { get; set; }
    public string? Citazione { get; set; }
    public string? TestoSegnalazione { get; set; }
    public DateTime? ValidoDal { get; set; }
    public DateTime? ValidoAl { get; set; }
    public string? Note { get; set; }
    public List<DatiAbbinamento> Abbinamenti { get; set; } = new();
}

public class DatiAbbinamento
{
    public int IdParametroArcLab { get; set; }
    public string? NomeParametro { get; set; }
    public int? IdMatriceArcLab { get; set; }
    public string? NomeMatrice { get; set; }
    public int? IdControlloArcLab { get; set; }
    public string? NomeControllo { get; set; }
    public decimal? FattoreUnita { get; set; }
}

/// <summary>Esito di download o caricamento di una fonte.</summary>
public record EsitoLetturaFonte(bool Riuscita, string Messaggio, int? IdVersioneFonte, bool Nuova);

/// <summary>
/// Schede limite e fonti normative (17/10/2026, fase 1a). Nessuna cancellazione fisica: le schede si respingono o
/// diventano «Superate», le fonti si disattivano. Una scheda Validata non si modifica: la correzione crea una nuova
/// versione «Proposta» che sostituisce la precedente solo quando viene validata.
/// </summary>
public class ServizioLimitiNormativi
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly HttpClient _http;
    private readonly IEstrattoreTestoPdf _pdf;

    public ServizioLimitiNormativi(ContestoLaboratorioAI ctx, HttpClient http, IEstrattoreTestoPdf pdf)
    {
        _ctx = ctx; _http = http; _pdf = pdf;
    }

    // ── Schede ───────────────────────────────────────────────────

    public async Task<LimiteNormativo?> SchedaAsync(int id, CancellationToken ct) =>
        // senza la versione della fonte: testo e copia salvata pesano anche diversi MB
        await _ctx.LimitiNormativi.Include(x => x.Abbinamenti).FirstOrDefaultAsync(x => x.IdLimiteNormativo == id, ct);

    /// <summary>Errori bloccanti dei dati della scheda, o null se va bene.</summary>
    public static string? Controlla(DatiScheda d)
    {
        if (string.IsNullOrWhiteSpace(d.Parametro)) return "Indicare il parametro.";
        if (!Enum.IsDefined(d.Operatore)) return "Indicare come si confronta il risultato (operatore).";
        if (!Enum.IsDefined(d.TipoLimite)) return "Indicare il tipo di limite.";
        if (d.Operatore != OperatoreLimite.AssenteIn && d.Valore is null) return "Indicare il valore del limite.";
        if (d.Operatore == OperatoreLimite.Intervallo && (d.ValoreMax is null || d.ValoreMax < d.Valore))
            return "Per un intervallo indicare il valore massimo (non inferiore al minimo).";
        if (d.Operatore == OperatoreLimite.AssenteIn && string.IsNullOrWhiteSpace(d.Unita))
            return "Per «assente in» indicare la quantità di campione nell'unità (es. 25 g).";
        if (d.RegolaValutazione == RegolaValutazioneLimite.PianoCampionamento && (d.N is null || d.C is null))
            return "Per il piano di campionamento indicare almeno n e c.";
        if (d.ValidoDal is DateTime dal && d.ValidoAl is DateTime al && al < dal) return "«Valido al» precede «Valido dal».";
        return null;
    }

    private static void Copia(DatiScheda d, LimiteNormativo s)
    {
        s.Famiglia = d.Famiglia;
        s.IdVersioneFonte = d.IdVersioneFonte;
        s.Parametro = d.Parametro.Trim();
        s.MatriceCategoria = Vuoto(d.MatriceCategoria);
        s.Operatore = d.Operatore;
        s.Valore = d.Valore;
        s.ValoreMax = d.Operatore == OperatoreLimite.Intervallo ? d.ValoreMax : null;
        s.Unita = Vuoto(d.Unita);
        var piano = d.RegolaValutazione == RegolaValutazioneLimite.PianoCampionamento;
        s.N = piano ? d.N : null; s.C = piano ? d.C : null;
        s.M = piano ? d.M : null; s.MMaiuscola = piano ? d.MMaiuscola : null;
        s.TipoLimite = d.TipoLimite;
        s.RegolaValutazione = d.RegolaValutazione;
        s.Condizioni = Vuoto(d.Condizioni);
        s.Riferimento = Vuoto(d.Riferimento);
        s.Citazione = Vuoto(d.Citazione);
        s.TestoSegnalazione = Vuoto(d.TestoSegnalazione);
        s.ValidoDal = d.ValidoDal; s.ValidoAl = d.ValidoAl;
        s.Note = Vuoto(d.Note);
    }

    private static List<AbbinamentoLimite> Abbinamenti(IEnumerable<DatiAbbinamento> elenco) =>
        elenco.Where(a => a.IdParametroArcLab > 0)
            .GroupBy(a => (a.IdParametroArcLab, a.IdMatriceArcLab, a.IdControlloArcLab)).Select(g => g.First())
            .Select(a => new AbbinamentoLimite
            {
                IdParametroArcLab = a.IdParametroArcLab, NomeParametro = Vuoto(a.NomeParametro),
                IdMatriceArcLab = a.IdMatriceArcLab, NomeMatrice = Vuoto(a.NomeMatrice),
                IdControlloArcLab = a.IdControlloArcLab, NomeControllo = Vuoto(a.NomeControllo),
                FattoreUnita = a.FattoreUnita is 0 ? null : a.FattoreUnita
            }).ToList();

    private static string? Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<string> NuovoCodiceAsync(FamigliaNormativa f, CancellationToken ct)
    {
        var pre = RegoleLimiti.Prefisso(f) + "-";
        var codici = await _ctx.LimitiNormativi.Where(x => x.CodiceScheda.StartsWith(pre)).Select(x => x.CodiceScheda).Distinct().ToListAsync(ct);
        // anche quelli appena aggiunti e non ancora salvati
        codici.AddRange(_ctx.ChangeTracker.Entries<LimiteNormativo>().Select(e => e.Entity.CodiceScheda));
        return RegoleLimiti.ProssimoCodice(f, codici);
    }

    /// <summary>Nuova scheda «Proposta» inserita a mano (o dall'AI, con <paramref name="operatore"/> null).</summary>
    public async Task<LimiteNormativo> CreaAsync(DatiScheda d, OperatoreLimiti? operatore, CancellationToken ct, bool salva = true)
    {
        var s = new LimiteNormativo
        {
            CodiceScheda = await NuovoCodiceAsync(d.Famiglia, ct),
            Versione = 1,
            Stato = StatoSchedaLimite.Proposta,
            PropostaDa = operatore?.Nome ?? "AI",
            IdUtenteProposta = operatore?.IdUtente,
            DataProposta = DateTime.UtcNow,
            Abbinamenti = Abbinamenti(d.Abbinamenti)
        };
        Copia(d, s);
        _ctx.LimitiNormativi.Add(s);
        if (salva) await _ctx.SaveChangesAsync(ct);
        return s;
    }

    /// <summary>
    /// Correzione. Proposta / Da riverificare: si corregge la stessa riga (e chi corregge non potrà validarla).
    /// Validata: nasce una nuova versione «Proposta», la validata resta in vigore fino alla validazione della nuova.
    /// </summary>
    public async Task<(LimiteNormativo? Scheda, string? Errore)> CorreggiAsync(int id, DatiScheda d, OperatoreLimiti op, CancellationToken ct)
    {
        var s = await SchedaAsync(id, ct);
        if (s is null) return (null, "Scheda non trovata.");
        d.Famiglia = s.Famiglia;     // la famiglia fa parte del codice: non cambia
        switch (s.Stato)
        {
            case StatoSchedaLimite.Proposta or StatoSchedaLimite.DaRiverificare:
                Copia(d, s);
                s.ModificataDa = op.Nome; s.IdUtenteModifica = op.IdUtente; s.DataModifica = DateTime.UtcNow;
                foreach (var a in s.Abbinamenti.Where(a => a.Attivo)) a.Attivo = false;
                foreach (var a in Abbinamenti(d.Abbinamenti))
                {
                    var esistente = s.Abbinamenti.FirstOrDefault(x => x.IdParametroArcLab == a.IdParametroArcLab
                        && x.IdMatriceArcLab == a.IdMatriceArcLab && x.IdControlloArcLab == a.IdControlloArcLab);
                    if (esistente is null) s.Abbinamenti.Add(a);
                    else { esistente.Attivo = true; esistente.FattoreUnita = a.FattoreUnita; }
                }
                await _ctx.SaveChangesAsync(ct);
                return (s, null);
            case StatoSchedaLimite.Validata:
                if (await _ctx.LimitiNormativi.AnyAsync(x => x.CodiceScheda == s.CodiceScheda
                        && (x.Stato == StatoSchedaLimite.Proposta || x.Stato == StatoSchedaLimite.DaRiverificare), ct))
                    return (null, $"C'è già una nuova versione di {s.CodiceScheda} in attesa di validazione: correggi quella.");
                var nuova = new LimiteNormativo
                {
                    CodiceScheda = s.CodiceScheda,
                    Versione = await _ctx.LimitiNormativi.Where(x => x.CodiceScheda == s.CodiceScheda).MaxAsync(x => x.Versione, ct) + 1,
                    IdLimitePrecedente = s.IdLimiteNormativo,
                    Stato = StatoSchedaLimite.Proposta,
                    PropostaDa = op.Nome, IdUtenteProposta = op.IdUtente, DataProposta = DateTime.UtcNow,
                    Abbinamenti = Abbinamenti(d.Abbinamenti)
                };
                Copia(d, nuova);
                _ctx.LimitiNormativi.Add(nuova);
                await _ctx.SaveChangesAsync(ct);
                return (nuova, null);
            default:
                return (null, "Le schede respinte o superate non si modificano.");
        }
    }

    public async Task<(LimiteNormativo? Scheda, string? Errore)> ValidaAsync(int id, string? note, OperatoreLimiti op, CancellationToken ct)
    {
        var s = await SchedaAsync(id, ct);
        if (s is null) return (null, "Scheda non trovata.");
        var motivo = RegoleLimiti.MotivoNonValidabile(s, op.IdUtente);
        if (motivo is not null) return (null, motivo);
        var errore = Controlla(DaScheda(s));
        if (errore is not null) return (null, "Scheda incompleta: " + errore);
        if (string.IsNullOrWhiteSpace(s.Citazione) && string.IsNullOrWhiteSpace(note))
            return (null, "La scheda non ha la citazione della norma: scrivi nella nota da dove hai verificato il limite.");
        s.Stato = StatoSchedaLimite.Validata;
        s.ValidataDa = op.Nome; s.IdUtenteValidazione = op.IdUtente; s.DataValidazione = DateTime.UtcNow;
        s.NoteValidazione = Vuoto(note);
        // la versione validata precedente della stessa scheda è sostituita
        var precedenti = await _ctx.LimitiNormativi.Where(x => x.CodiceScheda == s.CodiceScheda
            && x.IdLimiteNormativo != s.IdLimiteNormativo && x.Stato == StatoSchedaLimite.Validata).ToListAsync(ct);
        foreach (var p in precedenti) p.Stato = StatoSchedaLimite.Superata;
        await _ctx.SaveChangesAsync(ct);
        return (s, null);
    }

    public async Task<(LimiteNormativo? Scheda, string? Errore)> RespingiAsync(int id, string? motivo, OperatoreLimiti op, CancellationToken ct)
    {
        var s = await SchedaAsync(id, ct);
        if (s is null) return (null, "Scheda non trovata.");
        if (s.Stato is not (StatoSchedaLimite.Proposta or StatoSchedaLimite.DaRiverificare))
            return (null, "Si respingono solo le schede «Proposta» o «Da riverificare».");
        if (string.IsNullOrWhiteSpace(motivo)) return (null, "Scrivi il motivo.");
        s.Stato = StatoSchedaLimite.Respinta;
        s.ValidataDa = op.Nome; s.IdUtenteValidazione = op.IdUtente; s.DataValidazione = DateTime.UtcNow;
        s.NoteValidazione = motivo.Trim();
        await _ctx.SaveChangesAsync(ct);
        return (s, null);
    }

    /// <summary>
    /// 20/10/2026: ritira una scheda validata (doppia o che non si vuole più usare). Nessuna cancellazione:
    /// la scheda passa a «Respinta» con il motivo, resta nello storico e non viene più usata nei controlli.
    /// </summary>
    public async Task<(LimiteNormativo? Scheda, string? Errore)> RitiraAsync(int id, string? motivo, OperatoreLimiti op, CancellationToken ct)
    {
        var s = await SchedaAsync(id, ct);
        if (s is null) return (null, "Scheda non trovata.");
        if (s.Stato != StatoSchedaLimite.Validata) return (null, "Si ritirano solo le schede validate.");
        if (string.IsNullOrWhiteSpace(motivo)) return (null, "Scrivi il motivo (es. «doppia di MOL-003»).");
        s.Stato = StatoSchedaLimite.Respinta;
        s.ValidataDa = op.Nome; s.IdUtenteValidazione = op.IdUtente; s.DataValidazione = DateTime.UtcNow;
        s.NoteValidazione = "Ritirata (era validata): " + motivo.Trim();
        await _ctx.SaveChangesAsync(ct);
        return (s, null);
    }

    public static DatiScheda DaScheda(LimiteNormativo s) => new()
    {
        Famiglia = s.Famiglia, IdVersioneFonte = s.IdVersioneFonte, Parametro = s.Parametro, MatriceCategoria = s.MatriceCategoria,
        Operatore = s.Operatore, Valore = s.Valore, ValoreMax = s.ValoreMax, Unita = s.Unita, N = s.N, C = s.C, M = s.M,
        MMaiuscola = s.MMaiuscola, TipoLimite = s.TipoLimite, RegolaValutazione = s.RegolaValutazione, Condizioni = s.Condizioni,
        Riferimento = s.Riferimento, Citazione = s.Citazione, TestoSegnalazione = s.TestoSegnalazione,
        ValidoDal = s.ValidoDal, ValidoAl = s.ValidoAl, Note = s.Note,
        Abbinamenti = s.Abbinamenti.Where(a => a.Attivo).Select(a => new DatiAbbinamento
        {
            IdParametroArcLab = a.IdParametroArcLab, NomeParametro = a.NomeParametro, IdMatriceArcLab = a.IdMatriceArcLab,
            NomeMatrice = a.NomeMatrice, IdControlloArcLab = a.IdControlloArcLab, NomeControllo = a.NomeControllo, FattoreUnita = a.FattoreUnita
        }).ToList()
    };

    /// <summary>Schede in attesa di un validatore (Proposta + Da riverificare): contatore della Home.</summary>
    public Task<int> DaValidareAsync(CancellationToken ct) =>
        _ctx.LimitiNormativi.CountAsync(x => x.Stato == StatoSchedaLimite.Proposta || x.Stato == StatoSchedaLimite.DaRiverificare, ct);

    // ── Fonti ────────────────────────────────────────────────────

    /// <summary>
    /// Fonti proposte per la fase 1 (Legionella, acque, molluschi). Gli indirizzi vanno verificati: se il server non
    /// riesce a scaricarli (EUR-Lex a volte blocca i download automatici) si carica a mano il PDF o l'HTML salvato.
    /// </summary>
    public static readonly (FamigliaNormativa Famiglia, string Titolo, string Ente, TipoFonteNormativa Tipo, string Url, string Note)[] FontiFase1 =
    [
        (FamigliaNormativa.Legionella, "Linee guida per la prevenzione e il controllo della legionellosi (7 maggio 2015)",
            "Ministero della Salute", TipoFonteNormativa.MinisteroSalute,
            "https://www.salute.gov.it/imgs/C_17_pubblicazioni_2362_allegato.pdf",
            "Tabelle delle azioni per concentrazione di Legionella (acqua sanitaria). Verificare eventuali aggiornamenti regionali."),
        (FamigliaNormativa.AcqueConsumoUmano, "D.Lgs. 23 febbraio 2023, n. 18 – Qualità delle acque destinate al consumo umano",
            "Normattiva", TipoFonteNormativa.Normattiva,
            "https://www.normattiva.it/uri-res/N2Ls?urn:nir:stato:decreto.legislativo:2023-02-23;18",
            "Testo vigente letto dall'agente fonti con le API Open Data di Normattiva, per accorgersi delle modifiche. L'Allegato I (valori di parametro) in Gazzetta è un'immagine: i valori si estraggono dalla Direttiva (UE) 2020/2184 e si verificano sul PDF della Gazzetta."),
        (FamigliaNormativa.AcqueConsumoUmano, "Direttiva (UE) 2020/2184 – Qualità delle acque destinate al consumo umano (testo consolidato)",
            "EUR-Lex", TipoFonteNormativa.EurLex,
            "https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32020L2184",
            "Allegato I, parti A, B e C: valori di parametro recepiti dal D.Lgs. 18/2023 (riferimento delle schede: D.Lgs. 18/2023, All. I). Verificare eventuali differenze nazionali sul PDF della Gazzetta."),
        (FamigliaNormativa.Molluschi, "Reg. (CE) n. 2073/2005 – Criteri microbiologici (testo consolidato)",
            "EUR-Lex", TipoFonteNormativa.EurLex,
            "https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32005R2073",
            "Allegato I, criteri 1.17 (E. coli molluschi) e 1.18 (Salmonella). L'agente fonti scarica da Cellar l'ultimo testo consolidato."),
        (FamigliaNormativa.Molluschi, "Reg. (UE) 2019/627 – Controlli ufficiali sui molluschi bivalvi vivi (classificazione zone)",
            "EUR-Lex", TipoFonteNormativa.EurLex,
            "https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32019R0627",
            "Artt. 53-55: limiti di E. coli per le zone di classe A, B, C."),
        (FamigliaNormativa.Molluschi, "Reg. (CE) n. 853/2004 – Igiene degli alimenti di origine animale (testo consolidato)",
            "EUR-Lex", TipoFonteNormativa.EurLex,
            "https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32004R0853",
            "Allegato III, sezione VII, capo V: norme sanitarie per i molluschi bivalvi vivi (biotossine). Testo molto lungo: caricare solo la sezione VII se l'estrazione rifiuta il testo."),
        (FamigliaNormativa.Molluschi, "Reg. (UE) 2023/915 – Tenori massimi di contaminanti negli alimenti",
            "EUR-Lex", TipoFonteNormativa.EurLex,
            "https://eur-lex.europa.eu/legal-content/IT/TXT/?uri=CELEX:32023R0915",
            "Allegato I: metalli (piombo, cadmio, mercurio) e IPA nei molluschi bivalvi."),
    ];

    public async Task<int> AggiungiFontiFase1Async(OperatoreLimiti op, CancellationToken ct)
    {
        var esistenti = await _ctx.FontiNormative.Select(f => new { f.Url, f.Titolo }).ToListAsync(ct);
        var n = 0;
        foreach (var f in FontiFase1.Where(f => !esistenti.Any(e => e.Url == f.Url || e.Titolo == f.Titolo)))
        {
            _ctx.FontiNormative.Add(new FonteNormativa
            {
                Famiglia = f.Famiglia, Titolo = f.Titolo, Ente = f.Ente, TipoFonte = f.Tipo, Url = f.Url, Note = f.Note,
                Attiva = true, DataCreazione = DateTime.UtcNow, CreataDa = op.Nome
            });
            n++;
        }
        await _ctx.SaveChangesAsync(ct);
        return n;
    }

    /// <summary>Scarica il testo vigente dall'indirizzo della fonte. Se l'impronta non cambia, nessuna nuova versione.</summary>
    public async Task<EsitoLetturaFonte> ScaricaAsync(int idFonte, OperatoreLimiti? op, CancellationToken ct)
    {
        var fonte = await _ctx.FontiNormative.FirstOrDefaultAsync(f => f.IdFonteNormativa == idFonte, ct);
        if (fonte is null) return new(false, "Fonte non trovata.", null, false);
        if (string.IsNullOrWhiteSpace(fonte.Url)) return new(false, "La fonte non ha un indirizzo: caricare il file a mano.", null, false);
        // 17/10/2026: EUR-Lex e Normattiva bloccano il server o danno solo il primo articolo: le aggiorna l'agente fonti.
        if (TestoFonti.CanaleAutomatico(fonte.Url).Canale is string canale)
            return new(false, $"Questa fonte la aggiorna l'agente fonti dal PC dell'ufficio ({canale}): lanciare l'agente oppure usare «Carica file».", null, false);
        byte[] dati; string? tipo;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(90));
            using var req = new HttpRequestMessage(HttpMethod.Get, fonte.Url);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) LaboratorioAI MARE.A");
            req.Headers.TryAddWithoutValidation("Accept-Language", "it-IT,it;q=0.9");
            using var r = await _http.SendAsync(req, cts.Token);
            if (!r.IsSuccessStatusCode)
                return await RegistraVerificaAsync(fonte, new(false, $"Il sito ha risposto {(int)r.StatusCode}: caricare il file a mano.", null, false), ct);
            dati = await r.Content.ReadAsByteArrayAsync(cts.Token);
            tipo = r.Content.Headers.ContentType?.MediaType;
            var charset = r.Content.Headers.ContentType?.CharSet;
            if (!TestoFonti.EPdf(dati) && !string.IsNullOrEmpty(charset) && !charset.Contains("utf", StringComparison.OrdinalIgnoreCase))
            {
                try { dati = System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.GetEncoding(charset.Trim('"')).GetString(dati)); }
                catch (Exception e) when (e is ArgumentException or NotSupportedException) { /* si legge come UTF-8 */ }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return await RegistraVerificaAsync(fonte, new(false, "Download non riuscito (" + ex.Message + "): caricare il file a mano.", null, false), ct);
        }
        var nome = Uri.TryCreate(fonte.Url, UriKind.Absolute, out var u) ? Path.GetFileName(u.AbsolutePath) : null;
        return await SalvaVersioneAsync(fonte, dati, tipo, nome, "Scaricata", null, op, ct);
    }

    /// <summary>Testo inviato dall'agente fonti dal PC dell'ufficio (Cellar / Normattiva, 17/10/2026).</summary>
    public async Task<EsitoLetturaFonte> RiceviDaAgenteAsync(int idFonte, byte[] dati, string? nomeFile, string? tipo, DateTime? dataDichiarata,
        CancellationToken ct)
    {
        var fonte = await _ctx.FontiNormative.FirstOrDefaultAsync(f => f.IdFonteNormativa == idFonte, ct);
        if (fonte is null) return new(false, "Fonte non trovata.", null, false);
        return await SalvaVersioneAsync(fonte, dati, tipo, nomeFile, "Agente", dataDichiarata,
            new OperatoreLimiti(0, "Agente fonti (PC ufficio)"), ct);
    }

    /// <summary>Caricamento a mano del file (PDF, HTML salvato dal browser, testo).</summary>
    public async Task<EsitoLetturaFonte> CaricaAsync(int idFonte, byte[] dati, string? nomeFile, string? tipo, DateTime? dataDichiarata,
        OperatoreLimiti op, CancellationToken ct)
    {
        var fonte = await _ctx.FontiNormative.FirstOrDefaultAsync(f => f.IdFonteNormativa == idFonte, ct);
        if (fonte is null) return new(false, "Fonte non trovata.", null, false);
        return await SalvaVersioneAsync(fonte, dati, tipo, nomeFile, "Caricata", dataDichiarata, op, ct);
    }

    private async Task<EsitoLetturaFonte> SalvaVersioneAsync(FonteNormativa fonte, byte[] dati, string? tipo, string? nome, string origine,
        DateTime? dataDichiarata, OperatoreLimiti? op, CancellationToken ct)
    {
        string testo;
        if (TestoFonti.EPdf(dati))
        {
            testo = TestoFonti.Pulisci(_pdf.EstraiTesto(dati));
            tipo = "application/pdf";
            if (testo.Length < 200)
                return await RegistraVerificaAsync(fonte, new(false, "Il PDF non contiene testo selezionabile (è una scansione?): serve un PDF con testo o la pagina HTML.", null, false), ct);
        }
        else
        {
            var grezzo = System.Text.Encoding.UTF8.GetString(dati);
            var html = grezzo.Contains("<html", StringComparison.OrdinalIgnoreCase) || grezzo.Contains("<body", StringComparison.OrdinalIgnoreCase)
                || (tipo?.Contains("html", StringComparison.OrdinalIgnoreCase) ?? false);
            testo = html ? TestoFonti.DaHtml(grezzo) : TestoFonti.Pulisci(grezzo);
            tipo ??= html ? "text/html" : "text/plain";
            if (origine == "Scaricata" && (fonte.Url ?? "").Split('?')[0].EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                return await RegistraVerificaAsync(fonte, new(false, "Il sito ha risposto con una pagina web invece del PDF (controllo anti-bot): apri l'indirizzo dal browser, salva il PDF e usa «Carica file».", null, false), ct);
            if (TestoFonti.PaginaDiBlocco(testo))
                return await RegistraVerificaAsync(fonte, new(false, "Il sito ha risposto con una pagina di controllo (verifica del browser / cookie) invece del testo della norma: apri l'indirizzo dal browser, salva la pagina o il PDF e usa «Carica file».", null, false), ct);
        }
        var impronta = RegoleLimiti.Impronta(testo);
        var ultima = await _ctx.VersioniFonti.Where(v => v.IdFonteNormativa == fonte.IdFonteNormativa)
            .OrderByDescending(v => v.IdVersioneFonte).Select(v => new { v.IdVersioneFonte, v.Impronta }).FirstOrDefaultAsync(ct);
        if (ultima is not null && ultima.Impronta == impronta)
            return await RegistraVerificaAsync(fonte, new(true, "Nessuna modifica rispetto all'ultima versione salvata.", ultima.IdVersioneFonte, false), ct);

        var v = new VersioneFonte
        {
            IdFonteNormativa = fonte.IdFonteNormativa,
            DataAggiornamentoDichiarata = dataDichiarata ?? TestoFonti.DataAggiornamento(testo),
            DataConsultazione = DateTime.UtcNow,
            Origine = origine,
            NomeFile = nome is { Length: > 0 } ? (nome.Length > 250 ? nome[..250] : nome) : null,
            TipoContenuto = tipo,
            ContenutoOriginale = dati,
            TestoEstratto = testo,
            Impronta = impronta,
            CaricataDa = op?.Nome ?? "LaboratorioAI (automatico)",
            StatoEstrazione = StatoEstrazioneLimiti.Nessuna
        };
        _ctx.VersioniFonti.Add(v);
        // Testo cambiato: le schede validate tratte da questa fonte vanno riverificate (procedura § 6.6, 7 giorni lavorativi).
        var daRiverificare = 0;
        if (ultima is not null)
        {
            var versioniFonte = _ctx.VersioniFonti.Where(x => x.IdFonteNormativa == fonte.IdFonteNormativa).Select(x => (int?)x.IdVersioneFonte);
            var validate = await _ctx.LimitiNormativi.Where(x => x.Stato == StatoSchedaLimite.Validata && versioniFonte.Contains(x.IdVersioneFonte)).ToListAsync(ct);
            foreach (var sch in validate)
            {
                sch.Stato = StatoSchedaLimite.DaRiverificare;
                sch.NoteValidazione = $"Da riverificare: nuova versione della fonte del {DateTime.Now:dd/MM/yyyy}. " + sch.NoteValidazione;
                if (sch.NoteValidazione.Length > 1000) sch.NoteValidazione = sch.NoteValidazione[..1000];
            }
            daRiverificare = validate.Count;
        }
        await _ctx.SaveChangesAsync(ct);
        var msg = ultima is null ? "Prima versione salvata" : "Testo cambiato: salvata una nuova versione";
        if (daRiverificare > 0) msg += $" — {daRiverificare} schede validate passano a «Da riverificare»";
        msg += $" ({testo.Length:N0} caratteri).";
        if (testo.Length > TestoFonti.CaratteriMassimi)
            msg += $" Attenzione: oltre {TestoFonti.CaratteriMassimi:N0} caratteri l'estrazione con l'AI non parte: caricare solo l'allegato con i limiti.";
        return await RegistraVerificaAsync(fonte, new(true, msg, v.IdVersioneFonte, true), ct);
    }

    private async Task<EsitoLetturaFonte> RegistraVerificaAsync(FonteNormativa fonte, EsitoLetturaFonte esito, CancellationToken ct)
    {
        fonte.DataUltimaVerifica = DateTime.UtcNow;
        fonte.EsitoUltimaVerifica = esito.Messaggio.Length > 1000 ? esito.Messaggio[..1000] : esito.Messaggio;
        await _ctx.SaveChangesAsync(ct);
        return esito;
    }

    /// <summary>Mette in coda l'estrazione con l'AI (la esegue il servizio in background).</summary>
    public async Task<string?> AccodaEstrazioneAsync(int idVersione, CancellationToken ct, IReadOnlyCollection<FamigliaNormativa>? famiglie = null)
    {
        var v = await _ctx.VersioniFonti.FirstOrDefaultAsync(x => x.IdVersioneFonte == idVersione, ct);
        if (v is null) return "Versione non trovata.";
        if (v.StatoEstrazione == StatoEstrazioneLimiti.InCorso) return "L'estrazione è già in corso.";
        if (TestoFonti.PaginaDiBlocco(v.TestoEstratto))
            return "Questa versione non contiene la norma ma una pagina di controllo del sito: caricare il file a mano e usare la nuova versione.";
        if (v.TestoEstratto.Length > TestoFonti.CaratteriMassimi)
            return $"Il testo è troppo lungo ({v.TestoEstratto.Length:N0} caratteri, massimo {TestoFonti.CaratteriMassimi:N0}): caricare solo l'allegato con i limiti.";
        v.StatoEstrazione = StatoEstrazioneLimiti.InCorso;
        v.DataEstrazione = null;           // null = in coda, valorizzata quando parte
        v.EsitoEstrazione = RegoleLimiti.TestoCoda(famiglie);
        await _ctx.SaveChangesAsync(ct);
        return null;
    }
}
