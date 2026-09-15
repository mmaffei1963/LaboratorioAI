using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Genera l'offerta scrivendola dentro il modello Word di carta intestata (header/footer reali).
/// Usa solo OpenXml (nessun MigraDoc) per evitare conflitti di tipo. Lavora su una copia in memoria:
/// il modello non viene mai modificato.
/// </summary>
internal static class OffertaDocx
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>Rientro laterale aggiuntivo del contenuto (twips). 0 = usa direttamente i margini di pagina.</summary>
    private const int IndentLato = 0;

    /// <summary>Carattere del documento.</summary>
    private const string FontNome = "Montserrat";

    /// <summary>Margine sinistro/destro della pagina in twips (2 cm = 1134).</summary>
    private const uint MargineLatoTwips = 1134;

    public static byte[] Genera(Preventivo p, string percorsoModello)
    {
        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(percorsoModello)) fs.CopyTo(ms);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var sectPr = body.Elements<SectionProperties>().LastOrDefault();

            // Margini pagina: 2 cm a sinistra e destra (l'intestazione/piè della carta restano invariati).
            var pm = sectPr?.Elements<PageMargin>().FirstOrDefault();
            if (pm is not null) { pm.Left = MargineLatoTwips; pm.Right = MargineLatoTwips; }

            void Aggiungi(OpenXmlElement e)
            {
                if (sectPr is not null) body.InsertBefore(e, sectPr);
                else body.AppendChild(e);
            }

            var num = p.Numero is int n ? $"{p.Anno}/{n:000}" : "—";
            Aggiungi(Para($"OFFERTA N. {num}", bold: true, size: 28, align: JustificationValues.Center, spazioPrima: 120, spazioDopo: 120));

            // Testata cliente / offerta
            var data = (p.DataOfferta ?? DateTime.Today).ToString("dd/MM/yyyy", It);
            Aggiungi(Riga2("Data offerta", data, "Validità", $"{p.ValiditaGiorni} giorni"));
            Aggiungi(Riga2("Cliente", p.ClienteRagioneSociale ?? "—", "Partita IVA", Vuoto(p.ClientePartitaIva)));
            var indirizzo = string.Join(" ", new[] { p.ClienteIndirizzo, p.ClienteCap, p.ClienteCitta, p.ClienteProvincia }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            Aggiungi(Riga1("Indirizzo", Vuoto(indirizzo)));
            Aggiungi(Riga1("Oggetto", Vuoto(p.Oggetto)));

            var righe = p.Righe.OrderBy(r => r.Progressivo).ToList();
            var controlli = righe.Where(r => r.Tipo != TipoRigaPreventivo.Accessorio).ToList();
            var accessori = righe.Where(r => r.Tipo == TipoRigaPreventivo.Accessorio).ToList();

            // Blocchi analisi
            foreach (var r in controlli)
            {
                var subappalto = r.Metodi.Any(m => m.IdLabEsternoArcLab != null) || r.Tipo == TipoRigaPreventivo.ControlloLibero;

                var idPart = r.IdControlloArcLab is int idc ? $" (ID {idc})" : "";
                Aggiungi(Titolo("ANALISI DI LABORATORIO — " + (r.Descrizione ?? "").ToUpperInvariant() + idPart));
                if (subappalto)
                    Aggiungi(Para("(Le seguenti analisi possono essere eseguite presso laboratorio esterno accreditato)", size: 17, italic: true));

                if (r.Metodi.Count > 0)
                {
                    var t = Tabella(4150, 4150, 900);
                    t.Append(RigaTab(true, "F3F3F3", ("Parametro", 4150, false), ("Metodo", 4150, false), ("Accr.", 900, false)));
                    foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
                        t.Append(RigaTab(false, null,
                            (m.Descrizione ?? m.DenominazioneRiga ?? "—", 4150, false),
                            (m.RiferimentoNormativo ?? "—", 4150, false),
                            (m.IdLabEsternoArcLab != null ? "*" : (m.Accreditato ? "Sì" : "—"), 900, false)));
                    Aggiungi(t);
                }

                var prezzo = $"Prezzo: € {Fmt(r.PrezzoFinale)}" + (r.ScontoPercentuale > 0 ? $"  (sconto {Fmt(r.ScontoPercentuale)}%)" : "");
                Aggiungi(Para(prezzo, bold: true, align: JustificationValues.Right, spazioDopo: 60));

                foreach (var frase in r.Metodi.Where(m => !string.IsNullOrWhiteSpace(m.LabEsternoFrase))
                                              .Select(m => m.LabEsternoFrase!.Trim()).Distinct())
                    Aggiungi(Para("* " + frase, size: 17));
            }

            // Servizi accessori
            if (accessori.Count > 0)
            {
                Aggiungi(Titolo("SERVIZI ACCESSORI"));
                var t = Tabella(7200, 2000);
                foreach (var a in accessori)
                    t.Append(RigaTab(false, null,
                        ((a.Descrizione ?? "—") + (a.Quantita > 1 ? $" × {a.Quantita}" : ""), 7200, false),
                        ($"€ {Fmt(a.PrezzoFinale)}", 2000, true)));
                Aggiungi(t);
            }

            // Condizioni economiche
            var iva = Math.Round(p.TotaleImponibile * p.AliquotaIva / 100m, 2, MidpointRounding.AwayFromZero);
            Aggiungi(Titolo("CONDIZIONI ECONOMICHE"));
            var te = Tabella(false, 7200, 2000);
            te.Append(RigaImporto("Imponibile", p.TotaleImponibile, false));
            te.Append(RigaImporto($"IVA {Fmt(p.AliquotaIva)}%", iva, false));
            te.Append(RigaImporto("TOTALE", p.TotaleImponibile + iva, true));
            Aggiungi(te);

            if (!string.IsNullOrWhiteSpace(p.CondizioniPagamento))
                Aggiungi(Para("Condizioni di pagamento: " + p.CondizioniPagamento!.Trim(), size: 17, bold: true, spazioPrima: 120));
            Aggiungi(Para($"Offerta valida {p.ValiditaGiorni} giorni dalla data indicata. I prezzi si intendono IVA esclusa.", size: 17));
            if (!string.IsNullOrWhiteSpace(p.Note))
                Aggiungi(Para(p.Note!.Trim(), size: 17));

            // Condizioni generali di fornitura
            var rev = string.IsNullOrWhiteSpace(p.RevisioneCondizioni) ? "Mod. Com. 05" : p.RevisioneCondizioni!.Trim();
            Aggiungi(Titolo("CONDIZIONI GENERALI DI FORNITURA"));
            Aggiungi(Para(
                $"Il presente contratto è integrato dalle Condizioni Generali di Fornitura di MARE.A s.r.l. ({rev}), che ne " +
                "costituiscono parte integrante e sostanziale. Le Condizioni Generali di Fornitura sono scaricabili dal sito " +
                "www.mare-a.com e disponibili su richiesta. Il COMMITTENTE dichiara di conoscerle e di accettarle integralmente, " +
                "con particolare riferimento alle clausole su prove in subappalto, tempi di consegna, regola decisionale e " +
                "giudizi di conformità, campionamento, conservazione dei campioni e trattamento dei dati. In caso di contrasto, " +
                "per le sole condizioni economiche e tecniche specifiche prevale quanto indicato nel presente contratto.", size: 17));
            Aggiungi(Para(
                "La presente offerta e le Condizioni Generali di Fornitura si intendono integralmente accettate dal COMMITTENTE " +
                "al momento della consegna del campione al laboratorio.", size: 17, bold: true, spazioPrima: 120));

            // Informativa privacy (fissa, uguale per ogni offerta)
            AggiungiInformativaPrivacy(Aggiungi);

            // Firma
            Aggiungi(Para("Per accettazione (timbro e firma)                                        MARE.A s.r.l.", spazioPrima: 240));
            Aggiungi(Para("______________________________                                        ______________________________", spazioPrima: 240));

            doc.MainDocumentPart.Document.Save();
        }

        return ms.ToArray();
    }

    // ── Helper testo ───────────────────────────────────────────────
    private static string Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();
    private static string Fmt(decimal v) => v.ToString("N2", It);

    // ── Informativa privacy (art. 13 Reg. UE 2016/679) — testo fisso MARE.A ──
    private static void Sotto(Action<OpenXmlElement> agg, string t) => agg(Para(t, bold: true, size: 17, spazioPrima: 80));
    private static void Testo(Action<OpenXmlElement> agg, string t) => agg(Para(t, size: 16));

    private static void AggiungiInformativaPrivacy(Action<OpenXmlElement> agg)
    {
        agg(Para("", size: 8, spazioPrima: 220));   // un po' di spazio dopo le Condizioni Generali
        agg(Titolo("INFORMATIVA PRIVACY"));
        agg(Para("Ai sensi dell'art. 13 Regolamento UE 2016/679", size: 16, italic: true, spazioDopo: 60));

        Sotto(agg, "Identità e dati di contatto del titolare del trattamento");
        Testo(agg, "Il titolare del trattamento è la ditta MARE.A s.r.l. con sede legale in Via E. Toti, 2 - 47841, Cattolica (RN) tel. 0541 830442, fax 0541 830460 e-mail: info@mare-a.com – CF/P.IVA 03670240401.");

        Sotto(agg, "Finalità e base giuridica del trattamento");
        Testo(agg, "I suoi dati personali sono trattati nel rispetto dei principi di correttezza, liceità e trasparenza, per le seguenti finalità:");
        Testo(agg, "1) Fornitura del servizio ed adempimenti amministrativi e fiscali connessi al rapporto contrattuale;");
        Testo(agg, "2) Servizio di assistenza post-vendita, ivi compresi eventuali contatti a mezzo posta cartacea e/o telefonici e/o tramite e-mail e/o sms per comunicazioni inerenti lo svolgimento dei servizi da lei affidatici;");
        Testo(agg, "3) Newsletter: previo ottenimento del suo consenso, il Titolare e gli incaricati al trattamento potranno inoltre inviarle, a mezzo posta cartacea, via e-mail, sms o App di messaggistica, occasionali aggiornamenti circa nuovi servizi obbligatori e/o funzionali alla sua attività;");
        Testo(agg, "4) Profilazione: previo ottenimento del suo consenso, il Titolare e gli incaricati al trattamento potranno categorizzare la sua attività (es. Hotel, Ristorante, Ingrosso ecc.) al fine di poter selezionare più accuratamente le comunicazioni a lei destinate.");

        Sotto(agg, "Destinatari dei dati personali");
        Testo(agg, "Nell'ambito dei trattamenti di cui alla presente informativa, i suoi dati, anche appartenenti alle categorie particolari di cui all'art. 9 del Regolamento UE 2016/679, saranno trattati da nostri incaricati interni e potranno essere comunicati anche a soggetti terzi, formalmente nominati responsabili del trattamento.");
        Testo(agg, "Nell'ambito del trattamento di cui al punto 1), in particolare, i suoi dati saranno comunicati ai consulenti amministrativi e fiscali del Titolare ed alle Autorità pubbliche competenti in materia.");

        Sotto(agg, "Obbligo di fornirci i suoi dati");
        Testo(agg, "La fornitura dei suoi dati personali per le finalità 1) e 2), anche appartenenti alle categorie particolari di cui all'art. 9 del Regolamento UE 2016/679, è necessaria ai fini dell'esecuzione del servizio contrattuale e conseguentemente la loro mancata o incompleta fornitura renderà impossibile assicurare tale servizio.");
        Testo(agg, "Il conferimento dei dati per le finalità di cui ai punti 3) e 4), invece, non è necessario ed il soggetto interessato ha il diritto di opporsi in qualsiasi momento a tale trattamento, anche in parte, inviando la richiesta all'indirizzo del Titolare del trattamento indicato sopra. Per quanto riguarda le finalità di cui al punto 4), infine, il conferimento dei suoi dati non è obbligatorio, ma il mancato consenso alla profilazione ci impedirà di inviarle comunicazioni personalizzate.");

        Sotto(agg, "Periodo di conservazione dei dati");
        Testo(agg, "I suoi dati personali saranno conservati per tutto il tempo imposto in base a disposizioni normative inerenti i servizi da lei affidatici e la disciplina amministrativa e fiscale.");
        Testo(agg, "Per finalità informative e per l'invio di aggiornamenti normativi, news relative ad iniziative commerciali, nonché per comunicare iniziative ed altri eventi, saranno conservati per un periodo di 24 mesi, fatta salva l'ipotesi di ulteriori vendite e/o sue richieste che dovessero avvenire in tale arco di tempo e/o la necessità di conservare i dati per le altre finalità di cui alla presente informativa.");

        Sotto(agg, "Diritti dell'interessato");
        Testo(agg, "Lei ha diritto di revocare il consenso prestato in calce alla presente informativa in qualsiasi momento tramite comunicazione all'indirizzo del Titolare del trattamento. Allo stesso modo lei potrà chiedere l'accesso ai suoi dati personali, la rettifica, la cancellazione, la limitazione o l'opposizione del trattamento oltre che la portabilità dei dati (art. 20 del Regolamento UE 2016/679). La richiesta di cancellazione dei dati è subordinata agli obblighi di conservazione dei documenti imposti da norme di legge.");

        Sotto(agg, "Diritto di proporre reclamo all'Autorità di controllo");
        Testo(agg, "Lei potrà, nel caso ritenga che il trattamento di dati che la riguardano violi il Regolamento UE 2016/679, proporre reclamo all'Autorità per la protezione dei dati personali.");

        Sotto(agg, "Consensi");
        Testo(agg, "Acconsento al trattamento dei dati di natura particolare ai sensi dell'art. 9 del Reg. UE 2016/679.");
        Testo(agg, "[  ] SI        [  ] NO");
        Testo(agg, "Acconsento ad essere contattato a mezzo posta cartacea, via e-mail, sms o App di messaggistica, per l'invio di occasionali aggiornamenti circa nuovi servizi obbligatori e/o funzionali alla sua attività.");
        Testo(agg, "[  ] SI        [  ] NO");
        Testo(agg, "Acconsento al trattamento dei miei dati personali per l'attività di profilazione al fine di poter selezionare più accuratamente le comunicazioni a lei destinate.");
        Testo(agg, "[  ] SI        [  ] NO");
    }

    private static Paragraph Para(string testo, bool bold = false, int size = 20, bool italic = false,
        JustificationValues? align = null, string? shadingFill = null, int spazioPrima = 0, int spazioDopo = 0,
        bool cella = false)
    {
        var pPr = new ParagraphProperties();
        if (shadingFill is not null)
            pPr.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = shadingFill });
        if (spazioPrima > 0 || spazioDopo > 0)
            pPr.Append(new SpacingBetweenLines { Before = spazioPrima.ToString(), After = spazioDopo.ToString() });
        if (!cella)
            pPr.Append(new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
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

    private static Paragraph Riga1(string label, string valore)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { After = "20" },
            new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
        var para = new Paragraph(pPr);
        para.Append(RunTesto(label + ": ", true));
        para.Append(RunTesto(valore, false));
        return para;
    }

    private static Paragraph Riga2(string l1, string v1, string l2, string v2)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { After = "20" },
            new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
        var para = new Paragraph(pPr);
        para.Append(RunTesto(l1 + ": ", true));
        para.Append(RunTesto(v1 + "        ", false));
        para.Append(RunTesto(l2 + ": ", true));
        para.Append(RunTesto(v2, false));
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

    // ── Helper tabelle ─────────────────────────────────────────────
    private static Table Tabella(params int[] larghezze) => Tabella(true, larghezze);

    private static Table Tabella(bool bordi, params int[] larghezze)
    {
        var props = new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Dxa, Width = larghezze.Sum().ToString() },
            new TableIndentation { Type = TableWidthUnitValues.Dxa, Width = IndentLato });
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

    // Il param bool per cella indica l'allineamento a destra.
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
