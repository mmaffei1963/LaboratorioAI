using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.IA;

/// <summary>Esito di una chiamata al modello.</summary>
public record RispostaModello(string Testo, int TokenInput, int TokenOutput, string Modello)
{
    /// <summary>Token di input scritti nella cache (prompt caching), pagati 1,25 volte.</summary>
    public int TokenCacheScritti { get; init; }
    /// <summary>Token di input letti dalla cache, pagati 0,1 volte.</summary>
    public int TokenCacheLetti { get; init; }
}

/// <summary>Chiamata all'API Anthropic.</summary>
public interface IClientAnthropic
{
    Task<RispostaModello> InterrogaAsync(string prospetto, CancellationToken ct = default);

    /// <summary>Interroga il modello allegando un PDF (blocco «document»), es. una scheda tecnica.</summary>
    Task<RispostaModello> InterrogaConPdfAsync(string prompt, byte[] pdf, CancellationToken ct = default);

    /// <summary>
    /// Interroga il modello con istruzioni di sistema lunghe e stabili (es. il testo delle guide), messe in cache per 5 minuti:
    /// le domande successive con lo stesso testo costano circa un decimo sull'input (11/10/2026).
    /// </summary>
    Task<RispostaModello> InterrogaConSistemaAsync(string sistema, string messaggio, int? tokenMassimi = null, CancellationToken ct = default);

    /// <summary>
    /// Come <see cref="InterrogaConSistemaAsync"/> ma con un tempo massimo dedicato, per i lavori lunghi eseguiti in
    /// background (es. estrazione dei limiti da un testo di legge, 17/10/2026). Le altre chiamate restano a TimeoutSecondi.
    /// </summary>
    Task<RispostaModello> InterrogaLungaAsync(string sistema, string messaggio, int tokenMassimi, int timeoutSecondi, CancellationToken ct = default,
        string? modello = null);
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

    // 20/10/2026: temperatura 0 per l'analisi dei risultati: due campioni uguali devono avere lo stesso giudizio.
    public Task<RispostaModello> InterrogaAsync(string prospetto, CancellationToken ct = default) =>
        InviaAsync([new MessaggioApi { Role = "user", Content = prospetto }], ct, temperatura: 0);

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

    public Task<RispostaModello> InterrogaConSistemaAsync(string sistema, string messaggio, int? tokenMassimi = null, CancellationToken ct = default)
    {
        var blocchiSistema = new object[]
        {
            new { type = "text", text = sistema, cache_control = new { type = "ephemeral" } }
        };
        return InviaAsync([new MessaggioApi { Role = "user", Content = messaggio }], ct, blocchiSistema, tokenMassimi);
    }

    public Task<RispostaModello> InterrogaLungaAsync(string sistema, string messaggio, int tokenMassimi, int timeoutSecondi, CancellationToken ct = default,
        string? modello = null)
    {
        var blocchiSistema = new object[]
        {
            new { type = "text", text = sistema, cache_control = new { type = "ephemeral" } }
        };
        return InviaAsync([new MessaggioApi { Role = "user", Content = messaggio }], ct, blocchiSistema, tokenMassimi, timeoutSecondi, modello);
    }

    private async Task<RispostaModello> InviaAsync(List<MessaggioApi> messaggi, CancellationToken ct,
        object[]? sistema = null, int? tokenMassimi = null, int? timeoutSecondi = null, string? modello = null, double? temperatura = null)
    {
        if (!_opzioni.Configurato)
            throw new InvalidOperationException(
                "Chiave API Anthropic non configurata. Impostare Anthropic:ChiaveApi.");

        var richiesta = new RichiestaApi
        {
            Model = string.IsNullOrWhiteSpace(modello) ? _opzioni.Modello : modello,
            MaxTokens = tokenMassimi ?? _opzioni.TokenMassimi,
            System = sistema,
            Temperature = temperatura,
            Messages = messaggi
        };

        using var messaggio = new HttpRequestMessage(HttpMethod.Post, _opzioni.Endpoint)
        {
            Content = JsonContent.Create(richiesta)
        };
        messaggio.Headers.Add("x-api-key", _opzioni.ChiaveApi);
        messaggio.Headers.Add("anthropic-version", _opzioni.VersioneApi);

        // 17/10/2026: il tempo massimo è applicato qui, per chiamata (l'HttpClient non ha più un suo timeout).
        var secondi = timeoutSecondi ?? _opzioni.TimeoutSecondi;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(secondi));
        HttpResponseMessage risposta;
        try
        {
            risposta = await _http.SendAsync(messaggio, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Il modello non ha risposto entro {secondi} secondi.");
        }
        using var _r = risposta;

        if (!risposta.IsSuccessStatusCode)
        {
            var dettaglio = await risposta.Content.ReadAsStringAsync(cts.Token);
            throw new HttpRequestException(
                $"Chiamata al modello non riuscita ({(int)risposta.StatusCode}): {dettaglio}");
        }

        var corpo = await risposta.Content.ReadFromJsonAsync<RispostaApi>(cts.Token)
            ?? throw new InvalidOperationException("Risposta del modello non interpretabile.");

        var testo = string.Concat(corpo.Content
            .Where(b => b.Type == "text")
            .Select(b => b.Text));

        return new RispostaModello(
            testo,
            corpo.Usage?.InputTokens ?? 0,
            corpo.Usage?.OutputTokens ?? 0,
            corpo.Model ?? _opzioni.Modello)
        {
            TokenCacheScritti = corpo.Usage?.CacheCreationInputTokens ?? 0,
            TokenCacheLetti = corpo.Usage?.CacheReadInputTokens ?? 0
        };
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
        [JsonPropertyName("system"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object[]? System { get; set; }
        [JsonPropertyName("temperature"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? Temperature { get; set; }
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
        [JsonPropertyName("cache_creation_input_tokens")] public int? CacheCreationInputTokens { get; set; }
        [JsonPropertyName("cache_read_input_tokens")] public int? CacheReadInputTokens { get; set; }
    }
}
