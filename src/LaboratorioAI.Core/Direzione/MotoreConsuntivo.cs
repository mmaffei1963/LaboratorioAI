namespace LaboratorioAI.Core.Direzione;

// Consuntivo per area (20/10/2026), come il report Access «Conteggio dei costi e dei ricavi per area».
// Definizione: claude/definizione-consuntivi.md
//  - Fonte: contabilità analitica di AZW (dati_importati_preconsuntivo = TESTANA + DETTANA), periodo su DATREG.
//  - Colonne = centri di costo; FON (formazione accreditata) è sommato a FOR (formazione).
//  - Righe = voci (VOCICOST), costi (TIPO C) e ricavi (TIPO R), importo netto DARE + AVERE.
//  - Contributo AMM: il risultato dell'amministrazione (03) è diviso in parti uguali sulle altre aree.

/// <summary>Una riga della contabilità analitica, già con segno (ricavi positivi, costi negativi).</summary>
public sealed record RigaConsuntivo(DateTime Data, string Centro, string Voce, decimal Importo);

/// <summary>Un'area (colonna) del consuntivo.</summary>
public sealed record AreaConsuntivo(string Codice, string Etichetta, bool DaRipartire);

/// <summary>Una voce del consuntivo: valori per area nel periodo e totale dello stesso periodo dell'anno prima.</summary>
public sealed class VoceConsuntivo
{
    public string Voce { get; init; } = "";
    public string Descrizione { get; init; } = "";
    public bool Ricavo { get; init; }
    public Dictionary<string, decimal> Valori { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, decimal> ValoriPrecedenti { get; } = new(StringComparer.Ordinal);
    public decimal Totale => Valori.Values.Sum();
    public decimal TotalePrecedente => ValoriPrecedenti.Values.Sum();
}

/// <summary>Totali di un periodo per area: costi, ricavi, utile, contributo AMM e utile con contributo.</summary>
public sealed class TotaliConsuntivo
{
    public Dictionary<string, decimal> Costi { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, decimal> Ricavi { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, decimal> Utile { get; } = new(StringComparer.Ordinal);
    /// <summary>Quota del risultato dell'amministrazione: all'area 03 il suo risultato cambiato di segno, alle altre la quota.</summary>
    public Dictionary<string, decimal> Contributo { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, decimal> UtileConContributo { get; } = new(StringComparer.Ordinal);
    public decimal TotaleCosti => Costi.Values.Sum();
    public decimal TotaleRicavi => Ricavi.Values.Sum();
    public decimal UtileMarea => Utile.Values.Sum();
}

/// <summary>Un mese dell'andamento: ricavi e utile (senza contributo AMM) per area.</summary>
public sealed record MeseConsuntivo(int Anno, int Mese, IReadOnlyDictionary<string, decimal> Ricavi, IReadOnlyDictionary<string, decimal> Utile);

public sealed class RisultatoConsuntivo
{
    public DateTime Dal { get; init; }
    public DateTime Al { get; init; }
    public DateTime DalPrecedente { get; init; }
    public DateTime AlPrecedente { get; init; }
    public List<AreaConsuntivo> Aree { get; init; } = [];
    public List<VoceConsuntivo> Costi { get; init; } = [];
    public List<VoceConsuntivo> Ricavi { get; init; } = [];
    public TotaliConsuntivo Totali { get; init; } = new();
    public TotaliConsuntivo TotaliPrecedenti { get; init; } = new();
    public List<MeseConsuntivo> Mesi { get; init; } = [];
    /// <summary>Stessi mesi dell'anno prima (per i grafici di confronto).</summary>
    public List<MeseConsuntivo> MesiPrecedenti { get; init; } = [];
}

public static class MotoreConsuntivo
{
    /// <summary>Centro dell'amministrazione, il cui risultato si ripartisce sulle altre aree (CENTCOST.DARIPAR = S).</summary>
    public const string CentroDaRipartire = "03";

    /// <summary>Aree standard nell'ordine del report Access.</summary>
    public static readonly IReadOnlyList<AreaConsuntivo> AreeStandard =
    [
        new("03", "Amministrazione", true),
        new("06", "Igiene", false),
        new("FOR", "Formazione", false),
        new("04", "Laboratorio", false),
        new("05", "Sicurezza", false),
        new("PRY", "Consulenze", false),
    ];

    /// <summary>Centri accorpati a un'altra area (FON formazione accreditata → FOR formazione).</summary>
    public static readonly IReadOnlyDictionary<string, string> Accorpamenti =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["FON"] = "FOR" };

    /// <summary>Area a cui va un centro di costo AZW.</summary>
    public static string AreaDi(string centro)
    {
        var c = (centro ?? "").Trim().ToUpperInvariant();
        return Accorpamenti.TryGetValue(c, out var a) ? a : c;
    }

    /// <summary>Stesso periodo dell'anno prima (il 29 febbraio diventa 28).</summary>
    public static (DateTime Dal, DateTime Al) PeriodoPrecedente(DateTime dal, DateTime al) => (dal.AddYears(-1), al.AddYears(-1));

    /// <param name="righe">Righe di entrambi i periodi (corrente e anno prima); le altre sono ignorate.</param>
    /// <param name="voci">Descrizione e tipo (ricavo) delle voci AZW.</param>
    /// <param name="centri">Descrizione dei centri AZW, per le aree non standard.</param>
    public static RisultatoConsuntivo Calcola(IEnumerable<RigaConsuntivo> righe, DateTime dal, DateTime al,
        IReadOnlyDictionary<string, (string Descrizione, bool Ricavo)> voci, IReadOnlyDictionary<string, string?> centri)
    {
        dal = dal.Date; al = al.Date;
        var (dalP, alP) = PeriodoPrecedente(dal, al);
        bool InCorrente(DateTime d) => d.Date >= dal && d.Date <= al;
        bool InPrecedente(DateTime d) => d.Date >= dalP && d.Date <= alP;

        var elenco = righe.Where(r => InCorrente(r.Data) || InPrecedente(r.Data)).ToList();

        // Aree: le standard sempre; le altre solo se hanno movimenti nei due periodi.
        var aree = AreeStandard.ToList();
        foreach (var codice in elenco.Select(r => AreaDi(r.Centro)).Distinct().OrderBy(c => c, StringComparer.Ordinal))
        {
            if (aree.Any(a => a.Codice == codice)) continue;
            var etichetta = codice.Length == 0 ? "Senza centro"
                : centri.TryGetValue(codice, out var d) && !string.IsNullOrWhiteSpace(d) ? d!.Trim() : codice;
            aree.Add(new AreaConsuntivo(codice, etichetta, false));
        }

        // Voci.
        var perVoce = new Dictionary<string, VoceConsuntivo>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in elenco)
        {
            var codice = (r.Voce ?? "").Trim();
            if (!perVoce.TryGetValue(codice, out var v))
            {
                var nota = voci.TryGetValue(codice, out var x);
                v = new VoceConsuntivo
                {
                    Voce = codice,
                    Descrizione = nota && !string.IsNullOrWhiteSpace(x.Descrizione) ? x.Descrizione.Trim()
                        : codice.Length == 0 ? "Senza voce" : $"Voce {codice} (non in VOCICOST)",
                    // Voce sconosciuta: ricavo se l'importo è in avere.
                    Ricavo = nota ? x.Ricavo : r.Importo > 0m
                };
                perVoce[codice] = v;
            }
            var area = AreaDi(r.Centro);
            var dest = InCorrente(r.Data) ? v.Valori : v.ValoriPrecedenti;
            dest[area] = dest.GetValueOrDefault(area) + r.Importo;
        }
        var tutte = perVoce.Values.Where(v => v.Valori.Values.Any(x => x != 0m) || v.ValoriPrecedenti.Values.Any(x => x != 0m)).ToList();
        var costi = tutte.Where(v => !v.Ricavo).OrderBy(v => v.Descrizione, StringComparer.CurrentCultureIgnoreCase).ToList();
        var ricavi = tutte.Where(v => v.Ricavo).OrderBy(v => v.Descrizione, StringComparer.CurrentCultureIgnoreCase).ToList();

        var totali = Totali(aree, costi, ricavi, v => v.Valori);
        var totaliP = Totali(aree, costi, ricavi, v => v.ValoriPrecedenti);

        // Andamento mese per mese (periodo corrente e anno prima).
        var tipoVoce = perVoce.ToDictionary(k => k.Key, k => k.Value.Ricavo, StringComparer.OrdinalIgnoreCase);
        List<MeseConsuntivo> Mesi(DateTime da, DateTime a, Func<DateTime, bool> nelPeriodo)
        {
            var lista = new List<MeseConsuntivo>();
            for (var m = new DateTime(da.Year, da.Month, 1); m <= a; m = m.AddMonths(1))
            {
                var ric = aree.ToDictionary(x => x.Codice, _ => 0m, StringComparer.Ordinal);
                var utl = aree.ToDictionary(x => x.Codice, _ => 0m, StringComparer.Ordinal);
                foreach (var r in elenco.Where(r => nelPeriodo(r.Data) && r.Data.Year == m.Year && r.Data.Month == m.Month))
                {
                    var area = AreaDi(r.Centro);
                    utl[area] += r.Importo;
                    if (tipoVoce.GetValueOrDefault((r.Voce ?? "").Trim())) ric[area] += r.Importo;
                }
                lista.Add(new MeseConsuntivo(m.Year, m.Month, ric, utl));
            }
            return lista;
        }
        var mesi = Mesi(dal, al, InCorrente);
        var mesiP = Mesi(dalP, alP, InPrecedente);

        return new RisultatoConsuntivo
        {
            Dal = dal, Al = al, DalPrecedente = dalP, AlPrecedente = alP,
            Aree = aree, Costi = costi, Ricavi = ricavi, Totali = totali, TotaliPrecedenti = totaliP, Mesi = mesi, MesiPrecedenti = mesiP
        };
    }

    private static TotaliConsuntivo Totali(List<AreaConsuntivo> aree, List<VoceConsuntivo> costi, List<VoceConsuntivo> ricavi,
        Func<VoceConsuntivo, Dictionary<string, decimal>> valori)
    {
        var t = new TotaliConsuntivo();
        foreach (var a in aree)
        {
            var c = costi.Sum(v => valori(v).GetValueOrDefault(a.Codice));
            var r = ricavi.Sum(v => valori(v).GetValueOrDefault(a.Codice));
            t.Costi[a.Codice] = c;
            t.Ricavi[a.Codice] = r;
            t.Utile[a.Codice] = c + r;
        }
        // Contributo AMM: il risultato dell'amministrazione in parti uguali sulle altre aree.
        var amm = t.Utile.GetValueOrDefault(CentroDaRipartire);
        var riceventi = aree.Where(a => !a.DaRipartire).Select(a => a.Codice).ToList();
        var quota = riceventi.Count == 0 ? 0m : amm / riceventi.Count;
        foreach (var a in aree)
        {
            var contributo = a.DaRipartire ? -t.Utile[a.Codice] : quota;
            t.Contributo[a.Codice] = contributo;
            t.UtileConContributo[a.Codice] = t.Utile[a.Codice] + contributo;
        }
        return t;
    }
}
