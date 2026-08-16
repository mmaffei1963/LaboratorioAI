using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Verifica che il servizio di validazione riproduca esattamente i valori attesi
/// del set di riferimento congelato: 3 casi × 4 punti × 5 parametri = 60 confronti,
/// tutti conformi. Se questo test fallisce, il motore di calcolo è cambiato.
/// </summary>
public class ValidazioneTaraturaTest
{
    [Fact]
    public void SetRiferimento_valida_completamente()
    {
        var servizio = new ServizioValidazioneTaratura(new ServizioTaratura());

        var esito = servizio.Esegui("test");

        Assert.Equal(60, esito.NumeroOk);
        Assert.Equal(0, esito.NumeroKo);
        Assert.True(esito.Validato);
        Assert.All(esito.Casi, c => Assert.True(c.Validato, $"Caso {c.Nome} non validato."));
    }
}
