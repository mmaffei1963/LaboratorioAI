using ClosedXML.Excel;
using LaboratorioAI.Core.Presenze;
using LaboratorioAI.Dati.Presenze;

namespace LaboratorioAI.Web.Esportazioni;

/// <summary>
/// Esportazione in Excel del prospetto mensile delle presenze (19/11/2026). Stesso schema del CALENDARIO in uso a MARE.A:
/// una riga per lavoratore (nome nel colore del lavoratore), colonne C..AG = giorni 1..31, AH = totali, riga «Ore totali».
/// Colori: FS/FE giallo · chiusure rosso · assenze nel colore del lavoratore (motivo nel commento) · straordinario in carattere rosso ·
/// ore non lavorate grigio corsivo. È solo una copia in lettura: i dati restano nel database.
/// </summary>
public static class EsportaProspettoExcel
{
    private const string Carattere = "Montserrat";
    private const int ColonnaNome = 2, ColonnaGiorno1 = 3, ColonnaTotale = 34;
    private const string FormatoOre = "0.##";

    public static byte[] Crea(int anno, int mese, string? sede, IReadOnlyList<ProspettoLavoratore> prospetti)
    {
        using var cartella = new XLWorkbook();
        var foglio = cartella.AddWorksheet($"{MotoreCalcoloPresenze.Mesi[mese - 1]} {anno}");

        var titolo = foglio.Cell(1, ColonnaNome);
        titolo.Value = MotoreCalcoloPresenze.Mesi[mese - 1] + (sede is null ? "" : " - " + sede);
        Stile(titolo, 9, grassetto: true, colore: "#003399");
        titolo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        var giorni = MotoreCalcoloPresenze.GiorniMese(anno, mese).ToList();
        for (var i = 0; i < giorni.Count; i++)
        {
            var d = giorni[i];
            var sigla = foglio.Cell(1, ColonnaGiorno1 + i);
            var numero = foglio.Cell(2, ColonnaGiorno1 + i);
            sigla.Value = MotoreCalcoloPresenze.GiorniBrevi[MotoreCalcoloPresenze.IndiceGiornoSettimana(d)];
            numero.Value = d.Day;
            Stile(sigla, 9); Stile(numero, 9); Bordo(numero);
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                sigla.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF9");
                numero.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF9");
            }
        }
        var intestazioneTotali = foglio.Cell(2, ColonnaTotale);
        intestazioneTotali.Value = "Totali";
        Stile(intestazioneTotali, 9, grassetto: true, colore: "#003399");

        var riga = 3;
        foreach (var p in prospetti)
        {
            var nome = foglio.Cell(riga, ColonnaNome);
            nome.Value = p.Calcolo.Nome;
            Stile(nome, 9, grassetto: true, colore: ProspettoPresenze.ColoreScuro(p.Calcolo.Colore) ? "#FFFFFF" : null);
            Bordo(nome);
            nome.Style.Fill.BackgroundColor = XLColor.FromHtml(p.Calcolo.Colore);

            for (var col = ColonnaGiorno1; col < ColonnaGiorno1 + 31; col++)
            {
                var vuota = foglio.Cell(riga, col);
                Stile(vuota, 9); Bordo(vuota); vuota.Style.NumberFormat.Format = FormatoOre;
            }
            foreach (var g in p.Giorni)
            {
                var contenuto = ContenutoCellaPresenze.Costruisci(p.Calcolo, g);
                if (contenuto is null || !contenuto.HaValore) continue;
                var cella = foglio.Cell(riga, ColonnaGiorno1 + g.Data.Day - 1);
                if (contenuto.Ore is double ore) cella.Value = ore; else cella.Value = contenuto.Testo ?? "";
                Stile(cella, 9, contenuto.Grassetto, ProspettoPresenze.ColoreCarattere(contenuto, p.Calcolo.Colore) is string c ? ProspettoPresenze.ColoreHtml(c) : null,
                    contenuto.Corsivo);
                if (contenuto.Sfondo is not null) cella.Style.Fill.BackgroundColor = XLColor.FromHtml(ProspettoPresenze.ColoreHtml(contenuto.Sfondo));
                if (!string.IsNullOrEmpty(contenuto.Nota))
                {
                    var commento = cella.CreateComment();
                    commento.AddText(contenuto.Nota);
                    commento.Style.Size.SetWidth(260);
                    commento.Style.Size.SetHeight(90);
                }
            }
            var totaleRiga = foglio.Cell(riga, ColonnaTotale);
            totaleRiga.FormulaA1 = $"SUM(C{riga}:AG{riga})";
            Stile(totaleRiga, 9, grassetto: true); Bordo(totaleRiga); totaleRiga.Style.NumberFormat.Format = FormatoOre;
            riga++;
        }

        var etichetta = foglio.Cell(riga, ColonnaNome);
        etichetta.Value = "Ore totali";
        Stile(etichetta, 9, grassetto: true, colore: "#003399"); Bordo(etichetta);
        if (prospetti.Count > 0)
            for (var col = ColonnaGiorno1; col <= ColonnaTotale; col++)
            {
                var lettera = XLHelper.GetColumnLetterFromNumber(col);
                var cella = foglio.Cell(riga, col);
                cella.FormulaA1 = col < ColonnaTotale ? $"SUM({lettera}3:{lettera}{riga - 1})" : $"SUM(C{riga}:AG{riga})";
                Stile(cella, 9, grassetto: true, colore: "#003399"); Bordo(cella); cella.Style.NumberFormat.Format = FormatoOre;
            }

        foglio.Column(1).Width = 3;
        foglio.Column(ColonnaNome).Width = 18;
        for (var col = ColonnaGiorno1; col < ColonnaGiorno1 + 31; col++) foglio.Column(col).Width = 5;
        foglio.Column(ColonnaTotale).Width = 8;
        foglio.SheetView.FreezeColumns(ColonnaNome);
        foglio.SheetView.FreezeRows(2);

        AggiungiLegenda(cartella);
        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    private static void AggiungiLegenda(XLWorkbook cartella)
    {
        var foglio = cartella.AddWorksheet("Legenda");
        var voci = new (string Esempio, string? Sfondo, string? Carattere, string Significato)[]
        {
            ("FS / FE", "#FFFF00", null, "Fine settimana e festività"),
            ("8", "#FF0000", null, "Chiusura aziendale (ore coperte)"),
            ("4", "#FFC000", null, "Ferie, permessi, malattia e altre assenze: ore nel colore del lavoratore, motivo nel commento"),
            ("1,5", null, "#FF0000", "Straordinario: ore oltre il monte ore del giorno, carattere rosso"),
            ("2", "#D9D9D9", null, "Ore non lavorate da giustificare (grigio, corsivo)"),
        };
        for (var i = 0; i < voci.Length; i++)
        {
            var esempio = foglio.Cell(i + 1, 1);
            esempio.Value = voci[i].Esempio;
            Stile(esempio, 9, grassetto: true, colore: voci[i].Carattere, corsivo: voci[i].Sfondo == "#D9D9D9"); Bordo(esempio);
            if (voci[i].Sfondo is string sfondo) esempio.Style.Fill.BackgroundColor = XLColor.FromHtml(sfondo);
            var significato = foglio.Cell(i + 1, 2);
            significato.Value = voci[i].Significato;
            Stile(significato, 9);
        }
        foglio.Column(1).Width = 10;
        foglio.Column(2).Width = 90;
    }

    private static void Stile(IXLCell cella, int dimensione, bool grassetto = false, string? colore = null, bool corsivo = false)
    {
        cella.Style.Font.FontName = Carattere;
        cella.Style.Font.FontSize = dimensione;
        cella.Style.Font.Bold = grassetto;
        cella.Style.Font.Italic = corsivo;
        if (colore is not null) cella.Style.Font.FontColor = XLColor.FromHtml(colore);
        cella.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cella.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void Bordo(IXLCell cella)
    {
        cella.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        cella.Style.Border.OutsideBorderColor = XLColor.Black;
    }
}
