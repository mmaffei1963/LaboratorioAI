using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Verifica che il servizio di validazione mappatura riproduca gli attesi del set congelato.</summary>
public class ValidazioneMappaturaTest
{
    [Fact]
    public void SetRiferimento_valida_completamente()
    {
        var servizio = new ServizioValidazioneMappatura(new ServizioMappaturaIncubatori());

        var esito = servizio.Esegui("test");

        Assert.Equal(0, esito.NumeroKo);
        Assert.True(esito.Validato);
        Assert.All(esito.Casi, c => Assert.True(c.Validato, $"Caso {c.Nome} non validato."));
    }
}
