using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Limiti;
using LaboratorioAI.Core.Repository;
using Xunit;

namespace LaboratorioAI.Core.Tests;

/// <summary>Limiti normativi fase 1b (18/10/2026): colori e segnalazioni in «Da Validare».</summary>
public class MotoreSegnalazioniTest
{
    private const string Acqua = "Acqua destinata al consumo umano";
    private const string Insalata = "Insalata pronta";

    private static LimiteNormativo S(string cod, FamigliaNormativa f, TipoLimiteNormativo t, OperatoreLimite o, decimal? v, string u, int par,
        string? cond = null, string? cat = null, decimal? m = null, decimal? mM = null)
    {
        var s = new LimiteNormativo
        {
            CodiceScheda = cod, Famiglia = f, TipoLimite = t, Operatore = o, Valore = v, Unita = u, Condizioni = cond, MatriceCategoria = cat,
            M = m, MMaiuscola = mM, Stato = StatoSchedaLimite.Validata,
            RegolaValutazione = mM is null ? RegolaValutazioneLimite.Puntuale : RegolaValutazioneLimite.PianoCampionamento
        };
        s.Abbinamenti.Add(new AbbinamentoLimite { IdParametroArcLab = par, Attivo = true });
        return s;
    }

    private static ProvaPerSegnalazioni P(int camp, int par, string nome, string mat, double? v, string u, bool minore = false) => new()
    {
        IdCampione = camp, IdProva = camp * 100 + par, IdParametro = par, NomeParametro = nome, IdMatrice = mat == Acqua ? 1 : mat == Insalata ? 2 : 3,
        Matrice = mat, ValoreCalcolato = v, UnitaMisura = u, MinoreDi = minore, RegoleArrotondamento = true,
        DataPrelievo = new(2026, 10, 10), DataRicezione = new(2026, 10, 10, 9, 0, 0), DataInizioProva = new(2026, 10, 10, 15, 0, 0)
    };

    private static readonly List<LimiteNormativo> Schede =
    [
        S("ACQ-001", FamigliaNormativa.AcqueConsumoUmano, TipoLimiteNormativo.Sicurezza, OperatoreLimite.ValoreMassimo, 50, "mg/l", 10),
        S("ALI-001", FamigliaNormativa.Alimenti, TipoLimiteNormativo.Sicurezza, OperatoreLimite.ValoreMassimo, 100, "ufc/g", 20, "immessi sul mercato", "Alimenti pronti"),
        S("ALI-002", FamigliaNormativa.Alimenti, TipoLimiteNormativo.Sicurezza, OperatoreLimite.AssenteIn, null, "25 g", 20, "prima che lasci il controllo del produttore", "Alimenti pronti"),
        S("ALI-003", FamigliaNormativa.Alimenti, TipoLimiteNormativo.IgieneProcesso, OperatoreLimite.ValoreMassimo, 10, "ufc/g", 21, null, "Alimenti pronti", 10, 100),
        S("ALI-004", FamigliaNormativa.Alimenti, TipoLimiteNormativo.IgieneProcesso, OperatoreLimite.ValoreMassimo, 1, "MPN/g", 22, null, "Alimenti pronti", 1, 10),
    ];

    private static readonly Dictionary<int, CategorieMatrice> Categorie = new()
    {
        [2] = new(false, new HashSet<string> { RegoleLimiti.ChiaveCategoria("Alimenti pronti") })
    };

    private static EsitoCampione Uno(params ProvaPerSegnalazioni[] prove) =>
        MotoreSegnalazioni.Calcola(prove, Schede, Categorie, Tempi).Values.Single();

    private static readonly List<TempoMassimoProva> Tempi =
    [
        new() { Famiglia = FamigliaNormativa.AcqueConsumoUmano, Ore = 24, Attivo = true },
        new() { Famiglia = FamigliaNormativa.AcqueConsumoUmano, IdParametroArcLab = 11, Ore = 72, Attivo = true },
        new() { Famiglia = FamigliaNormativa.Alimenti, Ore = 48, Attivo = false },
    ];

    [Fact]
    public void OreMassime_ParametroPrevaleSullaFamiglia()
    {
        Assert.Equal(72, MotoreSegnalazioni.OreMassime(Tempi, FamigliaNormativa.AcqueConsumoUmano, 11));
        Assert.Equal(24, MotoreSegnalazioni.OreMassime(Tempi, FamigliaNormativa.AcqueConsumoUmano, 10));
        Assert.Equal((int?)null, MotoreSegnalazioni.OreMassime(Tempi, FamigliaNormativa.Alimenti, 20));
    }

    [Fact] public void SottoLimite_Verde() => Assert.Equal(ColoreRilievo.Verde, Uno(P(1, 10, "Nitrati", Acqua, 20, "mg/l")).Colore);

    [Fact] public void Oltre80PerCento_Arancione() => Assert.Equal("vicino-limite", Uno(P(1, 10, "Nitrati", Acqua, 42, "mg/l")).Rilievi.Single().Regola);

    [Fact] public void LimiteSicurezzaSuperato_Rosso() => Assert.Equal(ColoreRilievo.Rosso, Uno(P(1, 10, "Nitrati", Acqua, 60, "mg/l")).Colore);

    [Fact] public void UnitaDiversa_Grigio() => Assert.Equal("non-confrontabile", Uno(P(1, 10, "Nitrati", Acqua, 60, "µg/l")).Rilievi.Single().Regola);

    [Fact] public void RisultatoMancante_Grigio() => Assert.Equal(ColoreRilievo.Grigio, Uno(P(1, 10, "Nitrati", Acqua, null, "mg/l")).Colore);

    [Fact]
    public void DipendeDallaFase_Arancione()
    {
        var e = Uno(P(1, 20, "Listeria monocytogenes", Insalata, 50, "ufc/g"));
        Assert.Equal(ColoreRilievo.Arancione, e.Colore);
        Assert.Equal("fase", e.Rilievi.Single().Regola);
    }

    [Fact] public void SuperaEntrambeLeFasi_Rosso() => Assert.Equal(ColoreRilievo.Rosso, Uno(P(1, 20, "Listeria monocytogenes", Insalata, 500, "ufc/g")).Colore);

    [Fact] public void NonRilevato_Verde() => Assert.Equal(ColoreRilievo.Verde, Uno(P(1, 20, "Listeria monocytogenes", Insalata, 10, "ufc/g", minore: true)).Colore);

    [Fact] public void TraMeM_IgieneProcesso_Arancione() => Assert.Equal("da-segnalare", Uno(P(1, 21, "E. coli", Insalata, 50, "ufc/g")).Rilievi.Single().Regola);

    [Fact] public void MatriceSenzaCategoria_Grigio() => Assert.Equal("matrice-senza-categoria", Uno(P(1, 20, "Listeria", "Torta salata", 500, "ufc/g")).Rilievi.Single().Regola);

    [Fact]
    public void MatriceNessunaCategoria_NessunConfronto()
    {
        var esiti = MotoreSegnalazioni.Calcola([P(1, 20, "Listeria", "Torta salata", 500, "ufc/g")], Schede,
            new Dictionary<int, CategorieMatrice> { [3] = new(true, new HashSet<string>()) }, []);
        Assert.Equal(ColoreRilievo.Verde, esiti[1].Colore);
    }

    // 05/11/2026: piano a 3 classi (ALI-003: m = 10, M = 100): «< 40» non è viola, «< 200» sì.
    [Fact] public void LoqTraMeM_PianoACampionamento_NonViola() => Assert.Equal(ColoreRilievo.Verde, Uno(P(1, 21, "Enterobatteriacee", Insalata, 40, "ufc/g", minore: true)).Colore);
    [Fact] public void LoqSopraM_PianoACampionamento_Viola() => Assert.Equal("loq-sopra-limite", Uno(P(1, 21, "Enterobatteriacee", Insalata, 200, "ufc/g", minore: true)).Rilievi.Single().Regola);

    [Fact]
    public void LoqSopraIlLimite_Viola()
    {
        var e = Uno(P(1, 10, "Nitrati", Acqua, 80, "mg/l", minore: true));
        Assert.Equal("loq-sopra-limite", e.Rilievi.Single().Regola);
    }

    [Fact]
    public void ControlliTecnici_Viola()
    {
        var senzaUnita = P(1, 10, "Nitrati", Acqua, 20, "");
        Assert.Contains(Uno(senzaUnita).Rilievi, r => r.Regola == "unita-mancante");
        Assert.DoesNotContain(Uno(P(1, 11, "pH", Acqua, 7, "")).Rilievi, r => r.Regola == "unita-mancante");

        var arr = P(1, 10, "Nitrati", Acqua, 20, "mg/l"); arr.RegoleArrotondamento = false;
        Assert.Equal("arrotondamento", Uno(arr).Rilievi.Single().Regola);

        var sub = P(1, 10, "Nitrati", Acqua, 20, "mg/l"); sub.IdLabEsterno = 5; sub.LabEsterno = "Lab X";
        Assert.Equal("subappalto-frase", Uno(sub).Rilievi.Single().Regola);

        var date = P(1, 10, "Nitrati", Acqua, 20, "mg/l"); date.DataPrelievo = new(2026, 10, 12);
        Assert.Equal("prelievo-dopo-ricezione", Uno(date).Rilievi.Single().Regola);

        var tardi = P(1, 10, "Nitrati", Acqua, 20, "mg/l"); tardi.DataInizioProva = new(2026, 10, 12, 10, 0, 0);
        Assert.Equal("tempi-inizio", Uno(tardi).Rilievi.Single().Regola);
        var esterna = P(1, 10, "Nitrati", Acqua, 20, "mg/l"); esterna.DataInizioProva = new(2026, 10, 15, 10, 0, 0);
        esterna.IdLabEsterno = 5; esterna.LabEsterno = "Lab X"; esterna.FraseLabEsterno = "Prova eseguita da Lab X";
        Assert.Equal(ColoreRilievo.Verde, Uno(esterna).Colore);   // subappalto: tempi non controllati

        // shelf life: ricezione 10/10/2024 + 729 giorni di ritardo programmato = inizio previsto 09/10/2026
        var shelf = P(1, 10, "Nitrati", Acqua, 20, "mg/l");
        shelf.DataPrelievo = new(2024, 10, 10); shelf.DataRicezione = new(2024, 10, 10); shelf.RitardoAnalisi = 729; shelf.DataInizioProva = new(2026, 10, 9, 11, 55, 0);
        Assert.Equal(ColoreRilievo.Verde, Uno(shelf).Colore);
        shelf.DataInizioProva = new(2026, 10, 12, 9, 0, 0);
        Assert.Equal("tempi-inizio", Uno(shelf).Rilievi.Single().Regola);

        Assert.Contains(Uno(P(1, 10, "Nitrati", Acqua, 20, "mg/l"), P(1, 10, "Nitrati", Acqua, 21, "mg/l")).Rilievi, r => r.Regola == "parametro-ripetuto");
        Assert.Equal("ecoli-coliformi", Uno(P(1, 30, "Escherichia coli", Acqua, 5, "UFC/100 ml"), P(1, 31, "Coliformi totali", Acqua, 2, "UFC/100 ml")).Rilievi.Single().Regola);
    }

    [Fact]
    public void ProvaEsclusa_Ignorata()
    {
        var p = P(1, 10, "Nitrati", Acqua, 60, "mg/l"); p.Esclusa = true;
        Assert.Equal(ColoreRilievo.Verde, Uno(p).Colore);
    }

    // 20/10/2026: 5 unità campionarie (9 per l'istamina) sono ammesse; un altro numero no.
    [Fact]
    public void ParametroRipetuto_CinqueUnitaAmmesse_AltroNumeroViola()
    {
        ProvaPerSegnalazioni[] N(int n, int par, string nome) => Enumerable.Range(0, n).Select(_ => P(1, par, nome, Insalata, 10, "ufc/g")).ToArray();
        Assert.DoesNotContain(Uno(N(5, 30, "Enterobatteriacee")).Rilievi, r => r.Regola == "parametro-ripetuto");
        Assert.Contains(Uno(N(4, 30, "Enterobatteriacee")).Rilievi, r => r.Regola == "parametro-ripetuto");
        Assert.DoesNotContain(Uno(N(9, 31, "Istamina")).Rilievi, r => r.Regola == "parametro-ripetuto");
        Assert.Contains(Uno(N(5, 31, "Istamina")).Rilievi, r => r.Regola == "parametro-ripetuto");
    }

    // 20/10/2026: coliformi nell'acqua sopra il valore di parametro (indicatore) → rosso, non conforme.
    [Fact]
    public void Indicatore_Superato_Rosso()
    {
        var schede = new List<LimiteNormativo>
        {
            S("ACQ-010", FamigliaNormativa.AcqueConsumoUmano, TipoLimiteNormativo.Indicatore, OperatoreLimite.AssenteIn, null, "100 ml", 40)
        };
        var e = MotoreSegnalazioni.Calcola([P(1, 40, "Batteri coliformi a 37°C", Acqua, 3, "UFC/100 ml")], schede, Categorie, Tempi).Values.Single();
        Assert.Equal(ColoreRilievo.Rosso, e.Colore);
        Assert.Contains(e.Rilievi, r => r.Regola == "indicatore");
        var ok = MotoreSegnalazioni.Calcola([P(1, 40, "Batteri coliformi a 37°C", Acqua, 1, "UFC/100 ml", minore: true)], schede, Categorie, Tempi).Values.Single();
        Assert.DoesNotContain(ok.Rilievi, r => r.Regola == "indicatore");
    }

    // ── 26/10/2026 ──

    [Fact]
    public void AttivitaAcqua_SenzaUnitaNonSegnalata()
    {
        var e = Uno(P(1, 30, "Attività dell'acqua", Insalata, 0.931, ""));
        Assert.DoesNotContain(e.Rilievi, r => r.Regola == "unita-mancante");
    }

    [Fact]
    public void NessunaSchedaApplicabile_VerdeMaNonConfrontato()
    {
        var e = Uno(P(1, 99, "Stafilococchi coagulasi positivi", Insalata, 1209, "ufc/g"));
        Assert.Equal(ColoreRilievo.Verde, e.Colore);
        Assert.False(e.Confrontato);
    }

    [Fact]
    public void SottoLimite_Confrontato() => Assert.True(Uno(P(1, 10, "Nitrati", Acqua, 20, "mg/l")).Confrontato);

    [Fact]
    public void MpnEUfcNegliAlimenti_MaiConfrontati()
    {
        var e = Uno(P(1, 22, "Escherichia coli beta glucuronidasi positiva", Insalata, 10, "ufc/g", minore: true));
        Assert.Empty(e.Rilievi);
        Assert.False(e.Confrontato);
    }

    // 05/11/2026: limiti di legge passati all'analisi AI (stessa regola di applicabilità del controllo automatico).
    [Fact]
    public void LimitiPerProva_DescriveLaSchedaApplicabile()
    {
        var p = P(1, 10, "Nitrati", Acqua, 30, "mg/l");
        var l = MotoreSegnalazioni.LimitiPerProva([p], Schede, Categorie);
        Assert.True(l.TryGetValue(p.IdProva, out var d));
        Assert.Contains(d!, x => x.Contains("ACQ-001") && x.Contains("50"));
    }

    [Fact]
    public void LimitiPerProva_NienteSeLaCategoriaNonCorrispondeOManca()
    {
        var altra = P(2, 20, "Listeria", "Matrice senza categoria", 10, "ufc/g");
        var l = MotoreSegnalazioni.LimitiPerProva([altra], Schede, Categorie);
        Assert.False(l.ContainsKey(altra.IdProva));
    }
}
