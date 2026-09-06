using System.Globalization;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;

namespace LaboratorioAI.IA;

/// <summary>
/// Estrae in modo DETERMINISTICO la data di emissione dal testo del certificato PDF
/// (più affidabile del modello su questo dato). Ritorna la data in "yyyy-MM-dd" o null.
/// </summary>
public static class EstrattoreDataEmissione
{
    // I separatori di parola sono \s* così i pattern reggono anche se il PDF perde gli spazi.
    private static readonly Regex[] Pattern =
    {
        // "Data Emissione / Date of issue 21/08/2026", "Data di emissione: 12/08/2026"
        new(@"data\s*(?:di\s*)?emissione(?:\s*/?\s*date\s*of\s*issue)?\s*[:\-]?\s*(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // Data di firma prima del responsabile/direttore (es. CRM: "Cesenatico, 03/09/2026 Il Responsabile…")
        new(@"(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{4})\s+Il\s+(?:Responsabile|Direttore)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // "Rapporto di prova n°: 26LA16791 del 12/08/2026"
        new(@"rapporto\s*di\s*prova\s*n[°º\.]?\s*:?\s*\S+\s+del\s+(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
        // "RAPPORTO DI PROVA n° 24SA13613 … del 05/04/2024"
        new(@"rapporto\s*di\s*prova\b[^\n]{0,80}?\bdel\s+(\d{1,2}[/.\-]\d{1,2}[/.\-]\d{2,4})",
            RegexOptions.IgnoreCase | RegexOptions.Compiled),
    };

    public static string? Estrai(byte[] pdf)
    {
        string testo;
        try
        {
            using var doc = PdfDocument.Open(pdf);
            // Estrazione per parole: garantisce gli spazi tra i token.
            testo = string.Join(" ", doc.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        }
        catch { return null; }
        if (string.IsNullOrWhiteSpace(testo)) return null;

        foreach (var rx in Pattern)
        {
            var m = rx.Match(testo);
            if (m.Success && ParseData(m.Groups[1].Value) is DateOnly d)
                return d.ToString("yyyy-MM-dd");
        }
        return null;
    }

    private static DateOnly? ParseData(string s)
    {
        s = s.Replace('.', '/').Replace('-', '/');
        foreach (var fmt in new[] { "d/M/yyyy", "d/M/yy" })
            if (DateOnly.TryParseExact(s, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return d;
        return null;
    }
}
