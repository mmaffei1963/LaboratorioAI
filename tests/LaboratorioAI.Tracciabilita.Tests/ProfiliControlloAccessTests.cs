using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Importazione;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class ProfiliControlloAccessTests
{
    private static readonly TestoControllo[] Testi =
    {
        new(TipoControllo.Temperatura, 1, "Ambiente"),
        new(TipoControllo.Temperatura, 2, "ambiente "),   // altra riga, stesso testo
        new(TipoControllo.Temperatura, 3, "Refrigerato 0-4 °C"),
        new(TipoControllo.Confezione, 1, "Integra")
    };

    [Fact]
    public void MatriciConStessiTesti_FinisconoNelloStessoProfilo_AncheConIdDiversi()
    {
        var righe = new[]
        {
            new RigaControlliMatrice(10, null, 1, 1, null, null, null, null),
            new RigaControlliMatrice(11, null, 1, 2, null, null, null, null),
            new RigaControlliMatrice(12, null, 1, 3, null, null, null, null)
        };
        var g = ProfiliControlloAccess.Raggruppa(righe, Testi);
        Assert.Equal(2, g.Count);
        Assert.Equal(new[] { 10, 11 }, g[0].IdMatrici);
        Assert.Equal(new[] { 12 }, g[1].IdMatrici);
    }

    [Fact]
    public void ControlloSenzaTesto_NonEntraNelProfilo()
    {
        var g = ProfiliControlloAccess.Raggruppa(new[] { new RigaControlliMatrice(1, 99, 1, null, null, null, null, null) }, Testi);
        Assert.Single(g);
        Assert.Equal(new[] { TipoControllo.Confezione }, g[0].Testi.Keys);
    }

    [Fact]
    public void ElencoVuoto_NessunProfilo() =>
        Assert.Empty(ProfiliControlloAccess.Raggruppa(Array.Empty<RigaControlliMatrice>(), Testi));
}
