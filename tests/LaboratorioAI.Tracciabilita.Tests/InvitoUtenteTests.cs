using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class InvitoUtenteTests
{
    private static readonly DateTime Adesso = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Il_codice_e_casuale_lungo_e_senza_caratteri_strani()
    {
        var a = InvitoUtente.Crea(Adesso);
        var b = InvitoUtente.Crea(Adesso);
        Assert.NotEqual(a.Codice, b.Codice);
        Assert.True(a.Codice.Length >= 43);
        Assert.Matches("^[A-Za-z0-9_-]+$", a.Codice);
    }

    [Fact]
    public void Nel_database_va_l_impronta_non_il_codice()
    {
        var i = InvitoUtente.Crea(Adesso);
        Assert.NotEqual(i.Codice, i.Impronta);
        Assert.Equal(64, i.Impronta.Length);
        Assert.Equal(i.Impronta, InvitoUtente.ImprontaDi(i.Codice));
    }

    [Fact]
    public void Vale_sette_giorni()
    {
        var i = InvitoUtente.Crea(Adesso);
        Assert.Equal(Adesso.AddDays(7), i.ScadenzaUtc);
        Assert.True(InvitoUtente.Valido(i.Impronta, i.ScadenzaUtc, i.Codice, Adesso.AddDays(6)));
        Assert.False(InvitoUtente.Valido(i.Impronta, i.ScadenzaUtc, i.Codice, Adesso.AddDays(7).AddMinutes(1)));
    }

    [Fact]
    public void Un_codice_sbagliato_o_vuoto_non_vale()
    {
        var i = InvitoUtente.Crea(Adesso);
        Assert.False(InvitoUtente.Valido(i.Impronta, i.ScadenzaUtc, "altro", Adesso));
        Assert.False(InvitoUtente.Valido(i.Impronta, i.ScadenzaUtc, "", Adesso));
        Assert.False(InvitoUtente.Valido(null, i.ScadenzaUtc, i.Codice, Adesso));
        Assert.False(InvitoUtente.Valido(i.Impronta, null, i.Codice, Adesso));
    }
}
