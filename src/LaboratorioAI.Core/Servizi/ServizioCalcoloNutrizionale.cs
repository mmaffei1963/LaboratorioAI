using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioCalcoloNutrizionale" />
///
/// <remarks>
/// Riproduce la logica del foglio di calcolo storico, verificata numericamente
/// sul caso «Torta Mimosa»:
/// 1. somma pesata dei nutrienti sulla massa totale (valori per 100 g);
/// 2. calo peso in cottura (acqua evaporata) e ricalcolo per 100 g di cotto;
/// 3. energia con i coefficienti dell'Allegato XIV, calcolata sui valori esatti
///    (prima dell'arrotondamento);
/// 4. dichiarazione per unità di consumo e percentuali di riferimento (All. XIII);
/// 5. versione arrotondata per l'etichetta.
///
/// Nota: le soglie di arrotondamento (metodo <see cref="Arrotonda"/>) seguono le
/// regole comunemente applicate; vanno confermate rispetto alla tabella delle
/// Linee guida del Ministero della Salute (16/6/2016) prima della messa in uso.
/// </remarks>
public class ServizioCalcoloNutrizionale : IServizioCalcoloNutrizionale
{
    public RisultatoNutrizionale Calcola(DatiCalcoloNutrizionale dati)
    {
        var ingredienti = dati.Ingredienti ?? new List<IngredienteCalcolo>();
        decimal massaTotKg = ingredienti.Sum(i => i.QuantitaKg);
        if (massaTotKg <= 0m)
            throw new ArgumentException("La massa totale della ricetta deve essere maggiore di zero.");

        // Grammi assoluti di ciascun nutriente nell'intera ricetta.
        // valore/100 (per 100 g) × grammi di ingrediente (kg × 1000).
        decimal Assoluto(Func<ValoriPer100g, decimal> selettore) =>
            ingredienti.Sum(i => selettore(i.Valori) / 100m * (i.QuantitaKg * 1000m));

        decimal aGrassi = Assoluto(v => v.Grassi);
        decimal aSaturi = Assoluto(v => v.GrassiSaturi);
        decimal aCarbo = Assoluto(v => v.Carboidrati);
        decimal aZucc = Assoluto(v => v.Zuccheri);
        decimal aProt = Assoluto(v => v.Proteine);
        decimal aSale = Assoluto(v => v.Sale);
        decimal aFibre = Assoluto(v => v.Fibre);
        decimal aAcqua = Assoluto(v => v.Acqua);

        // Nutrienti aggiuntivi (USA/Canada): il valore mancante conta come 0.
        decimal aSodio = Assoluto(v => v.Sodio ?? 0m);
        decimal aTrans = Assoluto(v => v.GrassiTrans ?? 0m);
        decimal aMono = Assoluto(v => v.GrassiMonoinsaturi ?? 0m);
        decimal aPoli = Assoluto(v => v.GrassiPolinsaturi ?? 0m);
        decimal aColest = Assoluto(v => v.Colesterolo ?? 0m);
        decimal aVitD = Assoluto(v => v.VitaminaD ?? 0m);
        decimal aCalcio = Assoluto(v => v.Calcio ?? 0m);
        decimal aFerro = Assoluto(v => v.Ferro ?? 0m);
        decimal aPotassio = Assoluto(v => v.Potassio ?? 0m);

        // Calo peso: dalla % (o dal peso cotto) si ricava l'acqua evaporata.
        decimal caloFrazione = 0m;
        if (dati.PesoCottoKg is decimal cotto && cotto > 0m && cotto < massaTotKg)
            caloFrazione = (massaTotKg - cotto) / massaTotKg;
        else if (dati.CaloPesoPercentuale is decimal perc && perc > 0m)
            caloFrazione = perc / 100m;

        decimal massaCottaKg = massaTotKg * (1m - caloFrazione);
        decimal acquaEvaporataG = massaTotKg * 1000m * caloFrazione;
        aAcqua = Math.Max(0m, aAcqua - acquaEvaporataG);

        decimal massaFinaleG = massaCottaKg * 1000m;

        decimal Per100(decimal assolutoG) => assolutoG / massaFinaleG * 100m;

        var grassi = Per100(aGrassi);
        var saturi = Per100(aSaturi);
        var carbo = Per100(aCarbo);
        var zucc = Per100(aZucc);
        var prot = Per100(aProt);
        var sale = Per100(aSale);
        var fibre = Per100(aFibre);
        var acqua = Per100(aAcqua);

        decimal kj = grassi * CostantiNutrizionali.GrassiKjG
                   + carbo * CostantiNutrizionali.CarboidratiKjG
                   + prot * CostantiNutrizionali.ProteineKjG
                   + fibre * CostantiNutrizionali.FibreKjG;
        decimal kcal = grassi * CostantiNutrizionali.GrassiKcalG
                     + carbo * CostantiNutrizionali.CarboidratiKcalG
                     + prot * CostantiNutrizionali.ProteineKcalG
                     + fibre * CostantiNutrizionali.FibreKcalG;

        var per100 = new Dichiarazione
        {
            EnergiaKj = kj,
            EnergiaKcal = kcal,
            Grassi = grassi,
            GrassiSaturi = saturi,
            Carboidrati = carbo,
            Zuccheri = zucc,
            Proteine = prot,
            Sale = sale,
            Fibre = fibre,
            Acqua = acqua,
            Sodio = Per100(aSodio),
            GrassiTrans = Per100(aTrans),
            GrassiMonoinsaturi = Per100(aMono),
            GrassiPolinsaturi = Per100(aPoli),
            Colesterolo = Per100(aColest),
            VitaminaD = Per100(aVitD),
            Calcio = Per100(aCalcio),
            Ferro = Per100(aFerro),
            Potassio = Per100(aPotassio)
        };

        decimal fattore = dati.UnitaConsumoG / 100m;
        var perUnita = Scala(per100, fattore);

        return new RisultatoNutrizionale
        {
            Per100g = per100,
            PerUnitaConsumo = perUnita,
            Per100gArrotondato = Arrotonda(per100),
            PerUnitaArrotondato = Arrotonda(perUnita),
            RiferimentiPer100g = Riferimenti(per100),
            RiferimentiPerUnita = Riferimenti(perUnita),
            MassaTotaleKg = massaTotKg,
            MassaCottaKg = massaCottaKg,
            Avvisi = Valida(ingredienti)
        };
    }

    private static Dichiarazione Scala(Dichiarazione d, decimal f) => new()
    {
        EnergiaKj = d.EnergiaKj * f,
        EnergiaKcal = d.EnergiaKcal * f,
        Grassi = d.Grassi * f,
        GrassiSaturi = d.GrassiSaturi * f,
        Carboidrati = d.Carboidrati * f,
        Zuccheri = d.Zuccheri * f,
        Proteine = d.Proteine * f,
        Sale = d.Sale * f,
        Fibre = d.Fibre * f,
        Acqua = d.Acqua * f,
        Sodio = d.Sodio * f,
        GrassiTrans = d.GrassiTrans * f,
        GrassiMonoinsaturi = d.GrassiMonoinsaturi * f,
        GrassiPolinsaturi = d.GrassiPolinsaturi * f,
        Colesterolo = d.Colesterolo * f,
        VitaminaD = d.VitaminaD * f,
        Calcio = d.Calcio * f,
        Ferro = d.Ferro * f,
        Potassio = d.Potassio * f
    };

    private static PercentualiRiferimento Riferimenti(Dichiarazione d) => new()
    {
        EnergiaKj = d.EnergiaKj / CostantiNutrizionali.RiEnergiaKj,
        EnergiaKcal = d.EnergiaKcal / CostantiNutrizionali.RiEnergiaKcal,
        Grassi = d.Grassi / CostantiNutrizionali.RiGrassiG,
        GrassiSaturi = d.GrassiSaturi / CostantiNutrizionali.RiGrassiSaturiG,
        Carboidrati = d.Carboidrati / CostantiNutrizionali.RiCarboidratiG,
        Zuccheri = d.Zuccheri / CostantiNutrizionali.RiZuccheriG,
        Proteine = d.Proteine / CostantiNutrizionali.RiProteineG,
        Sale = d.Sale / CostantiNutrizionali.RiSaleG
    };

    // ── Arrotondamento per l'etichetta ─────────────────────────────
    // Regole comuni (da confermare con la tabella Min. Salute 16/6/2016):
    //  energia → intero; nutrienti < 0,5 g → 0; ≤ 10 g → 1 decimale; > 10 g → intero;
    //  sale < 0,0125 g → 0; < 1 g → 2 decimali; altrimenti 1 decimale.
    private static Dichiarazione Arrotonda(Dichiarazione d) => new()
    {
        EnergiaKj = Math.Round(d.EnergiaKj, 0, MidpointRounding.AwayFromZero),
        EnergiaKcal = Math.Round(d.EnergiaKcal, 0, MidpointRounding.AwayFromZero),
        Grassi = ArrotondaNutriente(d.Grassi),
        GrassiSaturi = ArrotondaNutriente(d.GrassiSaturi),
        Carboidrati = ArrotondaNutriente(d.Carboidrati),
        Zuccheri = ArrotondaNutriente(d.Zuccheri),
        Proteine = ArrotondaNutriente(d.Proteine),
        Sale = ArrotondaSale(d.Sale),
        Fibre = ArrotondaNutriente(d.Fibre),
        Acqua = Math.Round(d.Acqua, 1, MidpointRounding.AwayFromZero)
    };

    private static decimal ArrotondaNutriente(decimal v)
    {
        // ≤ 10 g → un decimale; > 10 g → intero. Nessun azzeramento sotto 0,5 g:
        // i valori piccoli restano visibili (es. fibre 0,3), come nella prassi.
        if (v <= 10m) return Math.Round(v, 1, MidpointRounding.AwayFromZero);
        return Math.Round(v, 0, MidpointRounding.AwayFromZero);
    }

    private static decimal ArrotondaSale(decimal v)
    {
        if (v < 0.0125m) return 0m;
        if (v < 1m) return Math.Round(v, 2, MidpointRounding.AwayFromZero);
        return Math.Round(v, 1, MidpointRounding.AwayFromZero);
    }

    // ── Validazione dei dati di ingresso ───────────────────────────
    // La somma di grassi + carboidrati + proteine + sale + fibre + acqua per
    // 100 g dovrebbe avvicinarsi a 100 (zuccheri e saturi sono sottoinsiemi e
    // non si sommano). Scostamenti marcati segnalano un dato di fonte errato.
    private static IReadOnlyList<string> Valida(IReadOnlyList<IngredienteCalcolo> ingredienti)
    {
        var avvisi = new List<string>();
        foreach (var i in ingredienti)
        {
            var v = i.Valori;
            decimal somma = v.Grassi + v.Carboidrati + v.Proteine + v.Sale + v.Fibre + v.Acqua;
            if (somma > 0m && Math.Abs(somma - 100m) > 5m)
                avvisi.Add($"«{i.Denominazione}»: somma componenti {somma:0.#} g/100 g (attesa ~100).");
        }
        return avvisi;
    }
}
