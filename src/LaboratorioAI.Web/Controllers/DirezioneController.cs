using ClosedXML.Excel;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.Provvigioni;
using LaboratorioAI.IA;
using LaboratorioAI.Dati.Direzione;
using System.Text.Json;
using LaboratorioAI.Core.Entita;
using Microsoft.Extensions.Options;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Cruscotto Direzione (13/10/2026), riservato al ruolo Direzione (soci e direzione di laboratorio).
/// Fase 1: indicatori economici di MAREA e delle aree dalla contabilità analitica di AZW (stessa fonte e stesse regole di
/// Power BI) e classificazione delle voci. Legge solo il database di LabAI (AZW è importato ogni notte, in sola lettura).
/// </summary>
[RichiedePermesso(Permessi.DirezioneConsulta)]
[Route("Direzione")]
public class DirezioneController : Controller
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;
    private readonly ImportatoreAzw _importatore;
    private readonly ServizioProvvigioniLab _provvigioni;
    private readonly IClientAnthropic _ia;
    private readonly OpzioniAnthropic _opzioniIa;
    private readonly ServizioRendimentoCommerciali _rendimento;

    public DirezioneController(ContestoLaboratorioAI ctx, IContestoUtente utente, IServizioAudit audit,
        ImportatoreAzw importatore, ServizioProvvigioniLab provvigioni, IClientAnthropic ia, IOptions<OpzioniAnthropic> opzioniIa,
        ServizioRendimentoCommerciali rendimento)
    {
        _rendimento = rendimento;
        _ia = ia;
        _opzioniIa = opzioniIa.Value;
        _ctx = ctx;
        _utente = utente;
        _audit = audit;
        _importatore = importatore;
        _provvigioni = provvigioni;
    }

    /// <summary>
    /// «Aggiorna da AZW» (13/10/2026): la stessa importazione notturna (e di «Importa AZW ora» in Clienti da seguire),
    /// lanciata dalla Direzione. Legge l'ultima copia del file AZW: se Copia.bat non è ancora girato, i dati restano quelli.
    /// </summary>
    [HttpPost("/api/direzione/importa-azw")]
    [RichiedePermesso(Permessi.DirezioneGestisci)]
    public async Task<IActionResult> ImportaAzw(CancellationToken ct)
    {
        var esito = await _importatore.ImportaAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Provvigioni, "provvigioni.importazione.azw",
            esito.Riuscita ? EsitoOperazione.Successo : EsitoOperazione.Errore,
            dettaglio: new { esito.Fatture, esito.Incassi, esito.NoteCredito, esito.Messaggio, Da = "Direzione" }, ct: ct);
        if (esito.Riuscita) await _provvigioni.SincronizzaConvenzioniAsync(ct);
        return esito.Riuscita
            ? Json(new { messaggio = "Importazione AZW completata" + (esito.Messaggio is null ? "." : ": " + esito.Messaggio) })
            : BadRequest(new { messaggio = "Importazione AZW non riuscita: " + esito.Messaggio });
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Titolo"] = "Indicatori economici";
        ViewData["PuoModificare"] = _utente.HaPermesso(Permessi.DirezioneGestisci);
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.indicatori.consulta", EsitoOperazione.Successo, ct: ct);
        return View("~/Views/Direzione/Index.cshtml");
    }

    [HttpGet("Voci")]
    public IActionResult Voci()
    {
        ViewData["Titolo"] = "Classificazione voci";
        ViewData["PuoModificare"] = _utente.HaPermesso(Permessi.DirezioneGestisci);
        return View("~/Views/Direzione/Voci.cshtml");
    }

    // ── Indicatori ──────────────────────────────────────────────────
    [HttpGet("/api/direzione/indicatori")]
    public async Task<IActionResult> Indicatori([FromQuery] string? centro, [FromQuery] int? dal, [FromQuery] int? al, CancellationToken ct)
    {
        var (anni, area, righe, voci) = await DatiAsync(centro, dal, al, ct);
        var risultati = MotoreIndicatoriDirezione.Calcola(righe, voci, anni, area);
        var centri = await _ctx.CentriCostoAzw.AsNoTracking().OrderBy(c => c.Codice)
            .Select(c => new { c.Codice, c.Descrizione }).ToListAsync(ct);
        var daVerificare = await _ctx.ClassificazioniVoci.CountAsync(v => v.DaVerificare, ct);
        var vociUsate = righe.Select(r => r.Voce).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var senzaTipo = voci.Where(v => !v.Ricavo && v.TipoCosto is null && vociUsate.Contains(v.Codice)).Select(v => v.Codice).ToList();
        return Json(new
        {
            aggiornamento = await AggiornamentoAsync(ct),
            centri,
            centro = area,
            anni,
            risultati,
            vociDaVerificare = daVerificare,
            vociSenzaTipo = senzaTipo
        });
    }

    [HttpGet("/api/direzione/indicatori/excel")]
    public async Task<IActionResult> IndicatoriExcel([FromQuery] string? centro, [FromQuery] int? dal, [FromQuery] int? al, CancellationToken ct)
    {
        var (anni, area, righe, voci) = await DatiAsync(centro, dal, al, ct);
        var risultati = MotoreIndicatoriDirezione.Calcola(righe, voci, anni, area);
        var nomeArea = area is null ? "MAREA"
            : (await _ctx.CentriCostoAzw.AsNoTracking().Where(c => c.Codice == area).Select(c => c.Descrizione).FirstOrDefaultAsync(ct)) ?? area;

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Indicatori");
        ws.Cell(1, 1).Value = $"Indicatori economici — {nomeArea}";
        ws.Cell(2, 1).Value = "Voce";
        for (int i = 0; i < risultati.Count; i++) ws.Cell(2, i + 2).Value = risultati[i].Anno;
        int riga = 3;
        foreach (var (nome, sel, pct) in RigheIndicatori(area is not null))
        {
            ws.Cell(riga, 1).Value = nome;
            for (int i = 0; i < risultati.Count; i++)
            {
                var v = sel(risultati[i]);
                var c = ws.Cell(riga, i + 2);
                if (v is decimal d) { c.Value = d; c.Style.NumberFormat.Format = pct ? "0.00%" : "#,##0.00 €"; }
            }
            riga++;
        }
        ws.Row(2).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.indicatori.excel", EsitoOperazione.Successo, dettaglio: new { centro = area }, ct: ct);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Indicatori_{nomeArea.Replace(' ', '_')}_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    private static IEnumerable<(string Nome, Func<IndicatoriAnno, decimal?> Valore, bool Percento)> RigheIndicatori(bool area)
    {
        yield return ("Fatturato", r => r.Fatturato, false);
        yield return (area ? "Totale spese fisse (con 1/5 Amministrazione)" : "Totale spese fisse", r => r.SpeseFisse, false);
        if (area) yield return ("  di cui quota Amministrazione (1/5)", r => r.QuotaAmministrazione, false);
        yield return ("Totale spese variabili", r => r.SpeseVariabili, false);
        yield return ("Costo materie prime", r => r.MateriePrime, false);
        yield return ("Margine di contribuzione", r => r.MargineContribuzione, false);
        yield return ("Incidenza costi variabili su fatturato", r => r.IncidenzaVariabili, true);
        yield return ("Personale", r => r.Personale, false);
        yield return ("Incidenza personale su fatturato", r => r.IncidenzaPersonale, true);
        yield return ("Valore aggiunto", r => r.ValoreAggiunto, false);
        yield return ("EBITDA (MOL)", r => r.Ebitda, false);
        yield return ("Ammortamenti e accantonamenti", r => r.Ammortamenti, false);
        yield return ("EBIT (reddito operativo)", r => r.Ebit, false);
        yield return ("ROS", r => r.Ros, true);
        yield return ("Imposte (IRES + IRAP)", r => r.Imposte, false);
        yield return ("Utile ante imposte", r => r.UtileAnteImposte, false);
        yield return ("Marginalità lorda %", r => r.MarginalitaPercento, true);
        yield return ("Utile netto (di bilancio)", r => r.UtileNetto, false);
        yield return ("Fatturato di pareggio (BEP)", r => r.FatturatoPareggio, false);
        yield return ("% fatturato di pareggio", r => r.PareggioPercento, true);
        yield return ("Costi non classificati", r => r.CostiNonClassificati, false);
    }

    private async Task<(int[] Anni, string? Area, List<MovimentoIndicatori> Righe, List<VoceIndicatori> Voci)> DatiAsync(
        string? centro, int? dal, int? al, CancellationToken ct)
    {
        var area = string.IsNullOrWhiteSpace(centro) ? null : centro.Trim();
        var annoMax = DateTime.Today.Year;
        var a2 = Math.Clamp(al ?? annoMax, 2000, annoMax);
        var a1 = Math.Clamp(dal ?? a2 - 6, 2000, a2);
        var anni = Enumerable.Range(a1, a2 - a1 + 1).ToArray();
        // Tutti i centri servono anche per un'area (quota di 1/5 dell'Amministrazione).
        var righe = await _ctx.MovimentiAnalitici.AsNoTracking()
            .Where(m => m.Anno >= a1 && m.Anno <= a2)
            .GroupBy(m => new { m.Anno, m.Centro, m.Voce, m.Conto })
            .Select(g => new { g.Key.Anno, g.Key.Centro, g.Key.Voce, g.Key.Conto, Importo = g.Sum(x => x.Importo) })
            .ToListAsync(ct);
        var voci = await _ctx.ClassificazioniVoci.AsNoTracking()
            .Select(v => new VoceIndicatori(v.CodiceVoce, v.Ricavo, v.TipoCosto, v.Macrocategoria, v.MateriaPrima)).ToListAsync(ct);
        return (anni, area, righe.Select(r => new MovimentoIndicatori(r.Anno, r.Centro, r.Voce, r.Conto, r.Importo)).ToList(), voci);
    }

    private async Task<object?> AggiornamentoAsync(CancellationToken ct)
    {
        var ultima = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).Select(i => new { i.DataOra, i.DataModificaFile, i.Messaggio }).FirstOrDefaultAsync(ct);
        if (ultima is null) return null;
        var file = ultima.DataModificaFile is DateTime f ? DateTime.SpecifyKind(f, DateTimeKind.Utc) : (DateTime?)null;
        // Avviso se il file AZW ha più di un giorno lavorativo (Copia.bat gira ogni notte sul PC di Mirko).
        var vecchio = file is DateTime ff && GiorniLavorativiDa(ff.ToLocalTime().Date, DateTime.Today) > 1;
        return new { importazione = DateTime.SpecifyKind(ultima.DataOra, DateTimeKind.Utc), file, vecchio, messaggio = ultima.Messaggio };
    }

    private static int GiorniLavorativiDa(DateTime da, DateTime a)
    {
        int n = 0;
        for (var d = da.AddDays(1); d <= a; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) n++;
        return n;
    }

    // ── Andamento territorio (fase 2, 13/10/2026) ───────────────────
    [HttpGet("Territorio")]
    public async Task<IActionResult> Territorio(CancellationToken ct)
    {
        ViewData["Titolo"] = "Andamento territorio";
        ViewData["PuoModificare"] = _utente.HaPermesso(Permessi.DirezioneGestisci);
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.territorio.consulta", EsitoOperazione.Successo, ct: ct);
        return View("~/Views/Direzione/Territorio.cshtml");
    }

    public record FiltriTerritorio(int? Dal, int? Al, string? Centro, string? Voce, string? Commessa, string? Zona,
        int? Commerciale, string? Livello, string? Regione, string? Provincia);

    /// <summary>Anno di apertura della sede di Pesaro (Mirko Maffei, 13/10/2026).</summary>
    private const int AnnoAperturaPesaro = 2022;

    [HttpGet("/api/direzione/territorio")]
    public async Task<IActionResult> DatiTerritorio([FromQuery] FiltriTerritorio f, CancellationToken ct)
    {
        var r = await CalcolaTerritorioAsync(f, ct);
        return Json(new
        {
            aggiornamento = await AggiornamentoAsync(ct),
            filtri = await ElenchiFiltriAsync(ct),
            r.Anni, livello = r.Livello.ToString(), r.Zone, r.Totale, r.Pesaro,
            infoPesaro = new { annoApertura = AnnoAperturaPesaro, annoInizio = r.AnnoInizioPesaro, ultimoAnno = r.UltimoAnnoCompleto }
        });
    }

    [HttpGet("/api/direzione/territorio/excel")]
    public async Task<IActionResult> TerritorioExcel([FromQuery] FiltriTerritorio f, CancellationToken ct)
    {
        var r = await CalcolaTerritorioAsync(f, ct);
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Territorio");
        ws.Cell(1, 1).Value = r.Livello.ToString();
        ws.Cell(1, 2).Value = "Regione";
        for (int i = 0; i < r.Anni.Length; i++)
        {
            ws.Cell(1, 3 + i).Value = $"Fatturato {r.Anni[i]}";
            ws.Cell(1, 3 + r.Anni.Length + i).Value = $"Clienti {r.Anni[i]}";
        }
        int riga = 2;
        foreach (var z in r.Zone)
        {
            ws.Cell(riga, 1).Value = z.Etichetta;
            ws.Cell(riga, 2).Value = z.Regione ?? "";
            for (int i = 0; i < z.Anni.Count; i++)
            {
                ws.Cell(riga, 3 + i).Value = z.Anni[i].Fatturato;
                ws.Cell(riga, 3 + i).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(riga, 3 + r.Anni.Length + i).Value = z.Anni[i].ClientiAttivi;
            }
            riga++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.territorio.excel", EsitoOperazione.Successo, dettaglio: f, ct: ct);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Territorio_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    private sealed record RisultatoTerritorio(int[] Anni, LivelloTerritorio Livello, IReadOnlyList<ZonaTerritorio> Zone,
        IReadOnlyList<ValoriZonaAnno> Totale, IReadOnlyList<GruppoConfronto> Pesaro, int AnnoInizioPesaro, int UltimoAnnoCompleto,
        List<FatturatoClienteAnno> RigheNelTerritorio, Dictionary<string, ClienteTerritorio> Clienti, Dictionary<string, string> Acquisiti);

    /// <summary>
    /// Clienti tolti dalla lista dei clienti da seguire perché acquisiti da un nostro cliente (14/10/2026): codice → codice
    /// del cliente che li ha acquisiti. Nel territorio escono come «acquisiti», non fra i persi.
    /// </summary>
    private async Task<Dictionary<string, string>> AcquisitiAsync(CancellationToken ct) =>
        (await _ctx.ClientiTolti.AsNoTracking()
            .Where(t => !t.Annullata && t.Motivo == MotivoUscitaCliente.AcquisitoDaCliente && t.CodiceClienteSuccessore != null)
            .Select(t => new { t.CodiceCliente, t.CodiceClienteSuccessore }).ToListAsync(ct))
        .GroupBy(t => t.CodiceCliente.Trim()).ToDictionary(g => g.Key, g => g.First().CodiceClienteSuccessore!.Trim());

    private sealed record ClienteTerritorio(string? RagioneSociale, string? Provincia, string? Citta, string? Zona, string? CodiceVenditore);

    private async Task<RisultatoTerritorio> CalcolaTerritorioAsync(FiltriTerritorio f, CancellationToken ct)
    {
        var annoMax = DateTime.Today.Year;
        var a2 = Math.Clamp(f.Al ?? annoMax, 2018, annoMax);
        var a1 = Math.Clamp(f.Dal ?? a2 - 7, 2018, a2);
        var anni = Enumerable.Range(a1, a2 - a1 + 1).ToArray();
        var livello = Enum.TryParse<LivelloTerritorio>(f.Livello, true, out var lv) ? lv : LivelloTerritorio.Provincia;
        const int annoInizioPesaro = 2018;
        var ultimoCompleto = annoMax - 1;
        var daAnno = Math.Min(a1, annoInizioPesaro) - 1;   // l'anno prima serve per nuovi e persi

        var clienti = await ClientiTerritorioAsync(ct);
        var sedi = clienti.ToDictionary(c => c.Key, c => new SedeCliente(c.Value.Provincia, c.Value.Citta));
        var righe = await RigheFiltrateAsync(f, clienti, daAnno, a2, null, ct);

        var acquisiti = await AcquisitiAsync(ct);
        var setAcquisiti = acquisiti.Keys.ToHashSet();
        var pesaro = MotoreTerritorio.ConfrontoPesaro(righe, sedi, annoInizioPesaro, AnnoAperturaPesaro, ultimoCompleto, setAcquisiti);
        var nelTerritorio = NelTerritorio(righe, sedi, f);
        var zone = MotoreTerritorio.Calcola(nelTerritorio, sedi, anni, livello, setAcquisiti);
        var totale = MotoreTerritorio.Totale(nelTerritorio, anni, setAcquisiti);
        return new RisultatoTerritorio(anni, livello, zone, totale, pesaro, annoInizioPesaro, ultimoCompleto, nelTerritorio, clienti, acquisiti);
    }

    private async Task<Dictionary<string, ClienteTerritorio>> ClientiTerritorioAsync(CancellationToken ct) =>
        (await _ctx.AzwClienti.AsNoTracking()
            .Select(c => new { c.CodiceCliente, c.RagioneSociale, c.Provincia, c.Citta, c.Zona, c.CodiceVenditore }).ToListAsync(ct))
        .GroupBy(c => c.CodiceCliente.Trim())
        .ToDictionary(g => g.Key, g => { var c = g.First(); return new ClienteTerritorio(c.RagioneSociale, c.Provincia, c.Citta, c.Zona, c.CodiceVenditore); });

    /// <summary>
    /// Fatturato per cliente e anno con i filtri di area, voce, commessa, zona e commerciale (non quelli territoriali).
    /// Con <paramref name="meseMassimo"/> si sommano solo i mesi fino a quello (progressivo dell'anno).
    /// </summary>
    private async Task<List<FatturatoClienteAnno>> RigheFiltrateAsync(FiltriTerritorio f, Dictionary<string, ClienteTerritorio> clienti,
        int daAnno, int aAnno, int? meseMassimo, CancellationToken ct)
    {
        var q = _ctx.FatturatoClienti.AsNoTracking().Where(x => x.Anno >= daAnno && x.Anno <= aAnno);
        if (meseMassimo is int mm) q = q.Where(x => x.Mese <= mm);
        if (!string.IsNullOrWhiteSpace(f.Centro)) q = q.Where(x => x.Centro == f.Centro);
        if (!string.IsNullOrWhiteSpace(f.Voce)) q = q.Where(x => x.Voce == f.Voce);
        if (!string.IsNullOrWhiteSpace(f.Commessa)) q = q.Where(x => x.Commessa == f.Commessa);
        var righe = (await q.GroupBy(x => new { x.Anno, x.CodiceCliente })
                .Select(g => new { g.Key.Anno, g.Key.CodiceCliente, Importo = g.Sum(x => x.Importo) }).ToListAsync(ct))
            .Select(x => new FatturatoClienteAnno(x.Anno, x.CodiceCliente, x.Importo)).ToList();

        // Filtri sul cliente: zona (responsabile), commerciale (codice venditore attuale, senza storico).
        if (!string.IsNullOrWhiteSpace(f.Zona) || f.Commerciale is not null)
        {
            var codici = f.Commerciale is int idc
                ? (await _ctx.CodiciVenditore.AsNoTracking().Where(c => c.IdCommerciale == idc).Select(c => c.CodiceVenditore).ToListAsync(ct))
                    .Select(c => c.Trim().ToUpperInvariant()).ToHashSet()
                : null;
            var ammessi = clienti.Where(c =>
                    (string.IsNullOrWhiteSpace(f.Zona) || string.Equals(c.Value.Zona, f.Zona, StringComparison.OrdinalIgnoreCase))
                    && (codici is null || (c.Value.CodiceVenditore is string v && codici.Contains(v.ToUpperInvariant()))))
                .Select(c => c.Key).ToHashSet();
            righe = righe.Where(r => ammessi.Contains(r.CodiceCliente.Trim())).ToList();
        }
        return righe;
    }

    /// <summary>Restringimento territoriale (regione o provincia) per l'elenco, il totale e i clienti.</summary>
    private static List<FatturatoClienteAnno> NelTerritorio(List<FatturatoClienteAnno> righe, Dictionary<string, SedeCliente> sedi, FiltriTerritorio f)
    {
        if (!string.IsNullOrWhiteSpace(f.Provincia))
            return righe.Where(r => sedi.TryGetValue(r.CodiceCliente.Trim(), out var s) && ProvinceItaliane.Sigla(s.Provincia) == ProvinceItaliane.Sigla(f.Provincia)).ToList();
        if (!string.IsNullOrWhiteSpace(f.Regione))
            return righe.Where(r => r.CodiceCliente.Length > 0 && sedi.TryGetValue(r.CodiceCliente.Trim(), out var s)
                && ProvinceItaliane.Regione(s.Provincia) == f.Regione).ToList();
        return righe;
    }

    private async Task<object> ElenchiFiltriAsync(CancellationToken ct)
    {
        var centri = await _ctx.CentriCostoAzw.AsNoTracking().OrderBy(c => c.Codice).Select(c => new { c.Codice, c.Descrizione }).ToListAsync(ct);
        var voci = await _ctx.ClassificazioniVoci.AsNoTracking().Where(v => v.Ricavo).OrderBy(v => v.CodiceVoce)
            .Select(v => new { Codice = v.CodiceVoce, v.Descrizione }).ToListAsync(ct);
        var commesse = await _ctx.CommesseAzw.AsNoTracking().OrderBy(c => c.Descrizione).Select(c => new { c.Codice, c.Descrizione }).ToListAsync(ct);
        var nomiZone = await _ctx.ResponsabiliCliente.AsNoTracking().ToDictionaryAsync(r => r.Sigla.ToUpper(), r => r.Nome, ct);
        var zone = (await _ctx.AzwClienti.AsNoTracking().Where(c => c.Zona != null && c.Zona != "").Select(c => c.Zona!).Distinct().ToListAsync(ct))
            .OrderBy(z => z).Select(z => new { Codice = z, Descrizione = nomiZone.TryGetValue(z.ToUpperInvariant(), out var n) ? n : null });
        var commerciali = await _ctx.Commerciali.AsNoTracking().OrderBy(c => c.Nome).Select(c => new { c.IdCommerciale, c.Nome }).ToListAsync(ct);
        var regioni = ProvinceItaliane.Regioni().Select(r => new { nome = r.Regione, province = r.Province });
        return new { centri, voci, commesse, zone, commerciali, regioni };
    }

    // ── Rendimento commerciali (fase 3, 13/10/2026) ─────────────────
    [HttpGet("Commerciali")]
    public async Task<IActionResult> Commerciali(CancellationToken ct)
    {
        ViewData["Titolo"] = "Rendimento commerciali";
        ViewData["PuoModificare"] = _utente.HaPermesso(Permessi.DirezioneGestisci);
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.commerciali.consulta", EsitoOperazione.Successo, ct: ct);
        return View("~/Views/Direzione/Commerciali.cshtml");
    }

    public record FiltriCommerciali(int? Commerciale, int? Dal, int? Al);

    [HttpGet("/api/direzione/commerciali")]
    public async Task<IActionResult> DatiCommerciali([FromQuery] FiltriCommerciali f, CancellationToken ct)
    {
        var r = await _rendimento.CalcolaAsync(f.Commerciale, f.Dal, f.Al, ct);
        var elenco = await _ctx.Commerciali.AsNoTracking().Where(c => c.Attivo).OrderBy(c => c.Nome).Select(c => new { c.IdCommerciale, c.Nome }).ToListAsync(ct);
        return Json(new
        {
            aggiornamento = await AggiornamentoAsync(ct),
            elenco,
            anni = r.Anni,
            annoRiferimento = r.AnnoRiferimento,
            commerciali = r.Commerciali.Select(VistaRendimento)
        });
    }

    private static object VistaRendimento(RendimentoCommerciale c) => new
    {
        c.IdCommerciale, c.Nome, c.VoceCosto, c.DescrizioneVoceCosto, c.AnnoInizio, c.Avvisi,
        aree = c.Aree.Select(a => new { centro = a.Centro, peso = a.Peso }),
        anni = c.Anni.Select(a => new
        {
            a.Anno, a.CostoStipendio, a.Provvigioni, a.CostoTotale, a.Portato, a.PortatoNuovi, a.ClientiPortati,
            a.Seguito, a.ClientiSeguiti, a.ClientiSeguitiNuovi, a.ClientiSeguitiPersi, a.ClientiSeguitiAcquisiti,
            a.IncidenzaSuPortato, a.IncidenzaSuSeguito, a.PortatoPerEuro, a.MargineContribuzionePercento, a.MarginePortato, a.Contributo,
            a.PercentualeIncassata
        }),
        cumulato = c.ContributoCumulato.Select(x => new { anno = x.Anno, valore = x.Cumulato }),
        clienti = c.ClientiPortati.Take(RiassuntoDirezioneAi.ClientiPerElenco)
            .Select(x => new { x.Codice, x.Nome, x.Provincia, x.AnnoPrima, x.AnnoRiferimento }),
        altriClienti = Math.Max(0, c.ClientiPortati.Count - RiassuntoDirezioneAi.ClientiPerElenco)
    };

    [HttpGet("/api/direzione/commerciali/excel")]
    public async Task<IActionResult> CommercialiExcel([FromQuery] FiltriCommerciali f, CancellationToken ct)
    {
        var r = await _rendimento.CalcolaAsync(f.Commerciale, f.Dal, f.Al, ct);
        using var wb = new XLWorkbook();
        foreach (var c in r.Commerciali)
        {
            var nome = new string(c.Nome.Where(ch => !"[]*?/\\:".Contains(ch)).ToArray());
            var ws = wb.AddWorksheet(nome.Length > 31 ? nome[..31] : nome);
            ws.Cell(1, 1).Value = $"Rendimento — {c.Nome}" + (c.VoceCosto is null ? "" : $" (voce costo {c.VoceCosto})");
            ws.Cell(2, 1).Value = "Voce";
            for (int i = 0; i < c.Anni.Count; i++) ws.Cell(2, i + 2).Value = c.Anni[i].Anno;
            int riga = 3;
            foreach (var (titolo, sel, tipo) in RigheRendimento())
            {
                ws.Cell(riga, 1).Value = titolo;
                for (int i = 0; i < c.Anni.Count; i++)
                    if (sel(c.Anni[i]) is decimal v)
                    {
                        var cell = ws.Cell(riga, i + 2);
                        cell.Value = v;
                        cell.Style.NumberFormat.Format = tipo switch { 1 => "0.0%", 2 => "0", 3 => "0.00", _ => "#,##0 €" };
                    }
                riga++;
            }
            riga++;
            ws.Cell(riga++, 1).Value = $"Clienti portati (base provvigioni {r.AnnoRiferimento - 1} e {r.AnnoRiferimento})";
            foreach (var cl in c.ClientiPortati)
            {
                ws.Cell(riga, 1).Value = cl.Nome; ws.Cell(riga, 2).Value = cl.Provincia ?? "";
                ws.Cell(riga, 3).Value = cl.AnnoPrima; ws.Cell(riga, 4).Value = cl.AnnoRiferimento;
                ws.Range(riga, 3, riga, 4).Style.NumberFormat.Format = "#,##0 €";
                riga++;
            }
            ws.Row(2).Style.Font.Bold = true;
            ws.Columns().AdjustToContents();
        }
        if (r.Commerciali.Count == 0) wb.AddWorksheet("Vuoto");
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.commerciali.excel", EsitoOperazione.Successo, dettaglio: f, ct: ct);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"RendimentoCommerciali_{DateTime.Today:yyyyMMdd}.xlsx");
    }

    /// <summary>Righe della tabella: tipo 0 = euro, 1 = percentuale, 2 = numero, 3 = rapporto.</summary>
    private static IEnumerable<(string, Func<RendimentoAnno, decimal?>, int)> RigheRendimento()
    {
        yield return ("Costo stipendio (voce AZW)", a => a.CostoStipendio, 0);
        yield return ("Provvigioni", a => a.Provvigioni, 0);
        yield return ("Costo totale", a => a.CostoTotale, 0);
        yield return ("Portato (base provvigioni)", a => a.Portato, 0);
        yield return ("  di cui clienti nuovi", a => a.PortatoNuovi, 0);
        yield return ("Clienti portati", a => a.ClientiPortati, 2);
        yield return ("Seguito (clienti con il suo codice venditore)", a => a.Seguito, 0);
        yield return ("Clienti seguiti attivi", a => a.ClientiSeguiti, 2);
        yield return ("  nuovi", a => a.ClientiSeguitiNuovi, 2);
        yield return ("  persi", a => a.ClientiSeguitiPersi, 2);
        yield return ("  acquisiti da un nostro cliente", a => a.ClientiSeguitiAcquisiti, 2);
        yield return ("Incidenza costo su portato", a => a.IncidenzaSuPortato, 1);
        yield return ("Incidenza costo su seguito", a => a.IncidenzaSuSeguito, 1);
        yield return ("€ portati per € di costo", a => a.PortatoPerEuro, 3);
        yield return ("Margine di contribuzione % aree", a => a.MargineContribuzionePercento, 1);
        yield return ("Margine portato", a => a.MarginePortato, 0);
        yield return ("Contributo (margine portato − costo)", a => a.Contributo, 0);
        yield return ("Incassato sulle fatture dell'anno (seguiti)", a => a.PercentualeIncassata, 1);
    }

    [HttpPost("/api/direzione/commento-ai/commerciali")]
    public async Task<IActionResult> CommentoCommerciali([FromBody] FiltriCommerciali f, CancellationToken ct)
    {
        var r = await _rendimento.CalcolaAsync(f.Commerciale, f.Dal, f.Al, ct);
        var descrizione = $"anni {r.Anni[0]}–{r.Anni[^1]}, " + (r.Commerciali.Count == 1 ? $"commerciale {r.Commerciali[0].Nome}" : "tutti i commerciali attivi");
        var dati = RiassuntoDirezioneAi.Commerciali(descrizione, await TestoAggiornamentoAsync(ct), r.Anni, r.AnnoRiferimento, r.Commerciali);
        return await GeneraCommentoAsync("commerciali", f, descrizione, dati, ct);
    }

    // ── Commento AI (13/10/2026) ─────────────────────────────────────
    // Il programma calcola i numeri, l'AI li commenta. Ogni commento si conserva con i filtri e i dati inviati.
    // Con i nomi dei primi 30 clienti per elenco (Mirko Maffei, 13/10/2026).

    [HttpPost("/api/direzione/commento-ai/territorio")]
    public async Task<IActionResult> CommentoTerritorio([FromBody] FiltriTerritorio f, CancellationToken ct)
    {
        var r = await CalcolaTerritorioAsync(f, ct);
        var annoMax = DateTime.Today.Year;
        var annoRif = Math.Min(r.Anni[^1], annoMax - 1);
        var sedi = r.Clienti.ToDictionary(c => c.Key, c => new SedeCliente(c.Value.Provincia, c.Value.Citta));

        // Clienti: anno di riferimento e anno prima, nel territorio scelto.
        var perCliente = r.RigheNelTerritorio.Where(x => x.CodiceCliente.Trim().Length > 0 && (x.Anno == annoRif || x.Anno == annoRif - 1))
            .GroupBy(x => x.CodiceCliente.Trim())
            .Select(g =>
            {
                r.Clienti.TryGetValue(g.Key, out var c);
                return new ClienteConfronto(g.Key, (c?.RagioneSociale ?? "").Trim() is { Length: > 0 } n ? n : "Cliente " + g.Key,
                    ProvinceItaliane.Esiste(ProvinceItaliane.Sigla(c?.Provincia)) ? ProvinceItaliane.Sigla(c?.Provincia) : null,
                    g.Where(x => x.Anno == annoRif - 1).Sum(x => x.Importo), g.Where(x => x.Anno == annoRif).Sum(x => x.Importo));
            }).ToList();

        // Progressivo dell'anno in corso (solo se il periodo arriva all'anno in corso).
        ProgressivoAnno? progressivo = null;
        if (r.Anni[^1] == annoMax && await _ctx.FatturatoClienti.AnyAsync(x => x.Anno == annoMax, ct))
        {
            var mese = await _ctx.FatturatoClienti.Where(x => x.Anno == annoMax).MaxAsync(x => x.Mese, ct);
            var righeProg = NelTerritorio(await RigheFiltrateAsync(f, r.Clienti, annoMax - 1, annoMax, mese, ct), sedi, f);
            progressivo = new ProgressivoAnno(annoMax, mese,
                righeProg.Where(x => x.Anno == annoMax).Sum(x => x.Importo), righeProg.Where(x => x.Anno == annoMax - 1).Sum(x => x.Importo));
        }

        var descrizione = await DescriviFiltriTerritorioAsync(f, r, ct);
        var acquisitiNomi = r.Acquisiti.ToDictionary(x => x.Key, x => r.Clienti.TryGetValue(x.Value, out var cs) && !string.IsNullOrWhiteSpace(cs.RagioneSociale) ? cs.RagioneSociale!.Trim() : "Cliente " + x.Value);
        var dati = RiassuntoDirezioneAi.Territorio(new DatiCommentoTerritorio(descrizione, await TestoAggiornamentoAsync(ct), r.Anni, r.Livello,
            r.Zone, r.Totale, r.Pesaro, AnnoAperturaPesaro, r.AnnoInizioPesaro, r.UltimoAnnoCompleto, annoRif, perCliente, progressivo) { Acquisiti = acquisitiNomi });
        return await GeneraCommentoAsync("territorio", f, descrizione, dati, ct);
    }

    public record FiltriIndicatori(string? Centro, int? Dal, int? Al);

    [HttpPost("/api/direzione/commento-ai/indicatori")]
    public async Task<IActionResult> CommentoIndicatori([FromBody] FiltriIndicatori f, CancellationToken ct)
    {
        var (anni, area, righe, voci) = await DatiAsync(f.Centro, f.Dal, f.Al, ct);
        var risultati = MotoreIndicatoriDirezione.Calcola(righe, voci, anni, area);
        var centri = await _ctx.CentriCostoAzw.AsNoTracking().OrderBy(c => c.Codice).ToListAsync(ct);
        string NomeCentro(string codice) => centri.FirstOrDefault(c => c.Codice == codice)?.Descrizione is { Length: > 0 } d ? $"{d} ({codice})" : codice;
        var nomeArea = area is null ? "MAREA (tutte le aree)" : NomeCentro(area);
        var annoMax = DateTime.Today.Year;

        // Per MAREA: confronto fra le aree nell'ultimo anno completo (Amministrazione esclusa: si ripartisce sulle altre).
        var altre = new List<(string, IndicatoriAnno)>();
        var annoRif = Math.Min(anni[^1], annoMax - 1);
        if (area is null && anni.Contains(annoRif))
            foreach (var c in centri.Where(c => c.Codice != "03"))
            {
                var v = MotoreIndicatoriDirezione.Calcola(righe, voci, [annoRif], c.Codice).FirstOrDefault();
                if (v is not null && v.Fatturato > 0) altre.Add((NomeCentro(c.Codice), v));
            }
        int? mese = anni.Contains(annoMax) && await _ctx.MovimentiAnalitici.AnyAsync(m => m.Anno == annoMax, ct)
            ? await _ctx.MovimentiAnalitici.Where(m => m.Anno == annoMax).MaxAsync(m => m.Mese, ct) : null;

        var descrizione = $"{anni[0]}–{anni[^1]}, {nomeArea}";
        var dati = RiassuntoDirezioneAi.Indicatori(new DatiCommentoIndicatori(descrizione, await TestoAggiornamentoAsync(ct), nomeArea,
            area is not null, risultati, altre.OrderByDescending(a => a.Item2.Fatturato).ToList(), annoMax, mese));
        return await GeneraCommentoAsync("indicatori", f, descrizione, dati, ct);
    }

    private async Task<IActionResult> GeneraCommentoAsync(string pagina, object filtri, string descrizione, string dati, CancellationToken ct)
    {
        RispostaModello risposta;
        try
        {
            risposta = await _ia.InterrogaConSistemaAsync(RiassuntoDirezioneAi.Sistema, dati, 3000, ct);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { messaggio = ex.Message }); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StatusCode(502, new { messaggio = "Servizio AI non raggiungibile: " + ex.Message });
        }

        var costo = (risposta.TokenInput + risposta.TokenCacheScritti * 1.25m + risposta.TokenCacheLetti * 0.1m) / 1_000_000m * _opzioniIa.CostoInputPerMilione
                    + risposta.TokenOutput / 1_000_000m * _opzioniIa.CostoOutputPerMilione;
        var commento = new CommentoAiDirezione
        {
            Pagina = pagina,
            Filtri = Tronca(JsonSerializer.Serialize(filtri), 1000),
            DescrizioneFiltri = Tronca(descrizione, 500),
            DatiInviati = dati,
            Testo = risposta.Testo.Trim(),
            Modello = Tronca(risposta.Modello, 100),
            TokenInput = risposta.TokenInput + risposta.TokenCacheScritti + risposta.TokenCacheLetti,
            TokenOutput = risposta.TokenOutput,
            CostoStimato = Math.Round(costo, 4),
            IdUtente = _utente.IdUtente,
            NomeUtente = _utente.NomeCompleto,
            DataCreazione = DateTime.UtcNow
        };
        _ctx.CommentiAiDirezione.Add(commento);
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.commento-ai", EsitoOperazione.Successo,
            "CommentoAiDirezione", commento.IdCommentoAiDirezione.ToString(), new { pagina, descrizione, commento.CostoStimato }, ct);
        return Json(VistaCommento(commento, completo: true));
    }

    [HttpGet("/api/direzione/commenti-ai")]
    public async Task<IActionResult> ElencoCommenti([FromQuery] string? pagina, CancellationToken ct)
    {
        var q = _ctx.CommentiAiDirezione.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(pagina)) q = q.Where(c => c.Pagina == pagina);
        var elenco = await q.OrderByDescending(c => c.DataCreazione).Take(100).ToListAsync(ct);
        return Json(elenco.Select(c => VistaCommento(c, completo: false)));
    }

    [HttpGet("/api/direzione/commenti-ai/{id:int}")]
    public async Task<IActionResult> Commento(int id, CancellationToken ct)
    {
        var c = await _ctx.CommentiAiDirezione.AsNoTracking().FirstOrDefaultAsync(x => x.IdCommentoAiDirezione == id, ct);
        return c is null ? NotFound(new { messaggio = "Commento non trovato." }) : Json(VistaCommento(c, completo: true));
    }

    /// <summary>Commento come file di testo (con i dati inviati): da allegare a un verbale o a una mail.</summary>
    [HttpGet("/api/direzione/commenti-ai/{id:int}/testo")]
    public async Task<IActionResult> CommentoTesto(int id, CancellationToken ct)
    {
        var c = await _ctx.CommentiAiDirezione.AsNoTracking().FirstOrDefaultAsync(x => x.IdCommentoAiDirezione == id, ct);
        if (c is null) return NotFound();
        var testo = $"Commento AI — {(c.Pagina switch { "territorio" => "Andamento territorio", "commerciali" => "Rendimento commerciali", _ => "Indicatori economici" })}\r\n"
            + $"{c.DataCreazione.ToLocalTime():dd/MM/yyyy HH:mm}, {c.NomeUtente}\r\nFiltri: {c.DescrizioneFiltri}\r\n"
            + "Testo scritto dall'AI sui numeri calcolati da LaboratorioAI: verificare i dati prima di usarlo.\r\n\r\n"
            + c.Testo.Replace("\r\n", "\n").Replace("\n", "\r\n")
            + "\r\n\r\n──────── Dati inviati all'AI ────────\r\n" + c.DatiInviati.Replace("\r\n", "\n").Replace("\n", "\r\n");
        return File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(testo)).ToArray(),
            "text/plain; charset=utf-8", $"CommentoAI_{c.Pagina}_{c.DataCreazione.ToLocalTime():yyyyMMdd_HHmm}.txt");
    }

    private static object VistaCommento(CommentoAiDirezione c, bool completo) => new
    {
        id = c.IdCommentoAiDirezione,
        c.Pagina,
        data = DateTime.SpecifyKind(c.DataCreazione, DateTimeKind.Utc),
        utente = c.NomeUtente,
        descrizione = c.DescrizioneFiltri,
        filtri = c.Filtri,
        costo = c.CostoStimato,
        c.Modello,
        testo = completo ? c.Testo : null,
        anteprima = completo ? null : Anteprima(c.Testo),
        datiInviati = completo ? c.DatiInviati : null
    };

    private static string Anteprima(string testo)
    {
        var righe = testo.Split('\n').Select(r => r.Trim().TrimStart('-', '#', ' ').Replace("**", "")).Where(r => r.Length > 0 && r != "In sintesi");
        var t = string.Join(" ", righe);
        return t.Length > 220 ? t[..220] + "…" : t;
    }

    private static string Tronca(string s, int max) => s.Length <= max ? s : s[..max];

    private async Task<string?> TestoAggiornamentoAsync(CancellationToken ct)
    {
        var ultima = await _ctx.ImportazioniAzw.AsNoTracking().Where(i => i.Riuscita)
            .OrderByDescending(i => i.DataOra).Select(i => new { i.DataOra, i.DataModificaFile }).FirstOrDefaultAsync(ct);
        if (ultima is null) return null;
        var file = ultima.DataModificaFile ?? ultima.DataOra;
        return $"file AZW del {DateTime.SpecifyKind(file, DateTimeKind.Utc).ToLocalTime():dd/MM/yyyy}";
    }

    private async Task<string> DescriviFiltriTerritorioAsync(FiltriTerritorio f, RisultatoTerritorio r, CancellationToken ct)
    {
        var parti = new List<string> { $"anni {r.Anni[0]}–{r.Anni[^1]}", $"dettaglio per {r.Livello.ToString().ToLowerInvariant()}" };
        if (!string.IsNullOrWhiteSpace(f.Provincia)) parti.Add($"provincia {ProvinceItaliane.NomeProvincia(ProvinceItaliane.Sigla(f.Provincia))}");
        else if (!string.IsNullOrWhiteSpace(f.Regione)) parti.Add($"regione {f.Regione}");
        if (!string.IsNullOrWhiteSpace(f.Centro))
            parti.Add("area " + ((await _ctx.CentriCostoAzw.AsNoTracking().Where(c => c.Codice == f.Centro).Select(c => c.Descrizione).FirstOrDefaultAsync(ct)) ?? f.Centro));
        if (!string.IsNullOrWhiteSpace(f.Voce))
            parti.Add("voce " + f.Voce + ((await _ctx.ClassificazioniVoci.AsNoTracking().Where(v => v.CodiceVoce == f.Voce).Select(v => v.Descrizione).FirstOrDefaultAsync(ct)) is { } dv ? $" ({dv})" : ""));
        if (!string.IsNullOrWhiteSpace(f.Commessa))
            parti.Add("chi fa il lavoro " + ((await _ctx.CommesseAzw.AsNoTracking().Where(c => c.Codice == f.Commessa).Select(c => c.Descrizione).FirstOrDefaultAsync(ct)) ?? f.Commessa));
        if (!string.IsNullOrWhiteSpace(f.Zona))
            parti.Add("responsabile cliente " + ((await _ctx.ResponsabiliCliente.AsNoTracking().Where(z => z.Sigla == f.Zona).Select(z => z.Nome).FirstOrDefaultAsync(ct)) ?? f.Zona));
        if (f.Commerciale is int idc)
            parti.Add("commerciale " + ((await _ctx.Commerciali.AsNoTracking().Where(c => c.IdCommerciale == idc).Select(c => c.Nome).FirstOrDefaultAsync(ct)) ?? idc.ToString()));
        if (parti.Count == 2) parti.Add("nessun altro filtro");
        return string.Join(", ", parti);
    }

    // ── Classificazione delle voci ──────────────────────────────────
    [HttpGet("/api/direzione/voci")]
    public async Task<IActionResult> ElencoVoci(CancellationToken ct)
    {
        var annoMin = DateTime.Today.Year - 2;
        var usate = await _ctx.MovimentiAnalitici.AsNoTracking().Where(m => m.Anno >= annoMin)
            .GroupBy(m => m.Voce).Select(g => new { Voce = g.Key, Importo = g.Sum(x => x.Importo) }).ToListAsync(ct);
        var perVoce = usate.ToDictionary(x => x.Voce, x => x.Importo, StringComparer.OrdinalIgnoreCase);
        var voci = await _ctx.ClassificazioniVoci.AsNoTracking().OrderBy(v => v.Ricavo).ThenBy(v => v.CodiceVoce).ToListAsync(ct);
        return Json(new
        {
            macrocategorie = ClassificatoreVociAzw.Macrocategorie,
            voci = voci.Select(v => new
            {
                v.IdClassificazioneVoce, v.CodiceVoce, v.Descrizione, v.Ricavo, v.TipoCosto, v.Macrocategoria, v.MateriaPrima, v.DaVerificare,
                ImportoUltimiAnni = perVoce.TryGetValue(v.CodiceVoce, out var imp) ? imp : 0m
            })
        });
    }

    public record RichiestaVoce(string? TipoCosto, string? Macrocategoria, bool MateriaPrima);

    [HttpPost("/api/direzione/voci/{id:int}")]
    [RichiedePermesso(Permessi.DirezioneGestisci)]
    public async Task<IActionResult> SalvaVoce(int id, [FromBody] RichiestaVoce req, CancellationToken ct)
    {
        var v = await _ctx.ClassificazioniVoci.FirstOrDefaultAsync(x => x.IdClassificazioneVoce == id, ct);
        if (v is null) return NotFound(new { messaggio = "Voce non trovata." });
        if (v.Ricavo) return BadRequest(new { messaggio = "Le voci di ricavo non si classificano." });
        var tipo = req.TipoCosto is ClassificatoreVociAzw.Fisso or ClassificatoreVociAzw.Variabile ? req.TipoCosto : null;
        var macro = ClassificatoreVociAzw.Macrocategorie.FirstOrDefault(m => m == req.Macrocategoria && m != ClassificatoreVociAzw.MacroRicavo);
        if (macro is null) return BadRequest(new { messaggio = "Macrocategoria non valida." });
        var prima = new { v.TipoCosto, v.Macrocategoria, v.MateriaPrima };
        v.TipoCosto = tipo; v.Macrocategoria = macro; v.MateriaPrima = req.MateriaPrima;
        v.DaVerificare = false; v.DataUltimaModifica = DateTime.UtcNow; v.IdUtente = _utente.IdUtente;
        await _ctx.SaveChangesAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "direzione.voce.modifica", EsitoOperazione.Successo,
            "ClassificazioneVoce", v.CodiceVoce, new { prima, dopo = new { tipo, macro, req.MateriaPrima } }, ct);
        return Json(new { messaggio = $"Voce {v.CodiceVoce} salvata." });
    }
}
