using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

/// <summary>
/// Rende confrontabili i nomi scritti a mano nel vecchio programma («MOLINO NALDONI SRL», «Molino Naldoni s.r.l.»):
/// minuscole, senza accenti né punteggiatura, senza forma societaria.
/// Serve solo a PROPORRE gli abbinamenti: l'ultima parola spetta sempre a una persona.
/// </summary>
public static class NomiNormalizzati
{
    private static readonly HashSet<string> FormeSocietarie = new()
    {
        "srl", "srls", "spa", "snc", "sas", "sapa", "sa", "ltd", "gmbh", "bv", "sl", "coop", "cooperativa",
        "soc", "societa", "agricola", "ss"
    };

    private static readonly HashSet<string> ParoleVuote = new()
    {
        "di", "e", "il", "lo", "la", "le", "i", "gli", "da", "del", "dei", "della", "delle", "per", "con", "al", "alla", "in", "a"
    };

    public static IReadOnlyList<string> Parole(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return Array.Empty<string>();

        var s = SenzaAccenti(testo).ToLowerInvariant();
        s = s.Replace(".", "").Replace("'", "").Replace("’", "");        // «s.r.l.» → «srl», «dell'orto» → «dellorto»
        s = Regex.Replace(s, @"[^a-z0-9]+", " ").Trim();
        return s.Length == 0 ? Array.Empty<string>() : s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>Chiave di un fornitore: parole in ordine, senza forma societaria.</summary>
    public static string ChiaveFornitore(string? nome) =>
        string.Join(' ', Parole(nome).Where(p => !FormeSocietarie.Contains(p) && !ParoleVuote.Contains(p)));

    /// <summary>Chiave di un articolo: parole significative in ordine alfabetico («farina bio integrale» = «farina integrale bio»).</summary>
    public static string ChiaveArticolo(string? descrizione) =>
        string.Join(' ', Parole(descrizione).Where(p => !ParoleVuote.Contains(p)).Distinct().OrderBy(p => p, StringComparer.Ordinal));

    /// <summary>Somiglianza tra due nomi da 0 a 1: parole uguali o con un solo errore di battitura (parole lunghe).</summary>
    public static double Somiglianza(string? a, string? b, bool comeFornitore)
    {
        var pa = Filtra(Parole(a), comeFornitore);
        var pb = Filtra(Parole(b), comeFornitore);
        if (pa.Count == 0 || pb.Count == 0) return 0;

        var libere = new List<string>(pb);
        var uguali = 0;
        foreach (var p in pa)
        {
            var i = libere.FindIndex(q => ParolaUguale(p, q));
            if (i < 0) continue;
            uguali++;
            libere.RemoveAt(i);
        }
        return (double)uguali / (pa.Count + pb.Count - uguali);
    }

    private static List<string> Filtra(IReadOnlyList<string> parole, bool comeFornitore) =>
        parole.Where(p => !ParoleVuote.Contains(p) && (!comeFornitore || !FormeSocietarie.Contains(p))).ToList();

    private static bool ParolaUguale(string a, string b) =>
        a == b || (a.Length >= 5 && b.Length >= 5 && Math.Abs(a.Length - b.Length) <= 1 && Distanza(a, b) <= 1);

    private static int Distanza(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    private static string SenzaAccenti(string testo)
    {
        var sb = new StringBuilder();
        foreach (var c in testo.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
