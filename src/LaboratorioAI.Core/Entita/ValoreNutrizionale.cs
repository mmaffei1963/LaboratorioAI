namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Un set di valori nutrizionali per 100 g di parte edibile di una materia
/// prima, con la sua fonte e priorità. Una materia prima può averne più di uno
/// (fonti diverse, aggiornamenti nel tempo): lo storico non si cancella, così i
/// rapporti già emessi restano riproducibili.
/// </summary>
public class ValoreNutrizionale
{
    public int IdValoreNutrizionale { get; set; }

    public int IdMateriaPrima { get; set; }
    public MateriaPrima? MateriaPrima { get; set; }

    // ── Valori per 100 g ────────────────────────────────────────────
    public decimal Grassi { get; set; }
    public decimal GrassiSaturi { get; set; }
    public decimal Carboidrati { get; set; }
    public decimal Zuccheri { get; set; }
    public decimal Proteine { get; set; }
    public decimal Sale { get; set; }
    public decimal Fibre { get; set; }
    public decimal Acqua { get; set; }

    /// <summary>Energia, se fornita dalla fonte; altrimenti si ricava dai macro.</summary>
    public decimal? EnergiaKj { get; set; }
    public decimal? EnergiaKcal { get; set; }

    // ── Nutrienti aggiuntivi per etichette USA/Canada (per 100 g) ───
    // Nullable: "sconosciuto" è diverso da "zero" per i micronutrienti.
    public decimal? Sodio { get; set; }            // mg
    public decimal? GrassiTrans { get; set; }      // g
    public decimal? GrassiMonoinsaturi { get; set; } // g
    public decimal? GrassiPolinsaturi { get; set; } // g
    public decimal? Colesterolo { get; set; }      // mg
    public decimal? VitaminaD { get; set; }        // µg
    public decimal? Calcio { get; set; }           // mg
    public decimal? Ferro { get; set; }            // mg
    public decimal? Potassio { get; set; }         // mg

    public int? IdFonte { get; set; }
    public Fonte? Fonte { get; set; }

    /// <summary>Priorità del set (1 = più alta). Guida la scelta del set corrente.</summary>
    public int Priorita { get; set; }

    /// <summary>Set attualmente attivo per la materia prima.</summary>
    public bool Corrente { get; set; }

    public DateTime? DataValidita { get; set; }
    public DateTime DataAcquisizione { get; set; }

    /// <summary>Riferimento al documento di origine (n° referto, file scheda, URL).</summary>
    public string? RiferimentoDocumento { get; set; }
    public string? Note { get; set; }

    public DateTime DataCreazione { get; set; }
}
