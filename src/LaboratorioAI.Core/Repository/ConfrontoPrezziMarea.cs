namespace LaboratorioAI.Core.Repository;

/// <summary>Commessa di laboratorio attiva in ArcLab (prezzo per insediamento e controllo).</summary>
/// <param name="NContratto">commessa.NContratto = Marea.Listini.IDContratto (28/10/2026).</param>
/// <param name="DescrIncarico">commessa.DescrIncarico = Marea.Listini.DescrizioneIncarico.</param>
public record CommessaPrezzoArcLab(int IdCommessa, int IdCliente, string? RagioneSociale, int? IdInsediamento, string? Insediamento,
    int IdControllo, string? Controllo, decimal Prezzo, int? NContratto = null, string? DescrIncarico = null,
    DateTime? DataCommessa = null, DateTime? DataUltimoRinnovo = null, DateTime? UltimoCampione = null);

/// <summary>Riga di listino del database Marea (portale), già collegata al cliente ArcLab (Clienti.IDArcLab).</summary>
public record ListinoMarea(int IdClienteArcLab, string? Cliente, int IdControllo, string? DescrizioneIncarico, decimal Prezzo, DateTime? UltimaModifica,
    int? IdContratto = null);

/// <summary>Una segnalazione del confronto prezzi ArcLab ↔ Marea.</summary>
public record RigaPrezzoMarea(string Caso, int IdCliente, string? Cliente, string? Insediamento, int IdControllo, string? Controllo,
    int? IdCommessa, decimal? PrezzoArcLab, string? PrezzoMarea, decimal? Differenza, DateTime? UltimaModificaMarea)
{
    /// <summary>28/10/2026: date della commessa ArcLab (creazione e ultimo rinnovo).</summary>
    public DateTime? DataCommessa { get; init; }
    public DateTime? DataUltimoRinnovo { get; init; }
    /// <summary>Data di ricevimento dell'ultimo lotto della commessa.</summary>
    public DateTime? UltimoCampione { get; init; }
}

/// <summary>
/// 28/10/2026 (Mirko Maffei): Ricerca errori → prezzi delle commesse di laboratorio attive in ArcLab (LabAnalisiDati)
/// confrontati con i listini del database Marea. Fa fede ArcLab. Ogni riga di listino si abbina alla sua commessa con
/// Listini.IDContratto = commessa.NContratto e lo stesso controllo; le righe senza contratto per cliente e controllo.
/// Casi: prezzo diverso, manca in Marea, solo in Marea (nessuna commessa attiva in ArcLab), prezzi diversi tra insediamenti.
/// </summary>
public static class ConfrontoPrezziMarea
{
    public const string PrezzoDiverso = "Prezzo diverso";
    public const string MancaInMarea = "Manca in Marea";
    public const string SoloInMarea = "Solo in Marea";
    public const string DiversiTraInsediamenti = "Prezzi diversi tra insediamenti";

    private static bool Uguali(decimal a, decimal b) => Math.Abs(a - b) < 0.005m;

    /// <summary>
    /// Abbina le righe di listino alle commesse: prima per contratto + controllo, altrimenti (listino senza contratto) per
    /// cliente + controllo. Restituisce le righe per commessa e quelle rimaste senza commessa.
    /// </summary>
    public static (Dictionary<int, List<ListinoMarea>> PerCommessa, List<ListinoMarea> NonAbbinati) Abbina(
        IEnumerable<(int IdCommessa, int? NContratto, int IdCliente, int IdControllo)> commesse, IEnumerable<ListinoMarea> listini)
    {
        var tutti = listini.ToList();
        var perContratto = tutti.Where(l => l.IdContratto is not null).ToLookup(l => (l.IdContratto!.Value, l.IdControllo));
        var senzaContratto = tutti.Where(l => l.IdContratto is null).ToLookup(l => (l.IdClienteArcLab, l.IdControllo));
        var usati = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var perCommessa = new Dictionary<int, List<ListinoMarea>>();
        foreach (var c in commesse)
        {
            var trovati = c.NContratto is int nc && perContratto[(nc, c.IdControllo)].Any()
                ? perContratto[(nc, c.IdControllo)].ToList()
                : senzaContratto[(c.IdCliente, c.IdControllo)].ToList();
            perCommessa[c.IdCommessa] = trovati;
            foreach (var l in trovati) usati.Add(l);
        }
        return (perCommessa, tutti.Where(l => !usati.Contains(l)).ToList());
    }

    private static string Prezzi(IEnumerable<decimal> p) => string.Join(" / ", p.Select(x => Math.Round(x, 2)).Distinct().OrderBy(x => x).Select(x => x.ToString("0.00")));

    public static List<RigaPrezzoMarea> Confronta(IEnumerable<CommessaPrezzoArcLab> commesse, IEnumerable<ListinoMarea> listini)
    {
        var elenco = commesse.ToList();
        var (perCommessa, nonAbbinati) = Abbina(elenco.Select(c => (c.IdCommessa, c.NContratto, c.IdCliente, c.IdControllo)), listini);
        var traInsediamenti = elenco.GroupBy(c => (c.IdCliente, c.IdControllo))
            .Where(g => g.Select(c => Math.Round(c.Prezzo, 2)).Distinct().Count() > 1).Select(g => g.Key).ToHashSet();
        var righe = new List<RigaPrezzoMarea>();

        foreach (var c in elenco)
        {
            var lm = perCommessa.GetValueOrDefault(c.IdCommessa) ?? [];
            var prezziMarea = lm.Select(l => Math.Round(l.Prezzo, 2)).Distinct().ToList();
            var casi = new List<string>();
            decimal? diff = null;
            // 28/10/2026: il portale prende solo le commesse con numero di contratto; quelle senza sono un errore a parte
            // (sezione 12), qui non contano come «manca».
            if (prezziMarea.Count == 0) { if (c.NContratto is not null) casi.Add(MancaInMarea); }
            else if (prezziMarea.Any(p => !Uguali(p, c.Prezzo)))
            {
                casi.Add(PrezzoDiverso);
                diff = c.Prezzo - prezziMarea.OrderBy(p => Math.Abs(p - c.Prezzo)).First();
            }
            if (traInsediamenti.Contains((c.IdCliente, c.IdControllo))) casi.Add(DiversiTraInsediamenti);
            if (casi.Count == 0) continue;
            righe.Add(new RigaPrezzoMarea(string.Join("; ", casi), c.IdCliente, c.RagioneSociale, c.Insediamento, c.IdControllo,
                c.Controllo, c.IdCommessa, c.Prezzo, prezziMarea.Count == 0 ? null : Prezzi(prezziMarea), diff, lm.Max(l => l.UltimaModifica))
            { DataCommessa = c.DataCommessa, DataUltimoRinnovo = c.DataUltimoRinnovo, UltimoCampione = c.UltimoCampione });
        }

        foreach (var g in nonAbbinati.GroupBy(l => (l.IdClienteArcLab, l.IdControllo, l.IdContratto)))
        {
            var l0 = g.First();
            righe.Add(new RigaPrezzoMarea(SoloInMarea, l0.IdClienteArcLab, l0.Cliente, null, l0.IdControllo,
                l0.DescrizioneIncarico?.Trim() + (l0.IdContratto is int ic ? $" (contratto {ic})" : ""), null, null,
                Prezzi(g.Select(l => l.Prezzo)), null, g.Max(l => l.UltimaModifica)));
        }

        return righe.OrderBy(r => r.Cliente, StringComparer.CurrentCultureIgnoreCase).ThenBy(r => r.IdControllo).ThenBy(r => r.Insediamento).ToList();
    }
}
