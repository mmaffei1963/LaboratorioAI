using LaboratorioAI.Core.Provvigioni;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Test del motore dei compensi ai segnalatori. Valori calcolati a mano.</summary>
public class MotoreCompensiSegnalatoriTest
{
    private static readonly MotoreCompensiSegnalatori Motore = new();
    private static DateTime D(int a, int m, int g) => new(a, m, g);

    private static SegnalazioneCompenso S(DateTime inizio, decimal aliquota = 0.05m, int mesi = 24, int? anni = null, DateTime? fine = null) =>
        new(1, "C1", inizio, fine, aliquota, mesi, anni);

    private static RisultatoCompensiSegnalatori Calcola(SegnalazioneCompenso s, IEnumerable<FatturaSegnalazione> fatture,
        IEnumerable<IncassoFattura> incassi, IEnumerable<NotaSegnalazione>? note = null, DateTime? oggi = null) =>
        Motore.Calcola(new InputCompensiSegnalatori(new[] { s }, fatture.ToList(), incassi.ToList(),
            (note ?? Array.Empty<NotaSegnalazione>()).ToList(), oggi ?? D(2026, 10, 5)));

    [Fact]
    public void Incasso_Intero_CinquePerCento_SulNettoDellAmbito()
    {
        // Lordo 1.220, netto nell'ambito 600 (il resto è di altri servizi): incasso intero → base 600 → 5% = 30.
        var f = new FatturaSegnalazione(1, "F1", D(2026, 2, 1), 600m, 1220m);
        var r = Calcola(S(D(2026, 1, 1)), [f], [new IncassoFattura("F1", D(2026, 3, 10), 1220m)]);
        var m = Assert.Single(r.Movimenti);
        Assert.Equal(600m, m.Base);
        Assert.Equal(30m, m.Importo);
        Assert.Equal("2026-T1", m.Trimestre);
    }

    [Fact]
    public void FatturaNonPagata_NessunCompenso()
    {
        var f = new FatturaSegnalazione(1, "F1", D(2026, 2, 1), 1000m, 1220m);
        var r = Calcola(S(D(2026, 1, 1)), [f], []);
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void IncassoParziale_E_Storno()
    {
        // Metà incassata (610 → base 500 → 25), poi storno di 122 (base −100 → −5).
        var f = new FatturaSegnalazione(1, "F1", D(2026, 2, 1), 1000m, 1220m);
        var r = Calcola(S(D(2026, 1, 1)), [f],
            [new IncassoFattura("F1", D(2026, 3, 1), 610m), new IncassoFattura("F1", D(2026, 4, 2), -122m)]);
        Assert.Equal(2, r.Movimenti.Count);
        Assert.Equal(25m, r.Movimenti[0].Importo);
        Assert.Equal(-5m, r.Movimenti[1].Importo);
        Assert.Equal("2026-T2", r.Movimenti[1].Trimestre);
    }

    [Fact]
    public void FatturaPrimaDellInizio_NonConta()
    {
        var f = new FatturaSegnalazione(1, "F1", D(2025, 12, 20), 1000m, 1220m);
        var r = Calcola(S(D(2026, 1, 1)), [f], [new IncassoFattura("F1", D(2026, 1, 15), 1220m)]);
        Assert.Empty(r.Movimenti);
    }

    [Fact]
    public void Interruzione_Oltre24Mesi_FermaIlCompenso()
    {
        // F1 a marzo 2026, F2 a maggio 2028 (oltre 24 mesi dopo F1): F2 non conta, né le successive.
        var f1 = new FatturaSegnalazione(1, "F1", D(2026, 3, 1), 1000m, 1220m);
        var f2 = new FatturaSegnalazione(1, "F2", D(2028, 5, 1), 1000m, 1220m);
        var f3 = new FatturaSegnalazione(1, "F3", D(2028, 6, 1), 1000m, 1220m);
        var r = Calcola(S(D(2026, 1, 1)), [f1, f2, f3],
            [new IncassoFattura("F1", D(2026, 3, 30), 1220m), new IncassoFattura("F2", D(2028, 5, 30), 1220m), new IncassoFattura("F3", D(2028, 6, 30), 1220m)],
            oggi: D(2028, 10, 1));
        var m = Assert.Single(r.Movimenti);
        Assert.Equal("F1", m.Riferimento);
        Assert.Equal(D(2028, 3, 1), r.FineCompenso[1]);
    }

    [Fact]
    public void ContinuitaEntro24Mesi_ContinuaPerAnni()
    {
        // Una fattura ogni anno: il compenso continua (pluriennale finché il cliente compra).
        var fatture = Enumerable.Range(0, 5).Select(k => new FatturaSegnalazione(1, $"F{k}", D(2026 + k, 4, 1), 100m, 122m)).ToList();
        var incassi = fatture.Select(f => new IncassoFattura(f.ChiaveFattura, f.Data.AddDays(20), 122m)).ToList();
        var r = Calcola(S(D(2026, 1, 1)), fatture, incassi, oggi: D(2030, 6, 1));
        Assert.Equal(5, r.Movimenti.Count);
        Assert.All(r.Movimenti, m => Assert.Equal(5m, m.Importo));
    }

    [Fact]
    public void DurataMassima_DueAnni()
    {
        var f1 = new FatturaSegnalazione(1, "F1", D(2026, 6, 1), 100m, 122m);
        var f2 = new FatturaSegnalazione(1, "F2", D(2028, 4, 1), 100m, 122m);
        var r = Calcola(S(D(2026, 3, 1), anni: 2), [f1, f2],
            [new IncassoFattura("F1", D(2026, 6, 20), 122m), new IncassoFattura("F2", D(2028, 4, 20), 122m)], oggi: D(2028, 6, 1));
        var m = Assert.Single(r.Movimenti);
        Assert.Equal("F1", m.Riferimento);
    }

    [Fact]
    public void NotaDiCredito_NelTrimestreDellaNota()
    {
        var f = new FatturaSegnalazione(1, "F1", D(2026, 2, 1), 1000m, 1220m);
        var nota = new NotaSegnalazione(1, "NC1", D(2026, 5, 10), 200m);
        var r = Calcola(S(D(2026, 1, 1)), [f], [new IncassoFattura("F1", D(2026, 3, 1), 1220m)], [nota]);
        Assert.Equal(2, r.Movimenti.Count);
        var n = r.Movimenti[1];
        Assert.Equal(TipoMovimento.NotaCredito, n.Tipo);
        Assert.Equal(-200m, n.Base);
        Assert.Equal(-10m, n.Importo);
        Assert.Equal("2026-T2", n.Trimestre);
    }

    [Fact]
    public void AliquotaZero_NessunImporto_MaBaseVisibile()
    {
        // Segnalazioni di MARE.A o dei consulenti allo 0%: la base resta visibile nel riepilogo, il compenso è zero.
        var f = new FatturaSegnalazione(1, "F1", D(2026, 2, 1), 1000m, 1220m);
        var r = Calcola(S(D(2026, 1, 1), aliquota: 0m), [f], [new IncassoFattura("F1", D(2026, 3, 1), 1220m)]);
        var m = Assert.Single(r.Movimenti);
        Assert.Equal(1000m, m.Base);
        Assert.Equal(0m, m.Importo);
    }
}
