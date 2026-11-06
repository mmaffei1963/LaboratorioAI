using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace LaboratorioAI.Core.Limiti;

/// <summary>
/// Testo leggibile dalle pagine ufficiali (EUR-Lex, Normattiva, Ministero) per l'estrazione dei limiti (17/10/2026).
/// Le tabelle restano leggibili: celle separate da « | », una riga per riga di tabella.
/// </summary>
public static class TestoFonti
{
    /// <summary>Testi più lunghi non si mandano all'AI: si carica solo l'allegato che serve.</summary>
    public const int CaratteriMassimi = 500_000;

    private static readonly Regex BlocchiDaTogliereRx = new(@"<(script|style|noscript|head|nav|footer|svg)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex CommentiRx = new(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex FineCellaRx = new(@"</t[dh]\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FineBloccoRx = new(@"<br\s*/?>|</(p|div|tr|li|h[1-6]|table|section|article|dd|dt)\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TagRx = new(@"<[^>]+>", RegexOptions.None | RegexOptions.Compiled);
    private static readonly Regex SpaziRx = new(@"[ \t\u00A0]+", RegexOptions.None | RegexOptions.Compiled);
    private static readonly Regex RigheVuoteRx = new(@"\n\s*\n\s*\n+", RegexOptions.None | RegexOptions.Compiled);
    private static readonly Regex DataDichiarataRx = new(@"(?:vigente al|consolidato al|aggiornat[oa] al|testo in vigore dal|in vigore dal|versione del)\s*:?\s*(\d{1,2})[./-](\d{1,2})[./-](\d{4})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CellaRx = new(@"<t([dh])\b([^>]*)>(.*?)</t\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex BloccoInternoRx = new(@"<br\s*/?>|</?(p|div|li|ul|ol|span)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    /// <summary>Segni delle modifiche nei testi consolidati EUR-Lex: «►M9», «▼B», «►C1», «◄».</summary>
    private static readonly Regex SegniConsolidatoRx = new(@"[►▼]\s*[A-Z]{1,2}\d{0,3}|◄", RegexOptions.Compiled);
    /// <summary>Richiami di nota EUR-Lex con gli spazi interni: «( 7 )», «( *1 )».</summary>
    private static readonly Regex RichiamoNotaRx = new(@"\( \*?\d{1,3} \)", RegexOptions.Compiled);

    /// <summary>Da HTML a testo semplice.</summary>
    public static string DaHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // gli a capo del sorgente HTML sono semplici spazi: le righe le decidono i tag di blocco
        var s = BlocchiDaTogliereRx.Replace(html.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' '), " ");
        s = CommentiRx.Replace(s, " ");
        // 17/10/2026: nelle celle delle tabelle (EUR-Lex mette un <p> in ogni cella) niente a capo: una riga di tabella = una riga di testo.
        s = CellaRx.Replace(s, m => "<t" + m.Groups[1].Value + m.Groups[2].Value + ">" + BloccoInternoRx.Replace(m.Groups[3].Value, " ") + "</t" + m.Groups[1].Value + ">");
        s = FineCellaRx.Replace(s, " | ");
        s = FineBloccoRx.Replace(s, "\n");
        s = TagRx.Replace(s, " ");
        s = WebUtility.HtmlDecode(s);
        s = SegniConsolidatoRx.Replace(s, " ");
        s = RichiamoNotaRx.Replace(s, " ");
        return Pulisci(s);
    }

    /// <summary>Spazi multipli ridotti, righe vuote ridotte, «|» finali tolti.</summary>
    public static string Pulisci(string testo)
    {
        var sb = new StringBuilder(testo.Length);
        foreach (var riga0 in testo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var riga = SpaziRx.Replace(riga0, " ").Trim();
            while (riga.EndsWith('|')) riga = riga[..^1].TrimEnd();
            while (riga.StartsWith('|')) riga = riga[1..].TrimStart();
            sb.Append(riga).Append('\n');
        }
        return RigheVuoteRx.Replace(sb.ToString(), "\n\n").Trim();
    }

    /// <summary>Data di aggiornamento dichiarata nel testo («vigente al 25/02/2024», «consolidato al …»), se c'è.</summary>
    public static DateTime? DataAggiornamento(string testo)
    {
        var m = DataDichiarataRx.Match(testo.Length > 20000 ? testo[..20000] : testo);
        if (!m.Success) return null;
        try
        {
            return new DateTime(int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), 0, 0, 0, DateTimeKind.Utc);
        }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static readonly string[] SegniDiBlocco =
    [
        "convalida del browser", "abilita i cookie", "abilitare i cookie", "richiede i cookie", "servizio di sicurezza per proteggersi",
        "enable cookies", "enable javascript", "just a moment", "checking your browser", "verify you are human", "captcha",
        "access denied", "request rejected", "accesso negato", "attention required", "ddos protection"
    ];

    /// <summary>
    /// Pagina di controllo anti-bot o di errore al posto della norma (17/10/2026: salute.gov.it risponde al server con
    /// «Pagina di convalida del browser»). Testo breve con una delle frasi tipiche: non è una fonte.
    /// </summary>
    public static bool PaginaDiBlocco(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo) || testo.Length < 200) return true;
        if (testo.Length > 5000) return false;
        var t = testo.ToLowerInvariant();
        return SegniDiBlocco.Any(t.Contains);
    }

    /// <summary>
    /// Canale ufficiale per programmi da cui l'agente sul PC dell'ufficio scarica la fonte (17/10/2026), ricavato
    /// dall'indirizzo: «Cellar» (Ufficio pubblicazioni UE, testi EUR-Lex) con il numero CELEX di base (es. 32005R2073),
    /// oppure «Normattiva» (API Open Data) con l'URN dell'atto. Null: nessun canale, si carica a mano.
    /// </summary>
    public static (string? Canale, string? Codice) CanaleAutomatico(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return (null, null);
        var u = Uri.UnescapeDataString(url.Trim());
        var i = u.IndexOf("CELEX:", StringComparison.OrdinalIgnoreCase);
        if (i < 0) i = u.IndexOf("celex/", StringComparison.OrdinalIgnoreCase);
        if (i >= 0 && (u.Contains("eur-lex.europa.eu", StringComparison.OrdinalIgnoreCase) || u.Contains("publications.europa.eu", StringComparison.OrdinalIgnoreCase)))
        {
            var codice = new string(u[(i + 6)..].TakeWhile(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
            // «02005R2073» (consolidato) e «32005R2073» (originale) → numero di base «32005R2073»
            if (codice.Length >= 10 && (codice[0] == '0' || codice[0] == '3')) return ("Cellar", "3" + codice[1..10]);
            return (null, null);
        }
        i = u.IndexOf("urn:nir:", StringComparison.OrdinalIgnoreCase);
        if (i >= 0 && u.Contains("normattiva.it", StringComparison.OrdinalIgnoreCase))
        {
            var urn = new string(u[i..].TakeWhile(c => !char.IsWhiteSpace(c) && c is not '&' and not '#' and not '!' and not '~' and not '@').ToArray());
            return ("Normattiva", urn);
        }
        return (null, null);
    }

    /// <summary>Il contenuto è un PDF? (firma «%PDF» all'inizio).</summary>
    public static bool EPdf(byte[] dati) =>
        dati.Length >= 4 && dati[0] == (byte)'%' && dati[1] == (byte)'P' && dati[2] == (byte)'D' && dati[3] == (byte)'F';
}
