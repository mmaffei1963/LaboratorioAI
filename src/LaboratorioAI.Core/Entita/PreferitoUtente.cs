namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Funzione della home segnata con la stellina da un utente (08/10/2026). Identificata dall'indirizzo della voce
/// di menu (Web\Navigazione\MenuApplicazione.cs), così non serve una tabella delle funzioni.
/// Togliere la stella non cancella la riga: la disattiva (nessuna cancellazione fisica); rimetterla la riattiva.
/// </summary>
public class PreferitoUtente
{
    public int IdPreferitoUtente { get; set; }
    public int IdUtente { get; set; }

    /// <summary>Indirizzo della voce di menu (es. "/Validazione").</summary>
    public string Url { get; set; } = null!;

    /// <summary>Ordine di visualizzazione: per ora quello di aggiunta.</summary>
    public int Ordine { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}
