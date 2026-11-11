using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Wordprocessing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Documenti.Dvr;
using A = DocumentFormat.OpenXml.Drawing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// Le firme nei documenti (11/11/2026).
///
/// Una firma e' un PNG che si infila nel controllo <c>FIRMA:</c>, con la
/// larghezza fissa del motore e l'altezza in proporzione, cosi' la firma
/// non esce schiacciata.
///
/// Dove la firma manca il comportamento e' quello del motore: per il
/// consulente si svuota il riquadro — il documento esce senza la riga
/// del consulente invece che con un segnaposto — mentre per il legale
/// rappresentante si lascia il modello com'e', perche' li' sotto c'e' la
/// riga su cui firmera' a mano.
///
/// Si conserva solo l'immagine. Niente pressione, velocita' o tempi:
/// non e' firma grafometrica e non si trattano dati biometrici.
/// </summary>
internal static class FirmeDocumento
{
    private const string Prefisso = "FIRMA:";

    /// <summary>Larghezza della firma: 1.440.000 EMU, come nel motore (circa 3,8 cm).</summary>
    private const long Larghezza = 1_440_000;

    private static int _progressivo = 1;

    /// <summary>Mette le firme disponibili nei controlli che le chiedono.</summary>
    /// <param name="documento">Il documento in lavorazione.</param>
    /// <param name="legaleRappresentante">PNG della firma del cliente, se raccolta.</param>
    /// <param name="consulente">PNG della firma di chi di MARE.A segue il cliente.</param>
    public static void Applica(
        WordprocessingDocument documento, byte[]? legaleRappresentante, byte[]? consulente)
    {
        var parte = documento.MainDocumentPart;
        if (parte is null) return;

        foreach (var sdt in ControlliContenuto.Tutti(documento).ToList())
        {
            var tag = ControlliContenuto.Tag(sdt);
            if (tag is null || !tag.StartsWith(Prefisso, StringComparison.Ordinal)) continue;

            var nome = tag[Prefisso.Length..];
            var perConsulente = string.Equals(nome, "CONSULENTE", StringComparison.Ordinal);
            var immagine = perConsulente ? consulente : legaleRappresentante;

            if (immagine is null || immagine.Length == 0)
            {
                if (perConsulente) Svuota(sdt);
                continue;
            }

            Inserisci(parte, sdt, immagine);
        }
    }

    /// <summary>Toglie il testo del riquadro lasciando la formattazione.</summary>
    private static void Svuota(SdtElement sdt)
    {
        foreach (var riga in sdt.Descendants<Run>())
            foreach (var figlio in riga.ChildElements.ToList())
                if (figlio is not RunProperties) figlio.Remove();
    }

    /// <summary>Sostituisce il contenuto del riquadro con la firma.</summary>
    private static void Inserisci(MainDocumentPart parte, SdtElement sdt, byte[] png)
    {
        var immagine = parte.AddImagePart(ImagePartType.Png);
        using (var flusso = new MemoryStream(png)) immagine.FeedData(flusso);

        var (larghezza, altezza) = Dimensioni(png);
        var cx = Larghezza;
        var cy = altezza > 0 && larghezza > 0
            ? (long)Math.Round((double)Larghezza * altezza / larghezza)
            : Larghezza / 3;

        var riga = Figura(parte.GetIdOfPart(immagine), cx, cy);

        // Si tiene la formattazione della riga che c'era: il riquadro ha
        // un allineamento suo, e perderlo sposta la firma.
        var vecchia = sdt.Descendants<Run>().FirstOrDefault();
        if (vecchia?.GetFirstChild<RunProperties>() is { } proprieta)
            riga.InsertAt((RunProperties)proprieta.CloneNode(true), 0);

        foreach (var r in sdt.Descendants<Run>().ToList()) r.Remove();

        var paragrafo = sdt.Descendants<Paragraph>().FirstOrDefault();
        if (paragrafo is not null) paragrafo.AppendChild(riga);
        else sdt.AppendChild(new Paragraph(riga));
    }

    /// <summary>La riga di Word che contiene l'immagine.</summary>
    private static Run Figura(string idRelazione, long cx, long cy)
    {
        var numero = (uint)(7000 + _progressivo++);

        return new Run(new Drawing(
            new Inline(
                new Extent { Cx = cx, Cy = cy },
                new DocProperties { Id = numero, Name = "Firma" },
                new A.Graphic(
                    new A.GraphicData(
                        new PIC.Picture(
                            new PIC.NonVisualPictureProperties(
                                new PIC.NonVisualDrawingProperties { Id = 0U, Name = "firma.png" },
                                new PIC.NonVisualPictureDrawingProperties()),
                            new PIC.BlipFill(
                                new A.Blip { Embed = idRelazione },
                                new A.Stretch(new A.FillRectangle())),
                            new PIC.ShapeProperties(
                                new A.Transform2D(
                                    new A.Offset { X = 0L, Y = 0L },
                                    new A.Extents { Cx = cx, Cy = cy }),
                                new A.PresetGeometry(new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            {
                DistanceFromTop = 0U,
                DistanceFromBottom = 0U,
                DistanceFromLeft = 0U,
                DistanceFromRight = 0U
            }));
    }

    /// <summary>
    /// Larghezza e altezza di un PNG, lette dall'intestazione IHDR: sono
    /// otto byte a partire dal sedicesimo, in formato big-endian. Serve
    /// solo la proporzione, non si apre l'immagine.
    /// </summary>
    private static (int Larghezza, int Altezza) Dimensioni(byte[] png)
    {
        if (png.Length < 24) return (0, 0);

        var l = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var a = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];

        return l > 0 && a > 0 ? (l, a) : (0, 0);
    }
}
