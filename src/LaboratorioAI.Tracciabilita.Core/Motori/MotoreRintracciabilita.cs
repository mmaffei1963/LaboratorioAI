namespace LaboratorioAI.Tracciabilita.Core.Motori;

/// <summary>Il lotto di ingresso è stato usato in una produzione che ha dato il lotto prodotto.</summary>
public sealed record ArcoConsumo(int IdProduzione, int IdLottoIngresso, int IdLottoProdotto, DateOnly Giorno, decimal? Quantita);

/// <summary>Un lotto è stato spedito a un cliente.</summary>
public sealed record ArcoSpedizione(int IdSpedizione, int IdLotto, int IdCliente, DateOnly Data, string? NumeroDocumento, decimal? Quantita);

public sealed record LottoRaggiunto(int IdLotto, int Livello);

public enum DirezioneRicerca
{
    /// <summary>Da un lotto di prodotto agli ingredienti usati.</summary>
    Indietro = 1,
    /// <summary>Da un lotto di ingrediente ai prodotti e ai clienti.</summary>
    Avanti = 2
}

public sealed class RisultatoRicerca
{
    public DirezioneRicerca Direzione { get; init; }
    public int IdLottoOrigine { get; init; }

    /// <summary>Tutti i lotti raggiunti, origine compresa (livello 0), nell'ordine di scoperta.</summary>
    public List<LottoRaggiunto> Lotti { get; } = new();

    public SortedSet<int> Produzioni { get; } = new();

    /// <summary>Spedizioni dei lotti raggiunti (solo nella ricerca «in avanti»).</summary>
    public List<ArcoSpedizione> Spedizioni { get; } = new();

    public SortedSet<int> Clienti { get; } = new();

    /// <summary>La ricerca si è fermata al limite di livelli: il risultato potrebbe essere incompleto.</summary>
    public bool Troncato { get; set; }
}

/// <summary>
/// Motore di rintracciabilità: attraversa il grafo dei lotti, a monte e a valle,
/// su più livelli (un impasto che diventa piadina, ecc.). Funziona su elenchi
/// già caricati, quindi si può collaudare senza database. Le produzioni e le spedizioni
/// annullate vanno escluse prima di passarle qui.
/// </summary>
public sealed class MotoreRintracciabilita
{
    private readonly ILookup<int, ArcoConsumo> _consumiPerProdotto;
    private readonly ILookup<int, ArcoConsumo> _consumiPerIngresso;
    private readonly ILookup<int, ArcoSpedizione> _spedizioniPerLotto;
    private readonly int _maxLivelli;

    public MotoreRintracciabilita(
        IEnumerable<ArcoConsumo> consumi,
        IEnumerable<ArcoSpedizione> spedizioni,
        int maxLivelli = 20)
    {
        var elencoConsumi = consumi.ToList();
        _consumiPerProdotto = elencoConsumi.ToLookup(c => c.IdLottoProdotto);
        _consumiPerIngresso = elencoConsumi.ToLookup(c => c.IdLottoIngresso);
        _spedizioniPerLotto = spedizioni.ToLookup(s => s.IdLotto);
        _maxLivelli = maxLivelli;
    }

    /// <summary>Dal lotto prodotto, tutti gli ingredienti (e gli ingredienti degli ingredienti).</summary>
    public RisultatoRicerca Indietro(int idLotto) =>
        Attraversa(idLotto, DirezioneRicerca.Indietro);

    /// <summary>Dal lotto di un ingrediente, tutte le produzioni, i lotti prodotti e i clienti raggiunti.</summary>
    public RisultatoRicerca Avanti(int idLotto) =>
        Attraversa(idLotto, DirezioneRicerca.Avanti);

    private RisultatoRicerca Attraversa(int origine, DirezioneRicerca direzione)
    {
        var risultato = new RisultatoRicerca { Direzione = direzione, IdLottoOrigine = origine };
        var visitati = new HashSet<int> { origine };
        var coda = new Queue<(int Lotto, int Livello)>();

        coda.Enqueue((origine, 0));
        risultato.Lotti.Add(new LottoRaggiunto(origine, 0));

        while (coda.Count > 0)
        {
            var (lotto, livello) = coda.Dequeue();

            if (direzione == DirezioneRicerca.Avanti)
            {
                foreach (var s in _spedizioniPerLotto[lotto])
                {
                    risultato.Spedizioni.Add(s);
                    risultato.Clienti.Add(s.IdCliente);
                }
            }

            var archi = direzione == DirezioneRicerca.Indietro
                ? _consumiPerProdotto[lotto]
                : _consumiPerIngresso[lotto];

            foreach (var arco in archi)
            {
                risultato.Produzioni.Add(arco.IdProduzione);

                var prossimo = direzione == DirezioneRicerca.Indietro
                    ? arco.IdLottoIngresso
                    : arco.IdLottoProdotto;

                if (!visitati.Add(prossimo)) continue;

                if (livello + 1 > _maxLivelli)
                {
                    risultato.Troncato = true;
                    continue;
                }

                risultato.Lotti.Add(new LottoRaggiunto(prossimo, livello + 1));
                coda.Enqueue((prossimo, livello + 1));
            }
        }

        return risultato;
    }
}
