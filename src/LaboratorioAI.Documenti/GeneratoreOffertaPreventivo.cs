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
        normale.Font.Name = "Arial";
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

            var h = sec.AddParagraph("ANALISI DI LABORATORIO — " + (r.Descrizione ?? "").ToUpperInvariant());
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

    private static void Nota(Section sec, string testo, bool spazioPrima = false, bool grassetto = false)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Size = 8.5;
        p.Format.Font.Bold = grassetto;
        if (spazioPrima) p.Format.SpaceBefore = "0.4cm";
    }

    private static string Fmt(decimal v) => v.ToString("N2", It);
}
