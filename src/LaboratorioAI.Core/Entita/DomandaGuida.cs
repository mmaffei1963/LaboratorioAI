namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Domanda fatta a «Chiedi alla guida» (11/10/2026): l'AI risponde usando solo il testo delle guide che la persona può vedere.
///
/// Si registrano domanda, risposta e voto (👍/👎) per capire dove le guide non sono chiare e quanto costa il servizio.
/// Dopo 12 mesi il testo di domanda e risposta viene svuotato (<see cref="DataAnonimizzazione"/>): la riga resta, per i conteggi,
/// senza cancellazione fisica.
/// </summary>
public class DomandaGuida
{
    public int IdDomandaGuida { get; set; }

    /// <summary>Data e ora UTC della domanda.</summary>
    public DateTime DataOra { get; set; }

    public int? IdUtente { get; set; }
    public string? EmailUtente { get; set; }
    public string? NomeUtente { get; set; }

    /// <summary>Pagina da cui è partita la domanda (es. /Validazione).</summary>
    public string? Percorso { get; set; }

    /// <summary>Guida della pagina, se c'è (es. Validazione).</summary>
    public string? CodiceGuida { get; set; }

    public string Domanda { get; set; } = "";
    public string? Risposta { get; set; }

    /// <summary>1 = utile, -1 = non utile, null = non valutata.</summary>
    public short? Valutazione { get; set; }

    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }
    public int TokenCacheScritti { get; set; }
    public int TokenCacheLetti { get; set; }

    /// <summary>Costo stimato della chiamata, nella valuta dei prezzi configurati.</summary>
    public decimal CostoStimato { get; set; }

    public string? Modello { get; set; }
    public int DurataMs { get; set; }

    /// <summary>Messaggio d'errore se la chiamata al modello non è riuscita.</summary>
    public string? Errore { get; set; }

    /// <summary>Quando il testo è stato svuotato per scadenza dei 12 mesi.</summary>
    public DateTime? DataAnonimizzazione { get; set; }
}
