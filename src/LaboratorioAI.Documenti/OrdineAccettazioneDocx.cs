using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Genera l'Ordine di Accettazione dentro il modello Word di carta intestata (header/footer reali).
/// Solo OpenXml (nessun MigraDoc). Lavora su una copia in memoria: il modello non viene mai modificato.
/// Stessi contenuti della versione PDF: riepilogo campioni + totali, riquadro condizioni (Allegato A
/// Rev. 19), allegato tecnico per campione.
/// </summary>
internal static class OrdineAccettazioneDocx
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    private const string FontNome = "Montserrat";
    private const uint MargineLatoTwips = 1134; // 2 cm

    public static byte[] Genera(OrdineAccettazione o, string percorsoModello)
    {
        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(percorsoModello)) fs.CopyTo(ms);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var sectPr = body.Elements<SectionProperties>().LastOrDefault();

            var pm = sectPr?.Elements<PageMargin>().FirstOrDefault();
            if (pm is not null) { pm.Left = MargineLatoTwips; pm.Right = MargineLatoTwips; }

            void Aggiungi(OpenXmlElement e)
            {
                if (sectPr is not null) body.InsertBefore(e, sectPr);
                else body.AppendChild(e);
            }

            // Titolo
            Aggiungi(Para("ORDINE DI ACCETTAZIONE", bold: true, size: 28, align: JustificationValues.Center, spazioPrima: 120, spazioDopo: 60));
            Aggiungi(Para($"Riferimento lotto n. {Vuoto(o.CodLotto)}        Stampato in data {DateTime.Today.ToString("dd/MM/yyyy", It)}",
                size: 18, align: JustificationValues.Center, spazioDopo: string.IsNullOrWhiteSpace(o.RiferimentoModulo) ? 160 : 20));
            if (!string.IsNullOrWhiteSpace(o.RiferimentoModulo))
                Aggiungi(Para(o.RiferimentoModulo!.Trim(), size: 15, italic: true, align: JustificationValues.Center, spazioDopo: 160));

            // Destinatario
            var righeDest = new List<string> { "Spett.le  " + Vuoto(o.ClienteRagioneSociale) };
            var indirizzo = string.Join(" ", new[] { o.ClienteIndirizzo, o.ClienteCap, o.ClienteCitta,
                    string.IsNullOrWhiteSpace(o.ClienteProvincia) ? null : $"({o.ClienteProvincia})" }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            if (!string.IsNullOrWhiteSpace(indirizzo)) righeDest.Add(indirizzo);
            if (!string.IsNullOrWhiteSpace(o.ClientePartitaIva)) righeDest.Add("P.IVA " + o.ClientePartitaIva);
            if (!string.IsNullOrWhiteSpace(o.Insediamento)) righeDest.Add("Insediamento: " + o.Insediamento);
            foreach (var (r, idx) in righeDest.Select((r, i) => (r, i)))
                Aggiungi(Para(r, bold: idx == 0, size: 20, spazioDopo: idx == righeDest.Count - 1 ? 120 : 0));

            Aggiungi(Para("OGGETTO: ACCETTAZIONE CAMPIONI", bold: true, spazioDopo: 120));

            // ── Riepilogo campioni ─────────────────────────────────
            Aggiungi(Titolo("RIEPILOGO CAMPIONI"));
            var t = Tabella(1500, 6400, 1700);
            t.Append(RigaTab(true, "F3F3F3",
                ("Riferimento", 1500, false), ("Campione", 6400, false), ("Prezzo", 1700, true)));
            foreach (var c in o.Campioni)
            {
                var descr = c.Denominazione ?? "—";
                if (!string.IsNullOrWhiteSpace(c.Matrice)) descr += $" — {c.Matrice}";
                t.Append(RigaTab(false, null,
                    (c.Riferimento, 1500, false),
                    (descr, 6400, false),
                    (c.Prezzo is decimal pz ? $"€ {Fmt(pz)}" : "—", 1700, true)));
            }
            Aggiungi(t);

            // Totali
            var te = Tabella(false, 7200, 2000);
            te.Append(RigaImporto("Imponibile", o.TotaleImponibile, false));
            te.Append(RigaImporto($"IVA {Fmt(o.AliquotaIva)}%", o.TotaleIva, false));
            te.Append(RigaImporto("TOTALE (IVA compresa)", o.TotaleConIva, true));
            Aggiungi(te);

            // ── Riquadro condizioni (Allegato A Rev. 19) ───────────
            Aggiungi(RiquadroCondizioni(o.RevisioneCondizioni));

            // ── Allegato tecnico ───────────────────────────────────
            Aggiungi(Para("ALLEGATO TECNICO", bold: true, size: 24, spazioPrima: 240, spazioDopo: 120));

            var frasiLab = new List<string>();
            foreach (var c in o.Campioni)
            {
                Aggiungi(DoppiaRiga());
                Aggiungi(Titolo(c.Riferimento + " — " + (c.Denominazione ?? "").ToUpperInvariant()));

                var d = Tabella(false, 3600, 13400);
                DatoRiga(d, "Riferimento", c.Riferimento);
                DatoRiga(d, "Descrizione", c.Denominazione);
                DatoRiga(d, "Matrice", c.Matrice);
                DatoRiga(d, "Imballaggio", c.Imballaggio);
                DatoRiga(d, "Punto di prelievo", c.PuntoPrelievo);
                DatoRiga(d, "Data accettazione", o.DataRicezione?.ToString("dd/MM/yyyy", It));
                DatoRiga(d, "Data prelievo", o.DataPrelievo?.ToString("dd/MM/yyyy", It));
                DatoRiga(d, "T° di ricevimento", c.TemperaturaRicevimento);
                DatoRiga(d, "Priorità", c.PrioritaTesto);
                var ctrl = c.Controllo is null ? null
                    : c.Controllo + (c.IdControllo is int idc ? $" (ID controllo {idc})" : "");
                DatoRiga(d, "Controllo", ctrl);
                Aggiungi(d);

                Aggiungi(Para("Analisi", bold: true, size: 20, spazioPrima: 100, spazioDopo: 40));
                if (c.Analisi.Count > 0)
                {
                    var ta = Tabella(4900, 4900);
                    ta.Append(RigaTab(true, "F7F7F7", ("Denominazione", 4900, false), ("Metodo", 4900, false)));
                    foreach (var an in c.Analisi)
                    {
                        var par = an.EtichettaParametro;
                        if (!string.IsNullOrWhiteSpace(an.LaboratorioEsterno)) par += "  *";
                        ta.Append(RigaTab(false, null, (par, 4900, false), (an.Metodo ?? "—", 4900, false)));
                        if (!string.IsNullOrWhiteSpace(an.FraseLabEsterno))
                            frasiLab.Add(an.FraseLabEsterno!.Trim());
                    }
                    Aggiungi(ta);
                }
                else Aggiungi(Para("Nessuna prova associata.", size: 17));
            }

            foreach (var frase in frasiLab.Distinct())
                Aggiungi(Para("* " + frase, size: 16));

            // ── Pagina finale: dati aziendali ──────────────────────
            Aggiungi(Para("DATI AZIENDALI", bold: true, size: 26, spazioDopo: 120, pageBreakBefore: true));
            var indFatt = ComponiIndirizzo(o.ClienteIndirizzo, o.ClienteCap, o.ClienteCitta, o.ClienteProvincia);
            var indInsed = ComponiIndirizzo(o.InsediamentoIndirizzo, o.InsediamentoCap, o.InsediamentoCitta, o.InsediamentoProvincia);
            Aggiungi(Voce("Ragione sociale", o.ClienteRagioneSociale));
            Aggiungi(Voce("P.IVA", o.ClientePartitaIva));
            Aggiungi(Voce("Indirizzo di fatturazione", indFatt));
            Aggiungi(Voce("Insediamento", o.Insediamento));
            Aggiungi(Voce("Indirizzo insediamento", indInsed));
            Aggiungi(Voce("Email", o.InsediamentoEmail));

            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    // ── Riquadro condizioni: tabella a cella unica con bordo e sfondo ──
    private static Table RiquadroCondizioni(string revisione)
    {
        var t = Tabella(true, 17000);
        var cellPr = new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "17000" },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "F7F7F7" });
        var cell = new TableCell(cellPr);
        cell.Append(Para(TestiCondizioniAccettazione.TitoloRiquadro, bold: true, size: 20, spazioPrima: 60, spazioDopo: 40, cella: true));
        BloccoBox(cell, TestiCondizioniAccettazione.TitoloVerifica, TestiCondizioniAccettazione.Verifica);
        BloccoBox(cell, TestiCondizioniAccettazione.TitoloPrelievo, TestiCondizioniAccettazione.Prelievo);
        BloccoBox(cell, TestiCondizioniAccettazione.TitoloCondizioni, TestiCondizioniAccettazione.Condizioni(revisione));
        t.Append(new TableRow(cell));
        return t;
    }

    private static void BloccoBox(TableCell cell, string titolo, string corpo)
    {
        cell.Append(Para(titolo, bold: true, size: 17, spazioPrima: 120, spazioDopo: 20, cella: true));
        cell.Append(Para(corpo, size: 16, align: JustificationValues.Both, cella: true, spazioDopo: 40));
    }

    // ── Helper testo ───────────────────────────────────────────────
    private static string Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();
    private static string Fmt(decimal v) => v.ToString("N2", It);

    private static Paragraph Para(string testo, bool bold = false, int size = 20, bool italic = false,
        JustificationValues? align = null, string? shadingFill = null, int spazioPrima = 0, int spazioDopo = 0,
        bool cella = false, bool pageBreakBefore = false)
    {
        var pPr = new ParagraphProperties();
        if (pageBreakBefore)
            pPr.Append(new PageBreakBefore());
        if (shadingFill is not null)
            pPr.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = shadingFill });
        if (spazioPrima > 0 || spazioDopo > 0)
            pPr.Append(new SpacingBetweenLines { Before = spazioPrima.ToString(), After = spazioDopo.ToString() });
        if (align is not null)
            pPr.Append(new Justification { Val = align.Value });

        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome });
        if (bold) rPr.Append(new Bold());
        if (italic) rPr.Append(new Italic());
        rPr.Append(new FontSize { Val = size.ToString() });

        var run = new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(rPr);

        var para = new Paragraph();
        para.Append(pPr);
        para.Append(run);
        return para;
    }

    private static Paragraph Titolo(string testo) =>
        Para(testo, bold: true, size: 22, shadingFill: "EEEEEE", spazioPrima: 160, spazioDopo: 60);

    // Doppia riga nera: paragrafo vuoto con bordo inferiore "double" (due filetti paralleli).
    private static Paragraph DoppiaRiga()
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { Before = "220", After = "40" },
            new ParagraphBorders(
                new BottomBorder { Val = BorderValues.Double, Size = 6, Color = "000000", Space = 1 }));
        var run = new Run(new RunProperties(new FontSize { Val = "4" }));
        return new Paragraph(pPr, run);
    }

    private static Paragraph Voce(string label, string? valore)
    {
        var pPr = new ParagraphProperties(new SpacingBetweenLines { After = "60" });
        var para = new Paragraph(pPr);
        para.Append(RunTesto(label + ": ", true));
        para.Append(RunTesto(string.IsNullOrWhiteSpace(valore) ? "—" : valore!.Trim(), false));
        return para;
    }

    private static Run RunTesto(string testo, bool bold)
    {
        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome });
        if (bold) rPr.Append(new Bold());
        rPr.Append(new FontSize { Val = "20" });
        var run = new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(rPr);
        return run;
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

    // ── Helper tabelle ─────────────────────────────────────────────
    private static Table Tabella(params int[] larghezze) => Tabella(true, larghezze);

    private static Table Tabella(bool bordi, params int[] larghezze)
    {
        var props = new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Dxa, Width = larghezze.Sum().ToString() });
        if (bordi)
        {
            EnumValue<BorderValues> s = BorderValues.Single;
            props.Append(new TableBorders(
                new TopBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new LeftBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new BottomBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new RightBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new InsideHorizontalBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new InsideVerticalBorder { Val = s, Size = 4, Color = "BBBBBB" }));
        }
        var t = new Table();
        t.Append(props);
        var grid = new TableGrid();
        foreach (var w in larghezze) grid.Append(new GridColumn { Width = w.ToString() });
        t.Append(grid);
        return t;
    }

    private static TableRow RigaTab(bool grassetto, string? shadingFill, params (string Testo, int Larghezza, bool Destra)[] celle)
    {
        var tr = new TableRow();
        foreach (var (testo, larghezza, destra) in celle)
        {
            var tcPr = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = larghezza.ToString() });
            if (shadingFill is not null)
                tcPr.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = shadingFill });
            var para = Para(testo, bold: grassetto, size: 18, align: destra ? JustificationValues.Right : null, cella: true);
            tr.Append(new TableCell(tcPr, para));
        }
        return tr;
    }

    private static void DatoRiga(Table t, string label, string? valore)
    {
        var tr = new TableRow();
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "3600" }),
            Para(label, bold: true, size: 18, cella: true)));
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "13400" }),
            Para(string.IsNullOrWhiteSpace(valore) ? "—" : valore!.Trim(), size: 18, cella: true)));
        t.Append(tr);
    }

    private static TableRow RigaImporto(string label, decimal importo, bool grassetto)
    {
        var tr = new TableRow();
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "7200" }),
            Para(label, bold: grassetto, align: JustificationValues.Right, cella: true)));
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2000" }),
            Para($"€ {Fmt(importo)}", bold: grassetto, align: JustificationValues.Right, cella: true)));
        return tr;
    }
}
