using LaboratorioAI.Core.Provvigioni;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Test del motore provvigioni del commerciale di laboratorio. Valori calcolati a mano.
/// Regole di default: 5% nuovo (12 mesi), 3% mantenimento, 2% sviluppo a obiettivo raggiunto,
/// inizio accordo 1/9/2025, premio a gradino 800/1.600/3.000 € a 50/70/100 mila €.
/// </summary>
public class MotoreProvvigioniLabTest
{
    private static readonly MotoreProvvigioniLab Motore = new();
    private static DateTime D(int a, int m, int g) => new(a, m, g);

    private static InputProvvigioniLab Input(
        IEnumerable<FatturaLab> fatture, IEnumerable<IncassoFattura> incassi,
        IEnumerable<NotaCreditoLab>? note = null, IEnumerable<ObiettivoSviluppo>? obiettivi = null,
        RegoleProvvigioniLab? regole = null) =>
        new(fatture.ToList(), incassi.ToList(), (note ?? Array.Empty<NotaCreditoLab>()).ToList(), (obiettivi ?? Array.Empty<ObiettivoSviluppo>()).ToList(), regole ?? new RegoleProvvigioniLab());

    [Fact]
    public void ClienteNuovo_PagaCinquePerCentoSuQuotaDiPerimetro()
    {
        // Fattura: lordo 1.220, di cui laboratorio netto 600 (resto non di perimetro: 400 netto).
        // Incasso totale 1.220 → quota perimetro = 1.220 × 600 / 1.220 = 600 → 5% = 30.
        var f = new FatturaLab("C1", "C1|2026|10", D(2026, 3, 10), 600m, 1220m);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2026|10", D(2026, 4, 20), 1220m)]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal("2026-T2", m.Trimestre);
        Assert.Equal(VoceProvvigione.Nuovo, m.Voce);
        Assert.Equal(600m, m.Base);
        Assert.Equal(30m, m.Importo);
    }

    [Fact]
    public void IncassoParziale_ProporzionaLaQuota()
    {
        var f = new FatturaLab("C1", "C1|2026|10", D(2026, 3, 10), 1000m, 1220m);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2026|10", D(2026, 4, 1), 610m)]));
        Assert.Equal(500m, r.Movimenti.Single().Base);     // 610 × 1000 / 1220
        Assert.Equal(25m, r.Movimenti.Single().Importo);   // 5%
    }

    [Fact]
    public void DopoDodiciMesi_ClienteDiventaMantenimento_TrePerCento()
    {
        var f1 = new FatturaLab("C1", "C1|2026|1", D(2026, 1, 15), 100m, 122m);
        var f2 = new FatturaLab("C1", "C1|2027|2", D(2027, 2, 15), 100m, 122m);  // 13° mese
        var r = Motore.Calcola(Input([f1, f2], [
            new IncassoFattura("C1|2026|1", D(2026, 2, 1), 122m),
            new IncassoFattura("C1|2027|2", D(2027, 3, 1), 122m)]));

        Assert.Equal(new[] { VoceProvvigione.Nuovo, VoceProvvigione.Mantenimento }, r.Movimenti.Select(m => m.Voce).ToArray());
        Assert.Equal(new[] { 5m, 3m }, r.Movimenti.Select(m => m.Importo).ToArray());
    }

    [Fact]
    public void PausaOltreVentiquattroMesi_ClienteTornaNuovo()
    {
        // Rapporto iniziato dopo l'accordo, poi pausa di 30 mesi: la nuova fattura riparte dal 5%.
        var f1 = new FatturaLab("C1", "C1|2025|1", D(2025, 10, 1), 100m, 122m);
        var f2 = new FatturaLab("C1", "C1|2028|2", D(2028, 4, 5), 100m, 122m);
        var r = Motore.Calcola(Input([f1, f2], [new IncassoFattura("C1|2028|2", D(2028, 5, 1), 122m)]));
        Assert.Equal(VoceProvvigione.Nuovo, r.Movimenti.Single().Voce);
    }

    [Fact]
    public void ClienteEsistente_NonPagaVoceUnoNeDue()
    {
        // Rapporto iniziato nel giugno 2024 (prima dell'accordo) e mai interrotto oltre 24 mesi.
        var f1 = new FatturaLab("C1", "C1|2024|1", D(2024, 6, 1), 100m, 122m);
        var f2 = new FatturaLab("C1", "C1|2026|2", D(2026, 5, 1), 100m, 122m);
        var r = Motore.Calcola(Input([f1, f2], [new IncassoFattura("C1|2026|2", D(2026, 6, 1), 122m)]));
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void NotaDiCredito_DedottaNelTrimestreDellaNota()
    {
        var f = new FatturaLab("C1", "C1|2026|1", D(2026, 2, 1), 1000m, 1220m);
        var r = Motore.Calcola(Input(
            [f], [new IncassoFattura("C1|2026|1", D(2026, 3, 1), 1220m)],
            note: [new NotaCreditoLab("C1", "NC 5", D(2026, 5, 10), 200m)]));

        var nota = r.Movimenti.Single(m => m.Tipo == TipoMovimento.NotaCredito);
        Assert.Equal("2026-T2", nota.Trimestre);
        Assert.Equal(-200m, nota.Base);
        Assert.Equal(-10m, nota.Importo);
        Assert.Equal(40m, r.Movimenti.Sum(m => m.Importo));  // 50 − 10
    }

    [Fact]
    public void IncassoPrimaDellInizioCalcolo_NonProduceMovimentiMaServeAllaStoria()
    {
        var regole = new RegoleProvvigioniLab { DataInizioCalcolo = new DateTime(2027, 1, 1), DataInizioAccordo = new DateTime(2025, 9, 1) };
        var f = new FatturaLab("C1", "C1|2026|1", D(2026, 11, 1), 100m, 122m);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2026|1", D(2026, 12, 1), 122m)], regole: regole));
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void IncassoSenzaFattura_ProduceAvviso()
    {
        var r = Motore.Calcola(Input([], [new IncassoFattura("X|2026|9", D(2026, 5, 1), 100m)]));
        Assert.Empty(r.Movimenti);
        Assert.Single(r.Avvisi);
    }

    // ── Voce 3 ────────────────────────────────────────────────────────

    [Fact]
    public void Sviluppo_ObiettivoRaggiunto_PagaDueDaCumulato()
    {
        // Rapporto iniziato a giugno 2025 (prima dell'accordo), senza pause → cliente Esistente.
        var storia = new FatturaLab("E1", "E1|2025|1", D(2025, 6, 1), 50m, 61m);
        var a = new FatturaLab("E1", "E1|2027|1", D(2027, 1, 20), 1000m, 1220m);
        var b = new FatturaLab("E1", "E1|2027|2", D(2027, 5, 20), 1500m, 1830m);
        var ob = new ObiettivoSviluppo("E1", 2027, D(2027, 1, 1), 2000m);

        var r = Motore.Calcola(Input([storia, a, b], [
            new IncassoFattura("E1|2027|1", D(2027, 2, 10), 1220m),   // T1: cumulato 1.000 → sotto obiettivo
            new IncassoFattura("E1|2027|2", D(2027, 6, 10), 1830m)],  // T2: cumulato 2.500 → raggiunto
            obiettivi: [ob]));

        var mov = r.Movimenti.Single();
        Assert.Equal(TipoMovimento.SviluppoObiettivo, mov.Tipo);
        Assert.Equal("2027-T2", mov.Trimestre);
        Assert.Equal(50m, mov.Importo);   // 2% × 2.500
    }

    [Fact]
    public void Sviluppo_ObiettivoNonRaggiunto_NessunCompenso()
    {
        var storia = new FatturaLab("E1", "E1|2025|1", D(2025, 6, 1), 50m, 61m);
        var a = new FatturaLab("E1", "E1|2027|1", D(2027, 1, 20), 1000m, 1220m);
        var r = Motore.Calcola(Input([storia, a], [new IncassoFattura("E1|2027|1", D(2027, 2, 10), 1220m)],
            obiettivi: [new ObiettivoSviluppo("E1", 2027, D(2027, 1, 1), 5000m)]));
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void Sviluppo_TrimestriSuccessiviAMaturazione_AccumulanoSoloLaDifferenza()
    {
        var storia = new FatturaLab("E1", "E1|2025|1", D(2025, 6, 1), 50m, 61m);
        var a = new FatturaLab("E1", "E1|2027|1", D(2027, 1, 20), 3000m, 3660m);
        var b = new FatturaLab("E1", "E1|2027|2", D(2027, 7, 20), 1000m, 1220m);
        var r = Motore.Calcola(Input([storia, a, b], [
            new IncassoFattura("E1|2027|1", D(2027, 2, 10), 3660m),   // T1: 3.000 ≥ 2.000 → 60
            new IncassoFattura("E1|2027|2", D(2027, 8, 10), 1220m)],  // T3: 4.000 → 80, delta 20
            obiettivi: [new ObiettivoSviluppo("E1", 2027, D(2027, 1, 1), 2000m)]));

        Assert.Equal(new[] { 60m, 20m }, r.Movimenti.Select(m => m.Importo).ToArray());
        Assert.Equal(new[] { "2027-T1", "2027-T3" }, r.Movimenti.Select(m => m.Trimestre).ToArray());
    }

    [Fact]
    public void Sviluppo_NonSiSommaAllaVoceUnoSuClienteNuovo()
    {
        // Cliente nuovo (prima fattura gennaio 2027) inserito per errore in elenco: vale solo il 5%.
        var f = new FatturaLab("N1", "N1|2027|1", D(2027, 1, 20), 1000m, 1220m);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("N1|2027|1", D(2027, 2, 10), 1220m)],
            obiettivi: [new ObiettivoSviluppo("N1", 2027, D(2027, 1, 1), 500m)]));
        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.Nuovo, m.Voce);
    }

    [Fact]
    public void Sviluppo_AvanzamentoMostraCumulatoERaggiungimento()
    {
        var storia = new FatturaLab("E1", "E1|2025|1", D(2025, 6, 1), 50m, 61m);
        var a = new FatturaLab("E1", "E1|2027|1", D(2027, 1, 20), 1000m, 1220m);
        var r = Motore.Calcola(Input([storia, a], [new IncassoFattura("E1|2027|1", D(2027, 2, 10), 1220m)],
            obiettivi: [new ObiettivoSviluppo("E1", 2027, D(2027, 1, 1), 4000m)]));

        var av = Assert.Single(r.Obiettivi);
        Assert.Equal(1000m, av.Cumulato);
        Assert.False(av.Raggiunto);
        Assert.Equal(0m, av.Provvigione);
    }

    // ── Premio ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(49_999, 0, 50_000)]
    [InlineData(50_000, 800, 70_000)]
    [InlineData(72_000, 1_600, 100_000)]
    [InlineData(100_000, 3_000, null)]
    public void Premio_AGradino(int incassato, int premioAtteso, int? prossima)
    {
        // Un solo cliente nuovo, una fattura incassata nell'anno con netto = incassato.
        var f = new FatturaLab("N1", "N1|2027|1", D(2027, 1, 10), incassato, incassato);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("N1|2027|1", D(2027, 3, 1), incassato)]));
        var p = Assert.Single(r.Premi);
        Assert.Equal(2027, p.Anno);
        Assert.Equal((decimal)premioAtteso, p.Premio);
        Assert.Equal(prossima, p.ProssimaSoglia is null ? (int?)null : (int)p.ProssimaSoglia.Value);
    }

    [Fact]
    public void Premio_ProporzionatoAlFattoreOrario()
    {
        var regole = new RegoleProvvigioniLab { FattoreOrario = 0.5m };
        var f = new FatturaLab("N1", "N1|2027|1", D(2027, 1, 10), 25_000m, 25_000m);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("N1|2027|1", D(2027, 3, 1), 25_000m)], regole: regole));
        Assert.Equal(400m, r.Premi.Single().Premio);  // soglia 25.000, premio 800 × 0,5
    }

    [Fact]
    public void Premio_NonConteggiaIClientiInMantenimento()
    {
        var f1 = new FatturaLab("C1", "C1|2026|1", D(2026, 1, 15), 10m, 10m);
        var f2 = new FatturaLab("C1", "C1|2027|2", D(2027, 4, 1), 60_000m, 60_000m);  // 15° mese: mantenimento
        var r = Motore.Calcola(Input([f1, f2], [new IncassoFattura("C1|2027|2", D(2027, 5, 1), 60_000m)]));
        Assert.Empty(r.Premi);   // nessun incasso di clienti "nuovi" nel 2027
    }

    [Fact]
    public void BaseDiCalcoloPerVoce_UsaIlNettoDellaVoce()
    {
        // Fattura con 1.000 di laboratorio, di cui 400 di Legionella esclusi dalla base della voce Nuovo:
        // incasso intero 1.220 → quota = 1.220 × 600 / 1.220 = 600 → 5% = 30.
        var netti = new Dictionary<VoceProvvigione, decimal> { [VoceProvvigione.Nuovo] = 600m, [VoceProvvigione.Mantenimento] = 1000m };
        var f = new FatturaLab("C1", "C1|2026|10", D(2026, 3, 10), 600m, 1220m, netti);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2026|10", D(2026, 4, 20), 1220m)]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.Nuovo, m.Voce);
        Assert.Equal(600m, m.Base);
        Assert.Equal(30m, m.Importo);
    }

    [Fact]
    public void BaseDiCalcoloPerVoce_NettoZero_NessunMovimento()
    {
        var netti = new Dictionary<VoceProvvigione, decimal> { [VoceProvvigione.Nuovo] = 0m };
        var f = new FatturaLab("C1", "C1|2026|10", D(2026, 3, 10), 500m, 1220m, netti);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2026|10", D(2026, 4, 20), 1220m)]));
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void ExtraLaboratorio_DieciPerCentoNeiPrimiDodiciMesi_SullaBaseFuoriLaboratorio()
    {
        // Cliente con prima commessa di consulenza del commerciale il 1/3/2026. Fattura del 10/4/2026:
        // laboratorio 0 (cliente non suo per il laboratorio), consulenza netta 1.000 su lordo 1.220 → 10% = 100.
        var netti = new Dictionary<VoceProvvigione, decimal>
        {
            [VoceProvvigione.Nuovo] = 0m, [VoceProvvigione.Mantenimento] = 0m, [VoceProvvigione.ExtraLaboratorio] = 1000m
        };
        var f = new FatturaLab("C9", "C9|2026|5", D(2026, 4, 10), 0m, 1220m, netti);
        var input = new InputProvvigioniLab([f], [new IncassoFattura("C9|2026|5", D(2026, 5, 2), 1220m)],
            Array.Empty<NotaCreditoLab>(), Array.Empty<ObiettivoSviluppo>(), new RegoleProvvigioniLab(),
            [new IncaricoExtraLaboratorio("C9", D(2026, 3, 1))]);
        var r = Motore.Calcola(input);

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.ExtraLaboratorio, m.Voce);
        Assert.Equal(1000m, m.Base);
        Assert.Equal(100m, m.Importo);
    }

    [Fact]
    public void ExtraLaboratorio_DopoDodiciMesi_NonMatura()
    {
        var netti = new Dictionary<VoceProvvigione, decimal> { [VoceProvvigione.Nuovo] = 0m, [VoceProvvigione.ExtraLaboratorio] = 1000m };
        var f = new FatturaLab("C9", "C9|2027|5", D(2027, 3, 1), 0m, 1220m, netti);
        var input = new InputProvvigioniLab([f], [new IncassoFattura("C9|2027|5", D(2027, 3, 20), 1220m)],
            Array.Empty<NotaCreditoLab>(), Array.Empty<ObiettivoSviluppo>(), new RegoleProvvigioniLab(),
            [new IncaricoExtraLaboratorio("C9", D(2026, 3, 1))]);
        Assert.Empty(Motore.Calcola(input).Movimenti);
    }
}
