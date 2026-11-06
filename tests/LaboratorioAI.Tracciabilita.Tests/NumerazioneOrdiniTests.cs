using LaboratorioAI.Tracciabilita.Core.Motori;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class NumerazioneOrdiniTests
{
    [Fact]
    public void SenzaOrdiniParteDalNumeroScelto() =>
        Assert.Equal(8208, NumerazioneOrdini.Prossimo(Array.Empty<string>(), 8208));

    [Fact]
    public void SenzaOrdiniEsenzaScelta1() =>
        Assert.Equal(1, NumerazioneOrdini.Prossimo(Array.Empty<string>()));

    [Fact]
    public void ProseguePartendoDalPiuAlto() =>
        Assert.Equal(8210, NumerazioneOrdini.Prossimo(new[] { "8207", "8209", "8100" }, 5));

    [Fact]
    public void IgnoraIDatiNonNumerici() =>
        Assert.Equal(11, NumerazioneOrdini.Prossimo(new[] { "10", "A-3" }));

    [Fact]
    public void ZeroOPrimoNegativoDanno1() =>
        Assert.Equal(1, NumerazioneOrdini.Prossimo(Array.Empty<string>(), 0));
}
