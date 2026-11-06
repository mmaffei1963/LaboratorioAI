using LaboratorioAI.Core.Andamento;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Test del motore «clienti da seguire». Data di riferimento 7/10/2026: ultimi 12 mesi = ott 2025–set 2026.</summary>
public class MotoreAndamentoClientiTest
{
    private static readonly MotoreAndamentoClienti Motore = new();
    private static readonly DateTime Oggi = new(2026, 10, 7);
    private static readonly SoglieAndamento Soglie = new();

    private static FatturatoMensile F(string c, int a, int m, decimal lab, decimal leg = 0m, decimal altro = 0m) => new(c, a, m, lab, leg, altro);
    private static CampioniMensili C(string c, int a, int m, int id, int n, decimal valore, int giorno = 15, bool leg = false) =>
        new(c, a, m, id, $"Controllo {id}", n, valore, new DateTime(a, m, giorno), leg);

    [Fact]
    public void CaloForte_Rosso_ConPerditaEVariazione()
    {
        var r = Motore.Calcola([F("A", 2025, 3, 1000m), F("A", 2026, 3, 600m)], [], Soglie, Oggi);
        var a = Assert.Single(r.Clienti);
        Assert.Equal(1000m, a.LabPrecedenti);
        Assert.Equal(600m, a.LabUltimi);
        Assert.Equal(-0.4m, a.Variazione);
        Assert.Equal(400m, a.Perdita);
        Assert.Equal(Semaforo.Rosso, a.Semaforo);
        Assert.Equal(202609, r.UltimoMese);
    }

    [Fact]
    public void SottoIlMinimo_NessunSemaforo()
    {
        var r = Motore.Calcola([F("A", 2025, 3, 400m)], [], Soglie, Oggi);
        Assert.Equal(Semaforo.Nessuno, Assert.Single(r.Clienti).Semaforo);
    }

    [Fact]
    public void MeseInCorso_NonConta()
    {
        // Ottobre 2026 è il mese della data di riferimento: escluso; ottobre 2024 è nei 12 mesi precedenti.
        var r = Motore.Calcola([F("A", 2024, 10, 1000m), F("A", 2026, 10, 1000m)], [], Soglie, Oggi);
        var a = Assert.Single(r.Clienti);
        Assert.Equal(1000m, a.LabPrecedenti);
        Assert.Equal(0m, a.LabUltimi);
        Assert.Equal(Semaforo.Rosso, a.Semaforo);
        Assert.Contains("Nessuna analisi nel periodo", a.Motivi);
    }

    [Fact]
    public void AnalisiPersa_GialloSoloSeIlFatturatoScende()
    {
        var campioni = new[] { C("A", 2025, 5, 10, 5, 500m), C("A", 2026, 5, 11, 5, 500m) };
        var cresce = Motore.Calcola([F("A", 2025, 5, 1000m), F("A", 2026, 5, 1050m)], campioni, Soglie, Oggi);
        var a = Assert.Single(cresce.Clienti);
        Assert.Single(a.AnalisiPerse);
        Assert.Equal(Semaforo.Verde, a.Semaforo);

        var scende = Motore.Calcola([F("A", 2025, 5, 1000m), F("A", 2026, 5, 950m)], campioni, Soglie, Oggi);
        Assert.Equal(Semaforo.Giallo, Assert.Single(scende.Clienti).Semaforo);
    }

    [Fact]
    public void FermoNegliUltimiTreMesi_Giallo()
    {
        var campioni = new[] { C("A", 2025, 7, 1, 2, 50m), C("A", 2025, 8, 1, 2, 50m), C("A", 2026, 5, 1, 4, 100m) };
        var r = Motore.Calcola([F("A", 2025, 7, 600m), F("A", 2026, 5, 600m)], campioni, Soglie, Oggi);
        var a = Assert.Single(r.Clienti);
        Assert.True(a.FermoUltimoTrimestre);
        Assert.Equal(Semaforo.Giallo, a.Semaforo);
    }

    [Fact]
    public void SoloConsulenza()
    {
        var r = Motore.Calcola([F("A", 2026, 2, 100m, altro: 2000m)], [], Soglie, Oggi);
        Assert.True(Assert.Single(r.Clienti).SoloConsulenza);
    }

    [Fact]
    public void Legionella_InCalo_UsaLaSogliaPropria()
    {
        // Mensile: 24 campioni nei 12 mesi precedenti (ott 2024–set 2025), 17 negli ultimi 12 (−29%).
        var campioni = new List<CampioniMensili>();
        for (var i = 0; i < 12; i++)
        {
            var (a1, m1) = (2024 + (9 + i) / 12, (9 + i) % 12 + 1);
            var (a2, m2) = (2025 + (9 + i) / 12, (9 + i) % 12 + 1);
            campioni.Add(C("L", a1, m1, 1, 2, 40m, leg: true));
            campioni.Add(C("L", a2, m2, 1, i < 5 ? 2 : 1, 20m, leg: true));
        }
        var standard = Motore.Calcola([], campioni, Soglie, Oggi);
        Assert.Equal(StatoLegionella.InCalo, standard.Legionella.Single().Stato);

        // Soglia Legionella −30%: −29% non basta più, anche se la soglia del rosso resta −20%.
        var tollerante = Motore.Calcola([], campioni, Soglie with { LegionellaSogliaCalo = -0.30m }, Oggi);
        Assert.NotEqual(StatoLegionella.InCalo, tollerante.Legionella.Single().Stato);

        // La soglia del rosso non cambia più la Legionella.
        var rosso = Motore.Calcola([], campioni, Soglie with { SogliaRosso = -0.50m }, Oggi);
        Assert.Equal(StatoLegionella.InCalo, rosso.Legionella.Single().Stato);
    }

    [Fact]
    public void Legionella_Perso_InRitardo_DaRicordare()
    {
        var campioni = new[]
        {
            // Perso: campioni nel 2025, niente negli ultimi 12 mesi.
            C("P", 2025, 6, 1, 10, 200m, leg: true),
            // Semestrale (mag e nov) fermo dal maggio 2026? no: ultimo novembre 2025 → oltre 182+30 giorni.
            C("R", 2024, 11, 1, 2, 40m, leg: true), C("R", 2025, 5, 1, 2, 40m, leg: true), C("R", 2025, 11, 1, 2, 40m, 10, true),
            // Annuale, ultimo 20/10/2025 → prossimo atteso 20/10/2026, fra 13 giorni.
            C("D", 2024, 10, 1, 3, 60m, 20, true), C("D", 2025, 10, 1, 3, 60m, 20, true)
        };
        var r = Motore.Calcola([], campioni, Soglie, Oggi);
        Assert.Equal(StatoLegionella.Perso, r.Legionella.Single(x => x.Cliente == "P").Stato);
        Assert.Equal(StatoLegionella.InRitardo, r.Legionella.Single(x => x.Cliente == "R").Stato);
        var d = r.Legionella.Single(x => x.Cliente == "D");
        Assert.Equal(365, d.IntervalloAtteso);
        Assert.Equal(StatoLegionella.DaRicordare, d.Stato);
        Assert.Equal(13, d.GiorniAlProssimo);
    }

    [Fact]
    public void Semestre_ControDueAnniPrima_ConMinimoInProporzione()
    {
        // Ultimi 6 mesi = apr–set 2026; due anni prima = apr–set 2024. Minimo 500 € l'anno → 250 € nel semestre.
        var f = new[] { F("A", 2024, 5, 300m), F("A", 2025, 5, 300m), F("A", 2026, 5, 200m), F("A", 2026, 2, 900m) };
        var r = Motore.Calcola(f, [], Soglie, Oggi, new FinestraConfronto { Mesi = 6, AnniIndietro = 2 });
        var a = Assert.Single(r.Clienti);
        Assert.Equal(300m, a.LabPrecedenti);
        Assert.Equal(200m, a.LabUltimi);
        Assert.Equal(250m, r.MinimoApplicato);
        Assert.Equal(202604, r.InizioPeriodo);
        Assert.Equal(202404, r.InizioConfronto);
        Assert.Equal(202409, r.FineConfronto);
        Assert.Equal(Semaforo.Rosso, a.Semaforo);
    }

    [Fact]
    public void DaInizioAnno_ControAnnoPrima()
    {
        var f = new[] { F("A", 2025, 3, 1000m), F("A", 2025, 11, 5000m), F("A", 2026, 3, 950m) };
        var r = Motore.Calcola(f, [], Soglie, Oggi, new FinestraConfronto { DaInizioAnno = true });
        var a = Assert.Single(r.Clienti);
        Assert.Equal(1000m, a.LabPrecedenti);
        Assert.Equal(950m, a.LabUltimi);
        Assert.Equal(Semaforo.Verde, a.Semaforo);
        Assert.Equal(202601, r.InizioPeriodo);
    }
}
