using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Valore atteso per un punto di carico del caso di validazione bilance.</summary>
public record AttesoPuntoBilancia
{
    public double IncertezzaTipoErrore { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double IncertezzaEstesaUso { get; init; }
}

/// <summary>
/// Set di riferimento CONGELATO per la validazione del motore bilance:
/// l'esempio ufficiale della guida EURAMET cg-18 (pag. 64). NON modificare i numeri.
/// </summary>
public static class SetRiferimentoBilancia
{
    public const string Versione = "1.0";
    public const double Tolleranza = 5e-7;

    public static DatiBilancia Caso() => new()
    {
        Portata = 220,
        UnitaFormato = 0.0001,
        CoefficienteSensibilitaTermica = 1.5e-6,
        MaxIncertezzaAmmessa = 0.02,
        TemperaturaTaratura = 21,
        TemperaturaUtilizzo = 22,
        Eccentricita = new ProvaEccentricita
        {
            CaricoNominale = 100, ValoreConvenzionale = 99.9999,
            Letture = [100.0006, 100.0004, 100.0005, 100.0007, 100.0005]
        },
        Ripetibilita = [100.0006, 100.0003, 100.0005, 100.0004, 100.0005],
        Linearita =
        [
            new() { CaricoNominale = 0,   ValoreConvenzionale = 0,        Lettura = 0,        IncertezzaMassa = 0,      MpeMassa = 0 },
            new() { CaricoNominale = 50,  ValoreConvenzionale = 50,       Lettura = 50.0004,  IncertezzaMassa = 1.5e-5, MpeMassa = 0.0001 },
            new() { CaricoNominale = 100, ValoreConvenzionale = 99.9999,  Lettura = 100.0006, IncertezzaMassa = 2.5e-5, MpeMassa = 0.00016 },
            new() { CaricoNominale = 150, ValoreConvenzionale = 149.9999, Lettura = 150.0009, IncertezzaMassa = 4e-5,   MpeMassa = 0.00026 },
            new() { CaricoNominale = 220, ValoreConvenzionale = 220.0001, Lettura = 220.0014, IncertezzaMassa = 6.2e-5, MpeMassa = 0.000285 },
        ]
    };

    public static IReadOnlyList<AttesoPuntoBilancia> Attesi() =>
    [
        new() { IncertezzaTipoErrore = 0.00011761519176618489, IncertezzaEstesa = 0.00033755560036895064, IncertezzaEstesaUso = 0.00047181611885786903 },
        new() { IncertezzaTipoErrore = 0.00046519038468240424, IncertezzaEstesa = 0.0009303807693648085,  IncertezzaEstesaUso = 0.0010932101243630392 },
        new() { IncertezzaTipoErrore = 0.000900255774396697,   IncertezzaEstesa = 0.001800511548793394,   IncertezzaEstesaUso = 0.0020084426248619253 },
        new() { IncertezzaTipoErrore = 0.0013466656709573827,  IncertezzaEstesa = 0.0026933313419147654,  IncertezzaEstesaUso = 0.002960326608224191 },
        new() { IncertezzaTipoErrore = 0.0019573182488657284,  IncertezzaEstesa = 0.003914636497731457,   IncertezzaEstesaUso = 0.004222745438850272 },
    ];

    // Eccentricità e ripetibilità attese.
    public const double EccentricitaScostamentoMax = 0.0002000000000066393;
    public const double RipetibilitaScartoTipo = 0.00011401754251369879;
    public const double RipetibilitaComposta = 0.00011761519176618489;
}
