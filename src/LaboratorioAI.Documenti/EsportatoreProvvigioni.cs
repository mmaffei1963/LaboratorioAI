using ClosedXML.Excel;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using LaboratorioAI.Dati.Provvigioni;

namespace LaboratorioAI.Documenti;

/// <summary>Esporta in Excel le tabelle della pagina Provvigioni di un commerciale.</summary>
public interface IEsportatoreProvvigioni
{
    byte[] Genera(string commerciale, IReadOnlyList<RigaTrimestre> trimestri, RisultatoProvvigioniLab risultato,
        IReadOnlyDictionary<string, string> nomiClienti, DettaglioVoci? dettaglio,
        IReadOnlyList<ConvenzioneProvvigioni>? convenzioni = null);
    string NomeFile(string commerciale);
}

/// <inheritdoc />
public class EsportatoreProvvigioni : IEsportatoreProvvigioni
{
    private const string Euro = "#,##0.00";

    public byte[] Genera(string commerciale, IReadOnlyList<RigaTrimestre> trimestri, RisultatoProvvigioniLab risultato,
        IReadOnlyDictionary<string, string> nomiClienti, DettaglioVoci? dettaglio,
        IReadOnlyList<ConvenzioneProvvigioni>? convenzioni = null)
    {
        convenzioni ??= Array.Empty<ConvenzioneProvvigioni>();
        // Convenzione in vigore per un cliente a una data: la più recente iniziata entro quella data.
        ConvenzioneProvvigioni? ConvenzioneAl(string cliente, DateTime data) => convenzioni
            .Where(c => c.CodiceCliente == cliente && c.DataInizio.Date <= data.Date)
            .OrderByDescending(c => c.DataInizio).FirstOrDefault();
        // 1 = primi 12 mesi dalla data di inizio, 2 = dal 13° al 24° mese, …
        static int AnnoDiContratto(DateTime inizio, DateTime data)
        {
            var mesi = (data.Year - inizio.Year) * 12 + data.Month - inizio.Month - (data.Day < inizio.Day ? 1 : 0);
            return Math.Max(0, mesi) / 12 + 1;
        }
        using var cartella = new XLWorkbook();
        var sotto = $"{commerciale} — estratto il {DateTime.Now:dd/MM/yyyy HH:mm}. Trimestri non chiusi: valori provvisori.";
        string Nome(string cod) => nomiClienti.TryGetValue(cod, out var n) && n.Length > 0 ? n : "";

        // ── Trimestri ──
        {
            var f = cartella.Worksheets.Add("Trimestri");
            const int col = 5;
            var r = StileFoglio.Titolo(f, "Provvigioni per trimestre di incasso", sotto, col);
            string[] h = { "Trimestre", "Incassato nella base €", "Note di credito €", "Provvigione €", "Stato" };
            for (var i = 0; i < h.Length; i++) f.Cell(r, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, r, col, StileFoglio.Rosso);
            var prima = r + 1;
            foreach (var t in trimestri)
            {
                r++;
                f.Cell(r, 1).Value = t.Etichetta;
                f.Cell(r, 2).Value = t.Incassato;
                f.Cell(r, 3).Value = t.NoteCredito;
                f.Cell(r, 4).Value = t.Provvigione;
                f.Cell(r, 5).Value = t.Chiuso ? "chiuso" : "provvisorio";
            }
            r++;
            f.Cell(r, 1).Value = "Totale";
            f.Cell(r, 2).FormulaA1 = $"SUM(B{prima}:B{r - 1})";
            f.Cell(r, 3).FormulaA1 = $"SUM(C{prima}:C{r - 1})";
            f.Cell(r, 4).FormulaA1 = $"SUM(D{prima}:D{r - 1})";
            f.Row(r).Style.Font.Bold = true;
            f.Range(prima, 2, r, 4).Style.NumberFormat.Format = Euro;
            StileFoglio.Rifinisci(f, prima - 1, r, col);
        }

        // ── Per cliente ──
        {
            var f = cartella.Worksheets.Add("Per cliente");
            const int col = 5;
            var r = StileFoglio.Titolo(f, "Provvigioni per cliente (tutto il periodo)", sotto, col);
            string[] h = { "Cliente", "Codice", "Voce (ultima)", "Base €", "Provvigione €" };
            for (var i = 0; i < h.Length; i++) f.Cell(r, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, r, col, StileFoglio.Rosso);
            var prima = r + 1;
            foreach (var g in risultato.Movimenti.GroupBy(m => m.Cliente).OrderByDescending(g => g.Sum(m => m.Importo)))
            {
                r++;
                f.Cell(r, 1).Value = Nome(g.Key);
                f.Cell(r, 2).Value = g.Key;
                f.Cell(r, 3).Value = EtichetteProvvigioni.Voce(g.OrderBy(m => m.Data).Last().Voce);
                f.Cell(r, 4).Value = g.Sum(m => m.Base);
                f.Cell(r, 5).Value = g.Sum(m => m.Importo);
            }
            f.Range(prima, 4, Math.Max(prima, r), 5).Style.NumberFormat.Format = Euro;
            StileFoglio.Rifinisci(f, prima - 1, r, col);
        }

        // ── Dettaglio per voce di fattura ──
        if (dettaglio is not null && dettaglio.IdBase is not null)
        {
            var f = cartella.Worksheets.Add("Dettaglio per voce");
            var col = 2 + dettaglio.Mostrate.Count + 2;
            var nomeBase = dettaglio.Basi.FirstOrDefault(b => b.IdBaseCalcolo == dettaglio.IdBase)?.Nome ?? "";
            var filtro = dettaglio.Mostrate.Count == dettaglio.Colonne.Count ? "tutte le voci" : string.Join(", ", dettaglio.Mostrate);
            var r = StileFoglio.Titolo(f, $"Dettaglio per voce di fattura — base «{nomeBase}», anno {dettaglio.Anno}",
                sotto + $" Voci: {filtro}. Base e provvigione ripartite sulle righe di fattura in proporzione al netto.", col);
            f.Cell(r, 1).Value = "Cliente";
            f.Cell(r, 2).Value = "Codice";
            for (var i = 0; i < dettaglio.Mostrate.Count; i++) f.Cell(r, 3 + i).Value = dettaglio.Mostrate[i] + " €";
            f.Cell(r, col - 1).Value = "Totale base €";
            f.Cell(r, col).Value = "Provvigione €";
            StileFoglio.Intestazione(f, r, col, StileFoglio.Rosso);
            var prima = r + 1;
            foreach (var x in dettaglio.Righe)
            {
                r++;
                f.Cell(r, 1).Value = Nome(x.Cliente);
                f.Cell(r, 2).Value = x.Cliente;
                for (var i = 0; i < dettaglio.Mostrate.Count; i++)
                    f.Cell(r, 3 + i).Value = x.PerVoce.TryGetValue(dettaglio.Mostrate[i], out var v) ? v : 0m;
                f.Cell(r, col - 1).Value = x.Base;
                f.Cell(r, col).Value = x.Provvigione;
            }
            r++;
            f.Cell(r, 1).Value = "Totale";
            for (var c = 3; c <= col; c++)
                f.Cell(r, c).FormulaA1 = $"SUM({f.Cell(prima, c).Address.ToStringRelative()}:{f.Cell(r - 1, c).Address.ToStringRelative()})";
            f.Row(r).Style.Font.Bold = true;
            f.Range(prima, 3, r, col).Style.NumberFormat.Format = Euro;
            StileFoglio.Rifinisci(f, prima - 1, r, col);
        }

        // ── Movimenti ──
        {
            var f = cartella.Worksheets.Add("Movimenti");
            var col = convenzioni.Count > 0 ? 12 : 10;
            var r = StileFoglio.Titolo(f, "Movimenti di provvigione (uno per incasso, nota di credito o obiettivo)", sotto, col);
            string[] h = convenzioni.Count > 0
                ? new[] { "Trimestre", "Data", "Cliente", "Codice", "Riferimento", "Tipo", "Voce", "Base €", "Aliquota %", "Provvigione €", "Inizio convenzione", "Anno di contratto" }
                : new[] { "Trimestre", "Data", "Cliente", "Codice", "Riferimento", "Tipo", "Voce", "Base €", "Aliquota %", "Provvigione €" };
            for (var i = 0; i < h.Length; i++) f.Cell(r, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, r, col, StileFoglio.Rosso);
            var prima = r + 1;
            foreach (var m in risultato.Movimenti)
            {
                r++;
                f.Cell(r, 1).Value = m.Trimestre;
                f.Cell(r, 2).Value = m.Data;
                f.Cell(r, 3).Value = Nome(m.Cliente);
                f.Cell(r, 4).Value = m.Cliente;
                f.Cell(r, 5).Value = m.Riferimento;
                f.Cell(r, 6).Value = m.Tipo switch
                {
                    TipoMovimento.Incasso => "Incasso",
                    TipoMovimento.NotaCredito => "Nota di credito",
                    _ => "Obiettivo"
                };
                f.Cell(r, 7).Value = EtichetteProvvigioni.Voce(m.Voce);
                f.Cell(r, 8).Value = m.Base;
                f.Cell(r, 9).Value = m.Aliquota * 100m;
                f.Cell(r, 10).Value = m.Importo;
                if (convenzioni.Count > 0 && ConvenzioneAl(m.Cliente, m.Data) is { } cv)
                {
                    f.Cell(r, 11).Value = cv.DataInizio;
                    f.Cell(r, 12).Value = AnnoDiContratto(cv.DataInizio, m.Data);
                }
            }
            if (r >= prima)
            {
                f.Range(prima, 2, r, 2).Style.DateFormat.Format = "dd/MM/yyyy";
                f.Range(prima, 8, r, 8).Style.NumberFormat.Format = Euro;
                f.Range(prima, 9, r, 9).Style.NumberFormat.Format = "0.##";
                f.Range(prima, 10, r, 10).Style.NumberFormat.Format = Euro;
                if (col > 10) f.Range(prima, 11, r, 11).Style.DateFormat.Format = "dd/MM/yyyy";
                f.Range(prima - 1, 1, r, col).SetAutoFilter();
            }
            StileFoglio.Rifinisci(f, prima - 1, r, col);
        }

        // ── Convenzioni (solo per chi le ha) ──
        if (convenzioni.Count > 0)
        {
            var f = cartella.Worksheets.Add("Convenzioni");
            const int col = 9;
            var r = StileFoglio.Titolo(f, "Convenzioni e data di inizio", sotto + " Anno di contratto calcolato a oggi dalla data di inizio.", col);
            string[] h = { "Cliente", "Codice", "Inizio", "Data da verificare", "Origine", "Origine confermata", "Anno di contratto oggi", "Attiva", "Descrizione" };
            for (var i = 0; i < h.Length; i++) f.Cell(r, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, r, col, StileFoglio.Rosso);
            var prima = r + 1;
            foreach (var cv in convenzioni.OrderBy(c => c.DataInizio))
            {
                r++;
                f.Cell(r, 1).Value = Nome(cv.CodiceCliente);
                f.Cell(r, 2).Value = cv.CodiceCliente;
                f.Cell(r, 3).Value = cv.DataInizio;
                f.Cell(r, 4).Value = cv.DataDaVerificare ? "sì" : "";
                f.Cell(r, 5).Value = cv.Origine == OrigineConvenzione.IndicataDalCommerciale ? "Indicata" : "Segnalata da MARE.A";
                f.Cell(r, 6).Value = cv.OrigineConfermata ? "sì" : "proposta";
                f.Cell(r, 7).Value = AnnoDiContratto(cv.DataInizio, DateTime.Today);
                f.Cell(r, 8).Value = cv.Attivo ? "sì" : "no";
                f.Cell(r, 9).Value = cv.Descrizione ?? "";
            }
            if (r >= prima)
            {
                f.Range(prima, 3, r, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                f.Range(prima - 1, 1, r, col).SetAutoFilter();
            }
            StileFoglio.Rifinisci(f, prima - 1, r, col);
        }

        // ── Premio ──
        if (risultato.Premi.Count > 0)
        {
            var f = cartella.Worksheets.Add("Premio");
            const int col = 4;
            var r = StileFoglio.Titolo(f, "Premio annuale", sotto, col);
            string[] h = { "Anno", "Incassato clienti nuovi €", "Premio €", "Prossima soglia €" };
            for (var i = 0; i < h.Length; i++) f.Cell(r, i + 1).Value = h[i];
            StileFoglio.Intestazione(f, r, col, StileFoglio.Rosso);
            var prima = r + 1;
            foreach (var p in risultato.Premi)
            {
                r++;
                f.Cell(r, 1).Value = p.Anno;
                f.Cell(r, 2).Value = p.BaseIncassata;
                f.Cell(r, 3).Value = p.Premio;
                if (p.ProssimaSoglia is decimal ps) f.Cell(r, 4).Value = ps;
            }
            f.Range(prima, 2, r, 4).Style.NumberFormat.Format = Euro;
            StileFoglio.Rifinisci(f, prima - 1, r, col);
        }

        using var flusso = new MemoryStream();
        cartella.SaveAs(flusso);
        return flusso.ToArray();
    }

    public string NomeFile(string commerciale) =>
        $"Provvigioni_{new string(commerciale.Where(char.IsLetterOrDigit).ToArray())}_{DateTime.Now:yyyyMMdd}.xlsx";
}
