using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Validazione del motore bilance (Modulo 45) contro l'esempio ufficiale della
/// guida EURAMET cg-18 (pag. 64): eccentricità, ripetibilità e budget d'incertezza
/// per punto di carico. Confronto a 6 decimali.
/// </summary>
public class BilanciaTest
{
    private static RisultatoBilancia Calcola()
    {
        var dati = new DatiBilancia
        {
            Portata = 220,
            UnitaFormato = 0.0001,
            CoefficienteSensibilitaTermica = 1.5e-6,
            MaxIncertezzaAmmessa = 0.02,
            TemperaturaTaratura = 21,
            TemperaturaUtilizzo = 22,
            Eccentricita = new ProvaEccentricita
            {
                CaricoNominale = 100, ValoreConvenzionale = 99.9999,
                Letture = [100.0006, 100.0004, 100.0005, 100.0007, 100.0005]
            },
            Ripetibilita = [100.0006, 100.0003, 100.0005, 100.0004, 100.0005],
            Linearita =
            [
                new() { CaricoNominale = 0,   ValoreConvenzionale = 0,        Lettura = 0,        IncertezzaMassa = 0,      MpeMassa = 0 },
                new() { CaricoNominale = 50,  ValoreConvenzionale = 50,       Lettura = 50.0004,  IncertezzaMassa = 1.5e-5, MpeMassa = 0.0001 },
                new() { CaricoNominale = 100, ValoreConvenzionale = 99.9999,  Lettura = 100.0006, IncertezzaMassa = 2.5e-5, MpeMassa = 0.00016 },
                new() { CaricoNominale = 150, ValoreConvenzionale = 149.9999, Lettura = 150.0009, IncertezzaMassa = 4e-5,   MpeMassa = 0.00026 },
                new() { CaricoNominale = 220, ValoreConvenzionale = 220.0001, Lettura = 220.0014, IncertezzaMassa = 6.2e-5, MpeMassa = 0.000285 },
            ]
        };
        return new ServizioBilancia().Calcola(dati);
    }

    [Fact]
    public void Eccentricita_e_ripetibilita()
    {
        var r = Calcola();
        Assert.Equal(0.0002, r.EccentricitaScostamentoMax, 6);
        Assert.True(r.EccentricitaConforme);
        Assert.Equal(0.00011402, r.RipetibilitaScartoTipo, 6);
        Assert.Equal(0.00011762, r.RipetibilitaComposta, 6);
    }

    [Theory]
    [InlineData(0, 0.00011762, 0.00033756, 0.00047182)]
    [InlineData(1, 0.00046519, 0.00093038, 0.00109321)]
    [InlineData(2, 0.00090026, 0.00180051, 0.00200844)]
    [InlineData(3, 0.00134667, 0.00269333, 0.00296033)]
    [InlineData(4, 0.00195732, 0.00391464, 0.00422275)]
    public void BudgetIncertezza(int i, double uE, double UE, double uso)
    {
        var p = Calcola().Punti[i];
        Assert.Equal(uE, p.IncertezzaTipoErrore, 6);
        Assert.Equal(UE, p.IncertezzaEstesa, 6);
        Assert.Equal(uso, p.IncertezzaEstesaUso, 6);
    }
}
