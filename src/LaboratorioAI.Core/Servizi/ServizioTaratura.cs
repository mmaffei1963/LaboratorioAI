using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioTaratura" />
///
/// <remarks>
/// Riproduce la logica del foglio storico (Modulo Statistico 04-01), verificata
/// numericamente sui casi VAL_Caso1/2/3:
/// 1. correzione del CDR interpolata per regressione lineare (differenza vs
///    temperatura indicata), estesa per estrapolazione fuori dai punti;
/// 2. temperatura reale di riferimento tr = nominale + correzione;
/// 3. errore dello strumento Dw = Tw − tr;
/// 4. budget d'incertezza: u_rip = s(Tw)/√n, u_lab = u(CR) interpolata,
///    u_ris = risoluzione/(2√3), u_stab = (tr_primo − tr_ultimo)/(2√3);
/// 5. u_c = √(Σ contributi²), estesa U = k·u_c (k=2), correzione = −media(Dw).
/// </remarks>
public class ServizioTaratura : IServizioTaratura
{
    public RisultatoTaratura Calcola(DatiTaratura dati)
    {
        var cert = dati.Certificato;
        if (cert.Count < 2)
            throw new ArgumentException("Servono almeno due punti di certificato del CDR.");

        var xs = cert.Select(c => c.TIndicata).ToArray();
        var (mCorr, qCorr) = Regressione(xs, cert.Select(c => c.Differenza).ToArray());
        var (mUlab, qUlab) = Regressione(xs, cert.Select(c => c.IncertezzaTipo).ToArray());

        double uRis = dati.RisoluzioneStrumento / (2.0 * Math.Sqrt(3.0));

        var punti = new List<RisultatoPuntoTaratura>();
        foreach (var p in dati.Punti)
        {
            int n = p.Letture.Count;
            if (n == 0) continue;

            var tr = p.Letture.Select(l => l.TemperaturaNominale + (mCorr * l.TemperaturaNominale + qCorr)).ToArray();
            var tw = p.Letture.Select(l => l.LetturaStrumento).ToArray();
            var dw = new double[n];
            for (int i = 0; i < n; i++) dw[i] = tw[i] - tr[i];

            double meanTr = tr.Average();
            double meanTw = tw.Average();
            double meanDw = dw.Average();

            double uRip = DeviazioneStandardCampionaria(tw) / Math.Sqrt(n);
            double uLab = mUlab * meanTw + qUlab;
            double uStab = (tr[0] - tr[n - 1]) / (2.0 * Math.Sqrt(3.0));

            double uc = Math.Sqrt(uRip * uRip + uLab * uLab + uRis * uRis + uStab * uStab);

            punti.Add(new RisultatoPuntoTaratura
            {
                Etichetta = p.Etichetta,
                NumeroMisure = n,
                TemperaturaMediaRiferimento = meanTr,
                TemperaturaMediaStrumento = meanTw,
                DifferenzaMedia = meanDw,
                URipetibilita = uRip,
                URiferimento = uLab,
                URisoluzione = uRis,
                UStabilita = uStab,
                IncertezzaComposta = uc,
                IncertezzaEstesa = dati.FattoreCopertura * uc,
                FattoreCorrezione = -meanDw
            });
        }

        return new RisultatoTaratura
        {
            Punti = punti,
            PendenzaCorrezione = mCorr,
            IntercettaCorrezione = qCorr
        };
    }

    /// <summary>Regressione lineare y = m·x + q (minimi quadrati), come LINEST.</summary>
    private static (double M, double Q) Regressione(double[] x, double[] y)
    {
        int n = x.Length;
        double mx = x.Average(), my = y.Average();
        double sxx = 0, sxy = 0;
        for (int i = 0; i < n; i++)
        {
            sxx += (x[i] - mx) * (x[i] - mx);
            sxy += (x[i] - mx) * (y[i] - my);
        }
        double m = sxx == 0 ? 0 : sxy / sxx;
        return (m, my - m * mx);
    }

    /// <summary>Scarto tipo campionario (n−1), come STDEV di Excel.</summary>
    private static double DeviazioneStandardCampionaria(double[] v)
    {
        int n = v.Length;
        if (n < 2) return 0;
        double m = v.Average();
        double s = 0;
        foreach (var x in v) s += (x - m) * (x - m);
        return Math.Sqrt(s / (n - 1));
    }
}
