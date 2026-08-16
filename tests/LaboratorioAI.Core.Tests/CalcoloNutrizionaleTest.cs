using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Test di regressione del motore nutrizionale sul caso reale «Torta Mimosa»
/// (foglio storico). Gli attesi sono i valori esatti pre-arrotondamento
/// verificati numericamente: il motore è corretto se li riproduce.
/// </summary>
public class CalcoloNutrizionaleTest
{
    private static IngredienteCalcolo Ing(string nome, decimal kg,
        decimal grassi, decimal saturi, decimal carbo, decimal zucc,
        decimal prot, decimal sale, decimal fibre, decimal acqua) =>
        new()
        {
            Denominazione = nome,
            QuantitaKg = kg,
            Valori = new ValoriPer100g
            {
                Grassi = grassi, GrassiSaturi = saturi, Carboidrati = carbo,
                Zuccheri = zucc, Proteine = prot, Sale = sale, Fibre = fibre, Acqua = acqua
            }
        };

    private static DatiCalcoloNutrizionale TortaMimosa() => new()
    {
        UnitaConsumoG = 100m,
        Ingredienti = new List<IngredienteCalcolo>
        {
            Ing("Farina 00",          0.180m, 0.7m, 0m,    77m,   1.7m,  11.5m, 0m,    2.2m, 8.6m),
            Ing("acqua",              0.575m, 0m,   0m,    0m,    0m,    0m,    0m,    0m,   100m),
            Ing("misto uovo",         0.340m, 10m,  3.6m,  0m,    0m,    11m,   0.3m,  0m,   78.7m),
            Ing("lievito baking",     0.010m, 0.2m, 0m,    42.1m, 0m,    0.1m,  40.8m, 0.5m, 16.3m),
            Ing("super jilk",         0.040m, 31m,  30m,   4.5m,  0m,    0m,    0m,    0m,   64.5m),
            Ing("whippak panna",      0.170m, 24.6m,24.3m, 13m,   11.4m, 0.6m,  0.18m, 0.3m, 61.4m),
            Ing("hello jolly cream",  0.110m, 2.9m, 1.7m,  89.2m, 64.6m, 2m,    1.18m, 0m,   4.72m),
            Ing("petit gocce",        0.030m, 2.4m, 30.9m, 55.9m, 53.9m, 3.7m,  0.26m, 0m,   37.74m),
            Ing("zucchero semolato",  0.310m, 0m,   0m,    100m,  100m,  0m,    0m,    0m,   0m),
            Ing("bagna liquore",      0.005m, 0m,   0m,    0m,    0m,    0m,    0m,    0m,   0m),
            Ing("zucchero velo",      0.010m, 0m,   0m,    100m,  97m,   0m,    0m,    0m,   0m),
        }
    };

    [Fact]
    public void Per100g_riproduce_i_valori_del_foglio()
    {
        var esito = new ServizioCalcoloNutrizionale().Calcola(TortaMimosa());
        var d = esito.Per100g;

        Assert.Equal(1.78m, esito.MassaTotaleKg, 3);
        Assert.Equal(5.2478m, d.Grassi, 3);
        Assert.Equal(4.3084m, d.GrassiSaturi, 3);
        Assert.Equal(33.7978m, d.Carboidrati, 3);
        Assert.Equal(24.1219m, d.Zuccheri, 3);
        Assert.Equal(3.5079m, d.Proteine, 3);
        Assert.Equal(0.3810m, d.Sale, 3);
        Assert.Equal(0.2539m, d.Fibre, 3);
        Assert.Equal(56.5384m, d.Acqua, 3);
    }

    [Fact]
    public void Energia_calcolata_sui_valori_esatti()
    {
        var esito = new ServizioCalcoloNutrizionale().Calcola(TortaMimosa());
        Assert.Equal(830.39m, esito.Per100g.EnergiaKj, 2);
        Assert.Equal(196.96m, esito.Per100g.EnergiaKcal, 2);
    }

    [Fact]
    public void Senza_calo_peso_unita_100g_coincide_con_per100g()
    {
        var esito = new ServizioCalcoloNutrizionale().Calcola(TortaMimosa());
        Assert.Equal(esito.Per100g.EnergiaKcal, esito.PerUnitaConsumo.EnergiaKcal, 6);
        Assert.Equal(1.78m, esito.MassaCottaKg, 3);
    }

    [Fact]
    public void Calo_peso_concentra_i_valori_e_riduce_acqua()
    {
        var dati = TortaMimosa() with { CaloPesoPercentuale = 10m };
        var esito = new ServizioCalcoloNutrizionale().Calcola(dati);

        // Con 10% di calo la massa cotta è il 90% e i nutrienti (non acqua) si concentrano.
        Assert.Equal(1.602m, esito.MassaCottaKg, 3);
        Assert.True(esito.Per100g.Grassi > 5.2478m);
        Assert.True(esito.Per100g.Acqua < 56.5384m);
    }
}
