using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Validazione del motore incubatori (Modulo Statistico 04-02) contro i tre casi VAL del foglio storico.
/// I dati e i valori attesi sono quelli del set di riferimento congelato <see cref="SetRiferimentoIncubatori"/>
/// (lo stesso usato dalla validazione nell'applicazione), così test e applicazione non possono divergere.
/// Tolleranza di confronto 1e-4 °C come nel foglio.
/// </summary>
public class TaraturaIncubatoriTest
{
    public static IEnumerable<object[]> Casi() =>
        SetRiferimentoIncubatori.Casi().Select(c => new object[] { c.Nome });

    [Theory]
    [MemberData(nameof(Casi))]
    public void Caso_del_foglio_storico(string nome)
    {
        var caso = SetRiferimentoIncubatori.Casi().Single(c => c.Nome == nome);
        var r = new ServizioTaraturaIncubatori().Calcola(caso.Dati);
        var a = caso.Atteso;

        Assert.Equal(a.TemperaturaMediaRiferimento, r.TemperaturaMediaRiferimento, 4);
        Assert.Equal(a.TemperaturaMediaStrumento, r.TemperaturaMediaStrumento, 4);
        Assert.Equal(a.IncertezzaEstesa, r.IncertezzaEstesa, 4);
        Assert.Equal(a.FattoreCorrezione, r.FattoreCorrezione, 4);
        Assert.Equal(a.AllarmeMinimo, r.AllarmeMinimo, 4);
        Assert.Equal(a.AllarmeMassimo, r.AllarmeMassimo, 4);
        Assert.Equal(a.Conforme, r.Conforme);
    }

    /// <summary>
    /// Incertezze del certificato del CDR diverse per punto (0,03 a 0 °C, 0,06 a 25 °C): a 20 °C u(CR) è presa
    /// in modo cautelativo come la maggiore delle due (0,06), quindi U resta quella del foglio (0,1244) e non
    /// scende a 0,1216 come accadrebbe interpolando.
    /// </summary>
    [Fact]
    public void Incertezza_del_CDR_cautelativa_fra_i_punti_del_certificato()
    {
        var caso = SetRiferimentoIncubatori.Casi().Single(c => c.Nome == "Caso 1");
        var cert = caso.Dati.Certificato
            .Select(p => p.TIndicata == 0.0 ? p with { IncertezzaEstesa = 0.03 } : p)
            .ToList();
        var dati = caso.Dati with { Certificato = cert };

        var r = new ServizioTaraturaIncubatori().Calcola(dati);

        Assert.Equal(caso.Atteso.IncertezzaEstesa, r.IncertezzaEstesa, 4);
    }
}
