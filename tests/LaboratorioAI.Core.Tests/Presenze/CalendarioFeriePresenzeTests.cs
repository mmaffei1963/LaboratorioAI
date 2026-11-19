using System;
using System.Collections.Generic;
using LaboratorioAI.Core.Presenze;
using Xunit;

namespace LaboratorioAI.Core.Tests.Presenze;

public class CalendarioFeriePresenzeTests
{
    private static readonly LavoratorePresenze Anna = new("Anna", "#ffc000", new double[] { 8, 8, 8, 8, 8, 0, 0 });
    private static readonly DateOnly Lunedi = new(2027, 3, 1);
    private static readonly DateOnly Sabato = new(2027, 3, 6);
    private static readonly IReadOnlyList<AssenzaGiorno> Nessuna = Array.Empty<AssenzaGiorno>();

    [Theory]
    [InlineData("FER", true)]
    [InlineData("PER", true)]
    [InlineData("MAL", false)]
    [InlineData("104", false)]
    [InlineData("DS", false)]
    [InlineData("SEG", false)]
    [InlineData("ALT", false)]
    public void Condivisibile_SoloFerieEPermessi(string causale, bool atteso) =>
        Assert.Equal(atteso, CalendarioFeriePresenze.Condivisibile(causale));

    [Fact]
    public void Ferie_OreNelColoreDelLavoratoreConNota()
    {
        var c = CalendarioFeriePresenze.Cella(Anna, Lunedi, new[] { new AssenzaGiorno("FER", 8) }, null);
        Assert.NotNull(c);
        Assert.Equal(8.0, c!.Ore);
        Assert.Equal("FFFFC000", c.Sfondo);
        Assert.True(c.Grassetto);
        Assert.Equal("Ferie 8:00", c.Nota);
    }

    [Fact]
    public void FerieEPermessoNelloStessoGiorno_OreSommate()
    {
        var c = CalendarioFeriePresenze.Cella(Anna, Lunedi, new[] { new AssenzaGiorno("FER", 4), new AssenzaGiorno("PER", 2) }, null);
        Assert.Equal(6.0, c!.Ore);
        Assert.True(c.Nota.Contains("Ferie"));
        Assert.True(c.Nota.Contains("Permesso"));
    }

    [Fact]
    public void Malattia_NonSiVedeENonRivelaIlMotivo()
    {
        var c = CalendarioFeriePresenze.Cella(Anna, Lunedi, new[] { new AssenzaGiorno("MAL", 8) }, null);
        Assert.Null(c);
        var conFerie = CalendarioFeriePresenze.Cella(Anna, Lunedi, new[] { new AssenzaGiorno("FER", 4), new AssenzaGiorno("104", 4) }, null);
        Assert.Equal(4.0, conFerie!.Ore);
        Assert.False(conFerie.Nota.Contains("104"));
    }

    [Fact]
    public void Festivita_GialloFE()
    {
        var c = CalendarioFeriePresenze.Cella(Anna, Lunedi, Nessuna, new VoceCalendario("FE", "Capodanno"));
        Assert.Equal("FE", c!.Testo);
        Assert.Equal(ContenutoCellaPresenze.Giallo, c.Sfondo);
        Assert.Equal("Capodanno", c.Nota);
    }

    [Fact]
    public void Chiusura_OreDelContrattoOParziali_Rosso()
    {
        var intera = CalendarioFeriePresenze.Cella(Anna, Lunedi, Nessuna, new VoceCalendario("CH", "Ponte"));
        Assert.Equal(8.0, intera!.Ore);
        Assert.Equal(ContenutoCellaPresenze.Rosso, intera.Sfondo);
        Assert.Equal("Ponte", intera.Nota);
        var parziale = CalendarioFeriePresenze.Cella(Anna, Lunedi, Nessuna, new VoceCalendario("CH", null, 4));
        Assert.Equal(4.0, parziale!.Ore);
        Assert.Equal("Chiusura aziendale", parziale.Nota);
        // una chiusura di sabato per chi il sabato non lavora non porta ore: resta il fine settimana
        Assert.Equal("FS", CalendarioFeriePresenze.Cella(Anna, Sabato, Nessuna, new VoceCalendario("CH", "Ponte"))!.Testo);
    }

    [Fact]
    public void FineSettimana_FSSoloSeSenzaOreDaContratto()
    {
        var c = CalendarioFeriePresenze.Cella(Anna, Sabato, Nessuna, null);
        Assert.Equal("FS", c!.Testo);
        var sabatoLavorato = new LavoratorePresenze("Bruno", "#00ff00", new double[] { 8, 8, 8, 8, 8, 4, 0 });
        Assert.Null(CalendarioFeriePresenze.Cella(sabatoLavorato, Sabato, Nessuna, null));
    }

    [Fact]
    public void GiornoFeriale_SenzaNulla_Null() =>
        Assert.Null(CalendarioFeriePresenze.Cella(Anna, Lunedi, Nessuna, null));

    [Fact]
    public void FerieSopraLaFestivita_VinconoLeFerie()
    {
        var c = CalendarioFeriePresenze.Cella(Anna, Lunedi, new[] { new AssenzaGiorno("FER", 8) }, new VoceCalendario("FE", "Festa"));
        Assert.Equal(8.0, c!.Ore);
    }

    [Fact]
    public void Assente_SoloConFerieOPermessi()
    {
        Assert.True(CalendarioFeriePresenze.Assente(new[] { new AssenzaGiorno("FER", 2) }));
        Assert.False(CalendarioFeriePresenze.Assente(new[] { new AssenzaGiorno("MAL", 8) }));
        Assert.False(CalendarioFeriePresenze.Assente(Nessuna));
    }
}
