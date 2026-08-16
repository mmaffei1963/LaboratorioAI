using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel il report dei tamponi in contratto (dettaglio + riepilogo).</summary>
public interface IEsportatoreTamponiContratto
{
    byte[] Genera(EsitoTamponiContratto esito, CriteriTamponiContratto criteri);
    string NomeFile(CriteriTamponiContratto criteri);
}

/// <inheritdoc />
public class EsportatoreTamponiContratto : IEsportatoreTamponiContratto
{
    public byte[] Genera(EsitoTamponiContratto esito, CriteriTamponiContratto criteri)
    {
        using var cartella = new XLWorkbook();

        var periodo = $"Periodo (data certificato) dal {criteri.DataDa:dd/MM/yyyy} "
                    + $"al {criteri.DataA:dd/MM/yyyy} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";

        ScriviRiepilogo(cartella, esito.Riepilogo, periodo);
        ScriviDettaglio(cartella, esito.Tamponi, periodo);

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(CriteriTamponiContratto criteri) =>
        $"TamponiInContratto_{criteri.DataDa:yyyyMMdd}-{criteri.DataA:yyyyMMdd}.xlsx";

    // ── Foglio 1: riepilogo per insediamento ───────────────────────

    private static void ScriviRiepilogo(
        XLWorkbook cartella, IReadOnlyList<RiepilogoInsediamentoTamponi> righe, string periodo)
    {
        var foglio = cartella.Worksheets.Add("Riepilogo insediamenti");
        const int colonne = 7;

        var riga = StileFoglio.Titolo(foglio, "Tamponi in contratto — riepilogo per insediamento", periodo, colonne);

        foglio.Cell(riga, 1).Value = "Cliente";
        foglio.Cell(riga, 2).Value = "Insediamento";
        foglio.Cell(riga, 3).Value = "Contratto HACCP";
        foglio.Cell(riga, 4).Value = "Tamponi inclusi";
        foglio.Cell(riga, 5).Value = "Tamponi fatti";
        foglio.Cell(riga, 6).Value = "Differenza";
        foglio.Cell(riga, 7).Value = "Prelevatore/i";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var r in righe)
        {
            foglio.Cell(corrente, 1).Value = r.RagioneSociale;
            foglio.Cell(corrente, 2).Value = r.NomeInsediamento;
            foglio.Cell(corrente, 3).Value = r.ContrattoHaccp ? "Sì" : "No";
            if (r.TamponiInclusi is int inclusi) foglio.Cell(corrente, 4).Value = inclusi;
            foglio.Cell(corrente, 5).Value = r.TamponiFatti;
            if (r.Differenza is int diff) foglio.Cell(corrente, 6).Value = diff;
            foglio.Cell(corrente, 7).Value = r.Prelevatori;
            corrente++;
        }

        StileFoglio.Rifinisci(foglio, riga, corrente, colonne);
    }

    // ── Foglio 2: dettaglio per tampone ────────────────────────────

    private static void ScriviDettaglio(
        XLWorkbook cartella, IReadOnlyList<TamponeContratto> righe, string periodo)
    {
        var foglio = cartella.Worksheets.Add("Dettaglio tamponi");
        const int colonne = 11;

        var riga = StileFoglio.Titolo(foglio, "Tamponi in contratto — dettaglio", periodo, colonne);

        foglio.Cell(riga, 1).Value = "Cliente";
        foglio.Cell(riga, 2).Value = "Insediamento";
        foglio.Cell(riga, 3).Value = "N° campione";
        foglio.Cell(riga, 4).Value = "Controllo";
        foglio.Cell(riga, 5).Value = "N° certificato";
        foglio.Cell(riga, 6).Value = "Data certificato";
        foglio.Cell(riga, 7).Value = "Prelevatore";
        foglio.Cell(riga, 8).Value = "Non fatturare";
        foglio.Cell(riga, 9).Value = "Contratto HACCP";
        foglio.Cell(riga, 10).Value = "Tamponi inclusi";
        foglio.Cell(riga, 11).Value = "Prestazione (anno)";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var t in righe)
        {
            foglio.Cell(corrente, 1).Value = t.RagioneSociale;
            foglio.Cell(corrente, 2).Value = t.NomeInsediamento;
            foglio.Cell(corrente, 3).Value = t.NumeroCampione;
            foglio.Cell(corrente, 4).Value = t.Controllo;
            foglio.Cell(corrente, 5).Value = t.NumeroCertificato;
            if (t.DataCertificato is DateTime dc) foglio.Cell(corrente, 6).Value = dc.Date;
            foglio.Cell(corrente, 7).Value = t.Prelevatore;
            foglio.Cell(corrente, 8).Value = t.NonFatturare ? "Sì" : "No";
            foglio.Cell(corrente, 9).Value = t.ContrattoHaccp ? "Sì" : "No";
            if (t.TamponiInclusi is int inclusi) foglio.Cell(corrente, 10).Value = inclusi;
            if (t.DataPrestazione is DateTime dp) foglio.Cell(corrente, 11).Value = dp.Date;
            corrente++;
        }

        foglio.Column(6).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(11).Style.DateFormat.Format = "dd/MM/yyyy";

        StileFoglio.Rifinisci(foglio, riga, corrente, colonne);
    }
}
