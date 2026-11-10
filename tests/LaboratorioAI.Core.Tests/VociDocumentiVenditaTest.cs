using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Ricomposizione delle voci delle fatture AZW (10/11/2026).</summary>
public class VociDocumentiVenditaTest
{
    private static RigaDocumentoAzw R(string tipo, string num, int riga, int tipoRiga, string testo, decimal q = 0, decimal p = 0, decimal s1 = 0) =>
        new(tipo, 2026, num, new DateTime(2026, 10, 30), "1046", "RICCIONE BEVANDE S.R.L.", riga, tipoRiga, null, testo, null, null, q, p, s1, 0, 0, "FOR", "O62", null);

    [Fact]
    public void RigheDiTesto_SiUnisconoAllaVoceConIlPrezzo()
    {
        var voci = VociDocumentiVendita.Componi(new[]
        {
            R("FE", "4882", 10, 0, "Corso di formazione addetti sicurezza", 1, 30),
            R("FE", "4882", 20, 1, "solo lezione generale"),
            R("FE", "4882", 30, 1, "modalità e-learning"),
        });
        var v = Assert.Single(voci);
        Assert.Equal("Corso di formazione addetti sicurezza solo lezione generale modalità e-learning", v.Descrizione);
        Assert.Equal(30m, v.Importo);
    }

    [Fact]
    public void QuantitaSconti_ENoteDiCreditoInNegativo()
    {
        var voci = VociDocumentiVendita.Componi(new[]
        {
            R("FE", "4886", 10, 0, "Corso", 3, 60),
            R("FE", "4886", 20, 0, "Altro servizio", 2, 50, 10),
            R("CE", "12", 10, 0, "Storno corso e-learning", 1, 50),
        });
        Assert.Equal(180m, voci.Single(v => v.Descrizione == "Corso").Importo);
        var scontata = voci.Single(v => v.Descrizione == "Altro servizio");
        Assert.Equal(90m, scontata.Importo);
        Assert.Equal("10", scontata.Sconto);
        Assert.Equal(-50m, voci.Single(v => v.TipoDocumento == "CE").Importo);
    }

    [Fact]
    public void Parole_SeparateDaPuntoEVirgola() =>
        Assert.Equal(new[] { "e-learning", "FAD" }, VociDocumentiVendita.Parole(" e-learning ; FAD; ;fad"));
}
