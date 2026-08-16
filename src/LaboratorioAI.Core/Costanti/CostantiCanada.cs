namespace LaboratorioAI.Core.Costanti;

/// <summary>
/// Valori giornalieri di riferimento (Daily Values) canadesi per il Tableau de
/// la valeur nutritive / Nutrition Facts table (Health Canada, 2016, FDR).
/// </summary>
public static class CostantiCanada
{
    public const decimal DvGrassiG = 75m;
    public const decimal DvSaturiTransG = 20m;   // saturi + trans, %DV combinata
    public const decimal DvFibreG = 28m;
    public const decimal DvZuccheriG = 100m;
    public const decimal DvSodioMg = 2300m;
    public const decimal DvPotassioMg = 3400m;
    public const decimal DvCalcioMg = 1300m;
    public const decimal DvFerroMg = 18m;
    // Carboidrati, colesterolo, proteine: dichiarati senza %DV.
}
