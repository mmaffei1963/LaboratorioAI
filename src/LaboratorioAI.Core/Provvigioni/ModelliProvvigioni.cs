namespace LaboratorioAI.Core.Provvigioni;

/// <summary>Categoria di un cliente rispetto all'accordo del commerciale (derivata dalla storia delle fatture).</summary>
public enum CategoriaCliente
{
    /// <summary>Rapporto di laboratorio iniziato prima dell'accordo: non genera 5% né 3%.</summary>
    Esistente = 0,

    /// <summary>Primo anno dalla prima fattura (dopo almeno 24 mesi senza fatture).</summary>
    Nuovo = 1,

    /// <summary>Dal tredicesimo mese, senza limite finché il cliente fattura.</summary>
    Mantenimento = 2,

    /// <summary>Categoria non applicabile (commerciali il cui accordo non classifica i clienti, es. Carloni).</summary>
    NonApplicabile = 9
}

/// <summary>Voce dell'accordo a cui appartiene un movimento di provvigione.</summary>
public enum VoceProvvigione
{
    /// <summary>Voce 1 — clienti nuovi (5%).</summary>
    Nuovo = 1,

    /// <summary>Voce 2 — mantenimento (3%).</summary>
    Mantenimento = 2,

    /// <summary>Voce 3 — sviluppo clienti MARE.A in elenco, a obiettivo raggiunto (2%).</summary>
    Sviluppo = 3,

    /// <summary>Voce 4 — servizi extra laboratorio procurati dal commerciale di laboratorio, primi 12 mesi (10%).</summary>
    ExtraLaboratorio = 4,

    /// <summary>Carloni punto 1 — convenzione indicata dal commerciale, primo anno (15%).</summary>
    ConvenzioneIndicataAnno1 = 11,

    /// <summary>Carloni punto 2 — convenzione indicata dal commerciale, dal 2° al 5° anno (10%).</summary>
    ConvenzioneIndicataAnni2a5 = 12,

    /// <summary>Carloni punto 3 — convenzione segnalata da MARE.A, dal 1° al 5° anno (10%).</summary>
    ConvenzioneSegnalata = 13,

    /// <summary>Carloni punto 4 — altri servizi contrattualizzati con clienti propri (10%).</summary>
    AltriServizi = 14,

    /// <summary>Contratti occasionali (spot, commesse di consulenza con rapporto «occasionale»): solo il primo anno dalla prima fattura.</summary>
    ContrattoOccasionale = 15,

    /// <summary>Contratti di analisi (commesse di laboratorio con il riferimento del commerciale): solo il primo anno dalla prima fattura.</summary>
    ContrattoAnalisi = 16
}

/// <summary>Tipo di accordo: decide quale motore di calcolo si usa e quali voci sono ammesse.</summary>
public enum TipoAccordo
{
    /// <summary>Commerciale di laboratorio: voci Nuovo, Mantenimento, Sviluppo e premio annuale.</summary>
    Laboratorio = 1,

    /// <summary>Convenzioni pluriennali: voci Convenzione indicata (1° anno, anni 2-5), Convenzione segnalata, Altri servizi,
    /// Contratti occasionali e Contratti di analisi (primo anno).</summary>
    Convenzioni = 2
}

public enum StatoAccordo
{
    Bozza = 1,
    Attivo = 2,
    Annullato = 3
}

/// <summary>Natura del movimento.</summary>
public enum TipoMovimento
{
    Incasso = 1,
    NotaCredito = 2,
    SviluppoObiettivo = 3
}

/// <summary>
/// Regole di calcolo del commerciale di laboratorio. In produzione provengono da tabella
/// con date di validità; i valori di default sono quelli concordati il 3/10/2026.
/// </summary>
public sealed record RegoleProvvigioniLab
{
    public decimal AliquotaNuovo { get; init; } = 0.05m;
    public decimal AliquotaMantenimento { get; init; } = 0.03m;
    public decimal AliquotaSviluppo { get; init; } = 0.02m;

    /// <summary>Voce 4: servizi extra laboratorio procurati (consulenza, formazione), per i primi mesi dalla prima commessa.</summary>
    public decimal AliquotaExtraLaboratorio { get; init; } = 0.10m;
    public int MesiExtraLaboratorio { get; init; } = 12;

    /// <summary>Durata in mesi del primo anno (voce 1).</summary>
    public int MesiPrimoAnno { get; init; } = 12;

    /// <summary>Mesi senza fatture di laboratorio oltre i quali il cliente, se torna, è di nuovo "nuovo".</summary>
    public int MesiPausa { get; init; } = 24;

    /// <summary>I rapporti iniziati prima di questa data sono "Esistente" (voce 3 solo se in elenco).</summary>
    public DateTime DataInizioAccordo { get; init; } = new(2025, 9, 1);

    /// <summary>Gli incassi precedenti a questa data non producono movimenti (la storia serve comunque per la classificazione).</summary>
    public DateTime DataInizioCalcolo { get; init; } = new(2025, 9, 1);

    /// <summary>Soglie del premio annuale, in ordine crescente. Premio a gradino: vale la soglia più alta raggiunta.</summary>
    public IReadOnlyList<SogliaPremio> SoglieEPremi { get; init; } = new[]
    {
        new SogliaPremio(50_000m, 800m),
        new SogliaPremio(70_000m, 1_600m),
        new SogliaPremio(100_000m, 3_000m)
    };

    /// <summary>Fattore di proporzione di soglie e premio se cambia l'orario di lavoro (1 = tempo pieno).</summary>
    public decimal FattoreOrario { get; init; } = 1m;
}

public sealed record SogliaPremio(decimal Soglia, decimal Premio);

/// <summary>Fattura di laboratorio (centro 04, voci di analisi) del cliente, con gli importi necessari alla quota di perimetro.</summary>
/// <param name="Cliente">Codice contabilità (CODCF).</param>
/// <param name="ChiaveFattura">Chiave univoca: CODCF|anno|numero documento.</param>
/// <param name="NettoPerimetro">Imponibile delle sole righe di laboratorio.</param>
/// <param name="LordoFattura">Totale fattura IVA inclusa (dal partitario).</param>
/// <param name="NettoPerVoce">Netto nella base di calcolo di ciascuna voce dell'accordo; se assente vale <paramref name="NettoPerimetro"/> per tutte.</param>
public sealed record FatturaLab(string Cliente, string ChiaveFattura, DateTime DataFattura, decimal NettoPerimetro, decimal LordoFattura,
    IReadOnlyDictionary<VoceProvvigione, decimal>? NettoPerVoce = null)
{
    public decimal NettoDi(VoceProvvigione voce) =>
        NettoPerVoce is not null && NettoPerVoce.TryGetValue(voce, out var n) ? n : NettoPerimetro;
}

/// <summary>Incasso (lordo IVA) di una fattura.</summary>
public sealed record IncassoFattura(string ChiaveFattura, DateTime DataIncasso, decimal ImportoLordo);

/// <summary>Nota di credito di laboratorio, dedotta nel trimestre della sua data.</summary>
public sealed record NotaCreditoLab(string Cliente, string Riferimento, DateTime Data, decimal NettoPerimetro,
    IReadOnlyDictionary<VoceProvvigione, decimal>? NettoPerVoce = null)
{
    public decimal NettoDi(VoceProvvigione voce) =>
        NettoPerVoce is not null && NettoPerVoce.TryGetValue(voce, out var n) ? n : NettoPerimetro;
}

/// <summary>Cliente dell'elenco "in sviluppo" con obiettivo di ricavi di laboratorio per l'anno.</summary>
/// <param name="ValidoDal">Primo giorno del trimestre di inserimento nell'elenco: contano gli incassi da questa data.</param>
public sealed record ObiettivoSviluppo(string Cliente, int Anno, DateTime ValidoDal, decimal Obiettivo);

/// <summary>Cliente per cui il commerciale ha procurato servizi extra laboratorio (riferimento della commessa in ArcLab).</summary>
/// <param name="DataInizio">Data della prima commessa di consulenza o formazione con il suo riferimento.</param>
public sealed record IncaricoExtraLaboratorio(string Cliente, DateTime DataInizio);

public sealed record InputProvvigioniLab(
    IReadOnlyList<FatturaLab> Fatture,
    IReadOnlyList<IncassoFattura> Incassi,
    IReadOnlyList<NotaCreditoLab> NoteCredito,
    IReadOnlyList<ObiettivoSviluppo> Obiettivi,
    RegoleProvvigioniLab Regole,
    IReadOnlyList<IncaricoExtraLaboratorio>? IncarichiExtra = null);

/// <summary>Movimento di provvigione: la riga che il trimestre congela alla chiusura.</summary>
/// <param name="Trimestre">Formato <c>2027-T1</c>.</param>

/// <param name="Base">Quota di perimetro incassata (negativa per le note di credito).</param>
public sealed record MovimentoProvvigione(
    string Trimestre,
    string Cliente,
    string Riferimento,
    DateTime Data,
    TipoMovimento Tipo,
    VoceProvvigione Voce,
    CategoriaCliente Categoria,
    decimal Base,
    decimal Aliquota,
    decimal Importo);

public sealed record RisultatoPremio(int Anno, decimal BaseIncassata, decimal Premio, decimal? ProssimaSoglia);

/// <summary>Avanzamento di un cliente dell'elenco "in sviluppo" verso l'obiettivo concordato.</summary>
/// <param name="Cumulato">Quota di perimetro incassata nell'anno dal trimestre di ingresso, al netto delle note.</param>
/// <param name="Provvigione">2% sul cumulato se l'obiettivo è raggiunto, altrimenti zero.</param>
public sealed record AvanzamentoObiettivo(string Cliente, int Anno, decimal Obiettivo, decimal Cumulato, bool Raggiunto, decimal Provvigione);

public sealed record RisultatoProvvigioniLab(
    IReadOnlyList<MovimentoProvvigione> Movimenti,
    IReadOnlyList<RisultatoPremio> Premi,
    IReadOnlyList<string> Avvisi,
    IReadOnlyList<AvanzamentoObiettivo> Obiettivi);
