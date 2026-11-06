using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>Dati della Ricevuta di approvazione delle Condizioni Generali (Specifica v0.4 §9, 10/10/2026).</summary>
public sealed class DatiRicevutaApprovazione
{
    public string NumeroRicevuta { get; init; } = "";
    public string? RagioneSociale { get; init; }
    public string? PartitaIva { get; init; }
    public string NomeCognome { get; init; } = "";
    public string Qualifica { get; init; } = "";
    public string Email { get; init; } = "";
    public string Revisione { get; init; } = "";
    public string? ImprontaCondizioni { get; init; }
    public string? Offerta { get; init; }
    public string? ImprontaOfferta { get; init; }
    /// <summary>Data e ora locali del perfezionamento.</summary>
    public DateTime DataApprovazione { get; init; }
    /// <summary>Modalità in parole (es. «Caselle dal Portale clienti», «Caselle e firma grafica con dito»).</summary>
    public string Modalita { get; init; } = "";
    public string? IndirizzoIp { get; init; }
    public string Casella1 { get; init; } = "";
    public DateTime? OraCasella1 { get; init; }
    public string Casella2Introduzione { get; init; } = "";
    public IReadOnlyList<string> Clausole { get; init; } = Array.Empty<string>();
    public DateTime? OraCasella2 { get; init; }
    public string Casella3 { get; init; } = "";
    public DateTime? OraCasella3 { get; init; }
    public string ImprontaTesti { get; init; } = "";
    /// <summary>Immagini PNG delle firme grafiche (riquadro 1 e 2), se modalità D.</summary>
    public byte[]? Firma1 { get; init; }
    public byte[]? Firma2 { get; init; }
    /// <summary>Descrizione della firma con codice OTP (15/10/2026), se usata.</summary>
    public string? FirmaOtp { get; init; }
}

public interface IGeneratoreRicevutaApprovazione
{
    /// <summary>Ricevuta in PDF; <paramref name="percorsoWwwRoot"/> serve per la carta intestata (cartella img).</summary>
    byte[] GeneraPdf(DatiRicevutaApprovazione d, string? percorsoWwwRoot);
}

/// <inheritdoc />
public class GeneratoreRicevutaApprovazione : IGeneratoreRicevutaApprovazione
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiRicevutaApprovazione d, string? percorsoWwwRoot)
    {
        FontResolverLabAI.Registra();
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial";
        normale.Font.Size = 9;

        var header = Immagine(percorsoWwwRoot, "intestazione_marea_pulita.png") ?? Immagine(percorsoWwwRoot, "intestazione carta intestata MAREA.png");
        var footer = Immagine(percorsoWwwRoot, "Piede carta intestata MAREA.png");
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(header is null ? 1.5 : 3.6);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(footer is null ? 1.5 : 2.4);
        sec.PageSetup.HeaderDistance = Unit.FromCentimeter(0.6);
        sec.PageSetup.FooterDistance = Unit.FromCentimeter(0.4);
        if (header is not null)
        {
            var hp = sec.Headers.Primary.AddParagraph(); hp.Format.Alignment = ParagraphAlignment.Center;
            var img = hp.AddImage(header); img.LockAspectRatio = true; img.Width = Unit.FromCentimeter(16.5);
        }
        if (footer is not null)
        {
            var fp = sec.Footers.Primary.AddParagraph(); fp.Format.Alignment = ParagraphAlignment.Center;
            var img = fp.AddImage(footer); img.LockAspectRatio = true; img.Width = Unit.FromCentimeter(17);
        }
        var pie = sec.Footers.Primary.AddParagraph();
        pie.Format.Alignment = ParagraphAlignment.Center; pie.Format.Font.Size = 7;
        pie.AddText($"Ricevuta n. {d.NumeroRicevuta} – Pagina "); pie.AddPageField(); pie.AddText(" di "); pie.AddNumPagesField();

        var t = sec.AddParagraph($"RICEVUTA DI APPROVAZIONE N. {d.NumeroRicevuta}");
        t.Format.Alignment = ParagraphAlignment.Center; t.Format.Font.Bold = true; t.Format.Font.Size = 13; t.Format.SpaceAfter = "0.1cm";
        var st = sec.AddParagraph("Approvazione delle Condizioni Generali di Fornitura" + (d.Offerta is null ? "" : " e accettazione dell'Offerta") + " tramite il Portale clienti MARE.A");
        st.Format.Alignment = ParagraphAlignment.Center; st.Format.Font.Size = 9; st.Format.SpaceAfter = "0.4cm";

        var dati = sec.AddTable();
        dati.Borders.Width = 0.5; dati.Borders.Color = new Color(0xFFCCCCCC);
        dati.AddColumn(Unit.FromCentimeter(4.5)); dati.AddColumn(Unit.FromCentimeter(12.5));
        Riga(dati, "Cliente", d.RagioneSociale + (string.IsNullOrWhiteSpace(d.PartitaIva) ? "" : $" – P. IVA {d.PartitaIva}"));
        Riga(dati, "Firmatario", $"{d.NomeCognome}, {d.Qualifica} – {d.Email}");
        Riga(dati, "Condizioni Generali", $"{d.Revisione}\nImpronta SHA-256 del PDF: {Spezza(d.ImprontaCondizioni)}");
        if (d.Offerta is not null) Riga(dati, "Offerta", $"n. {d.Offerta}\nImpronta SHA-256 del PDF: {Spezza(d.ImprontaOfferta)}");
        Riga(dati, "Approvata il", d.DataApprovazione.ToString("dd/MM/yyyy 'alle ore' HH:mm:ss", It));
        Riga(dati, "Modalità", d.Modalita);
        Riga(dati, "Indirizzo IP", d.IndirizzoIp ?? "—");

        Titolo(sec, "Casella 1 – Accettazione" + Ora(d.OraCasella1));
        Testo(sec, d.Casella1);
        if (d.Firma1 is not null) Firma(sec, d.Firma1, "Firma per accettazione");

        Titolo(sec, "Casella 2 – Approvazione specifica (artt. 1341-1342 c.c.)" + Ora(d.OraCasella2));
        Testo(sec, d.Casella2Introduzione);
        foreach (var c in d.Clausole)
        {
            var p = sec.AddParagraph("– " + c);
            p.Format.LeftIndent = Unit.FromCentimeter(0.5); p.Format.Font.Size = 8.5; p.Format.SpaceAfter = "0.05cm";
        }
        if (d.Firma2 is not null) Firma(sec, d.Firma2, "Firma per approvazione specifica");
        if (d.FirmaOtp is not null)
        {
            var f = sec.AddParagraph(d.FirmaOtp);
            f.Format.Font.Size = 8.5; f.Format.Font.Bold = true; f.Format.SpaceBefore = "0.15cm";
        }

        Titolo(sec, "Casella 3 – Poteri" + Ora(d.OraCasella3));
        Testo(sec, d.Casella3);

        var imp = sec.AddParagraph($"Impronta SHA-256 dei testi approvati: {d.ImprontaTesti}");
        imp.Format.Font.Size = 7.5; imp.Format.SpaceBefore = "0.3cm";

        var nota = sec.AddParagraph("La presente ricevuta attesta l'avvenuta approvazione tramite il Portale clienti MARE.A. Copia dei documenti approvati " +
            "è conservata da MARE.A s.r.l. e resta consultabile nel Portale. Di ogni passo dell'approvazione (apertura dei documenti, spunta delle caselle, " +
            "conferme, invio e verifica del codice) è conservata evidenza in un registro a sola aggiunta con impronte concatenate.");
        nota.Format.Font.Size = 8; nota.Format.SpaceBefore = "0.3cm"; nota.Format.Alignment = ParagraphAlignment.Justify;
        var prom = sec.AddParagraph("Si ricorda che i Rapporti di Prova devono essere scaricati e conservati dal Cliente entro 30 giorni dalla pubblicazione (Art. 9.8).");
        prom.Format.Font.Size = 8; prom.Format.Font.Bold = true; prom.Format.SpaceBefore = "0.2cm";

        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        using var ms = new MemoryStream();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>Impronta in due gruppi da 32 caratteri, così va a capo dentro la cella.</summary>
    private static string Spezza(string? h) => h is { Length: 64 } ? h[..32] + " " + h[32..] : h ?? "—";

    private static string Ora(DateTime? ora) => ora is DateTime o ? $" · spuntata alle {o.ToString("HH:mm:ss", It)} del {o.ToString("dd/MM/yyyy", It)}" : "";

    private static void Riga(Table t, string etichetta, string? valore)
    {
        var r = t.AddRow();
        r.Cells[0].Shading.Color = new Color(0xFFF4F4F4);
        r.Cells[0].AddParagraph(etichetta).Format.Font.Bold = true;
        var p = r.Cells[1].AddParagraph();
        var righe = (string.IsNullOrWhiteSpace(valore) ? "—" : valore).Split('\n');
        for (var i = 0; i < righe.Length; i++) { if (i > 0) p.AddLineBreak(); p.AddText(righe[i]); }
    }

    private static void Titolo(Section s, string testo)
    {
        var p = s.AddParagraph(testo);
        p.Format.Font.Bold = true; p.Format.SpaceBefore = "0.4cm"; p.Format.SpaceAfter = "0.1cm";
        p.Format.Shading.Color = new Color(0xFFEEEEEE);
    }

    private static void Testo(Section s, string testo)
    {
        var p = s.AddParagraph(testo);
        p.Format.Alignment = ParagraphAlignment.Justify; p.Format.SpaceAfter = "0.1cm";
    }

    private static void Firma(Section s, byte[] png, string etichetta)
    {
        var e = s.AddParagraph(etichetta + ":");
        e.Format.Font.Size = 8; e.Format.SpaceBefore = "0.15cm";
        var p = s.AddParagraph();
        var img = p.AddImage("base64:" + Convert.ToBase64String(png));
        img.LockAspectRatio = true; img.Width = Unit.FromCentimeter(7);
    }

    private static string? Immagine(string? percorsoWwwRoot, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoWwwRoot)) return null;
        var full = Path.Combine(percorsoWwwRoot, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }
}
