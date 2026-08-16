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
}
