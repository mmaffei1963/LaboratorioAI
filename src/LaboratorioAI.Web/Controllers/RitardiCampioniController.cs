using ClosedXML.Excel;
using LaboratorioAI.Core.CicloAnalitico;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.CicloAnalitico;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace LaboratorioAI.Web.Controllers;

public sealed record ModelloRitardiCampioni(IReadOnlyList<CampioneInRitardo> Campioni, ImpostazioniRitardi Impostazioni,
    IReadOnlyList<CampioneNascostoRitardi> Nascosti, bool PuoGestire, string? Errore, DateTime CalcolatoIl);

/// <summary>
/// Avviso campioni in ritardo (22/10/2026). Campioni non ancora usciti (senza rapporto di prova) oltre la soglia del loro
/// tipo, contando dalla data di inizio = ricezione + ritardo programmato. Lo vede chi vede «Da Validare»; impostazioni e
/// «non mostrare più» per chi può confermare le validazioni. Nel giorno impostato compare un avviso all'apertura.
/// Dati ArcLab in sola lettura.
/// </summary>
[RichiedePermesso(Permessi.ValidazioneVisualizza)]
public class RitardiCampioniController : Controller
{
    private const string ChiaveCache = "ritardi-campioni";
    private readonly ServizioRitardiCampioni _servizio;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly IMemoryCache _cache;

    public RitardiCampioniController(ServizioRitardiCampioni servizio, IContestoUtente utente, IServizioAudit audit, IMemoryCache cache)
    {
        _servizio = servizio; _utente = utente; _audit = audit; _cache = cache;
    }

    private bool PuoGestire => _utente.HaPermesso(Permessi.ValidazioneConferma);

    /// <summary>Calcolo con cache di 10 minuti (la query su ArcLab è pesante); svuotata dalle modifiche.</summary>
    private async Task<(List<CampioneInRitardo> Campioni, DateTime Il)> CampioniAsync(bool forza, CancellationToken ct)
    {
        if (!forza && _cache.TryGetValue(ChiaveCache, out (List<CampioneInRitardo>, DateTime) c)) return c;
        var r = (await _servizio.InRitardoAsync(ct), DateTime.Now);
        _cache.Set(ChiaveCache, r, TimeSpan.FromMinutes(10));
        return r;
    }

    public async Task<IActionResult> Index(bool aggiorna, CancellationToken ct)
    {
        ViewData["Titolo"] = "Campioni in ritardo";
        string? errore = null;
        List<CampioneInRitardo> campioni = new();
        var il = DateTime.Now;
        try { (campioni, il) = await CampioniAsync(aggiorna, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { errore = "Lettura da ArcLab non riuscita: " + ex.Message; }
        return View(new ModelloRitardiCampioni(campioni, await _servizio.ImpostazioniAsync(ct), await _servizio.NascostiAsync(ct),
            PuoGestire, errore, il));
    }

    /// <summary>Per l'avviso all'apertura: risponde con i numeri solo nei giorni impostati.</summary>
    [HttpGet("api/ritardi-campioni/avviso")]
    public async Task<IActionResult> Avviso(CancellationToken ct)
    {
        var imp = await _servizio.ImpostazioniAsync(ct);
        if (!imp.GiorniAvviso.Contains((int)DateTime.Today.DayOfWeek)) return Json(new { giornoAvviso = false });
        try
        {
            var (c, _) = await CampioniAsync(false, ct);
            return Json(new
            {
                giornoAvviso = true, totale = c.Count,
                parametri = c.Count(x => x.Stato == StatoRitardo.ParametriMancanti),
                rapporto = c.Count(x => x.Stato == StatoRitardo.RapportoDaEmettere),
                subappalto = c.Count(x => x.Stato == StatoRitardo.Subappalto),
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return Json(new { giornoAvviso = true, errore = ex.Message }); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SalvaImpostazioni(List<int>? giorni, int sogliaSubappalto, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        var imp = new ImpostazioniRitardi
        {
            GiorniAvviso = (giorni ?? new()).Where(g => g is >= 0 and <= 6).Distinct().OrderBy(g => g).ToList(),
            SogliaSubappalto = Math.Clamp(sogliaSubappalto, 0, 365)
        };
        foreach (var f in Enum.GetValues<FamigliaNormativa>())
            if (int.TryParse(Request.Form[$"soglia_{f}"], out var s)) imp.Soglie[f.ToString()] = Math.Clamp(s, 0, 365);
        await _servizio.SalvaImpostazioniAsync(imp, _utente.IdUtente, ct);
        _cache.Remove(ChiaveCache);
        await _audit.RegistraAsync(AreaFunzionale.Validazione, "ritardi.impostazioni", EsitoOperazione.Successo, dettaglio: imp, ct: ct);
        TempData["Messaggio"] = "Impostazioni salvate.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Nascondi(int id, string numero, string? motivo, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        if (string.IsNullOrWhiteSpace(motivo)) { TempData["Errore"] = "Scrivi il motivo per cui il campione non va più mostrato."; return RedirectToAction(nameof(Index)); }
        await _servizio.NascondiAsync(id, numero ?? id.ToString(), motivo.Trim(), _utente.Email ?? "?", ct);
        _cache.Remove(ChiaveCache);
        await _audit.RegistraAsync(AreaFunzionale.Validazione, "ritardi.nascondi", EsitoOperazione.Successo, tipoEntita: "Campione",
            idEntita: id.ToString(), dettaglio: new { numero, motivo }, ct: ct);
        TempData["Messaggio"] = $"Campione {numero} tolto dall'elenco. Lo ritrovi in «Campioni non mostrati».";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Ripristina(int id, CancellationToken ct)
    {
        if (!PuoGestire) return Forbid();
        await _servizio.RipristinaAsync(id, _utente.Email ?? "?", ct);
        _cache.Remove(ChiaveCache);
        await _audit.RegistraAsync(AreaFunzionale.Validazione, "ritardi.ripristina", EsitoOperazione.Successo, idEntita: id.ToString(), ct: ct);
        TempData["Messaggio"] = "Il campione torna nell'elenco (se è ancora in ritardo).";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Esporta(CancellationToken ct)
    {
        var (c, il) = await CampioniAsync(false, ct);
        using var libro = new XLWorkbook();
        var ws = libro.AddWorksheet("Campioni in ritardo");
        string[] h = ["Stato", "Campione", "Cliente", "Matrice", "Tipo", "Controllo", "Ricezione", "Ritardo programmato (gg)", "Inizio", "Giorni dall'inizio", "Soglia", "Giorni di ritardo", "Prove mancanti"];
        for (var i = 0; i < h.Length; i++) ws.Cell(1, i + 1).Value = h[i];
        ws.Row(1).Style.Font.Bold = true;
        var r = 2;
        foreach (var x in c)
        {
            ws.Cell(r, 1).Value = NomeStato(x.Stato); ws.Cell(r, 2).Value = x.NumeroCampione; ws.Cell(r, 3).Value = x.RagioneSociale;
            ws.Cell(r, 4).Value = x.Matrice; ws.Cell(r, 5).Value = NomeFamiglia(x.Famiglia); ws.Cell(r, 6).Value = x.Controllo;
            ws.Cell(r, 7).Value = x.DataRicezione; ws.Cell(r, 8).Value = x.RitardoProgrammato; ws.Cell(r, 9).Value = x.DataInizio;
            ws.Cell(r, 10).Value = x.GiorniTrascorsi; ws.Cell(r, 11).Value = x.Soglia; ws.Cell(r, 12).Value = x.GiorniRitardo;
            ws.Cell(r, 13).Value = string.Join("; ", x.ProveMancanti); r++;
        }
        ws.Column(7).Style.DateFormat.Format = "dd/MM/yyyy"; ws.Column(9).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Columns(1, 12).AdjustToContents();
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Campioni_in_ritardo_{il:yyyyMMdd}.xlsx");
    }

    public static string NomeStato(StatoRitardo s) => s switch
    {
        StatoRitardo.ParametriMancanti => "Parametri mancanti",
        StatoRitardo.RapportoDaEmettere => "Rapporto di prova da emettere",
        _ => "In attesa del subappalto",
    };

    public static string NomeFamiglia(FamigliaNormativa f) => f switch
    {
        FamigliaNormativa.Legionella => "Legionella",
        FamigliaNormativa.AcqueConsumoUmano => "Acque destinate al consumo umano",
        FamigliaNormativa.Molluschi => "Molluschi vivi",
        FamigliaNormativa.Alimenti => "Alimenti",
        FamigliaNormativa.Piscine => "Piscine",
        FamigliaNormativa.Scarichi => "Scarichi e reflui",
        FamigliaNormativa.SuperficiAria => "Superfici e aria",
        _ => "Altro",
    };
}
