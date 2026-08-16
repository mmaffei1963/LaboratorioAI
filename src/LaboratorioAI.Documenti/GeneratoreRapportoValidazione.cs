using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di validazione del motore di calcolo della taratura (PDF): confronto
/// dei risultati odierni con i valori attesi indipendenti dei casi di riferimento
/// congelati. Evidenzia VALIDATO / NON VALIDATO ed elenca ogni confronto.
/// </summary>
public interface IGeneratoreRapportoValidazione
{
    byte[] GeneraPdf(EsitoValidazioneCalcoli esito, string? operatore);
    string NomeFile(EsitoValidazioneCalcoli esito);
}

/// <inheritdoc />
public class GeneratoreRapportoValidazione : IGeneratoreRapportoValidazione
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(EsitoValidazioneCalcoli e, string? operatore)
    {
        var doc = Costruisci(e, operatore);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public string NomeFile(EsitoValidazioneCalcoli e) =>
        $"Rapporto_Validazione_Calcolo_{e.DataEsecuzione.ToLocalTime():yyyyMMdd_HHmm}.pdf";

    private static Document Costruisci(EsitoValidazioneCalcoli e, string? operatore)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 9;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.0);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.0);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(1.8);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(1.8);

        var h = sec.AddParagraph("MARE.A s.r.l"); h.Format.Font.Bold = true; h.Format.Font.Size = 12;
        Piccolo(sec, "Laboratorio Analisi • Via E. Toti, 2 — 47841 Cattolica (RN)");

        var tit = sec.AddParagraph("RAPPORTO DI VALIDAZIONE DEL CALCOLO");
        tit.Format.Alignment = ParagraphAlignment.Center; tit.Format.Font.Bold = true; tit.Format.Font.Size = 13;
        tit.Format.SpaceBefore = "0.25cm";
        var sub = sec.AddParagraph("Motore di calcolo — verifica sui casi di riferimento congelati");
        sub.Format.Alignment = ParagraphAlignment.Center; sub.Format.Font.Size = 8; sub.Format.SpaceAfter = "0.2cm";

        // Esito complessivo, ben evidente.
        var esito = sec.AddParagraph(e.Validato ? "ESITO: VALIDATO" : "ESITO: NON VALIDATO");
        esito.Format.Alignment = ParagraphAlignment.Center;
        esito.Format.Font.Bold = true; esito.Format.Font.Size = 14;
        esito.Format.Font.Color = e.Validato ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        esito.Format.Shading.Color = new Color(0xFFF3F3F3);
        esito.Format.SpaceAfter = "0.2cm";

        var t = TabellaInfo(sec);
        RigaInfo(t, "Data esecuzione", e.DataEsecuzione.ToLocalTime().ToString("dd/MM/yyyy HH:mm", It),
                    "Confronti", $"{e.NumeroOk} OK / {e.NumeroKo} KO");
        RigaInfo(t, "Versione set di riferimento", e.VersioneSet,
                    "Tolleranza", e.Tolleranza.ToString("0.#######", It) + " °C");
        RigaInfo(t, "Versione applicazione", e.VersioneApplicazione ?? "—",
                    "Eseguita da", string.IsNullOrWhiteSpace(operatore) ? "—" : operatore!);

        // Riepilogo per caso
        Titolo(sec, "Riepilogo per caso");
        var ric = TabellaValori(sec, new[] { 3.2, 8.6, 2.4, 2.4 }, "Caso", "Descrizione", "OK/KO", "Esito");
        foreach (var c in e.Casi)
        {
            var r = ric.AddRow();
            r.Cells[0].AddParagraph(c.Nome);
            r.Cells[1].AddParagraph(c.Descrizione);
            r.Cells[2].AddParagraph($"{c.Ok}/{c.Ko}").Format.Alignment = ParagraphAlignment.Center;
            var pe = r.Cells[3].AddParagraph(c.Validato ? "VALIDATO" : "NON VALIDATO");
            pe.Format.Alignment = ParagraphAlignment.Center; pe.Format.Font.Bold = true;
            pe.Format.Font.Color = c.Validato ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        }

        // Dettaglio confronti
        Titolo(sec, "Dettaglio dei confronti");
        var td = TabellaValori(sec, new[] { 1.8, 1.4, 5.8, 3.1, 3.1, 1.2 },
            "Caso", "Punto", "Parametro", "Calcolato", "Atteso", "Esito");
        foreach (var d in e.Dettagli)
        {
            var r = td.AddRow();
            if (!d.Ok) r.Shading.Color = new Color(0xFFFDECEA);
            r.Cells[0].AddParagraph(d.Caso);
            r.Cells[1].AddParagraph(d.Punto).Format.Alignment = ParagraphAlignment.Center;
            r.Cells[2].AddParagraph(d.Parametro);
            r.Cells[3].AddParagraph(d.Calcolato.ToString("0.########", It)).Format.Alignment = ParagraphAlignment.Right;
            r.Cells[4].AddParagraph(d.Atteso.ToString("0.########", It)).Format.Alignment = ParagraphAlignment.Right;
            var pe = r.Cells[5].AddParagraph(d.Ok ? "OK" : "KO");
            pe.Format.Alignment = ParagraphAlignment.Center; pe.Format.Font.Bold = true;
            pe.Format.Font.Color = d.Ok ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        }

        var nota = sec.AddParagraph(
            "Metodo: la validazione confronta i risultati calcolati oggi dall'applicazione con valori attesi "
            + "indipendenti (calcolo separato) sui tre casi di riferimento — errore nullo, errore positivo con "
            + "ripetibilità, errore negativo con deriva del riferimento. I dati d'ingresso e i valori attesi "
            + "sono congelati dal foglio Excel storico «Modulo Statistico 04-01» (fogli VAL_Caso1/2/3 e "
            + "Validazione_Foglio), archiviato in forma protetta come evidenza primaria. Un parametro è conforme "
            + "se lo scarto tra calcolato e atteso non supera la tolleranza indicata.");
        nota.Format.Font.Size = 8; nota.Format.Font.Italic = true; nota.Format.SpaceBefore = "0.3cm";

        return doc;
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Piccolo(Section sec, string testo) => sec.AddParagraph(testo).Format.Font.Size = 8;

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true; p.Format.SpaceBefore = "0.25cm"; p.Format.SpaceAfter = "0.05cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }

    private static Table TabellaInfo(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0; t.Format.SpaceAfter = "0.05cm"; t.Format.Font.Size = 8.5;
        t.AddColumn(Unit.FromCentimeter(4.4)); t.AddColumn(Unit.FromCentimeter(4.6));
        t.AddColumn(Unit.FromCentimeter(4.0)); t.AddColumn(Unit.FromCentimeter(4.4));
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

    private static Table TabellaValori(Section sec, double[] larghezze, params string[] intest)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5; t.Borders.Color = new Color(0xFFBBBBBB);
        t.Format.Font.Size = 8.5; t.Format.SpaceAfter = "0.05cm";
        foreach (var w in larghezze) t.AddColumn(Unit.FromCentimeter(w));
        var h = t.AddRow(); h.Format.Font.Bold = true; h.Shading.Color = new Color(0xFFF3F3F3);
        for (int i = 0; i < intest.Length; i++)
            h.Cells[i].AddParagraph(intest[i]);
        return t;
    }
}
