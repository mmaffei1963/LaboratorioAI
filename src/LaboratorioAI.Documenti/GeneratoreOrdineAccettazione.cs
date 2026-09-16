using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Ordine di Accettazione generato con MigraDoc (PDF) o, su carta intestata, in Word (OpenXml).
/// Riproduce la struttura del documento Sicural: riepilogo dei campioni con prezzi e totali IVA,
/// riquadro delle condizioni (Allegato A Rev. 19) e allegato tecnico per campione.
/// I dati provengono da ArcLab (LabAnalisiDati) in sola lettura.
/// </summary>
public interface IGeneratoreOrdineAccettazione
{
    /// <summary>Ordine di Accettazione in Word (.docx) scritto sul modello di carta intestata indicato.</summary>
    byte[] GeneraWord(OrdineAccettazione o, string percorsoModello);
    /// <summary>Ordine di Accettazione in PDF. <paramref name="percorsoWwwRoot"/> serve a caricare la carta intestata (cartella img).</summary>
    byte[] GeneraPdf(OrdineAccettazione o, string? percorsoWwwRoot = null);
    string NomeFile(OrdineAccettazione o, string estensione);
}

/// <inheritdoc />
public class GeneratoreOrdineAccettazione : IGeneratoreOrdineAccettazione
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraWord(OrdineAccettazione o, string percorsoModello) =>
        OrdineAccettazioneDocx.Genera(o, percorsoModello);

    public byte[] GeneraPdf(OrdineAccettazione o, string? percorsoWwwRoot = null)
    {
        var doc = Costruisci(o, percorsoWwwRoot);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public string NomeFile(OrdineAccettazione o, string estensione)
    {
        // AAAAMMGG_OA_CodLotto_IdLotto_idInsediamento_ClienteSlug.ext (data = ricezione lotto)
        var data = (o.DataRicezione ?? DateTime.Today).ToString("yyyyMMdd", It);
        var cod = Slug(o.CodLotto, 20);
        var ins = o.IdInsediamento?.ToString() ?? "0";
        var cli = Slug(o.ClienteRagioneSociale, 30);
        return $"{data}_OA_{cod}_{o.IdLotto}_{ins}_{cli}.{estensione}";
    }

    /// <summary>Slug per nome file: solo lettere/cifre, il resto diventa "_", niente ripetizioni, troncato.</summary>
    internal static string Slug(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return "NA";
        var sb = new System.Text.StringBuilder();
        bool ultimoUnderscore = false;
        foreach (var ch in s.Trim())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); ultimoUnderscore = false; }
            else if (!ultimoUnderscore) { sb.Append('_'); ultimoUnderscore = true; }
        }
        var r = sb.ToString().Trim('_');
        if (r.Length > max) r = r.Substring(0, max).Trim('_');
        return r.Length == 0 ? "NA" : r;
    }

    // ── Costruzione documento ──────────────────────────────────────
    private static Document Costruisci(OrdineAccettazione o, string? percorsoWwwRoot)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        // PDF (PDFSharp): usa un font di sistema sempre risolvibile. Montserrat richiederebbe il file
        // TTF + un IFontResolver registrato; il Word invece usa Montserrat direttamente (vedi OrdineAccettazioneDocx).
        normale.Font.Name = "Arial";
        normale.Font.Size = 10;

        // Carta intestata (immagini header/piè) se disponibili in wwwroot/img.
        // Header: versione ritagliata pulita (lo screenshot originale conteneva la finestra del browser);
        // fallback all'immagine originale se il ritaglio non c'è.
        var headerImg = Immagine(percorsoWwwRoot, "intestazione_marea_pulita.png")
                        ?? Immagine(percorsoWwwRoot, "intestazione carta intestata MAREA.png");
        var footerImg = Immagine(percorsoWwwRoot, "Piede carta intestata MAREA.png");
        bool cartaIntestata = headerImg is not null || footerImg is not null;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        // Con la carta intestata si lascia più spazio sopra/sotto per header e piè.
        sec.PageSetup.TopMargin = Unit.FromCentimeter(cartaIntestata ? 3.6 : 1.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(cartaIntestata ? 2.4 : 1.2);
        sec.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
        sec.PageSetup.FooterDistance = Unit.FromCentimeter(0.4);

        if (headerImg is not null)
        {
            var hp = sec.Headers.Primary.AddParagraph();
            hp.Format.Alignment = ParagraphAlignment.Center;
            var img = hp.AddImage(headerImg);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(16.5); // aspect ~6.3 → alt. ~2.6 cm, entro il margine superiore
        }
        if (footerImg is not null)
        {
            var fp = sec.Footers.Primary.AddParagraph();
            fp.Format.Alignment = ParagraphAlignment.Center;
            var img = fp.AddImage(footerImg);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(17);
        }

        if (!cartaIntestata)
        {
            // Nessuna carta intestata: intestazione testuale minima.
            var intest = sec.AddParagraph("MARE.A s.r.l");
            intest.Format.Font.Bold = true;
            intest.Format.Font.Size = 12;
            Piccolo(sec, "Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • info@mare-a.com • www.mare-a.com");
            Piccolo(sec, "Laboratorio accreditato ACCREDIA n. 00729 L — Iscritto nell'elenco regionale Emilia Romagna al n. 008/RN/003");
        }

        // Titolo
        var tit = sec.AddParagraph("ORDINE DI ACCETTAZIONE");
        tit.Format.Alignment = ParagraphAlignment.Center;
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 14;
        tit.Format.SpaceBefore = "0.5cm";
        tit.Format.SpaceAfter = "0.2cm";

        var rif = sec.AddParagraph($"Riferimento lotto n. {Vuoto(o.CodLotto)}    •    Stampato in data {DateTime.Today.ToString("dd/MM/yyyy", It)}");
        rif.Format.Alignment = ParagraphAlignment.Center;
        rif.Format.Font.Size = 9;
        rif.Format.SpaceAfter = string.IsNullOrWhiteSpace(o.RiferimentoModulo) ? "0.3cm" : "0.05cm";
        if (!string.IsNullOrWhiteSpace(o.RiferimentoModulo))
        {
            var mod = sec.AddParagraph(o.RiferimentoModulo!.Trim());
            mod.Format.Alignment = ParagraphAlignment.Center;
            mod.Format.Font.Size = 8;
            mod.Format.Font.Italic = true;
            mod.Format.SpaceAfter = "0.3cm";
        }

        // Destinatario
        var dest = sec.AddParagraph();
        dest.Format.SpaceAfter = "0.2cm";
        dest.AddFormattedText("Spett.le  ", TextFormat.Bold);
        dest.AddFormattedText(Vuoto(o.ClienteRagioneSociale), TextFormat.Bold);
        var indirizzo = string.Join(" ", new[] { o.ClienteIndirizzo, o.ClienteCap, o.ClienteCitta,
                string.IsNullOrWhiteSpace(o.ClienteProvincia) ? null : $"({o.ClienteProvincia})" }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!string.IsNullOrWhiteSpace(indirizzo)) { dest.AddLineBreak(); dest.AddText(indirizzo); }
        if (!string.IsNullOrWhiteSpace(o.ClientePartitaIva)) { dest.AddLineBreak(); dest.AddText("P.IVA " + o.ClientePartitaIva); }
        if (!string.IsNullOrWhiteSpace(o.Insediamento)) { dest.AddLineBreak(); dest.AddText("Insediamento: " + o.Insediamento); }

        var ogg = sec.AddParagraph("OGGETTO: ACCETTAZIONE CAMPIONI");
        ogg.Format.Font.Bold = true;
        ogg.Format.SpaceAfter = "0.2cm";

        // ── Riepilogo campioni ─────────────────────────────────────
        Titolo(sec, "RIEPILOGO CAMPIONI");
        var t = sec.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = new Color(0xFFBBBBBB);
        t.AddColumn(Unit.FromCentimeter(2.6));   // riferimento
        t.AddColumn(Unit.FromCentimeter(11.4));  // descrizione
        t.AddColumn(Unit.FromCentimeter(3.0));   // prezzo
        var hr = t.AddRow();
        hr.Format.Font.Bold = true;
        hr.Shading.Color = new Color(0xFFF3F3F3);
        hr.Cells[0].AddParagraph("Riferimento");
        hr.Cells[1].AddParagraph("Campione");
        hr.Cells[2].AddParagraph("Prezzo").Format.Alignment = ParagraphAlignment.Right;

        foreach (var c in o.Campioni)
        {
            var row = t.AddRow();
            row.Cells[0].AddParagraph(c.Riferimento);
            var descr = c.Denominazione ?? "—";
            if (!string.IsNullOrWhiteSpace(c.Matrice)) descr += $" — {c.Matrice}";
            row.Cells[1].AddParagraph(descr);
            row.Cells[2].AddParagraph(c.Prezzo is decimal pz ? $"€ {Fmt(pz)}" : "—")
                .Format.Alignment = ParagraphAlignment.Right;
        }

        // Totali
        var te = sec.AddTable();
        te.Borders.Width = 0;
        te.AddColumn(Unit.FromCentimeter(14));
        te.AddColumn(Unit.FromCentimeter(3));
        RigaImporto(te, "Imponibile", o.TotaleImponibile, false);
        RigaImporto(te, $"IVA {Fmt(o.AliquotaIva)}%", o.TotaleIva, false);
        RigaImporto(te, "TOTALE (IVA compresa)", o.TotaleConIva, true);

        // ── Riquadro condizioni (Allegato A Rev. 19) ───────────────
        RiquadroCondizioni(sec, o.RevisioneCondizioni);

        // ── Allegato tecnico ───────────────────────────────────────
        var att = sec.AddParagraph("ALLEGATO TECNICO");
        att.Format.Font.Bold = true;
        att.Format.Font.Size = 12;
        att.Format.SpaceBefore = "0.5cm";
        att.Format.SpaceAfter = "0.2cm";

        var frasiLab = new List<string>();
        foreach (var c in o.Campioni)
        {
            DoppiaRiga(sec);
            var h = sec.AddParagraph(c.Riferimento + " — " + (c.Denominazione ?? "").ToUpperInvariant());
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.35cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.Shading.Color = new Color(0xFFEEEEEE);

            // Dati identificativi
            var d = sec.AddTable();
            d.Borders.Width = 0;
            d.AddColumn(Unit.FromCentimeter(4.0));
            d.AddColumn(Unit.FromCentimeter(13.0));
            Dato(d, "Riferimento", c.Riferimento);
            Dato(d, "Descrizione", c.Denominazione);
            Dato(d, "Matrice", c.Matrice);
            Dato(d, "Imballaggio", c.Imballaggio);
            Dato(d, "Punto di prelievo", c.PuntoPrelievo);
            Dato(d, "Data accettazione", o.DataRicezione?.ToString("dd/MM/yyyy", It));
            Dato(d, "Data prelievo", o.DataPrelievo?.ToString("dd/MM/yyyy", It));
            Dato(d, "T° di ricevimento", c.TemperaturaRicevimento);
            Dato(d, "Priorità", c.PrioritaTesto);
            var ctrl = c.Controllo is null ? null
                : c.Controllo + (c.IdControllo is int idc ? $" (ID controllo {idc})" : "");
            Dato(d, "Controllo", ctrl);

            // Analisi
            var a = sec.AddParagraph("Analisi");
            a.Format.Font.Bold = true;
            a.Format.SpaceBefore = "0.15cm";
            a.Format.SpaceAfter = "0.05cm";

            if (c.Analisi.Count > 0)
            {
                var ta = sec.AddTable();
                ta.Borders.Width = 0.5;
                ta.Borders.Color = new Color(0xFFCCCCCC);
                ta.AddColumn(Unit.FromCentimeter(8.5));
                ta.AddColumn(Unit.FromCentimeter(8.5));
                var har = ta.AddRow();
                har.Format.Font.Bold = true;
                har.Shading.Color = new Color(0xFFF7F7F7);
                har.Cells[0].AddParagraph("Denominazione");
                har.Cells[1].AddParagraph("Metodo");
                foreach (var an in c.Analisi)
                {
                    var r = ta.AddRow();
                    var par = an.EtichettaParametro;
                    if (!string.IsNullOrWhiteSpace(an.LaboratorioEsterno)) par += "  *";
                    r.Cells[0].AddParagraph(par);
                    r.Cells[1].AddParagraph(an.Metodo ?? "—");
                    if (!string.IsNullOrWhiteSpace(an.FraseLabEsterno))
                        frasiLab.Add(an.FraseLabEsterno!.Trim());
                }
            }
            else
            {
                Piccolo(sec, "Nessuna prova associata.");
            }
        }

        // Frasi dei laboratori esterni (subappalto), una sola volta
        foreach (var frase in frasiLab.Distinct())
            Piccolo(sec, "* " + frase);

        // ── Pagina finale: dati aziendali ──────────────────────────
        PaginaDatiAziendali(sec, o);

        return doc;
    }

    // ── Pagina finale con i dati aziendali ─────────────────────────
    private static void PaginaDatiAziendali(Section sec, OrdineAccettazione o)
    {
        var tit = sec.AddParagraph("DATI AZIENDALI");
        tit.Format.PageBreakBefore = true;
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 13;
        tit.Format.SpaceAfter = "0.3cm";

        var indFatt = ComponiIndirizzo(o.ClienteIndirizzo, o.ClienteCap, o.ClienteCitta, o.ClienteProvincia);
        var indInsed = ComponiIndirizzo(o.InsediamentoIndirizzo, o.InsediamentoCap, o.InsediamentoCitta, o.InsediamentoProvincia);

        Voce(sec, "Ragione sociale", o.ClienteRagioneSociale);
        Voce(sec, "P.IVA", o.ClientePartitaIva);
        Voce(sec, "Indirizzo di fatturazione", indFatt);
        Voce(sec, "Insediamento", o.Insediamento);
        Voce(sec, "Indirizzo insediamento", indInsed);
        Voce(sec, "Email", o.InsediamentoEmail);
    }

    private static void Voce(Section sec, string label, string? valore)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceAfter = "0.15cm";
        p.AddFormattedText(label + ": ", TextFormat.Bold);
        p.AddText(string.IsNullOrWhiteSpace(valore) ? "—" : valore!.Trim());
    }

    private static string ComponiIndirizzo(string? indirizzo, string? cap, string? citta, string? prov)
    {
        var parti = new[]
        {
            indirizzo,
            string.Join(" ", new[] { cap, citta }.Where(s => !string.IsNullOrWhiteSpace(s))),
            string.IsNullOrWhiteSpace(prov) ? null : $"({prov!.Trim()})"
        }.Where(s => !string.IsNullOrWhiteSpace(s));
        return string.Join(" ", parti).Trim();
    }

    // Doppia riga nera (due filetti paralleli) tramite bordi sopra e sotto di un paragrafo vuoto.
    private static void DoppiaRiga(Section sec)
    {
        var p = sec.AddParagraph();
        p.Format.SpaceBefore = "0.35cm";
        p.Format.SpaceAfter = "0.1cm";
        p.Format.Borders.Distance = 1.5;
        p.Format.Borders.Top.Width = 1.0;
        p.Format.Borders.Top.Color = Colors.Black;
        p.Format.Borders.Bottom.Width = 1.0;
        p.Format.Borders.Bottom.Color = Colors.Black;
    }

    private static string? Immagine(string? percorsoWwwRoot, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoWwwRoot)) return null;
        var full = Path.Combine(percorsoWwwRoot, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }

    // ── Riquadro condizioni ────────────────────────────────────────
    private static void RiquadroCondizioni(Section sec, string revisione)
    {
        var box = sec.AddTable();
        box.Borders.Width = 0.75;
        box.Borders.Color = new Color(0xFF999999);
        box.Format.SpaceBefore = "0.4cm";
        box.AddColumn(Unit.FromCentimeter(17));
        var cella = box.AddRow().Cells[0];
        cella.Shading.Color = new Color(0xFFF7F7F7);

        BloccoBox(cella, TestiCondizioniAccettazione.TitoloRiquadro, null, titoloGrande: true);
        BloccoBox(cella, TestiCondizioniAccettazione.TitoloVerifica, TestiCondizioniAccettazione.Verifica);
        BloccoBox(cella, TestiCondizioniAccettazione.TitoloPrelievo, TestiCondizioniAccettazione.Prelievo);
        BloccoBox(cella, TestiCondizioniAccettazione.TitoloCondizioni, TestiCondizioniAccettazione.Condizioni(revisione));
    }

    private static void BloccoBox(Cell cella, string titolo, string? corpo, bool titoloGrande = false)
    {
        var t = cella.AddParagraph(titolo);
        t.Format.Font.Bold = true;
        t.Format.Font.Size = titoloGrande ? 10 : 8.5;
        t.Format.SpaceBefore = titoloGrande ? "0.1cm" : "0.2cm";
        t.Format.SpaceAfter = "0.05cm";
        if (corpo is not null)
        {
            var p = cella.AddParagraph(corpo);
            p.Format.Font.Size = 8;
            p.Format.Alignment = ParagraphAlignment.Justify;
        }
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Piccolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
    }

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = "0.4cm";
        p.Format.SpaceAfter = "0.1cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }

    private static void Dato(Table t, string label, string? valore)
    {
        var row = t.AddRow();
        var pl = row.Cells[0].AddParagraph(label);
        pl.Format.Font.Bold = true;
        pl.Format.Font.Size = 9;
        var pv = row.Cells[1].AddParagraph(string.IsNullOrWhiteSpace(valore) ? "—" : valore!.Trim());
        pv.Format.Font.Size = 9;
    }

    private static void RigaImporto(Table t, string label, decimal importo, bool grassetto)
    {
        var row = t.AddRow();
        var pl = row.Cells[0].AddParagraph(label);
        pl.Format.Alignment = ParagraphAlignment.Right;
        pl.Format.Font.Bold = grassetto;
        var pv = row.Cells[1].AddParagraph($"€ {Fmt(importo)}");
        pv.Format.Alignment = ParagraphAlignment.Right;
        pv.Format.Font.Bold = grassetto;
    }

    private static string Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();
    private static string Fmt(decimal v) => v.ToString("N2", It);
}
