using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace LaboratorioAI.Documenti.Dvr;

/// <summary>
/// Interventi conclusivi sul documento generato.
///
/// Raccoglie le correzioni che rendono il file consegnabile: aggiornamento
/// dell'indice, rimozione delle sezioni non pertinenti e delle pagine
/// rimaste vuote.
/// </summary>
public static class RifinituraDocumento
{
    // ═══════════════════════════════════════════════════════════════
    // INDICE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Segnala a Word di ricostruire i campi all'apertura.
    ///
    /// L'indice generale è un campo calcolato: senza questa impostazione
    /// il documento mostrerebbe l'indice del modello, con numeri di pagina
    /// e titoli non corrispondenti al contenuto effettivo.
    /// </summary>
    public static void RichiediAggiornamentoIndice(WordprocessingDocument documento)
    {
        var principale = documento.MainDocumentPart;
        if (principale is null) return;

        var impostazioni = principale.DocumentSettingsPart
            ?? principale.AddNewPart<DocumentSettingsPart>();

        impostazioni.Settings ??= new Settings();

        // Evita il duplicato se il modello lo prevede già
        if (impostazioni.Settings.Elements<UpdateFieldsOnOpen>().Any()) return;

        impostazioni.Settings.AppendChild(new UpdateFieldsOnOpen { Val = true });
        impostazioni.Settings.Save();
    }

    // ═══════════════════════════════════════════════════════════════
    // SEZIONI CONDIZIONATE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Rimuove dal documento le sezioni non pertinenti alla struttura.
    ///
    /// Le sezioni condizionate sono racchiuse in controlli contenuto il cui
    /// tag inizia con <c>se_</c> seguito dal nome della condizione. Quando
    /// la condizione è falsa l'intero controllo viene eliminato; quando è
    /// vera il controllo viene sciolto lasciando il solo contenuto.
    /// </summary>
    public static IReadOnlyList<string> ApplicaSezioni(
        WordprocessingDocument documento,
        IReadOnlyDictionary<string, bool> condizioni)
    {
        const string prefisso = "se_";
        var applicate = new List<string>();

        var sezioni = ControlliContenuto.Tutti(documento)
            .Where(sdt => ControlliContenuto.Tag(sdt)?
                .StartsWith(prefisso, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        foreach (var sezione in sezioni)
        {
            var tag = ControlliContenuto.Tag(sezione)!;
            var nome = NormalizzazioneTesto.Chiave(tag[prefisso.Length..]);

            // Una condizione non prevista si considera falsa: è preferibile
            // omettere una sezione piuttosto che includerne una non pertinente
            var mantenere = condizioni.TryGetValue(nome, out var valore) && valore;

            if (mantenere)
            {
                ControlliContenuto.Sciogli(sezione);
                applicate.Add(nome);
            }
            else
            {
                RimuoviConParagrafi(sezione);
            }
        }

        return applicate;
    }

    /// <summary>
    /// Elimina il controllo e il paragrafo che lo contiene se resterebbe vuoto.
    /// Senza questo accorgimento la rimozione lascia righe vuote che si
    /// accumulano fino a produrre pagine bianche.
    /// </summary>
    private static void RimuoviConParagrafi(SdtElement sdt)
    {
        var paragrafoContenitore = sdt.Ancestors<Paragraph>().FirstOrDefault();
        sdt.Remove();

        if (paragrafoContenitore is not null && ParagrafoVuoto(paragrafoContenitore))
            paragrafoContenitore.Remove();
    }

    // ═══════════════════════════════════════════════════════════════
    // PAGINE VUOTE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Elimina le pagine rimaste vuote dopo la rimozione delle sezioni.
    ///
    /// Una pagina vuota si riconosce da un'interruzione di pagina preceduta
    /// solo da paragrafi privi di contenuto. La rimozione procede a ritroso
    /// per non invalidare le posizioni durante l'iterazione.
    /// </summary>
    public static int RimuoviPagineVuote(WordprocessingDocument documento)
    {
        var corpo = documento.MainDocumentPart?.Document?.Body;
        if (corpo is null) return 0;

        var rimossi = 0;
        var elementi = corpo.ChildElements.ToList();

        for (var i = elementi.Count - 1; i >= 0; i--)
        {
            if (elementi[i] is not Paragraph paragrafo) continue;
            if (!ContieneInterruzionePagina(paragrafo)) continue;

            // Risale finché trova paragrafi vuoti, che vengono eliminati
            var precedente = i - 1;
            var vuotiConsecutivi = 0;

            while (precedente >= 0 &&
                   elementi[precedente] is Paragraph p &&
                   ParagrafoVuoto(p))
            {
                vuotiConsecutivi++;
                precedente--;
            }

            // Due o più paragrafi vuoti prima di un salto pagina indicano
            // uno spazio rimasto dopo la rimozione di una sezione
            if (vuotiConsecutivi >= 2)
            {
                for (var j = i - 1; j > i - 1 - vuotiConsecutivi; j--)
                {
                    elementi[j].Remove();
                    rimossi++;
                }
            }
        }

        return rimossi;
    }

    private static bool ContieneInterruzionePagina(Paragraph paragrafo) =>
        paragrafo.Descendants<Break>()
            .Any(b => b.Type?.Value == BreakValues.Page)
        || paragrafo.ParagraphProperties?.PageBreakBefore is not null;

    private static bool ParagrafoVuoto(Paragraph paragrafo)
    {
        if (paragrafo.Descendants<Drawing>().Any()) return false;
        if (paragrafo.Descendants<SdtElement>().Any()) return false;
        if (ContieneInterruzionePagina(paragrafo)) return false;

        var testo = string.Concat(paragrafo.Descendants<Text>().Select(t => t.Text));
        return string.IsNullOrWhiteSpace(testo);
    }

    // ═══════════════════════════════════════════════════════════════
    // PROPRIETÀ DEL FILE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Compila le proprietà del documento, utili per l'archiviazione.</summary>
    public static void ImpostaProprieta(
        WordprocessingDocument documento, string titolo, string oggetto)
    {
        var proprieta = documento.PackageProperties;

        proprieta.Title = titolo;
        proprieta.Subject = oggetto;
        proprieta.Creator = "MARE.A srl — Laboratorio AI";
        proprieta.Created = DateTime.Now;
        proprieta.Modified = DateTime.Now;
        proprieta.LastModifiedBy = "Laboratorio AI";
    }
}
