using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Excel delle anomalie delle offerte (17/11/2026): una riga per commessa coinvolta.</summary>
public static class EsportatoreAnomalieOfferte
{
    public static byte[] Genera(IReadOnlyList<AnomaliaOfferta> anomalie)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Anomalie offerte");
        const int colonne = 11;
        var riga = StileFoglio.Titolo(foglio, "Anomalie offerte",
            $"Stesso controllo in insediamenti diversi: B prezzo uguale e descrizione diversa, D prezzo diverso e descrizione uguale — situazione al {DateTime.Now:dd/MM/yyyy HH:mm}", colonne);
        var intest = new[] { "Caso", "Cliente", "Cod. contab.", "Id cliente", "Cod. controllo", "Controllo", "Insediamento", "Id commessa", "Descrizione incarico", "Prezzo", "Data commessa" };
        for (var i = 0; i < intest.Length; i++) foglio.Cell(riga, i + 1).Value = intest[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);
        var prima = riga + 1; var r = prima;
        foreach (var a in anomalie)
            foreach (var c in a.Commesse)
            {
                foglio.Cell(r, 1).Value = a.Caso;
                foglio.Cell(r, 2).Value = a.Cliente;
                foglio.Cell(r, 3).Value = a.CodContabilita;
                foglio.Cell(r, 4).Value = a.IdCliente;
                foglio.Cell(r, 5).Value = a.IdControllo;
                foglio.Cell(r, 6).Value = a.Controllo;
                foglio.Cell(r, 7).Value = c.Insediamento;
                foglio.Cell(r, 8).Value = c.IdCommessa;
                foglio.Cell(r, 9).Value = c.Incarico;
                foglio.Cell(r, 10).Value = c.Prezzo;
                foglio.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00 €";
                if (c.DataCommessa is DateTime d) { foglio.Cell(r, 11).Value = d; foglio.Cell(r, 11).Style.DateFormat.Format = "dd/mm/yyyy"; }
                r++;
            }
        StileFoglio.Rifinisci(foglio, prima, Math.Max(prima, r - 1), colonne);
        foglio.Column(9).Width = 50;
        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }
}
