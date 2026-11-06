using LaboratorioAI.Core.Andamento;
using LaboratorioAI.Core.Direzione;
using LaboratorioAI.Core.Entita;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Clienti tolti dalla lista (14/10/2026): rientro automatico, storico al cliente che acquisisce, Direzione.</summary>
public class UsciteClientiTest
{
    private static FatturatoMensile F(string c, int a, int m, decimal lab) => new(c, a, m, lab, 0m, 0m);

    [Fact]
    public void Acquisito_StoricoSommatoAlSuccessore_AncheACatena()
    {
        var uscite = new[]
        {
            new UscitaCliente("FRESCO", MotivoUscitaCliente.AcquisitoDaCliente, "RICCIONE", new DateTime(2026, 10, 14)),
            new UscitaCliente("RICCIONE", MotivoUscitaCliente.AcquisitoDaCliente, "GRUPPO", new DateTime(2026, 10, 14)),
            new UscitaCliente("CHIUSO", MotivoUscitaCliente.CessataAttivita, null, new DateTime(2026, 10, 14)),
        };
        var fatt = new[] { F("FRESCO", 2025, 3, 1000m), F("RICCIONE", 2025, 3, 500m), F("GRUPPO", 2026, 9, 2000m), F("CHIUSO", 2025, 1, 300m) };
        var rientrati = UsciteClienti.Rientrati(uscite, fatt, []);
        Assert.Empty(rientrati);
        var dest = UsciteClienti.Destinazioni(uscite, rientrati);
        Assert.Equal("GRUPPO", dest["FRESCO"]);
        Assert.Equal("GRUPPO", dest["RICCIONE"]);
        Assert.False(dest.ContainsKey("CHIUSO"));
        var unito = UsciteClienti.Unisci(fatt, dest);
        Assert.Equal(1500m, unito.Single(f => f.Cliente == "GRUPPO" && f.Anno == 2025).Laboratorio);
        // Nella scheda del cliente acquisito il suo storico resta suo.
        Assert.Equal(1000m, UsciteClienti.Unisci(fatt, dest, "FRESCO").Single(f => f.Cliente == "FRESCO").Laboratorio);
    }

    [Fact]
    public void Rientrato_SeHaAttivitaDopoIlMeseDiUscita()
    {
        var u = new[] { new UscitaCliente("A", MotivoUscitaCliente.AcquisitoDaCliente, "B", new DateTime(2026, 5, 20)) };
        Assert.Empty(UsciteClienti.Rientrati(u, [F("A", 2026, 5, 100m)], []));          // stesso mese: no
        var r = UsciteClienti.Rientrati(u, [], [new CampioniMensili("A", 2026, 7, 1, "x", 2, 50m, new DateTime(2026, 7, 3), false)]);
        Assert.Single(r);
        Assert.Empty(UsciteClienti.Destinazioni(u, r));                                   // rientrato: niente somma
    }

    [Fact]
    public void Territorio_AcquisitiNonSonoPersi()
    {
        var righe = new[] { new FatturatoClienteAnno(2025, "A", 100m), new FatturatoClienteAnno(2025, "B", 100m), new FatturatoClienteAnno(2026, "C", 50m) };
        var t = MotoreTerritorio.Totale(righe, [2026], new HashSet<string> { "A" });
        Assert.Equal(1, t[0].ClientiPersi);
        Assert.Equal(1, t[0].ClientiAcquisiti);
    }
}
