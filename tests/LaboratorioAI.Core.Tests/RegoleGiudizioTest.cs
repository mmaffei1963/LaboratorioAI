using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Validazione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Validazione da LaboratorioAI (18/10/2026): un campione, un solo giudizio; 212 automatico solo senza richiesta del cliente.</summary>
public class RegoleGiudizioTest
{
    private static StatoCampioneArcLab Stato(bool giudizioConf = false) => new() { IdCampione = 1, Prove = 3, GiudizioConf = giudizioConf };
    private static readonly List<GiudizioArcLab> Proposti = [new() { IdGiudizio = 300, CodGiudizio = "2073 ok" }];

    [Fact]
    public void SenzaRichiesta_NessunaScelta_212Automatico()
    {
        var s = RegoleGiudizio.Scegli(Stato(), null, Proposti);
        Assert.Equal(212, s.IdGiudizio);
        Assert.True(s.Automatico);
    }

    [Fact]
    public void ConRichiesta_NessunaScelta_Errore()
    {
        var s = RegoleGiudizio.Scegli(Stato(true), null, Proposti);
        Assert.Equal((int?)null, s.IdGiudizio);
        Assert.False(s.Errore is null);
    }

    [Fact]
    public void ConRichiesta_212SceltoAMano_Ammesso()
    {
        var s = RegoleGiudizio.Scegli(Stato(true), 212, Proposti);
        Assert.Equal(212, s.IdGiudizio);
        Assert.False(s.Automatico);
    }

    [Fact]
    public void GiudizioFuoriMatrice_Rifiutato() => Assert.False(RegoleGiudizio.Scegli(Stato(), 999, Proposti).Errore is null);

    [Fact]
    public void Blocchi()
    {
        Assert.True(RegoleGiudizio.MotivoBlocco(Stato()) is null);
        var chiusa = Stato(); chiusa.AnalisiChiusa = true;
        Assert.False(RegoleGiudizio.MotivoBlocco(chiusa) is null);
        var conGiudizio = Stato(); conGiudizio.Giudizi.Add(new GiudizioArcLab { IdGiudizio = 300, CodGiudizio = "x" });
        Assert.False(RegoleGiudizio.MotivoBlocco(conGiudizio) is null);
        var nonVerificate = Stato(); nonVerificate.ProveNonVerificate = 1;
        Assert.False(RegoleGiudizio.MotivoBlocco(nonVerificate) is null);
        Assert.True(RegoleGiudizio.ChiedeConferma("rosso"));
        Assert.True(RegoleGiudizio.ChiedeConferma("viola"));
        Assert.False(RegoleGiudizio.ChiedeConferma("arancione"));
    }
}
