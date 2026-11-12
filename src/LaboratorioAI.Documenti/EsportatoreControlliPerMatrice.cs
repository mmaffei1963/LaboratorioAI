using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Excel della vista «Controlli per matrice» di Metodi e Controlli (12/11/2026).</summary>
public static class EsportatoreControlliPerMatrice
{
    public static byte[] Genera(IReadOnlyList<ControlloPerMatrice> righe, IReadOnlyList<string> matrici, DateTime dal, DateTime al, bool conPrezzi)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Controlli");
        var colonne = conPrezzi ? 8 : 5;
        var sotto = $"Matrici: {string.Join(", ", matrici)} — campioni ricevuti dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}, " +
                    "solo commesse di laboratorio attive con campioni" + (conPrezzi ? " — medio pesato per cliente (ogni cliente conta uno)" : "");
        var riga = StileFoglio.Titolo(foglio, "Controlli per matrice", sotto, colonne);

        var intest = new List<string> { "ID", "Controllo", "Campioni", "Clienti", "Commesse" };
        if (conPrezzi) intest.AddRange(new[] { "Prezzo minimo", "Prezzo medio pesato", "Prezzo massimo" });
        for (var i = 0; i < intest.Count; i++) foglio.Cell(riga, i + 1).Value = intest[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var prima = riga + 1; var r = prima;
        foreach (var c in righe)
        {
            foglio.Cell(r, 1).Value = c.IdControllo;
            foglio.Cell(r, 2).Value = c.Controllo;
            foglio.Cell(r, 3).Value = c.Campioni;
            foglio.Cell(r, 4).Value = c.Clienti;
            foglio.Cell(r, 5).Value = c.Commesse;
            if (conPrezzi)
            {
                Prezzo(r, 6, c.PrezzoMinimo); Prezzo(r, 7, c.PrezzoMedio); Prezzo(r, 8, c.PrezzoMassimo);
            }
            r++;
        }
        StileFoglio.Rifinisci(foglio, prima, Math.Max(prima, r - 1), colonne);
        foglio.Column(2).Width = 60;

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();

        void Prezzo(int rr, int col, decimal? v)
        {
            if (v is not decimal d) return;
            foglio.Cell(rr, col).Value = d;
            foglio.Cell(rr, col).Style.NumberFormat.Format = "#,##0.00 €";
        }
    }
}
