using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Test del calcolatore canadese: %DV canadesi (grassi 75 g, saturi+trans 20 g
/// combinati, zuccheri 100 g, potassio 3400 mg) e voci senza %DV.
/// </summary>
public class EtichettaCanadaTest
{
    private static NutrizioneCanada Calcola() => new ServizioEtichettaCanada().Calcola(
        new Dichiarazione
        {
            EnergiaKcal = 200m, Grassi = 10m, GrassiSaturi = 3m, GrassiTrans = 0.2m,
            Colesterolo = 30m, Sodio = 400m, Carboidrati = 25m, Zuccheri = 12m,
            Proteine = 5m, Fibre = 2m, Calcio = 100m, Ferro = 2m, Potassio = 300m
        },
        porzioneG: 100m);

    [Fact]
    public void Dv_canadesi()
    {
        var f = Calcola();
        Assert.Equal(13, f.Grassi.Dv);       // 10 / 75
        Assert.Equal(16, f.SaturiTransDv);   // (3 + 0,2) / 20
        Assert.Equal(12, f.Zuccheri.Dv);     // 12 / 100
        Assert.Equal(17, f.Sodio.Dv);        // 400 / 2300
        Assert.Equal(7, f.Fibre.Dv);         // 2 / 28
        Assert.Equal(9, f.Potassio.Dv);      // 300 / 3400
        Assert.Equal(8, f.Calcio.Dv);        // 100 / 1300
    }

    [Fact]
    public void Voci_senza_dv_e_quantita()
    {
        var f = Calcola();
        Assert.Equal("30 mg", f.Colesterolo);   // colesterolo senza %DV
        Assert.Equal("25 g", f.Carboidrati);    // carboidrati senza %DV
        Assert.Equal("5 g", f.Proteine);
        Assert.Equal("3 g", f.GrassiSaturi);
        Assert.Equal("0 g", f.GrassiTrans);     // 0,2 < 0,5
    }
}
