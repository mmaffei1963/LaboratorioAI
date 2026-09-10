using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Portale.Servizi;

/// <summary>Esporta in Excel l'elenco dei campioni mostrato al cliente nel portale.</summary>
public static class EsportatoreCampioniPortale
{
    public static byte[] Genera(IReadOnlyList<CampionePortale> campioni)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Campioni");

        var intest = new[]
        {
            "Stato", "Numero", "Cliente", "Insediamento", "Denominazione", "Matrice", "Controllo",
            "Luogo prelievo", "Punto prelievo", "Verbale prelievo", "Data ricezione", "Data prelievo"
        };
        for (int i = 0; i < intest.Length; i++)
        {
            var c = ws.Cell(1, i + 1);
            c.Value = intest[i];
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3864");
            c.Style.Font.FontColor = XLColor.White;
        }

        int r = 1;
        foreach (var x in campioni)
        {
            r++;
            ws.Cell(r, 1).Value = x.Stato.ToString();
            ws.Cell(r, 2).Value = x.NumeroCampione;
            ws.Cell(r, 3).Value = x.RagioneSociale ?? "";
            ws.Cell(r, 4).Value = x.NomeInsediamento ?? "";
            ws.Cell(r, 5).Value = x.DenominazioneCampione ?? "";
            ws.Cell(r, 6).Value = x.Matrice ?? "";
            ws.Cell(r, 7).Value = x.Controllo ?? "";
            ws.Cell(r, 8).Value = x.LuogoPrelievo ?? "";
            ws.Cell(r, 9).Value = x.PuntoPrelievo ?? "";
            ws.Cell(r, 10).Value = x.VerbalePrelievo ?? "";
            if (x.DataRicezione is DateTime dr)
            {
                ws.Cell(r, 11).Value = dr;
                ws.Cell(r, 11).Style.DateFormat.Format = "dd/MM/yyyy";
            }
            if (x.DataPrelievo is DateTime dp)
            {
                ws.Cell(r, 12).Value = dp;
                ws.Cell(r, 12).Style.DateFormat.Format = "dd/MM/yyyy";
            }
        }

        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
