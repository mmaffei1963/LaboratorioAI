using LaboratorioAI.Core.Provvigioni;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>
/// Test del motore provvigioni di Carloni. Valori calcolati a mano.
/// 15% primo anno e 10% anni 2-5 sulle convenzioni indicate; 10% sulle segnalate; 10% sugli altri servizi dei clienti propri.
/// </summary>
public class MotoreProvvigioniCarloniTest
{
    private static readonly MotoreProvvigioniCarloni Motore = new();
    private static DateTime D(int a, int m, int g) => new(a, m, g);

    private static InputProvvigioniCarloni Input(
        IEnumerable<FatturaCommerciale> fatture, IEnumerable<IncassoFattura> incassi,
        IEnumerable<ConvenzioneCommerciale>? convenzioni = null, IEnumerable<string>? propri = null,
        IEnumerable<NotaCreditoCommerciale>? note = null, RegoleProvvigioniCarloni? regole = null,
        IEnumerable<ContrattoCommerciale>? contratti = null) =>
        new(fatture.ToList(), incassi.ToList(), (note ?? Array.Empty<NotaCreditoCommerciale>()).ToList(),
            (convenzioni ?? Array.Empty<ConvenzioneCommerciale>()).ToList(),
            new HashSet<string>(propri ?? Array.Empty<string>()), regole ?? new RegoleProvvigioniCarloni(),
            (contratti ?? Array.Empty<ContrattoCommerciale>()).ToList());

    [Fact]
    public void ConvenzioneIndicata_PrimoAnno_QuindiciPerCento()
    {
        // Lordo 1.220, netto 1.000: incasso intero → base 1.000 → 15% = 150.
        var f = new FatturaCommerciale("C1", "C1|2026|10", D(2026, 2, 1), 1000m, 1220m);
        var conv = new ConvenzioneCommerciale("C1", D(2026, 1, 15), OrigineConvenzione.IndicataDalCommerciale);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2026|10", D(2026, 3, 1), 1220m)], [conv]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.ConvenzioneIndicataAnno1, m.Voce);
        Assert.Equal(1000m, m.Base);
        Assert.Equal(150m, m.Importo);
    }

    [Fact]
    public void ConvenzioneIndicata_SecondoAnno_DieciPerCento()
    {
        var f = new FatturaCommerciale("C1", "C1|2027|10", D(2027, 2, 1), 1000m, 1220m);
        var conv = new ConvenzioneCommerciale("C1", D(2026, 1, 15), OrigineConvenzione.IndicataDalCommerciale);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2027|10", D(2027, 3, 1), 1220m)], [conv]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.ConvenzioneIndicataAnni2a5, m.Voce);
        Assert.Equal(100m, m.Importo);
    }

    [Fact]
    public void ConvenzioneIndicata_OltreCinqueAnni_DiventaAltriServiziSeClienteProprio()
    {
        var f = new FatturaCommerciale("C1", "C1|2031|10", D(2031, 2, 1), 1000m, 1220m);
        var conv = new ConvenzioneCommerciale("C1", D(2026, 1, 15), OrigineConvenzione.IndicataDalCommerciale);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C1|2031|10", D(2031, 3, 1), 1220m)], [conv], propri: ["C1"]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.AltriServizi, m.Voce);
        Assert.Equal(100m, m.Importo);
    }

    [Fact]
    public void ConvenzioneSegnalata_DieciPerCentoDalPrimoAnno_EAncheSeIlClienteNonEPropio()
    {
        var f = new FatturaCommerciale("C2", "C2|2026|5", D(2026, 2, 1), 500m, 610m);
        var conv = new ConvenzioneCommerciale("C2", D(2026, 1, 1), OrigineConvenzione.SegnalataDaAzienda);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("C2|2026|5", D(2026, 2, 20), 610m)], [conv]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.ConvenzioneSegnalata, m.Voce);
        Assert.Equal(50m, m.Importo);
    }

    [Fact]
    public void AltriServizi_SoloPerClientiPropri()
    {
        var fp = new FatturaCommerciale("P", "P|2026|1", D(2026, 2, 1), 1000m, 1220m);
        var fa = new FatturaCommerciale("A", "A|2026|1", D(2026, 2, 1), 1000m, 1220m);
        var r = Motore.Calcola(Input([fp, fa],
            [new IncassoFattura("P|2026|1", D(2026, 3, 1), 1220m), new IncassoFattura("A|2026|1", D(2026, 3, 1), 1220m)],
            propri: ["P"]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal("P", m.Cliente);
        Assert.Equal(VoceProvvigione.AltriServizi, m.Voce);
        Assert.Equal(100m, m.Importo);
    }

    [Fact]
    public void FineRapporto_EscludeSoloPunti2E3()
    {
        var regole = new RegoleProvvigioniCarloni { DataFineRapporto = D(2026, 12, 31) };
        var conv = new ConvenzioneCommerciale("C1", D(2026, 1, 15), OrigineConvenzione.IndicataDalCommerciale);
        // Fattura del 2° anno emessa dopo la fine del rapporto → nulla. Fattura del 1° anno → vale.
        var f2 = new FatturaCommerciale("C1", "C1|2027|1", D(2027, 2, 1), 1000m, 1220m);
        var r = Motore.Calcola(Input([f2], [new IncassoFattura("C1|2027|1", D(2027, 3, 1), 1220m)], [conv], regole: regole));
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void NotaDiCredito_DedottaConLaVoceDellaDataDellaNota()
    {
        var conv = new ConvenzioneCommerciale("C1", D(2026, 1, 15), OrigineConvenzione.IndicataDalCommerciale);
        var nota = new NotaCreditoCommerciale("C1", "NC 3/2026", D(2026, 6, 10), 200m);
        var r = Motore.Calcola(Input([], [], [conv], note: [nota]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(TipoMovimento.NotaCredito, m.Tipo);
        Assert.Equal("2026-T2", m.Trimestre);
        Assert.Equal(-200m, m.Base);
        Assert.Equal(-30m, m.Importo);
    }

    [Fact]
    public void IncassiPrecedentiAllaDataDiInizioCalcolo_NonProduconoMovimenti()
    {
        var f = new FatturaCommerciale("P", "P|2025|1", D(2025, 6, 1), 1000m, 1220m);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("P|2025|1", D(2025, 7, 1), 1220m)], propri: ["P"]));
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void BaseDiCalcoloPerVoce_UsaIlNettoDellaVoce()
    {
        // Netto totale 1.000, ma la base di «altri servizi» conta solo 500 → 10% = 50.
        var netti = new Dictionary<VoceProvvigione, decimal> { [VoceProvvigione.AltriServizi] = 500m };
        var f = new FatturaCommerciale("P", "P|2026|1", D(2026, 2, 1), 1000m, 1220m, netti);
        var r = Motore.Calcola(Input([f], [new IncassoFattura("P|2026|1", D(2026, 3, 1), 1220m)], propri: ["P"]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(500m, m.Base);
        Assert.Equal(50m, m.Importo);
    }

    // ── Accordo dell'08/01/2025: contratti occasionali (10%, primo anno) e di analisi (5%, primo anno) ──

    private static readonly RegoleProvvigioniCarloni Regole2025 = new()
    {
        AliquotaIndicataAnno1 = 0.10m, AliquotaIndicataAnni2a5 = 0.10m, AliquotaSegnalata = 0.10m,
        AliquotaAltriServizi = 0m, AliquotaOccasionale = 0.10m, AliquotaAnalisi = 0.05m
    };

    [Fact]
    public void Occasionale_PrimoAnnoDallaPrimaFattura_DieciPerCento()
    {
        // Commessa spot del 10/1/2026, prima fattura 1/3/2026: primo anno fino al 28/2/2027.
        var f1 = new FatturaCommerciale("C1", "C1|2026|1", D(2026, 3, 1), 1000m, 1220m);
        var f2 = new FatturaCommerciale("C1", "C1|2027|1", D(2027, 2, 15), 500m, 610m);
        var f3 = new FatturaCommerciale("C1", "C1|2027|2", D(2027, 3, 1), 800m, 976m);
        var r = Motore.Calcola(Input([f1, f2, f3],
            [new("C1|2026|1", D(2026, 3, 10), 1220m), new("C1|2027|1", D(2027, 3, 1), 610m), new("C1|2027|2", D(2027, 3, 10), 976m)],
            propri: ["C1"], regole: Regole2025,
            contratti: [new ContrattoCommerciale("C1", D(2026, 1, 10), TipoContrattoCommerciale.Occasionale)]));

        var occ = r.Movimenti.Where(m => m.Voce == VoceProvvigione.ContrattoOccasionale).ToList();
        Assert.Equal(2, occ.Count);
        Assert.Equal(150m, occ.Sum(m => m.Importo));
        // La fattura dopo il primo anno va agli altri servizi, che in questo accordo sono allo 0%.
        var altri = Assert.Single(r.Movimenti, m => m.Voce == VoceProvvigione.AltriServizi);
        Assert.Equal(0m, altri.Importo);
    }

    [Fact]
    public void Occasionale_NonSiApplicaDentroUnaConvenzione()
    {
        var f = new FatturaCommerciale("C1", "C1|2026|1", D(2026, 3, 1), 1000m, 1220m);
        var r = Motore.Calcola(Input([f], [new("C1|2026|1", D(2026, 3, 10), 1220m)],
            [new ConvenzioneCommerciale("C1", D(2024, 5, 1), OrigineConvenzione.SegnalataDaAzienda)],
            propri: ["C1"], regole: Regole2025,
            contratti: [new ContrattoCommerciale("C1", D(2026, 1, 10), TipoContrattoCommerciale.Occasionale)]));

        var m = Assert.Single(r.Movimenti);
        Assert.Equal(VoceProvvigione.ConvenzioneSegnalata, m.Voce);
        Assert.Equal(100m, m.Importo);
    }

    [Fact]
    public void Analisi_CinquePerCentoSullaParteDiAnalisi_InsiemeAllaConvenzione()
    {
        // Fattura con 1.000 di consulenza (convenzione) e 400 di analisi: 10% + 5%.
        var netti = new Dictionary<VoceProvvigione, decimal>
        {
            [VoceProvvigione.ConvenzioneSegnalata] = 1000m, [VoceProvvigione.ContrattoAnalisi] = 400m
        };
        var f = new FatturaCommerciale("C1", "C1|2026|1", D(2026, 4, 1), 1400m, 1708m, netti);
        var r = Motore.Calcola(Input([f], [new("C1|2026|1", D(2026, 4, 20), 1708m)],
            [new ConvenzioneCommerciale("C1", D(2025, 2, 1), OrigineConvenzione.SegnalataDaAzienda)],
            regole: Regole2025,
            contratti: [new ContrattoCommerciale("C1", D(2026, 3, 1), TipoContrattoCommerciale.Analisi)]));

        Assert.Equal(100m, Assert.Single(r.Movimenti, m => m.Voce == VoceProvvigione.ConvenzioneSegnalata).Importo);
        Assert.Equal(20m, Assert.Single(r.Movimenti, m => m.Voce == VoceProvvigione.ContrattoAnalisi).Importo);
    }

    [Fact]
    public void Analisi_CommessaVecchia_PrimoAnnoGiaFinito()
    {
        var netti = new Dictionary<VoceProvvigione, decimal> { [VoceProvvigione.ContrattoAnalisi] = 400m };
        var vecchia = new FatturaCommerciale("C1", "C1|2019|1", D(2019, 2, 1), 400m, 488m, netti);
        var nuova = new FatturaCommerciale("C1", "C1|2026|1", D(2026, 4, 1), 400m, 488m, netti);
        var r = Motore.Calcola(Input([vecchia, nuova], [new("C1|2026|1", D(2026, 4, 20), 488m)],
            regole: Regole2025,
            contratti: [new ContrattoCommerciale("C1", D(2018, 6, 1), TipoContrattoCommerciale.Analisi)]));

        Assert.DoesNotContain(r.Movimenti, m => m.Voce == VoceProvvigione.ContrattoAnalisi);
    }
}
