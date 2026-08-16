namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Rapporto nutrizionale generato. Sullo stampo di <c>AnalisiAi</c>: congela in
/// JSON i risultati e la ricetta usata (con quale set di valori per ogni
/// ingrediente), così resta riproducibile anche se il catalogo cambia.
/// </summary>
public class RapportoNutrizionale
{
    public int IdRapporto { get; set; }

    public int IdRicetta { get; set; }
    public Ricetta? Ricetta { get; set; }

    public DateTime DataRapporto { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }

    public string? RedattoDa { get; set; }

    // ── Snapshot ────────────────────────────────────────────────────
    public string? Committente { get; set; }
    public string Denominazione { get; set; } = null!;
    public string? Lotto { get; set; }

    /// <summary>Risultati calcolati (per 100 g, per unità, %RI, arrotondati) in JSON.</summary>
    public string Risultati { get; set; } = null!;

    /// <summary>Ricetta e set di valori effettivamente usati, in JSON.</summary>
    public string RicettaUsata { get; set; } = null!;

    public string VersioneCalcolo { get; set; } = null!;
    public string VersioneApplicazione { get; set; } = null!;

    public string? NomeFileGenerato { get; set; }
    public string? PercorsoDocumento { get; set; }
}
