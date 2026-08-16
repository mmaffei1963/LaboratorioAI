using System.Text.RegularExpressions;
using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel il report Non fatturare (riepilogo per prelevatore + dettaglio).</summary>
public interface IEsportatoreNonFatturare
{
    byte[] Genera(IReadOnlyList<CampioneNonFatturare> campioni, CriteriNonFatturare criteri);
    string NomeFile(CriteriNonFatturare criteri);
}

/// <inheritdoc />
public class EsportatoreNonFatturare : IEsportatoreNonFatturare
{
    public byte[] Genera(IReadOnlyList<CampioneNonFatturare> campioni, CriteriNonFatturare criteri)
    {
        using var cartella = new XLWorkbook();

        var periodo = $"Periodo (data ricezione) dal {criteri.DataDa:dd/MM/yyyy} "
                    + $"al {criteri.DataA:dd/MM/yyyy} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";

        ScriviRiepilogo(cartella, campioni, periodo,
            "Riepilogo prelevatori", "Non fatturare — riepilogo per prelevatore",
            "Prelevatore", c => Prelevatore(c.Prelevatore));
        ScriviRiepilogo(cartella, campioni, periodo,
            "Riepilogo ricevitori", "Non fatturare — riepilogo per ricevitore",
            "Ricevitore", c => Ricevitore(c.Ricevitore));
        ScriviDettaglio(cartella, campioni, periodo);

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(CriteriNonFatturare criteri) =>
        $"NonFatturare_{criteri.DataDa:yyyyMMdd}-{criteri.DataA:yyyyMMdd}.xlsx";

    /// <summary>Normalizza il nome prelevatore (spazi doppi/estremità), come a video.</summary>
    public static string Prelevatore(string? p)
    {
        var n = Regex.Replace((p ?? "").Trim(), @"\s+", " ");
        return n.Length == 0 ? "(nessuno)" : n;
    }

    /// <summary>Normalizza il nome ricevitore (spazi doppi/estremità), come a video.</summary>
    public static string Ricevitore(string? r)
    {
        var n = Regex.Replace((r ?? "").Trim(), @"\s+", " ");
        return n.Length == 0 ? "(nessuno)" : n;
    }

    // ── Fogli 1-2: riepilogo per prelevatore / ricevitore ──────────

    private static void ScriviRiepilogo(
        XLWorkbook cartella, IReadOnlyList<CampioneNonFatturare> campioni, string periodo,
        string nomeFoglio, string titolo, string intestazioneChiave,
        Func<CampioneNonFatturare, string> chiave)
    {
        var foglio = cartella.Worksheets.Add(nomeFoglio);
        const int colonne = 4;

        var riga = StileFoglio.Titolo(foglio, titolo, periodo, colonne);

        foglio.Cell(riga, 1).Value = intestazioneChiave;
        foglio.Cell(riga, 2).Value = "N° campioni";
        foglio.Cell(riga, 3).Value = "di cui in contratto HACCP";
        foglio.Cell(riga, 4).Value = "Totale €";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var gruppi = campioni
            .GroupBy(chiave)
            .Select(g => new
            {
                Chiave = g.Key,
                Campioni = g.Count(),
                InContratto = g.Count(x => x.ContrattoHaccp),
                Totale = g.Sum(x => x.PrezzoControllo ?? 0m)
            })
            .OrderByDescending(x => x.Campioni)
            .ThenBy(x => x.Chiave)
            .ToList();

        var corrente = riga + 1;
        foreach (var g in gruppi)
        {
            foglio.Cell(corrente, 1).Value = g.Chiave;
            foglio.Cell(corrente, 2).Value = g.Campioni;
            foglio.Cell(corrente, 3).Value = g.InContratto;
            foglio.Cell(corrente, 4).Value = g.Totale;
            corrente++;
        }

        if (gruppi.Count > 0)
        {
            foglio.Cell(corrente, 1).Value = "Totale generale";
            foglio.Cell(corrente, 2).Value = gruppi.Sum(x => x.Campioni);
            foglio.Cell(corrente, 3).Value = gruppi.Sum(x => x.InContratto);
            foglio.Cell(corrente, 4).Value = gruppi.Sum(x => x.Totale);
            foglio.Range(corrente, 1, corrente, colonne).Style.Font.Bold = true;
            foglio.Range(corrente, 1, corrente, colonne).Style.Fill.BackgroundColor = StileFoglio.Chiaro;
        }

        foglio.Column(4).Style.NumberFormat.Format = "#,##0.00";
        StileFoglio.Rifinisci(foglio, riga, corrente, colonne);
    }

    // ── Foglio 2: dettaglio ────────────────────────────────────────

    private static void ScriviDettaglio(
        XLWorkbook cartella, IReadOnlyList<CampioneNonFatturare> campioni, string periodo)
    {
        var foglio = cartella.Worksheets.Add("Dettaglio");
        const int colonne = 9;

        var riga = StileFoglio.Titolo(foglio, "Non fatturare — dettaglio", periodo, colonne);

        foglio.Cell(riga, 1).Value = "Prelevatore";
        foglio.Cell(riga, 2).Value = "Ricevitore";
        foglio.Cell(riga, 3).Value = "Cliente";
        foglio.Cell(riga, 4).Value = "Insediamento";
        foglio.Cell(riga, 5).Value = "N° campione";
        foglio.Cell(riga, 6).Value = "Controllo";
        foglio.Cell(riga, 7).Value = "Data ricezione";
        foglio.Cell(riga, 8).Value = "Contratto HACCP";
        foglio.Cell(riga, 9).Value = "Prezzo €";
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var corrente = riga + 1;
        foreach (var c in campioni
            .OrderBy(x => Prelevatore(x.Prelevatore)).ThenBy(x => x.RagioneSociale))
        {
            foglio.Cell(corrente, 1).Value = Prelevatore(c.Prelevatore);
            foglio.Cell(corrente, 2).Value = Ricevitore(c.Ricevitore);
            foglio.Cell(corrente, 3).Value = c.RagioneSociale;
            foglio.Cell(corrente, 4).Value = c.NomeInsediamento;
            foglio.Cell(corrente, 5).Value = c.NumeroCampione;
            foglio.Cell(corrente, 6).Value = c.Controllo;
            if (c.DataRicezione is DateTime dr) foglio.Cell(corrente, 7).Value = dr.Date;
            foglio.Cell(corrente, 8).Value = c.ContrattoHaccp ? "Sì" : "No";
            if (c.PrezzoControllo is decimal pz) foglio.Cell(corrente, 9).Value = pz;
            corrente++;
        }

        foglio.Column(7).Style.DateFormat.Format = "dd/MM/yyyy";
        foglio.Column(9).Style.NumberFormat.Format = "#,##0.00";
        StileFoglio.Rifinisci(foglio, riga, corrente - 1, colonne);

        if (campioni.Count > 0)
            foglio.Range(riga, 1, corrente - 1, colonne).SetAutoFilter();
    }
}
