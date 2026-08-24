using System.Globalization;
using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Genera l'Excel delle etichette termometri, replicando il blocchetto del foglio 2020
/// (Cod.Termometro / N°Rapporto taratura + Del / Scadenza taratura) con sfondo colorato per anno.</summary>
public interface IGeneratoreEtichetteTermometri
{
    byte[] Genera(IReadOnlyList<RigaEtichetta> righe);
}

/// <inheritdoc />
public class GeneratoreEtichetteTermometri : IGeneratoreEtichetteTermometri
{
    private const string Font = "Abadi MT Condensed Light";
    private static readonly double[] Larghezze = { 8.43, 5.43, 6.86, 4.29, 8.57 };

    // Ciclo colori per anno (sfondo scuro, testo bianco, scadenza gialla).
    private static readonly string[] Ciclo =
        { "A0392B", "2980B9", "1A3E8C", "2E7D32", "B5651D", "6A3D9A" };

    private static XLColor ColoreAnno(int anno)
    {
        int i = ((anno - 2026) % Ciclo.Length + Ciclo.Length) % Ciclo.Length;
        return XLColor.FromHtml("#" + Ciclo[i]);
    }

    private static string Data(DateTime? d) => d?.ToString("dd/MM/yyyy", CultureInfo.GetCultureInfo("it-IT")) ?? "";

    public byte[] Genera(IReadOnlyList<RigaEtichetta> righe)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Etichette");
        for (int c = 0; c < Larghezze.Length; c++) ws.Column(c + 1).Width = Larghezze[c];

        int r = 2;
        foreach (var e in righe)
        {
            Etichetta(ws, r, e);
            r += 4; // 3 righe etichetta + 1 riga vuota di separazione
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Etichetta(IXLWorksheet ws, int top, RigaEtichetta e)
    {
        var sfondo = ColoreAnno(e.AnnoColore);
        // Sfondo pieno + testo bianco su tutte le 3 righe, colonne A–E
        var blocco = ws.Range(top, 1, top + 2, 5);
        blocco.Style.Fill.SetBackgroundColor(sfondo);
        blocco.Style.Font.SetFontName(Font);
        blocco.Style.Font.SetFontSize(10);
        blocco.Style.Font.SetFontColor(XLColor.White);

        // Riga 1: Cod.Termometro: <codice>
        ws.Cell(top, 3).Value = "Cod.Termometro:";
        ws.Cell(top, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        var cod = ws.Cell(top, 5); cod.Value = e.Codice;
        cod.Style.Font.Bold = true; cod.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Riga 2: <etichetta rapporto> <valore>   Del: <data>
        ws.Cell(top + 1, 1).Value = e.RigaRapportoEtichetta;
        var val = ws.Cell(top + 1, 3); val.Value = e.RigaRapportoValore;
        val.Style.Font.Bold = true; val.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        ws.Cell(top + 1, 4).Value = "Del:";
        ws.Cell(top + 1, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        var del = ws.Cell(top + 1, 5);
        del.Value = Data(e.DataTaratura);   // testo: non mostra ### in colonna stretta
        del.Style.Font.Bold = true; del.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        // Riga 3: Scadenza taratura: <data> (in giallo)
        ws.Cell(top + 2, 1).Value = "Scadenza taratura:";
        var scad = ws.Cell(top + 2, 3);
        scad.Value = Data(e.Scadenza);
        scad.Style.Font.Bold = true; scad.Style.Font.FontColor = XLColor.FromHtml("#FFF200");
        scad.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
    }
}
