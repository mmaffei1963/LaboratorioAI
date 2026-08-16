// LaboratorioAI.Documenti/Piscine/GeneratorePiscine.cs
//
// Produzione del documento piscine con DocumentFormat.OpenXml.
// Flusso: apre il modello -> valorizza i controlli contenuto per TAG ->
// mantiene/rimuove le sezioni condizionate se_ -> replica le righe delle
// tabelle tab_ -> abilita l'aggiornamento dei campi (indice) -> salva.
//
// NOTA DI INTEGRAZIONE
// Le operazioni di basso livello sui controlli contenuto qui isolate nei metodi
// privati (ImpostaTesto, ImpostaCasella, GestisciBlocco, PopolaTabelle) e la
// normalizzazione del testo corrispondono ai helper già usati dai DVR
// (ControlliContenuto, RifinituraDocumento, NormalizzazioneTesto). In fase di
// innesto in Laboratorio AI vanno SOSTITUITE dal riuso di quei helper — in
// particolare NormalizzazioneTesto va riusata, non riscritta. Qui sono locali
// solo per rendere il file autonomo e verificabile.

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Modelli;
using Office = DocumentFormat.OpenXml.Office2010.Word;

namespace LaboratorioAI.Documenti.Piscine;

/// <summary>Generatore dei documenti piscine basato su OpenXml.</summary>
public sealed class GeneratorePiscine : IGeneratorePiscine
{
    private readonly OpzioniPiscine _opzioni;

    public GeneratorePiscine(OpzioniPiscine opzioni) => _opzioni = opzioni;

    /// <inheritdoc/>
    public RisultatoGenerazionePiscina Genera(SchedaPiscina scheda)
    {
        try
        {
            var valori = MappaValori.Mappa(scheda);
            var mancanti = new List<string>();

            // Si lavora su una copia in memoria del modello: il modello sorgente
            // non viene mai modificato.
            using var ms = new MemoryStream();
            var modello = File.ReadAllBytes(_opzioni.PercorsoModello);
            ms.Write(modello, 0, modello.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, true))
            {
                var body = doc.MainDocumentPart?.Document.Body
                    ?? throw new InvalidOperationException("Modello privo di corpo documento.");

                ApplicaControlli(body, valori, mancanti);
                PopolaTabelle(body, valori.Tabelle, mancanti);
                AbilitaAggiornamentoCampi(doc);

                doc.MainDocumentPart!.Document.Save();
            }

            var documento = ms.ToArray();
            var percorso = ArchiviaSePresente(scheda, documento);
            return RisultatoGenerazionePiscina.Ok(documento, mancanti, percorso);
        }
        catch (Exception ex)
        {
            return RisultatoGenerazionePiscina.Fallito(ex.Message);
        }
    }

    // ---- Controlli contenuto (testo, discesa, data, casella, blocco se_) ----

    private static void ApplicaControlli(Body body, ValoriModello valori, List<string> mancanti)
    {
        var trovati = new HashSet<string>(StringComparer.Ordinal);

        // Snapshot: si modifica l'albero durante l'iterazione (rimozione blocchi).
        foreach (var sdt in body.Descendants<SdtElement>().ToList())
        {
            var tag = LeggiTag(sdt);
            if (tag is null)
                continue;

            if (valori.Blocchi.TryGetValue(tag, out var mantieni))
            {
                GestisciBlocco(sdt, mantieni);
                trovati.Add(tag);
            }
            else if (valori.Caselle.TryGetValue(tag, out var spuntato))
            {
                ImpostaCasella(sdt, spuntato);
                trovati.Add(tag);
            }
            else if (valori.Testi.TryGetValue(tag, out var testo))
            {
                ImpostaTesto(sdt, testo);
                trovati.Add(tag);
            }
        }

        foreach (var tag in valori.Testi.Keys.Concat(valori.Caselle.Keys).Concat(valori.Blocchi.Keys))
            if (!trovati.Contains(tag))
                mancanti.Add(tag);
    }

    private static string? LeggiTag(SdtElement sdt) =>
        sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

    private static OpenXmlElement? ContenutoDi(SdtElement sdt) =>
        (OpenXmlElement?)sdt.GetFirstChild<SdtContentRun>()
        ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentBlock>()
        ?? sdt.GetFirstChild<SdtContentCell>();

    /// <summary>Imposta il testo di un controllo di testo/discesa/data.</summary>
    private static void ImpostaTesto(SdtElement sdt, string? valore)
    {
        var runs = sdt.Descendants<Run>().ToList();
        if (runs.Count == 0)
        {
            var contenuto = ContenutoDi(sdt);
            if (contenuto is null)
                return;
            runs.Add(contenuto.AppendChild(new Run()));
        }

        var primo = runs[0];
        // Toglie l'evidenziazione gialla del segnaposto, se presente.
        primo.RunProperties?.GetFirstChild<Highlight>()?.Remove();

        var text = primo.GetFirstChild<Text>() ?? primo.AppendChild(new Text());
        text.Text = valore ?? string.Empty;
        text.Space = SpaceProcessingModeValues.Preserve;

        // Elimina eventuali run successivi, così non resta traccia del segnaposto.
        for (var i = 1; i < runs.Count; i++)
            runs[i].Remove();
    }

    /// <summary>Spunta o deseleziona una casella di controllo (w14:checkbox).</summary>
    private static void ImpostaCasella(SdtElement sdt, bool spuntato)
    {
        var checkbox = sdt.SdtProperties?.GetFirstChild<Office.SdtContentCheckBox>();
        if (checkbox is not null)
        {
            checkbox.Checked ??= new Office.Checked();
            checkbox.Checked.Val = spuntato ? Office.OnOffValues.One : Office.OnOffValues.Zero;
        }

        // Glifo visibile: ☒ (2612) se spuntato, ☐ (2610) se vuoto.
        var text = sdt.Descendants<Run>().FirstOrDefault()?.GetFirstChild<Text>();
        if (text is not null)
        {
            text.Text = spuntato ? "☒" : "☐";
            text.Space = SpaceProcessingModeValues.Preserve;
        }
    }

    /// <summary>Mantiene (sciogliendo il controllo-blocco) o rimuove una sezione se_.</summary>
    private static void GestisciBlocco(SdtElement sdt, bool mantieni)
    {
        if (!mantieni)
        {
            sdt.Remove();
            return;
        }

        // Mantieni: sostituisci l'SDT con il contenuto del blocco (unwrap).
        var contenuto = sdt.GetFirstChild<SdtContentBlock>();
        var genitore = sdt.Parent;
        if (contenuto is null || genitore is null)
            return;

        foreach (var figlio in contenuto.ChildElements.ToList())
            genitore.InsertBefore(figlio.CloneNode(true), sdt);

        sdt.Remove();
    }

    // ---- Tabelle a righe variabili tab_ -------------------------------------

    private static void PopolaTabelle(
        Body body,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string?>>> tabelle,
        List<string> mancanti)
    {
        foreach (var (tag, righe) in tabelle)
        {
            var cellaMarcatore = body.Descendants<TableCell>()
                .FirstOrDefault(c => ContieneMarcatore(c, tag));

            var rigaModello = cellaMarcatore?.Ancestors<TableRow>().FirstOrDefault();
            var tabella = rigaModello?.Ancestors<Table>().FirstOrDefault();
            if (rigaModello is null || tabella is null)
            {
                mancanti.Add(tag);
                continue;
            }

            OpenXmlElement punto = rigaModello;
            foreach (var riga in righe)
            {
                var nuova = (TableRow)rigaModello.CloneNode(true);
                RiempiRiga(nuova, riga);
                tabella.InsertAfter(nuova, punto);
                punto = nuova;
            }

            // Rimuove la riga-modello che contiene il marcatore tab_.
            rigaModello.Remove();
        }
    }

    private static bool ContieneMarcatore(TableCell cella, string tag) =>
        Normalizza(TestoDi(cella)).Contains(tag, StringComparison.OrdinalIgnoreCase);

    private static void RiempiRiga(TableRow riga, IReadOnlyList<string?> valori)
    {
        var celle = riga.Elements<TableCell>().ToList();
        for (var i = 0; i < celle.Count; i++)
            ImpostaTestoCella(celle[i], i < valori.Count ? valori[i] : null);
    }

    private static void ImpostaTestoCella(TableCell cella, string? valore)
    {
        // Conserva il primo paragrafo (e le sue proprietà) rimuovendo gli altri,
        // poi sostituisce i run con il valore.
        var paragrafi = cella.Elements<Paragraph>().ToList();
        for (var i = 1; i < paragrafi.Count; i++)
            paragrafi[i].Remove();

        var par = paragrafi.Count > 0 ? paragrafi[0] : cella.AppendChild(new Paragraph());
        foreach (var run in par.Elements<Run>().ToList())
            run.Remove();

        var nuovo = par.AppendChild(new Run());
        var text = nuovo.AppendChild(new Text(valore ?? string.Empty));
        text.Space = SpaceProcessingModeValues.Preserve;
    }

    // ---- Rifinitura e utilità ----------------------------------------------

    /// <summary>
    /// Chiede a Word di aggiornare i campi all'apertura, così l'indice riflette
    /// il contenuto effettivo del documento generato.
    /// </summary>
    private static void AbilitaAggiornamentoCampi(WordprocessingDocument doc)
    {
        var parte = doc.MainDocumentPart!.DocumentSettingsPart
            ?? doc.MainDocumentPart.AddNewPart<DocumentSettingsPart>();
        parte.Settings ??= new Settings();

        if (parte.Settings.GetFirstChild<UpdateFieldsOnOpen>() is null)
            parte.Settings.AppendChild(new UpdateFieldsOnOpen { Val = true });

        parte.Settings.Save();
    }

    private static string TestoDi(OpenXmlElement elemento) =>
        string.Concat(elemento.Descendants<Text>().Select(t => t.Text));

    // Versione locale minima: in integrazione usare NormalizzazioneTesto di
    // Laboratorio AI (va riusata, non riscritta).
    private static string Normalizza(string? testo) => (testo ?? string.Empty)
        .Replace('’', '\'')
        .Replace('‘', '\'')
        .Replace('–', '-')
        .Replace('—', '-')
        .Trim();

    private string? ArchiviaSePresente(SchedaPiscina scheda, byte[] documento)
    {
        if (string.IsNullOrWhiteSpace(_opzioni.CartellaArchivio))
            return null;

        Directory.CreateDirectory(_opzioni.CartellaArchivio);
        var nome = $"DVR_Piscine_{NomeFile(scheda)}_{scheda.DataValutazione:yyyyMMdd}.docx";
        var percorso = Path.Combine(_opzioni.CartellaArchivio, nome);
        File.WriteAllBytes(percorso, documento);
        return percorso;
    }

    /// <summary>Etichetta per il nome file: codice contabilità + ragione sociale, ripuliti.</summary>
    private static string NomeFile(SchedaPiscina scheda)
    {
        var a = scheda.Anagrafica;
        var etichetta = $"{a.CodContabilita}_{a.RagioneSociale}".Trim('_', ' ');
        foreach (var c in Path.GetInvalidFileNameChars())
            etichetta = etichetta.Replace(c, '_');
        etichetta = etichetta.Replace(' ', '_');
        if (etichetta.Length > 60) etichetta = etichetta[..60];
        return string.IsNullOrWhiteSpace(etichetta) ? a.IdInsediamento.ToString() : etichetta;
    }

    // ═══════════════════════════════════════════════════════════════
    // REGOLAMENTO INTERNO (modello a token testuali)
    // ═══════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public RisultatoGenerazionePiscina GeneraRegolamento(SchedaPiscina scheda)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_opzioni.RegolamentoModello))
                return RisultatoGenerazionePiscina.Fallito("Modello del Regolamento non configurato.");

            var mappa = MappaRegolamento(scheda);

            using var ms = new MemoryStream();
            var modello = File.ReadAllBytes(_opzioni.RegolamentoModello);
            ms.Write(modello, 0, modello.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, true))
            {
                var body = doc.MainDocumentPart?.Document.Body
                    ?? throw new InvalidOperationException("Modello privo di corpo documento.");

                // La riga idromassaggio compare solo se esiste una vasca di quel tipo.
                var haIdro = scheda.Vasche.Any(v =>
                    (v.Tipo ?? "").Contains("idromass", StringComparison.OrdinalIgnoreCase));
                if (!haIdro)
                    RimuoviBlocchiIdromassaggio(body);

                foreach (var testo in body.Descendants<Text>())
                    foreach (var kv in mappa)
                        if (testo.Text.Contains(kv.Key))
                            testo.Text = testo.Text.Replace(kv.Key, kv.Value);

                doc.MainDocumentPart!.Document.Save();
            }

            var documento = ms.ToArray();
            var percorso = ArchiviaConNome(documento, $"Regolamento_Piscine_{NomeFile(scheda)}_{scheda.DataValutazione:yyyyMMdd}.docx");
            return RisultatoGenerazionePiscina.Ok(documento, new List<string>(), percorso);
        }
        catch (Exception ex)
        {
            return RisultatoGenerazionePiscina.Fallito(ex.Message);
        }
    }

    private static Dictionary<string, string> MappaRegolamento(SchedaPiscina s)
    {
        static bool EIdro(VascaPiscina v) =>
            (v.Tipo ?? "").Contains("idromass", StringComparison.OrdinalIgnoreCase);
        static string V(string? x) => string.IsNullOrWhiteSpace(x) ? "____" : x!;

        // Converte una profondità in piedi. Numeri >= 10 interpretati come cm,
        // altrimenti come metri (es. "139" → 4,56 ft; "1,39" → 4,56 ft).
        static string Ft(string? prof)
        {
            if (string.IsNullOrWhiteSpace(prof)) return "____";
            var m = System.Text.RegularExpressions.Regex.Match(prof.Replace(',', '.'), @"\d+(\.\d+)?");
            if (!m.Success || !double.TryParse(m.Value, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var n))
                return "____";
            var metri = n >= 10 ? n / 100.0 : n;
            return (metri * 3.28084).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }

        var principale = s.Vasche.FirstOrDefault(v => !EIdro(v)) ?? s.Vasche.FirstOrDefault();
        var idro = s.Vasche.FirstOrDefault(EIdro);

        return new Dictionary<string, string>
        {
            ["{ragione_sociale}"]    = s.Anagrafica.RagioneSociale ?? "",
            ["{orario_dalle}"]       = V(s.OrarioDalle),
            ["{orario_alle}"]        = V(s.OrarioAlle),
            ["{ubicazione_servizi}"] = V(s.UbicazioneServizi),
            ["{cap_vasca}"]          = V(principale?.Capienza),
            ["{prof_vasca}"]         = V(principale?.Profondita),
            ["{prof_vasca_ft}"]      = Ft(principale?.Profondita),
            ["{cap_idro}"]           = V(idro?.Capienza),
            ["{prof_idro}"]          = V(idro?.Profondita),
            ["{prof_idro_ft}"]       = Ft(idro?.Profondita),
        };
    }

    private string? ArchiviaConNome(byte[] documento, string nome)
    {
        if (string.IsNullOrWhiteSpace(_opzioni.CartellaArchivio))
            return null;

        Directory.CreateDirectory(_opzioni.CartellaArchivio);
        var percorso = Path.Combine(_opzioni.CartellaArchivio, nome);
        File.WriteAllBytes(percorso, documento);
        return percorso;
    }

    /// <summary>Rimuove dal regolamento le righe/paragrafi dell'idromassaggio (token dedicati).</summary>
    private static void RimuoviBlocchiIdromassaggio(Body body)
    {
        foreach (var riga in body.Descendants<TableRow>()
                     .Where(r => r.InnerText.Contains("{prof_idro}")).ToList())
            riga.Remove();

        foreach (var par in body.Descendants<Paragraph>()
                     .Where(p => p.InnerText.Contains("{cap_idro}")).ToList())
            par.Remove();
    }

    // ═══════════════════════════════════════════════════════════════
    // MANUALE DI AUTOCONTROLLO / REGISTRO TECNICO (modello a token, per regione)
    // ═══════════════════════════════════════════════════════════════

    /// <inheritdoc/>
    public RisultatoGenerazionePiscina GeneraManuale(SchedaPiscina scheda)
    {
        try
        {
            var modelloPath = ModelloManualePerRegione(scheda.Anagrafica.Regione);
            if (string.IsNullOrWhiteSpace(modelloPath))
                return RisultatoGenerazionePiscina.Fallito(
                    "Modello del Manuale non configurato per la regione dell'insediamento.");
            if (!File.Exists(modelloPath))
                return RisultatoGenerazionePiscina.Fallito($"Modello non trovato: {modelloPath}");

            var mappa = MappaManuale(scheda);

            using var ms = new MemoryStream();
            var modello = File.ReadAllBytes(modelloPath);
            ms.Write(modello, 0, modello.Length);
            ms.Position = 0;

            using (var doc = WordprocessingDocument.Open(ms, true))
            {
                var parte = doc.MainDocumentPart
                    ?? throw new InvalidOperationException("Modello privo di corpo documento.");

                // Regola UNI 10637 sulla vasca idromassaggio (prima della sostituzione token).
                if (parte.Document.Body is { } corpo)
                    GestisciIdromassaggio(corpo, scheda);

                // Corpo (comprese le caselle di testo della copertina) e, soprattutto,
                // intestazioni e piè di pagina: i token vivono anche lì.
                SostituisciToken(parte.Document, mappa);
                foreach (var header in parte.HeaderParts)
                    SostituisciToken(header.Header, mappa);
                foreach (var footer in parte.FooterParts)
                    SostituisciToken(footer.Footer, mappa);

                // Tabella dei certificati dell'acqua di piscina (solo se presenti).
                if (parte.Document.Body is { } corpoCert)
                    InserisciTabellaCertificati(corpoCert, scheda);

                // Rete di sicurezza: un <w:p> non può contenere un altro <w:p>.
                // Word rifiuta i paragrafi annidati (LibreOffice li tollera).
                if (parte.Document.Body is { } b2)
                    CorreggiParagrafiAnnidati(b2);

                AbilitaAggiornamentoCampi(doc);
                parte.Document.Save();
            }

            var documento = ms.ToArray();
            var percorso = ArchiviaConNome(documento,
                $"Manuale_Piscine_{NomeFile(scheda)}_{scheda.DataValutazione:yyyyMMdd}.docx");
            return RisultatoGenerazionePiscina.Ok(documento, new List<string>(), percorso);
        }
        catch (Exception ex)
        {
            return RisultatoGenerazionePiscina.Fallito(ex.Message);
        }
    }

    /// <summary>
    /// Applica la regola UNI 10637 alla vasca idromassaggio: se assente, o con
    /// volume &lt; 4 m³, la sezione «VASCA IDROMASSAGGIO» viene rimossa; se il
    /// volume è &lt; 4 m³ si inserisce una frase in rosso. Con volume ≥ 4 m³ (o
    /// non determinabile) la sezione resta e la premessa si valorizza col token.
    /// </summary>
    private static void GestisciIdromassaggio(Body body, SchedaPiscina scheda)
    {
        static bool EIdro(VascaPiscina v) =>
            (v.Tipo ?? "").Contains("idromass", StringComparison.OrdinalIgnoreCase);

        var idro = scheda.Vasche.FirstOrDefault(EIdro);
        var volume = ParsaNumero(idro?.Volume);
        var tratta = idro is not null && (volume is null || volume >= 4.0);
        if (tratta) return;   // sezione mantenuta; {vi_premessa} verrà valorizzato

        var elems = body.ChildElements.ToList();
        var start = elems.FindIndex(e =>
            e is Paragraph && TestoDi(e).ToUpperInvariant().Contains("VASCA IDROMASSAGGIO"));
        if (start < 0) return;
        var end = elems.FindIndex(start + 1, e =>
            e is Paragraph && TestoDi(e).ToUpperInvariant().Contains("SEZIONE SERVIZI"));
        if (end < 0) end = elems.Count;

        var daRimuovere = elems.GetRange(start, end - start);

        // Segnalibri (heading) contenuti nella sezione rimossa: le loro voci
        // nell'indice (TOC) vanno tolte, altrimenti restano PAGEREF a segnalibri
        // inesistenti e Word rifiuta di aprire il documento.
        var segnalibriRimossi = daRimuovere
            .SelectMany(e => e.Descendants<BookmarkStart>())
            .Select(b => b.Name?.Value)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet();

        if (idro is not null)   // presente ma < 4 m³: mostra la frase in rosso
        {
            var frase = ParagrafoRosso(
                "La vasca, accatastata come idromassaggio, presenta un volume inferiore a 4m3; " +
                "pertanto non è accomunabile ad una vasca piscina ai sensi della UNI 10637 e " +
                "quindi non viene trattata in questo manuale");
            daRimuovere[0].Parent!.InsertBefore(frase, daRimuovere[0]);
        }
        foreach (var e in daRimuovere) e.Remove();

        if (segnalibriRimossi.Count > 0)
            RimuoviVociIndice(body, segnalibriRimossi);
    }

    /// <summary>
    /// Inserisce la tabella dei certificati dell'acqua di piscina prima della
    /// sezione «Punti di prelievo», solo se ci sono analisi storiche.
    /// </summary>
    private static void InserisciTabellaCertificati(Body body, SchedaPiscina scheda)
    {
        if (scheda.AnalisiStoriche.Count == 0) return;

        // Il titolo vero è in maiuscolo (stile Titolo2); esiste anche una voce
        // elenco «Punti di prelievo» in minuscolo, da non agganciare.
        var ancora = body.Descendants<Paragraph>()
            .FirstOrDefault(p => string.Equals(TestoDi(p).Trim(), "PUNTI DI PRELIEVO", StringComparison.Ordinal));
        if (ancora?.Parent is null) return;

        var titolo = ParagrafoTitolo("CERTIFICATI DELLE ANALISI DELL'ACQUA DI PISCINA");
        var tabella = CostruisciTabellaCertificati(scheda.AnalisiStoriche);

        ancora.Parent.InsertBefore(titolo, ancora);
        ancora.Parent.InsertBefore(tabella, ancora);
        ancora.Parent.InsertBefore(new Paragraph(), ancora);   // stacco prima di «Punti di prelievo»
    }

    private static Paragraph ParagrafoTitolo(string testo)
    {
        var pPr = new ParagraphProperties(new SpacingBetweenLines { Before = "200", After = "80" });
        var rPr = new RunProperties(
            new RunFonts { Ascii = "Montserrat", HighAnsi = "Montserrat" },
            new Bold(), new Color { Val = "003399" }, new FontSize { Val = "22" });
        return new Paragraph(pPr, new Run(rPr, new Text(testo) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table CostruisciTabellaCertificati(IReadOnlyList<AnalisiStoricaPiscina> righe)
    {
        var t = new Table(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4 },
                new BottomBorder { Val = BorderValues.Single, Size = 4 },
                new LeftBorder { Val = BorderValues.Single, Size = 4 },
                new RightBorder { Val = BorderValues.Single, Size = 4 },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 })));

        t.Append(RigaCertificati(
            new[] { "Campione", "Prelievo", "Matrice", "Punto", "Luogo", "N° certificato", "Data cert." }, true));

        foreach (var r in righe)
            t.Append(RigaCertificati(new[]
            {
                r.NumeroCampione ?? "",
                (r.DataPrelievo ?? r.DataRicezione)?.ToString("dd/MM/yyyy") ?? "",
                r.Matrice ?? "",
                r.PuntoPrelievo ?? "",
                r.LuogoPrelievo ?? "",
                r.NumeroCertificato ?? "",
                r.DataCertificato?.ToString("dd/MM/yyyy") ?? "",
            }, false));

        return t;
    }

    private static TableRow RigaCertificati(string[] valori, bool intestazione)
    {
        var riga = new TableRow();
        foreach (var v in valori)
        {
            var rPr = intestazione
                ? new RunProperties(new RunFonts { Ascii = "Montserrat", HighAnsi = "Montserrat" }, new Bold(), new FontSize { Val = "16" })
                : new RunProperties(new RunFonts { Ascii = "Montserrat", HighAnsi = "Montserrat" }, new FontSize { Val = "16" });
            var par = new Paragraph(new Run(rPr, new Text(v) { Space = SpaceProcessingModeValues.Preserve }));
            riga.Append(new TableCell(new TableCellProperties(), par));
        }
        return riga;
    }

    /// <summary>
    /// Corregge i paragrafi annidati (<c>w:p</c> figlio diretto di un altro
    /// <c>w:p</c>): sposta il contenuto inline nel paragrafo padre e rimuove il
    /// paragrafo annidato. Struttura invalida che Word rifiuta.
    /// </summary>
    private static void CorreggiParagrafiAnnidati(OpenXmlElement radice)
    {
        foreach (var padre in radice.Descendants<Paragraph>().ToList())
        {
            foreach (var annidato in padre.Elements<Paragraph>().ToList())
            {
                foreach (var figlio in annidato.ChildElements
                             .Where(c => c is not ParagraphProperties).ToList())
                {
                    figlio.Remove();
                    padre.InsertBefore(figlio, annidato);
                }
                annidato.Remove();
            }
        }
    }

    /// <summary>Rimuove le voci di indice (paragrafi con campo PAGEREF) che puntano ai segnalibri indicati.</summary>
    private static void RimuoviVociIndice(Body body, IReadOnlySet<string> segnalibri)
    {
        foreach (var par in body.Descendants<Paragraph>().ToList())
        {
            var codice = string.Concat(par.Descendants<FieldCode>().Select(f => f.Text));
            if (codice.Contains("PAGEREF") && segnalibri.Any(bm => codice.Contains(bm!)))
                par.Remove();
        }
    }

    private static double? ParsaNumero(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        var m = System.Text.RegularExpressions.Regex.Match(testo.Replace(',', '.'), @"\d+(\.\d+)?");
        return m.Success && double.TryParse(m.Value, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static Paragraph ParagrafoRosso(string testo)
    {
        var pPr = new ParagraphProperties(new SpacingBetweenLines { After = "600" }); // 30 pt dopo
        var rPr = new RunProperties(
            new RunFonts { Ascii = "Montserrat", HighAnsi = "Montserrat" },
            new Color { Val = "EE0000" },
            new FontSize { Val = "20" },              // 10 pt
            new FontSizeComplexScript { Val = "20" });
        var run = new Run(rPr, new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(pPr, run);
    }

    /// <summary>Sostituisce i token testuali in una parte del documento (corpo, header o footer).</summary>
    private static void SostituisciToken(OpenXmlElement radice, IReadOnlyDictionary<string, string> mappa)
    {
        foreach (var testo in radice.Descendants<Text>())
            foreach (var kv in mappa)
                if (testo.Text.Contains(kv.Key))
                    testo.Text = testo.Text.Replace(kv.Key, kv.Value);
    }

    /// <summary>
    /// Sceglie il modello del manuale in base alla regione (Marche vs default ER),
    /// preferendo un modello effettivamente presente su disco: così, finché il
    /// modello Marche non è pronto, gli insediamenti marchigiani ripiegano su ER.
    /// </summary>
    private string? ModelloManualePerRegione(string? regione)
    {
        var r = Normalizza(regione).ToLowerInvariant();
        var preferito = r.Contains("march") ? _opzioni.ManualeModelloMarche : _opzioni.ManualeModelloEmiliaRomagna;
        var alternativo = r.Contains("march") ? _opzioni.ManualeModelloEmiliaRomagna : _opzioni.ManualeModelloMarche;

        if (!string.IsNullOrWhiteSpace(preferito) && File.Exists(preferito)) return preferito;
        if (!string.IsNullOrWhiteSpace(alternativo) && File.Exists(alternativo)) return alternativo;
        return preferito ?? alternativo;   // lascia emergere l'errore "non trovato" con un percorso sensato
    }

    /// <summary>
    /// Mappa dei token del Manuale (strato 1: intestazione, anagrafica, figure,
    /// classificazione, autorizzazioni). Un modello contiene solo i token che gli
    /// servono: le voci non usate sono innocue.
    /// </summary>
    private static Dictionary<string, string> MappaManuale(SchedaPiscina s)
    {
        static string V(string? x) => string.IsNullOrWhiteSpace(x) ? "" : x!.Trim();
        var a = s.Anagrafica;

        static string Categoria(CategoriaPiscina? c) => c switch
        {
            CategoriaPiscina.A => "Categoria A – Piscine di proprietà pubblica o privata, destinate ad utenza pubblica",
            CategoriaPiscina.B => "Categoria B – Piscine di proprietà pubblica o privata, ad uso collettivo (condominiali, alberghiere)",
            CategoriaPiscina.C => "Categoria C – Piscine ad usi speciali (curative, termali)",
            _ => "",
        };

        // Descrizione del numero vasche dai tipi presenti (es. "1 vasca principale / 1 vasca idromassaggio").
        static bool EIdro(VascaPiscina v) =>
            (v.Tipo ?? "").Contains("idromass", StringComparison.OrdinalIgnoreCase);
        string NumeroVasche()
        {
            if (s.Vasche.Count == 0)
                return s.NumeroVasche is int n ? $"{n}" : "";
            var idro = s.Vasche.Count(EIdro);
            var altre = s.Vasche.Count - idro;
            var parti = new List<string>();
            if (altre > 0) parti.Add($"{altre} vasca principale");
            if (idro > 0) parti.Add($"{idro} vasca idromassaggio");
            return string.Join(" / ", parti);
        }

        // Vasca principale e idromassaggio: alimentano i blocchi vp_ / vi_ del manuale.
        var principale = s.Vasche.FirstOrDefault(v => !EIdro(v)) ?? s.Vasche.FirstOrDefault();
        var idro = s.Vasche.FirstOrDefault(EIdro);

        // Frase completa sulle pompe di riserva (Sì → testo, altrimenti vuoto).
        static string Riserva(VascaPiscina? v) =>
            string.Equals(v?.PompeRiserva, "Sì", StringComparison.OrdinalIgnoreCase)
                ? "L'impianto è dotato di pompe di riserva che possono essere inserite in caso di anomalia della pompa, al fine di garantire in ogni momento un'adeguata circolazione dell'acqua"
                : "";

        return new Dictionary<string, string>
        {
            // Dati generali azienda
            ["{ragione_sociale}"]   = V(a.RagioneSociale),
            ["{comune_provincia}"]  = V(a.ComuneProvincia),
            ["{sede_legale}"]       = V(a.SedeLegale),
            ["{sede_operativa}"]    = V(a.Indirizzo),
            ["{piva}"]              = V(a.PartitaIva),
            ["{codfisc}"]           = V(a.CodiceFiscale),
            ["{ateco}"]             = V(s.CodiceAteco),
            ["{attivita}"]          = V(s.AttivitaEsercitata),
            ["{periodo_esercizio}"] = V(s.PeriodoEsercizio),
            // Classificazione e autorizzazioni
            ["{anno_costruzione}"]  = s.AnnoCostruzione?.ToString() ?? "",
            ["{aut_protocollo}"]    = V(s.AutorizzazioneSanitaria),
            ["{aut_data}"]          = s.DataAutorizzazioneSanitaria?.ToString("dd/MM/yyyy") ?? "",
            ["{numero_vasche}"]     = NumeroVasche(),
            ["{categoria}"]         = Categoria(s.Categoria),
            ["{gruppo}"]            = V(s.Gruppo),
            // Caratteristiche tecniche funzionali e vigilanza
            ["{tipologia_strutturale}"]   = V(s.TipologiaStrutturale),
            ["{tipo_utilizzazione}"]      = V(s.TipoUtilizzazione),
            ["{fonte_approvvigionamento}"]= V(s.FonteApprovvigionamento),
            ["{contatore_immissione}"]    = V(s.ContatoreImmissione),
            ["{registrazione_ingressi}"]  = V(s.RegistrazioneIngressi),
            ["{carvasca_superficie}"]     = V(s.CaratteristicheVascaSuperficie),
            ["{carvasca_profondita}"]     = V(s.CaratteristicheVascaProfondita),
            ["{lati_bordo}"]              = V(s.LatiBordoLiberi),
            ["{vigilanza_bagnanti}"]      = V(s.VigilanzaBagnanti),
            ["{assistente_bagnanti}"]     = V(s.AssistenteBagnantiScelta),
            ["{istruttori_nuoto}"]        = V(s.IstruttoriNuoto),
            // HACCP — analisi punti critici (pag. 44+)
            ["{ph_pericolo}"]         = V(s.PhPericolo),
            ["{ph_azione_prev}"]      = V(s.PhAzionePreventiva),
            ["{ph_monitoraggio}"]     = V(s.PhMonitoraggio),
            ["{cll_pericolo}"]        = V(s.CloroLiberoPericolo),
            ["{cll_azione_prev}"]     = V(s.CloroLiberoAzionePreventiva),
            ["{cll_monitoraggio}"]    = V(s.CloroLiberoMonitoraggio),
            ["{clc_pericolo}"]        = V(s.CloroCombinatoPericolo),
            ["{pompe_azione_corr}"]   = V(s.PompeAzioneCorrettiva),
            ["{filtri_azione_corr}"]  = V(s.FiltriAzioneCorrettiva),
            // Sostanze chimiche per il trattamento
            ["{disinf_prodotto}"]     = V(s.DisinfettanteProdotto),
            ["{disinf_dosaggio}"]     = V(s.DisinfettanteDosaggio),
            ["{reintegro_prodotto}"]  = V(s.ReintegroCloroProdotto),
            ["{reintegro_modalita}"]  = V(s.ReintegroCloroModalita),
            ["{flocc_prodotto}"]      = V(s.FlocculanteProdotto),
            ["{flocc_modalita}"]      = V(s.FlocculanteModalita),
            ["{corrph_prodotto}"]     = V(s.CorrettorePhProdotto),
            ["{corrph_modalita}"]     = V(s.CorrettorePhModalita),
            ["{antialghe_prodotto}"]  = V(s.AntialgheProdotto),
            ["{antialghe_modalita}"]  = V(s.AntialgheModalita),
            // Servizi igienici
            ["{servizi_caratterizzazione}"] = V(s.ServiziCaratterizzazione),
            ["{servizi_distinzione}"]       = V(s.ServiziDistinzione),
            ["{servizi_n_docce}"]           = V(s.ServiziNumeroDocce),
            ["{servizi_n_lavabo}"]          = V(s.ServiziNumeroLavabo),
            // Servizi, docce, lavapiedi, pronto soccorso, accesso
            ["{passaggio_accesso}"]      = V(s.PassaggioAccesso),
            ["{lavapiedi_numero}"]       = V(s.LavapiedNumero),
            ["{lavapiedi_rilascio}"]     = V(s.LavapiedRilascio),
            ["{lavapiedi_soluzione}"]    = V(s.LavapiedSoluzione),
            ["{antimic_frequenza}"]      = V(s.AntimicFrequenza),
            ["{antimic_procedura}"]      = V(s.AntimicProcedura),
            ["{docce_caratterizzazione}"]= V(s.DocceCaratterizzazione),
            ["{docce_composizione}"]     = V(s.DocceComposizione),
            ["{docce_linee}"]            = V(s.DocceLinee),
            ["{docce_miscelazione}"]     = V(s.DocceMiscelazione),
            ["{pronto_soccorso}"]        = V(s.ProntoSoccorso),
            ["{accessibilita_mezzi}"]    = V(s.AccessibilitaMezzi),
            ["{ps_azione_prev}"]         = V(s.PsAzionePreventiva),
            ["{ps_luogo_cons}"]          = V(s.PsLuogoConservazione),
            ["{ps_ubicazione}"]          = V(s.PsUbicazione),
            ["{addetto_pulizie}"]        = V(s.AddettoPulizie),

            // ── Registro tecnico funzionale (Marche) ──────────────────────
            ["{vp_struttura}"]     = V(principale?.Struttura),
            ["{vp_rivestimento}"]  = V(principale?.Rivestimento),
            ["{vp_dimensioni}"]    = V(principale?.Dimensioni),
            ["{mar_categoria}"]    = s.Categoria switch
            {
                CategoriaPiscina.A => "CATEGORIA A - Piscine di proprietà pubblica o privata, destinate ad utenza pubblica.",
                CategoriaPiscina.B => "CATEGORIA B - Piscine che, destinate esclusivamente all'uso degli alloggiati, fanno parte di:",
                CategoriaPiscina.C => "CATEGORIA C - Piscine ad usi speciali (curative, termali).",
                _ => "",
            },
            ["{mar_gruppo_struttura}"]    = V(s.MarcheGruppoStruttura),
            ["{mar_carvasca_sup}"]        = V(s.MarcheCarVascaSup),
            ["{mar_carvasca_prof}"]       = V(s.MarcheCarVascaProf),
            ["{mar_posti_letto}"]         = V(s.MarchePostiLetto),
            ["{mar_immissione_n}"]        = V(s.MarcheImmissioneN),
            ["{mar_immissione_tipo}"]     = V(s.MarcheImmissioneTipo),
            ["{mar_ripresa_sup}"]         = V(s.MarcheRipresaSup),
            ["{mar_ripresa_imm_n}"]       = V(s.MarcheRipresaImmN),
            ["{mar_ripresa_imm_tipo}"]    = V(s.MarcheRipresaImmTipo),
            ["{mar_filtri_modello}"]      = V(s.MarcheFiltriModello),
            ["{mar_filtri_altezza}"]      = V(s.MarcheFiltriAltezza),
            ["{mar_filtri_portata}"]      = V(s.MarcheFiltriPortata),
            ["{mar_pompe_modello}"]       = V(s.MarchePompeModello),
            ["{mar_pompe_hp}"]            = V(s.MarchePompeHp),
            ["{mar_pompe_riserva}"]       = V(s.MarchePompeRiserva),
            ["{mar_curva}"]               = V(s.MarcheCurva),
            ["{mar_neutralizzante_prod}"] = V(s.MarcheNeutralizzanteProd),
            ["{mar_neutralizzante_mod}"]  = V(s.MarcheNeutralizzanteMod),
            ["{mar_sistema_auto}"]        = V(s.MarcheSistemaAuto),
            ["{ditta_ord_rag}"]    = V(s.DittaOrdRag),
            ["{ditta_ord_sede}"]   = V(s.DittaOrdSede),
            ["{ditta_ord_comune}"] = V(s.DittaOrdComune),
            ["{ditta_ord_piva}"]   = V(s.DittaOrdPiva),
            ["{ditta_str_rag}"]    = V(s.DittaStrRag),
            ["{ditta_str_sede}"]   = V(s.DittaStrSede),
            ["{ditta_str_comune}"] = V(s.DittaStrComune),
            ["{ditta_str_piva}"]   = V(s.DittaStrPiva),
            // Gestore (dati anagrafici) e vasca autonoma Marche
            ["{mar_gestore_nascita}"]   = V(s.MarcheGestoreNascita),
            ["{mar_gestore_luogo}"]     = V(s.MarcheGestoreLuogo),
            ["{mar_gestore_residenza}"] = V(s.MarcheGestoreResidenza),
            ["{mar_gestore_comune}"]    = V(s.MarcheGestoreComune),
            ["{mar_gestore_cf}"]        = V(s.MarcheGestoreCf),
            ["{mar_bagnanti_max}"]      = V(s.MarcheBagnantiMax),
            ["{mar_struttura_vasca}"]   = V(s.MarcheStrutturaVasca),
            ["{mar_rivestimento}"]      = V(s.MarcheRivestimento),
            ["{mar_dimensioni}"]        = V(s.MarcheDimensioni),
            ["{mar_profondita}"]        = V(s.MarcheProfondita),
            ["{mar_superficie}"]        = V(s.MarcheSuperficie),
            ["{mar_volume}"]            = V(s.MarcheVolume),
            ["{mar_filtri_tipo}"]       = V(s.MarcheFiltriTipo),
            ["{mar_filtri_n}"]          = V(s.MarcheFiltriN),
            ["{mar_filtri_diam}"]       = V(s.MarcheFiltriDiam),
            ["{mar_pompe_kw}"]          = V(s.MarchePompeKw),
            ["{mar_pompe_n}"]           = V(s.MarchePompeN),
            ["{mar_manut_interno}"]     = V(s.MarcheManutInterno),
            // Data del documento: usata per la dichiarazione e la firma del responsabile
            ["{data_valutazione}"]  = s.DataValutazione.ToString("dd/MM/yyyy"),
            // Figure di responsabilità: nome intero su una sola riga (niente Nome/Cognome)
            ["{fig_datore}"]        = V(s.DatoreLavoro),
            ["{fig_gestore}"]       = V(s.Gestore),
            ["{fig_resp_piscina}"]  = V(s.ResponsabilePiscina),
            ["{fig_resp_dati}"]     = V(s.ResponsabilePiscina),
            ["{fig_resp_impianti}"] = V(s.AddettoImpianti),
            ["{fig_resp_bagnanti}"] = V(s.ResponsabileSicurezzaBagnanti),
            ["{fig_assistente}"]    = V(s.AssistenteBagnanti),

            // SEZIONE VASCHE — DESCRIZIONE, vasca principale (vp_) e idromassaggio (vi_)
            ["{vp_forma}"]        = V(principale?.Forma),
            ["{vp_superficie}"]   = V(principale?.Superficie),
            ["{vp_volume}"]       = V(principale?.Volume),
            ["{vp_profondita}"]   = V(principale?.Profondita),
            ["{vp_variazione}"]   = V(principale?.VariazioneBatimetrica),
            ["{vp_attrezzature}"] = V(principale?.AttrezzatureAccessorie),
            ["{vi_forma}"]        = V(idro?.Forma),
            ["{vi_superficie}"]   = V(idro?.Superficie),
            ["{vi_volume}"]       = V(idro?.Volume),
            ["{vi_profondita}"]   = V(idro?.Profondita),
            ["{vi_variazione}"]   = V(idro?.VariazioneBatimetrica),
            ["{vi_attrezzature}"] = V(idro?.AttrezzatureAccessorie),

            // SEZIONE VASCHE — IMPIANTO TECNICO (pag. 37)
            ["{vp_delimitazione}"]  = V(principale?.DelimitazioneArea),
            ["{vp_pompe_n}"]        = V(principale?.PompeNumero),
            ["{vp_pompe_potenza}"]  = V(principale?.PompePotenza),
            ["{vp_pompe_portata}"]  = V(principale?.PompePortata),
            ["{vp_filtri_n}"]       = V(principale?.FiltriNumero),
            ["{vp_filtri_diam}"]    = V(principale?.FiltriDiametro),
            ["{vp_filtri_tipo}"]    = V(principale?.FiltriTipologia),
            ["{vp_lavaggi_mod}"]    = V(principale?.ContrLavaggiModalita),
            ["{vp_lavaggi_freq}"]   = V(principale?.ContrLavaggiFrequenza),
            ["{vp_prefiltri_n}"]    = V(principale?.PrefiltriNumero),
            ["{vp_riscaldamento}"]  = V(principale?.Riscaldamento),
            ["{vp_temp_es}"]        = V(principale?.TemperaturaEsercizio),
            ["{vp_ricambio}"]       = V(principale?.RicambioAcqua),
            ["{vp_contatore_ded}"]  = V(principale?.ContatoreDedicato),
            ["{vp_ispezioni}"]      = V(principale?.Ispezioni),
            ["{vi_delimitazione}"]  = V(idro?.DelimitazioneArea),
            ["{vi_pompe_n}"]        = V(idro?.PompeNumero),
            ["{vi_pompe_potenza}"]  = V(idro?.PompePotenza),
            ["{vi_pompe_portata}"]  = V(idro?.PompePortata),
            ["{vi_filtri_n}"]       = V(idro?.FiltriNumero),
            ["{vi_filtri_diam}"]    = V(idro?.FiltriDiametro),
            ["{vi_filtri_tipo}"]    = V(idro?.FiltriTipologia),
            ["{vi_lavaggi_mod}"]    = V(idro?.ContrLavaggiModalita),
            ["{vi_lavaggi_freq}"]   = V(idro?.ContrLavaggiFrequenza),
            ["{vi_prefiltri_n}"]    = V(idro?.PrefiltriNumero),
            ["{vi_riscaldamento}"]  = V(idro?.Riscaldamento),
            ["{vi_temp_es}"]        = V(idro?.TemperaturaEsercizio),
            ["{vi_ricambio}"]       = V(idro?.RicambioAcqua),
            ["{vi_contatore_ded}"]  = V(idro?.ContatoreDedicato),
            ["{vi_ispezioni}"]      = V(idro?.Ispezioni),

            // SEZIONE VASCHE — CIRCOLAZIONE (pag. 38)
            ["{vp_pompe_riserva}"]  = Riserva(principale),
            ["{vp_boc_parete}"]     = V(principale?.BocchetteParete),
            ["{vp_boc_fondo}"]      = V(principale?.BocchetteFondo),
            ["{vp_vasca_compenso}"] = V(principale?.VascaCompenso),
            ["{vi_pompe_riserva}"]  = Riserva(idro),
            ["{vi_boc_parete}"]     = V(idro?.BocchetteParete),
            ["{vi_boc_fondo}"]      = V(idro?.BocchetteFondo),
            ["{vi_vasca_compenso}"] = V(idro?.VascaCompenso),
            // Premessa idromassaggio ≥ 4 m³ (la sezione < 4 m³ è già stata rimossa)
            ["{vi_premessa}"] = "La vasca, seppur accatastata come idromassaggio, presenta un volume > 4m3; " +
                                "pertanto accomunabile ad una vasca piscina ai sensi della UNI 10637 e come tale sarà nominata.",
        };
    }
}
