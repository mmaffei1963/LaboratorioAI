using System.Globalization;
using System.Text;
using ExcelDataReader;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;

namespace LaboratorioAI.Dati.Import;

/// <inheritdoc cref="ILettoreFileTempLog" />
///
/// <remarks>
/// Legge il file notturno Econorma (BIFF .xls oppure .xlsx). Struttura del foglio:
///   riga 1: "DATA DAL" / "AL" ; riga 2: le due date;
///   riga 6: intestazione "ORA", poi le etichette canale (T01, T02, …);
///   dalla riga 7: ORA in colonna A e le letture per canale nelle colonne successive.
/// I valori non numerici o vuoti diventano letture nulle.
/// </remarks>
public class LettoreFileTempLog : ILettoreFileTempLog
{
    static LettoreFileTempLog() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // richiesto da ExcelDataReader per .xls

    private const int RigaIntestazione = 6;   // 1-based: riga con "ORA" e le etichette canale

    public FileTempLog Leggi(Stream contenuto, string? nomeFile = null)
    {
        using var reader = ExcelReaderFactory.CreateReader(contenuto);
        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });
        if (ds.Tables.Count == 0) throw new InvalidOperationException("Il file non contiene fogli.");
        var t = ds.Tables[0];

        DateTime? dal = LeggiData(t, 1, 0);
        DateTime? al = LeggiData(t, 1, 1);

        // Intestazione: colonna 0 = ORA, colonne >0 = canali (fino alla prima cella vuota).
        int rIntest = RigaIntestazione - 1;
        var canali = new List<string>();
        var colonnaCanale = new List<int>();
        if (rIntest < t.Rows.Count)
        {
            for (int c = 1; c < t.Columns.Count; c++)
            {
                string et = (t.Rows[rIntest][c]?.ToString() ?? "").Trim();
                if (et.Length == 0) continue;
                canali.Add(et);
                colonnaCanale.Add(c);
            }
        }

        var letture = canali.ToDictionary(x => x, _ => (IReadOnlyList<LetturaTempLog>)new List<LetturaTempLog>(),
            StringComparer.OrdinalIgnoreCase);
        var buffer = canali.ToDictionary(x => x, _ => new List<LetturaTempLog>(), StringComparer.OrdinalIgnoreCase);

        for (int r = rIntest + 1; r < t.Rows.Count; r++)
        {
            string ora = FormattaOra(t.Rows[r][0]);
            bool rigaVuota = true;
            for (int k = 0; k < canali.Count; k++)
            {
                double? v = LeggiNumero(t.Rows[r][colonnaCanale[k]]);
                if (v.HasValue) rigaVuota = false;
                buffer[canali[k]].Add(new LetturaTempLog { Ora = ora, Valore = v });
            }
            if (rigaVuota && string.IsNullOrWhiteSpace(ora))
            {
                // riga interamente vuota: rimuovi l'ultima aggiunta e fermati
                foreach (var k in canali) buffer[k].RemoveAt(buffer[k].Count - 1);
                break;
            }
        }
        foreach (var k in canali) letture[k] = buffer[k];

        return new FileTempLog
        {
            DataDal = dal,
            DataAl = al,
            NomeFile = nomeFile,
            Canali = canali,
            Letture = letture
        };
    }

    private static DateTime? LeggiData(System.Data.DataTable t, int r, int c)
    {
        if (r >= t.Rows.Count || c >= t.Columns.Count) return null;
        var v = t.Rows[r][c];
        return v switch
        {
            DateTime d => d,
            double num => DateTime.FromOADate(num),
            _ => DateTime.TryParse(v?.ToString(), out var p) ? p : null
        };
    }

    private static double? LeggiNumero(object? v)
    {
        if (v is null || v is DBNull) return null;
        if (v is double d) return d;
        if (v is int i) return i;
        var s = v.ToString()?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var a)) return a;
        if (double.TryParse(s.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var b)) return b;
        return null;
    }

    private static string FormattaOra(object? v) => v switch
    {
        null or DBNull => "",
        DateTime d => d.ToString("HH:mm", CultureInfo.InvariantCulture),
        double num => DateTime.FromOADate(num).ToString("HH:mm", CultureInfo.InvariantCulture),
        _ => v.ToString()?.Trim() ?? ""
    };
}
