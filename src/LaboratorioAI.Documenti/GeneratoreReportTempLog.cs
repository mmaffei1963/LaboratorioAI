using System.Globalization;
using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SkiaSharp;

namespace LaboratorioAI.Documenti;

/// <summary>Report del monitoraggio TempLog: riepilogo canali ed escursioni, in Excel e PDF (con grafici per canale).</summary>
public interface IGeneratoreReportTempLog
{
    byte[] GeneraExcel(RisultatoMonitoraggio r);
    byte[] GeneraPdf(RisultatoMonitoraggio r);
    string NomeFile(RisultatoMonitoraggio r, string estensione);

    byte[] GeneraSondeExcel(IReadOnlyList<SondaEconorma> sonde, DateTime? ultimaSincronizzazione);
    byte[] GeneraSondePdf(IReadOnlyList<SondaEconorma> sonde, DateTime? ultimaSincronizzazione);
}

/// <inheritdoc />
public class GeneratoreReportTempLog : IGeneratoreReportTempLog
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public string NomeFile(RisultatoMonitoraggio r, string estensione)
    {
        var g = r.DataDal?.ToString("yyyyMMdd") ?? DateTime.Now.ToString("yyyyMMdd");
        return $"Report_Econorma_{g}.{estensione}";
    }

    // ── Excel ───────────────────────────────────────────────────────
    public byte[] GeneraExcel(RisultatoMonitoraggio r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Riepilogo");
        ws.Cell(1, 1).Value = "Monitoraggio TempLog";
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = $"File: {r.NomeFile}   Periodo: {Data(r.DataDal)} → {Data(r.DataAl)}";
        ws.Cell(3, 1).Value = r.TuttoConforme ? "Esito: TUTTO NEI LIMITI" : $"Esito: {r.NumeroCanaliConEscursioni} canali con escursioni ({r.Escursioni.Count} escursioni)";

        var h = new[] { "Canale", "Ambiente", "VN", "Min", "Max", "Letture", "Sopra", "Sotto", "Media", "Min oss.", "Max oss.", "Esito" };
        int row = 5;
        for (int i = 0; i < h.Length; i++) { ws.Cell(row, i + 1).Value = h[i]; ws.Cell(row, i + 1).Style.Font.Bold = true; }
        foreach (var c in r.Canali)
        {
            row++;
            ws.Cell(row, 1).Value = c.Canale;
            ws.Cell(row, 2).Value = c.Ambiente ?? c.Sonda ?? "";
            SetNum(ws.Cell(row, 3), c.VN); SetNum(ws.Cell(row, 4), c.Min); SetNum(ws.Cell(row, 5), c.Max);
            ws.Cell(row, 6).Value = c.NumeroLetture; ws.Cell(row, 7).Value = c.NumeroSopra; ws.Cell(row, 8).Value = c.NumeroSotto;
            SetNum(ws.Cell(row, 9), c.ValoreMedio); SetNum(ws.Cell(row, 10), c.ValoreMin); SetNum(ws.Cell(row, 11), c.ValoreMax);
            ws.Cell(row, 12).Value = !c.LimitiDisponibili ? "da sistemare" : c.Escursioni.Count > 0 ? $"{c.Escursioni.Count} escursioni" : "conforme";
            if (c.NumeroSopra > 0) ws.Cell(row, 7).Style.Font.FontColor = XLColor.Red;
            if (c.NumeroSotto > 0) ws.Cell(row, 8).Style.Font.FontColor = XLColor.Blue;
        }
        ws.Columns().AdjustToContents();

        var we = wb.AddWorksheet("Escursioni");
        var he = new[] { "Data", "Canale", "Ambiente", "Tipo", "Dalle", "Alle", "N. letture", "Valore peggiore", "Limite", "Scostamento" };
        for (int i = 0; i < he.Length; i++) { we.Cell(1, i + 1).Value = he[i]; we.Cell(1, i + 1).Style.Font.Bold = true; }
        int re = 1;
        foreach (var e in r.Escursioni)
        {
            re++;
            we.Cell(re, 1).Value = Data(e.Data);
            we.Cell(re, 2).Value = e.Canale; we.Cell(re, 3).Value = e.Ambiente ?? "";
            we.Cell(re, 4).Value = e.Tipo; we.Cell(re, 5).Value = e.OraInizio; we.Cell(re, 6).Value = e.OraFine;
            we.Cell(re, 7).Value = e.NumeroLetture; we.Cell(re, 8).Value = e.ValorePeggiore;
            we.Cell(re, 9).Value = e.LimiteSuperato; we.Cell(re, 10).Value = e.ScostamentoMax;
            we.Cell(re, 4).Style.Font.FontColor = e.Tipo.Contains("massimo") ? XLColor.Red : XLColor.Blue;
        }
        we.Columns().AdjustToContents();

        // Foglio "Valori per orario" (matrice ore × canali, come Foglio1)
        var wv = wb.AddWorksheet("Valori");
        var baseC = r.Canali.OrderByDescending(c => c.Letture.Count).FirstOrDefault();
        var ore = baseC?.Letture.Select(x => x.Ora).ToList() ?? new List<string>();
        wv.Cell(1, 1).Value = "Ora"; wv.Cell(1, 1).Style.Font.Bold = true;
        for (int j = 0; j < r.Canali.Count; j++)
        {
            var cell = wv.Cell(1, j + 2); cell.Value = r.Canali[j].Canale; cell.Style.Font.Bold = true;
        }
        for (int i = 0; i < ore.Count; i++)
        {
            wv.Cell(i + 2, 1).Value = ore[i];
            for (int j = 0; j < r.Canali.Count; j++)
            {
                var lt = r.Canali[j].Letture;
                if (i >= lt.Count || lt[i].Valore is null) continue;
                var cell = wv.Cell(i + 2, j + 2); cell.Value = lt[i].Valore!.Value;
                if (lt[i].Stato == StatoLettura.SopraMax) cell.Style.Font.FontColor = XLColor.Red;
                else if (lt[i].Stato == StatoLettura.SottoMin) cell.Style.Font.FontColor = XLColor.Blue;
            }
        }
        wv.SheetView.FreezeRows(1); wv.SheetView.FreezeColumns(1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── PDF (con grafici per canale, stile Foglio3) ─────────────────
    public byte[] GeneraPdf(RisultatoMonitoraggio r)
    {
        var temp = new List<string>();
        var doc = new Document();
        var normale = doc.Styles["Normal"]!; normale.Font.Name = "Arial"; normale.Font.Size = 9;
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1); sec.PageSetup.BottomMargin = Unit.FromCentimeter(1);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.4); sec.PageSetup.RightMargin = Unit.FromCentimeter(1.4);

        var tit = sec.AddParagraph("MONITORAGGIO TEMPLOG"); tit.Format.Font.Bold = true; tit.Format.Font.Size = 14;
        sec.AddParagraph($"File: {r.NomeFile}    Periodo: {Data(r.DataDal)} → {Data(r.DataAl)}").Format.Font.Size = 8;
        var esito = sec.AddParagraph(r.TuttoConforme ? "TUTTO NEI LIMITI"
            : $"{r.NumeroCanaliConEscursioni} canali con escursioni — {r.Escursioni.Count} escursioni totali");
        esito.Format.Font.Bold = true; esito.Format.Font.Color = r.TuttoConforme ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        esito.Format.SpaceAfter = "0.2cm";

        // Escursioni
        Titolo(sec, "Escursioni rilevate");
        var te = sec.AddTable(); te.Borders.Width = 0.5; te.Borders.Color = new Color(0xFFBBBBBB); te.Format.Font.Size = 8;
        foreach (var w in new[] { 1.8, 1.4, 4.0, 2.6, 1.6, 1.6, 1.2, 2.0, 1.6 }) te.AddColumn(Unit.FromCentimeter(w));
        var hr = te.AddRow(); hr.Format.Font.Bold = true; hr.Shading.Color = new Color(0xFFF3F3F3);
        var intest = new[] { "Data", "Canale", "Ambiente", "Tipo", "Dalle", "Alle", "N.", "Peggiore", "Scost." };
        for (int i = 0; i < intest.Length; i++) hr.Cells[i].AddParagraph(intest[i]);
        foreach (var e in r.Escursioni)
        {
            var row = te.AddRow();
            row.Cells[0].AddParagraph(Data(e.Data)); row.Cells[1].AddParagraph(e.Canale);
            row.Cells[2].AddParagraph(e.Ambiente ?? ""); row.Cells[3].AddParagraph(e.Tipo);
            row.Cells[3].Format.Font.Color = e.Tipo.Contains("massimo") ? new Color(0xFFB3261E) : new Color(0xFF1741B3);
            row.Cells[4].AddParagraph(e.OraInizio); row.Cells[5].AddParagraph(e.OraFine);
            row.Cells[6].AddParagraph(e.NumeroLetture.ToString());
            row.Cells[7].AddParagraph(Num(e.ValorePeggiore)); row.Cells[8].AddParagraph(Num(e.ScostamentoMax, 2));
        }
        if (r.Escursioni.Count == 0) te.AddRow().Cells[0].AddParagraph("Nessuna escursione.");

        // Monitor: un grafico per canale (griglia 2 colonne)
        Titolo(sec, "Monitor per canale");
        var griglia = sec.AddTable(); griglia.Borders.Width = 0;
        griglia.AddColumn(Unit.FromCentimeter(9)); griglia.AddColumn(Unit.FromCentimeter(9));
        Row? riga = null;
        int col = 0;
        foreach (var c in r.Canali)
        {
            if (col == 0) riga = griglia.AddRow();
            var png = DisegnaCanale(c);
            if (png != null)
            {
                temp.Add(png);
                var img = riga!.Cells[col].AddImage(png);
                img.LockAspectRatio = true; img.Width = Unit.FromCentimeter(8.5);
            }
            riga!.Cells[col].AddParagraph($"{c.Canale} — {c.Ambiente ?? c.Sonda ?? ""}").Format.Font.Size = 7;
            col = (col + 1) % 2;
        }

        try
        {
            var renderer = new PdfDocumentRenderer { Document = doc };
            renderer.RenderDocument();
            using var ms = new MemoryStream(); renderer.PdfDocument.Save(ms, false);
            return ms.ToArray();
        }
        finally { foreach (var f in temp) if (File.Exists(f)) File.Delete(f); }
    }

    // ── Report sonde ArcLab ─────────────────────────────────────────
    public byte[] GeneraSondeExcel(IReadOnlyList<SondaEconorma> sonde, DateTime? ultimaSync)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sonde ArcLab");
        ws.Cell(1, 1).Value = "Sonde Econorma — anagrafica da ArcLab";
        ws.Cell(1, 1).Style.Font.Bold = true; ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Cell(2, 1).Value = "Ultima sincronizzazione: " + (ultimaSync.HasValue ? ultimaSync.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", It) : "mai");
        var h = new[] { "Canale", "IdStrumento", "Sonda", "Ambiente", "IdApp ambiente", "Piano", "VN", "Min", "Max", "Limiti", "In uso" };
        for (int i = 0; i < h.Length; i++) { ws.Cell(4, i + 1).Value = h[i]; ws.Cell(4, i + 1).Style.Font.Bold = true; }
        int row = 4;
        foreach (var s in sonde)
        {
            row++;
            ws.Cell(row, 1).Value = s.CanaleIpotesi ?? ""; ws.Cell(row, 2).Value = s.IdStrumento;
            ws.Cell(row, 3).Value = s.Sonda; ws.Cell(row, 4).Value = s.Ambiente ?? "";
            ws.Cell(row, 5).Value = s.IdApparecchioAmbiente; ws.Cell(row, 6).Value = s.NomePiano ?? "";
            SetNum(ws.Cell(row, 7), s.VN); SetNum(ws.Cell(row, 8), s.Min); SetNum(ws.Cell(row, 9), s.Max);
            ws.Cell(row, 10).Value = (s.Min.HasValue && s.Max.HasValue) ? "ok" : "mancanti";
            ws.Cell(row, 11).Value = s.SondaNonInUso ? "no" : "sì";
            if (!(s.Min.HasValue && s.Max.HasValue)) ws.Cell(row, 10).Style.Font.FontColor = XLColor.Red;
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream(); wb.SaveAs(ms); return ms.ToArray();
    }

    public byte[] GeneraSondePdf(IReadOnlyList<SondaEconorma> sonde, DateTime? ultimaSync)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!; normale.Font.Name = "Arial"; normale.Font.Size = 8;
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4; sec.PageSetup.Orientation = Orientation.Landscape;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1); sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.2); sec.PageSetup.RightMargin = Unit.FromCentimeter(1.2);
        var tit = sec.AddParagraph("Sonde Econorma — anagrafica da ArcLab"); tit.Format.Font.Bold = true; tit.Format.Font.Size = 13;
        sec.AddParagraph("Ultima sincronizzazione: " + (ultimaSync.HasValue ? ultimaSync.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", It) : "mai")).Format.SpaceAfter = "0.2cm";
        var t = sec.AddTable(); t.Borders.Width = 0.5; t.Borders.Color = new Color(0xFFBBBBBB); t.Format.Font.Size = 8;
        foreach (var w in new[] { 1.6, 2.0, 5.5, 6.0, 2.2, 4.5, 1.6, 1.6, 1.6, 1.8 }) t.AddColumn(Unit.FromCentimeter(w));
        var hr = t.AddRow(); hr.Format.Font.Bold = true; hr.Shading.Color = new Color(0xFFF3F3F3);
        var hh = new[] { "Canale", "IdStrum.", "Sonda", "Ambiente", "IdApp amb.", "Piano", "VN", "Min", "Max", "Limiti" };
        for (int i = 0; i < hh.Length; i++) hr.Cells[i].AddParagraph(hh[i]);
        foreach (var s in sonde)
        {
            var row = t.AddRow();
            row.Cells[0].AddParagraph(s.CanaleIpotesi ?? ""); row.Cells[1].AddParagraph(s.IdStrumento.ToString());
            row.Cells[2].AddParagraph(s.Sonda); row.Cells[3].AddParagraph(s.Ambiente ?? "");
            row.Cells[4].AddParagraph(s.IdApparecchioAmbiente?.ToString() ?? ""); row.Cells[5].AddParagraph(s.NomePiano ?? "");
            row.Cells[6].AddParagraph(s.VN?.ToString("0.##", It) ?? ""); row.Cells[7].AddParagraph(s.Min?.ToString("0.##", It) ?? "");
            row.Cells[8].AddParagraph(s.Max?.ToString("0.##", It) ?? "");
            var ok = s.Min.HasValue && s.Max.HasValue;
            var cl = row.Cells[9].AddParagraph(ok ? "ok" : "mancanti"); if (!ok) cl.Format.Font.Color = new Color(0xFFB3261E);
        }
        var renderer = new PdfDocumentRenderer { Document = doc }; renderer.RenderDocument();
        using var ms = new MemoryStream(); renderer.PdfDocument.Save(ms, false); return ms.ToArray();
    }

    private static string? DisegnaCanale(EsitoCanale c)
    {
        var vals = c.Letture.Where(x => x.Valore.HasValue).Select(x => x.Valore!.Value).ToList();
        if (vals.Count == 0) return null;
        int W = 520, H = 200, ml = 44, mr = 12, mt = 22, mb = 22;
        double lo = vals.Min(), hi = vals.Max();
        if (c.LimitiDisponibili) { lo = Math.Min(lo, c.Min!.Value); hi = Math.Max(hi, c.Max!.Value); }
        double pad = (hi - lo) * 0.1; if (pad <= 0) pad = 1; lo -= pad; hi += pad;
        int n = c.Letture.Count;
        float PX(int i) => (float)(ml + (double)i / Math.Max(n - 1, 1) * (W - ml - mr));
        float PY(double v) => (float)(mt + (hi - v) / (hi - lo) * (H - mt - mb));

        using var bmp = new SKBitmap(W, H); using var g = new SKCanvas(bmp); g.Clear(SKColors.White);
        using var axis = new SKPaint { Color = SKColors.Gray, StrokeWidth = 1, IsAntialias = true };
        using var testo = new SKPaint { Color = SKColors.Black, IsAntialias = true, TextSize = 12 };
        // titolo
        g.DrawText($"{c.Canale}  {(c.LimitiDisponibili ? $"{Num(c.Min!.Value)}…{Num(c.Max!.Value)}" : "(senza limiti)")}", ml, 14, testo);
        if (c.LimitiDisponibili)
        {
            using var band = new SKPaint { Color = new SKColor(0x1A, 0x7F, 0x37, 30), Style = SKPaintStyle.Fill };
            g.DrawRect(ml, PY(c.Max!.Value), W - ml - mr, PY(c.Min!.Value) - PY(c.Max.Value), band);
            using var lmax = new SKPaint { Color = new SKColor(0xB3, 0x26, 0x1E), StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash(new float[] { 4, 3 }, 0) };
            using var lmin = new SKPaint { Color = new SKColor(0x17, 0x41, 0xB3), StrokeWidth = 1, PathEffect = SKPathEffect.CreateDash(new float[] { 4, 3 }, 0) };
            g.DrawLine(ml, PY(c.Max.Value), W - mr, PY(c.Max.Value), lmax);
            g.DrawLine(ml, PY(c.Min!.Value), W - mr, PY(c.Min.Value), lmin);
        }
        g.DrawLine(ml, mt, ml, H - mb, axis); g.DrawLine(ml, H - mb, W - mr, H - mb, axis);
        g.DrawText(hi.ToString("0.0", It), 2, PY(hi) + 4, testo); g.DrawText(lo.ToString("0.0", It), 2, PY(lo) + 4, testo);

        using var linea = new SKPaint { Color = new SKColor(0x17, 0x41, 0xB3), StrokeWidth = 1.4f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        SKPoint? prev = null;
        for (int i = 0; i < c.Letture.Count; i++)
        {
            var v = c.Letture[i].Valore; if (v is null) { prev = null; continue; }
            var p = new SKPoint(PX(i), PY(v.Value));
            if (prev is SKPoint pp) g.DrawLine(pp, p, linea);
            prev = p;
        }
        for (int i = 0; i < c.Letture.Count; i++)
        {
            var st = c.Letture[i].Stato;
            if (st == StatoLettura.SopraMax || st == StatoLettura.SottoMin)
            {
                using var dot = new SKPaint { Color = st == StatoLettura.SopraMax ? new SKColor(0xB3, 0x26, 0x1E) : new SKColor(0x17, 0x41, 0xB3), Style = SKPaintStyle.Fill, IsAntialias = true };
                g.DrawCircle(PX(i), PY(c.Letture[i].Valore!.Value), 2.5f, dot);
            }
        }
        using var img = SKImage.FromBitmap(bmp); using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        var path = Path.Combine(Path.GetTempPath(), "labai_tl_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static void Titolo(Section sec, string t)
    {
        var p = sec.AddParagraph(t); p.Format.Font.Bold = true; p.Format.SpaceBefore = "0.25cm"; p.Format.SpaceAfter = "0.05cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }
    private static void SetNum(IXLCell cell, double? v) { if (v.HasValue) cell.Value = v.Value; }
    private static string Data(DateTime? d) => d?.ToString("dd/MM/yyyy", It) ?? "—";
    private static string Num(double v, int dec = 1) => v.ToString("N" + dec, It);
}
