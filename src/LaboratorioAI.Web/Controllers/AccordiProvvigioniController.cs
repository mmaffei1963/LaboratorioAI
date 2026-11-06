using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Accordi provvigionali dei commerciali (versioni con date di validità, voci con percentuale e base di calcolo,
/// soglie del premio) e basi di calcolo (centri di costo e voci AZW). Dati retributivi: solo <c>provvigioni.gestisci</c>.
/// Regole: una bozza si modifica; un accordo attivo no (si duplica). Una base usata da un accordo attivo non si modifica.
/// </summary>
// 11/10/2026: i soci (provvigioni.consulta) vedono tutto in sola lettura; le modifiche restano a provvigioni.gestisci.
[RichiedeUnoDeiPermessi(Permessi.ProvvigioniGestisci, Permessi.ProvvigioniConsulta)]
[SolaLetturaSenza(Permessi.ProvvigioniGestisci)]
public class AccordiProvvigioniController : Controller
{
    private readonly ContestoLaboratorioAI _db;
    private readonly ServizioProvvigioniLab _servizio;
    private readonly IServizioAudit _audit;

    public AccordiProvvigioniController(ContestoLaboratorioAI db, ServizioProvvigioniLab servizio, IServizioAudit audit)
    {
        _db = db;
        _servizio = servizio;
        _audit = audit;
    }

    // ── Elenco accordi ────────────────────────────────────────────────

    public async Task<IActionResult> Index(int? idCommerciale, CancellationToken ct)
    {
        ViewData["Titolo"] = "Accordi provvigioni";
        await _servizio.InizializzaAsync(ct);
        var commerciali = await _db.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).ToListAsync(ct);
        var scelto = commerciali.FirstOrDefault(c => c.IdCommerciale == idCommerciale) ?? commerciali.FirstOrDefault();

        var accordi = scelto is null
            ? new List<AccordoProvvigioni>()
            : await _db.AccordiProvvigioni.AsNoTracking().Include(a => a.Voci)
                .Where(a => a.IdCommerciale == scelto.IdCommerciale).OrderByDescending(a => a.ValidoDal).ThenByDescending(a => a.IdAccordo)
                .ToListAsync(ct);
        var periodi = ServizioProvvigioniLab.Periodi(accordi.Where(a => a.Stato == StatoAccordo.Attivo))
            .ToDictionary(p => p.Accordo.IdAccordo, p => (p.Dal, p.Al));
        var basi = await _db.BasiCalcolo.AsNoTracking().ToDictionaryAsync(b => b.IdBaseCalcolo, b => b.Nome, ct);
        var fineChiuso = await _servizio.FineUltimoTrimestreChiusoAsync(ct);
        return View(new ModelloAccordi(commerciali, scelto, accordi, periodi, basi, fineChiuso));
    }

    /// <summary>Nuovo accordo in bozza, con tutte le voci del tipo a 0% (da compilare).</summary>
    [HttpPost]
    public async Task<IActionResult> Nuovo(int idCommerciale, TipoAccordo tipo, string? nome, DateTime validoDal, CancellationToken ct)
    {
        await _servizio.InizializzaAsync(ct);
        var nomeBase = tipo == TipoAccordo.Laboratorio ? ServizioProvvigioniLab.BaseLaboratorio : ServizioProvvigioniLab.BaseTutto;
        var idBase = await _db.BasiCalcolo.Where(b => b.Nome == nomeBase).Select(b => b.IdBaseCalcolo).FirstAsync(ct);
        var a = new AccordoProvvigioni
        {
            IdCommerciale = idCommerciale, Nome = string.IsNullOrWhiteSpace(nome) ? "Nuovo accordo" : nome.Trim(), Tipo = tipo,
            Stato = StatoAccordo.Bozza, ValidoDal = validoDal.Year < 2000 ? DateTime.Today : validoDal.Date,
            Voci = ServizioProvvigioniLab.VociDelTipo(tipo).Select(v => new VoceAccordo { Voce = v, Percentuale = 0m, IdBaseCalcolo = idBase }).ToList()
        };
        _db.AccordiProvvigioni.Add(a);
        await _db.SaveChangesAsync(ct);
        await Audit("provvigioni.accordo.creazione", a.IdAccordo, new { idCommerciale, tipo, a.Nome, a.ValidoDal }, ct);
        return RedirectToAction(nameof(Dettaglio), new { id = a.IdAccordo });
    }

    // ── Dettaglio e modifica ──────────────────────────────────────────

    public async Task<IActionResult> Dettaglio(int id, CancellationToken ct)
    {
        await _servizio.InizializzaAsync(ct); // aggiunge le voci nuove agli accordi esistenti (allo 0%)
        var a = await _db.AccordiProvvigioni.AsNoTracking().Include(x => x.Voci).Include(x => x.Soglie)
            .FirstOrDefaultAsync(x => x.IdAccordo == id, ct);
        if (a is null) return NotFound();
        ViewData["Titolo"] = a.Nome;
        var commerciale = await _db.Commerciali.AsNoTracking().FirstAsync(c => c.IdCommerciale == a.IdCommerciale, ct);
        var basi = await _db.BasiCalcolo.AsNoTracking().Where(b => b.Attivo).OrderBy(b => b.Nome).ToListAsync(ct);

        // Periodo effettivo (per gli attivi) e anteprima (per le bozze).
        var attivi = await _db.AccordiProvvigioni.AsNoTracking()
            .Where(x => x.IdCommerciale == a.IdCommerciale && x.Stato == StatoAccordo.Attivo).ToListAsync(ct);
        var periodo = ServizioProvvigioniLab.Periodi(attivi).FirstOrDefault(p => p.Accordo.IdAccordo == a.IdAccordo);

        List<RigaAnteprima>? anteprima = null;
        List<RigaAnteprimaPremio>? anteprimaPremi = null;
        if (a.Stato == StatoAccordo.Bozza && await _db.ImportazioniAzw.AnyAsync(i => i.Riuscita, ct))
        {
            var attuale = await _servizio.CalcolaAsync(a.IdCommerciale, null, ct);
            var conBozza = await _servizio.CalcolaAsync(a.IdCommerciale, a.IdAccordo, ct);
            var trimestri = attuale.Movimenti.Select(m => m.Trimestre).Concat(conBozza.Movimenti.Select(m => m.Trimestre))
                .Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            anteprima = trimestri.Select(t => new RigaAnteprima(t,
                attuale.Movimenti.Where(m => m.Trimestre == t).Sum(m => m.Importo),
                conBozza.Movimenti.Where(m => m.Trimestre == t).Sum(m => m.Importo))).ToList();
            anteprimaPremi = attuale.Premi.Select(p => p.Anno).Concat(conBozza.Premi.Select(p => p.Anno)).Distinct().OrderBy(y => y)
                .Select(y => new RigaAnteprimaPremio(y,
                    attuale.Premi.FirstOrDefault(p => p.Anno == y)?.Premio ?? 0m,
                    conBozza.Premi.FirstOrDefault(p => p.Anno == y)?.Premio ?? 0m)).ToList();
        }
        var fineChiuso = await _servizio.FineUltimoTrimestreChiusoAsync(ct);
        return View(new ModelloAccordo(a, commerciale, basi, periodo is null ? ((DateTime Dal, DateTime Al)?)null : (periodo.Dal, periodo.Al), anteprima, anteprimaPremi, fineChiuso));
    }

    [HttpPost]
    public async Task<IActionResult> SalvaTestata(int id, string nome, DateTime validoDal, string? riferimentoDocumento,
        int mesiPrimoAnno, int mesiPausa, DateTime? dataClientiEsistenti, int anniConvenzione, DateTime? dataFineRapporto,
        string? fattoreOrario, string? note, CancellationToken ct)
    {
        var a = await BozzaAsync(id, ct);
        if (a is null) return Bloccato(id);
        var fattore = Decimale(fattoreOrario) ?? 1m;
        if (string.IsNullOrWhiteSpace(nome) || validoDal.Year < 2000 || mesiPrimoAnno < 1 || mesiPausa < 1 || anniConvenzione < 1 || fattore <= 0m)
        {
            TempData["Messaggio"] = "Dati non validi: servono nome, data di inizio e valori maggiori di zero.";
            return RedirectToAction(nameof(Dettaglio), new { id });
        }
        a.Nome = nome.Trim();
        a.ValidoDal = validoDal.Date;
        a.RiferimentoDocumento = Tronca(riferimentoDocumento, 300);
        a.MesiPrimoAnno = mesiPrimoAnno;
        a.MesiPausa = mesiPausa;
        a.DataClientiEsistenti = dataClientiEsistenti?.Date;
        a.AnniConvenzione = anniConvenzione;
        a.DataFineRapporto = dataFineRapporto?.Date;
        a.FattoreOrario = fattore;
        a.Note = Tronca(note, 2000);
        await _db.SaveChangesAsync(ct);
        await Audit("provvigioni.accordo.modifica", id, new { a.Nome, a.ValidoDal, a.MesiPrimoAnno, a.MesiPausa, a.DataClientiEsistenti, a.AnniConvenzione, a.DataFineRapporto, a.FattoreOrario }, ct);
        TempData["Messaggio"] = "Dati dell'accordo salvati.";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    /// <summary>Salva percentuale, base e descrizione di tutte le voci della bozza (percentuale 0 = voce non prevista).</summary>
    [HttpPost]
    public async Task<IActionResult> SalvaVoci(int id, List<RigaVoceForm> voci, CancellationToken ct)
    {
        var a = await BozzaAsync(id, ct);
        if (a is null) return Bloccato(id);
        var basiValide = await _db.BasiCalcolo.Where(b => b.Attivo).Select(b => b.IdBaseCalcolo).ToListAsync(ct);
        foreach (var r in voci)
        {
            var v = a.Voci.FirstOrDefault(x => x.IdVoceAccordo == r.IdVoceAccordo);
            if (v is null) continue;
            var pct = Decimale(r.Percentuale);
            if (pct is null || pct < 0m || pct > 100m || !basiValide.Contains(r.IdBaseCalcolo))
            {
                TempData["Messaggio"] = $"Valori non validi per «{EtichetteProvvigioni.Voce(v.Voce)}»: percentuale fra 0 e 100 e una base di calcolo.";
                return RedirectToAction(nameof(Dettaglio), new { id });
            }
            v.Percentuale = pct.Value;
            v.IdBaseCalcolo = r.IdBaseCalcolo;
            v.Descrizione = Tronca(r.Descrizione, 1000);
        }
        await _db.SaveChangesAsync(ct);
        await Audit("provvigioni.accordo.voci", id, a.Voci.Select(v => new { v.Voce, v.Percentuale, v.IdBaseCalcolo }), ct);
        TempData["Messaggio"] = "Voci salvate.";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    /// <summary>Salva le soglie del premio. Una soglia a 0 non vale (le righe non si cancellano); le righe nuove vuote sono ignorate.</summary>
    [HttpPost]
    public async Task<IActionResult> SalvaSoglie(int id, List<RigaSogliaForm> soglie, CancellationToken ct)
    {
        var a = await BozzaAsync(id, ct);
        if (a is null) return Bloccato(id);
        foreach (var r in soglie)
        {
            var soglia = Decimale(r.Soglia) ?? 0m;
            var premio = Decimale(r.Premio) ?? 0m;
            if (soglia < 0m || premio < 0m)
            {
                TempData["Messaggio"] = "Soglie e premi non possono essere negativi.";
                return RedirectToAction(nameof(Dettaglio), new { id });
            }
            if (r.IdSogliaAccordo > 0)
            {
                var s = a.Soglie.FirstOrDefault(x => x.IdSogliaAccordo == r.IdSogliaAccordo);
                if (s is null) continue;
                s.Soglia = soglia;
                s.Premio = premio;
            }
            else if (soglia > 0m)
            {
                a.Soglie.Add(new SogliaAccordo { Soglia = soglia, Premio = premio });
            }
        }
        await _db.SaveChangesAsync(ct);
        await Audit("provvigioni.accordo.soglie", id, a.Soglie.Where(s => s.Soglia > 0m).Select(s => new { s.Soglia, s.Premio }), ct);
        TempData["Messaggio"] = "Soglie del premio salvate.";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Attiva(int id, CancellationToken ct)
    {
        var errore = await _servizio.AttivaAccordoAsync(id, ct);
        await Audit("provvigioni.accordo.attivazione", id, new { errore }, ct, errore is null);
        TempData["Messaggio"] = errore ?? "Accordo attivato: da ora il calcolo usa questa versione dalla sua data di inizio.";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> RiportaInBozza(int id, CancellationToken ct)
    {
        var errore = await _servizio.RiportaInBozzaAsync(id, ct);
        await Audit("provvigioni.accordo.bozza", id, new { errore }, ct, errore is null);
        TempData["Messaggio"] = errore ?? "Accordo riportato in bozza: il calcolo usa di nuovo la versione precedente.";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Annulla(int id, CancellationToken ct)
    {
        var a = await BozzaAsync(id, ct);
        if (a is null) return Bloccato(id);
        a.Stato = StatoAccordo.Annullato;
        await _db.SaveChangesAsync(ct);
        await Audit("provvigioni.accordo.annullamento", id, new { a.Nome }, ct);
        TempData["Messaggio"] = "Bozza annullata (resta visibile nell'elenco, non entra nel calcolo).";
        return RedirectToAction(nameof(Index), new { idCommerciale = a.IdCommerciale });
    }

    [HttpPost]
    public async Task<IActionResult> Duplica(int id, CancellationToken ct)
    {
        var nuovo = await _servizio.DuplicaAccordoAsync(id, ct);
        await Audit("provvigioni.accordo.duplicazione", nuovo, new { DaAccordo = id }, ct);
        TempData["Messaggio"] = "Creata una nuova versione in bozza: modifica i valori, controlla l'anteprima e attivala.";
        return RedirectToAction(nameof(Dettaglio), new { id = nuovo });
    }

    // ── Basi di calcolo ───────────────────────────────────────────────

    public async Task<IActionResult> Basi(CancellationToken ct)
    {
        ViewData["Titolo"] = "Basi di calcolo";
        await _servizio.InizializzaAsync(ct);
        var basi = await _db.BasiCalcolo.AsNoTracking().OrderByDescending(b => b.Attivo).ThenBy(b => b.Nome).ToListAsync(ct);
        var inUso = await _servizio.BasiInUsoAsync(ct);
        return View(new ModelloBasi(basi, inUso));
    }

    public async Task<IActionResult> Base(int id, CancellationToken ct)
    {
        var b = await _db.BasiCalcolo.AsNoTracking().FirstOrDefaultAsync(x => x.IdBaseCalcolo == id, ct);
        if (b is null) return NotFound();
        ViewData["Titolo"] = "Base di calcolo: " + b.Nome;
        var inUso = (await _servizio.BasiInUsoAsync(ct)).Contains(id);

        // Centri e voci presenti nelle fatture importate, con il fatturato degli ultimi 12 mesi per riconoscerli.
        var dal = DateTime.Today.AddMonths(-12);
        var combinazioni = await _db.AzwRigheFattura.AsNoTracking()
            .Join(_db.AzwFatture, r => r.ChiaveFattura, f => f.Chiave, (r, f) => new { r.Centro, r.Voce, r.Netto, f.DataFattura })
            .GroupBy(x => new { x.Centro, x.Voce })
            .Select(g => new { g.Key.Centro, g.Key.Voce, Ultimi12 = g.Sum(x => x.DataFattura >= dal ? x.Netto : 0m), Righe = g.Count() })
            .ToListAsync(ct);
        var voci = combinazioni.Select(x => new VoceAzw(x.Centro, x.Voce, x.Ultimi12, x.Righe))
            .OrderBy(x => x.Centro, StringComparer.Ordinal).ThenByDescending(x => x.Ultimi12Mesi).ToList();
        return View(new ModelloBase(b, inUso, voci));
    }

    [HttpPost]
    public async Task<IActionResult> NuovaBase(string? nome, CancellationToken ct)
    {
        var b = new BaseCalcolo { Nome = string.IsNullOrWhiteSpace(nome) ? "Nuova base" : nome.Trim(), Elementi = "" };
        _db.BasiCalcolo.Add(b);
        await _db.SaveChangesAsync(ct);
        await AuditBase("provvigioni.base.creazione", b.IdBaseCalcolo, new { b.Nome }, ct);
        return RedirectToAction(nameof(Base), new { id = b.IdBaseCalcolo });
    }

    [HttpPost]
    public async Task<IActionResult> DuplicaBase(int id, CancellationToken ct)
    {
        var o = await _db.BasiCalcolo.AsNoTracking().FirstOrDefaultAsync(x => x.IdBaseCalcolo == id, ct);
        if (o is null) return NotFound();
        var b = new BaseCalcolo { Nome = o.Nome + " (copia)", Descrizione = o.Descrizione, TuttoIlFatturato = o.TuttoIlFatturato, Elementi = o.Elementi };
        _db.BasiCalcolo.Add(b);
        await _db.SaveChangesAsync(ct);
        await AuditBase("provvigioni.base.duplicazione", b.IdBaseCalcolo, new { DaBase = id }, ct);
        TempData["Messaggio"] = "Base duplicata: modificala e poi assegnala alle voci di un accordo in bozza.";
        return RedirectToAction(nameof(Base), new { id = b.IdBaseCalcolo });
    }

    [HttpPost]
    public async Task<IActionResult> SalvaBase(int id, string nome, string? descrizione, bool tuttoIlFatturato, string[]? elementi, bool attivo, CancellationToken ct)
    {
        var b = await _db.BasiCalcolo.FirstOrDefaultAsync(x => x.IdBaseCalcolo == id, ct);
        if (b is null) return NotFound();
        if ((await _servizio.BasiInUsoAsync(ct)).Contains(id))
        {
            TempData["Messaggio"] = "Questa base è usata da un accordo che copre un trimestre già chiuso e non si può modificare: duplicala e usa la copia in una nuova versione dell'accordo.";
            return RedirectToAction(nameof(Base), new { id });
        }
        if (string.IsNullOrWhiteSpace(nome))
        {
            TempData["Messaggio"] = "Serve un nome.";
            return RedirectToAction(nameof(Base), new { id });
        }

        // Se è spuntato "intero centro" (04:*), le singole voci di quel centro sono superflue.
        var scelti = (elementi ?? Array.Empty<string>()).Select(e => e.Trim()).Where(e => e.Contains(':')).Distinct().ToList();
        var interi = scelti.Where(e => e.EndsWith(":*", StringComparison.Ordinal)).Select(e => e[..^2]).ToHashSet();
        scelti = scelti.Where(e => e.EndsWith(":*", StringComparison.Ordinal) || !interi.Contains(e.Split(':')[0])).OrderBy(e => e, StringComparer.Ordinal).ToList();

        b.Nome = nome.Trim();
        b.Descrizione = Tronca(descrizione, 500);
        b.TuttoIlFatturato = tuttoIlFatturato;
        b.Elementi = string.Join(";", scelti);
        b.Attivo = attivo;
        await _db.SaveChangesAsync(ct);
        await AuditBase("provvigioni.base.modifica", id, new { b.Nome, b.TuttoIlFatturato, b.Elementi, b.Attivo }, ct);
        TempData["Messaggio"] = "Base di calcolo salvata.";
        return RedirectToAction(nameof(Base), new { id });
    }

    // ── Utilità ───────────────────────────────────────────────────────

    private async Task<AccordoProvvigioni?> BozzaAsync(int id, CancellationToken ct)
    {
        var a = await _db.AccordiProvvigioni.Include(x => x.Voci).Include(x => x.Soglie).FirstOrDefaultAsync(x => x.IdAccordo == id, ct);
        return a?.Stato == StatoAccordo.Bozza ? a : null;
    }

    private IActionResult Bloccato(int id)
    {
        TempData["Messaggio"] = "Si possono modificare solo gli accordi in bozza. Per cambiare un accordo attivo usa «Duplica come nuova versione».";
        return RedirectToAction(nameof(Dettaglio), new { id });
    }

    private Task Audit(string azione, int idAccordo, object dettaglio, CancellationToken ct, bool riuscita = true) =>
        _audit.RegistraAsync(AreaFunzionale.Provvigioni, azione, riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            "AccordoProvvigioni", idAccordo.ToString(CultureInfo.InvariantCulture), dettaglio, ct);

    private Task AuditBase(string azione, int idBase, object dettaglio, CancellationToken ct) =>
        _audit.RegistraAsync(AreaFunzionale.Provvigioni, azione, EsitoOperazione.Successo,
            "BaseCalcolo", idBase.ToString(CultureInfo.InvariantCulture), dettaglio, ct);

    /// <summary>Legge un decimale scritto all'italiana o all'inglese ("5,5", "5.5", "1.600,00", "1600").</summary>
    internal static decimal? Decimale(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        var t = testo.Trim().Replace(" ", "").Replace("€", "").Replace("%", "");
        if (t.Contains(',')) t = t.Replace(".", "").Replace(',', '.');
        else if (t.Count(c => c == '.') > 1) t = t.Replace(".", "");
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static string? Tronca(string? v, int max)
    {
        v = v?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}

public sealed class RigaVoceForm
{
    public int IdVoceAccordo { get; set; }
    public string? Percentuale { get; set; }
    public int IdBaseCalcolo { get; set; }
    public string? Descrizione { get; set; }
}

public sealed class RigaSogliaForm
{
    public int IdSogliaAccordo { get; set; }
    public string? Soglia { get; set; }
    public string? Premio { get; set; }
}

public sealed record ModelloAccordi(
    IReadOnlyList<Commerciale> Commerciali,
    Commerciale? Scelto,
    IReadOnlyList<AccordoProvvigioni> Accordi,
    IReadOnlyDictionary<int, (DateTime Dal, DateTime Al)> Periodi,
    IReadOnlyDictionary<int, string> NomiBasi,
    DateTime? FineUltimoTrimestreChiuso);

public sealed record ModelloAccordo(
    AccordoProvvigioni Accordo,
    Commerciale Commerciale,
    IReadOnlyList<BaseCalcolo> Basi,
    (DateTime Dal, DateTime Al)? Periodo,
    IReadOnlyList<RigaAnteprima>? Anteprima,
    IReadOnlyList<RigaAnteprimaPremio>? AnteprimaPremi,
    DateTime? FineUltimoTrimestreChiuso);

public sealed record RigaAnteprima(string Trimestre, decimal Attuale, decimal ConBozza);
public sealed record RigaAnteprimaPremio(int Anno, decimal Attuale, decimal ConBozza);

public sealed record ModelloBasi(IReadOnlyList<BaseCalcolo> Basi, IReadOnlySet<int> InUso);

public sealed record ModelloBase(BaseCalcolo Base, bool InUso, IReadOnlyList<VoceAzw> VociAzw);

/// <summary>Combinazione centro di costo + voce trovata nelle fatture AZW, con fatturato degli ultimi 12 mesi.</summary>
public sealed record VoceAzw(string Centro, string Voce, decimal Ultimi12Mesi, int Righe);
