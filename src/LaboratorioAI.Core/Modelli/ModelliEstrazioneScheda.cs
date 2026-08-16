namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Valori nutrizionali estratti da una scheda tecnica in PDF tramite AI.
/// È una PROPOSTA da rivedere e confermare a mano: i valori dichiarati in
/// etichetta restano responsabilità del committente.
/// </summary>
public record EstrazioneScheda
{
    public string? Denominazione { get; init; }

    public decimal? Grassi { get; init; }
    public decimal? GrassiSaturi { get; init; }
    public decimal? Carboidrati { get; init; }
    public decimal? Zuccheri { get; init; }
    public decimal? Proteine { get; init; }
    public decimal? Fibre { get; init; }
    public decimal? Sale { get; init; }
    public decimal? Acqua { get; init; }
    public decimal? EnergiaKj { get; init; }
    public decimal? EnergiaKcal { get; init; }

    /// <summary>Base dei valori: "100g", "porzione" o "sconosciuta".</summary>
    public string? Base { get; init; }

    /// <summary>Avvertenze dell'estrazione (per porzione, sodio convertito, ecc.).</summary>
    public string? Note { get; init; }
}
