#nullable enable
using System;
using System.Globalization;

namespace LaboratorioAI.Core.Presenze;

/// <summary>Lettura dei valori digitati dalle persone (ore e orari) nei moduli delle presenze.</summary>
public static class ParsingPresenze
{
    /// <summary>
    /// «8», «7,5», «7.5», «7:30» → 7.5. Testo vuoto → null.
    /// Solleva <see cref="FormatException"/> se il valore non è un numero di ore.
    /// </summary>
    public static double? ParseOre(string? testo)
    {
        testo = (testo ?? "").Trim().Replace(',', '.');
        if (testo.Length == 0)
            return null;
        var due = testo.IndexOf(':');
        if (due >= 0)
        {
            var ore = int.Parse(testo.Substring(0, due), NumberStyles.Integer, CultureInfo.InvariantCulture);
            var minuti = int.Parse(testo.Substring(due + 1), NumberStyles.Integer, CultureInfo.InvariantCulture);
            return ore + minuti / 60.0;
        }
        return double.Parse(testo, NumberStyles.Float, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Valida un orario «HH:mm» (24 ore) e lo restituisce in forma normalizzata.
    /// A differenza dell'app originale accetta solo due cifre per ore e minuti («8:05» non è valido),
    /// così l'ordinamento per orario resta corretto.
    /// </summary>
    public static bool TryValidaOra(string? testo, out string ora)
    {
        ora = "";
        if (testo is null)
            return false;
        if (!TimeOnly.TryParseExact(testo.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
            return false;
        ora = t.ToString("HH:mm", CultureInfo.InvariantCulture);
        return true;
    }
}
