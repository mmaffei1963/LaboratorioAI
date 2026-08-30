using ClosedXML.Excel;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Esporta in Excel i risultati completi (dati rapporto di prova), una riga per prova.
/// Generico: scrive le colonne così come arrivano dalla query (passthrough).
/// </summary>
public interface IEsportatoreRisultatiRdp
{
    byte[] Genera(IReadOnlyList<IDictionary<string, object>> righe);
    string NomeFile();
}

/// <inheritdoc />
public class EsportatoreRisultatiRdp : IEsportatoreRisultatiRdp
{
    public byte[] Genera(IReadOnlyList<IDictionary<string, object>> righe)
    {
        using var wb = new XLWorkbook();
        var foglio = wb.Worksheets.Add("Risultati");

        var colonne = righe.Count > 0 ? righe[0].Keys.ToList() : new List<string> { "Nessun risultato" };
        int nc = colonne.Count;

        var riga = StileFoglio.Titolo(foglio, "Risultati completi (rapporto di prova)",
            $"Estratto il {DateTime.Now:dd/MM/yyyy HH:mm} · {righe.Count} righe (una per prova)", nc);

        for (int i = 0; i < nc; i++) foglio.Cell(riga, i + 1).Value = colonne[i];
        StileFoglio.Intestazione(foglio, riga, nc, StileFoglio.Rosso);

        int r = riga + 1;
        foreach (var row in righe)
        {
            for (int c = 0; c < nc; c++)
            {
                row.TryGetValue(colonne[c], out var v);
                ImpostaCella(foglio.Cell(r, c + 1), v);
            }
            r++;
        }

        StileFoglio.Rifinisci(foglio, riga, r - 1, nc);
        if (righe.Count > 0) foglio.Range(riga, 1, r - 1, nc).SetAutoFilter();

        using var flusso = new MemoryStream();
        wb.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile() => $"RisultatiRDP_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

    private static void ImpostaCella(IXLCell cell, object? v)
    {
        switch (v)
        {
            case null or DBNull:
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd/MM/yyyy";
                break;
            case bool b:
                cell.Value = b ? "Sì" : "NO";
                break;
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                cell.Value = Convert.ToDouble(v);
                break;
            default:
                cell.Value = v.ToString();
                break;
        }
    }
}
