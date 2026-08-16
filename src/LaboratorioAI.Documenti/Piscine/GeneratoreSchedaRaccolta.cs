// LaboratorioAI.Documenti/Piscine/GeneratoreSchedaRaccolta.cs
//
// Produce un modulo Word VUOTO e stampabile per la raccolta dei dati piscine
// presso il cliente (compilazione a mano durante il sopralluogo). Non usa un
// modello a token: costruisce il documento da zero con OpenXml. È indipendente
// dai dati salvati, così può essere stampato anche prima di aprire una scheda.
//
// I campi rispecchiano la scheda di raccolta dell'applicazione. La sezione
// specifica Marche viene inclusa solo quando richiesto.
//
// Nota OpenXml: l'ORDINE dei figli di RunProperties, ParagraphProperties,
// TableProperties e TableCellProperties è vincolato dallo schema. Rispettarlo
// è ciò che garantisce l'apertura del file in Word (non solo in LibreOffice).

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LaboratorioAI.Documenti.Piscine;

/// <summary>Genera il modulo cartaceo di raccolta dati piscine (vuoto).</summary>
public static class GeneratoreSchedaRaccolta
{
    /// <summary>Costruisce il .docx in memoria. <paramref name="marche"/> aggiunge la sezione regionale.</summary>
    public static byte[] Genera(bool marche)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = new Body();
            main.Document.Append(body);

            Intestazione(body, marche);

            SezioneCampi(body, "1. Dati identificativi",
                "Ragione sociale", "Denominazione insediamento", "Indirizzo",
                "Comune", "Provincia", "Partita IVA / Codice fiscale",
                "Codice contabilità", "Data del sopralluogo", "Rilevato da");

            SezioneCampi(body, "2. Struttura e classificazione",
                "Tipologia struttura (albergo / villaggio / residence / agriturismo / altro)",
                "Codice ATECO", "Attività esercitata", "Periodo di esercizio",
                "Anno di costruzione", "Categoria piscina (A / B / C)", "Gruppo",
                "Collocazione impianto (coperto / scoperto / misto)", "Numero di vasche");

            SezioneCampi(body, "3. Autorizzazioni e approvvigionamento",
                "Autorizzazione sanitaria (n° e data)", "Autorizzazione allo scarico",
                "Fonte di approvvigionamento idrico");

            SezioneCampi(body, "4. Figure e responsabilità",
                "Datore di lavoro", "Gestore", "Responsabile della piscina",
                "Addetto agli impianti", "Responsabile sicurezza bagnanti",
                "Assistente bagnanti", "Addetto alle pulizie");

            SezioneVaschePerColonna(body, "5. Vasche", new[]
            {
                "Denominazione", "Tipo (nuoto / bambini / idromassaggio…)", "Forma",
                "Dimensioni (L × L, m)", "Profondità min – max (cm)", "Superficie (m²)",
                "Volume (m³)", "Temperatura (°C)", "Capienza bagnanti",
                "Struttura", "Rivestimento", "Ricircolo (sfioro / bordo / misto)"
            });

            SezioneVaschePerColonna(body, "6. Impianti tecnologici per vasca", new[]
            {
                "Pompe – numero", "Pompe – potenza (kW)", "Pompe – portata (m³/h)",
                "Pompe di riserva (Sì / No)", "Filtri – numero", "Filtri – diametro",
                "Filtri – tipologia", "Prefiltri – numero", "Controlavaggi – modalità",
                "Controlavaggi – frequenza", "Immissione", "Ripresa",
                "Bocchette a parete – n°", "Bocchette a fondo – n°",
                "Vasca di compenso (m³)", "Contatore dedicato", "Ispezioni"
            });

            SezioneCampi(body, "7. Trattamento dell'acqua",
                "Tipo di trattamento (cloro / sale / ossigeno attivo / UV-ozono / altro)",
                "Sistema di dosaggio", "Principio attivo",
                "Sistema automatico di analisi (Sì / No, tipo)");

            SezioneTabellaEtichettata(body, "8. Sostanze chimiche impiegate",
                new[] { "Impiego", "Prodotto (nome commerciale)", "Dosaggio / modalità" },
                new[] { "Disinfettante", "Reintegro cloro", "Flocculante",
                        "Correttore pH", "Antialghe" });

            SezioneTabellaEtichettata(body, "9. HACCP – punti critici di controllo",
                new[] { "Punto critico", "Pericolo", "Azione preventiva / correttiva", "Monitoraggio" },
                new[] { "pH", "Cloro libero", "Cloro combinato", "Pompe", "Filtri" });

            SezioneCampi(body, "10. Vigilanza e sicurezza bagnanti",
                "Presenza assistente bagnanti (continuativo / fasce orarie / assente)",
                "Superficie vasca (< / > 100 m²)", "Profondità vasca (≤ / > 140 cm)",
                "Lati del bordo liberi da ostacoli (Sì / No)",
                "Istruttori di nuoto presenti", "DAE presente (Sì / No)",
                "Modalità di vigilanza dei bagnanti");

            SezioneCampi(body, "11. Servizi igienici, docce, pronto soccorso",
                "Numero docce", "Numero lavabo", "Numero lavapiedi",
                "Passaggio obbligato / accesso alle vasche",
                "Pronto soccorso – ubicazione e dotazioni",
                "Accessibilità dei mezzi di soccorso");

            SezioneTabellaVuota(body, "12. Punti di prelievo",
                new[] { "Punto", "Ubicazione", "Frequenza" }, 6);

            SezioneChecklist(body, "13. Misure di prevenzione (verifica in loco)",
                "Griglie di fondo conformi", "Cartellonistica e segnaletica",
                "Salvagente e dotazioni di salvataggio", "Cassetta di pronto soccorso",
                "DAE (defibrillatore)", "Regolamento interno esposto",
                "Registro dei controlli", "Piano di manutenzione",
                "Formazione del personale");

            if (marche)
            {
                SezioneCampi(body, "14. Dati specifici — Registro tecnico funzionale (Marche)",
                    "Gruppo struttura", "Caratteristiche vasca – superficie (< / > 240 m²)",
                    "Caratteristiche vasca – profondità (≤ / > 160 cm)",
                    "Posti letto (< / > 120)", "Immissione (n° / tipo)",
                    "Ripresa (superficie / n° / tipo)", "Filtri (modello / altezza / portata)",
                    "Pompe (modello / HP / riserva)", "Curva caratteristica",
                    "Neutralizzante (prodotto / modalità)", "Sistema automatico",
                    "Numero massimo bagnanti", "Manutenzione interna");

                SezioneTabellaEtichettata(body, "15. Ditte di manutenzione",
                    new[] { "Tipo", "Ragione sociale", "Sede", "Comune", "P.IVA" },
                    new[] { "Ordinaria", "Straordinaria" });

                SezioneCampi(body, "16. Gestore — dati anagrafici (Marche)",
                    "Data di nascita", "Luogo di nascita", "Residenza",
                    "Comune", "Codice fiscale");
            }

            SezioneNote(body);

            body.Append(ProprietaSezione());
            main.Document.Save();
        }
        return ms.ToArray();
    }

    // ═══════════════════════════════════════════════════════════════════
    // BLOCCHI DI CONTENUTO
    // ═══════════════════════════════════════════════════════════════════

    private static void Intestazione(Body body, bool marche)
    {
        var tipo = marche
            ? "Registro tecnico funzionale — impianti natatori (Marche)"
            : "Manuale di autocontrollo — impianti natatori (Emilia-Romagna)";

        body.Append(Paragrafo("MARE.A srl — Laboratorio accreditato ACCREDIA n° 00729",
            18, bold: true, colore: "555555"));
        body.Append(Paragrafo("Scheda di raccolta dati per il sopralluogo", 30, bold: true,
            spazioPrima: 60));
        body.Append(Paragrafo(tipo, 22, colore: "555555", spazioDopo: 40));
        body.Append(Paragrafo(
            "Modulo da compilare a mano durante il sopralluogo e da riportare poi nell'applicazione.",
            18, corsivo: true, colore: "777777", spazioDopo: 160));
    }

    private static void SezioneNote(Body body)
    {
        body.Append(TitoloSezione("Note"));
        for (var i = 0; i < 4; i++)
            body.Append(RigaVuota());
    }

    // ── Sezione a campi (etichetta | spazio da compilare) ──────────────
    private static void SezioneCampi(Body body, string titolo, params string[] etichette)
    {
        body.Append(TitoloSezione(titolo));

        var larghezze = new[] { 3100, 6200 };
        var tabella = TabellaBase(larghezze);
        foreach (var etichetta in etichette)
        {
            var riga = new TableRow(AltezzaRiga(360));
            riga.Append(Cella(etichetta, larghezze[0], sfondo: "F2F2F2"));
            riga.Append(Cella("", larghezze[1]));
            tabella.Append(riga);
        }
        body.Append(tabella);
        body.Append(Spazio());
    }

    // ── Sezione "campi × colonne" (per le vasche) ──────────────────────
    private static void SezioneVaschePerColonna(Body body, string titolo, string[] campi)
    {
        body.Append(TitoloSezione(titolo));

        var colonne = new[] { "Vasca 1", "Vasca 2", "Vasca 3" };
        var larghezze = new[] { 3000, 2100, 2100, 2100 };
        var tabella = TabellaBase(larghezze);

        var intest = new TableRow(AltezzaRiga(320));
        intest.Append(Cella("", larghezze[0], sfondo: "F2F2F2"));
        for (var i = 0; i < colonne.Length; i++)
            intest.Append(Cella(colonne[i], larghezze[i + 1], sfondo: "F2F2F2", grassetto: true, centrato: true));
        tabella.Append(intest);

        foreach (var campo in campi)
        {
            var riga = new TableRow(AltezzaRiga(360));
            riga.Append(Cella(campo, larghezze[0], sfondo: "F8F8F8"));
            for (var i = 0; i < colonne.Length; i++)
                riga.Append(Cella("", larghezze[i + 1]));
            tabella.Append(riga);
        }
        body.Append(tabella);
        body.Append(Spazio());
    }

    // ── Tabella con righe pre-etichettate e colonne da compilare ───────
    private static void SezioneTabellaEtichettata(Body body, string titolo,
        string[] intestazioni, string[] righeEtichette)
    {
        body.Append(TitoloSezione(titolo));

        var larghezze = LarghezzeColonne(intestazioni.Length, primaLarga: true);
        var tabella = TabellaBase(larghezze);

        var intest = new TableRow(AltezzaRiga(320));
        for (var i = 0; i < intestazioni.Length; i++)
            intest.Append(Cella(intestazioni[i], larghezze[i], sfondo: "F2F2F2",
                grassetto: true, centrato: i > 0));
        tabella.Append(intest);

        foreach (var etichetta in righeEtichette)
        {
            var riga = new TableRow(AltezzaRiga(380));
            riga.Append(Cella(etichetta, larghezze[0], sfondo: "F8F8F8"));
            for (var i = 1; i < intestazioni.Length; i++)
                riga.Append(Cella("", larghezze[i]));
            tabella.Append(riga);
        }
        body.Append(tabella);
        body.Append(Spazio());
    }

    // ── Tabella con sole intestazioni e righe vuote ────────────────────
    private static void SezioneTabellaVuota(Body body, string titolo,
        string[] intestazioni, int righeVuote)
    {
        body.Append(TitoloSezione(titolo));

        var larghezze = LarghezzeColonne(intestazioni.Length, primaLarga: false);
        var tabella = TabellaBase(larghezze);

        var intest = new TableRow(AltezzaRiga(320));
        for (var i = 0; i < intestazioni.Length; i++)
            intest.Append(Cella(intestazioni[i], larghezze[i], sfondo: "F2F2F2", grassetto: true));
        tabella.Append(intest);

        for (var r = 0; r < righeVuote; r++)
        {
            var riga = new TableRow(AltezzaRiga(380));
            for (var i = 0; i < intestazioni.Length; i++)
                riga.Append(Cella("", larghezze[i]));
            tabella.Append(riga);
        }
        body.Append(tabella);
        body.Append(Spazio());
    }

    // ── Checklist con caselle da spuntare ──────────────────────────────
    private static void SezioneChecklist(Body body, string titolo, params string[] voci)
    {
        body.Append(TitoloSezione(titolo));
        foreach (var voce in voci)
            body.Append(Paragrafo("☐  " + voce, 20, spazioDopo: 40));
        body.Append(Spazio());
    }

    // ═══════════════════════════════════════════════════════════════════
    // PRIMITIVE OPENXML  (ordine dei figli conforme allo schema)
    // ═══════════════════════════════════════════════════════════════════

    private static Paragraph TitoloSezione(string testo) => Paragrafo(
        testo, 24, bold: true, spazioPrima: 200, spazioDopo: 80, bordoInferiore: true);

    private static Paragraph Spazio() => Paragrafo("", 8);

    private static Paragraph RigaVuota()
    {
        // pBdr prima di spacing (ordine di schema).
        var proprieta = new ParagraphProperties();
        proprieta.Append(new ParagraphBorders(new BottomBorder
        {
            Val = BorderValues.Single, Size = 4, Color = "BBBBBB", Space = 1
        }));
        proprieta.Append(new SpacingBetweenLines { After = "220" });
        return new Paragraph(proprieta);
    }

    private static Paragraph Paragrafo(string testo, int size, bool bold = false,
        bool corsivo = false, string? colore = null, int spazioPrima = 0, int spazioDopo = 0,
        bool bordoInferiore = false)
    {
        // ParagraphProperties: pBdr → spacing (ordine di schema).
        var proprieta = new ParagraphProperties();
        if (bordoInferiore)
            proprieta.Append(new ParagraphBorders(new BottomBorder
            {
                Val = BorderValues.Single, Size = 6, Color = "888888", Space = 2
            }));
        proprieta.Append(new SpacingBetweenLines
        {
            Before = spazioPrima.ToString(),
            After = spazioDopo.ToString(),
            Line = "240", LineRule = LineSpacingRuleValues.Auto
        });

        // RunProperties: rFonts → b → i → color → sz (ordine di schema).
        var runProps = new RunProperties();
        runProps.Append(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
        if (bold) runProps.Append(new Bold());
        if (corsivo) runProps.Append(new Italic());
        if (colore is not null) runProps.Append(new Color { Val = colore });
        runProps.Append(new FontSize { Val = size.ToString() });

        var run = new Run(runProps, new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(proprieta, run);
    }

    private static Table TabellaBase(int[] larghezzeColonne)
    {
        var tabella = new Table();
        tabella.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "AAAAAA" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "AAAAAA" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "AAAAAA" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "AAAAAA" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" }),
            new TableLayout { Type = TableLayoutValues.Fixed }));

        var griglia = new TableGrid();
        foreach (var larghezza in larghezzeColonne)
            griglia.Append(new GridColumn { Width = larghezza.ToString() });
        tabella.Append(griglia);

        return tabella;
    }

    private static TableRowProperties AltezzaRiga(int twips) =>
        new(new TableRowHeight
        {
            Val = (uint)twips, HeightType = HeightRuleValues.AtLeast
        });

    private static TableCell Cella(string testo, int larghezza, string? sfondo = null,
        bool grassetto = false, bool centrato = false)
    {
        // TableCellProperties: tcW → shd → vAlign (ordine di schema).
        var proprieta = new TableCellProperties();
        proprieta.Append(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = larghezza.ToString() });
        if (sfondo is not null)
            proprieta.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = sfondo });
        proprieta.Append(new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center });

        Paragraph par;
        if (string.IsNullOrEmpty(testo))
        {
            var pp = new ParagraphProperties(new SpacingBetweenLines { Before = "20", After = "20" });
            if (centrato) pp.Append(new Justification { Val = JustificationValues.Center });
            par = new Paragraph(pp);
        }
        else
        {
            par = Paragrafo(testo, 19, bold: grassetto);
            if (centrato)
                par.ParagraphProperties!.Append(new Justification { Val = JustificationValues.Center });
        }

        return new TableCell(proprieta, par);
    }

    private static int[] LarghezzeColonne(int numero, bool primaLarga)
    {
        const int totale = 9300;
        var larghezze = new int[numero];
        if (primaLarga && numero > 1)
        {
            larghezze[0] = 2600;
            var resto = (totale - 2600) / (numero - 1);
            for (var i = 1; i < numero; i++) larghezze[i] = resto;
        }
        else
        {
            var quota = totale / numero;
            for (var i = 0; i < numero; i++) larghezze[i] = quota;
        }
        return larghezze;
    }

    private static SectionProperties ProprietaSezione() => new(
        new PageSize { Width = 11906, Height = 16838 },              // A4 verticale
        new PageMargin { Top = 900, Bottom = 900, Left = 900, Right = 900, Header = 480, Footer = 480 });
}
