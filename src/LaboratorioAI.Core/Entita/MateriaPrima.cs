using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Identità di una materia prima nel catalogo. I valori nutrizionali veri e
/// propri stanno in <see cref="ValoreNutrizionale"/> (uno o più set per fonte
/// e nel tempo). Scrittura solo su questo database, mai su ArcLab.
/// </summary>
public class MateriaPrima
{
    public int IdMateriaPrima { get; set; }

    public string Denominazione { get; set; } = null!;
    public TipoMateriaPrima Tipo { get; set; }

    // ── Valorizzati per le materie prime specifiche ─────────────────
    public string? Marca { get; set; }
    public string? Fornitore { get; set; }

    /// <summary>Riferimento logico al cliente ArcLab, se legata a un committente (sola lettura).</summary>
    public int? IdClienteArcLab { get; set; }

    public string? Categoria { get; set; }
    public string? CodiceInterno { get; set; }
    public string? Note { get; set; }

    // ── Nessuna cancellazione fisica ────────────────────────────────
    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }

    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public ICollection<ValoreNutrizionale> Valori { get; set; } = new List<ValoreNutrizionale>();
}
