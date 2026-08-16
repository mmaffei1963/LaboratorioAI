using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Verifica che il servizio di validazione incubatori riproduca i valori attesi
/// del set di riferimento congelato: 3 casi × 7 grandezze = 21 confronti conformi.
/// </summary>
public class ValidazioneIncubatoriTest
{
    [Fact]
    public void SetRiferimento_valida_completamente()
    {
        var servizio = new ServizioValidazioneIncubatori(new ServizioTaraturaIncubatori());

        var esito = servizio.Esegui("test");

        Assert.Equal(21, esito.NumeroOk);
        Assert.Equal(0, esito.NumeroKo);
        Assert.True(esito.Validato);
        Assert.All(esito.Casi, c => Assert.True(c.Validato, $"Caso {c.Nome} non validato."));
    }
}
