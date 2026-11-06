using System.Diagnostics;
using System.Text;
using ClosedXML.Excel;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.IA;
using LaboratorioAI.Web.Navigazione;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Guide d'uso (11/10/2026): elenco, guida a pagina intera (stampabile) e pannello laterale aperto dal pulsante
/// «? Guida». Accessibili a ogni utente autenticato, ma ciascuno vede solo le guide delle funzioni che può usare.
/// </summary>
public class GuideController : Controller
{
    private readonly IContestoUtente _utente;
    private readonly IWebHostEnvironment _ambiente;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IClientAnthropic _ia;
    private readonly OpzioniAnthropic _opzioniIa;
    private readonly ICompositeViewEngine _viste;
    private readonly ITempDataProvider _tempData;
    private readonly IServizioAudit _audit;
    private readonly ILogger<GuideController> _log;

    public GuideController(IContestoUtente utente, IWebHostEnvironment ambiente, ContestoLaboratorioAI ctx, IClientAnthropic ia,
        IOptions<OpzioniAnthropic> opzioniIa, ICompositeViewEngine viste, ITempDataProvider tempData, IServizioAudit audit,
        ILogger<GuideController> log)
    {
        _utente = utente; _ambiente = ambiente; _ctx = ctx; _ia = ia; _opzioniIa = opzioniIa.Value;
        _viste = viste; _tempData = tempData; _audit = audit; _log = log;
    }

    [HttpGet("/Guide")]
    public IActionResult Index()
    {
        ViewData["Titolo"] = "Guide";
        return View(Guide.Visibili(_utente.HaPermesso).ToList());
    }

    [HttpGet("/Guide/{codice}")]
    public IActionResult Guida(string codice)
    {
        var g = Guide.PerCodice(codice, _utente.HaPermesso);
        if (g is null) return NotFound();
        ViewData["Titolo"] = "Guida · " + g.Titolo;
        return View("Guida", g);
    }

    [HttpGet("/Guide/{codice}/pannello")]
    public IActionResult Pannello(string codice)
    {
        var g = Guide.PerCodice(codice, _utente.HaPermesso);
        if (g is null) return NotFound();
        return PartialView("_" + g.Codice, g);
    }

    public sealed record RichiestaSchermata(string? Nome, string? Png);

    /// <summary>
    /// SOLO SVILUPPO (11/10/2026): salva una schermata per le guide in wwwroot/img/guide/{nome}.png. Serve a preparare le
    /// immagini delle guide dall'ambiente di sviluppo con dati di prova; in produzione risponde 404.
    /// </summary>
    [HttpPost("/api/guide/schermata")]
    [RichiedePermesso(LaboratorioAI.Core.Costanti.Permessi.AmministrazioneUtenti)]
    public IActionResult SalvaSchermata([FromBody] RichiestaSchermata req)
    {
        if (!_ambiente.IsDevelopment()) return NotFound();
        var nome = System.Text.RegularExpressions.Regex.Replace(req.Nome ?? "", "[^a-z0-9-]", "");
        const string prefisso = "data:image/png;base64,";
        if (nome.Length == 0 || req.Png is null || !req.Png.StartsWith(prefisso)) return BadRequest(new { messaggio = "Nome o immagine non validi." });
        var byte_ = Convert.FromBase64String(req.Png[prefisso.Length..]);
        if (byte_.Length > 3_000_000) return BadRequest(new { messaggio = "Immagine troppo grande." });
        var cartella = Path.Combine(_ambiente.WebRootPath, "img", "guide");
        Directory.CreateDirectory(cartella);
        System.IO.File.WriteAllBytes(Path.Combine(cartella, nome + ".png"), byte_);
        return Json(new { salvata = $"/img/guide/{nome}.png", byte_.Length });
    }

    // ── Chiedi alla guida (11/10/2026) ─────────────────────────────

    public sealed record RichiestaDomanda(string? Domanda, string? Percorso);
    public sealed record RichiestaVoto(int Id, int Voto);

    /// <summary>
    /// Risponde a una domanda sull'uso del programma usando solo le guide (e il menu) che la persona può vedere.
    /// Al modello non arrivano dati di ArcLab né dei clienti: solo il testo delle guide e la domanda. Ogni domanda è registrata.
    /// </summary>
    [HttpPost("/api/guide/chiedi")]
    public async Task<IActionResult> Chiedi([FromBody] RichiestaDomanda req, CancellationToken ct)
    {
        if (!_utente.Autenticato) return Unauthorized();
        var domanda = (req.Domanda ?? "").Trim();
        if (domanda.Length < 3) return BadRequest(new { messaggio = "Scrivi la domanda." });
        if (domanda.Length > 1000) return BadRequest(new { messaggio = "Domanda troppo lunga: massimo 1000 caratteri." });

        var percorso = (req.Percorso ?? "").Trim();
        if (percorso.Length > 300 || !percorso.StartsWith('/')) percorso = "";
        var guidaPagina = percorso.Length > 0 ? Guide.PerPercorso(percorso, _utente.HaPermesso) : null;

        var riga = new DomandaGuida
        {
            DataOra = DateTime.UtcNow,
            IdUtente = _utente.IdUtente,
            EmailUtente = _utente.Email,
            NomeUtente = _utente.NomeCompleto,
            Percorso = percorso.Length > 0 ? percorso : null,
            CodiceGuida = guidaPagina?.Codice,
            Domanda = domanda
        };

        var cronometro = Stopwatch.StartNew();
        try
        {
            var sistema = await PreparaSistemaAsync();
            var messaggio = new StringBuilder();
            if (percorso.Length > 0)
                messaggio.Append("La persona si trova nella pagina ").Append(percorso)
                    .Append(guidaPagina is null ? "" : $" (guida «{guidaPagina.Titolo}»)").Append(".\n");
            messaggio.Append("Domanda: ").Append(domanda);

            var r = await _ia.InterrogaConSistemaAsync(sistema, messaggio.ToString(), 900, ct);
            riga.Risposta = string.IsNullOrWhiteSpace(r.Testo) ? TestoGuide.NonTrovato : r.Testo.Trim();
            riga.TokenInput = r.TokenInput;
            riga.TokenOutput = r.TokenOutput;
            riga.TokenCacheScritti = r.TokenCacheScritti;
            riga.TokenCacheLetti = r.TokenCacheLetti;
            riga.Modello = r.Modello;
            riga.CostoStimato = StimaCosto(r);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Chiedi alla guida: chiamata al modello non riuscita.");
            riga.Errore = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
        }
        riga.DurataMs = (int)cronometro.ElapsedMilliseconds;

        _ctx.DomandeGuida.Add(riga);
        await _ctx.SaveChangesAsync(CancellationToken.None);

        if (riga.Errore is not null)
            return StatusCode(503, new { messaggio = "In questo momento non riesco a rispondere: riprova tra poco oppure apri la guida." });
        return Json(new { id = riga.IdDomandaGuida, risposta = riga.Risposta });
    }

    /// <summary>👍 / 👎 sulla risposta: ognuno può valutare solo le proprie domande.</summary>
    [HttpPost("/api/guide/valuta")]
    public async Task<IActionResult> Valuta([FromBody] RichiestaVoto req, CancellationToken ct)
    {
        if (!_utente.Autenticato || _utente.IdUtente is null) return Unauthorized();
        if (req.Voto is not (1 or -1)) return BadRequest(new { messaggio = "Voto non valido." });
        var riga = await _ctx.DomandeGuida.FirstOrDefaultAsync(x => x.IdDomandaGuida == req.Id && x.IdUtente == _utente.IdUtente, ct);
        if (riga is null) return NotFound();
        riga.Valutazione = (short)req.Voto;
        await _ctx.SaveChangesAsync(ct);
        return Json(new { ok = true });
    }

    /// <summary>Istruzioni + menu + testo di tutte le guide visibili alla persona (con le sezioni che dipendono dai suoi permessi).</summary>
    private async Task<string> PreparaSistemaAsync()
    {
        var sb = new StringBuilder(TestoGuide.Istruzioni);
        sb.Append("\n\n=== MENU DI LABORATORIO AI (solo le voci che questa persona può usare) ===\n");
        sb.Append(TestoGuide.Menu(MenuApplicazione.Visibili(_utente.HaPermesso)));
        foreach (var g in Guide.Visibili(_utente.HaPermesso))
        {
            var html = await RenderizzaAsync("_" + g.Codice, g);
            sb.Append("\n\n=== GUIDA «").Append(g.Titolo).Append("» (pagine: ").Append(string.Join(", ", g.Percorsi)).Append(") ===\n");
            sb.Append(g.Descrizione).Append('\n');
            sb.Append(TestoGuide.DaHtml(html));
        }
        return sb.ToString();
    }

    private async Task<string> RenderizzaAsync(string nomeVista, Guida guida)
    {
        var esito = _viste.FindView(ControllerContext, nomeVista, isMainPage: false);
        if (esito.View is null) return "";
        var dati = new ViewDataDictionary<Guida>(new EmptyModelMetadataProvider(), new ModelStateDictionary()) { Model = guida };
        await using var scrittore = new StringWriter();
        var contesto = new ViewContext(ControllerContext, esito.View, dati, new TempDataDictionary(HttpContext, _tempData),
            scrittore, new HtmlHelperOptions());
        await esito.View.RenderAsync(contesto);
        return scrittore.ToString();
    }

    /// <summary>Costo: input normale a prezzo pieno, scrittura in cache ×1,25, lettura dalla cache ×0,1.</summary>
    private decimal StimaCosto(RispostaModello r) =>
        (r.TokenInput + r.TokenCacheScritti * 1.25m + r.TokenCacheLetti * 0.1m) / 1_000_000m * _opzioniIa.CostoInputPerMilione
        + r.TokenOutput / 1_000_000m * _opzioniIa.CostoOutputPerMilione;

    // ── Pagina «Domande alla guida» (solo amministratore) ──────────

    public sealed record FiltroDomande(DateOnly? Dal, DateOnly? Al, string? Persona, string? Testo, bool SoloNegative);
    public sealed record ConteggioTesto(string Testo, int Volte, int Negative);
    public sealed record DatiDomandeGuida(
        FiltroDomande Filtro, List<DomandaGuida> Righe, int Totale, int Persone, int Positive, int Negative, int Errori,
        decimal CostoPeriodo, decimal CostoMese, decimal CostoMesePrecedente,
        List<ConteggioTesto> Frequenti, List<ConteggioTesto> PerGuida);

    [HttpGet("/Guide/Domande")]
    [RichiedePermesso(Permessi.GuideDomande)]
    public async Task<IActionResult> Domande([FromQuery] DateOnly? dal, [FromQuery] DateOnly? al, [FromQuery] string? persona,
        [FromQuery] string? testo, [FromQuery] bool soloNegative, CancellationToken ct)
    {
        var oggi = DateOnly.FromDateTime(DateTime.Now);
        var filtro = new FiltroDomande(dal ?? oggi.AddDays(-30), al ?? oggi, persona?.Trim(), testo?.Trim(), soloNegative);
        var q = Filtra(filtro);

        var periodo = await q.Select(x => new { x.IdUtente, x.EmailUtente, x.Valutazione, x.Errore, x.CostoStimato, x.Domanda, x.CodiceGuida })
            .ToListAsync(ct);
        var righe = await q.OrderByDescending(x => x.DataOra).Take(300).ToListAsync(ct);

        var inizioMese = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToUniversalTime();
        var inizioPrec = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1).ToUniversalTime();
        var costoMese = await _ctx.DomandeGuida.Where(x => x.DataOra >= inizioMese).SumAsync(x => (decimal?)x.CostoStimato, ct) ?? 0;
        var costoPrec = await _ctx.DomandeGuida.Where(x => x.DataOra >= inizioPrec && x.DataOra < inizioMese).SumAsync(x => (decimal?)x.CostoStimato, ct) ?? 0;

        var frequenti = periodo.Where(x => x.Domanda.Length > 0)
            .GroupBy(x => Normalizza(x.Domanda))
            .Select(g => new ConteggioTesto(g.First().Domanda, g.Count(), g.Count(x => x.Valutazione == -1)))
            .OrderByDescending(x => x.Volte).ThenByDescending(x => x.Negative).Take(15).ToList();
        var titoli = Guide.Elenco.ToDictionary(g => g.Codice, g => g.Titolo);
        var perGuida = periodo
            .GroupBy(x => x.CodiceGuida is null ? "(pagina senza guida)" : titoli.GetValueOrDefault(x.CodiceGuida, x.CodiceGuida))
            .Select(g => new ConteggioTesto(g.Key, g.Count(), g.Count(x => x.Valutazione == -1)))
            .OrderByDescending(x => x.Volte).Take(15).ToList();

        var dati = new DatiDomandeGuida(filtro, righe, periodo.Count,
            periodo.Select(x => x.IdUtente?.ToString() ?? x.EmailUtente).Where(x => x is not null).Distinct().Count(),
            periodo.Count(x => x.Valutazione == 1), periodo.Count(x => x.Valutazione == -1), periodo.Count(x => x.Errore is not null),
            periodo.Sum(x => x.CostoStimato), costoMese, costoPrec, frequenti, perGuida);
        ViewData["Titolo"] = "Domande alla guida";
        return View("Domande", dati);
    }

    [HttpGet("/Guide/Domande/Excel")]
    [RichiedePermesso(Permessi.GuideDomande)]
    public async Task<IActionResult> DomandeExcel([FromQuery] DateOnly? dal, [FromQuery] DateOnly? al, [FromQuery] string? persona,
        [FromQuery] string? testo, [FromQuery] bool soloNegative, CancellationToken ct)
    {
        var oggi = DateOnly.FromDateTime(DateTime.Now);
        var filtro = new FiltroDomande(dal ?? oggi.AddDays(-30), al ?? oggi, persona?.Trim(), testo?.Trim(), soloNegative);
        var righe = await Filtra(filtro).OrderByDescending(x => x.DataOra).Take(20000).ToListAsync(ct);
        var titoli = Guide.Elenco.ToDictionary(g => g.Codice, g => g.Titolo);

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Domande");
        string[] intest = ["Data e ora", "Persona", "Email", "Pagina", "Guida della pagina", "Domanda", "Risposta", "Voto", "Errore",
            "Token input", "Token output", "Token cache scritti", "Token cache letti", "Costo stimato", "Durata (ms)"];
        for (var c = 0; c < intest.Length; c++) ws.Cell(1, c + 1).Value = intest[c];
        var n = 2;
        foreach (var r in righe)
        {
            ws.Cell(n, 1).Value = DateTime.SpecifyKind(r.DataOra, DateTimeKind.Utc).ToLocalTime();
            ws.Cell(n, 1).Style.DateFormat.Format = "dd/mm/yyyy hh:mm";
            ws.Cell(n, 2).Value = r.NomeUtente ?? "";
            ws.Cell(n, 3).Value = r.EmailUtente ?? "";
            ws.Cell(n, 4).Value = r.Percorso ?? "";
            ws.Cell(n, 5).Value = r.CodiceGuida is null ? "" : titoli.GetValueOrDefault(r.CodiceGuida, r.CodiceGuida);
            ws.Cell(n, 6).Value = r.DataAnonimizzazione is null ? r.Domanda : "(testo rimosso dopo 12 mesi)";
            ws.Cell(n, 7).Value = r.Risposta ?? "";
            ws.Cell(n, 8).Value = r.Valutazione switch { 1 => "utile", -1 => "non utile", _ => "" };
            ws.Cell(n, 9).Value = r.Errore ?? "";
            ws.Cell(n, 10).Value = r.TokenInput;
            ws.Cell(n, 11).Value = r.TokenOutput;
            ws.Cell(n, 12).Value = r.TokenCacheScritti;
            ws.Cell(n, 13).Value = r.TokenCacheLetti;
            ws.Cell(n, 14).Value = r.CostoStimato;
            ws.Cell(n, 14).Style.NumberFormat.Format = "0.0000";
            ws.Cell(n, 15).Value = r.DurataMs;
            n++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns(1, 5).AdjustToContents();
        ws.Column(6).Width = 60; ws.Column(7).Width = 80;
        ws.Columns(6, 7).Style.Alignment.WrapText = true;
        ws.SheetView.FreezeRows(1);

        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "guide.domande.esportate", tipoEntita: "DomandaGuida",
            dettaglio: new { filtro.Dal, filtro.Al, righe = righe.Count }, ct: ct);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"DomandeGuida_{filtro.Dal:yyyyMMdd}_{filtro.Al:yyyyMMdd}.xlsx");
    }

    private IQueryable<DomandaGuida> Filtra(FiltroDomande f)
    {
        var q = _ctx.DomandeGuida.AsNoTracking().AsQueryable();
        if (f.Dal is DateOnly d1)
        {
            var da = d1.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(x => x.DataOra >= da);
        }
        if (f.Al is DateOnly d2)
        {
            var a = d2.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(x => x.DataOra < a);
        }
        if (!string.IsNullOrEmpty(f.Persona))
            q = q.Where(x => (x.EmailUtente != null && x.EmailUtente.Contains(f.Persona)) || (x.NomeUtente != null && x.NomeUtente.Contains(f.Persona)));
        if (!string.IsNullOrEmpty(f.Testo))
            q = q.Where(x => x.Domanda.Contains(f.Testo) || (x.Risposta != null && x.Risposta.Contains(f.Testo)));
        if (f.SoloNegative) q = q.Where(x => x.Valutazione == -1);
        return q;
    }

    private static string Normalizza(string s) =>
        new string(s.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim();
}
