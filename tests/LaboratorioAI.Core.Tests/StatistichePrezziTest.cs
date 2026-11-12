using LaboratorioAI.Core.Modelli;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Prezzo medio pesato per cliente e controlli per matrice (12/11/2026).</summary>
public class StatistichePrezziTest
{
    [Fact]
    public void MedioPerCliente_OgniClienteContaUno()
    {
        // Cliente A con due commesse (20 e 30 → 25), cliente B a 50: media (25 + 50) / 2 = 37,5.
        var medio = StatistichePrezzi.MedioPerCliente(new (string, decimal?)[] { ("A", 20m), ("A", 30m), ("B", 50m), ("C", null), ("D", 0m) });
        Assert.Equal(37.5m, medio);
        Assert.Null(StatistichePrezzi.MedioPerCliente(new (string, decimal?)[] { ("A", null) }));
    }

    [Fact]
    public void PerMatrice_OrdinaPerCampioniEContaClienti()
    {
        var righe = new[]
        {
            new CampioniCommessaMatrice(10, "Olio - acidità", 1, "A", 20m, 5),
            new CampioniCommessaMatrice(10, "Olio - acidità", 2, "B", 40m, 1),
            new CampioniCommessaMatrice(10, "Olio - acidità", 3, "B", null, 2),
            new CampioniCommessaMatrice(11, "Olio - perossidi", 4, "A", 15m, 3),
        };
        var c = StatistichePrezzi.PerMatrice(righe);
        Assert.Equal(10, c[0].IdControllo);
        Assert.Equal(8, c[0].Campioni);
        Assert.Equal(2, c[0].Clienti);
        Assert.Equal(3, c[0].Commesse);
        Assert.Equal(20m, c[0].PrezzoMinimo);
        Assert.Equal(40m, c[0].PrezzoMassimo);
        Assert.Equal(30m, c[0].PrezzoMedio);
        Assert.Equal(11, c[1].IdControllo);
    }
}
