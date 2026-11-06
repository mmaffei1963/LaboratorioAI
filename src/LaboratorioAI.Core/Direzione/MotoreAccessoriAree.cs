using System.Globalization;

namespace LaboratorioAI.Core.Direzione;

/// <summary>Come si divide sulle aree il compenso di un socio: una quota fissa all'amministrazione e il resto in percentuale.</summary>
public sealed record RipartizioneSocio(decimal QuotaFissaAmministrazione, IReadOnlyDictionary<string, decimal> PercentualiResto)
{
    /// <summary>
    /// 22/10/2026: dove va la differenza ancora da pagare, se diversa dalla ripartizione generale (es. Vaccarini: tutta
    /// all'Igiene). Null = come la ripartizione generale.
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? PercentualiDifferenza { get; init; }

    /// <summary>Differenza da pagare predefinita per voce AZW del socio (22/10/2026: Vaccarini → Igiene).</summary>
    public static IReadOnlyDictionary<string, decimal>? DifferenzaPredefinita(string voceCompensi) =>
        voceCompensi.Trim().ToUpperInvariant() == "DAN" ? new Dictionary<string, decimal> { ["06"] = 100m } : null;

    /// <summary>Testo «04=100» o «05=50;FOR=50» → percentuali per area (codici del consuntivo).</summary>
    public static IReadOnlyDictionary<string, decimal> Leggi(string? testo)
    {
        var d = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var parte in (testo ?? "").Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = parte.Split('=', ':');
            if (kv.Length == 2 && decimal.TryParse(kv[1].Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p > 0)
                d[MotoreConsuntivo.AreaDi(kv[0])] = d.GetValueOrDefault(MotoreConsuntivo.AreaDi(kv[0])) + p;
        }
        return d;
    }

    public static string Scrivi(IReadOnlyDictionary<string, decimal> p) =>
        string.Join(";", p.Select(x => $"{x.Key}={x.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));

    /// <summary>Ripartizioni decise il 22/10/2026 (per voce AZW dei compensi del socio): valori di partenza.</summary>
    public static RipartizioneSocio? Predefinita(string voceCompensi) => voceCompensi.Trim().ToUpperInvariant() switch
    {
        "MIR" => new(16000m, new Dictionary<string, decimal> { ["04"] = 100m }),
        "DAN" => new(6000m, new Dictionary<string, decimal> { ["04"] = 100m }),
        "MAR" => new(0m, new Dictionary<string, decimal> { ["05"] = 50m, ["FOR"] = 50m }),
        "STE" => new(0m, new Dictionary<string, decimal> { ["05"] = 100m }),
        "GIU" => new(0m, new Dictionary<string, decimal> { ["06"] = 100m }),
        _ => null,
    };
}

/// <summary>Accessori, rimborsi e compensi maturati non pagati di un'area, con fatturato e margine dal consuntivo.</summary>
public sealed class AreaAccessori
{
    public string Area { get; init; } = "";
    public string Etichetta { get; init; } = "";
    public bool DaRipartire { get; init; }
    public decimal Fatturato { get; set; }
    /// <summary>Utile dell'area prima del contributo dell'amministrazione (consuntivo).</summary>
    public decimal Margine { get; set; }
    /// <summary>Utile dell'area dopo il contributo dell'amministrazione (consuntivo).</summary>
    public decimal MargineConAmm { get; set; }
    public decimal IndennitaLaboratorio { get; set; }
    public decimal PremiLaboratorio { get; set; }
    public decimal Rspp { get; set; }
    public decimal ResponsabileArea { get; set; }
    public decimal Premio { get; set; }
    public decimal Assicurazione { get; set; }
    public decimal Amministratore { get; set; }
    public decimal Rimborsi { get; set; }
    /// <summary>Compensi ancora da pagare ai soci: la differenza del prospetto (comprende il debito degli anni prima).</summary>
    public decimal MaturatoNonPagato { get; set; }
    /// <summary>Accessori e maturato dell'amministrazione ripartiti in parti uguali (versione «dopo AMM»).</summary>
    public decimal QuotaAccessoriAmm { get; set; }
    public decimal QuotaMaturatoAmm { get; set; }

    public decimal TotaleAccessori => IndennitaLaboratorio + PremiLaboratorio + Rspp + ResponsabileArea + Premio + Assicurazione + Amministratore;
    public decimal? SuFatturato => Fatturato > 0 ? TotaleAccessori / Fatturato : null;
    public decimal? SuMargine => Margine > 0 ? TotaleAccessori / Margine : null;
    public decimal AccessoriConAmm => TotaleAccessori + QuotaAccessoriAmm;
    public decimal? SuMargineConAmm => MargineConAmm > 0 ? AccessoriConAmm / MargineConAmm : null;
    public decimal MargineRettificato => Margine - MaturatoNonPagato;
    public decimal MargineConAmmRettificato => MargineConAmm - MaturatoNonPagato - QuotaMaturatoAmm;
}

/// <summary>Consuntivo per area rettificato con i compensi soci ancora da pagare di un anno intero (22/10/2026).</summary>
public sealed class RettificaCompensiSoci
{
    public int Anno { get; init; }
    /// <summary>False = l'anno non c'è nei compensi soci: la rettifica non si può fare.</summary>
    public bool Presente { get; init; }
    /// <summary>Compensi ancora da pagare per area (importo positivo = costo in più).</summary>
    public Dictionary<string, decimal> DaPagare { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, decimal> UtileRettificato { get; } = new(StringComparer.Ordinal);
    /// <summary>Quota della parte da pagare dell'amministrazione (all'amministrazione il suo importo cambiato di segno).</summary>
    public Dictionary<string, decimal> QuotaAmm { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, decimal> UtileConContributoRettificato { get; } = new(StringComparer.Ordinal);
    public decimal Totale => DaPagare.Values.Sum();
    public decimal UtileMareaRettificato => UtileRettificato.Values.Sum();
}

/// <summary>
/// Compensi soci (22/10/2026): accessori (indennità, premi, RSPP, responsabile d'area, premio, assicurazione, amministratore)
/// e compensi maturati non pagati divisi per area e confrontati con fatturato e margine del consuntivo. Regole:
/// indennità di laboratorio e premi dell'area laboratorio → Laboratorio; quota RSPP → Sicurezza; il resto del socio secondo
/// la sua ripartizione (quota fissa all'amministrazione, il resto in percentuale), applicata in proporzione al suo compenso
/// maturato dell'anno. Rimborsi a parte. Da pagare = differenza del prospetto (anche il debito degli anni prima), divisa
/// con la ripartizione della differenza se impostata (Vaccarini: Igiene). Versione «dopo AMM»: quanto va all'amministrazione si divide in parti uguali sulle altre aree.
/// </summary>
public static class MotoreAccessoriAree
{
    public const string Laboratorio = "04", Sicurezza = "05", Amministrazione = MotoreConsuntivo.CentroDaRipartire;

    /// <summary>Quote per area (somma 1) del compenso generico di un socio.</summary>
    public static Dictionary<string, decimal> Quote(RipartizioneSocio rip, decimal compensoMaturato)
    {
        var q = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var amm = compensoMaturato > 0 ? Math.Min(rip.QuotaFissaAmministrazione / compensoMaturato, 1m) : (rip.PercentualiResto.Count == 0 ? 1m : 0m);
        if (amm > 0) q[Amministrazione] = amm;
        var totPerc = rip.PercentualiResto.Values.Sum();
        if (totPerc <= 0) { q[Amministrazione] = 1m; return q; }
        foreach (var (area, p) in rip.PercentualiResto)
            q[area] = q.GetValueOrDefault(area) + (1m - amm) * p / totPerc;
        return q;
    }

    /// <summary>Quote per area della differenza da pagare: la ripartizione apposita se c'è, altrimenti quella generale.</summary>
    private static Dictionary<string, decimal> QuoteDifferenza(RipartizioneSocio rip, Dictionary<string, decimal> quoteGenerali) =>
        rip.PercentualiDifferenza is { Count: > 0 } pd ? Quote(new RipartizioneSocio(0m, pd), 1m) : quoteGenerali;

    private static RipartizioneSocio RipartizioneDi(IReadOnlyDictionary<int, RipartizioneSocio> ripartizioni, int idSocio) =>
        ripartizioni.GetValueOrDefault(idSocio) ?? new RipartizioneSocio(0m, new Dictionary<string, decimal>());

    /// <summary>
    /// 22/10/2026: compensi ancora da pagare ai soci (differenza del prospetto, tutta) divisi per area, come nel prospetto
    /// «Compensi ancora da pagare ai soci, per area». Serve alla rettifica del consuntivo per area.
    /// </summary>
    public static Dictionary<string, decimal> DaPagarePerArea(RisultatoCompensi r, IReadOnlyDictionary<int, RipartizioneSocio> ripartizioni)
    {
        var d = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in r.Soci)
        {
            var rip = RipartizioneDi(ripartizioni, s.IdSocio);
            var q = Quote(rip, s.Totale - s.Recupero - s.Rimborsi - s.RimborsiTrasferte);
            foreach (var (area, quota) in QuoteDifferenza(rip, q)) d[area] = d.GetValueOrDefault(area) + s.Differenza * quota;
        }
        foreach (var k in d.Keys.ToList()) d[k] = Math.Round(d[k], 2);
        return d;
    }

    /// <summary>
    /// 22/10/2026: rettifica del consuntivo per area con i compensi soci ancora da pagare (solo per un anno intero).
    /// Utile rettificato = utile dell'area − da pagare; con contributo AMM: anche la parte da pagare dell'amministrazione
    /// si divide in parti uguali sulle altre aree, come il contributo. <paramref name="daPagare"/> null = anno non presente.
    /// </summary>
    public static RettificaCompensiSoci Rettifica(int anno, IReadOnlyList<AreaConsuntivo> aree, TotaliConsuntivo t,
        IReadOnlyDictionary<string, decimal>? daPagare)
    {
        var x = new RettificaCompensiSoci { Anno = anno, Presente = daPagare is not null };
        if (daPagare is null) return x;
        // Aree non presenti nel consuntivo (non dovrebbe succedere): vanno all'amministrazione.
        foreach (var (area, v) in daPagare)
        {
            var dest = aree.Any(a => string.Equals(a.Codice, area, StringComparison.OrdinalIgnoreCase))
                ? aree.First(a => string.Equals(a.Codice, area, StringComparison.OrdinalIgnoreCase)).Codice : Amministrazione;
            x.DaPagare[dest] = x.DaPagare.GetValueOrDefault(dest) + v;
        }
        var riceventi = aree.Where(a => !a.DaRipartire).ToList();
        var quotaAmm = riceventi.Count == 0 ? 0m : Math.Round(aree.Where(a => a.DaRipartire).Sum(a => x.DaPagare.GetValueOrDefault(a.Codice)) / riceventi.Count, 2);
        foreach (var a in aree)
        {
            var dp = x.DaPagare.GetValueOrDefault(a.Codice);
            x.DaPagare[a.Codice] = dp;
            x.UtileRettificato[a.Codice] = t.Utile.GetValueOrDefault(a.Codice) - dp;
            x.QuotaAmm[a.Codice] = a.DaRipartire ? -dp : quotaAmm;
            x.UtileConContributoRettificato[a.Codice] = a.DaRipartire ? 0m : t.UtileConContributo.GetValueOrDefault(a.Codice) - dp - quotaAmm;
        }
        return x;
    }

    public static List<AreaAccessori> Calcola(RisultatoCompensi r, IReadOnlyDictionary<int, RipartizioneSocio> ripartizioni,
        IReadOnlyList<AreaConsuntivo> aree, TotaliConsuntivo consuntivo)
    {
        var righe = aree.ToDictionary(a => a.Codice, a => new AreaAccessori
        {
            Area = a.Codice, Etichetta = a.Etichetta, DaRipartire = a.DaRipartire,
            Fatturato = consuntivo.Ricavi.GetValueOrDefault(a.Codice), Margine = consuntivo.Utile.GetValueOrDefault(a.Codice),
            MargineConAmm = consuntivo.UtileConContributo.GetValueOrDefault(a.Codice),
        }, StringComparer.OrdinalIgnoreCase);
        AreaAccessori Riga(string area)
        {
            if (!righe.TryGetValue(area, out var x)) { x = new AreaAccessori { Area = area, Etichetta = area }; righe[area] = x; }
            return x;
        }

        foreach (var s in r.Soci)
        {
            var rip = RipartizioneDi(ripartizioni, s.IdSocio);
            var compenso = s.Totale - s.Recupero - s.Rimborsi - s.RimborsiTrasferte;
            var q = Quote(rip, compenso);
            Riga(Laboratorio).IndennitaLaboratorio += s.IndennitaLaboratorio;
            Riga(Sicurezza).Rspp += s.Rspp;
            foreach (var (area, quota) in q)
            {
                var x = Riga(area);
                x.ResponsabileArea += s.ResponsabileArea * quota;
                x.Premio += s.Premio * quota;
                x.Assicurazione += s.Assicurazione * quota;
                x.Amministratore += s.Amministratore * quota;
                x.Rimborsi += (s.Rimborsi + s.RimborsiTrasferte) * quota;
            }
            // 22/10/2026 (Mirko Maffei): conta la differenza ancora da pagare, tutta; con la ripartizione apposita se c'è.
            foreach (var (area, quota) in QuoteDifferenza(rip, q)) Riga(area).MaturatoNonPagato += s.Differenza * quota;
        }
        Riga(Laboratorio).PremiLaboratorio += r.Premi.Where(p => p.IdSocio is null).Sum(p => p.Importo);

        // Dopo AMM: quanto è dell'amministrazione si divide in parti uguali sulle altre aree (come il contributo del consuntivo).
        var operative = righe.Values.Where(x => !x.DaRipartire && x.Area != Amministrazione).ToList();
        if (righe.TryGetValue(Amministrazione, out var amm) && operative.Count > 0)
            foreach (var x in operative)
            {
                x.QuotaAccessoriAmm = amm.TotaleAccessori / operative.Count;
                x.QuotaMaturatoAmm = amm.MaturatoNonPagato / operative.Count;
            }
        foreach (var x in righe.Values)
        {
            x.IndennitaLaboratorio = Math.Round(x.IndennitaLaboratorio, 2); x.Rspp = Math.Round(x.Rspp, 2);
            x.ResponsabileArea = Math.Round(x.ResponsabileArea, 2); x.Premio = Math.Round(x.Premio, 2);
            x.Assicurazione = Math.Round(x.Assicurazione, 2); x.Amministratore = Math.Round(x.Amministratore, 2);
            x.Rimborsi = Math.Round(x.Rimborsi, 2); x.MaturatoNonPagato = Math.Round(x.MaturatoNonPagato, 2);
            x.QuotaAccessoriAmm = Math.Round(x.QuotaAccessoriAmm, 2); x.QuotaMaturatoAmm = Math.Round(x.QuotaMaturatoAmm, 2);
        }
        var ordine = aree.Select(a => a.Codice).ToList();
        return righe.Values.OrderBy(x => ordine.IndexOf(x.Area) is var i and >= 0 ? i : 99).ToList();
    }
}
