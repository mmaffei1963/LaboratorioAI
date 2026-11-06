using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Compensi soci (22/10/2026): accessori e maturato non pagato per area.</summary>
public class MotoreAccessoriAreeTest
{
    private static readonly List<AreaConsuntivo> Aree = MotoreConsuntivo.AreeStandard.ToList();

    private static ContoSocio Conto(int id, decimal indennita = 0m, decimal rspp = 0m, decimal resp = 0m, decimal premio = 0m,
        decimal totale = 0m, decimal recupero = 0m, decimal differenza = 0m) =>
        new(id, "S" + id, 0m, 0m, resp, resp, rspp, indennita, resp + rspp + indennita, 0m, 0m, recupero, premio, totale, 0m, 0m, 0m, differenza);

    private static RisultatoCompensi Risultato(params ContoSocio[] soci) =>
        new(2025, soci, [], 0m, 0m, false, 0m, 0m, [new PremioLaboratorio("Dip", 1000m, null), new PremioLaboratorio("Socio", 500m, 2)]);

    [Fact]
    public void QuotaFissaAmministrazioneInProporzioneAlCompenso()
    {
        var q = MotoreAccessoriAree.Quote(new RipartizioneSocio(16000m, new Dictionary<string, decimal> { ["04"] = 100m }), 80000m);
        Assert.Equal(0.2m, q["03"]);
        Assert.Equal(0.8m, q["04"]);
    }

    [Fact]
    public void IndennitaAlLaboratorioRsppAllaSicurezzaRestoSecondoRipartizione()
    {
        var t = new TotaliConsuntivo();
        t.Ricavi["04"] = 100000m; t.Utile["04"] = 50000m; t.UtileConContributo["04"] = 40000m;
        var r = Risultato(
            Conto(1, indennita: 20000m, resp: 10000m, premio: 5000m, totale: 100000m, recupero: 20000m, differenza: 30000m),
            Conto(2, rspp: 16000m, resp: 10000m, totale: 60000m, differenza: 5000m));
        var rip = new Dictionary<int, RipartizioneSocio>
        {
            [1] = new(16000m, new Dictionary<string, decimal> { ["04"] = 100m }),   // compenso 80.000 → 20% AMM
            [2] = new(0m, new Dictionary<string, decimal> { ["05"] = 50m, ["FOR"] = 50m }),
        };
        var a = MotoreAccessoriAree.Calcola(r, rip, Aree, t).ToDictionary(x => x.Area);
        Assert.Equal(20000m, a["04"].IndennitaLaboratorio);
        Assert.Equal(1000m, a["04"].PremiLaboratorio);           // solo il premio non collegato a un socio
        Assert.Equal(16000m, a["05"].Rspp);
        Assert.Equal(8000m + 0m, a["04"].ResponsabileArea);      // 80% di 10.000 del socio 1
        Assert.Equal(2000m, a["03"].ResponsabileArea);
        Assert.Equal(5000m, a["05"].ResponsabileArea);           // metà del socio 2
        Assert.Equal(24000m, a["04"].MaturatoNonPagato);         // differenza 30.000 × 80%
        Assert.Equal(2500m, a["FOR"].MaturatoNonPagato);
        Assert.Equal(a["03"].TotaleAccessori / 5m, a["04"].QuotaAccessoriAmm, 2);
    }

    [Fact]
    public void DifferenzaConRipartizioneApposita()
    {
        var r = Risultato(Conto(1, resp: 10000m, totale: 76500m, differenza: 12970m));
        var rip = new Dictionary<int, RipartizioneSocio>
        {
            [1] = new(6000m, new Dictionary<string, decimal> { ["04"] = 100m }) { PercentualiDifferenza = new Dictionary<string, decimal> { ["06"] = 100m } },
        };
        var a = MotoreAccessoriAree.Calcola(r, rip, Aree, new TotaliConsuntivo()).ToDictionary(x => x.Area);
        Assert.Equal(12970m, a["06"].MaturatoNonPagato);
        Assert.Equal(0m, a["04"].MaturatoNonPagato);
        Assert.True(a["04"].ResponsabileArea > 0m);              // gli accessori restano al Laboratorio
    }

    [Fact]
    public void RettificaDelConsuntivoConICompensiDaPagare()
    {
        var r = Risultato(
            Conto(1, resp: 10000m, totale: 100000m, recupero: 20000m, differenza: 30000m),
            Conto(2, totale: 60000m, differenza: 5000m));
        var rip = new Dictionary<int, RipartizioneSocio>
        {
            [1] = new(16000m, new Dictionary<string, decimal> { ["04"] = 100m }),   // 20% AMM
            [2] = new(0m, new Dictionary<string, decimal> { ["05"] = 100m }),
        };
        var dp = MotoreAccessoriAree.DaPagarePerArea(r, rip);
        Assert.Equal(6000m, dp["03"]);
        Assert.Equal(24000m, dp["04"]);
        Assert.Equal(5000m, dp["05"]);

        var t = new TotaliConsuntivo();
        foreach (var a in Aree) { t.Utile[a.Codice] = 10000m; t.UtileConContributo[a.Codice] = 8000m; }
        t.Utile["03"] = -10000m; t.UtileConContributo["03"] = 0m;
        var x = MotoreAccessoriAree.Rettifica(2025, Aree, t, dp);
        Assert.True(x.Presente);
        Assert.Equal(35000m, x.Totale);
        Assert.Equal(10000m - 24000m, x.UtileRettificato["04"]);
        Assert.Equal(-16000m, x.UtileRettificato["03"]);
        Assert.Equal(1200m, x.QuotaAmm["04"]);                           // 6.000 dell'amministrazione su 5 aree
        Assert.Equal(8000m - 24000m - 1200m, x.UtileConContributoRettificato["04"]);
        Assert.Equal(8000m - 1200m, x.UtileConContributoRettificato["06"]);
        Assert.Equal(t.Utile.Values.Sum() - 35000m, x.UtileMareaRettificato);

        Assert.False(MotoreAccessoriAree.Rettifica(2024, Aree, t, null).Presente);
    }
}
