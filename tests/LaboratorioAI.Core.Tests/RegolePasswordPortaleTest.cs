using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Password del portale clienti (17/11/2026).</summary>
public class RegolePasswordPortaleTest
{
    [Fact]
    public void PasswordValida_Accettata() =>
        Assert.Null(RegolePasswordPortale.Valida("Piadina2026!", "Piadina2026!", "01234567890", "1649"));

    [Fact]
    public void PasswordCorta_DiversaDaRipetuta_UgualeAPartitaIva_Rifiutate()
    {
        Assert.NotNull(RegolePasswordPortale.Valida("corta", "corta", "01234567890", "1649"));
        Assert.NotNull(RegolePasswordPortale.Valida("Piadina2026!", "Piadina2026?", "01234567890", "1649"));
        Assert.NotNull(RegolePasswordPortale.Valida("01234567890", "01234567890", " 01234567890 ", "1649"));
        Assert.NotNull(RegolePasswordPortale.Valida(" spazio iniziale", " spazio iniziale", "01234567890", "1649"));
    }

    [Fact]
    public void IndirizziEmail_SeparaIValidi()
    {
        var c = new CredenzialiClienteArcLab(1, "1649", "Cliente", "01234567890", "a@cliente.it; b@cliente.it ,non-email\r\nA@CLIENTE.IT");
        Assert.Equal(2, c.IndirizziEmail.Count);
        Assert.Empty(new CredenzialiClienteArcLab(1, null, null, null, null).IndirizziEmail);
    }
}
