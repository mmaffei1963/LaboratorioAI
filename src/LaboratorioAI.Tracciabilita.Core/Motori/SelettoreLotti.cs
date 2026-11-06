using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Motori;

/// <summary>Ingrediente richiesto da una ricetta (una categoria di materia prima).</summary>
public sealed record IngredienteRichiesto(int IdCategoria, string Nome);

/// <summary>Un lotto segnato «in uso» per una categoria, con i dati che servono a sceglierlo.</summary>
public sealed record LottoInUsoCandidato(
    int IdLotto,
    int IdCategoria,
    string Codice,
    DateTime Inizio,
    DateTime? Fine,
    DateOnly? DataScadenza,
    StatoLotto Stato);

public sealed record LottoProposto(int IdLotto, string Codice, DateOnly? DataScadenza, IReadOnlyList<string> Avvisi);

public sealed class PropostaIngrediente
{
    public int IdCategoria { get; init; }
    public string Nome { get; init; } = "";
    public IReadOnlyList<LottoProposto> Lotti { get; init; } = Array.Empty<LottoProposto>();

    /// <summary>Nessun lotto in uso per questo ingrediente: la produzione non può essere confermata così.</summary>
    public bool Mancante => Lotti.Count == 0;

    public IReadOnlyList<string> Avvisi { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Propone, per ogni ingrediente di una ricetta, i lotti in uso a una data.
///
/// Regole:
/// <list type="bullet">
/// <item>un lotto è in uso se è iniziato entro la data e non è finito prima;</item>
/// <item>i lotti bloccati o richiamati non si propongono mai (si segnala);</item>
/// <item>un lotto scaduto si propone ma con un avviso, perché nascondere un lotto
/// già usato renderebbe la tracciabilità incompleta;</item>
/// <item>se i lotti in uso sono più d'uno (cambio di sacco) si propongono tutti: mai uno solo a caso.</item>
/// </list>
/// </summary>
public static class SelettoreLotti
{
    public static IReadOnlyList<PropostaIngrediente> Proponi(
        IEnumerable<IngredienteRichiesto> ingredienti,
        IEnumerable<LottoInUsoCandidato> lottiInUso,
        DateOnly giorno,
        CriterioProposta criterio)
    {
        var perCategoria = lottiInUso
            .GroupBy(l => l.IdCategoria)
            .ToDictionary(g => g.Key, g => g.ToList());

        var risultato = new List<PropostaIngrediente>();

        foreach (var ingrediente in ingredienti)
        {
            var avvisiIngrediente = new List<string>();
            var proposti = new List<(LottoInUsoCandidato Candidato, List<string> Avvisi)>();

            if (perCategoria.TryGetValue(ingrediente.IdCategoria, out var candidati))
            {
                foreach (var c in candidati)
                {
                    if (!InUsoAl(c, giorno)) continue;

                    if (c.Stato is StatoLotto.Bloccato or StatoLotto.Richiamato)
                    {
                        avvisiIngrediente.Add($"Il lotto {c.Codice} è {(c.Stato == StatoLotto.Bloccato ? "bloccato" : "richiamato")}: non proposto.");
                        continue;
                    }

                    var avvisi = new List<string>();
                    if (c.DataScadenza is { } scadenza && scadenza < giorno)
                        avvisi.Add($"Il lotto {c.Codice} risulta scaduto il {scadenza:dd/MM/yyyy}.");

                    proposti.Add((c, avvisi));
                }
            }

            var ordinati = criterio == CriterioProposta.ScadenzaPiuVicina
                ? proposti.OrderBy(p => p.Candidato.DataScadenza ?? DateOnly.MaxValue).ThenBy(p => p.Candidato.Inizio)
                : proposti.OrderBy(p => p.Candidato.Inizio).ThenBy(p => p.Candidato.IdLotto);

            var lotti = ordinati
                .Select(p => new LottoProposto(p.Candidato.IdLotto, p.Candidato.Codice, p.Candidato.DataScadenza, p.Avvisi))
                .ToList();

            if (lotti.Count == 0)
                avvisiIngrediente.Add($"Nessun lotto in uso per «{ingrediente.Nome}».");
            else if (lotti.Count > 1)
                avvisiIngrediente.Add($"Più lotti in uso per «{ingrediente.Nome}»: controllare quale è stato usato.");

            risultato.Add(new PropostaIngrediente
            {
                IdCategoria = ingrediente.IdCategoria,
                Nome = ingrediente.Nome,
                Lotti = lotti,
                Avvisi = avvisiIngrediente
            });
        }

        return risultato;
    }

    /// <summary>Un lotto è in uso a un giorno se è iniziato entro quel giorno e non è finito prima.</summary>
    public static bool InUsoAl(LottoInUsoCandidato c, DateOnly giorno)
    {
        var inizio = DateOnly.FromDateTime(c.Inizio);
        if (inizio > giorno) return false;
        if (c.Fine is { } fine && DateOnly.FromDateTime(fine) < giorno) return false;
        return true;
    }
}
