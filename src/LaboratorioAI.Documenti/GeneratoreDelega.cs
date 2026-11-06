using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Delega operativa per i consulenti: compila il modello Word (segnaposto {{CHIAVE}}) e ne ricava il PDF.
/// Il testo vive SOLO nel modello Word: per cambiarlo si modifica il file, non il programma.
/// </summary>
public interface IGeneratoreDelega
{
    /// <summary>
    /// Compila il modello Word sostituendo i segnaposto {{CHIAVE}} con i valori. La riga di tabella che
    /// contiene {{SEDE_OPERATIVA}} si ripete per ogni sede indicata (la prima conserva l'etichetta).
    /// </summary>
    byte[] GeneraWord(string percorsoModello, IReadOnlyDictionary<string, string?> valori,
        IReadOnlyList<(string Sede, string? Comune)>? sedi = null);

    /// <summary>
    /// PDF ricavato dal Word compilato (impaginazione semplificata: paragrafi, titoli, elenchi, tabelle),
    /// su carta intestata MARE.A (immagini in wwwroot/img) come gli altri documenti.
    /// </summary>
    byte[] GeneraPdf(byte[] word, string? percorsoWwwRoot = null);

    /// <summary>Segnaposto ancora presenti nel modello che non hanno un valore (per controllo).</summary>
    IReadOnlyList<string> SegnapostoSconosciuti(string percorsoModello, IReadOnlyCollection<string> chiaviNote);
}

/// <inheritdoc />
public class GeneratoreDelega : IGeneratoreDelega
{
    private static readonly Regex Segnaposto = new(@"\{\{([A-Z0-9_]+)\}\}", RegexOptions.Compiled);

    public byte[] GeneraWord(string percorsoModello, IReadOnlyDictionary<string, string?> valori,
        IReadOnlyList<(string Sede, string? Comune)>? sedi = null)
    {
        var modello = File.ReadAllBytes(percorsoModello);
        using var ms = new MemoryStream();
        ms.Write(modello, 0, modello.Length);
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var main = doc.MainDocumentPart ?? throw new InvalidOperationException("Modello Word non valido.");
            if (sedi is { Count: > 0 }) RipetiRigheSedi(main.Document.Body!, sedi);
            Sostituisci(main.Document.Body!, valori);
            foreach (var h in main.HeaderParts) Sostituisci(h.Header, valori);
            foreach (var f in main.FooterParts) Sostituisci(f.Footer, valori);
            main.Document.Save();
        }
        return ms.ToArray();
    }

    public IReadOnlyList<string> SegnapostoSconosciuti(string percorsoModello, IReadOnlyCollection<string> chiaviNote)
    {
        using var doc = WordprocessingDocument.Open(percorsoModello, false);
        var testo = doc.MainDocumentPart?.Document.Body?.InnerText ?? "";
        return Segnaposto.Matches(testo).Select(m => m.Groups[1].Value).Distinct()
            .Where(k => !chiaviNote.Contains(k)).ToList();
    }

    /// <summary>
    /// Ripete la riga con {{SEDE_OPERATIVA}} per ogni sede: nella prima resta l'etichetta della colonna
    /// (es. "SEDE OPERATIVA"), nelle successive la prima cella si svuota. I segnaposto della riga vengono
    /// sostituiti qui con i valori della sede.
    /// </summary>
    private static void RipetiRigheSedi(W.Body body, IReadOnlyList<(string Sede, string? Comune)> sedi)
    {
        var riga = body.Descendants<W.TableRow>().FirstOrDefault(r => r.InnerText.Contains("{{SEDE_OPERATIVA}}"));
        if (riga is null) return;
        var modelloRiga = (W.TableRow)riga.CloneNode(true);
        OpenXmlElement ultima = riga;
        for (var i = 0; i < sedi.Count; i++)
        {
            var r = i == 0 ? riga : (W.TableRow)modelloRiga.CloneNode(true);
            if (i > 0)
            {
                var prima = r.Elements<W.TableCell>().FirstOrDefault();
                foreach (var t in prima?.Descendants<W.Text>() ?? Enumerable.Empty<W.Text>()) t.Text = "";
                ultima.InsertAfterSelf(r);
                ultima = r;
            }
            Sostituisci(r, new Dictionary<string, string?>
            {
                ["SEDE_OPERATIVA"] = sedi[i].Sede,
                ["SEDE_OPERATIVA_COMUNE"] = sedi[i].Comune ?? ""
            });
        }
    }

    /// <summary>
    /// Sostituzione a livello di paragrafo: Word spezza spesso il testo in più "run" (es. "{{RAGIONE_" +
    /// "SOCIALE}}" dopo una modifica del modello), quindi si ricompone il testo del paragrafo, si
    /// sostituisce e lo si rimette nel primo run (ne conserva la formattazione).
    /// </summary>
    private static void Sostituisci(OpenXmlElement radice, IReadOnlyDictionary<string, string?> valori)
    {
        foreach (var p in radice.Descendants<W.Paragraph>().ToList())
        {
            var testi = p.Descendants<W.Text>().ToList();
            if (testi.Count == 0) continue;
            var completo = string.Concat(testi.Select(t => t.Text));
            if (!completo.Contains("{{")) continue;
            var nuovo = Segnaposto.Replace(completo, m =>
                valori.TryGetValue(m.Groups[1].Value, out var v) ? (v ?? "") : m.Value);
            testi[0].Text = nuovo;
            testi[0].Space = SpaceProcessingModeValues.Preserve;
            foreach (var t in testi.Skip(1)) t.Text = "";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // PDF dal Word (impaginazione semplificata con MigraDoc)
    // ═══════════════════════════════════════════════════════════════

    public byte[] GeneraPdf(byte[] word, string? percorsoWwwRoot = null) =>
        ConvertitoreWordPdf.Converti(word, percorsoWwwRoot, "Pagina ");
}
