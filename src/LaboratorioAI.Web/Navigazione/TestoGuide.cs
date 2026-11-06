using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LaboratorioAI.Web.Navigazione;

/// <summary>
/// «Chiedi alla guida» (11/10/2026): trasforma l'HTML delle guide in testo semplice da dare al modello e prepara le istruzioni.
/// Il testo cambia solo quando cambiano le guide o i permessi della persona: così resta uguale tra una domanda e l'altra
/// e il modello lo tiene in cache (costo dell'input circa un decimo).
/// </summary>
public static class TestoGuide
{
    public const string NonTrovato = "Non lo trovo nelle guide: chiedi a Mirko.";

    public const string Istruzioni = """
        Sei «Chiedi alla guida», l'assistente di Laboratorio AI: il programma interno di MARE.A srl (laboratorio di analisi accreditato ACCREDIA)
        che affianca il gestionale ArcLab. Rispondi ai colleghi su COME SI USA il programma, usando SOLO le guide e il menu riportati sotto.

        Regole:
        1. Usa solo le informazioni scritte qui sotto. Non inventare pagine, pulsanti, campi, permessi o procedure.
           Se la risposta non c'è, rispondi soltanto: «Non lo trovo nelle guide: chiedi a Mirko.» e, se esiste, indica la guida più vicina all'argomento.
        2. Rispondi in italiano, breve e pratico: pochi passi numerati (1. 2. 3.), con i nomi di pagine e pulsanti scritti come nelle guide.
        3. Termina sempre con una riga che inizia con «Dove:» e indica la voce di menu e la guida con la situazione da aprire,
           es. «Dove: menu Ciclo analitico → Validazione · Guida «Validazione», situazione «…»».
        4. Solo testo semplice: niente asterischi, cancelletti, tabelle o altra formattazione markdown.
        5. Non hai accesso ai dati (campioni, clienti, risultati, importi): se chiedono un dato preciso, spiega in quale pagina e come cercarlo.
        6. La persona vede solo le funzioni elencate qui sotto. Se chiede di una funzione che non compare, dille che forse non ha l'accesso e di chiedere a Mirko.
        7. Se la domanda non riguarda l'uso di Laboratorio AI, rispondi che puoi aiutare solo sull'uso del programma.
        8. Se nella domanda ci sono nomi di persone o di clienti, non ripeterli nella risposta.
        """;

    /// <summary>Testo semplice da un frammento HTML di guida: titoli e situazioni marcati, elenchi con trattino, immagini tolte.</summary>
    public static string DaHtml(string html)
    {
        var s = ScriptStileRx.Replace(html, "");
        s = ImmaginiRx.Replace(s, "");
        s = Regex.Replace(s, @"<summary[^>]*>", "\n### Situazione: ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<h[1-4][^>]*>", "\n## ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<li[^>]*>", "\n- ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<t[dh][^>]*>", " | ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<(br|p|div|tr|table|ul|ol|details|dt|dd|figure|figcaption)\b[^>]*>|</(summary|h[1-4]|p|div|tr|table|ul|ol|details)>", "\n", RegexOptions.IgnoreCase);
        s = TagRx.Replace(s, "");
        s = WebUtility.HtmlDecode(s);

        var righe = new StringBuilder();
        var vuota = true;
        foreach (var r in s.Split('\n'))
        {
            var t = SpaziRx.Replace(r, " ").Trim();
            if (t.Length == 0) { if (!vuota) righe.Append('\n'); vuota = true; continue; }
            righe.Append(t).Append('\n');
            vuota = false;
        }
        return righe.ToString().Trim();
    }

    /// <summary>Menu visibile alla persona, una riga per voce.</summary>
    public static string Menu(IEnumerable<GruppoMenu> gruppi)
    {
        var sb = new StringBuilder();
        foreach (var g in gruppi)
            foreach (var v in g.Voci)
                sb.Append("- menu ").Append(g.Titolo).Append(" → ").Append(v.Titolo).Append(" (").Append(v.Url).Append("): ").Append(v.Descrizione).Append('\n');
        return sb.ToString();
    }

    private static readonly Regex ScriptStileRx = new(@"<(script|style)\b.*?</\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ImmaginiRx = new(@"<img\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRx = new(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex SpaziRx = new(@"[ \t\r\u00A0]+", RegexOptions.Compiled);
}
