using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.Direzione;

/// <summary>Riga della tabella «percepito per mese» (compensi letti da AZW sulla voce del socio).</summary>
public sealed record PercepitoMese(int IdSocio, int Mese, decimal Importo);

/// <summary>Prospetto di un anno: il calcolo più le informazioni per la pagina.</summary>
public sealed record ProspettoCompensi(
    AnnoCompensi Anno,
    RisultatoCompensi Risultato,
    IReadOnlyList<Socio> Soci,
    IReadOnlyList<PercepitoMese> PercepitiMese,
    /// <summary>Soci per cui il recupero viene dall'anno prima (falso = scritto a mano o zero).</summary>
    IReadOnlyDictionary<int, string> OrigineRecupero);

/// <summary>
/// Compensi soci (21/10/2026). Legge da AZW (tabella di lavoro <c>dir.RegistrazioniAnalitiche</c>, sola lettura) il
/// fatturato RSPP, la base del laboratorio e i compensi percepiti, e li passa a <see cref="MotoreCompensiSoci"/>.
/// Il recupero di un anno è la differenza dell'anno prima, calcolata allo stesso modo. Un anno chiuso usa le cifre AZW
/// salvate alla chiusura. Definizione: claude/definizione-compensi-soci.md
/// </summary>
public class ServizioCompensiSoci
{
    public const string VoceRspp = "RSP";
    private readonly ContestoLaboratorioAI _ctx;

    public ServizioCompensiSoci(ContestoLaboratorioAI ctx) => _ctx = ctx;

    // ── Dati iniziali ────────────────────────────────────────────────

    /// <summary>Soci e categorie di rimborso di partenza (solo se le tabelle sono vuote).</summary>
    public async Task AssicuraDatiInizialiAsync(CancellationToken ct = default)
    {
        var ora = DateTime.UtcNow;
        if (!await _ctx.Soci.AnyAsync(ct))
        {
            var soci = new (string Nome, string Email, string Voce, string? Commessa, bool Gestore)[]
            {
                ("Marco Sertori",      "msertori@mare-a.com",    "MAR", "MARCO",  false),
                ("Mirko Maffei",       "mmaffei@mare-a.com",     "MIR", null,     true),
                ("Stefano Scaramucci", "sscaramucci@mare-a.com", "STE", "STEFAN", false),
                ("Giuliana Giulini",   "ggiulini@mare-a.com",    "GIU", null,     false),
                ("Daniele Vaccarini",  "dvaccarini@mare-a.com",  "DAN", null,     false),
            };
            var i = 0;
            foreach (var s in soci)
                _ctx.Soci.Add(new Socio
                {
                    Nome = s.Nome, Email = s.Email, VoceCompensiAzw = s.Voce, CommessaRsppAzw = s.Commessa, Gestore = s.Gestore,
                    Ordine = ++i * 10, Attivo = true, DataCreazione = ora, DataUltimaModifica = ora
                });
        }
        if (!await _ctx.CategorieRimborsi.AnyAsync(ct))
        {
            var cat = new (string Nome, decimal Magg, bool Km)[]
            {
                ("Abbonamenti e software", 0m, false),
                ("Hardware", 0m, false),
                ("Acquisti esteri senza fattura", 30m, false),
                ("Altre spese", 0m, false),
            };
            var i = 0;
            foreach (var c in cat)
                _ctx.CategorieRimborsi.Add(new CategoriaRimborso { Nome = c.Nome, Maggiorazione = c.Magg, ConKm = c.Km, Ordine = ++i * 10 });
        }
        await _ctx.SaveChangesAsync(ct);
    }

    /// <summary>Il socio attivo collegato all'e-mail dell'utente, oppure null.</summary>
    public async Task<Socio?> SocioDaEmailAsync(string? email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var e = email.Trim().ToLower();
        return await _ctx.Soci.AsNoTracking().FirstOrDefaultAsync(s => s.Attivo && s.Email.ToLower() == e, ct);
    }

    /// <summary>
    /// 22/10/2026: accessori e maturato non pagato per area, con fatturato e margine del consuntivo dell'anno
    /// (1/1–31/12, o fino a oggi per l'anno in corso).
    /// </summary>
    public async Task<List<AreaAccessori>> AccessoriPerAreaAsync(RisultatoCompensi r, ServizioConsuntivi consuntivi, CancellationToken ct = default)
    {
        var anno = r.Anno;
        var al = new DateTime(anno, 12, 31);
        if (al > DateTime.Today) al = DateTime.Today;
        var cons = await consuntivi.CalcolaAsync(new DateTime(anno, 1, 1), al, ct);
        var dati = await _ctx.CompensiSociAnno.AsNoTracking().Include(x => x.Socio).Where(x => x.Anno == anno).ToListAsync(ct);
        var rip = dati.ToDictionary(d => d.IdSocio, d => RipartizioneDi(d));
        return MotoreAccessoriAree.Calcola(r, rip, cons.Aree, cons.Totali);
    }

    /// <summary>
    /// 22/10/2026: compensi ancora da pagare ai soci per area di un anno, per la rettifica del consuntivo per area.
    /// Null se l'anno non c'è nei compensi soci.
    /// </summary>
    public async Task<Dictionary<string, decimal>?> DaPagarePerAreaAsync(int anno, CancellationToken ct = default)
    {
        var r = await CalcolaAsync(anno, ct);
        if (r is null) return null;
        var dati = await _ctx.CompensiSociAnno.AsNoTracking().Include(x => x.Socio).Where(x => x.Anno == anno).ToListAsync(ct);
        return MotoreAccessoriAree.DaPagarePerArea(r, dati.ToDictionary(d => d.IdSocio, d => RipartizioneDi(d)));
    }

    /// <summary>Ripartizione del socio per l'anno: quella impostata o quella di partenza.</summary>
    public static RipartizioneSocio RipartizioneDi(CompensoSocioAnno d)
    {
        var pred = RipartizioneSocio.Predefinita(d.Socio?.VoceCompensiAzw ?? "");
        var perc = string.IsNullOrWhiteSpace(d.RipartizioneAree) ? pred?.PercentualiResto ?? new Dictionary<string, decimal>()
            : RipartizioneSocio.Leggi(d.RipartizioneAree);
        var diff = string.IsNullOrWhiteSpace(d.RipartizioneDifferenza)
            ? RipartizioneSocio.DifferenzaPredefinita(d.Socio?.VoceCompensiAzw ?? "") : RipartizioneSocio.Leggi(d.RipartizioneDifferenza);
        return new RipartizioneSocio(d.QuotaFissaAmministrazione ?? pred?.QuotaFissaAmministrazione ?? 0m, perc) { PercentualiDifferenza = diff };
    }

    /// <summary>Il socio gestore attivo (Mirko Maffei).</summary>
    public Task<Socio?> SocioGestoreAsync(CancellationToken ct = default) =>
        _ctx.Soci.AsNoTracking().Where(s => s.Attivo && s.Gestore).OrderBy(s => s.Ordine).FirstOrDefaultAsync(ct);

    public Task<List<int>> AnniAsync(CancellationToken ct = default) =>
        _ctx.AnniCompensi.AsNoTracking().OrderByDescending(a => a.Anno).Select(a => a.Anno).ToListAsync(ct);

    // ── Calcolo ──────────────────────────────────────────────────────

    public async Task<ProspettoCompensi?> ProspettoAsync(int anno, CancellationToken ct = default)
    {
        var cache = new Dictionary<int, RisultatoCompensi?>();
        var a = await _ctx.AnniCompensi.AsNoTracking().FirstOrDefaultAsync(x => x.Anno == anno, ct);
        if (a is null) return null;
        var (risultato, origine) = await CalcolaConOrigineAsync(anno, cache, 0, ct);
        if (risultato is null) return null;
        var soci = await _ctx.Soci.AsNoTracking().OrderBy(s => s.Ordine).ToListAsync(ct);
        var percepiti = await PercepitiMeseAsync(anno, soci, ct);
        return new ProspettoCompensi(a, risultato, soci, percepiti, origine);
    }

    public async Task<RisultatoCompensi?> CalcolaAsync(int anno, CancellationToken ct = default) =>
        (await CalcolaConOrigineAsync(anno, new Dictionary<int, RisultatoCompensi?>(), 0, ct)).Risultato;

    private async Task<(RisultatoCompensi? Risultato, Dictionary<int, string> Origine)> CalcolaConOrigineAsync(int anno,
        Dictionary<int, RisultatoCompensi?> cache, int profondita, CancellationToken ct)
    {
        var origine = new Dictionary<int, string>();
        var a = await _ctx.AnniCompensi.AsNoTracking().FirstOrDefaultAsync(x => x.Anno == anno, ct);
        if (a is null) return (null, origine);
        var dati = await _ctx.CompensiSociAnno.AsNoTracking().Include(x => x.Socio)
            .Where(x => x.Anno == anno).ToListAsync(ct);
        dati = dati.OrderBy(x => x.Socio!.Ordine).ToList();
        var dal = new DateTime(anno, 1, 1); var al = new DateTime(anno, 12, 31);

        // Base del laboratorio
        var percentuali = await _ctx.PercentualiVociLaboratorio.AsNoTracking()
            .Where(x => x.Anno == anno && x.Attivo).ToListAsync(ct);
        var codiciVoci = percentuali.Select(x => x.Voce).Distinct().ToList();
        var importiVoci = await _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= dal && r.DataRegistrazione <= al && codiciVoci.Contains(r.Voce))
            .GroupBy(r => r.Voce).Select(g => new { Voce = g.Key, Importo = g.Sum(r => r.Importo) })
            .ToDictionaryAsync(x => x.Voce, x => x.Importo, StringComparer.OrdinalIgnoreCase, ct);
        var descrizioni = await _ctx.ClassificazioniVoci.AsNoTracking()
            .Where(v => codiciVoci.Contains(v.CodiceVoce)).ToDictionaryAsync(v => v.CodiceVoce, v => v.Descrizione, StringComparer.OrdinalIgnoreCase, ct);
        var vociLab = percentuali.OrderByDescending(x => x.Percentuale).ThenBy(x => x.Voce)
            .Select(x => new VoceBaseLaboratorio(x.Voce, descrizioni.GetValueOrDefault(x.Voce),
                importiVoci.GetValueOrDefault(x.Voce), x.Percentuale)).ToList();

        // Premi area laboratorio
        var premi = await _ctx.PremiAreaLaboratorio.AsNoTracking().Where(x => x.Anno == anno && x.Attivo)
            .OrderBy(x => x.Nome).Select(x => new PremioLaboratorio(x.Nome, x.Importo, x.IdSocio)).ToListAsync(ct);

        // Rimborsi (con maggiorazione)
        var rimborsi = (await _ctx.RimborsiSpese.AsNoTracking()
                .Where(r => r.Attivo && r.Data >= dal && r.Data <= al)
                .Select(r => new { r.IdSocio, r.Importo, r.Maggiorazione }).ToListAsync(ct))
            .GroupBy(r => r.IdSocio)
            .ToDictionary(g => g.Key, g => g.Sum(r => MotoreCompensiSoci.ImportoRimborso(r.Importo, r.Maggiorazione)));

        // Trasferte con mezzo privato da AZW (22/10/2026), abbinate al socio per nome dell'operatore.
        var trasferte = new Dictionary<int, decimal>();
        if (a.TrasferteDaAzw)
        {
            var lista = await TrasfertePrivateAsync(dal, al, ct);
            foreach (var d in dati)
            {
                var nome = NomeOperatore(d.Socio!);
                trasferte[d.IdSocio] = lista.Where(t => string.Equals((t.Operatore ?? "").Trim(), nome, StringComparison.OrdinalIgnoreCase)).Sum(t => t.Rimborso);
            }
        }

        // RSPP e percepiti da AZW (o dalla chiusura)
        var commesse = dati.Select(d => d.Socio!.CommessaRsppAzw).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!).ToList();
        var rspp = await _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= dal && r.DataRegistrazione <= al && r.Voce == VoceRspp && commesse.Contains(r.Commessa))
            .GroupBy(r => r.Commessa).Select(g => new { g.Key, I = g.Sum(r => r.Importo) })
            .ToDictionaryAsync(x => x.Key, x => x.I, StringComparer.OrdinalIgnoreCase, ct);
        var vociSoci = dati.Select(d => d.Socio!.VoceCompensiAzw).ToList();
        var percepiti = await _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= dal && r.DataRegistrazione <= al && vociSoci.Contains(r.Voce))
            .GroupBy(r => r.Voce).Select(g => new { g.Key, I = g.Sum(r => r.Importo) })
            .ToDictionaryAsync(x => x.Key, x => -x.I, StringComparer.OrdinalIgnoreCase, ct);

        // Recupero: differenza dell'anno prima (se esiste), altrimenti quello scritto a mano.
        RisultatoCompensi? precedente = null;
        if (dati.Any(d => d.RecuperoManuale is null) && profondita < 50)
        {
            if (!cache.TryGetValue(anno - 1, out precedente))
            {
                precedente = (await CalcolaConOrigineAsync(anno - 1, cache, profondita + 1, ct)).Risultato;
                cache[anno - 1] = precedente;
            }
        }

        var datiSoci = new List<DatiSocioCompensi>();
        foreach (var d in dati)
        {
            var s = d.Socio!;
            decimal recupero;
            if (d.RecuperoManuale is decimal rm) { recupero = rm; origine[s.IdSocio] = "scritto a mano"; }
            else if (precedente?.Soci.FirstOrDefault(x => x.IdSocio == s.IdSocio) is ContoSocio cp)
            { recupero = cp.Differenza; origine[s.IdSocio] = $"differenza {anno - 1}"; }
            else { recupero = 0m; origine[s.IdSocio] = "nessun anno prima"; }

            var fatturatoRspp = a.Chiuso && d.RsppAzwChiusura is decimal rc ? rc
                : (s.CommessaRsppAzw is { Length: > 0 } c ? rspp.GetValueOrDefault(c) : 0m);
            var percepito = a.Chiuso && d.PercepitoAzwChiusura is decimal pc ? pc : percepiti.GetValueOrDefault(s.VoceCompensiAzw);
            var rimbTrasferte = !a.TrasferteDaAzw ? 0m : a.Chiuso && d.TrasferteAzwChiusura is decimal tc ? tc : trasferte.GetValueOrDefault(s.IdSocio);
            datiSoci.Add(new DatiSocioCompensi(s.IdSocio, s.Nome, d.OreSettimanali, d.MesiAmministratore, d.ResponsabileArea,
                d.PercentualeAutoPropria, d.QuotaRspp, d.IndennitaLaboratorio, d.Premio, fatturatoRspp,
                rimborsi.GetValueOrDefault(s.IdSocio), recupero, percepito, d.RettificaPercepito ?? 0m, rimbTrasferte));
        }

        // Anno chiuso: la base AZW è quella salvata (le voci restano visibili come dettaglio, ricalcolate oggi).
        var parametri = new ParametriCompensi(a.Anno, a.BaseTempoPieno, a.OreTempoPieno, a.CompensoAmministratore,
            a.CompensoResponsabileArea, a.AssicurazioneAuto, a.Premio, a.PercentualeRspp, a.PercentualeIndennitaLaboratorio,
            a.BaseLaboratorioManuale ?? (a.Chiuso ? a.BaseLaboratorioAzwChiusura : null));
        var risultato = MotoreCompensiSoci.Calcola(parametri, datiSoci, vociLab, premi);
        if (a.Chiuso && a.BaseLaboratorioManuale is null && a.BaseLaboratorioAzwChiusura is not null)
            risultato = risultato with { BaseLaboratorioManuale = false };
        cache[anno] = risultato;
        return (risultato, origine);
    }

    /// <summary>Nome dell'operatore AZW delle trasferte di un socio (impostato, o il nome del socio).</summary>
    public static string NomeOperatore(Socio s) => (string.IsNullOrWhiteSpace(s.OperatoreTrasferteAzw) ? s.Nome : s.OperatoreTrasferteAzw!).Trim();

    private Task<List<TrasfertaAzw>> TrasfertePrivateAsync(DateTime dal, DateTime al, CancellationToken ct) =>
        _ctx.TrasferteAzw.AsNoTracking()
            .Where(t => t.Data >= dal && t.Data <= al && t.TipoMezzo == TrasfertaAzw.TipoMezzoPrivato).ToListAsync(ct);

    /// <summary>Trasferte con mezzo privato dei soci nel periodo (per la pagina), con il socio abbinato.</summary>
    public async Task<List<(Socio Socio, TrasfertaAzw Trasferta)>> TrasferteSociAsync(DateTime dal, DateTime al, CancellationToken ct = default)
    {
        var soci = await _ctx.Soci.AsNoTracking().Where(s => s.Attivo).OrderBy(s => s.Ordine).ToListAsync(ct);
        var lista = await TrasfertePrivateAsync(dal, al, ct);
        var r = new List<(Socio, TrasfertaAzw)>();
        foreach (var s in soci)
        {
            var nome = NomeOperatore(s);
            r.AddRange(lista.Where(t => string.Equals((t.Operatore ?? "").Trim(), nome, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Data).Select(t => (s, t)));
        }
        return r;
    }

    private async Task<List<PercepitoMese>> PercepitiMeseAsync(int anno, List<Socio> soci, CancellationToken ct)
    {
        var dal = new DateTime(anno, 1, 1); var al = new DateTime(anno, 12, 31);
        var voci = soci.ToDictionary(s => s.VoceCompensiAzw, s => s.IdSocio, StringComparer.OrdinalIgnoreCase);
        var codici = voci.Keys.ToList();
        var righe = await _ctx.RegistrazioniAnalitiche.AsNoTracking()
            .Where(r => r.DataRegistrazione >= dal && r.DataRegistrazione <= al && codici.Contains(r.Voce))
            .GroupBy(r => new { r.Voce, r.DataRegistrazione.Month })
            .Select(g => new { g.Key.Voce, g.Key.Month, I = g.Sum(r => r.Importo) }).ToListAsync(ct);
        return righe.Where(r => voci.ContainsKey(r.Voce)).Select(r => new PercepitoMese(voci[r.Voce], r.Month, -r.I)).ToList();
    }

    /// <summary>Registrazioni AZW dietro una cifra: «rspp», «percepito» (per socio) o «voce» (base laboratorio).</summary>
    public async Task<List<RegistrazioneAnalitica>> RegistrazioniAsync(int anno, string tipo, int? idSocio, string? voce, CancellationToken ct = default)
    {
        var dal = new DateTime(anno, 1, 1); var al = new DateTime(anno, 12, 31);
        var q = _ctx.RegistrazioniAnalitiche.AsNoTracking().Where(r => r.DataRegistrazione >= dal && r.DataRegistrazione <= al);
        Socio? s = idSocio is int id ? await _ctx.Soci.AsNoTracking().FirstOrDefaultAsync(x => x.IdSocio == id, ct) : null;
        switch (tipo)
        {
            case "rspp" when s?.CommessaRsppAzw is { Length: > 0 } c:
                q = q.Where(r => r.Voce == VoceRspp && r.Commessa == c); break;
            case "percepito" when s is not null:
                q = q.Where(r => r.Voce == s.VoceCompensiAzw); break;
            case "voce" when !string.IsNullOrWhiteSpace(voce):
                q = q.Where(r => r.Voce == voce); break;
            default: return new();
        }
        return await q.OrderBy(r => r.DataRegistrazione).ThenBy(r => r.NumeroRegistrazione).ToListAsync(ct);
    }

    // ── Gestione dell'anno ───────────────────────────────────────────

    /// <summary>
    /// Crea un anno copiando regole, percentuali e dati dei soci dall'anno prima (se c'è); altrimenti con i valori
    /// di partenza. Il recupero non si copia: viene dalla differenza dell'anno prima.
    /// </summary>
    public async Task<AnnoCompensi> CreaAnnoAsync(int anno, CancellationToken ct = default)
    {
        if (await _ctx.AnniCompensi.AnyAsync(x => x.Anno == anno, ct))
            throw new InvalidOperationException($"L'anno {anno} esiste già.");
        var ora = DateTime.UtcNow;
        var prima = await _ctx.AnniCompensi.AsNoTracking().Where(x => x.Anno < anno).OrderByDescending(x => x.Anno).FirstOrDefaultAsync(ct);
        var a = new AnnoCompensi
        {
            Anno = anno, DataCreazione = ora, DataUltimaModifica = ora,
            BaseTempoPieno = prima?.BaseTempoPieno ?? 50000m, OreTempoPieno = prima?.OreTempoPieno ?? 40m,
            CompensoAmministratore = prima?.CompensoAmministratore ?? 6000m, CompensoResponsabileArea = prima?.CompensoResponsabileArea ?? 10000m,
            AssicurazioneAuto = prima?.AssicurazioneAuto ?? 1000m, Premio = prima?.Premio ?? 5000m,
            PercentualeRspp = prima?.PercentualeRspp ?? 50m, PercentualeIndennitaLaboratorio = prima?.PercentualeIndennitaLaboratorio ?? 5m,
        };
        _ctx.AnniCompensi.Add(a);

        var percentuali = prima is null
            ? MotoreCompensiSoci.PercentualiPredefinite.Select(p => (p.Voce, p.Percentuale)).ToList()
            : (await _ctx.PercentualiVociLaboratorio.AsNoTracking().Where(x => x.Anno == prima.Anno && x.Attivo)
                .Select(x => new { x.Voce, x.Percentuale }).ToListAsync(ct)).Select(x => (x.Voce, x.Percentuale)).ToList();
        foreach (var (voce, perc) in percentuali)
            _ctx.PercentualiVociLaboratorio.Add(new PercentualeVoceLaboratorio { Anno = anno, Voce = voce, Percentuale = perc });

        var datiPrima = prima is null ? new List<CompensoSocioAnno>()
            : await _ctx.CompensiSociAnno.AsNoTracking().Where(x => x.Anno == prima.Anno).ToListAsync(ct);
        var soci = await _ctx.Soci.AsNoTracking().Where(s => s.Attivo).ToListAsync(ct);
        foreach (var s in soci)
        {
            var p = datiPrima.FirstOrDefault(x => x.IdSocio == s.IdSocio);
            _ctx.CompensiSociAnno.Add(new CompensoSocioAnno
            {
                Anno = anno, IdSocio = s.IdSocio, DataCreazione = ora, DataUltimaModifica = ora,
                OreSettimanali = p?.OreSettimanali ?? 40m, MesiAmministratore = p?.MesiAmministratore ?? 0,
                ResponsabileArea = p?.ResponsabileArea ?? true, PercentualeAutoPropria = p?.PercentualeAutoPropria ?? 100m,
                QuotaRspp = p?.QuotaRspp ?? !string.IsNullOrWhiteSpace(s.CommessaRsppAzw),
                IndennitaLaboratorio = p?.IndennitaLaboratorio ?? false,
                QuotaFissaAmministrazione = p?.QuotaFissaAmministrazione, RipartizioneAree = p?.RipartizioneAree,
                RipartizioneDifferenza = p?.RipartizioneDifferenza,
            });
        }
        await _ctx.SaveChangesAsync(ct);
        return a;
    }

    /// <summary>Chiude l'anno: salva le cifre AZW usate (RSPP, percepiti, base laboratorio). Riapribile.</summary>
    public async Task ChiudiAsync(int anno, string? chi, CancellationToken ct = default)
    {
        var a = await _ctx.AnniCompensi.FirstAsync(x => x.Anno == anno, ct);
        if (a.Chiuso) return;
        var r = await CalcolaAsync(anno, ct) ?? throw new InvalidOperationException("Anno non calcolabile.");
        var dati = await _ctx.CompensiSociAnno.Include(x => x.Socio).Where(x => x.Anno == anno).ToListAsync(ct);
        var dal = new DateTime(anno, 1, 1); var al = new DateTime(anno, 12, 31);
        foreach (var d in dati)
        {
            var conto = r.Soci.First(x => x.IdSocio == d.IdSocio);
            d.PercepitoAzwChiusura = conto.PercepitoAzw;
            d.TrasferteAzwChiusura = conto.RimborsiTrasferte;
            var c = d.Socio!.CommessaRsppAzw;
            d.RsppAzwChiusura = string.IsNullOrWhiteSpace(c) ? 0m : await _ctx.RegistrazioniAnalitiche
                .Where(x => x.DataRegistrazione >= dal && x.DataRegistrazione <= al && x.Voce == VoceRspp && x.Commessa == c)
                .SumAsync(x => x.Importo, ct);
            d.DataUltimaModifica = DateTime.UtcNow;
        }
        a.BaseLaboratorioAzwChiusura = r.BaseLaboratorioCalcolata;
        a.Chiuso = true; a.DataChiusura = DateTime.UtcNow; a.ChiusoDa = chi; a.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task RiapriAsync(int anno, CancellationToken ct = default)
    {
        var a = await _ctx.AnniCompensi.FirstAsync(x => x.Anno == anno, ct);
        a.Chiuso = false; a.DataChiusura = null; a.ChiusoDa = null; a.BaseLaboratorioAzwChiusura = null;
        a.DataUltimaModifica = DateTime.UtcNow;
        foreach (var d in await _ctx.CompensiSociAnno.Where(x => x.Anno == anno).ToListAsync(ct))
        {
            d.RsppAzwChiusura = null; d.PercepitoAzwChiusura = null; d.TrasferteAzwChiusura = null; d.DataUltimaModifica = DateTime.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<bool> AnnoChiusoAsync(int anno, CancellationToken ct = default) =>
        await _ctx.AnniCompensi.AnyAsync(x => x.Anno == anno && x.Chiuso, ct);
}
