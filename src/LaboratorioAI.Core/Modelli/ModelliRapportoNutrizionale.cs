namespace LaboratorioAI.Core.Modelli;

/// <summary>Dati per generare il Rapporto di stima dei valori nutrizionali.</summary>
public record DatiRapportoNutrizionale
{
    public string Denominazione { get; init; } = "";
    public string? Committente { get; init; }
    public string? Lotto { get; init; }
    public string? RedattoDa { get; init; }
    public DateOnly DataRapporto { get; init; }

    public decimal UnitaConsumoG { get; init; } = 100m;
    public decimal? UnitaConsumoPezzi { get; init; }

    public RisultatoNutrizionale Risultato { get; init; } = new();

    /// <summary>Ingredienti con quantità e fonte dei valori: il cliente verifica questi dati.</summary>
    public IReadOnlyList<IngredienteRapporto> Ingredienti { get; init; } = new List<IngredienteRapporto>();

    /// <summary>Massa totale della ricetta (somma degli ingredienti), kg.</summary>
    public decimal? MassaTotaleKg { get; init; }

    /// <summary>Massa dopo cottura (peso cotto o calcolata dal calo peso), kg.</summary>
    public decimal? MassaCottaKg { get; init; }
}

/// <summary>Testi fissi del rapporto nutrizionale.</summary>
public static class TestiRapportoNutrizionale
{
    /// <summary>Clausola di condivisione del rischio con il committente.</summary>
    public const string ClausolaResponsabilita =
        "I valori nutrizionali sono stati calcolati (Reg. UE 1169/2011) a partire dalla ricetta, dalle " +
        "quantità dichiarate dal committente e dai valori delle fonti indicate per ciascun ingrediente. " +
        "Utilizzando questi dati per l'etichettatura, il committente si assume la responsabilità di verificare " +
        "la corretta trascrizione della ricetta e delle quantità e l'idoneità delle fonti impiegate. In caso di " +
        "dubbi è tenuto a contattare il laboratorio per chiarimenti prima dell'utilizzo del dato.";
}

/// <summary>Riga ingrediente della scheda di verifica: quantità, percentuale e fonte dei valori.</summary>
public record IngredienteRapporto
{
    public string Denominazione { get; init; } = "";
    public decimal QuantitaKg { get; init; }
    public decimal Percentuale { get; init; }
    public string? Fonte { get; init; }
    public string? Riferimento { get; init; }
}
