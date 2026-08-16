using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <inheritdoc cref="IServizioBilancia" />
///
/// <remarks>
/// Riproduce il foglio storico «Modulo Statistico 45» secondo EURAMET cg-18,
/// verificato sull'esempio della guida (pag. 64):
/// - eccentricità: scostamento massimo dalla prima posizione, conforme se &lt; 15 divisioni;
/// - ripetibilità: scarto tipo delle letture; u_rip composta = √(s² + uf²/12);
/// - errore d'indicazione E = lettura − valore convenzionale;
/// - budget per carico: u(E) = √(u_dig0² + u_digL² + u_rip² + u_ecc² + u_mc² + w_mb² + u_md²),
///   con u_dig = uf/√12, u_ecc effetto eccentricità, w_mb spinta aerostatica,
///   u_md deriva; U(E) = k·u(E); U d'uso include l'effetto temperatura.
/// </remarks>
public class ServizioBilancia : IServizioBilancia
{
    private static readonly double S3 = Math.Sqrt(3.0);
    private static readonly double S12 = Math.Sqrt(12.0);

    public RisultatoBilancia Calcola(DatiBilancia d)
    {
        double uf = d.UnitaFormato;
        if (uf <= 0) throw new ArgumentException("Unità di formato non valida.");
        if (d.Linearita.Count == 0) throw new ArgumentException("Inserisci almeno un punto di linearità.");

        // ── Eccentricità ───────────────────────────────────────────
        var ecc = d.Eccentricita.Letture;
        double eccMax = 0, eccMedia = 0;
        if (ecc.Count > 0)
        {
            double primo = ecc[0];
            eccMax = ecc.Max(x => Math.Abs(x - primo));
            eccMedia = ecc.Average();
        }
        bool eccConforme = eccMax < 15 * uf;

        // ── Ripetibilità ───────────────────────────────────────────
        double urep = DeviazioneStandardCampionaria(d.Ripetibilita);
        double uripComb = Math.Sqrt(urep * urep + uf * uf / 12.0);

        double eccConv = d.Eccentricita.ValoreConvenzionale;
        double eccCarico = d.Eccentricita.CaricoNominale;
        double dT = Math.Abs(d.TemperaturaUtilizzo - d.TemperaturaTaratura);

        var punti = new List<RisultatoPuntoBilancia>();
        foreach (var p in d.Linearita)
        {
            double L = p.CaricoNominale;
            double errore = p.Lettura - p.ValoreConvenzionale;

            double uDig0 = uf / S12;
            double uDigL = L == 0 ? 0 : uf / S12;
            double uIecc = (L == 0 || eccConv == 0 || eccCarico == 0)
                ? 0
                : (eccMedia * Math.Abs(eccMax) / (2 * eccConv * S3)) / eccCarico * L;
            double uMc = p.IncertezzaMassa;
            double wMb = L == 0 ? 0 : ((0.1 * (1.2 / 8000.0)) + (p.MpeMassa / (4 * L))) / S3 * L;
            double uMd = L == 0 ? 0 : (uMc * 2 * 1.25) / S3;

            double uE = Math.Sqrt(uDig0 * uDig0 + uDigL * uDigL + urep * urep + uIecc * uIecc
                + uMc * uMc + wMb * wMb + uMd * uMd);

            // Gradi di libertà effettivi (contributi finiti: ripetibilità ed eccentricità, ν=4).
            double denom = (Math.Pow(urep, 4) / 4.0) + (Math.Pow(uIecc, 4) / 4.0);
            double veff = denom > 0 ? Math.Floor(Math.Pow(uE, 4) / denom) : double.PositiveInfinity;
            double k = FattoreCopertura(veff);
            double UE = uE * k;

            double uMt = (dT * d.CoefficienteSensibilitaTermica * p.ValoreConvenzionale) / S3;
            double uso = 2 * Math.Sqrt(UE * UE / 4.0 + errore * errore / 3.0
                + uripComb * uripComb + Math.Pow(eccMax / S3, 2) + uMt * uMt);

            punti.Add(new RisultatoPuntoBilancia
            {
                CaricoNominale = L,
                ValoreConvenzionale = p.ValoreConvenzionale,
                Lettura = p.Lettura,
                Errore = errore,
                IncertezzaTipoErrore = uE,
                FattoreCopertura = k,
                IncertezzaEstesa = UE,
                IncertezzaEstesaUso = uso,
                GradiLibertaEffettivi = double.IsInfinity(veff) ? 0 : veff
            });
        }

        double maxU = punti.Count > 0 ? punti.Max(x => x.IncertezzaEstesa) : 0;
        double maxUso = punti.Count > 0 ? punti.Max(x => x.IncertezzaEstesaUso) : 0;

        return new RisultatoBilancia
        {
            EccentricitaScostamentoMax = eccMax,
            EccentricitaMedia = eccMedia,
            EccentricitaConforme = eccConforme,
            RipetibilitaScartoTipo = urep,
            RipetibilitaComposta = uripComb,
            Punti = punti,
            IncertezzaEstesaMax = maxU,
            IncertezzaEstesaUsoMax = maxUso,
            Conforme = maxU <= d.MaxIncertezzaAmmessa
        };
    }

    /// <summary>Fattore di copertura per livello di fiducia ~95,45% dai gradi di libertà effettivi.</summary>
    private static double FattoreCopertura(double veff)
    {
        if (double.IsInfinity(veff) || veff >= 100) return 2.0;
        // Coefficienti t per 95,45% (approssimazione da tabella EURAMET/GUM).
        (double v, double k)[] tab =
        {
            (1, 13.97), (2, 4.53), (3, 3.31), (4, 2.87), (5, 2.65), (6, 2.52),
            (7, 2.43), (8, 2.37), (10, 2.28), (15, 2.18), (20, 2.13), (30, 2.09),
            (50, 2.05), (100, 2.02)
        };
        if (veff <= tab[0].v) return tab[0].k;
        for (int i = 1; i < tab.Length; i++)
            if (veff <= tab[i].v)
            {
                var (v0, k0) = tab[i - 1];
                var (v1, k1) = tab[i];
                return k0 + (k1 - k0) * (veff - v0) / (v1 - v0);
            }
        return 2.0;
    }

    private static double DeviazioneStandardCampionaria(IReadOnlyList<double> v)
    {
        int n = v.Count;
        if (n < 2) return 0;
        double m = v.Average(), s = 0;
        foreach (var x in v) s += (x - m) * (x - m);
        return Math.Sqrt(s / (n - 1));
    }
}
