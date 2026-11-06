// LaboratorioAI.Core/Modelli/ModelliStatistiche.cs
//
// Sezione "Statistiche": ricerca trasversale a livello di singola prova
// (parametro analizzato), con filtri combinabili su cliente, insediamento,
// commessa, metodo e valore calcolato. Sola lettura su ArcLab.

namespace LaboratorioAI.Core.Modelli;

/// <summary>Criteri di ricerca delle statistiche. Il periodo (data ricezione) è obbligatorio.</summary>
public record CriteriStatistiche
{
    // ── Periodo (obbligatorio, su Lotto.DataRic) ─────────────────────
    public DateOnly DataRicDa { get; init; }
    public DateOnly DataRicA { get; init; }

    // ── Periodo di prelievo (facoltativo, su Lotto.DataPrelievo) ─────
    public DateOnly? DataPrelievoDa { get; init; }
    public DateOnly? DataPrelievoA { get; init; }

    // ── Filtri richiesti ──────────────────────────────────────────────
    public string? Cliente { get; init; }
    public string? Insediamento { get; init; }
    public string? Citta { get; init; }
    public string? Provincia { get; init; }
    public string? Cap { get; init; }
    public string? DescrIncarico { get; init; }
    public string? Metodo { get; init; }
    public decimal? ValoreCalcolatoDa { get; init; }
    public decimal? ValoreCalcolatoA { get; init; }

    // ── Filtri aggiuntivi ─────────────────────────────────────────────
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }
    public string? NCertificato { get; init; }
    public string? NumeroCampione { get; init; }   // "4530" oppure "4530/01"
    public bool? Accreditato { get; init; }

    public int Limite { get; init; } = 20000;
}

/// <summary>Una riga di dettaglio: una prova (parametro analizzato) su un campione.</summary>
public record RigaStatistica
{
    public int IdCampione { get; init; }
    public int IdProva { get; init; }
    public string NumeroCampione { get; init; } = "";

    public int IdCliente { get; init; }
    public string? Cliente { get; init; }
    public int? IdInsediamento { get; init; }
    public string? Insediamento { get; init; }
    public string? Citta { get; init; }
    public string? Provincia { get; init; }
    public string? Cap { get; init; }
    public string? Indirizzo { get; init; }

    public int IdCommessa { get; init; }
    public string? Commessa { get; init; }

    public string? NCertificato { get; init; }
    public string? DenominazioneCampione { get; init; }
    public string? Matrice { get; init; }

    public DateTime? DataRicezione { get; init; }
    public DateTime? DataPrelievo { get; init; }
    public string? PuntoPrelievo { get; init; }
    /// <summary>Campione.TempPrelievo: temperatura al prelievo (come registrata in ArcLab).</summary>
    public string? TempPrelievo { get; init; }

    public string? LottoProduzione { get; init; }
    public DateTime? DataProduzione { get; init; }
    public string? Lavorazione { get; init; }

    public string? Parametro { get; init; }
    public string? Metodo { get; init; }
    public string? RiferimentoNormativo { get; init; }
    public bool Accreditato { get; init; }

    public decimal? ValoreCalcolato { get; init; }
    public bool MinoreDi { get; init; }
    public string? UnitaMisura { get; init; }
}

/// <summary>Esito della ricerca statistiche.</summary>
public record EsitoStatistiche
{
    public required IReadOnlyList<RigaStatistica> Righe { get; init; }
    public bool Troncato { get; init; }

    public int Totale => Righe.Count;
}
