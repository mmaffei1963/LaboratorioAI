using System.Globalization;
using LaboratorioAI.Core.Entita;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Testi fissi del rapporto di intervento (modificabili in Amministrazione → Impostazioni documenti).
/// I predefiniti sono quelli del modello MOD. CF 05 approvato il 5/10/2026.
/// </summary>
public record TestiRapportino(
    string FonteInformazioni,
    string PianoAnalitico,
    string ModalitaConsegna,
    string Responsabilita,
    string NonCertificazione,
    string DecisioniCliente,
    string AnalisiLaboratorio,
    string Osservazioni,
    string Rendicontazione,
    string Condizioni)
{
    public static readonly TestiRapportino Predefiniti = new(
        FonteInformazioni:
            "Dati, procedure e documenti esaminati sono quelli forniti o resi disponibili dal Cliente in data odierna, salvo diversa indicazione.",
        PianoAnalitico:
            "Frequenze, matrici, punti di prelievo e parametri sono stati definiti con il Cliente, che adotta il piano come proprio documento; " +
            "la scelta dei Campioni da consegnare al Laboratorio resta del Cliente.",
        ModalitaConsegna:
            "Il presente rapporto e i documenti emessi sono pubblicati nel Portale clienti; la pubblicazione vale come consegna. " +
            "Il Cliente è tenuto a scaricarli e conservarli entro 30 giorni dalla pubblicazione; l'avviso e-mail ha funzione di cortesia.",
        Responsabilita:
            "Il presente rapporto descrive l'attività di consulenza svolta e si basa sulla documentazione e sulle informazioni rese disponibili " +
            "dal Cliente alla data dell'intervento. Il Cliente resta responsabile della completezza, correttezza e aggiornamento di tali " +
            "informazioni; MARE.A SRL e il Consulente non rispondono delle conseguenze derivanti da dati, documenti o circostanze non " +
            "comunicati o comunicati in modo incompleto o non veritiero.",
        NonCertificazione:
            "Salvo diversa e specifica indicazione, il presente rapporto non costituisce attestazione o certificazione di conformità del " +
            "Cliente, dei suoi sistemi di autocontrollo o di gestione, locali, impianti o prodotti alla normativa applicabile, ma un " +
            "resoconto dell'attività di supporto tecnico erogata.",
        DecisioniCliente:
            "Le decisioni conseguenti a esiti analitici, non conformità, audit o controlli ufficiali (notifica alle Autorità competenti, fra " +
            "cui quella prevista dall'art. 19 del Reg. (CE) 178/2002, blocco, ritiro o richiamo di prodotti, sospensione di attività o " +
            "impianti, azioni correttive) spettano esclusivamente al Cliente in qualità di operatore del settore alimentare, datore di " +
            "lavoro, gestore dell'impianto o titolare del trattamento. Il supporto del Consulente non trasferisce tali responsabilità a " +
            "MARE.A SRL né comporta obblighi di comunicazione ad Autorità o a terzi da parte di MARE.A SRL, salvo specifico incarico " +
            "o autorizzazione scritta del Cliente.",
        AnalisiLaboratorio:
            "Le analisi eventualmente eseguite dal Laboratorio MARE.A sono documentate esclusivamente dai Rapporti di Prova pubblicati nel " +
            "Portale clienti e sono regolate dalle Condizioni Generali di Fornitura del Laboratorio. Il presente rapporto non è un Rapporto " +
            "di Prova, non riporta risultati analitici e non costituisce giudizio di conformità sui risultati; i Campioni analizzati sono " +
            "quelli scelti, prelevati o indicati dal Cliente, che ne valuta la rappresentatività.",
        Osservazioni:
            "Eventuali osservazioni sul presente rapporto vanno comunicate entro 3 giorni lavorativi dalla pubblicazione nel Portale clienti, " +
            "tramite il Portale o ai recapiti indicati in calce. Trascorso tale termine il rapporto si intende accettato nei contenuti e negli " +
            "importi e le prestazioni saranno fatturate come indicato.",
        Rendicontazione:
            "Eventuali osservazioni sul presente rapporto vanno comunicate entro 3 giorni lavorativi dalla pubblicazione nel Portale clienti, " +
            "tramite il Portale o ai recapiti indicati in calce. Trascorso tale termine il rapporto si intende accettato nei contenuti.",
        Condizioni:
            "Prestazione regolata dalle Condizioni Generali di Fornitura – Servizi di Consulenza e Formazione (MOD. CF 01), pubblicate su " +
            "www.mare-a.com e nel Portale clienti, accettate con l'Offerta sopra richiamata.");
}

/// <summary>Rapporto di intervento di consulenza (PDF su carta intestata) da inviare al cliente prima della fattura.</summary>
public interface IGeneratoreRapportino
{
    /// <param name="dataConsegna">Data di invio (non stampata: la data di pubblicazione non va nel rapporto).</param>
    byte[] GeneraPdf(Rapportino r, TestiRapportino testi, DateTime? dataConsegna, string? percorsoWwwRoot);
    string NomeFile(Rapportino r);
}

/// <inheritdoc />
public class GeneratoreRapportino : IGeneratoreRapportino
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>Aliquota IVA ordinaria applicata alle consulenze.</summary>
    public const decimal AliquotaIva = 22m;

    public string NomeFile(Rapportino r)
    {
        // AAAAMMGG_RC_IdRapportino_IdInsediamento_ClienteSlug.pdf (data = intervento), come gli Ordini (…_OA_…).
        var data = r.DataIntervento.ToString("yyyyMMdd", It);
        var cli = GeneratoreOrdineAccettazione.Slug(r.RagioneSociale, 30);
        return $"{data}_RC_{r.IdRapportino}_{r.IdInsediamentoArcLab}_{cli}.pdf";
    }

    public byte[] GeneraPdf(Rapportino r, TestiRapportino testi, DateTime? dataConsegna, string? percorsoWwwRoot)
    {
        FontResolverLabAI.Registra();
        var doc = Costruisci(r, testi, dataConsegna, percorsoWwwRoot);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public static string TestoLuogo(LuogoPrestazione l) => l switch
    {
        LuogoPrestazione.PressoSede => "Presso MARE.A",
        LuogoPrestazione.DaRemoto => "Da remoto",
        _ => "Presso il Cliente"
    };

    private static Document Costruisci(Rapportino r, TestiRapportino testi, DateTime? dataConsegna, string? www)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 9;

        // Carta intestata: nessun marchio né numero ACCREDIA nei rapporti di consulenza (nota d'uso 4).
        var headerImg = Immagine(www, "intestazione_marea_pulita.png") ?? Immagine(www, "intestazione carta intestata MAREA.png");
        var footerImg = Immagine(www, "Piede carta intestata MAREA.png");
        bool cartaIntestata = headerImg is not null || footerImg is not null;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(cartaIntestata ? 3.3 : 1.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(cartaIntestata ? 2.3 : 1.2);
        sec.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
        sec.PageSetup.FooterDistance = Unit.FromCentimeter(0.4);

        if (headerImg is not null)
        {
            var hp = sec.Headers.Primary.AddParagraph();
            hp.Format.Alignment = ParagraphAlignment.Center;
            var img = hp.AddImage(headerImg);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(16.5);
        }
        if (footerImg is not null)
        {
            var fp = sec.Footers.Primary.AddParagraph();
            fp.Format.Alignment = ParagraphAlignment.Center;
            var img = fp.AddImage(footerImg);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(17);
        }
        var pag = sec.Footers.Primary.AddParagraph();
        pag.Format.Alignment = ParagraphAlignment.Center;
        pag.Format.Font.Size = 7;
        pag.AddText($"Rapporto di intervento n. {r.IdRapportino} – Pagina ");
        pag.AddPageField();
        pag.AddText(" di ");
        pag.AddNumPagesField();

        if (!cartaIntestata)
        {
            var intest = sec.AddParagraph("MARE.A s.r.l");
            intest.Format.Font.Bold = true;
            intest.Format.Font.Size = 12;
            var ind = sec.AddParagraph("Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • info@mare-a.com • www.mare-a.com");
            ind.Format.Font.Size = 8.5;
        }

        var tit = sec.AddParagraph("RAPPORTO DI INTERVENTO DI CONSULENZA");
        tit.Format.Alignment = ParagraphAlignment.Center;
        tit.Format.Font.Bold = true;
        tit.Format.Font.Size = 13;
        tit.Format.SpaceBefore = "0.2cm";
        tit.Format.SpaceAfter = "0.1cm";

        var rif = sec.AddParagraph($"n. {r.IdRapportino} del {r.DataIntervento.ToString("dd/MM/yyyy", It)}");
        rif.Format.Alignment = ParagraphAlignment.Center;
        rif.Format.Font.Size = 9;
        rif.Format.Font.Bold = true;
        rif.Format.SpaceAfter = "0.25cm";

        // Destinatario
        var dest = sec.AddParagraph();
        dest.Format.SpaceAfter = "0.15cm";
        dest.AddText("Spett.le ");
        dest.AddFormattedText(Vuoto(r.RagioneSociale), TextFormat.Bold);
        if (!string.IsNullOrWhiteSpace(r.NomeInsediamento))
        {
            dest.AddLineBreak(); dest.AddText("Insediamento: ");
            dest.AddFormattedText(r.NomeInsediamento!.Trim(), TextFormat.Bold);
        }
        if (!string.IsNullOrWhiteSpace(r.IndirizzoInsediamento))
        {
            dest.AddLineBreak(); dest.AddText("Indirizzo: ");
            dest.AddFormattedText(r.IndirizzoInsediamento!.Trim(), TextFormat.Bold);
        }

        // ── INTERVENTO ──
        Titolo(sec, "INTERVENTO");
        var d = TabellaDati(sec);
        Dato(d, "Data", r.DataIntervento.ToString("dd/MM/yyyy", It));
        Dato(d, "Consulente", r.Consulente);
        Dato(d, "Luogo", TestoLuogo(r.Luogo));
        Dato(d, "Ore", Ore(r.Ore));
        if (!string.IsNullOrWhiteSpace(r.RiferimentoOfferta)) Dato(d, "Rif. Offerta / contratto", r.RiferimentoOfferta);
        // Sempre presente (come nel modello precedente): senza delega in essere lo si dichiara.
        Dato(d, "Rif. delega operativa", string.IsNullOrWhiteSpace(r.RiferimentoDelega) ? "nessuna delega operativa in essere" : r.RiferimentoDelega);

        // ── ATTIVITÀ SVOLTA ──
        Titolo(sec, "ATTIVITÀ SVOLTA");
        foreach (var par in Paragrafi(string.IsNullOrWhiteSpace(r.AttivitaSvolta) ? r.DescrizioneFattura : r.AttivitaSvolta))
        {
            var p = sec.AddParagraph(par);
            p.Format.SpaceAfter = "0.08cm";
            p.Format.Alignment = ParagraphAlignment.Justify;
        }

        // ── DOCUMENTAZIONE DI RIFERIMENTO ──
        Titolo(sec, "DOCUMENTAZIONE DI RIFERIMENTO");
        var doc2 = TabellaDati(sec);
        var docs = r.Documenti.Where(x => x.Attivo).OrderBy(x => x.Ordine).ToList();
        var esaminati = docs.Where(x => x.Azione == AzioneDocumento.Esaminato).ToList();
        var modificati = docs.Where(x => x.Azione != AzioneDocumento.Esaminato).ToList();
        if (esaminati.Count > 0)
            DatoRighe(doc2, "Documenti esaminati", esaminati.Select(x => DescriviDocumento(x, false)));
        if (modificati.Count > 0)
            DatoRighe(doc2, "Documenti modificati/emessi", modificati.Select(x => DescriviDocumento(x, true)));
        Dato(doc2, "Fonte delle informazioni",
            string.IsNullOrWhiteSpace(r.FonteInformazioni) ? testi.FonteInformazioni : r.FonteInformazioni);
        if (modificati.Any(x => x.PianoAnalitico))
        {
            var testo = testi.PianoAnalitico;
            if (!string.IsNullOrWhiteSpace(r.NotaPianoAnalitico)) testo = r.NotaPianoAnalitico!.Trim() + " " + testo;
            Dato(doc2, "Piano di autocontrollo analitico", testo);
        }
        // Nessuna data di pubblicazione nel rapporto (decisione 5/10/2026): la pubblicazione avviene dopo
        // l'emissione e scriverla prima rischierebbe errori. Un eventuale "{data}" residuo nel testo si toglie.
        Dato(doc2, "Modalità di consegna", testi.ModalitaConsegna.Replace(" in data {data}", "").Replace("{data}", "").Trim());

        // ── ESITO E RESPONSABILITÀ ──
        Titolo(sec, "ESITO E RESPONSABILITÀ");
        foreach (var t in new[] { testi.Responsabilita, testi.NonCertificazione, testi.DecisioniCliente, testi.AnalisiLaboratorio })
        {
            if (string.IsNullOrWhiteSpace(t)) continue;
            var p = sec.AddParagraph(t.Trim());
            p.Format.Font.Size = 7.5;
            p.Format.Alignment = ParagraphAlignment.Justify;
            p.Format.SpaceAfter = "0.08cm";
        }
        if (!string.IsNullOrWhiteSpace(r.FollowUpAzione))
        {
            var p = sec.AddParagraph();
            p.Format.SpaceBefore = "0.1cm";
            p.AddFormattedText("Azioni di follow-up: ", TextFormat.Bold);
            p.AddText(r.FollowUpAzione!.Trim());
            if (!string.IsNullOrWhiteSpace(r.FollowUpResponsabile)) p.AddText($" — a cura di: {r.FollowUpResponsabile!.Trim()}");
            if (!string.IsNullOrWhiteSpace(r.FollowUpTermine)) p.AddText($" — termine indicativo: {r.FollowUpTermine!.Trim()}");
        }

        // ── RIEPILOGO ──
        Titolo(sec, "RIEPILOGO");
        if (r.ModalitaFatturazione == ModalitaFatturazione.Convenzione)
        {
            var p = sec.AddParagraph("Prestazione compresa nella convenzione" +
                (string.IsNullOrWhiteSpace(r.DescrizioneCommessa) ? "" : $" «{r.DescrizioneCommessa!.Trim()}»") +
                ": non è prevista una fatturazione separata per questo intervento.");
            p.Format.SpaceAfter = "0.2cm";
        }
        else if (r.Prepagato)
        {
            var t = TabellaImporti(sec);
            Riga(t, "Prestazione scalata dal pacchetto di ore prepagato", "");
            if (r.DataUltimoScalo is DateTime dus) Riga(t, "Ultimo scalo precedente", dus.ToString("dd/MM/yyyy", It));
            if (r.OreResiduePrima is decimal orp) Riga(t, "Ore residue prima dell'intervento", Ore(orp));
            Riga(t, "Ore utilizzate", Ore(r.Ore));
            if (r.OreResidueDopo is decimal ord) Riga(t, "Ore residue dopo l'intervento", Ore(ord), grassetto: true);
            TieniUnita(t);
        }
        else
        {
            var t = TabellaImporti(sec);
            RigaIntera(t, $"Voce in fattura ({(r.ModalitaFatturazione == ModalitaFatturazione.AOre ? "a ore" : "a consuntivo")}): ", r.DescrizioneFattura);
            if (r.ModalitaImporto == ModalitaImporto.Orario && r.TariffaOraria is decimal tar)
                Riga(t, $"{Ore(r.Ore)} ore × € {Fmt(tar)}", $"€ {Fmt(r.Importo)}");
            if (r.ImportoCorretto is decimal ic && ic != r.Importo)
                Riga(t, "Importo rettificato", $"€ {Fmt(ic)}");
            var imponibile = r.ImportoDaFatturare;
            var iva = Math.Round(imponibile * AliquotaIva / 100m, 2, MidpointRounding.AwayFromZero);
            Riga(t, "Imponibile", $"€ {Fmt(imponibile)}");
            Riga(t, $"IVA {AliquotaIva.ToString("0.##", It)}%", $"€ {Fmt(iva)}");
            Riga(t, "TOTALE (IVA compresa)", $"€ {Fmt(imponibile + iva)}", grassetto: true);
            TieniUnita(t);
        }

        var nota = sec.AddParagraph((r.SoloRendicontato ? testi.Rendicontazione : testi.Osservazioni).Trim());
        nota.Format.Font.Size = 8;
        nota.Format.SpaceBefore = "0.25cm";
        nota.Format.Alignment = ParagraphAlignment.Justify;

        var condizioni = testi.Condizioni.Trim();
        // Senza riferimento all'offerta, "sopra richiamata" non avrebbe a cosa riferirsi.
        if (string.IsNullOrWhiteSpace(r.RiferimentoOfferta)) condizioni = condizioni.Replace(" sopra richiamata", "");
        if (condizioni.Length > 0)
        {
            var c = sec.AddParagraph(condizioni);
            c.Format.Font.Size = 7.5;
            c.Format.Font.Italic = true;
            c.Format.SpaceBefore = "0.15cm";
        }

        return doc;
    }

    private static string DescriviDocumento(RapportinoDocumento x, bool conAzione)
    {
        var s = x.Nome.Trim();
        if (!string.IsNullOrWhiteSpace(x.Revisione)) s += $" rev. {x.Revisione!.Trim()}";
        if (x.DataDocumento is DateTime dd) s += $" del {dd.ToString("dd/MM/yyyy", It)}";
        if (conAzione)
        {
            s += x.Azione == AzioneDocumento.Emesso ? " — emesso" : " — modificato";
            if (x.Consegna is ConsegnaDocumento c)
                s += c == ConsegnaDocumento.Bozza ? ", consegnato al Cliente in bozza" : ", consegnato al Cliente in versione definitiva";
            else s += ", non ancora consegnato";
        }
        return s;
    }

    private static IEnumerable<string> Paragrafi(string? testo) =>
        (testo ?? "").Replace("\r\n", "\n").Split('\n').Select(x => x.Trim()).Where(x => x.Length > 0);

    private static Table TabellaDati(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.AddColumn(Unit.FromCentimeter(3.9));
        t.AddColumn(Unit.FromCentimeter(13.1));
        return t;
    }

    private static Table TabellaImporti(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5;
        t.Borders.Color = new Color(0xFFBBBBBB);
        t.AddColumn(Unit.FromCentimeter(11));
        t.AddColumn(Unit.FromCentimeter(6));
        return t;
    }

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true;
        p.Format.SpaceBefore = "0.25cm";
        p.Format.SpaceAfter = "0.08cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
        p.Format.KeepWithNext = true;
    }

    private static void Dato(Table t, string label, string? valore)
    {
        var row = t.AddRow();
        var pl = row.Cells[0].AddParagraph(label);
        pl.Format.Font.Bold = true;
        pl.Format.Font.Size = 8.5;
        var pv = row.Cells[1].AddParagraph(Vuoto(valore));
        pv.Format.Font.Size = 8.5;
        pv.Format.SpaceAfter = "0.05cm";
    }

    private static void DatoRighe(Table t, string label, IEnumerable<string> righe)
    {
        var row = t.AddRow();
        var pl = row.Cells[0].AddParagraph(label);
        pl.Format.Font.Bold = true;
        pl.Format.Font.Size = 8.5;
        Paragraph? ultimo = null;
        foreach (var x in righe)
        {
            ultimo = row.Cells[1].AddParagraph("• " + x);
            ultimo.Format.Font.Size = 8.5;
        }
        if (ultimo is not null) ultimo.Format.SpaceAfter = "0.05cm";
    }

    private static void Riga(Table t, string label, string valore, bool grassetto = false)
    {
        var row = t.AddRow();
        var pl = row.Cells[0].AddParagraph(label);
        pl.Format.Font.Bold = grassetto;
        var pv = row.Cells[1].AddParagraph(valore);
        pv.Format.Alignment = ParagraphAlignment.Right;
        pv.Format.Font.Bold = grassetto;
    }

    /// <summary>Il riepilogo resta tutto sulla stessa pagina.</summary>
    private static void TieniUnita(Table t)
    {
        if (t.Rows.Count > 1) t.Rows[0].KeepWith = t.Rows.Count - 1;
    }

    /// <summary>Riga del riepilogo su tutta la larghezza (testo lungo, es. la voce in fattura).</summary>
    private static void RigaIntera(Table t, string label, string valore)
    {
        var row = t.AddRow();
        row.Cells[0].MergeRight = 1;
        var p = row.Cells[0].AddParagraph();
        p.AddFormattedText(label, TextFormat.Bold);
        p.AddText(Vuoto(valore));
    }

    private static string? Immagine(string? www, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(www)) return null;
        var full = Path.Combine(www, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }

    private static string Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();
    private static string Fmt(decimal v) => v.ToString("N2", It);
    private static string Ore(decimal v) => v.ToString("0.##", It);
}
