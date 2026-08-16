namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Esito archiviato di una validazione periodica del motore di calcolo delle
/// tarature di incubatori/celle (confronto coi casi di riferimento congelati).
/// </summary>
public class EsitoValidazioneIncubatore
{
    public int IdEsito { get; set; }

    public DateTime DataEsecuzione { get; set; }
    public bool Validato { get; set; }
    public int NumeroOk { get; set; }
    public int NumeroKo { get; set; }
    public double Tolleranza { get; set; }

    public string VersioneSet { get; set; } = null!;
    public string? VersioneApplicazione { get; set; }

    /// <summary>Dettaglio completo dei confronti, serializzato in JSON.</summary>
    public string Dettaglio { get; set; } = null!;

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
    public string? Operatore { get; set; }
}
