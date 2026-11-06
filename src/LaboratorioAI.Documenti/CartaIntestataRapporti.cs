using MigraDoc.DocumentObjectModel;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Carta intestata MARE.A dei rapporti di taratura (termometri, incubatori, mappatura, bilance), 03/11/2026:
/// stessa intestazione e stesso piè di pagina per tutti, presi dalle immagini in wwwroot/img. Se le immagini
/// non ci sono (es. test) si usa l'intestazione testuale di prima.
/// </summary>
internal static class CartaIntestataRapporti
{
    /// <summary>Imposta pagina A4, margini, intestazione e piè di pagina della sezione.</summary>
    public static void Applica(Section sec, string? percorsoWwwRoot, double margineLato = 2.0, double margineMinimo = 1.0)
    {
        var headerImg = Immagine(percorsoWwwRoot, "intestazione_marea_pulita.png")
                        ?? Immagine(percorsoWwwRoot, "intestazione carta intestata MAREA.png");
        var footerImg = Immagine(percorsoWwwRoot, "Piede carta intestata MAREA.png");
        bool cartaIntestata = headerImg is not null || footerImg is not null;

        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(margineLato);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(margineLato);
        sec.PageSetup.TopMargin = Unit.FromCentimeter(cartaIntestata ? 3.4 : margineMinimo);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(cartaIntestata ? 2.2 : margineMinimo);
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
            var h = sec.AddParagraph("MARE.A s.r.l"); h.Format.Font.Bold = true; h.Format.Font.Size = 12;
            Piccolo(sec, "Sede legale e operativa: Via E. Toti, 2 • 47841 Cattolica (RN) • Tel 0541 830442 • Fax 0541 830460");
            Piccolo(sec, "info@mare-a.com • www.mare-a.com • Cod. Fisc. e P. Iva 03670240401 • Laboratorio Analisi");
        }
    }

    /// <summary>Committente da stampare: il cliente, oppure «MARE.A (interno)».</summary>
    public static string Committente(string? committente) =>
        string.IsNullOrWhiteSpace(committente) ? "MARE.A (interno)" : committente.Trim();

    private static void Piccolo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo); p.Format.Font.Size = 7.5;
    }

    private static string? Immagine(string? percorsoWwwRoot, string nomeFile)
    {
        if (string.IsNullOrWhiteSpace(percorsoWwwRoot)) return null;
        var full = Path.Combine(percorsoWwwRoot, "img", nomeFile);
        return File.Exists(full) ? full : null;
    }
}
