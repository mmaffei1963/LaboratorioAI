using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Compensi soci (21/10/2026): stessi conti del foglio Excel «Compensi_Soci». Cifre di prova, non quelle reali.</summary>
public class MotoreCompensiSociTest
{
    private static readonly ParametriCompensi Parametri = new(2025, 40000m, 40m, 6000m, 8000m, 1000m, 4000m, 50m, 5m, null);

    private static DatiSocioCompensi Socio(int id, decimal ore = 40m, int mesiAmm = 0, bool resp = true, decimal auto = 100m,
        bool rspp = false, bool lab = false, decimal? premio = null, decimal fatturatoRspp = 0m, decimal rimborsi = 0m,
        decimal recupero = 0m, decimal percepito = 0m, decimal rettifica = 0m) =>
        new(id, "Socio " + id, ore, mesiAmm, resp, auto, rspp, lab, premio, fatturatoRspp, rimborsi, recupero, percepito, rettifica);

    private static readonly VoceBaseLaboratorio[] Voci =
    [
        new("ANA", "analisi", 100000m, 100m),
        new("ANE", "analisi esterne", 20000m, 50m),
    ];

    [Fact]
    public void BaseInProporzioneAlleOreEAmministratorePerMesi()
    {
        var r = MotoreCompensiSoci.Calcola(Parametri, [Socio(1, ore: 34m, mesiAmm: 1), Socio(2, mesiAmm: 11)], [], []);
        Assert.Equal(34000m, r.Soci[0].Base);          // 34 ore su 40 = 85%
        Assert.Equal(500m, r.Soci[0].Amministratore);  // 6000 / 12 × 1
        Assert.Equal(5500m, r.Soci[1].Amministratore);
        Assert.Equal(34000m + 500m + 8000m, r.Soci[0].Fisso);
    }

    [Fact]
    public void IndennitaLaboratorioMenoPremiEPremioDelSocioNellaSuaColonna()
    {
        var premi = new[] { new PremioLaboratorio("Dipendente", 1000m, null), new PremioLaboratorio("Socio 2", 2000m, 2) };
        var r = MotoreCompensiSoci.Calcola(Parametri, [Socio(1, lab: true), Socio(2)], Voci, premi);
        Assert.Equal(110000m, r.BaseLaboratorioCalcolata);   // 100000 + 50% di 20000
        Assert.Equal(5500m, r.IndennitaLaboratorioLorda);
        Assert.Equal(5500m - 3000m, r.Soci[0].IndennitaLaboratorio);
        Assert.Equal(2000m, r.Soci[1].IndennitaLaboratorio);
    }

    [Fact]
    public void BaseLaboratorioScrittaAManoVinceSuQuellaCalcolata()
    {
        var r = MotoreCompensiSoci.Calcola(Parametri with { BaseLaboratorioManuale = 50000m }, [Socio(1, lab: true)], Voci, []);
        Assert.Equal(110000m, r.BaseLaboratorioCalcolata);
        Assert.Equal(2500m, r.Soci[0].IndennitaLaboratorio);
        Assert.True(r.BaseLaboratorioManuale);
    }

    [Fact]
    public void TotaleEDifferenzaComeNelFoglioExcel()
    {
        var s = Socio(1, rspp: true, fatturatoRspp: 30000m, rimborsi: 1200m, recupero: 15000m, auto: 50m, percepito: 60000m, rettifica: 500m);
        var c = MotoreCompensiSoci.Calcola(Parametri, [s], [], []).Soci[0];
        Assert.Equal(15000m, c.Rspp);
        Assert.Equal(500m, c.Assicurazione);   // 1000 × 50%
        Assert.Equal(40000m + 8000m + 15000m + 1200m + 500m + 15000m + 4000m, c.Totale);
        Assert.Equal(60500m, c.Percepito);
        Assert.Equal(c.Totale - 60500m, c.Differenza);
    }

    [Fact]
    public void PremioDelSocioSostituisceQuelloDellAnnoESenzaRsppNonSiContaIlFatturato()
    {
        var c = MotoreCompensiSoci.Calcola(Parametri, [Socio(1, premio: 0m, fatturatoRspp: 9999m)], [], []).Soci[0];
        Assert.Equal(0m, c.Premio);
        Assert.Equal(0m, c.Rspp);
    }

    [Fact]
    public void RimborsiTrasferteNelTotale()
    {
        var s = new DatiSocioCompensi(1, "Socio 1", 40m, 0, false, 0m, false, false, 0m, 0m, 100m, 0m, 0m, 0m, RimborsiTrasferte: 350m);
        var c = MotoreCompensiSoci.Calcola(Parametri, [s], [], []).Soci[0];
        Assert.Equal(350m, c.RimborsiTrasferte);
        Assert.Equal(40000m + 350m + 100m, c.Totale);
    }

    [Fact]
    public void MaggiorazioneDelRimborso()
    {
        Assert.Equal(1164.50m, MotoreCompensiSoci.ImportoRimborso(895.77m, 30m));
        Assert.Equal(100m, MotoreCompensiSoci.ImportoRimborso(100m, 0m));
    }
}
