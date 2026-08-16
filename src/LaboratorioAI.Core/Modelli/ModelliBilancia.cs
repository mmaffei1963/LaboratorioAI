namespace LaboratorioAI.Core.Modelli;

/// <summary>Prova di eccentricità: letture nelle posizioni con un carico centrale.</summary>
public record ProvaEccentricita
{
    public double CaricoNominale { get; init; }
    /// <summary>Valore convenzionale del carico usato per la prova.</summary>
    public double ValoreConvenzionale { get; init; }
    public IReadOnlyList<double> Letture { get; init; } = new List<double>();
}

/// <summary>Punto della prova di linearità/errore d'indicazione.</summary>
public record PuntoLinearita
{
    public double CaricoNominale { get; init; }
    /// <summary>Valore convenzionale del carico (dalla pesiera/combinazione).</summary>
    public double ValoreConvenzionale { get; init; }
    /// <summary>Lettura della bilancia.</summary>
    public double Lettura { get; init; }
    /// <summary>Incertezza tipo sul valore di massa u(mc) [g].</summary>
    public double IncertezzaMassa { get; init; }
    /// <summary>Errore massimo ammesso mpe della massa [g].</summary>
    public double MpeMassa { get; init; }
}

/// <summary>Dati per la taratura di una bilancia (Modulo 45, EURAMET cg-18).</summary>
public record DatiBilancia
{
    public double Portata { get; init; }
    /// <summary>Unità di formato / risoluzione (g).</summary>
    public double UnitaFormato { get; init; }
    public double CoefficienteSensibilitaTermica { get; init; }
    /// <summary>Massima incertezza estesa ammessa dal laboratorio (g).</summary>
    public double MaxIncertezzaAmmessa { get; init; } = 0.02;

    public double TemperaturaTaratura { get; init; }
    public double TemperaturaUtilizzo { get; init; }

    public required ProvaEccentricita Eccentricita { get; init; }
    public IReadOnlyList<double> Ripetibilita { get; init; } = new List<double>();
    public required IReadOnlyList<PuntoLinearita> Linearita { get; init; }
}

/// <summary>Esito di un punto del budget d'incertezza (per carico).</summary>
public record RisultatoPuntoBilancia
{
    public double CaricoNominale { get; init; }
    public double ValoreConvenzionale { get; init; }
    public double Lettura { get; init; }
    public double Errore { get; init; }
    public double IncertezzaTipoErrore { get; init; }   // u(E)
    public double FattoreCopertura { get; init; }        // k
    public double IncertezzaEstesa { get; init; }        // U(E)
    public double IncertezzaEstesaUso { get; init; }     // U d'uso
    public double GradiLibertaEffettivi { get; init; }
}

/// <summary>Esito completo della taratura di una bilancia.</summary>
public record RisultatoBilancia
{
    // Eccentricità
    public double EccentricitaScostamentoMax { get; init; }
    public double EccentricitaMedia { get; init; }
    public bool EccentricitaConforme { get; init; }

    // Ripetibilità
    public double RipetibilitaScartoTipo { get; init; }   // urep
    public double RipetibilitaComposta { get; init; }     // uripComb

    public IReadOnlyList<RisultatoPuntoBilancia> Punti { get; init; } = new List<RisultatoPuntoBilancia>();

    public double IncertezzaEstesaMax { get; init; }
    public double IncertezzaEstesaUsoMax { get; init; }
    public bool Conforme { get; init; }
}
