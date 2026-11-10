using ClosedXML.Excel;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>Excel della ricerca delle fatture di vendita per descrizione (10/11/2026).</summary>
public static class EsportatoreRicercaFatture
{
    public static byte[] Genera(IReadOnlyList<VoceDocumentoVenditaAzw> voci, DateTime dal, DateTime al, string parole)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Fatture");
        const int colonne = 11;
        var documenti = voci.Select(v => (v.TipoDocumento, v.AnnoDocumento, v.NumeroDocumento)).Distinct().Count();
        var sotto = $"Dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy} — testo: «{parole}» — {voci.Count} voci, {documenti} documenti, " +
                    $"totale imponibile € {voci.Sum(v => v.Importo):N2} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
        var riga = StileFoglio.Titolo(foglio, "Fatture di vendita per descrizione (AZW)", sotto, colonne);

        string[] intest = { "Data", "Tipo", "Numero", "Codice cliente", "Cliente", "Descrizione", "Quantità", "Prezzo", "Sconto", "Imponibile", "Centro/Voce" };
        for (var i = 0; i < intest.Length; i++) foglio.Cell(riga, i + 1).Value = intest[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var prima = riga + 1; var r = prima;
        foreach (var v in voci)
        {
            foglio.Cell(r, 1).Value = v.DataDocumento; foglio.Cell(r, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            foglio.Cell(r, 2).Value = v.TipoDocumento == "CE" ? "Nota di credito" : "Fattura";
            foglio.Cell(r, 3).Value = $"{v.NumeroDocumento}/{v.AnnoDocumento}";
            foglio.Cell(r, 4).Value = v.CodiceCliente;
            foglio.Cell(r, 5).Value = v.RagioneSociale ?? "";
            foglio.Cell(r, 6).Value = v.Descrizione;
            foglio.Cell(r, 7).Value = v.Quantita;
            foglio.Cell(r, 8).Value = v.Prezzo; foglio.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
            foglio.Cell(r, 9).Value = v.Sconto ?? "";
            foglio.Cell(r, 10).Value = v.Importo; foglio.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";
            foglio.Cell(r, 11).Value = string.Join("/", new[] { v.Centro, v.Voce }.Where(s => !string.IsNullOrWhiteSpace(s)));
            r++;
        }
        foglio.Cell(r, 9).Value = "Totale"; foglio.Cell(r, 9).Style.Font.Bold = true;
        foglio.Cell(r, 10).Value = voci.Sum(v => v.Importo); foglio.Cell(r, 10).Style.Font.Bold = true;
        foglio.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";
        StileFoglio.Rifinisci(foglio, prima, Math.Max(prima, r), colonne);
        foglio.Column(6).Width = 70; foglio.Column(6).Style.Alignment.WrapText = true;

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }
}
