using System.Globalization;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Calcola i valori del Tableau de la valeur nutritive / Nutrition Facts table
/// canadese (Health Canada, FDR): porzione, quantità arrotondate e %DV canadesi,
/// con la %DV combinata di saturi + trans. Deterministico.
/// </summary>
public interface IServizioEtichettaCanada
{
    NutrizioneCanada Calcola(Dichiarazione per100g, decimal porzioneG);
}

/// <inheritdoc />
public class ServizioEtichettaCanada : IServizioEtichettaCanada
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public NutrizioneCanada Calcola(Dichiarazione d, decimal porzioneG)
    {
        if (porzioneG <= 0m) porzioneG = 100m;
        decimal f = porzioneG / 100m;

        decimal cal = d.EnergiaKcal * f;
        decimal grassi = d.Grassi * f, saturi = d.GrassiSaturi * f, trans = d.GrassiTrans * f;
        decimal colest = d.Colesterolo * f, sodio = d.Sodio * f;
        decimal carbo = d.Carboidrati * f, fibre = d.Fibre * f, zucc = d.Zuccheri * f, prot = d.Proteine * f;
        decimal potassio = d.Potassio * f, calcio = d.Calcio * f, ferro = d.Ferro * f;

        var avvisi = new List<string>();
        if (calcio == 0m && ferro == 0m && potassio == 0m)
            avvisi.Add("Micronutrienti (potassio, calcio, ferro) assenti negli ingredienti: valori a 0, da verificare.");

        return new NutrizioneCanada
        {
            PorzioneG = porzioneG,
            Calorie = Calorie(cal),
            Grassi = new RigaUsa { Quantita = GrassoG(grassi), Dv = Dv(grassi, CostantiCanada.DvGrassiG) },
            GrassiSaturi = GrassoG(saturi),
            GrassiTrans = GrassoG(trans),
            SaturiTransDv = Dv(saturi + trans, CostantiCanada.DvSaturiTransG),
            Colesterolo = ColesteroloMg(colest),
            Sodio = new RigaUsa { Quantita = SodioMg(sodio), Dv = Dv(sodio, CostantiCanada.DvSodioMg) },
            Carboidrati = MacroG(carbo),
            Fibre = new RigaUsa { Quantita = MacroG(fibre), Dv = Dv(fibre, CostantiCanada.DvFibreG) },
            Zuccheri = new RigaUsa { Quantita = MacroG(zucc), Dv = Dv(zucc, CostantiCanada.DvZuccheriG) },
            Proteine = MacroG(prot),
            Potassio = new RigaUsa { Quantita = Num(ArrA(potassio, 10m)) + " mg", Dv = Dv(potassio, CostantiCanada.DvPotassioMg) },
            Calcio = new RigaUsa { Quantita = Num(ArrA(calcio, 10m)) + " mg", Dv = Dv(calcio, CostantiCanada.DvCalcioMg) },
            Ferro = new RigaUsa { Quantita = Num(ArrA(ferro, 0.1m)) + " mg", Dv = Dv(ferro, CostantiCanada.DvFerroMg) },
            Avvisi = avvisi
        };
    }

    private static int Dv(decimal q, decimal dv) =>
        (int)Math.Round(q / dv * 100m, 0, MidpointRounding.AwayFromZero);

    private static string Calorie(decimal x) =>
        x < 5m ? "0" : (x <= 50m ? Num(ArrA(x, 5m)) : Num(ArrA(x, 10m)));

    private static string GrassoG(decimal x)
    {
        if (x < 0.5m) return "0 g";
        return (x <= 5m ? Num(ArrA(x, 0.5m)) : Num(ArrA(x, 1m))) + " g";
    }

    private static string MacroG(decimal x)
    {
        if (x < 0.5m) return "0 g";
        if (x < 1m) return "< 1 g";
        return Num(ArrA(x, 1m)) + " g";
    }

    private static string ColesteroloMg(decimal x) =>
        x < 2m ? "0 mg" : Num(ArrA(x, 5m)) + " mg";

    private static string SodioMg(decimal x)
    {
        if (x < 5m) return "0 mg";
        return (x <= 140m ? Num(ArrA(x, 5m)) : Num(ArrA(x, 10m))) + " mg";
    }

    private static decimal ArrA(decimal x, decimal incr) =>
        Math.Round(x / incr, 0, MidpointRounding.AwayFromZero) * incr;

    private static string Num(decimal v) =>
        v == Math.Truncate(v) ? ((long)v).ToString(Inv) : v.ToString("0.#", Inv);
}
