namespace LaboratorioAI.Core.Costanti;

/// <summary>
/// Costanti normative per il calcolo nutrizionale (Reg. UE 1169/2011).
///
/// - Coefficienti di conversione energetica: Allegato XIV.
/// - Consumi di riferimento dell'adulto medio: Allegato XIII, parte B
///   (8 400 kJ / 2 000 kcal).
///
/// I valori sono fissati dalla norma: raccolti qui in un solo punto perché il
/// motore di calcolo non li abbia sparsi nel codice.
/// </summary>
public static class CostantiNutrizionali
{
    // ── Coefficienti energetici (Allegato XIV) ─────────────────────
    // kJ per grammo / kcal per grammo. Zuccheri e saturi non si sommano:
    // sono sottoinsiemi di carboidrati e grassi.
    public const decimal GrassiKjG = 37m;
    public const decimal GrassiKcalG = 9m;
    public const decimal CarboidratiKjG = 17m;
    public const decimal CarboidratiKcalG = 4m;
    public const decimal ProteineKjG = 17m;
    public const decimal ProteineKcalG = 4m;
    public const decimal FibreKjG = 8m;
    public const decimal FibreKcalG = 2m;

    // ── Consumi di riferimento adulto medio (Allegato XIII, parte B) ─
    public const decimal RiEnergiaKj = 8400m;
    public const decimal RiEnergiaKcal = 2000m;
    public const decimal RiGrassiG = 70m;
    public const decimal RiGrassiSaturiG = 20m;
    public const decimal RiCarboidratiG = 260m;
    public const decimal RiZuccheriG = 90m;
    public const decimal RiProteineG = 50m;
    public const decimal RiSaleG = 6m;
}
