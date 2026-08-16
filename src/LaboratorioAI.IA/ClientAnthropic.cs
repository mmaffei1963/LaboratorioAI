using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.IA;

/// <summary>Esito di una chiamata al modello.</summary>
public record RispostaModello(string Testo, int TokenInput, int TokenOutput, string Modello);

/// <summary>Chiamata all'API Anthropic.</summary>
public interface IClientAnthropic
{
    Task<RispostaModello> InterrogaAsync(string prospetto, CancellationToken ct = default);

    /// <summary>Interroga il modello allegando un PDF (blocco «document»), es. una scheda tecnica.</summary>
    Task<RispostaModello> InterrogaConPdfAsync(string prompt, byte[] pdf, CancellationToken ct = default);
}

/// <summary>
/// Implementazione basata su HttpClient.
///
/// Anthropic non pubblica un SDK ufficiale per .NET; l'API è però
/// REST/JSON e l'integrazione diretta è preferibile a una libreria
/// di terze parti non mantenuta.
/// </summary>
public class ClientAnthropic : IClientAnthropic
{
    private readonly HttpClient _http;
    private readonly OpzioniAnthropic _opzioni;

    public ClientAnthropic(HttpClient http, IOptions<OpzioniAnthropic> opzioni)
    {
        _http = http;
        _opzioni = opzioni.Value;
    }

    public Task<RispostaModello> InterrogaAsync(string prospetto, CancellationToken ct = default) =>
        InviaAsync([new MessaggioApi { Role = "user", Content = prospetto }], ct);

    public Task<RispostaModello> InterrogaConPdfAsync(string prompt, byte[] pdf, CancellationToken ct = default)
    {
        // Contenuto misto: prima il documento PDF, poi il testo della richiesta.
        var contenuto = new object[]
        {
            new
            {
                type = "document",
                source = new
                {
                    type = "base64",
                    media_type = "application/pdf",
                    data = Convert.ToBase64String(pdf)
                }
            },
            new { type = "text", text = prompt }
        };
        return InviaAsync([new MessaggioApi { Role = "user", Content = contenuto }], ct);
    }

    private async Task<RispostaModello> InviaAsync(List<MessaggioApi> messaggi, CancellationToken ct)
    {
        if (!_opzioni.Configurato)
            throw new InvalidOperationException(
                "Chiave API Anthropic non configurata. Impostare Anthropic:ChiaveApi.");

        var richiesta = new RichiestaApi
        {
            Model = _opzioni.Modello,
            MaxTokens = _opzioni.TokenMassimi,
            Messages = messaggi
        };

        using var messaggio = new HttpRequestMessage(HttpMethod.Post, _opzioni.Endpoint)
        {
            Content = JsonContent.Create(richiesta)
        };
        messaggio.Headers.Add("x-api-key", _opzioni.ChiaveApi);
        messaggio.Headers.Add("anthropic-version", _opzioni.VersioneApi);

        using var risposta = await _http.SendAsync(messaggio, ct);

        if (!risposta.IsSuccessStatusCode)
        {
            var dettaglio = await risposta.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"Chiamata al modello non riuscita ({(int)risposta.StatusCode}): {dettaglio}");
        }

        var corpo = await risposta.Content.ReadFromJsonAsync<RispostaApi>(ct)
            ?? throw new InvalidOperationException("Risposta del modello non interpretabile.");

        var testo = string.Concat(corpo.Content
            .Where(b => b.Type == "text")
            .Select(b => b.Text));

        return new RispostaModello(
            testo,
            corpo.Usage?.InputTokens ?? 0,
            corpo.Usage?.OutputTokens ?? 0,
            corpo.Model ?? _opzioni.Modello);
    }

    /// <summary>Stima del costo di una chiamata, in euro.</summary>
    public decimal StimaCosto(int tokenInput, int tokenOutput) =>
        (tokenInput  / 1_000_000m * _opzioni.CostoInputPerMilione) +
        (tokenOutput / 1_000_000m * _opzioni.CostoOutputPerMilione);

    // ── Contratti dell'API ─────────────────────────────────────────

    private class RichiestaApi
    {
        [JsonPropertyName("model")]      public string Model { get; set; } = "";
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; set; }
        [JsonPropertyName("messages")]   public List<MessaggioApi> Messages { get; set; } = [];
    }

    private class MessaggioApi
    {
        [JsonPropertyName("role")]    public string Role { get; set; } = "";

        /// <summary>Testo semplice (string) oppure elenco di blocchi (documento + testo).</summary>
        [JsonPropertyName("content")] public object Content { get; set; } = "";
    }

    private class RispostaApi
    {
        [JsonPropertyName("model")]   public string? Model { get; set; }
        [JsonPropertyName("content")] public List<BloccoContenuto> Content { get; set; } = [];
        [JsonPropertyName("usage")]   public Consumo? Usage { get; set; }
    }

    private class BloccoContenuto
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "";
        [JsonPropertyName("text")] public string Text { get; set; } = "";
    }

    private class Consumo
    {
        [JsonPropertyName("input_tokens")]  public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
    }
}
