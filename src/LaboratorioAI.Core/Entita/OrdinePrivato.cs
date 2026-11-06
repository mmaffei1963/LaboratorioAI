namespace LaboratorioAI.Core.Entita;

/// <summary>Come il cliente privato riceve il Rapporto di Prova (14/10/2026).</summary>
public enum ConsegnaRapportoPrivato
{
    Email = 1,
    AMano = 2,
    Entrambi = 3
}

/// <summary>
/// Ordine di Accettazione stampato da firmare per un cliente privato, che non usa il Portale (14/10/2026).
/// Si stampa dal pulsante «Stampa da firmare» di ArcLab; l'Ordine si firma con le Condizioni Generali per i privati
/// alla consegna dei campioni e l'originale va nel raccoglitore dei privati. L'e-mail per il Rapporto di Prova la dà il
/// privato e si tiene solo qui (ArcLab resta in sola lettura). Uno per lotto: una nuova stampa aggiorna la stessa riga.
/// Nessuna cancellazione fisica.
/// </summary>
public class OrdinePrivato
{
    public int IdOrdinePrivato { get; set; }
    /// <summary>Lotto ArcLab (chiave logica, univoca).</summary>
    public int IdLotto { get; set; }
    public string? CodLotto { get; set; }
    public int? IdClienteArcLab { get; set; }
    public string? Cliente { get; set; }
    public int NumeroCampioni { get; set; }
    public DateTime? DataRicezione { get; set; }
    /// <summary>E-mail indicata dal privato per il Rapporto di Prova.</summary>
    public string? EmailRapporto { get; set; }
    public ConsegnaRapportoPrivato Consegna { get; set; } = ConsegnaRapportoPrivato.AMano;
    /// <summary>Giorno della firma di Ordine e Condizioni (di norma quello della stampa).</summary>
    public DateTime? FirmatoIl { get; set; }
    public string? Note { get; set; }
    public DateTime DataStampa { get; set; }
    public int Stampe { get; set; }
    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }
    public string? NomeFile { get; set; }
    public string? PercorsoFile { get; set; }
    public string? Hash { get; set; }
    public DateTime? DataUltimaModifica { get; set; }
    public string? UltimaModificaDa { get; set; }
    public bool Annullato { get; set; }
}
