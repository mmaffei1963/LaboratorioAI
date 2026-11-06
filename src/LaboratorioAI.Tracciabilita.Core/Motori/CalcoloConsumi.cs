using LaboratorioAI.Tracciabilita.Core.Enumerazioni;

namespace LaboratorioAI.Tracciabilita.Core.Motori;

/// <summary>Un ingrediente della ricetta con i lotti scelti per quella produzione.</summary>
public sealed record IngredienteDaConsumare(int IdRiga, string Nome, decimal? QuantitaTeorica, IReadOnlyList<int> LottiScelti);

public sealed record RisultatoConsumi(IReadOnlyDictionary<int, decimal?> KgPerLotto, IReadOnlyList<string> Errori, bool CalcolatiDallaRicetta);

/// <summary>
/// Quanti kg di ogni lotto di ingresso sono stati usati in una produzione. Funzione pura.
/// <list type="bullet">
/// <item><b>SoloLotti</b>: nessuna quantità.</item>
/// <item><b>Manuale</b>: valgono solo i kg scritti dall'operatore.</item>
/// <item><b>Teorica</b>: i kg scritti dall'operatore valgono; per gli altri lotti si divide il totale della ricetta
/// (kg ricetta × quantità prodotta ÷ quantità di riferimento) in parti uguali fra i lotti senza valore.</item>
/// </list>
/// </summary>
public static class CalcoloConsumi
{
    public static RisultatoConsumi Calcola(
        ModoQuantitaConsumi modo, bool obbligatorie, decimal? quantitaProdotta, decimal? quantitaRiferimento,
        IReadOnlyList<IngredienteDaConsumare> ingredienti, IReadOnlyDictionary<int, decimal?> digitati)
    {
        var kg = new Dictionary<int, decimal?>();
        var errori = new List<string>();
        var dallaRicetta = false;

        foreach (var ing in ingredienti)
        {
            foreach (var lotto in ing.LottiScelti)
                kg[lotto] = modo != ModoQuantitaConsumi.SoloLotti && digitati.TryGetValue(lotto, out var v) && v is > 0 ? Arrotonda(v.Value) : null;

            if (modo == ModoQuantitaConsumi.SoloLotti) continue;

            var senzaValore = ing.LottiScelti.Where(l => kg[l] is null).ToList();
            if (senzaValore.Count == 0) continue;

            if (modo == ModoQuantitaConsumi.Teorica
                && ing.QuantitaTeorica is > 0 && quantitaProdotta is > 0 && quantitaRiferimento is > 0)
            {
                var totale = ing.QuantitaTeorica.Value * quantitaProdotta.Value / quantitaRiferimento.Value;
                var gia = ing.LottiScelti.Where(l => kg[l] is not null).Sum(l => kg[l]!.Value);
                var residuo = Math.Max(0m, totale - gia);
                var parte = Arrotonda(residuo / senzaValore.Count);
                for (var i = 0; i < senzaValore.Count; i++)
                {
                    // L'ultimo prende il resto, così il totale torna esatto.
                    var q = i == senzaValore.Count - 1 ? Arrotonda(residuo - parte * (senzaValore.Count - 1)) : parte;
                    kg[senzaValore[i]] = q > 0 ? q : null;
                }
                dallaRicetta = true;
                continue;
            }

            if (obbligatorie)
            {
                if (modo == ModoQuantitaConsumi.Manuale)
                    errori.Add($"Scrivi i kg usati per «{ing.Nome}».");
                else if (quantitaProdotta is not > 0)
                    errori.Add($"Scrivi la quantità prodotta: serve per calcolare i kg di «{ing.Nome}».");
                else if (quantitaRiferimento is not > 0)
                    errori.Add("Nella ricetta manca la quantità prodotta di riferimento: indicala nella scheda della ricetta.");
                else
                    errori.Add($"Nella ricetta manca la quantità di «{ing.Nome}»: indicala nella scheda della ricetta, oppure scrivi i kg a mano.");
            }
        }
        return new RisultatoConsumi(kg, errori.Distinct().ToList(), dallaRicetta);
    }

    private static decimal Arrotonda(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
