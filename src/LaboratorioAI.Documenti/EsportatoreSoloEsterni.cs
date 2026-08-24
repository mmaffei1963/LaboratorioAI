using ClosedXML.Excel;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel il confronto "Solo esterni".</summary>
public interface IEsportatoreSoloEsterni
{
    byte[] Genera(IReadOnlyList<RigaSoloEsterni> righe);
    string NomeFile();
}

/// <inheritdoc />
public class EsportatoreSoloEsterni : IEsportatoreSoloEsterni
{
    private static readonly string[] Intestazioni =
    {
        "Anno", "Lotto", "Campione", "Cliente", "Data upload", "Nome file esterno",
        "Ha interno", "N° certificato", "Data fattura", "Fatturato",
        "Id controllo", "Controllo", "Prezzo €", "Campione ArcLab"
    };

    public byte[] Genera(IReadOnlyList<RigaSoloEsterni> righe)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Solo esterni");
        int colonne = Intestazioni.Length;

        var riga = StileFoglio.Titolo(foglio, "Solo esterni — confronto certificati",
            $"Estratto il {DateTime.Now:dd/MM/yyyy HH:mm} · {righe.Count} righe", colonne);

        for (int i = 0; i < colonne; i++) foglio.Cell(riga, i + 1).Value = Intestazioni[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var r = riga + 1;
        foreach (var x in righe)
        {
            foglio.Cell(r, 1).Value = x.Anno;
            foglio.Cell(r, 2).Value = x.Lotto;
            foglio.Cell(r, 3).Value = x.Campione;
            foglio.Cell(r, 4).Value = x.RagioneSociale;
            foglio.Cell(r, 5).Value = x.DataUpload;
            foglio.Cell(r, 6).Value = x.NomeFile;
            foglio.Cell(r, 7).Value = x.HaInterno ? "Sì" : "NO";
            foglio.Cell(r, 8).Value = x.NCertificato;
            if (x.DataFattura is DateTime df) foglio.Cell(r, 9).Value = df;
            foglio.Cell(r, 10).Value = x.Fatturato ? "Sì" : "NO";
            foglio.Cell(r, 11).Value = x.IdControllo;
            foglio.Cell(r, 12).Value = x.Controllo;
            if (x.PrezzoControllo is decimal pz) foglio.Cell(r, 13).Value = pz;
            foglio.Cell(r, 14).Value = x.IdCampione.HasValue ? "Sì" : "NO";
            r++;
        }

        foglio.Column(5).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(9).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(13).Style.NumberFormat.Format = "#,##0.00";
        StileFoglio.Rifinisci(foglio, riga, r - 1, colonne);
        if (righe.Count > 0) foglio.Range(riga, 1, r - 1, colonne).SetAutoFilter();

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile() => $"SoloEsterni_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
}
