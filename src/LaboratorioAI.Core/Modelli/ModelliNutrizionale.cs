// LaboratorioAI.Core/Modelli/ModelliNutrizionale.cs
//
// Modelli di calcolo per i report nutrizionali (Reg. UE 1169/2011).
// Sono modelli di puro calcolo, indipendenti dalla persistenza: le entità EF
// del catalogo materie prime / ricette verranno aggiunte in un passo successivo.

namespace LaboratorioAI.Core.Modelli;

/// <summary>
/// Valori nutrizionali per 100 g di parte edibile di una materia prima.
/// Gli otto valori usati oggi; l'energia si ricava dai macro (Allegato XIV).
/// </summary>
public record ValoriPer100g
{
    public decimal Grassi { get; init; }
    public decimal GrassiSaturi { get; init; }
    public decimal Carboidrati { get; init; }
    public decimal Zuccheri { get; init; }
    public decimal Proteine { get; init; }
    public decimal Sale { get; init; }
    public decimal Fibre { get; init; }
    public decimal Acqua { get; init; }

    // ── Nutrienti aggiuntivi (USA/Canada), nullable = sconosciuto ──
    public decimal? Sodio { get; init; }              // mg
    public decimal? GrassiTrans { get; init; }        // g
    public decimal? GrassiMonoinsaturi { get; init; } // g
    public decimal? GrassiPolinsaturi { get; init; }  // g
    public decimal? Colesterolo { get; init; }        // mg
    public decimal? VitaminaD { get; init; }          // µg
    public decimal? Calcio { get; init; }             // mg
    public decimal? Ferro { get; init; }              // mg
    public decimal? Potassio { get; init; }           // mg
}

/// <summary>Un ingrediente della ricetta: materia prima + quantità in kg.</summary>
public record IngredienteCalcolo
{
    public string Denominazione { get; init; } = "";
    public decimal QuantitaKg { get; init; }
    public ValoriPer100g Valori { get; init; } = new();
}

/// <summary>Dati in ingresso al calcolo di una ricetta.</summary>
public record DatiCalcoloNutrizionale
{
    public IReadOnlyList<IngredienteCalcolo> Ingredienti { get; init; } = new List<IngredienteCalcolo>();

    /// <summary>Unità di consumo in grammi (per la dichiarazione a porzione).</summary>
    public decimal UnitaConsumoG { get; init; } = 100m;

    /// <summary>Calo peso in cottura come percentuale del peso crudo (tutto assunto acqua).</summary>
    public decimal? CaloPesoPercentuale { get; init; }

    /// <summary>In alternativa: peso del prodotto cotto (kg). Se presente, prevale e ricava la %.</summary>
    public decimal? PesoCottoKg { get; init; }
}

/// <summary>
/// Dichiarazione nutrizionale (valori energetici + nutrienti). Usata sia per
/// «per 100 g» sia per «per unità di consumo», e nella versione arrotondata.
/// </summary>
public record Dichiarazione
{
    public decimal EnergiaKj { get; init; }
    public decimal EnergiaKcal { get; init; }
    public decimal Grassi { get; init; }
    public decimal GrassiSaturi { get; init; }
    public decimal Carboidrati { get; init; }
    public decimal Zuccheri { get; init; }
    public decimal Proteine { get; init; }
    public decimal Sale { get; init; }
    public decimal Fibre { get; init; }
    public decimal Acqua { get; init; }

    // ── Nutrienti aggiuntivi (per etichetta USA/Canada) ────────────
    public decimal Sodio { get; init; }              // mg
    public decimal GrassiTrans { get; init; }        // g
    public decimal GrassiMonoinsaturi { get; init; } // g
    public decimal GrassiPolinsaturi { get; init; }  // g
    public decimal Colesterolo { get; init; }        // mg
    public decimal VitaminaD { get; init; }          // µg
    public decimal Calcio { get; init; }             // mg
    public decimal Ferro { get; init; }              // mg
    public decimal Potassio { get; init; }           // mg
}

/// <summary>
/// Percentuali di assunzione di riferimento (Allegato XIII, adulto medio).
/// Espresse come frazione (0..1); il report le mostra ×100.
/// </summary>
public record PercentualiRiferimento
{
    public decimal EnergiaKj { get; init; }
    public decimal EnergiaKcal { get; init; }
    public decimal Grassi { get; init; }
    public decimal GrassiSaturi { get; init; }
    public decimal Carboidrati { get; init; }
    public decimal Zuccheri { get; init; }
    public decimal Proteine { get; init; }
    public decimal Sale { get; init; }
}

/// <summary>Esito completo del calcolo nutrizionale di una ricetta.</summary>
public record RisultatoNutrizionale
{
    /// <summary>Valori esatti (non arrotondati) per 100 g di prodotto cotto.</summary>
    public Dichiarazione Per100g { get; init; } = new();

    /// <summary>Valori esatti per unità di consumo.</summary>
    public Dichiarazione PerUnitaConsumo { get; init; } = new();

    /// <summary>Valori per 100 g arrotondati secondo le regole di etichettatura.</summary>
    public Dichiarazione Per100gArrotondato { get; init; } = new();

    /// <summary>Valori per unità di consumo arrotondati.</summary>
    public Dichiarazione PerUnitaArrotondato { get; init; } = new();

    public PercentualiRiferimento RiferimentiPer100g { get; init; } = new();
    public PercentualiRiferimento RiferimentiPerUnita { get; init; } = new();

    public decimal MassaTotaleKg { get; init; }
    public decimal MassaCottaKg { get; init; }

    /// <summary>Segnalazioni non bloccanti (es. materia prima con somma componenti lontana da 100).</summary>
    public IReadOnlyList<string> Avvisi { get; init; } = new List<string>();
}
