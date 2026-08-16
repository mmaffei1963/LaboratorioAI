// LaboratorioAI.Documenti/Piscine/RisultatoGenerazionePiscina.cs
//
// Esito della generazione di un documento piscine.

namespace LaboratorioAI.Documenti.Piscine;

/// <summary>Esito della produzione del documento a partire da una scheda.</summary>
public record RisultatoGenerazionePiscina
{
    /// <summary>Vero se il documento è stato prodotto.</summary>
    public required bool Riuscito { get; init; }

    /// <summary>Contenuto del .docx generato, in memoria. Nullo se fallito.</summary>
    public byte[]? Documento { get; init; }

    /// <summary>Percorso del file archiviato, se è stata configurata la cartella.</summary>
    public string? PercorsoArchiviato { get; init; }

    /// <summary>
    /// Tag/marcatori attesi dai valori ma non trovati nel modello. Utile per
    /// diagnosticare disallineamenti fra codice e modello senza far fallire la
    /// generazione.
    /// </summary>
    public IReadOnlyList<string> TagNonTrovati { get; init; } = [];

    /// <summary>Messaggio d'errore in caso di fallimento.</summary>
    public string? Errore { get; init; }

    /// <summary>Costruisce un esito positivo.</summary>
    public static RisultatoGenerazionePiscina Ok(
        byte[] documento, IReadOnlyList<string> tagNonTrovati, string? percorso = null) => new()
    {
        Riuscito = true,
        Documento = documento,
        TagNonTrovati = tagNonTrovati,
        PercorsoArchiviato = percorso,
    };

    /// <summary>Costruisce un esito negativo.</summary>
    public static RisultatoGenerazionePiscina Fallito(string errore) => new()
    {
        Riuscito = false,
        Errore = errore,
    };
}
