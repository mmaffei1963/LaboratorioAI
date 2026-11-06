using System.Globalization;
using System.Text;
using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Limiti;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LaboratorioAI.IA;

/// <summary>Esito dell'estrazione di una versione della fonte.</summary>
public record EsitoEstrazioneLimiti(int Proposte, int Scartate, int GiaPresenti, string Messaggio);

/// <summary>
/// Estrazione delle schede limite da una fonte ufficiale con l'AI (17/10/2026, fase 1a, procedura § 6.3).
/// Il modello legge il testo salvato della fonte e propone le schede in JSON; ogni proposta deve riportare una
/// citazione presente parola per parola nel testo salvato, altrimenti è scartata. Le schede nascono «Proposta»:
/// nessuna produce segnalazioni finché una persona non la valida.
/// </summary>
public class EstrattoreLimitiNormativi
{
    private const int TokenUscita = 16000;
    private const int TimeoutSecondi = 600;
    private const int GiriMassimi = 4;

    private readonly IClientAnthropic _ia;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioLimitiNormativi _limiti;
    private readonly IRepositoryLimitiArcLab _arclab;
    private readonly ILogger<EstrattoreLimitiNormativi> _log;

    public EstrattoreLimitiNormativi(IClientAnthropic ia, ContestoLaboratorioAI ctx, ServizioLimitiNormativi limiti,
        IRepositoryLimitiArcLab arclab, ILogger<EstrattoreLimitiNormativi> log)
    {
        _ia = ia; _ctx = ctx; _limiti = limiti; _arclab = arclab; _log = log;
    }

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Proposta come la scrive il modello (enumerazioni come testo, controllate dopo).</summary>
    public class PropostaAi
    {
        public string? Famiglia { get; set; }
        public string? Parametro { get; set; }
        public string? MatriceCategoria { get; set; }
        public string? Operatore { get; set; }
        public decimal? Valore { get; set; }
        public decimal? ValoreMax { get; set; }
        public string? Unita { get; set; }
        public int? N { get; set; }
        public int? C { get; set; }
        public decimal? PiccolaM { get; set; }
        public decimal? GrandeM { get; set; }
        public string? TipoLimite { get; set; }
        public string? RegolaValutazione { get; set; }
        public string? Condizioni { get; set; }
        public string? Riferimento { get; set; }
        public string? Citazione { get; set; }
        public string? TestoSegnalazione { get; set; }
        public List<int>? ParametriArcLab { get; set; }
        public string? Note { get; set; }
    }

    private const string Istruzioni = """
        Sei un esperto di normativa per laboratori di analisi accreditati ISO/IEC 17025 (MARE.A srl, Italia).
        Nel messaggio di sistema c'è il TESTO UFFICIALE di una fonte normativa. Devi ricavarne le «schede limite»:
        ogni valore numerico (o di assenza) con cui confrontare un risultato analitico.

        Famiglie di interesse (estrai SOLO i limiti che appartengono a una di queste; ignora il resto della norma):
        {FAMIGLIE}

        Rispondi ESCLUSIVAMENTE con un array JSON (nessun testo prima o dopo), un oggetto per limite:
        {
          "famiglia": uno dei codici delle famiglie di interesse elencate sopra, scelto in base alla matrice/categoria,
          "parametro": "nome del parametro come scritto nella norma",
          "matriceCategoria": "matrice o categoria di alimento/acqua a cui si applica, come nella norma",
          "operatore": "ValoreMassimo" | "MinoreDi" | "ValoreMinimo" | "Intervallo" | "AssenteIn",
          "valore": numero (null solo per AssenteIn),
          "valoreMax": numero (solo per Intervallo),
          "unita": "unità di misura della norma (es. UFC/L, MPN/100 g, µg/L, mg/kg peso fresco); per AssenteIn la quantità (es. 25 g)",
          "n": intero o null, "c": intero o null, "piccolaM": numero o null, "grandeM": numero o null,
          "tipoLimite": "Sicurezza" | "IgieneProcesso" | "Indicatore" | "LineaGuida" | "Riferimento",
          "regolaValutazione": "Puntuale" | "PianoCampionamento",
          "condizioni": "fase o condizione di applicazione (es. prodotti immessi sul mercato durante il loro periodo di conservabilità), o null",
          "riferimento": "norma, articolo, allegato, punto o tabella",
          "citazione": "frase o riga di tabella COPIATA PAROLA PER PAROLA dal testo, 20-300 caratteri, che contiene il valore",
          "testoSegnalazione": "frase breve (max 120 caratteri) da mostrare al validatore se il limite è superato",
          "parametriArcLab": [IdParametro dell'elenco ArcLab che misurano questo parametro],
          "note": "dubbi o avvertenze, o null"
        }

        Regole:
        - Non inventare: se un valore non è scritto nel testo, non creare la scheda.
        - Non sono limiti, e non vanno estratti: le tabelle sulle prestazioni dei metodi di analisi (incertezza di
          misura, limite di quantificazione, esattezza, precisione, «% del valore di parametro»), le frequenze di
          campionamento e i numeri di campioni. Estrai solo valori con cui confrontare il risultato di un campione.
        - Non creare schede per parametri senza un valore numerico o di assenza (es. «accettabile per i consumatori e
          senza variazioni anomale», «senza variazioni anomale»): non sono confrontabili con un risultato.
        - «citazione» deve essere copiata esattamente dal testo (stesse parole e numeri). Le citazioni non trovate
          nel testo saranno scartate automaticamente. Nel testo le tabelle sono scritte una riga per riga di tabella,
          con le celle separate da « | »: per un limite in tabella copia la riga (o la sua parte iniziale fino al
          valore) così com'è, separatori compresi. Non riformulare («Non rilevabile» resta «Non rilevabile»).
        - Numeri in formato JSON (punto decimale, niente separatori delle migliaia: 1.000 UFC/L → 1000).
        - «≤ x» o «non superiore a x» → ValoreMassimo; «< x» → MinoreDi; «≥ x» → ValoreMinimo; «assente in 25 g» → AssenteIn.
        - tipoLimite: Sicurezza = limite di legge il cui superamento rende il prodotto/l'acqua non conforme
          (criteri di sicurezza alimentare, valori di parametro microbiologici e chimici delle acque, tenori massimi
          di contaminanti); IgieneProcesso = criteri di igiene del processo; Indicatore = parametri indicatori
          delle acque; LineaGuida = soglie di linee guida (es. Legionella: livelli che richiedono azioni);
          Riferimento = altro.
        - Per i piani di campionamento a più unità (n, c, m, M) usa regolaValutazione = PianoCampionamento,
          valore = m (piccolaM) e, se c'è, grandeM = M; operatore ValoreMassimo.
        - Per le linee guida con più soglie (es. 100, 1.000, 10.000 UFC/L) crea una scheda per ciascuna soglia, con
          testoSegnalazione che riassume l'azione prevista per quel livello.
        - Una scheda per combinazione parametro + categoria + valore. Niente duplicati.
        - famiglia: decidila dalla categoria della norma (es. «Molluschi bivalvi vivi» → Molluschi; «Alimenti pronti
          per lattanti» → Alimenti). Se una categoria comprende più famiglie, crea una scheda per ciascuna.
        - parametriArcLab: scegli solo IdParametro presenti nell'elenco fornito per la stessa famiglia e che misurano davvero quel parametro
          (stesso microrganismo/analita); lista vuota se nessuno corrisponde.
        """;

    /// <summary>Esegue l'estrazione di una versione già in coda o da rifare.</summary>
    /// <param name="famiglie">Famiglie da estrarre (18/10/2026); null o vuoto = tutte.</param>
    public async Task<EsitoEstrazioneLimiti> EstraiAsync(int idVersioneFonte, IReadOnlyCollection<FamigliaNormativa>? famiglie, CancellationToken ct)
    {
        var versione = await _ctx.VersioniFonti.Include(v => v.Fonte).FirstAsync(v => v.IdVersioneFonte == idVersioneFonte, ct);
        var famigliaFonte = versione.Fonte!.Famiglia;
        var consentite = (famiglie is { Count: > 0 } ? famiglie : Enum.GetValues<FamigliaNormativa>()).Distinct().ToHashSet();
        var testo = versione.TestoEstratto;
        if (testo.Length > TestoFonti.CaratteriMassimi)
            return new(0, 0, 0, "Testo troppo lungo: caricare solo l'allegato con i limiti.");

        // Parametri ArcLab misurati negli ultimi 3 anni nelle matrici di questa famiglia.
        // Se ArcLab non risponde l'estrazione prosegue senza abbinamenti (si aggiungono a mano nella scheda).
        IReadOnlyList<ParametroUsatoArcLab> daArcLab;
        string? avvisoArcLab = null;
        try { daArcLab = await _arclab.ParametriUsatiAsync(3, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Estrazione limiti: parametri ArcLab non letti.");
            daArcLab = [];
            avvisoArcLab = "Parametri ArcLab non letti (" + ex.Message + "): schede senza abbinamenti, da aggiungere a mano.";
        }
        // Parametri ArcLab per famiglia (le matrici decidono la famiglia, come nell'analisi del perimetro).
        var perFamiglia = daArcLab
            .Select(p => (Famiglia: RegoleLimiti.FamigliaDaMatrice(p.Matrice), p))
            .Where(x => consentite.Contains(x.Famiglia))
            .GroupBy(x => x.Famiglia)
            .ToDictionary(g => g.Key, g => g.GroupBy(x => x.p.IdParametro)
                .Select(h => new
                {
                    Id = h.Key,
                    Nome = h.First().p.NomeParametro.Trim(),
                    Unita = string.Join(", ", h.Select(x => x.p.UnitaMisura?.Trim()).Where(u => !string.IsNullOrEmpty(u)).Distinct().Take(4)),
                    Prove = h.Sum(x => x.p.Prove)
                })
                .OrderByDescending(x => x.Prove).Take(150).ToList());
        var nomi = new Dictionary<int, string>();
        var elenco = new StringBuilder();
        foreach (var (fam, parametri) in perFamiglia.OrderBy(k => k.Key))
        {
            elenco.AppendLine($"Parametri ArcLab misurati da MARE.A per la famiglia {RegoleLimiti.CodiceFamiglia(fam)} (IdParametro | nome | unità | prove in 3 anni):");
            foreach (var p in parametri) { elenco.AppendLine($"{p.Id} | {p.Nome} | {p.Unita} | {p.Prove}"); nomi.TryAdd(p.Id, p.Nome); }
            elenco.AppendLine();
        }
        var descrizioneFamiglie = string.Join("\n", consentite.OrderBy(f => f)
            .Select(f => $"- {RegoleLimiti.CodiceFamiglia(f)}: {RegoleLimiti.DescrizioneFamiglia(f)}"));

        var fonte = $"FONTE: {versione.Fonte.Titolo}\nEnte: {versione.Fonte.Ente}\nIndirizzo: {versione.Fonte.Url}\n"
            + $"Data di aggiornamento dichiarata: {versione.DataAggiornamentoDichiarata:dd/MM/yyyy}\n\n=== TESTO ===\n{testo}";
        var messaggio = Istruzioni.Replace("{FAMIGLIE}", descrizioneFamiglie) + "\n\n" + elenco;

        // 17/10/2026: se la risposta supera il limite di token si chiede il seguito (fino a 4 giri), elencando i limiti già
        // estratti. Il testo della fonte è in cache: i giri successivi costano poco sull'input.
        var proposte = new List<PropostaAi>();
        int tokenIn = 0, tokenOut = 0, giri = 0;
        var troncata = true;
        while (troncata && giri < GiriMassimi)
        {
            giri++;
            var richiesta = messaggio;
            if (proposte.Count > 0)
                richiesta += "\n\nLIMITI GIÀ ESTRATTI (non ripeterli, continua con i successivi; se non ce ne sono altri rispondi []):\n"
                    + string.Join("\n", proposte.Select(p => $"- {p.Famiglia} | {p.Parametro} | {p.MatriceCategoria} | {p.Valore} {p.Unita}"));
            versione.DataEstrazione = DateTime.UtcNow;           // il servizio non la considera abbandonata
            versione.EsitoEstrazione = giri == 1 ? "L'AI sta leggendo il testo…" : $"L'AI sta leggendo il testo… (seguito, giro {giri})";
            await _ctx.SaveChangesAsync(ct);
            var risposta = await _ia.InterrogaLungaAsync(fonte, richiesta, TokenUscita, TimeoutSecondi, ct);
            tokenIn += risposta.TokenInput + risposta.TokenCacheScritti + risposta.TokenCacheLetti;
            tokenOut += risposta.TokenOutput;
            _log.LogInformation("Estrazione limiti versione {Id}, giro {Giro}: token {In}+{Out} (cache {Cs}/{Cl})", idVersioneFonte, giri,
                risposta.TokenInput, risposta.TokenOutput, risposta.TokenCacheScritti, risposta.TokenCacheLetti);
            var nuove = LeggiProposte(risposta.Testo, out troncata);
            if (nuove.Count == 0) break;
            proposte.AddRange(nuove);
        }
        var testoNorm = RegoleLimiti.NormalizzaTesto(testo);
        var testoComp = RegoleLimiti.Compatta(testoNorm);

        // Schede già presenti nella famiglia (non respinte né superate). Stessa fonte: niente doppioni.
        // Altra fonte (es. D.Lgs. e direttiva con lo stesso valore): la scheda si crea, con una nota per chi valida.
        var esistenti = await _ctx.LimitiNormativi
            .Where(s => consentite.Contains(s.Famiglia) && s.Stato != StatoSchedaLimite.Respinta && s.Stato != StatoSchedaLimite.Superata)
            .Select(s => new
            {
                s.Famiglia, s.Parametro, s.MatriceCategoria, s.Operatore, s.Valore, s.Unita, s.CodiceScheda,
                IdFonte = s.VersioneFonte == null ? (int?)null : s.VersioneFonte.IdFonteNormativa,
                Titolo = s.VersioneFonte == null ? null : s.VersioneFonte.Fonte!.Titolo
            }).ToListAsync(ct);
        var stessaFonte = esistenti.Where(e => e.IdFonte == versione.IdFonteNormativa)
            .Select(e => Chiave(e.Famiglia, e.Parametro, e.MatriceCategoria, e.Operatore, e.Valore, e.Unita)).ToHashSet();
        var altreFonti = esistenti.Where(e => e.IdFonte != versione.IdFonteNormativa)
            .GroupBy(e => Chiave(e.Famiglia, e.Parametro, e.MatriceCategoria, e.Operatore, e.Valore, e.Unita))
            .ToDictionary(g => g.Key, g => g.First());

        int create = 0, scartate = 0, doppie = 0, inAltreFonti = 0;
        var motivi = new List<string>();
        var citazioniScartate = new List<string>();
        foreach (var p in proposte)
        {
            var d = Converti(p, famigliaFonte, consentite, versione.IdVersioneFonte, nomi, out var motivo);
            if (d is null || !RegoleLimiti.CitazionePresente(p.Citazione, testoNorm, testoComp))
            {
                scartate++;
                if (motivi.Count < 8) motivi.Add($"{p.Parametro}: {motivo ?? "citazione non trovata nel testo"}");
                if (motivo is null && citazioniScartate.Count < 3)
                    citazioniScartate.Add($"«{(p.Citazione is { Length: > 160 } c ? c[..160] + "…" : p.Citazione)}»");
                continue;
            }
            var chiave = Chiave(d.Famiglia, d.Parametro, d.MatriceCategoria, d.Operatore, d.Valore, d.Unita);
            if (!stessaFonte.Add(chiave)) { doppie++; continue; }
            if (altreFonti.TryGetValue(chiave, out var gemella))
            {
                inAltreFonti++;
                var nota = $"Stesso limite anche nella scheda {gemella.CodiceScheda} ({gemella.Titolo ?? "inserita a mano"}): validarne una e respingere l'altra se doppia.";
                d.Note = string.IsNullOrEmpty(d.Note) ? nota : (nota + " " + d.Note);
                if (d.Note.Length > 2000) d.Note = d.Note[..2000];
            }
            await _limiti.CreaAsync(d, null, ct, salva: false);
            create++;
        }

        var msg = $"{create} schede proposte, {scartate} scartate, {doppie} già presenti.";
        if (inAltreFonti > 0) msg += $" {inAltreFonti} hanno lo stesso valore di schede di altre fonti (vedi note).";
        if (giri > 1) msg += $" Lette in {giri} giri.";
        if (troncata) msg += " La risposta dell'AI era ancora incompleta: rilanciare l'estrazione per le schede mancanti.";
        if (citazioniScartate.Count > 0) msg += " Esempi di citazioni non trovate: " + string.Join(" ", citazioniScartate);
        if (motivi.Count > 0) msg += " Scartate: " + string.Join("; ", motivi) + (scartate > motivi.Count ? "; …" : "");
        if (avvisoArcLab is not null) msg += " " + avvisoArcLab;
        msg += $" (token {tokenIn:N0}+{tokenOut:N0})";

        versione.StatoEstrazione = StatoEstrazioneLimiti.Completata;
        versione.EsitoEstrazione = msg.Length > 2000 ? msg[..2000] : msg;
        versione.SchedeProposte = create;
        versione.SchedeScartate = scartate;
        await _ctx.SaveChangesAsync(ct);
        return new(create, scartate, doppie, msg);
    }

    private static string Chiave(FamigliaNormativa famiglia, string parametro, string? matrice, OperatoreLimite op, decimal? valore, string? unita) =>
        string.Join("|", (int)famiglia, RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(parametro)),
            RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(matrice)), (int)op,
            valore?.ToString("0.######", CultureInfo.InvariantCulture), RegoleLimiti.Compatta(RegoleLimiti.NormalizzaTesto(unita)));

    /// <summary>Dalla proposta del modello ai dati della scheda; null (con motivo) se non è utilizzabile.</summary>
    public static DatiScheda? Converti(PropostaAi p, FamigliaNormativa famigliaFonte, IReadOnlySet<FamigliaNormativa> consentite,
        int idVersione, IReadOnlyDictionary<int, string> nomiArcLab, out string? motivo)
    {
        motivo = null;
        // 18/10/2026: la famiglia la indica l'AI per ogni scheda; se manca vale quella della fonte.
        var famiglia = RegoleLimiti.DaCodiceFamiglia(p.Famiglia) ?? famigliaFonte;
        if (!consentite.Contains(famiglia)) { motivo = $"famiglia {RegoleLimiti.CodiceFamiglia(famiglia)} non richiesta"; return null; }
        if (!Enum.TryParse<OperatoreLimite>(p.Operatore, true, out var op) || !Enum.IsDefined(op)) { motivo = "operatore non valido"; return null; }
        if (!Enum.TryParse<TipoLimiteNormativo>(p.TipoLimite, true, out var tipo) || !Enum.IsDefined(tipo)) { motivo = "tipo di limite non valido"; return null; }
        var regola = Enum.TryParse<RegolaValutazioneLimite>(p.RegolaValutazione, true, out var r) && Enum.IsDefined(r) ? r : RegolaValutazioneLimite.Puntuale;
        var valore = p.Valore ?? (regola == RegolaValutazioneLimite.PianoCampionamento ? p.PiccolaM : null);
        var d = new DatiScheda
        {
            Famiglia = famiglia, IdVersioneFonte = idVersione,
            Parametro = Taglia(p.Parametro, 300) ?? "", MatriceCategoria = Taglia(p.MatriceCategoria, 500),
            Operatore = op, Valore = valore, ValoreMax = p.ValoreMax, Unita = Taglia(p.Unita, 50),
            N = p.N, C = p.C, M = p.PiccolaM, MMaiuscola = p.GrandeM,
            TipoLimite = tipo, RegolaValutazione = regola,
            Condizioni = Taglia(p.Condizioni, 1000), Riferimento = Taglia(p.Riferimento, 500),
            Citazione = Taglia(p.Citazione, 4000), TestoSegnalazione = Taglia(p.TestoSegnalazione, 500),
            Note = Taglia(p.Note, 2000),
            Abbinamenti = (p.ParametriArcLab ?? []).Where(nomiArcLab.ContainsKey).Distinct()
                .Select(id => new DatiAbbinamento { IdParametroArcLab = id, NomeParametro = nomiArcLab[id] }).ToList()
        };
        motivo = ServizioLimitiNormativi.Controlla(d);
        return motivo is null ? d : null;
    }

    private static string? Taglia(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim()[..max] : s.Trim());

    /// <summary>
    /// Legge l'array JSON della risposta. Se la risposta è stata troncata (limite di token), recupera gli oggetti completi.
    /// </summary>
    public static List<PropostaAi> LeggiProposte(string testo, out bool troncata)
    {
        troncata = false;
        if (string.IsNullOrWhiteSpace(testo)) return [];
        var inizio = testo.IndexOf('[');
        if (inizio < 0) return [];
        var fine = testo.LastIndexOf(']');
        if (fine > inizio)
        {
            try { return JsonSerializer.Deserialize<List<PropostaAi>>(testo[inizio..(fine + 1)], Opzioni) ?? []; }
            catch (JsonException) { /* forse troncata: si prova a recuperare */ }
        }
        troncata = true;
        // Taglia all'ultimo oggetto completo «}» seguito da virgola o fine e chiude l'array.
        var corpo = testo[inizio..];
        for (var i = corpo.LastIndexOf('}'); i > 0; i = corpo.LastIndexOf('}', i - 1))
        {
            try { return JsonSerializer.Deserialize<List<PropostaAi>>(corpo[..(i + 1)] + "]", Opzioni) ?? []; }
            catch (JsonException) { }
        }
        return [];
    }
}
