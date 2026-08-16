using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Validazione del motore di mappatura (Modulo 04-03) contro i tre casi VAL del
/// foglio storico, sui parametri effettivamente verificati (budget d'incertezza,
/// medie per ripiano, intervallo ammesso). Tolleranza 5e-4 °C come nel foglio.
/// </summary>
public class MappaturaTest
{
    private static IReadOnlyList<PuntoCertificatoCdr> Cert() =>
    [
        new() { TIndicata = -25.11, TRiferimento = -24.95, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  -0.11, TRiferimento =   0.00, IncertezzaEstesa = 0.03 },
        new() { TIndicata =  25.00, TRiferimento =  25.07, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  39.82, TRiferimento =  39.90, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  99.89, TRiferimento = 100.02, IncertezzaEstesa = 0.06 },
    ];

    private static RisultatoMappatura Calcola(double nominale, double tol, (string pos, double t)[] letture) =>
        new ServizioMappaturaIncubatori().Calcola(new DatiMappatura
        {
            Certificato = Cert(),
            ValoreNominale = nominale,
            Tolleranza = tol,
            RisoluzioneCdr = 0.01,
            IncertezzaEstesaCertificato = 0.03,
            IncertezzaInstabilita = 0,
            FattoreCopertura = 2.0,
            Letture = letture.Select(l => new LetturaMappatura { Posizione = l.pos, TemperaturaRilevata = l.t }).ToList()
        });

    private static double Media(RisultatoMappatura r, string pos) => r.Ripiani.First(x => x.Posizione == pos).Media;

    [Fact]
    public void Caso1_un_ripiano()
    {
        var r = Calcola(5, 0.3, [("A", 5), ("A", 5.01), ("A", 4.99), ("A", 5), ("A", 5)]);

        Assert.Equal(5.07814018, r.TemperaturaMedia, 4);
        Assert.Equal(0.00316228, r.ScartoTipoMedia, 4);
        Assert.Equal(0.03276177, r.IncertezzaEstesa, 4);
        Assert.Equal(0.07814018, r.DifferenzaNominale, 4);
        Assert.Equal(4.73276177, r.IntervalloMinimo, 4);
        Assert.Equal(5.26723823, r.IntervalloMassimo, 4);
        Assert.Equal(5.07814018, Media(r, "A"), 4);
    }

    [Fact]
    public void Caso2_due_ripiani()
    {
        var r = Calcola(5, 0.3,
            [("A", 5), ("A", 5.01), ("A", 4.99), ("A", 5), ("A", 5),
             ("B", 5.04), ("B", 5.05), ("B", 5.03), ("B", 5.04), ("B", 5.05)]);

        Assert.Equal(5.09914018, r.TemperaturaMedia, 4);
        Assert.Equal(0.00737111, r.ScartoTipoMedia, 4);
        Assert.Equal(0.03536477, r.IncertezzaEstesa, 4);
        Assert.Equal(0.09914018, r.DifferenzaNominale, 4);
        Assert.Equal(4.73536477, r.IntervalloMinimo, 4);
        Assert.Equal(5.26463523, r.IntervalloMassimo, 4);
        Assert.Equal(5.07814018, Media(r, "A"), 4);
        Assert.Equal(5.12014018, Media(r, "B"), 4);
    }

    [Fact]
    public void Caso3_quattro_ripiani()
    {
        var r = Calcola(5, 0.3,
            [("A", 4.85), ("A", 4.86), ("A", 4.84), ("A", 4.85), ("A", 4.87),
             ("B", 5), ("B", 5.02), ("B", 4.99), ("B", 5.01), ("B", 5),
             ("C", 5.13), ("C", 5.15), ("C", 5.14), ("C", 5.13), ("C", 5.16),
             ("D", 5.28), ("D", 5.3), ("D", 5.27), ("D", 5.29), ("D", 5.31)]);

        Assert.Equal(5.15064018, r.TemperaturaMedia, 4);
        Assert.Equal(0.03718995, r.ScartoTipoMedia, 4);
        Assert.Equal(0.08102902, r.IncertezzaEstesa, 4);
        Assert.Equal(0.15064018, r.DifferenzaNominale, 4);
        Assert.Equal(4.78102902, r.IntervalloMinimo, 4);
        Assert.Equal(5.21897098, r.IntervalloMassimo, 4);
        Assert.Equal(4.93214018, Media(r, "A"), 4);
        Assert.Equal(5.08214018, Media(r, "B"), 4);
        Assert.Equal(5.22014018, Media(r, "C"), 4);
        Assert.Equal(5.36814018, Media(r, "D"), 4);
    }
}
