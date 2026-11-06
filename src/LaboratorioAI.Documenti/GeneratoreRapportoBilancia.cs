using System.Globalization;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using MigraDoc.RtfRendering;
using SkiaSharp;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Rapporto di Taratura di una bilancia (Modulo 45, EURAMET cg-18) in PDF e Word:
/// tabelle (eccentricità, ripetibilità, errori d'indicazione con incertezza) e il
/// grafico dell'errore d'indicazione con banda d'incertezza estesa.
/// </summary>
public interface IGeneratoreRapportoBilancia
{
    byte[] GeneraPdf(DatiRapportoBilancia dati, string? percorsoWwwRoot = null);
    byte[] GeneraWord(DatiRapportoBilancia dati, string? percorsoWwwRoot = null);
    string NomeFile(DatiRapportoBilancia dati, string estensione);
}

/// <inheritdoc />
public class GeneratoreRapportoBilancia : IGeneratoreRapportoBilancia
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    public byte[] GeneraPdf(DatiRapportoBilancia d, string? percorsoWwwRoot = null)
    {
        var (doc, tmp) = Costruisci(d, percorsoWwwRoot);
        try
        {
            var renderer = new PdfDocumentRenderer { Document = doc };
            renderer.RenderDocument();
            using var ms = new MemoryStream();
            renderer.PdfDocument.Save(ms, false);
            return ms.ToArray();
        }
        finally { Pulisci(tmp); }
    }

    public byte[] GeneraWord(DatiRapportoBilancia d, string? percorsoWwwRoot = null)
    {
        var (doc, tmp) = Costruisci(d, percorsoWwwRoot);
        var rtf = Path.Combine(Path.GetTempPath(), "labai_bil_" + Guid.NewGuid().ToString("N") + ".rtf");
        try
        {
            new RtfDocumentRenderer().Render(doc, rtf, Path.GetTempPath());
            return File.ReadAllBytes(rtf);
        }
        finally { if (File.Exists(rtf)) File.Delete(rtf); Pulisci(tmp); }
    }

    public string NomeFile(DatiRapportoBilancia d, string estensione)
    {
        var nome = NomiFileRapporti.Slug(d.BilanciaIdentificazione, "Bilancia");
        var cli = NomiFileRapporti.Slug(d.Committente, "");
        return $"Rapporto_Taratura_Bilancia{(cli.Length == 0 ? "" : "_" + cli)}_{nome}_{d.DataTaratura:yyyyMMdd}.{estensione}";
    }

    private static void Pulisci(string? f) { if (f != null && File.Exists(f)) File.Delete(f); }

    private static (Document, string? Grafico) Costruisci(DatiRapportoBilancia d, string? percorsoWwwRoot)
    {
        var r = d.Risultato;
        var doc = new Document();
        var normale = doc.Styles["Normal"]!;
        normale.Font.Name = "Arial"; normale.Font.Size = 9;

        var sec = doc.AddSection();
        CartaIntestataRapporti.Applica(sec, percorsoWwwRoot, margineLato: 1.5, margineMinimo: 0.8);   // 03/11/2026: come i termometri

        var tit = sec.AddParagraph("RAPPORTO DI TARATURA — BILANCIA");
        tit.Format.Alignment = ParagraphAlignment.Center; tit.Format.Font.Bold = true; tit.Format.Font.Size = 13;
        tit.Format.SpaceBefore = "0.25cm";
        var sub = sec.AddParagraph("Modulo Statistico 45 — secondo EURAMET Calibration Guide N. 18");
        sub.Format.Alignment = ParagraphAlignment.Center; sub.Format.Font.Size = 8; sub.Format.SpaceAfter = "0.2cm";

        var t = TabellaInfo(sec);
        RigaInfo(t, "Operatore", d.Operatore ?? d.RedattoDa ?? "—", "Identificativo rapporto", d.NumeroRapporto);
        RigaInfo(t, "Rapporto del", d.DataTaratura.ToString("dd/MM/yyyy", It),
            "Scadenza taratura", d.DataScadenza is DateOnly s ? s.ToString("dd/MM/yyyy", It) : "—");
        RigaInfo(t, "Procedura", d.RiferimentoIstruzione ?? "—", "Committente", CartaIntestataRapporti.Committente(d.Committente));
        if (!string.IsNullOrWhiteSpace(d.Insediamento)) RigaInfo(t, "Insediamento", d.Insediamento!, "", "");

        Titolo(sec, "Bilancia in taratura");
        var b = TabellaInfo(sec);
        RigaInfo(b, "Identificazione", d.BilanciaIdentificazione, "Modello", d.BilanciaModello ?? "—");
        RigaInfo(b, "Tipo", d.Tipo ?? "—", "SDA", d.Sda ?? "—");
        RigaInfo(b, "Portata strumento", $"{Num(d.Portata, 2)} g", "Campo di taratura (0 a)", $"{Num(d.CampoTaratura, 2)} g");
        RigaInfo(b, "Unità di formato", $"{Num(d.UnitaFormato, 4)} g",
            "Coeff. sens. termica", $"{d.CoefficienteSensibilitaTermica.ToString("0.###E+0", It)} 1/°C");
        RigaInfo(b, "Tempo stabilizzazione", $"{Num(d.TempoStabilizzazione, 0)} s",
            "Intervallo di lettura", $"{Num(d.TempoIntervalloLettura, 0)} s");
        RigaInfo(b, "Pesiera", d.PesieraIdentificazione ?? "—", "Misure eseguite il", d.DataTaratura.ToString("dd/MM/yyyy", It));

        RigaInfo(b, "Termometro utilizzato", d.TermometroUtilizzato ?? "—",
            "Temp. taratura / utilizzo", $"{Num(d.TemperaturaTaratura, 1)} / {Num(d.TemperaturaUtilizzo, 1)} °C");

        // ── Prova di eccentricità (pagina 1) ──
        Titolo(sec, "Prova di eccentricità");
        if (d.Eccentricita is ProvaEccentricita ecc && ecc.Letture.Count > 0)
        {
            var te = sec.AddTable();
            te.Borders.Width = 0.5; te.Borders.Color = new Color(0xFFBBBBBB);
            te.Format.Font.Size = 8.5; te.Format.SpaceAfter = "0.05cm";
            te.AddColumn(Unit.FromCentimeter(5.0)); te.AddColumn(Unit.FromCentimeter(5.0));
            var teh = te.AddRow(); teh.Format.Font.Bold = true; teh.Shading.Color = new Color(0xFFF3F3F3);
            teh.Cells[0].AddParagraph($"Posizione (carico {Num(ecc.CaricoNominale, 1)} g)");
            teh.Cells[1].AddParagraph("Lettura [g]").Format.Alignment = ParagraphAlignment.Right;
            for (int i = 0; i < ecc.Letture.Count; i++)
            {
                var row = te.AddRow();
                row.Cells[0].AddParagraph($"Posizione n°{i + 1}");
                row.Cells[1].AddParagraph(Num(ecc.Letture[i], 4)).Format.Alignment = ParagraphAlignment.Right;
            }
        }
        var ecInfo = TabellaInfo(sec);
        RigaInfo(ecInfo, "Max differenza dovuta all'eccentricità", $"{Num(r.EccentricitaScostamentoMax, 5)} g",
            "Media letture", $"{Num(r.EccentricitaMedia, 5)} g");
        var giudEcc = sec.AddParagraph(r.EccentricitaConforme
            ? "La prova di decentramento del carico è positiva: la massima differenza dovuta all'eccentricità non supera le 15 divisioni."
            : "La prova di decentramento NON è positiva: la differenza dovuta all'eccentricità supera le 15 divisioni.");
        giudEcc.Format.Font.Italic = true; giudEcc.Format.Font.Size = 8.5;
        giudEcc.Format.Font.Color = r.EccentricitaConforme ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);

        // ── Prova di ripetibilità ──
        Titolo(sec, "Prova di ripetibilità");
        if (d.Ripetibilita.Count > 0)
        {
            var tr = sec.AddTable();
            tr.Borders.Width = 0.5; tr.Borders.Color = new Color(0xFFBBBBBB);
            tr.Format.Font.Size = 8.5; tr.Format.SpaceAfter = "0.05cm";
            tr.AddColumn(Unit.FromCentimeter(5.0)); tr.AddColumn(Unit.FromCentimeter(5.0));
            var trh = tr.AddRow(); trh.Format.Font.Bold = true; trh.Shading.Color = new Color(0xFFF3F3F3);
            trh.Cells[0].AddParagraph("n."); trh.Cells[1].AddParagraph("Lettura L [g]").Format.Alignment = ParagraphAlignment.Right;
            for (int i = 0; i < d.Ripetibilita.Count; i++)
            {
                var row = tr.AddRow();
                row.Cells[0].AddParagraph((i + 1).ToString());
                row.Cells[1].AddParagraph(Num(d.Ripetibilita[i], 4)).Format.Alignment = ParagraphAlignment.Right;
            }
            var rr = tr.AddRow(); rr.Format.Font.Bold = true;
            rr.Cells[0].AddParagraph("Media");
            rr.Cells[1].AddParagraph(Num(d.Ripetibilita.Average(), 4)).Format.Alignment = ParagraphAlignment.Right;
            var rmax = tr.AddRow(); rmax.Cells[0].AddParagraph("Massimo");
            rmax.Cells[1].AddParagraph(Num(d.Ripetibilita.Max(), 4)).Format.Alignment = ParagraphAlignment.Right;
            var rmin = tr.AddRow(); rmin.Cells[0].AddParagraph("Minimo");
            rmin.Cells[1].AddParagraph(Num(d.Ripetibilita.Min(), 4)).Format.Alignment = ParagraphAlignment.Right;
        }
        var ripInfo = TabellaInfo(sec);
        RigaInfo(ripInfo, "Incertezza di ripetibilità u(rep)", $"{Num(r.RipetibilitaScartoTipo, 6)} g",
            "Incertezza composta (u rep+dig)", $"{Num(r.RipetibilitaComposta, 6)} g");

        // ── Grafico dell'incertezza/errore d'indicazione (prima del giudizio) ──
        string? grafico = DisegnaGrafico(r);
        if (grafico != null)
        {
            var tg = sec.AddParagraph("Errore d'indicazione con banda d'incertezza estesa");
            tg.Format.Font.Bold = true; tg.Format.Font.Size = 8; tg.Format.SpaceBefore = "0.15cm";
            var img = sec.AddImage(grafico);
            img.LockAspectRatio = true;
            img.Width = Unit.FromCentimeter(11);
        }

        // ── Giudizio di conformità (fine pagina 1) ──
        var giudizio = sec.AddParagraph(r.Conforme
            ? "LA TARATURA È CONFORME ai criteri del laboratorio."
            : "LA TARATURA NON È CONFORME ai criteri del laboratorio.");
        giudizio.Format.SpaceBefore = "0.25cm"; giudizio.Format.Font.Bold = true;
        giudizio.Format.Font.Color = r.Conforme ? new Color(0xFF1A7F37) : new Color(0xFFB3261E);
        giudizio.Format.Shading.Color = new Color(0xFFF3F3F3);

        var criteri = sec.AddParagraph(
            "La taratura è ritenuta conforme secondo i criteri stabiliti dal laboratorio quando il max valore di "
            + "incertezza estesa U risulta inferiore al 2% del carico minimo testato nella prova di linearità. "
            + "La prova di eccentricità è ritenuta superata quando la max differenza dovuta all'eccentricità non supera le 15 divisioni.");
        criteri.Format.Font.Italic = true; criteri.Format.Font.Size = 8; criteri.Format.SpaceBefore = "0.1cm";

        // ── Pagina 2: incertezza dell'errore, pesiera utilizzata, note ──
        var p2 = sec.AddParagraph("RAPPORTO DI TARATURA BILANCIA — dettaglio taratura");
        p2.Format.PageBreakBefore = true;
        p2.Format.Font.Bold = true; p2.Format.Font.Size = 11; p2.Format.SpaceAfter = "0.15cm";
        var b2 = TabellaInfo(sec);
        RigaInfo(b2, "Bilancia", d.BilanciaIdentificazione, "Rapporto n°", d.NumeroRapporto);
        RigaInfo(b2, "Termometro di riferimento", d.TermometroUtilizzato ?? "—",
            "Temp. taratura / utilizzo", $"{Num(d.TemperaturaTaratura, 1)} / {Num(d.TemperaturaUtilizzo, 1)} °C");

        // Tabella incertezza dell'errore d'indicazione (linearità)
        Titolo(sec, "Correzioni della lettura e incertezza estesa (errore d'indicazione)");
        var ei = sec.AddTable();
        ei.Borders.Width = 0.5; ei.Borders.Color = new Color(0xFFBBBBBB);
        ei.Format.Font.Size = 8; ei.Format.SpaceAfter = "0.05cm";
        foreach (var w in new[] { 2.7, 3.3, 2.7, 2.9, 2.7, 2.7 }) ei.AddColumn(Unit.FromCentimeter(w));
        var hr = ei.AddRow(); hr.Format.Font.Bold = true; hr.Shading.Color = new Color(0xFFF3F3F3);
        foreach (var (txt, i) in new[] { "Carico nom. [g]", "Valore conv. [g]", "Lettura [g]", "Errore ΔM [g]", "Incert. U [g]", "U d'uso [g]" }.Select((x, i) => (x, i)))
            hr.Cells[i].AddParagraph(txt).Format.Alignment = ParagraphAlignment.Center;
        foreach (var p in r.Punti)
        {
            var row = ei.AddRow();
            row.Cells[0].AddParagraph(Num(p.CaricoNominale, 1)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[1].AddParagraph(Num(p.ValoreConvenzionale, 4)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[2].AddParagraph(Num(p.Lettura, 4)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[3].AddParagraph(Num(p.Errore, 5)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[4].AddParagraph(Num(p.IncertezzaEstesa, 5)).Format.Alignment = ParagraphAlignment.Right;
            row.Cells[5].AddParagraph(Num(p.IncertezzaEstesaUso, 5)).Format.Alignment = ParagraphAlignment.Right;
        }
        var maxInfo = TabellaInfo(sec);
        RigaInfo(maxInfo, "Incertezza estesa massima", $"{Num(r.IncertezzaEstesaMax, 5)} g",
            "Incertezza estesa d'uso massima", $"{Num(r.IncertezzaEstesaUsoMax, 5)} g");

        // Tabella pesiera utilizzata
        if (d.Pesiera.Count > 0)
        {
            Titolo(sec, $"Pesiera utilizzata{(string.IsNullOrWhiteSpace(d.PesieraIdentificazione) ? "" : " — " + d.PesieraIdentificazione)}");
            var pt = sec.AddTable();
            pt.Borders.Width = 0.5; pt.Borders.Color = new Color(0xFFBBBBBB);
            pt.Format.Font.Size = 7.5; pt.Format.SpaceAfter = "0.05cm";
            foreach (var w in new[] { 1.9, 2.6, 1.9, 1.9, 2.5, 2.1, 2.1, 1.6 }) pt.AddColumn(Unit.FromCentimeter(w));
            var ph = pt.AddRow(); ph.Format.Font.Bold = true; ph.Shading.Color = new Color(0xFFF3F3F3);
            foreach (var (txt, i) in new[] { "Nom. [g]", "V. conv. [g]", "U [mg]", "u [mg]", "Cert. LAT", "Data cert.", "Scadenza", "Centro" }.Select((x, i) => (x, i)))
                ph.Cells[i].AddParagraph(txt).Format.Alignment = ParagraphAlignment.Center;
            foreach (var m in d.Pesiera)
            {
                var row = pt.AddRow();
                row.Cells[0].AddParagraph(Num(m.ValoreNominale, 1)).Format.Alignment = ParagraphAlignment.Right;
                row.Cells[1].AddParagraph(Num(m.ValoreConvenzionale, 7)).Format.Alignment = ParagraphAlignment.Right;
                row.Cells[2].AddParagraph(Num(m.IncertezzaEstesa, 5)).Format.Alignment = ParagraphAlignment.Right;
                row.Cells[3].AddParagraph(Num(m.IncertezzaTipo, 5)).Format.Alignment = ParagraphAlignment.Right;
                row.Cells[4].AddParagraph(m.CertificatoLat ?? "—");
                row.Cells[5].AddParagraph(m.DataCertificato is DateTime dc ? dc.ToString("dd/MM/yy", It) : "—").Format.Alignment = ParagraphAlignment.Center;
                row.Cells[6].AddParagraph(m.DataScadenza is DateTime ds2 ? ds2.ToString("dd/MM/yy", It) : "—").Format.Alignment = ParagraphAlignment.Center;
                row.Cells[7].AddParagraph(m.CentroLat?.ToString() ?? "—").Format.Alignment = ParagraphAlignment.Center;
            }
        }

        // ── Note esplicative in corsivo ──
        var noteTit = sec.AddParagraph("Note esplicative:");
        noteTit.Format.Font.Bold = true; noteTit.Format.Font.Size = 8; noteTit.Format.SpaceBefore = "0.3cm";
        var noteEsplicative = new[]
        {
            "1. La taratura di cui sono riportati i risultati vale nelle condizioni operative ed ambientali riscontrate durante le prove. Se la bilancia verrà rimossa i risultati qui riportati non sono più validi.",
            "2. La bilancia è stata tarata in “valore convenzionale di massa”, cioè indicherà la massa di un oggetto di densità 8000 kg m-3 in grado di equilibrare il misurando in aria di densità 1,2 kg m-3, alla temperatura 20 °C.",
            "3. La tabella delle correzioni riporta i valori ΔM = M - L e la corrispondente incertezza estesa di taratura per i valori nominali di misura. Se la densità dell'oggetto in misura non è molto diversa da 8000 kg m-3, data una lettura L si potrà ricavare la massa convenzionale M ad essa equivalente con: M = L + ΔM. Punti di misura non riportati nella tabella devono essere interpolati linearmente.",
            "4. L'incertezza estesa di taratura U è stata valutata ad un livello di fiducia del 95,45 %; essa non coincide con quella d'uso Uu. Per stimare quest'ultima, sapendo la portata dello strumento Mmax, bisogna tenere conto dei fattori ambientali ed operativi variati rispetto alla taratura.",
        };
        foreach (var testo in noteEsplicative)
        {
            var n = sec.AddParagraph(testo);
            n.Format.Font.Size = 7.5; n.Format.Font.Italic = true; n.Format.SpaceAfter = "0.05cm";
        }

        // ── Responsabile della taratura, in fondo a tutto ──
        var firmaLbl = sec.AddParagraph("Responsabile della Taratura");
        firmaLbl.Format.Font.Bold = true; firmaLbl.Format.SpaceBefore = "0.5cm"; firmaLbl.Format.Alignment = ParagraphAlignment.Center;
        var firmaNome = sec.AddParagraph(d.RedattoDa ?? "");
        firmaNome.Format.SpaceBefore = "0.05cm"; firmaNome.Format.Alignment = ParagraphAlignment.Center;

        return (doc, grafico);
    }

    /// <summary>Disegna il grafico errore vs carico con banda ±U in PNG; ritorna il percorso temporaneo.</summary>
    private static string? DisegnaGrafico(RisultatoBilancia r)
    {
        var punti = r.Punti;
        if (punti.Count < 2) return null;

        int W = 900, H = 460, ml = 90, mr = 30, mt = 30, mb = 60;
        double xMin = punti.Min(p => p.CaricoNominale), xMax = punti.Max(p => p.CaricoNominale);
        double yMax = punti.Max(p => Math.Abs(p.Errore) + p.IncertezzaEstesa);
        if (yMax <= 0) yMax = 1e-4;
        yMax *= 1.15;
        double yMin = -yMax;

        float PX(double x) => (float)(ml + (x - xMin) / Math.Max(xMax - xMin, 1e-9) * (W - ml - mr));
        float PY(double y) => (float)(mt + (yMax - y) / (yMax - yMin) * (H - mt - mb));

        using var bmp = new SKBitmap(W, H);
        using var cnv = new SKCanvas(bmp);
        cnv.Clear(SKColors.White);
        using var axis = new SKPaint { Color = SKColors.Black, StrokeWidth = 1.5f, IsAntialias = true };
        using var grid = new SKPaint { Color = new SKColor(0xDD, 0xDD, 0xDD), StrokeWidth = 1 };
        using var band = new SKPaint { Color = new SKColor(0x1A, 0x7F, 0x37, 60), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var linea = new SKPaint { Color = new SKColor(0xC0, 0x00, 0x00), StrokeWidth = 2.2f, IsAntialias = true, Style = SKPaintStyle.Stroke };
        using var dot = new SKPaint { Color = new SKColor(0xC0, 0x00, 0x00), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var testo = new SKPaint { Color = SKColors.Black, IsAntialias = true, TextSize = 16 };

        // Assi e zero
        cnv.DrawLine(PX(xMin), PY(0), PX(xMax), PY(0), grid);
        cnv.DrawLine(ml, mt, ml, H - mb, axis);
        cnv.DrawLine(ml, H - mb, W - mr, H - mb, axis);

        // Banda d'incertezza (superiore ed inferiore)
        var sup = new SKPath(); var inf = new SKPath();
        for (int i = 0; i < punti.Count; i++)
        {
            float x = PX(punti[i].CaricoNominale);
            float yUp = PY(punti[i].Errore + punti[i].IncertezzaEstesa);
            if (i == 0) sup.MoveTo(x, yUp); else sup.LineTo(x, yUp);
        }
        for (int i = punti.Count - 1; i >= 0; i--)
        {
            float x = PX(punti[i].CaricoNominale);
            float yLo = PY(punti[i].Errore - punti[i].IncertezzaEstesa);
            sup.LineTo(x, yLo);
        }
        sup.Close();
        cnv.DrawPath(sup, band);

        // Linea errore + punti
        for (int i = 0; i < punti.Count; i++)
        {
            float x = PX(punti[i].CaricoNominale), y = PY(punti[i].Errore);
            if (i > 0) cnv.DrawLine(PX(punti[i - 1].CaricoNominale), PY(punti[i - 1].Errore), x, y, linea);
            cnv.DrawCircle(x, y, 4, dot);
        }

        // Etichette assi
        cnv.DrawText("Errore [g]", 8, mt + 4, testo);
        cnv.DrawText("Carico [g]", W - mr - 90, H - 18, testo);
        for (int i = 0; i < punti.Count; i++)
            cnv.DrawText(Num(punti[i].CaricoNominale, 0), PX(punti[i].CaricoNominale) - 10, H - mb + 20, testo);

        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 90);
        var path = Path.Combine(Path.GetTempPath(), "labai_bilgraf_" + Guid.NewGuid().ToString("N") + ".png");
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

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
        t.Borders.Width = 0; t.Format.SpaceAfter = "0.05cm"; t.Format.Font.Size = 8.5;
        t.AddColumn(Unit.FromCentimeter(4.3)); t.AddColumn(Unit.FromCentimeter(4.6));
        t.AddColumn(Unit.FromCentimeter(4.3)); t.AddColumn(Unit.FromCentimeter(4.2));
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

    private static string Num(double v, int dec) => v.ToString("N" + dec, It);
}
