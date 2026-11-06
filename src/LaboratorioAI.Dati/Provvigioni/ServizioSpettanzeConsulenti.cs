using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Provvigioni;

public record RigaMeseSpettanze(int Mese, IReadOnlyDictionary<string, decimal> PerVoce, decimal Maturato, decimal Acconti, decimal DocenzeOrarie)
{
    /// <summary>22/10/2026: anno del mese (nella vista filtrata i mesi possono essere di anni diversi).</summary>
    public int AnnoMese { get; init; }
}

/// <summary>22/10/2026: spettanze divise per anno della fattura incassata (null = anno non noto).</summary>
public record RigaAnnoFatturaSpettanze(int? AnnoFattura, IReadOnlyDictionary<string, decimal> PerVoce, decimal Base, decimal Totale);

/// <summary>
/// 22/10/2026: filtro della vista. Fattura dal/al = date delle fatture; incasso dal/al = date degli incassi (e delle note).
/// Se c'è solo il filtro sulle fatture, gli incassi vanno dalla prima data delle fatture a oggi.
/// </summary>
public sealed record FiltroSpettanze(DateTime? FatturaDal, DateTime? FatturaAl, DateTime? IncassoDal, DateTime? IncassoAl)
{
    public bool Attivo => FatturaDal is not null || FatturaAl is not null || IncassoDal is not null || IncassoAl is not null;

    public (DateTime Dal, DateTime Al) PeriodoIncassi(int anno)
    {
        var dal = IncassoDal ?? FatturaDal ?? new DateTime(anno, 1, 1);
        var al = IncassoAl ?? (FatturaDal is not null || FatturaAl is not null ? DateTime.Today : new DateTime(anno, 12, 31));
        return dal <= al ? (dal.Date, al.Date) : (al.Date, dal.Date);
    }
}
public record RigaClienteSpettanze(string Cliente, string? RagioneSociale, string? Zona, IReadOnlyDictionary<string, decimal> PerVoce, decimal Base, decimal Totale);
public record RigaFatturaConsulente(DateTime Data, string Documento, string Centro, string Voce, decimal Importo, bool DocenzaOraria);

public sealed record RisultatoSpettanze(
    ConsulentePercentuale Consulente, int Anno, AnnoSpettanzeChiuso? Chiuso, DateTime? UltimaImportazione,
    IReadOnlyList<MovimentoSpettanza> Movimenti, IReadOnlyList<RigaMeseSpettanze> Mesi, IReadOnlyList<RigaClienteSpettanze> Clienti,
    IReadOnlyList<RigaFatturaConsulente> Fatture, IReadOnlyDictionary<string, decimal> PerVoce,
    decimal Maturato, decimal Acconti, decimal DocenzeOrarie, IReadOnlyList<string> Avvisi)
{
    public decimal Conguaglio => Maturato - Acconti;
    /// <summary>22/10/2026: filtro della vista (null = vista dell'anno: incassi dell'anno, acconti e conguaglio).</summary>
    public FiltroSpettanze? Filtro { get; init; }
    public DateTime IncassiDal { get; init; }
    public DateTime IncassiAl { get; init; }
    /// <summary>22/10/2026: spettanze per anno della fattura (es. incassi 2026 di fatture 2026 e di fatture 2025).</summary>
    public IReadOnlyList<RigaAnnoFatturaSpettanze> PerAnnoFattura { get; init; } = [];
    public bool Filtrato => Filtro?.Attivo == true;
}

/// <summary>
/// Spettanze dei consulenti a percentuale (19/10/2026): carica le copie di lavoro AZW, applica il motore e gestisce
/// configurazione, acconti (fatture del consulente) e chiusura annuale. Definizione: claude/analisi-spettanze-consulenti.md.
/// </summary>
public sealed class ServizioSpettanzeConsulenti
{
    private readonly ContestoLaboratorioAI _db;
    public ServizioSpettanzeConsulenti(ContestoLaboratorioAI db) => _db = db;

    // ── Popolamento iniziale (idempotente) ──

    public static async Task PopolaAsync(ContestoLaboratorioAI db, CancellationToken ct)
    {
        var ora = DateTime.UtcNow;
        if (!await db.VociGruppiSpettanze.AnyAsync(ct))
        {
            void G(GruppoVociSpettanze g, string v) => db.VociGruppiSpettanze.Add(new VociGruppoSpettanze { Gruppo = g, Voci = v, ModificatoDa = "Sistema (valori iniziali)", DataModifica = ora });
            G(GruppoVociSpettanze.Analisi, "ANA;ANE;AMB;LEC;LEG;LEV;LEB;LEP;LPR;LBA;AND;ANR");
            G(GruppoVociSpettanze.AnalisiEsterne, "ANE");
            G(GruppoVociSpettanze.Formazione, "F62;FHA;FON;CFG;FSM;FPS;FOA;OHA;O62;OAN;CBA;CAA;SFU;HFU");
            G(GruppoVociSpettanze.Consulenza, "626;HAC;AT;RIF;PRY;RSP;TRA;ASI;C81;CHA;CRS;CIA;APR;ATR;MAP;COV;ACQ;ARI");
            G(GruppoVociSpettanze.Tarature, "TAR");
            await db.SaveChangesAsync(ct);
        }
        if (await db.ConsulentiPercentuale.AnyAsync(ct)) return;
        var bac = new ConsulentePercentuale { Nome = "Matteo Bacchiani", CodiceZona = "BAC", CodiceCommessa = "BACCHI", VoceCosto = "BAC", CodiceFornitore = "0530", CentriDocenzeOrarie = "FOR" };
        var dea = new ConsulentePercentuale { Nome = "Valerio De Angelis", CodiceZona = "DEA", CodiceCommessa = "VALERI", VoceCosto = "VAL", CodiceFornitore = "0510", CentriDocenzeOrarie = "FOR" };
        var rem = new ConsulentePercentuale { Nome = "Gianluca Remedia", CodiceZona = "REM", CodiceCommessa = "REMEDI", VoceCosto = "REM", CodiceFornitore = "0829", CentriDocenzeOrarie = "FOR" };
        db.ConsulentiPercentuale.AddRange(bac, dea, rem);
        await db.SaveChangesAsync(ct);
        ContrattoConsulente K(ConsulentePercentuale c, DateTime dal, DateTime? al, string rif, decimal? tar, string? nota) => new()
        {
            IdConsulentePercentuale = c.IdConsulentePercentuale, ValidoDal = dal, ValidoAl = al, Riferimento = rif, PercTarature = tar, Note = nota,
            ModificatoDa = "Sistema (valori iniziali)", DataModifica = ora
        };
        db.ContrattiConsulenti.AddRange(
            K(bac, new DateTime(2017, 1, 1), new DateTime(2026, 1, 8), "Contratto firmato il 04/04/2025", null,
                "Data di inizio da verificare: applicato anche agli anni precedenti (contratti di pari contenuto)."),
            K(bac, new DateTime(2026, 1, 9), null, "Contratto firmato il 09/01/2026", 50m, "Aggiunge le tarature al 50% e la docenza on line."),
            K(dea, new DateTime(2017, 1, 1), null, "Contratto firmato il 14/01/2026", 50m,
                "Tarature al 50% come Bacchiani (decisione 19/10/2026). Data di inizio da verificare."),
            K(rem, new DateTime(2025, 5, 27), null, "Contratto firmato il 27/05/2025", null, "Acconto mensile indicato nel contratto: 1.000 €."));
        await db.SaveChangesAsync(ct);
    }

    public async Task<GruppiVociConsulenti> GruppiAsync(CancellationToken ct)
    {
        var g = await _db.VociGruppiSpettanze.AsNoTracking().ToDictionaryAsync(x => x.Gruppo, x => x.Voci, ct);
        IReadOnlySet<string> I(GruppoVociSpettanze x) => GruppiVociConsulenti.Insieme(g.GetValueOrDefault(x));
        return new GruppiVociConsulenti
        {
            Analisi = I(GruppoVociSpettanze.Analisi), AnalisiEsterne = I(GruppoVociSpettanze.AnalisiEsterne),
            Formazione = I(GruppoVociSpettanze.Formazione), Consulenza = I(GruppoVociSpettanze.Consulenza), Tarature = I(GruppoVociSpettanze.Tarature)
        };
    }

    private static bool DocenzaOraria(ConsulentePercentuale c, string centro) =>
        GruppiVociConsulenti.Insieme(c.CentriDocenzeOrarie).Contains(centro.Trim().ToUpperInvariant());

    // ── Calcolo ──

    public Task<RisultatoSpettanze> CalcolaAsync(int idConsulente, int anno, CancellationToken ct) => CalcolaAsync(idConsulente, anno, null, ct);

    /// <summary>
    /// Calcolo dell'anno (incassi dell'anno, acconti, conguaglio) oppure, con un <paramref name="filtro"/>, vista filtrata per
    /// date delle fatture e degli incassi: sempre ricalcolata da AZW, senza acconti né conguaglio (che restano annuali).
    /// </summary>
    public async Task<RisultatoSpettanze> CalcolaAsync(int idConsulente, int anno, FiltroSpettanze? filtro, CancellationToken ct)
    {
        await PopolaAsync(_db, ct);
        var c = await _db.ConsulentiPercentuale.AsNoTracking().FirstAsync(x => x.IdConsulentePercentuale == idConsulente, ct);
        var filtrato = filtro?.Attivo == true;
        var (dal, al) = filtrato ? filtro!.PeriodoIncassi(anno) : (new DateTime(anno, 1, 1), new DateTime(anno, 12, 31));
        var chiusoAnno = await _db.AnniSpettanzeChiusi.AsNoTracking().FirstOrDefaultAsync(x => x.IdConsulentePercentuale == idConsulente && x.Anno == anno, ct);
        var chiuso = filtrato ? null : chiusoAnno;
        var ultima = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).OrderByDescending(i => i.DataOra).Select(i => (DateTime?)i.DataOra).FirstOrDefaultAsync(ct);
        var avvisi = new List<string>();
        if (filtrato)
        {
            avvisi.Add($"Vista filtrata: incassi dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}"
                + (filtro!.FatturaDal is not null || filtro.FatturaAl is not null
                    ? $" di fatture {(filtro.FatturaDal is DateTime fd ? "dal " + fd.ToString("dd/MM/yyyy") : "")} {(filtro.FatturaAl is DateTime fa ? "al " + fa.ToString("dd/MM/yyyy") : "")}".TrimEnd()
                    : "") + ". Acconti e conguaglio si vedono solo nella vista dell'anno.");
            if (chiusoAnno is not null) avvisi.Add($"L'anno {anno} è chiuso: questa vista è ricalcolata con i dati AZW di oggi e può differire dai valori congelati.");
        }

        List<MovimentoSpettanza> movimenti;
        if (chiuso is not null)
        {
            movimenti = (await _db.MovimentiSpettanze.AsNoTracking().Where(m => m.IdAnnoSpettanzeChiuso == chiuso.IdAnnoSpettanzeChiuso).ToListAsync(ct))
                .Select(m => new MovimentoSpettanza(m.Data, m.Voce, m.CodiceCliente, m.Documento, m.VoceAzw, m.Commessa, m.Origine, m.Base, m.Percentuale, m.Importo))
                .OrderBy(m => m.Data).ToList();
        }
        else
        {
            var contratti = await _db.ContrattiConsulenti.AsNoTracking().Where(k => k.IdConsulentePercentuale == idConsulente && !k.Annullato).ToListAsync(ct);
            if (contratti.Count == 0) avvisi.Add("Nessun contratto registrato: nessun calcolo.");
            var gruppi = await GruppiAsync(ct);
            var voci = gruppi.Analisi.Concat(gruppi.Formazione).Concat(gruppi.Consulenza).Concat(gruppi.Tarature).Distinct().ToList();
            var zone = await _db.AzwClienti.AsNoTracking().GroupBy(x => x.CodiceCliente).Select(g => new { g.Key, Zona = g.Max(x => x.Zona) })
                .ToDictionaryAsync(x => x.Key, x => x.Zona, ct);
            var righeDb = await (from r in _db.AzwRigheFattura.AsNoTracking()
                                 join f in _db.AzwFatture.AsNoTracking() on r.ChiaveFattura equals f.Chiave
                                 where voci.Contains(r.Voce)
                                 select new { r.ChiaveFattura, f.CodiceCliente, f.DataFattura, r.Voce, r.Commessa, r.Netto }).ToListAsync(ct);
            var righe = righeDb.Select(r => new RigaRicavoConsulente(r.ChiaveFattura, r.CodiceCliente, zone.GetValueOrDefault(r.CodiceCliente),
                r.DataFattura, r.Voce, r.Commessa, r.Netto)).ToList();
            var prime = MotoreSpettanzeConsulenti.PrimeFatture(righe);
            var incassi = await _db.AzwIncassi.AsNoTracking().Where(i => i.DataIncasso >= dal && i.DataIncasso <= al)
                .Select(i => new IncassoConsulente(i.ChiaveFattura, i.DataIncasso, i.ImportoLordo)).ToListAsync(ct);
            var chiavi = incassi.Select(i => i.Chiave).Distinct().ToList();
            var lordi = await _db.AzwFatture.AsNoTracking().Where(f => chiavi.Contains(f.Chiave)).ToDictionaryAsync(f => f.Chiave, f => f.Lordo, ct);
            var note = (await _db.AzwRigheNoteCredito.AsNoTracking().Where(n => n.Data >= dal && n.Data <= al && voci.Contains(n.Voce)).ToListAsync(ct))
                .Select(n => new RigaNotaConsulente(n.Riferimento, n.CodiceCliente, zone.GetValueOrDefault(n.CodiceCliente), n.Data, n.Voce, n.Commessa, n.Netto)).ToList();
            if (righe.Count > 0 && righe.All(r => r.Commessa is null))
                avvisi.Add("Le righe di fattura non hanno ancora il codice COMMESSA: rifare l'importazione da AZW (Provvigioni → Importa).");
            movimenti = MotoreSpettanzeConsulenti.Calcola(c, contratti, gruppi, righe, lordi, incassi, note, prime, dal, al,
                filtro?.FatturaDal, filtro?.FatturaAl);
        }

        var fatture = string.IsNullOrWhiteSpace(c.CodiceFornitore) || filtrato ? new List<RigaFatturaConsulente>()
            : (await _db.AzwFattureConsulenti.AsNoTracking()
                .Where(f => f.CodiceFornitore == c.CodiceFornitore && f.DataRegistrazione >= dal && f.DataRegistrazione <= al).ToListAsync(ct))
              .Select(f => new RigaFatturaConsulente(f.DataRegistrazione, f.Documento, f.Centro, f.Voce, f.Importo, DocenzaOraria(c, f.Centro)))
              .OrderBy(f => f.Data).ToList();
        if (string.IsNullOrWhiteSpace(c.CodiceFornitore) && !filtrato) avvisi.Add("Codice fornitore non indicato: acconti non disponibili.");

        var lettere = MotoreSpettanzeConsulenti.NomiVoci.Keys.ToList();
        // Mesi dell'anno; nella vista filtrata i mesi del periodo degli incassi (al massimo 36).
        var elencoMesi = new List<DateTime>();
        for (var m = new DateTime(dal.Year, dal.Month, 1); m <= al && elencoMesi.Count < 36; m = m.AddMonths(1)) elencoMesi.Add(m);
        var mesi = elencoMesi.Select(mese =>
        {
            var mm = movimenti.Where(x => x.Data.Year == mese.Year && x.Data.Month == mese.Month).ToList();
            var pv = lettere.ToDictionary(l => l, l => mm.Where(x => x.Voce == l).Sum(x => x.Importo));
            var ff = fatture.Where(f => f.Data.Year == mese.Year && f.Data.Month == mese.Month).ToList();
            return new RigaMeseSpettanze(mese.Month, pv, mm.Sum(x => x.Importo), ff.Where(f => !f.DocenzaOraria).Sum(f => f.Importo), ff.Where(f => f.DocenzaOraria).Sum(f => f.Importo))
                { AnnoMese = mese.Year };
        }).ToList();
        var perAnnoFattura = movimenti.GroupBy(m => m.AnnoFattura).OrderByDescending(g => g.Key ?? 0)
            .Select(g => new RigaAnnoFatturaSpettanze(g.Key, lettere.ToDictionary(l => l, l => g.Where(x => x.Voce == l).Sum(x => x.Importo)),
                g.Sum(x => x.Base), g.Sum(x => x.Importo))).ToList();

        var codici = movimenti.Select(m => m.Cliente).Distinct().ToList();
        var anag = await _db.AzwClienti.AsNoTracking().Where(x => codici.Contains(x.CodiceCliente))
            .GroupBy(x => x.CodiceCliente).Select(g => new { g.Key, Rag = g.Max(x => x.RagioneSociale), Zona = g.Max(x => x.Zona) }).ToDictionaryAsync(x => x.Key, ct);
        var clienti = movimenti.GroupBy(m => m.Cliente).Select(g => new RigaClienteSpettanze(g.Key, anag.GetValueOrDefault(g.Key)?.Rag, anag.GetValueOrDefault(g.Key)?.Zona,
                lettere.ToDictionary(l => l, l => g.Where(x => x.Voce == l).Sum(x => x.Importo)), g.Sum(x => x.Base), g.Sum(x => x.Importo)))
            .OrderByDescending(x => x.Totale).ToList();
        var perVoce = lettere.ToDictionary(l => l, l => movimenti.Where(x => x.Voce == l).Sum(x => x.Importo));
        var maturato = chiuso?.Maturato ?? movimenti.Sum(m => m.Importo);
        var acconti = chiuso?.Acconti ?? fatture.Where(f => !f.DocenzaOraria).Sum(f => f.Importo);
        var docenze = chiuso?.DocenzeOrarie ?? fatture.Where(f => f.DocenzaOraria).Sum(f => f.Importo);
        if (chiuso is null && anno == DateTime.Today.Year && !filtrato) avvisi.Add("Anno in corso: il maturato cresce con gli incassi; il conguaglio si determina al 31 dicembre.");
        return new RisultatoSpettanze(c, anno, chiuso, ultima, movimenti, mesi, clienti, fatture, perVoce, maturato, acconti, docenze, avvisi)
        {
            Filtro = filtrato ? filtro : null, IncassiDal = dal, IncassiAl = al, PerAnnoFattura = perAnnoFattura
        };
    }

    // ── Chiusura annuale ──

    public async Task<string?> ChiudiAsync(int idConsulente, int anno, string chiusoDa, string? note, CancellationToken ct)
    {
        if (anno >= DateTime.Today.Year) return "Si può chiudere solo un anno concluso.";
        if (await _db.AnniSpettanzeChiusi.AnyAsync(x => x.IdConsulentePercentuale == idConsulente && x.Anno == anno, ct)) return "Anno già chiuso.";
        var ultima = await _db.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).OrderByDescending(i => i.DataOra).FirstOrDefaultAsync(ct);
        if (ultima is null || ultima.DataOra < new DateTime(anno + 1, 1, 1)) return "L'ultima importazione da AZW non copre la fine dell'anno: rifarla prima di chiudere.";
        var r = await CalcolaAsync(idConsulente, anno, ct);
        var strategia = _db.Database.CreateExecutionStrategy();
        await strategia.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var a = new AnnoSpettanzeChiuso
            {
                IdConsulentePercentuale = idConsulente, Anno = anno, Maturato = r.Maturato, Acconti = r.Acconti, DocenzeOrarie = r.DocenzeOrarie,
                Conguaglio = r.Conguaglio, DataChiusura = DateTime.UtcNow, ChiusoDa = chiusoDa, DataImportazioneAzw = ultima.DataOra,
                Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 1000)]
            };
            _db.AnniSpettanzeChiusi.Add(a);
            await _db.SaveChangesAsync(ct);
            _db.MovimentiSpettanze.AddRange(r.Movimenti.Select(m => new MovimentoSpettanzaArchiviato
            {
                IdAnnoSpettanzeChiuso = a.IdAnnoSpettanzeChiuso, Data = m.Data, Voce = m.Voce, CodiceCliente = m.Cliente, Documento = m.Documento,
                VoceAzw = m.VoceAzw, Commessa = m.Commessa, Origine = m.Origine, Base = m.Base, Percentuale = m.Percentuale, Importo = m.Importo
            }));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return null;
    }

    // ── Configurazione ──

    public async Task<string?> SalvaConsulenteAsync(ConsulentePercentuale dati, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dati.Nome) || string.IsNullOrWhiteSpace(dati.CodiceZona) || string.IsNullOrWhiteSpace(dati.CodiceCommessa))
            return "Nome, ZONA e COMMESSA sono obbligatori.";
        ConsulentePercentuale c;
        if (dati.IdConsulentePercentuale > 0)
            c = await _db.ConsulentiPercentuale.FirstOrDefaultAsync(x => x.IdConsulentePercentuale == dati.IdConsulentePercentuale, ct) ?? throw new InvalidOperationException("Consulente non trovato.");
        else { c = new ConsulentePercentuale(); _db.ConsulentiPercentuale.Add(c); }
        if (dati.IdUtente is int u && await _db.ConsulentiPercentuale.AnyAsync(x => x.IdUtente == u && x.IdConsulentePercentuale != dati.IdConsulentePercentuale, ct))
            return "Questo utente è già collegato a un altro consulente.";
        c.Nome = dati.Nome.Trim(); c.CodiceZona = dati.CodiceZona.Trim().ToUpperInvariant(); c.CodiceCommessa = dati.CodiceCommessa.Trim().ToUpperInvariant();
        c.VoceCosto = Pulisci(dati.VoceCosto)?.ToUpperInvariant(); c.CodiceFornitore = Pulisci(dati.CodiceFornitore);
        c.CentriDocenzeOrarie = Pulisci(dati.CentriDocenzeOrarie)?.ToUpperInvariant(); c.IdUtente = dati.IdUtente; c.Attivo = dati.Attivo; c.Note = Pulisci(dati.Note);
        await _db.SaveChangesAsync(ct);
        return null;
    }

    public async Task<string?> SalvaContrattoAsync(ContrattoConsulente dati, string modificatoDa, CancellationToken ct)
    {
        if (dati.ValidoAl is DateTime fine && fine < dati.ValidoDal) return "La data di fine è prima di quella di inizio.";
        foreach (var p in new decimal?[] { dati.PercConsulenza, dati.PercCampionamento, dati.PercCampionamentoEsterne, dati.PercAnalisiClienti, dati.PercPrimoAnnoServizi, dati.PercFormazioneAltri, dati.PercDocenza, dati.PercTarature })
            if (p is < 0m or > 100m) return "Le percentuali vanno da 0 a 100.";
        if (dati.MesiPrimoAnno is < 1 or > 60) return "Mesi del primo anno: da 1 a 60.";
        ContrattoConsulente k;
        if (dati.IdContrattoConsulente > 0)
        {
            k = await _db.ContrattiConsulenti.FirstOrDefaultAsync(x => x.IdContrattoConsulente == dati.IdContrattoConsulente, ct) ?? throw new InvalidOperationException("Contratto non trovato.");
            if (await AnnoChiusoNelPeriodoAsync(k.IdConsulentePercentuale, k.ValidoDal, k.ValidoAl, ct))
                return "Il contratto copre un anno già chiuso: non si modifica (registrare una nuova versione con data successiva).";
        }
        else { k = new ContrattoConsulente { IdConsulentePercentuale = dati.IdConsulentePercentuale }; _db.ContrattiConsulenti.Add(k); }
        if (await AnnoChiusoNelPeriodoAsync(k.IdConsulentePercentuale, dati.ValidoDal, dati.ValidoAl, ct))
            return "Il periodo comprende un anno già chiuso.";
        var altri = await _db.ContrattiConsulenti.AsNoTracking()
            .Where(x => x.IdConsulentePercentuale == k.IdConsulentePercentuale && !x.Annullato && x.IdContrattoConsulente != dati.IdContrattoConsulente).ToListAsync(ct);
        if (!dati.Annullato && altri.Any(x => x.ValidoDal <= (dati.ValidoAl ?? DateTime.MaxValue) && (x.ValidoAl ?? DateTime.MaxValue) >= dati.ValidoDal))
            return "Il periodo si sovrappone a un altro contratto: chiudere prima quello (data di fine).";
        k.ValidoDal = dati.ValidoDal.Date; k.ValidoAl = dati.ValidoAl?.Date; k.Riferimento = Pulisci(dati.Riferimento);
        k.PercConsulenza = dati.PercConsulenza; k.PercCampionamento = dati.PercCampionamento; k.PercCampionamentoEsterne = dati.PercCampionamentoEsterne; k.PercAnalisiClienti = dati.PercAnalisiClienti;
        k.PercPrimoAnnoServizi = dati.PercPrimoAnnoServizi; k.PercFormazioneAltri = dati.PercFormazioneAltri; k.PercDocenza = dati.PercDocenza;
        k.PercTarature = dati.PercTarature; k.MesiPrimoAnno = dati.MesiPrimoAnno; k.Annullato = dati.Annullato; k.Note = Pulisci(dati.Note);
        k.ModificatoDa = modificatoDa; k.DataModifica = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return null;
    }

    private Task<bool> AnnoChiusoNelPeriodoAsync(int idConsulente, DateTime dal, DateTime? al, CancellationToken ct) =>
        _db.AnniSpettanzeChiusi.AnyAsync(a => a.IdConsulentePercentuale == idConsulente && a.Anno >= dal.Year && a.Anno <= (al ?? DateTime.MaxValue).Year, ct);

    public async Task<string?> SalvaGruppoAsync(GruppoVociSpettanze gruppo, string voci, string modificatoDa, CancellationToken ct)
    {
        // I movimenti degli anni chiusi sono congelati: la modifica vale solo per gli anni aperti.
        var g = await _db.VociGruppiSpettanze.FirstOrDefaultAsync(x => x.Gruppo == gruppo, ct);
        if (g is null) { g = new VociGruppoSpettanze { Gruppo = gruppo }; _db.VociGruppiSpettanze.Add(g); }
        g.Voci = string.Join(";", GruppiVociConsulenti.Insieme(voci).OrderBy(x => x));
        g.ModificatoDa = modificatoDa; g.DataModifica = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return null;
    }

    private static string? Pulisci(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
