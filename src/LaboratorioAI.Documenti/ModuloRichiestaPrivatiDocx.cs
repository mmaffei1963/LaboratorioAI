using System.Text.Json;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Supporto alle richieste di analisi dei clienti privati. Dal 06/11/2026 il documento da firmare è l'Ordine di
/// Accettazione per i privati (<see cref="OrdinePrivatoDocx"/>, Mod. Com. 05B); il modello Word con i segnaposto
/// (wwwroot/docx/Modulo_Richiesta_Analisi_Privati.docx) non è più usato e si può eliminare.
/// </summary>
public static class ModuloRichiestaPrivatiDocx
{
    /// <summary>Metodi di una riga, copiati da ArcLab al momento della richiesta.</summary>
    public static IReadOnlyList<MetodoRichiestaPrivato> Metodi(RigaRichiestaAnalisiPrivato riga)
    {
        if (string.IsNullOrWhiteSpace(riga.MetodiJson)) return [];
        try { return JsonSerializer.Deserialize<List<MetodoRichiestaPrivato>>(riga.MetodiJson) ?? []; }
        catch (JsonException) { return []; }
    }
}
