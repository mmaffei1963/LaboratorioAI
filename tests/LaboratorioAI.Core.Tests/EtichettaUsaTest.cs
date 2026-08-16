using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Test degli arrotondamenti FDA (21 CFR 101.9) e delle %DV 2016 della
/// Nutrition Facts label. La porzione di 100 g rende i valori per porzione
/// uguali a quelli per 100 g, così gli attesi sono immediati.
/// </summary>
public class EtichettaUsaTest
{
    private static NutrizioneUsa Calcola() => new ServizioEtichettaUsa().Calcola(
        new Dichiarazione
        {
            EnergiaKcal = 200m, Grassi = 10m, GrassiSaturi = 3m, GrassiTrans = 0.2m,
            Colesterolo = 30m, Sodio = 400m, Carboidrati = 25m, Zuccheri = 12m,
            Proteine = 5m, Fibre = 2m, VitaminaD = 1m, Calcio = 100m, Ferro = 2m, Potassio = 300m
        },
        porzioneG: 100m, zuccheriAggiuntiPer100g: 5m);

    [Fact]
    public void Quantita_arrotondate_secondo_FDA()
    {
        var f = Calcola();
        Assert.Equal("200", f.Calorie);
        Assert.Equal("10 g", f.Grassi.Quantita);
        Assert.Equal("3 g", f.GrassiSaturi.Quantita);
        Assert.Equal("0 g", f.GrassiTrans);            // 0,2 < 0,5
        Assert.Equal("30 mg", f.Colesterolo.Quantita);
        Assert.Equal("400 mg", f.Sodio.Quantita);
        Assert.Equal("25 g", f.Carboidrati.Quantita);
        Assert.Equal("2 g", f.Fibre.Quantita);
        Assert.Equal("12 g", f.ZuccheriTotali);
        Assert.Equal("5 g", f.ZuccheriAggiunti.Quantita);
        Assert.Equal("5 g", f.Proteine);
    }

    [Fact]
    public void PercentualiDv_calcolate_sui_valori_esatti()
    {
        var f = Calcola();
        Assert.Equal(13, f.Grassi.Dv);        // 10 / 78
        Assert.Equal(15, f.GrassiSaturi.Dv);  // 3 / 20
        Assert.Equal(10, f.Colesterolo.Dv);   // 30 / 300
        Assert.Equal(17, f.Sodio.Dv);         // 400 / 2300
        Assert.Equal(9, f.Carboidrati.Dv);    // 25 / 275
        Assert.Equal(7, f.Fibre.Dv);          // 2 / 28
        Assert.Equal(10, f.ZuccheriAggiunti.Dv); // 5 / 50
        Assert.Equal(8, f.Calcio.Dv);         // 100 / 1300
    }

    [Fact]
    public void Regole_speciali_soglie_basse()
    {
        var f = new ServizioEtichettaUsa().Calcola(
            new Dichiarazione { EnergiaKcal = 3m, Sodio = 3m, Carboidrati = 0.7m, Colesterolo = 4m },
            porzioneG: 100m, zuccheriAggiuntiPer100g: 0m);
        Assert.Equal("0", f.Calorie);                 // < 5 kcal
        Assert.Equal("0 mg", f.Sodio.Quantita);       // < 5 mg
        Assert.Equal("meno di 1 g", f.Carboidrati.Quantita); // 0,5–1 g
        Assert.Equal("meno di 5 mg", f.Colesterolo.Quantita); // 2–5 mg
    }
}
