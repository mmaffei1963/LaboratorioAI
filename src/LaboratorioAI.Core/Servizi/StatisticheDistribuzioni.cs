namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Funzioni di distribuzione statistica (F di Fisher e chi-quadro) implementate
/// numericamente, senza librerie esterne: gamma incompleta e beta incompleta
/// regolarizzate (algoritmi classici). Servono ai test della mappatura.
/// </summary>
public static class StatisticheDistribuzioni
{
    /// <summary>Coda destra della chi-quadro: P(X > x) con k gradi di libertà.</summary>
    public static double ChiQuadroCodaDestra(double x, double k)
    {
        if (x <= 0) return 1.0;
        return GammaQ(k / 2.0, x / 2.0);
    }

    /// <summary>Funzione di ripartizione della F di Fisher: P(F ≤ f) con (d1, d2) gradi di libertà.</summary>
    public static double FCdf(double f, double d1, double d2)
    {
        if (f <= 0) return 0.0;
        double x = d1 * f / (d1 * f + d2);
        return BetaI(d1 / 2.0, d2 / 2.0, x);
    }

    /// <summary>Quantile della F: valore f tale che P(F ≤ f) = p (per il valore critico).</summary>
    public static double FQuantile(double p, double d1, double d2)
    {
        double lo = 1e-6, hi = 1e6;
        for (int i = 0; i < 200; i++)
        {
            double mid = 0.5 * (lo + hi);
            if (FCdf(mid, d1, d2) < p) lo = mid; else hi = mid;
        }
        return 0.5 * (lo + hi);
    }

    // ── Gamma incompleta regolarizzata ─────────────────────────────
    private static double GammaLn(double x)
    {
        double[] c =
        {
            76.18009172947146, -86.50532032941677, 24.01409824083091,
            -1.231739572450155, 0.1208650973866179e-2, -0.5395239384953e-5
        };
        double y = x, tmp = x + 5.5;
        tmp -= (x + 0.5) * Math.Log(tmp);
        double ser = 1.000000000190015;
        for (int j = 0; j < 6; j++) { y += 1; ser += c[j] / y; }
        return -tmp + Math.Log(2.5066282746310005 * ser / x);
    }

    private static double GammaP(double a, double x)
    {
        if (x <= 0) return 0.0;
        if (x < a + 1.0)
        {
            double ap = a, sum = 1.0 / a, del = sum;
            for (int n = 0; n < 200; n++)
            {
                ap += 1; del *= x / ap; sum += del;
                if (Math.Abs(del) < Math.Abs(sum) * 1e-12) break;
            }
            return sum * Math.Exp(-x + a * Math.Log(x) - GammaLn(a));
        }
        return 1.0 - GammaQContinua(a, x);
    }

    private static double GammaQ(double a, double x)
    {
        if (x <= 0) return 1.0;
        if (x < a + 1.0) return 1.0 - GammaP(a, x);
        return GammaQContinua(a, x);
    }

    private static double GammaQContinua(double a, double x)
    {
        double b = x + 1.0 - a, c = 1e30, d = 1.0 / b, h = d;
        for (int i = 1; i <= 200; i++)
        {
            double an = -i * (i - a);
            b += 2.0;
            d = an * d + b; if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = b + an / c; if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1.0 / d; double del = d * c; h *= del;
            if (Math.Abs(del - 1.0) < 1e-12) break;
        }
        return Math.Exp(-x + a * Math.Log(x) - GammaLn(a)) * h;
    }

    // ── Beta incompleta regolarizzata ──────────────────────────────
    private static double BetaI(double a, double b, double x)
    {
        if (x <= 0) return 0.0;
        if (x >= 1) return 1.0;
        double bt = Math.Exp(GammaLn(a + b) - GammaLn(a) - GammaLn(b)
            + a * Math.Log(x) + b * Math.Log(1.0 - x));
        if (x < (a + 1.0) / (a + b + 2.0))
            return bt * BetaCf(a, b, x) / a;
        return 1.0 - bt * BetaCf(b, a, 1.0 - x) / b;
    }

    private static double BetaCf(double a, double b, double x)
    {
        double qab = a + b, qap = a + 1.0, qam = a - 1.0;
        double c = 1.0, d = 1.0 - qab * x / qap;
        if (Math.Abs(d) < 1e-30) d = 1e-30;
        d = 1.0 / d; double h = d;
        for (int m = 1; m <= 200; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));
            d = 1.0 + aa * d; if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = 1.0 + aa / c; if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1.0 / d; h *= d * c;
            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));
            d = 1.0 + aa * d; if (Math.Abs(d) < 1e-30) d = 1e-30;
            c = 1.0 + aa / c; if (Math.Abs(c) < 1e-30) c = 1e-30;
            d = 1.0 / d; double del = d * c; h *= del;
            if (Math.Abs(del - 1.0) < 1e-12) break;
        }
        return h;
    }
}
