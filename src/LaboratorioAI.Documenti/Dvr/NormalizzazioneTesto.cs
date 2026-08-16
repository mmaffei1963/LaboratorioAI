using System.Text;

namespace LaboratorioAI.Documenti.Dvr;

/// <summary>
/// Uniformazione dei caratteri che Word introduce automaticamente.
///
/// La correzione automatica di Word sostituisce l'apostrofo dritto con
/// quello tipografico e i trattini con lineette. I modelli documentali
/// contengono quindi caratteri diversi da quelli digitati nel codice, e
/// il confronto fra le due forme fallisce senza una normalizzazione.
///
/// È la causa più frequente di controlli che risultano introvabili pur
/// essendo visibilmente presenti nel modello.
/// </summary>
public static class NormalizzazioneTesto
{
    private static readonly (char Origine, char Destinazione)[] Sostituzioni =
    [
        ('\u2019', '\''),   // apostrofo tipografico
        ('\u2018', '\''),   // virgoletta singola aperta
        ('\u201C', '"'),    // virgolette doppie aperte
        ('\u201D', '"'),    // virgolette doppie chiuse
        ('\u2013', '-'),    // lineetta enne
        ('\u2014', '-'),    // lineetta emme
        ('\u00A0', ' '),    // spazio unificatore
        ('\u200B', ' ')     // spazio a larghezza nulla
    ];

    /// <summary>Riporta il testo a una forma confrontabile.</summary>
    public static string Normalizza(string? testo)
    {
        if (string.IsNullOrEmpty(testo)) return "";

        var sb = new StringBuilder(testo.Length);

        foreach (var carattere in testo)
        {
            var sostituito = carattere;

            foreach (var (origine, destinazione) in Sostituzioni)
            {
                if (carattere == origine) { sostituito = destinazione; break; }
            }

            sb.Append(sostituito);
        }

        return sb.ToString().Trim();
    }

    /// <summary>Confronto fra testi, indifferente a maiuscole e forma dei caratteri.</summary>
    public static bool Corrispondono(string? primo, string? secondo) =>
        string.Equals(Normalizza(primo), Normalizza(secondo),
            StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Chiave stabile ricavata da un testo, per l'uso come identificativo.
    /// Riduce a minuscolo, elimina la punteggiatura e comprime gli spazi.
    /// </summary>
    public static string Chiave(string? testo)
    {
        var normalizzato = Normalizza(testo).ToLowerInvariant();
        var sb = new StringBuilder(normalizzato.Length);
        var spazioPrecedente = false;

        foreach (var carattere in normalizzato)
        {
            if (char.IsLetterOrDigit(carattere))
            {
                sb.Append(carattere);
                spazioPrecedente = false;
            }
            else if (!spazioPrecedente)
            {
                sb.Append('_');
                spazioPrecedente = true;
            }
        }

        return sb.ToString().Trim('_');
    }
}
