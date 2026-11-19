using System;
using LaboratorioAI.Core.Presenze;
using Xunit;

namespace LaboratorioAI.Core.Tests.Presenze;

public class ProspettoPresenzeTests
{
    [Theory]
    [InlineData(8.0, "8")]
    [InlineData(1.5, "1,5")]
    [InlineData(7.25, "7,25")]
    [InlineData(0.0, "0")]
    [InlineData(2.3333333, "2,33")]
    public void TestoOre_VirgolaESenzaZeriInutili(double ore, string atteso) =>
        Assert.Equal(atteso, ProspettoPresenze.TestoOre(ore));

    [Fact]
    public void TestoCella_TestoOreOVuota()
    {
        Assert.Equal("FE", ProspettoPresenze.TestoCella(new ContenutoCella { Testo = "FE" }));
        Assert.Equal("1,5", ProspettoPresenze.TestoCella(new ContenutoCella { Ore = 1.5 }));
        Assert.Equal("", ProspettoPresenze.TestoCella(new ContenutoCella()));
        Assert.Equal("", ProspettoPresenze.TestoCella(null));
    }

    [Theory]
    [InlineData("#003399", true)]
    [InlineData("FF003399", true)]
    [InlineData("#ffc000", false)]
    [InlineData("#FFFF00", false)]
    [InlineData("rosso", false)]
    [InlineData(null, false)]
    public void ColoreScuro(string? colore, bool atteso) =>
        Assert.Equal(atteso, ProspettoPresenze.ColoreScuro(colore));

    [Fact]
    public void ColoreCarattere_RossoBiancoOAutomatico()
    {
        Assert.Equal("FFFF0000", ProspettoPresenze.ColoreCarattere(new ContenutoCella { Ore = 1, CarattereRosso = true }, "#003399"));
        var suScuro = new ContenutoCella { Ore = 8, Sfondo = "FF003399" };
        Assert.Equal("FFFFFFFF", ProspettoPresenze.ColoreCarattere(suScuro, "#003399"));
        var suChiaro = new ContenutoCella { Ore = 8, Sfondo = "FFFFC000" };
        Assert.Null(ProspettoPresenze.ColoreCarattere(suChiaro, "#ffc000"));
        var giallo = new ContenutoCella { Testo = "FE", Sfondo = ContenutoCellaPresenze.Giallo };
        Assert.Null(ProspettoPresenze.ColoreCarattere(giallo, "#003399"));
    }

    [Fact]
    public void ColoreHtml() => Assert.Equal("#FFC000", ProspettoPresenze.ColoreHtml("FFFFC000"));
}

public class RegoleCalendarioPresenzeTests
{
    private static readonly DateOnly Lunedi = new(2027, 3, 1);

    [Fact]
    public void Festivita_TuttiIGiorniFineSettimanaCompreso()
    {
        var e = RegoleCalendarioPresenze.Pianifica("FE", Lunedi, Lunedi.AddDays(6), null);
        Assert.True(e.Valido);
        Assert.Equal(7, e.Giorni.Count);
        Assert.Null(e.Ore);
    }

    [Fact]
    public void Chiusura_PiuGiorniSaltaIlFineSettimana()
    {
        var e = RegoleCalendarioPresenze.Pianifica("CH", Lunedi, Lunedi.AddDays(6), "4");
        Assert.True(e.Valido);
        Assert.Equal(5, e.Giorni.Count);
        Assert.Equal(4.0, e.Ore);
    }

    [Fact]
    public void Chiusura_UnSoloGiornoVale_AncheNelFineSettimana()
    {
        var sabato = new DateOnly(2027, 3, 6);
        var e = RegoleCalendarioPresenze.Pianifica("CH", sabato, sabato, null);
        Assert.True(e.Valido);
        Assert.Equal(1, e.Giorni.Count);
        Assert.Null(e.Ore);
    }

    [Fact]
    public void Chiusura_SoloFineSettimanaSenzaGiorni_Errore()
    {
        var e = RegoleCalendarioPresenze.Pianifica("CH", new DateOnly(2027, 3, 6), new DateOnly(2027, 3, 7), null);
        Assert.False(e.Valido);
    }

    [Theory]
    [InlineData("XX", "Tipo non valido.")]
    [InlineData(null, "Tipo non valido.")]
    public void TipoNonValido(string? tipo, string messaggio) =>
        Assert.Equal(messaggio, RegoleCalendarioPresenze.Pianifica(tipo, Lunedi, Lunedi, null).Errore);

    [Fact]
    public void Intervalli_NonValidi()
    {
        Assert.False(RegoleCalendarioPresenze.Pianifica("FE", Lunedi, Lunedi.AddDays(-1), null).Valido);
        Assert.False(RegoleCalendarioPresenze.Pianifica("FE", Lunedi, Lunedi.AddDays(63), null).Valido);
        Assert.True(RegoleCalendarioPresenze.Pianifica("FE", Lunedi, Lunedi.AddDays(62), null).Valido);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("25")]
    [InlineData("abc")]
    public void OreNonValide_Errore(string ore) =>
        Assert.False(RegoleCalendarioPresenze.Pianifica("CH", Lunedi, Lunedi, ore).Valido);

    [Fact]
    public void OreSuFestivita_Ignorate() =>
        Assert.Null(RegoleCalendarioPresenze.Pianifica("FE", Lunedi, Lunedi, "4").Ore);

    [Fact]
    public void Descrizione_PredefinitaPulitaETroncata()
    {
        Assert.Equal("Chiusura aziendale", RegoleCalendarioPresenze.Descrizione("CH", "  "));
        Assert.Equal("Festività", RegoleCalendarioPresenze.Descrizione("FE", null));
        Assert.Equal("Ponte di Pasqua", RegoleCalendarioPresenze.Descrizione("CH", "  Ponte   di Pasqua "));
        Assert.Equal(80, RegoleCalendarioPresenze.Descrizione("FE", new string('a', 200)).Length);
    }
}
