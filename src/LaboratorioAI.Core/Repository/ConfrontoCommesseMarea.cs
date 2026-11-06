using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LaboratorioAI.Core.Repository;

/// <summary>Commessa di laboratorio attiva in ArcLab (con o senza prezzo).</summary>
public record CommessaArcLab(int IdCommessa, int IdCliente, string? RagioneSociale, int? IdInsediamento, string? Insediamento,
    int IdControllo, string? Controllo, int? NContratto = null, string? DescrIncarico = null,
    DateTime? DataCommessa = null, DateTime? DataUltimoRinnovo = null, DateTime? UltimoCampione = null);

/// <summary>Parametro di un controllo ArcLab (nome del parametro e denominazione della riga).</summary>
public record ParametroControlloArcLab(int IdControllo, string? NomeParametro, string? DenominazioneRiga);

/// <summary>Nome di un controllo ArcLab.</summary>
public record NomeControlloArcLab(int IdControllo, string? Controllo);

/// <summary>Commessa del database Marea (non obsoleta).</summary>
public record CommessaMarea(int IdCommessa, string? DescCommessa, int? IdClienteArcLab, int? IdInsediamentoArcLab, string? Parametri);

/// <summary>
/// 28/10/2026: cliente ancora attivo nel portale (Marea.Clienti non obsoleto) ma non attivo in ArcLab, senza codice
/// contabilità, inesistente in ArcLab o non collegato (IDArcLab vuoto).
/// </summary>
public record ClienteMareaNonAttivo(int IdMarea, string? Nome, string? CodContabilitaMarea, int? IdArcLab, string? RagioneSocialeArcLab,
    string Motivo, int Listini, int Commesse, DateTime? UltimoAccessoPortale);

/// <summary>
/// 28/10/2026: commessa di laboratorio attiva senza numero di contratto (commessa.NContratto vuoto): non va nel portale
/// ed è un errore da sistemare in ArcLab.
/// </summary>
public record CommessaSenzaContratto(int IdCommessa, string? CodCommessa, int IdCliente, string? RagioneSociale, string? CodContabilita,
    string? Insediamento, int? IdControllo, string? Controllo, string? DescrIncarico, decimal? Prezzo, DateTime? DataCommessa,
    DateTime? UltimoCampione, int Lotti);

/// <summary>Dati letti da ArcLab e da Marea per il confronto delle commesse.</summary>
public record DatiCommesseMarea(IReadOnlyList<CommessaArcLab> Commesse, IReadOnlyList<ParametroControlloArcLab> Parametri,
    IReadOnlyList<NomeControlloArcLab> Controlli, IReadOnlyList<CommessaMarea> CommesseMarea, IReadOnlyList<ListinoMarea> Listini,
    IReadOnlyList<ClienteMareaNonAttivo>? ClientiNonAttivi = null);

/// <summary>Una segnalazione del confronto commesse ArcLab ↔ Marea.</summary>
public record RigaCommessaMarea(string Caso, int? IdCliente, string? Cliente, string? Insediamento, int? IdControllo, string? Controllo,
    int? IdCommessaArcLab, int? IdCommessaMarea, string Dettaglio)
{
    /// <summary>28/10/2026: date della commessa ArcLab (creazione e ultimo rinnovo) e ultima modifica del listino nel portale.</summary>
    public DateTime? DataCommessa { get; init; }
    public DateTime? DataUltimoRinnovo { get; init; }
    public DateTime? UltimaModificaMarea { get; init; }
    /// <summary>Data di ricevimento dell'ultimo lotto della commessa ArcLab.</summary>
    public DateTime? UltimoCampione { get; init; }
}

/// <summary>
/// 28/10/2026 (Mirko Maffei): Ricerca errori → differenze tra le commesse di laboratorio attive di ArcLab e il database
/// Marea, oltre ai prezzi. Fa fede ArcLab. Casi: descrizione del listino diversa da quella della commessa (DescrIncarico,
/// abbinamento come per i prezzi: contratto + controllo); commessa mancante in Marea;
/// commessa di Marea non attiva in ArcLab; insediamento diverso; controllo non riconoscibile nella descrizione;
/// parametri diversi. In Marea.Commesse il controllo si ricava dal numero all'inizio di DescCommessa («3394 - …»).
/// </summary>
public static class ConfrontoCommesseMarea
{
    public const string NomeDiverso = "Descrizione diversa";
    public const string MancaInMarea = "Commessa mancante in Marea";
    public const string NonAttivaInArcLab = "Non attiva in ArcLab";
    public const string InsediamentoDiverso = "Insediamento diverso";
    public const string ControlloNonRiconoscibile = "Controllo non riconoscibile";
    public const string ParametriDiversi = "Parametri diversi";
    public const string ClienteNonAttivo = "Cliente attivo nel portale ma non in ArcLab";

    private static readonly Regex PrefissoId = new(@"^\s*(\d+)\s*(?:-\s*)?", RegexOptions.Compiled);
    private static readonly Regex UnitaCampionaria = new(@"unita\s*campionaria\s*\d+", RegexOptions.Compiled);
    private static readonly Regex AcapoHtml = new(@"<\s*br\s*/?\s*>|\r?\n", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Forma confrontabile: minuscole, senza accenti, spazi e punteggiatura («°C» = «c»).</summary>
    public static string Normalizza(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder();
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = char.ToLowerInvariant(ch);
            sb.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }
        var t = Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        t = UnitaCampionaria.Replace(t, "");
        return t.Replace(" ", "");
    }

    /// <summary>Id del controllo all'inizio della descrizione («3394 - Acque…» → 3394), oppure null.</summary>
    public static int? IdControlloDa(string? descrizione)
    {
        var m = PrefissoId.Match(descrizione ?? "");
        return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    /// <summary>Descrizione senza il numero iniziale («2442 Alimenti…» → «Alimenti…»).</summary>
    public static string SenzaId(string? descrizione) => PrefissoId.Replace(descrizione ?? "", "").Trim();

    public static List<RigaCommessaMarea> Confronta(
        IReadOnlyList<CommessaArcLab> commesse, IReadOnlyList<ParametroControlloArcLab> parametri,
        IReadOnlyList<NomeControlloArcLab> controlli, IReadOnlyList<CommessaMarea> commesseMarea, IReadOnlyList<ListinoMarea> listini,
        IReadOnlyList<ClienteMareaNonAttivo>? clientiNonAttivi = null)
    {
        var righe = new List<RigaCommessaMarea>();

        // 0. Clienti ancora attivi nel portale ma non in ArcLab (da far disattivare nel portale)
        foreach (var c in clientiNonAttivi ?? [])
            righe.Add(new(ClienteNonAttivo, c.IdArcLab, c.Nome?.Trim(), null, null, null, null, null,
                $"{c.Motivo}" + (string.IsNullOrWhiteSpace(c.RagioneSocialeArcLab) ? "" : $" (in ArcLab: {c.RagioneSocialeArcLab.Trim()})")
                + $" · nel portale: cod. contabilità {(string.IsNullOrWhiteSpace(c.CodContabilitaMarea) ? "—" : c.CodContabilitaMarea.Trim())}, "
                + $"{c.Listini} righe di listino, {c.Commesse} commesse"
                + (c.UltimoAccessoPortale is DateTime u ? $", ultimo accesso al portale {u:dd/MM/yyyy}" : ", mai entrato nel portale")));
        var nomi = controlli.GroupBy(c => c.IdControllo).ToDictionary(g => g.Key, g => g.First().Controllo);
        var clienti = commesse.GroupBy(c => c.IdCliente).ToDictionary(g => g.Key, g => g.First().RagioneSociale);

        // 1. Descrizione del listino contro la descrizione della commessa (28/10/2026: Marea copia commessa.DescrIncarico)
        var (perCommessa, nonAbbinati) = ConfrontoPrezziMarea.Abbina(
            commesse.Select(c => (c.IdCommessa, c.NContratto, c.IdCliente, c.IdControllo)), listini);
        foreach (var c in commesse)
        {
            var descr = SenzaId(c.DescrIncarico);
            foreach (var l in (perCommessa.GetValueOrDefault(c.IdCommessa) ?? []).GroupBy(l => Normalizza(SenzaId(l.DescrizioneIncarico))).Select(g => g.First()))
                if (Normalizza(SenzaId(l.DescrizioneIncarico)) != Normalizza(descr))
                    righe.Add(new(NomeDiverso, c.IdCliente, c.RagioneSociale, c.Insediamento, c.IdControllo, c.Controllo, c.IdCommessa, null,
                        $"listino Marea: «{SenzaId(l.DescrizioneIncarico)}» · commessa ArcLab: «{descr}»")
                    { DataCommessa = c.DataCommessa, DataUltimoRinnovo = c.DataUltimoRinnovo, UltimoCampione = c.UltimoCampione, UltimaModificaMarea = l.UltimaModifica });
        }
        foreach (var l in nonAbbinati.Where(l => !nomi.ContainsKey(l.IdControllo)).GroupBy(l => (l.IdClienteArcLab, l.IdControllo)).Select(g => g.First()))
            righe.Add(new(NomeDiverso, l.IdClienteArcLab, l.Cliente, null, l.IdControllo, SenzaId(l.DescrizioneIncarico), null, null,
                "listino Marea su un controllo che non esiste in ArcLab") { UltimaModificaMarea = l.UltimaModifica });

        // 2. Commesse presenti / mancanti (chiave insediamento + controllo)
        var marea = new List<(CommessaMarea M, int Ctrl)>();
        foreach (var m in commesseMarea)
        {
            if (IdControlloDa(m.DescCommessa) is int ic) marea.Add((m, ic));
            else righe.Add(new(ControlloNonRiconoscibile, m.IdClienteArcLab, ClienteDi(m.IdClienteArcLab), null, null, m.DescCommessa?.Trim(),
                null, m.IdCommessa, "la descrizione della commessa in Marea non inizia con l'ID del controllo"));
        }
        string? ClienteDi(int? id) => id is int i && clienti.TryGetValue(i, out var n) ? n : null;

        var arcLabLibere = commesse.ToList();
        var abbinate = new List<(CommessaArcLab A, CommessaMarea M)>();
        var mareaLibere = new List<(CommessaMarea M, int Ctrl)>();
        foreach (var x in marea)
        {
            var a = arcLabLibere.FirstOrDefault(c => c.IdInsediamento == x.M.IdInsediamentoArcLab && c.IdControllo == x.Ctrl)
                    ?? commesse.FirstOrDefault(c => c.IdInsediamento == x.M.IdInsediamentoArcLab && c.IdControllo == x.Ctrl);
            if (a is not null) { abbinate.Add((a, x.M)); arcLabLibere.Remove(a); }
            else mareaLibere.Add(x);
        }
        foreach (var x in mareaLibere)
        {
            var altro = arcLabLibere.FirstOrDefault(c => c.IdCliente == x.M.IdClienteArcLab && c.IdControllo == x.Ctrl);
            if (altro is not null)
            {
                arcLabLibere.Remove(altro);
                righe.Add(new(InsediamentoDiverso, altro.IdCliente, altro.RagioneSociale, altro.Insediamento, x.Ctrl, altro.Controllo,
                    altro.IdCommessa, x.M.IdCommessa, $"ArcLab: insediamento {altro.IdInsediamento} · Marea: insediamento {x.M.IdInsediamentoArcLab}")
                    { DataCommessa = altro.DataCommessa, DataUltimoRinnovo = altro.DataUltimoRinnovo, UltimoCampione = altro.UltimoCampione });
                abbinate.Add((altro, x.M));
            }
            else
                righe.Add(new(NonAttivaInArcLab, x.M.IdClienteArcLab, ClienteDi(x.M.IdClienteArcLab), null, x.Ctrl,
                    nomi.GetValueOrDefault(x.Ctrl) ?? SenzaId(x.M.DescCommessa), null, x.M.IdCommessa,
                    $"in Marea c'è la commessa per l'insediamento {x.M.IdInsediamentoArcLab}, in ArcLab non c'è una commessa attiva"));
        }
        // 28/10/2026: senza numero di contratto la commessa non va nel portale (errore a parte, sezione 12)
        foreach (var a in arcLabLibere.Where(a => a.NContratto is not null))
            righe.Add(new(MancaInMarea, a.IdCliente, a.RagioneSociale, a.Insediamento, a.IdControllo, a.Controllo, a.IdCommessa, null,
                "commessa attiva in ArcLab senza la commessa corrispondente in Marea") { DataCommessa = a.DataCommessa, DataUltimoRinnovo = a.DataUltimoRinnovo, UltimoCampione = a.UltimoCampione });

        // 3. Parametri delle commesse abbinate
        var perControllo = parametri.GroupBy(p => p.IdControllo).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var (a, m) in abbinate)
        {
            if (!perControllo.TryGetValue(a.IdControllo, out var pa)) continue;
            var nomiMarea = AcapoHtml.Split(m.Parametri ?? "").Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
            if (nomiMarea.Count == 0) continue;
            var chiaviMarea = nomiMarea.Select(Normalizza).ToHashSet();
            // un parametro ArcLab è presente se Marea ne riporta il nome o la denominazione di una sua riga
            var gruppiArcLab = pa.GroupBy(p => Normalizza(p.NomeParametro))
                .Select(g => (Nome: g.First().NomeParametro?.Trim() ?? g.First().DenominazioneRiga?.Trim() ?? "",
                              Chiavi: g.Select(p => Normalizza(p.DenominazioneRiga)).Append(g.Key).Where(k => k.Length > 0).ToHashSet()))
                .ToList();
            var tutteArcLab = gruppiArcLab.SelectMany(g => g.Chiavi).ToHashSet();
            var mancanti = gruppiArcLab.Where(g => !g.Chiavi.Overlaps(chiaviMarea)).Select(g => g.Nome).Distinct().ToList();
            var inPiu = nomiMarea.Where(n => !tutteArcLab.Contains(Normalizza(n))).Select(n => UnitaCampionariaTesto(n)).Distinct().ToList();
            if (mancanti.Count == 0 && inPiu.Count == 0) continue;
            var dettaglio = new List<string>();
            if (mancanti.Count > 0) dettaglio.Add("in ArcLab ma non in Marea: " + string.Join(", ", mancanti));
            if (inPiu.Count > 0) dettaglio.Add("in Marea ma non in ArcLab: " + string.Join(", ", inPiu));
            righe.Add(new(ParametriDiversi, a.IdCliente, a.RagioneSociale, a.Insediamento, a.IdControllo, a.Controllo, a.IdCommessa,
                m.IdCommessa, string.Join(" · ", dettaglio)) { DataCommessa = a.DataCommessa, DataUltimoRinnovo = a.DataUltimoRinnovo, UltimoCampione = a.UltimoCampione });
        }

        return righe.OrderBy(r => r.Cliente ?? "", StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.IdControllo).ThenBy(r => r.Caso).ToList();
    }

    private static string UnitaCampionariaTesto(string s) =>
        Regex.Replace(s, @"\s*Unit[àa]\s*campionaria\s*\d+", "", RegexOptions.IgnoreCase).Trim();
}
