using System.Text.RegularExpressions;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Controlli sugli indirizzi e-mail usati dalle Condizioni e firme (11/10/2026): formato, caselle generiche
/// (info@, amministrazione@…, sconsigliate per il Firmatario ma non bloccate) ed elenco di più indirizzi in un campo.
/// Condiviso da LaboratorioAI Web e dal Portale.
/// </summary>
public static class IndirizziEmail
{
    public static readonly HashSet<string> Generiche = new(StringComparer.OrdinalIgnoreCase)
    {
        "info", "qualita", "qualità", "amministrazione", "admin", "segreteria", "ufficio", "contabilita", "office",
        "posta", "mail", "commerciale", "acquisti", "ordini", "direzione", "noreply", "no-reply", "haccp"
    };

    public static readonly Regex Formato = new(@"^[^@\s;,]+@[^@\s;,]+\.[^@\s;,]+$", RegexOptions.Compiled);

    public static bool Generica(string email) => Generiche.Contains(email.Trim().Split('@')[0]);

    /// <summary>Indirizzi validi contenuti in un campo che può averne più d'uno (separati da «;», «,» o spazi).</summary>
    public static IReadOnlyList<string> Elenco(string? campo) =>
        (campo ?? "").Split(new[] { ';', ',', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().Trim('<', '>', '"', '\'')).Where(x => Formato.IsMatch(x))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
