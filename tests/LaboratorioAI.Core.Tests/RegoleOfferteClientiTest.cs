using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Offerte;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Offerte sul portale (17/11/2026): stesso controllo su più insediamenti, regole A-D.</summary>
public class RegoleOfferteClientiTest
{
    private static CommessaOffertaArcLab C(int id, string ins, int ctrl, string descr, decimal prezzo, int anno = 2025, int cliente = 1) =>
        new() { IdCliente = cliente, RagioneSociale = "Cliente", IdInsediamento = id, Insediamento = ins, IdCommessa = id, IdControllo = ctrl,
                Controllo = "Controllo " + ctrl, DescrIncarico = descr, Prezzo = prezzo, DataCommessa = new DateTime(anno, 1, 1) };

    [Fact]
    public void A_PrezzoEDescrizioneUguali_UnaRiga()
    {
        var e = RegoleOfferteClienti.Applica([C(1, "Sede", 10, "Acqua potabile", 45), C(2, "Filiale", 10, " acqua  POTABILE ", 45, 2024)]);
        var r = Assert.Single(e.Righe);
        Assert.Equal(45m, r.Prezzo);
        Assert.Equal(new DateTime(2024, 1, 1), r.DataIncarico);
        Assert.Equal(2, r.Insediamenti.Count);
        Assert.Empty(e.Anomalie);
    }

    [Fact]
    public void B_PrezzoUgualeDescrizioneDiversa_TutteEAnomalia()
    {
        var e = RegoleOfferteClienti.Applica([C(1, "Sede", 10, "Piadina micro", 30), C(2, "Filiale", 10, "Piadina microbiologia", 30)]);
        Assert.Equal(2, e.Righe.Count);
        var a = Assert.Single(e.Anomalie);
        Assert.Equal(RegoleOfferteClienti.CasoB, a.Caso);
        Assert.Equal(2, a.Commesse.Count);
    }

    [Fact]
    public void C_PrezzoEDescrizioneDiversi_TutteSenzaAnomalia()
    {
        var e = RegoleOfferteClienti.Applica([C(1, "Sede", 10, "Piadina base", 30), C(2, "Filiale", 10, "Piadina completa", 60)]);
        Assert.Equal(2, e.Righe.Count);
        Assert.Empty(e.Anomalie);
    }

    [Fact]
    public void D_PrezzoDiversoDescrizioneUguale_PrezzoPiuAltoEAnomalia()
    {
        var e = RegoleOfferteClienti.Applica([C(1, "Sede", 10, "Acqua potabile", 45), C(2, "Filiale", 10, "Acqua potabile", 50)]);
        var r = Assert.Single(e.Righe);
        Assert.Equal(50m, r.Prezzo);
        var a = Assert.Single(e.Anomalie);
        Assert.Equal(RegoleOfferteClienti.CasoD, a.Caso);
    }

    [Fact]
    public void ClientiEControlliDiversi_NonSiMescolano()
    {
        var e = RegoleOfferteClienti.Applica([C(1, "Sede", 10, "Acqua", 45), C(2, "Sede", 11, "Acqua", 50), C(3, "Altro", 10, "Acqua", 60, cliente: 2)]);
        Assert.Equal(3, e.Righe.Count);
        Assert.Empty(e.Anomalie);
    }

    [Fact]
    public void DescrizioneVuota_UsaIlNomeDelControllo()
    {
        var c = C(1, "Sede", 10, "", 45);
        Assert.Equal("Controllo 10", c.Incarico);
    }

    [Fact]
    public void UltimaModifica_PrimaDelDataIstat_21Gennaio2026ConEtichetta()
    {
        var e = RegoleOfferteClienti.Applica([C(1, "Sede", 10, "Acqua", 45, 2024), C(2, "Sede", 11, "Piadina", 30, 2026)]);
        var vecchia = Assert.Single(e.Righe, r => r.IdControllo == 10);
        Assert.Equal(new DateTime(2026, 1, 21), vecchia.UltimaModifica);
        Assert.Equal(RegoleOfferteClienti.EtichettaIstat2026, vecchia.EtichettaModifica);
        // incarico del 01/01/2026, comunque prima del 21/01: ISTAT
        Assert.Equal(new DateTime(2026, 1, 21), Assert.Single(e.Righe, r => r.IdControllo == 11).UltimaModifica);
        var (d, et) = RegoleOfferteClienti.UltimaModificaIniziale(new DateTime(2026, 3, 5));
        Assert.Equal(new DateTime(2026, 3, 5), d);
        Assert.Null(et);
    }
}
