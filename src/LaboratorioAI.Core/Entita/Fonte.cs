using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Fonte riutilizzabile dei valori nutrizionali (una scheda tecnica, il
/// dataset CREA, un referto, ecc.). La priorità di default riflette l'ordine
/// dichiarato nel rapporto: scheda tecnica → CREA/IEO → EuroFIR → altro.
/// </summary>
public class Fonte
{
    public int IdFonte { get; set; }

    public TipoFonte Tipo { get; set; }
    public string Descrizione { get; set; } = null!;
    public string? Riferimento { get; set; }
    public string? Url { get; set; }
    public DateTime? Data { get; set; }

    /// <summary>Priorità usata se non specificata sul singolo set (1 = più alta).</summary>
    public int PrioritaDefault { get; set; }

    public bool Attivo { get; set; } = true;

    public ICollection<ValoreNutrizionale> Valori { get; set; } = new List<ValoreNutrizionale>();
}
