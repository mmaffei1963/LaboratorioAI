using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Verifica del costruttore del nome file dei certificati esterni: convenzione
/// GG-MM-AA-lotto-campione-idInsediamento-cliente-prodotto-matrice-idMatrice-EST-LAB-idLabEsterno-RdP-(AVV|STD),
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
            IdInsediamento = "1234",
            Cliente = "I.C.I.", Prodotto = "integratori", Matrice = "integratore", IdMatrice = "45",
            LabAcronimo = "SIC", IdLabEsterno = "12", RdPEsterno = "26LA17574", ConAvviso = false
        };
        Assert.Equal(
            "19-08-26-7183-02-1234-I.C.I.-integratori-integratore-45-EST-SIC-12-26LA17574-STD",
            CostruttoreNomeCertificatoEsterno.Componi(d));
    }

    [Fact]
    public void Nome_EptaNord_nonconforme_ha_suffisso_AVV()
    {
        var d = new DatiNomeCertificatoEsterno
        {
            Data = new DateOnly(2026, 8, 21),
            Lotto = "7113", Campione = "02",
            IdInsediamento = "77",
            Cliente = "ALIMENTA", Prodotto = "saltimbocca", Matrice = "panespeciale", IdMatrice = "7",
            LabAcronimo = "EP", IdLabEsterno = "3", RdPEsterno = "26LB0112156", ConAvviso = true
        };
        var nome = CostruttoreNomeCertificatoEsterno.Componi(d);
        Assert.Equal(
            "21-08-26-7113-02-77-ALIMENTA-saltimbocca-panespeciale-7-EST-EP-3-26LB0112156-AVV",
            nome);
    }

    [Fact]
    public void Pulisci_sostituisce_slash_e_caratteri_vietati()
    {
        Assert.Equal("7113-02", CostruttoreNomeCertificatoEsterno.Pulisci("7113/02"));
        Assert.Equal("26FI0021853-005", CostruttoreNomeCertificatoEsterno.Pulisci("26FI0021853-005"));
        Assert.Equal("a-b-c", CostruttoreNomeCertificatoEsterno.Pulisci(@"a/b\c"));
    }

    [Fact]
    public void Id_mancanti_diventano_segnaposto_e_non_spostano_le_posizioni()
    {
        var d = new DatiNomeCertificatoEsterno
        {
            Data = new DateOnly(2026, 8, 21), Lotto = "7113", Campione = "02", IdInsediamento = "77",
            Cliente = "ALIMENTA", Prodotto = "piadina", Matrice = "prodotto da forno",
            LabAcronimo = "EP", RdPEsterno = "26LB0112156"
        };
        var nome = CostruttoreNomeCertificatoEsterno.Componi(d);
        Assert.Equal("21-08-26-7113-02-77-ALIMENTA-piadina-prodotto da forno-IDMATRICE-EST-EP-IDLAB-26LB0112156-STD", nome);
        Assert.True(CostruttoreNomeCertificatoEsterno.HaSegnaposti(nome));
    }

    [Fact]
    public void Ogni_trattino_del_nome_e_un_separatore_15_campi()
    {
        var d = new DatiNomeCertificatoEsterno
        {
            Data = new DateOnly(2026, 3, 20), Lotto = "949", Campione = "01", IdInsediamento = "9250",
            Cliente = "ERDIS MARCHE - ENTE REGIONALE", Prodotto = "salumi stagionati e/o fermentati",
            Matrice = "Acqua reflua", IdMatrice = "31", LabAcronimo = "CSA", IdLabEsterno = "5",
            RdPEsterno = "2604327-001", ConAvviso = true
        };
        var nome = CostruttoreNomeCertificatoEsterno.Componi(d);
        Assert.Equal("20-03-26-949-01-9250-ERDIS MARCHE ENTE REGIONALE-salumi stagionati e o fermentati-Acqua reflua-31-EST-CSA-5-2604327_001-AVV", nome);
        Assert.Equal(15, nome.Split('-').Length);
    }

    [Fact]
    public void Id_negativi_ArcLab_diventano_N_e_non_aggiungono_trattini()
    {
        var d = new DatiNomeCertificatoEsterno
        {
            Data = new DateOnly(2026, 8, 12), Lotto = "7028", Campione = "01", IdInsediamento = "732",
            Cliente = "I.C.I.", Prodotto = "molluschi", Matrice = "prodotto surgelato", IdMatrice = "40",
            LabAcronimo = "CRM", IdLabEsterno = "-867474552", RdPEsterno = "2609494"
        };
        var nome = CostruttoreNomeCertificatoEsterno.Componi(d);
        Assert.Equal("12-08-26-7028-01-732-I.C.I.-molluschi-prodotto surgelato-40-EST-CRM-N867474552-2609494-STD", nome);
        Assert.Equal(15, nome.Split('-').Length);
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
