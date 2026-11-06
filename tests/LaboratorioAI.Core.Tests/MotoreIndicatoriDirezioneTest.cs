using System.IO;
using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Cruscotto Direzione (13/10/2026): classificazione come Power BI e indicatori.</summary>
public class MotoreIndicatoriDirezioneTest
{
    [Fact]
    public void Classificazione_ComePowerBI()
    {
        Assert.Equal((ClassificatoreVociAzw.Fisso, ClassificatoreVociAzw.MacroPersonale, false), ClassificatoreVociAzw.Proponi("ZAT", "Zatterin Ludovico", false));
        Assert.Equal((ClassificatoreVociAzw.Variabile, "Produzione", true), ClassificatoreVociAzw.Proponi("EST", "Analisi laboratorio esterne", false));
        Assert.Equal((ClassificatoreVociAzw.Fisso, "Generali", false), ClassificatoreVociAzw.Proponi("SGP", "Spese sede Pesaro", false));
        Assert.Equal(((string?)null, ClassificatoreVociAzw.MacroRicavo, false), ClassificatoreVociAzw.Proponi("ANA", "analisi di laboratorio", true));
        // Voce sconosciuta: nessun tipo, «Altri costi».
        Assert.Equal(((string?)null, ClassificatoreVociAzw.MacroAltri, false), ClassificatoreVociAzw.Proponi("GAS", "Giusti Asia", false));
    }

    private static readonly VoceIndicatori[] Voci =
    [
        new("ANA", true, null, ClassificatoreVociAzw.MacroRicavo, false),
        new("EST", false, ClassificatoreVociAzw.Variabile, "Produzione", true),
        new("DIP", false, ClassificatoreVociAzw.Fisso, ClassificatoreVociAzw.MacroPersonale, false),
        new("SPG", false, ClassificatoreVociAzw.Fisso, "Generali", false),
        new("AMM", false, ClassificatoreVociAzw.Fisso, "Generali", false),
    ];

    private static readonly MovimentoIndicatori[] Movimenti =
    [
        new(2025, "04", "ANA", "RV001", 1000m),
        new(2025, "04", "EST", "CO001", -200m),
        new(2025, "04", "DIP", "CO002", -300m),
        new(2025, "04", "AMM", "FS56800", -50m),
        new(2025, "04", "SPG", "MX82504", -40m),
        new(2025, "03", "DIP", "CO002", -100m),   // amministrazione: 1/5 = 20 sulle aree
    ];

    [Fact]
    public void TotaleMarea()
    {
        var r = Assert.Single(MotoreIndicatoriDirezione.Calcola(Movimenti, Voci, [2025]));
        Assert.Equal(1000m, r.Fatturato);
        Assert.Equal(200m, r.SpeseVariabili);
        Assert.Equal(200m, r.MateriePrime);
        Assert.Equal(800m, r.MargineContribuzione);
        Assert.Equal(490m, r.SpeseFisse);          // 300 + 50 + 40 + 100, nessuna quota sul totale
        Assert.Equal(0m, r.QuotaAmministrazione);
        Assert.Equal(400m, r.Personale);
        Assert.Equal(800m, r.ValoreAggiunto);      // 1000 − 200 (esclusi personale, ammortamenti, imposte)
        Assert.Equal(400m, r.Ebitda);
        Assert.Equal(50m, r.Ammortamenti);
        Assert.Equal(350m, r.Ebit);
        Assert.Equal(40m, r.Imposte);
        Assert.Equal(310m, r.UtileNetto);
        Assert.Equal(350m, r.UtileAnteImposte);    // = EBIT
        Assert.Equal(612.5m, r.FatturatoPareggio); // 490 × 1000 / 800
    }

    [Fact]
    public void Area_ConQuotaAmministrazione()
    {
        var r = Assert.Single(MotoreIndicatoriDirezione.Calcola(Movimenti, Voci, [2025], "04"));
        Assert.Equal(20m, r.QuotaAmministrazione);
        Assert.Equal(410m, r.SpeseFisse);          // 300 + 50 + 40 + 20
        Assert.Equal(390m, r.UtileNetto);
        Assert.Equal(430m, r.UtileAnteImposte);    // 1000 − 590 − 20 + 40
        Assert.Equal(300m, r.Personale);
    }

    /// <summary>
    /// Riscontro con Power BI sui dati reali (13/10/2026), se il file di prova c'è (non è nel repository: contiene costi).
    /// </summary>
    [Fact]
    public void RiscontroPowerBI_SeIlFileCe()
    {
        // Cartella con agg.csv (anno, centro, voce, conto, importo) e VOCICOST.csv estratti dal file AZW.
        var cartella = Environment.GetEnvironmentVariable("LABAI_RISCONTRO_DIREZIONE");
        if (string.IsNullOrWhiteSpace(cartella)) return;
        var agg = Path.Combine(cartella, "agg.csv");
        var vc = Path.Combine(cartella, "VOCICOST.csv");
        if (!File.Exists(agg) || !File.Exists(vc)) return;
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        var voci = File.ReadAllLines(vc).Skip(1).Select(l => l.Split(',')).Select(p =>
        {
            var desc = string.Join(",", p[1..^1]);
            var (t, m, mp) = ClassificatoreVociAzw.Proponi(p[0], desc, p[^1] == "R");
            return new VoceIndicatori(p[0], p[^1] == "R", t, m, mp);
        }).ToList();
        var mov = File.ReadAllLines(agg).Skip(1).Select(l => l.Split(','))
            .Select(p => new MovimentoIndicatori(int.Parse(p[0]), p[1], p[2], p[3], decimal.Parse(p[4], inv))).ToList();
        var tot = MotoreIndicatoriDirezione.Calcola(mov, voci, [2020, 2025]);
        Assert.Equal(924042.85m, tot[0].Fatturato);
        Assert.Equal(238626.38m, tot[0].SpeseVariabili);
        Assert.Equal(292999.05m, tot[0].MateriePrime);
        Assert.Equal(467181.84m, tot[0].Personale);
        Assert.Equal(15837.82m, tot[0].Ammortamenti);
        Assert.Equal(48031.11m, tot[0].UtileNetto);
        Assert.Equal(65316.11m, tot[0].UtileAnteImposte);
        Assert.Equal(1694122.67m, tot[1].Fatturato);
        Assert.Equal(1001274.24m, tot[1].Personale);
        Assert.Equal(186842.51m, tot[1].UtileAnteImposte);
        var lab = MotoreIndicatoriDirezione.Calcola(mov, voci, [2020, 2025], "04");
        Assert.Equal(215901.94m, lab[0].SpeseFisse);
        Assert.Equal(171441.93m, lab[0].SpeseVariabili);
        Assert.Equal(48710.50m, lab[0].UtileAnteImposte);
        Assert.Equal(154478.43m, lab[0].Personale);
        Assert.Equal(455670.44m, lab[1].SpeseFisse);
        Assert.Equal(144455.00m, lab[1].UtileAnteImposte);
        Assert.Equal(892663.84m, lab[1].Fatturato);
    }
}

/// <summary>Andamento territorio (13/10/2026).</summary>
public class MotoreTerritorioTest
{
    private static readonly Dictionary<string, SedeCliente> Sedi = new()
    {
        ["C1"] = new("RN", "Cattolica"), ["C2"] = new("PU", "Pesaro"), ["C3"] = new("pu", "Fano"), ["C4"] = new("", null),
    };

    [Fact]
    public void ProvinceClientiNuoviEPersi()
    {
        FatturatoClienteAnno[] righe =
        [
            new(2024, "C1", 100m), new(2025, "C1", 150m),
            new(2024, "C2", 50m),                          // perso nel 2025
            new(2025, "C3", 80m), new(2025, "C3", -80m),   // nota di credito: non attivo
            new(2025, "C4", 10m),                          // senza provincia
            new(2025, "", 5m),                             // non legato a un cliente
        ];
        var z = MotoreTerritorio.Calcola(righe, Sedi, [2025], LivelloTerritorio.Provincia).ToDictionary(x => x.Chiave);
        Assert.Equal(150m, z["RN"].Anni[0].Fatturato);
        Assert.Equal(0, z["RN"].Anni[0].ClientiNuovi);
        Assert.Equal(0m, z["PU"].Anni[0].Fatturato);
        Assert.Equal(1, z["PU"].Anni[0].ClientiPersi);
        Assert.Equal(0, z["PU"].Anni[0].ClientiAttivi);
        Assert.Equal(10m, z[ProvinceItaliane.Estero].Anni[0].Fatturato);
        Assert.Equal(5m, z[MotoreTerritorio.NonLegato].Anni[0].Fatturato);
        Assert.Equal("Emilia-Romagna", z["RN"].Regione);
        var tot = Assert.Single(MotoreTerritorio.Totale(righe, [2025]));
        Assert.Equal(165m, tot.Fatturato);
        Assert.Equal(2, tot.ClientiAttivi);
    }

    [Fact]
    public void ConfrontoPesaro_CrescitaPrimaEDopo()
    {
        FatturatoClienteAnno[] righe = [new(2019, "C2", 100m), new(2021, "C2", 121m), new(2025, "C2", 177.1561m)];
        var g = MotoreTerritorio.ConfrontoPesaro(righe, Sedi, 2019, 2022, 2025);
        var pu = g[0];
        Assert.Equal(0.1m, Math.Round(pu.CrescitaPrima!.Value, 4));   // 100 → 121 in 2 anni
        Assert.Equal(0.1m, Math.Round(pu.CrescitaDopo!.Value, 4));    // 121 → 177,16 in 4 anni
        Assert.Null(g[2].CrescitaDopo);                               // Rimini senza fatturato nel periodo
    }
}
