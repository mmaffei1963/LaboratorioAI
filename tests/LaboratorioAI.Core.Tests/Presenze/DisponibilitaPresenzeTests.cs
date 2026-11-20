using System;
using System.Collections.Generic;
using LaboratorioAI.Core.Presenze;
using Xunit;

namespace LaboratorioAI.Core.Tests.Presenze;

public class DisponibilitaPresenzeTests
{
    private static CalcoloGiorno Giorno(double contratto = 8, IReadOnlyList<TimbraturaGiorno>? timbrature = null,
        IReadOnlyList<AssenzaGiorno>? assenze = null, bool festivo = false, string chiusura = "", double oreChiusura = 0, bool weekend = false) =>
        new()
        {
            Data = new DateOnly(2027, 3, 3),
            Contratto = contratto,
            Timbrature = timbrature ?? Array.Empty<TimbraturaGiorno>(),
            Assenze = assenze ?? Array.Empty<AssenzaGiorno>(),
            Festivo = festivo,
            FestivoDescrizione = festivo ? "Festa" : "",
            ChiusuraDescrizione = chiusura,
            Chiusura = oreChiusura,
            Weekend = weekend,
        };

    [Fact]
    public void UltimaTimbraturaEntrata_InServizioConOraELuogo()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(timbrature: new[] { new TimbraturaGiorno("08:02", "E"), new TimbraturaGiorno("13:00", "U"), new TimbraturaGiorno("14:01", "E", " da casa ") }));
        Assert.Equal(StatoDisponibilita.InServizio, d.Stato);
        Assert.Equal("14:01", d.Ora);
        Assert.Equal("da casa", d.Luogo);
    }

    [Fact]
    public void UltimaTimbraturaUscita_UscitoSenzaLuogo()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(timbrature: new[] { new TimbraturaGiorno("08:02", "E", "cliente"), new TimbraturaGiorno("13:00", "U") }));
        Assert.Equal(StatoDisponibilita.Uscito, d.Stato);
        Assert.Equal("13:00", d.Ora);
        Assert.True(d.Luogo is null);
    }

    [Fact]
    public void LeTimbratureSiOrdinanoPerOra()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(timbrature: new[] { new TimbraturaGiorno("13:00", "U"), new TimbraturaGiorno("08:00", "E") }));
        Assert.Equal(StatoDisponibilita.Uscito, d.Stato);
    }

    [Fact]
    public void LuogoVuotoNonSiMostra()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(timbrature: new[] { new TimbraturaGiorno("08:00", "E", "   ") }));
        Assert.True(d.Luogo is null);
    }

    [Fact]
    public void NessunaTimbratura_NonTimbrato()
    {
        Assert.Equal(StatoDisponibilita.NonTimbrato, DisponibilitaPresenze.Valuta(Giorno()).Stato);
    }

    [Theory]
    [InlineData("FER")]
    [InlineData("MAL")]
    [InlineData("104")]
    public void AssenzaDiGiornataIntera_AssenteQualunqueCausale(string causale)
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(assenze: new[] { new AssenzaGiorno(causale, 8) }));
        Assert.Equal(StatoDisponibilita.Assente, d.Stato);
        Assert.True(d.Motivo is null);
    }

    [Fact]
    public void PermessoDiPocheOre_NonTimbratoConNotaParziale()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(assenze: new[] { new AssenzaGiorno("PER", 2) }));
        Assert.Equal(StatoDisponibilita.NonTimbrato, d.Stato);
        Assert.True(d.PermessoParziale);
    }

    [Fact]
    public void PermessoDiPocheOreConTimbratura_InServizioConNota()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(timbrature: new[] { new TimbraturaGiorno("08:00", "E") }, assenze: new[] { new AssenzaGiorno("PER", 2) }));
        Assert.Equal(StatoDisponibilita.InServizio, d.Stato);
        Assert.True(d.PermessoParziale);
    }

    [Fact]
    public void ChiHaTimbratoEInServizioAncheConAssenzaIntera()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(timbrature: new[] { new TimbraturaGiorno("08:00", "E") }, assenze: new[] { new AssenzaGiorno("FER", 8) }));
        Assert.Equal(StatoDisponibilita.InServizio, d.Stato);
        Assert.False(d.PermessoParziale);
    }

    [Fact]
    public void AssenzaConContrattoZero_Assente()
    {
        // prima dell'avvio dei conteggi le ore da contratto sono zero, ma l'assenza registrata si vede
        Assert.Equal(StatoDisponibilita.Assente, DisponibilitaPresenze.Valuta(Giorno(contratto: 0, assenze: new[] { new AssenzaGiorno("FER", 8) })).Stato);
    }

    [Fact]
    public void Festivo_NonPrevistoConDescrizione()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(contratto: 0, festivo: true));
        Assert.Equal(StatoDisponibilita.NonPrevisto, d.Stato);
        Assert.Equal("Festa", d.Motivo);
    }

    [Fact]
    public void ChiusuraIntera_NonPrevisto()
    {
        var d = DisponibilitaPresenze.Valuta(Giorno(chiusura: "Ponte", oreChiusura: 8));
        Assert.Equal(StatoDisponibilita.NonPrevisto, d.Stato);
        Assert.Equal("Ponte", d.Motivo);
    }

    [Fact]
    public void ChiusuraParziale_NonTimbrato()
    {
        Assert.Equal(StatoDisponibilita.NonTimbrato, DisponibilitaPresenze.Valuta(Giorno(chiusura: "Mezza", oreChiusura: 4)).Stato);
    }

    [Fact]
    public void GiornoSenzaOre_NonPrevisto()
    {
        Assert.Equal("Fine settimana", DisponibilitaPresenze.Valuta(Giorno(contratto: 0, weekend: true)).Motivo);
        Assert.Equal("Non lavora oggi", DisponibilitaPresenze.Valuta(Giorno(contratto: 0)).Motivo);
    }

    [Fact]
    public void ContaInServizio()
    {
        var righe = new List<RigaDisponibilita>
        {
            new(1, "A", "#fff", 1, "Cattolica", new DisponibilitaGiorno(StatoDisponibilita.InServizio, "08:00", null, false, null)),
            new(2, "B", "#fff", 1, "Cattolica", new DisponibilitaGiorno(StatoDisponibilita.Uscito, "13:00", null, false, null)),
            new(3, "C", "#fff", 2, "Pesaro", new DisponibilitaGiorno(StatoDisponibilita.InServizio, "09:00", "da casa", false, null)),
        };
        Assert.Equal(2, DisponibilitaPresenze.InServizio(righe));
    }
}
