using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Verifica del costruttore del nome file dei certificati esterni: convenzione
/// GG-MM-AA-lotto-campione-idInsediamento-accett-cliente-prodotto-matrice-EST-LAB-RdP-(AVV|STD),
/// pulizia dei caratteri vietati e riconoscimento dell'acronimo laboratorio.
/// </summary>
public class CertificatoEsternoTest
{
    [Fact]
    public void Nome_Sicural_conforme_ha_suffisso_STD()
    {
        var d = new DatiNomeCertificatoEsterno
        {
            Data = new DateOnly(2026, 8, 19),
            Lotto = "7183", Campione = "02",
            IdInsediamento = "1234", Accettazione = "71A26",
            Cliente = "I.C.I.", Prodotto = "integratori", Matrice = "integratore",
            LabAcronimo = "SIC", RdPEsterno = "26LA17574", ConAvviso = false
        };
        Assert.Equal(
            "19-08-26-7183-02-1234-71A26-I.C.I.-integratori-integratore-EST-SIC-26LA17574-STD",
            CostruttoreNomeCertificatoEsterno.Componi(d));
    }

    [Fact]
    public void Nome_EptaNord_nonconforme_ha_suffisso_AVV()
    {
        var d = new DatiNomeCertificatoEsterno
        {
            Data = new DateOnly(2026, 8, 21),
            Lotto = "7113", Campione = "02",
            IdInsediamento = "77", Accettazione = "70A26",
            Cliente = "ALIMENTA", Prodotto = "saltimbocca", Matrice = "panespeciale",
            LabAcronimo = "EP", RdPEsterno = "26LB0112156", ConAvviso = true
        };
        var nome = CostruttoreNomeCertificatoEsterno.Componi(d);
        Assert.Equal(
            "21-08-26-7113-02-77-70A26-ALIMENTA-saltimbocca-panespeciale-EST-EP-26LB0112156-AVV",
            nome);
    }

    [Fact]
    public void Pulisci_sostituisce_slash_e_caratteri_vietati()
    {
        Assert.Equal("7113-02", CostruttoreNomeCertificatoEsterno.Pulisci("7113/02"));
        Assert.Equal("26FI0021853-005", CostruttoreNomeCertificatoEsterno.Pulisci("26FI0021853-005"));
        Assert.Equal("a-b-c", CostruttoreNomeCertificatoEsterno.Pulisci(@"a/b\c"));
    }

    [Theory]
    [InlineData("SICURAL S.R.L. CONSORTILE", "SIC")]
    [InlineData("EPTA NORD srl", "EP")]
    [InlineData("Gruppo C.S.A. S.p.A.", "CSA")]
    [InlineData("Istituto Zooprofilattico Sperimentale delle Venezie", "IZSVE")]
    public void Acronimo_riconosce_i_laboratori(string nome, string atteso)
    {
        Assert.Equal(atteso, CostruttoreNomeCertificatoEsterno.AcronimoDa(nome));
    }
}
