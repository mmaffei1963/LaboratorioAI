using LaboratorioAI.Tracciabilita.Core.Motori;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class RicercaLottiPerDataTests
{
    private static LottoImpegnato L(int id, int cat, int forn, string nomeForn, string d) =>
        new(id, cat, forn, nomeForn, "L" + id, "Farina", DateOnly.Parse(d), null);

    [Fact]
    public void PrendeGliUltimiNPerFornitoreEIgnoraIlFuturo()
    {
        var c = new[]
        {
            L(1, 2, 10, "Verrini", "2026-10-01"), L(2, 2, 10, "Verrini", "2026-10-10"), L(3, 2, 10, "Verrini", "2026-10-20"),
            L(4, 2, 10, "Verrini", "2026-10-24"),
            L(5, 3, 11, "Naldoni", "2026-10-15"),
        };
        var r = RicercaLottiPerData.Cerca(c, new[] { 2, 3 }, new DateOnly(2026, 10, 22), 2);
        Assert.Equal(2, r.Count);
        Assert.Equal("Naldoni", r.Last().Fornitore);
        var ver = r.Single(x => x.IdFornitore == 10);
        Assert.Equal(new[] { 3, 2 }, ver.Lotti.Select(x => x.IdLotto));
    }

    [Fact]
    public void IFornitoriInattiviDa90GiorniSiEscludonoDaSoli()
    {
        var c = new[] { L(1, 2, 10, "Verrini", "2026-10-01"), L(2, 2, 12, "Padano", "2025-06-01") };
        var r = RicercaLottiPerData.Cerca(c, new[] { 2 }, new DateOnly(2026, 10, 22), 3);
        Assert.Single(r);
        var scartati = RicercaLottiPerData.FornitoriScartati(c, new[] { 2 }, new DateOnly(2026, 10, 22));
        Assert.Equal("Padano", scartati.Single().Fornitore);
    }

    [Fact]
    public void LaDataDiUnGiornoPassatoRiapreIFornitoriDiAllora()
    {
        var c = new[] { L(2, 2, 12, "Padano", "2025-06-01") };
        Assert.Single(RicercaLottiPerData.Cerca(c, new[] { 2 }, new DateOnly(2025, 6, 20), 3));
    }

    [Fact]
    public void EsclusioneManualeEAltraCategoriaNonEntrano()
    {
        var c = new[] { L(1, 2, 10, "Verrini", "2026-10-01"), L(2, 7, 11, "Strutto srl", "2026-10-02") };
        var r = RicercaLottiPerData.Cerca(c, new[] { 2 }, new DateOnly(2026, 10, 22), 3, fornitoriEsclusi: new[] { 10 });
        Assert.Empty(r);
    }

    [Fact]
    public void LoStessoLottoInPiuMatriciCompareUnaVolta()
    {
        var c = new[] { L(1, 2, 10, "Verrini", "2026-10-01"), L(1, 3, 10, "Verrini", "2026-10-02") };
        var r = RicercaLottiPerData.Cerca(c, new[] { 2, 3 }, new DateOnly(2026, 10, 22), 3);
        Assert.Single(r.Single().Lotti);
    }

    [Fact]
    public void UnIngredientePocoUsatoRestaVisibileAncheSeVecchio()
    {
        var c = new[] { L(1, 5, 20, "Pegaso", "2024-04-03"), L(2, 5, 20, "Pegaso", "2023-07-04") };
        var r = RicercaLottiPerData.Cerca(c, new[] { 5 }, new DateOnly(2026, 9, 10), 2);
        Assert.Equal(2, r.Single().Lotti.Count);
    }
}
