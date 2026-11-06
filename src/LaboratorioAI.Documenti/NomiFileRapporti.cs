namespace LaboratorioAI.Documenti;

/// <summary>Parti dei nomi file dei rapporti (03/11/2026): solo lettere e cifre, il resto «_», al massimo 40 caratteri.</summary>
internal static class NomiFileRapporti
{
    public static string Slug(string? s, string fallback)
    {
        if (string.IsNullOrWhiteSpace(s)) return fallback;
        var sb = new System.Text.StringBuilder();
        bool underscore = false;
        foreach (var ch in s.Trim())
        {
            if (char.IsLetterOrDigit(ch)) { sb.Append(ch); underscore = false; }
            else if (!underscore) { sb.Append('_'); underscore = true; }
        }
        var r = sb.ToString().Trim('_');
        if (r.Length > 40) r = r.Substring(0, 40).Trim('_');
        return r.Length == 0 ? fallback : r;
    }
}
