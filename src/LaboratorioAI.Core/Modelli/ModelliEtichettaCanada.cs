namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Dati del Tableau de la valeur nutritive / Nutrition Facts table canadese
/// (bilingue). Quantità già arrotondate; %DV sui valori esatti. Riusa
/// <see cref="RigaUsa"/> per le voci con %DV.
/// </summary>
public record NutrizioneCanada
{
    public decimal PorzioneG { get; init; }
    public string Calorie { get; init; } = "";

    public RigaUsa Grassi { get; init; } = new();
    public string GrassiSaturi { get; init; } = "";
    public string GrassiTrans { get; init; } = "";
    /// <summary>%DV combinata di saturi + trans (contro 20 g).</summary>
    public int? SaturiTransDv { get; init; }

    public string Colesterolo { get; init; } = "";   // senza %DV
    public RigaUsa Sodio { get; init; } = new();
    public string Carboidrati { get; init; } = "";   // senza %DV
    public RigaUsa Fibre { get; init; } = new();
    public RigaUsa Zuccheri { get; init; } = new();
    public string Proteine { get; init; } = "";      // senza %DV

    public RigaUsa Potassio { get; init; } = new();
    public RigaUsa Calcio { get; init; } = new();
    public RigaUsa Ferro { get; init; } = new();

    public IReadOnlyList<string> Avvisi { get; init; } = new List<string>();
}

/// <summary>Dati per generare il pannello grafico canadese bilingue.</summary>
public record DatiEtichettaCanada
{
    public string Denominazione { get; init; } = "";
    public decimal? PorzioniPerConfezione { get; init; }
    public NutrizioneCanada Facts { get; init; } = new();

    // ── Scheda di verifica dati (approvazione committente) ──────────
    public IReadOnlyList<IngredienteRapporto> Ingredienti { get; init; } = new List<IngredienteRapporto>();
    public decimal? MassaTotaleKg { get; init; }
    public decimal? MassaCottaKg { get; init; }
}
