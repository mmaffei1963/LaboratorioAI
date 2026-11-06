using LaboratorioAI.Core.Repository;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>28/10/2026: confronto prezzi commesse ArcLab ↔ listini Marea.</summary>
public class ConfrontoPrezziMareaTest
{
    private static CommessaPrezzoArcLab C(int id, int cli, int ins, int ctrl, decimal p) => new(id, cli, "Cliente " + cli, ins, "Ins " + ins, ctrl, "Ctrl " + ctrl, p, 9000 + id);
    private static ListinoMarea L(int cli, int ctrl, decimal p) => new(cli, "Cliente " + cli, ctrl, "Ctrl " + ctrl, p, new DateTime(2026, 10, 27));

    [Fact]
    public void PrezziUguali_NessunaSegnalazione() =>
        Assert.Empty(ConfrontoPrezziMarea.Confronta([C(1, 10, 100, 3394, 33m)], [L(10, 3394, 33m)]));

    [Fact]
    public void PrezzoDiverso_ConDifferenza()
    {
        var r = Assert.Single(ConfrontoPrezziMarea.Confronta([C(1, 10, 100, 3394, 35m)], [L(10, 3394, 33m)]));
        Assert.Equal(ConfrontoPrezziMarea.PrezzoDiverso, r.Caso);
        Assert.Equal(2m, r.Differenza);
    }

    [Fact]
    public void MancaInMareaESoloInMarea()
    {
        var r = ConfrontoPrezziMarea.Confronta([C(1, 10, 100, 3394, 33m)], [L(11, 180, 15.2m)]);
        Assert.Contains(r, x => x.Caso == ConfrontoPrezziMarea.MancaInMarea && x.IdCliente == 10);
        Assert.Contains(r, x => x.Caso == ConfrontoPrezziMarea.SoloInMarea && x.IdCliente == 11);
    }

    [Fact]
    public void PrezziDiversiTraInsediamenti()
    {
        var r = ConfrontoPrezziMarea.Confronta([C(1, 10, 100, 3394, 33m), C(2, 10, 101, 3394, 30m)], [L(10, 3394, 33m)]);
        Assert.Equal(2, r.Count);
        Assert.All(r, x => Assert.Contains(ConfrontoPrezziMarea.DiversiTraInsediamenti, x.Caso));
        Assert.Contains(r, x => x.Caso.Contains(ConfrontoPrezziMarea.PrezzoDiverso) && x.PrezzoArcLab == 30m);
    }

    [Fact]
    public void AbbinamentoPerContratto()
    {
        // stesso cliente e controllo su due insediamenti: ogni listino va alla commessa del suo contratto
        var c1 = new CommessaPrezzoArcLab(1, 10, "C", 100, "I1", 3394, "Leg", 33.5m, 5646);
        var c2 = new CommessaPrezzoArcLab(2, 10, "C", 101, "I2", 3394, "Leg", 30m, 5700);
        var r = ConfrontoPrezziMarea.Confronta([c1, c2],
            [new ListinoMarea(10, "C", 3394, "Leg", 33m, null, 5646), new ListinoMarea(10, "C", 3394, "Leg", 30m, null, 5700)]);
        var d1 = Assert.Single(r, x => x.IdCommessa == 1);
        Assert.Contains(ConfrontoPrezziMarea.PrezzoDiverso, d1.Caso);
        Assert.Equal(0.5m, d1.Differenza);
        Assert.DoesNotContain(ConfrontoPrezziMarea.PrezzoDiverso, Assert.Single(r, x => x.IdCommessa == 2).Caso);
        Assert.DoesNotContain(r, x => x.Caso == ConfrontoPrezziMarea.SoloInMarea);
    }

    [Fact]
    public void SenzaContratto_NonContaComeMancante() =>
        Assert.Empty(ConfrontoPrezziMarea.Confronta([new CommessaPrezzoArcLab(1, 10, "C", 100, "I", 3394, "Leg", 33m)], []));
}
