using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Core.Provvigioni;

/// <summary>Riga di fattura AZW per il calcolo delle spettanze (cliente con la ZONA attuale, voce, commessa).</summary>
public record RigaRicavoConsulente(string Chiave, string Cliente, string? Zona, DateTime DataDocumento, string Voce, string? Commessa, decimal Netto);

/// <summary>Riga di nota di credito (Netto positivo = importo della nota).</summary>
public record RigaNotaConsulente(string Riferimento, string Cliente, string? Zona, DateTime Data, string Voce, string? Commessa, decimal Netto);

public record IncassoConsulente(string Chiave, DateTime Data, decimal ImportoLordo);

/// <summary>Movimento di spettanza: <paramref name="Voce"/> è la lettera dell'art. 4 (a … g).</summary>
public record MovimentoSpettanza(DateTime Data, string Voce, string Cliente, string Documento, string VoceAzw, string? Commessa,
    string Origine, decimal Base, decimal Percentuale, decimal Importo)
{
    /// <summary>22/10/2026: data della fattura incassata (per la nota di credito: la sua data). Null nei movimenti archiviati.</summary>
    public DateTime? DataFattura { get; init; }

    /// <summary>Anno della fattura: dalla data, o dal documento «Fatt. n/anno» (movimenti archiviati); null se non si sa.</summary>
    public int? AnnoFattura => DataFattura?.Year
        ?? (Documento.LastIndexOf('/') is var i and > 0 && int.TryParse(Documento[(i + 1)..].Trim(), out var a) && a is > 2000 and < 2100 ? a : null);
}

/// <summary>Gruppi di voci AZW (VOCICOST).</summary>
public sealed class GruppiVociConsulenti
{
    public IReadOnlySet<string> Analisi { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> AnalisiEsterne { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> Formazione { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> Consulenza { get; init; } = new HashSet<string>();
    public IReadOnlySet<string> Tarature { get; init; } = new HashSet<string>();

    public static IReadOnlySet<string> Insieme(string? voci) =>
        (voci ?? "").Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Spettanze dei consulenti a percentuale (19/10/2026; claude/analisi-spettanze-consulenti.md). Funzioni pure.
/// Base: incassi dell'intervallo (quota netta = incasso × netto della riga ÷ lordo della fattura) e note di credito
/// alla loro data. Ogni riga va a UNA sola voce, nell'ordine: a) consulenza sua 50% · b) analisi portate da lui 10%
/// (le analisi esterne con la loro percentuale, se indicata nel contratto) ·
/// c) analisi interne dei suoi clienti 10% · f) docenze sue 50% · g) tarature sue · d) consulenze di altri ai suoi
/// clienti nel primo anno 10% · e) formazione di altri ai suoi clienti 10%. «Suo» = COMMESSA della riga; «suo cliente» =
/// ZONA del cliente. Percentuali dal contratto valido alla data del movimento.
/// </summary>
public static class MotoreSpettanzeConsulenti
{
    public static readonly IReadOnlyDictionary<string, string> NomiVoci = new Dictionary<string, string>
    {
        ["a"] = "a) Consulenze svolte da lui",
        ["b"] = "b) Campionamento (analisi portate da lui)",
        ["c"] = "c) Analisi interne dei suoi clienti",
        ["d"] = "d) Primo anno di servizi di altri ai suoi clienti",
        ["e"] = "e) Formazione di altri ai suoi clienti",
        ["f"] = "f) Docenze sue (fatturato del corso)",
        ["g"] = "g) Tarature",
    };

    public static ContrattoConsulente? ContrattoAlla(IEnumerable<ContrattoConsulente> contratti, DateTime data) =>
        contratti.Where(c => !c.Annullato && c.ValidoDal.Date <= data.Date && (c.ValidoAl is null || c.ValidoAl.Value.Date >= data.Date))
            .OrderByDescending(c => c.ValidoDal).FirstOrDefault();

    /// <summary>Voce e percentuale di una riga, o null se la riga non dà spettanza.</summary>
    public static (string Voce, decimal Perc)? Classifica(string voceAzw, string? commessa, string? zonaCliente, string cliente, DateTime dataDocumento,
        ConsulentePercentuale c, ContrattoConsulente k, GruppiVociConsulenti g, IReadOnlyDictionary<(string Cliente, string Voce), DateTime> primaFattura)
    {
        var v = (voceAzw ?? "").Trim().ToUpperInvariant();
        var suo = !string.IsNullOrWhiteSpace(commessa) && string.Equals(commessa.Trim(), c.CodiceCommessa, StringComparison.OrdinalIgnoreCase);
        var suoCliente = !string.IsNullOrWhiteSpace(zonaCliente) && string.Equals(zonaCliente.Trim(), c.CodiceZona, StringComparison.OrdinalIgnoreCase);
        if (!suo && !suoCliente) return null;
        if (suo && g.Consulenza.Contains(v)) return ("a", k.PercConsulenza);
        // 22/10/2026: le analisi esterne portate da lui possono avere una percentuale diversa (vuota = la stessa).
        if (suo && g.AnalisiEsterne.Contains(v)) return ("b", k.PercCampionamentoEsterne ?? k.PercCampionamento);
        if (suo && g.Analisi.Contains(v)) return ("b", k.PercCampionamento);
        if (suoCliente && g.Analisi.Contains(v) && !g.AnalisiEsterne.Contains(v)) return ("c", k.PercAnalisiClienti);
        if (suo && g.Formazione.Contains(v)) return ("f", k.PercDocenza);
        if (suo && g.Tarature.Contains(v) && k.PercTarature is decimal pt) return ("g", pt);
        if (suoCliente && g.Consulenza.Contains(v))
        {
            var prima = primaFattura.TryGetValue((cliente, v), out var p) ? p : dataDocumento;
            return dataDocumento.Date < prima.Date.AddMonths(k.MesiPrimoAnno) ? ("d", k.PercPrimoAnnoServizi) : null;
        }
        if (suoCliente && g.Formazione.Contains(v)) return ("e", k.PercFormazioneAltri);
        return null;
    }

    /// <summary>Prima fattura (data) per cliente e voce, dalle righe positive.</summary>
    public static Dictionary<(string Cliente, string Voce), DateTime> PrimeFatture(IEnumerable<RigaRicavoConsulente> righe)
    {
        var d = new Dictionary<(string, string), DateTime>();
        foreach (var r in righe.Where(r => r.Netto > 0m))
        {
            var k = (r.Cliente, r.Voce.Trim().ToUpperInvariant());
            if (!d.TryGetValue(k, out var x) || r.DataDocumento < x) d[k] = r.DataDocumento;
        }
        return d;
    }

    public static List<MovimentoSpettanza> Calcola(ConsulentePercentuale consulente, IReadOnlyList<ContrattoConsulente> contratti, GruppiVociConsulenti gruppi,
        IReadOnlyList<RigaRicavoConsulente> righe, IReadOnlyDictionary<string, decimal> lordiFatture, IReadOnlyList<IncassoConsulente> incassi,
        IReadOnlyList<RigaNotaConsulente> note, IReadOnlyDictionary<(string Cliente, string Voce), DateTime> primaFattura, DateTime dal, DateTime al,
        DateTime? fatturaDal = null, DateTime? fatturaAl = null)
    {
        // 22/10/2026: [dal, al] = date degli incassi (e delle note); [fatturaDal, fatturaAl] = date delle fatture (facoltativo).
        bool FatturaNelPeriodo(DateTime d) => (fatturaDal is null || d.Date >= fatturaDal.Value.Date) && (fatturaAl is null || d.Date <= fatturaAl.Value.Date);
        var mov = new List<MovimentoSpettanza>();
        var perFattura = righe.GroupBy(r => r.Chiave).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var inc in incassi.Where(i => i.Data.Date >= dal.Date && i.Data.Date <= al.Date))
        {
            if (!perFattura.TryGetValue(inc.Chiave, out var rr)) continue;
            var k = ContrattoAlla(contratti, inc.Data);
            if (k is null) continue;
            var lordo = lordiFatture.TryGetValue(inc.Chiave, out var l) && l != 0m ? l : rr.Sum(r => r.Netto) * 1.22m;
            if (lordo == 0m) continue;
            var quota = inc.ImportoLordo / lordo;
            foreach (var r in rr)
            {
                if (!FatturaNelPeriodo(r.DataDocumento)) continue;
                var cl = Classifica(r.Voce, r.Commessa, r.Zona, r.Cliente, r.DataDocumento, consulente, k, gruppi, primaFattura);
                if (cl is null) continue;
                var baseNetta = Math.Round(r.Netto * quota, 2, MidpointRounding.ToEven);
                if (baseNetta == 0m) continue;
                mov.Add(new MovimentoSpettanza(inc.Data.Date, cl.Value.Voce, r.Cliente, Documento(r.Chiave), r.Voce, r.Commessa, "incasso",
                    baseNetta, cl.Value.Perc, Math.Round(baseNetta * cl.Value.Perc / 100m, 2, MidpointRounding.ToEven)) { DataFattura = r.DataDocumento.Date });
            }
        }
        foreach (var n in note.Where(n => n.Data.Date >= dal.Date && n.Data.Date <= al.Date && FatturaNelPeriodo(n.Data)))
        {
            var k = ContrattoAlla(contratti, n.Data);
            if (k is null) continue;
            var cl = Classifica(n.Voce, n.Commessa, n.Zona, n.Cliente, n.Data, consulente, k, gruppi, primaFattura);
            if (cl is null) continue;
            mov.Add(new MovimentoSpettanza(n.Data.Date, cl.Value.Voce, n.Cliente, n.Riferimento, n.Voce, n.Commessa, "nota di credito",
                -n.Netto, cl.Value.Perc, Math.Round(-n.Netto * cl.Value.Perc / 100m, 2, MidpointRounding.ToEven)) { DataFattura = n.Data.Date });
        }
        return mov.OrderBy(m => m.Data).ThenBy(m => m.Cliente).ToList();
    }

    /// <summary>«CODCF|anno|numero» → «numero/anno».</summary>
    public static string Documento(string chiave)
    {
        var p = chiave.Split('|');
        return p.Length == 3 ? $"Fatt. {p[2]}/{p[1]}" : chiave;
    }
}
