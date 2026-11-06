using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Excel dei campioni inviati a un laboratorio esterno (06/11/2026), con le colonne della query Access di MARE.A.
/// Una riga per campione e prova; le aliquote della stessa prova (stessa denominazione e metodo) diventano una riga.
/// </summary>
public static class EsportatoreInvioLaboratorioEsterno
{
    public static string NomeFile(string laboratorio, DateTime dataInvio)
    {
        var slug = new string(laboratorio.Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray()).Trim().Replace(' ', '_');
        if (slug.Length > 40) slug = slug[..40];
        return $"MAREA_Campioni_{slug}_{dataInvio:yyyyMMdd_HHmm}.xlsx";
    }

    /// <summary>Righe uniche dell'Excel (campione + prova + metodo), nell'ordine di data analisi.</summary>
    public static IReadOnlyList<ProvaDaInviare> Righe(IEnumerable<ProvaDaInviare> prove) =>
        prove.GroupBy(p => (p.NumeroCampione, p.Prova ?? "", p.RifBiblio ?? "", p.RifMetodo ?? ""))
             .Select(g => g.First())
             .OrderBy(p => p.DataAnalisi).ThenBy(p => p.NumeroCampione).ThenBy(p => p.Prova)
             .ToList();

    public static byte[] Genera(IEnumerable<ProvaDaInviare> prove, string laboratorio, DateTime dal, DateTime al)
    {
        var righe = Righe(prove);
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Campioni");
        const int colonne = 11;
        var titolo = $"Campioni inviati da MARE.A s.r.l. a {laboratorio}";
        var sotto = $"Data analisi dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy} — {righe.Select(r => r.NumeroCampione).Distinct().Count()} campioni, {righe.Count} prove";
        var riga = StileFoglio.Titolo(foglio, titolo, sotto, colonne);

        string[] intest = { "Cliente / Insediamento", "Lotto/Campione", "Denominazione campione", "Prova", "Rif. bibliografico",
                            "Metodo", "Data analisi", "Temp. prelievo", "Data ricevimento", "Ora ricevimento", "Laboratorio esterno" };
        for (var i = 0; i < intest.Length; i++) foglio.Cell(riga, i + 1).Value = intest[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var prima = riga + 1;
        var r = prima;
        foreach (var p in righe)
        {
            foglio.Cell(r, 1).Value = p.ClienteInsediamento?.Trim() ?? "";
            foglio.Cell(r, 2).Value = p.NumeroCampione;
            foglio.Cell(r, 3).Value = p.DenominazioneCampione?.Trim() ?? "";
            foglio.Cell(r, 4).Value = p.Prova?.Trim() ?? "";
            foglio.Cell(r, 5).Value = p.RifBiblio?.Trim() ?? "";
            foglio.Cell(r, 6).Value = p.RifMetodo?.Trim() ?? "";
            if (p.DataAnalisi is DateTime da) { foglio.Cell(r, 7).Value = da; foglio.Cell(r, 7).Style.DateFormat.Format = "dd/mm/yyyy"; }
            foglio.Cell(r, 8).Value = p.TemperaturaPrelievo?.Trim() ?? "";
            if (p.DataRicezione is DateTime dr) { foglio.Cell(r, 9).Value = dr.Date; foglio.Cell(r, 9).Style.DateFormat.Format = "dd/mm/yyyy"; }
            if (p.OraRicezione is TimeSpan ora) foglio.Cell(r, 10).Value = ora.ToString(@"hh\:mm");
            foglio.Cell(r, 11).Value = p.LabEsterno?.Trim() ?? laboratorio;
            r++;
        }
        StileFoglio.Rifinisci(foglio, prima, Math.Max(prima, r - 1), colonne);

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }
}
