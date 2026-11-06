using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioTaraturaIncubatori" />
///
/// <remarks>
/// Riproduce la logica del foglio storico «Modulo Statistico 04-02», verificata
/// numericamente sui casi VAL_Caso1/2/3:
/// 1. correzione del CDR alla temperatura di esercizio per interpolazione lineare
///    tra i due punti del certificato più vicini (sopra e sotto);
/// 2. temperatura reale tr = t_rif + correzione; differenza Dw = tw − tr;
/// 3. budget d'incertezza del misuratore:
///    u_c = √(u_rip² + u(CR)² + u_ris² + u_mappa² + u(stCR)²),
///    con u_rip = s(Dw)/√n, u(CR) = UE(CR)/k presa, in modo cautelativo, come il
///    valore più alto fra i due punti di certificato che racchiudono la temperatura
///    di esercizio (scelta del Responsabile di Laboratorio, 5/10/2026), u_ris =
///    risoluzione/(2√3), u_mappa = dispersione mappatura/(2√3), u(stCR) instabilità CDR;
/// 4. U = k·u_c; fattore di correzione = Tr − Tw; limiti allarme = nominale −
///    correzione ± (tolleranza − U); conforme se U ≤ tolleranza/2.
/// </remarks>
public class ServizioTaraturaIncubatori : IServizioTaraturaIncubatori
{
    public RisultatoTaraturaIncubatore Calcola(DatiTaraturaIncubatore d)
    {
        if (d.Certificato.Count < 2)
            throw new ArgumentException("Servono almeno due punti di certificato del CDR.");
        if (d.Letture.Count == 0)
            throw new ArgumentException("Inserisci almeno una lettura.");

        double n = d.Certificato.Count;
        double N = d.ValoreNominale;
        double k = d.FattoreCopertura <= 0 ? 2.0 : d.FattoreCopertura;

        double correzione = CorrezioneNominale(d.Certificato, N);

        // u(CR): incertezza estesa del CDR alla temperatura di esercizio, cautelativa:
        // la maggiore fra i due punti di certificato che la racchiudono (non si
        // interpola, per non sottostimare U). Fuori intervallo: punto più vicino.
        double uCr = IncertezzaCdrCautelativa(d.Certificato, N) / k;

        double uRis = d.RisoluzioneStrumento / (2.0 * Math.Sqrt(3.0));
        double uMappa = d.DispersioneMappatura / (2.0 * Math.Sqrt(3.0));

        // Instabilità/deriva del CDR: se è disponibile la taratura secondaria si
        // calcola come nel foglio (confronto delle correzioni al punto più vicino),
        // altrimenti si usa il valore fisso registrato sul CDR.
        double uStab = d.CertificatoSecondario is { Count: >= 2 }
            ? Deriva(d.Certificato, d.CertificatoSecondario, N)
            : d.IncertezzaInstabilita;

        int m = d.Letture.Count;
        var diff = new double[m];
        double sommaTr = 0, sommaTw = 0;
        for (int i = 0; i < m; i++)
        {
            double tr = d.Letture[i].TemperaturaRilevata + correzione;
            double tw = d.Letture[i].LetturaStrumento;
            diff[i] = tw - tr;
            sommaTr += tr;
            sommaTw += tw;
        }

        double meanTr = sommaTr / m;
        double meanTw = sommaTw / m;
        double meanDiff = diff.Average();

        double uRip = DeviazioneStandardCampionaria(diff) / Math.Sqrt(m);
        double uc = Math.Sqrt(uRip * uRip + uCr * uCr + uRis * uRis + uMappa * uMappa + uStab * uStab);
        double U = k * uc;
        double correzioneStrumento = meanTr - meanTw;

        return new RisultatoTaraturaIncubatore
        {
            CorrezioneCdr = correzione,
            TemperaturaMediaRiferimento = meanTr,
            TemperaturaMediaStrumento = meanTw,
            DifferenzaMedia = meanDiff,
            IncertezzaRipetibilita = uRip,
            IncertezzaRiferimento = uCr,
            IncertezzaRisoluzione = uRis,
            IncertezzaMappatura = uMappa,
            IncertezzaInstabilita = uStab,
            IncertezzaComposta = uc,
            IncertezzaEstesa = U,
            FattoreCorrezione = correzioneStrumento,
            AllarmeMinimo = N - correzioneStrumento - (d.Tolleranza - U),
            AllarmeMassimo = N - correzioneStrumento + (d.Tolleranza - U),
            Conforme = U <= d.Tolleranza / 2.0,
            NumeroLetture = m
        };
    }

    /// <summary>
    /// Incertezza estesa del CDR alla temperatura di esercizio, cautelativa: la
    /// maggiore fra i due punti di certificato che la racchiudono (su T indicata).
    /// Fuori dall'intervallo del certificato: punto più vicino. Con incertezza
    /// uniforme sul certificato coincide con il foglio storico.
    /// </summary>
    private static double IncertezzaCdrCautelativa(IReadOnlyList<PuntoCertificatoCdr> cert, double N)
    {
        var lower = cert.Where(c => N - c.TIndicata >= 0).OrderByDescending(c => c.TIndicata).FirstOrDefault();
        var upper = cert.Where(c => c.TIndicata - N > 0).OrderBy(c => c.TIndicata).FirstOrDefault();
        if (lower is null || upper is null)
            return cert.OrderBy(c => Math.Abs(c.TIndicata - N)).First().IncertezzaEstesa;
        return Math.Max(lower.IncertezzaEstesa, upper.IncertezzaEstesa);
    }

    /// <summary>
    /// Correzione del CDR alla temperatura di esercizio, per interpolazione tra i
    /// due punti di certificato più vicini (uno ≤ nominale, uno &gt; nominale).
    /// </summary>
    private static double CorrezioneNominale(IReadOnlyList<PuntoCertificatoCdr> cert, double N)
    {
        var lower = cert.Where(c => N - c.TIndicata >= 0).ToList();
        var upper = cert.Where(c => c.TIndicata - N > 0).ToList();

        // Fuori dall'intervallo del certificato: correzione del punto più vicino.
        if (lower.Count == 0 || upper.Count == 0)
        {
            var vicino = cert.OrderBy(c => Math.Abs(c.TIndicata - N)).First();
            return vicino.TRiferimento - vicino.TIndicata;
        }

        double w32 = lower.Max(c => c.TIndicata);
        double v32 = lower.Max(c => c.TRiferimento);
        double w33 = upper.Min(c => c.TIndicata);
        double v33 = upper.Min(c => c.TRiferimento);
        double x32 = v32 - w32;
        double x33 = v33 - w33;

        return (x32 > 0 && x33 > 0)
            ? (x32 * (N - w32) + x33 * (w33 - N)) / (w33 - w32)
            : x32 + x33;
    }

    /// <summary>
    /// Incertezza di instabilità/deriva del CDR u(stCR), come nel foglio: differenza
    /// tra la correzione della taratura primaria e quella secondaria, valutate al
    /// punto più vicino alla temperatura di esercizio, distribuita rettangolare (/√3).
    /// </summary>
    private static double Deriva(IReadOnlyList<PuntoCertificatoCdr> prim, IReadOnlyList<PuntoCertificatoCdr> sec, double N)
    {
        double aa32 = CorrezionePuntoVicino(prim, N);

        // W38 = massima T indicata tra i 3 punti primari più vicini.
        double w38 = prim.OrderBy(c => Math.Abs(c.TIndicata - N)).Take(3).Max(c => c.TIndicata);

        var sopra = sec.Where(c => c.TIndicata - N > 0).ToList();
        var sotto = sec.Where(c => N - c.TIndicata >= 0).ToList();
        if (sopra.Count == 0 || sotto.Count == 0)
        {
            var vic = sec.OrderBy(c => Math.Abs(c.TIndicata - N)).First();
            return Math.Abs(aa32 - (vic.TIndicata - vic.TRiferimento)) / Math.Sqrt(3.0);
        }

        double af31 = sopra.Min(c => c.TIndicata);   // sec: nearest above (indicata)
        double al31 = sopra.Min(c => c.TRiferimento); // sec: reale del punto sopra
        double ag31 = sotto.Max(c => c.TIndicata);   // sec: nearest below (indicata)
        double am31 = sotto.Max(c => c.TRiferimento); // sec: reale del punto sotto

        double ak32, al32;
        if (Math.Abs(w38 - af31) >= Math.Abs(w38 - ag31)) { ak32 = ag31; al32 = am31; }
        else { ak32 = af31; al32 = al31; }
        double am32 = ak32 - al32;

        return Math.Abs(aa32 - am32) / Math.Sqrt(3.0);
    }

    /// <summary>Correzione (T indicata − T riferim) del punto più vicino a N, tra i due che lo racchiudono.</summary>
    private static double CorrezionePuntoVicino(IReadOnlyList<PuntoCertificatoCdr> cert, double N)
    {
        var lower = cert.Where(c => N - c.TIndicata >= 0).ToList();
        var upper = cert.Where(c => c.TIndicata - N > 0).ToList();
        if (lower.Count == 0 || upper.Count == 0)
        {
            var v = cert.OrderBy(c => Math.Abs(c.TIndicata - N)).First();
            return v.TIndicata - v.TRiferimento;
        }
        double u31 = lower.Max(c => c.TIndicata), aa31 = lower.Max(c => c.TRiferimento);
        double t31 = upper.Min(c => c.TIndicata), z31 = upper.Min(c => c.TRiferimento);
        return Math.Abs(N - t31) >= Math.Abs(N - u31) ? (u31 - aa31) : (t31 - z31);
    }

    /// <summary>Scarto tipo campionario (n−1), come STDEV di Excel.</summary>
    private static double DeviazioneStandardCampionaria(double[] v)
    {
        int n = v.Length;
        if (n < 2) return 0;
        double media = v.Average();
        double s = 0;
        foreach (var x in v) s += (x - media) * (x - media);
        return Math.Sqrt(s / (n - 1));
    }
}
