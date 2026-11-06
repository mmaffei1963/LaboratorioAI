using System.Globalization;
using LaboratorioAI.Core.Entita;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Offerta commerciale (preventivo) generata con MigraDoc: un'unica definizione del documento
/// produce sia il PDF sia il file Word (RTF, apribile e modificabile in Word). L'offerta richiama
/// sempre le Condizioni Generali di Fornitura MARE.A e, per le prove in subappalto, riporta la
/// frase del laboratorio esterno.
/// </summary>
public interface IGeneratoreOffertaPreventivo
{
    /// <summary>Offerta Word (.docx) scritta sul modello di carta intestata indicato.</summary>
    byte[] GeneraWord(Preventivo p, string percorsoModello);

    /// <summary>Offerta PDF con logo e dati societari MARE.A in testata e piè di pagina.</summary>
    /// <param name="cartellaImmagini">Cartella contenente logo_header.png e logo_footer.png (carta intestata).</param>
    byte[] GeneraPdf(Preventivo p, string cartellaImmagini);
    string NomeFile(Preventivo p, string estensione);
}

/// <inheritdoc />
public class GeneratoreOffertaPreventivo : IGeneratoreOffertaPreventivo
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>Larghezza del logo di testata/piè pagina: coincide con la larghezza utile della pagina (21 - 2×2 cm).</summary>
    private static readonly Unit LarghezzaLoghi = Unit.FromCentimeter(17);

    /// <summary>
    /// Frase di verifica ACCREDIA per le prove in subappalto, al singolare se il laboratorio esterno
    /// coinvolto nell'offerta è uno solo (simbolo "§"), al plurale se sono più d'uno (simboli "§1",
    /// "§2", ecc., necessari per distinguerli).
    /// </summary>
    private static string TestoVerificaAccredia(int numeroLaboratori) =>
        TestiOfferta.NotaSubappalto(numeroLaboratori);

    /// <summary>Simbolo del laboratorio esterno: "§" da solo se in offerta ce n'è uno solo, "§N" se ce ne sono più d'uno.</summary>
    private static string Simbolo(int numeroLaboratoriTotali, int numero) =>
        numeroLaboratoriTotali > 1 ? "§" + numero : "§";

    // Testi fissi dell'offerta (presentazione, condizioni, firme): vedi TestiOfferta.cs (30/09/2026).

    public byte[] GeneraWord(Preventivo p, string percorsoModello) =>
        OffertaDocx.Genera(p, percorsoModello);

    public byte[] GeneraPdf(Preventivo p, string cartellaImmagini)
    {
        FontResolverLabAI.Registra();
        var doc = Costruisci(p, cartellaImmagini);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public string NomeFile(Preventivo p, string estensione)
    {
        var num = p.Numero is int n ? $"{p.Anno}_{n:000}" : "bozza";
        var cli = new string((p.ClienteRagioneSociale ?? "Cliente").Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
        return $"Offerta_{num}_{cli}.{estensione}";
    }

    // ── Costruzione documento ──────────────────────────────────────
    private static Document Costruisci(Preventivo p, string cartellaImmagini)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Montserrat";
        normale.Font.Size = 10;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        // Margini superiore/inferiore più ampi per lasciare spazio a logo (testata) e dati societari (piè pagina).
        sec.PageSetup.TopMargin = Unit.FromCentimeter(3.4);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(2.6);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        sec.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
        sec.PageSetup.FooterDistance = Unit.FromCentimeter(0.6);

        // Testata: logo MARE.A (stessa immagine usata nella carta intestata Word)
        var percorsoLogoHeader = Path.Combine(cartellaImmagini, "logo_header.png");
        if (File.Exists(percorsoLogoHeader))
        {
            var imgHeader = sec.Headers.Primary.AddImage(percorsoLogoHeader);
            imgHeader.Width = LarghezzaLoghi;
            imgHeader.LockAspectRatio = true;
        }

        // Piè di pagina: dati societari MARE.A (stessa immagine usata nella carta intestata Word)
        var percorsoLogoFooter = Path.Combine(cartellaImmagini, "logo_footer.png");
        if (File.Exists(percorsoLogoFooter))
        {
            var imgFooter = sec.Footers.Primary.AddImage(percorsoLogoFooter);
            imgFooter.Width = LarghezzaLoghi;
            imgFooter.LockAspectRatio = true;
        }

        // Numerazione di pagina (29/09/2026), sotto ai dati societari del piè di pagina.
        var paginazione = sec.Footers.Primary.AddParagraph();
        paginazione.Format.Alignment = ParagraphAlignment.Center;
        paginazione.Format.Font.Size = 7;
        paginazione.AddText($"Offerta N. {TestiOfferta.NumeroOfferta(p.Anno, p.Numero)} – Pagina ");
        paginazione.AddPageField();
        paginazione.AddText(" di ");
        paginazione.AddNumPagesField();

        Piccolo(sec, "Iscritto nell'elenco regionale Emilia Romagna al n. 008/RN/003");

        var num = p.Numero is int n ? $"{p.Anno}/{n:000}" : "—";
        var tit = sec.AddParagraph($"OFFERTA N. {num}");
        tit.Format.Alignment = ParagraphAlignment.Center;
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 14;
        tit.Format.SpaceBefore = "0.5cm";
        tit.Format.SpaceAfter = "0.3cm";

        // Testata (cliente + dati offerta)
        var testata = TabellaInfo(sec);
        RigaInfo(testata, "Data offerta", (p.DataOfferta ?? DateTime.Today).ToString("dd/MM/yyyy", It), "Durata", TestiOfferta.DurataBreve(p.ValiditaGiorni, !p.SenzaRinnovoTacito));
        if (TestiOfferta.ClienteGenerico(p.ClienteRagioneSociale))
        {
            // 21/10/2026: «Spett.le cliente»: tutti i dati, anche quelli vuoti, da compilare a mano.
            var dc = TestiOfferta.DaCompilare;
            RigaInfo(testata, "Cliente", string.IsNullOrWhiteSpace(p.ClienteRagioneSociale) ? "Spett.le Cliente" : p.ClienteRagioneSociale!.Trim(), "Nome cliente", dc(p.ClienteNome));
            RigaInfo(testata, "Partita IVA / C.F.", dc(p.ClientePartitaIva), "Telefono", dc(p.ClienteTelefono));
            RigaInfo(testata, "Indirizzo", dc(p.ClienteIndirizzo), "CAP", dc(p.ClienteCap));
            RigaInfo(testata, "Città (Prov.)", dc(string.Join(" ", new[] { p.ClienteCitta, p.ClienteProvincia }.Where(s => !string.IsNullOrWhiteSpace(s)))), "E-mail / PEC", dc(null));
            RigaInfo(testata, "Oggetto", string.IsNullOrWhiteSpace(p.Oggetto) ? "—" : p.Oggetto!, "", "");
            // 21/10/2026: due caselle; «Pagato» con la X se segnato in maschera, altrimenti entrambe vuote da spuntare a mano.
            CasellePagamento(sec, p.GiaPagato);
        }
        else
        {
            RigaInfo(testata, "Cliente", p.ClienteRagioneSociale ?? "—", "Partita IVA", string.IsNullOrWhiteSpace(p.ClientePartitaIva) ? "—" : p.ClientePartitaIva!);
            var indirizzo = string.Join(" ", new[] { p.ClienteIndirizzo, p.ClienteCap, p.ClienteCitta, p.ClienteProvincia }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
            RigaInfo(testata, "Indirizzo", string.IsNullOrWhiteSpace(indirizzo) ? "—" : indirizzo, "Oggetto", string.IsNullOrWhiteSpace(p.Oggetto) ? "—" : p.Oggetto!);
            if (!string.IsNullOrWhiteSpace(p.ClienteTelefono)) RigaInfo(testata, "Telefono", p.ClienteTelefono!.Trim(), "", "");
        }

        // Copertina: solo i dati identificativi sopra. L'allegato tecnico (analisi, prezzi,
        // condizioni economiche) parte sempre su una pagina nuova.
        sec.AddPageBreak();

        var righe = p.Righe.OrderBy(r => r.Progressivo).ToList();
        var controlli = righe.Where(r => r.Tipo is TipoRigaPreventivo.ControlloEsistente or TipoRigaPreventivo.PropostaControllo
            or TipoRigaPreventivo.ControlloLibero or TipoRigaPreventivo.GruppoControlli).ToList();
        var accessori = righe.Where(r => r.Tipo == TipoRigaPreventivo.Accessorio).ToList();

        // Blocchi analisi
        var haSubappalto = controlli.Any(r => r.Metodi.Any(m => m.IdLabEsternoArcLab != null) || r.Tipo == TipoRigaPreventivo.ControlloLibero);

        // Numerazione dei laboratori esterni distinti (§1, §2, ...): un controllo raggruppato può
        // riunire metodi in subappalto presso laboratori diversi, quindi il simbolo § da solo non
        // basta più a distinguerli. La numerazione è unica per l'intera offerta (non per riga), nel
        // primo ordine in cui i laboratori compaiono, e le frasi corrispondenti si riportano una sola
        // volta ciascuna, dopo tutti i blocchi di analisi.
        var numeroLab = new Dictionary<int, int>();
        var fraseLab = new List<string>();
        foreach (var r in controlli)
            foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
            {
                if (m.IdLabEsternoArcLab is not int idLabNum || numeroLab.ContainsKey(idLabNum)) continue;
                numeroLab[idLabNum] = numeroLab.Count + 1;
                fraseLab.Add(TestiOfferta.FraseSubappalto(m.LabEsternoFrase, m.LabEsterno));
            }

        foreach (var r in controlli)
        {
            var idPart = !string.IsNullOrWhiteSpace(r.IdControlliOrigine) ? $" (ID {r.IdControlliOrigine})"
                : r.IdControlloArcLab is int idc ? $" (ID {idc})"
                : !string.IsNullOrWhiteSpace(r.RifLocale) ? $" (ID {r.RifLocale})" : "";
            var h = sec.AddParagraph("ANALISI DI LABORATORIO — " + (r.Descrizione ?? "").ToUpperInvariant() + idPart);
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.4cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.Shading.Color = new Color(0xFFEEEEEE);
            if (p.MostraInsediamenti && !string.IsNullOrWhiteSpace(r.Insediamento))   // 05/11/2026; solo se spuntato (06/11/2026)
            {
                var ins = sec.AddParagraph("Insediamento: " + r.Insediamento);
                ins.Format.Font.Italic = true; ins.Format.Font.Size = 8.5; ins.Format.SpaceAfter = "0.1cm";
            }

            if (r.Metodi.Count > 0)
            {
                // Colonna "Parametro" rimossa su richiesta (29/09/2026): lo spazio liberato va quasi
                // tutto alla colonna "Metodo" (riferimento normativo, spesso un testo lungo — anche
                // con un carattere più piccolo delle altre colonne), che in precedenza era la più
                // compressa delle quattro. "Accr." resta la più stretta possibile.
                var t = sec.AddTable();
                t.Borders.Width = 0.5;
                t.Borders.Color = new Color(0xFFBBBBBB);
                // 20/10/2026: colonna «U.M.» (unità di misura del metodo).
                t.AddColumn(Unit.FromCentimeter(4.2));
                t.AddColumn(Unit.FromCentimeter(9.7));
                t.AddColumn(Unit.FromCentimeter(2.0));
                t.AddColumn(Unit.FromCentimeter(1.1));
                var hr = t.AddRow();
                hr.Format.Font.Bold = true;
                hr.Shading.Color = new Color(0xFFF3F3F3);
                hr.Cells[0].AddParagraph("Denominazione");
                hr.Cells[1].AddParagraph("Metodo");
                hr.Cells[2].AddParagraph("U.M.");
                hr.Cells[3].AddParagraph("Accr.");
                foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
                {
                    var row = t.AddRow();
                    row.Cells[0].AddParagraph(m.Denominazione ?? m.Descrizione ?? m.DenominazioneRiga ?? "—");
                    var celMetodo = row.Cells[1].AddParagraph(m.RiferimentoNormativo ?? "—");
                    celMetodo.Format.Font.Size = 8; // più piccolo del default (9pt): la colonna è spesso lunga.
                    var celUm = row.Cells[2].AddParagraph(string.IsNullOrWhiteSpace(m.UnitaMisura) ? "—" : m.UnitaMisura.Trim());
                    celUm.Format.Font.Size = 8;
                    row.Cells[3].AddParagraph(m.IdLabEsternoArcLab is int idLabRiga ? Simbolo(numeroLab.Count, numeroLab[idLabRiga]) : (m.Accreditato ? "Sì" : "—"));
                }
            }

            // Prezzo (12/10/2026): solo IVA esclusa; con lo sconto prezzo pieno, percentuale e prezzo scontato.
            RighePrezzo(sec, TestiOfferta.RighePrezzo(r.PrezzoBase, r.ScontoPercentuale, r.Quantita, r.PrezzoFinale, accessorio: false));
        }

        // Frasi dei laboratori esterni (subappalto): una sola volta per laboratorio, con il simbolo
        // corrispondente a quello riportato in tabella (semplice "§" se il laboratorio è uno solo).
        for (var i = 0; i < fraseLab.Count; i++)
            Piccolo(sec, $"{Simbolo(numeroLab.Count, i + 1)} {fraseLab[i]}");

        // Avviso ACCREDIA: una sola volta per offerta (non ripetuto per ogni blocco/laboratorio),
        // evidenziato perché vale per tutte le prove in subappalto marcate con §.
        if (haSubappalto)
        {
            var avviso = sec.AddParagraph(TestoVerificaAccredia(numeroLab.Count));
            avviso.Format.Font.Bold = true;
            avviso.Format.Shading.Color = new Color(0xFFFFF3D6);
            avviso.Format.SpaceBefore = "0.3cm";
            avviso.Format.SpaceAfter = "0.3cm";
        }

        // Servizi accessori (12/10/2026): titolo e testo descrittivo su tutta la larghezza, prezzo sotto a destra.
        if (accessori.Count > 0)
        {
            var h = sec.AddParagraph("SERVIZI ACCESSORI");
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.4cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.Shading.Color = new Color(0xFFEEEEEE);

            foreach (var a in accessori)
            {
                var t = sec.AddTable();
                t.Borders.Width = 0.5;
                t.Borders.Color = new Color(0xFFBBBBBB);
                t.AddColumn(Unit.FromCentimeter(17));
                var titolo = t.AddRow();
                titolo.Format.Font.Bold = true;
                titolo.Shading.Color = new Color(0xFFF3F3F3);
                titolo.Cells[0].AddParagraph(a.Descrizione ?? "—");
                var paragrafi = TestiOfferta.Paragrafi(a.Note);
                if (paragrafi.Count > 0)
                {
                    var corpo = t.AddRow();
                    for (var k = 0; k < paragrafi.Count; k++)
                    {
                        var par = corpo.Cells[0].AddParagraph(paragrafi[k]);
                        if (k < paragrafi.Count - 1) par.Format.SpaceAfter = "0.25cm";
                    }
                }
                RighePrezzo(sec, TestiOfferta.RighePrezzo(a.PrezzoBase, a.ScontoPercentuale, a.Quantita, a.PrezzoFinale, accessorio: true));
            }
        }

        // Condizioni economiche
        var iva = Math.Round(p.TotaleImponibile * p.AliquotaIva / 100m, 2, MidpointRounding.AwayFromZero);
        Titolo(sec, "CONDIZIONI ECONOMICHE");
        var te = sec.AddTable();
        te.Borders.Width = 0;
        te.AddColumn(Unit.FromCentimeter(13));
        te.AddColumn(Unit.FromCentimeter(4));
        RigaImporto(te, "Imponibile", p.TotaleImponibile, false);
        RigaImporto(te, $"IVA {Fmt(p.AliquotaIva)}%", iva, false);
        RigaImporto(te, "TOTALE", p.TotaleImponibile + iva, true);

        CondizioniPagamento(sec, p.CondizioniPagamento);
        Nota(sec, TestiOfferta.Validita(p.ValiditaGiorni, !p.SenzaRinnovoTacito));
        if (!string.IsNullOrWhiteSpace(p.Note))
            Nota(sec, p.Note!.Trim());

        // Richiamo alle Condizioni Generali di Fornitura
        var rev = string.IsNullOrWhiteSpace(p.RevisioneCondizioni) ? "Mod. Com. 05" : p.RevisioneCondizioni!.Trim();
        Titolo(sec, TestiOfferta.TitoloCondizioni);
        Nota(sec, TestiOfferta.Condizioni(rev));
        Nota(sec, TestiOfferta.Accettazione, spazioPrima: true, grassetto: true);

        // Riepilogo promozionale "Il nostro lavoro" (opzionale, a scelta del commerciale): in pagine a
        // parte, oltre la fine delle condizioni, per non mescolarlo al contenuto contrattuale/operativo.
        if (p.RiepilogoPromozionale)
        {
            sec.AddPageBreak();
            AggiungiRiepilogoPromozionale(sec);
        }

        // Informativa privacy (fissa, uguale per ogni offerta)
        AggiungiInformativaPrivacy(sec);

        // Firma facoltativa (09/10/2026): l'accettazione dal Portale non richiede firme.
        Nota(sec, TestiOfferta.NotaFirme, spazioPrima: true);
        // Firma (tabella a due colonne: allineamento indipendente dal carattere usato)
        var tf = sec.AddTable();
        tf.Borders.Width = 0;
        tf.Format.SpaceBefore = "1cm";
        tf.AddColumn(Unit.FromCentimeter(8.5));
        tf.AddColumn(Unit.FromCentimeter(8.5));

        var rigaEtichette = tf.AddRow();
        rigaEtichette.Cells[0].AddParagraph(TestiOfferta.FirmaAccettazione);
        rigaEtichette.Cells[1].AddParagraph("MARE.A s.r.l.");

        var rigaLinee = tf.AddRow();
        rigaLinee.TopPadding = "0.8cm";
        rigaLinee.Cells[0].AddParagraph("______________________________");
        rigaLinee.Cells[1].AddParagraph("______________________________");

        // Approvazione specifica delle clausole vessatorie (artt. 1341-1342 c.c.): firma SEPARATA,
        // una firma sola "per accettazione" non basta. Solo con Condizioni ad articoli numerati (Rev. 19+).
        if (TestiOfferta.HaArticoliNumerati(rev))
        {
            Nota(sec, TestiOfferta.TitoloApprovazioneSpecifica, spazioPrima: true, grassetto: true);
            Nota(sec, TestiOfferta.ApprovazioneSpecifica);
            var ta = sec.AddTable();
            ta.Borders.Width = 0;
            ta.Format.SpaceBefore = "0.4cm";
            ta.AddColumn(Unit.FromCentimeter(8.5));
            ta.AddColumn(Unit.FromCentimeter(8.5));
            ta.AddRow().Cells[0].AddParagraph(TestiOfferta.FirmaApprovazioneSpecifica);
            var rl = ta.AddRow();
            rl.TopPadding = "0.8cm";
            rl.Cells[0].AddParagraph("______________________________");
        }

        return doc;
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Piccolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
    }

    /// <summary>«Pagamento: [ ] Pagato  [ ] Non pagato» con caselle disegnate come celle bordate (il font non ha ☐).</summary>
    private static void CasellePagamento(Section sec, bool pagato)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.Format.SpaceAfter = "0.2cm";
        foreach (var w in new[] { 3.0, 0.42, 2.6, 0.42, 3.0 }) t.AddColumn(Unit.FromCentimeter(w));
        var row = t.AddRow();
        row.Height = "0.42cm";
        row.HeightRule = RowHeightRule.Exactly;
        row.VerticalAlignment = VerticalAlignment.Center;
        var l = row.Cells[0].AddParagraph("Pagamento");
        l.Format.Font.Bold = true;
        foreach (var (i, testo, x) in new[] { (1, "Pagato", pagato), (3, "Non pagato", false) })
        {
            var c = row.Cells[i];
            c.Borders.Width = 0.75;
            c.Borders.Color = Colors.Black;
            var px = c.AddParagraph(x ? "X" : "");
            px.Format.Alignment = ParagraphAlignment.Center;
            px.Format.Font.Bold = true;
            var pt = row.Cells[i + 1].AddParagraph(" " + testo);
            pt.Format.LeftIndent = "0.1cm";
        }
    }

    private static Table TabellaInfo(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.Format.SpaceAfter = "0.2cm";
        t.AddColumn(Unit.FromCentimeter(3.0));
        t.AddColumn(Unit.FromCentimeter(5.5));
        t.AddColumn(Unit.FromCentimeter(3.0));
        t.AddColumn(Unit.FromCentimeter(5.5));
        return t;
    }

    private static void RigaInfo(Table t, string l1, string v1, string l2, string v2)
    {
        var row = t.AddRow();
        row.Cells[0].AddParagraph(l1).Format.Font.Bold = true;
        row.Cells[1].AddParagraph(v1);
        row.Cells[2].AddParagraph(l2).Format.Font.Bold = true;
        row.Cells[3].AddParagraph(v2);
    }

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = "0.4cm";
        p.Format.SpaceAfter = "0.1cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
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

    // ── Informativa privacy (art. 13 Reg. UE 2016/679) — testo fisso MARE.A ──
    private static void Sotto(Section sec, string t)
    {
        var p = sec.AddParagraph(t);
        p.Format.Font.Bold = true;
        p.Format.Font.Size = 8.5;
        p.Format.SpaceBefore = "0.25cm";
    }

    private static void AggiungiInformativaPrivacy(Section sec)
    {
        var tit = sec.AddParagraph("INFORMATIVA PRIVACY");
        tit.Format.SpaceBefore = "0.6cm";
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 12;
        tit.Format.SpaceAfter = "0.1cm";
        var sub = sec.AddParagraph("Ai sensi dell'art. 13 Regolamento UE 2016/679");
        sub.Format.Font.Size = 8;
        sub.Format.Font.Italic = true;
        sub.Format.SpaceAfter = "0.2cm";

        Sotto(sec, "Identità e dati di contatto del titolare del trattamento");
        Nota(sec, "Il titolare del trattamento è la ditta MARE.A s.r.l. con sede legale in Via E. Toti, 2 - 47841, Cattolica (RN) tel. 0541 830442, fax 0541 830460 e-mail: info@mare-a.com – CF/P.IVA 03670240401.");

        Sotto(sec, "Finalità e base giuridica del trattamento");
        Nota(sec, "I suoi dati personali sono trattati nel rispetto dei principi di correttezza, liceità e trasparenza, per le seguenti finalità:");
        Nota(sec, "1) Fornitura del servizio ed adempimenti amministrativi e fiscali connessi al rapporto contrattuale;");
        Nota(sec, "2) Servizio di assistenza post-vendita, ivi compresi eventuali contatti a mezzo posta cartacea e/o telefonici e/o tramite e-mail e/o sms per comunicazioni inerenti lo svolgimento dei servizi da lei affidatici;");
        Nota(sec, "3) Newsletter: previo ottenimento del suo consenso, il Titolare e gli incaricati al trattamento potranno inoltre inviarle, a mezzo posta cartacea, via e-mail, sms o App di messaggistica, occasionali aggiornamenti circa nuovi servizi obbligatori e/o funzionali alla sua attività;");
        Nota(sec, "4) Profilazione: previo ottenimento del suo consenso, il Titolare e gli incaricati al trattamento potranno categorizzare la sua attività (es. Hotel, Ristorante, Ingrosso ecc.) al fine di poter selezionare più accuratamente le comunicazioni a lei destinate.");

        Sotto(sec, "Destinatari dei dati personali");
        Nota(sec, "Nell'ambito dei trattamenti di cui alla presente informativa, i suoi dati, anche appartenenti alle categorie particolari di cui all'art. 9 del Regolamento UE 2016/679, saranno trattati da nostri incaricati interni e potranno essere comunicati anche a soggetti terzi, formalmente nominati responsabili del trattamento.");
        Nota(sec, "Nell'ambito del trattamento di cui al punto 1), in particolare, i suoi dati saranno comunicati ai consulenti amministrativi e fiscali del Titolare ed alle Autorità pubbliche competenti in materia.");

        Sotto(sec, "Obbligo di fornirci i suoi dati");
        Nota(sec, "La fornitura dei suoi dati personali per le finalità 1) e 2), anche appartenenti alle categorie particolari di cui all'art. 9 del Regolamento UE 2016/679, è necessaria ai fini dell'esecuzione del servizio contrattuale e conseguentemente la loro mancata o incompleta fornitura renderà impossibile assicurare tale servizio.");
        Nota(sec, "Il conferimento dei dati per le finalità di cui ai punti 3) e 4), invece, non è necessario ed il soggetto interessato ha il diritto di opporsi in qualsiasi momento a tale trattamento, anche in parte, inviando la richiesta all'indirizzo del Titolare del trattamento indicato sopra. Per quanto riguarda le finalità di cui al punto 4), infine, il conferimento dei suoi dati non è obbligatorio, ma il mancato consenso alla profilazione ci impedirà di inviarle comunicazioni personalizzate.");

        Sotto(sec, "Periodo di conservazione dei dati");
        Nota(sec, "I suoi dati personali saranno conservati per tutto il tempo imposto in base a disposizioni normative inerenti i servizi da lei affidatici e la disciplina amministrativa e fiscale.");
        Nota(sec, "Per finalità informative e per l'invio di aggiornamenti normativi, news relative ad iniziative commerciali, nonché per comunicare iniziative ed altri eventi, saranno conservati per un periodo di 24 mesi, fatta salva l'ipotesi di ulteriori vendite e/o sue richieste che dovessero avvenire in tale arco di tempo e/o la necessità di conservare i dati per le altre finalità di cui alla presente informativa.");

        Sotto(sec, "Diritti dell'interessato");
        Nota(sec, "Lei ha diritto di revocare il consenso prestato in calce alla presente informativa in qualsiasi momento tramite comunicazione all'indirizzo del Titolare del trattamento. Allo stesso modo lei potrà chiedere l'accesso ai suoi dati personali, la rettifica, la cancellazione, la limitazione o l'opposizione del trattamento oltre che la portabilità dei dati (art. 20 del Regolamento UE 2016/679). La richiesta di cancellazione dei dati è subordinata agli obblighi di conservazione dei documenti imposti da norme di legge.");

        Sotto(sec, "Diritto di proporre reclamo all'Autorità di controllo");
        Nota(sec, "Lei potrà, nel caso ritenga che il trattamento di dati che la riguardano violi il Regolamento UE 2016/679, proporre reclamo all'Autorità per la protezione dei dati personali.");

        Sotto(sec, "Consensi");
        Nota(sec, "Acconsento al trattamento dei dati di natura particolare ai sensi dell'art. 9 del Reg. UE 2016/679.");
        Nota(sec, "[  ] SI        [  ] NO");
        Nota(sec, "Acconsento ad essere contattato a mezzo posta cartacea, via e-mail, sms o App di messaggistica, per l'invio di occasionali aggiornamenti circa nuovi servizi obbligatori e/o funzionali alla sua attività.");
        Nota(sec, "[  ] SI        [  ] NO");
        Nota(sec, "Acconsento al trattamento dei miei dati personali per l'attività di profilazione al fine di poter selezionare più accuratamente le comunicazioni a lei destinate.");
        Nota(sec, "[  ] SI        [  ] NO");
    }

    /// <summary>Righe del prezzo in grassetto a destra (sotto un controllo o una voce accessoria).</summary>
    private static void RighePrezzo(Section sec, IReadOnlyList<string> righe)
    {
        for (var k = 0; k < righe.Count; k++)
        {
            var pz = sec.AddParagraph(righe[k]);
            pz.Format.Alignment = ParagraphAlignment.Right;
            pz.Format.Font.Bold = true;
            if (k == righe.Count - 1) pz.Format.SpaceAfter = "0.1cm";
        }
    }

    /// <summary>
    /// Condizioni di pagamento (12/10/2026): con una modalità titolo e righe; con due o più la frase di scelta e, per
    /// ognuna, una casella quadrata con la prima riga in grassetto e le altre rientrate (come nel modello di Mirko).
    /// </summary>
    private static void CondizioniPagamento(Section sec, string? testo)
    {
        var modalita = TestiOfferta.ModalitaPagamento(testo);
        if (modalita.Count == 0) return;
        Nota(sec, modalita.Count == 1 ? "Condizioni di pagamento:" : TestiOfferta.SceltaPagamento, spazioPrima: true, grassetto: true);
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.AddColumn(Unit.FromCentimeter(0.38));
        t.AddColumn(Unit.FromCentimeter(0.3));
        t.AddColumn(Unit.FromCentimeter(15.5));
        foreach (var m in modalita)
        {
            var spazio = t.AddRow();
            spazio.Height = "0.15cm";
            spazio.HeightRule = RowHeightRule.Exactly;
            for (var k = 0; k < m.Count; k++)
            {
                var row = t.AddRow();
                if (k == 0 && modalita.Count > 1)
                {
                    // Casella quadrata: cella con il solo bordo, alta una riga (il carattere del documento non ha il simbolo ☐).
                    row.Height = "0.38cm";
                    row.HeightRule = RowHeightRule.Exactly;
                    var c = row.Cells[0];
                    c.Borders.Width = 0.75;
                    c.Borders.Color = Colors.Black;
                }
                var par = row.Cells[2].AddParagraph(m[k]);
                par.Format.Font.Size = 8.5;
                par.Format.Font.Bold = k == 0;
            }
        }
        var fine = t.AddRow();
        fine.Height = "0.3cm";
        fine.HeightRule = RowHeightRule.Exactly;
        t.Rows.LeftIndent = Unit.FromCentimeter(0.4);
    }

    private static void Nota(Section sec, string testo, bool spazioPrima = false, bool grassetto = false)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
        p.Format.Font.Bold = grassetto;
        if (spazioPrima) p.Format.SpaceBefore = "0.4cm";
    }

    private static string Fmt(decimal v) => v.ToString("N2", It);

    private static void AggiungiRiepilogoPromozionale(Section sec)
    {
        Titolo(sec, TestiOfferta.TitoloIlNostroLavoro);
        var testo = sec.AddParagraph(TestiOfferta.IlNostroLavoro);
        testo.Format.SpaceAfter = "0.2cm";

        var tagline = sec.AddParagraph(TestiOfferta.TaglineIlNostroLavoro);
        tagline.Format.Font.Bold = true;
        tagline.Format.Font.Italic = true;
        tagline.Format.SpaceAfter = "0.3cm";

        var sotto = sec.AddParagraph(TestiOfferta.TitoloLaboratorioAnalisi);
        sotto.Format.Font.Bold = true;
        sotto.Format.SpaceAfter = "0.1cm";
        sec.AddParagraph(TestiOfferta.LaboratorioAnalisi).Format.SpaceAfter = "0.3cm";
    }
}
