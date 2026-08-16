using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Validazione del motore incubatori (Modulo Statistico 04-02) contro i tre casi
/// VAL del foglio storico. Tolleranza di confronto 1e-4 °C come nel foglio.
/// </summary>
public class TaraturaIncubatoriTest
{
    // Certificato del CDR comune (T indicata, T riferim, incertezza estesa).
    private static IReadOnlyList<PuntoCertificatoCdr> Cert() =>
    [
        new() { TIndicata = -25.08, TRiferimento = -25.04, IncertezzaEstesa = 0.06 },
        new() { TIndicata =   0.00, TRiferimento =   0.00, IncertezzaEstesa = 0.03 },
        new() { TIndicata =  25.05, TRiferimento =  25.04, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  40.03, TRiferimento =  40.00, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  99.96, TRiferimento =  99.98, IncertezzaEstesa = 0.06 },
    ];

    // Taratura secondaria del CDR, per il calcolo della deriva u(stCR).
    private static IReadOnlyList<PuntoCertificatoCdr> CertSec() =>
    [
        new() { TIndicata = -25.11, TRiferimento = -24.95 },
        new() { TIndicata =  -0.11, TRiferimento =   0.00 },
        new() { TIndicata =  25.00, TRiferimento =  25.07 },
        new() { TIndicata =  39.82, TRiferimento =  39.90 },
        new() { TIndicata =  99.89, TRiferimento = 100.02 },
    ];

    private static DatiTaraturaIncubatore Dati(double nominale, double tolleranza, double uStab, double[] tref, double[] tw) =>
        new()
        {
            Certificato = Cert(),
            CertificatoSecondario = CertSec(),
            ValoreNominale = nominale,
            Tolleranza = tolleranza,
            RisoluzioneStrumento = 0.1,
            IncertezzaInstabilita = uStab,
            DispersioneMappatura = 0,
            FattoreCopertura = 2.0,
            Letture = Enumerable.Range(0, tref.Length)
                .Select(i => new LetturaIncubatore { Posizione = "A", TemperaturaRilevata = tref[i], LetturaStrumento = tw[i] })
                .ToList()
        };

    [Fact]
    public void Caso1_errore_positivo()
    {
        var r = new ServizioTaraturaIncubatori().Calcola(Dati(20, 1, 0.04618802153517113,
            [20.2, 20.2, 20.21, 20.19, 20.2, 20.2, 20.21, 20.2, 20.19, 20.2, 20.2, 20.21, 20.2, 20.19, 20.2, 20.2],
            [20, 20, 20.01, 20, 20, 20.01, 20, 20, 20, 20.01, 20, 20, 20.01, 20, 20, 20]));

        Assert.Equal(20.19, r.TemperaturaMediaRiferimento, 4);
        Assert.Equal(20.0025, r.TemperaturaMediaStrumento, 4);
        Assert.Equal(0.12441195, r.IncertezzaEstesa, 4);
        Assert.Equal(0.1875, r.FattoreCorrezione, 4);
        Assert.Equal(18.93691195, r.AllarmeMinimo, 4);
        Assert.Equal(20.68808805, r.AllarmeMassimo, 4);
        Assert.True(r.Conforme);
    }

    [Fact]
    public void Caso2_errore_negativo()
    {
        var r = new ServizioTaraturaIncubatori().Calcola(Dati(20, 1, 0.04618802153517113,
            [20.2, 20.21, 20.2, 20.2, 20.19, 20.2, 20.21, 20.2, 20.2, 20.19, 20.2, 20.21, 20.2, 20.2, 20.19, 20.2],
            [20.4, 20.39, 20.4, 20.41, 20.4, 20.4, 20.39, 20.4, 20.41, 20.4, 20.4, 20.39, 20.4, 20.41, 20.4, 20.4]));

        Assert.Equal(20.19, r.TemperaturaMediaRiferimento, 4);
        Assert.Equal(20.4, r.TemperaturaMediaStrumento, 4);
        Assert.Equal(0.12448561, r.IncertezzaEstesa, 4);
        Assert.Equal(-0.21, r.FattoreCorrezione, 4);
        Assert.Equal(19.33448561, r.AllarmeMinimo, 4);
        Assert.Equal(21.08551439, r.AllarmeMassimo, 4);
        Assert.True(r.Conforme);
    }

    [Fact]
    public void Caso3_punto_negativo_tolleranza_ridotta()
    {
        var r = new ServizioTaraturaIncubatori().Calcola(Dati(-20, 0.5, 0.0404145188432743,
            [-19.9, -19.89, -19.9, -19.91, -19.9, -19.89, -19.9, -19.91, -19.9, -19.89, -19.9, -19.91, -19.9, -19.89, -19.9, -19.91],
            [-20.1, -20.11, -20.1, -20.09, -20.1, -20.11, -20.1, -20.09, -20.1, -20.11, -20.1, -20.09, -20.1, -20.11, -20.1, -20.09]));

        Assert.Equal(-19.86, r.TemperaturaMediaRiferimento, 4);
        Assert.Equal(-20.1, r.TemperaturaMediaStrumento, 4);
        Assert.Equal(0.11627553, r.IncertezzaEstesa, 4);
        Assert.Equal(0.24, r.FattoreCorrezione, 4);
        Assert.Equal(-20.62372447, r.AllarmeMinimo, 4);
        Assert.Equal(-19.85627553, r.AllarmeMassimo, 4);
        Assert.True(r.Conforme);
    }
}
