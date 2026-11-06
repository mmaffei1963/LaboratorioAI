using ClosedXML.Excel;
using LaboratorioAI.Core.Andamento;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Clienti;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// «Cambi di gestione» (subentri, 12/10/2026): insediamenti che facevano Legionella e non mandano più campioni, con il
/// nuovo cliente trovato nello stesso posto. Commerciali, consulenti e accettazione confermano o scartano gli
/// abbinamenti; gli importi li vede solo chi ha «Clienti da seguire». Scrive solo nel database di LabAI.
/// </summary>
[RichiedeUnoDeiPermessi(Permessi.ClientiSeguire, Permessi.ClientiSubentri)]
[Route("ClientiDaSeguire/Subentri")]
public class SubentriClientiController : Controller
{
    private readonly ServizioSubentri _servizio;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public SubentriClientiController(ServizioSubentri servizio, IContestoUtente utente, IServizioAudit audit)
    {
        _servizio = servizio;
        _utente = utente;
        _audit = audit;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? esito, string? filtro, string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        ViewData["Titolo"] = "Cambi di gestione";
        var r = await _servizio.CalcolaAsync(DateTime.Today, Finestra(periodo, confronto), Ambito(servizio), ct);
        esito = esito is "a" or "b" or "c" or "d" or "e" or "verifica" or "confermati" ? esito : "tutti";
        var righe = Filtra(r, esito, filtro).ToList();
        return View("~/Views/SubentriClienti/Index.cshtml",
            new ModelloSubentri(r, righe, esito, filtro, _utente.HaPermesso(Permessi.ClientiSeguire), Periodo(periodo), Confronto(confronto)));
    }

    [HttpPost("Conferma")]
    public async Task<IActionResult> Conferma(int idPrecedente, int? idSuccessivo, bool manuale, string? note, string? esito, string? filtro, string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        var errore = await _servizio.DecidiAsync(idPrecedente, idSuccessivo, true, manuale, note,
            _utente.IdUtente, _utente.NomeCompleto ?? _utente.Email, ct);
        if (errore is null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, idSuccessivo is null ? "clienti.subentro.nessuno" : "clienti.subentro.conferma",
                EsitoOperazione.Successo, "Insediamento", idPrecedente.ToString(), new { idSuccessivo, manuale, note }, ct);
        TempData["Messaggio"] = errore ?? (idSuccessivo is null ? "Registrato: nessun subentro." : "Cambio di gestione confermato.");
        return RedirectToAction(nameof(Index), new { esito, filtro, periodo = Periodo(periodo), confronto = Confronto(confronto), servizio = (int)Ambito(servizio) });
    }

    [HttpPost("Scarta")]
    public async Task<IActionResult> Scarta(int idPrecedente, int idSuccessivo, string? esito, string? filtro, string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        var errore = await _servizio.DecidiAsync(idPrecedente, idSuccessivo, false, false, null,
            _utente.IdUtente, _utente.NomeCompleto ?? _utente.Email, ct);
        if (errore is null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.subentro.scarta", EsitoOperazione.Successo,
                "Insediamento", idPrecedente.ToString(), new { idSuccessivo }, ct);
        TempData["Messaggio"] = errore ?? "Abbinamento scartato: non verrà più proposto.";
        return RedirectToAction(nameof(Index), new { esito, filtro, periodo = Periodo(periodo), confronto = Confronto(confronto), servizio = (int)Ambito(servizio) });
    }

    [HttpPost("Annulla")]
    public async Task<IActionResult> Annulla(int id, string? esito, string? filtro, string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        var d = await _servizio.AnnullaAsync(id, _utente.NomeCompleto ?? _utente.Email, ct);
        if (d is not null)
            await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.subentro.annulla", EsitoOperazione.Successo,
                "SubentroInsediamento", id.ToString(), new { d.IdInsediamentoPrecedente, d.IdInsediamentoSuccessivo }, ct);
        TempData["Messaggio"] = d is null ? "Decisione non trovata o già annullata." : "Decisione annullata.";
        return RedirectToAction(nameof(Index), new { esito, filtro, periodo = Periodo(periodo), confronto = Confronto(confronto), servizio = (int)Ambito(servizio) });
    }

    [HttpGet("Cerca")]
    public async Task<IActionResult> Cerca(string? q, CancellationToken ct) => Json(await _servizio.CercaAsync(q ?? "", ct));

    [HttpPost("Vies")]
    public async Task<IActionResult> Vies(string? partitaIva, string? esito, string? filtro, string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        var v = await _servizio.VerificaPartitaIvaAsync(partitaIva, ct);
        TempData["Messaggio"] = v is null ? "P.IVA non verificabile su VIES (estera o non valida)."
            : v.Valida == true ? $"P.IVA {v.PartitaIva}: attiva in VIES{(v.Nome is null ? "" : " · " + v.Nome)}."
            : v.Valida == false ? $"P.IVA {v.PartitaIva}: non trovata in VIES (cessata oppure non abilitata agli scambi con l'estero)."
            : $"P.IVA {v.PartitaIva}: {v.Errore} Riprovare più tardi.";
        return RedirectToAction(nameof(Index), new { esito, filtro, periodo = Periodo(periodo), confronto = Confronto(confronto), servizio = (int)Ambito(servizio) });
    }

    /// <summary>Verifica VIES delle P.IVA dei clienti persi senza subentro (C e D), al massimo 60 per volta.</summary>
    [HttpPost("ViesBlocco")]
    public async Task<IActionResult> ViesBlocco(string? esito, string? filtro, string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        var r = await _servizio.CalcolaAsync(DateTime.Today, Finestra(periodo, confronto), Ambito(servizio), ct);
        var piva = r.Risultato.Persi.Where(p => p.Esito is EsitoSubentro.NessunSubentro or EsitoSubentro.SubentratoNonAttivo)
            .Select(p => p.Perso.Insediamento.PartitaIva);
        var (n, valide, non, err) = await _servizio.VerificaInBloccoAsync(piva, 60, ct);
        await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.subentro.vies", EsitoOperazione.Successo, dettaglio: new { n, valide, non, err }, ct: ct);
        TempData["Messaggio"] = n == 0 ? "Nessuna P.IVA da verificare (già controllate negli ultimi 30 giorni)."
            : $"Verificate {n} P.IVA: {valide} attive, {non} non trovate in VIES{(err > 0 ? $", {err} non riuscite (riprovare più tardi)" : "")}.";
        return RedirectToAction(nameof(Index), new { esito, filtro, periodo = Periodo(periodo), confronto = Confronto(confronto), servizio = (int)Ambito(servizio) });
    }

    [HttpGet("Excel")]
    public async Task<IActionResult> Excel(string? periodo, int? confronto, int? servizio, CancellationToken ct)
    {
        var r = await _servizio.CalcolaAsync(DateTime.Today, Finestra(periodo, confronto), Ambito(servizio), ct);
        var importi = _utente.HaPermesso(Permessi.ClientiSeguire);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Cambi di gestione");
        var intest = new List<string> { "Esito", "Da verificare", "Confermato", "Codice", "Cliente", "P.IVA", "VIES", "Insediamento", "Indirizzo", "Città",
            $"Ultima attività ({AmbitiSubentri.Nome(r.Ambito)})", $"{AmbitiSubentri.Unita(r.Ambito)} {r.Risultato.Confronto}", $"{AmbitiSubentri.Unita(r.Ambito)} cliente altrove {r.Risultato.Attuale}",
            "Nuovo codice", "Nuovo cliente", "Nuovo insediamento", "Affidabilità", "Primo campione nuovo", "Ultimo campione nuovo", $"{AmbitiSubentri.Unita(r.Ambito)} nuovo {r.Risultato.Attuale}" };
        if (importi) { intest.Insert(12, $"Valore {r.Risultato.Confronto} (listino)"); intest.Add($"Valore nuovo {r.Risultato.Attuale} (listino)"); }
        for (var c = 0; c < intest.Count; c++) ws.Cell(1, c + 1).Value = intest[c];
        var n = 2;
        foreach (var p in r.Risultato.Persi)
        {
            var i = p.Perso.Insediamento;
            var s = p.Successore?.Stato;
            var v = TestiSubentri.Vies(r, i.PartitaIva);
            var valori = new List<object?> { TestiSubentri.Esito(p.Esito), p.DaVerificare ? "sì" : "", p.Confermato ? "sì" : "", i.CodiceCliente, i.RagioneSociale,
                i.PartitaIva, v?.Testo, i.Nome, i.Indirizzo, i.Citta, p.Perso.UltimaAttivitaAmbito, p.Perso.AmbitoPrecedenti, p.CampioniClienteAltrove,
                s?.Insediamento.CodiceCliente, s?.Insediamento.RagioneSociale, s?.Insediamento.Nome,
                p.Successore is null ? null : TestiSubentri.Affidabilita(p.Successore.Affidabilita), s?.PrimoCampione, s?.UltimoCampione, s?.CampioniUltimi };
            if (importi) { valori.Insert(12, p.Perso.ValorePrecedenti); valori.Add(s?.ValoreUltimi); }
            for (var c = 0; c < valori.Count; c++)
            {
                var cella = ws.Cell(n, c + 1);
                switch (valori[c])
                {
                    case null: break;
                    case DateTime d: cella.Value = d; cella.Style.DateFormat.Format = "dd/mm/yyyy"; break;
                    case decimal m: cella.Value = m; cella.Style.NumberFormat.Format = "#,##0.00"; break;
                    case int k: cella.Value = k; break;
                    default: cella.Value = valori[c]!.ToString(); break;
                }
            }
            n++;
        }
        ws.Row(1).Style.Font.Bold = true;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(n, 200));
        await _audit.RegistraAsync(AreaFunzionale.Clienti, "clienti.subentro.esporta", EsitoOperazione.Successo, dettaglio: new { righe = n - 2 }, ct: ct);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CambiDiGestione_{Ambito(servizio)}_{DateTime.Today:yyyyMMdd}_{Periodo(periodo)}m.xlsx");
    }

    // Finestre temporali come in «Clienti da seguire» (12/10/2026): periodo 12/6/3 mesi o da inizio anno, confronto 1 o 2 anni prima.
    private static string Periodo(string? periodo) => periodo is "6" or "3" or "anno" ? periodo : "12";
    private static int Confronto(int? confronto) => confronto == 2 ? 2 : 1;
    // Servizio (13/10/2026): Legionella di base, poi analisi e aree della consulenza.
    private static AmbitoSubentri Ambito(int? servizio) =>
        servizio is int v && Enum.IsDefined(typeof(AmbitoSubentri), v) ? (AmbitoSubentri)v : AmbitoSubentri.Legionella;
    private static FinestraConfronto Finestra(string? periodo, int? confronto) =>
        ClientiDaSeguireController.Finestra(Periodo(periodo), Confronto(confronto));

    private static IEnumerable<RisultatoSubentro> Filtra(RiepilogoSubentri r, string esito, string? filtro)
    {
        var righe = r.Risultato.Persi.Where(p => esito switch
        {
            "a" => p.Esito == EsitoSubentro.SubentratoAttivo,
            "b" => p.Esito == EsitoSubentro.AnagraficaDoppia,
            "c" => p.Esito == EsitoSubentro.SubentratoNonAttivo,
            "d" => p.Esito == EsitoSubentro.NessunSubentro,
            "e" => p.Esito == EsitoSubentro.ClienteAttivoAltrove,
            "verifica" => p.DaVerificare,
            "confermati" => p.Confermato,
            _ => true
        });
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            var t = filtro.Trim();
            bool Contiene(string? s) => s is not null && s.Contains(t, StringComparison.OrdinalIgnoreCase);
            righe = righe.Where(p =>
            {
                var i = p.Perso.Insediamento;
                var s = p.Successore?.Stato.Insediamento;
                return i.CodiceCliente == t || Contiene(i.RagioneSociale) || Contiene(i.Nome) || Contiene(i.Citta) || Contiene(i.Indirizzo)
                       || (s is not null && (s.CodiceCliente == t || Contiene(s.RagioneSociale) || Contiene(s.Nome)));
            });
        }
        return righe;
    }
}

public sealed record ModelloSubentri(RiepilogoSubentri Riepilogo, List<RisultatoSubentro> Righe, string Esito, string? Filtro, bool VedeImporti,
    string Periodo, int Confronto);

/// <summary>Testi comuni della pagina «Cambi di gestione» e della sezione Legionella.</summary>
public static class TestiSubentri
{
    public static string Esito(EsitoSubentro e) => e switch
    {
        EsitoSubentro.SubentratoAttivo => "A – nuovo gestore, ancora nostro",
        EsitoSubentro.AnagraficaDoppia => "B – anagrafica doppia, attivo",
        EsitoSubentro.SubentratoNonAttivo => "C – nuovo gestore non attivo",
        EsitoSubentro.NessunSubentro => "D – nessun subentro trovato",
        EsitoSubentro.ClienteAttivoAltrove => "E – cliente attivo da un'altra sede",
        _ => e.ToString()
    };

    public static string Classe(EsitoSubentro e) => e switch
    {
        EsitoSubentro.SubentratoAttivo or EsitoSubentro.AnagraficaDoppia or EsitoSubentro.ClienteAttivoAltrove => "etichetta-conforme",
        EsitoSubentro.SubentratoNonAttivo => "etichetta-borderline",
        _ => "etichetta-non-conforme"
    };

    public static string Affidabilita(AffidabilitaSubentro a) => a switch
    {
        AffidabilitaSubentro.Alta => "alta (stesso nome)",
        AffidabilitaSubentro.Media => "media (stesso indirizzo, nome simile)",
        _ => "bassa (solo indirizzo)"
    };

    public sealed record StatoVies(string Testo, string Classe, string? Dettaglio);

    public static StatoVies? Vies(RiepilogoSubentri r, string? partitaIva)
    {
        var p = MotoreSubentri.PartitaIvaValida(partitaIva);
        if (p is null || !r.Vies.TryGetValue(p, out var v)) return null;
        var quando = v.DataVerifica.ToLocalTime().ToString("dd/MM/yy");
        return v.Valida switch
        {
            true => new StatoVies("VIES: attiva", "etichetta-conforme", $"{v.Nome} · verificata il {quando}"),
            false => new StatoVies("VIES: non trovata", "etichetta-borderline",
                $"Verificata il {quando}: cessata oppure non abilitata agli scambi con l'estero (molte piccole imprese non lo sono)"),
            _ => new StatoVies("VIES: da riprovare", "etichetta-neutro", v.Errore)
        };
    }

    /// <summary>Riassunto per cliente nella sezione Legionella: l'esito più favorevole tra le sue sedi perse.</summary>
    public static Dictionary<string, (string Testo, string Classe)> PerCliente(RiepilogoSubentri r)
    {
        int Ordine(EsitoSubentro e) => e switch
        {
            EsitoSubentro.SubentratoAttivo => 0, EsitoSubentro.AnagraficaDoppia => 1, EsitoSubentro.ClienteAttivoAltrove => 2,
            EsitoSubentro.SubentratoNonAttivo => 3, _ => 4
        };
        return r.Risultato.Persi.Where(p => !string.IsNullOrEmpty(p.Perso.Insediamento.CodiceCliente))
            .GroupBy(p => p.Perso.Insediamento.CodiceCliente!)
            .ToDictionary(g => g.Key, g =>
            {
                var p = g.OrderBy(x => x.DaVerificare ? 1 : 0).ThenBy(x => Ordine(x.Esito)).First();
                var nuovo = p.Successore?.Stato.Insediamento.RagioneSociale;
                var testo = p.Esito switch
                {
                    EsitoSubentro.SubentratoAttivo => $"nuovo gestore attivo: {nuovo}",
                    EsitoSubentro.AnagraficaDoppia => $"anagrafica doppia, attiva: {nuovo}",
                    EsitoSubentro.ClienteAttivoAltrove => "attivo da un'altra sede",
                    EsitoSubentro.SubentratoNonAttivo => $"nuovo gestore non attivo: {nuovo}",
                    _ => "nessun subentro trovato"
                };
                if (p.DaVerificare) testo += " (da verificare)";
                return (testo, p.DaVerificare ? "etichetta-attesa" : Classe(p.Esito));
            }, StringComparer.Ordinal);
    }
}
