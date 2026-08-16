namespace LaboratorioAI.Core.Costanti;

/// <summary>
/// Valori giornalieri di riferimento (Daily Values) FDA per la Nutrition Facts
/// label, aggiornamento 2016 (21 CFR 101.9). Usati per il calcolo delle %DV.
/// </summary>
public static class CostantiUsa
{
    public const decimal DvGrassiG = 78m;
    public const decimal DvSaturiG = 20m;
    public const decimal DvColesteroloMg = 300m;
    public const decimal DvSodioMg = 2300m;
    public const decimal DvCarboidratiG = 275m;
    public const decimal DvFibreG = 28m;
    public const decimal DvZuccheriAggiuntiG = 50m;
    public const decimal DvProteineG = 50m;      // %DV facoltativa salvo claim
    public const decimal DvVitaminaDUg = 20m;
    public const decimal DvCalcioMg = 1300m;
    public const decimal DvFerroMg = 18m;
    public const decimal DvPotassioMg = 4700m;
}
