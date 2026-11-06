using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LaboratorioAI.Dati.Clienti;

/// <summary>Esito di una verifica VIES: Valida null = servizio non disponibile, riprovare più tardi.</summary>
public sealed record EsitoVies(bool? Valida, string? Nome, string? Indirizzo, string? Errore);

/// <summary>
/// Verifica di una P.IVA sul servizio pubblico VIES della Commissione europea (12/10/2026), API REST gratuita.
/// Attenzione: in VIES compaiono solo le imprese abilitate alle operazioni intracomunitarie, quindi «non trovata»
/// non vuol dire per forza «cessata». Si inviano solo paese e numero di P.IVA.
/// </summary>
public sealed class ClientVies
{
    public const string Indirizzo = "https://ec.europa.eu/taxation_customs/vies/rest-api/ms/";
    private readonly HttpClient _http;

    public ClientVies(HttpClient http) => _http = http;

    public async Task<EsitoVies> VerificaAsync(string paese, string partitaIva, CancellationToken ct = default)
    {
        try
        {
            var r = await _http.GetFromJsonAsync<RispostaVies>(
                $"{Indirizzo}{Uri.EscapeDataString(paese)}/vat/{Uri.EscapeDataString(partitaIva)}", ct);
            if (r is null) return new EsitoVies(null, null, null, "Risposta vuota da VIES.");
            if (r.IsValid) return new EsitoVies(true, Pulito(r.Name), Pulito(r.Address), null);
            // userError: VALID / INVALID oppure un codice di servizio non disponibile (MS_UNAVAILABLE, TIMEOUT, …).
            return string.Equals(r.UserError, "INVALID", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(r.UserError)
                ? new EsitoVies(false, null, null, null)
                : new EsitoVies(null, null, null, $"VIES non disponibile ({r.UserError}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            return new EsitoVies(null, null, null, "VIES non raggiungibile: " + (ex.Message.Length > 200 ? ex.Message[..200] : ex.Message));
        }
    }

    private static string? Pulito(string? s) =>
        string.IsNullOrWhiteSpace(s) || s.Trim() == "---" ? null : s.Replace('\n', ' ').Trim();

    private sealed class RispostaVies
    {
        [JsonPropertyName("isValid")] public bool IsValid { get; set; }
        [JsonPropertyName("userError")] public string? UserError { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("address")] public string? Address { get; set; }
    }
}
