using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel i risultati della ricerca campioni (Consultazione).</summary>
public interface IEsportatoreConsultazione
{
    byte[] Genera(IReadOnlyList<RisultatoRicercaCampione> campioni, CriteriRicercaCampioni criteri);
    string NomeFile(CriteriRicercaCampioni criteri);
}

/// <inheritdoc />
public class EsportatoreConsultazione : IEsportatoreConsultazione
{
    private static readonly string[] Intestazioni =
    {
        "N° campione", "Cliente", "Cod. contabilità", "Email", "Insediamento", "Denominazione",
        "Matrice", "Controllo", "Luogo prelievo", "Punto prelievo", "Verbale",
        "Prelevatore", "Prezzo €", "Data ricezione", "Data prelievo", "Stato", "N° prove"
    };

    public byte[] Genera(IReadOnlyList<RisultatoRicercaCampione> campioni, CriteriRicercaCampioni criteri)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Consultazione");
        int colonne = Intestazioni.Length;

        var periodo = $"Estratto il {DateTime.Now:dd/MM/yyyy HH:mm}"
            + (string.IsNullOrWhiteSpace(criteri.Email) ? "" : $" — email: {criteri.Email}");

        var riga = StileFoglio.Titolo(foglio, "Consultazione campioni", periodo, colonne);

        for (int i = 0; i < colonne; i++) foglio.Cell(riga, i + 1).Value = Intestazioni[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var c in campioni)
        {
            foglio.Cell(corrente, 1).Value = c.NumeroCampione;
            foglio.Cell(corrente, 2).Value = c.RagioneSociale;
            foglio.Cell(corrente, 3).Value = c.CodContabilita;
            foglio.Cell(corrente, 4).Value = c.Email;
            foglio.Cell(corrente, 5).Value = c.NomeInsediamento;
            foglio.Cell(corrente, 6).Value = c.DenominazioneCampione;
            foglio.Cell(corrente, 7).Value = c.Matrice;
            foglio.Cell(corrente, 8).Value = c.Controllo;
            foglio.Cell(corrente, 9).Value = c.LuogoPrelievo;
            foglio.Cell(corrente, 10).Value = c.PuntoPrelievo;
            foglio.Cell(corrente, 11).Value = c.VerbalePrelievo;
            foglio.Cell(corrente, 12).Value = c.Prelevatore;
            if (c.PrezzoControllo is decimal pz) foglio.Cell(corrente, 13).Value = pz;
            if (c.DataRicezione is DateTime dr) foglio.Cell(corrente, 14).Value = dr.Date;
            if (c.DataPrelievo is DateTime dp) foglio.Cell(corrente, 15).Value = dp.Date;
            foglio.Cell(corrente, 16).Value = c.Stato.ToString();
            foglio.Cell(corrente, 17).Value = c.NumeroProve;
            corrente++;
        }

        foglio.Column(13).Style.NumberFormat.Format = "#,##0.00";
        foglio.Column(14).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(15).Style.DateFormat.Format = "dd/MM/yyyy";
        StileFoglio.Rifinisci(foglio, riga, corrente - 1, colonne);
        if (campioni.Count > 0) foglio.Range(riga, 1, corrente - 1, colonne).SetAutoFilter();

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(CriteriRicercaCampioni criteri) =>
        $"Consultazione_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
}
