using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using Xunit;

namespace LaboratorioAI.Tracciabilita.Tests;

public class RegolePasswordTests
{
    [Fact]
    public void Password_buona_e_accettata() =>
        Assert.Empty(RegolePassword.Valida("Pinsa-e-Piadina-2026", "mario.rossi@azienda.it"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Breve1!")]
    [InlineData("12345678901")]   // 11 caratteri
    public void Password_troppo_corta_rifiutata(string? password) =>
        Assert.NotEmpty(RegolePassword.Valida(password));

    [Theory]
    [InlineData("123456789012")]
    [InlineData("aaaaaaaaaaaa")]
    [InlineData("Password1234")]
    public void Password_ovvie_rifiutate(string password) =>
        Assert.NotEmpty(RegolePassword.Valida(password));

    [Fact]
    public void Password_con_il_nome_dell_email_rifiutata() =>
        Assert.NotEmpty(RegolePassword.Valida("xx-mario.rossi-2026", "mario.rossi@azienda.it"));

    [Fact]
    public void Nome_email_corto_non_fa_scattare_la_regola() =>
        Assert.Empty(RegolePassword.Valida("ab-Cavallo-Blu-77", "ab@azienda.it"));
}

public class PoliticaBloccoAccessoTests
{
    private static readonly DateTime Adesso = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void I_primi_errori_contano_senza_bloccare()
    {
        var tentativi = 0;
        for (var i = 1; i < PoliticaBloccoAccesso.TentativiMassimi; i++)
        {
            var (t, fino) = PoliticaBloccoAccesso.DopoFallimento(tentativi, Adesso);
            Assert.Null(fino);
            Assert.Equal(i, t);
            tentativi = t;
        }
    }

    [Fact]
    public void Al_quinto_errore_scatta_il_blocco_di_15_minuti_e_il_contatore_riparte()
    {
        var (t, fino) = PoliticaBloccoAccesso.DopoFallimento(PoliticaBloccoAccesso.TentativiMassimi - 1, Adesso);
        Assert.Equal(0, t);
        Assert.Equal(Adesso.AddMinutes(15), fino);
    }

    [Fact]
    public void Il_blocco_finisce_alla_scadenza()
    {
        var fino = Adesso.AddMinutes(15);
        Assert.True(PoliticaBloccoAccesso.EBloccato(fino, Adesso));
        Assert.True(PoliticaBloccoAccesso.EBloccato(fino, Adesso.AddMinutes(14)));
        Assert.False(PoliticaBloccoAccesso.EBloccato(fino, fino));
        Assert.False(PoliticaBloccoAccesso.EBloccato(null, Adesso));
    }

    [Fact]
    public void Minuti_rimasti_arrotondati_per_eccesso()
    {
        Assert.Equal(15, PoliticaBloccoAccesso.MinutiRimasti(Adesso.AddMinutes(15), Adesso));
        Assert.Equal(1, PoliticaBloccoAccesso.MinutiRimasti(Adesso.AddSeconds(10), Adesso));
        Assert.Equal(0, PoliticaBloccoAccesso.MinutiRimasti(null, Adesso));
    }

    [Fact]
    public void Un_accesso_riuscito_azzera_tutto() =>
        Assert.Equal((0, (DateTime?)null), PoliticaBloccoAccesso.DopoSuccesso());
}
