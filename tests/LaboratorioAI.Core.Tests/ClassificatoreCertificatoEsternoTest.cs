using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Servizi;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Certificati esterni solo esterni / misti e controllo dei valori in ArcLab (12/10/2026).</summary>
public class ClassificatoreCertificatoEsternoTest
{
    private static ProvaCampioneArcLab P(int id, string par, int? lab, bool valore = false) =>
        new() { IdProva = id, IdCampione = 100, Parametro = par, IdLabEsterno = lab, ValoreInserito = valore };

    [Fact]
    public void SoloProveEsterne_SoloEsterni()
    {
        Assert.Equal(TipoCertificatoEsterno.SoloEsterni,
            ClassificatoreCertificatoEsterno.Classifica([P(1, "Legionella", 7), P(2, "Mercurio", 7)]));
    }

    [Fact]
    public void UnaProvaNostra_Misto_AncheConLabZero()
    {
        Assert.Equal(TipoCertificatoEsterno.Misto,
            ClassificatoreCertificatoEsterno.Classifica([P(1, "Mercurio", 7), P(2, "Carica batterica", null)]));
        Assert.Equal(TipoCertificatoEsterno.Misto,
            ClassificatoreCertificatoEsterno.Classifica([P(1, "Mercurio", 7), P(2, "Istamina", 0)]));
    }

    [Fact]
    public void NessunaProva_NonDeterminabile()
    {
        Assert.Null(ClassificatoreCertificatoEsterno.Classifica([]));
    }

    [Fact]
    public void StatoInserimento_SoloProveDelLaboratorioDelCertificato()
    {
        ProvaCampioneArcLab[] prove = [P(1, "Mercurio", 7, true), P(2, "Cadmio", 7), P(3, "Diossine", 9), P(4, "Carica batterica", null)];
        var st = ClassificatoreCertificatoEsterno.StatoInserimento(prove, 7);
        Assert.Equal(2, st.Prove);
        Assert.Equal(1, st.Inserite);
        Assert.Equal("Cadmio", Assert.Single(st.Mancanti));
        Assert.False(st.Completo);

        // Laboratorio non noto: tutte le esterne, mai le nostre.
        var tutte = ClassificatoreCertificatoEsterno.StatoInserimento(prove, null);
        Assert.Equal(3, tutte.Prove);

        var fatto = ClassificatoreCertificatoEsterno.StatoInserimento([P(1, "Mercurio", 7, true), P(4, "Carica batterica", null)], 7);
        Assert.True(fatto.Completo);
    }
}
