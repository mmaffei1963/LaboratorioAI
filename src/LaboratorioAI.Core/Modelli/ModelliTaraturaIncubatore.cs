namespace LaboratorioAI.Core.Modelli;

/// <summary>Singola lettura in una posizione della camera (mappatura).</summary>
public record LetturaIncubatore
{
    /// <summary>Posizione nella camera (A/B/C/D…), per la mappatura.</summary>
    public string Posizione { get; init; } = "A";

    /// <summary>Temperatura rilevata dal CDR (t rif).</summary>
    public double TemperaturaRilevata { get; init; }

    /// <summary>Lettura del misuratore in taratura (tw).</summary>
    public double LetturaStrumento { get; init; }
}

/// <summary>
/// Dati per la taratura del misuratore di un incubatore/cella alla sua
/// temperatura di esercizio (valore nominale con tolleranza), contro il CDR.
/// </summary>
public record DatiTaraturaIncubatore
{
    /// <summary>Punti del certificato del CDR (T indicata, T riferimento, incertezza estesa).</summary>
    public required IReadOnlyList<PuntoCertificatoCdr> Certificato { get; init; }

    /// <summary>
    /// Punti della taratura secondaria/precedente del CDR (interna). Se presenti
    /// (≥ 2), l'incertezza di instabilità u(stCR) è calcolata come nel foglio dal
    /// confronto con il certificato primario; altrimenti si usa il valore fisso.
    /// </summary>
    public IReadOnlyList<PuntoCertificatoCdr>? CertificatoSecondario { get; init; }

    /// <summary>Temperatura di esercizio (valore nominale).</summary>
    public double ValoreNominale { get; init; }

    /// <summary>Semiampiezza dei limiti di tolleranza (±) della temperatura di esercizio.</summary>
    public double Tolleranza { get; init; }

    /// <summary>Risoluzione del misuratore in taratura (°C).</summary>
    public double RisoluzioneStrumento { get; init; }

    /// <summary>Incertezza tipo di instabilità/deriva del CDR, u(stCR) (°C). Dai certificati del CDR.</summary>
    public double IncertezzaInstabilita { get; init; }

    /// <summary>Semidispersione da mappatura (posizioni), opzionale (°C). Default 0.</summary>
    public double DispersioneMappatura { get; init; }

    /// <summary>Fattore di copertura k (normalmente 2).</summary>
    public double FattoreCopertura { get; init; } = 2.0;

    public required IReadOnlyList<LetturaIncubatore> Letture { get; init; }
}

/// <summary>Esito del calcolo della taratura di un incubatore/cella.</summary>
public record RisultatoTaraturaIncubatore
{
    /// <summary>Correzione del CDR alla temperatura di esercizio (interpolata), Δ(CR).</summary>
    public double CorrezioneCdr { get; init; }

    public double TemperaturaMediaRiferimento { get; init; }
    public double TemperaturaMediaStrumento { get; init; }
    public double DifferenzaMedia { get; init; }

    public double IncertezzaRipetibilita { get; init; }
    public double IncertezzaRiferimento { get; init; }   // u(CR)
    public double IncertezzaRisoluzione { get; init; }
    public double IncertezzaMappatura { get; init; }
    public double IncertezzaInstabilita { get; init; }   // u(stCR)

    public double IncertezzaComposta { get; init; }
    public double IncertezzaEstesa { get; init; }

    /// <summary>Fattore di correzione del misuratore (Tr − Tw).</summary>
    public double FattoreCorrezione { get; init; }

    public double AllarmeMinimo { get; init; }
    public double AllarmeMassimo { get; init; }

    /// <summary>Conforme se l'incertezza estesa è ≤ 50% della tolleranza.</summary>
    public bool Conforme { get; init; }

    public int NumeroLetture { get; init; }
}
