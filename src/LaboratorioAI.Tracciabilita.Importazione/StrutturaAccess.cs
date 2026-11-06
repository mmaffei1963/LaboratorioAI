using System.Data;
using System.Data.Odbc;
using System.Text;

namespace LaboratorioAI.Tracciabilita.Importazione;

/// <summary>
/// Sola lettura: elenca le tabelle del file Access con le loro colonne e il numero di righe.
/// Non stampa nessun dato (nomi, indirizzi): solo la struttura.
/// </summary>
public static class StrutturaAccess
{
    public static string Descrivi(string percorso)
    {
        var sb = new StringBuilder();
        var cs = $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={percorso};ReadOnly=1;";
        using var c = new OdbcConnection(cs);
        c.Open();

        var tabelle = new List<string>();
        var elenco = c.GetSchema("Tables");
        foreach (DataRow r in elenco.Rows)
        {
            var nome = r["TABLE_NAME"]?.ToString();
            var tipo = r["TABLE_TYPE"]?.ToString();
            if (string.IsNullOrEmpty(nome) || nome.StartsWith("MSys", StringComparison.OrdinalIgnoreCase)) continue;
            if (tipo is "TABLE" or "VIEW" or "LINK") tabelle.Add(nome);
        }
        tabelle.Sort(StringComparer.OrdinalIgnoreCase);

        sb.AppendLine("STRUTTURA DEL FILE ACCESS (nessun dato, solo nomi di tabelle e colonne)");
        foreach (var t in tabelle)
        {
            sb.AppendLine();
            sb.AppendLine($"[{t}]  righe: {Conta(c, $"SELECT COUNT(*) FROM [{t}]")}");
            try
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"SELECT * FROM [{t}] WHERE 1=0";
                using var lettore = cmd.ExecuteReader(CommandBehavior.SchemaOnly);
                var schema = lettore.GetSchemaTable();
                if (schema is null) continue;
                foreach (DataRow col in schema.Rows)
                    sb.AppendLine($"    {col["ColumnName"]}  ({(col["DataType"] as Type)?.Name})");
            }
            catch (Exception e)
            {
                sb.AppendLine("    (colonne non leggibili: " + e.Message + ")");
            }
        }

        // Confronto utile: MAGAZZINO nasce da Ordine (solo righe con Lotto).
        if (tabelle.Contains("Ordine", StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine();
            sb.AppendLine("CONFRONTO ORDINE / MAGAZZINO");
            sb.AppendLine($"  righe di Ordine: {Conta(c, "SELECT COUNT(*) FROM [Ordine]")}");
            sb.AppendLine($"  righe di Ordine con Lotto compilato: {Conta(c, "SELECT COUNT(*) FROM [Ordine] WHERE [Lotto] IS NOT NULL")}");
            sb.AppendLine($"  righe di MAGAZZINO: {Conta(c, "SELECT COUNT(*) FROM [MAGAZZINO]")}");
        }
        return sb.ToString();
    }

    private static string Conta(OdbcConnection c, string sql)
    {
        try
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToString(cmd.ExecuteScalar()) ?? "?";
        }
        catch (Exception e)
        {
            return "errore (" + e.Message + ")";
        }
    }
}
