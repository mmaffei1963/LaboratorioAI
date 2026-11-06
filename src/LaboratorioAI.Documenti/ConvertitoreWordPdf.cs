using DocumentFormat.OpenXml.Packaging;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Conversione semplificata di un documento Word (generato dal programma) in PDF con MigraDoc, su carta intestata
/// MARE.A: paragrafi con grassetto, corsivo, dimensione e colore, titoli, elenchi, sfondi, tabelle con bordi e
/// celle unite. Usata per deleghe operative e contratti di consulenza, così il PDF segue sempre il Word.
/// </summary>
internal static class ConvertitoreWordPdf
{
    /// <summary>PDF dal Word: paragrafi, titoli, elenchi, tabelle, sfondi; carta intestata da wwwroot/img.</summary>
    public static byte[] Converti(byte[] word, string? percorsoWwwRoot, string prefissoPagina = "Pagina ")
    {
        FontResolverLabAI.Registra();
        using var ms = new MemoryStream(word);
        using var w = WordprocessingDocument.Open(ms, false);
        var body = w.MainDocumentPart?.Document.Body ?? throw new InvalidOperationException("Documento Word vuoto.");

        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = DimensioneBase(w);

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);

        // Carta intestata MARE.A, come Ordini di Accettazione e rapportini.
        var headerImg = Immagine(percorsoWwwRoot, "intestazione_marea_pulita.png") ?? Immagine(percorsoWwwRoot, "intestazione carta intestata MAREA.png");
        var footerImg = Immagine(percorsoWwwRoot, "Piede carta intestata MAREA.png");
        bool cartaIntestata = headerImg is not null || footerImg is not null;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(cartaIntestata ? 3.6 : 1.5);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(cartaIntestata ? 2.4 : 1.5);
        sec.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
        sec.PageSetup.FooterDistance = Unit.FromCentimeter(0.4);
        if (headerImg is not null)
        {
            var hp = sec.Headers.Primary.AddParagraph();
            hp.Format.Alignment = ParagraphAlignment.Center;
            var img = hp.AddImage(headerImg);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(16.5);
        }
        if (footerImg is not null)
        {
            var fp = sec.Footers.Primary.AddParagraph();
            fp.Format.Alignment = ParagraphAlignment.Center;
            var img = fp.AddImage(footerImg);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(17);
        }

        var pag = sec.Footers.Primary.AddParagraph();
        pag.Format.Alignment = ParagraphAlignment.Center;
        pag.Format.Font.Size = 7;
        pag.AddText(prefissoPagina);
        pag.AddPageField();
        pag.AddText(" di ");
        pag.AddNumPagesField();

        var larghezza = 21.0 - sec.PageSetup.LeftMargin.Centimeter - sec.PageSetup.RightMargin.Centimeter;
        foreach (var el in body.ChildElements)
        {
            switch (el)
            {
                case W.Paragraph p: Paragrafo(p, sec.AddParagraph()); break;
                case W.Table t: Tabella(t, sec, larghezza); break;
            }
        }

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var outMs = new MemoryStream();
        renderer.PdfDocument.Save(outMs, false);
        return outMs.ToArray();
    }

    private static double DimensioneBase(WordprocessingDocument w)
    {
        var sz = w.MainDocumentPart?.StyleDefinitionsPart?.Styles?.DocDefaults?.RunPropertiesDefault?
            .RunPropertiesBaseStyle?.FontSize?.Val?.Value;
        return double.TryParse(sz, out var hp) && hp > 0 ? Math.Min(hp / 2.0, 11) : 10;
    }

    private static string? Immagine(string? www, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(www)) return null;
        var full = Path.Combine(www, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }

    private static void Paragrafo(W.Paragraph p, Paragraph dest)
    {
        var pPr = p.ParagraphProperties;
        var stile = pPr?.ParagraphStyleId?.Val?.Value ?? "";
        var elenco = pPr?.NumberingProperties is not null;

        if (stile.StartsWith("Titolo1", StringComparison.OrdinalIgnoreCase) || stile.StartsWith("Heading1", StringComparison.OrdinalIgnoreCase))
        { dest.Format.Font.Bold = true; dest.Format.Font.Size = 13; dest.Format.SpaceBefore = "10pt"; dest.Format.KeepWithNext = true; }
        else if (stile.StartsWith("Titolo2", StringComparison.OrdinalIgnoreCase) || stile.StartsWith("Heading2", StringComparison.OrdinalIgnoreCase))
        { dest.Format.Font.Bold = true; dest.Format.Font.Size = 11.5; dest.Format.Font.Color = Blu; dest.Format.SpaceBefore = "10pt"; dest.Format.SpaceAfter = "4pt"; dest.Format.KeepWithNext = true; }
        else if (stile.StartsWith("Titolo3", StringComparison.OrdinalIgnoreCase) || stile.StartsWith("Heading3", StringComparison.OrdinalIgnoreCase))
        { dest.Format.Font.Bold = true; dest.Format.SpaceBefore = "7pt"; dest.Format.SpaceAfter = "3pt"; dest.Format.KeepWithNext = true; }

        var sp = pPr?.SpacingBetweenLines;
        if (sp?.Before?.Value is string b && int.TryParse(b, out var bv)) dest.Format.SpaceBefore = Unit.FromPoint(bv / 20.0);
        if (sp?.After?.Value is string a && int.TryParse(a, out var av)) dest.Format.SpaceAfter = Unit.FromPoint(Math.Min(av / 20.0, 12));
        else if (dest.Format.SpaceAfter.IsEmpty) dest.Format.SpaceAfter = "3pt";

        if (ColoreDa(pPr?.Shading?.Fill?.Value) is Color sfondo) dest.Format.Shading.Color = sfondo;

        var jc = pPr?.Justification?.Val?.Value;
        if (jc == W.JustificationValues.Center) dest.Format.Alignment = ParagraphAlignment.Center;
        else if (jc == W.JustificationValues.Right) dest.Format.Alignment = ParagraphAlignment.Right;
        else if (jc == W.JustificationValues.Both) dest.Format.Alignment = ParagraphAlignment.Justify;

        if (elenco)
        {
            dest.Format.LeftIndent = "0.7cm";
            dest.Format.FirstLineIndent = "-0.4cm";
            dest.Format.Alignment = ParagraphAlignment.Justify;
            dest.AddText("•");
            dest.AddTab();
            dest.Format.TabStops.AddTabStop("0.7cm");
        }

        foreach (var r in p.Descendants<W.Run>())
        {
            var rPr = r.RunProperties;
            var bold = rPr?.Bold is W.Bold bb && (bb.Val is null || bb.Val.Value);
            var italic = rPr?.Italic is W.Italic ii && (ii.Val is null || ii.Val.Value);
            double? size = double.TryParse(rPr?.FontSize?.Val?.Value, out var hs) ? hs / 2.0 : null;
            Color? colore = ColoreDa(rPr?.Color?.Val?.Value);
            foreach (var c in r.ChildElements)
            {
                switch (c)
                {
                    case W.Text t when t.Text.Length > 0:
                        var ft = dest.AddFormattedText(t.Text);
                        if (bold) ft.Bold = true;
                        if (italic) ft.Italic = true;
                        if (size is double s) ft.Size = s;
                        if (colore is Color col) ft.Color = col;
                        break;
                    case W.Break:
                        dest.AddLineBreak();
                        break;
                    case W.TabChar:
                        dest.AddTab();
                        break;
                }
            }
        }
    }

    private static readonly Color Blu = new(0xFF003399);

    private static Color? ColoreDa(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex == "auto" || hex.Length != 6) return null;
        return uint.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var v) ? new Color(0xFF000000 | v) : null;
    }

    private static void Tabella(W.Table t, Section sec, double larghezzaCm)
    {
        var colonne = t.GetFirstChild<W.TableGrid>()?.Elements<W.GridColumn>()
            .Select(g => int.TryParse(g.Width?.Value, out var v) ? v / 567.0 : 0).ToList() ?? new List<double>();
        if (colonne.Count == 0) return;
        var totale = colonne.Sum();
        if (totale > larghezzaCm) colonne = colonne.Select(c => c * larghezzaCm / totale).ToList();

        var tab = sec.AddTable();
        tab.Format.SpaceAfter = "0pt";
        var bordi = t.GetFirstChild<W.TableProperties>()?.TableBorders;
        var conBordi = bordi is not null && bordi.ChildElements.OfType<W.BorderType>()
            .Any(b => b.Val is not null && b.Val.Value != W.BorderValues.None && b.Val.Value != W.BorderValues.Nil);
        tab.Borders.Width = conBordi ? 0.5 : 0;
        tab.Borders.Color = new Color(0xFFBBBBBB);
        tab.TopPadding = "2pt";
        tab.BottomPadding = "2pt";
        foreach (var c in colonne) tab.AddColumn(Unit.FromCentimeter(c));
        if (t.GetFirstChild<W.TableProperties>()?.TableJustification?.Val?.Value == W.TableRowAlignmentValues.Center)
            tab.Rows.Alignment = RowAlignment.Center;

        foreach (var tr in t.Elements<W.TableRow>())
        {
            var row = tab.AddRow();
            var idx = 0;
            foreach (var tc in tr.Elements<W.TableCell>())
            {
                if (idx >= colonne.Count) break;
                var span = tc.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                var cell = row.Cells[idx];
                if (span > 1) cell.MergeRight = Math.Min(span, colonne.Count - idx) - 1;
                cell.VerticalAlignment = VerticalAlignment.Center;
                if (ColoreDa(tc.TableCellProperties?.Shading?.Fill?.Value) is Color sf) cell.Shading.Color = sf;
                foreach (var p in tc.Elements<W.Paragraph>()) Paragrafo(p, cell.AddParagraph());
                idx += span;
            }
        }
        sec.AddParagraph().Format.SpaceAfter = "2pt";
    }
}
