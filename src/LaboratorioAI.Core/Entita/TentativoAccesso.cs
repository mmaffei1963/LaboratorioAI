namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Tentativo di accesso al portale, riuscito o fallito.
///
/// Alimenta la protezione contro gli attacchi a forza bruta e fornisce
/// evidenza in caso di verifica sugli accessi.
/// </summary>
public class TentativoAccesso
{
    public long IdTentativo { get; set; }

    public DateTime DataOra { get; set; }

    /// <summary>Codice contabilità presentato. La credenziale non viene mai registrata.</summary>
    public string CodiceContabilita { get; set; } = null!;

    public string? IndirizzoIp { get; set; }
    public bool Riuscito { get; set; }
    public string? Motivo { get; set; }
}
