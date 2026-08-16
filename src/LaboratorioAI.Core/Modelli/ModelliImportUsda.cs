namespace LaboratorioAI.Core.Modelli;

/// <summary>Esito dell'importazione USDA FoodData Central.</summary>
public record EsitoImportUsda
{
    public int Alimenti { get; init; }
    public int Nuove { get; init; }
    public int Aggiornate { get; init; }
}
