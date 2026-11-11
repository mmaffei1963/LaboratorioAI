using ClosedXML.Excel;
using LaboratorioAI.Core.Direzione;

namespace LaboratorioAI.Documenti;

/// <summary>Excel della ricerca dei documenti AZW per descrizione (10/11/2026; costi, aree, commesse e zone dall'11/11/2026).</summary>
public static class EsportatoreRicercaFatture
{
    public static byte[] Genera(IReadOnlyList<RigaRicercaDocumento> righe, RiepilogoRicercaDocumenti riepilogo, DateTime dal, DateTime al,
        string filtri, Func<string, string> nomeArea, Func<string?, string?> nomeCommessa, Func<string?, string?> nomeZona)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.Worksheets.Add("Documenti");
        const int colonne = 15;
        var sotto = $"Dal {dal:dd/MM/yyyy} al {al:dd/MM/yyyy}" + (filtri.Length > 0 ? " — " + filtri : "") +
                    $" — {righe.Count} righe — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}";
        var riga = StileFoglio.Titolo(foglio, "Documenti AZW per descrizione (ricavi e costi)", sotto, colonne);

        string[] intest = { "Data", "Tipo", "Documento", "Codice", "Cliente / fornitore", "Descrizione", "Quantità", "Prezzo", "Sconto",
                            "Importo", "Area", "Centro/Voce", "Chi ha fatto il lavoro (commessa)", "Chi gestisce il cliente (zona)", "Ricavo/Costo" };
        for (var i = 0; i < intest.Length; i++) foglio.Cell(riga, i + 1).Value = intest[i];
        StileFoglio.Intestazione(foglio, riga, colonne, StileFoglio.Rosso);

        var prima = riga + 1; var r = prima;
        foreach (var v in righe)
        {
            foglio.Cell(r, 1).Value = v.Data; foglio.Cell(r, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            foglio.Cell(r, 2).Value = v.Costo ? "Costo " + v.TipoDocumento
                : VociDocumentiVendita.ENotaCredito(v.TipoDocumento) ? "Nota di credito" : "Fattura";
            foglio.Cell(r, 3).Value = v.Numero;
            foglio.Cell(r, 4).Value = v.CodiceControparte ?? "";
            foglio.Cell(r, 5).Value = v.Controparte ?? "";
            foglio.Cell(r, 6).Value = v.Descrizione;
            if (v.Quantita is decimal q) foglio.Cell(r, 7).Value = q;
            if (v.Prezzo is decimal p) { foglio.Cell(r, 8).Value = p; foglio.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00"; }
            foglio.Cell(r, 9).Value = v.Sconto ?? "";
            foglio.Cell(r, 10).Value = v.Importo; foglio.Cell(r, 10).Style.NumberFormat.Format = "#,##0.00";
            foglio.Cell(r, 11).Value = nomeArea(v.Area);
            foglio.Cell(r, 12).Value = string.Join("/", new[] { v.Centro, v.Voce }.Where(s => !string.IsNullOrWhiteSpace(s)));
            foglio.Cell(r, 13).Value = nomeCommessa(v.Commessa) ?? "";
            foglio.Cell(r, 14).Value = nomeZona(v.Zona) ?? "";
            foglio.Cell(r, 15).Value = v.Costo ? "Costo" : "Ricavo";
            r++;
        }
        StileFoglio.Rifinisci(foglio, prima, Math.Max(prima, r - 1), colonne);
        foglio.Column(6).Width = 70; foglio.Column(6).Style.Alignment.WrapText = true;

        // Foglio dei totali: gli stessi riquadri della pagina e la ripartizione per area.
        var tot = cartella.Worksheets.Add("Totali");
        var t = StileFoglio.Titolo(tot, "Totali della ricerca", sotto, 3);
        void Riga(string etichetta, decimal valore, string? nota = null)
        {
            tot.Cell(t, 1).Value = etichetta; tot.Cell(t, 2).Value = valore; tot.Cell(t, 2).Style.NumberFormat.Format = "#,##0.00";
            tot.Cell(t, 3).Value = nota ?? ""; t++;
        }
        Riga("Ricavi netti (fatture − note di credito)", riepilogo.Ricavi, $"{riepilogo.Fatture} fatture, {riepilogo.NoteCredito} note di credito, {riepilogo.Clienti} clienti");
        Riga("Costi", riepilogo.Costi, $"{riepilogo.DocumentiCosto} registrazioni, {riepilogo.Fornitori} fornitori");
        Riga("Differenza ricavi − costi", riepilogo.Differenza);
        t++;
        tot.Cell(t, 1).Value = "Area"; tot.Cell(t, 2).Value = "Ricavi"; tot.Cell(t, 3).Value = "Costi";
        StileFoglio.Intestazione(tot, t, 3, StileFoglio.Rosso);
        var primaArea = ++t;
        foreach (var a in riepilogo.PerArea)
        {
            tot.Cell(t, 1).Value = nomeArea(a.Area);
            tot.Cell(t, 2).Value = a.Ricavi; tot.Cell(t, 2).Style.NumberFormat.Format = "#,##0.00";
            tot.Cell(t, 3).Value = a.Costi; tot.Cell(t, 3).Style.NumberFormat.Format = "#,##0.00";
            t++;
        }
        StileFoglio.Rifinisci(tot, primaArea, Math.Max(primaArea, t - 1), 3);
        tot.Column(1).Width = 42; tot.Column(2).Width = 16; tot.Column(3).Width = 50;

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }
}
