using System.Globalization;
using ClosedXML.Excel;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Legge il database CREA dal foglio «Etichetta_100g» (vista operativa con i
/// valori per 100 g già pronti e il Sale calcolato dal Sodio). Le colonne sono
/// individuate per intestazione, così l'ordine non è vincolante.
/// </summary>
public static class ImportatoreCrea
{
    private const string NomeFoglio = "Etichetta_100g";

    public static IReadOnlyList<RigaCrea> Leggi(Stream xlsx)
    {
        using var wb = new XLWorkbook(xlsx);
        var ws = wb.Worksheets.FirstOrDefault(w =>
                     string.Equals(w.Name, NomeFoglio, StringComparison.OrdinalIgnoreCase))
                 ?? throw new InvalidOperationException(
                     $"Foglio «{NomeFoglio}» non trovato nel file CREA.");

        var intestazione = ws.Row(1);
        int ultimaCol = intestazione.LastCellUsed()?.Address.ColumnNumber ?? 0;

        int Col(params string[] token)
        {
            for (int c = 1; c <= ultimaCol; c++)
            {
                var h = intestazione.Cell(c).GetString();
                if (!string.IsNullOrWhiteSpace(h) &&
                    token.All(t => h.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    return c;
            }
            throw new InvalidOperationException(
                $"Colonna con [{string.Join(", ", token)}] non trovata nel foglio CREA.");
        }

        int cCodice = Col("codice");
        int cAlimento = Col("alimento");
        int cCategoria = Col("categoria");
        int cAcqua = Col("acqua");
        int cKj = Col("energia", "kj");
        int cKcal = Col("energia", "kcal");
        int cGrassi = Col("grassi", "g/100g");
        int cSaturi = Col("saturi", "g/100g");
        int cCarbo = Col("carboidrati");
        int cZucc = Col("zuccheri");
        int cFibre = Col("fibre");
        int cProt = Col("proteine");
        int cSale = Col("sale");
        int cFonte = Col("fonte");

        var righe = new List<RigaCrea>();
        foreach (var r in ws.RowsUsed().Skip(1))
        {
            var alimento = r.Cell(cAlimento).GetString().Trim();
            if (alimento.Length == 0) continue;

            righe.Add(new RigaCrea
            {
                Codice = r.Cell(cCodice).GetString().Trim(),
                Alimento = alimento,
                Categoria = Testo(r.Cell(cCategoria)),
                Acqua = Num(r.Cell(cAcqua)),
                EnergiaKj = Num(r.Cell(cKj)),
                EnergiaKcal = Num(r.Cell(cKcal)),
                Grassi = Num(r.Cell(cGrassi)),
                GrassiSaturi = Num(r.Cell(cSaturi)),
                Carboidrati = Num(r.Cell(cCarbo)),
                Zuccheri = Num(r.Cell(cZucc)),
                Fibre = Num(r.Cell(cFibre)),
                Proteine = Num(r.Cell(cProt)),
                Sale = Num(r.Cell(cSale)),
                FonteUrl = Testo(r.Cell(cFonte))
            });
        }
        return righe;
    }

    private static string? Testo(IXLCell c)
    {
        var s = c.GetString().Trim();
        return s.Length == 0 ? null : s;
    }

    private static decimal? Num(IXLCell c)
    {
        if (c.TryGetValue<double>(out var d)) return (decimal)d;
        var s = c.GetString().Trim();
        if (s.Length == 0) return null;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m)) return m;
        if (decimal.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out m)) return m;
        return null;
    }
}
