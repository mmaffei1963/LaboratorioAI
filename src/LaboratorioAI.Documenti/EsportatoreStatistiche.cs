using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel il risultato della ricerca Statistiche (un foglio di dettaglio).</summary>
public interface IEsportatoreStatistiche
{
    byte[] Genera(IReadOnlyList<RigaStatistica> righe, CriteriStatistiche criteri);
    string NomeFile(CriteriStatistiche criteri);
}

/// <inheritdoc />
public class EsportatoreStatistiche : IEsportatoreStatistiche
{
    public byte[] Genera(IReadOnlyList<RigaStatistica> righe, CriteriStatistiche criteri)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Statistiche");

        string[] intestazioni =
        {
            "Cliente", "Insediamento", "Città", "Provincia", "CAP", "Indirizzo",
            "Commessa", "N° certificato", "N° campione", "Denominazione campione", "Matrice",
            "Data ricezione", "Data prelievo", "Punto prelievo", "Temp. prelievo",
            "Lotto produzione", "Data produzione", "Lavorazione",
            "Parametro", "Metodo", "Rif. normativo", "Accreditato",
            "Valore calcolato", "Unità di misura",
        };
        var totColonne = intestazioni.Length;

        var periodo = $"Periodo (data ricezione) dal {criteri.DataRicDa:dd/MM/yyyy} "
                    + $"al {criteri.DataRicA:dd/MM/yyyy} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
        var riga = StileFoglio.Titolo(foglio, "Statistiche — dettaglio prove", periodo, totColonne);

        for (var i = 0; i < intestazioni.Length; i++)
            foglio.Cell(riga, i + 1).Value = intestazioni[i];
        StileFoglio.Intestazione(foglio, riga, totColonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var r in righe)
        {
            var c = 1;
            foglio.Cell(corrente, c++).Value = r.Cliente;
            foglio.Cell(corrente, c++).Value = r.Insediamento;
            foglio.Cell(corrente, c++).Value = r.Citta;
            foglio.Cell(corrente, c++).Value = r.Provincia;
            foglio.Cell(corrente, c++).Value = r.Cap;
            foglio.Cell(corrente, c++).Value = r.Indirizzo;
            foglio.Cell(corrente, c++).Value = r.Commessa;
            foglio.Cell(corrente, c++).Value = r.NCertificato;
            foglio.Cell(corrente, c++).Value = r.NumeroCampione;
            foglio.Cell(corrente, c++).Value = r.DenominazioneCampione;
            foglio.Cell(corrente, c++).Value = r.Matrice;
            if (r.DataRicezione is DateTime dr) foglio.Cell(corrente, c).Value = dr.Date;
            c++;
            if (r.DataPrelievo is DateTime dp) foglio.Cell(corrente, c).Value = dp.Date;
            c++;
            foglio.Cell(corrente, c++).Value = r.PuntoPrelievo;
            // Temperatura: numero se leggibile (anche con la virgola), altrimenti il testo così com'è.
            var temp = r.TempPrelievo?.Trim();
            if (decimal.TryParse(temp?.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var tv))
                foglio.Cell(corrente, c).Value = tv;
            else
                foglio.Cell(corrente, c).Value = temp;
            c++;
            foglio.Cell(corrente, c++).Value = r.LottoProduzione;
            if (r.DataProduzione is DateTime dpr) foglio.Cell(corrente, c).Value = dpr.Date;
            c++;
            foglio.Cell(corrente, c++).Value = r.Lavorazione;
            foglio.Cell(corrente, c++).Value = r.Parametro;
            foglio.Cell(corrente, c++).Value = r.Metodo;
            foglio.Cell(corrente, c++).Value = r.RiferimentoNormativo;
            foglio.Cell(corrente, c++).Value = r.Accreditato ? "Sì" : "No";
            if (r.ValoreCalcolato is decimal v)
                foglio.Cell(corrente, c).Value = (r.MinoreDi ? "< " : "") + v.ToString("0.####");
            c++;
            foglio.Cell(corrente, c).Value = r.UnitaMisura;

            corrente++;
        }

        foglio.Column(12).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(13).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(16).Style.DateFormat.Format = "dd/MM/yyyy";

        StileFoglio.Rifinisci(foglio, riga, corrente - 1, totColonne);
        if (righe.Count > 0)
            foglio.Range(riga, 1, corrente - 1, totColonne).SetAutoFilter();

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(CriteriStatistiche criteri) =>
        $"Statistiche_{criteri.DataRicDa:yyyyMMdd}-{criteri.DataRicA:yyyyMMdd}.xlsx";
}
