#nullable enable
using System;
using System.Globalization;

namespace LaboratorioAI.Core.Presenze;

/// <summary>
/// Piccoli aiuti per mostrare il prospetto mensile (pagina e Excel): testo delle celle e colore del carattere.
/// Portati da <c>excel_master.py</c> dell'app di Vaccarini.
/// </summary>
public static class ProspettoPresenze
{
    /// <summary>Ore con al massimo due decimali e virgola, senza zeri inutili: 8 → «8», 1.5 → «1,5», 7.25 → «7,25».</summary>
    public static string TestoOre(double ore)
    {
        var arrotondate = Math.Round(ore + 1e-9, 2, MidpointRounding.ToEven);
        return arrotondate.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>Testo da scrivere nella cella: «FE», «FS», le ore, oppure stringa vuota.</summary>
    public static string TestoCella(ContenutoCella? cella)
    {
        if (cella is null) return "";
        if (cella.Testo is not null) return cella.Testo;
        return cella.Ore.HasValue ? TestoOre(cella.Ore.Value) : "";
    }

    /// <summary>Colore scuro (luminosità bassa): sopra serve il carattere bianco. Accetta «#RRGGBB», «RRGGBB» o «AARRGGBB».</summary>
    public static bool ColoreScuro(string? colore)
    {
        var h = (colore ?? "").TrimStart('#');
        if (h.Length == 8) h = h.Substring(2);
        if (h.Length != 6) return false;
        if (!int.TryParse(h.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !int.TryParse(h.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !int.TryParse(h.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
            return false;
        return 0.299 * r + 0.587 * g + 0.114 * b < 140;
    }

    /// <summary>Colore del carattere nella cella: rosso per lo straordinario, bianco sul colore scuro del lavoratore, altrimenti automatico (null).</summary>
    public static string? ColoreCarattere(ContenutoCella cella, string coloreLavoratore)
    {
        if (cella.CarattereRosso) return ContenutoCellaPresenze.Rosso;
        var sulColoreDelLavoratore = cella.Sfondo is not null &&
            string.Equals(cella.Sfondo, ContenutoCellaPresenze.ColoreArgb(coloreLavoratore), StringComparison.OrdinalIgnoreCase);
        return sulColoreDelLavoratore && ColoreScuro(coloreLavoratore) ? "FFFFFFFF" : null;
    }

    /// <summary>«FFFFC000» → «#FFC000» (per i fogli di stile della pagina).</summary>
    public static string ColoreHtml(string argb) => "#" + (argb.Length == 8 ? argb.Substring(2) : argb.TrimStart('#'));
}
