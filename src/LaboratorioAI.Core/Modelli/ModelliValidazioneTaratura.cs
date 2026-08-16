namespace LaboratorioAI.Core.Modelli;

/// <summary>Valori attesi (dal foglio storico) per un punto di un caso di validazione.</summary>
public record AttesoPuntoValidazione
{
    public string Etichetta { get; init; } = "";
    public double TemperaturaMediaRiferimento { get; init; }
    public double TemperaturaMediaStrumento { get; init; }
    public double DifferenzaMedia { get; init; }
    public double IncertezzaEstesa { get; init; }
    public double FattoreCorrezione { get; init; }
}

/// <summary>
/// Caso di riferimento congelato per la validazione periodica del motore di
/// calcolo: dati d'ingresso fissi (CDR + letture) e valori attesi indipendenti,
/// ricavati una volta dal foglio Excel storico (VAL_Caso1/2/3).
/// </summary>
public record CasoValidazione
{
    public string Nome { get; init; } = "";
    public string Descrizione { get; init; } = "";
    public required DatiTaratura Dati { get; init; }
    public required IReadOnlyList<AttesoPuntoValidazione> Attesi { get; init; }
}

/// <summary>Confronto di un singolo parametro (calcolato oggi vs atteso).</summary>
public record ConfrontoParametro
{
    public string Caso { get; init; } = "";
    public string Punto { get; init; } = "";
    public string Parametro { get; init; } = "";
    public double Calcolato { get; init; }
    public double Atteso { get; init; }
    public double Scarto => Math.Abs(Calcolato - Atteso);
    public bool Ok { get; init; }
}

/// <summary>Riepilogo esito per singolo caso.</summary>
public record RiepilogoCasoValidazione
{
    public string Nome { get; init; } = "";
    public string Descrizione { get; init; } = "";
    public int Ok { get; init; }
    public int Ko { get; init; }
    public bool Validato => Ko == 0;
}

/// <summary>Esito complessivo della validazione dei calcoli di taratura.</summary>
public record EsitoValidazioneCalcoli
{
    public DateTime DataEsecuzione { get; init; }
    public string VersioneSet { get; init; } = "";
    public string VersioneApplicazione { get; init; } = "";
    public double Tolleranza { get; init; }
    public bool Validato { get; init; }
    public int NumeroOk { get; init; }
    public int NumeroKo { get; init; }
    public IReadOnlyList<RiepilogoCasoValidazione> Casi { get; init; } = new List<RiepilogoCasoValidazione>();
    public IReadOnlyList<ConfrontoParametro> Dettagli { get; init; } = new List<ConfrontoParametro>();
}
