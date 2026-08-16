using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di Taratura di un incubatore/cella (Modulo Statistico 04-02) in PDF e
/// Word. Riproduce la pagina di rapporto del foglio storico: strumenti, CDR,
/// modalità, dati riepilogativi, regolazione allarmi e giudizio di conformità.
/// </summary>
public interface IGeneratoreRapportoIncubatore
{
    byte[] GeneraPdf(DatiRapportoIncubatore dati);
    byte[] GeneraWord(DatiRapportoIncubatore dati);
    string NomeFile(DatiRapportoIncubatore dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreRapportoIncubatore : IGeneratoreRapportoIncubatore
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiRapportoIncubatore d)
    {
        var doc = Costruisci(d);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public byte[] GeneraWord(DatiRapportoIncubatore d)
    {
        var doc = Costruisci(d);
        var tmp = Path.Combine(Path.GetTempPath(), "labai_inc_" + Guid.NewGuid().ToString("N") + ".rtf");
        try
        {
            new RtfDocumentRenderer().Render(doc, tmp, Path.GetTempPath());
            return File.ReadAllBytes(tmp);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public string NomeFile(DatiRapportoIncubatore d, string estensione)
    {
        var nome = new string((d.MisuratoreIdentificazione ?? "Incubatore")
            .Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Rapporto_Taratura_Incubatore_{nome}_{d.DataTaratura:yyyyMMdd}.{estensione}";
    }

    private static Document Costruisci(DatiRapportoIncubatore d)
    {
        var r = d.Risultato;
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 9;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.0);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.0);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);

        var h = sec.AddParagraph("MARE.A s.r.l"); h.Format.Font.Bold = true; h.Format.Font.Size = 12;
        Piccolo(sec, "Sede legale e operativa: Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • Fax 0541 830460");
        Piccolo(sec, "info@mare-a.com • www.mare-a.com • Cod. Fisc. e P. Iva 03670240401 • Laboratorio Analisi");

        var tit = sec.AddParagraph("RAPPORTO DI TARATURA");
        tit.Format.Alignment = ParagraphAlignment.Center; tit.Format.Font.Bold = true; tit.Format.Font.Size = 13;
        tit.Format.SpaceBefore = "0.25cm";
        var sub = sec.AddParagraph("Modulo Statistico 04-02 — Taratura termostati, frigoriferi e incubatori");
        sub.Format.Alignment = ParagraphAlignment.Center; sub.Format.Font.Size = 8; sub.Format.SpaceAfter = "0.2cm";

        // Testata
        var t = TabellaInfo(sec);
        RigaInfo(t, "Rapporto n°", d.NumeroRapporto, "Data taratura", d.DataTaratura.ToString("dd/MM/yyyy", It));
        RigaInfo(t, "Rif. istruzione", d.RiferimentoIstruzione ?? "—",
            "Data scadenza", d.DataScadenza is DateOnly s ? s.ToString("dd/MM/yyyy", It) : "—");
        RigaInfo(t, "Committente", d.Committente ?? "—", "Redatto da", d.RedattoDa ?? "—");

        // Strumenti
        Titolo(sec, "Strumenti");
        var us = TabellaInfo(sec);
        RigaInfo(us, "Misuratore in taratura", d.MisuratoreIdentificazione, "Codice", d.MisuratoreCodice ?? "—");
        RigaInfo(us, "Apparecchiatura", d.ApparecchiaturaIdentificazione, "Codice", d.ApparecchiaturaCodice ?? "—");
        RigaInfo(us, "Temperatura di esercizio", $"{Num(d.ValoreNominale, 2)} ± {Num(d.Tolleranza, 2)} °C",
            "Risoluzione misuratore", $"{Num(d.RisoluzioneStrumento, 3)} °C");

        // CDR
        Titolo(sec, "Campione di riferimento (CDR)");
        var cd = TabellaInfo(sec);
        RigaInfo(cd, "Identificazione", d.CdrIdentificazione, "Centro LAT", d.CdrCentroLat ?? "—");
        RigaInfo(cd, "Certificato n°", d.CdrNumeroCertificato ?? "—",
            "Risoluzione CDR", $"{Num(d.CdrRisoluzione, 3)} °C");
        RigaInfo(cd, "Tarato il", d.CdrDataTaratura is DateOnly ct ? ct.ToString("dd/MM/yyyy", It) : "—",
            "Scadenza CDR", d.CdrDataScadenza is DateOnly cs ? cs.ToString("dd/MM/yyyy", It) : "—");

        // Modalità (corsivo)
        var metTit = sec.AddParagraph("Modalità di taratura:");
        metTit.Format.Font.Bold = true; metTit.Format.SpaceBefore = "0.2cm";
        var met = sec.AddParagraph(
            "La verifica è stata eseguita confrontando i valori del termometro campione (CDR) con il misuratore "
            + "di temperatura dell'apparecchiatura nelle comuni condizioni di utilizzo, alla temperatura di esercizio "
            + "indicata. I risultati si riferiscono esclusivamente all'oggetto in taratura e sono validi nel momento "
            + "e nelle condizioni di taratura, salvo diversamente specificato.");
        met.Format.Font.Size = 8.5; met.Format.Font.Italic = true;

        // Dati riepilogativi
        Titolo(sec, "Dati riepilogativi della taratura");
        var ris = sec.AddTable();
        ris.Borders.Width = 0.5; ris.Borders.Color = new Color(0xFFBBBBBB);
        ris.Format.Font.Size = 8.5; ris.Format.SpaceAfter = "0.05cm";
        double[] larg = { 3.6, 2.9, 2.9, 2.9, 2.9, 1.8 };
        foreach (var w in larg) ris.AddColumn(Unit.FromCentimeter(w));
        var hr = ris.AddRow(); hr.Format.Font.Bold = true; hr.Shading.Color = new Color(0xFFF3F3F3);
        string[] intest = ["Strumento", "Rif. Tr [°C]", "Strum. Tw [°C]", "Diff. Tr−Tw [°C]", "Incert. U (1) [°C]", "N° punti"];
        for (int i = 0; i < intest.Length; i++) hr.Cells[i].AddParagraph(intest[i]).Format.Alignment = ParagraphAlignment.Center;
        var rr = ris.AddRow();
        rr.Cells[0].AddParagraph(d.MisuratoreIdentificazione);
        rr.Cells[1].AddParagraph(Num(r.TemperaturaMediaRiferimento, 2)).Format.Alignment = ParagraphAlignment.Right;
        rr.Cells[2].AddParagraph(Num(r.TemperaturaMediaStrumento, 2)).Format.Alignment = ParagraphAlignment.Right;
        rr.Cells[3].AddParagraph(Num(r.FattoreCorrezione, 3)).Format.Alignment = ParagraphAlignment.Right;
        rr.Cells[4].AddParagraph(Num(r.IncertezzaEstesa, 3)).Format.Alignment = ParagraphAlignment.Right;
        rr.Cells[5].AddParagraph(r.NumeroLetture.ToString(It)).Format.Alignment = ParagraphAlignment.Center;

        var notaU = sec.AddParagraph(
            "(1) L'incertezza estesa include ripetibilità, incertezza del campione di riferimento e sua instabilità, "
            + "risoluzione del misuratore e dispersione di mappatura; k = 2.");
        notaU.Format.Font.Size = 8; notaU.Format.Font.Italic = true; notaU.Format.SpaceBefore = "0.05cm";

        // Fattore di correzione e regolazione allarmi
        var corr = sec.AddParagraph();
        corr.Format.SpaceBefore = "0.2cm";
        corr.AddFormattedText("Fattore di correzione del misuratore: ", TextFormat.Bold);
        corr.AddText($"{Num(r.FattoreCorrezione, 3)} °C   (incertezza {Num(r.IncertezzaEstesa, 3)} °C)");

        var allarmi = sec.AddParagraph();
        allarmi.AddFormattedText("Regolare gli allarmi tra ", TextFormat.Bold);
        allarmi.AddText($"{Num(r.AllarmeMinimo, 2)} °C  e  {Num(r.AllarmeMassimo, 2)} °C");

        // Giudizio di conformità
        var giudizio = sec.AddParagraph(r.Conforme
            ? "LA TARATURA È CONFORME — l'incertezza estesa è inferiore al 50% del limite di tolleranza"
            : "LA TARATURA NON È CONFORME — l'incertezza estesa è superiore al 50% del limite di tolleranza");
        giudizio.Format.SpaceBefore = "0.25cm";
        giudizio.Format.Font.Bold = true;
        giudizio.Format.Font.Color = r.Conforme ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        giudizio.Format.Shading.Color = new Color(0xFFF3F3F3);

        // Note
        var nota = sec.AddParagraph(
            "Le incertezze di misura dichiarate sono determinate conformemente al documento EA-4/02 e alla «Guide to "
            + "calculating the uncertainty of the performance of environmental chambers», e sono espresse come incertezza "
            + "estesa ottenuta moltiplicando l'incertezza tipo per il fattore di copertura k corrispondente a un livello "
            + "di fiducia di circa il 95%. Normalmente tale fattore vale 2.");
        nota.Format.Font.Size = 8; nota.Format.Font.Italic = true; nota.Format.SpaceBefore = "0.3cm";

        // Firma centrata
        var firmaLbl = sec.AddParagraph("Responsabile del Rapporto");
        firmaLbl.Format.Font.Bold = true; firmaLbl.Format.SpaceBefore = "0.7cm";
        firmaLbl.Format.Alignment = ParagraphAlignment.Center;
        var firmaNome = sec.AddParagraph(d.RedattoDa ?? "");
        firmaNome.Format.SpaceBefore = "0.05cm";
        firmaNome.Format.Alignment = ParagraphAlignment.Center;

        return doc;
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Piccolo(Section sec, string testo) => sec.AddParagraph(testo).Format.Font.Size = 8;

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true; p.Format.SpaceBefore = "0.2cm"; p.Format.SpaceAfter = "0.05cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }

    private static Table TabellaInfo(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0; t.Format.SpaceAfter = "0.05cm"; t.Format.Font.Size = 8.5;
        t.AddColumn(Unit.FromCentimeter(4.0)); t.AddColumn(Unit.FromCentimeter(4.5));
        t.AddColumn(Unit.FromCentimeter(4.0)); t.AddColumn(Unit.FromCentimeter(4.5));
        return t;
    }

    private static void RigaInfo(Table t, string l1, string v1, string l2, string v2)
    {
        var r = t.AddRow();
        r.Cells[0].AddParagraph(l1).Format.Font.Bold = true;
        r.Cells[1].AddParagraph(v1);
        if (!string.IsNullOrEmpty(l2)) r.Cells[2].AddParagraph(l2).Format.Font.Bold = true;
        if (!string.IsNullOrEmpty(v2)) r.Cells[3].AddParagraph(v2);
    }

    private static string Num(double v, int dec) => v.ToString("N" + dec, It);
}
