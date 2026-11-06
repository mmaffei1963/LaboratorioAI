using System.Globalization;
using System.Text;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Ricerca tollerante dei nomi (ragioni sociali) per le caselle «cerca cliente» (15/10/2026). Ignora maiuscole, accenti,
/// punteggiatura e spazi: «mare-a», «marea», «Mare.A srl» trovano tutti «MARE.A S.R.L.». I risultati sono ordinati per
/// pertinenza (prima chi comincia con il testo cercato), non in ordine alfabetico: così un nome corto non sparisce
/// dietro decine di nomi che lo contengono.
/// </summary>
public static class RicercaNomi
{
    /// <summary>Lettere e cifre in maiuscolo senza accenti; ogni altro carattere diventa uno spazio singolo.</summary>
    public static string Normalizza(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        var spazio = false;
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) { sb.Append(char.ToUpperInvariant(ch)); spazio = false; }
            else if (!spazio && sb.Length > 0) { sb.Append(' '); spazio = true; }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Forma compatta, senza spazi: «MARE A S R L» → «MAREASRL».</summary>
    public static string Compatta(string normalizzato) => normalizzato.Replace(" ", "");

    /// <summary>
    /// Pertinenza del nome per il testo cercato (più basso = più pertinente), oppure null se non corrisponde.
    /// 0 uguale · 1 comincia con il testo · 2 tutte le parole sono inizi di parola · 3 tutte le parole contenute · 4 contenuto senza spazi.
    /// </summary>
    public static int? Rango(string? nome, string? cercato)
    {
        var q = Normalizza(cercato);
        if (q.Length == 0) return null;
        var n = Normalizza(nome);
        if (n.Length == 0) return null;
        var qc = Compatta(q); var nc = Compatta(n);
        if (nc == qc) return 0;
        if (nc.StartsWith(qc, StringComparison.Ordinal)) return 1;
        var paroleNome = n.Split(' ');
        var paroleCercate = q.Split(' ');
        if (paroleCercate.All(p => paroleNome.Any(w => w.StartsWith(p, StringComparison.Ordinal)))) return 2;
        if (paroleCercate.All(p => n.Contains(p, StringComparison.Ordinal))) return 3;
        if (nc.Contains(qc, StringComparison.Ordinal)) return 4;
        return null;
    }

    /// <summary>Filtra e ordina per pertinenza, poi per nome; al massimo <paramref name="massimo"/> risultati.</summary>
    public static List<T> Cerca<T>(IEnumerable<T> elementi, Func<T, string?> nome, string? cercato, int massimo = 40) =>
        elementi.Select(e => (e, r: Rango(nome(e), cercato)))
            .Where(x => x.r is not null)
            .OrderBy(x => x.r).ThenBy(x => nome(x.e), StringComparer.OrdinalIgnoreCase)
            .Take(massimo).Select(x => x.e).ToList();
}
