using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Richieste di analisi dei clienti privati (05/11/2026): tempi proposti e prezzi IVA inclusa.</summary>
public class RegoleRichiestePrivatiTest
{
    private static MetodoRichiestaPrivato M(string nome, int? lab = null) => new(nome, "metodo", true, lab, lab is null ? null : "Lab", null);

    [Fact]
    public void Tempi_ProveInterne_10Giorni() =>
        Assert.Equal(10, RegoleRichiestePrivati.TempiControllo("Potabilità base", [M("Escherichia coli"), M("Nitrati")]));

    [Fact]
    public void Tempi_Legionella_20Giorni()
    {
        Assert.Equal(20, RegoleRichiestePrivati.TempiControllo("Acqua calda sanitaria", [M("Legionella pneumophila")]));
        Assert.Equal(20, RegoleRichiestePrivati.TempiControllo("Ricerca LEGIONELLA", [M("Conta")]));
    }

    [Fact]
    public void Tempi_ProvaEsterna_20Giorni() =>
        Assert.Equal(20, RegoleRichiestePrivati.TempiControllo("Metalli", [M("Ferro"), M("Arsenico", lab: 7)]));

    [Fact]
    public void Tempi_Richiesta_IlPiuLungo()
    {
        Assert.Equal(20, RegoleRichiestePrivati.TempiRichiesta([10, 20, 10]));
        Assert.Equal(10, RegoleRichiestePrivati.TempiRichiesta([]));
    }

    [Fact]
    public void Totale_IvaInclusaPiuSupplementoSoloSeRichiesto()
    {
        var r = new RichiestaAnalisiPrivato { AliquotaIva = 22m, SupplementoGiudizio = 50m };
        r.Righe.Add(new RigaRichiestaAnalisiPrivato { PrezzoImponibile = 50m });
        r.Righe.Add(new RigaRichiestaAnalisiPrivato { PrezzoImponibile = 33.33m });
        RegoleRichiestePrivati.Ricalcola(r);
        Assert.Equal(61.00m, r.Righe[0].PrezzoIvaInclusa);
        Assert.Equal(40.66m, r.Righe[1].PrezzoIvaInclusa);
        Assert.Equal(101.66m, r.TotaleIvaInclusa);
        r.GiudizioRichiesto = true;
        RegoleRichiestePrivati.Ricalcola(r);
        Assert.Equal(151.66m, r.TotaleIvaInclusa);
        Assert.Equal(83.33m + 40.98m, r.TotaleImponibile);     // supplemento 50 € IVA inclusa = 40,98 € + IVA
        Assert.Equal(27.35m, RegoleRichiestePrivati.Iva(r));
    }

    [Fact]
    public void Numero_Formattato() => Assert.Equal("PRIV-2026/007", RegoleRichiestePrivati.NumeroFormattato(2026, 7));
}
