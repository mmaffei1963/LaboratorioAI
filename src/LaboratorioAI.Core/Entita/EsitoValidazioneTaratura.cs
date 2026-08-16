namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Esito archiviato di una validazione periodica del motore di calcolo della
/// taratura (confronto coi casi di riferimento congelati). Costituisce la traccia
/// per l'accreditamento: chi, quando, con quale versione, con quale risultato.
/// </summary>
public class EsitoValidazioneTaratura
{
    public int IdEsito { get; set; }

    public DateTime DataEsecuzione { get; set; }

    /// <summary>Vero se tutti i confronti rientrano in tolleranza.</summary>
    public bool Validato { get; set; }

    public int NumeroOk { get; set; }
    public int NumeroKo { get; set; }

    public double Tolleranza { get; set; }

    /// <summary>Versione del set di riferimento congelato usato.</summary>
    public string VersioneSet { get; set; } = null!;

    public string? VersioneApplicazione { get; set; }

    /// <summary>Dettaglio completo dei confronti, serializzato in JSON.</summary>
    public string Dettaglio { get; set; } = null!;

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }

    /// <summary>Nome dell'operatore che ha eseguito la validazione.</summary>
    public string? Operatore { get; set; }
}
