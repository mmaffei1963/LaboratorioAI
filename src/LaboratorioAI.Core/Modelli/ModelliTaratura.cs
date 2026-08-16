// LaboratorioAI.Core/Modelli/ModelliTaratura.cs
//
// Modelli di calcolo per la taratura dei termometri (confronto con campione di
// riferimento, budget d'incertezza GUM). Puro calcolo, indipendente dalla
// persistenza. Si usa il tipo double per riprodurre esattamente la matematica
// del foglio Excel storico.

namespace LaboratorioAI.Core.Modelli;

/// <summary>Un punto del certificato del campione di riferimento (CDR).</summary>
public record PuntoCertificatoCdr
{
    public double TIndicata { get; init; }
    public double TRiferimento { get; init; }
    /// <summary>Correzione/differenza dal certificato (per l'interpolazione).</summary>
    public double Differenza { get; init; }
    /// <summary>Incertezza estesa del certificato UE(CR) — a fini di riferimento.</summary>
    public double IncertezzaEstesa { get; init; }
    /// <summary>Incertezza tipo del CDR usata nel budget (contributo «u lab»).</summary>
    public double IncertezzaTipo { get; init; }
}

/// <summary>Una lettura: temperatura di riferimento nominale + lettura dello strumento (Tw).</summary>
public record LetturaTaratura
{
    public double TemperaturaNominale { get; init; }
    public double LetturaStrumento { get; init; }
}

/// <summary>Un punto di taratura con le sue letture ripetute.</summary>
public record PuntoTaratura
{
    public string Etichetta { get; init; } = "";
    public IReadOnlyList<LetturaTaratura> Letture { get; init; } = new List<LetturaTaratura>();
}

/// <summary>Dati in ingresso al calcolo di una taratura.</summary>
public record DatiTaratura
{
    public IReadOnlyList<PuntoCertificatoCdr> Certificato { get; init; } = new List<PuntoCertificatoCdr>();
    public double RisoluzioneStrumento { get; init; }
    public double FattoreCopertura { get; init; } = 2.0;
    public IReadOnlyList<PuntoTaratura> Punti { get; init; } = new List<PuntoTaratura>();
}

/// <summary>Esito per un punto di taratura.</summary>
public record RisultatoPuntoTaratura
{
    public string Etichetta { get; init; } = "";
    public double TemperaturaMediaRiferimento { get; init; }
    public double TemperaturaMediaStrumento { get; init; }
    public double DifferenzaMedia { get; init; }

    // Contributi d'incertezza (standard)
    public double URipetibilita { get; init; }   // tipo A
    public double URiferimento { get; init; }     // tipo B — u(CR)
    public double URisoluzione { get; init; }     // tipo B — risoluzione UUT
    public double UStabilita { get; init; }       // tipo B — stabilità riferimento

    public double IncertezzaComposta { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double FattoreCorrezione { get; init; }
}

/// <summary>Esito completo della taratura.</summary>
public record RisultatoTaratura
{
    public IReadOnlyList<RisultatoPuntoTaratura> Punti { get; init; } = new List<RisultatoPuntoTaratura>();
    public double PendenzaCorrezione { get; init; }
    public double IntercettaCorrezione { get; init; }
}
