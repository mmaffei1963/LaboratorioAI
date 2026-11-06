using System.Data.Common;
using System.Data.Odbc;
using LaboratorioAI.Tracciabilita.Core.Importazione;

namespace LaboratorioAI.Tracciabilita.Importazione;

/// <summary>
/// Legge il vecchio file Access con il driver ODBC di Windows (a 64 bit).
/// Sola lettura: il file si apre con ReadOnly=1 e si fanno solo SELECT.
/// </summary>
public sealed class LettoreAccessOdbc : ILettoreAccess
{
    private readonly string _percorso;

    public LettoreAccessOdbc(string percorso) => _percorso = percorso;

    public DatiAccess Leggi()
    {
        var cs = $"Driver={{Microsoft Access Driver (*.mdb, *.accdb)}};Dbq={_percorso};ReadOnly=1;";
        using var connessione = new OdbcConnection(cs);
        connessione.Open();

        // Si legge Ordine (che ha più informazioni di MAGAZZINO, creato da Ordine con la sola condizione «Lotto compilato»).
        var magazzino = new List<RigaMagazzino>();
        var n = 0;
        foreach (var r in Righe(connessione, "Ordine", "[Lotto] IS NOT NULL"))
        {
            n++;
            var numero = Numero(r, "ID") is { } id ? (int)id : n;   // il numero «ID» di Ordine ritrova la riga nel vecchio programma
            magazzino.Add(new RigaMagazzino(
                numero,
                Testo(r, "Tipo Documento"), Data(r, "Data Doc"), Testo(r, "N Doc"),
                Testo(r, "Fornitore"), Testo(r, "Articolo"), Numero(r, "Quantita"), Testo(r, "Unita di misura"),
                Testo(r, "Lotto"), Data(r, "Data Impegno"), Testo(r, "Impegno"), Testo(r, "Ente Bio"),
                Numero(r, "IDMatrice") is > 0 and { } im ? (int)im : null,
                Testo(r, "Data scadenza"), Testo(r, "Data produzione"), Testo(r, "Giudizio di accettazione"), Testo(r, "Note e raclami"),
                Testo(r, "Integrita"), Testo(r, "Sottovuoto"), Testo(r, "Controllo temperatura"), Testo(r, "Accettazione Temperatura"),
                Testo(r, "Prelievo campione"), Testo(r, "Analisi laboratorio"), Testo(r, "Verifica organolettica o visiva"), Testo(r, "Cetificato n")));
        }

        var (matriciConId, righeControlli, testiControlli) = ValoriAccess.LeggiControlli(connessione);

        // Alcuni fornitori non hanno il codice: si usa la partita IVA oppure, in mancanza, la ragione sociale.
        var fornitori = Righe(connessione, "Anag_fornitori")
            .Where(r => Testo(r, "RAGSOC") is not null)
            .Select(r => new FornitoreAccess(
                Testo(r, "CODCF") ?? Testo(r, "PARTIVA") ?? "RS:" + Testo(r, "RAGSOC")!,
                Testo(r, "RAGSOC")!, Testo(r, "INDIR"), Testo(r, "CAP"),
                Testo(r, "LOCAL"), Testo(r, "PROV"), Testo(r, "PARTIVA"), Testo(r, "TEL")))
            .ToList();

        var materie = Righe(connessione, "Mat-fornitori")
            .Where(r => Testo(r, "MATERIALE") is not null && Testo(r, "CODCF") is not null)
            .Select(r => new MateriaFornitoreAccess(Testo(r, "MATERIALE")!, Testo(r, "CODCF")!))
            .ToList();

        var matrici = Righe(connessione, "Matrici").Select(r => Testo(r, "Matrice")).OfType<string>().ToList();

        return new DatiAccess(magazzino, fornitori, materie, matrici, matriciConId, righeControlli, testiControlli);
    }

    // Ogni riga diventa un dizionario nome colonna → valore (così non dipende dall'ordine delle colonne).
    private static IEnumerable<Dictionary<string, object?>> Righe(OdbcConnection connessione, string tabella, string? condizione = null)
    {
        using var comando = connessione.CreateCommand();
        comando.CommandText = $"SELECT * FROM [{tabella}]" + (condizione is null ? "" : " WHERE " + condizione) + (tabella == "Ordine" ? " ORDER BY [ID]" : "");
        using var lettore = comando.ExecuteReader();
        while (lettore.Read())
        {
            var riga = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < lettore.FieldCount; i++)
                riga[lettore.GetName(i)] = lettore.IsDBNull(i) ? null : lettore.GetValue(i);
            yield return riga;
        }
    }

    private static string? Testo(Dictionary<string, object?> r, string colonna) =>
        r.TryGetValue(colonna, out var v) && v is not null && !string.IsNullOrWhiteSpace(v.ToString()) ? v.ToString()!.Trim() : null;

    private static DateTime? Data(Dictionary<string, object?> r, string colonna) =>
        r.TryGetValue(colonna, out var v) && v is DateTime d ? d : null;

    private static decimal? Numero(Dictionary<string, object?> r, string colonna) =>
        r.TryGetValue(colonna, out var v) && v is not null && decimal.TryParse(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : null;
}
