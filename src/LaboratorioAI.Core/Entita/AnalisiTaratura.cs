namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Analisi anti-errore di una taratura: controlli deterministici + valutazione AI
/// che segnala possibili errori grossolani di inserimento. Archiviata: la «Firma»
/// rappresenta i dati analizzati, così una taratura invariata non richiama il
/// modello una seconda volta.
/// </summary>
public class AnalisiTaratura
{
    public int IdAnalisi { get; set; }

    public int IdTaratura { get; set; }
    public Taratura? Taratura { get; set; }

    public DateTime DataAnalisi { get; set; }

    /// <summary>Impronta dei dati analizzati (strumento, CDR, misure, risultati).</summary>
    public string Firma { get; set; } = null!;

    /// <summary>Vero se i controlli deterministici hanno trovato anomalie.</summary>
    public bool AnomalieDeterministiche { get; set; }

    /// <summary>Elenco anomalie deterministiche, serializzato in JSON.</summary>
    public string? Controlli { get; set; }

    /// <summary>Verdetto sintetico dell'AI (prima riga della risposta).</summary>
    public string? Esito { get; set; }

    /// <summary>Testo completo della valutazione AI.</summary>
    public string Testo { get; set; } = null!;

    public string? ModelloAi { get; set; }
    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }

    public string? VersioneApplicazione { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
}
