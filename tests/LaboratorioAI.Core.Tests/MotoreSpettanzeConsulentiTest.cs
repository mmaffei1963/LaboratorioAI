using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Provvigioni;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Spettanze dei consulenti a percentuale (19/10/2026).</summary>
public class MotoreSpettanzeConsulentiTest
{
    private static readonly ConsulentePercentuale Bac = new() { Nome = "Bacchiani", CodiceZona = "BAC", CodiceCommessa = "BACCHI" };
    private static readonly GruppiVociConsulenti G = new()
    {
        Analisi = GruppiVociConsulenti.Insieme("ANA;ANE;LEV"), AnalisiEsterne = GruppiVociConsulenti.Insieme("ANE"),
        Formazione = GruppiVociConsulenti.Insieme("O62;HFU"), Consulenza = GruppiVociConsulenti.Insieme("626;HAC;AT"), Tarature = GruppiVociConsulenti.Insieme("TAR")
    };
    private static readonly List<ContrattoConsulente> K =
    [
        new() { ValidoDal = new(2017, 1, 1), ValidoAl = new(2026, 1, 8) },
        new() { ValidoDal = new(2026, 1, 9), PercTarature = 50m },
    ];

    private static List<MovimentoSpettanza> Calcola(params (string Voce, string? Commessa, string? Zona, decimal Netto)[] righe)
    {
        var rr = righe.Select((r, i) => new RigaRicavoConsulente("C1|2026|1", "C1", r.Zona, new DateTime(2026, 3, 1), r.Voce, r.Commessa, r.Netto)).ToList();
        var lordo = rr.Sum(r => r.Netto) * 1.22m;
        var inc = new List<IncassoConsulente> { new("C1|2026|1", new DateTime(2026, 4, 10), lordo) };
        return MotoreSpettanzeConsulenti.Calcola(Bac, K, G, rr, new Dictionary<string, decimal> { ["C1|2026|1"] = lordo }, inc, [],
            MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2026, 1, 1), new(2026, 12, 31));
    }

    [Fact]
    public void Regole_PerVoce()
    {
        var m = Calcola(("HAC", "BACCHI", "XX", 1000m), ("ANA", "BACCHI", "XX", 200m), ("ANA", "BUSCAG", "BAC", 300m), ("ANE", "BUSCAG", "BAC", 400m),
            ("O62", "BACCHI", "XX", 500m), ("O62", null, "BAC", 600m), ("TAR", "BACCHI", "XX", 100m), ("626", "STEFAN", "XX", 999m));
        Assert.Equal(500m, m.Single(x => x.Voce == "a").Importo);
        Assert.Equal(20m, m.Single(x => x.Voce == "b").Importo);
        Assert.Equal(30m, m.Single(x => x.Voce == "c").Importo);        // ANE dei suoi clienti esclusa
        Assert.Equal(250m, m.Single(x => x.Voce == "f").Importo);
        Assert.Equal(60m, m.Single(x => x.Voce == "e").Importo);        // formazione senza commessa = di altri
        Assert.Equal(50m, m.Single(x => x.Voce == "g").Importo);        // tarature dal contratto 2026
        Assert.Equal(6, m.Count);                                       // la riga di altri su altro cliente non conta
    }

    [Fact]
    public void AnalisiPortateDaLuiSuSuoCliente_Solo10()
    {
        var m = Calcola(("ANA", "BACCHI", "BAC", 1000m));
        Assert.Equal(100m, m.Single().Importo);
        Assert.Equal("b", m.Single().Voce);
    }

    [Fact]
    public void Campionamento_AnalisiEsterneConLaLoroPercentuale()
    {
        // 22/10/2026: contratto con 20% su ANA e legionelle e 10% sulle analisi esterne.
        var k = new List<ContrattoConsulente> { new() { ValidoDal = new(2017, 1, 1), PercCampionamento = 20m, PercCampionamentoEsterne = 10m } };
        var rr = new List<RigaRicavoConsulente>
        {
            new("C1|2026|1", "C1", "XX", new DateTime(2026, 3, 1), "ANA", "BACCHI", 1000m),
            new("C1|2026|1", "C1", "XX", new DateTime(2026, 3, 1), "LEV", "BACCHI", 500m),
            new("C1|2026|1", "C1", "XX", new DateTime(2026, 3, 1), "ANE", "BACCHI", 400m),
        };
        var lordo = rr.Sum(r => r.Netto) * 1.22m;
        var m = MotoreSpettanzeConsulenti.Calcola(Bac, k, G, rr, new Dictionary<string, decimal> { ["C1|2026|1"] = lordo },
            [new("C1|2026|1", new DateTime(2026, 4, 10), lordo)], [], MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2026, 1, 1), new(2026, 12, 31));
        Assert.Equal(200m, m.Single(x => x.VoceAzw == "ANA").Importo);
        Assert.Equal(100m, m.Single(x => x.VoceAzw == "LEV").Importo);
        Assert.Equal(40m, m.Single(x => x.VoceAzw == "ANE").Importo);
        Assert.All(m, x => Assert.Equal("b", x.Voce));
    }

    [Fact]
    public void Campionamento_EsterneVuotoUsaLaStessaPercentuale()
    {
        var m = Calcola(("ANE", "BACCHI", "XX", 1000m));
        Assert.Equal(100m, m.Single().Importo);   // contratto senza «b) esterne»: 10% come b)
    }

    [Fact]
    public void FiltroDateFatture_SoloFattureDelPeriodoEDataFatturaNelMovimento()
    {
        // 22/10/2026: incassi 2026 di una fattura 2025 e di una fattura 2026; filtro sulle fatture 2026.
        var rr = new List<RigaRicavoConsulente>
        {
            new("C1|2025|9", "C1", "XX", new DateTime(2025, 12, 15), "ANA", "BACCHI", 1000m),
            new("C1|2026|1", "C1", "XX", new DateTime(2026, 2, 1), "ANA", "BACCHI", 500m),
        };
        var lordi = new Dictionary<string, decimal> { ["C1|2025|9"] = 1220m, ["C1|2026|1"] = 610m };
        var inc = new List<IncassoConsulente> { new("C1|2025|9", new DateTime(2026, 1, 20), 1220m), new("C1|2026|1", new DateTime(2026, 3, 5), 610m) };
        var tutti = MotoreSpettanzeConsulenti.Calcola(Bac, K, G, rr, lordi, inc, [], MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2026, 1, 1), new(2026, 12, 31));
        Assert.Equal(2, tutti.Count);
        Assert.Equal(2025, tutti.Single(m => m.Documento == "Fatt. 9/2025").AnnoFattura);
        var solo2026 = MotoreSpettanzeConsulenti.Calcola(Bac, K, G, rr, lordi, inc, [], MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2026, 1, 1), new(2026, 12, 31),
            new DateTime(2026, 1, 1), new DateTime(2026, 12, 31));
        Assert.Equal(50m, solo2026.Single().Importo);
        Assert.Equal(new DateTime(2026, 2, 1), solo2026.Single().DataFattura);
    }

    [Fact]
    public void AnnoFattura_DalDocumentoNeiMovimentiArchiviati()
    {
        var m = new MovimentoSpettanza(new(2026, 1, 20), "b", "C1", "Fatt. 123/2025", "ANA", "BACCHI", "incasso", 100m, 10m, 10m);
        Assert.Equal(2025, m.AnnoFattura);
    }

    [Fact]
    public void PrimoAnno_SoloEntro12Mesi()
    {
        var rr = new List<RigaRicavoConsulente>
        {
            new("C1|2024|1", "C1", "BAC", new DateTime(2024, 1, 10), "AT", "DANIEL", 100m),
            new("C1|2026|2", "C1", "BAC", new DateTime(2026, 3, 1), "AT", "DANIEL", 1000m),
            new("C2|2026|3", "C2", "BAC", new DateTime(2026, 3, 1), "AT", "DANIEL", 1000m),
        };
        var inc = new List<IncassoConsulente> { new("C1|2026|2", new DateTime(2026, 4, 1), 1220m), new("C2|2026|3", new DateTime(2026, 4, 1), 1220m) };
        var m = MotoreSpettanzeConsulenti.Calcola(Bac, K, G, rr, new Dictionary<string, decimal> { ["C1|2026|2"] = 1220m, ["C2|2026|3"] = 1220m }, inc, [],
            MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2026, 1, 1), new(2026, 12, 31));
        Assert.Equal("C2", m.Single().Cliente);   // C1 ha la prima fattura AT nel 2024: oltre il primo anno
        Assert.Equal(100m, m.Single().Importo);
    }

    [Fact]
    public void IncassoParziale_E_NotaCredito()
    {
        var rr = new List<RigaRicavoConsulente> { new("C1|2026|1", "C1", "XX", new DateTime(2026, 3, 1), "HAC", "BACCHI", 1000m) };
        var inc = new List<IncassoConsulente> { new("C1|2026|1", new DateTime(2026, 4, 1), 610m) };
        var note = new List<RigaNotaConsulente> { new("NC 5/2026", "C1", "XX", new DateTime(2026, 5, 1), "HAC", "BACCHI", 100m) };
        var m = MotoreSpettanzeConsulenti.Calcola(Bac, K, G, rr, new Dictionary<string, decimal> { ["C1|2026|1"] = 1220m }, inc, note,
            MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2026, 1, 1), new(2026, 12, 31));
        Assert.Equal(250m, m.Single(x => x.Origine == "incasso").Importo);   // metà incassata: 500 × 50%
        Assert.Equal(-50m, m.Single(x => x.Origine != "incasso").Importo);
    }

    [Fact]
    public void SenzaContratto_NienteCalcolo()
    {
        var rr = new List<RigaRicavoConsulente> { new("C1|2016|1", "C1", "XX", new DateTime(2016, 3, 1), "HAC", "BACCHI", 1000m) };
        var inc = new List<IncassoConsulente> { new("C1|2016|1", new DateTime(2016, 4, 1), 1220m) };
        var m = MotoreSpettanzeConsulenti.Calcola(Bac, K, G, rr, new Dictionary<string, decimal> { ["C1|2016|1"] = 1220m }, inc, [],
            MotoreSpettanzeConsulenti.PrimeFatture(rr), new(2016, 1, 1), new(2016, 12, 31));
        Assert.Equal(0, m.Count);
        Assert.Equal((decimal?)null, MotoreSpettanzeConsulenti.Classifica("TAR", "BACCHI", "XX", "C1", new DateTime(2025, 6, 1), Bac, K[0], G,
            new Dictionary<(string, string), DateTime>())?.Perc);   // tarature non previste nel contratto 2025
    }
}
