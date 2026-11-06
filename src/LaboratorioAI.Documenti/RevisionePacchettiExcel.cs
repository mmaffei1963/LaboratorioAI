using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Excel per la revisione dei pacchetti di settore con i consulenti (decisione del 6/10/2026). Il foglio
/// «Analisi da sostituire» elenca le righe di analisi generiche importate dai vecchi Word («Tampone superficiale»,
/// «Analisi alimento»…) con i controlli ArcLab che potrebbero corrispondere; in riunione si sceglie l'ID del
/// controllo giusto e il file si reimporta. Gli altri fogli servono a consultare il catalogo e a rivedere i pacchetti.
/// </summary>
public static class RevisionePacchettiExcel
{
    public const string FoglioAnalisi = "Analisi da sostituire";
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    // Colonne del foglio «Analisi da sostituire» (1-based): l'importazione le legge per posizione.
    private const int CIdPacchetto = 1, CPacchetto = 2, CIdRiga = 3, CAnalisi = 4, CPrezzo = 5, CConoscenza = 6, CPossibili = 7,
        CIdScelto = 8, CNome = 9, CModalita = 10, CQuantita = 11, CPrezzoNuovo = 12, CTogli = 13, CNote = 14;

    public static string NomeFile() => $"Revisione_pacchetti_consulenza_{DateTime.Now:yyyyMMdd}.xlsx";

    /// <summary>Riga di attività di un pacchetto che è in realtà un'analisi generica da sostituire con un controllo.</summary>
    public static bool AnalisiGenerica(AttivitaConsulenza? a) =>
        a is not null && (a.Descrizione.StartsWith("Analisi laboratorio", StringComparison.OrdinalIgnoreCase) || a.IdTipoIncarico is 118 or 119);

    public static byte[] Genera(IReadOnlyList<PacchettoConsulenza> pacchetti, IReadOnlyDictionary<int, AttivitaConsulenza> attivita,
        IReadOnlyList<(int Id, string Nome)> controlli)
    {
        using var wb = new XLWorkbook();
        Istruzioni(wb.AddWorksheet("Istruzioni"));
        Analisi(wb.AddWorksheet(FoglioAnalisi), pacchetti, attivita, controlli);
        Catalogo(wb.AddWorksheet("Controlli ArcLab"), controlli);
        Riepilogo(wb.AddWorksheet("Pacchetti (riepilogo)"), pacchetti, attivita);
        wb.Worksheet(FoglioAnalisi).SetTabActive();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void Istruzioni(IXLWorksheet ws)
    {
        var righe = new[]
        {
            "Revisione dei pacchetti di settore – analisi di laboratorio",
            "",
            "1. Nel foglio «Analisi da sostituire» c'è una riga per ogni analisi generica dei pacchetti (es. «Tampone superficiale», «Analisi alimento»).",
            "2. Nella colonna «Controlli possibili» ci sono i controlli ArcLab che contengono le stesse parole: è solo un suggerimento.",
            "   Per cercarne altri usa il foglio «Controlli ArcLab» (filtro sulla colonna Controllo).",
            "3. Nella colonna gialla «ID controllo scelto» scrivi l'ID del controllo giusto. Più controlli in un'unica riga con un solo prezzo: 123-456.",
            "   Per mettere più analisi distinte al posto di una riga generica, copia la riga e scrivi un ID diverso in ciascuna.",
            "4. Facoltativo: nome nel contratto (per i gruppi), come si paga, analisi all'anno, prezzo (vuoto = listino o prezzo di commessa del cliente).",
            "5. «Togli la riga generica»: sì = dopo l'importazione la vecchia riga di attività sparisce dal pacchetto.",
            "6. Salva il file e caricalo in Laboratorio AI: Amministrazione → Attività e pacchetti consulenza → Pacchetti → «Importa revisione».",
            "   Le righe senza ID vengono ignorate; quelle già importate non si duplicano. Puoi importare più volte.",
            "",
            "Il foglio «Pacchetti (riepilogo)» riporta attività, contenuti e prezzi di ogni pacchetto, per rivedere i testi in riunione.",
        };
        for (var i = 0; i < righe.Length; i++) ws.Cell(i + 1, 1).Value = righe[i];
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Column(1).Width = 140;
    }

    private static void Analisi(IXLWorksheet ws, IReadOnlyList<PacchettoConsulenza> pacchetti,
        IReadOnlyDictionary<int, AttivitaConsulenza> attivita, IReadOnlyList<(int Id, string Nome)> controlli)
    {
        string[] intest = { "IdPacchetto", "Pacchetto", "IdRiga", "Analisi attuale", "Prezzo attuale €", "Per conoscenza",
            "Controlli possibili (ID – nome)", "ID controllo scelto", "Nome nel contratto (per i gruppi)", "Come si paga",
            "Analisi all'anno", "Prezzo € (vuoto = listino)", "Togli la riga generica", "Note" };
        for (var c = 0; c < intest.Length; c++) ws.Cell(1, c + 1).Value = intest[c];
        var r = 2;
        var cache = new Dictionary<string, string>();
        foreach (var p in pacchetti.Where(p => p.Attivo).OrderBy(p => p.Nome))
            foreach (var riga in p.Righe.OrderBy(x => x.Ordine))
            {
                attivita.TryGetValue(riga.IdAttivitaConsulenza, out var a);
                if (!AnalisiGenerica(a)) continue;
                var testo = string.IsNullOrWhiteSpace(riga.DescrizioneLibera) ? a!.Descrizione : riga.DescrizioneLibera!;
                if (!cache.TryGetValue(testo, out var possibili))
                    cache[testo] = possibili = string.Join("\n", Candidati(testo, controlli).Select(x => $"{x.Id} – {x.Nome}"));
                ws.Cell(r, CIdPacchetto).Value = p.IdPacchettoConsulenza;
                ws.Cell(r, CPacchetto).Value = p.Nome;
                ws.Cell(r, CIdRiga).Value = riga.IdRigaPacchetto;
                ws.Cell(r, CAnalisi).Value = testo;
                ws.Cell(r, CPrezzo).Value = riga.Prezzo;
                ws.Cell(r, CConoscenza).Value = riga.Informativa ? "sì" : "no";
                ws.Cell(r, CPossibili).Value = possibili;
                ws.Cell(r, CModalita).Value = "A pagamento";
                ws.Cell(r, CTogli).Value = "sì";
                r++;
            }
        var ultima = Math.Max(2, r - 1);
        var h = ws.Range(1, 1, 1, intest.Length);
        h.Style.Font.Bold = true;
        h.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
        h.Style.Alignment.WrapText = true;
        ws.Range(2, CIdScelto, ultima, CIdScelto).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFF3B0");
        // Testo: «12-3» non deve diventare una data.
        ws.Range(2, CIdScelto, ultima + 200, CIdScelto).Style.NumberFormat.Format = "@";
        ws.Range(2, CPossibili, ultima, CPossibili).Style.Alignment.WrapText = true;
        ws.Range(2, 1, ultima, intest.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        ws.Range(2, CPrezzo, ultima, CPrezzo).Style.NumberFormat.Format = "#,##0.00";
        ws.Range(2, CModalita, ultima, CModalita).CreateDataValidation().List("\"A pagamento,Compresa nel canone\"");
        ws.Range(2, CTogli, ultima, CTogli).CreateDataValidation().List("\"sì,no\"");
        double[] larghezze = { 8, 26, 7, 30, 10, 9, 60, 16, 30, 20, 10, 14, 12, 30 };
        for (var c = 0; c < larghezze.Length; c++) ws.Column(c + 1).Width = larghezze[c];
        ws.Columns(CIdPacchetto, CIdPacchetto).Hide();
        ws.Columns(CIdRiga, CIdRiga).Hide();
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, ultima, intest.Length).SetAutoFilter();
    }

    private static void Catalogo(IXLWorksheet ws, IReadOnlyList<(int Id, string Nome)> controlli)
    {
        ws.Cell(1, 1).Value = "ID";
        ws.Cell(1, 2).Value = "Controllo";
        var r = 2;
        foreach (var c in controlli.OrderBy(c => c.Nome)) { ws.Cell(r, 1).Value = c.Id; ws.Cell(r, 2).Value = c.Nome; r++; }
        ws.Range(1, 1, 1, 2).Style.Font.Bold = true;
        ws.Column(1).Width = 8;
        ws.Column(2).Width = 90;
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, Math.Max(2, r - 1), 2).SetAutoFilter();
    }

    private static void Riepilogo(IXLWorksheet ws, IReadOnlyList<PacchettoConsulenza> pacchetti, IReadOnlyDictionary<int, AttivitaConsulenza> attivita)
    {
        string[] intest = { "Pacchetto", "Tipo", "Descrizione", "Descrizione aggiuntiva", "Contenuti spuntati", "Prezzo €", "Anni successivi €", "Note" };
        for (var c = 0; c < intest.Length; c++) ws.Cell(1, c + 1).Value = intest[c];
        var r = 2;
        foreach (var p in pacchetti.Where(p => p.Attivo).OrderBy(p => p.Nome))
        {
            foreach (var riga in p.Righe.OrderBy(x => x.Ordine))
            {
                attivita.TryGetValue(riga.IdAttivitaConsulenza, out var a);
                var chiavi = (riga.Contenuti ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
                var contenuti = a?.Contenuti.Where(c => c.Attivo && (chiavi.Count == 0 ? c.Predefinito : c.Chiave is not null && chiavi.Contains(c.Chiave)))
                    .OrderBy(c => c.Ordine).Select(c => "– " + c.Testo) ?? Enumerable.Empty<string>();
                ws.Cell(r, 1).Value = p.Nome;
                ws.Cell(r, 2).Value = AnalisiGenerica(a) ? "Analisi generica" : "Attività";
                ws.Cell(r, 3).Value = a?.Descrizione ?? "";
                ws.Cell(r, 4).Value = riga.DescrizioneLibera ?? "";
                ws.Cell(r, 5).Value = string.Join("\n", contenuti);
                ws.Cell(r, 6).Value = riga.Prezzo;
                if (riga.PrezzoSuccessivi is decimal ps) ws.Cell(r, 7).Value = ps;
                ws.Cell(r, 8).Value = riga.Informativa ? "per conoscenza" : "";
                r++;
            }
            foreach (var an in p.Analisi.OrderBy(x => x.Ordine))
            {
                ws.Cell(r, 1).Value = p.Nome;
                ws.Cell(r, 2).Value = "Analisi (controllo ArcLab)";
                ws.Cell(r, 3).Value = $"{an.Descrizione} ({AnalisiConsulenza.Riferimento(an.IdControlloArcLab, an.IdControlliOrigine)})";
                ws.Cell(r, 4).Value = an.Modalita == ModalitaAnalisiConsulenza.CompresaNelCanone ? "compresa nel canone" : "a pagamento";
                ws.Cell(r, 5).Value = an.QuantitaAnnua is decimal q ? q.ToString("0.##", It) + " all'anno" : "";
                if (an.Prezzo is decimal pr) ws.Cell(r, 6).Value = pr;
                ws.Cell(r, 8).Value = an.Note ?? "";
                r++;
            }
        }
        var ultima = Math.Max(2, r - 1);
        ws.Range(1, 1, 1, intest.Length).Style.Font.Bold = true;
        ws.Range(1, 1, 1, intest.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF5");
        ws.Range(2, 5, ultima, 5).Style.Alignment.WrapText = true;
        ws.Range(2, 1, ultima, intest.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        ws.Range(2, 6, ultima, 7).Style.NumberFormat.Format = "#,##0.00";
        double[] larghezze = { 24, 18, 40, 30, 70, 10, 12, 16 };
        for (var c = 0; c < larghezze.Length; c++) ws.Column(c + 1).Width = larghezze[c];
        ws.SheetView.FreezeRows(1);
        ws.Range(1, 1, ultima, intest.Length).SetAutoFilter();
    }

    // ── Suggerimenti di controlli ───────────────────────────────────

    private static readonly HashSet<string> Ignora = new(StringComparer.OrdinalIgnoreCase)
        { "analisi", "prelievo", "punto", "punti", "campione", "controllo", "laboratorio", "ricerca", "determinazione", "della", "delle", "degli" };

    /// <summary>Controlli il cui nome contiene le parole significative del testo (radice di 6 lettere), i più pertinenti per primi.</summary>
    public static IReadOnlyList<(int Id, string Nome)> Candidati(string testo, IReadOnlyList<(int Id, string Nome)> controlli, int quanti = 8)
    {
        var radici = Normalizza(testo).Split(new[] { ' ', '(', ')', ',', '.', '-', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !Ignora.Contains(w)).Select(w => w.Length > 6 ? w[..6] : w).Distinct().ToList();
        if (radici.Count == 0) return Array.Empty<(int, string)>();
        return controlli
            .Select(c => (c, punti: radici.Count(rd => Normalizza(c.Nome).Contains(rd))))
            .Where(x => x.punti > 0)
            .OrderByDescending(x => x.punti).ThenBy(x => x.c.Nome.Length)
            .Take(quanti).Select(x => x.c).ToList();
    }

    private static string Normalizza(string s)
    {
        var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return new string(d.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
    }

    // ── Importazione ────────────────────────────────────────────────

    /// <summary>Riga compilata del foglio «Analisi da sostituire».</summary>
    public sealed record RigaRevisione(int Riga, int IdPacchetto, int? IdRigaGenerica, string IdScelti, string? Nome,
        ModalitaAnalisiConsulenza Modalita, decimal? QuantitaAnnua, decimal? Prezzo, bool TogliGenerica, string? Note);

    /// <summary>Legge le righe con un ID controllo scelto. Le altre si ignorano.</summary>
    public static IReadOnlyList<RigaRevisione> Leggi(Stream file)
    {
        using var wb = new XLWorkbook(file);
        if (!wb.TryGetWorksheet(FoglioAnalisi, out var ws))
            throw new InvalidOperationException($"Nel file manca il foglio «{FoglioAnalisi}»: usa l'Excel esportato da Laboratorio AI.");
        var res = new List<RigaRevisione>();
        var ultima = ws.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= ultima; r++)
        {
            var ids = Testo(ws.Cell(r, CIdScelto));
            if (string.IsNullOrWhiteSpace(ids)) continue;
            if (Numero(ws.Cell(r, CIdPacchetto)) is not decimal idp) continue;
            var mod = Testo(ws.Cell(r, CModalita)) ?? "";
            var togli = (Testo(ws.Cell(r, CTogli)) ?? "sì").Trim().ToLowerInvariant();
            res.Add(new RigaRevisione(r, (int)idp, Numero(ws.Cell(r, CIdRiga)) is decimal ir ? (int)ir : null, ids!, Testo(ws.Cell(r, CNome)),
                mod.Contains("compres", StringComparison.OrdinalIgnoreCase) ? ModalitaAnalisiConsulenza.CompresaNelCanone : ModalitaAnalisiConsulenza.APagamento,
                Numero(ws.Cell(r, CQuantita)) is decimal q && q > 0 ? q : null,
                Numero(ws.Cell(r, CPrezzoNuovo)) is decimal pz && pz > 0 ? pz : null,
                !(togli.StartsWith("n")), Testo(ws.Cell(r, CNote))));
        }
        return res;
    }

    /// <summary>ID dei controlli scritti nella cella: "123", "123-456", "123; 456"…</summary>
    public static IReadOnlyList<int> IdDaTesto(string testo) =>
        testo.Split(new[] { '-', ';', ',', ' ', '+', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => int.TryParse(x.Trim(), out var n) ? n : 0).Where(n => n > 0).Distinct().ToList();

    private static string? Testo(IXLCell c)
    {
        var s = c.GetFormattedString()?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static decimal? Numero(IXLCell c)
    {
        if (c.IsEmpty()) return null;
        if (c.DataType == XLDataType.Number) return (decimal)c.GetDouble();
        var s = c.GetFormattedString().Trim().Replace("€", "").Trim();
        return decimal.TryParse(s, NumberStyles.Number, It, out var d) ? d
            : decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d) ? d : null;
    }
}
