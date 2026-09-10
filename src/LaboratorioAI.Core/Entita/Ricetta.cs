namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Ricetta/prodotto di cui si calcola la dichiarazione nutrizionale.
/// Il committente è un riferimento logico all'anagrafica ArcLab (sola lettura).
/// </summary>
public class Ricetta
{
    public int IdRicetta { get; set; }

    public string Denominazione { get; set; } = null!;

    // ── Committente da anagrafica ArcLab (nessun vincolo referenziale) ──
    public int? IdClienteArcLab { get; set; }
    public int? IdInsediamentoArcLab { get; set; }
    public string? Committente { get; set; }
    public string? Lotto { get; set; }

    public decimal UnitaConsumoG { get; set; } = 100m;
    public decimal? UnitaConsumoPezzi { get; set; }

    /// <summary>Calo peso in cottura, % del peso crudo (tutto assunto acqua evaporata).</summary>
    public decimal? CaloPesoPercentuale { get; set; }

    /// <summary>In alternativa: peso del prodotto cotto (kg). Se presente, ricava la %.</summary>
    public decimal? PesoCottoKg { get; set; }

    public string? Note { get; set; }

    /// <summary>Responsabile del rapporto (può differire dall'utente che redige).</summary>
    public string? RedattoDa { get; set; }

    /// <summary>Descrizione testuale della porzione per l'etichetta USA (misura casalinga), es. "1/4 crust".</summary>
    public string? PorzioneDescrizioneUsa { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }

    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public ICollection<RigaRicetta> Righe { get; set; } = new List<RigaRicetta>();
}
