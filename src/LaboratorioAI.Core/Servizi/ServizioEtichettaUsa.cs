using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Calcola i valori della Nutrition Facts label statunitense (21 CFR 101.9):
/// porzione, quantità arrotondate secondo le regole FDA e %DV (Daily Values 2016).
/// Deterministico: gli arrotondamenti sono legge e stanno tutti qui.
/// </summary>
public interface IServizioEtichettaUsa
{
    NutrizioneUsa Calcola(Dichiarazione per100g, decimal porzioneG, decimal zuccheriAggiuntiPer100g);
}

/// <inheritdoc />
public class ServizioEtichettaUsa : IServizioEtichettaUsa
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public NutrizioneUsa Calcola(Dichiarazione d, decimal porzioneG, decimal zuccheriAggiuntiPer100g)
    {
        if (porzioneG <= 0m) porzioneG = 100m;
        decimal f = porzioneG / 100m;

        // Valori per porzione (esatti).
        decimal cal = d.EnergiaKcal * f;
        decimal grassi = d.Grassi * f, saturi = d.GrassiSaturi * f, trans = d.GrassiTrans * f;
        decimal colest = d.Colesterolo * f, sodio = d.Sodio * f;
        decimal carbo = d.Carboidrati * f, fibre = d.Fibre * f;
        decimal zuccTot = d.Zuccheri * f, zuccAgg = zuccheriAggiuntiPer100g * f;
        decimal prot = d.Proteine * f;
        decimal vitD = d.VitaminaD * f, calcio = d.Calcio * f, ferro = d.Ferro * f, potassio = d.Potassio * f;

        var avvisi = new List<string>();
        if (vitD == 0m && calcio == 0m && ferro == 0m && potassio == 0m)
            avvisi.Add("Micronutrienti (vitamina D, calcio, ferro, potassio) assenti negli ingredienti: valori a 0, da verificare.");

        return new NutrizioneUsa
        {
            PorzioneG = porzioneG,
            Calorie = Calorie(cal),
            Grassi = new RigaUsa { Quantita = GrassoG(grassi), Dv = Dv(grassi, CostantiUsa.DvGrassiG) },
            GrassiSaturi = new RigaUsa { Quantita = GrassoG(saturi), Dv = Dv(saturi, CostantiUsa.DvSaturiG) },
            GrassiTrans = GrassoG(trans),
            Colesterolo = new RigaUsa { Quantita = ColesteroloMg(colest), Dv = Dv(colest, CostantiUsa.DvColesteroloMg) },
            Sodio = new RigaUsa { Quantita = SodioMg(sodio), Dv = Dv(sodio, CostantiUsa.DvSodioMg) },
            Carboidrati = new RigaUsa { Quantita = MacroG(carbo), Dv = Dv(carbo, CostantiUsa.DvCarboidratiG) },
            Fibre = new RigaUsa { Quantita = MacroG(fibre), Dv = Dv(fibre, CostantiUsa.DvFibreG) },
            ZuccheriTotali = MacroG(zuccTot),
            ZuccheriAggiunti = new RigaUsa { Quantita = MacroG(zuccAgg), Dv = Dv(zuccAgg, CostantiUsa.DvZuccheriAggiuntiG) },
            Proteine = MacroG(prot),
            VitaminaD = new RigaUsa { Quantita = Num(ArrA(vitD, 0.1m)) + " mcg", Dv = Dv(vitD, CostantiUsa.DvVitaminaDUg) },
            Calcio = new RigaUsa { Quantita = Num(ArrA(calcio, 10m)) + " mg", Dv = Dv(calcio, CostantiUsa.DvCalcioMg) },
            Ferro = new RigaUsa { Quantita = Num(ArrA(ferro, 0.1m)) + " mg", Dv = Dv(ferro, CostantiUsa.DvFerroMg) },
            Potassio = new RigaUsa { Quantita = Num(ArrA(potassio, 10m)) + " mg", Dv = Dv(potassio, CostantiUsa.DvPotassioMg) },
            Avvisi = avvisi
        };
    }

    // ── %DV (dai valori esatti, arrotondata all'intero) ────────────
    private static int Dv(decimal quantita, decimal valoreGiornaliero) =>
        (int)Math.Round(quantita / valoreGiornaliero * 100m, 0, MidpointRounding.AwayFromZero);

    // ── Arrotondamenti FDA (21 CFR 101.9(c)) ───────────────────────
    private static string Calorie(decimal x)
    {
        if (x < 5m) return "0";
        return x <= 50m ? Num(ArrA(x, 5m)) : Num(ArrA(x, 10m));
    }

    // Grassi (totali, saturi, trans): <0,5 → 0; ≤5 → 0,5 g; >5 → 1 g.
    private static string GrassoG(decimal x)
    {
        if (x < 0.5m) return "0 g";
        return (x <= 5m ? Num(ArrA(x, 0.5m)) : Num(ArrA(x, 1m))) + " g";
    }

    // Carboidrati, fibre, zuccheri, proteine: <0,5 → 0; <1 → «meno di 1 g»; >1 → 1 g.
    private static string MacroG(decimal x)
    {
        if (x < 0.5m) return "0 g";
        if (x < 1m) return "meno di 1 g";
        return Num(ArrA(x, 1m)) + " g";
    }

    private static string ColesteroloMg(decimal x)
    {
        if (x < 2m) return "0 mg";
        if (x <= 5m) return "meno di 5 mg";
        return Num(ArrA(x, 5m)) + " mg";
    }

    private static string SodioMg(decimal x)
    {
        if (x < 5m) return "0 mg";
        return (x <= 140m ? Num(ArrA(x, 5m)) : Num(ArrA(x, 10m))) + " mg";
    }

    private static decimal ArrA(decimal x, decimal incremento) =>
        Math.Round(x / incremento, 0, MidpointRounding.AwayFromZero) * incremento;

    private static string Num(decimal v) =>
        v == Math.Truncate(v) ? ((long)v).ToString(Inv) : v.ToString("0.#", Inv);
}
