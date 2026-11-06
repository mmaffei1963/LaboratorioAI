using ClosedXML.Excel;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Direzione;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

public sealed record ModelloCompensiSoci(int? Anno, IReadOnlyList<int> Anni, ProspettoCompensi? Prospetto, Socio SocioCorrente,
    bool PuoGestire, DateTime? UltimaImportazioneAzw, EsitoImportazioneCompensi? Importazione)
{
    /// <summary>22/10/2026: accessori e maturato non pagato per area (null se il consuntivo non si calcola).</summary>
    public IReadOnlyList<AreaAccessori>? Aree { get; init; }
}

public sealed record ModelloGestioneCompensi(int Anno, IReadOnlyList<int> Anni, AnnoCompensi? DatiAnno,
    List<PercentualeVoceLaboratorio> Percentuali, List<CompensoSocioAnno> DatiSoci, List<PremioAreaLaboratorio> Premi,
    List<Socio> Soci, List<CategoriaRimborso> Categorie, Dictionary<string, string?> DescrizioniVoci);

public sealed record ModelloRimborsi(int Anno, IReadOnlyList<int> Anni, List<RimborsoSpesa> Rimborsi, List<CategoriaRimborso> Categorie,
    List<Socio> Soci, Socio SocioCorrente, bool AnnoChiuso, RimborsoSpesa? InModifica);

/// <summary>
/// Compensi soci (21/10/2026): conguaglio annuale dei compensi dei soci, come il foglio Excel «Compensi_Soci», con i dati
/// AZW (RSPP, base laboratorio, percepiti), i rimborsi spese e il recupero automatico dall'anno prima.
/// Riservato: permesso «compensisoci.consulta» E socio attivo collegato all'utente; per modificare servono il permesso
/// «compensisoci.gestisci» e il socio «gestore». I rimborsi li modifica solo il proprietario.
/// Definizione: claude/definizione-compensi-soci.md
/// </summary>
[RichiedePermesso(Permessi.CompensiSociConsulta)]
public class CompensiSociController : Controller
{
    private const int MaxRicevuta = 10 * 1024 * 1024;
    private static readonly string[] TipiRicevuta = ["application/pdf", "image/jpeg", "image/png", "image/heic", "image/webp"];

    private readonly ContestoLaboratorioAI _ctx;
    private readonly ServizioCompensiSoci _servizio;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private Socio? _socio;

    public CompensiSociController(ContestoLaboratorioAI ctx, ServizioCompensiSoci servizio, IContestoUtente utente, IServizioAudit audit)
    {
        _ctx = ctx; _servizio = servizio; _utente = utente; _audit = audit;
    }

    /// <summary>Prima di ogni azione: l'utente deve essere un socio attivo (gli Amministratori non soci non entrano).</summary>
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await _servizio.AssicuraDatiInizialiAsync(HttpContext.RequestAborted);
        _socio = await _servizio.SocioDaEmailAsync(_utente.Email, HttpContext.RequestAborted);
        // 22/10/2026: solo in sviluppo, con l'accesso di sviluppo attivo, l'utente di sviluppo vede la sezione come il socio
        // gestore (per provarla). In produzione l'accesso di sviluppo non esiste e questa strada è chiusa.
        if (_socio is null && SviluppoComeGestore())
        {
            _socio = await _servizio.SocioGestoreAsync(HttpContext.RequestAborted);
            if (_socio is not null) ViewData["AvvisoSviluppo"] = $"Ambiente di sviluppo: stai vedendo la sezione come {_socio.Nome} (socio gestore).";
        }
        if (_socio is null)
        {
            ViewData["Titolo"] = "Compensi soci";
            context.Result = View("NonSocio");
            return;
        }
        await next();
    }

    private bool SviluppoComeGestore()
    {
        var amb = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        return amb.IsDevelopment() && cfg.GetValue<bool>("AutenticazioneSviluppo:Abilitato")
            && string.Equals(cfg["AutenticazioneSviluppo:Email"], _utente.Email, StringComparison.OrdinalIgnoreCase);
    }

    private bool PuoGestire => _socio is { Gestore: true } && _utente.HaPermesso(Permessi.CompensiSociGestisci);

    private Task Audit(string azione, object? dettaglio = null, EsitoOperazione esito = EsitoOperazione.Successo) =>
        _audit.RegistraAsync(AreaFunzionale.Direzione, "compensisoci." + azione, esito, dettaglio: dettaglio, ct: HttpContext.RequestAborted);

    private static int AnnoPredefinito(IReadOnlyList<int> anni) => anni.Count > 0 ? anni[0] : DateTime.Today.Year - 1;

    // ═══════════════════════════════ Prospetto ═══════════════════════════════

    public async Task<IActionResult> Index(int? anno, [FromServices] ServizioConsuntivi consuntivi, CancellationToken ct)
    {
        ViewData["Titolo"] = "Compensi soci";
        var anni = await _servizio.AnniAsync(ct);
        var a = anno ?? (anni.Count > 0 ? AnnoPredefinito(anni) : (int?)null);
        var prospetto = a is int x ? await _servizio.ProspettoAsync(x, ct) : null;
        var imp = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).MaxAsync(i => (DateTime?)i.DataOra, ct);
        EsitoImportazioneCompensi? esito = null;
        if (TempData["ImportazioneCompensi"] is string js)
            esito = System.Text.Json.JsonSerializer.Deserialize<EsitoImportazioneCompensi>(js);
        IReadOnlyList<AreaAccessori>? aree = null;
        if (prospetto is not null)
        {
            try { aree = await _servizio.AccessoriPerAreaAsync(prospetto.Risultato, consuntivi, ct); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { aree = null; }
        }
        return View(new ModelloCompensiSoci(a, anni, prospetto, _socio!, PuoGestire, imp, esito) { Aree = aree });
    }

    /// <summary>Registrazioni AZW dietro una cifra (finestra di dettaglio).</summary>
    public async Task<IActionResult> Registrazioni(int anno, string tipo, int? idSocio, string? voce, CancellationToken ct)
    {
        var righe = await _servizio.RegistrazioniAsync(anno, tipo ?? "", idSocio, voce, ct);
        return PartialView("~/Views/Consuntivi/_Dettaglio.cshtml", righe);
    }

    public async Task<IActionResult> Esporta(int anno, CancellationToken ct)
    {
        var p = await _servizio.ProspettoAsync(anno, ct);
        if (p is null) return NotFound();
        await Audit("esporta", new { anno });
        var dal = new DateTime(anno, 1, 1); var al = new DateTime(anno, 12, 31);
        var rimborsi = await _ctx.RimborsiSpese.AsNoTracking().Include(r => r.Socio).Include(r => r.Categoria)
            .Where(r => r.Attivo && r.Data >= dal && r.Data <= al).OrderBy(r => r.Socio!.Ordine).ThenBy(r => r.Data).ToListAsync(ct);
        using var libro = new XLWorkbook();
        EsportazioneCompensi.Prospetto(libro, p);
        EsportazioneCompensi.BaseLaboratorio(libro, p);
        EsportazioneCompensi.Rimborsi(libro, rimborsi);
        EsportazioneCompensi.Percepiti(libro, p);
        try { EsportazioneCompensi.Aree(libro, await _servizio.AccessoriPerAreaAsync(p.Risultato, HttpContext.RequestServices.GetRequiredService<ServizioConsuntivi>(), ct)); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException) { /* foglio facoltativo */ }
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Compensi_soci_{anno}.xlsx");
    }

    // ═══════════════════════════════ Gestione (solo gestore) ═══════════════════════════════

    public async Task<IActionResult> Gestione(int? anno, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        ViewData["Titolo"] = "Compensi soci · impostazioni";
        var anni = await _servizio.AnniAsync(ct);
        var a = anno ?? AnnoPredefinito(anni);
        var datiAnno = await _ctx.AnniCompensi.AsNoTracking().FirstOrDefaultAsync(x => x.Anno == a, ct);
        var perc = await _ctx.PercentualiVociLaboratorio.AsNoTracking().Where(x => x.Anno == a && x.Attivo)
            .OrderByDescending(x => x.Percentuale).ThenBy(x => x.Voce).ToListAsync(ct);
        var dati = await _ctx.CompensiSociAnno.AsNoTracking().Include(x => x.Socio).Where(x => x.Anno == a).ToListAsync(ct);
        dati = dati.OrderBy(x => x.Socio!.Ordine).ToList();
        var premi = await _ctx.PremiAreaLaboratorio.AsNoTracking().Where(x => x.Anno == a && x.Attivo).OrderBy(x => x.Nome).ToListAsync(ct);
        var soci = await _ctx.Soci.AsNoTracking().OrderBy(s => s.Ordine).ToListAsync(ct);
        var cat = await _ctx.CategorieRimborsi.AsNoTracking().OrderBy(c => c.Ordine).ToListAsync(ct);
        var voci = await _ctx.ClassificazioniVoci.AsNoTracking().Where(v => v.Ricavo).OrderBy(v => v.CodiceVoce)
            .ToDictionaryAsync(v => v.CodiceVoce, v => v.Descrizione, ct);
        return View(new ModelloGestioneCompensi(a, anni, datiAnno, perc, dati, premi, soci, cat, voci));
    }

    private async Task<IActionResult?> ControllaAnnoModificabile(int anno, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        if (await _servizio.AnnoChiusoAsync(anno, ct))
        {
            TempData["Errore"] = $"L'anno {anno} è chiuso: riaprilo per modificarlo.";
            return RedirectToAction(nameof(Gestione), new { anno });
        }
        return null;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreaAnno(int anno, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        if (anno < 2020 || anno > DateTime.Today.Year + 1) { TempData["Errore"] = "Anno non valido."; return RedirectToAction(nameof(Gestione)); }
        try
        {
            await _servizio.CreaAnnoAsync(anno, ct);
            await Audit("crea-anno", new { anno });
            TempData["Messaggio"] = $"Anno {anno} creato copiando le regole dell'anno prima: controlla i dati dei soci.";
        }
        catch (InvalidOperationException ex) { TempData["Errore"] = ex.Message; }
        return RedirectToAction(nameof(Gestione), new { anno });
    }

    public sealed class RichiestaParametri
    {
        public int Anno { get; set; }
        [Dec] public decimal BaseTempoPieno { get; set; }
        [Dec] public decimal OreTempoPieno { get; set; }
        [Dec] public decimal CompensoAmministratore { get; set; }
        [Dec] public decimal CompensoResponsabileArea { get; set; }
        [Dec] public decimal AssicurazioneAuto { get; set; }
        [Dec] public decimal Premio { get; set; }
        [Dec] public decimal PercentualeRspp { get; set; }
        [Dec] public decimal PercentualeIndennitaLaboratorio { get; set; }
        [Dec] public decimal? BaseLaboratorioManuale { get; set; }
        public string? NotaBaseLaboratorio { get; set; }
        public string? Note { get; set; }
        public bool TrasferteDaAzw { get; set; }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaParametri(RichiestaParametri r, CancellationToken ct)
    {
        if (await ControllaAnnoModificabile(r.Anno, ct) is IActionResult no) return no;
        if (r.BaseLaboratorioManuale is not null && string.IsNullOrWhiteSpace(r.NotaBaseLaboratorio))
        {
            TempData["Errore"] = "Se scrivi a mano la base del laboratorio serve una nota che dica perché.";
            return RedirectToAction(nameof(Gestione), new { anno = r.Anno });
        }
        if (r.OreTempoPieno <= 0) { TempData["Errore"] = "Le ore del tempo pieno devono essere più di zero."; return RedirectToAction(nameof(Gestione), new { anno = r.Anno }); }
        var a = await _ctx.AnniCompensi.FirstAsync(x => x.Anno == r.Anno, ct);
        a.BaseTempoPieno = r.BaseTempoPieno; a.OreTempoPieno = r.OreTempoPieno; a.CompensoAmministratore = r.CompensoAmministratore;
        a.CompensoResponsabileArea = r.CompensoResponsabileArea; a.AssicurazioneAuto = r.AssicurazioneAuto; a.Premio = r.Premio;
        a.PercentualeRspp = r.PercentualeRspp; a.PercentualeIndennitaLaboratorio = r.PercentualeIndennitaLaboratorio;
        a.BaseLaboratorioManuale = r.BaseLaboratorioManuale;
        a.NotaBaseLaboratorio = r.BaseLaboratorioManuale is null ? null : r.NotaBaseLaboratorio?.Trim();
        a.Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
        a.TrasferteDaAzw = r.TrasferteDaAzw;
        a.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await Audit("parametri", r);
        TempData["Messaggio"] = "Regole dell'anno salvate.";
        return RedirectToAction(nameof(Gestione), new { anno = r.Anno });
    }

    public sealed class RigaPercentuale { public int? Id { get; set; } public string? Voce { get; set; } [Dec] public decimal Percentuale { get; set; } public bool Togli { get; set; } }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaPercentuali(int anno, List<RigaPercentuale> righe, CancellationToken ct)
    {
        if (await ControllaAnnoModificabile(anno, ct) is IActionResult no) return no;
        var esistenti = await _ctx.PercentualiVociLaboratorio.Where(x => x.Anno == anno && x.Attivo).ToListAsync(ct);
        foreach (var r in righe ?? new())
        {
            var voce = (r.Voce ?? "").Trim().ToUpperInvariant();
            var e = r.Id is int id ? esistenti.FirstOrDefault(x => x.IdPercentualeVoceLaboratorio == id) : null;
            if (e is not null)
            {
                if (r.Togli) e.Attivo = false;
                else e.Percentuale = r.Percentuale;
            }
            else if (voce.Length > 0 && !r.Togli && !esistenti.Any(x => x.Attivo && x.Voce == voce))
            {
                var nuova = new PercentualeVoceLaboratorio { Anno = anno, Voce = voce, Percentuale = r.Percentuale };
                _ctx.PercentualiVociLaboratorio.Add(nuova); esistenti.Add(nuova);
            }
        }
        await _ctx.SaveChangesAsync(ct);
        await Audit("percentuali-voci", new { anno, righe });
        TempData["Messaggio"] = "Percentuali delle voci salvate.";
        return RedirectToAction(nameof(Gestione), new { anno });
    }

    public sealed class RigaDatiSocio
    {
        public int Id { get; set; }
        [Dec] public decimal OreSettimanali { get; set; }
        public int MesiAmministratore { get; set; }
        public bool ResponsabileArea { get; set; }
        [Dec] public decimal PercentualeAutoPropria { get; set; }
        public bool QuotaRspp { get; set; }
        public bool IndennitaLaboratorio { get; set; }
        [Dec] public decimal? Premio { get; set; }
        [Dec] public decimal? RecuperoManuale { get; set; }
        [Dec] public decimal? RettificaPercepito { get; set; }
        public string? NotaRettificaPercepito { get; set; }
        public string? Note { get; set; }
        [Dec] public decimal? QuotaFissaAmministrazione { get; set; }
        public string? RipartizioneAree { get; set; }
        public string? RipartizioneDifferenza { get; set; }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaDatiSoci(int anno, List<RigaDatiSocio> righe, CancellationToken ct)
    {
        if (await ControllaAnnoModificabile(anno, ct) is IActionResult no) return no;
        if ((righe ?? new()).Any(r => r.RettificaPercepito is not null && r.RettificaPercepito != 0 && string.IsNullOrWhiteSpace(r.NotaRettificaPercepito)))
        {
            TempData["Errore"] = "Ogni rettifica del percepito ha bisogno di una nota.";
            return RedirectToAction(nameof(Gestione), new { anno });
        }
        if ((righe ?? new()).Any(r => r.MesiAmministratore is < 0 or > 12 || r.OreSettimanali < 0 || r.PercentualeAutoPropria is < 0 or > 100))
        {
            TempData["Errore"] = "Controlla i valori: mesi da 0 a 12, ore non negative, uso auto da 0 a 100%.";
            return RedirectToAction(nameof(Gestione), new { anno });
        }
        var dati = await _ctx.CompensiSociAnno.Where(x => x.Anno == anno).ToListAsync(ct);
        foreach (var r in righe ?? new())
        {
            var d = dati.FirstOrDefault(x => x.IdCompensoSocioAnno == r.Id);
            if (d is null) continue;
            d.OreSettimanali = r.OreSettimanali; d.MesiAmministratore = r.MesiAmministratore; d.ResponsabileArea = r.ResponsabileArea;
            d.PercentualeAutoPropria = r.PercentualeAutoPropria; d.QuotaRspp = r.QuotaRspp; d.IndennitaLaboratorio = r.IndennitaLaboratorio;
            d.Premio = r.Premio; d.RecuperoManuale = r.RecuperoManuale;
            d.RettificaPercepito = r.RettificaPercepito is 0m ? null : r.RettificaPercepito;
            d.NotaRettificaPercepito = d.RettificaPercepito is null ? null : r.NotaRettificaPercepito?.Trim();
            d.Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
            d.QuotaFissaAmministrazione = r.QuotaFissaAmministrazione;
            d.RipartizioneAree = string.IsNullOrWhiteSpace(r.RipartizioneAree) ? null
                : RipartizioneSocio.Scrivi(RipartizioneSocio.Leggi(r.RipartizioneAree)) is { Length: > 0 } rip ? rip : null;
            d.RipartizioneDifferenza = string.IsNullOrWhiteSpace(r.RipartizioneDifferenza) ? null
                : RipartizioneSocio.Scrivi(RipartizioneSocio.Leggi(r.RipartizioneDifferenza)) is { Length: > 0 } rd ? rd : null;
            d.DataUltimaModifica = DateTime.UtcNow;
        }
        // Soci attivi senza riga per l'anno (es. socio nuovo): si aggiungono.
        var mancanti = await _ctx.Soci.Where(s => s.Attivo && !_ctx.CompensiSociAnno.Any(c => c.Anno == anno && c.IdSocio == s.IdSocio)).ToListAsync(ct);
        foreach (var s in mancanti)
            _ctx.CompensiSociAnno.Add(new CompensoSocioAnno { Anno = anno, IdSocio = s.IdSocio, DataCreazione = DateTime.UtcNow, DataUltimaModifica = DateTime.UtcNow });
        await _ctx.SaveChangesAsync(ct);
        await Audit("dati-soci", new { anno, righe });
        TempData["Messaggio"] = "Dati dei soci salvati.";
        return RedirectToAction(nameof(Gestione), new { anno });
    }

    public sealed class RigaPremio { public int? Id { get; set; } public string? Nome { get; set; } [Dec] public decimal Importo { get; set; } public int? IdSocio { get; set; } public string? Note { get; set; } public bool Togli { get; set; } }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaPremi(int anno, List<RigaPremio> righe, CancellationToken ct)
    {
        if (await ControllaAnnoModificabile(anno, ct) is IActionResult no) return no;
        var esistenti = await _ctx.PremiAreaLaboratorio.Where(x => x.Anno == anno && x.Attivo).ToListAsync(ct);
        foreach (var r in righe ?? new())
        {
            var e = r.Id is int id ? esistenti.FirstOrDefault(x => x.IdPremioAreaLaboratorio == id) : null;
            if (e is not null)
            {
                if (r.Togli) { e.Attivo = false; continue; }
                e.Nome = (r.Nome ?? e.Nome).Trim(); e.Importo = r.Importo; e.IdSocio = r.IdSocio; e.Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim();
            }
            else if (!r.Togli && !string.IsNullOrWhiteSpace(r.Nome) && r.Importo != 0)
                _ctx.PremiAreaLaboratorio.Add(new PremioAreaLaboratorio
                {
                    Anno = anno, Nome = r.Nome.Trim(), Importo = r.Importo, IdSocio = r.IdSocio, DataCreazione = DateTime.UtcNow,
                    Note = string.IsNullOrWhiteSpace(r.Note) ? null : r.Note.Trim()
                });
        }
        await _ctx.SaveChangesAsync(ct);
        await Audit("premi-laboratorio", new { anno, righe });
        TempData["Messaggio"] = "Premi dell'area laboratorio salvati.";
        return RedirectToAction(nameof(Gestione), new { anno });
    }

    public sealed class RigaSocio { public int Id { get; set; } public string? Email { get; set; } public string? VoceCompensiAzw { get; set; } public string? CommessaRsppAzw { get; set; } public string? OperatoreTrasferteAzw { get; set; } public bool Attivo { get; set; } }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaSoci(int anno, List<RigaSocio> righe, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        var soci = await _ctx.Soci.ToListAsync(ct);
        foreach (var r in righe ?? new())
        {
            var s = soci.FirstOrDefault(x => x.IdSocio == r.Id);
            if (s is null) continue;
            if (!string.IsNullOrWhiteSpace(r.Email)) s.Email = r.Email.Trim();
            if (!string.IsNullOrWhiteSpace(r.VoceCompensiAzw)) s.VoceCompensiAzw = r.VoceCompensiAzw.Trim().ToUpperInvariant();
            s.CommessaRsppAzw = string.IsNullOrWhiteSpace(r.CommessaRsppAzw) ? null : r.CommessaRsppAzw.Trim().ToUpperInvariant();
            s.OperatoreTrasferteAzw = string.IsNullOrWhiteSpace(r.OperatoreTrasferteAzw) ? null : r.OperatoreTrasferteAzw.Trim();
            if (s.Gestore && !r.Attivo) { TempData["Errore"] = "Il socio gestore non si può disattivare."; return RedirectToAction(nameof(Gestione), new { anno }); }
            s.Attivo = r.Attivo; s.DataUltimaModifica = DateTime.UtcNow;
        }
        await _ctx.SaveChangesAsync(ct);
        await Audit("soci", righe);
        TempData["Messaggio"] = "Soci salvati.";
        return RedirectToAction(nameof(Gestione), new { anno });
    }

    public sealed class RigaCategoria { public int? Id { get; set; } public string? Nome { get; set; } [Dec] public decimal Maggiorazione { get; set; } public bool ConKm { get; set; } public bool Attivo { get; set; } = true; }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaCategorie(int anno, List<RigaCategoria> righe, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        var cat = await _ctx.CategorieRimborsi.ToListAsync(ct);
        var ordine = cat.Count == 0 ? 0 : cat.Max(c => c.Ordine);
        foreach (var r in righe ?? new())
        {
            var c = r.Id is int id ? cat.FirstOrDefault(x => x.IdCategoriaRimborso == id) : null;
            if (c is not null)
            {
                if (!string.IsNullOrWhiteSpace(r.Nome)) c.Nome = r.Nome.Trim();
                c.Maggiorazione = r.Maggiorazione; c.ConKm = r.ConKm; c.Attivo = r.Attivo;
            }
            else if (!string.IsNullOrWhiteSpace(r.Nome))
                _ctx.CategorieRimborsi.Add(new CategoriaRimborso { Nome = r.Nome.Trim(), Maggiorazione = r.Maggiorazione, ConKm = r.ConKm, Ordine = ordine += 10 });
        }
        await _ctx.SaveChangesAsync(ct);
        await Audit("categorie-rimborsi", righe);
        TempData["Messaggio"] = "Categorie dei rimborsi salvate. La maggiorazione nuova vale per i rimborsi inseriti da adesso.";
        return RedirectToAction(nameof(Gestione), new { anno });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Chiudi(int anno, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        await _servizio.ChiudiAsync(anno, _utente.Email, ct);
        await Audit("chiudi", new { anno });
        TempData["Messaggio"] = $"Anno {anno} chiuso: le cifre AZW sono fissate e i rimborsi bloccati.";
        return RedirectToAction(nameof(Index), new { anno });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Riapri(int anno, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        await _servizio.RiapriAsync(anno, ct);
        await Audit("riapri", new { anno });
        TempData["Messaggio"] = $"Anno {anno} riaperto: le cifre AZW tornano a essere lette ogni volta.";
        return RedirectToAction(nameof(Index), new { anno });
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> ImportaExcel(int anno, string? foglio, IFormFile? file,
        [FromServices] ImportatoreCompensiSociExcel importatore, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        if (file is null || file.Length == 0) { TempData["Errore"] = "Scegli il file Excel."; return RedirectToAction(nameof(Gestione), new { anno }); }
        try
        {
            await using var s = file.OpenReadStream();
            using var ms = new MemoryStream();
            await s.CopyToAsync(ms, ct); ms.Position = 0;
            var esito = await importatore.ImportaAsync(ms, anno, foglio, _utente.Email, ct);
            await Audit("importa-excel", new { anno, esito.Foglio, file.FileName });
            TempData["ImportazioneCompensi"] = System.Text.Json.JsonSerializer.Serialize(esito);
            TempData["Messaggio"] = $"Anno {anno} caricato dal foglio «{esito.Foglio}». Sotto il confronto con l'Excel.";
            return RedirectToAction(nameof(Index), new { anno });
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or ArgumentException)
        {
            await Audit("importa-excel", new { anno, errore = ex.Message }, EsitoOperazione.Errore);
            TempData["Errore"] = "Importazione non riuscita: " + ex.Message;
            return RedirectToAction(nameof(Gestione), new { anno });
        }
    }

    // ═══════════════════════════════ Rimborsi trasferte (AZW) ═══════════════════════════════

    /// <summary>22/10/2026: trasferte con mezzo privato dei soci lette da AZW (Viaggi), per anno; tutti i soci le vedono.</summary>
    public async Task<IActionResult> Trasferte(int? anno, CancellationToken ct)
    {
        ViewData["Titolo"] = "Rimborsi trasferte soci";
        var a = anno ?? DateTime.Today.Year;
        var righe = await _servizio.TrasferteSociAsync(new DateTime(a, 1, 1), new DateTime(a, 12, 31), ct);
        var imp = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita).MaxAsync(i => (DateTime?)i.DataOra, ct);
        var annoComp = await _ctx.AnniCompensi.AsNoTracking().FirstOrDefaultAsync(x => x.Anno == a, ct);
        ViewData["Anno"] = a;
        ViewData["UltimaImportazione"] = imp;
        ViewData["TrasferteDaAzw"] = annoComp?.TrasferteDaAzw ?? true;
        ViewData["SocioCorrente"] = _socio!.IdSocio;
        return View(righe);
    }

    public async Task<IActionResult> EsportaTrasferte(int anno, CancellationToken ct)
    {
        var righe = await _servizio.TrasferteSociAsync(new DateTime(anno, 1, 1), new DateTime(anno, 12, 31), ct);
        using var libro = new XLWorkbook();
        var ws = libro.AddWorksheet("Trasferte");
        string[] h = ["Socio", "Data", "Km", "Costo al km", "Costo chilometrico", "Autostrada", "Vitto e alloggio", "Costi diversi", "Rimborso", "Mezzo", "Note"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var (s, t) in righe)
        {
            ws.Cell(r, 1).Value = s.Nome; ws.Cell(r, 2).Value = t.Data; ws.Cell(r, 3).Value = t.Km; ws.Cell(r, 4).Value = t.CostoKm;
            ws.Cell(r, 5).Value = t.CostoChilometrico; ws.Cell(r, 6).Value = t.Autostrada; ws.Cell(r, 7).Value = t.VittoAlloggio;
            ws.Cell(r, 8).Value = t.CostiDiversi; ws.Cell(r, 9).Value = t.Rimborso; ws.Cell(r, 10).Value = t.Mezzo; ws.Cell(r, 11).Value = t.Note; r++;
        }
        ws.Column(2).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Range(2, 5, Math.Max(r, 2), 9).Style.NumberFormat.Format = "#,##0.00";
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        await Audit("esporta-trasferte", new { anno });
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Trasferte_soci_{anno}.xlsx");
    }

    // ═══════════════════════════════ Rimborsi spese ═══════════════════════════════

    public async Task<IActionResult> Rimborsi(int? anno, int? modifica, CancellationToken ct)
    {
        ViewData["Titolo"] = "Rimborsi spese soci";
        var anni = (await _servizio.AnniAsync(ct)).ToList();
        var a = anno ?? DateTime.Today.Year;
        if (!anni.Contains(a)) anni.Insert(0, a);
        anni = anni.Distinct().OrderByDescending(x => x).ToList();
        var dal = new DateTime(a, 1, 1); var al = new DateTime(a, 12, 31);
        var rimborsi = await _ctx.RimborsiSpese.AsNoTracking().Include(r => r.Socio).Include(r => r.Categoria)
            .Where(r => r.Attivo && r.Data >= dal && r.Data <= al)
            .OrderBy(r => r.Socio!.Ordine).ThenByDescending(r => r.Data).ToListAsync(ct);
        var idRimborsi = rimborsi.Select(r => r.IdRimborsoSpesa).ToList();
        var conRicevuta = await _ctx.RicevuteRimborsi.AsNoTracking().Where(x => idRimborsi.Contains(x.IdRimborsoSpesa))
            .Select(x => new { x.IdRimborsoSpesa, x.NomeFile }).ToListAsync(ct);
        foreach (var r in rimborsi)
        {
            var ric = conRicevuta.FirstOrDefault(x => x.IdRimborsoSpesa == r.IdRimborsoSpesa);
            if (ric is not null) r.Ricevuta = new RicevutaRimborso { IdRimborsoSpesa = r.IdRimborsoSpesa, NomeFile = ric.NomeFile };
        }
        var cat = await _ctx.CategorieRimborsi.AsNoTracking().OrderBy(c => c.Ordine).ToListAsync(ct);
        var soci = await _ctx.Soci.AsNoTracking().Where(s => s.Attivo).OrderBy(s => s.Ordine).ToListAsync(ct);
        var inModifica = modifica is int m ? rimborsi.FirstOrDefault(r => r.IdRimborsoSpesa == m && r.IdSocio == _socio!.IdSocio) : null;
        return View(new ModelloRimborsi(a, anni, rimborsi, cat, soci, _socio!, await _servizio.AnnoChiusoAsync(a, ct), inModifica));
    }

    public sealed class RichiestaRimborso
    {
        public int? Id { get; set; }
        public DateTime Data { get; set; }
        public int IdCategoriaRimborso { get; set; }
        public string? Descrizione { get; set; }
        [Dec] public decimal Importo { get; set; }
        [Dec] public decimal? Km { get; set; }
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(12 * 1024 * 1024)]
    public async Task<IActionResult> SalvaRimborso(RichiestaRimborso r, IFormFile? ricevuta, CancellationToken ct)
    {
        var anno = r.Data.Year;
        if (r.Data == default || anno < 2020) { TempData["Errore"] = "Indica la data della spesa."; return RedirectToAction(nameof(Rimborsi)); }
        if (await _servizio.AnnoChiusoAsync(anno, ct)) { TempData["Errore"] = $"L'anno {anno} è chiuso: i rimborsi non si modificano più."; return RedirectToAction(nameof(Rimborsi), new { anno }); }
        if (string.IsNullOrWhiteSpace(r.Descrizione) || r.Importo <= 0) { TempData["Errore"] = "Servono una descrizione e un importo maggiore di zero."; return RedirectToAction(nameof(Rimborsi), new { anno }); }
        var cat = await _ctx.CategorieRimborsi.FirstOrDefaultAsync(c => c.IdCategoriaRimborso == r.IdCategoriaRimborso && c.Attivo, ct);
        if (cat is null) { TempData["Errore"] = "Scegli la categoria."; return RedirectToAction(nameof(Rimborsi), new { anno }); }
        if (ricevuta is { Length: > 0 } && (ricevuta.Length > MaxRicevuta || !TipiRicevuta.Contains(ricevuta.ContentType)))
        {
            TempData["Errore"] = "La ricevuta deve essere un PDF o una foto (JPG, PNG, HEIC, WEBP) di al massimo 10 MB.";
            return RedirectToAction(nameof(Rimborsi), new { anno });
        }

        var ora = DateTime.UtcNow;
        RimborsoSpesa rimborso;
        if (r.Id is int id)
        {
            rimborso = await _ctx.RimborsiSpese.Include(x => x.Ricevuta).FirstOrDefaultAsync(x => x.IdRimborsoSpesa == id && x.Attivo, ct)
                       ?? throw new InvalidOperationException("Rimborso non trovato.");
            if (rimborso.IdSocio != _socio!.IdSocio) return Forbid();   // solo il proprietario
            if (await _servizio.AnnoChiusoAsync(rimborso.Data.Year, ct)) { TempData["Errore"] = "Il rimborso è in un anno chiuso."; return RedirectToAction(nameof(Rimborsi), new { anno }); }
            if (rimborso.IdCategoriaRimborso != cat.IdCategoriaRimborso) rimborso.Maggiorazione = cat.Maggiorazione;
        }
        else
        {
            rimborso = new RimborsoSpesa { IdSocio = _socio!.IdSocio, DataCreazione = ora, InseritoDa = _utente.Email, Maggiorazione = cat.Maggiorazione };
            _ctx.RimborsiSpese.Add(rimborso);
        }
        rimborso.Data = r.Data.Date; rimborso.IdCategoriaRimborso = cat.IdCategoriaRimborso;
        rimborso.Descrizione = r.Descrizione.Trim(); rimborso.Importo = Math.Round(r.Importo, 2);
        rimborso.Km = cat.ConKm ? r.Km : null; rimborso.DataUltimaModifica = ora;

        if (ricevuta is { Length: > 0 })
        {
            using var ms = new MemoryStream();
            await ricevuta.CopyToAsync(ms, ct);
            rimborso.Ricevuta ??= new RicevutaRimborso();
            rimborso.Ricevuta.NomeFile = Path.GetFileName(ricevuta.FileName);
            rimborso.Ricevuta.TipoContenuto = ricevuta.ContentType;
            rimborso.Ricevuta.Contenuto = ms.ToArray();
            rimborso.Ricevuta.Dimensione = (int)ms.Length;
            rimborso.Ricevuta.DataCaricamento = ora;
        }
        await _ctx.SaveChangesAsync(ct);
        await Audit(r.Id is null ? "rimborso-nuovo" : "rimborso-modifica",
            new { rimborso.IdRimborsoSpesa, data = rimborso.Data.ToString("yyyy-MM-dd"), rimborso.Importo, categoria = cat.Nome });
        TempData["Messaggio"] = "Rimborso salvato.";
        return RedirectToAction(nameof(Rimborsi), new { anno });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogliRimborso(int id, CancellationToken ct)
    {
        var r = await _ctx.RimborsiSpese.FirstOrDefaultAsync(x => x.IdRimborsoSpesa == id && x.Attivo, ct);
        if (r is null) return NotFound();
        if (r.IdSocio != _socio!.IdSocio) return Forbid();
        if (await _servizio.AnnoChiusoAsync(r.Data.Year, ct)) { TempData["Errore"] = "Il rimborso è in un anno chiuso."; return RedirectToAction(nameof(Rimborsi), new { anno = r.Data.Year }); }
        r.Attivo = false; r.DataUltimaModifica = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        await Audit("rimborso-tolto", new { r.IdRimborsoSpesa });
        TempData["Messaggio"] = "Rimborso tolto (resta nello storico, non conta più nei conti).";
        return RedirectToAction(nameof(Rimborsi), new { anno = r.Data.Year });
    }

    public async Task<IActionResult> Ricevuta(int id, CancellationToken ct)
    {
        var ric = await _ctx.RicevuteRimborsi.AsNoTracking().FirstOrDefaultAsync(x => x.IdRimborsoSpesa == id, ct);
        if (ric is null) return NotFound();
        return File(ric.Contenuto, ric.TipoContenuto, ric.NomeFile);
    }
}
