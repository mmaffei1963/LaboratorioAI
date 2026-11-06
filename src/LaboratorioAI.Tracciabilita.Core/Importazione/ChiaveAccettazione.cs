using System.Globalization;

namespace LaboratorioAI.Tracciabilita.Core.Importazione;

/// <summary>
/// Identifica un'accettazione già importata. Le quantità si confrontano arrotondate a 2 decimali:
/// il database le restituisce come 15180.00 mentre il vecchio programma ha 15180.
/// </summary>
public static class ChiaveAccettazione
{
    public static decimal Quantita(decimal q) => decimal.Round(q, 2);

    public static string Crea(int idFornitore, int idArticolo, DateOnly arrivo, string? documento, decimal quantita, int idLotto) =>
        string.Create(CultureInfo.InvariantCulture,
            $"{idFornitore}|{idArticolo}|{arrivo:yyyyMMdd}|{documento?.Trim()}|{Quantita(quantita):0.##}|{idLotto}");
}
