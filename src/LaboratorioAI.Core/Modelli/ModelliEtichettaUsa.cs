namespace LaboratorioAI.Core.Modelli;

/// <summary>Una voce della Nutrition Facts: quantità (già arrotondata FDA) e %DV.</summary>
public record RigaUsa
{
    public string Quantita { get; init; } = "";
    public int? Dv { get; init; }
}

/// <summary>
/// Dati della Nutrition Facts label (USA, 21 CFR 101.9). Le quantità sono già
/// arrotondate secondo le regole FDA; le %DV sono calcolate sui valori esatti.
/// </summary>
public record NutrizioneUsa
{
    public decimal PorzioneG { get; init; }
    public string Calorie { get; init; } = "";

    public RigaUsa Grassi { get; init; } = new();
    public RigaUsa GrassiSaturi { get; init; } = new();
    public string GrassiTrans { get; init; } = "";

    public RigaUsa Colesterolo { get; init; } = new();
    public RigaUsa Sodio { get; init; } = new();
    public RigaUsa Carboidrati { get; init; } = new();
    public RigaUsa Fibre { get; init; } = new();
    public string ZuccheriTotali { get; init; } = "";
    public RigaUsa ZuccheriAggiunti { get; init; } = new();
    public string Proteine { get; init; } = "";

    public RigaUsa VitaminaD { get; init; } = new();
    public RigaUsa Calcio { get; init; } = new();
    public RigaUsa Ferro { get; init; } = new();
    public RigaUsa Potassio { get; init; } = new();

    /// <summary>Segnalazioni (es. micronutrienti mancanti negli ingredienti).</summary>
    public IReadOnlyList<string> Avvisi { get; init; } = new List<string>();
}

/// <summary>Dati per generare il pannello grafico Nutrition Facts (USA).</summary>
public record DatiEtichettaUsa
{
    public string Denominazione { get; init; } = "";
    public decimal? PorzioniPerConfezione { get; init; }

    /// <summary>Descrizione testuale della porzione (misura casalinga), es. "1/4 crust". Mostrata come "1/4 crust (45g)".</summary>
    public string? PorzioneDescrizione { get; init; }

    public NutrizioneUsa Facts { get; init; } = new();

    // ── Scheda di verifica dati (approvazione committente) ──────────
    public IReadOnlyList<IngredienteRapporto> Ingredienti { get; init; } = new List<IngredienteRapporto>();
    public decimal? MassaTotaleKg { get; init; }
    public decimal? MassaCottaKg { get; init; }
}
