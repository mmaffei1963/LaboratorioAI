using LaboratorioAI.Core.Repository;

namespace LaboratorioAI.Core.Modelli;

// Offerte sul portale clienti (17/11/2026): l'offerta di laboratorio di un cliente costruita dalle sue commesse ArcLab
// attive (sola lettura). Definizione in claude/definizione-offerte-portale.md.

/// <summary>Commessa di laboratorio attiva letta da ArcLab (una per insediamento e controllo).</summary>
public record CommessaOffertaArcLab
{
    public int IdCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public string? CodContabilita { get; init; }
    public int IdInsediamento { get; init; }
    public string? Insediamento { get; init; }
    public int IdCommessa { get; init; }
    public int IdControllo { get; init; }
    public string? Controllo { get; init; }
    public string? DescrIncarico { get; init; }
    public decimal Prezzo { get; init; }
    public DateTime? DataCommessa { get; init; }
    public bool NonFatturare { get; init; }

    /// <summary>Testo dell'incarico: la descrizione della commessa, o il nome del controllo se manca.</summary>
    public string Incarico => !string.IsNullOrWhiteSpace(DescrIncarico) ? DescrIncarico!.Trim() : (Controllo ?? "").Trim();
}

/// <summary>Dati del cliente per la testata dell'offerta.</summary>
public record ClienteOffertaArcLab
{
    public int IdCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public string? CodContabilita { get; init; }
    public string? PartitaIva { get; init; }
    public string? Indirizzo { get; init; }
    public string? Cap { get; init; }
    public string? Citta { get; init; }
    public string? Provincia { get; init; }
}

/// <summary>Riga della tabella «ALL.1 Offerta analisi di laboratorio».</summary>
public record RigaOffertaCliente(
    int IdControllo,
    string Incarico,
    decimal Prezzo,
    DateTime? DataIncarico,
    DateTime? UltimaModifica,
    string? EtichettaModifica,
    IReadOnlyList<string> Insediamenti,
    IReadOnlyList<int> IdCommesse);

/// <summary>
/// Anomalia sullo stesso controllo in insediamenti diversi: «B» prezzo uguale e descrizione diversa,
/// «D» prezzo diverso e descrizione uguale (sul portale va il prezzo più alto).
/// </summary>
public record AnomaliaOfferta(
    int IdCliente,
    string? Cliente,
    string? CodContabilita,
    int IdControllo,
    string? Controllo,
    string Caso,
    string Descrizione,
    IReadOnlyList<CommessaOffertaArcLab> Commesse);

public record EsitoRegoleOfferta(IReadOnlyList<RigaOffertaCliente> Righe, IReadOnlyList<AnomaliaOfferta> Anomalie);

/// <summary>Metodi di un controllo per il «Dettaglio incarico».</summary>
public record DettaglioControlloOfferta(int IdControllo, string Titolo, IReadOnlyList<VoceMetodo> Metodi);

/// <summary>Offerta completa di un cliente, per il portale e il PDF.</summary>
public record OffertaCliente(
    ClienteOffertaArcLab Cliente,
    IReadOnlyList<RigaOffertaCliente> Righe,
    IReadOnlyList<DettaglioControlloOfferta> Dettagli,
    DateTime GenerataIl);
