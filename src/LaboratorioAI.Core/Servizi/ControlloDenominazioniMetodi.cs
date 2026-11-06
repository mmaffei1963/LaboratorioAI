using System.Globalization;
using System.Text;
using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Una segnalazione sulla denominazione di un metodo.</summary>
public sealed record AnomaliaDenominazione(RigaMetodoDenominazione Metodo, string Tipo, string Motivo);

/// <summary>
/// Controllo delle denominazioni dei metodi ArcLab (Ricerca errori, 09/10/2026), nato dal caso «Manganese» stampato per
/// il metodo del mercurio (UNI EN ISO 12846). La denominazione (Metodo.DettagliMetodo) è quella che finisce nel Rapporto
/// di Prova e nell'Ordine di Accettazione. Due segnalazioni, entrambe da verificare a occhio:
/// 1. <see cref="TipoParametroAssente"/>: nessuna parola significativa del parametro compare nella denominazione
///    (confronto senza accenti né maiuscole, sulle prime 5 lettere per tollerare singolare/plurale: «nitrato»/«nitrati»);
/// 2. <see cref="TipoDenominazioneCondivisa"/>: la stessa denominazione è usata per parametri diversi.
/// Ci saranno falsi allarmi (denominazioni corrette scritte con altre parole): si segnano una volta come corrette.
/// Solo logica, nessun accesso ai dati.
/// </summary>
public static class ControlloDenominazioniMetodi
{
    public const string TipoParametroAssente = "ParametroAssente";
    public const string TipoDenominazioneCondivisa = "DenominazioneCondivisa";

    private const int LunghezzaRadice = 5;

    /// <summary>Parole che non identificano un parametro (articoli, preposizioni, unità, qualificatori generici).</summary>
    private static readonly HashSet<string> ParoleVuote = new(StringComparer.Ordinal)
    {
        "del", "dei", "della", "delle", "degli", "dello", "con", "per", "come", "sul", "sulla", "nel", "nella",
        "totale", "totali", "tot", "ione", "ioni", "the", "and", "for", "total", "ufc", "mpn", "cfu", "presenza",
        "assenza", "ricerca", "conta", "determinazione", "contenuto", "valore", "residuo", "residui", "solubile",
    };

    public static IReadOnlyList<AnomaliaDenominazione> Analizza(IEnumerable<RigaMetodoDenominazione> metodi)
    {
        var elenco = metodi.Where(m => !string.IsNullOrWhiteSpace(m.DettagliMetodo)).ToList();
        var anomalie = new List<AnomaliaDenominazione>();

        // 1. La denominazione non cita il parametro.
        foreach (var m in elenco)
        {
            if (!CitaParametro(m.NomeParametro, m.DettagliMetodo))
                anomalie.Add(new AnomaliaDenominazione(m, TipoParametroAssente,
                    $"La denominazione non cita il parametro «{m.NomeParametro?.Trim()}»"));
        }

        // 2. Stessa denominazione per parametri diversi.
        foreach (var gruppo in elenco.Where(m => m.IdParametro is not null).GroupBy(m => Normalizza(m.DettagliMetodo)))
        {
            var parametri = gruppo.Select(m => m.IdParametro).Distinct().Count();
            if (parametri < 2) continue;
            foreach (var m in gruppo)
            {
                var altri = gruppo.Where(x => x.IdParametro != m.IdParametro)
                    .Select(x => x.NomeParametro?.Trim()).Where(x => !string.IsNullOrEmpty(x)).Distinct().Take(5);
                anomalie.Add(new AnomaliaDenominazione(m, TipoDenominazioneCondivisa,
                    $"Stessa denominazione usata anche per: {string.Join(", ", altri)}"));
            }
        }
        return anomalie;
    }

    /// <summary>
    /// True se almeno una parola significativa del parametro compare nella denominazione (prime 5 lettere), oppure se
    /// il parametro non ha parole significative (nessun confronto possibile).
    /// </summary>
    public static bool CitaParametro(string? parametro, string? denominazione)
    {
        var parole = Parole(parametro);
        if (parole.Count == 0) return true;
        var testo = Parole(denominazione);
        return parole.Any(p => testo.Any(t => Radice(t) == Radice(p)
            || (p.Length < LunghezzaRadice && t == p)));
    }

    private static string Radice(string parola) => parola.Length <= LunghezzaRadice ? parola : parola[..LunghezzaRadice];

    /// <summary>Parole di almeno 3 lettere, senza accenti e in minuscolo, escluse quelle generiche.</summary>
    public static List<string> Parole(string? testo) =>
        Normalizza(testo).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 3 && !ParoleVuote.Contains(p) && !p.All(char.IsDigit))
            .Distinct().ToList();

    /// <summary>Minuscolo, senza accenti, solo lettere e cifre separate da uno spazio.</summary>
    public static string Normalizza(string? testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return "";
        var sb = new StringBuilder(testo.Length);
        foreach (var c in testo.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
