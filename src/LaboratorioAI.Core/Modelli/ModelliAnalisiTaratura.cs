namespace LaboratorioAI.Core.Modelli;

/// <summary>Gravità di un'anomalia rilevata dai controlli deterministici.</summary>
public enum LivelloAnomalia
{
    Avviso = 0,
    Errore = 1
}

/// <summary>Singola anomalia rilevata sui dati di una taratura.</summary>
public record AnomaliaTaratura
{
    public LivelloAnomalia Livello { get; init; }
    public string? Punto { get; init; }
    public string Messaggio { get; init; } = "";
}

/// <summary>Esito dei controlli deterministici (senza AI).</summary>
public record RisultatoControlliTaratura
{
    public IReadOnlyList<AnomaliaTaratura> Anomalie { get; init; } = new List<AnomaliaTaratura>();
    public bool CiSonoAnomalie => Anomalie.Count > 0;
    public bool CiSonoErrori => Anomalie.Any(a => a.Livello == LivelloAnomalia.Errore);
}
