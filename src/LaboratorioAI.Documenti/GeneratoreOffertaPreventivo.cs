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
    byte[] GeneraPdf(Preventivo p);
    string NomeFile(Preventivo p, string estensione);
}

/// <inheritdoc />
public class GeneratoreOffertaPreventivo : IGeneratoreOffertaPreventivo
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraWord(Preventivo p, string percorsoModello) =>
        OffertaDocx.Genera(p, percorsoModello);

    public byte[] GeneraPdf(Preventivo p)
    {
        FontResolverLabAI.Registra();
        var doc = Costruisci(p);
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
    private static Document Costruisci(Preventivo p)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Montserrat";
        normale.Font.Size = 10;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);

        // Intestazione azienda
        var intest = sec.AddParagraph("MARE.A s.r.l");
        intest.Format.Font.Bold = true;
        intest.Format.Font.Size = 12;
        Piccolo(sec, "Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • info@mare-a.com • www.mare-a.com");
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
        RigaInfo(testata, "Data offerta", (p.DataOfferta ?? DateTime.Today).ToString("dd/MM/yyyy", It), "Validità", $"{p.ValiditaGiorni} giorni");
        RigaInfo(testata, "Cliente", p.ClienteRagioneSociale ?? "—", "Partita IVA", string.IsNullOrWhiteSpace(p.ClientePartitaIva) ? "—" : p.ClientePartitaIva!);
        var indirizzo = string.Join(" ", new[] { p.ClienteIndirizzo, p.ClienteCap, p.ClienteCitta, p.ClienteProvincia }
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        RigaInfo(testata, "Indirizzo", string.IsNullOrWhiteSpace(indirizzo) ? "—" : indirizzo, "Oggetto", string.IsNullOrWhiteSpace(p.Oggetto) ? "—" : p.Oggetto!);

        var righe = p.Righe.OrderBy(r => r.Progressivo).ToList();
        var controlli = righe.Where(r => r.Tipo is TipoRigaPreventivo.ControlloEsistente or TipoRigaPreventivo.PropostaControllo or TipoRigaPreventivo.ControlloLibero).ToList();
        var accessori = righe.Where(r => r.Tipo == TipoRigaPreventivo.Accessorio).ToList();

        // Blocchi analisi
        foreach (var r in controlli)
        {
            var subappalto = r.Metodi.Any(m => m.IdLabEsternoArcLab != null) || r.Tipo == TipoRigaPreventivo.ControlloLibero;

            var idPart = r.IdControlloArcLab is int idc ? $" (ID {idc})" : "";
            var h = sec.AddParagraph("ANALISI DI LABORATORIO — " + (r.Descrizione ?? "").ToUpperInvariant() + idPart);
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.4cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.Shading.Color = new Color(0xFFEEEEEE);

            if (subappalto)
            {
                var s = sec.AddParagraph("(Le seguenti analisi possono essere eseguite presso laboratorio esterno accreditato)");
                s.Format.Font.Size = 8.5;
                s.Format.Font.Italic = true;
            }

            if (r.Metodi.Count > 0)
            {
                var t = sec.AddTable();
                t.Borders.Width = 0.5;
                t.Borders.Color = new Color(0xFFBBBBBB);
                t.AddColumn(Unit.FromCentimeter(7.5));
                t.AddColumn(Unit.FromCentimeter(7.5));
                t.AddColumn(Unit.FromCentimeter(2.0));
                var hr = t.AddRow();
                hr.Format.Font.Bold = true;
                hr.Shading.Color = new Color(0xFFF3F3F3);
                hr.Cells[0].AddParagraph("Parametro");
                hr.Cells[1].AddParagraph("Metodo");
                hr.Cells[2].AddParagraph("Accr.");
                foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
                {
                    var row = t.AddRow();
                    row.Cells[0].AddParagraph(m.Descrizione ?? m.DenominazioneRiga ?? "—");
                    row.Cells[1].AddParagraph(m.RiferimentoNormativo ?? "—");
                    row.Cells[2].AddParagraph(m.IdLabEsternoArcLab != null ? "*" : (m.Accreditato ? "Sì" : "—"));
                }
            }

            // Prezzo del controllo
            var pz = sec.AddParagraph($"Prezzo: € {Fmt(r.PrezzoFinale)}" + (r.ScontoPercentuale > 0 ? $"  (sconto {Fmt(r.ScontoPercentuale)}%)" : ""));
            pz.Format.Alignment = ParagraphAlignment.Right;
            pz.Format.Font.Bold = true;
            pz.Format.SpaceAfter = "0.1cm";

            // Frasi dei laboratori esterni (subappalto)
            foreach (var frase in r.Metodi.Where(m => !string.IsNullOrWhiteSpace(m.LabEsternoFrase))
                                          .Select(m => m.LabEsternoFrase!.Trim()).Distinct())
                Piccolo(sec, "* " + frase);
        }

        // Servizi accessori
        if (accessori.Count > 0)
        {
            var h = sec.AddParagraph("SERVIZI ACCESSORI");
            h.Format.Font.Bold = true;
            h.Format.SpaceBefore = "0.4cm";
            h.Format.SpaceAfter = "0.1cm";
            h.Format.Shading.Color = new Color(0xFFEEEEEE);

            var t = sec.AddTable();
            t.Borders.Width = 0.5;
            t.Borders.Color = new Color(0xFFBBBBBB);
            t.AddColumn(Unit.FromCentimeter(13));
            t.AddColumn(Unit.FromCentimeter(4));
            foreach (var a in accessori)
            {
                var row = t.AddRow();
                row.Cells[0].AddParagraph((a.Descrizione ?? "—") + (a.Quantita > 1 ? $" × {a.Quantita}" : ""));
                row.Cells[1].AddParagraph($"€ {Fmt(a.PrezzoFinale)}").Format.Alignment = ParagraphAlignment.Right;
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

        if (!string.IsNullOrWhiteSpace(p.CondizioniPagamento))
            Nota(sec, "Condizioni di pagamento: " + p.CondizioniPagamento!.Trim(), spazioPrima: true, grassetto: true);
        Nota(sec, $"Offerta valida {p.ValiditaGiorni} giorni dalla data indicata. I prezzi si intendono IVA esclusa.");
        if (!string.IsNullOrWhiteSpace(p.Note))
            Nota(sec, p.Note!.Trim());

        // Richiamo alle Condizioni Generali di Fornitura
        var rev = string.IsNullOrWhiteSpace(p.RevisioneCondizioni) ? "Mod. Com. 05" : p.RevisioneCondizioni!.Trim();
        Titolo(sec, "CONDIZIONI GENERALI DI FORNITURA");
        Nota(sec,
            $"Il presente contratto è integrato dalle Condizioni Generali di Fornitura di MARE.A s.r.l. ({rev}), che ne " +
            "costituiscono parte integrante e sostanziale. Le Condizioni Generali di Fornitura sono scaricabili dal sito " +
            "www.mare-a.com e disponibili su richiesta. Il COMMITTENTE dichiara di conoscerle e di accettarle integralmente, " +
            "con particolare riferimento alle clausole su prove in subappalto, tempi di consegna, regola decisionale e " +
            "giudizi di conformità, campionamento, conservazione dei campioni e trattamento dei dati. In caso di contrasto, " +
            "per le sole condizioni economiche e tecniche specifiche prevale quanto indicato nel presente contratto.");
        Nota(sec,
            "La presente offerta e le Condizioni Generali di Fornitura si intendono integralmente accettate dal COMMITTENTE " +
            "al momento della consegna del campione al laboratorio.", spazioPrima: true, grassetto: true);

        // Informativa privacy (fissa, uguale per ogni offerta)
        AggiungiInformativaPrivacy(sec);

        // Firma
        var firma = sec.AddParagraph("Per accettazione (timbro e firma)                                        MARE.A s.r.l.");
        firma.Format.SpaceBefore = "1cm";
        var linee = sec.AddParagraph("______________________________                                        ______________________________");
        linee.Format.SpaceBefore = "0.8cm";

        return doc;
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Piccolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
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

    private static void Nota(Section sec, string testo, bool spazioPrima = false, bool grassetto = false)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
        p.Format.Font.Bold = grassetto;
        if (spazioPrima) p.Format.SpaceBefore = "0.4cm";
    }

    private static string Fmt(decimal v) => v.ToString("N2", It);
}
