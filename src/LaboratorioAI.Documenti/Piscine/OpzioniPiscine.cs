// LaboratorioAI.Documenti/Piscine/OpzioniPiscine.cs
//
// Parametri di configurazione del generatore piscine, legati alla sezione
// "Piscine" (IOptions/appsettings), sullo stesso schema delle opzioni dei DVR.

namespace LaboratorioAI.Documenti.Piscine;

/// <summary>Configurazione del generatore piscine.</summary>
public record OpzioniPiscine
{
    /// <summary>Nome della sezione di configurazione (appsettings).</summary>
    public const string Sezione = "Piscine";

    /// <summary>Percorso del modello Word (.docx) con i controlli contenuto.</summary>
    public required string PercorsoModello { get; init; }

    /// <summary>
    /// Cartella in cui archiviare i documenti generati. Se nulla o vuota, il
    /// documento viene solo restituito in memoria senza scrittura su disco.
    /// </summary>
    public string? CartellaArchivio { get; init; }

    /// <summary>Percorso del modello Word del Regolamento interno (con token).</summary>
    public string? RegolamentoModello { get; init; }

    /// <summary>
    /// Modello Word del Manuale di autocontrollo per l'Emilia-Romagna
    /// (DGR 1092/2005), a token testuali. Usato come default.
    /// </summary>
    public string? ManualeModelloEmiliaRomagna { get; init; }

    /// <summary>
    /// Modello Word del Registro tecnico funzionale per le Marche
    /// (DGR 1431/2013), a token testuali.
    /// </summary>
    public string? ManualeModelloMarche { get; init; }
}
