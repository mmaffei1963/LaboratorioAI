using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di stima dei valori nutrizionali (Reg. UE 1169/2011) generato con
/// MigraDoc: un'unica definizione del documento produce sia il PDF sia il file
/// Word (RTF, apribile e modificabile in Word). Le note esplicative sono
/// riportate verbatim dal documento originale MARE.A.
/// </summary>
public interface IGeneratoreRapportoNutrizionale
{
    byte[] GeneraPdf(DatiRapportoNutrizionale dati);
    byte[] GeneraWord(DatiRapportoNutrizionale dati);
    string NomeFile(DatiRapportoNutrizionale dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreRapportoNutrizionale : IGeneratoreRapportoNutrizionale
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private const string UtenteSviluppo = "Utente di sviluppo";

    public byte[] GeneraPdf(DatiRapportoNutrizionale d)
    {
        var doc = Costruisci(d);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public byte[] GeneraWord(DatiRapportoNutrizionale d)
    {
        var doc = Costruisci(d);
        var tmp = Path.Combine(Path.GetTempPath(), "labai_rapporto_" + Guid.NewGuid().ToString("N") + ".rtf");
        try
        {
            new RtfDocumentRenderer().Render(doc, tmp, Path.GetTempPath());
            return File.ReadAllBytes(tmp);
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public string NomeFile(DatiRapportoNutrizionale d, string estensione)
    {
        var nome = new string((d.Denominazione ?? "Rapporto")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Rapporto_Nutrizionale_{nome}_{d.DataRapporto:yyyyMMdd}.{estensione}";
    }

    // ── Costruzione del documento ──────────────────────────────────
    private static Document Costruisci(DatiRapportoNutrizionale d)
    {
        var r = d.Risultato;
        var a = r.Per100gArrotondato;
        var aU = r.PerUnitaArrotondato;
        var ri = r.RiferimentiPer100g;
        var riU = r.RiferimentiPerUnita;

        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 10;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);

        // Intestazione
        var intest = sec.AddParagraph("MARE.A s.r.l");
        intest.Format.Font.Bold = true;
        intest.Format.Font.Size = 12;
        Piccolo(sec, "Sede legale: Via E. Toti, 2 - 47841 Cattolica (RN)");
        Piccolo(sec, "Sede operativa: Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • Fax 0541 830460");
        Piccolo(sec, "info@mare-a.com • www.mare-a.com");
        Piccolo(sec, "Cod. Fisc. e P. Iva 03670240401");

        var tit = sec.AddParagraph("RAPPORTO DI STIMA DEI VALORI NUTRIZIONALI");
        tit.Format.Alignment = ParagraphAlignment.Center;
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 14;
        tit.Format.SpaceBefore = "0.5cm";
        tit.Format.SpaceAfter = "0.3cm";

        // Testata (tabella senza bordi)
        var pezzi = d.UnitaConsumoPezzi is decimal p ? Fmt(p, 0) : "—";
        var testata = TabellaInfo(sec);
        RigaInfo(testata, "Rapporto del", d.DataRapporto.ToString("dd/MM/yyyy", It), "Redatto da", RedattoDa(d.RedattoDa));
        RigaInfo(testata, "Denominazione", d.Denominazione, "Lotto", string.IsNullOrWhiteSpace(d.Lotto) ? "—" : d.Lotto!);
        RigaInfo(testata, "Committente", d.Committente ?? "—", "Unità di consumo", $"{Fmt(d.UnitaConsumoG, 0)} g / {pezzi} pz");

        // Dichiarazione nutrizionale
        Titolo(sec, "DICHIARAZIONE NUTRIZIONALE");
        var dic = TabellaValori(sec, "Elemento nutritivo", "per 100 g", $"per unità di consumo ({Fmt(d.UnitaConsumoG, 0)} g)");
        RigaVal(dic, "Energia", $"{Fmt(a.EnergiaKj, 0)} kJ / {Fmt(a.EnergiaKcal, 0)} kcal", $"{Fmt(aU.EnergiaKj, 0)} kJ / {Fmt(aU.EnergiaKcal, 0)} kcal");
        RigaVal(dic, "Grassi", G(a.Grassi, 1), G(aU.Grassi, 1));
        RigaVal(dic, "di cui acidi grassi saturi", G(a.GrassiSaturi, 1), G(aU.GrassiSaturi, 1), true);
        RigaVal(dic, "Carboidrati", G(a.Carboidrati, a.Carboidrati > 10 ? 0 : 1), G(aU.Carboidrati, aU.Carboidrati > 10 ? 0 : 1));
        RigaVal(dic, "di cui zuccheri", G(a.Zuccheri, a.Zuccheri > 10 ? 0 : 1), G(aU.Zuccheri, aU.Zuccheri > 10 ? 0 : 1), true);
        RigaVal(dic, "Proteine", G(a.Proteine, 1), G(aU.Proteine, 1));
        RigaVal(dic, "Sale", G(a.Sale, 2), G(aU.Sale, 2));
        RigaVal(dic, "Fibre", G(a.Fibre, 1), G(aU.Fibre, 1));

        // Assunzioni di riferimento
        Titolo(sec, "ASSUNZIONE DI RIFERIMENTO DI UN ADULTO MEDIO (8400 kJ / 2000 kcal)");
        var rif = TabellaValori(sec, "% delle assunzioni di riferimento", "per 100 g", "per unità di consumo");
        RigaVal(rif, "Energia", Pct(ri.EnergiaKcal), Pct(riU.EnergiaKcal));
        RigaVal(rif, "Grassi", Pct(ri.Grassi), Pct(riU.Grassi));
        RigaVal(rif, "di cui acidi grassi saturi", Pct(ri.GrassiSaturi), Pct(riU.GrassiSaturi), true);
        RigaVal(rif, "Carboidrati", Pct(ri.Carboidrati), Pct(riU.Carboidrati));
        RigaVal(rif, "di cui zuccheri", Pct(ri.Zuccheri), Pct(riU.Zuccheri), true);
        RigaVal(rif, "Proteine", Pct(ri.Proteine), Pct(riU.Proteine));
        RigaVal(rif, "Sale", Pct(ri.Sale), Pct(riU.Sale));
        RigaVal(rif, "Fibre", "n.d.", "n.d.", true);

        // Fonti e note (verbatim)
        Nota(sec, Fonti1, spazioPrima: true, grassetto: true);
        Nota(sec, Fonti2);
        foreach (var linea in NoteEsplicative.Split('\n'))
            Nota(sec, linea, grassetto: linea.Trim() == "Note esplicative:");

        // Firma (allineata a destra, come in un documento classico)
        var firma = sec.AddParagraph("MARE.A srl - Responsabile rapporto");
        firma.Format.SpaceBefore = "0.8cm";
        firma.Format.Alignment = ParagraphAlignment.Right;
        firma.Format.RightIndent = Unit.FromCentimeter(1.5);
        var nomeRed = sec.AddParagraph(RedattoDa(d.RedattoDa));
        nomeRed.Format.Font.Bold = true;
        nomeRed.Format.Alignment = ParagraphAlignment.Right;
        nomeRed.Format.RightIndent = Unit.FromCentimeter(1.5);

        // ── Seconda pagina: scheda di verifica ingredienti e fonti ──
        if (d.Ingredienti.Count > 0)
        {
            var tit2 = sec.AddParagraph("SCHEDA DI VERIFICA DATI — INGREDIENTI E FONTI");
            tit2.Format.PageBreakBefore = true;
            tit2.Format.Font.Bold = true;
            tit2.Format.Font.Size = 12;
            tit2.Format.SpaceAfter = "0.3cm";
            var sub = sec.AddParagraph("Dati da verificare a cura del committente.");
            sub.Format.Font.Size = 9;
            sub.Format.SpaceAfter = "0.2cm";

            var tab = sec.AddTable();
            tab.Borders.Width = 0.5;
            tab.AddColumn(Unit.FromCentimeter(6.5));
            tab.AddColumn(Unit.FromCentimeter(2.5));
            tab.AddColumn(Unit.FromCentimeter(1.5));
            tab.AddColumn(Unit.FromCentimeter(6.5));
            var h = tab.AddRow();
            h.Format.Font.Bold = true;
            h.Format.Shading.Color = new Color(0xFFEEEEEE);
            h.Cells[0].AddParagraph("Ingrediente");
            h.Cells[1].AddParagraph("Quantità (kg)");
            h.Cells[2].AddParagraph("%");
            h.Cells[3].AddParagraph("Fonte dei valori");
            foreach (var ing in d.Ingredienti)
            {
                var row = tab.AddRow();
                row.Cells[0].AddParagraph(ing.Denominazione);
                row.Cells[1].AddParagraph(Fmt(ing.QuantitaKg, 3));
                row.Cells[2].AddParagraph(Fmt(ing.Percentuale, 1));
                var f = string.IsNullOrWhiteSpace(ing.Fonte) ? "—" : ing.Fonte!;
                if (!string.IsNullOrWhiteSpace(ing.Riferimento)) f += " (" + ing.Riferimento + ")";
                row.Cells[3].AddParagraph(f);
            }

            // Riepilogo valori (arrotondati per etichetta e non arrotondati) + masse
            Nota(sec, "Arrotondati per etichetta (100 g): " + RigaValoriTesto(a), spazioPrima: true, grassetto: true);
            Nota(sec, "Non arrotondati (calcolati, 100 g): " + RigaValoriTesto(r.Per100g));
            var mt = d.MassaTotaleKg is decimal m1 ? Fmt(m1, 3) + " kg" : "—";
            var mc = d.MassaCottaKg is decimal m2 ? " · massa cotta " + Fmt(m2, 3) + " kg" : "";
            var rid = d.MassaTotaleKg is decimal mtot && mtot > 0 && d.MassaCottaKg is decimal mcot
                ? " · calo peso " + Fmt((mtot - mcot) / mtot * 100m, 1) + " %"
                : "";
            Nota(sec, "Massa totale " + mt + mc + rid);

            Nota(sec, "Responsabilità del committente:", spazioPrima: true, grassetto: true);
            Nota(sec, TestiRapportoNutrizionale.ClausolaResponsabilita);
        }

        return doc;
    }

    // ── Helper documento ───────────────────────────────────────────
    private static void Piccolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
    }

    private static string RedattoDa(string? nome) =>
        string.IsNullOrWhiteSpace(nome) || nome == UtenteSviluppo ? "—" : nome!;

    private static Table TabellaInfo(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.Format.SpaceAfter = "0.2cm";
        t.AddColumn(Unit.FromCentimeter(3.2));
        t.AddColumn(Unit.FromCentimeter(5.3));
        t.AddColumn(Unit.FromCentimeter(3.2));
        t.AddColumn(Unit.FromCentimeter(5.3));
        return t;
    }

    private static void RigaInfo(Table t, string l1, string v1, string l2, string v2)
    {
        var row = t.AddRow();
        row.Cells[0].AddParagraph(l1).Format.Font.Bold = true;
        row.Cells[1].AddParagraph(v1);
        row.Cells[2].AddParagraph(l2).Format.Font.Bold = true;
        row.Cells[3].AddParagraph(v2);
    }

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = "0.4cm";
        p.Format.SpaceAfter = "0.1cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }

    private static Table TabellaValori(Section sec, string intest, string col2, string col3)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = new Color(0xFFBBBBBB);
        t.AddColumn(Unit.FromCentimeter(8.5));
        t.AddColumn(Unit.FromCentimeter(4.3));
        t.AddColumn(Unit.FromCentimeter(4.3));
        var h = t.AddRow();
        h.Format.Font.Bold = true;
        h.Shading.Color = new Color(0xFFF3F3F3);
        h.Cells[0].AddParagraph(intest);
        h.Cells[1].AddParagraph(col2).Format.Alignment = ParagraphAlignment.Right;
        h.Cells[2].AddParagraph(col3).Format.Alignment = ParagraphAlignment.Right;
        return t;
    }

    private static void RigaVal(Table t, string label, string v100, string vUnita, bool indent = false)
    {
        var row = t.AddRow();
        var pl = row.Cells[0].AddParagraph((indent ? "    " : "") + label);
        if (!indent) pl.Format.Font.Bold = true;
        row.Cells[1].AddParagraph(v100).Format.Alignment = ParagraphAlignment.Right;
        row.Cells[2].AddParagraph(vUnita).Format.Alignment = ParagraphAlignment.Right;
    }

    private static void Nota(Section sec, string testo, bool spazioPrima = false, bool grassetto = false)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
        p.Format.Font.Bold = grassetto;
        if (spazioPrima) p.Format.SpaceBefore = "0.4cm";
    }

    private static string G(decimal v, int dec) => Fmt(v, dec) + " g";
    private static string Fmt(decimal v, int dec) => v.ToString("N" + dec, It);

    private static string RigaValoriTesto(Dichiarazione x) =>
        $"Energia {Fmt(x.EnergiaKcal, 0)} kcal / {Fmt(x.EnergiaKj, 0)} kJ · Grassi {Fmt(x.Grassi, 2)} · di cui saturi {Fmt(x.GrassiSaturi, 2)} · " +
        $"Carboidrati {Fmt(x.Carboidrati, 2)} · zuccheri {Fmt(x.Zuccheri, 2)} · Proteine {Fmt(x.Proteine, 2)} · Sale {Fmt(x.Sale, 2)} · Fibre {Fmt(x.Fibre, 2)}";
    private static string Pct(decimal frazione) => (frazione * 100m).ToString("N1", It) + " %";

    // ── Note esplicative — VERBATIM dal documento originale MARE.A ──
    private const string Fonti1 =
        "I valori nutrizionali  dei singoli ingredienti sono stati ricavati dalle seguenti fonti in ordine di priorità:";

    private const string Fonti2 =
        "Schede tecniche produttori ingredienti, Database INRAN e/o IEO, EUROFIR Food Composition DataBases , Altre fonti bibliografiche";

    private const string NoteEsplicative =
        "Note esplicative:\n" +
        "1)Per il calcolo dei valori contenuti nel presente Rapporto si è proceduco conformemente a quanto prescritto dal REGOLAMENTO (UE) N. 1169/2011 DEL PARLAMENTO EUROPEO E DEL CONSIGLIO del 25 ottobre 2011 all' Art. 31 punto 4.  b)\" I valori dichiarati sono valori medi stabiliti sulla base  del calcolo effettuato a partire dai valori medi noti o effettivi relativi agli ingredienti utilizzati\". Gli arrotondamenti vengono eseguiti sulla base della tabella 1: Regole di Arrotondamento Applicabili alla Dichiarazione dei Nutrienti in Linee Guida sulle Tolleranze Analitiche Applicabili in Fase di Controllo Ufficiale del Ministero della Salute - Roma 16 giugno 2016. In particolare, per meglio rappresentare i valori nutrizionali dell'alimento,  i calcoli dai quali derivano i valori del contenuto energetico, vengono eseguiti utilizzando i  valori dei nutrienti  prima dell'arrotondamento, successivamente i valori ottenuti, espressi in kJ/100g e kcal/100g,  vengono arrotondati attenendosi ai criteri della tabella 1 sopra citata.\n" +
        "2)Il valore energetico è calcolato mediante i coefficienti di conversione elencati nell’allegato XIV  de  REGOLAMENTO (UE) N. 1169/2011 DEL PARLAMENTO EUROPEO E DEL CONSIGLIO del 25 ottobre 2011.\n" +
        "3) I consumi di riferimento di elementi energetici e di determinati elementi  nutritivi  utilizzati, sono quelli riportati nell'Allegato XIII parte B) del  REGOLAMENTO (UE) N. 1169/2011 DEL PARLAMENTO EUROPEO E DEL CONSIGLIO del 25 ottobre 2011.\n" +
        "4) La MARE.A srl ha comunicato al committente che la determinazione di cui al punto 1) potrebbe essere affetta da errori significativi dovuti ai processi tecnologici ai quali l'alimento è stato sottoposto e/o alla composizione variabile delle materie prime.\n" +
        "5) I calcoli di cui al punto 1) vengono eseguiti sulla base della ricetta e/o del contenuto quantitativo in ingredienti così come comunicato dal committente che se ne assume per intero la responsabilità.";
}
