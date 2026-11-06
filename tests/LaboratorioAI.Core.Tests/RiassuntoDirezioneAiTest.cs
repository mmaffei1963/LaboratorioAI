using LaboratorioAI.Core.Direzione;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Testo che l'AI commenta (13/10/2026): i numeri sono quelli del programma, con i limiti decisi da Mirko Maffei.</summary>
public class RiassuntoDirezioneAiTest
{
    private static DatiCommentoTerritorio Dati(IReadOnlyList<ClienteConfronto> clienti, ProgressivoAnno? progressivo = null)
    {
        var anni = new[] { 2024, 2025, 2026 };
        var zona = new ZonaTerritorio("PU", "Pesaro e Urbino", "Marche",
            [new(2024, 300_000m, 40, 5, 3), new(2025, 390_000m, 45, 8, 3), new(2026, 250_000m, 41, 2, 6)]);
        return new DatiCommentoTerritorio("anni 2024–2026, dettaglio per provincia", "file AZW del 13/10/2026", anni, LivelloTerritorio.Provincia,
            [zona], zona.Anni, [], 2022, 2018, 2025, 2025, clienti, progressivo);
    }

    [Fact]
    public void Elenchi_limitati_ai_primi_30_con_il_nome()
    {
        var clienti = Enumerable.Range(1, 45).Select(i => new ClienteConfronto($"C{i}", $"Cliente {i:00} srl", "PU", 1000m * i, 1100m * i)).ToList();
        var t = RiassuntoDirezioneAi.Territorio(Dati(clienti));
        Assert.True(t.Contains("Cliente 45 srl"));          // il più grande è nei primi 30
        Assert.False(t.Contains("Cliente 01 srl"));         // il più piccolo no
        Assert.True(t.Contains("… altri 15"));
    }

    [Fact]
    public void Nuovi_persi_e_concentrazione()
    {
        var clienti = new List<ClienteConfronto>
        {
            new("A", "Alfa spa", "PU", 0m, 60_000m),        // nuovo
            new("B", "Beta srl", "RN", 40_000m, 0m),        // perso
            new("C", "Gamma snc", "PU", 30_000m, 40_000m),  // in crescita
        };
        var t = RiassuntoDirezioneAi.Territorio(Dati(clienti));
        Assert.True(t.Contains("Clienti nuovi nel 2025 (nessun fatturato nel 2024) — in tutto 1"));
        Assert.True(t.Contains("Clienti persi nel 2025 (fatturato nel 2024, nessuno nel 2025) — in tutto 1"));
        Assert.True(t.Contains("Primi 1: 60.000 € = 60,0% del totale"));   // 60k su 100k
        Assert.True(t.Contains("- Gamma snc (PU): 2024 30.000 € → 2025 40.000 € (+33,3%)"));
    }

    [Fact]
    public void Anno_in_corso_parziale_e_progressivo()
    {
        var t = RiassuntoDirezioneAi.Territorio(Dati([], new ProgressivoAnno(2026, 9, 250_000m, 280_000m)));
        Assert.True(t.Contains("il 2026 è parziale (dati fino a settembre)"));
        Assert.True(t.Contains("250.000 € contro 280.000 €"));
        Assert.True(t.Contains("(-10,7%)"));
    }

    [Fact]
    public void Istruzioni_vietano_numeri_inventati()
    {
        Assert.True(RiassuntoDirezioneAi.Sistema.Contains("Usa SOLO i numeri presenti"));
        Assert.Equal(30, RiassuntoDirezioneAi.ClientiPerElenco);
    }
}
