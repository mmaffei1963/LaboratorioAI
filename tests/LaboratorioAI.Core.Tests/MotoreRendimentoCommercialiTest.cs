using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Rendimento dei commerciali (fase 3, 13/10/2026).</summary>
public class MotoreRendimentoCommercialiTest
{
    private static readonly (string, string?, bool)[] Voci =
    [
        ("ZAT", "Zatterin Ludovico", false), ("CAR", "Carloni Andrea", false), ("DIP", "Dipendenti", false), ("LAB", "Analisi Andrea", true)
    ];

    [Fact]
    public void VoceDiCosto_DalNome_InQualsiasiOrdine()
    {
        Assert.Equal("ZAT", MotoreRendimentoCommerciali.TrovaVoceCosto("Ludovico Zatterin", Voci)!.Value.Codice);
        Assert.Equal("CAR", MotoreRendimentoCommerciali.TrovaVoceCosto("CARLONI Andrea", Voci)!.Value.Codice);
        Assert.Null(MotoreRendimentoCommerciali.TrovaVoceCosto("Mario Rossi", Voci));
    }

    [Fact]
    public void MargineMedio_PesatoSulCosto()
    {
        var pesi = MotoreRendimentoCommerciali.Pesi([("05", -18_000m), ("FOR", -6_000m)]);
        Assert.Equal(0.75m, pesi.First(p => p.Centro == "05").Peso);
        var m = MotoreRendimentoCommerciali.MargineMedio(pesi, new Dictionary<string, decimal?> { ["05"] = 0.60m, ["FOR"] = 0.40m });
        Assert.Equal(0.55m, m);   // 0,75 × 60% + 0,25 × 40%
    }

    [Fact]
    public void Contributo_IncidenzaECumulato()
    {
        var a1 = new RendimentoAnno(2025, 30_000m, 2_000m, 40_000m, 10_000m, 12, 200_000m, 80, 10, 5, 0.5m, 100_000m, 90_000m);
        var a2 = new RendimentoAnno(2026, 30_000m, 4_000m, 100_000m, 30_000m, 20, 250_000m, 90, 15, 4, 0.5m, 100_000m, 70_000m);
        Assert.Equal(32_000m, a1.CostoTotale);
        Assert.Equal(0.8m, a1.IncidenzaSuPortato);
        Assert.Equal(20_000m, a1.MarginePortato);
        Assert.Equal(-12_000m, a1.Contributo);
        Assert.Equal(0.9m, a1.PercentualeIncassata);
        var cum = MotoreRendimentoCommerciali.Cumulato([a2, a1]);
        Assert.Equal(-12_000m, cum[0].Cumulato);
        Assert.Equal(4_000m, cum[1].Cumulato);   // −12.000 + (50.000 − 34.000)
    }

    [Fact]
    public void SenzaFatturato_RapportiNonDisponibili()
    {
        var a = new RendimentoAnno(2024, 10_000m, 0m, 0m, 0m, 0, 0m, 0, 0, 0, null, 0m, 0m);
        Assert.Null(a.IncidenzaSuPortato);
        Assert.Null(a.Contributo);
        Assert.Null(a.PercentualeIncassata);
    }
}
