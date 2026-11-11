using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Documenti.Dvr;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// I testi alternativi dei modelli (11/11/2026).
///
/// Un marcatore <c>TESTO:</c> non e' un campo da riempire: e' un punto
/// in cui il modello porta con se' due o piu' versioni della stessa
/// frase, e il generatore sceglie quale tenere. Le versioni stanno
/// nell'elenco a discesa del controllo, scritte dal consulente che ha
/// marcato il modello: cosi' chi corregge una frase lo fa in Word, non
/// qui dentro.
///
/// Dove l'elenco non c'e' le frasi sono scritte qui, come nel motore,
/// in italiano e in inglese. L'inglese si riconosce dalla posizione: le
/// informative hanno la versione inglese in coda, dopo il titolo
/// «INFORMATION ON THE PROCESSING».
///
/// Questa versione ne conosce cinque, quelli delle due informative.
/// Un modello che ne usa altri viene rifiutato, non generato a meta'.
/// </summary>
internal static class TestiAlternativi
{
    private const string Prefisso = "TESTO:";
    private const string TitoloInglese = "INFORMATION ON THE PROCESSING";

    /// <summary>I testi che si sanno scegliere.</summary>
    private static readonly IReadOnlySet<string> Conosciuti =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "CONSERVAZIONE_VIDEO", "CONSERVAZIONE_PISCINA",
            "REGISTRAZIONI_AUTORITA_VIDEO", "REGISTRAZIONI_AUTORITA_PISCINA",
            "CATEGORIE_IMMAGINI_VIDEO",
            "PAGAMENTO_BONIFICO", "PAGAMENTO_RIBA"
        };

    /// <summary>I consensi del contratto: il nome dopo il prefisso cambia.</summary>
    private const string Consenso = "CONSENSO_";

    /// <summary>Vero se il marcatore indicato (senza prefisso) si sa trattare.</summary>
    public static bool Conosce(string nome) =>
        Conosciuti.Contains(nome) || nome.StartsWith(Consenso, StringComparison.Ordinal);

    /// <summary>Sceglie e scrive ogni testo alternativo del documento.</summary>
    public static void Applica(
        WordprocessingDocument documento, ContestoGenerazione contesto, List<string> avvisi)
    {
        var inglesi = Inglesi(documento);

        foreach (var sdt in ControlliContenuto.Tutti(documento).ToList())
        {
            var tag = ControlliContenuto.Tag(sdt);
            if (tag is null || !tag.StartsWith(Prefisso, StringComparison.Ordinal)) continue;

            var nome = tag[Prefisso.Length..];
            var scelto = Scegli(nome, sdt, contesto, inglesi.Contains(sdt));

            if (scelto is null)
            {
                avvisi.Add($"Testo alternativo «TESTO:{nome}» non previsto: e' rimasto quello del modello, va riletto.");
                continue;
            }

            ControlliContenuto.Scrivi(sdt, scelto);
        }
    }

    /// <summary>
    /// I controlli che stanno nella parte inglese del documento.
    ///
    /// Si scorre il corpo nell'ordine in cui e' scritto: da quando si
    /// incontra il titolo inglese in poi, tutto quello che segue e' la
    /// traduzione. Intestazioni e pie' di pagina restano in italiano.
    /// </summary>
    private static HashSet<SdtElement> Inglesi(WordprocessingDocument documento)
    {
        var trovati = new HashSet<SdtElement>();
        var corpo = documento.MainDocumentPart?.Document?.Body;
        if (corpo is null) return trovati;

        var dopoIlTitolo = false;

        foreach (var elemento in corpo.Descendants())
        {
            if (!dopoIlTitolo && elemento is Paragraph p
                && p.InnerText.TrimStart().StartsWith(TitoloInglese, StringComparison.Ordinal))
            {
                dopoIlTitolo = true;
                continue;
            }

            if (dopoIlTitolo && elemento is SdtElement sdt) trovati.Add(sdt);
        }

        return trovati;
    }

    /// <summary>La versione da tenere, o null se il testo non e' previsto.</summary>
    private static string? Scegli(
        string nome, SdtElement sdt, ContestoGenerazione contesto, bool inglese)
    {
        if (!Conosce(nome)) return null;

        var ore = contesto.DurataOre;
        var voci = Voci(sdt);

        string? Voce(string modello) =>
            voci.FirstOrDefault(v => Regex.IsMatch(v, modello, RegexOptions.IgnoreCase));

        // Registra l'impianto di cui parla questo testo? I marcatori
        // finiscono per VIDEO o per PISCINA.
        var registra = contesto.Vale(
            nome.EndsWith("VIDEO", StringComparison.Ordinal) ? "VIDEO_REGISTRAZIONE" : "PISCINA_REGISTRAZIONE",
            out _);

        switch (nome)
        {
            case "CONSERVAZIONE_VIDEO":
            case "CONSERVAZIONE_PISCINA":
            {
                // Le due versioni della frase stanno nell'elenco del
                // controllo, con un numero di ore d'esempio da sostituire.
                //
                // Qui ci si discosta dal motore di proposito. Il motore
                // cerca le due voci con le parole «durata massima» e «non
                // sono conservati», che i modelli approvati non usano piu'
                // («massimo 24h…», «non saranno conservati»): non trova
                // niente e lascia il periodo di conservazione in bianco in
                // un'informativa da consegnare. Qui si riconoscono tutte e
                // due le formulazioni, in italiano e in inglese, e se non
                // si riconosce nulla si scrive comunque la frase giusta
                // invece di lasciare il buco. Da riallineare con Vaccarini.
                var scelta = registra
                    ? Voce(@"durata massima|massimo\s*\d+\s*h|maximum of")
                    : Voce(@"non (sono|saranno) conservat|will not be stored");

                if (!string.IsNullOrWhiteSpace(scelta))
                    return registra ? OreNelTesto(scelta, ore) : scelta;

                if (inglese)
                    return registra
                        ? $"a maximum of {ore} hours, followed by automatic deletion. "
                        : "the data collected for this purpose will not be stored.";

                return registra
                    ? $"massimo {ore}h, con successiva cancellazione automatica. "
                    : "i dati raccolti per tale finalità non saranno conservati.";
            }

            case "REGISTRAZIONI_AUTORITA_VIDEO":
            case "REGISTRAZIONI_AUTORITA_PISCINA":
                // Senza registrazione resta uno spazio, non il vuoto: il
                // testo sta in mezzo a una frase.
                if (!registra) return " ";
                return inglese
                    ? "The recordings may also be viewed by the competent public authorities. "
                    : "Le registrazioni possono essere visionate anche dalle Autorità pubbliche competenti in materia. ";

            case "CATEGORIE_IMMAGINI_VIDEO":
                return contesto.Vale("VIDEO_O_PISCINA", out _)
                    ? (inglese ? ", video surveillance images." : ", immagini di videosorveglianza.")
                    : ".";

            // Il modo di pagamento: una crocetta su una delle due caselle.
            case "PAGAMENTO_BONIFICO":
            case "PAGAMENTO_RIBA":
            {
                var modo = contesto.Campo("CONTRATTO_PAGAMENTO", out _);
                var questo = nome["PAGAMENTO_".Length..];

                return string.Equals(modo, questo, StringComparison.OrdinalIgnoreCase)
                    ? Voce("X") ?? "[X]"
                    : Voce(@"^\[\s*\]$") ?? "[  ]";
            }
        }

        // I consensi dell'informativa allegata al contratto.
        //
        // Finche' il cliente non ha scelto restano **tutte e due le
        // caselle vuote**, perche' le sbarra lui davanti al consulente:
        // un consenso prestampato non e' un consenso.
        if (nome.StartsWith(Consenso, StringComparison.Ordinal))
        {
            var scelta = contesto.Campo(nome, out _);

            return scelta switch
            {
                "SI" => Voce(@"\[X\] SI") ?? "[X] SI  [  ] NO",
                "NO" => Voce(@"\[X\] NO") ?? "[  ] SI  [X] NO",
                _ => Voce(@"^\[\s+\] SI\s+\[\s+\] NO$") ?? "[  ] SI  [  ] NO"
            };
        }

        return null;
    }

    /// <summary>
    /// Mette le ore della check list al posto di quelle d'esempio nel
    /// modello. Solo la prima volta: <c>Regex.Replace</c> le cambierebbe
    /// tutte, mentre il motore ne cambia una.
    /// </summary>
    private static string OreNelTesto(string testo, int ore)
    {
        var italiano = new Regex(@"\d+\s*h\b");
        if (italiano.IsMatch(testo)) return italiano.Replace(testo, ore + "h", 1);

        // «a maximum of 24 hours»: la forma italiana non la prende, perche'
        // dopo la h c'e' un'altra lettera.
        var inglese = new Regex(@"\d+(\s*)(hours?)\b", RegexOptions.IgnoreCase);
        return inglese.IsMatch(testo)
            ? inglese.Replace(testo, m => ore + m.Groups[1].Value + m.Groups[2].Value, 1)
            : testo;
    }

    /// <summary>
    /// Le versioni scritte nell'elenco a discesa del controllo. Come nel
    /// motore si preferisce il testo mostrato al valore: in Word si
    /// scrive il primo, il secondo spesso resta uguale.
    /// </summary>
    private static List<string> Voci(SdtElement sdt)
    {
        var proprieta = sdt.SdtProperties;
        if (proprieta is null) return [];

        var elementi =
            proprieta.GetFirstChild<SdtContentDropDownList>()?.Elements<ListItem>()
            ?? proprieta.GetFirstChild<SdtContentComboBox>()?.Elements<ListItem>();

        return elementi is null
            ? []
            : elementi.Select(v => v.DisplayText?.Value ?? v.Value?.Value ?? "")
                .Where(v => v.Length > 0)
                .ToList();
    }
}
