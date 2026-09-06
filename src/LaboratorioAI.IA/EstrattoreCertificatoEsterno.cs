using System.Text.Json;
using System.Text.Json.Serialization;
using LaboratorioAI.Core.Modelli;

namespace LaboratorioAI.IA;

/// <summary>
/// Legge un certificato (Rapporto di Prova) di un laboratorio ESTERNO in PDF e ne
/// estrae, tramite il modello Anthropic, il nostro lotto/campione, il numero RdP del
/// laboratorio e le criticità. È una PROPOSTA: il codice va validato contro ArcLab.
/// </summary>
public interface IEstrattoreCertificatoEsterno
{
    Task<EstrazioneCertificatoEsterno> EstraiAsync(byte[] pdf, CancellationToken ct = default);
}

/// <inheritdoc />
public class EstrattoreCertificatoEsterno : IEstrattoreCertificatoEsterno
{
    private readonly IClientAnthropic _client;

    public EstrattoreCertificatoEsterno(IClientAnthropic client) => _client = client;

    private static readonly JsonSerializerOptions Opzioni = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private const string Prompt = """
        Sei un assistente che legge il Rapporto di Prova (certificato) di un laboratorio
        ESTERNO inviato a MARE.A (documento allegato). Estrai i dati per rinominare e
        valutare il certificato. Rispondi ESCLUSIVAMENTE con un oggetto JSON valido,
        senza testo prima o dopo, con questi campi (usa null quando il dato non è presente):
        {
          "laboratorio": string,
          "lotto": string,
          "campione": string,
          "rdpEsterno": string,
          "nonConforme": true/false,
          "criticita": [string],
          "note": string
        }

        Regole:
        - "laboratorio": ragione sociale del laboratorio emittente (dall'intestazione o piè di pagina).
        - Il NOSTRO codice (lotto/campione, formato NNNN/NN) è indicato dal cliente e si
          trova in "Descrizione campione" ("CAMPIONE 7113/02 ..."), oppure in
          "Vs. Rif."/"Vostro riferimento", oppure "cod. ...". "lotto" = parte prima della
          "/", "campione" = parte dopo. NON confonderlo con il numero di accettazione del
          laboratorio (è un codice diverso). Se non è presente, lascia lotto e campione a
          null e segnalalo in "note".
        - "rdpEsterno": il numero di Rapporto di Prova del laboratorio (es. 26LA17574).
        - "nonConforme": true se il certificato dichiara una non conformità o un
          superamento di limite di legge.
        - "criticita": elenca qualunque cosa fuori specifica — non conformità dichiarate,
          parametri fuori limite, e valori elevati/anomali per il tipo di prodotto anche se
          non regolamentati da norme specifiche. Lista vuota se non ci sono criticità.
        - Non inventare dati.
        """;

    public async Task<EstrazioneCertificatoEsterno> EstraiAsync(byte[] pdf, CancellationToken ct = default)
    {
        var risposta = await _client.InterrogaConPdfAsync(Prompt, pdf, ct);
        var json = EstraiJson(risposta.Testo)
            ?? throw new InvalidOperationException("Il modello non ha restituito un JSON interpretabile.");
        return JsonSerializer.Deserialize<EstrazioneCertificatoEsterno>(json, Opzioni)
               ?? throw new InvalidOperationException("Estrazione non interpretabile.");
    }

    /// <summary>Isola l'oggetto JSON dalla risposta (rimuove code fence o testo attorno).</summary>
    private static string? EstraiJson(string testo)
    {
        if (string.IsNullOrWhiteSpace(testo)) return null;
        int inizio = testo.IndexOf('{');
        int fine = testo.LastIndexOf('}');
        return inizio >= 0 && fine > inizio ? testo[inizio..(fine + 1)] : null;
    }
}
