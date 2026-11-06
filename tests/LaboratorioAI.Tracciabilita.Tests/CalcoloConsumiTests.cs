using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class CalcoloConsumiTests
{
    private static IngredienteDaConsumare Ing(int riga, string nome, decimal? qt, params int[] lotti) => new(riga, nome, qt, lotti);
    private static readonly Dictionary<int, decimal?> Nessuno = new();

    [Fact]
    public void SoloLottiNonCalcolaNulla()
    {
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.SoloLotti, true, 100, 100, new[] { Ing(1, "Farina", 60, 10) }, new Dictionary<int, decimal?> { [10] = 5 });
        Assert.Null(r.KgPerLotto[10]);
        Assert.Empty(r.Errori);
    }

    [Fact]
    public void ManualeUsaSoloIKgScritti()
    {
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Manuale, false, 100, 100, new[] { Ing(1, "Farina", 60, 10, 11) }, new Dictionary<int, decimal?> { [10] = 40.5m });
        Assert.Equal(40.5m, r.KgPerLotto[10]);
        Assert.Null(r.KgPerLotto[11]);
        Assert.Empty(r.Errori);
    }

    [Fact]
    public void ManualeObbligatorioSegnalaIlMancante()
    {
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Manuale, true, null, null, new[] { Ing(1, "Sale", null, 20) }, Nessuno);
        Assert.Single(r.Errori);
        Assert.Contains("Sale", r.Errori[0]);
    }

    [Fact]
    public void TeoricaScalaLaRicettaSullaQuantitaProdotta()
    {
        // 60 kg di farina ogni 100 kg di prodotto; si producono 250 kg → 150 kg.
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Teorica, true, 250, 100, new[] { Ing(1, "Farina", 60, 10) }, Nessuno);
        Assert.Equal(150m, r.KgPerLotto[10]);
        Assert.True(r.CalcolatiDallaRicetta);
        Assert.Empty(r.Errori);
    }

    [Fact]
    public void TeoricaDivideInPartiUgualiEIlTotaleTorna()
    {
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Teorica, false, 100, 100, new[] { Ing(1, "Farina", 100, 1, 2, 3) }, Nessuno);
        Assert.Equal(100m, r.KgPerLotto.Values.Sum(v => v ?? 0m));
        Assert.Equal(33.33m, r.KgPerLotto[1]);
        Assert.Equal(33.34m, r.KgPerLotto[3]);
    }

    [Fact]
    public void TeoricaRispettaIKgCorrettiADitoEDividePerIlResto()
    {
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Teorica, false, 100, 100, new[] { Ing(1, "Farina", 100, 1, 2) },
            new Dictionary<int, decimal?> { [1] = 70 });
        Assert.Equal(70m, r.KgPerLotto[1]);
        Assert.Equal(30m, r.KgPerLotto[2]);
    }

    [Fact]
    public void TeoricaSenzaDatiDellaRicettaSegnalaSeObbligatoria()
    {
        var r = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Teorica, true, 100, null, new[] { Ing(1, "Farina", 60, 10) }, Nessuno);
        Assert.Single(r.Errori);
        Assert.Contains("riferimento", r.Errori[0]);
        var senza = CalcoloConsumi.Calcola(ModoQuantitaConsumi.Teorica, false, 100, null, new[] { Ing(1, "Farina", 60, 10) }, Nessuno);
        Assert.Empty(senza.Errori);
        Assert.Null(senza.KgPerLotto[10]);
    }
}
