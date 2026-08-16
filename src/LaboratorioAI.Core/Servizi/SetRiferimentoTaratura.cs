using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Set di riferimento CONGELATO per la validazione del motore di calcolo della
/// taratura. I dati d'ingresso (certificato CDR, risoluzione, k, letture) e i
/// valori attesi provengono, una volta per tutte, dal foglio Excel storico
/// «Modulo Statistico 04-01» (fogli nascosti VAL_Caso1/2/3 e Validazione_Foglio).
/// I tre casi coprono: errore nullo, errore positivo (ripetibilità), errore
/// negativo con deriva del riferimento (stabilità).
///
/// NON modificare i numeri: sono l'evidenza indipendente contro cui si verifica
/// che il calcolo dell'applicazione riproduca quello validato del foglio.
/// </summary>
public static class SetRiferimentoTaratura
{
    /// <summary>Versione del set di riferimento (da incrementare se i dati cambiano).</summary>
    public const string Versione = "1.0";

    /// <summary>Tolleranza di accettazione sul confronto (come nel foglio: 1e-6).</summary>
    public const double Tolleranza = 1e-6;

    // Certificato del CDR, comune ai tre casi (T indicata, T riferim, differenza, UE, u(CR)).
    private static IReadOnlyList<PuntoCertificatoCdr> Certificato() =>
    [
        new() { TIndicata = -25.08, TRiferimento = -25.04, Differenza = -0.16, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
        new() { TIndicata =   0.00, TRiferimento =   0.00, Differenza = -0.11, IncertezzaEstesa = 0.03, IncertezzaTipo = 0.025 },
        new() { TIndicata =  25.05, TRiferimento =  25.04, Differenza = -0.07, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
        new() { TIndicata =  40.03, TRiferimento =  40.00, Differenza = -0.08, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
        new() { TIndicata =  99.96, TRiferimento =  99.98, Differenza = -0.13, IncertezzaEstesa = 0.06, IncertezzaTipo = 0.025 },
    ];

    private static PuntoTaratura Punto(string et, double[] nom, double[] tw) =>
        new()
        {
            Etichetta = et,
            Letture = Enumerable.Range(0, nom.Length)
                .Select(i => new LetturaTaratura { TemperaturaNominale = nom[i], LetturaStrumento = tw[i] })
                .ToList()
        };

    private static double[] Ripeti(double v, int n = 10) => Enumerable.Repeat(v, n).ToArray();

    private static AttesoPuntoValidazione Atteso(string et, double tr, double tw, double diff, double u, double corr) =>
        new()
        {
            Etichetta = et, TemperaturaMediaRiferimento = tr, TemperaturaMediaStrumento = tw,
            DifferenzaMedia = diff, IncertezzaEstesa = u, FattoreCorrezione = corr
        };

    private static DatiTaratura Dati(params PuntoTaratura[] punti) =>
        new() { Certificato = Certificato(), RisoluzioneStrumento = 0.1, FattoreCopertura = 2.0, Punti = punti };

    /// <summary>I tre casi di riferimento congelati.</summary>
    public static IReadOnlyList<CasoValidazione> Casi() =>
    [
        // ── Caso 1: errore nullo (letture costanti) ────────────────────
        new CasoValidazione
        {
            Nome = "Caso 1",
            Descrizione = "Verifica base — errore nullo, letture costanti",
            Dati = Dati(
                Punto("A", Ripeti(-20), Ripeti(-20.1)),
                Punto("B", Ripeti(0),   Ripeti(0)),
                Punto("C", Ripeti(20),  Ripeti(20)),
                Punto("D", Ripeti(44),  Ripeti(44))),
            Attesi =
            [
                Atteso("A", -20.117831272277105, -20.099999999999998, 0.01783127227710324, 0.07637626158259735, -0.01783127227710679),
                Atteso("B",  -0.11456769823263754, 0.0,               0.11456769823263754, 0.07637626158259735, -0.11456769823263754),
                Atteso("C",  19.88869587581183,   20.0,               0.1113041241881696,  0.07637626158259735, -0.1113041241881696),
                Atteso("D",  43.892612164665195,  44.0,               0.107387835334805,   0.07637626158259735, -0.107387835334805),
            ]
        },

        // ── Caso 2: errore positivo con ripetibilità ───────────────────
        new CasoValidazione
        {
            Nome = "Caso 2",
            Descrizione = "Errore positivo (sottostima) — ripetibilità non nulla",
            Dati = Dati(
                Punto("A", Ripeti(-19.5), [-19.3, -19.4, -19.3, -19.4, -19.3, -19.4, -19.3, -19.4, -19.3, -19.4]),
                Punto("B", Ripeti(5),     [5.2, 5.1, 5.2, 5.1, 5.2, 5.1, 5.2, 5.1, 5.2, 5.1]),
                Punto("C", Ripeti(25),    [25.2, 25.3, 25.2, 25.3, 25.2, 25.3, 25.2, 25.3, 25.2, 25.3]),
                Punto("D", Ripeti(50),    [49.8, 49.9, 49.8, 49.9, 49.8, 49.9, 49.8, 49.9, 49.8, 49.9])),
            Attesi =
            [
                Atteso("A", -19.617749682925993, -19.35,              0.2677496829259933,   0.08333333333333305, -0.26774968292599155),
                Atteso("B",   4.886248195278481,   5.15,              0.26375180472152016,  0.08333333333333341, -0.2637518047215197),
                Atteso("C",  24.889511769322947,  25.25,              0.3604882306770527,   0.08333333333333352, -0.3604882306770527),
                Atteso("D",  49.893591236878535,  49.849999999999994, -0.04359123687853739, 0.08333333333333352,  0.04359123687854094),
            ]
        },

        // ── Caso 3: errore negativo con deriva del riferimento ─────────
        new CasoValidazione
        {
            Nome = "Caso 3",
            Descrizione = "Errore negativo (sovrastima) — deriva del riferimento (stabilità)",
            Dati = Dati(
                Punto("A", [-10, -10.01, -10.02, -10.03, -10.04, -10.05, -10.06, -10.07, -10.08, -10.09],
                           [-10.2, -10.2, -10.1, -10.2, -10.1, -10.2, -10.1, -10.2, -10.1, -10.2]),
                Punto("B", [10, 10.01, 10.02, 10.03, 10.04, 10.05, 10.06, 10.07, 10.08, 10.09],
                           [9.8, 9.9, 9.8, 9.9, 9.8, 9.9, 9.8, 9.9, 9.8, 9.9]),
                Punto("C", [30, 30.01, 30.02, 30.03, 30.04, 30.05, 30.06, 30.07, 30.08, 30.09],
                           [30.2, 30.1, 30.2, 30.1, 30.2, 30.1, 30.2, 30.1, 30.2, 30.1]),
                Punto("D", [60, 60.01, 60.02, 60.03, 60.04, 60.05, 60.06, 60.07, 60.08, 60.09],
                           [59.8, 59.7, 59.8, 59.7, 59.8, 59.7, 59.8, 59.7, 59.8, 59.7])),
            Attesi =
            [
                Atteso("A", -10.161206828296471, -10.16,              0.0012068282964722953, 0.09798408665128094, -0.0012068282964712296),
                Atteso("B",   9.932071431831195,   9.850000000000001, -0.08207143183119588,  0.09821061898455823,  0.08207143183119392),
                Atteso("C",  29.935335005875658,  30.15,              0.2146649941243357,    0.09821061898455857, -0.21466499412434104),
                Atteso("D",  59.940230366942366,  59.75,              -0.19023036694236808,  0.09821061898455816,  0.19023036694236595),
            ]
        },
    ];
}
