using ClosedXML.Excel;
using LaboratorioAI.Dati.MonitorTemperature.Models;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Esporta in Excel una tabella storico del modulo Monitor Temperature
/// (Econorma). Usato sia per i dati grezzi (Misure) sia per i dati
/// corretti (Misure1): il chiamante passa già l'elenco filtrato come
/// visualizzato a video.
/// </summary>
public interface IEsportatoreMonitorTemperature
{
    byte[] Genera(IReadOnlyList<StoricoTemperatura> dati, string titolo, string sottotitolo);
    byte[] GeneraDifferenze(IReadOnlyList<DifferenzaTemperatura> dati, string titolo, string sottotitolo);
    string NomeFile(string prefisso);
}

/// <inheritdoc />
public class EsportatoreMonitorTemperature : IEsportatoreMonitorTemperature
{
    public byte[] Genera(IReadOnlyList<StoricoTemperatura> dati, string titolo, string sottotitolo)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Storico");
        const int colonne = 9;

        var riga = StileFoglio.Titolo(foglio, titolo, sottotitolo, colonne);

        foglio.Cell(riga, 1).Value = "Data/Ora";
        foglio.Cell(riga, 2).Value = "Sonda";
        foglio.Cell(riga, 3).Value = "Apparecchiatura";
        foglio.Cell(riga, 4).Value = "Temperatura grezza";
        foglio.Cell(riga, 5).Value = "Offset";
        foglio.Cell(riga, 6).Value = "Temperatura corretta";
        foglio.Cell(riga, 7).Value = "Limite Min";
        foglio.Cell(riga, 8).Value = "Limite Max";
        foglio.Cell(riga, 9).Value = "Stato";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var m in dati.OrderByDescending(x => x.DataOra))
        {
            foglio.Cell(corrente, 1).Value = m.DataOra;
            foglio.Cell(corrente, 2).Value = m.CodiceSonda;
            foglio.Cell(corrente, 3).Value = m.Apparecchiatura;
            if (m.TemperaturaGrezza is decimal tg) foglio.Cell(corrente, 4).Value = tg;
            if (m.OffsetTemperatura is decimal off) foglio.Cell(corrente, 5).Value = off;
            if (m.TemperaturaCorretta is decimal tc) foglio.Cell(corrente, 6).Value = tc;
            if (m.LimiteMin is decimal lmin) foglio.Cell(corrente, 7).Value = lmin;
            if (m.LimiteMax is decimal lmax) foglio.Cell(corrente, 8).Value = lmax;
            foglio.Cell(corrente, 9).Value = m.AllarmeCalcolato == 1 ? "ALLARME" : "OK";
            corrente++;
        }

        foglio.Column(1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        foglio.Column(4).Style.NumberFormat.Format = "0.000";
        foglio.Column(5).Style.NumberFormat.Format = "0.000";
        foglio.Column(6).Style.NumberFormat.Format = "0.000";
        foglio.Column(7).Style.NumberFormat.Format = "0.00";
        foglio.Column(8).Style.NumberFormat.Format = "0.00";

        StileFoglio.Rifinisci(foglio, riga, corrente - 1, colonne);
        if (dati.Count > 0)
        {
            foglio.Range(riga, 1, corrente - 1, colonne).SetAutoFilter();
        }

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public byte[] GeneraDifferenze(IReadOnlyList<DifferenzaTemperatura> dati, string titolo, string sottotitolo)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Differenze");
        const int colonne = 11;

        var riga = StileFoglio.Titolo(foglio, titolo, sottotitolo, colonne);

        foglio.Cell(riga, 1).Value = "Data/Ora";
        foglio.Cell(riga, 2).Value = "Sonda";
        foglio.Cell(riga, 3).Value = "Apparecchiatura";
        foglio.Cell(riga, 4).Value = "Temperatura originale";
        foglio.Cell(riga, 5).Value = "Temperatura Monitor Controllo";
        foglio.Cell(riga, 6).Value = "Differenza";
        foglio.Cell(riga, 7).Value = "Limite Min";
        foglio.Cell(riga, 8).Value = "Limite Max";
        foglio.Cell(riga, 9).Value = "Originale fuori limite";
        foglio.Cell(riga, 10).Value = "Controllo fuori limite";
        foglio.Cell(riga, 11).Value = "Congelatore";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var m in dati.OrderByDescending(x => x.DataOra))
        {
            foglio.Cell(corrente, 1).Value = m.DataOra;
            foglio.Cell(corrente, 2).Value = m.CodiceSonda;
            foglio.Cell(corrente, 3).Value = m.Apparecchiatura;
            foglio.Cell(corrente, 4).Value = m.TemperaturaOriginale;
            foglio.Cell(corrente, 5).Value = m.TemperaturaControllo;
            foglio.Cell(corrente, 6).Value = m.Differenza;
            if (m.LimiteMin is decimal lmin) foglio.Cell(corrente, 7).Value = lmin;
            if (m.LimiteMax is decimal lmax) foglio.Cell(corrente, 8).Value = lmax;
            foglio.Cell(corrente, 9).Value = m.FuoriLimiteOriginale > 0 ? $"+{m.FuoriLimiteOriginale:0.00}" : "NO";
            foglio.Cell(corrente, 10).Value = m.FuoriLimiteControllo > 0 ? $"+{m.FuoriLimiteControllo:0.00}" : "NO";
            foglio.Cell(corrente, 11).Value = m.Congelatore ? "Sì" : "No";
            corrente++;
        }

        foglio.Column(1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm:ss";
        for (int colonna = 4; colonna <= 8; colonna++)
        {
            foglio.Column(colonna).Style.NumberFormat.Format = "0.000";
        }

        StileFoglio.Rifinisci(foglio, riga, corrente - 1, colonne);
        if (dati.Count > 0)
        {
            foglio.Range(riga, 1, corrente - 1, colonne).SetAutoFilter();
        }

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(string prefisso) => $"{prefisso}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
}
