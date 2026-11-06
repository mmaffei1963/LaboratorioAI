using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Valori attesi (dal foglio storico) per un caso di validazione incubatori.</summary>
public record AttesoIncubatore
{
    public double TemperaturaMediaRiferimento { get; init; }
    public double TemperaturaMediaStrumento { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double FattoreCorrezione { get; init; }
    public double AllarmeMinimo { get; init; }
    public double AllarmeMassimo { get; init; }
    public bool Conforme { get; init; }
}

/// <summary>Caso di riferimento congelato per la validazione del motore incubatori.</summary>
public record CasoValidazioneIncubatore
{
    public string Nome { get; init; } = "";
    public string Descrizione { get; init; } = "";
    public required DatiTaraturaIncubatore Dati { get; init; }
    public required AttesoIncubatore Atteso { get; init; }
}

/// <summary>
/// Set di riferimento CONGELATO per la validazione del motore di calcolo delle
/// tarature di incubatori/celle (Modulo Statistico 04-02). Dati d'ingresso e
/// valori attesi ricavati dai fogli VAL_Caso1/2/3 e Validazione_Foglio del foglio
/// storico. NON modificare i numeri: sono l'evidenza indipendente di validazione.
/// </summary>
public static class SetRiferimentoIncubatori
{
    public const string Versione = "1.0";
    public const double Tolleranza = 1e-4;

    private static IReadOnlyList<PuntoCertificatoCdr> Cert() =>
    [
        // Nota: il foglio storico usa un'unica incertezza estesa del CDR (0,06) per
        // tutti i punti; qui è uniforme così che u(CR) resti 0,06 nei casi VAL e
        // riproduca il foglio (u(CR) è il valore più alto dei due punti vicini). In esercizio si inseriscono i valori
        // per-punto reali del certificato del CDR (es. 0,03 attorno a 0 °C).
        new() { TIndicata = -25.08, TRiferimento = -25.04, IncertezzaEstesa = 0.06 },
        new() { TIndicata =   0.00, TRiferimento =   0.00, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  25.05, TRiferimento =  25.04, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  40.03, TRiferimento =  40.00, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  99.96, TRiferimento =  99.98, IncertezzaEstesa = 0.06 },
    ];

    // Taratura secondaria/interna del CDR (per il calcolo della deriva u(stCR)).
    private static IReadOnlyList<PuntoCertificatoCdr> CertSec() =>
    [
        new() { TIndicata = -25.11, TRiferimento = -24.95 },
        new() { TIndicata =  -0.11, TRiferimento =   0.00 },
        new() { TIndicata =  25.00, TRiferimento =  25.07 },
        new() { TIndicata =  39.82, TRiferimento =  39.90 },
        new() { TIndicata =  99.89, TRiferimento = 100.02 },
    ];

    private static DatiTaraturaIncubatore Dati(double nom, double tol, double uStab, double[] tref, double[] tw) =>
        new()
        {
            Certificato = Cert(),
            CertificatoSecondario = CertSec(),
            ValoreNominale = nom,
            Tolleranza = tol,
            RisoluzioneStrumento = 0.1,
            IncertezzaInstabilita = uStab,
            DispersioneMappatura = 0,
            FattoreCopertura = 2.0,
            Letture = Enumerable.Range(0, tref.Length)
                .Select(i => new LetturaIncubatore { Posizione = "A", TemperaturaRilevata = tref[i], LetturaStrumento = tw[i] })
                .ToList()
        };

    public static IReadOnlyList<CasoValidazioneIncubatore> Casi() =>
    [
        new CasoValidazioneIncubatore
        {
            Nome = "Caso 1",
            Descrizione = "Errore positivo dello strumento (Tw < Tr)",
            Dati = Dati(20, 1, 0.04618802153517113,
                [20.2, 20.2, 20.21, 20.19, 20.2, 20.2, 20.21, 20.2, 20.19, 20.2, 20.2, 20.21, 20.2, 20.19, 20.2, 20.2],
                [20, 20, 20.01, 20, 20, 20.01, 20, 20, 20, 20.01, 20, 20, 20.01, 20, 20, 20]),
            Atteso = new AttesoIncubatore
            {
                TemperaturaMediaRiferimento = 20.189999999999998,
                TemperaturaMediaStrumento = 20.0025,
                IncertezzaEstesa = 0.12441195012270216,
                FattoreCorrezione = 0.18749999999999645,
                AllarmeMinimo = 18.936911950122706,
                AllarmeMassimo = 20.6880880498773,
                Conforme = true
            }
        },
        new CasoValidazioneIncubatore
        {
            Nome = "Caso 2",
            Descrizione = "Errore negativo dello strumento (Tw > Tr)",
            Dati = Dati(20, 1, 0.04618802153517113,
                [20.2, 20.21, 20.2, 20.2, 20.19, 20.2, 20.21, 20.2, 20.2, 20.19, 20.2, 20.21, 20.2, 20.2, 20.19, 20.2],
                [20.4, 20.39, 20.4, 20.41, 20.4, 20.4, 20.39, 20.4, 20.41, 20.4, 20.4, 20.39, 20.4, 20.41, 20.4, 20.4]),
            Atteso = new AttesoIncubatore
            {
                TemperaturaMediaRiferimento = 20.189999999999998,
                TemperaturaMediaStrumento = 20.4,
                IncertezzaEstesa = 0.12448560827126588,
                FattoreCorrezione = -0.21000000000000085,
                AllarmeMinimo = 19.334485608271265,
                AllarmeMassimo = 21.085514391728736,
                Conforme = true
            }
        },
        new CasoValidazioneIncubatore
        {
            Nome = "Caso 3",
            Descrizione = "Punto negativo con tolleranza ridotta",
            Dati = Dati(-20, 0.5, 0.0404145188432743,
                [-19.9, -19.89, -19.9, -19.91, -19.9, -19.89, -19.9, -19.91, -19.9, -19.89, -19.9, -19.91, -19.9, -19.89, -19.9, -19.91],
                [-20.1, -20.11, -20.1, -20.09, -20.1, -20.11, -20.1, -20.09, -20.1, -20.11, -20.1, -20.09, -20.1, -20.11, -20.1, -20.09]),
            Atteso = new AttesoIncubatore
            {
                TemperaturaMediaRiferimento = -19.860000000000003,
                TemperaturaMediaStrumento = -20.1,
                IncertezzaEstesa = 0.11627553482998976,
                FattoreCorrezione = 0.23999999999999844,
                AllarmeMinimo = -20.62372446517001,
                AllarmeMassimo = -19.856275534829987,
                Conforme = true
            }
        },
    ];
}
