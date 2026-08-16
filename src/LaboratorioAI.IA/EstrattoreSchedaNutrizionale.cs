using System.Text.Json;
using System.Text.Json.Serialization;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.IA;

/// <summary>
/// Estrae i valori nutrizionali per 100 g da una scheda tecnica in PDF, tramite
/// il modello Anthropic. Restituisce una PROPOSTA da rivedere a mano: non salva
/// nulla ed è pensata per precompilare la maschera della materia prima.
/// </summary>
public interface IEstrattoreSchedaNutrizionale
{
    Task<EstrazioneScheda> EstraiAsync(byte[] pdf, CancellationToken ct = default);
}

/// <inheritdoc />
public class EstrattoreSchedaNutrizionale : IEstrattoreSchedaNutrizionale
{
    private readonly IClientAnthropic _client;

    public EstrattoreSchedaNutrizionale(IClientAnthropic client) => _client = client;

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private const string Prompt = """
        Sei un assistente che estrae la dichiarazione nutrizionale da una scheda
        tecnica di un alimento (documento allegato).

        Estrai i valori nutrizionali PER 100 g di prodotto. Rispondi ESCLUSIVAMENTE
        con un oggetto JSON valido, senza testo prima o dopo, con questi campi
        (usa null quando il dato non è presente):
        {
          "denominazione": string,
          "grassi": number, "grassiSaturi": number,
          "carboidrati": number, "zuccheri": number,
          "proteine": number, "fibre": number,
          "sale": number, "acqua": number,
          "energiaKj": number, "energiaKcal": number,
          "base": "100g" | "porzione" | "sconosciuta",
          "note": string
        }

        Regole:
        - I numeri usano il punto come separatore decimale, senza unità di misura.
        - Se la scheda riporta il Sodio ma non il Sale, calcola Sale = Sodio × 2,5
          e segnalalo nel campo "note".
        - Se i valori sono espressi per porzione e non per 100 g, riportali comunque
          e imposta "base" a "porzione", indicandolo in "note".
        - In "note" segnala ogni incertezza o assunzione. Non inventare valori.
        """;

    public async Task<EstrazioneScheda> EstraiAsync(byte[] pdf, CancellationToken ct = default)
    {
        var risposta = await _client.InterrogaConPdfAsync(Prompt, pdf, ct);
        var json = EstraiJson(risposta.Testo);
        if (json is null)
            throw new InvalidOperationException("Il modello non ha restituito un JSON interpretabile.");

        return JsonSerializer.Deserialize<EstrazioneScheda>(json, Opzioni)
               ?? throw new InvalidOperationException("Estrazione non interpretabile.");
    }

    /// <summary>Isola l'oggetto JSON dalla risposta (rimuove eventuali code fence o testo).</summary>
    private static string? EstraiJson(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        int inizio = testo.IndexOf('{');
        int fine = testo.LastIndexOf('}');
        return inizio >= 0 && fine > inizio ? testo[inizio..(fine + 1)] : null;
    }
}
