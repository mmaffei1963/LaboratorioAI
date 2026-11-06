using LaboratorioAI.Core.Repository;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>28/10/2026: differenze tra commesse ArcLab e database Marea (oltre ai prezzi).</summary>
public class ConfrontoCommesseMareaTest
{
    private static CommessaArcLab A(int id, int cli, int ins, int ctrl) => new(id, cli, "Cliente " + cli, ins, "Ins " + ins, ctrl, "Acque legionella spp (metodo ISO)",
        5646, "   Acque legionella spp (metodo ISO)");
    private static CommessaMarea M(int id, int cli, int ins, string desc, string? par = null) => new(id, desc, cli, ins, par);
    private static readonly List<NomeControlloArcLab> Nomi = [new(3394, "Acque legionella spp (metodo ISO)"), new(180, "Superfici C Slide (CBT)")];
    private static readonly List<ParametroControlloArcLab> Par = [new(3394, "Legionella spp", "Legionella spp")];

    [Fact]
    public void TuttoAllineato_NessunaSegnalazione() =>
        Assert.Empty(ConfrontoCommesseMarea.Confronta([A(1, 10, 100, 3394)], Par, Nomi,
            [M(9, 10, 100, "3394 - Acque legionella spp (metodo ISO)", "Legionella spp")],
            [new ListinoMarea(10, "Cliente 10", 3394, "   Acque legionella spp (metodo ISO)", 33m, null)]));

    [Fact]
    public void DescrizioneDiversaDaQuellaDellaCommessa()
    {
        // stessa matrice tra parentesi: uguale; regolamento diverso: segnalato
        var a = new CommessaArcLab(1, 10, "C", 100, "I", 2626, "Alimenti Nutrizionale", 5650, " Alimenti Nutrizionale Reg UE 1169/2011 (Alimenti)");
        var ok = ConfrontoCommesseMarea.Confronta([a], [], [new(2626, "Alimenti Nutrizionale")], [],
            [new ListinoMarea(10, "C", 2626, "Alimenti Nutrizionale Reg UE 1169/2011  (Alimenti)", 236m, null, 5650)]);
        Assert.DoesNotContain(ok, x => x.Caso == ConfrontoCommesseMarea.NomeDiverso);
        var r = ConfrontoCommesseMarea.Confronta([a], [], [new(2626, "Alimenti Nutrizionale")], [],
            [new ListinoMarea(10, "C", 2626, "Alimenti Nutrizionale Reg UE 1196/2011 (Alimenti)", 230m, null, 5650)]);
        Assert.Contains(r, x => x.Caso == ConfrontoCommesseMarea.NomeDiverso && x.IdCommessaArcLab == 1);
    }

    [Fact]
    public void MancanteNonAttivaEInsediamentoDiverso()
    {
        var r = ConfrontoCommesseMarea.Confronta([A(1, 10, 100, 3394), A(2, 11, 200, 3394)], [], Nomi,
            [M(9, 11, 201, "3394 - Acque legionella"), M(8, 12, 300, "180 - superfici C Slide (CBT)"), M(7, 12, 300, "2495 - ")], []);
        Assert.Contains(r, x => x.Caso == ConfrontoCommesseMarea.MancaInMarea && x.IdCommessaArcLab == 1);
        Assert.Contains(r, x => x.Caso == ConfrontoCommesseMarea.InsediamentoDiverso && x.IdCommessaArcLab == 2);
        Assert.Contains(r, x => x.Caso == ConfrontoCommesseMarea.NonAttivaInArcLab && x.IdCommessaMarea == 8);
        Assert.DoesNotContain(r, x => x.Caso == ConfrontoCommesseMarea.ControlloNonRiconoscibile);
    }

    [Fact]
    public void ControlloNonRiconoscibile() =>
        Assert.Equal(ConfrontoCommesseMarea.ControlloNonRiconoscibile,
            Assert.Single(ConfrontoCommesseMarea.Confronta([], [], Nomi, [M(7, 12, 300, " - senza id")], [])).Caso);

    [Fact]
    public void ParametriDiversi_EUnitaCampionarieIgnorate()
    {
        var par = new List<ParametroControlloArcLab> { new(2850, "Escherichia coli beta-glucoronidasi positiva", "E. coli"), new(2850, "Salmonella spp", "Salmonella") };
        var a = new CommessaArcLab(1, 10, "C", 100, "I", 2850, "Alimenti");
        var ok = M(9, 10, 100, "2850 - Alimenti", "Escherichia coli beta-glucoronidasi positiva Unità campionaria 01<br/>Escherichia coli beta-glucoronidasi positiva Unità campionaria 02<br/>Salmonella spp");
        Assert.Empty(ConfrontoCommesseMarea.Confronta([a], par, [new(2850, "Alimenti")], [ok], []));
        var diversa = M(9, 10, 100, "2850 - Alimenti", "Escherichia coli beta-glucoronidasi positiva<br/>Listeria monocytogenes");
        var r = Assert.Single(ConfrontoCommesseMarea.Confronta([a], par, [new(2850, "Alimenti")], [diversa], []));
        Assert.Equal(ConfrontoCommesseMarea.ParametriDiversi, r.Caso);
        Assert.Contains("Salmonella spp", r.Dettaglio);
        Assert.Contains("Listeria monocytogenes", r.Dettaglio);
    }

    [Fact]
    public void ClienteAttivoNelPortaleMaNonInArcLab()
    {
        var r = Assert.Single(ConfrontoCommesseMarea.Confronta([], [], Nomi, [], [],
            [new ClienteMareaNonAttivo(5, "Acquafarina", "C123", 777, "ACQUAFARINA SRL", "non attivo in ArcLab", 4, 2, null)]));
        Assert.Equal(ConfrontoCommesseMarea.ClienteNonAttivo, r.Caso);
        Assert.Contains("mai entrato nel portale", r.Dettaglio);
    }
}
