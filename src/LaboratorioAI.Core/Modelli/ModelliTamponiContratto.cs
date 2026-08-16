// LaboratorioAI.Core/Modelli/ModelliTamponiContratto.cs
//
// Modelli per il report "Tamponi in contratto": individua i tamponi
// (IdControllo 180/203) fatturabili in un periodo e verifica, per
// Cliente + Insediamento, se esiste un contratto di consulenza HACCP
// (commessa tipo 5) che li comprende — così da non fatturarli a parte.
//
// SOLA LETTURA su ArcLab: nessun dato viene mai scritto sul gestionale.

namespace LaboratorioAI.Core.Modelli;

/// <summary>Stato di fatturazione del certificato da mostrare.</summary>
public enum FiltroFatturazione
{
    /// <summary>Solo tamponi con certificato non ancora fatturato (comportamento predefinito).</summary>
    NonFatturati = 0,

    /// <summary>Solo tamponi con certificato già fatturato.</summary>
    Fatturati = 1,

    /// <summary>Tutti, a prescindere dalla data fattura.</summary>
    Tutti = 2
}

/// <summary>Criteri del report tamponi in contratto: periodo sui certificati.</summary>
public record CriteriTamponiContratto
{
    /// <summary>Estremo iniziale del periodo (data certificato).</summary>
    public DateOnly DataDa { get; init; }

    /// <summary>Estremo finale del periodo (data certificato).</summary>
    public DateOnly DataA { get; init; }

    /// <summary>Controlli-tampone da includere. Default: 180, 203, 2513.</summary>
    public IReadOnlyList<int> Controlli { get; init; } = new[] { 180, 203, 2513 };

    /// <summary>
    /// Stato di fatturazione (agisce solo sulla data fattura del certificato;
    /// gli altri filtri di fatturabilità restano sempre attivi). Default: non fatturati.
    /// </summary>
    public FiltroFatturazione Fatturazione { get; init; } = FiltroFatturazione.NonFatturati;

    /// <summary>Limite di righe a video; l'esportazione usa un limite più alto.</summary>
    public int Limite { get; init; } = 5000;

    // ── Filtri applicati solo in esportazione (riflettono la vista) ──
    /// <summary>Se valorizzato, filtra per presenza (true) o assenza (false) del contratto HACCP.</summary>
    public bool? SoloHaccp { get; init; }

    /// <summary>Se valorizzato, filtra i tamponi per prelevatore.</summary>
    public string? Prelevatore { get; init; }

    /// <summary>Se valorizzato, filtra i tamponi per ricevitore.</summary>
    public string? Ricevitore { get; init; }

    /// <summary>Se valorizzato, filtra i tamponi per insediamento selezionato.</summary>
    public int? IdInsediamento { get; init; }
}

/// <summary>Singolo tampone da fatturare nel periodo, con l'eventuale contratto HACCP.</summary>
public record TamponeContratto
{
    public int IdCampione { get; init; }
    public string NumeroCampione { get; init; } = "";
    public string? CodContabilita { get; init; }
    public int IdCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public int? IdInsediamento { get; init; }
    public string? NomeInsediamento { get; init; }
    public int IdControllo { get; init; }
    public string? Controllo { get; init; }
    public string? NumeroCertificato { get; init; }
    public DateTime? DataCertificato { get; init; }
    public string? Prelevatore { get; init; }
    public string? Ricevitore { get; init; }

    /// <summary>Stato "non fatturare" della commessa del tampone (dato mostrato in tabella).</summary>
    public bool NonFatturare { get; init; }

    // ── Contratto di consulenza HACCP (commessa tipo 5) ──────────────
    /// <summary>Vero se l'insediamento ha un contratto HACCP che comprende i tamponi.</summary>
    public bool ContrattoHaccp { get; init; }

    /// <summary>Numero di tamponi inclusi nel contratto (Prestazioni.DurataPres, anno in corso).</summary>
    public int? TamponiInclusi { get; init; }

    /// <summary>Data della prestazione HACCP dell'anno in corso.</summary>
    public DateTime? DataPrestazione { get; init; }
}

/// <summary>Riepilogo per insediamento: tamponi fatti nel periodo vs inclusi nel contratto.</summary>
public record RiepilogoInsediamentoTamponi
{
    public int IdCliente { get; init; }
    public string? RagioneSociale { get; init; }
    public int? IdInsediamento { get; init; }
    public string? NomeInsediamento { get; init; }
    public bool ContrattoHaccp { get; init; }
    public int? TamponiInclusi { get; init; }
    public int TamponiFatti { get; init; }

    /// <summary>Tamponi fatti meno inclusi: positivo = eccedenza da valutare per la fatturazione.</summary>
    public int? Differenza => TamponiInclusi is null ? null : TamponiFatti - TamponiInclusi;

    public string? Prelevatori { get; init; }

    /// <summary>Costruisce il riepilogo per insediamento a partire dai tamponi di dettaglio.</summary>
    public static IReadOnlyList<RiepilogoInsediamentoTamponi> Da(IEnumerable<TamponeContratto> tamponi) =>
        tamponi
            .GroupBy(t => new { t.IdCliente, t.RagioneSociale, t.IdInsediamento, t.NomeInsediamento })
            .Select(g => new RiepilogoInsediamentoTamponi
            {
                IdCliente = g.Key.IdCliente,
                RagioneSociale = g.Key.RagioneSociale,
                IdInsediamento = g.Key.IdInsediamento,
                NomeInsediamento = g.Key.NomeInsediamento,
                ContrattoHaccp = g.Any(x => x.ContrattoHaccp),
                TamponiInclusi = g.Max(x => x.TamponiInclusi),
                TamponiFatti = g.Count(),
                Prelevatori = string.Join(", ", g
                    .Select(x => x.Prelevatore)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct())
            })
            .OrderBy(r => r.RagioneSociale)
            .ThenBy(r => r.NomeInsediamento)
            .ToList();
}

/// <summary>Esito completo del report tamponi in contratto.</summary>
public record EsitoTamponiContratto
{
    public required IReadOnlyList<TamponeContratto> Tamponi { get; init; }
    public required IReadOnlyList<RiepilogoInsediamentoTamponi> Riepilogo { get; init; }
    public bool Troncato { get; init; }

    public int TotaleTamponi   => Tamponi.Count;
    public int InContratto     => Tamponi.Count(t => t.ContrattoHaccp);
    public int FuoriContratto  => Tamponi.Count(t => !t.ContrattoHaccp);
    public int InsediamentiDistinti => Riepilogo.Count;
}
