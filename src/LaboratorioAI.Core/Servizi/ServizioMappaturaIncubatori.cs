using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioMappaturaIncubatori" />
///
/// <remarks>
/// Riproduce la logica del foglio storico «Modulo Statistico 04-03». La parte
/// numerica (correzione, temperatura reale, budget d'incertezza, intervallo
/// ammesso, medie per ripiano) è verificata sui casi VAL. I test statistici
/// (F di Fisher/ANOVA, Cochran, Kruskal-Wallis) determinano il giudizio testuale.
/// </remarks>
public class ServizioMappaturaIncubatori : IServizioMappaturaIncubatori
{
    private static readonly double S3 = Math.Sqrt(3.0);

    public RisultatoMappatura Calcola(DatiMappatura d)
    {
        if (d.Certificato.Count < 2)
            throw new ArgumentException("Servono almeno due punti di certificato del CDR.");
        if (d.Letture.Count == 0)
            throw new ArgumentException("Inserisci almeno una lettura.");

        double N = d.ValoreNominale;
        double k = d.FattoreCopertura <= 0 ? 2.0 : d.FattoreCopertura;

        double correzione = CorrezioneNominale(d.Certificato, N);
        var g = d.Letture.Select(l => l.TemperaturaRilevata + correzione).ToArray();

        double media = g.Average();
        double stdev = DeviazioneStandardCampionaria(g);
        double stdMedia = stdev / Math.Sqrt(g.Length);

        // u(CR) = UE(CR)/k: incertezza estesa di testata del certificato (se indicata),
        // altrimenti la massima incertezza estesa tra i punti.
        double ueTestata = d.IncertezzaEstesaCertificato > 0
            ? d.IncertezzaEstesaCertificato
            : d.Certificato.Max(c => c.IncertezzaEstesa);
        double uCr = ueTestata / k;
        double uStab = d.CertificatoSecondario is { Count: >= 2 }
            ? Deriva(d.Certificato, d.CertificatoSecondario, N)
            : d.IncertezzaInstabilita;
        double uRis = d.RisoluzioneCdr / S3;

        double uc = Math.Sqrt(stdMedia * stdMedia + uCr * uCr + uStab * uStab + uRis * uRis);
        double U = k * uc;

        double intMin = N - (d.Tolleranza - U);
        double intMax = N + (d.Tolleranza - U);

        // ── Ripiani (posizioni) ────────────────────────────────────
        var gruppi = d.Letture
            .Select((l, i) => (l.Posizione, Valore: g[i]))
            .GroupBy(x => x.Posizione.Trim())
            .OrderBy(x => x.Key)
            .Select(x => new { Pos = x.Key, Valori = x.Select(v => v.Valore).ToArray() })
            .ToList();

        var ripiani = gruppi.Select(gr =>
        {
            double m = gr.Valori.Average(), mn = gr.Valori.Min(), mx = gr.Valori.Max();
            bool ok = m >= intMin && m <= intMax && mn >= intMin && mn <= intMax && mx >= intMin && mx <= intMax;
            return new RipianoMappatura
            {
                Posizione = gr.Pos, Media = m, Minimo = mn, Massimo = mx,
                NumeroLetture = gr.Valori.Length, Conforme = ok
            };
        }).ToList();

        var stat = TestStatistici(gruppi.Select(x => x.Valori).ToList());

        return new RisultatoMappatura
        {
            CorrezioneCdr = correzione,
            TemperaturaMedia = media,
            ScartoTipoMedia = stdMedia,
            IncertezzaComposta = uc,
            IncertezzaEstesa = U,
            DifferenzaNominale = media - N,
            IntervalloMinimo = intMin,
            IntervalloMassimo = intMax,
            NumeroLetture = g.Length,
            Ripiani = ripiani,
            FisherF = stat.F,
            FisherFCritico = stat.FCrit,
            VarianzeOmogenee = stat.Omogenee,
            KruskalWallis = stat.Kw,
            KruskalWallisP = stat.KwP,
            DifferenzeSignificative = stat.Significative,
            Giudizio = stat.Giudizio
        };
    }

    // ── Test statistici (F, Cochran, Kruskal-Wallis) ───────────────
    private static (double F, double FCrit, bool Omogenee, double Kw, double KwP, bool Significative, string Giudizio)
        TestStatistici(List<double[]> gruppi)
    {
        int p = gruppi.Count;
        if (p < 2)
            return (0, 0, true, 0, 1, false,
                "Una sola posizione: confronto tra ripiani non applicabile.");

        var medie = gruppi.Select(v => v.Average()).ToArray();
        var varianze = gruppi.Select(DeviazioneVarianzaCampionaria).ToArray();
        int nPrimo = gruppi[0].Length;
        int glWithin = gruppi.Sum(v => v.Length - 1);

        double ssw = gruppi.Sum(v => { double m = v.Average(); return v.Sum(x => (x - m) * (x - m)); });
        double msw = glWithin > 0 ? ssw / glWithin : 1e-7;
        if (msw == 0) msw = 1e-7;

        // F secondo il foglio: varianza tra le medie / n, su MSW.
        double varMedie = DeviazioneVarianzaCampionaria(medie);
        double msb = (varMedie == 0 ? 1e-9 : varMedie) / nPrimo;
        double f = msb / msw;
        double fCrit = StatisticheDistribuzioni.FQuantile(0.95, p - 1, glWithin);

        // Cochran: omogeneità delle varianze.
        double sommaVar = varianze.Sum();
        double c = sommaVar == 0 ? 0 : varianze.Max() / sommaVar;
        double cCrit = CochranCritico(p, nPrimo);
        bool omogenee = c < cCrit;

        // Kruskal-Wallis con correzione dei ties.
        var (kw, kwP) = KruskalWallis(gruppi);

        // Giudizio: se varianze omogenee usa il test F, altrimenti il non parametrico.
        bool significative = omogenee ? f >= fCrit : kwP < 0.01;
        string giudizio = significative
            ? "Le distribuzioni presentano differenze statisticamente significative tra le posizioni testate."
            : "Le distribuzioni non presentano differenze statisticamente significative tra le posizioni testate.";

        return (f, fCrit, omogenee, kw, kwP, significative, giudizio);
    }

    private static (double Kw, double P) KruskalWallis(List<double[]> gruppi)
    {
        var tutti = gruppi.SelectMany((v, gi) => v.Select(x => (Valore: x, Gruppo: gi))).ToList();
        int nTot = tutti.Count;
        var ordinati = tutti.Select((t, idx) => (t.Valore, t.Gruppo, Indice: idx)).OrderBy(t => t.Valore).ToList();

        // Ranghi medi per i ties.
        var rango = new double[nTot];
        double sommaTie = 0;
        int i = 0;
        while (i < nTot)
        {
            int j = i;
            while (j + 1 < nTot && ordinati[j + 1].Valore == ordinati[i].Valore) j++;
            double r = (i + j) / 2.0 + 1;   // rango medio (1-based)
            int t = j - i + 1;
            sommaTie += (double)t * t * t - t;
            for (int q = i; q <= j; q++) rango[ordinati[q].Indice] = r;
            i = j + 1;
        }

        // Somma dei ranghi per gruppo.
        var sommaRango = new double[gruppi.Count];
        var nGruppo = new int[gruppi.Count];
        int pos = 0;
        for (int gi = 0; gi < gruppi.Count; gi++)
        {
            nGruppo[gi] = gruppi[gi].Length;
            for (int q = 0; q < gruppi[gi].Length; q++) sommaRango[gi] += rango[pos++];
        }

        double h = 12.0 / (nTot * (nTot + 1.0)) * Enumerable.Range(0, gruppi.Count)
            .Sum(gi => sommaRango[gi] * sommaRango[gi] / nGruppo[gi]) - 3.0 * (nTot + 1.0);
        double correzione = 1.0 - sommaTie / ((double)nTot * nTot * nTot - nTot);
        double kw = correzione == 0 ? h : h / correzione;
        double pval = StatisticheDistribuzioni.ChiQuadroCodaDestra(kw, gruppi.Count - 1);
        return (kw, pval);
    }

    /// <summary>Valore critico di Cochran (α=0,05) via distribuzione F.</summary>
    private static double CochranCritico(int k, int n)
    {
        int df1 = n - 1, df2 = (k - 1) * (n - 1);
        if (df1 <= 0 || df2 <= 0) return 1.0;
        double fq = StatisticheDistribuzioni.FQuantile(1.0 - 0.05 / k, df1, df2);
        return 1.0 / (1.0 + (k - 1) / fq);
    }

    // ── Correzione e deriva del CDR (come negli altri moduli) ──────
    private static double CorrezioneNominale(IReadOnlyList<PuntoCertificatoCdr> cert, double N)
    {
        var lower = cert.Where(c => N - c.TIndicata >= 0).ToList();
        var upper = cert.Where(c => c.TIndicata - N > 0).ToList();
        if (lower.Count == 0 || upper.Count == 0)
        {
            var v = cert.OrderBy(c => Math.Abs(c.TIndicata - N)).First();
            return v.TRiferimento - v.TIndicata;
        }
        double w32 = lower.Max(c => c.TIndicata), v32 = lower.Max(c => c.TRiferimento);
        double w33 = upper.Min(c => c.TIndicata), v33 = upper.Min(c => c.TRiferimento);
        double x32 = v32 - w32, x33 = v33 - w33;
        return (x32 > 0 && x33 > 0) ? (x32 * (N - w32) + x33 * (w33 - N)) / (w33 - w32) : x32 + x33;
    }

    private static double Deriva(IReadOnlyList<PuntoCertificatoCdr> prim, IReadOnlyList<PuntoCertificatoCdr> sec, double N)
    {
        double errPrim = ErroreVicino(prim, N);
        double errSec = ErroreVicino(sec, N);
        return Math.Abs(errSec - errPrim) / S3;
    }

    /// <summary>Errore (indicata − riferim) del punto con T riferim ≤ N più grande (VLOOKUP approssimato).</summary>
    private static double ErroreVicino(IReadOnlyList<PuntoCertificatoCdr> cert, double N)
    {
        var candidati = cert.Where(c => c.TRiferimento <= N + 1e-12).OrderBy(c => c.TRiferimento).ToList();
        var p = candidati.Count > 0 ? candidati[^1] : cert.OrderBy(c => c.TRiferimento).First();
        return p.TIndicata - p.TRiferimento;
    }

    private static double DeviazioneStandardCampionaria(double[] v)
    {
        int n = v.Length;
        if (n < 2) return 0;
        double m = v.Average(); double s = 0;
        foreach (var x in v) s += (x - m) * (x - m);
        return Math.Sqrt(s / (n - 1));
    }

    private static double DeviazioneVarianzaCampionaria(double[] v)
    {
        int n = v.Length;
        if (n < 2) return 0;
        double m = v.Average(); double s = 0;
        foreach (var x in v) s += (x - m) * (x - m);
        return s / (n - 1);
    }
}
