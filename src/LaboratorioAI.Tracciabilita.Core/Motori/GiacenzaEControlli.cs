using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Motori;

public sealed record MovimentoGiacenza(int IdLotto, int IdInsediamento, decimal Quantita);

/// <summary>La giacenza non si scrive mai: è la somma dei movimenti.</summary>
public static class Giacenza
{
    public static decimal DelLotto(IEnumerable<MovimentoGiacenza> movimenti, int idLotto, int? idInsediamento = null) =>
        movimenti
            .Where(m => m.IdLotto == idLotto && (idInsediamento is null || m.IdInsediamento == idInsediamento))
            .Sum(m => m.Quantita);

    public static IReadOnlyDictionary<(int IdLotto, int IdInsediamento), decimal> PerLottoESede(IEnumerable<MovimentoGiacenza> movimenti) =>
        movimenti
            .GroupBy(m => (m.IdLotto, m.IdInsediamento))
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Quantita));

    /// <summary>Lotti con giacenza negativa in una sede: si è consumato o spedito più di quanto registrato.</summary>
    public static IReadOnlyList<(int IdLotto, int IdInsediamento, decimal Giacenza)> Negative(IEnumerable<MovimentoGiacenza> movimenti) =>
        PerLottoESede(movimenti)
            .Where(x => x.Value < 0)
            .Select(x => (x.Key.IdLotto, x.Key.IdInsediamento, x.Value))
            .OrderBy(x => x.IdLotto).ThenBy(x => x.IdInsediamento)
            .ToList();
}

/// <summary>Crea gli storni: i movimenti non si modificano, si annullano con un movimento uguale e contrario.</summary>
public static class StornoMovimento
{
    public static MovimentoLotto Crea(MovimentoLotto originale, DateTime oraUtc, int? idUtente, string motivo)
    {
        if (originale.IdMovimento <= 0)
            throw new InvalidOperationException("Si può stornare solo un movimento già registrato.");
        if (originale.Tipo == TipoMovimento.Storno)
            throw new InvalidOperationException("Uno storno non si storna: registrare un nuovo movimento.");
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Il motivo dello storno è obbligatorio.", nameof(motivo));

        return new MovimentoLotto
        {
            IdAzienda = originale.IdAzienda,
            IdInsediamento = originale.IdInsediamento,
            IdLotto = originale.IdLotto,
            Tipo = TipoMovimento.Storno,
            Quantita = -originale.Quantita,
            UnitaMisura = originale.UnitaMisura,
            DataMovimento = oraUtc,
            IdAccettazione = originale.IdAccettazione,
            IdProduzione = originale.IdProduzione,
            IdSpedizione = originale.IdSpedizione,
            IdTrasferimento = originale.IdTrasferimento,
            IdMovimentoStornato = originale.IdMovimento,
            Nota = motivo.Trim(),
            IdUtente = idUtente,
            DataCreazione = oraUtc
        };
    }
}

public sealed record ProduzioneDaControllare(int IdProduzione, DateOnly Giorno, IReadOnlyCollection<int> CategorieAttese, IReadOnlyCollection<int> CategorieRegistrate);

public sealed record LottoInUsoDaControllare(int IdLotto, int IdCategoria, string Codice, DateOnly? DataScadenza, StatoLotto Stato, bool Aperto);

/// <summary>Controlli di coerenza mostrati nella pagina degli allarmi.</summary>
public static class ControlliCoerenza
{
    /// <summary>Produzioni in cui manca il lotto di uno o più ingredienti della ricetta.</summary>
    public static IReadOnlyList<(int IdProduzione, IReadOnlyList<int> CategorieMancanti)> ProduzioniIncomplete(
        IEnumerable<ProduzioneDaControllare> produzioni) =>
        produzioni
            .Select(p => (p.IdProduzione, Mancanti: (IReadOnlyList<int>)p.CategorieAttese.Except(p.CategorieRegistrate).OrderBy(x => x).ToList()))
            .Where(x => x.Mancanti.Count > 0)
            .OrderBy(x => x.IdProduzione)
            .ToList();

    /// <summary>Lotti ancora in uso ma scaduti, bloccati o richiamati.</summary>
    public static IReadOnlyList<(int IdLotto, string Motivo)> LottiInUsoDaVerificare(
        IEnumerable<LottoInUsoDaControllare> lotti, DateOnly oggi)
    {
        var elenco = new List<(int, string)>();
        foreach (var l in lotti.Where(l => l.Aperto))
        {
            if (l.Stato == StatoLotto.Bloccato) elenco.Add((l.IdLotto, $"Il lotto {l.Codice} è bloccato ma risulta ancora in uso."));
            else if (l.Stato == StatoLotto.Richiamato) elenco.Add((l.IdLotto, $"Il lotto {l.Codice} è richiamato ma risulta ancora in uso."));
            else if (l.DataScadenza is { } d && d < oggi) elenco.Add((l.IdLotto, $"Il lotto {l.Codice} è scaduto il {d:dd/MM/yyyy} e risulta ancora in uso."));
        }
        return elenco.OrderBy(x => x.Item1).ToList();
    }
}
