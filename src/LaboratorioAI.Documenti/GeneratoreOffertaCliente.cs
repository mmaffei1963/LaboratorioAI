using System.Globalization;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Offerte;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// PDF dell'offerta di laboratorio del portale clienti (17/11/2026), con la stessa carta intestata e lo stesso stile
/// delle offerte (GeneratoreOffertaPreventivo): oggetto e presentazione, ALL.1, dettaglio incarico con le prove in
/// subappalto (§) come nell'allegato tecnico degli Ordini di accettazione, condizioni.
/// </summary>
public static class GeneratoreOffertaCliente
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private static readonly Unit LarghezzaLoghi = Unit.FromCentimeter(17);

    private static string Simbolo(int totale, int numero) => totale > 1 ? "§" + numero : "§";

    /// <summary>Nota sulle prove in subappalto, la stessa delle offerte (per il portale a video).</summary>
    public static string NotaSubappalto(int numeroLaboratori) => TestiOfferta.NotaSubappalto(numeroLaboratori);

    /// <summary>Frase del laboratorio esterno sotto le tabelle, la stessa delle offerte.</summary>
    public static string FraseSubappalto(string? fraseArcLab, string? nomeLaboratorio) => TestiOfferta.FraseSubappalto(fraseArcLab, nomeLaboratorio);

    public static string NomeFile(OffertaCliente o)
    {
        var cli = new string((o.Cliente.RagioneSociale ?? "Cliente").Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Offerta_laboratorio_{cli}_{o.GenerataIl:yyyyMMdd}.pdf";
    }

    public static byte[] GeneraPdf(OffertaCliente o, string cartellaImmagini)
    {
        FontResolverLabAI.Registra();
        var renderer = new PdfDocumentRenderer { Document = Costruisci(o, cartellaImmagini) };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static Document Costruisci(OffertaCliente o, string cartellaImmagini)
    {
        var doc = new Document();
        doc.Info.Title = "Offerta analisi di laboratorio";
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Montserrat";
        normale.Font.Size = 9.5;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(3.4);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(2.6);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        sec.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
        sec.PageSetup.FooterDistance = Unit.FromCentimeter(0.6);

        var logoHeader = Path.Combine(cartellaImmagini, "logo_header.png");
        if (File.Exists(logoHeader)) { var img = sec.Headers.Primary.AddImage(logoHeader); img.Width = LarghezzaLoghi; img.LockAspectRatio = true; }
        var logoFooter = Path.Combine(cartellaImmagini, "logo_footer.png");
        if (File.Exists(logoFooter)) { var img = sec.Footers.Primary.AddImage(logoFooter); img.Width = LarghezzaLoghi; img.LockAspectRatio = true; }
        var pagine = sec.Footers.Primary.AddParagraph();
        pagine.Format.Alignment = ParagraphAlignment.Center;
        pagine.Format.Font.Size = 7;
        pagine.AddText($"Offerta analisi di laboratorio – situazione al {o.GenerataIl:dd/MM/yyyy} – Pagina ");
        pagine.AddPageField();
        pagine.AddText(" di ");
        pagine.AddNumPagesField();

        // Destinatario
        var c = o.Cliente;
        var dest = sec.AddParagraph();
        dest.Format.Alignment = ParagraphAlignment.Right;
        dest.Format.SpaceAfter = "0.5cm";
        dest.AddFormattedText("Spett.le " + (c.RagioneSociale ?? ""), TextFormat.Bold);
        var indirizzo = string.Join(" ", new[] { c.Cap, c.Citta, string.IsNullOrWhiteSpace(c.Provincia) ? null : $"({c.Provincia})" }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(c.Indirizzo)) { dest.AddLineBreak(); dest.AddText(c.Indirizzo!); }
        if (indirizzo.Length > 0) { dest.AddLineBreak(); dest.AddText(indirizzo); }
        if (!string.IsNullOrWhiteSpace(c.PartitaIva)) { dest.AddLineBreak(); dest.AddText("P.IVA " + c.PartitaIva); }

        var data = sec.AddParagraph($"Cattolica, {o.GenerataIl:dd/MM/yyyy}");
        data.Format.SpaceAfter = "0.4cm";

        var ogg = sec.AddParagraph(TestiOffertaCliente.Oggetto);
        ogg.Format.Font.Bold = true;
        ogg.Format.SpaceAfter = "0.3cm";
        foreach (var p in TestiOffertaCliente.Presentazione)
        {
            var par = sec.AddParagraph(p);
            par.Format.SpaceAfter = "0.15cm";
        }

        // ALL.1
        Titolo(sec, TestiOffertaCliente.TitoloAllegato);
        if (o.Righe.Count == 0)
            sec.AddParagraph("Nessun incarico di analisi attivo.");
        else
        {
            var t = Tabella(sec, (1.7, "Cod. Controllo"), (8.0, "Incarico"), (2.2, "Costo"), (2.5, "Data incarico"), (2.6, "Ult. modifica"));
            foreach (var r in o.Righe)
            {
                var row = t.AddRow();
                row.Cells[0].AddParagraph(r.IdControllo.ToString(It));
                row.Cells[1].AddParagraph(r.Incarico);
                var costo = row.Cells[2].AddParagraph(r.Prezzo.ToString("N2", It) + " €");
                costo.Format.Alignment = ParagraphAlignment.Right;
                row.Cells[3].AddParagraph(r.DataIncarico?.ToString("dd/MM/yyyy", It) ?? "—");
                var mod = row.Cells[4].AddParagraph(r.UltimaModifica?.ToString("dd/MM/yyyy", It) ?? "—");
                if (!string.IsNullOrWhiteSpace(r.EtichettaModifica))
                {
                    mod.AddLineBreak();
                    mod.AddFormattedText(r.EtichettaModifica!, new Font { Size = 7, Italic = true });
                }
            }
            var nota = sec.AddParagraph("Prezzi IVA esclusa.");
            nota.Format.Font.Size = 7.5;
            nota.Format.SpaceBefore = "0.1cm";
        }

        // Dettaglio incarico
        Titolo(sec, TestiOffertaCliente.TitoloDettaglio);
        var numeroLab = new Dictionary<int, int>();
        var fraseLab = new List<string>();
        foreach (var m in o.Dettagli.SelectMany(d => d.Metodi))
        {
            if (m.IdLabEsterno is not int id || numeroLab.ContainsKey(id)) continue;
            numeroLab[id] = numeroLab.Count + 1;
            fraseLab.Add(TestiOfferta.FraseSubappalto(m.LabEsternoFrase, m.LabEsterno));
        }
        foreach (var d in o.Dettagli)
        {
            var h = sec.AddParagraph(d.Titolo);
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.3cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.KeepWithNext = true;
            if (d.Metodi.Count == 0) { sec.AddParagraph("Nessuna prova associata."); continue; }
            var t = Tabella(sec, (6.5, "Denominazione"), (9.0, "Metodo"), (1.5, "Accr."));
            foreach (var m in d.Metodi)
            {
                var row = t.AddRow();
                row.Cells[0].AddParagraph(m.DettagliMetodo ?? m.Descrizione ?? m.DenominazioneRiga ?? "—");
                var met = row.Cells[1].AddParagraph(m.RifBiblio ?? "—");
                met.Format.Font.Size = 8;
                row.Cells[2].AddParagraph(m.IdLabEsterno is int id ? Simbolo(numeroLab.Count, numeroLab[id]) : (m.Accreditato ? "Sì" : "—"));
            }
        }
        for (var i = 0; i < fraseLab.Count; i++)
        {
            var p = sec.AddParagraph($"{Simbolo(numeroLab.Count, i + 1)} {fraseLab[i]}");
            p.Format.Font.Size = 7.5;
            p.Format.SpaceBefore = "0.1cm";
        }
        if (numeroLab.Count > 0)
        {
            var avviso = sec.AddParagraph(TestiOfferta.NotaSubappalto(numeroLab.Count));
            avviso.Format.Font.Bold = true;
            avviso.Format.Shading.Color = new Color(0xFFFFF3D6);
            avviso.Format.SpaceBefore = "0.3cm";
            avviso.Format.SpaceAfter = "0.3cm";
        }

        // Condizioni
        foreach (var (titolo, paragrafi) in TestiOffertaCliente.Condizioni)
        {
            var h = sec.AddParagraph(titolo);
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.35cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.KeepWithNext = true;
            foreach (var p in paragrafi)
            {
                var par = sec.AddParagraph();
                par.Format.SpaceAfter = "0.1cm";
                var url = TestiOffertaCliente.IndirizzoCondizioni;
                var i = p.IndexOf(url, StringComparison.Ordinal);
                if (i < 0) { par.AddText(p); continue; }
                // Indirizzo delle Condizioni come collegamento cliccabile.
                par.AddText(p[..i]);
                var link = par.AddHyperlink(url, HyperlinkType.Web);
                link.AddFormattedText(url, new Font { Color = new Color(0xFF1F4E9E), Underline = Underline.Single });
                par.AddText(p[(i + url.Length)..]);
            }
        }

        return doc;
    }

    private static void Titolo(Section sec, string testo)
    {
        var h = sec.AddParagraph(testo);
        h.Format.Font.Bold = true;
        h.Format.Font.Size = 11;
        h.Format.SpaceBefore = "0.6cm";
        h.Format.SpaceAfter = "0.2cm";
        h.Format.Shading.Color = new Color(0xFFEEEEEE);
        h.Format.KeepWithNext = true;
    }

    private static Table Tabella(Section sec, params (double Cm, string Intestazione)[] colonne)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = new Color(0xFFBBBBBB);
        t.Format.Font.Size = 8.5;
        foreach (var (cm, _) in colonne) t.AddColumn(Unit.FromCentimeter(cm));
        var hr = t.AddRow();
        hr.HeadingFormat = true;
        hr.Format.Font.Bold = true;
        hr.Shading.Color = new Color(0xFFF3F3F3);
        for (var i = 0; i < colonne.Length; i++) hr.Cells[i].AddParagraph(colonne[i].Intestazione);
        return t;
    }
}
