using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Scheda di verifica dati (in inglese) da allegare ai pannelli USA/Canada:
/// ricetta, quantità, fonti dei valori e clausola di responsabilità, per la
/// condivisione del rischio tramite l'approvazione del committente.
/// </summary>
internal static class SchedaVerificaEtichetta
{
    private static readonly CultureInfo En = CultureInfo.InvariantCulture;

    public static void Aggiungi(Section sec, IReadOnlyList<IngredienteRapporto> ingredienti,
        decimal? massaTotale, decimal? massaCotta)
    {
        if (ingredienti is null || ingredienti.Count == 0) return;

        var tit = sec.AddParagraph("Data verification sheet");
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 13;
        tit.Format.PageBreakBefore = true;
        tit.Format.SpaceAfter = "0.15cm";

        var intro = sec.AddParagraph("Recipe, quantities and value sources used for the calculation. Please verify and approve.");
        intro.Format.Font.Size = 8.5;
        intro.Format.Font.Italic = true;
        intro.Format.SpaceAfter = "0.2cm";

        var t = sec.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = new Color(0xFFBBBBBB);
        t.Format.Font.Size = 8.5;
        foreach (var w in new[] { 6.5, 3.0, 2.0, 6.0 }) t.AddColumn(Unit.FromCentimeter(w));
        var hr = t.AddRow();
        hr.Format.Font.Bold = true;
        hr.Shading.Color = new Color(0xFFEEEEEE);
        var intest = new[] { "Ingredient", "Quantity (kg)", "%", "Source" };
        for (int i = 0; i < intest.Length; i++) hr.Cells[i].AddParagraph(intest[i]);

        foreach (var ing in ingredienti)
        {
            var row = t.AddRow();
            row.Cells[0].AddParagraph(ing.Denominazione);
            row.Cells[1].AddParagraph(ing.QuantitaKg.ToString("0.###", En));
            row.Cells[2].AddParagraph(ing.Percentuale.ToString("0.##", En));
            var fonte = string.IsNullOrWhiteSpace(ing.Fonte) ? "—" : ing.Fonte!;
            if (!string.IsNullOrWhiteSpace(ing.Riferimento)) fonte += " (" + ing.Riferimento + ")";
            row.Cells[3].AddParagraph(fonte);
        }

        if (massaTotale is decimal mt)
        {
            var m = sec.AddParagraph();
            m.Format.SpaceBefore = "0.15cm";
            m.Format.Font.Size = 8.5;
            m.AddText("Total mass " + mt.ToString("0.###", En) + " kg");
            if (massaCotta is decimal mc)
            {
                m.AddText(" · cooked mass " + mc.ToString("0.###", En) + " kg");
                if (mt > 0) m.AddText(" · weight loss " + Math.Round((mt - mc) / mt * 100m, 1).ToString("0.#", En) + " %");
            }
        }

        var clausolaTit = sec.AddParagraph("Customer's responsibility:");
        clausolaTit.Format.Font.Bold = true;
        clausolaTit.Format.SpaceBefore = "0.25cm";
        var clausola = sec.AddParagraph(TestiRapportoNutrizionale.ClausolaResponsabilitaEn);
        clausola.Format.Font.Size = 8.5;

        var appr = sec.AddParagraph("Approved by (customer): ______________________________     Date: ________________");
        appr.Format.SpaceBefore = "0.6cm";
    }
}
