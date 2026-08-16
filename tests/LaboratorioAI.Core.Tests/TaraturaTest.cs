using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Validazione del motore di taratura contro i casi VAL_Caso1/2/3 del foglio
/// Excel storico (Modulo Statistico 04-01). Gli attesi sono i valori del foglio
/// di validazione: il motore è corretto se li riproduce.
/// </summary>
public class TaraturaTest
{
    // Certificato del CDR (comune ai tre casi): TIndicata, TRiferim, Differenza, UE, u(CR).
    private static IReadOnlyList<PuntoCertificatoCdr> Cert() =>
    [
        new() { TIndicata = -25.08, TRiferimento = -25.04, Differenza = -0.16, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
        new() { TIndicata = 0,      TRiferimento = 0,      Differenza = -0.11, IncertezzaEstesa = 0.03, IncertezzaTipo = 0.025 },
        new() { TIndicata = 25.05,  TRiferimento = 25.04,  Differenza = -0.07, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
        new() { TIndicata = 40.03,  TRiferimento = 40,     Differenza = -0.08, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
        new() { TIndicata = 99.96,  TRiferimento = 99.98,  Differenza = -0.13, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
    ];

    private static PuntoTaratura Costante(string et, double nom, double tw, int n = 10) =>
        new() { Etichetta = et, Letture = Enumerable.Range(0, n)
            .Select(_ => new LetturaTaratura { TemperaturaNominale = nom, LetturaStrumento = tw }).ToList() };

    private static PuntoTaratura ConTw(string et, double nom, double[] tw) =>
        new() { Etichetta = et, Letture = tw
            .Select(t => new LetturaTaratura { TemperaturaNominale = nom, LetturaStrumento = t }).ToList() };

    private static PuntoTaratura NomTw(string et, double[] nom, double[] tw) =>
        new() { Etichetta = et, Letture = Enumerable.Range(0, nom.Length)
            .Select(i => new LetturaTaratura { TemperaturaNominale = nom[i], LetturaStrumento = tw[i] }).ToList() };

    private static RisultatoTaratura Calcola(params PuntoTaratura[] punti) =>
        new ServizioTaratura().Calcola(new DatiTaratura
        {
            Certificato = Cert(),
            RisoluzioneStrumento = 0.1,
            FattoreCopertura = 2.0,
            Punti = punti
        });

    [Fact]
    public void Caso1_letture_costanti()
    {
        var r = Calcola(
            Costante("A", -20, -20.1), Costante("B", 0, 0),
            Costante("C", 20, 20), Costante("D", 44, 44)).Punti;

        Assert.Equal(-20.117831, r[0].TemperaturaMediaRiferimento, 5);
        Assert.Equal(0.017831, r[0].DifferenzaMedia, 5);
        Assert.Equal(0.076376, r[0].IncertezzaEstesa, 5);
        Assert.Equal(-0.017831, r[0].FattoreCorrezione, 5);

        Assert.Equal(-0.114568, r[1].TemperaturaMediaRiferimento, 5);
        Assert.Equal(19.888696, r[2].TemperaturaMediaRiferimento, 5);
        Assert.Equal(43.892612, r[3].TemperaturaMediaRiferimento, 5);
        Assert.Equal(0.076376, r[3].IncertezzaEstesa, 5);
    }

    [Fact]
    public void Caso2_ripetibilita()
    {
        var r = Calcola(ConTw("A", -19.5,
            [-19.3, -19.4, -19.3, -19.4, -19.3, -19.4, -19.3, -19.4, -19.3, -19.4])).Punti;

        Assert.Equal(-19.617750, r[0].TemperaturaMediaRiferimento, 5);
        Assert.Equal(-19.35, r[0].TemperaturaMediaStrumento, 5);
        Assert.Equal(0.267750, r[0].DifferenzaMedia, 5);
        Assert.Equal(0.083333, r[0].IncertezzaEstesa, 5);   // ripetibilità non nulla
    }

    [Fact]
    public void Caso3_stabilita_riferimento()
    {
        var r = Calcola(NomTw("A",
            [-10, -10.01, -10.02, -10.03, -10.04, -10.05, -10.06, -10.07, -10.08, -10.09],
            [-10.2, -10.2, -10.1, -10.2, -10.1, -10.2, -10.1, -10.2, -10.1, -10.2])).Punti;

        Assert.Equal(-10.161207, r[0].TemperaturaMediaRiferimento, 5);
        Assert.Equal(0.001207, r[0].DifferenzaMedia, 5);
        Assert.Equal(0.097984, r[0].IncertezzaEstesa, 5);   // include stabilità del riferimento
    }
}
