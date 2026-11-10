using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>Un problema trovato nei dizionari dei modelli: non blocca, si segnala.</summary>
/// <param name="Codice">Il campo a cui si riferisce.</param>
/// <param name="Problema">In una riga, cosa non torna.</param>
/// <param name="Dettaglio">Dove guardare.</param>
public sealed record SegnalazioneTendina(string Codice, string Problema, string Dettaglio);

/// <summary>Un passo della check list.</summary>
public sealed record PassoChecklist(string Codice, string Nome);

/// <summary>Una domanda nell'elenco, con quante voci ha.</summary>
public sealed record VoceSintesi(
    int IdVoceChecklist, string Codice, string Etichetta, string? Gruppo,
    string? PassoProposto, string? SpegnimentoProposto,
    int Opzioni, int OpzioniLegateAlModello, bool Attivo);

/// <summary>Una voce di tendina, con il documento a cui e' legata se lo e'.</summary>
public sealed record OpzioneSintesi(string Valore, string? Documento, bool Attivo, bool DaCompilazione);

/// <summary>Com'e' andata la lettura dei dizionari dai modelli approvati.</summary>
public sealed record EsitoEstrazioneVoci(
    int ModelliLetti,
    int VociTrovate,
    int OpzioniTrovate,
    int OpzioniLegateAlModello,
    int VociCreate,
    int VociAggiornate,
    int OpzioniCreate,
    int OpzioniRiattivate,
    int OpzioniDisattivate,
    IReadOnlyList<SegnalazioneTendina> Segnalazioni,
    bool Scritto);

/// <summary>
/// Costruisce il dizionario della check list leggendo le tendine dei
/// modelli approvati (05/11/2026).
///
/// Il principio, deciso il 03/11: le voci delle tendine non si digitano.
/// Stanno gia' dentro i controlli contenuto dei modelli, ed e' da li'
/// che si leggono. Cosi' il dizionario non puo' divergere dai documenti
/// che genera: se Vaccarini aggiunge un fornitore alla tendina di un
/// modello, alla revisione successiva la voce si propone da sola.
///
/// Due regole che vengono dai modelli veri, non da come sarebbe comodo
/// che fossero:
///
/// 1. **L'identita' di un campo e' il tag piu' la sua occorrenza nel
///    blocco**, come fa il motore con <c>fillPos</c> («ETICHETTA#n»).
///    Dentro la scheda dell'archivio informatico «LUOGO DI CONSERVAZIONE»
///    compare due volte: una per i dati sul PC, con «Cloud» fra le voci,
///    e una per quelli del gestionale, con «Server farm del gestionale».
///    Stesso tag, due domande diverse.
///
/// 2. **Le tendine non sono sempre le stesse in tutti i documenti**, e
///    per progetto: nella lettera alla web agency fra gli interessati
///    c'e' «PROSPECT», in quella all'Experience fra i trattamenti ci
///    sono «Parchi tematici» e «Strutture termali». Dove le liste
///    differiscono, le opzioni restano legate al proprio modello.
/// </summary>
public class ServizioVociChecklist
{
    private readonly ContestoLaboratorioAI _db;

    public ServizioVociChecklist(ContestoLaboratorioAI db) => _db = db;

    /// <summary>
    /// I sei passi della check list, dalla specifica approvata il 05/11.
    /// Sono gli stessi del prototipo: cambiarli qui li cambia ovunque.
    /// </summary>
    public static readonly IReadOnlyList<PassoChecklist> Passi =
    [
        new("CLIENTE",      "1. Cliente, sedi, data e revisione"),
        new("STRUTTURA",    "2. Struttura e finalità"),
        new("AUTORIZZATI",  "3. Autorizzati"),
        new("RESPONSABILI", "4. Responsabili"),
        new("SISTEMI",      "5. Sistemi, archivi e misure"),
        new("FIRME",        "6. Firme, generazione e invio")
    ];

    /// <summary>Le domande di un tipo di manuale, con il conto delle voci.</summary>
    public async Task<IReadOnlyList<VoceSintesi>> ElencoAsync(string codiceTipo, CancellationToken ct = default)
    {
        var voci = await _db.VociChecklist.AsNoTracking()
            .Where(v => v.TipoManuale!.Codice == codiceTipo)
            .OrderBy(v => v.Ordine)
            .Select(v => new
            {
                v.IdVoceChecklist, v.Codice, v.Etichetta, v.Gruppo, v.Attivo,
                Opzioni = v.Opzioni.Count(o => o.Attivo),
                PerModello = v.Opzioni.Count(o => o.Attivo && o.IdModelloDocumento != null)
            })
            .ToListAsync(ct);

        return voci.Select(v => new VoceSintesi(
            v.IdVoceChecklist, v.Codice, v.Etichetta, v.Gruppo,
            Proposta(v.Codice), DaSpegnere(v.Codice),
            v.Opzioni, v.PerModello, v.Attivo)).ToList();
    }

    /// <summary>
    /// Il passo che il codice fa pensare, da rivedere a mano. E' un
    /// aiuto per non assegnare 85 domande una per una, non una regola:
    /// il nome di un campo dice dove sta nel documento, non in quale
    /// passo lo si chiede al cliente. Vince la prima regola che calza,
    /// e l'ordine conta — BACKUP_AUTORIZZATO e' una persona, non un
    /// sistema.
    /// </summary>
    private static readonly (string Inizio, string Passo)[] Proposte =
    [
        ("SEDE_",                      "CLIENTE"),
        ("INFORMAZIONI_FORNITE_DA",    "CLIENTE"),
        ("CONTRATTO_",                 "CLIENTE"),
        ("PREZZO_",                    "CLIENTE"),
        ("PAGAMENTO_",                 "CLIENTE"),

        ("INL_",                       "STRUTTURA"),
        ("DVR_",                       "STRUTTURA"),
        ("SCHERMO_",                   "STRUTTURA"),
        ("DURATA_CONSERVAZIONE",       "STRUTTURA"),

        ("BACKUP_AUTORIZZATO",         "AUTORIZZATI"),
        ("CREDENZIALI_AUTORIZZATO",    "AUTORIZZATI"),
        ("SOSTITUTO_LR",               "AUTORIZZATI"),
        ("AUTORIZZATO_",               "AUTORIZZATI"),
        ("FORMAZIONE_",                "AUTORIZZATI"),

        ("RESP_",                      "RESPONSABILI"),

        ("ARCHIVIO_",                  "SISTEMI"),
        ("BACKUP_",                    "SISTEMI"),
        ("CRM_", "SISTEMI"), ("CRS_", "SISTEMI"), ("CRO_", "SISTEMI"),
        ("WIFI_",                      "SISTEMI"),
        ("BUSTE_PAGA_",                "SISTEMI"),
        ("MISURA_", "SISTEMI"), ("MISURE_", "SISTEMI"),
        ("EMAIL_",                     "SISTEMI"),
        ("TEMPO_RIPRISTINO",           "SISTEMI")
    ];

    private static string? Proposta(string codice) =>
        Proposte.FirstOrDefault(r => codice.StartsWith(r.Inizio, StringComparison.Ordinal)).Passo;

    /// <summary>
    /// Le domande che il consulente non dovrebbe vedersi chiedere,
    /// perche' il valore arriva da solo: dall'anagrafica del cliente,
    /// dalla testata del documento, o da una tabella in cui il dato e'
    /// gia' stato inserito una volta.
    ///
    /// Non ci sono i campi dei blocchi ripetuti — archivi, responsabili —
    /// che sono chiesti eccome, ma dentro la loro scheda invece che in
    /// una schermata a se'. Spegnerli toglierebbe il dizionario alla
    /// scheda che lo usa.
    ///
    /// E' una proposta da rivedere, non una regola: dice da dove sembra
    /// che il valore arrivi, e il motivo si legge accanto alla domanda.
    /// </summary>
    private static readonly (string Inizio, string Motivo)[] Automatiche =
    [
        // Anagrafica del cliente, da ArcLab o dalla partita IVA
        ("RAGIONE_SOCIALE",        "anagrafica del cliente"),
        ("PARTITA_IVA",            "anagrafica del cliente"),
        ("CODICE_FISCALE",         "anagrafica del cliente"),
        ("CODICE_ATECO",           "anagrafica del cliente"),
        ("ATTIVITA_ESERCITATA",    "anagrafica del cliente"),
        ("SITI_WEB",               "anagrafica del cliente"),
        ("EMAIL",                  "anagrafica del cliente"),
        ("PEC",                    "anagrafica del cliente"),
        ("TELEFONO",               "anagrafica del cliente"),
        ("SEDE_LEGALE_",           "anagrafica del cliente"),

        // Testata e date
        ("REVISIONE_",             "testata del documento"),
        ("DATA_DOCUMENTI",         "testata del documento"),
        ("DATA_RIESAME_DPIA",      "calcolato: data dei documenti piu' due anni"),

        // Dati gia' inseriti in una tabella
        ("SEDE_TRATTAMENTO_",      "sedi del trattamento"),
        ("AUTORIZZATO_",           "elenco degli autorizzati"),
        ("LR_",                    "elenco degli autorizzati"),
        ("BACKUP_AUTORIZZATO",     "elenco degli autorizzati"),
        ("CREDENZIALI_AUTORIZZATO","elenco degli autorizzati"),
        ("SOSTITUTO_LR",           "elenco degli autorizzati"),
        ("FUNZIONE_VISIONE",       "ricavato da chi accede alle immagini"),
        ("CONTRATTO_",             "contratto di fornitura"),
        ("PREZZO_",                "contratto di fornitura"),

        // Numerati dal motore
        ("ARCHIVIO_CARTACEO_NUMERO",    "numerato dal programma"),
        ("ARCHIVIO_INFORMATICO_NUMERO", "numerato dal programma")
    ];

    private static string? DaSpegnere(string codice) =>
        Automatiche.FirstOrDefault(r => codice.StartsWith(r.Inizio, StringComparison.Ordinal)).Motivo;

    /// <summary>Le voci di tendina di una domanda, per guardarle.</summary>
    public async Task<IReadOnlyList<OpzioneSintesi>> OpzioniAsync(int idVoce, CancellationToken ct = default) =>
        await _db.OpzioniVoce.AsNoTracking()
            .Where(o => o.IdVoceChecklist == idVoce)
            .OrderBy(o => o.IdModelloDocumento == null ? 0 : 1)
            .ThenBy(o => o.ModelloDocumento!.Ordine)
            .ThenBy(o => o.Ordine)
            .Select(o => new OpzioneSintesi(
                o.Valore,
                o.IdModelloDocumento == null ? null : o.ModelloDocumento!.Nome,
                o.Attivo, o.DaCompilazione))
            .ToListAsync(ct);

    /// <summary>
    /// Assegna il passo a una o piu' domande. Il passo e' l'unica cosa
    /// che dai modelli non si ricava: la decide una persona, una volta.
    /// </summary>
    public async Task<int> ImpostaPassoAsync(
        IReadOnlyCollection<int> idVoci, string? passo, CancellationToken ct = default)
    {
        if (idVoci.Count == 0) return 0;
        if (passo is not null && !Passi.Any(p => p.Codice == passo)) return 0;

        var voci = await _db.VociChecklist.Where(v => idVoci.Contains(v.IdVoceChecklist)).ToListAsync(ct);
        foreach (var v in voci) v.Gruppo = passo;

        await _db.SaveChangesAsync(ct);
        return voci.Count;
    }

    /// <summary>
    /// Legge i modelli approvati e, se <paramref name="scrivi"/> e' vero,
    /// aggiorna le voci della check list. Con scrivi falso non tocca
    /// niente e dice solo cosa farebbe: si guarda prima.
    /// </summary>
    public async Task<EsitoEstrazioneVoci> EstraiAsync(
        string codiceTipo, bool scrivi, string? utente = null, CancellationToken ct = default)
    {
        var tipo = await _db.TipiManuale.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Codice == codiceTipo, ct);
        if (tipo is null)
            return new EsitoEstrazioneVoci(0, 0, 0, 0, 0, 0, 0, 0, 0,
                [new SegnalazioneTendina("", "Tipo di manuale sconosciuto.", codiceTipo)], false);

        var segnalazioni = new List<SegnalazioneTendina>();
        var modelli = await LeggiModelliApprovatiAsync(tipo.IdTipoManuale, segnalazioni, ct);
        var campi = Raccogli(modelli, VociGovernanti.Codici(tipo.Codice), segnalazioni);

        // Quante voci verrebbero scritte, e quante restano legate a un
        // singolo modello: serve a sapere cosa si sta per scrivere.
        var opzioni = campi.Sum(c => c.Comuni?.Count ?? c.PerModello.Values.Sum(o => o.Count));
        var legate = campi.Where(c => c.Comuni is null).Sum(c => c.PerModello.Values.Sum(o => o.Count));

        if (!scrivi)
            return new EsitoEstrazioneVoci(
                modelli.Count, campi.Count, opzioni, legate, 0, 0, 0, 0, 0, segnalazioni, false);

        var conteggi = await ScriviAsync(tipo.IdTipoManuale, tipo.Codice, campi, utente, ct);

        return new EsitoEstrazioneVoci(
            modelli.Count, campi.Count, opzioni, legate,
            conteggi.VociCreate, conteggi.VociAggiornate,
            conteggi.OpzioniCreate, conteggi.OpzioniRiattivate, conteggi.OpzioniDisattivate,
            segnalazioni, true);
    }

    /// <summary>
    /// Spegne o riaccende una o piu' domande. Spenta vuol dire che la
    /// check list non la chiede: il suo valore arriva da un'altra parte
    /// — il nome di un autorizzato, l'indirizzo di una sede, il prezzo
    /// nel contratto. Non si cancella, perche' il campo nel modello
    /// resta e un domani potrebbe tornare a essere una domanda.
    /// </summary>
    public async Task<int> ImpostaAttivoAsync(
        IReadOnlyCollection<int> idVoci, bool attivo, CancellationToken ct = default)
    {
        if (idVoci.Count == 0) return 0;

        var voci = await _db.VociChecklist.Where(v => idVoci.Contains(v.IdVoceChecklist)).ToListAsync(ct);
        foreach (var v in voci) v.Attivo = attivo;

        await _db.SaveChangesAsync(ct);
        return voci.Count;
    }

    // ── Lettura ────────────────────────────────────────────────────

    private sealed record ModelloLetto(
        int IdModelloDocumento, string Codice, string Nome,
        IReadOnlyList<TendinaTrovata> Tendine);

    /// <summary>
    /// L'ultima revisione approvata di ogni modello del tipo. I modelli
    /// senza revisione approvata non entrano: il loro contenuto non e'
    /// ancora quello ufficiale, e un dizionario costruito su una bozza
    /// direbbe cose che nei documenti non ci sono.
    /// </summary>
    private async Task<List<ModelloLetto>> LeggiModelliApprovatiAsync(
        int idTipoManuale, List<SegnalazioneTendina> segnalazioni, CancellationToken ct)
    {
        var modelli = await _db.ModelliDocumento.AsNoTracking()
            .Where(m => m.Attivo && m.IdTipoManuale == idTipoManuale)
            .OrderBy(m => m.Ordine)
            .Select(m => new { m.IdModelloDocumento, m.Codice, m.Nome })
            .ToListAsync(ct);

        var letti = new List<ModelloLetto>(modelli.Count);

        foreach (var m in modelli)
        {
            // Una revisione per volta: i .docx pesano centinaia di KB e
            // tenerli tutti in memoria insieme non serve a niente.
            var revisione = await _db.RevisioniModello.AsNoTracking()
                .Where(r => r.Attivo
                         && r.IdModelloDocumento == m.IdModelloDocumento
                         && r.Stato == StatoRevisioneModello.Approvato)
                .OrderByDescending(r => r.NumeroRevisione)
                .Select(r => new { r.ContenutoDocx })
                .FirstOrDefaultAsync(ct);

            if (revisione is null) continue;

            try
            {
                letti.Add(new ModelloLetto(
                    m.IdModelloDocumento, m.Codice, m.Nome,
                    TendineModello.Leggi(revisione.ContenutoDocx)));
            }
            catch (Exception ex)
            {
                segnalazioni.Add(new SegnalazioneTendina(
                    m.Codice, "Il modello non si e' potuto leggere.", ex.Message));
            }
        }

        return letti;
    }

    // ── Raccolta ───────────────────────────────────────────────────

    /// <summary>Un campo compilabile con il suo dizionario.</summary>
    private sealed class Campo
    {
        public required string Codice { get; init; }
        public string? Etichetta { get; set; }
        public int Ordine { get; set; }
        public TipoCampoChecklist Tipo { get; set; } = TipoCampoChecklist.Testo;

        /// <summary>Le opzioni per modello, nell'ordine del modello.</summary>
        public Dictionary<int, List<string>> PerModello { get; } = new();

        /// <summary>Le opzioni valide per tutti, quando i modelli concordano.</summary>
        public List<string>? Comuni { get; set; }
    }

    private static List<Campo> Raccogli(
        List<ModelloLetto> modelli, IReadOnlySet<string> governanti, List<SegnalazioneTendina> segnalazioni)
    {
        var campi = new Dictionary<string, Campo>(StringComparer.Ordinal);
        var ordine = 0;

        foreach (var m in modelli)
            foreach (var t in m.Tendine)
            {
                // Tre domande che governano compaiono anche nei modelli
                // come campi di testo: le ore di conservazione, di
                // formazione e di ripristino. Le possiede il codice, con
                // il loro tipo e il valore predefinito, e leggerle da qui
                // le ridurrebbe a testo libero.
                if (governanti.Contains(t.Codice)) continue;

                if (!campi.TryGetValue(t.Codice, out var c))
                    campi[t.Codice] = c = new Campo { Codice = t.Codice, Ordine = ordine++ };

                c.Etichetta ??= t.Etichetta;
                if (t.Opzioni.Count > 0) c.Tipo = TipoCampoChecklist.Tendina;
                else if (t.Data && c.Tipo == TipoCampoChecklist.Testo) c.Tipo = TipoCampoChecklist.Data;

                // Lo stesso campo due volte nello stesso documento non
                // succede: l'occorrenza fa parte dell'identita'.
                c.PerModello[m.IdModelloDocumento] = t.Opzioni.ToList();
            }

        // I modelli concordano? Allora le opzioni valgono per tutti.
        foreach (var c in campi.Values)
        {
            // Un documento in cui il campo non ha elenco non sta dicendo
            // una cosa diversa: non sta dicendo niente. Contarlo come una
            // versione del dizionario legherebbe le voci al solo documento
            // che l'elenco ce l'ha, e negli altri sparirebbero.
            var conElenco = c.PerModello.Where(x => x.Value.Count > 0).ToList();

            if (conElenco.Count == 0) { c.Comuni = []; continue; }

            var chiavi = conElenco.Select(x => Chiave(x.Value)).Distinct(StringComparer.Ordinal).ToList();
            if (chiavi.Count <= 1)
            {
                c.Comuni = conElenco[0].Value;
                continue;
            }

            // Restano legate al modello solo quelle dei documenti che un
            // elenco ce l'hanno, e che non vanno d'accordo fra loro.
            c.PerModello.Clear();
            foreach (var (idModello, opzioni) in conElenco) c.PerModello[idModello] = opzioni;

            segnalazioni.Add(new SegnalazioneTendina(
                c.Codice,
                "Tendine diverse da un documento all'altro: le opzioni restano legate al proprio modello.",
                $"{conElenco.Count} documenti, {chiavi.Count} versioni"));
        }

        return campi.Values.OrderBy(c => c.Ordine).ToList();
    }

    private static string Chiave(IEnumerable<string> opzioni) =>
        string.Join('\u001f', opzioni.OrderBy(o => o, StringComparer.Ordinal));

    // ── Scrittura ──────────────────────────────────────────────────

    private sealed record Conteggi(
        int VociCreate, int VociAggiornate, int OpzioniCreate, int OpzioniRiattivate, int OpzioniDisattivate);

    private async Task<Conteggi> ScriviAsync(
        int idTipoManuale, string codiceTipo, List<Campo> campi, string? utente, CancellationToken ct)
    {
        var esistenti = await _db.VociChecklist
            .Include(v => v.Opzioni)
            .Where(v => v.IdTipoManuale == idTipoManuale)
            .ToDictionaryAsync(v => v.Codice, StringComparer.Ordinal, ct);

        int create = 0, aggiornate = 0, oCreate = 0, oRiattivate = 0, oDisattivate = 0;

        foreach (var campo in campi)
        {
            if (!esistenti.TryGetValue(campo.Codice, out var voce))
            {
                voce = new VoceChecklist
                {
                    IdTipoManuale = idTipoManuale,
                    Codice = campo.Codice,
                    // Il passo non si ricava dai modelli: viene dalla
                    // rassegna gia' fatta, che viaggia con il codice. Si
                    // assegna solo alla nascita, cosi' chi lo sposta dalla
                    // pagina non se lo vede tornare indietro. Una domanda
                    // nuova nasce senza passo, e si vede.
                    Gruppo = PassiDecisi.Per(codiceTipo, campo.Codice),
                    CreatoIlUtc = DateTime.UtcNow,
                    CreatoDa = utente
                };
                _db.VociChecklist.Add(voce);
                create++;
            }
            else aggiornate++;

            voce.Etichetta = campo.Etichetta ?? Leggibile(campo.Codice);
            voce.TipoCampo = campo.Tipo;
            // Le domande che governano i documenti stanno sotto il mille e
            // vengono prima: dentro un passo si chiede «c'e' la
            // videosorveglianza?» prima di «dov'e' il DVR?».
            voce.Ordine = 1000 + campo.Ordine;
            // Attivo non si tocca: una domanda spenta a mano — perche' il
            // suo valore viene da un'altra parte — non deve riaccendersi
            // alla rilettura dei modelli, o la rassegna si perde.

            // Cosa dovrebbe esserci: (modello, valore, ordine).
            var attese = new List<(int? Modello, string Valore, int Ordine)>();
            if (campo.Comuni is not null)
                attese.AddRange(campo.Comuni.Select((v, i) => ((int?)null, v, i)));
            else
                foreach (var (idModello, opzioni) in campo.PerModello)
                    attese.AddRange(opzioni.Select((v, i) => ((int?)idModello, v, i)));

            // Le voci scritte a mano dai consulenti sono loro: non si toccano.
            var daiModelli = voce.Opzioni.Where(o => !o.DaCompilazione).ToList();

            foreach (var (modello, valore, ordine) in attese)
            {
                var opzione = daiModelli.FirstOrDefault(
                    o => o.IdModelloDocumento == modello && string.Equals(o.Valore, valore, StringComparison.Ordinal));

                if (opzione is null)
                {
                    voce.Opzioni.Add(new OpzioneVoce
                    {
                        IdModelloDocumento = modello,
                        Valore = valore,
                        Ordine = ordine,
                        CreatoIlUtc = DateTime.UtcNow,
                        CreatoDa = utente
                    });
                    oCreate++;
                    continue;
                }

                opzione.Ordine = ordine;
                if (!opzione.Attivo) { opzione.Attivo = true; oRiattivate++; }
            }

            // Sparita dai modelli: si spegne, non si cancella. I documenti
            // gia' prodotti la citano, e devono restare spiegabili.
            var atteseChiave = attese.Select(a => (a.Modello, a.Valore)).ToHashSet();
            foreach (var o in daiModelli.Where(o => o.Attivo && !atteseChiave.Contains((o.IdModelloDocumento, o.Valore))))
            {
                o.Attivo = false;
                oDisattivate++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new Conteggi(create, aggiornate, oCreate, oRiattivate, oDisattivate);
    }

    /// <summary>Ripiego quando il modello non porta un'etichetta: ARCHIVIO_CARTACEO_LUOGO → «Archivio cartaceo luogo».</summary>
    private static string Leggibile(string codice)
    {
        var testo = codice.Replace('_', ' ').ToLowerInvariant();
        return testo.Length == 0 ? codice : char.ToUpperInvariant(testo[0]) + testo[1..];
    }
}
