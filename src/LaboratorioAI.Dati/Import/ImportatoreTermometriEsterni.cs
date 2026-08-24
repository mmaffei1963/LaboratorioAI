using System.Data;
using System.Globalization;
using System.Text;
using ExcelDataReader;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Dati.Import;

/// <summary>Importa lo storico dei termometri esterni dal file Excel dei clienti.</summary>
public interface IImportatoreTermometriEsterni
{
    IReadOnlyList<TermometroEsterno> Leggi(Stream contenuto);
}

/// <inheritdoc />
///
/// <remarks>
/// Preferisce il foglio «2011-2026» (registro corrente, intestazione a riga 1). Le colonne
/// sono mappate per testo dell'intestazione, così funziona anche con l'altro layout
/// (foglio «TARATURE», intestazione a riga 3). Vengono importate solo le tarature CORRENTI:
/// le righe con «RINNOVATO» o «DISMESSO» valorizzati (sostituite o ritirate) sono saltate.
/// Il flag «Fatturato» viene letto dalla colonna «Fatt».
/// </remarks>
public class ImportatoreTermometriEsterni : IImportatoreTermometriEsterni
{
    static ImportatoreTermometriEsterni() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public IReadOnlyList<TermometroEsterno> Leggi(Stream contenuto)
    {
        using var reader = ExcelReaderFactory.CreateReader(contenuto);
        var ds = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = false }
        });

        var t = ScegliFoglio(ds);
        if (t is null) return new List<TermometroEsterno>();

        int rIntest = TrovaIntestazione(t);
        if (rIntest < 0) return new List<TermometroEsterno>();
        var col = MappaColonne(t, rIntest);

        int? C(string k) => col.TryGetValue(k, out var i) ? i : null;
        int? cNum = C("rapporto"), cTip = C("tipologia"), cTar = C("esecuzione"), cScad = C("scadenza taratura"),
             cCli = C("cliente"), cRif = C("rif."), cNote = C("note"), cCons = C("consegna"),
             cOp = C("operatore"), cFatt2 = C("rif fattura"), cFatt = C("fatt"),
             cRinn = C("rinnovato"), cDism = C("dismesso");

        var righe = new List<TermometroEsterno>();
        for (int r = rIntest + 1; r < t.Rows.Count; r++)
        {
            var row = t.Rows[r];
            int? numero = cNum is int a ? Intero(row[a]) : null;
            string? tip = Testo(row, cTip), cliente = Testo(row, cCli);
            if (numero is null && string.IsNullOrWhiteSpace(tip) && string.IsNullOrWhiteSpace(cliente)) continue;

            // solo tarature correnti: salta rinnovate o dismesse
            if (!string.IsNullOrWhiteSpace(Testo(row, cRinn))) continue;
            if (!string.IsNullOrWhiteSpace(Testo(row, cDism))) continue;

            righe.Add(new TermometroEsterno
            {
                NumeroRapporto = numero,
                Tipologia = tip,
                DataTaratura = Data(row, cTar),
                DataScadenza = Data(row, cScad),
                Cliente = cliente,
                Riferimento = Testo(row, cRif),
                Note = Testo(row, cNote),
                DataConsegna = Data(row, cCons),
                OperatoreConsegna = Testo(row, cOp),
                RifFattura = Testo(row, cFatt2),
                Fatturato = Flag(row, cFatt)   // colonna «Fatt» = «Sì» → già fatturato
            });
        }
        return righe;
    }

    private static DataTable? ScegliFoglio(DataSet ds)
    {
        if (ds.Tables.Contains("2011-2026")) return ds.Tables["2011-2026"];
        // altrimenti il foglio con più righe che ha una colonna «N°Rapporto di prova»
        DataTable? migliore = null;
        foreach (DataTable dt in ds.Tables)
            if (TrovaIntestazione(dt) >= 0 && (migliore is null || dt.Rows.Count > migliore.Rows.Count))
                migliore = dt;
        return migliore ?? (ds.Tables.Count > 0 ? ds.Tables[0] : null);
    }

    /// <summary>Riga (0-based) che contiene «Rapporto di prova» in una cella; -1 se assente.</summary>
    private static int TrovaIntestazione(DataTable t)
    {
        for (int r = 0; r < Math.Min(6, t.Rows.Count); r++)
            for (int c = 0; c < t.Columns.Count; c++)
                if ((t.Rows[r][c]?.ToString() ?? "").ToLowerInvariant().Contains("rapporto di prova"))
                    return r;
        return -1;
    }

    /// <summary>Mappa chiave→indice colonna dal testo dell'intestazione.</summary>
    private static Dictionary<string, int> MappaColonne(DataTable t, int rIntest)
    {
        var mappa = new Dictionary<string, int>();
        void Set(string chiave, int i) { if (!mappa.ContainsKey(chiave)) mappa[chiave] = i; }
        for (int c = 0; c < t.Columns.Count; c++)
        {
            var h = (t.Rows[rIntest][c]?.ToString() ?? "").Trim().ToLowerInvariant();
            if (h.Length == 0) continue;
            if (h.Contains("rapporto")) Set("rapporto", c);
            else if (h.Contains("tipologia")) Set("tipologia", c);
            else if (h.Contains("esecuzione")) Set("esecuzione", c);
            else if (h.Contains("scadenza taratura")) Set("scadenza taratura", c);
            else if (h.Contains("cliente")) Set("cliente", c);
            else if (h.StartsWith("rif.")) Set("rif.", c);
            else if (h.Contains("note")) Set("note", c);
            else if (h.Contains("consegna") && !h.Contains("operatore")) Set("consegna", c);
            else if (h.Contains("operatore")) Set("operatore", c);
            else if (h.Contains("rif fattura")) Set("rif fattura", c);
            else if (h == "fatt") Set("fatt", c);
            else if (h.Contains("rinnovato")) Set("rinnovato", c);
            else if (h.Contains("dismesso")) Set("dismesso", c);
        }
        return mappa;
    }

    private static string? Testo(DataRow row, int? c)
    {
        if (c is not int i || i >= row.Table.Columns.Count) return null;
        var s = row[i]?.ToString()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static bool Flag(DataRow row, int? c)
    {
        var s = Testo(row, c)?.ToLowerInvariant();
        return !string.IsNullOrEmpty(s) && s is "si" or "sì" or "x" or "1" or "true" or "vero" or "ok";
    }

    private static int? Intero(object? v)
    {
        if (v is null || v is DBNull) return null;
        if (v is double d) return (int)d;
        return int.TryParse(v.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static DateTime? Data(DataRow row, int? c)
    {
        if (c is not int i || i >= row.Table.Columns.Count) return null;
        var v = row[i];
        return v switch
        {
            DateTime dt => dt,
            double num => DateTime.FromOADate(num),
            _ => DateTime.TryParse(v?.ToString(), out var p) ? p : null
        };
    }
}
