using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Consuntivo per area (20/10/2026): ripartizione dell'amministrazione, FON in formazione, anno prima.</summary>
public class MotoreConsuntivoTest
{
    private static readonly Dictionary<string, (string Descrizione, bool Ricavo)> Voci = new()
    {
        ["ANA"] = ("analisi di laboratorio", true),
        ["SPG"] = ("Spese generali di gestione", false),
        ["FHO"] = ("Formazione Haccp on-line", true),
    };
    private static readonly Dictionary<string, string?> Centri = new() { ["ZZZ"] = "Centro nuovo" };
    private static readonly DateTime Dal = new(2026, 1, 1), Al = new(2026, 9, 30);

    [Fact]
    public void AmministrazioneDivisaInPartiUgualiSulleAltreCinqueAree()
    {
        var righe = new List<RigaConsuntivo>
        {
            new(new(2026, 3, 1), "03", "SPG", -1000m),
            new(new(2026, 3, 1), "04", "ANA", 5000m),
        };
        var r = MotoreConsuntivo.Calcola(righe, Dal, Al, Voci, Centri);
        Assert.Equal(-200m, r.Totali.Contributo["04"]);
        Assert.Equal(-200m, r.Totali.Contributo["PRY"]);
        Assert.Equal(0m, r.Totali.UtileConContributo["03"]);
        Assert.Equal(4800m, r.Totali.UtileConContributo["04"]);
        Assert.Equal(4000m, r.Totali.UtileMarea);
        Assert.Equal(r.Totali.UtileMarea, r.Totali.UtileConContributo.Values.Sum());
    }

    [Fact]
    public void FormazioneAccreditataSommataAFormazione()
    {
        var righe = new List<RigaConsuntivo>
        {
            new(new(2026, 2, 1), "FON", "FHO", 300m),
            new(new(2026, 2, 1), "FOR", "FHO", 700m),
        };
        var r = MotoreConsuntivo.Calcola(righe, Dal, Al, Voci, Centri);
        Assert.Equal(1000m, r.Totali.Ricavi["FOR"]);
        Assert.DoesNotContain(r.Aree, a => a.Codice == "FON");
    }

    [Fact]
    public void AnnoPrimaFuoriDalPeriodoCorrenteEPeriodoADate()
    {
        var righe = new List<RigaConsuntivo>
        {
            new(new(2025, 9, 30), "04", "ANA", 100m),   // anno prima, ultimo giorno
            new(new(2025, 10, 1), "04", "ANA", 999m),   // fuori da entrambi
            new(new(2026, 9, 30), "04", "ANA", 50m),    // corrente, ultimo giorno
            new(new(2026, 10, 1), "04", "ANA", 777m),   // fuori
        };
        var r = MotoreConsuntivo.Calcola(righe, Dal, Al, Voci, Centri);
        Assert.Equal(50m, r.Totali.Ricavi["04"]);
        Assert.Equal(100m, r.TotaliPrecedenti.Ricavi["04"]);
        Assert.Equal(9, r.Mesi.Count);
        Assert.Equal(50m, r.Mesi[8].Ricavi["04"]);
    }

    [Fact]
    public void CentroNuovoDiventaColonnaEPrendeLaQuota()
    {
        var righe = new List<RigaConsuntivo>
        {
            new(new(2026, 3, 1), "03", "SPG", -600m),
            new(new(2026, 3, 1), "ZZZ", "ANA", 100m),
        };
        var r = MotoreConsuntivo.Calcola(righe, Dal, Al, Voci, Centri);
        Assert.Contains(r.Aree, a => a.Codice == "ZZZ" && a.Etichetta == "Centro nuovo");
        Assert.Equal(-100m, r.Totali.Contributo["ZZZ"]);
    }

    [Fact]
    public void VoceSconosciutaClassificataDalSegno()
    {
        var righe = new List<RigaConsuntivo> { new(new(2026, 3, 1), "04", "XYZ", -10m) };
        var r = MotoreConsuntivo.Calcola(righe, Dal, Al, Voci, Centri);
        Assert.Contains(r.Costi, v => v.Voce == "XYZ");
    }
}
