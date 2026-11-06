using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>22/10/2026: la contabilità analitica non si riscrive con un file AZW più vecchio di quanto già importato.</summary>
public class ControlloContabilitaAnaliticaTest
{
    private static readonly DateTime Ott = new(2026, 10, 21);

    [Fact]
    public void PrimaImportazioneSempreAmmessa() =>
        Assert.Null(ControlloContabilitaAnalitica.Verifica(null, 0, new DateTime(2025, 12, 31), 100));

    [Fact]
    public void FileFermoAlloScorsoAnnoBloccato() =>
        Assert.NotNull(ControlloContabilitaAnalitica.Verifica(Ott, 50000, new DateTime(2025, 12, 31), 50000));

    [Fact]
    public void FileVuotoBloccato() =>
        Assert.NotNull(ControlloContabilitaAnalitica.Verifica(Ott, 50000, null, 0));

    [Fact]
    public void MoltoMenoRigheBloccato() =>
        Assert.NotNull(ControlloContabilitaAnalitica.Verifica(Ott, 50000, Ott, 40000));

    [Fact]
    public void PiccoleDifferenzeAmmesse()
    {
        Assert.Null(ControlloContabilitaAnalitica.Verifica(Ott, 50000, Ott.AddDays(-3), 49800));
        Assert.Null(ControlloContabilitaAnalitica.Verifica(Ott, 50000, Ott.AddDays(1), 50100));
    }
}
