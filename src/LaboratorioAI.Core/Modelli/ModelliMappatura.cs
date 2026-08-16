namespace LaboratorioAI.Core.Modelli;

/// <summary>Singola lettura del CDR in una posizione/ripiano dell'apparecchiatura.</summary>
public record LetturaMappatura
{
    /// <summary>Ripiano/posizione (A/B/C/D).</summary>
    public string Posizione { get; init; } = "A";

    /// <summary>Temperatura rilevata dal CDR (t rif).</summary>
    public double TemperaturaRilevata { get; init; }
}

/// <summary>Dati per la mappatura di un'apparecchiatura termostatica (Modulo 04-03).</summary>
public record DatiMappatura
{
    public required IReadOnlyList<PuntoCertificatoCdr> Certificato { get; init; }
    public IReadOnlyList<PuntoCertificatoCdr>? CertificatoSecondario { get; init; }

    /// <summary>Temperatura impostata (nominale) dell'apparecchiatura.</summary>
    public double ValoreNominale { get; init; }

    /// <summary>Semiampiezza dei limiti di tolleranza (±).</summary>
    public double Tolleranza { get; init; }

    /// <summary>Risoluzione del CDR (°C).</summary>
    public double RisoluzioneCdr { get; init; }

    /// <summary>Incertezza estesa di testata del certificato UE(CR) (°C). Se 0, si usa il massimo tra i punti.</summary>
    public double IncertezzaEstesaCertificato { get; init; }

    /// <summary>Instabilità del CDR u(stCR) di ripiego, se manca la taratura secondaria (°C).</summary>
    public double IncertezzaInstabilita { get; init; }

    public double FattoreCopertura { get; init; } = 2.0;

    public required IReadOnlyList<LetturaMappatura> Letture { get; init; }
}

/// <summary>Riepilogo di un ripiano/posizione.</summary>
public record RipianoMappatura
{
    public string Posizione { get; init; } = "";
    public double Media { get; init; }
    public double Minimo { get; init; }
    public double Massimo { get; init; }
    public int NumeroLetture { get; init; }
    /// <summary>Vero se media, minimo e massimo sono entro l'intervallo ammesso.</summary>
    public bool Conforme { get; init; }
}

/// <summary>Esito della mappatura di un'apparecchiatura termostatica.</summary>
public record RisultatoMappatura
{
    public double CorrezioneCdr { get; init; }

    public double TemperaturaMedia { get; init; }         // Tr
    public double ScartoTipoMedia { get; init; }
    public double IncertezzaComposta { get; init; }
    public double IncertezzaEstesa { get; init; }          // U
    public double DifferenzaNominale { get; init; }        // Tr − nominale

    public double IntervalloMinimo { get; init; }          // nominale − (tol − U)
    public double IntervalloMassimo { get; init; }         // nominale + (tol − U)

    public int NumeroLetture { get; init; }
    public IReadOnlyList<RipianoMappatura> Ripiani { get; init; } = new List<RipianoMappatura>();

    // ── Test statistici (per il giudizio; non nel set di validazione) ──
    public double FisherF { get; init; }
    public double FisherFCritico { get; init; }
    public bool VarianzeOmogenee { get; init; }
    public double KruskalWallis { get; init; }
    public double KruskalWallisP { get; init; }

    /// <summary>Vero se i test indicano differenze significative tra le posizioni.</summary>
    public bool DifferenzeSignificative { get; init; }

    /// <summary>Giudizio testuale sintetico (per il rapporto).</summary>
    public string Giudizio { get; init; } = "";
}
