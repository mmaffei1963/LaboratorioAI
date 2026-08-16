using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LaboratorioAI.Documenti.Dvr;

/// <summary>
/// Manipolazione dei controlli contenuto strutturati di Word.
///
/// I modelli DVR usano i controlli contenuto per delimitare i punti da
/// compilare. Il riconoscimento avviene per etichetta o per tag quando
/// disponibili; in loro assenza si ricorre all'impronta delle opzioni
/// di un elenco a discesa, che nei modelli storici Ã¨ l'unico elemento
/// distintivo presente.
/// </summary>
public static class ControlliContenuto
{
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // INDIVIDUAZIONE
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>Tutti i controlli contenuto del documento, corpo e intestazioni comprese.</summary>
    public static IEnumerable<SdtElement> Tutti(WordprocessingDocument documento)
    {
        var principale = documento.MainDocumentPart;
        if (principale is null) yield break;

        foreach (var sdt in principale.Document.Descendants<SdtElement>())
            yield return sdt;

        foreach (var intestazione in principale.HeaderParts)
            foreach (var sdt in intestazione.Header.Descendants<SdtElement>())
                yield return sdt;

        foreach (var piede in principale.FooterParts)
            foreach (var sdt in piede.Footer.Descendants<SdtElement>())
                yield return sdt;
    }

    /// <summary>Etichetta del controllo, se assegnata nel modello.</summary>
    public static string? Etichetta(SdtElement sdt) =>
        sdt.SdtProperties?.GetFirstChild<SdtAlias>()?.Val?.Value;

    /// <summary>Tag del controllo, se assegnato nel modello.</summary>
    public static string? Tag(SdtElement sdt) =>
        sdt.SdtProperties?.GetFirstChild<Tag>()?.Val?.Value;

    /// <summary>Opzioni di un elenco a discesa o casella combinata.</summary>
    public static IReadOnlyList<string> Opzioni(SdtElement sdt)
    {
        var proprieta = sdt.SdtProperties;
        if (proprieta is null) return [];

        var elenco = proprieta.GetFirstChild<SdtContentDropDownList>();
        if (elenco is not null)
            return elenco.Elements<ListItem>()
                .Select(v => v.Value?.Value ?? "")
                .Where(v => v.Length > 0)
                .ToList();

        var combinata = proprieta.GetFirstChild<SdtContentComboBox>();
        if (combinata is not null)
            return combinata.Elements<ListItem>()
                .Select(v => v.Value?.Value ?? "")
                .Where(v => v.Length > 0)
                .ToList();

        return [];
    }

    /// <summary>
    /// Impronta di un controllo privo di etichetta e tag.
    ///
    /// Si ricava dall'insieme ordinato delle opzioni: due elenchi con le
    /// stesse voci producono la stessa impronta e sono quindi lo stesso
    /// campo logico, anche se il modello non li ha nominati.
    /// </summary>
    public static string? Impronta(SdtElement sdt)
    {
        var opzioni = Opzioni(sdt);
        if (opzioni.Count == 0) return null;

        var chiavi = opzioni
            .Select(NormalizzazioneTesto.Chiave)
            .Where(k => k.Length > 0)
            .OrderBy(k => k, StringComparer.Ordinal);

        return string.Join('|', chiavi);
    }

    /// <summary>
    /// Identificativo del controllo, nell'ordine di preferenza:
    /// tag, etichetta, impronta delle opzioni.
    /// </summary>
    public static string? Identificativo(SdtElement sdt)
    {
        var tag = Tag(sdt);
        if (!string.IsNullOrWhiteSpace(tag))
            return NormalizzazioneTesto.Chiave(tag);

        var etichetta = Etichetta(sdt);
        if (!string.IsNullOrWhiteSpace(etichetta))
            return NormalizzazioneTesto.Chiave(etichetta);

        var impronta = Impronta(sdt);
        return impronta is null ? null : $"opz:{impronta}";
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // COMPILAZIONE
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    /// <summary>
    /// Scrive un valore nel controllo.
    ///
    /// Il contenuto viene ricostruito conservando la formattazione della
    /// prima esecuzione presente: sostituire l'intero contenuto farebbe
    /// perdere carattere, corpo e stile impostati nel modello.
    /// </summary>
    public static bool Scrivi(SdtElement sdt, string? valore)
    {
        var testo = valore ?? "";
        var contenuto = ContenutoDi(sdt);
        if (contenuto is null) return false;

        RimuoviSegnaposto(sdt);

        var paragrafi = contenuto.Descendants<Paragraph>().ToList();

        // Controllo di blocco: si scrive nel primo paragrafo
        if (paragrafi.Count > 0)
        {
            ScriviInParagrafo(paragrafi[0], testo);

            // I paragrafi eccedenti appartenevano al testo segnaposto
            foreach (var eccedente in paragrafi.Skip(1))
                eccedente.Remove();

            return true;
        }

        // Controllo in linea: si opera direttamente sulle esecuzioni
        return ScriviInEsecuzioni(contenuto, testo);
    }

    private static void ScriviInParagrafo(Paragraph paragrafo, string testo)
    {
        var esecuzioni = paragrafo.Elements<Run>().ToList();

        if (esecuzioni.Count == 0)
        {
            paragrafo.AppendChild(new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve }));
            return;
        }

        ImpostaTesto(esecuzioni[0], testo);

        foreach (var eccedente in esecuzioni.Skip(1))
            eccedente.Remove();
    }

    private static bool ScriviInEsecuzioni(OpenXmlElement contenuto, string testo)
    {
        var esecuzioni = contenuto.Descendants<Run>().ToList();

        if (esecuzioni.Count == 0)
        {
            contenuto.AppendChild(new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve }));
            return true;
        }

        ImpostaTesto(esecuzioni[0], testo);

        foreach (var eccedente in esecuzioni.Skip(1))
            eccedente.Remove();

        return true;
    }

    /// <summary>
    /// Imposta il testo di un'esecuzione gestendo gli a capo.
    /// Word richiede un elemento dedicato per l'interruzione di riga:
    /// un carattere di ritorno a capo verrebbe ignorato.
    /// </summary>
    private static void ImpostaTesto(Run esecuzione, string testo)
    {
        foreach (var elemento in esecuzione.Elements<Text>().ToList()) elemento.Remove();
        foreach (var elemento in esecuzione.Elements<Break>().ToList()) elemento.Remove();

        var righe = testo.Replace("\r\n", "\n").Split('\n');

        for (var i = 0; i < righe.Length; i++)
        {
            if (i > 0) esecuzione.AppendChild(new Break());
            esecuzione.AppendChild(new Text(righe[i]) { Space = SpaceProcessingModeValues.Preserve });
        }
    }

    /// <summary>
    /// Toglie l'indicazione di testo segnaposto.
    ///
    /// Senza questa rimozione Word continua a mostrare il contenuto in
    /// grigio chiaro e a trattarlo come non compilato, anche se il testo
    /// Ã¨ stato sostituito.
    /// </summary>
    private static void RimuoviSegnaposto(SdtElement sdt)
    {
        sdt.SdtProperties?.GetFirstChild<ShowingPlaceholder>()?.Remove();
    }

    /// <summary>Elemento che racchiude il contenuto del controllo.</summary>
    private static OpenXmlElement? ContenutoDi(SdtElement sdt) =>
        (OpenXmlElement?)sdt.GetFirstChild<SdtContentBlock>()
        ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentRun>()
        ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentCell>()
        ?? sdt.GetFirstChild<SdtContentRow>();

    /// <summary>Testo attualmente contenuto nel controllo.</summary>
    public static string Leggi(SdtElement sdt)
    {
        var contenuto = ContenutoDi(sdt);
        if (contenuto is null) return "";

        return string.Concat(contenuto.Descendants<Text>().Select(t => t.Text));
    }

    /// <summary>
    /// Sostituisce il controllo con il proprio contenuto.
    ///
    /// Si applica al termine della compilazione: lasciare i controlli nel
    /// documento consegnato permetterebbe modifiche accidentali e
    /// mostrerebbe i bordi grigi in stampa.
    /// </summary>
    public static void Sciogli(SdtElement sdt)
    {
        var contenuto = ContenutoDi(sdt);
        if (contenuto is null) { sdt.Remove(); return; }

        var genitore = sdt.Parent;
        if (genitore is null) return;

        foreach (var figlio in contenuto.ChildElements.ToList())
        {
            genitore.InsertBefore(figlio.CloneNode(true), sdt);
        }

        sdt.Remove();
    }
}
