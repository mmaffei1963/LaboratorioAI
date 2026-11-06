namespace LaboratorioAI.Core.Direzione;

/// <summary>
/// Un anno di un commerciale (cruscotto Direzione, fase 3, 13/10/2026). Definizioni decise da Mirko Maffei il 13/10/2026:
/// <list type="bullet">
/// <item><b>Costo</b> = voce personale del commerciale in AZW (es. ZAT, CAR: costo completo) + provvigioni calcolate da LaboratorioAI.</item>
/// <item><b>Portato</b> = base delle provvigioni: incassi (al netto delle note) su cui il modulo provvigioni ha calcolato una provvigione.</item>
/// <item><b>Seguito</b> = fatturato netto dei clienti che oggi in AZW hanno il suo codice venditore (senza storico).</item>
/// <item><b>Margine portato</b> = portato × margine di contribuzione % delle aree in cui è registrato il suo costo.</item>
/// </list>
/// </summary>
public record RendimentoAnno(
    int Anno,
    decimal CostoStipendio,
    decimal Provvigioni,
    decimal Portato,
    decimal PortatoNuovi,
    int ClientiPortati,
    decimal Seguito,
    int ClientiSeguiti,
    int ClientiSeguitiNuovi,
    int ClientiSeguitiPersi,
    decimal? MargineContribuzionePercento,
    decimal FatturatoLordoSeguito,
    decimal IncassatoLordoSeguito,
    int ClientiSeguitiAcquisiti = 0)
{
    public decimal CostoTotale => CostoStipendio + Provvigioni;
    public decimal? IncidenzaSuPortato => Rapporto(CostoTotale, Portato);
    public decimal? IncidenzaSuSeguito => Rapporto(CostoTotale, Seguito);
    /// <summary>Euro portati per ogni euro speso nel commerciale.</summary>
    public decimal? PortatoPerEuro => Rapporto(Portato, CostoTotale);
    public decimal? MarginePortato => MargineContribuzionePercento is decimal m ? Math.Round(Portato * m, 2) : null;
    /// <summary>Margine portato meno costo: positivo = il commerciale si ripaga nell'anno.</summary>
    public decimal? Contributo => MarginePortato is decimal mp ? mp - CostoTotale : null;
    /// <summary>Quota incassata delle fatture dell'anno dei clienti seguiti (lordo su lordo).</summary>
    public decimal? PercentualeIncassata => Rapporto(IncassatoLordoSeguito, FatturatoLordoSeguito);

    private static decimal? Rapporto(decimal a, decimal b) => b <= 0 ? null : a / b;
}

/// <summary>Un commerciale con i suoi anni e i clienti portati nell'ultimo anno completo.</summary>
public record RendimentoCommerciale(
    int IdCommerciale,
    string Nome,
    string? VoceCosto,
    string? DescrizioneVoceCosto,
    IReadOnlyList<(string Centro, decimal Peso)> Aree,
    int? AnnoInizio,
    IReadOnlyList<RendimentoAnno> Anni,
    IReadOnlyList<ClienteConfronto> ClientiPortati,
    IReadOnlyList<string> Avvisi)
{
    /// <summary>Contributo cumulato dall'anno di inizio: quando torna positivo, il commerciale si è ripagato.</summary>
    public IReadOnlyList<(int Anno, decimal Cumulato)> ContributoCumulato =>
        MotoreRendimentoCommerciali.Cumulato(Anni.Where(a => AnnoInizio is int i && a.Anno >= i));
}

/// <summary>Funzioni pure del rendimento dei commerciali.</summary>
public static class MotoreRendimentoCommerciali
{
    /// <summary>
    /// Voce personale AZW del commerciale: la descrizione della voce contiene tutte le parole del nome (in qualsiasi ordine,
    /// es. «Zatterin Ludovico» per «Ludovico Zatterin»). Nessun dato da inserire a mano; se non si trova, si avvisa.
    /// </summary>
    public static (string Codice, string? Descrizione)? TrovaVoceCosto(string nome, IEnumerable<(string Codice, string? Descrizione, bool Ricavo)> voci)
    {
        var parole = Parole(nome);
        if (parole.Count == 0) return null;
        var candidate = voci.Where(v => !v.Ricavo && v.Descrizione is not null && parole.All(p => Parole(v.Descrizione).Contains(p))).ToList();
        return candidate.Count == 1 ? (candidate[0].Codice, candidate[0].Descrizione) : null;
    }

    private static HashSet<string> Parole(string s) =>
        s.Split(new[] { ' ', '.', ',', '-', '\'' }, StringSplitOptions.RemoveEmptyEntries)
         .Where(p => p.Length > 1).Select(p => p.ToUpperInvariant()).ToHashSet();

    /// <summary>Margine di contribuzione % medio delle aree, pesato su quanto del costo del commerciale è registrato in ciascuna.</summary>
    public static decimal? MargineMedio(IReadOnlyList<(string Centro, decimal Peso)> aree, IReadOnlyDictionary<string, decimal?> marginePerCentro)
    {
        decimal somma = 0, pesi = 0;
        foreach (var (centro, peso) in aree)
            if (peso > 0 && marginePerCentro.TryGetValue(centro, out var m) && m is decimal mm) { somma += mm * peso; pesi += peso; }
        return pesi > 0 ? somma / pesi : null;
    }

    /// <summary>Pesi delle aree dal costo registrato per centro (valori assoluti).</summary>
    public static IReadOnlyList<(string Centro, decimal Peso)> Pesi(IEnumerable<(string Centro, decimal Costo)> costi)
    {
        var perCentro = costi.GroupBy(c => c.Centro).Select(g => (g.Key, Math.Abs(g.Sum(x => x.Costo)))).Where(x => x.Item2 > 0).ToList();
        var tot = perCentro.Sum(x => x.Item2);
        return tot == 0 ? [] : perCentro.Select(x => (x.Key, x.Item2 / tot)).OrderByDescending(x => x.Item2).ToList();
    }

    public static IReadOnlyList<(int Anno, decimal Cumulato)> Cumulato(IEnumerable<RendimentoAnno> anni)
    {
        decimal c = 0;
        var lista = new List<(int, decimal)>();
        foreach (var a in anni.OrderBy(a => a.Anno))
        {
            if (a.Contributo is not decimal v) continue;
            c += v;
            lista.Add((a.Anno, c));
        }
        return lista;
    }
}
