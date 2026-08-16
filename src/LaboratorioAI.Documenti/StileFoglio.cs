using ClosedXML.Excel;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Formattazione comune ai fogli di calcolo prodotti dal sistema,
/// per dare uniformità visiva alle esportazioni.
/// </summary>
internal static class StileFoglio
{
    public static readonly XLColor Rosso  = XLColor.FromHtml("#C00000");
    public static readonly XLColor Scuro  = XLColor.FromHtml("#1A1A1A");
    public static readonly XLColor Chiaro = XLColor.FromHtml("#F4F4F4");

    /// <summary>Applica lo stile di intestazione alla riga indicata.</summary>
    public static void Intestazione(IXLWorksheet foglio, int riga, int colonne, XLColor? sfondo = null)
    {
        var intervallo = foglio.Range(riga, 1, riga, colonne);
        intervallo.Style.Fill.BackgroundColor = sfondo ?? Scuro;
        intervallo.Style.Font.FontColor = XLColor.White;
        intervallo.Style.Font.Bold = true;
        intervallo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        intervallo.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        foglio.Row(riga).Height = 22;
        foglio.SheetView.FreezeRows(riga);
    }

    /// <summary>Titolo del foglio, con l'indicazione del periodo estratto.</summary>
    public static int Titolo(IXLWorksheet foglio, string testo, string sottotitolo, int colonne)
    {
        foglio.Cell(1, 1).Value = testo;
        foglio.Range(1, 1, 1, colonne).Merge();
        foglio.Cell(1, 1).Style.Font.FontSize = 14;
        foglio.Cell(1, 1).Style.Font.Bold = true;
        foglio.Cell(1, 1).Style.Font.FontColor = Rosso;

        foglio.Cell(2, 1).Value = sottotitolo;
        foglio.Range(2, 1, 2, colonne).Merge();
        foglio.Cell(2, 1).Style.Font.FontSize = 9;
        foglio.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;

        return 4;   // prima riga utile per l'intestazione
    }

    /// <summary>Bordi leggeri e larghezze automatiche sull'area dati.</summary>
    public static void Rifinisci(IXLWorksheet foglio, int primaRiga, int ultimaRiga, int colonne)
    {
        if (ultimaRiga < primaRiga) return;

        var area = foglio.Range(primaRiga, 1, ultimaRiga, colonne);
        area.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        area.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        area.Style.Border.InsideBorderColor = XLColor.FromHtml("#DDDDDD");

        foglio.Columns(1, colonne).AdjustToContents(primaRiga, ultimaRiga, 10, 55);
    }
}
