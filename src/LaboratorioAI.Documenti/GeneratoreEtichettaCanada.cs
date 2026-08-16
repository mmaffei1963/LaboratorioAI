using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Pannello grafico bilingue «Nutrition Facts / Valeur nutritive» (Canada, FDR)
/// in PDF e Word. Valori già calcolati e arrotondati (<see cref="NutrizioneCanada"/>).
/// </summary>
public interface IGeneratoreEtichettaCanada
{
    byte[] GeneraPdf(DatiEtichettaCanada dati);
    byte[] GeneraWord(DatiEtichettaCanada dati);
    string NomeFile(DatiEtichettaCanada dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreEtichettaCanada : IGeneratoreEtichettaCanada
{
    private const double Larghezza = 7.4;   // cm (più larga: bilingue)
    private const double TabDv = 7.0;

    public byte[] GeneraPdf(DatiEtichettaCanada d)
    {
        var doc = Costruisci(d);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public byte[] GeneraWord(DatiEtichettaCanada d)
    {
        var doc = Costruisci(d);
        var tmp = Path.Combine(Path.GetTempPath(), "labai_nfca_" + Guid.NewGuid().ToString("N") + ".rtf");
        try
        {
            new RtfDocumentRenderer().Render(doc, tmp, Path.GetTempPath());
            return File.ReadAllBytes(tmp);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public string NomeFile(DatiEtichettaCanada d, string estensione)
    {
        var nome = new string((d.Denominazione ?? "Etichetta")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Valeur_Nutritive_{nome}.{estensione}";
    }

    private const double RegolaSottile = 0.5, RegolaMedia = 2, RegolaSpessa = 5;

    private static Document Costruisci(DatiEtichettaCanada d)
    {
        var f = d.Facts;
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 7.5;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);

        if (!string.IsNullOrWhiteSpace(d.Denominazione))
        {
            var t = sec.AddParagraph(d.Denominazione);
            t.Format.Font.Bold = true;
            t.Format.SpaceAfter = "0.2cm";
        }

        var box = sec.AddTable();
        box.Borders.Width = 2.5;
        box.Borders.Color = Colors.Black;
        box.AddColumn(Unit.FromCentimeter(Larghezza));
        var cell = box.AddRow().Cells[0];
        cell.Format.Font.Size = 7.5;

        var tit = cell.AddParagraph("Nutrition Facts");
        tit.Format.Font.Bold = true; tit.Format.Font.Size = 15; tit.Format.SpaceAfter = "0pt";
        var tit2 = cell.AddParagraph("Valeur nutritive");
        tit2.Format.Font.Bold = true; tit2.Format.Font.Size = 15; tit2.Format.SpaceAfter = "1pt";

        if (d.PorzioniPerConfezione is decimal n && n > 0)
            cell.AddParagraph($"{Num(n)} servings per container / portions par contenant");

        Riga(cell, "Serving size / portion", "", $"{Num(f.PorzioneG)} g", true, 0, RegolaSpessa);

        var cal = cell.AddParagraph();
        cal.Format.TabStops.AddTabStop(Unit.FromCentimeter(TabDv), TabAlignment.Right);
        var cn = cal.AddFormattedText("Calories", TextFormat.Bold); cn.Size = 12;
        cal.AddTab();
        var cv = cal.AddFormattedText(f.Calorie, TextFormat.Bold); cv.Size = 18;
        Bordo(cal, RegolaMedia);

        var dv = cell.AddParagraph();
        dv.Format.TabStops.AddTabStop(Unit.FromCentimeter(TabDv), TabAlignment.Right);
        dv.AddTab();
        dv.AddFormattedText("% Daily Value* / % valeur quotidienne*", TextFormat.Bold);
        Bordo(dv, RegolaSottile);

        Riga(cell, "Fat / Lipides", f.Grassi.Quantita, Pc(f.Grassi.Dv), true, 0, RegolaSottile);
        Riga(cell, "Saturated / saturés", f.GrassiSaturi, Pc(f.SaturiTransDv), false, 1, 0);
        Riga(cell, "+ Trans / trans", f.GrassiTrans, "", false, 1, RegolaSottile);
        Riga(cell, "Cholesterol / Cholestérol", f.Colesterolo, "", true, 0, RegolaSottile);
        Riga(cell, "Sodium / Sodium", f.Sodio.Quantita, Pc(f.Sodio.Dv), true, 0, RegolaSottile);
        Riga(cell, "Carbohydrate / Glucides", f.Carboidrati, "", true, 0, RegolaSottile);
        Riga(cell, "Fibre / Fibres", f.Fibre.Quantita, Pc(f.Fibre.Dv), false, 1, RegolaSottile);
        Riga(cell, "Sugars / Sucres", f.Zuccheri.Quantita, Pc(f.Zuccheri.Dv), false, 1, RegolaSottile);
        Riga(cell, "Protein / Protéines", f.Proteine, "", true, 0, RegolaSpessa);

        Riga(cell, "Potassium / Potassium", f.Potassio.Quantita, Pc(f.Potassio.Dv), false, 0, RegolaSottile);
        Riga(cell, "Calcium / Calcium", f.Calcio.Quantita, Pc(f.Calcio.Dv), false, 0, RegolaSottile);
        Riga(cell, "Iron / Fer", f.Ferro.Quantita, Pc(f.Ferro.Dv), false, 0, RegolaMedia);

        var nota = cell.AddParagraph("* 5% or less is a little, 15% or more is a lot / "
            + "5% ou moins c'est peu, 15% ou plus c'est beaucoup");
        nota.Format.Font.Size = 6.5;
        nota.Format.SpaceBefore = "2pt";

        return doc;
    }

    private static void Riga(Cell cell, string nome, string quantita, string dv,
        bool nomeBold, int livello, double regola)
    {
        var p = cell.AddParagraph();
        p.Format.TabStops.AddTabStop(Unit.FromCentimeter(TabDv), TabAlignment.Right);
        if (livello > 0) p.Format.LeftIndent = Unit.FromCentimeter(0.35 * livello);
        p.Format.SpaceBefore = "1pt";
        p.Format.SpaceAfter = "1pt";
        p.AddFormattedText(nome, nomeBold ? TextFormat.Bold : TextFormat.NotBold);
        if (!string.IsNullOrEmpty(quantita)) p.AddText(" " + quantita);
        p.AddTab();
        if (!string.IsNullOrEmpty(dv)) p.AddFormattedText(dv, TextFormat.Bold);
        if (regola > 0) Bordo(p, regola);
    }

    private static void Bordo(Paragraph p, double punti)
    {
        p.Format.Borders.Bottom.Width = Unit.FromPoint(punti);
        p.Format.Borders.Bottom.Color = Colors.Black;
    }

    private static string Pc(int? dv) => dv.HasValue ? dv.Value + "%" : "";

    private static string Num(decimal v) =>
        v == Math.Truncate(v) ? ((long)v).ToString(System.Globalization.CultureInfo.InvariantCulture)
                              : v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
}
