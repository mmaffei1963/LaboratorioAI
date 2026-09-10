using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Pannello grafico «Nutrition Facts» (USA, formato verticale 21 CFR 101.9) in
/// PDF e Word, con riquadro nero e regole tipografiche. I valori sono già
/// calcolati e arrotondati a monte (<see cref="NutrizioneUsa"/>).
/// </summary>
public interface IGeneratoreEtichettaUsa
{
    byte[] GeneraPdf(DatiEtichettaUsa dati);
    byte[] GeneraWord(DatiEtichettaUsa dati);
    string NomeFile(DatiEtichettaUsa dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreEtichettaUsa : IGeneratoreEtichettaUsa
{
    private const double Larghezza = 6.6;   // cm
    private const double TabDv = 6.2;       // cm (colonna % Daily Value)

    public byte[] GeneraPdf(DatiEtichettaUsa d)
    {
        var doc = Costruisci(d);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public byte[] GeneraWord(DatiEtichettaUsa d)
    {
        var doc = Costruisci(d);
        var tmp = Path.Combine(Path.GetTempPath(), "labai_nf_" + Guid.NewGuid().ToString("N") + ".rtf");
        try
        {
            new RtfDocumentRenderer().Render(doc, tmp, Path.GetTempPath());
            return File.ReadAllBytes(tmp);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public string NomeFile(DatiEtichettaUsa d, string estensione)
    {
        var nome = new string((d.Denominazione ?? "Etichetta")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Nutrition_Facts_{nome}.{estensione}";
    }

    private static Document Costruisci(DatiEtichettaUsa d)
    {
        var f = d.Facts;
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 8;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);

        // Nome prodotto sopra il riquadro (fuori dall'etichetta).
        if (!string.IsNullOrWhiteSpace(d.Denominazione))
        {
            var t = sec.AddParagraph(d.Denominazione);
            t.Format.Font.Bold = true;
            t.Format.SpaceAfter = "0.2cm";
        }

        // Riquadro nero: tabella 1x1.
        var box = sec.AddTable();
        box.Borders.Width = 2.5;
        box.Borders.Color = Colors.Black;
        box.AddColumn(Unit.FromCentimeter(Larghezza));
        var cell = box.AddRow().Cells[0];
        cell.Format.Font.Size = 8;

        // Titolo.
        var tit = cell.AddParagraph("Nutrition Facts");
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 17;
        tit.Format.SpaceAfter = "1pt";

        if (d.PorzioniPerConfezione is decimal n && n > 0)
            cell.AddParagraph($"{Num(n)} servings per container");

        var testoPorzione = string.IsNullOrWhiteSpace(d.PorzioneDescrizione)
            ? $"{Num(f.PorzioneG)} g"
            : $"{d.PorzioneDescrizione} ({Num(f.PorzioneG)}g)";
        Riga(cell, "Serving size", "", testoPorzione, true, 0, RegolaSpessa);

        cell.AddParagraph("Amount per serving").Format.Font.Size = 7;

        // Calorie (riga grande).
        var cal = cell.AddParagraph();
        cal.Format.TabStops.AddTabStop(Unit.FromCentimeter(TabDv), TabAlignment.Right);
        var cn = cal.AddFormattedText("Calories", TextFormat.Bold); cn.Size = 13;
        cal.AddTab();
        var cv = cal.AddFormattedText(f.Calorie, TextFormat.Bold); cv.Size = 20;
        Bordo(cal, RegolaMedia);

        // % Daily Value.
        var dv = cell.AddParagraph();
        dv.Format.TabStops.AddTabStop(Unit.FromCentimeter(TabDv), TabAlignment.Right);
        dv.AddTab();
        dv.AddFormattedText("% Daily Value*", TextFormat.Bold);
        Bordo(dv, RegolaSottile);

        Riga(cell, "Total Fat", f.Grassi.Quantita, Pc(f.Grassi.Dv), true, 0, RegolaSottile);
        Riga(cell, "Saturated Fat", f.GrassiSaturi.Quantita, Pc(f.GrassiSaturi.Dv), false, 1, RegolaSottile);
        Riga(cell, "Trans Fat", f.GrassiTrans, "", false, 1, RegolaSottile);
        Riga(cell, "Cholesterol", f.Colesterolo.Quantita, Pc(f.Colesterolo.Dv), true, 0, RegolaSottile);
        Riga(cell, "Sodium", f.Sodio.Quantita, Pc(f.Sodio.Dv), true, 0, RegolaSottile);
        Riga(cell, "Total Carbohydrate", f.Carboidrati.Quantita, Pc(f.Carboidrati.Dv), true, 0, RegolaSottile);
        Riga(cell, "Dietary Fiber", f.Fibre.Quantita, Pc(f.Fibre.Dv), false, 1, RegolaSottile);
        Riga(cell, "Total Sugars", f.ZuccheriTotali, "", false, 1, RegolaSottile);
        Riga(cell, $"Includes {f.ZuccheriAggiunti.Quantita} Added Sugars", "", Pc(f.ZuccheriAggiunti.Dv), false, 2, RegolaSottile);
        Riga(cell, "Protein", f.Proteine, "", true, 0, RegolaSpessa);

        Riga(cell, "Vitamin D", f.VitaminaD.Quantita, Pc(f.VitaminaD.Dv), false, 0, RegolaSottile);
        Riga(cell, "Calcium", f.Calcio.Quantita, Pc(f.Calcio.Dv), false, 0, RegolaSottile);
        Riga(cell, "Iron", f.Ferro.Quantita, Pc(f.Ferro.Dv), false, 0, RegolaSottile);
        Riga(cell, "Potassium", f.Potassio.Quantita, Pc(f.Potassio.Dv), false, 0, RegolaMedia);

        var nota = cell.AddParagraph(
            "* The % Daily Value (DV) tells you how much a nutrient in a serving of food contributes "
            + "to a daily diet. 2,000 calories a day is used for general nutrition advice.");
        nota.Format.Font.Size = 6.5;
        nota.Format.SpaceBefore = "2pt";

        // Scheda di verifica dati (pagina 2), per l'approvazione del committente.
        SchedaVerificaEtichetta.Aggiungi(sec, d.Ingredienti, d.MassaTotaleKg, d.MassaCottaKg);

        return doc;
    }

    private const double RegolaSottile = 0.5, RegolaMedia = 2, RegolaSpessa = 5;

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

        Bordo(p, regola);
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
