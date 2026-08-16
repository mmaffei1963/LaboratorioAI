using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Valori attesi per un caso di validazione della mappatura.</summary>
public record AttesoMappatura
{
    public double TemperaturaMedia { get; init; }
    public double ScartoTipoMedia { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double DifferenzaNominale { get; init; }
    public double IntervalloMinimo { get; init; }
    public double IntervalloMassimo { get; init; }
    /// <summary>Medie attese per ripiano (posizione → media).</summary>
    public IReadOnlyDictionary<string, double> MediePosizioni { get; init; } = new Dictionary<string, double>();
}

public record CasoValidazioneMappatura
{
    public string Nome { get; init; } = "";
    public string Descrizione { get; init; } = "";
    public required DatiMappatura Dati { get; init; }
    public required AttesoMappatura Atteso { get; init; }
}

/// <summary>
/// Set di riferimento CONGELATO per la validazione del motore di mappatura
/// (Modulo 04-03), dai casi VAL_Caso1/2/3 del foglio storico. NON modificare i numeri.
/// </summary>
public static class SetRiferimentoMappatura
{
    public const string Versione = "1.0";
    public const double Tolleranza = 5e-4;

    private static IReadOnlyList<PuntoCertificatoCdr> Cert() =>
    [
        new() { TIndicata = -25.11, TRiferimento = -24.95, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  -0.11, TRiferimento =   0.00, IncertezzaEstesa = 0.03 },
        new() { TIndicata =  25.00, TRiferimento =  25.07, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  39.82, TRiferimento =  39.90, IncertezzaEstesa = 0.06 },
        new() { TIndicata =  99.89, TRiferimento = 100.02, IncertezzaEstesa = 0.06 },
    ];

    private static DatiMappatura Dati(double nom, double tol, (string p, double t)[] letture) =>
        new()
        {
            Certificato = Cert(),
            ValoreNominale = nom,
            Tolleranza = tol,
            RisoluzioneCdr = 0.01,
            IncertezzaEstesaCertificato = 0.03,   // UE di testata del CDR dei casi VAL
            IncertezzaInstabilita = 0,
            FattoreCopertura = 2.0,
            Letture = letture.Select(l => new LetturaMappatura { Posizione = l.p, TemperaturaRilevata = l.t }).ToList()
        };

    public static IReadOnlyList<CasoValidazioneMappatura> Casi() =>
    [
        new CasoValidazioneMappatura
        {
            Nome = "Caso 1", Descrizione = "Un solo ripiano",
            Dati = Dati(5, 0.3, [("A", 5), ("A", 5.01), ("A", 4.99), ("A", 5), ("A", 5)]),
            Atteso = new AttesoMappatura
            {
                TemperaturaMedia = 5.0781401831939466, ScartoTipoMedia = 0.0031622776601683117,
                IncertezzaEstesa = 0.032761766334148276, DifferenzaNominale = 0.07814018319394656,
                IntervalloMinimo = 4.732761766334148, IntervalloMassimo = 5.267238233665852,
                MediePosizioni = new Dictionary<string, double> { ["A"] = 5.0781401831939466 }
            }
        },
        new CasoValidazioneMappatura
        {
            Nome = "Caso 2", Descrizione = "Due ripiani",
            Dati = Dati(5, 0.3,
                [("A", 5), ("A", 5.01), ("A", 4.99), ("A", 5), ("A", 5),
                 ("B", 5.04), ("B", 5.05), ("B", 5.03), ("B", 5.04), ("B", 5.05)]),
            Atteso = new AttesoMappatura
            {
                TemperaturaMedia = 5.0991401831939465, ScartoTipoMedia = 0.007371114795831978,
                IncertezzaEstesa = 0.03536476589299957, DifferenzaNominale = 0.09914018319394646,
                IntervalloMinimo = 4.735364765892999, IntervalloMassimo = 5.264635234107001,
                MediePosizioni = new Dictionary<string, double> { ["A"] = 5.0781401831939466, ["B"] = 5.120140183193946 }
            }
        },
        new CasoValidazioneMappatura
        {
            Nome = "Caso 3", Descrizione = "Quattro ripiani",
            Dati = Dati(5, 0.3,
                [("A", 4.85), ("A", 4.86), ("A", 4.84), ("A", 4.85), ("A", 4.87),
                 ("B", 5), ("B", 5.02), ("B", 4.99), ("B", 5.01), ("B", 5),
                 ("C", 5.13), ("C", 5.15), ("C", 5.14), ("C", 5.13), ("C", 5.16),
                 ("D", 5.28), ("D", 5.3), ("D", 5.27), ("D", 5.29), ("D", 5.31)]),
            Atteso = new AttesoMappatura
            {
                TemperaturaMedia = 5.150640183193947, ScartoTipoMedia = 0.03718994629282432,
                IncertezzaEstesa = 0.08102901797742562, DifferenzaNominale = 0.15064018319394723,
                IntervalloMinimo = 4.781029017977426, IntervalloMassimo = 5.218970982022574,
                MediePosizioni = new Dictionary<string, double>
                { ["A"] = 4.932140183193946, ["B"] = 5.082140183193947, ["C"] = 5.220140183193946, ["D"] = 5.368140183193947 }
            }
        },
    ];
}
