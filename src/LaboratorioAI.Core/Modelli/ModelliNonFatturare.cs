// LaboratorioAI.Core/Modelli/ModelliNonFatturare.cs
//
// Report "Non fatturare": conteggio dei campioni con il flag NonFatturare
// attivo sulla commessa, raggruppati per prelevatore. Sola lettura su ArcLab.

namespace LaboratorioAI.Core.Modelli;

/// <summary>Criteri del report non-fatturare: periodo sulla data di ricezione.</summary>
public record CriteriNonFatturare
{
    public DateOnly DataDa { get; init; }
    public DateOnly DataA { get; init; }
    public int Limite { get; init; } = 20000;

    // ── Filtri applicati solo in esportazione (riflettono la vista) ──
    public string? Cliente { get; init; }
    public string? Insediamento { get; init; }
    public bool? SoloHaccp { get; init; }
    public string? Prelevatore { get; init; }
    public string? Ricevitore { get; init; }
    public IReadOnlyList<int>? Controlli { get; init; }
}

/// <summary>Campione con NonFatturare attivo (riga di dettaglio).</summary>
public record CampioneNonFatturare
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public int IdCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public int? IdInsediamento { get; init; }
    public string? NomeInsediamento { get; init; }
    public int IdControllo { get; init; }
    public string? Controllo { get; init; }
    public DateTime? DataRicezione { get; init; }
    public string? Prelevatore { get; init; }
    public string? Ricevitore { get; init; }

    /// <summary>Prezzo del controllo dalla commessa (commessa.PrezzoControllo).</summary>
    public decimal? PrezzoControllo { get; init; }

    /// <summary>Vero se l'insediamento ha un contratto di consulenza HACCP.</summary>
    public bool ContrattoHaccp { get; init; }
}

/// <summary>Esito del report: elenco dei campioni non-fatturare (il riepilogo si ricava a video).</summary>
public record EsitoNonFatturare
{
    public required IReadOnlyList<CampioneNonFatturare> Campioni { get; init; }
    public bool Troncato { get; init; }

    public int Totale => Campioni.Count;
}
