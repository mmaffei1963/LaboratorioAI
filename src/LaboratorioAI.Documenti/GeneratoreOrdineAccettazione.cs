using LaboratorioAI.Core.Entita;
using System.Globalization;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
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
    /// <param name="insediamenti">Insediamenti del cliente con indirizzo ed email, per il richiamo ai recapiti in fondo al documento.</param>
    byte[] GeneraWord(OrdineAccettazione o, string percorsoModello, IReadOnlyList<InsediamentoContatto> insediamenti);
    /// <summary>Ordine di Accettazione in PDF. <paramref name="percorsoWwwRoot"/> serve a caricare la carta intestata (cartella img).</summary>
    /// <param name="insediamenti">Insediamenti del cliente con indirizzo ed email, per il richiamo ai recapiti in fondo al documento.</param>
    byte[] GeneraPdf(OrdineAccettazione o, string? percorsoWwwRoot, IReadOnlyList<InsediamentoContatto> insediamenti);
    string NomeFile(OrdineAccettazione o, string estensione);
}

/// <inheritdoc />
public class GeneratoreOrdineAccettazione : IGeneratoreOrdineAccettazione
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>
    /// Frase di verifica ACCREDIA per le prove in subappalto, al singolare se il laboratorio esterno
    /// coinvolto nel documento è uno solo (simbolo "§"), al plurale se sono più d'uno (simboli "§1",
    /// "§2", ecc., necessari per distinguerli). Stessa frase usata in Preventivi/Offerte.
    /// </summary>
    private static string TestoVerificaAccredia(int numeroLaboratori) =>
        TestiOfferta.NotaSubappalto(numeroLaboratori);

    /// <summary>Simbolo del laboratorio esterno: "§" da solo se nel documento ce n'è uno solo, "§N" se ce ne sono più d'uno.</summary>
    private static string Simbolo(int numeroLaboratoriTotali, int numero) =>
        numeroLaboratoriTotali > 1 ? "§" + numero : "§";

    public byte[] GeneraWord(OrdineAccettazione o, string percorsoModello, IReadOnlyList<InsediamentoContatto> insediamenti) =>
        EPrivato(o)
            ? OrdinePrivatoDocx.GeneraWord(OrdinePrivatoDocx.DaLotto(o, o.Firma?.PerMarea), percorsoModello)
            : OrdineAccettazioneDocx.Genera(o, percorsoModello, insediamenti);

    /// <summary>
    /// 06/11/2026: i lotti del cliente «Spettabile cliente» (cod. contabilità MAF2) sono di clienti privati e escono con
    /// l'Ordine di Accettazione per i privati (Mod. Com. 05B, Condizioni 05A). Tutti gli altri clienti come prima.
    /// </summary>
    public static bool EPrivato(OrdineAccettazione o) =>
        string.Equals(o.ClienteCodContabilita?.Trim(), RegoleRichiestePrivati.CodiceClientePrivati, StringComparison.OrdinalIgnoreCase);

    public byte[] GeneraPdf(OrdineAccettazione o, string? percorsoWwwRoot, IReadOnlyList<InsediamentoContatto> insediamenti)
    {
        if (EPrivato(o) && percorsoWwwRoot is not null)
            return OrdinePrivatoDocx.GeneraPdf(OrdinePrivatoDocx.DaLotto(o, o.Firma?.PerMarea),
                Path.Combine(percorsoWwwRoot, OrdinePrivatoDocx.PercorsoModello), percorsoWwwRoot);
        FontResolverLabAI.Registra();
        var doc = Costruisci(o, percorsoWwwRoot, insediamenti);
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

    /// <summary>
    /// Spiegazione dei recapiti insediamenti, riportata prima della tabella: sono gli indirizzi email
    /// a cui arrivano le comunicazioni sui rapporti di prova generati dall'ordine.
    /// </summary>
    private const string TestoRecapitiInsediamenti =
        "Di seguito i recapiti degli insediamenti del Cliente risultanti nei nostri archivi: sono gli " +
        "indirizzi email a cui il Laboratorio può inviare avvisi di cortesia relativi al presente ordine e ai " +
        "rapporti di prova, che sono comunque pubblicati nel Portale clienti. Si invita il Cliente a verificarne " +
        "l'aggiornamento e la correttezza e a segnalarci eventuali variazioni prima dell'emissione dei rapporti di prova.";

    /// <summary>Prefisso del piè di pagina: "Mod. Acc. 01 Rev. 0 del … – Accettazione n. 8642 – ".</summary>
    internal static string PiedePagina(OrdineAccettazione o) =>
        (string.IsNullOrWhiteSpace(o.RiferimentoModulo) ? "" : o.RiferimentoModulo!.Trim() + " – ") +
        $"Accettazione n. {(string.IsNullOrWhiteSpace(o.CodLotto) ? "—" : o.CodLotto.Trim())} – ";

    // ── Costruzione documento ──────────────────────────────────────
    private static Document Costruisci(OrdineAccettazione o, string? percorsoWwwRoot, IReadOnlyList<InsediamentoContatto> insediamenti)
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

        // Piè di pagina: modulo, n. accettazione e numero di pagina su tutte le pagine (30/09/2026).
        var pag = sec.Footers.Primary.AddParagraph();
        pag.Format.Alignment = ParagraphAlignment.Center;
        pag.Format.Font.Size = 7;
        pag.AddText(PiedePagina(o) + "Pagina ");
        pag.AddPageField();
        pag.AddText(" di ");
        pag.AddNumPagesField();

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

        var rif = sec.AddParagraph($"Accettazione n. {Vuoto(o.CodLotto)}    •    Emesso il {DateTime.Today.ToString("dd/MM/yyyy", It)}");
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

        // Recapiti insediamenti: indirizzo email a cui arrivano le comunicazioni sui rapporti di
        // prova generati dall'ordine. Solo se il cliente è in archivio ArcLab e ha insediamenti.
        if (o.Firma is not null)
        {
            ConsegnaRapporto(sec, o.Firma);   // privati: e-mail e modalità di consegna al posto dei recapiti (14/10/2026)
        }
        else if (insediamenti.Count > 0)
        {
            Titolo(sec, "RECAPITI INSEDIAMENTI PER LE COMUNICAZIONI");
            var nota = sec.AddParagraph(TestoRecapitiInsediamenti);
            nota.Format.Font.Size = 8.5;
            nota.Format.SpaceAfter = "0.15cm";

            var ti = sec.AddTable();
            ti.Borders.Width = 0.5;
            ti.Borders.Color = new Color(0xFFBBBBBB);
            ti.AddColumn(Unit.FromCentimeter(4.5));
            ti.AddColumn(Unit.FromCentimeter(7.5));
            ti.AddColumn(Unit.FromCentimeter(5.0));
            var hi = ti.AddRow();
            hi.Format.Font.Bold = true;
            hi.Shading.Color = new Color(0xFFF3F3F3);
            hi.Cells[0].AddParagraph("Insediamento");
            hi.Cells[1].AddParagraph("Indirizzo");
            hi.Cells[2].AddParagraph("Email");
            foreach (var ins in insediamenti)
            {
                var row = ti.AddRow();
                row.Cells[0].AddParagraph(ins.Nome);
                row.Cells[0].Format.Font.Size = 8.5;
                row.Cells[1].AddParagraph(ins.Indirizzo ?? "—");
                row.Cells[1].Format.Font.Size = 8.5;
                CellaEmail(row.Cells[2], ins.Email);
            }
            var spazio = sec.AddParagraph();
            spazio.Format.SpaceAfter = "0.2cm";
        }

        // ── Riquadro condizioni (Allegato A Rev. 19) ───────────────
        RiquadroCondizioni(sec, o.RevisioneCondizioni, o.Firma);

        // ── Allegato tecnico ───────────────────────────────────────
        var att = sec.AddParagraph("ALLEGATO TECNICO");
        att.Format.Font.Bold = true;
        att.Format.Font.Size = 12;
        att.Format.SpaceBefore = "0.5cm";
        att.Format.SpaceAfter = "0.2cm";

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
            DoppiaRiga(sec);
            var h = sec.AddParagraph(c.Riferimento + " — " + (c.Denominazione ?? "").ToUpperInvariant());
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.35cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.Shading.Color = new Color(0xFFEEEEEE);

            // Dati identificativi
            var d = sec.AddTable();
            d.Borders.Width = 0;
            d.AddColumn(Unit.FromCentimeter(7.2));   // etichette: «Comunicazione anticipata dei fuori limite» su una riga (09/10/2026)
            d.AddColumn(Unit.FromCentimeter(9.8));
            Dato(d, "Riferimento", c.Riferimento);
            Dato(d, "Descrizione", c.Denominazione);
            Dato(d, "Matrice", c.Matrice);
            if (!string.IsNullOrWhiteSpace(c.Imballaggio)) Dato(d, "Imballaggio", c.Imballaggio);
            if (!string.IsNullOrWhiteSpace(c.PuntoPrelievo)) Dato(d, "Punto di prelievo", c.PuntoPrelievo);
            if (c.LuogoPrelievo is not null) Dato(d, "Luogo di prelievo", c.LuogoPrelievo);   // solo se compilato (15/10/2026)
            Dato(d, "Data accettazione", o.DataRicezione?.ToString("dd/MM/yyyy", It));
            if (o.DataPrelievo is not null) Dato(d, "Data prelievo", o.DataPrelievo?.ToString("dd/MM/yyyy", It));
            if (!string.IsNullOrWhiteSpace(c.TemperaturaRicevimento)) Dato(d, "T° di ricevimento", c.TemperaturaRicevimento);
            // Priorità: stampata solo se il Cliente ha chiesto l'urgenza ("Routine" non aggiunge nulla e
            // non va letto come impegno sui tempi). Prelievo e autocontrollo: Allegato A, Artt. 5.3 e 7.2.
            if (c.Urgente) Dato(d, "Priorità", "Urgente (su richiesta del Cliente)");
            if (!string.IsNullOrWhiteSpace(c.Prelevatore)) Dato(d, "Prelievo eseguito da", c.Prelevatore);
            if (c.AutocontrolloTesto is not null) Dato(d, "Autocontrollo", c.AutocontrolloTesto);
            if (c.ComunicazioneAnticipataTesto is not null) Dato(d, "Comunicazione anticipata dei fuori limite", c.ComunicazioneAnticipataTesto);
            if (o.GiudizioConformitaTesto is not null) Dato(d, "Giudizio di conformità e pareri", o.GiudizioConformitaTesto);
            var ctrl = c.Controllo is null ? null
                : c.Controllo + (c.IdControllo is int idc ? $" (ID controllo {idc})" : "");
            if (ctrl is not null) Dato(d, "Controllo", ctrl);   // campi vuoti non stampati (15/10/2026)

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
                ta.AddColumn(Unit.FromCentimeter(8.2));   // denominazione (09/10/2026: più larga)
                ta.AddColumn(Unit.FromCentimeter(7.3));
                ta.AddColumn(Unit.FromCentimeter(1.5));   // Accr.: solo Sì / — / §
                var har = ta.AddRow();
                har.Format.Font.Bold = true;
                har.Shading.Color = new Color(0xFFF7F7F7);
                har.Cells[0].AddParagraph("Denominazione");
                har.Cells[1].AddParagraph("Metodo");
                har.Cells[2].AddParagraph("Accr.");
                foreach (var (an, numero) in c.AnalisiRaggruppate())
                {
                    var r = ta.AddRow();
                    r.Cells[0].AddParagraph(CampioneAccettazione.EtichettaRaggruppata(an, numero));
                    r.Cells[1].AddParagraph(an.Metodo ?? "—");
                    // Come nelle offerte/preventivi: in subappalto il simbolo del laboratorio esterno
                    // (§, §1, §2...) sostituisce l'indicazione di accreditamento nostro.
                    r.Cells[2].AddParagraph(!string.IsNullOrWhiteSpace(an.LaboratorioEsterno)
                        ? Simbolo(numeroLab.Count, numeroLab[an.LaboratorioEsterno!.Trim()])
                        : (an.Accreditato ? "Sì" : "—"));
                }
            }
            else
            {
                Piccolo(sec, "Nessuna prova associata.");
            }
        }

        // Frasi dei laboratori esterni (subappalto): una sola volta per laboratorio, con il simbolo
        // corrispondente a quello riportato nell'allegato tecnico (semplice "§" se il laboratorio è uno solo).
        for (var i = 0; i < fraseLab.Count; i++)
            Piccolo(sec, $"{Simbolo(numeroLab.Count, i + 1)} {fraseLab[i]}");

        // Avviso ACCREDIA: una sola volta per documento (non ripetuto per ogni campione/laboratorio),
        // evidenziato perché vale per tutte le prove in subappalto marcate con §.
        if (numeroLab.Count > 0)
        {
            var avviso = sec.AddParagraph(TestoVerificaAccredia(numeroLab.Count));
            avviso.Format.Font.Bold = true;
            avviso.Format.Shading.Color = new Color(0xFFFFF3D6);
            avviso.Format.SpaceBefore = "0.3cm";
            avviso.Format.SpaceAfter = "0.3cm";
        }

        if (o.Firma is not null) RiquadroFirme(sec, o.Firma);

        // ── Pagina finale: dati aziendali ──────────────────────────
        PaginaDatiAziendali(sec, o);

        return doc;
    }

    // ── Versione da firmare per i privati (14/10/2026) ─────────────
    private static void ConsegnaRapporto(Section sec, FirmaPrivato f)
    {
        Titolo(sec, TestiCondizioniAccettazione.TitoloConsegnaRapporto);
        string Casella(bool x) => x ? "[X]" : "[  ]";
        var p = sec.AddParagraph();
        p.Format.SpaceAfter = "0.15cm";
        p.AddFormattedText("Modalità: ", TextFormat.Bold);
        p.AddText($"{Casella(f.Consegna is ConsegnaRapportoPrivato.Email or ConsegnaRapportoPrivato.Entrambi)} per e-mail     " +
                  $"{Casella(f.Consegna is ConsegnaRapportoPrivato.AMano or ConsegnaRapportoPrivato.Entrambi)} ritiro a mano presso il Laboratorio");
        var e = sec.AddParagraph();
        e.Format.SpaceAfter = "0.2cm";
        e.AddFormattedText("E-mail per l'invio del Rapporto di Prova: ", TextFormat.Bold);
        e.AddText(string.IsNullOrWhiteSpace(f.EmailRapporto) ? "_______________________________________________" : f.EmailRapporto!.Trim());
    }

    private static void RiquadroFirme(Section sec, FirmaPrivato f)
    {
        var tit = sec.AddParagraph("SOTTOSCRIZIONE");
        tit.Format.Font.Bold = true;
        tit.Format.SpaceBefore = "0.5cm";
        tit.Format.SpaceAfter = "0.1cm";
        tit.Format.KeepWithNext = true;
        var d = sec.AddParagraph(f.TestoFirma);
        d.Format.Font.Size = 8.5;
        d.Format.SpaceAfter = "0.2cm";
        d.Format.KeepWithNext = true;

        var t = sec.AddTable();
        t.Borders.Width = 0.75;
        t.Borders.Color = new Color(0xFF999999);
        t.AddColumn(Unit.FromCentimeter(8.5));
        t.AddColumn(Unit.FromCentimeter(8.5));
        var r = t.AddRow();
        r.TopPadding = Unit.FromCentimeter(0.15);
        r.BottomPadding = Unit.FromCentimeter(0.15);
        void Cella(Cell c, string titolo, (string Etichetta, string? Valore)[] righe)
        {
            var h = c.AddParagraph(titolo);
            h.Format.Font.Bold = true;
            h.Format.SpaceAfter = "0.25cm";
            foreach (var (et, val) in righe)
            {
                var x = c.AddParagraph();
                x.Format.SpaceAfter = "0.45cm";
                x.AddText(et + ": ");
                x.AddText(string.IsNullOrWhiteSpace(val) ? "______________________________" : val!);
            }
        }
        Cella(r.Cells[0], "Il Cliente", new (string, string?)[] { ("Nome e cognome", null), ("Firma", null), ("Data e ora", null) });
        Cella(r.Cells[1], "Per MARE.A s.r.l.", new (string, string?)[] { ("Nome e cognome", f.PerMarea), ("Firma", null), ("Data", null) });
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

    // Doppia riga nera (due filetti paralleli) disegnata come tabella di una riga bassa con bordo sopra
    // e sotto. NON usare un paragrafo vuoto con bordi: in PDFsharp/MigraDoc 6.1 se capita in fondo alla
    // pagina l'impaginazione va in errore ("'Rectangle' must not be null here", 01/10/2026).
    private static void DoppiaRiga(Section sec)
    {
        var spazio = sec.AddParagraph();               // spazio prima, senza bordi
        spazio.Format.Font.Size = 1;
        spazio.Format.SpaceBefore = "0.3cm";

        var t = sec.AddTable();
        t.Borders.Visible = false;
        t.TopPadding = 0;
        t.BottomPadding = 0;
        t.AddColumn(Unit.FromCentimeter(17));
        var r = t.AddRow();
        r.HeightRule = RowHeightRule.Exactly;
        r.Height = Unit.FromPoint(3);
        r.Format.Font.Size = 1;
        var c = r.Cells[0];
        c.Borders.Top.Visible = true;
        c.Borders.Top.Width = 1.0;
        c.Borders.Top.Color = Colors.Black;
        c.Borders.Bottom.Visible = true;
        c.Borders.Bottom.Width = 1.0;
        c.Borders.Bottom.Color = Colors.Black;
    }

    private static string? Immagine(string? percorsoWwwRoot, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoWwwRoot)) return null;
        var full = Path.Combine(percorsoWwwRoot, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }

    // ── Riquadro condizioni ────────────────────────────────────────
    private static void RiquadroCondizioni(Section sec, string revisione, FirmaPrivato? firma = null)
    {
        var box = sec.AddTable();
        box.Borders.Width = 0.75;
        box.Borders.Color = new Color(0xFF999999);
        box.Format.SpaceBefore = "0.4cm";
        box.AddColumn(Unit.FromCentimeter(17));

        // Una riga per blocco: MigraDoc non spezza mai una riga di tabella tra due pagine, e un'unica
        // riga con tutto il testo (Rev. 19) diventava più alta di una pagina → errore di impaginazione
        // ("'Rectangle' must not be null"). Con più righe il riquadro può continuare sulla pagina dopo.
        // I bordi interni sono nascosti, così il riquadro resta visivamente unico.
        var blocchi = new (string Titolo, string? Corpo, bool Grande)[]
        {
            (TestiCondizioniAccettazione.TitoloRiquadro, null, true),
            (TestiCondizioniAccettazione.TitoloVerifica, firma?.TestoVerifica ?? TestiCondizioniAccettazione.Verifica, false),
            (TestiCondizioniAccettazione.TitoloPrelievo, TestiCondizioniAccettazione.Prelievo, false),
            (TestiCondizioniAccettazione.TitoloCondizioni,
                firma is not null ? firma.TestoCondizioni.Replace("[revisione]", revisione) : TestiCondizioniAccettazione.Condizioni(revisione), false),
        };
        for (var i = 0; i < blocchi.Length; i++)
        {
            var cella = box.AddRow().Cells[0];
            cella.Shading.Color = new Color(0xFFF7F7F7);
            if (i > 0) cella.Borders.Top.Visible = false;
            if (i < blocchi.Length - 1) cella.Borders.Bottom.Visible = false;
            BloccoBox(cella, blocchi[i].Titolo, blocchi[i].Corpo, titoloGrande: blocchi[i].Grande);
        }
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

    /// <summary>
    /// Scrive un'email in cella andando a capo manualmente quando è lunga: un indirizzo email non ha
    /// spazi, quindi MigraDoc non lo spezzerebbe da solo e sforerebbe la larghezza della colonna.
    /// L'interruzione si inserisce dopo ogni carattere "di aggancio" naturale (@ . _ -) quando il
    /// segmento accumulato supera una soglia, così l'indirizzo va a capo dentro la cella.
    /// </summary>
    private static void CellaEmail(Cell cella, string? email)
    {
        var p = cella.AddParagraph();
        p.Format.Font.Size = 8.5;
        var testo = string.IsNullOrWhiteSpace(email) ? null : email!.Trim();
        if (testo is null) { p.AddText("—"); return; }

        const int sogliaSegmento = 16;
        var ultimo = 0;
        for (var i = 0; i < testo.Length; i++)
        {
            var spezzabile = testo[i] is '@' or '.' or '_' or '-';
            if (spezzabile && i - ultimo >= sogliaSegmento)
            {
                p.AddText(testo.Substring(ultimo, i - ultimo + 1));
                p.AddLineBreak();
                ultimo = i + 1;
            }
        }
        if (ultimo < testo.Length) p.AddText(testo.Substring(ultimo));
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
