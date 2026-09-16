using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di Taratura dei termometri (Modulo Statistico 04-01) in PDF e Word,
/// generato con MigraDoc da una definizione unica. Carta MARE.A, testata,
/// strumento in taratura, campione di riferimento con certificato, tabella
/// riepilogativa dei risultati per punto, note e firma.
/// </summary>
public interface IGeneratoreRapportoTaratura
{
    byte[] GeneraPdf(DatiRapportoTaratura dati, string? percorsoWwwRoot = null);
    byte[] GeneraWord(DatiRapportoTaratura dati, string? percorsoWwwRoot = null);
    string NomeFile(DatiRapportoTaratura dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreRapportoTaratura : IGeneratoreRapportoTaratura
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiRapportoTaratura d, string? percorsoWwwRoot = null)
    {
        var doc = Costruisci(d, percorsoWwwRoot);
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    public byte[] GeneraWord(DatiRapportoTaratura d, string? percorsoWwwRoot = null)
    {
        var doc = Costruisci(d, percorsoWwwRoot);
        var tmp = Path.Combine(Path.GetTempPath(), "labai_tar_" + Guid.NewGuid().ToString("N") + ".rtf");
        try
        {
            new RtfDocumentRenderer().Render(doc, tmp, Path.GetTempPath());
            return File.ReadAllBytes(tmp);
        }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
    }

    public string NomeFile(DatiRapportoTaratura d, string estensione)
    {
        var nome = Slug(d.StrumentoIdentificazione, "Taratura");
        var cod = Slug(d.StrumentoCodice, "");
        // Evita la ripetizione: aggiunge il codice solo se non è già contenuto nell'identificazione.
        bool giaPresente = cod.Length > 0 &&
            (nome.Contains(cod, StringComparison.OrdinalIgnoreCase) || cod.Contains(nome, StringComparison.OrdinalIgnoreCase));
        var parteCod = (cod.Length == 0 || giaPresente) ? "" : $"_{cod}";

        // Cliente (committente), quando presente.
        var cli = Slug(d.Committente, "");
        var parteCli = cli.Length == 0 ? "" : $"_{cli}";

        return $"Rapporto_Taratura{parteCli}_{nome}{parteCod}_{d.DataTaratura:yyyyMMdd}.{estensione}";
    }

    /// <summary>Slug per nome file: solo lettere/cifre, il resto "_", senza ripetizioni, troncato.</summary>
    private static string Slug(string? s, string fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var sb = new System.Text.StringBuilder();
        bool underscore = false;
        foreach (var ch in s.Trim())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); underscore = false; }
            else if (!underscore) { sb.Append('_'); underscore = true; }
        }
        var r = sb.ToString().Trim('_');
        if (r.Length > 40) r = r.Substring(0, 40).Trim('_');
        return r.Length == 0 ? fallback : r;
    }

    private static Document Costruisci(DatiRapportoTaratura d, string? percorsoWwwRoot)
    {
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 9;

        var headerImg = Immagine(percorsoWwwRoot, "intestazione_marea_pulita.png")
                        ?? Immagine(percorsoWwwRoot, "intestazione carta intestata MAREA.png");
        var footerImg = Immagine(percorsoWwwRoot, "Piede carta intestata MAREA.png");
        bool cartaIntestata = headerImg is not null || footerImg is not null;

        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(cartaIntestata ? 3.4 : 1.0);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(cartaIntestata ? 2.2 : 1.0);
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

        if (!cartaIntestata)
        {
            // Nessuna carta intestata disponibile: intestazione testuale (comportamento precedente).
            var h = sec.AddParagraph("MARE.A s.r.l"); h.Format.Font.Bold = true; h.Format.Font.Size = 12;
            Piccolo(sec, "Sede legale e operativa: Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • Fax 0541 830460");
            Piccolo(sec, "info@mare-a.com • www.mare-a.com • Cod. Fisc. e P. Iva 03670240401 • Laboratorio Analisi");
        }

        var tit = sec.AddParagraph("RAPPORTO DI TARATURA");
        tit.Format.Alignment = ParagraphAlignment.Center; tit.Format.Font.Bold = true; tit.Format.Font.Size = 13;
        tit.Format.SpaceBefore = "0.25cm";
        var sub = sec.AddParagraph("Modulo Statistico 04-01 — Taratura termometri");
        sub.Format.Alignment = ParagraphAlignment.Center; sub.Format.Font.Size = 8; sub.Format.SpaceAfter = "0.2cm";

        // Testata
        var t = TabellaInfo(sec);
        RigaInfo(t, "Rapporto n°", d.NumeroRapporto, "Data taratura", d.DataTaratura.ToString("dd/MM/yyyy", It));
        RigaInfo(t, "Rif. istruzione", d.RiferimentoIstruzione ?? "—", "Data scadenza",
            d.DataScadenza is DateOnly s ? s.ToString("dd/MM/yyyy", It) : "—");
        RigaInfo(t, "Committente", d.Committente ?? "MARE.A (interno)", "Redatto da", d.RedattoDa ?? "—");

        // Modalità di taratura (testo fisso, in corsivo)
        var metTit = sec.AddParagraph("Modalità di taratura:");
        metTit.Format.Font.Bold = true; metTit.Format.SpaceBefore = "0.2cm";
        var met = sec.AddParagraph(
            "La verifica è stata eseguita, conformemente al DT 09 DT confrontando i valori forniti dal "
            + "termometro campione o Campione di Riferimento (CDR) ed il termometro in esame su diversi valori "
            + "di temperatura ricavati immergendo i misuratori di temperatura in acqua distillata termostatata "
            + "alle temperature indicate nella tabella riepilogativa. I risultati sono riferiti esclusivamente "
            + "all'oggetto in taratura e sono validi nel momento e nelle condizioni di taratura, salvo "
            + "diversamente specificato.");
        met.Format.Font.Size = 8.5; met.Format.Font.Italic = true;

        // Strumento in taratura
        Titolo(sec, "Strumento in taratura");
        var us = TabellaInfo(sec);
        RigaInfo(us, "Identificazione", d.StrumentoIdentificazione, "Codice", d.StrumentoCodice ?? "—");
        RigaInfo(us, "Risoluzione", $"{Num(d.RisoluzioneStrumento, 3)} °C", "", "");

        // Campione di riferimento
        Titolo(sec, "Campione di riferimento (CDR)");
        var cd = TabellaInfo(sec);
        RigaInfo(cd, "Identificazione", d.CdrIdentificazione, "Tipo", d.CdrTipo ?? "—");
        RigaInfo(cd, "Centro LAT", d.CdrCentroLat ?? "—", "Certificato n°", d.CdrNumeroCertificato ?? "—");
        RigaInfo(cd, "Tarato il", d.CdrDataTaratura is DateOnly ct ? ct.ToString("dd/MM/yyyy", It) : "—",
            "Scadenza CDR", d.CdrDataScadenza is DateOnly cs ? cs.ToString("dd/MM/yyyy", It) : "—");
        RigaInfo(cd, "Risoluzione CDR", $"{Num(d.CdrRisoluzione, 3)} °C", "Fattore di copertura k", Num(d.CdrFattoreCopertura, 0));

        // Punti del certificato CDR — solo i primari (i secondari sono la taratura precedente,
        // usata unicamente per l'interpolazione; i valori "ufficiali" sono nel certificato allegato).
        var puntiCert = d.Certificato.Where(p => !p.Secondario).ToList();
        if (puntiCert.Count > 0)
        {
            var lblCert = sec.AddParagraph("Dati ultima taratura CDR");
            lblCert.Format.Font.Bold = true; lblCert.Format.SpaceBefore = "0.2cm"; lblCert.Format.SpaceAfter = "0.05cm";
            var cert = TabellaValori(sec, "T indicata °C", "T riferim. °C", "Differenza °C", "UE(CR) °C");
            foreach (var p in puntiCert)
                RigaValori(cert, Num(p.TIndicata, 2), Num(p.TRiferimento, 2), Num(p.Differenza, 3), Num(p.IncertezzaEstesa, 3));
        }

        // Risultati per punto
        Titolo(sec, "Dati riepilogativi della taratura");
        var ris = TabellaRisultati(sec);
        foreach (var p in d.Risultato.Punti)
            RigaRisultato(ris, p.Etichetta, p.NumeroMisure.ToString(It), Num(p.TemperaturaMediaRiferimento, 2), Num(p.TemperaturaMediaStrumento, 2),
                Num(p.DifferenzaMedia, 3), Num(p.IncertezzaEstesa, 3), Num(p.FattoreCorrezione, 3));

        var notaU = sec.AddParagraph(
            "(1) L'incertezza di misura include i contributi dovuti alla risoluzione e alla ripetibilità "
            + "dello strumento in taratura.");
        notaU.Format.Font.Size = 8; notaU.Format.Font.Italic = true; notaU.Format.SpaceBefore = "0.05cm";

        // Note (testo fisso, in corsivo)
        var nota = sec.AddParagraph(
            "I parametri identificati sono validi esclusivamente nelle condizioni di misura effettivamente "
            + "riscontrate; l'incertezza riportata è l'incertezza estesa di taratura del termometro. Le incertezza "
            + "di misura dichiarate in questo documento sono state determinate conformemente alla Guida ISO/IEC 98 "
            + "e al documento EA-04/02. Solitamente sono espresse come incertezza estesa ottenuta moltiplicando "
            + "l'incertezza tipo per il fattore di copertura k corrispondente ad un livello di fiducia di circa il "
            + "95%. Normalmente tale fattore vale 2.");
        nota.Format.Font.Size = 8; nota.Format.Font.Italic = true; nota.Format.SpaceBefore = "0.3cm";

        // Firma — etichetta e nome centrati
        var firmaLbl = sec.AddParagraph("Responsabile del Rapporto");
        firmaLbl.Format.Font.Bold = true;
        firmaLbl.Format.SpaceBefore = "0.7cm";
        firmaLbl.Format.Alignment = ParagraphAlignment.Center;
        var firmaNome = sec.AddParagraph(d.RedattoDa ?? "");
        firmaNome.Format.SpaceBefore = "0.05cm";
        firmaNome.Format.Alignment = ParagraphAlignment.Center;

        return doc;
    }

    // ── Helper ─────────────────────────────────────────────────────
    private static void Piccolo(Section sec, string testo) => sec.AddParagraph(testo).Format.Font.Size = 8;

    private static void Titolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo);
        p.Format.Font.Bold = true; p.Format.SpaceBefore = "0.2cm"; p.Format.SpaceAfter = "0.05cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }

    private static Table TabellaInfo(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0;
        t.Format.SpaceAfter = "0.05cm";
        t.Format.Font.Size = 8.5;
        t.AddColumn(Unit.FromCentimeter(4.0)); t.AddColumn(Unit.FromCentimeter(4.5));
        t.AddColumn(Unit.FromCentimeter(4.0)); t.AddColumn(Unit.FromCentimeter(4.5));
        return t;
    }

    private static void RigaInfo(Table t, string l1, string v1, string l2, string v2)
    {
        var r = t.AddRow();
        r.Cells[0].AddParagraph(l1).Format.Font.Bold = true;
        r.Cells[1].AddParagraph(v1);
        if (!string.IsNullOrEmpty(l2)) r.Cells[2].AddParagraph(l2).Format.Font.Bold = true;
        if (!string.IsNullOrEmpty(v2)) r.Cells[3].AddParagraph(v2);
    }

    private static Table TabellaValori(Section sec, params string[] intest)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5; t.Borders.Color = new Color(0xFFBBBBBB);
        t.Format.Font.Size = 8.5; t.Format.SpaceAfter = "0.05cm";
        foreach (var _ in intest) t.AddColumn(Unit.FromCentimeter(17.0 / intest.Length));
        var h = t.AddRow(); h.Format.Font.Bold = true; h.Shading.Color = new Color(0xFFF3F3F3);
        for (int i = 0; i < intest.Length; i++)
            h.Cells[i].AddParagraph(intest[i]).Format.Alignment = ParagraphAlignment.Center;
        return t;
    }

    private static void RigaValori(Table t, params string[] valori)
    {
        var r = t.AddRow();
        for (int i = 0; i < valori.Length; i++)
            r.Cells[i].AddParagraph(valori[i]).Format.Alignment = ParagraphAlignment.Center;
    }

    private static Table TabellaRisultati(Section sec)
    {
        var t = sec.AddTable();
        t.Borders.Width = 0.5; t.Borders.Color = new Color(0xFFBBBBBB);
        t.Format.Font.Size = 8.5; t.Format.SpaceAfter = "0.05cm";
        t.AddColumn(Unit.FromCentimeter(1.4));   // punto
        t.AddColumn(Unit.FromCentimeter(1.5));   // n. misure
        t.AddColumn(Unit.FromCentimeter(3.0));   // Tr
        t.AddColumn(Unit.FromCentimeter(3.0));   // Tw
        t.AddColumn(Unit.FromCentimeter(3.0));   // diff
        t.AddColumn(Unit.FromCentimeter(2.6));   // U
        t.AddColumn(Unit.FromCentimeter(2.5));   // correzione
        var h = t.AddRow(); h.Format.Font.Bold = true; h.Shading.Color = new Color(0xFFF3F3F3);
        string[] intest = ["Punto", "N. misure", "Rif. Tr [°C]", "Strum. Tw [°C]", "Diff. Tw−Tr [°C]", "Incert. U (1) [°C]", "Correzione [°C]"];
        for (int i = 0; i < intest.Length; i++)
        {
            h.Cells[i].AddParagraph(intest[i]).Format.Alignment = ParagraphAlignment.Center;
            h.Cells[i].Format.Font.Size = 8.5;
        }
        return t;
    }

    private static void RigaRisultato(Table t, string punto, string nMisure, string tr, string tw, string diff, string u, string corr)
    {
        var r = t.AddRow();
        r.Cells[0].AddParagraph(punto).Format.Alignment = ParagraphAlignment.Center;
        r.Cells[1].AddParagraph(nMisure).Format.Alignment = ParagraphAlignment.Center;
        string[] v = [tr, tw, diff, u, corr];
        for (int i = 0; i < v.Length; i++)
            r.Cells[i + 2].AddParagraph(v[i]).Format.Alignment = ParagraphAlignment.Right;
    }

    private static string Num(double v, int dec) => v.ToString("N" + dec, It);

    /// <summary>Percorso di un'immagine di carta intestata in wwwroot/img, se esiste.</summary>
    private static string? Immagine(string? percorsoWwwRoot, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoWwwRoot)) return null;
        var full = Path.Combine(percorsoWwwRoot, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }
}
