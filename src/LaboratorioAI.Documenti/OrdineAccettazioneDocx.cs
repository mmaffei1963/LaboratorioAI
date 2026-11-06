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

    internal const string FontNome = "Montserrat";
    internal const uint MargineLatoTwips = 1134; // 2 cm

    /// <summary>
    /// Frase di verifica ACCREDIA per le prove in subappalto, al singolare se il laboratorio esterno
    /// coinvolto nel documento è uno solo (simbolo "§"), al plurale se sono più d'uno (simboli "§1",
    /// "§2", ecc., necessari per distinguerli). Stessa frase usata in Preventivi/Offerte.
    /// </summary>
    internal static string TestoVerificaAccredia(int numeroLaboratori) =>
        TestiOfferta.NotaSubappalto(numeroLaboratori);

    /// <summary>Simbolo del laboratorio esterno: "§" da solo se nel documento ce n'è uno solo, "§N" se ce ne sono più d'uno.</summary>
    internal static string Simbolo(int numeroLaboratoriTotali, int numero) =>
        numeroLaboratoriTotali > 1 ? "§" + numero : "§";

    /// <summary>
    /// Spiegazione dei recapiti insediamenti, riportata prima della tabella: sono gli indirizzi email
    /// a cui arrivano le comunicazioni sui rapporti di prova generati dall'ordine.
    /// </summary>
    private const string TestoRecapitiInsediamenti =
        "Di seguito i recapiti degli insediamenti del Cliente risultanti nei nostri archivi: sono gli " +
        "indirizzi email a cui il Laboratorio può inviare avvisi di cortesia relativi al presente ordine e ai " +
        "rapporti di prova, che sono comunque pubblicati nel Portale clienti. Si invita il Cliente a verificarne " +
        "l'aggiornamento e la correttezza e a segnalarci eventuali variazioni prima dell'emissione dei rapporti di prova.";

    public static byte[] Genera(OrdineAccettazione o, string percorsoModello, IReadOnlyList<InsediamentoContatto> insediamenti)
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
            Aggiungi(Para($"Accettazione n. {Vuoto(o.CodLotto)}        Emesso il {DateTime.Today.ToString("dd/MM/yyyy", It)}",
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

            // Recapiti insediamenti: indirizzo email a cui arrivano le comunicazioni sui rapporti di
            // prova generati dall'ordine. Solo se il cliente è in archivio ArcLab e ha insediamenti.
            if (insediamenti.Count > 0)
            {
                Aggiungi(Titolo("RECAPITI INSEDIAMENTI PER LE COMUNICAZIONI"));
                Aggiungi(Para(TestoRecapitiInsediamenti, size: 17, spazioDopo: 80));
                var ti = Tabella(2552, 4252, 2835);
                ti.Append(RigaTab(true, "F3F3F3", ("Insediamento", 2552, false), ("Indirizzo", 4252, false), ("Email", 2835, false)));
                foreach (var ins in insediamenti)
                {
                    var tr = new TableRow();
                    tr.Append(new TableCell(
                        new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2552" }),
                        Para(ins.Nome, size: 17, cella: true)));
                    tr.Append(new TableCell(
                        new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "4252" }),
                        Para(ins.Indirizzo ?? "—", size: 17, cella: true)));
                    tr.Append(CellaEmail(ins.Email, 2835));
                    ti.Append(tr);
                }
                Aggiungi(ti);
                Aggiungi(Para("", spazioDopo: 120));
            }

            // ── Riquadro condizioni (Allegato A Rev. 19) ───────────
            Aggiungi(RiquadroCondizioni(o.RevisioneCondizioni));

            // ── Allegato tecnico ───────────────────────────────────
            Aggiungi(Para("ALLEGATO TECNICO", bold: true, size: 24, spazioPrima: 240, spazioDopo: 120));

            // Numerazione dei laboratori esterni distinti (§1, §2, ...), come nelle offerte/preventivi:
            // il semplice "*" non basta più a distinguerli quando in uno stesso ordine ricorrono più
            // laboratori diversi. Non essendoci qui un ID ArcLab del laboratorio (solo il nome), la
            // numerazione è per nome distinto, nel primo ordine in cui compare; le frasi si riportano
            // una sola volta ciascuna, dopo l'intero allegato tecnico.
            var numeroLab = new Dictionary<string, int>();
            var fraseLab = new List<string>();
            foreach (var c in o.Campioni)
                foreach (var an in c.Analisi)
                {
                    if (string.IsNullOrWhiteSpace(an.LaboratorioEsterno)) continue;
                    var nomeLab = an.LaboratorioEsterno!.Trim();
                    if (numeroLab.ContainsKey(nomeLab)) continue;
                    numeroLab[nomeLab] = numeroLab.Count + 1;
                    fraseLab.Add(TestiOfferta.FraseSubappalto(an.FraseLabEsterno, nomeLab));
                }

            foreach (var c in o.Campioni)
            {
                Aggiungi(DoppiaRiga());
                Aggiungi(Titolo(c.Riferimento + " — " + (c.Denominazione ?? "").ToUpperInvariant()));

                var d = Tabella(false, 4080, 5558); // etichette su una riga, i valori possono andare a capo // larghezza utile A4 con margini di 2 cm = 9638 twips
                DatoRiga(d, "Riferimento", c.Riferimento);
                DatoRiga(d, "Descrizione", c.Denominazione);
                DatoRiga(d, "Matrice", c.Matrice);
                if (!string.IsNullOrWhiteSpace(c.Imballaggio)) DatoRiga(d, "Imballaggio", c.Imballaggio);
                if (!string.IsNullOrWhiteSpace(c.PuntoPrelievo)) DatoRiga(d, "Punto di prelievo", c.PuntoPrelievo);
                if (c.LuogoPrelievo is not null) DatoRiga(d, "Luogo di prelievo", c.LuogoPrelievo);
                DatoRiga(d, "Data accettazione", o.DataRicezione?.ToString("dd/MM/yyyy", It));
                if (o.DataPrelievo is not null) DatoRiga(d, "Data prelievo", o.DataPrelievo?.ToString("dd/MM/yyyy", It));
                if (!string.IsNullOrWhiteSpace(c.TemperaturaRicevimento)) DatoRiga(d, "T° di ricevimento", c.TemperaturaRicevimento);
                // Priorità solo se urgente su richiesta del Cliente; prelievo e autocontrollo (Allegato A, Artt. 5.3 e 7.2).
                if (c.Urgente) DatoRiga(d, "Priorità", "Urgente (su richiesta del Cliente)");
                if (!string.IsNullOrWhiteSpace(c.Prelevatore)) DatoRiga(d, "Prelievo eseguito da", c.Prelevatore);
                if (c.AutocontrolloTesto is not null) DatoRiga(d, "Autocontrollo", c.AutocontrolloTesto);
                if (c.ComunicazioneAnticipataTesto is not null) DatoRiga(d, "Comunicazione anticipata dei fuori limite", c.ComunicazioneAnticipataTesto);
                if (o.GiudizioConformitaTesto is not null) DatoRiga(d, "Giudizio di conformità e pareri", o.GiudizioConformitaTesto);
                var ctrl = c.Controllo is null ? null
                    : c.Controllo + (c.IdControllo is int idc ? $" (ID controllo {idc})" : "");
                if (ctrl is not null) DatoRiga(d, "Controllo", ctrl);   // campi vuoti non stampati (15/10/2026)
                Aggiungi(d);

                Aggiungi(Para("Analisi", bold: true, size: 20, spazioPrima: 100, spazioDopo: 40));
                if (c.Analisi.Count > 0)
                {
                    var ta = Tabella(4650, 4140, 850);
                    ta.Append(RigaTab(true, "F7F7F7", ("Denominazione", 4650, false), ("Metodo", 4140, false), ("Accr.", 850, false)));
                    foreach (var (an, numero) in c.AnalisiRaggruppate())
                    {
                        // Come nelle offerte/preventivi: in subappalto il simbolo del laboratorio esterno
                        // (§, §1, §2...) sostituisce l'indicazione di accreditamento nostro.
                        var accr = !string.IsNullOrWhiteSpace(an.LaboratorioEsterno)
                            ? Simbolo(numeroLab.Count, numeroLab[an.LaboratorioEsterno!.Trim()])
                            : (an.Accreditato ? "Sì" : "—");
                        ta.Append(RigaTab(false, null, (CampioneAccettazione.EtichettaRaggruppata(an, numero), 4650, false), (an.Metodo ?? "—", 4140, false), (accr, 850, false)));
                    }
                    Aggiungi(ta);
                }
                else Aggiungi(Para("Nessuna prova associata.", size: 17));
            }

            // Frasi dei laboratori esterni (subappalto): una sola volta per laboratorio, con il simbolo
            // corrispondente a quello riportato nell'allegato tecnico (semplice "§" se il laboratorio è uno solo).
            for (var i = 0; i < fraseLab.Count; i++)
                Aggiungi(Para($"{Simbolo(numeroLab.Count, i + 1)} {fraseLab[i]}", size: 16));

            // Avviso ACCREDIA: una sola volta per documento (non ripetuto per ogni campione/laboratorio),
            // evidenziato perché vale per tutte le prove in subappalto marcate con §.
            if (numeroLab.Count > 0)
                Aggiungi(Para(TestoVerificaAccredia(numeroLab.Count), bold: true, size: 17, shadingFill: "FFF3D6", spazioPrima: 120, spazioDopo: 120));

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

            // Piè di pagina: modulo, n. accettazione e "Pagina X di Y" su ogni piè di pagina del modello
            // (30/09/2026), in fondo, senza toccare la grafica della carta intestata.
            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                var para = new Paragraph(new ParagraphProperties(new Justification { Val = JustificationValues.Center }));
                para.Append(RunPiede(GeneratoreOrdineAccettazione.PiedePagina(o) + "Pagina "));
                var fPag = new SimpleField { Instruction = "PAGE" }; fPag.Append(RunPiede("1"));
                var fTot = new SimpleField { Instruction = "NUMPAGES" }; fTot.Append(RunPiede("1"));
                para.Append(fPag);
                para.Append(RunPiede(" di "));
                para.Append(fTot);
                footerPart.Footer.Append(para);
                footerPart.Footer.Save();
            }
        }

        return ms.ToArray();
    }

    // ── Riquadro condizioni: tabella a cella unica con bordo e sfondo ──
    private static Table RiquadroCondizioni(string revisione)
    {
        var t = Tabella(true, 9638);
        var cellPr = new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "9638" },
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
    internal static string Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();
    internal static string Fmt(decimal v) => v.ToString("N2", It);

    internal static Paragraph Para(string testo, bool bold = false, int size = 20, bool italic = false,
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

    internal static Paragraph Titolo(string testo) =>
        Para(testo, bold: true, size: 22, shadingFill: "EEEEEE", spazioPrima: 160, spazioDopo: 60);

    // Doppia riga nera: paragrafo vuoto con bordo inferiore "double" (due filetti paralleli).
    internal static Paragraph DoppiaRiga()
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

    internal static Run RunPiede(string testo)
    {
        var rPr = new RunProperties(
            new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome },
            new FontSize { Val = "14" });
        var run = new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(rPr);
        return run;
    }

    internal static Run RunTesto(string testo, bool bold)
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
    internal static Table Tabella(params int[] larghezze) => Tabella(true, larghezze);

    internal static Table Tabella(bool bordi, params int[] larghezze)
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

    internal static TableRow RigaTab(bool grassetto, string? shadingFill, params (string Testo, int Larghezza, bool Destra)[] celle)
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

    /// <summary>
    /// Cella email con andata a capo manuale quando è lunga: un indirizzo email non ha spazi, quindi
    /// il word-wrap normale non lo spezzerebbe e sforerebbe la larghezza della colonna. L'interruzione
    /// si inserisce dopo ogni carattere "di aggancio" naturale (@ . _ -) quando il segmento accumulato
    /// supera una soglia, così l'indirizzo va a capo dentro la cella.
    /// </summary>
    private static TableCell CellaEmail(string? email, int larghezza)
    {
        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome });
        rPr.Append(new FontSize { Val = "17" });

        var para = new Paragraph(new ParagraphProperties());
        var testo = string.IsNullOrWhiteSpace(email) ? null : email!.Trim();
        if (testo is null)
        {
            var run = new Run(new Text("—") { Space = SpaceProcessingModeValues.Preserve });
            run.PrependChild((RunProperties)rPr.CloneNode(true));
            para.Append(run);
        }
        else
        {
            const int sogliaSegmento = 16;
            var ultimo = 0;
            for (var i = 0; i < testo.Length; i++)
            {
                var spezzabile = testo[i] is '@' or '.' or '_' or '-';
                if (spezzabile && i - ultimo >= sogliaSegmento)
                {
                    var run = new Run(new Text(testo.Substring(ultimo, i - ultimo + 1)) { Space = SpaceProcessingModeValues.Preserve });
                    run.PrependChild((RunProperties)rPr.CloneNode(true));
                    para.Append(run);
                    para.Append(new Run(new Break()));
                    ultimo = i + 1;
                }
            }
            if (ultimo < testo.Length)
            {
                var run = new Run(new Text(testo.Substring(ultimo)) { Space = SpaceProcessingModeValues.Preserve });
                run.PrependChild((RunProperties)rPr.CloneNode(true));
                para.Append(run);
            }
        }

        var tcPr = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = larghezza.ToString() });
        return new TableCell(tcPr, para);
    }

    internal static void DatoRiga(Table t, string label, string? valore)
    {
        var tr = new TableRow();
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2600" }),
            Para(label, bold: true, size: 18, cella: true)));
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "7038" }),
            Para(string.IsNullOrWhiteSpace(valore) ? "—" : valore!.Trim(), size: 18, cella: true)));
        t.Append(tr);
    }

    internal static TableRow RigaImporto(string label, decimal importo, bool grassetto)
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
