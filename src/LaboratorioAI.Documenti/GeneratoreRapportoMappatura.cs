using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di Mappatura di un'apparecchiatura termostatica (Modulo 04-03) in PDF
/// e Word. Riproduce la sintesi del foglio Report: dati apparecchiatura/CDR,
/// risultati principali, riepilogo per ripiano con conformità, intervallo ammesso,
/// giudizio dei test statistici e metodologia.
/// </summary>
public interface IGeneratoreRapportoMappatura
{
    byte[] GeneraPdf(DatiRapportoMappatura dati, string? percorsoWwwRoot = null);
    byte[] GeneraWord(DatiRapportoMappatura dati, string? percorsoWwwRoot = null);
    string NomeFile(DatiRapportoMappatura dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreRapportoMappatura : IGeneratoreRapportoMappatura
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiRapportoMappatura d, string? percorsoWwwRoot = null)
    {
        var doc = Costruisci(d, percorsoWwwRoot);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public byte[] GeneraWord(DatiRapportoMappatura d, string? percorsoWwwRoot = null)
    {
        var doc = Costruisci(d, percorsoWwwRoot);
        var tmp = Path.Combine(Path.GetTempPath(), "labai_map_" + Guid.NewGuid().ToString("N") + ".rtf");
        try { new RtfDocumentRenderer().Render(doc, tmp, Path.GetTempPath()); return File.ReadAllBytes(tmp); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public string NomeFile(DatiRapportoMappatura d, string estensione)
    {
        var nome = NomiFileRapporti.Slug(d.ApparecchiaturaIdentificazione, "Mappatura");
        var cli = NomiFileRapporti.Slug(d.Committente, "");
        return $"Rapporto_Mappatura{(cli.Length == 0 ? "" : "_" + cli)}_{nome}_{d.DataMappatura:yyyyMMdd}.{estensione}";
    }

    private static Document Costruisci(DatiRapportoMappatura d, string? percorsoWwwRoot)
    {
        var r = d.Risultato;
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 9;

        var sec = doc.AddSection();
        CartaIntestataRapporti.Applica(sec, percorsoWwwRoot, margineLato: 1.8);   // 03/11/2026: come i termometri

        var tit = sec.AddParagraph("RAPPORTO DI MAPPATURA — APPARECCHIATURA TERMOSTATICA");
        tit.Format.Alignment = ParagraphAlignment.Center; tit.Format.Font.Bold = true; tit.Format.Font.Size = 12.5;
        tit.Format.SpaceBefore = "0.25cm";
        var sub = sec.AddParagraph("Modulo Statistico 04-03 — Mappatura apparecchiature termostatiche");
        sub.Format.Alignment = ParagraphAlignment.Center; sub.Format.Font.Size = 8; sub.Format.SpaceAfter = "0.2cm";

        // Testata
        var t = TabellaInfo(sec);
        RigaInfo(t, "Rapporto n°", d.NumeroRapporto, "Data mappatura", d.DataMappatura.ToString("dd/MM/yyyy", It));
        RigaInfo(t, "Rif. istruzione", d.RiferimentoIstruzione ?? "—",
            "Data scadenza", d.DataScadenza is DateOnly s ? s.ToString("dd/MM/yyyy", It) : "—");
        RigaInfo(t, "Committente", CartaIntestataRapporti.Committente(d.Committente), "Redatto da", d.RedattoDa ?? "—");
        if (!string.IsNullOrWhiteSpace(d.Insediamento)) RigaInfo(t, "Insediamento", d.Insediamento!, "", "");

        Titolo(sec, "Apparecchiatura e condizioni");
        var ap = TabellaInfo(sec);
        RigaInfo(ap, "Apparecchiatura", d.ApparecchiaturaIdentificazione, "Codice", d.ApparecchiaturaCodice ?? "—");
        RigaInfo(ap, "Temperatura di esercizio", $"{Num(d.ValoreNominale, 2)} ± {Num(d.Tolleranza, 2)} °C", "", "");

        Titolo(sec, "Campione di riferimento (CDR)");
        var cd = TabellaInfo(sec);
        RigaInfo(cd, "Identificazione", d.CdrIdentificazione, "Certificato n°", d.CdrNumeroCertificato ?? "—");
        RigaInfo(cd, "Tarato il", d.CdrDataTaratura is DateOnly ct ? ct.ToString("dd/MM/yyyy", It) : "—",
            "Scadenza CDR", d.CdrDataScadenza is DateOnly cs ? cs.ToString("dd/MM/yyyy", It) : "—");

        // Risultati principali
        Titolo(sec, "Risultati principali");
        var rp = TabellaInfo(sec);
        RigaInfo(rp, "Temperatura media Tr", $"{Num(r.TemperaturaMedia, 3)} °C",
            "Differenza dal nominale", $"{Num(r.DifferenzaNominale, 3)} °C");
        RigaInfo(rp, "Scarto tipo della media (1)", $"{Num(r.ScartoTipoMedia, 4)} °C",
            "Incertezza estesa U", $"{Num(r.IncertezzaEstesa, 3)} °C");
        RigaInfo(rp, "Temp. ammessa minima", $"{Num(r.IntervalloMinimo, 2)} °C",
            "Temp. ammessa massima", $"{Num(r.IntervalloMassimo, 2)} °C");

        // Riepilogo per ripiano
        Titolo(sec, "Riepilogo per ripiano / posizione");
        var tr = sec.AddTable();
        tr.Borders.Width = 0.5; tr.Borders.Color = new Color(0xFFBBBBBB);
        tr.Format.Font.Size = 8.5; tr.Format.SpaceAfter = "0.05cm";
        foreach (var w in new[] { 3.0, 3.3, 3.3, 3.3, 4.5 }) tr.AddColumn(Unit.FromCentimeter(w));
        var hr = tr.AddRow(); hr.Format.Font.Bold = true; hr.Shading.Color = new Color(0xFFF3F3F3);
        foreach (var (txt, i) in new[] { "Ripiano", "Media [°C]", "Min [°C]", "Max [°C]", "Conforme" }.Select((x, i) => (x, i)))
            hr.Cells[i].AddParagraph(txt);
        foreach (var rip in r.Ripiani)
        {
            var row = tr.AddRow();
            if (!rip.Conforme) row.Shading.Color = new Color(0xFFFDECEA);
            row.Cells[0].AddParagraph(rip.Posizione).Format.Alignment = ParagraphAlignment.Center;
            row.Cells[1].AddParagraph(Num(rip.Media, 3)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[2].AddParagraph(Num(rip.Minimo, 3)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[3].AddParagraph(Num(rip.Massimo, 3)).Format.Alignment = ParagraphAlignment.Right;
            var pc = row.Cells[4].AddParagraph(rip.Conforme ? "entro intervallo" : "FUORI intervallo");
            pc.Format.Font.Bold = true;
            pc.Format.Font.Color = rip.Conforme ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        }

        // Giudizio dei test statistici
        var giudizio = sec.AddParagraph(r.Giudizio);
        giudizio.Format.SpaceBefore = "0.25cm"; giudizio.Format.Font.Bold = true;
        giudizio.Format.Font.Color = r.DifferenzeSignificative ? new Color(0xFFB3261E) : new Color(0xFF1A7F37);
        giudizio.Format.Shading.Color = new Color(0xFFF3F3F3);

        var dett = sec.AddParagraph(
            $"Test F di Fisher: F = {Num(r.FisherF, 3)} (critico {Num(r.FisherFCritico, 3)}); "
            + $"omogeneità delle varianze (Cochran): {(r.VarianzeOmogenee ? "verificata" : "non verificata")}; "
            + $"Kruskal-Wallis: H = {Num(r.KruskalWallis, 3)}, p = {Num(r.KruskalWallisP, 4)}.");
        dett.Format.Font.Size = 8; dett.Format.Font.Italic = true; dett.Format.SpaceBefore = "0.05cm";

        // Metodologia
        var nota = sec.AddParagraph(
            "Metodologia: registrazione dei valori del CDR nelle diverse posizioni (A, B, C, D) nelle comuni "
            + "condizioni d'uso. L'omogeneità tra le posizioni è valutata con test F (ANOVA), Cochran e, in caso di "
            + "varianze non omogenee, Kruskal-Wallis. Le incertezze sono espresse come incertezza estesa (k = 2, "
            + "livello di fiducia ~95%), conformemente a EA-4/02 e alla guida sulle camere climatiche.");
        nota.Format.Font.Size = 8; nota.Format.Font.Italic = true; nota.Format.SpaceBefore = "0.3cm";

        var notaScarto = sec.AddParagraph(
            "(1) Lo scarto tipo della media è derivato dalle misure tra le diverse posizioni e può essere "
            + "utilizzato per la taratura della stessa apparecchiatura.");
        notaScarto.Format.Font.Size = 8; notaScarto.Format.Font.Italic = true; notaScarto.Format.SpaceBefore = "0.1cm";

        var firmaLbl = sec.AddParagraph("Responsabile del Rapporto");
        firmaLbl.Format.Font.Bold = true; firmaLbl.Format.SpaceBefore = "0.7cm"; firmaLbl.Format.Alignment = ParagraphAlignment.Center;
        var firmaNome = sec.AddParagraph(d.RedattoDa ?? "");
        firmaNome.Format.SpaceBefore = "0.05cm"; firmaNome.Format.Alignment = ParagraphAlignment.Center;

        return doc;
    }

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
        t.AddColumn(Unit.FromCentimeter(4.3)); t.AddColumn(Unit.FromCentimeter(4.6));
        t.AddColumn(Unit.FromCentimeter(4.3)); t.AddColumn(Unit.FromCentimeter(4.2));
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
