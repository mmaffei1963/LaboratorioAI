namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Analisi anti-errore di una taratura di incubatore/cella: controlli
/// deterministici + valutazione AI. Archiviata (la «Firma» rappresenta i dati
/// analizzati), così una taratura invariata non richiama il modello due volte.
/// </summary>
public class AnalisiIncubatore
{
    public int IdAnalisi { get; set; }

    public int IdTaratura { get; set; }
    public TaraturaIncubatore? Taratura { get; set; }

    public DateTime DataAnalisi { get; set; }

    public string Firma { get; set; } = null!;

    public bool AnomalieDeterministiche { get; set; }
    public string? Controlli { get; set; }

    public string? Esito { get; set; }
    public string Testo { get; set; } = null!;

    public string? ModelloAi { get; set; }
    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }

    public string? VersioneApplicazione { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
}
