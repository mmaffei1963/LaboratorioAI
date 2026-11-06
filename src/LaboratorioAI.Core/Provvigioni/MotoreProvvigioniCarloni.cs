namespace LaboratorioAI.Core.Provvigioni;

/// <summary>Origine di una convenzione pluriennale rispetto al commerciale.</summary>
public enum OrigineConvenzione
{
    /// <summary>Contratto indicato dal commerciale: 15% il primo anno, 10% dal secondo al quinto.</summary>
    IndicataDalCommerciale = 1,

    /// <summary>Contratto segnalato da MARE.A: 10% dal primo al quinto anno.</summary>
    SegnalataDaAzienda = 2
}

/// <summary>Regole dell'accordo di Andrea Carloni (sicurezza, HACCP, privacy, formazione, analisi).</summary>
public sealed record RegoleProvvigioniCarloni
{
    public decimal AliquotaIndicataAnno1 { get; init; } = 0.15m;
    public decimal AliquotaIndicataAnni2a5 { get; init; } = 0.10m;
    public decimal AliquotaSegnalata { get; init; } = 0.10m;
    public decimal AliquotaAltriServizi { get; init; } = 0.10m;

    /// <summary>Contratti occasionali (spot): solo il primo anno (accordo dell'08/01/2025: 10%).</summary>
    public decimal AliquotaOccasionale { get; init; } = 0.10m;

    /// <summary>Contratti di analisi: solo il primo anno (accordo dell'08/01/2025: 5%).</summary>
    public decimal AliquotaAnalisi { get; init; } = 0.05m;

    /// <summary>Durata in mesi del «primo anno» dei contratti occasionali e di analisi, dalla prima fattura.</summary>
    public int MesiPrimoAnno { get; init; } = 12;

    /// <summary>Durata in anni della convenzione che dà diritto al compenso.</summary>
    public int AnniConvenzione { get; init; } = 5;

    /// <summary>Gli incassi precedenti a questa data non producono movimenti.</summary>
    public DateTime DataInizioCalcolo { get; init; } = new(2025, 9, 1);

    /// <summary>
    /// Fine del rapporto di lavoro, se nota. I compensi dei punti 2 e 3 valgono solo per i ricavi
    /// fatturati durante il rapporto (testo dell'accordo): le fatture successive non contano per queste voci.
    /// </summary>
    public DateTime? DataFineRapporto { get; init; }
}

/// <summary>Tipo di contratto «a primo anno» ricavato dalle commesse ArcLab con il riferimento del commerciale.</summary>
public enum TipoContrattoCommerciale
{
    /// <summary>Commessa di consulenza con rapporto occasionale (TipoStampa = 2).</summary>
    Occasionale = 1,

    /// <summary>Commessa di analisi (tipo 3) con il riferimento del commerciale.</summary>
    Analisi = 2
}

/// <summary>Contratto occasionale o di analisi: il primo anno parte dalla prima fattura del servizio dalla data della commessa.</summary>
public sealed record ContrattoCommerciale(string Cliente, DateTime DataCommessa, TipoContrattoCommerciale Tipo);

/// <summary>Convenzione pluriennale registrata dalla schermata (non esiste in AZW né in ArcLab).</summary>
public sealed record ConvenzioneCommerciale(string Cliente, DateTime DataInizio, OrigineConvenzione Origine);

/// <param name="Netto">Imponibile totale della fattura (tutti i servizi).</param>
/// <param name="Lordo">Totale IVA inclusa.</param>
/// <param name="NettoPerVoce">Netto nella base di calcolo di ciascuna voce; se assente vale <paramref name="Netto"/> per tutte.</param>
public sealed record FatturaCommerciale(string Cliente, string ChiaveFattura, DateTime DataFattura, decimal Netto, decimal Lordo,
    IReadOnlyDictionary<VoceProvvigione, decimal>? NettoPerVoce = null)
{
    public decimal NettoDi(VoceProvvigione voce) =>
        NettoPerVoce is not null && NettoPerVoce.TryGetValue(voce, out var n) ? n : Netto;
}

public sealed record NotaCreditoCommerciale(string Cliente, string Riferimento, DateTime Data, decimal Netto,
    IReadOnlyDictionary<VoceProvvigione, decimal>? NettoPerVoce = null)
{
    public decimal NettoDi(VoceProvvigione voce) =>
        NettoPerVoce is not null && NettoPerVoce.TryGetValue(voce, out var n) ? n : Netto;
}

public sealed record InputProvvigioniCarloni(
    IReadOnlyList<FatturaCommerciale> Fatture,
    IReadOnlyList<IncassoFattura> Incassi,
    IReadOnlyList<NotaCreditoCommerciale> NoteCredito,
    IReadOnlyList<ConvenzioneCommerciale> Convenzioni,
    IReadOnlySet<string> ClientiPropri,
    RegoleProvvigioniCarloni Regole,
    IReadOnlyList<ContrattoCommerciale>? Contratti = null);

/// <summary>
/// Motore di calcolo delle provvigioni di Carloni. Funzione pura, come <see cref="MotoreProvvigioniLab"/>.
///
/// Per ogni incasso (quota imponibile = incasso lordo × netto ÷ lordo della fattura) la voce dipende dalla data
/// della fattura: se cade negli anni di una convenzione del cliente → punti 1, 2 o 3 secondo origine e anno;
/// altrimenti, se cade nel primo anno di un contratto occasionale → contratti occasionali; altrimenti, se il cliente
/// è proprio → punto 4 (altri servizi). In più, se cade nel primo anno di un contratto di analisi, la parte di analisi
/// della fattura matura la voce «contratti di analisi». Il primo anno di un contratto occasionale o di analisi
/// parte dalla prima fattura del cliente con importi nella base di quella voce, emessa dalla data della commessa.
/// Le note di credito si deducono nel trimestre della nota con le voci valide alla data della nota.
/// </summary>
public sealed class MotoreProvvigioniCarloni
{
    public RisultatoProvvigioniLab Calcola(InputProvvigioniCarloni input)
    {
        var regole = input.Regole;
        var avvisi = new List<string>();
        var movimenti = new List<MovimentoProvvigione>();
        var fatture = input.Fatture.ToDictionary(f => f.ChiaveFattura, StringComparer.Ordinal);
        var finestre = Finestre(input);

        foreach (var inc in input.Incassi)
        {
            if (!fatture.TryGetValue(inc.ChiaveFattura, out var f))
            {
                avvisi.Add($"Incasso del {inc.DataIncasso:dd/MM/yyyy} senza fattura: {inc.ChiaveFattura}");
                continue;
            }
            if (f.Lordo <= 0m) { avvisi.Add($"Fattura con lordo non positivo: {f.ChiaveFattura}"); continue; }
            if (inc.DataIncasso < regole.DataInizioCalcolo) continue;

            foreach (var voce in Classifica(f.Cliente, f.DataFattura, input, finestre))
            {
                var netto = f.NettoDi(voce);
                if (netto == 0m) continue;
                var aliquota = AliquotaDi(voce, regole);
                var quota = inc.ImportoLordo * netto / f.Lordo;
                movimenti.Add(new MovimentoProvvigione(
                    Trimestre(inc.DataIncasso), f.Cliente, f.ChiaveFattura, inc.DataIncasso,
                    TipoMovimento.Incasso, voce, CategoriaCliente.NonApplicabile,
                    Arrotonda(quota), aliquota, Arrotonda(quota * aliquota)));
            }
        }

        foreach (var n in input.NoteCredito)
        {
            if (n.Data < regole.DataInizioCalcolo) continue;
            foreach (var voce in Classifica(n.Cliente, n.Data, input, finestre))
            {
                var nettoNota = n.NettoDi(voce);
                if (nettoNota == 0m) continue;
                var aliquota = AliquotaDi(voce, regole);
                var baseNota = -Math.Abs(nettoNota);
                movimenti.Add(new MovimentoProvvigione(
                    Trimestre(n.Data), n.Cliente, n.Riferimento, n.Data,
                    TipoMovimento.NotaCredito, voce, CategoriaCliente.NonApplicabile,
                    Arrotonda(baseNota), aliquota, Arrotonda(baseNota * aliquota)));
            }
        }

        var ordinati = movimenti
            .OrderBy(m => m.Data).ThenBy(m => m.Cliente, StringComparer.Ordinal).ThenBy(m => m.Riferimento, StringComparer.Ordinal)
            .ToList();
        return new RisultatoProvvigioniLab(ordinati, new List<RisultatoPremio>(), avvisi, new List<AvanzamentoObiettivo>());
    }

    /// <summary>Primo anno di un contratto occasionale o di analisi (dal giorno della prima fattura, escluso il giorno finale).</summary>
    private sealed record Finestra(string Cliente, TipoContrattoCommerciale Tipo, DateTime Dal, DateTime Al);

    /// <summary>
    /// Per ogni contratto occasionale o di analisi: la prima fattura del cliente emessa dalla data della commessa con
    /// importi nella base della voce apre il primo anno (<see cref="RegoleProvvigioniCarloni.MesiPrimoAnno"/> mesi).
    /// Un contratto senza fatture successive non apre nulla.
    /// </summary>
    private static List<Finestra> Finestre(InputProvvigioniCarloni input)
    {
        var finestre = new List<Finestra>();
        if (input.Contratti is null) return finestre;
        var perCliente = input.Fatture.ToLookup(f => f.Cliente, StringComparer.Ordinal);
        foreach (var c in input.Contratti)
        {
            var voce = c.Tipo == TipoContrattoCommerciale.Analisi ? VoceProvvigione.ContrattoAnalisi : VoceProvvigione.ContrattoOccasionale;
            var prima = perCliente[c.Cliente]
                .Where(f => f.DataFattura.Date >= c.DataCommessa.Date && f.NettoDi(voce) != 0m)
                .OrderBy(f => f.DataFattura).FirstOrDefault();
            if (prima is null) continue;
            var dal = prima.DataFattura.Date;
            var w = new Finestra(c.Cliente, c.Tipo, dal, dal.AddMonths(input.Regole.MesiPrimoAnno));
            if (!finestre.Contains(w)) finestre.Add(w);
        }
        return finestre;
    }

    private static bool InFinestra(List<Finestra> finestre, string cliente, TipoContrattoCommerciale tipo, DateTime data) =>
        finestre.Any(w => w.Cliente == cliente && w.Tipo == tipo && data.Date >= w.Dal && data.Date < w.Al);

    /// <summary>
    /// Voci valide per il cliente alla data indicata (della fattura o della nota). Al più una voce di servizio
    /// (convenzione, poi contratto occasionale, poi altri servizi) più l'eventuale voce dei contratti di analisi.
    /// </summary>
    private static List<VoceProvvigione> Classifica(string cliente, DateTime data, InputProvvigioniCarloni input, List<Finestra> finestre)
    {
        var voci = new List<VoceProvvigione>();
        var regole = input.Regole;

        // Convenzione in corso alla data: la più recente tra quelle iniziate prima e ancora nella durata.
        var convenzione = input.Convenzioni
            .Where(c => c.Cliente == cliente && c.DataInizio.Date <= data.Date && data.Date < c.DataInizio.Date.AddYears(regole.AnniConvenzione))
            .OrderByDescending(c => c.DataInizio)
            .FirstOrDefault();

        if (convenzione is not null)
        {
            var anno = 1;
            while (convenzione.DataInizio.Date.AddYears(anno) <= data.Date) anno++;

            // Punti 2 e 3: contano solo i ricavi fatturati durante il rapporto di lavoro.
            var oltreRapporto = regole.DataFineRapporto is { } fine && data.Date > fine.Date;

            if (convenzione.Origine == OrigineConvenzione.IndicataDalCommerciale && anno == 1)
                voci.Add(VoceProvvigione.ConvenzioneIndicataAnno1);
            else if (!oltreRapporto)
                voci.Add(convenzione.Origine == OrigineConvenzione.IndicataDalCommerciale
                    ? VoceProvvigione.ConvenzioneIndicataAnni2a5 : VoceProvvigione.ConvenzioneSegnalata);
        }
        else if (InFinestra(finestre, cliente, TipoContrattoCommerciale.Occasionale, data))
            voci.Add(VoceProvvigione.ContrattoOccasionale);
        else if (input.ClientiPropri.Contains(cliente))
            voci.Add(VoceProvvigione.AltriServizi);

        if (InFinestra(finestre, cliente, TipoContrattoCommerciale.Analisi, data))
            voci.Add(VoceProvvigione.ContrattoAnalisi);
        return voci;
    }

    private static decimal AliquotaDi(VoceProvvigione voce, RegoleProvvigioniCarloni r) => voce switch
    {
        VoceProvvigione.ConvenzioneIndicataAnno1 => r.AliquotaIndicataAnno1,
        VoceProvvigione.ConvenzioneIndicataAnni2a5 => r.AliquotaIndicataAnni2a5,
        VoceProvvigione.ConvenzioneSegnalata => r.AliquotaSegnalata,
        VoceProvvigione.AltriServizi => r.AliquotaAltriServizi,
        VoceProvvigione.ContrattoOccasionale => r.AliquotaOccasionale,
        VoceProvvigione.ContrattoAnalisi => r.AliquotaAnalisi,
        _ => throw new ArgumentOutOfRangeException(nameof(voce))
    };

    private static decimal Arrotonda(decimal v) => Math.Round(v, 2, MidpointRounding.ToEven);

    private static string Trimestre(DateTime d) => $"{d.Year}-T{(d.Month - 1) / 3 + 1}";
}
