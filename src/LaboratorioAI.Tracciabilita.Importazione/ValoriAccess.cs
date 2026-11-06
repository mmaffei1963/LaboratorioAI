using System.Data.Odbc;
using System.Globalization;
using System.Text;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Importazione;

namespace LaboratorioAI.Tracciabilita.Importazione;

/// <summary>
/// Sola lettura. Per capire come tradurre i controlli e le scadenze elenca i valori che compaiono
/// nelle colonne di esito (es. «OK», «Conforme») e fa il raggruppamento dei profili di controllo.
/// Non stampa nomi di fornitori, indirizzi o numeri di documento.
/// </summary>
public static class ValoriAccess
{
    private static readonly string[] ColonneEsito =
    {
        "Stato d'igiene", "Temperatura", "Controllo temperatura", "Accettazione Temperatura", "Integrita", "TMC",
        "Sottovuoto", "Prelievo campione", "Analisi laboratorio", "Verifica organolettica o visiva",
        "Giudizio di accettazione", "Ente Bio", "Impegno", "Unita di misura"
    };

    public static string Descrivi(string percorso)
    {
        var sb = new StringBuilder();
        var cs = $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={percorso};ReadOnly=1;";
        using var c = new OdbcConnection(cs);
        c.Open();

        sb.AppendLine("VALORI DELLE COLONNE DI ESITO (righe di Ordine con Lotto compilato)");
        foreach (var col in ColonneEsito)
        {
            sb.AppendLine();
            sb.AppendLine($"[{col}]");
            try
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"SELECT [{col}], COUNT(*) FROM [Ordine] WHERE [Lotto] IS NOT NULL GROUP BY [{col}] ORDER BY COUNT(*) DESC";
                using var r = cmd.ExecuteReader();
                var n = 0;
                var altri = 0;
                while (r.Read())
                {
                    if (n++ < 20)
                        sb.AppendLine($"    {(r.IsDBNull(0) ? "(vuoto)" : Pulisci(r.GetValue(0).ToString()))}: {r.GetValue(1)}");
                    else altri++;
                }
                if (altri > 0) sb.AppendLine($"    … e altri {altri} valori diversi");
            }
            catch (Exception e) { sb.AppendLine("    errore: " + e.Message); }
        }

        sb.AppendLine();
        sb.AppendLine("DATE SCRITTE COME TESTO");
        foreach (var col in new[] { "Data scadenza", "Data produzione" })
        {
            try
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"SELECT [{col}] FROM [Ordine] WHERE [Lotto] IS NOT NULL AND [{col}] IS NOT NULL";
                using var r = cmd.ExecuteReader();
                int pieni = 0, leggibili = 0;
                var nonLeggibili = new Dictionary<string, int>();
                var esempi = new List<string>();
                var it = new CultureInfo("it-IT");
                while (r.Read())
                {
                    var t = r.GetValue(0)?.ToString()?.Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    pieni++;
                    if (DateTime.TryParse(t, it, DateTimeStyles.None, out _)) { leggibili++; if (esempi.Count < 5) esempi.Add(t); }
                    else nonLeggibili[t] = nonLeggibili.GetValueOrDefault(t) + 1;
                }
                sb.AppendLine($"[{col}] compilate: {pieni}, leggibili come data: {leggibili}");
                sb.AppendLine("    esempi leggibili: " + string.Join(" | ", esempi));
                foreach (var kv in nonLeggibili.OrderByDescending(k => k.Value).Take(12))
                    sb.AppendLine($"    non leggibile: «{Pulisci(kv.Key)}» ×{kv.Value}");
            }
            catch (Exception e) { sb.AppendLine($"[{col}] errore: " + e.Message); }
        }

        sb.AppendLine();
        sb.AppendLine("ALTRE COLONNE (solo conteggi)");
        foreach (var (col, cond) in new[]
        {
            ("IDMatrice compilato", "[IDMatrice] IS NOT NULL"), ("IDMatrice vuoto", "[IDMatrice] IS NULL"),
            ("Cetificato n compilato", "[Cetificato n] IS NOT NULL"), ("Note e raclami compilate", "[Note e raclami] IS NOT NULL"),
            ("Prezzo compilato", "[Prezzo] IS NOT NULL"), ("Data Consegna compilata", "[Data Consegna] IS NOT NULL"),
            ("ID compilato", "[ID] IS NOT NULL")
        })
        {
            try
            {
                using var cmd = c.CreateCommand();
                cmd.CommandText = $"SELECT COUNT(*) FROM [Ordine] WHERE [Lotto] IS NOT NULL AND {cond}";
                sb.AppendLine($"  {col}: {cmd.ExecuteScalar()}");
            }
            catch (Exception e) { sb.AppendLine($"  {col}: errore ({e.Message})"); }
        }

        sb.AppendLine();
        sb.Append(Profili(c));
        return sb.ToString();
    }

    /// <summary>Legge matrici, «Tabella controlli» e testi dei controlli (usato anche dall'importazione).</summary>
    public static (List<MatriceAccess> Matrici, List<RigaControlliMatrice> Righe, List<TestoControllo> Testi) LeggiControlli(OdbcConnection c)
    {
        var matrici = new List<MatriceAccess>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT [IDMatrice], [Matrice] FROM [Matrici]";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                if (!r.IsDBNull(0) && !r.IsDBNull(1))
                    matrici.Add(new MatriceAccess(Convert.ToInt32(r.GetValue(0)), r.GetValue(1).ToString()!.Trim()));
        }

        // Ogni tabella ha l'id nella prima colonna e la descrizione in «Descrizione».
        var testi = new List<TestoControllo>();
        foreach (var (tabella, tipo) in new[]
        {
            ("ControlloEtichettatura", TipoControllo.Etichettatura), ("ControlloConfezione", TipoControllo.Confezione),
            ("ControlloTemperatura", TipoControllo.Temperatura), ("ControlloOrganolettico", TipoControllo.Organolettico),
            ("ControlloPrelievo", TipoControllo.PrelievoAnalisi), ("ControlloCertificati", TipoControllo.Certificati),
            ("ControlloPallet", TipoControllo.Pallet)
        })
        {
            using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT * FROM [{tabella}]";
            using var r = cmd.ExecuteReader();
            var posDescr = -1;
            for (var i = 0; i < r.FieldCount; i++) if (r.GetName(i).Equals("Descrizione", StringComparison.OrdinalIgnoreCase)) posDescr = i;
            while (r.Read())
                if (!r.IsDBNull(0) && posDescr >= 0 && !r.IsDBNull(posDescr))
                    testi.Add(new TestoControllo(tipo, Convert.ToInt32(r.GetValue(0)), r.GetValue(posDescr).ToString()!));
        }

        var righe = new List<RigaControlliMatrice>();
        using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT [Matrice], [Controllo Etichettatura], [Controllo Confezione], [Controllo Temperatura], [Controllo Organolettico], " +
                              "[Prelievo per Analisi di Laboratorio], [Certificati Fornitore], [ControlloPallet] FROM [Tabella controlli]";
            using var r = cmd.ExecuteReader();
            int? N(int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
            while (r.Read())
                if (N(0) is { } m) righe.Add(new RigaControlliMatrice(m, N(1), N(2), N(3), N(4), N(5), N(6), N(7)));
        }
        return (matrici, righe, testi);
    }

    private static string Profili(OdbcConnection c)
    {
        var sb = new StringBuilder("PROFILI DI CONTROLLO (matrici con gli stessi controlli)\n");
        var (elencoMatrici, righe, testi) = LeggiControlli(c);
        var matrici = elencoMatrici.GroupBy(m => m.IdMatrice).ToDictionary(g => g.Key, g => g.First().Nome);

        var gruppi = ProfiliControlloAccess.Raggruppa(righe, testi);
        sb.AppendLine($"Righe in «Tabella controlli»: {righe.Count}  ·  profili distinti: {gruppi.Count}");
        var k = 0;
        foreach (var g in gruppi)
        {
            k++;
            sb.AppendLine();
            sb.AppendLine($"Profilo {k}: {g.IdMatrici.Count} matrici");
            sb.AppendLine("  matrici: " + string.Join(", ", g.IdMatrici.Select(i => matrici.GetValueOrDefault(i, "#" + i))));
            foreach (var kv in g.Testi.OrderBy(x => x.Key))
                sb.AppendLine($"  {kv.Key}: {Pulisci(kv.Value, 140)}");
        }
        return sb.ToString();
    }

    private static string Pulisci(string? t, int max = 60)
    {
        t = (t ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
        return t.Length <= max ? t : t[..max] + "…";
    }
}
