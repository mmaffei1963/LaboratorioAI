using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Tarature;
using Xunit;

namespace LaboratorioAI.Core.Tests;

public class RinnoviTermometriTest
{
    private static TermometroEsterno T(int id, string cliente, string tipologia, string data, bool escluso = false, string? rinnovo = null) => new()
    {
        IdTermometroEsterno = id, Cliente = cliente, Tipologia = tipologia, DataTaratura = DateTime.Parse(data),
        DataScadenza = DateTime.Parse(data).AddYears(1), EscludiScadenze = escluso,
        DataRinnovo = rinnovo is null ? null : DateTime.Parse(rinnovo), Attivo = true
    };

    [Theory]
    [InlineData("RICCIONE PIADINA", "Riccione Piadina S.r.l.", true)]
    [InlineData("ECOTECH", "ECOTECH SRL", true)]
    [InlineData("L'ACQUAVIVA", "L ACQUAVIVA SNC DI ROSSI & C.", true)]
    [InlineData("ECOTECH", "ECO", false)]
    [InlineData("BAR SOLE", "BAR LUNA", false)]
    public void StessoCliente(string a, string b, bool atteso) => Assert.Equal(atteso, RinnoviTermometri.StessoCliente(a, b));

    [Theory]
    [InlineData("SONDA", "Sonda Testo 905 matr. 123", true)]
    [InlineData("PIROMETRO", "SONDA", false)]
    [InlineData("", "SONDA", false)]
    public void StessaTipologia(string a, string b, bool atteso) => Assert.Equal(atteso, RinnoviTermometri.StessaTipologia(a, b));

    [Fact]
    public void Candidati_StessoClienteStessaTipologiaInUso()
    {
        var nuova = T(10, "Ecotech S.r.l.", "SONDA", "2026-11-03");
        var archivio = new[]
        {
            T(1, "ECOTECH", "SONDA", "2025-10-25"),
            T(2, "ECOTECH", "SONDA", "2021-10-25"),
            T(3, "ECOTECH", "PIROMETRO", "2025-10-25"),                 // altra tipologia
            T(4, "ALTRO", "SONDA", "2025-10-25"),                       // altro cliente
            T(5, "ECOTECH", "SONDA", "2024-10-25", escluso: true),      // dismessa
            T(6, "ECOTECH", "SONDA", "2023-10-25", rinnovo: "2024-10-25"), // già rinnovata
            nuova
        };
        var c = RinnoviTermometri.Candidati(nuova, archivio);
        Assert.Equal(new[] { 1, 2 }, c.Select(x => x.IdTermometroEsterno).ToArray());
    }

    [Theory]
    [InlineData("SONDA", TipoTermometro.Sonda)]
    [InlineData("Termometro a gabbia", TipoTermometro.Gabbia)]
    [InlineData("TERMOMETRO A INFRAROSSI", TipoTermometro.Pirometro)]
    [InlineData("PIROMETRO", TipoTermometro.Pirometro)]
    [InlineData("Data logger Testo 174", TipoTermometro.Datalogger)]
    [InlineData("TERMOMETRO", TipoTermometro.NonIndicato)]
    public void TipoDaTipologia(string tipologia, TipoTermometro atteso) => Assert.Equal(atteso, RinnoviTermometri.TipoDaTipologia(tipologia));

    [Fact]
    public void StessoTipo_UsaIlTipoQuandoCe()
    {
        var a = T(1, "ECOTECH", "Testo 905", "2025-10-25"); a.TipoTermometro = TipoTermometro.Sonda;
        var b = T(2, "ECOTECH", "SONDA", "2024-10-25");                 // ricavato: a sonda
        var c = T(3, "ECOTECH", "Testo 905", "2024-10-25"); c.TipoTermometro = TipoTermometro.Gabbia;
        Assert.True(RinnoviTermometri.StessoTipo(a, b));
        Assert.False(RinnoviTermometri.StessoTipo(a, c));
    }

    [Fact]
    public void ScadutaDaOltre12Mesi_ContaLaScadenza()
    {
        var oggi = new DateTime(2026, 11, 3);
        Assert.True(RinnoviTermometri.ScadutaDaOltre(new DateTime(2025, 10, 1), oggi));
        Assert.False(RinnoviTermometri.ScadutaDaOltre(new DateTime(2025, 12, 1), oggi));
        Assert.False(RinnoviTermometri.ScadutaDaOltre(null, oggi));
    }

    [Fact]
    public void Stato()
    {
        Assert.Equal(RinnoviTermometri.InUso, RinnoviTermometri.Stato(T(1, "A", "S", "2026-01-01")));
        Assert.Equal(RinnoviTermometri.Dismessa, RinnoviTermometri.Stato(T(1, "A", "S", "2026-01-01", escluso: true)));
        Assert.Equal(RinnoviTermometri.Rinnovata, RinnoviTermometri.Stato(T(1, "A", "S", "2026-01-01", rinnovo: "2026-06-01")));
    }
}
