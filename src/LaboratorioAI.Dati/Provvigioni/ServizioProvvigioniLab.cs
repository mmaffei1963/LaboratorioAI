using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Provvigioni;

/// <summary>
/// Collega i dati importati da AZW (<c>provv.Azw*</c>) agli accordi dei commerciali e ai motori di calcolo,
/// e gestisce versioni degli accordi e chiusura dei trimestri.
///
/// Ogni movimento è calcolato con l'accordo valido alla sua data (incasso o nota); ogni voce dell'accordo
/// ha la sua base di calcolo (centri di costo e voci AZW). Un commerciale senza accordo attivo non ha calcolo.
/// Il commerciale di un cliente deriva dal suo codice venditore (CODVET) in AZW.
/// </summary>
public sealed class ServizioProvvigioniLab
{
    private readonly ContestoLaboratorioAI _db;
    private readonly MotoreProvvigioniLab _motore = new();
    private readonly MotoreProvvigioniCarloni _motoreCarloni = new();

    public const string BaseLaboratorio = "Laboratorio";
    public const string BaseTutto = "Tutto il fatturato";
    public const string BaseFuoriLaboratorio = "Fuori laboratorio";
    public const string BaseAreaAnalisi = "Area analisi";
    public const string BaseAreaFormazione = "Area formazione";
    public const string BaseAreaConsulenza = "Area consulenza";
    public const string SegnalatoreAzienda = "MARE.A";

    /// <summary>Codici ArcLab (mansione.IdMansione in commessa.CodCommessa) verificati il 5/10/2026.</summary>
    private const int MansioneZatterin = 2142476122;
    private const int MansioneCarloni = 1344149892;

    /// <summary>Data assegnata in ArcLab alle commesse caricate in blocco: non è la data reale del contratto.</summary>
    public static readonly DateTime DataCaricamentoInBlocco = new(2022, 1, 1);

    private static readonly VoceProvvigione[] VociLaboratorio = { VoceProvvigione.Nuovo, VoceProvvigione.Mantenimento, VoceProvvigione.Sviluppo };

    /// <summary>
    /// Voci legate ai servizi venduti dal commerciale: contano solo le righe di fattura con le voci AZW delle sue commesse
    /// (tipo di incarico → voce, tabella <c>cons.VociIncarico</c>). Così su un cliente «misto» non entrano le analisi
    /// né i servizi seguiti da altri.
    /// </summary>
    private static readonly HashSet<VoceProvvigione> VociPerIncarico = new()
    {
        VoceProvvigione.ExtraLaboratorio, VoceProvvigione.ConvenzioneIndicataAnno1, VoceProvvigione.ConvenzioneIndicataAnni2a5,
        VoceProvvigione.ConvenzioneSegnalata, VoceProvvigione.AltriServizi, VoceProvvigione.ContrattoOccasionale
    };

    /// <summary>Tipo commessa ArcLab delle analisi di laboratorio (le altre con riferimento sono formazione 4 e consulenza 5).</summary>
    private const int TipoCommessaAnalisi = 3;

    /// <summary>Tipo commessa ArcLab della consulenza: le commesse occasionali di questo tipo aprono i «contratti occasionali».</summary>
    private const int TipoCommessaConsulenza = 5;

    /// <summary>Per cliente, le voci di fattura AZW dei servizi venduti (dalle commesse con il riferimento del commerciale).</summary>
    private async Task<(Dictionary<string, HashSet<string>> PerCliente, int SenzaVoce)> VociVenduteAsync(
        IReadOnlyCollection<CommessaArcLab> commesse, CancellationToken ct)
    {
        var fasce = await _db.VociIncarico.AsNoTracking().Where(f => f.Attivo).ToListAsync(ct);
        string? VoceDi(int? t) => t is int x
            ? fasce.FirstOrDefault(f => x >= f.IdTipoIncaricoDa && x <= f.IdTipoIncaricoA)?.Voce?.Trim().ToUpperInvariant()
            : null;
        var perCliente = commesse.GroupBy(c => c.CodiceCliente, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => g.Select(c => VoceDi(c.IdTipoIncarico)).Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToHashSet(StringComparer.Ordinal),
                StringComparer.Ordinal);
        var senzaVoce = commesse.Count(c => string.IsNullOrEmpty(VoceDi(c.IdTipoIncarico)));
        return (perCliente, senzaVoce);
    }

    /// <summary>
    /// Quali righe di fattura contano per le voci «per incarico»: le voci AZW vendute dal commerciale a quel cliente
    /// (dalle sue commesse) oppure, per i clienti con un'eccezione «intera area», tutte le righe della base dell'eccezione
    /// nel periodo indicato. Un cliente senza commesse in ArcLab (convenzione inserita a mano) usa tutta la base.
    /// </summary>
    private sealed class FiltroRighe
    {
        private readonly Dictionary<string, HashSet<string>> _vendute;
        private readonly ILookup<string, (BaseCalcolo Base, DateTime Dal, DateTime? Al)> _eccezioni;

        public FiltroRighe(Dictionary<string, HashSet<string>> vendute, ILookup<string, (BaseCalcolo Base, DateTime Dal, DateTime? Al)> eccezioni)
        {
            _vendute = vendute;
            _eccezioni = eccezioni;
        }

        public bool Consentita(VoceProvvigione voce, string cliente, string centro, string voceAzw, DateTime data) =>
            !VociPerIncarico.Contains(voce)
            || (_vendute.TryGetValue(cliente, out var v)
                ? v.Contains((voceAzw ?? "").Trim().ToUpperInvariant()) || InEccezione(cliente, centro, voceAzw ?? "", data)
                : !_eccezioni.Contains(cliente) || InEccezione(cliente, centro, voceAzw ?? "", data));

        public bool InEccezione(string cliente, string centro, string voceAzw, DateTime data) =>
            _eccezioni[cliente].Any(e => data.Date >= e.Dal && (e.Al is null || data.Date <= e.Al) && e.Base.Include(centro, voceAzw));
    }

    private async Task<FiltroRighe> FiltroAsync(int idCommerciale, Dictionary<string, HashSet<string>> vendute,
        Dictionary<int, BaseCalcolo> basi, CancellationToken ct)
    {
        var ecc = await _db.EccezioniArea.AsNoTracking().Where(e => e.IdCommerciale == idCommerciale && e.Attivo).ToListAsync(ct);
        var lookup = ecc.Where(e => basi.ContainsKey(e.IdBaseCalcolo))
            .ToLookup(e => e.CodiceCliente, e => (basi[e.IdBaseCalcolo], e.DataInizio.Date, e.DataFine?.Date), StringComparer.Ordinal);
        return new FiltroRighe(vendute, lookup);
    }

    public ServizioProvvigioniLab(ContestoLaboratorioAI db) => _db = db;

    /// <summary>Voci ammesse per tipo di accordo (il modo di riconoscerle è nel codice dei motori).</summary>
    public static IReadOnlyList<VoceProvvigione> VociDelTipo(TipoAccordo tipo) => tipo switch
    {
        TipoAccordo.Laboratorio => new[] { VoceProvvigione.Nuovo, VoceProvvigione.Mantenimento, VoceProvvigione.Sviluppo, VoceProvvigione.ExtraLaboratorio },
        _ => new[]
        {
            VoceProvvigione.ConvenzioneIndicataAnno1, VoceProvvigione.ConvenzioneIndicataAnni2a5,
            VoceProvvigione.ConvenzioneSegnalata, VoceProvvigione.AltriServizi,
            VoceProvvigione.ContrattoOccasionale, VoceProvvigione.ContrattoAnalisi
        }
    };

    public async Task<bool> CalcoloAttivoAsync(int idCommerciale, CancellationToken ct = default) =>
        await _db.AccordiProvvigioni.AsNoTracking()
            .AnyAsync(a => a.IdCommerciale == idCommerciale && a.Stato == StatoAccordo.Attivo, ct);

    /// <summary>True se il commerciale ha un accordo a convenzioni: serve la schermata «Convenzioni».</summary>
    public async Task<bool> UsaConvenzioniAsync(int idCommerciale, CancellationToken ct = default) =>
        await _db.AccordiProvvigioni.AsNoTracking()
            .AnyAsync(a => a.IdCommerciale == idCommerciale && a.Tipo == TipoAccordo.Convenzioni && a.Stato != StatoAccordo.Annullato, ct);

    // ── Popolamento iniziale ──────────────────────────────────────────

    /// <summary>Crea commerciali, codici venditore, basi di calcolo e accordi iniziali se non esistono. Idempotente.</summary>
    public async Task InizializzaAsync(CancellationToken ct = default)
    {
        var ludo = await AsicuraAsync("Ludovico Zatterin", new[] { "ZAT", "HSE" }, MansioneZatterin, ct);
        var carloni = await AsicuraAsync("Andrea Carloni", new[] { "CAR" }, MansioneCarloni, ct);

        var lab = await AssicuraBaseAsync(BaseLaboratorio,
            "Analisi di laboratorio: centro 04 con le voci ANA, ANE, AMB, LEV, LEC, LEG (perimetro dell'accordo di Ludo).",
            false, "04:ANA;04:ANE;04:AMB;04:LEV;04:LEC;04:LEG", ct);
        var tutto = await AssicuraBaseAsync(BaseTutto, "Tutte le righe di fattura, di qualsiasi centro di costo e voce.", true, "", ct);
        var fuoriLab = await AssicuraBaseAsync(BaseFuoriLaboratorio,
            "Consulenza e formazione: centri 05, 06, FOR, FON, PRY e le voci di consulenza del centro 04 (626, HAC, AT, RIF, RSP, PRY, F62, FHA, FON). Non comprende le analisi.",
            false, "04:626;04:AT;04:F62;04:FHA;04:FON;04:HAC;04:PRY;04:RIF;04:RSP;05:*;06:*;FON:*;FOR:*;PRY:*", ct);

        // Aree per compensi e eccezioni «intera area» (tipi commessa ArcLab: 3 analisi, 4 formazione, 5 consulenza).
        // Ricavate dal fatturato AZW 2023-2026 per centro e voce; si correggono dalla pagina delle basi.
        var areaAnalisi = await AssicuraBaseAsync(BaseAreaAnalisi,
            "Tutte le analisi di laboratorio (tipo commessa 3): centro 04, voci di analisi e legionella.",
            false, "04:ANA;04:ANE;04:AMB;04:AND;04:ANR;04:ACQ;04:ARI;04:LEV;04:LEC;04:LEG;04:LEP;04:LPR;04:LBA;04:LEB", ct);
        await AssicuraBaseAsync(BaseAreaFormazione,
            "Tutta la formazione (tipo commessa 4): centri FOR e FON e le voci di formazione negli altri centri.",
            false, "FOR:*;FON:*;04:FPS;04:O62;04:F62;04:FHA;04:FOA;04:OHA;PRY:SFU;PRY:HFU;PRY:FOA;PRY:FPS;PRY:O62", ct);
        var areaConsulenza = await AssicuraBaseAsync(BaseAreaConsulenza,
            "Tutta la consulenza (tipo commessa 5): sicurezza, HACCP, RSPP, privacy, assistenza tecnica, igienico-sanitaria.",
            false, "05:*;06:*;PRY:626;PRY:HAC;PRY:PRY;PRY:AT;PRY:RSP;PRY:RIF;04:AT;04:PRY;04:MAP", ct);

        // MARE.A come segnalatore predefinito delle convenzioni «segnalate», allo 0%.
        if (!await _db.Segnalatori.AnyAsync(x => x.Tipo == TipoSegnalatore.Azienda, ct))
        {
            _db.Segnalatori.Add(new Segnalatore
            {
                Nome = SegnalatoreAzienda, Tipo = TipoSegnalatore.Azienda,
                Note = "Clienti già MARE.A o arrivati direttamente all'azienda. Predefinito per le convenzioni «segnalate».",
                Condizioni = { new CondizioneSegnalatore
                {
                    Nome = "Segnalazione interna", Percentuale = 0m, Ambito = AmbitoCompenso.Area, IdBaseCalcolo = tutto.IdBaseCalcolo
                } }
            });
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.AccordiProvvigioni.AnyAsync(a => a.IdCommerciale == ludo.IdCommerciale, ct))
        {
            _db.AccordiProvvigioni.Add(new AccordoProvvigioni
            {
                IdCommerciale = ludo.IdCommerciale, Nome = "Accordo provvigioni Laboratorio", Tipo = TipoAccordo.Laboratorio,
                Stato = StatoAccordo.Attivo, ValidoDal = new DateTime(2025, 9, 1), DataClientiEsistenti = new DateTime(2025, 9, 1),
                Note = "Creato automaticamente con i valori in vigore al 3/10/2026 (accordo firmato e bozza di integrazione).",
                Voci =
                {
                    new VoceAccordo { Voce = VoceProvvigione.Nuovo, Percentuale = 5m, IdBaseCalcolo = lab.IdBaseCalcolo,
                        Descrizione = "Clienti senza fatture nella base nei 24 mesi precedenti: per 12 mesi dalla prima fattura." },
                    new VoceAccordo { Voce = VoceProvvigione.Mantenimento, Percentuale = 3m, IdBaseCalcolo = lab.IdBaseCalcolo,
                        Descrizione = "Dal 13° mese dalla prima fattura, finché il cliente fattura; dopo 24 mesi senza fatture torna nuovo." },
                    new VoceAccordo { Voce = VoceProvvigione.Sviluppo, Percentuale = 2m, IdBaseCalcolo = lab.IdBaseCalcolo,
                        Descrizione = "Clienti MARE.A esistenti dell'elenco «in sviluppo» approvato, solo se raggiungono l'obiettivo di ricavi dell'anno." },
                    new VoceAccordo { Voce = VoceProvvigione.ExtraLaboratorio, Percentuale = 10m, IdBaseCalcolo = fuoriLab.IdBaseCalcolo,
                        Descrizione = DescrizioneExtra }
                },
                Soglie =
                {
                    new SogliaAccordo { Soglia = 50_000m, Premio = 800m },
                    new SogliaAccordo { Soglia = 70_000m, Premio = 1_600m },
                    new SogliaAccordo { Soglia = 100_000m, Premio = 3_000m }
                }
            });
        }
        if (!await _db.AccordiProvvigioni.AnyAsync(a => a.IdCommerciale == carloni.IdCommerciale, ct))
        {
            _db.AccordiProvvigioni.Add(new AccordoProvvigioni
            {
                IdCommerciale = carloni.IdCommerciale, Nome = "Accordo provvigioni Carloni", Tipo = TipoAccordo.Convenzioni,
                Stato = StatoAccordo.Bozza, ValidoDal = new DateTime(2025, 9, 1),
                Note = "Creato automaticamente dal testo dell'accordo. In bozza: attivarlo dopo aver registrato le convenzioni e verificato la data di inizio.",
                Voci =
                {
                    new VoceAccordo { Voce = VoceProvvigione.ConvenzioneIndicataAnno1, Percentuale = 15m, IdBaseCalcolo = tutto.IdBaseCalcolo,
                        Descrizione = "Punto 1: primo anno dei nuovi contratti pluriennali (convenzioni) indicati dal lavoratore." },
                    new VoceAccordo { Voce = VoceProvvigione.ConvenzioneIndicataAnni2a5, Percentuale = 10m, IdBaseCalcolo = tutto.IdBaseCalcolo,
                        Descrizione = "Punto 2: dal secondo al quinto anno dei contratti del punto 1, solo ricavi fatturati durante il rapporto di lavoro." },
                    new VoceAccordo { Voce = VoceProvvigione.ConvenzioneSegnalata, Percentuale = 10m, IdBaseCalcolo = tutto.IdBaseCalcolo,
                        Descrizione = "Punto 3: contratti pluriennali segnalati da MARE.A, dal primo al quinto anno, solo ricavi fatturati durante il rapporto." },
                    new VoceAccordo { Voce = VoceProvvigione.AltriServizi, Percentuale = 10m, IdBaseCalcolo = tutto.IdBaseCalcolo,
                        Descrizione = "Punto 4: tutti gli altri servizi contrattualizzati con i propri clienti (codice venditore)." },
                    new VoceAccordo { Voce = VoceProvvigione.ContrattoOccasionale, Percentuale = 0m, IdBaseCalcolo = areaConsulenza.IdBaseCalcolo,
                        Descrizione = DescrizioneOccasionale },
                    new VoceAccordo { Voce = VoceProvvigione.ContrattoAnalisi, Percentuale = 0m, IdBaseCalcolo = areaAnalisi.IdBaseCalcolo,
                        Descrizione = DescrizioneAnalisi }
                }
            });
        }
        await _db.SaveChangesAsync(ct);

        // Accordi di laboratorio creati prima della voce extra laboratorio: la si aggiunge (10% se nessun trimestre
        // chiuso cade nel loro periodo, altrimenti 0% per non cambiare il passato: in quel caso si fa una nuova versione).
        var fineChiuso = await FineUltimoTrimestreChiusoAsync(ct);
        var senzaExtra = await _db.AccordiProvvigioni.Include(a => a.Voci)
            .Where(a => a.Tipo == TipoAccordo.Laboratorio && a.Stato != StatoAccordo.Annullato
                        && !a.Voci.Any(v => v.Voce == VoceProvvigione.ExtraLaboratorio))
            .ToListAsync(ct);
        foreach (var a in senzaExtra)
        {
            var modificabile = a.Stato == StatoAccordo.Bozza || fineChiuso is null || fineChiuso.Value < a.ValidoDal.Date;
            a.Voci.Add(new VoceAccordo
            {
                Voce = VoceProvvigione.ExtraLaboratorio, Percentuale = modificabile ? 10m : 0m,
                IdBaseCalcolo = fuoriLab.IdBaseCalcolo, Descrizione = DescrizioneExtra
            });
        }
        await _db.SaveChangesAsync(ct);

        // Accordi a convenzioni creati prima delle voci «contratti occasionali» e «contratti di analisi»: si aggiungono
        // allo 0% (voce non prevista), così il passato non cambia; la percentuale si imposta dalla pagina dell'accordo.
        var senzaContratti = await _db.AccordiProvvigioni.Include(a => a.Voci)
            .Where(a => a.Tipo == TipoAccordo.Convenzioni && a.Stato != StatoAccordo.Annullato
                        && (!a.Voci.Any(v => v.Voce == VoceProvvigione.ContrattoOccasionale)
                            || !a.Voci.Any(v => v.Voce == VoceProvvigione.ContrattoAnalisi)))
            .ToListAsync(ct);
        foreach (var a in senzaContratti)
        {
            if (!a.Voci.Any(v => v.Voce == VoceProvvigione.ContrattoOccasionale))
                a.Voci.Add(new VoceAccordo { Voce = VoceProvvigione.ContrattoOccasionale, Percentuale = 0m,
                    IdBaseCalcolo = areaConsulenza.IdBaseCalcolo, Descrizione = DescrizioneOccasionale });
            if (!a.Voci.Any(v => v.Voce == VoceProvvigione.ContrattoAnalisi))
                a.Voci.Add(new VoceAccordo { Voce = VoceProvvigione.ContrattoAnalisi, Percentuale = 0m,
                    IdBaseCalcolo = areaAnalisi.IdBaseCalcolo, Descrizione = DescrizioneAnalisi });
        }
        await _db.SaveChangesAsync(ct);
    }

    private const string DescrizioneOccasionale =
        "Contratti spot: commesse di consulenza con rapporto «occasionale» e il riferimento del commerciale, solo i primi 12 mesi dalla prima fattura emessa dalla data della commessa. Non si applica se la fattura rientra in una convenzione.";

    private const string DescrizioneAnalisi =
        "Contratti di analisi: commesse di laboratorio con il riferimento del commerciale in ArcLab, solo i primi 12 mesi dalla prima fattura di analisi emessa dalla data della commessa.";

    private const string DescrizioneExtra =
        "Servizi extra laboratorio (consulenza, formazione) procurati dal commerciale: clienti con commesse con il suo riferimento in ArcLab, fatture dei primi 12 mesi dalla prima commessa.";

    private async Task<Commerciale> AsicuraAsync(string nome, string[] codici, int mansioneArcLab, CancellationToken ct)
    {
        var c = await _db.Commerciali.FirstOrDefaultAsync(x => x.Nome == nome, ct);
        if (c is null)
        {
            c = new Commerciale { Nome = nome };
            _db.Commerciali.Add(c);
            await _db.SaveChangesAsync(ct);
        }
        c.IdMansioneArcLab ??= mansioneArcLab;
        foreach (var cod in codici)
        {
            var esiste = await _db.CodiciVenditore.AnyAsync(x => x.IdCommerciale == c.IdCommerciale && x.CodiceVenditore == cod, ct);
            if (!esiste)
                _db.CodiciVenditore.Add(new CodiceVenditoreCommerciale
                { IdCommerciale = c.IdCommerciale, CodiceVenditore = cod, ValidoDal = new DateTime(2017, 1, 1) });
        }
        await _db.SaveChangesAsync(ct);
        return c;
    }

    private async Task<BaseCalcolo> AssicuraBaseAsync(string nome, string descrizione, bool tutto, string elementi, CancellationToken ct)
    {
        var b = await _db.BasiCalcolo.FirstOrDefaultAsync(x => x.Nome == nome, ct);
        if (b is not null) return b;
        b = new BaseCalcolo { Nome = nome, Descrizione = descrizione, TuttoIlFatturato = tutto, Elementi = elementi };
        _db.BasiCalcolo.Add(b);
        await _db.SaveChangesAsync(ct);
        return b;
    }

    /// <summary>
    /// Righe per trimestre di un commerciale: i trimestri chiusi mostrano i valori congelati,
    /// gli altri il calcolo attuale. "Chiudibile" = trimestre già finito e non ancora chiuso.
    /// </summary>
    public async Task<List<RigaTrimestre>> RigheTrimestriAsync(int idCommerciale, RisultatoProvvigioniLab live, CancellationToken ct = default)
    {
        var righe = new List<RigaTrimestre>();
        var chiusi = await _db.TrimestriProvvigioni.AsNoTracking().Where(t => t.Stato == StatoTrimestre.Chiuso).ToListAsync(ct);
        var idChiusi = chiusi.Select(t => t.IdTrimestre).ToList();
        var archiviati = await _db.MovimentiProvvigioni.AsNoTracking()
            .Where(m => m.IdCommerciale == idCommerciale && idChiusi.Contains(m.IdTrimestre)).ToListAsync(ct);

        foreach (var t in chiusi.OrderBy(t => t.Anno).ThenBy(t => t.Numero))
        {
            var mov = archiviati.Where(m => m.IdTrimestre == t.IdTrimestre).ToList();
            righe.Add(new RigaTrimestre($"{t.Anno}-T{t.Numero}", t.Anno, t.Numero,
                mov.Where(m => m.Tipo != TipoMovimento.NotaCredito).Sum(m => m.Base),
                mov.Where(m => m.Tipo == TipoMovimento.NotaCredito).Sum(m => m.Base),
                mov.Sum(m => m.Importo), Chiuso: true, Chiudibile: false));
        }
        var etichetteChiuse = righe.Select(r => r.Etichetta).ToHashSet();
        foreach (var g in live.Movimenti.GroupBy(m => m.Trimestre).OrderBy(g => g.Key))
        {
            if (etichetteChiuse.Contains(g.Key)) continue;
            var anno = int.Parse(g.Key[..4]);
            var num = int.Parse(g.Key[^1..]);
            var fine = new DateTime(anno, num * 3, DateTime.DaysInMonth(anno, num * 3));
            righe.Add(new RigaTrimestre(g.Key, anno, num,
                g.Where(m => m.Tipo != TipoMovimento.NotaCredito).Sum(m => m.Base),
                g.Where(m => m.Tipo == TipoMovimento.NotaCredito).Sum(m => m.Base),
                g.Sum(m => m.Importo), Chiuso: false, Chiudibile: fine < DateTime.Today));
        }
        return righe.OrderBy(r => r.Anno).ThenBy(r => r.Numero).ToList();
    }

    // ── Calcolo ───────────────────────────────────────────────────────

    /// <summary>
    /// Calcolo "a caldo" per un commerciale. Non scrive nulla. Con <paramref name="idAccordoAnteprima"/> una bozza
    /// è trattata come attiva dalla sua data di inizio (anteprima prima di attivarla).
    /// </summary>
    public async Task<RisultatoProvvigioniLab> CalcolaAsync(int idCommerciale, int? idAccordoAnteprima = null, CancellationToken ct = default)
    {
        var accordi = await _db.AccordiProvvigioni.AsNoTracking().Include(a => a.Voci).Include(a => a.Soglie)
            .Where(a => a.IdCommerciale == idCommerciale && (a.Stato == StatoAccordo.Attivo || a.IdAccordo == idAccordoAnteprima))
            .ToListAsync(ct);
        if (idAccordoAnteprima is int idA && accordi.FirstOrDefault(a => a.IdAccordo == idA) is { } bozza)
            accordi = accordi.Where(a => a.IdAccordo == idA || a.ValidoDal < bozza.ValidoDal).ToList();
        if (accordi.Count == 0)
            return Vuoto("Nessun accordo attivo per questo commerciale: crealo o attivalo in «Accordi provvigioni».");

        var basi = await _db.BasiCalcolo.AsNoTracking().ToDictionaryAsync(b => b.IdBaseCalcolo, ct);
        var codici = await _db.CodiciVenditore.AsNoTracking()
            .Where(x => x.IdCommerciale == idCommerciale).Select(x => x.CodiceVenditore).ToListAsync(ct);

        var avvisiGenerali = new List<string>();

        // Clienti del laboratorio: codice venditore del commerciale in AZW (le commesse di laboratorio non hanno riferimento).
        var propri = await _db.AzwFatture.AsNoTracking()
            .Where(f => f.CodiceVenditore != null && codici.Contains(f.CodiceVenditore))
            .Select(f => f.CodiceCliente).Distinct().ToListAsync(ct);
        var propriLab = propri.ToHashSet(StringComparer.Ordinal);

        // Consulenza e formazione: commesse con il suo riferimento in ArcLab (commessa.CodCommessa).
        var mansione = await _db.Commerciali.AsNoTracking().Where(c => c.IdCommerciale == idCommerciale)
            .Select(c => c.IdMansioneArcLab).FirstOrDefaultAsync(ct);
        var tutteRif = mansione is int idM
            ? await _db.CommesseArcLab.AsNoTracking().Where(c => c.IdMansioneRiferimento == idM).ToListAsync(ct)
            : new List<CommessaArcLab>();
        // Le commesse di analisi con il suo riferimento valgono solo per la voce «contratti di analisi».
        var commesseRif = tutteRif.Where(c => c.IdTipoCommessa != TipoCommessaAnalisi).ToList();
        var commesseAnalisi = tutteRif.Where(c => c.IdTipoCommessa == TipoCommessaAnalisi).ToList();
        var propriArcLab = commesseRif.Select(c => c.CodiceCliente).ToHashSet(StringComparer.Ordinal);
        var incarichiExtra = commesseRif.Where(c => c.DataCommessa != null)
            .GroupBy(c => c.CodiceCliente, StringComparer.Ordinal)
            .Select(g => new IncaricoExtraLaboratorio(g.Key, g.Min(x => x.DataCommessa!.Value)))
            .ToList();
        var clientiExtra = incarichiExtra.Select(x => x.Cliente).ToHashSet(StringComparer.Ordinal);
        var (vendute, senzaVoce) = await VociVenduteAsync(commesseRif, ct);
        var filtro = await FiltroAsync(idCommerciale, vendute, basi, ct);

        // Eccezioni «intera area»: i clienti contano anche senza commesse; per la voce extra la finestra parte dall'eccezione.
        var eccezioni = await _db.EccezioniArea.AsNoTracking().Where(e => e.IdCommerciale == idCommerciale && e.Attivo).ToListAsync(ct);
        foreach (var g in eccezioni.GroupBy(e => e.CodiceCliente, StringComparer.Ordinal))
            if (clientiExtra.Add(g.Key)) incarichiExtra.Add(new IncaricoExtraLaboratorio(g.Key, g.Min(e => e.DataInizio)));
        var usaArcLab = accordi.Any(a => a.Tipo == TipoAccordo.Convenzioni
            || a.Voci.Any(v => v.Voce == VoceProvvigione.ExtraLaboratorio && v.Percentuale > 0m));
        if (usaArcLab && mansione is null)
            avvisiGenerali.Add("Il commerciale non ha il codice di riferimento ArcLab: commesse di consulenza e formazione non riconosciute (impostalo nella pagina Provvigioni).");
        else if (usaArcLab && commesseRif.Count == 0)
            avvisiGenerali.Add("Nessuna commessa di consulenza o formazione con il riferimento del commerciale: importa di nuovo da AZW (l'importazione copia anche le commesse ArcLab).");
        if (usaArcLab && commesseRif.Count > 0 && commesseRif.All(c => c.IdTipoIncarico is null))
            avvisiGenerali.Add("Le commesse importate non hanno il tipo di incarico: importa di nuovo da AZW.");
        else if (usaArcLab && senzaVoce > 0)
            avvisiGenerali.Add($"{senzaVoce} commesse del commerciale hanno un tipo di incarico senza voce di fattura (cons.VociIncarico): i loro ricavi non contano.");

        var convenzioni = accordi.Any(a => a.Tipo == TipoAccordo.Convenzioni)
            ? await _db.Convenzioni.AsNoTracking().Where(c => c.IdCommerciale == idCommerciale && c.Attivo).ToListAsync(ct)
            : new List<ConvenzioneProvvigioni>();
        var contratti = commesseRif
            .Where(c => c.IdTipoCommessa == TipoCommessaConsulenza && c.TipoRapporto == 2 && c.DataCommessa != null)
            .Select(c => new ContrattoCommerciale(c.CodiceCliente, c.DataCommessa!.Value, TipoContrattoCommerciale.Occasionale))
            .Concat(commesseAnalisi.Where(c => c.DataCommessa != null)
                .Select(c => new ContrattoCommerciale(c.CodiceCliente, c.DataCommessa!.Value, TipoContrattoCommerciale.Analisi)))
            .Distinct().ToList();
        if (accordi.Any(a => a.Tipo == TipoAccordo.Convenzioni && a.Voci.Any(v => v.Voce == VoceProvvigione.ContrattoAnalisi && v.Percentuale > 0m))
            && commesseAnalisi.Count == 0)
            avvisiGenerali.Add("Nessuna commessa di analisi con il riferimento del commerciale: la voce «contratti di analisi» non produce importi (inserisci il riferimento in ArcLab e importa di nuovo da AZW).");

        var clienti = propri.Concat(propriArcLab).Concat(commesseAnalisi.Select(c => c.CodiceCliente)).Concat(convenzioni.Select(c => c.CodiceCliente))
            .Concat(eccezioni.Select(e => e.CodiceCliente)).Distinct().ToList();

        var fattureCliente = _db.AzwFatture.AsNoTracking().Where(f => clienti.Contains(f.CodiceCliente));
        var fatture = await fattureCliente.ToListAsync(ct);
        var righe = (await _db.AzwRigheFattura.AsNoTracking()
                .Join(fattureCliente, r => r.ChiaveFattura, f => f.Chiave, (r, f) => r).ToListAsync(ct))
            .ToLookup(r => r.ChiaveFattura, StringComparer.Ordinal);
        var incassi = await _db.AzwIncassi.AsNoTracking()
            .Join(fattureCliente, i => i.ChiaveFattura, f => f.Chiave, (i, f) => i).ToListAsync(ct);
        var note = await _db.AzwNoteCredito.AsNoTracking().Where(n => clienti.Contains(n.CodiceCliente)).ToListAsync(ct);
        var righeNote = (await _db.AzwRigheNoteCredito.AsNoTracking().Where(n => clienti.Contains(n.CodiceCliente)).ToListAsync(ct))
            .ToLookup(r => (r.CodiceCliente, r.Riferimento, r.Data));

        if (fatture.Count > 0 && righe.Count == 0)
            avvisiGenerali.Add("Mancano gli importi per centro di costo e voce: importa di nuovo da AZW.");

        // Contano solo le voci "in sviluppo" approvate (concordate con il responsabile di area).
        var obiettivi = (await _db.ElencoSviluppo.AsNoTracking()
                .Where(v => v.IdCommerciale == idCommerciale && v.Attivo && v.ApprovatoDa != null && v.ApprovatoDa != "").ToListAsync(ct))
            .Select(v => new ObiettivoSviluppo(v.CodiceCliente, v.Anno, v.ValidoDal, v.Obiettivo)).ToList();

        var periodi = Periodi(accordi);
        var parziali = new List<(PeriodoAccordo P, RisultatoProvvigioniLab R)>();
        foreach (var p in periodi)
        {
            var a = p.Accordo;
            var vociA = a.Voci.ToDictionary(v => v.Voce);

            Dictionary<VoceProvvigione, decimal> NettiFattura(string chiave, string cliente, DateTime data) =>
                vociA.ToDictionary(kv => kv.Key, kv => NettoInBase(righe[chiave]
                    .Where(x => filtro.Consentita(kv.Key, cliente, x.Centro, x.Voce, data))
                    .Select(x => (x.Centro, x.Voce, x.Netto)), basi, kv.Value.IdBaseCalcolo));
            Dictionary<VoceProvvigione, decimal> NettiNota(AzwNotaCredito n) =>
                vociA.ToDictionary(kv => kv.Key, kv => NettoInBase(righeNote[(n.CodiceCliente, n.Riferimento, n.Data)]
                    .Where(x => filtro.Consentita(kv.Key, n.CodiceCliente, x.Centro, x.Voce, n.Data))
                    .Select(x => (x.Centro, x.Voce, x.Netto)), basi, kv.Value.IdBaseCalcolo));

            // Voci di laboratorio solo sui clienti del codice venditore; voce extra solo sui clienti con commesse
            // di consulenza/formazione con il suo riferimento in ArcLab.
            void Maschera(Dictionary<VoceProvvigione, decimal> netti, string cliente)
            {
                if (!propriLab.Contains(cliente))
                    foreach (var v in VociLaboratorio) if (netti.ContainsKey(v)) netti[v] = 0m;
                if (!clientiExtra.Contains(cliente) && netti.ContainsKey(VoceProvvigione.ExtraLaboratorio))
                    netti[VoceProvvigione.ExtraLaboratorio] = 0m;
            }

            RisultatoProvvigioniLab r;
            if (a.Tipo == TipoAccordo.Laboratorio)
            {
                // Il "rapporto" con il cliente (nuovo, mantenimento, esistente) si misura sulla base della voce Nuovo.
                var fl = new List<FatturaLab>();
                foreach (var f in fatture)
                {
                    var n = NettiFattura(f.Chiave, f.CodiceCliente, f.DataFattura);
                    Maschera(n, f.CodiceCliente);
                    var rapporto = n.TryGetValue(VoceProvvigione.Nuovo, out var nn) ? nn : n.Values.DefaultIfEmpty(0m).Max();
                    if (rapporto == 0m && n.Values.All(x => x == 0m)) continue;
                    fl.Add(new FatturaLab(f.CodiceCliente, f.Chiave, f.DataFattura, rapporto, f.Lordo, n));
                }
                var chiavi = fl.Select(f => f.ChiaveFattura).ToHashSet(StringComparer.Ordinal);
                var nl = new List<NotaCreditoLab>();
                foreach (var n in note)
                {
                    var nn = NettiNota(n);
                    Maschera(nn, n.CodiceCliente);
                    if (nn.Values.All(x => x == 0m)) continue;
                    var rapporto = nn.TryGetValue(VoceProvvigione.Nuovo, out var v) ? v : nn.Values.Max();
                    nl.Add(new NotaCreditoLab(n.CodiceCliente, n.Riferimento, n.Data, rapporto, nn));
                }
                var regole = new RegoleProvvigioniLab
                {
                    AliquotaNuovo = Aliquota(vociA, VoceProvvigione.Nuovo),
                    AliquotaMantenimento = Aliquota(vociA, VoceProvvigione.Mantenimento),
                    AliquotaSviluppo = Aliquota(vociA, VoceProvvigione.Sviluppo),
                    AliquotaExtraLaboratorio = Aliquota(vociA, VoceProvvigione.ExtraLaboratorio),
                    MesiExtraLaboratorio = a.MesiPrimoAnno,
                    MesiPrimoAnno = a.MesiPrimoAnno,
                    MesiPausa = a.MesiPausa,
                    DataInizioAccordo = a.DataClientiEsistenti ?? a.ValidoDal,
                    DataInizioCalcolo = a.ValidoDal,
                    SoglieEPremi = a.Soglie.Where(s => s.Soglia > 0m).OrderBy(s => s.Soglia).Select(s => new SogliaPremio(s.Soglia, s.Premio)).ToList(),
                    FattoreOrario = a.FattoreOrario
                };
                r = _motore.Calcola(new InputProvvigioniLab(fl,
                    incassi.Where(i => chiavi.Contains(i.ChiaveFattura)).Select(i => new IncassoFattura(i.ChiaveFattura, i.DataIncasso, i.ImportoLordo)).ToList(),
                    nl, obiettivi, regole, incarichiExtra));
            }
            else
            {
                var fc = new List<FatturaCommerciale>();
                foreach (var f in fatture)
                {
                    var n = NettiFattura(f.Chiave, f.CodiceCliente, f.DataFattura);
                    if (n.Values.All(x => x == 0m)) continue;
                    fc.Add(new FatturaCommerciale(f.CodiceCliente, f.Chiave, f.DataFattura, f.NettoTotale, f.Lordo, n));
                }
                var chiavi = fc.Select(f => f.ChiaveFattura).ToHashSet(StringComparer.Ordinal);
                var nc = new List<NotaCreditoCommerciale>();
                foreach (var n in note)
                {
                    var nn = NettiNota(n);
                    if (nn.Values.All(x => x == 0m)) continue;
                    nc.Add(new NotaCreditoCommerciale(n.CodiceCliente, n.Riferimento, n.Data, n.NettoTotale, nn));
                }
                var regole = new RegoleProvvigioniCarloni
                {
                    AliquotaIndicataAnno1 = Aliquota(vociA, VoceProvvigione.ConvenzioneIndicataAnno1),
                    AliquotaIndicataAnni2a5 = Aliquota(vociA, VoceProvvigione.ConvenzioneIndicataAnni2a5),
                    AliquotaSegnalata = Aliquota(vociA, VoceProvvigione.ConvenzioneSegnalata),
                    AliquotaAltriServizi = Aliquota(vociA, VoceProvvigione.AltriServizi),
                    AliquotaOccasionale = Aliquota(vociA, VoceProvvigione.ContrattoOccasionale),
                    AliquotaAnalisi = Aliquota(vociA, VoceProvvigione.ContrattoAnalisi),
                    MesiPrimoAnno = a.MesiPrimoAnno,
                    AnniConvenzione = a.AnniConvenzione,
                    DataInizioCalcolo = a.ValidoDal,
                    DataFineRapporto = a.DataFineRapporto
                };
                var conv = convenzioni.Select(c => new ConvenzioneCommerciale(c.CodiceCliente, c.DataInizio, c.Origine)).ToList();
                r = _motoreCarloni.Calcola(new InputProvvigioniCarloni(fc,
                    incassi.Where(i => chiavi.Contains(i.ChiaveFattura)).Select(i => new IncassoFattura(i.ChiaveFattura, i.DataIncasso, i.ImportoLordo)).ToList(),
                    nc, conv, propriArcLab, regole, contratti));
                if (conv.Count == 0)
                    avvisiGenerali.Add("Nessuna convenzione registrata: tutto il fatturato dei clienti propri è calcolato come «altri servizi». Registra le convenzioni dalla schermata «Convenzioni».");
            }

            // Restano i movimenti del periodo di validità, delle voci previste (percentuale > 0).
            var movimenti = r.Movimenti
                .Where(m => m.Data.Date >= p.Dal && m.Data.Date <= p.Al && m.Aliquota > 0m)
                .ToList();
            parziali.Add((p, r with { Movimenti = movimenti }));
        }

        // Premio e obiettivi di un anno: dall'accordo valido al 31/12 (o dall'ultimo iniziato entro l'anno).
        RisultatoProvvigioniLab? PerAnno(int anno)
        {
            var fine = new DateTime(anno, 12, 31);
            return parziali.Where(x => x.P.Dal <= fine && x.P.Al >= new DateTime(anno, 1, 1))
                .OrderByDescending(x => x.P.Dal).Select(x => x.R).FirstOrDefault();
        }
        var anniPremi = parziali.SelectMany(x => x.R.Premi.Select(p => p.Anno)).Distinct();
        var premi = anniPremi.Select(y => PerAnno(y)?.Premi.FirstOrDefault(p => p.Anno == y)).OfType<RisultatoPremio>().OrderBy(p => p.Anno).ToList();
        var anniOb = parziali.SelectMany(x => x.R.Obiettivi.Select(o => o.Anno)).Distinct();
        var avanzamenti = anniOb.SelectMany(y => PerAnno(y)?.Obiettivi.Where(o => o.Anno == y) ?? Enumerable.Empty<AvanzamentoObiettivo>()).ToList();

        var tutti = parziali.SelectMany(x => x.R.Movimenti)
            .OrderBy(m => m.Data).ThenBy(m => m.Cliente, StringComparer.Ordinal).ThenBy(m => m.Riferimento, StringComparer.Ordinal)
            .ToList();
        var avvisi = avvisiGenerali.Concat(parziali.SelectMany(x => x.R.Avvisi)).Distinct().ToList();
        return new RisultatoProvvigioniLab(tutti, premi, avvisi, avanzamenti);
    }

    private static RisultatoProvvigioniLab Vuoto(string avviso) =>
        new(new List<MovimentoProvvigione>(), new List<RisultatoPremio>(), new List<string> { avviso }, new List<AvanzamentoObiettivo>());

    private static decimal Aliquota(Dictionary<VoceProvvigione, VoceAccordo> voci, VoceProvvigione v) =>
        voci.TryGetValue(v, out var x) ? x.Percentuale / 100m : 0m;

    private static decimal NettoInBase(IEnumerable<(string Centro, string Voce, decimal Netto)> righe, Dictionary<int, BaseCalcolo> basi, int idBase)
    {
        if (!basi.TryGetValue(idBase, out var b)) return 0m;
        return righe.Where(r => b.Include(r.Centro, r.Voce)).Sum(r => r.Netto);
    }

    public sealed record PeriodoAccordo(AccordoProvvigioni Accordo, DateTime Dal, DateTime Al);

    /// <summary>Periodi effettivi: ogni accordo vale dal suo inizio fino al giorno prima del successivo (o alla sua fine).</summary>
    public static List<PeriodoAccordo> Periodi(IEnumerable<AccordoProvvigioni> accordi)
    {
        var ordinati = accordi.OrderBy(a => a.ValidoDal).ToList();
        var periodi = new List<PeriodoAccordo>();
        for (var k = 0; k < ordinati.Count; k++)
        {
            var a = ordinati[k];
            var al = a.ValidoAl?.Date ?? new DateTime(9999, 12, 31);
            if (k + 1 < ordinati.Count && ordinati[k + 1].ValidoDal.Date.AddDays(-1) < al) al = ordinati[k + 1].ValidoDal.Date.AddDays(-1);
            if (al >= a.ValidoDal.Date) periodi.Add(new PeriodoAccordo(a, a.ValidoDal.Date, al));
        }
        return periodi;
    }

    // ── Convenzioni da ArcLab ─────────────────────────────────────────

    /// <summary>
    /// Crea le convenzioni mancanti dalle commesse continuative (TipoStampa = 1) con il riferimento del commerciale:
    /// inizio = prima commessa continuativa del cliente; «data da verificare» se è la data del caricamento in blocco;
    /// origine proposta dalle fatture (cliente già fatturato nei 24 mesi prima → segnalata, altrimenti indicata).
    /// Non tocca le convenzioni esistenti, comprese le correzioni fatte a mano. Restituisce quante ne ha create.
    /// </summary>
    public async Task<int> SincronizzaConvenzioniAsync(CancellationToken ct = default)
    {
        var commerciali = await _db.Commerciali.Where(c => c.Attivo && c.IdMansioneArcLab != null).ToListAsync(ct);
        var create = 0;
        foreach (var c in commerciali)
        {
            var aConvenzioni = await _db.AccordiProvvigioni.AnyAsync(a => a.IdCommerciale == c.IdCommerciale
                && a.Tipo == TipoAccordo.Convenzioni && a.Stato != StatoAccordo.Annullato, ct);
            if (!aConvenzioni) continue;

            var idM = c.IdMansioneArcLab!.Value;
            var continuative = await _db.CommesseArcLab.AsNoTracking()
                .Where(x => x.IdMansioneRiferimento == idM && x.TipoRapporto == 1 && x.DataCommessa != null
                            && x.IdTipoCommessa != TipoCommessaAnalisi)
                .ToListAsync(ct);
            var esistenti = (await _db.Convenzioni.Where(x => x.IdCommerciale == c.IdCommerciale && x.Attivo)
                .Select(x => x.CodiceCliente).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);

            foreach (var g in continuative.GroupBy(x => x.CodiceCliente, StringComparer.Ordinal))
            {
                if (esistenti.Contains(g.Key)) continue;
                var prima = g.OrderBy(x => x.DataCommessa).ThenBy(x => x.IdCommessa).First();
                var inizio = prima.DataCommessa!.Value.Date;
                var da = inizio.AddMonths(-24);
                var giaCliente = await _db.AzwFatture.AnyAsync(f => f.CodiceCliente == g.Key && f.DataFattura >= da && f.DataFattura < inizio, ct);
                var descr = $"ArcLab commessa {prima.IdCommessa}: {prima.Descrizione}";
                _db.Convenzioni.Add(new ConvenzioneProvvigioni
                {
                    IdCommerciale = c.IdCommerciale, CodiceCliente = g.Key, DataInizio = inizio,
                    Origine = giaCliente ? OrigineConvenzione.SegnalataDaAzienda : OrigineConvenzione.IndicataDalCommerciale,
                    Fonte = FonteConvenzione.ArcLab, DataDaVerificare = inizio == DataCaricamentoInBlocco, OrigineConfermata = false,
                    Descrizione = descr.Length > 200 ? descr[..200] : descr
                });
                create++;
            }
        }
        await _db.SaveChangesAsync(ct);
        return create;
    }

    /// <summary>Controllo incrociato fra codice venditore AZW e riferimento ArcLab, per correggere l'uno o l'altro.</summary>
    public async Task<ControlloClienti> ControllaClientiAsync(int idCommerciale, CancellationToken ct = default)
    {
        var codici = await _db.CodiciVenditore.AsNoTracking().Where(x => x.IdCommerciale == idCommerciale)
            .Select(x => x.CodiceVenditore).ToListAsync(ct);
        var mansione = await _db.Commerciali.AsNoTracking().Where(c => c.IdCommerciale == idCommerciale)
            .Select(c => c.IdMansioneArcLab).FirstOrDefaultAsync(ct);
        var conCodice = (await _db.AzwFatture.AsNoTracking()
            .Where(f => f.CodiceVenditore != null && codici.Contains(f.CodiceVenditore))
            .Select(f => f.CodiceCliente).Distinct().ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var conRiferimento = mansione is int idM
            ? (await _db.CommesseArcLab.AsNoTracking().Where(x => x.IdMansioneRiferimento == idM)
                .Select(x => x.CodiceCliente).Distinct().ToListAsync(ct)).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        return new ControlloClienti(
            conCodice.Except(conRiferimento).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            conRiferimento.Except(conCodice).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            conRiferimento.Count);
    }

    // ── Versioni degli accordi ────────────────────────────────────────

    /// <summary>Ultimo giorno dell'ultimo trimestre chiuso (null se nessuno): prima di questa data nulla può cambiare.</summary>
    public async Task<DateTime?> FineUltimoTrimestreChiusoAsync(CancellationToken ct = default)
    {
        var chiusi = await _db.TrimestriProvvigioni.AsNoTracking().Where(t => t.Stato == StatoTrimestre.Chiuso)
            .Select(t => new { t.Anno, t.Numero }).ToListAsync(ct);
        if (chiusi.Count == 0) return null;
        return chiusi.Select(t => new DateTime(t.Anno, t.Numero * 3, DateTime.DaysInMonth(t.Anno, t.Numero * 3))).Max();
    }

    /// <summary>Attiva una bozza: l'accordo attivo precedente si chiude il giorno prima. Restituisce un messaggio d'errore o null.</summary>
    public async Task<string?> AttivaAccordoAsync(int idAccordo, CancellationToken ct = default)
    {
        var a = await _db.AccordiProvvigioni.Include(x => x.Voci).FirstOrDefaultAsync(x => x.IdAccordo == idAccordo, ct);
        if (a is null) return "Accordo non trovato.";
        if (a.Stato != StatoAccordo.Bozza) return "Si può attivare solo un accordo in bozza.";
        if (!a.Voci.Any(v => v.Percentuale > 0m)) return "L'accordo non ha nessuna voce con percentuale maggiore di zero.";
        var fineChiuso = await FineUltimoTrimestreChiusoAsync(ct);
        if (fineChiuso is { } fc && a.ValidoDal.Date <= fc)
            return $"La data di inizio deve essere successiva all'ultimo trimestre chiuso ({fc:dd/MM/yyyy}): il passato già pagato non cambia.";

        var attivi = await _db.AccordiProvvigioni
            .Where(x => x.IdCommerciale == a.IdCommerciale && x.Stato == StatoAccordo.Attivo).ToListAsync(ct);
        if (attivi.FirstOrDefault(x => x.ValidoDal.Date >= a.ValidoDal.Date) is { } dopo)
            return $"Esiste già un accordo attivo che inizia il {dopo.ValidoDal:dd/MM/yyyy}: la nuova versione deve iniziare dopo.";

        foreach (var prec in attivi.Where(x => x.ValidoAl is null || x.ValidoAl.Value.Date >= a.ValidoDal.Date))
            prec.ValidoAl = a.ValidoDal.Date.AddDays(-1);
        a.Stato = StatoAccordo.Attivo;
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Riporta in bozza l'ultima versione attiva, se nessun trimestre chiuso cade nel suo periodo.</summary>
    public async Task<string?> RiportaInBozzaAsync(int idAccordo, CancellationToken ct = default)
    {
        var a = await _db.AccordiProvvigioni.FirstOrDefaultAsync(x => x.IdAccordo == idAccordo, ct);
        if (a is null) return "Accordo non trovato.";
        if (a.Stato != StatoAccordo.Attivo) return "L'accordo non è attivo.";
        var fineChiuso = await FineUltimoTrimestreChiusoAsync(ct);
        if (fineChiuso is { } fc && a.ValidoDal.Date <= fc)
            return $"Un trimestre chiuso ({fc:dd/MM/yyyy}) cade nel periodo di questo accordo: non si può più modificare. Usa «Duplica come nuova versione».";
        var attivi = await _db.AccordiProvvigioni
            .Where(x => x.IdCommerciale == a.IdCommerciale && x.Stato == StatoAccordo.Attivo && x.IdAccordo != a.IdAccordo).ToListAsync(ct);
        if (attivi.Any(x => x.ValidoDal > a.ValidoDal))
            return "Si può riportare in bozza solo l'ultima versione attiva.";

        var giornoPrima = a.ValidoDal.Date.AddDays(-1);
        foreach (var prec in attivi.Where(x => x.ValidoAl?.Date == giornoPrima)) prec.ValidoAl = null;
        a.Stato = StatoAccordo.Bozza;
        a.ValidoAl = null;
        await _db.SaveChangesAsync(ct);
        return null;
    }

    /// <summary>Crea una nuova bozza copiando testata, voci e soglie; inizio dal primo giorno del trimestre successivo.</summary>
    public async Task<int> DuplicaAccordoAsync(int idAccordo, CancellationToken ct = default)
    {
        var a = await _db.AccordiProvvigioni.AsNoTracking().Include(x => x.Voci).Include(x => x.Soglie)
            .FirstAsync(x => x.IdAccordo == idAccordo, ct);
        var oggi = DateTime.Today;
        var inizioProssimo = new DateTime(oggi.Year, (oggi.Month - 1) / 3 * 3 + 1, 1).AddMonths(3);
        var nuovo = new AccordoProvvigioni
        {
            IdCommerciale = a.IdCommerciale, Nome = a.Nome + " (nuova versione)", Tipo = a.Tipo, Stato = StatoAccordo.Bozza,
            ValidoDal = inizioProssimo > a.ValidoDal ? inizioProssimo : a.ValidoDal.AddMonths(3),
            RiferimentoDocumento = a.RiferimentoDocumento, MesiPrimoAnno = a.MesiPrimoAnno, MesiPausa = a.MesiPausa,
            DataClientiEsistenti = a.DataClientiEsistenti, AnniConvenzione = a.AnniConvenzione, DataFineRapporto = a.DataFineRapporto,
            FattoreOrario = a.FattoreOrario, Note = a.Note,
            Voci = a.Voci.Select(v => new VoceAccordo { Voce = v.Voce, Percentuale = v.Percentuale, IdBaseCalcolo = v.IdBaseCalcolo, Descrizione = v.Descrizione }).ToList(),
            Soglie = a.Soglie.Where(s => s.Soglia > 0m).Select(s => new SogliaAccordo { Soglia = s.Soglia, Premio = s.Premio }).ToList()
        };
        _db.AccordiProvvigioni.Add(nuovo);
        await _db.SaveChangesAsync(ct);
        return nuovo.IdAccordo;
    }

    /// <summary>
    /// Basi bloccate: usate da un accordo attivo il cui periodo comprende un trimestre già chiuso. Modificarle cambierebbe
    /// il calcolo di provvigioni già pagate, quindi si duplicano. Finché nessun trimestre è chiuso, sono tutte modificabili.
    /// </summary>
    public async Task<HashSet<int>> BasiInUsoAsync(CancellationToken ct = default)
    {
        var fineChiuso = await FineUltimoTrimestreChiusoAsync(ct);
        if (fineChiuso is null) return new HashSet<int>();
        var fine = fineChiuso.Value;
        return (await _db.VociAccordo.AsNoTracking()
            .Join(_db.AccordiProvvigioni.Where(a => a.Stato == StatoAccordo.Attivo && a.ValidoDal <= fine),
                v => v.IdAccordo, a => a.IdAccordo, (v, a) => v.IdBaseCalcolo)
            .Distinct().ToListAsync(ct)).ToHashSet();
    }

    // ── Dettaglio per voce di fattura ─────────────────────────────────

    /// <summary>
    /// Scompone base e provvigione dei movimenti di un anno per voce di fattura AZW (ANA, ANE, AMB…), dentro una base
    /// di calcolo: ogni movimento si ripartisce sulle righe della sua fattura (o nota) in proporzione al netto.
    /// <paramref name="filtro"/> limita colonne e totali alle voci scelte (vuoto = tutte).
    /// </summary>
    public async Task<DettaglioVoci> DettaglioPerVoceAsync(int idCommerciale, RisultatoProvvigioniLab risultato, int? anno,
        int? idBase, IReadOnlyCollection<string>? filtro, CancellationToken ct = default)
    {
        var accordi = await _db.AccordiProvvigioni.AsNoTracking().Include(a => a.Voci)
            .Where(a => a.IdCommerciale == idCommerciale && a.Stato == StatoAccordo.Attivo).ToListAsync(ct);
        var periodi = Periodi(accordi);
        int? BaseDi(MovimentoProvvigione m) =>
            periodi.FirstOrDefault(p => m.Data.Date >= p.Dal && m.Data.Date <= p.Al)?.Accordo.Voci
                .FirstOrDefault(v => v.Voce == m.Voce)?.IdBaseCalcolo;

        var anni = risultato.Movimenti.Select(m => m.Data.Year).Distinct().OrderBy(y => y).ToList();
        var annoScelto = anno is int a0 && anni.Contains(a0) ? a0 : (anni.Count > 0 ? anni[^1] : DateTime.Today.Year);
        var movAnno = risultato.Movimenti.Where(m => m.Data.Year == annoScelto)
            .Select(m => (M: m, Base: BaseDi(m))).Where(x => x.Base != null).ToList();

        var basi = await _db.BasiCalcolo.AsNoTracking().ToDictionaryAsync(b => b.IdBaseCalcolo, ct);
        var mansione = await _db.Commerciali.AsNoTracking().Where(c => c.IdCommerciale == idCommerciale)
            .Select(c => c.IdMansioneArcLab).FirstOrDefaultAsync(ct);
        var commesseRif = mansione is int idM
            ? await _db.CommesseArcLab.AsNoTracking().Where(c => c.IdMansioneRiferimento == idM && c.IdTipoCommessa != TipoCommessaAnalisi).ToListAsync(ct)
            : new List<CommessaArcLab>();
        var (vendute, _) = await VociVenduteAsync(commesseRif, ct);
        var filtroRighe = await FiltroAsync(idCommerciale, vendute, basi, ct);
        var basiUsate = movAnno.Select(x => x.Base!.Value).Distinct()
            .Select(id => new BaseDettaglio(id, basi.TryGetValue(id, out var b) ? b.Nome : $"Base {id}"))
            .OrderBy(b => b.Nome == BaseLaboratorio ? 0 : 1).ThenBy(b => b.Nome).ToList();
        var baseScelta = idBase is int ib && basiUsate.Any(b => b.IdBaseCalcolo == ib) ? ib : basiUsate.FirstOrDefault()?.IdBaseCalcolo;
        if (baseScelta is null || !basi.TryGetValue(baseScelta.Value, out var baseCalcolo))
            return new DettaglioVoci(basiUsate, null, annoScelto, anni, new List<string>(), new List<string>(), new List<RigaDettaglioVoci>());

        var mov = movAnno.Where(x => x.Base == baseScelta).Select(x => x.M).ToList();
        var chiavi = mov.Where(m => m.Tipo == TipoMovimento.Incasso).Select(m => m.Riferimento).Distinct().ToList();
        var righeFattura = (await _db.AzwRigheFattura.AsNoTracking().Where(r => chiavi.Contains(r.ChiaveFattura)).ToListAsync(ct))
            .ToLookup(r => r.ChiaveFattura, StringComparer.Ordinal);
        var clientiNote = mov.Where(m => m.Tipo == TipoMovimento.NotaCredito).Select(m => m.Cliente).Distinct().ToList();
        var righeNote = (await _db.AzwRigheNoteCredito.AsNoTracking().Where(r => clientiNote.Contains(r.CodiceCliente)).ToListAsync(ct))
            .ToLookup(r => (r.CodiceCliente, r.Riferimento, r.Data.Date));

        static string Etichetta(string centro, string voce) => centro == "04" ? (voce.Length > 0 ? voce : "(04)") : $"{centro} {voce}".Trim();

        // cliente → colonna → (base, provvigione)
        var acc = new Dictionary<string, Dictionary<string, (decimal Base, decimal Provv)>>(StringComparer.Ordinal);
        void Somma(string cliente, string col, decimal b, decimal p)
        {
            if (!acc.TryGetValue(cliente, out var d)) acc[cliente] = d = new Dictionary<string, (decimal, decimal)>(StringComparer.Ordinal);
            var x = d.GetValueOrDefault(col);
            d[col] = (x.Item1 + b, x.Item2 + p);
        }
        foreach (var m in mov)
        {
            IEnumerable<(string Centro, string Voce, decimal Netto)> righe = m.Tipo switch
            {
                TipoMovimento.Incasso => righeFattura[m.Riferimento].Select(x => (x.Centro, x.Voce, x.Netto)),
                TipoMovimento.NotaCredito => righeNote[(m.Cliente, m.Riferimento, m.Data.Date)].Select(x => (x.Centro, x.Voce, x.Netto)),
                _ => Enumerable.Empty<(string, string, decimal)>()
            };
            var nellaBase = righe.Where(x => baseCalcolo.Include(x.Centro, x.Voce) && filtroRighe.Consentita(m.Voce, m.Cliente, x.Centro, x.Voce, m.Data)).ToList();
            var tot = nellaBase.Sum(x => x.Netto);
            if (tot == 0m)
            {
                Somma(m.Cliente, m.Tipo == TipoMovimento.SviluppoObiettivo ? "Obiettivo" : "(non ripartibile)", m.Base, m.Importo);
                continue;
            }
            foreach (var riga in nellaBase)
            {
                var quota = riga.Netto / tot;
                Somma(m.Cliente, Etichetta(riga.Centro, riga.Voce), m.Base * quota, m.Importo * quota);
            }
        }

        var colonne = acc.Values.SelectMany(d => d.Keys).Distinct()
            .OrderBy(c => c.Contains(' ') || c.StartsWith('(') || c == "Obiettivo" ? 1 : 0).ThenBy(c => c, StringComparer.Ordinal).ToList();
        var scelte = (filtro ?? Array.Empty<string>()).Where(colonne.Contains).ToList();
        var mostrate = scelte.Count > 0 ? scelte : colonne;
        var righeDett = acc.Select(kv =>
            {
                var perVoce = mostrate.ToDictionary(c => c, c => Math.Round(kv.Value.GetValueOrDefault(c).Base, 2), StringComparer.Ordinal);
                var provv = mostrate.Sum(c => kv.Value.GetValueOrDefault(c).Provv);
                return new RigaDettaglioVoci(kv.Key, perVoce, perVoce.Values.Sum(), Math.Round(provv, 2));
            })
            .Where(r => r.Base != 0m || r.Provvigione != 0m)
            .OrderByDescending(r => r.Base).ToList();
        return new DettaglioVoci(basiUsate, baseScelta, annoScelto, anni, colonne, mostrate, righeDett);
    }

    // ── Chiusura ──────────────────────────────────────────────────────

    /// <summary>
    /// Chiude il trimestre: salva i movimenti calcolati di tutti i commerciali attivi e li congela, con gli accordi usati.
    /// Un trimestre chiuso non si può né ricalcolare né riaprire da qui. Restituisce il numero di movimenti salvati.
    /// </summary>
    public async Task<int> ChiudiTrimestreAsync(int anno, int numero, int? idUtente, string versioneApplicazione, CancellationToken ct = default)
    {
        if (numero is < 1 or > 4) throw new ArgumentOutOfRangeException(nameof(numero));

        var trimestre = await _db.TrimestriProvvigioni.FirstOrDefaultAsync(t => t.Anno == anno && t.Numero == numero, ct);
        if (trimestre?.Stato == StatoTrimestre.Chiuso)
            throw new InvalidOperationException($"Il trimestre {anno}-T{numero} è già chiuso.");

        var ultimoImport = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).FirstOrDefaultAsync(ct);
        var inizioTrimestre = new DateTime(anno, (numero - 1) * 3 + 1, 1);
        var fineTrimestre = new DateTime(anno, numero * 3, DateTime.DaysInMonth(anno, numero * 3));
        if (ultimoImport is null || ultimoImport.DataModificaFile is null || ultimoImport.DataModificaFile.Value.Date < fineTrimestre.Date)
            throw new InvalidOperationException("L'ultima importazione AZW non copre la fine del trimestre: importa i dati aggiornati prima di chiudere.");

        // Accordi e basi in vigore nel trimestre, registrati con il trimestre per poterlo rileggere in futuro.
        var accordiUsati = await _db.AccordiProvvigioni.AsNoTracking().Include(a => a.Voci).Include(a => a.Soglie)
            .Where(a => a.Stato == StatoAccordo.Attivo && a.ValidoDal <= fineTrimestre && (a.ValidoAl == null || a.ValidoAl >= inizioTrimestre))
            .ToListAsync(ct);
        var idBasi = accordiUsati.SelectMany(a => a.Voci).Select(v => v.IdBaseCalcolo).Distinct().ToList();
        var basiUsate = await _db.BasiCalcolo.AsNoTracking().Where(b => idBasi.Contains(b.IdBaseCalcolo)).ToListAsync(ct);
        var condizioniSegnalatori = await _db.CondizioniSegnalatori.AsNoTracking()
            .Join(_db.Segnalatori.AsNoTracking(), c => c.IdSegnalatore, s => s.IdSegnalatore,
                (c, s) => new { c.IdCondizioneSegnalatore, Segnalatore = s.Nome, c.Nome, c.Percentuale, c.Ambito, c.IdBaseCalcolo, c.MesiInterruzione, c.AnniDurataMassima })
            .ToListAsync(ct);
        var regoleUsate = JsonSerializer.Serialize(new
        {
            CondizioniSegnalatori = condizioniSegnalatori.Select(c => new
            {
                c.IdCondizioneSegnalatore, c.Segnalatore, c.Nome, c.Percentuale, Ambito = c.Ambito.ToString(),
                c.IdBaseCalcolo, c.MesiInterruzione, c.AnniDurataMassima
            }),
            Accordi = accordiUsati.Select(a => new
            {
                a.IdAccordo, a.IdCommerciale, a.Nome, Tipo = a.Tipo.ToString(), a.ValidoDal, a.ValidoAl,
                a.MesiPrimoAnno, a.MesiPausa, a.DataClientiEsistenti, a.AnniConvenzione, a.DataFineRapporto, a.FattoreOrario,
                Voci = a.Voci.Select(v => new { Voce = v.Voce.ToString(), v.Percentuale, v.IdBaseCalcolo }),
                Soglie = a.Soglie.Where(s => s.Soglia > 0m).Select(s => new { s.Soglia, s.Premio })
            }),
            Basi = basiUsate.Select(b => new { b.IdBaseCalcolo, b.Nome, b.TuttoIlFatturato, b.Elementi })
        });

        var etichetta = $"{anno}-T{numero}";
        var commerciali = await _db.Commerciali.Where(c => c.Attivo).ToListAsync(ct);

        var totale = 0;
        var strategia = _db.Database.CreateExecutionStrategy();
        await strategia.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            trimestre = await _db.TrimestriProvvigioni.FirstOrDefaultAsync(t => t.Anno == anno && t.Numero == numero, ct);
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            if (trimestre is null)
            {
                trimestre = new TrimestreProvvigioni { Anno = anno, Numero = numero, VersioneApplicazione = versioneApplicazione };
                _db.TrimestriProvvigioni.Add(trimestre);
                await _db.SaveChangesAsync(ct);
            }
            else
            {
                // Trimestre aperto con eventuali movimenti di prova: si riscrivono da zero.
                var idTrimestre = trimestre.IdTrimestre;
                await _db.MovimentiProvvigioni.Where(m => m.IdTrimestre == idTrimestre).ExecuteDeleteAsync(ct);
            }

            totale = 0;
            foreach (var c in commerciali)
            {
                var r = await CalcolaAsync(c.IdCommerciale, null, ct);
                foreach (var m in r.Movimenti.Where(m => m.Trimestre == etichetta))
                {
                    _db.MovimentiProvvigioni.Add(new MovimentoProvvigioneArchiviato
                    {
                        IdTrimestre = trimestre.IdTrimestre, IdCommerciale = c.IdCommerciale,
                        CodiceCliente = m.Cliente, Riferimento = m.Riferimento, Data = m.Data,
                        Tipo = m.Tipo, Voce = m.Voce, Categoria = m.Categoria,
                        Base = m.Base, Aliquota = m.Aliquota, Importo = m.Importo
                    });
                    totale++;
                }

                // Premio dell'anno: aggiornato a ogni chiusura (definitivo con il T4).
                foreach (var p in r.Premi.Where(p => p.Anno == anno))
                {
                    var esistente = await _db.PremiProvvigioni.FirstOrDefaultAsync(x => x.IdCommerciale == c.IdCommerciale && x.Anno == anno, ct);
                    if (esistente is null)
                        _db.PremiProvvigioni.Add(new PremioProvvigioni
                        { IdCommerciale = c.IdCommerciale, Anno = anno, BaseIncassata = p.BaseIncassata, Premio = p.Premio, DataCalcolo = DateTime.UtcNow });
                    else { esistente.BaseIncassata = p.BaseIncassata; esistente.Premio = p.Premio; esistente.DataCalcolo = DateTime.UtcNow; }
                }
            }

            // Compensi ai segnalatori dello stesso trimestre (indipendenti dai commerciali).
            totale += await new ServizioCompensiSegnalatori(_db).ArchiviaTrimestreAsync(trimestre.IdTrimestre, etichetta, ct);

            trimestre.Stato = StatoTrimestre.Chiuso;
            trimestre.DataChiusura = DateTime.UtcNow;
            trimestre.IdUtenteChiusura = idUtente;
            trimestre.VersioneApplicazione = versioneApplicazione;
            trimestre.RegoleUsate = regoleUsate;
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return totale;
    }
}

public sealed record BaseDettaglio(int IdBaseCalcolo, string Nome);

public sealed record RigaDettaglioVoci(string Cliente, IReadOnlyDictionary<string, decimal> PerVoce, decimal Base, decimal Provvigione);

/// <param name="Colonne">Voci di fattura presenti nella base per l'anno.</param>
/// <param name="Mostrate">Voci mostrate e sommate (filtro; tutte se il filtro è vuoto).</param>
public sealed record DettaglioVoci(
    IReadOnlyList<BaseDettaglio> Basi, int? IdBase, int Anno, IReadOnlyList<int> Anni,
    IReadOnlyList<string> Colonne, IReadOnlyList<string> Mostrate, IReadOnlyList<RigaDettaglioVoci> Righe);

/// <param name="SoloCodiceVenditore">Clienti con il codice venditore del commerciale ma senza commesse con il suo riferimento.</param>
/// <param name="SoloRiferimentoArcLab">Clienti con commesse con il suo riferimento ma senza il suo codice venditore.</param>
public sealed record ControlloClienti(IReadOnlyList<string> SoloCodiceVenditore, IReadOnlyList<string> SoloRiferimentoArcLab, int ClientiConRiferimento);

public sealed record RigaTrimestre(
    string Etichetta, int Anno, int Numero, decimal Incassato, decimal NoteCredito, decimal Provvigione, bool Chiuso, bool Chiudibile);
