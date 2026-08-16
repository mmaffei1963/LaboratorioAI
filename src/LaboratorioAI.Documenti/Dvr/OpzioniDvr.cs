using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.Documenti.Dvr;

/// <summary>Percorsi e comportamenti della generazione DVR.</summary>
public class OpzioniDvr
{
    public const string Sezione = "Dvr";

    /// <summary>Cartella dei modelli documentali.</summary>
    public string CartellaModelli { get; set; } = "";

    /// <summary>Cartella di destinazione dei documenti prodotti.</summary>
    public string CartellaArchivio { get; set; } = "";

    /// <summary>Nome del modello per ciascuna regione.</summary>
    public Dictionary<string, string> Modelli { get; set; } = new()
    {
        ["EmiliaRomagna"] = "DVR_Legionellosi_EmiliaRomagna.docx",
        ["Marche"]        = "DVR_Legionellosi_Marche.docx"
    };

    /// <summary>
    /// Se vero i controlli contenuto vengono sciolti al termine.
    /// Il documento consegnato risulta così un testo ordinario, senza
    /// i bordi grigi dei campi compilabili.
    /// </summary>
    public bool SciogliControlli { get; set; } = true;

    public string ModelloPer(RegioneDvr regione)
    {
        var chiave = regione.ToString();

        if (!Modelli.TryGetValue(chiave, out var nome))
            throw new InvalidOperationException(
                $"Nessun modello configurato per la regione {chiave}.");

        return Path.Combine(CartellaModelli, nome);
    }
}
