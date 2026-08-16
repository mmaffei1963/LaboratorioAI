namespace LaboratorioAI.IA;

/// <summary>Parametri di collegamento all'API Anthropic.</summary>
public class OpzioniAnthropic
{
    public const string Sezione = "Anthropic";

    /// <summary>
    /// Chiave API. Non deve essere scritta in appsettings versionato:
    /// va impostata in configurazione protetta o variabile d'ambiente.
    /// </summary>
    public string ChiaveApi { get; set; } = "";

    public string Modello { get; set; } = "claude-sonnet-4-6";
    public string Endpoint { get; set; } = "https://api.anthropic.com/v1/messages";
    public string VersioneApi { get; set; } = "2023-06-01";

    public int TokenMassimi { get; set; } = 2000;
    public int TimeoutSecondi { get; set; } = 120;

    /// <summary>Costo per milione di token, per la stima riportata sulle analisi.</summary>
    public decimal CostoInputPerMilione { get; set; } = 3.00m;
    public decimal CostoOutputPerMilione { get; set; } = 15.00m;

    public bool Configurato => !string.IsNullOrWhiteSpace(ChiaveApi);
}
