using ClosedXML.Excel;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Direzione;
using LaboratorioAI.Web.Autorizzazione;
using Microsoft.AspNetCore.Mvc;

namespace LaboratorioAI.Web.Controllers;

public sealed record ModelloConsuntivo(RisultatoConsuntivo Risultato, DateTime? UltimaRegistrazione, DateTime? UltimaImportazione)
{
    /// <summary>22/10/2026: avviso dell'ultima importazione AZW (es. contabilità analitica non aggiornata).</summary>
    public string? AvvisoImportazione { get; init; }
    /// <summary>22/10/2026: l'utente è un socio e può chiedere la rettifica con i compensi soci.</summary>
    public bool VedeCompensiSoci { get; init; }
    /// <summary>La rettifica è stata chiesta.</summary>
    public bool RettificaChiesta { get; init; }
    /// <summary>Rettifica dell'anno e dell'anno prima (solo per un anno intero e se chiesta).</summary>
    public RettificaCompensiSoci? Rettifica { get; init; }
    public RettificaCompensiSoci? RettificaPrecedente { get; init; }
}

/// <summary>
/// Consuntivo dei costi e dei ricavi per area (20/10/2026), come il report Access «Conteggio dei costi e dei ricavi per
/// area», con confronto sull'anno prima, andamento mensile, dettaglio delle registrazioni ed Excel.
/// Contiene gli stipendi voce per voce: riservato ai soci. Definizione: claude/definizione-consuntivi.md
/// </summary>
[RichiedePermesso(Permessi.ConsuntiviConsulta)]
public class ConsuntiviController : Controller
{
    private readonly ServizioConsuntivi _servizio;
    private readonly IServizioAudit _audit;
    private readonly ServizioCompensiSoci _compensi;
    private readonly IContestoUtente _utente;

    public ConsuntiviController(ServizioConsuntivi servizio, IServizioAudit audit, ServizioCompensiSoci compensi, IContestoUtente utente)
    {
        _servizio = servizio; _audit = audit; _compensi = compensi; _utente = utente;
    }

    /// <summary>Periodo di un anno intero (1/1–31/12 dello stesso anno): solo lì si può rettificare con i compensi soci.</summary>
    public static int? AnnoIntero(DateTime dal, DateTime al) =>
        dal.Month == 1 && dal.Day == 1 && al.Month == 12 && al.Day == 31 && dal.Year == al.Year ? dal.Year : null;

    /// <summary>
    /// 22/10/2026: la rettifica con i compensi soci la vedono solo i soci (come la sezione Compensi soci). In sviluppo, con
    /// l'accesso di sviluppo attivo, anche l'utente di sviluppo.
    /// </summary>
    private async Task<bool> VedeCompensiSociAsync(CancellationToken ct)
    {
        if (!_utente.HaPermesso(Permessi.CompensiSociConsulta)) return false;
        if (await _compensi.SocioDaEmailAsync(_utente.Email, ct) is not null) return true;
        var amb = HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
        var cfg = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        return amb.IsDevelopment() && cfg.GetValue<bool>("AutenticazioneSviluppo:Abilitato")
            && string.Equals(cfg["AutenticazioneSviluppo:Email"], _utente.Email, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(RettificaCompensiSoci? Anno, RettificaCompensiSoci? Precedente)> RettificheAsync(RisultatoConsuntivo r, CancellationToken ct)
    {
        if (AnnoIntero(r.Dal, r.Al) is not int anno) return (null, null);
        var corrente = MotoreAccessoriAree.Rettifica(anno, r.Aree, r.Totali, await _compensi.DaPagarePerAreaAsync(anno, ct));
        var prec = MotoreAccessoriAree.Rettifica(anno - 1, r.Aree, r.TotaliPrecedenti, await _compensi.DaPagarePerAreaAsync(anno - 1, ct));
        return (corrente, prec);
    }

    private static (DateTime Dal, DateTime Al) Periodo(DateTime? dal, DateTime? al)
    {
        var (d, a) = ServizioConsuntivi.PeriodoPredefinito(DateTime.Today);
        var inizio = (dal ?? d).Date;
        var fine = (al ?? a).Date;
        if (fine < inizio) (inizio, fine) = (fine, inizio);
        return (inizio, fine);
    }

    public async Task<IActionResult> Index(DateTime? dal, DateTime? al, bool rettifica, CancellationToken ct)
    {
        ViewData["Titolo"] = "Consuntivo per area";
        var (d, a) = Periodo(dal, al);
        var risultato = await _servizio.CalcolaAsync(d, a, ct);
        var (reg, imp, avvisoImp) = await _servizio.StatoDatiAsync(ct);
        var vede = await VedeCompensiSociAsync(ct);
        var (rett, rettP) = vede && rettifica ? await RettificheAsync(risultato, ct) : (null, null);
        return View(new ModelloConsuntivo(risultato, reg, imp)
        {
            AvvisoImportazione = avvisoImp, VedeCompensiSoci = vede, RettificaChiesta = vede && rettifica, Rettifica = rett, RettificaPrecedente = rettP
        });
    }

    /// <summary>Registrazioni di una voce (e di un'area): frammento HTML per la finestra di dettaglio.</summary>
    public async Task<IActionResult> Dettaglio(DateTime dal, DateTime al, string voce, string? area, CancellationToken ct)
    {
        var righe = await _servizio.DettaglioAsync(dal, al, voce ?? "", string.IsNullOrWhiteSpace(area) ? null : area, ct);
        return PartialView("_Dettaglio", righe);
    }

    public async Task<IActionResult> Esporta(DateTime? dal, DateTime? al, bool rettifica, CancellationToken ct)
    {
        var (d, a) = Periodo(dal, al);
        var r = await _servizio.CalcolaAsync(d, a, ct);
        var (rett, rettP) = rettifica && await VedeCompensiSociAsync(ct) ? await RettificheAsync(r, ct) : (null, null);
        var registrazioni = await _servizio.RegistrazioniAsync(d, a, ct);
        await _audit.RegistraAsync(AreaFunzionale.Direzione, "consuntivi.esporta", EsitoOperazione.Successo,
            dettaglio: new { dal = d.ToString("yyyy-MM-dd"), al = a.ToString("yyyy-MM-dd"), rettificaCompensiSoci = rett is not null }, ct: ct);

        using var libro = new XLWorkbook();
        FoglioConsuntivo(libro, r, rett, rettP);
        FoglioMesi(libro, r);
        FoglioRegistrazioni(libro, registrazioni, r.Aree);
        using var ms = new MemoryStream();
        libro.SaveAs(ms);
        return File(ms.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Consuntivo_{d:yyyyMMdd}_{a:yyyyMMdd}.xlsx");
    }

    private const string FormatoEuro = "#,##0.00;[Red]-#,##0.00";

    private static void FoglioConsuntivo(XLWorkbook libro, RisultatoConsuntivo r, RettificaCompensiSoci? rett, RettificaCompensiSoci? rettP)
    {
        var ws = libro.AddWorksheet("Consuntivo");
        ws.Cell(1, 1).Value = $"Consuntivo costi e ricavi per area dal {r.Dal:dd/MM/yyyy} al {r.Al:dd/MM/yyyy}";
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(2, 1).Value = $"Confronto: stesso periodo dell'anno prima (dal {r.DalPrecedente:dd/MM/yyyy} al {r.AlPrecedente:dd/MM/yyyy}).";
        var n = r.Aree.Count;
        int cTot = 2 + n, cPrec = cTot + 1, cDiff = cTot + 2;
        var riga = 4;
        ws.Cell(riga, 1).Value = "Voce";
        for (var i = 0; i < n; i++) ws.Cell(riga, 2 + i).Value = r.Aree[i].Etichetta;
        ws.Cell(riga, cTot).Value = "Totale";
        ws.Cell(riga, cPrec).Value = "Anno prima";
        ws.Cell(riga, cDiff).Value = "Differenza";
        ws.Row(riga).Style.Font.Bold = true;
        riga++;

        void Sezione(string titolo, List<VoceConsuntivo> voci, Dictionary<string, decimal> tot, Dictionary<string, decimal> totP, string etichettaTot)
        {
            ws.Cell(riga, 1).Value = titolo; ws.Cell(riga, 1).Style.Font.Bold = true; riga++;
            foreach (var v in voci)
            {
                ws.Cell(riga, 1).Value = v.Descrizione;
                for (var i = 0; i < n; i++)
                    if (v.Valori.TryGetValue(r.Aree[i].Codice, out var x) && x != 0m) ws.Cell(riga, 2 + i).Value = x;
                ws.Cell(riga, cTot).Value = v.Totale;
                ws.Cell(riga, cPrec).Value = v.TotalePrecedente;
                ws.Cell(riga, cDiff).Value = v.Totale - v.TotalePrecedente;
                riga++;
            }
            RigaTotale(etichettaTot, tot, totP, true);
            riga++;
        }

        void RigaTotale(string etichetta, Dictionary<string, decimal> tot, Dictionary<string, decimal>? totP, bool grassetto)
        {
            ws.Cell(riga, 1).Value = etichetta;
            for (var i = 0; i < n; i++) ws.Cell(riga, 2 + i).Value = tot.GetValueOrDefault(r.Aree[i].Codice);
            var t = tot.Values.Sum();
            ws.Cell(riga, cTot).Value = t;
            if (totP is not null)
            {
                var tp = totP.Values.Sum();
                ws.Cell(riga, cPrec).Value = tp;
                ws.Cell(riga, cDiff).Value = t - tp;
            }
            if (grassetto) ws.Row(riga).Style.Font.Bold = true;
            riga++;
        }

        Sezione("COSTI", r.Costi, r.Totali.Costi, r.TotaliPrecedenti.Costi, "Totale costi");
        Sezione("RICAVI", r.Ricavi, r.Totali.Ricavi, r.TotaliPrecedenti.Ricavi, "Totale ricavi");
        RigaTotale("Utile o perdita per area", r.Totali.Utile, r.TotaliPrecedenti.Utile, true);
        RigaTotale("Contributo amministrazione", r.Totali.Contributo, r.TotaliPrecedenti.Contributo, false);
        RigaTotale("Utile o perdita con contributo AMM", r.Totali.UtileConContributo, r.TotaliPrecedenti.UtileConContributo, true);
        if (rett is not null)
        {
            // 22/10/2026: rettifica con i compensi soci ancora da pagare (anno intero).
            riga++;
            ws.Cell(riga, 1).Value = $"RETTIFICA COMPENSI SOCI ANCORA DA PAGARE ({rett.Anno})"; ws.Cell(riga, 1).Style.Font.Bold = true; riga++;
            if (!rett.Presente)
            {
                ws.Cell(riga, 1).Value = $"L'anno {rett.Anno} non è presente nei compensi soci."; riga++;
            }
            else
            {
                var p = rettP is { Presente: true } ? rettP : null;
                Dictionary<string, decimal> Neg(Dictionary<string, decimal> x) => x.ToDictionary(k => k.Key, k => -k.Value);
                RigaTotale("Compensi soci ancora da pagare", Neg(rett.DaPagare), p is null ? null : Neg(p.DaPagare), false);
                RigaTotale("Utile o perdita rettificato", rett.UtileRettificato, p?.UtileRettificato, true);
                RigaTotale("Contributo amministrazione dei compensi da pagare", Neg(rett.QuotaAmm), p is null ? null : Neg(p.QuotaAmm), false);
                RigaTotale("Utile o perdita rettificato con contributo AMM", rett.UtileConContributoRettificato, p?.UtileConContributoRettificato, true);
                if (p is null) { ws.Cell(riga, 1).Value = $"L'anno {rett.Anno - 1} non è presente nei compensi soci: niente confronto."; riga++; }
            }
        }
        riga++;
        ws.Cell(riga, 1).Value = "Utile o perdita MARE.A"; ws.Cell(riga, 1).Style.Font.Bold = true;
        ws.Cell(riga, cTot).Value = r.Totali.UtileMarea;
        ws.Cell(riga, cPrec).Value = r.TotaliPrecedenti.UtileMarea;
        ws.Cell(riga, cDiff).Value = r.Totali.UtileMarea - r.TotaliPrecedenti.UtileMarea;
        ws.Row(riga).Style.Font.Bold = true;
        if (rett is { Presente: true })
        {
            riga++;
            ws.Cell(riga, 1).Value = "Utile o perdita MARE.A rettificato"; ws.Cell(riga, 1).Style.Font.Bold = true;
            ws.Cell(riga, cTot).Value = rett.UtileMareaRettificato;
            if (rettP is { Presente: true })
            {
                ws.Cell(riga, cPrec).Value = rettP.UtileMareaRettificato;
                ws.Cell(riga, cDiff).Value = rett.UtileMareaRettificato - rettP.UtileMareaRettificato;
            }
            ws.Row(riga).Style.Font.Bold = true;
        }

        ws.Range(5, 2, riga, cDiff).Style.NumberFormat.Format = FormatoEuro;
        ws.Column(1).Width = 40;
        for (var c = 2; c <= cDiff; c++) ws.Column(c).Width = 15;
        ws.SheetView.FreezeRows(4);
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.FitToPages(1, 0);
    }

    private static readonly string[] NomiMesi = ["", "gen", "feb", "mar", "apr", "mag", "giu", "lug", "ago", "set", "ott", "nov", "dic"];

    private static void FoglioMesi(XLWorkbook libro, RisultatoConsuntivo r)
    {
        var ws = libro.AddWorksheet("Andamento mensile");
        var riga = 1;
        foreach (var (titolo, valori) in new (string, Func<MeseConsuntivo, IReadOnlyDictionary<string, decimal>>)[]
                 { ("Utile o perdita per area (senza contributo AMM)", m => m.Utile), ("Ricavi per area", m => m.Ricavi) })
        {
            ws.Cell(riga, 1).Value = titolo; ws.Cell(riga, 1).Style.Font.Bold = true; riga++;
            ws.Cell(riga, 1).Value = "Mese";
            for (var i = 0; i < r.Aree.Count; i++) ws.Cell(riga, 2 + i).Value = r.Aree[i].Etichetta;
            ws.Cell(riga, 2 + r.Aree.Count).Value = "Totale";
            ws.Row(riga).Style.Font.Bold = true; riga++;
            var inizio = riga;
            foreach (var m in r.Mesi)
            {
                var v = valori(m);
                ws.Cell(riga, 1).Value = $"{NomiMesi[m.Mese]} {m.Anno}";
                for (var i = 0; i < r.Aree.Count; i++) ws.Cell(riga, 2 + i).Value = v.GetValueOrDefault(r.Aree[i].Codice);
                ws.Cell(riga, 2 + r.Aree.Count).Value = v.Values.Sum();
                riga++;
            }
            ws.Range(inizio, 2, riga - 1, 2 + r.Aree.Count).Style.NumberFormat.Format = FormatoEuro;
            riga += 2;
        }
        ws.Column(1).Width = 14;
        for (var c = 2; c <= 2 + r.Aree.Count; c++) ws.Column(c).Width = 15;
    }

    private static void FoglioRegistrazioni(XLWorkbook libro, List<RegistrazioneAnalitica> righe, List<AreaConsuntivo> aree)
    {
        var ws = libro.AddWorksheet("Registrazioni");
        string[] intest = ["Data", "Registrazione", "Documento", "Area", "Centro", "Voce", "Commessa", "Conto", "Descrizione", "Importo"];
        for (var i = 0; i < intest.Length; i++) ws.Cell(1, i + 1).Value = intest[i];
        ws.Row(1).Style.Font.Bold = true;
        var etichetta = aree.ToDictionary(a => a.Codice, a => a.Etichetta);
        var riga = 2;
        foreach (var x in righe)
        {
            ws.Cell(riga, 1).Value = x.DataRegistrazione;
            ws.Cell(riga, 2).Value = $"{x.NumeroRegistrazione}/{x.AnnoRegistrazione}";
            ws.Cell(riga, 3).Value = $"{x.TipoDocumento} {x.NumeroDocumento}".Trim();
            ws.Cell(riga, 4).Value = etichetta.GetValueOrDefault(MotoreConsuntivo.AreaDi(x.Centro), x.Centro);
            ws.Cell(riga, 5).Value = x.Centro;
            ws.Cell(riga, 6).Value = x.Voce;
            ws.Cell(riga, 7).Value = x.Commessa;
            ws.Cell(riga, 8).Value = x.Conto;
            ws.Cell(riga, 9).Value = x.Descrizione;
            ws.Cell(riga, 10).Value = x.Importo;
            riga++;
        }
        ws.Column(1).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Column(10).Style.NumberFormat.Format = FormatoEuro;
        if (riga > 2) ws.Range(1, 1, riga - 1, intest.Length).SetAutoFilter();
        ws.Columns(1, 8).AdjustToContents();
        ws.Column(9).Width = 50;
        ws.Column(10).Width = 14;
        ws.SheetView.FreezeRows(1);
    }
}
