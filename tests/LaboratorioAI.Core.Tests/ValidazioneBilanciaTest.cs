using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Verifica che il servizio di validazione bilance riproduca il caso EURAMET congelato.</summary>
public class ValidazioneBilanciaTest
{
    [Fact]
    public void SetRiferimento_valida_completamente()
    {
        var servizio = new ServizioValidazioneBilancia(new ServizioBilancia());

        var esito = servizio.Esegui("test");

        Assert.Equal(0, esito.NumeroKo);
        Assert.True(esito.Validato);
    }
}
