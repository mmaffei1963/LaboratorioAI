using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Ricerca documenti AZW: zone nel tempo, commessa di riserva, filtri e riepilogo (11/11/2026).</summary>
public class RicercaDocumentiAzwTest
{
    private static StoricoZonaCliente Z(string zona, DateTime dal, DateTime? al = null) =>
        new() { CodiceCliente = "1046", Zona = zona, DataDal = dal, DataAl = al };

    private static RigaRicercaDocumento R(bool costo, string tipo, string numero, decimal importo, string area = "04",
        string descrizione = "", string? controparte = null, string? codice = "1046", string? commessa = null, string? zona = null) =>
        new(costo, tipo, new DateTime(2026, 5, 1), numero, codice, controparte, descrizione, null, null, null, importo, area, area, null, commessa, zona);

    [Fact]
    public void ZonaAl_PrendeLaZonaValidaAllaData()
    {
        var st = new[] { Z("BAC", new DateTime(2026, 11, 11), new DateTime(2027, 3, 1)), Z("DEA", new DateTime(2027, 3, 1)) };
        Assert.Equal("BAC", RicercaDocumentiAzw.ZonaAl(st, new DateTime(2026, 12, 15)));
        Assert.Equal("DEA", RicercaDocumentiAzw.ZonaAl(st, new DateTime(2027, 3, 1)));
        Assert.Equal("DEA", RicercaDocumentiAzw.ZonaAl(st, new DateTime(2027, 9, 1)));
    }

    [Fact]
    public void ZonaAl_PrimaDellaPrimaRilevazione_UsaLaPrimaZona()
    {
        var st = new[] { Z("BAC", new DateTime(2026, 11, 11), new DateTime(2027, 3, 1)), Z("DEA", new DateTime(2027, 3, 1)) };
        Assert.Equal("BAC", RicercaDocumentiAzw.ZonaAl(st, new DateTime(2020, 1, 1)));
        Assert.Null(RicercaDocumentiAzw.ZonaAl(Array.Empty<StoricoZonaCliente>(), new DateTime(2020, 1, 1)));
    }

    [Fact]
    public void AllineaZone_ChiudeLaVecchiaEApreLaNuova()
    {
        var aperta = Z("BAC", new DateTime(2026, 11, 11));
        var nuove = RicercaDocumentiAzw.AllineaZone(new[] { aperta },
            new (string, string?)[] { ("1046", "dea"), ("2000", "REM"), ("2000", "XXX") }, new DateTime(2027, 3, 1, 22, 0, 0), out var modificate);

        Assert.Equal(new DateTime(2027, 3, 1), aperta.DataAl);
        Assert.Same(aperta, Assert.Single(modificate));
        Assert.Equal(2, nuove.Count);
        Assert.Contains(nuove, n => n.CodiceCliente == "1046" && n.Zona == "DEA" && n.DataDal == new DateTime(2027, 3, 1));
        Assert.Contains(nuove, n => n.CodiceCliente == "2000" && n.Zona == "REM");
    }

    [Fact]
    public void AllineaZone_ZonaUguale_NonCambiaNiente()
    {
        var aperta = Z("BAC", new DateTime(2026, 11, 11));
        var nuove = RicercaDocumentiAzw.AllineaZone(new[] { aperta }, new (string, string?)[] { ("1046", "BAC") }, new DateTime(2027, 1, 1), out var modificate);
        Assert.Empty(nuove);
        Assert.Empty(modificate);
        Assert.Null(aperta.DataAl);
    }

    [Fact]
    public void AllineaZone_CambioNelloStessoGiorno_CorreggeLaRiga()
    {
        var aperta = Z("BAC", new DateTime(2026, 11, 11));
        var nuove = RicercaDocumentiAzw.AllineaZone(new[] { aperta }, new (string, string?)[] { ("1046", "DEA") }, new DateTime(2026, 11, 11, 18, 0, 0), out _);
        Assert.Empty(nuove);
        Assert.Equal("DEA", aperta.Zona);
        Assert.Null(aperta.DataAl);
    }

    [Fact]
    public void CommessaDiRiserva_StessoCentroPoiDocumento()
    {
        var righe = new[] { ("04", "MAF"), ("05", "BAC"), ("05", "BAC") };
        Assert.Equal("BAC", RicercaDocumentiAzw.CommessaDiRiserva(righe, "05"));
        Assert.Null(RicercaDocumentiAzw.CommessaDiRiserva(righe, "FOR"));
        Assert.Equal("MAF", RicercaDocumentiAzw.CommessaDiRiserva(new[] { ("04", "MAF") }, "FOR"));
    }

    [Fact]
    public void ContieneTesto_PerICostiCercaAncheNelFornitore()
    {
        var parole = VociDocumentiVendita.Parole("amazon");
        Assert.True(RicercaDocumentiAzw.ContieneTesto(R(true, "FF", "1", 10, descrizione: "Fattura acquisto", controparte: "Amazon EU"), parole));
        Assert.False(RicercaDocumentiAzw.ContieneTesto(R(false, "FE", "1", 10, descrizione: "Corso", controparte: "Amazon EU"), parole));
        Assert.True(RicercaDocumentiAzw.ContieneTesto(R(false, "FE", "1", 10, descrizione: "Corso"), Array.Empty<string>()));
    }

    [Fact]
    public void Corrisponde_TrattinoSoloSenzaValore()
    {
        Assert.True(RicercaDocumentiAzw.Corrisponde(null, "-"));
        Assert.False(RicercaDocumentiAzw.Corrisponde("BAC", "-"));
        Assert.True(RicercaDocumentiAzw.Corrisponde("bac", "BAC"));
        Assert.True(RicercaDocumentiAzw.Corrisponde("BAC", null));
    }

    [Fact]
    public void Riepilogo_SommaRicaviCostiEPerArea()
    {
        var righe = new[]
        {
            R(false, "FE", "10/2026", 100m, "04"), R(false, "FE", "10/2026", 50m, "FOR"), R(false, "CE", "3/2026", -20m, "04"),
            R(true, "FF", "reg. 5/2026", 30m, "04", codice: "F1"),
        };
        var s = RicercaDocumentiAzw.Riepilogo(righe);
        Assert.Equal(130m, s.Ricavi);
        Assert.Equal(1, s.Fatture);
        Assert.Equal(1, s.NoteCredito);
        Assert.Equal(30m, s.Costi);
        Assert.Equal(100m, s.Differenza);
        Assert.Equal(1, s.Clienti);
        Assert.Equal(1, s.Fornitori);
        var lab = Assert.Single(s.PerArea, a => a.Area == "04");
        Assert.Equal(80m, lab.Ricavi);
        Assert.Equal(30m, lab.Costi);
        Assert.Equal("FOR", s.PerArea[0].Area);
    }
}
