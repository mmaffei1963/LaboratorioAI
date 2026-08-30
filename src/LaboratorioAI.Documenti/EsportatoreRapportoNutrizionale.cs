using System.Globalization;
using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di stima dei valori nutrizionali in Excel, con lo stesso impianto
/// del foglio «Rapporto» storico: intestazione MARE.A, testata, due dichiarazioni
/// affiancate (per 100 g e per unità di consumo), le percentuali di riferimento,
/// le note legali (Reg. UE 1169/2011) e il blocco firma.
/// </summary>
public interface IEsportatoreRapportoNutrizionale
{
    byte[] Genera(DatiRapportoNutrizionale dati);
    string NomeFile(DatiRapportoNutrizionale dati);
}

/// <inheritdoc />
public class EsportatoreRapportoNutrizionale : IEsportatoreRapportoNutrizionale
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    // Colonne: pannello sinistro (per 100 g) e destro (per unità di consumo)
    private const int LabelSx = 2, ValSx = 3, UnitSx = 4;
    private const int LabelDx = 6, ValDx = 7, UnitDx = 8;

    public byte[] Genera(DatiRapportoNutrizionale d)
    {
        var r = d.Risultato;
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Rapporto");

        ws.Style.Font.FontName = "Calibri";
        ws.Style.Font.FontSize = 10;
        ws.Column(1).Width = 2.5;
        ws.Column(LabelSx).Width = 22;
        ws.Column(ValSx).Width = 14;
        ws.Column(UnitSx).Width = 12;
        ws.Column(5).Width = 3;
        ws.Column(LabelDx).Width = 22;
        ws.Column(ValDx).Width = 14;
        ws.Column(UnitDx).Width = 12;

        // ── Intestazione MARE.A ────────────────────────────────────
        ws.Cell(2, LabelSx).Value = "MARE.A s.r.l";
        ws.Cell(2, LabelSx).Style.Font.Bold = true;
        ws.Cell(3, LabelSx).Value = "Sede legale: Via E. Toti, 2 - 47841 Cattolica (RN)";
        ws.Cell(4, LabelSx).Value = "Sede operativa: Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • Fax 0541 830460";
        ws.Cell(5, LabelSx).Value = "info@mare-a.com • www.mare-a.com";
        ws.Cell(6, LabelSx).Value = "Cod. Fisc. e P. Iva 03670240401";
        for (int rr = 3; rr <= 6; rr++) ws.Cell(rr, LabelSx).Style.Font.FontSize = 9;

        // ── Titolo ─────────────────────────────────────────────────
        int rt = 8;
        ws.Range(rt, LabelSx, rt, UnitDx).Merge();
        ws.Cell(rt, LabelSx).Value = "RAPPORTO DI STIMA DEI VALORI NUTRIZIONALI";
        ws.Cell(rt, LabelSx).Style.Font.Bold = true;
        ws.Cell(rt, LabelSx).Style.Font.FontSize = 13;
        ws.Cell(rt, LabelSx).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Row(rt).Height = 22;

        // ── Testata ────────────────────────────────────────────────
        int r1 = 10;
        Campo(ws, r1, LabelSx, "Rapporto del", ValSx, d.DataRapporto.ToString("dd/MM/yyyy", It));
        Campo(ws, r1, LabelDx, "Unità di consumo [g]", ValDx, Fmt(d.UnitaConsumoG, 0));
        Campo(ws, r1 + 1, LabelSx, "Denominazione", ValSx, d.Denominazione);
        Campo(ws, r1 + 1, LabelDx, "Unità di consumo [pezzi]", ValDx, d.UnitaConsumoPezzi is decimal p ? Fmt(p, 0) : "—");
        Campo(ws, r1 + 2, LabelSx, "Lotto", ValSx, string.IsNullOrWhiteSpace(d.Lotto) ? "—" : d.Lotto);
        Campo(ws, r1 + 2, LabelDx, "Redatto da", ValDx, d.RedattoDa ?? "—");
        Campo(ws, r1 + 3, LabelSx, "Committente", ValSx, d.Committente ?? "—");

        // ── Dichiarazione nutrizionale ─────────────────────────────
        int rd = 15;
        Titolo2(ws, rd, LabelSx, UnitSx, "DICHIARAZIONE NUTRIZIONALE per 100 g");
        Titolo2(ws, rd, LabelDx, UnitDx, "DICHIARAZIONE NUTRIZIONALE per unità di consumo");
        ws.Cell(rd + 1, LabelSx).Value = "per 100 g di parte edibile";
        ws.Cell(rd + 1, LabelDx).Value = $"per unità di consumo ({Fmt(d.UnitaConsumoG, 0)} g)";
        ws.Cell(rd + 1, LabelSx).Style.Font.Italic = true;
        ws.Cell(rd + 1, LabelDx).Style.Font.Italic = true;

        var a100 = r.Per100gArrotondato;
        var aU = r.PerUnitaArrotondato;
        int rr2 = rd + 2;
        rr2 = Riga(ws, rr2, "Energia", Fmt(a100.EnergiaKj, 0), "kJ", Fmt(aU.EnergiaKj, 0), "kJ");
        rr2 = Riga(ws, rr2, "Energia", Fmt(a100.EnergiaKcal, 0), "kcal", Fmt(aU.EnergiaKcal, 0), "kcal");
        rr2 = Riga(ws, rr2, "Grassi", Fmt(a100.Grassi, 1), "g", Fmt(aU.Grassi, 1), "g");
        rr2 = Riga(ws, rr2, "di cui acidi grassi saturi", Fmt(a100.GrassiSaturi, 1), "g", Fmt(aU.GrassiSaturi, 1), "g", indent: true);
        rr2 = Riga(ws, rr2, "Carboidrati", Fmt(a100.Carboidrati, a100.Carboidrati > 10 ? 0 : 1), "g", Fmt(aU.Carboidrati, aU.Carboidrati > 10 ? 0 : 1), "g");
        rr2 = Riga(ws, rr2, "di cui zuccheri", Fmt(a100.Zuccheri, a100.Zuccheri > 10 ? 0 : 1), "g", Fmt(aU.Zuccheri, aU.Zuccheri > 10 ? 0 : 1), "g", indent: true);
        rr2 = Riga(ws, rr2, "Proteine", Fmt(a100.Proteine, 1), "g", Fmt(aU.Proteine, 1), "g");
        rr2 = Riga(ws, rr2, "Sale", Fmt(a100.Sale, 2), "g", Fmt(aU.Sale, 2), "g");
        rr2 = Riga(ws, rr2, "Fibre", Fmt(a100.Fibre, 1), "g", Fmt(aU.Fibre, 1), "g");

        // ── Assunzioni di riferimento ──────────────────────────────
        int rri = rr2 + 1;
        Titolo2(ws, rri, LabelSx, UnitSx, "ASSUNZIONE DI RIFERIMENTO — adulto medio (8400 kJ / 2000 kcal)");
        Titolo2(ws, rri, LabelDx, UnitDx, "ASSUNZIONE DI RIFERIMENTO — adulto medio (8400 kJ / 2000 kcal)");
        ws.Cell(rri + 1, LabelSx).Value = "% delle assunzioni di riferimento";
        ws.Cell(rri + 1, LabelDx).Value = "% delle assunzioni di riferimento";
        ws.Cell(rri + 1, LabelSx).Style.Font.Italic = true;
        ws.Cell(rri + 1, LabelDx).Style.Font.Italic = true;

        var ri = r.RiferimentiPer100g;
        var riU = r.RiferimentiPerUnita;
        int rr3 = rri + 2;
        rr3 = RigaRi(ws, rr3, "Energia", Pct(ri.EnergiaKcal), Pct(riU.EnergiaKcal));
        rr3 = RigaRi(ws, rr3, "Grassi", Pct(ri.Grassi), Pct(riU.Grassi));
        rr3 = RigaRi(ws, rr3, "di cui acidi grassi saturi", Pct(ri.GrassiSaturi), Pct(riU.GrassiSaturi), indent: true);
        rr3 = RigaRi(ws, rr3, "Carboidrati", Pct(ri.Carboidrati), Pct(riU.Carboidrati));
        rr3 = RigaRi(ws, rr3, "di cui zuccheri", Pct(ri.Zuccheri), Pct(riU.Zuccheri), indent: true);
        rr3 = RigaRi(ws, rr3, "Proteine", Pct(ri.Proteine), Pct(riU.Proteine));
        rr3 = RigaRi(ws, rr3, "Sale", Pct(ri.Sale), Pct(riU.Sale));
        rr3 = RigaRi(ws, rr3, "Fibre", "n.d.", "n.d.", indent: true);

        // ── Fonti e note legali ────────────────────────────────────
        int rn = rr3 + 1;
        Nota(ws, rn++, "I valori nutrizionali dei singoli ingredienti sono stati ricavati dalle seguenti fonti, in ordine di priorità:", true);
        Nota(ws, rn++, "Schede tecniche dei produttori degli ingredienti; Database CREA (ex-INRAN) e/o IEO; EuroFIR Food Composition Databases; altre fonti bibliografiche.");
        rn++;
        Nota(ws, rn++, "Note esplicative:", true);
        foreach (var t in Note())
            rn = NotaLunga(ws, rn, t);

        // ── Firma ──────────────────────────────────────────────────
        int rf = rn + 2;
        ws.Cell(rf, LabelDx).Value = d.RedattoDa ?? "";
        ws.Cell(rf, LabelDx).Style.Font.Bold = true;
        ws.Cell(rf + 1, LabelDx).Value = "MARE.A s.r.l — Responsabile di Laboratorio";
        ws.Cell(rf + 1, LabelDx).Style.Font.FontSize = 9;

        // ── Impostazioni di stampa (A4, per PDF) ───────────────────
        ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
        ws.PageSetup.PaperSize = XLPaperSize.A4Paper;
        ws.PageSetup.Margins.Top = 0.5;
        ws.PageSetup.Margins.Bottom = 0.5;
        ws.PageSetup.Margins.Left = 0.5;
        ws.PageSetup.Margins.Right = 0.5;
        ws.PageSetup.PagesWide = 1;

        // ── Secondo foglio: scheda di verifica ingredienti e fonti ─
        if (d.Ingredienti.Count > 0)
        {
            var wv = wb.Worksheets.Add("Verifica dati");
            wv.Style.Font.FontName = "Calibri";
            wv.Style.Font.FontSize = 10;
            wv.Column(1).Width = 2.5;
            wv.Column(2).Width = 40;
            wv.Column(3).Width = 14;
            wv.Column(4).Width = 8;
            wv.Column(5).Width = 45;

            int rv = 2;
            wv.Cell(rv, 2).Value = "SCHEDA DI VERIFICA DATI — INGREDIENTI E FONTI";
            wv.Cell(rv, 2).Style.Font.Bold = true;
            wv.Cell(rv, 2).Style.Font.FontSize = 12;
            rv += 2;
            wv.Cell(rv, 2).Value = "Ingrediente";
            wv.Cell(rv, 3).Value = "Quantità (kg)";
            wv.Cell(rv, 4).Value = "%";
            wv.Cell(rv, 5).Value = "Fonte dei valori";
            wv.Range(rv, 2, rv, 5).Style.Font.Bold = true;
            rv++;
            foreach (var ing in d.Ingredienti)
            {
                wv.Cell(rv, 2).Value = ing.Denominazione;
                wv.Cell(rv, 3).Value = Fmt(ing.QuantitaKg, 3);
                wv.Cell(rv, 4).Value = Fmt(ing.Percentuale, 1);
                var f = string.IsNullOrWhiteSpace(ing.Fonte) ? "—" : ing.Fonte!;
                if (!string.IsNullOrWhiteSpace(ing.Riferimento)) f += " (" + ing.Riferimento + ")";
                wv.Cell(rv, 5).Value = f;
                rv++;
            }
            rv++;
            wv.Cell(rv, 2).Value = "Arrotondati per etichetta (100 g):";
            wv.Cell(rv, 2).Style.Font.Bold = true;
            rv++;
            wv.Range(rv, 2, rv, 5).Merge();
            wv.Cell(rv, 2).Value = RigaValori(r.Per100gArrotondato);
            wv.Cell(rv, 2).Style.Alignment.WrapText = true;
            rv++;
            wv.Cell(rv, 2).Value = "Non arrotondati (calcolati, 100 g):";
            wv.Cell(rv, 2).Style.Font.Bold = true;
            rv++;
            wv.Range(rv, 2, rv, 5).Merge();
            wv.Cell(rv, 2).Value = RigaValori(r.Per100g);
            wv.Cell(rv, 2).Style.Alignment.WrapText = true;
            rv++;
            var mt2 = d.MassaTotaleKg is decimal mm1 ? Fmt(mm1, 3) + " kg" : "—";
            var mc2 = d.MassaCottaKg is decimal mm2 ? " · massa cotta " + Fmt(mm2, 3) + " kg" : "";
            var rid2 = d.MassaTotaleKg is decimal mtot2 && mtot2 > 0 && d.MassaCottaKg is decimal mcot2
                ? " · calo peso " + Fmt((mtot2 - mcot2) / mtot2 * 100m, 1) + " %"
                : "";
            wv.Range(rv, 2, rv, 5).Merge();
            wv.Cell(rv, 2).Value = "Massa totale " + mt2 + mc2 + rid2;
            rv += 2;
            wv.Cell(rv, 2).Value = "Responsabilità del committente:";
            wv.Cell(rv, 2).Style.Font.Bold = true;
            rv++;
            wv.Range(rv, 2, rv, 5).Merge();
            wv.Cell(rv, 2).Value = TestiRapportoNutrizionale.ClausolaResponsabilita;
            wv.Cell(rv, 2).Style.Alignment.WrapText = true;
            wv.Row(rv).Height = 60;

            wv.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            wv.PageSetup.PaperSize = XLPaperSize.A4Paper;
        }

        using var flusso = new MemoryStream();
        wb.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(DatiRapportoNutrizionale d)
    {
        var nome = new string((d.Denominazione ?? "Rapporto")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Rapporto_Nutrizionale_{nome}_{d.DataRapporto:yyyyMMdd}.xlsx";
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Campo(IXLWorksheet ws, int riga, int colLabel, string label, int colVal, string valore)
    {
        ws.Cell(riga, colLabel).Value = label;
        ws.Cell(riga, colLabel).Style.Font.Bold = true;
        ws.Cell(riga, colVal).Value = valore;
    }

    private static void Titolo2(IXLWorksheet ws, int riga, int c0, int c1, string testo)
    {
        ws.Range(riga, c0, riga, c1).Merge();
        var cel = ws.Cell(riga, c0);
        cel.Value = testo;
        cel.Style.Font.Bold = true;
        cel.Style.Fill.BackgroundColor = XLColor.FromArgb(0xEE, 0xEE, 0xEE);
        cel.Style.Alignment.WrapText = true;
        ws.Range(riga, c0, riga, c1).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static int Riga(IXLWorksheet ws, int riga, string label,
        string v100, string u100, string vU, string uU, bool indent = false)
    {
        ws.Cell(riga, LabelSx).Value = (indent ? "   " : "") + label;
        ws.Cell(riga, ValSx).Value = v100;
        ws.Cell(riga, ValSx).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(riga, UnitSx).Value = u100;
        ws.Cell(riga, LabelDx).Value = (indent ? "   " : "") + label;
        ws.Cell(riga, ValDx).Value = vU;
        ws.Cell(riga, ValDx).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(riga, UnitDx).Value = uU;
        if (!indent) ws.Range(riga, LabelSx, riga, UnitSx).Style.Font.Bold = true;
        if (!indent) ws.Range(riga, LabelDx, riga, UnitDx).Style.Font.Bold = true;
        return riga + 1;
    }

    private static int RigaRi(IXLWorksheet ws, int riga, string label, string v100, string vU, bool indent = false)
    {
        ws.Cell(riga, LabelSx).Value = (indent ? "   " : "") + label;
        ws.Cell(riga, ValSx).Value = v100;
        ws.Cell(riga, ValSx).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(riga, LabelDx).Value = (indent ? "   " : "") + label;
        ws.Cell(riga, ValDx).Value = vU;
        ws.Cell(riga, ValDx).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        return riga + 1;
    }

    private static string RigaValori(Dichiarazione x) =>
        $"Energia {Fmt(x.EnergiaKcal, 0)} kcal / {Fmt(x.EnergiaKj, 0)} kJ · Grassi {Fmt(x.Grassi, 2)} · di cui saturi {Fmt(x.GrassiSaturi, 2)} · " +
        $"Carboidrati {Fmt(x.Carboidrati, 2)} · zuccheri {Fmt(x.Zuccheri, 2)} · Proteine {Fmt(x.Proteine, 2)} · Sale {Fmt(x.Sale, 2)} · Fibre {Fmt(x.Fibre, 2)}";

    private static void Nota(IXLWorksheet ws, int riga, string testo, bool grassetto = false)
    {
        ws.Range(riga, LabelSx, riga, UnitDx).Merge();
        var c = ws.Cell(riga, LabelSx);
        c.Value = testo;
        c.Style.Font.FontSize = 9;
        c.Style.Font.Bold = grassetto;
        c.Style.Alignment.WrapText = true;
    }

    private static int NotaLunga(IXLWorksheet ws, int riga, string testo)
    {
        ws.Range(riga, LabelSx, riga, UnitDx).Merge();
        var c = ws.Cell(riga, LabelSx);
        c.Value = testo;
        c.Style.Font.FontSize = 9;
        c.Style.Alignment.WrapText = true;
        ws.Row(riga).Height = 34;
        return riga + 1;
    }

    private static string Fmt(decimal v, int dec) => v.ToString("N" + dec, It);

    private static string Pct(decimal frazione) => (frazione * 100m).ToString("N1", It) + " %";

    private static IEnumerable<string> Note() =>
    [
        "1) Il calcolo è eseguito conformemente al Reg. (UE) n. 1169/2011, Art. 31 punto 4 lett. b): «I valori dichiarati sono valori medi stabiliti sulla base del calcolo effettuato a partire dai valori medi noti o effettivi relativi agli ingredienti utilizzati». Gli arrotondamenti seguono la Tabella 1 delle Linee guida sulle tolleranze analitiche (Ministero della Salute, Roma 16 giugno 2016); i valori energetici sono calcolati usando i valori dei nutrienti prima dell'arrotondamento.",
        "2) Il valore energetico è calcolato con i coefficienti di conversione dell'Allegato XIV del Reg. (UE) n. 1169/2011.",
        "3) I consumi di riferimento sono quelli dell'Allegato XIII, parte B, del Reg. (UE) n. 1169/2011 (adulto medio: 8400 kJ / 2000 kcal).",
        "4) La determinazione di cui al punto 1) potrebbe essere affetta da errori significativi dovuti ai processi tecnologici ai quali l'alimento è stato sottoposto e/o alla composizione variabile delle materie prime.",
        "5) I calcoli sono eseguiti sulla base della ricetta e/o del contenuto quantitativo in ingredienti così come comunicato dal committente, che se ne assume per intero la responsabilità."
    ];
}
