using System.Globalization;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace LaboratorioAI.Dati.Accredia;

/// <summary>Ultima revisione pubblicata da ACCREDIA per un laboratorio/sede.</summary>
public record UltimaRevisioneAccredia(int NumeroRevisione, DateTime? DataEmissione);

/// <summary>
/// Interroga il portale pubblico ACCREDIA (banca dati laboratori di prova) per un
/// laboratorio/sede specifico, identificato da numero di accreditamento e sede (es.
/// "00253"/"A"). Nessuna API pubblica: si usano gli URL stabili scoperti a mano
/// (https://certificati.accredia.it/cert_pa/L/{numero}/{sede}/A per lo storico
/// revisioni, .../C{numero} per il PDF di una revisione) — niente scraping di form o
/// automazione browser, solo richieste HTTP dirette e un feed XML interno alla pagina.
/// </summary>
public interface IClientAccredia
{
    /// <summary>Ultima revisione dell'elenco prove pubblicata per il laboratorio/sede indicato.</summary>
    Task<UltimaRevisioneAccredia?> UltimaRevisioneAsync(string numeroAccreditamento, string sede, CancellationToken ct = default);

    /// <summary>Scarica il PDF di una specifica revisione dell'elenco prove.</summary>
    Task<byte[]> ScaricaPdfRevisioneAsync(string numeroAccreditamento, string sede, int numeroRevisione, CancellationToken ct = default);
}

/// <inheritdoc cref="IClientAccredia" />
public class ClientAccredia : IClientAccredia
{
    private readonly HttpClient _http;
    private readonly ILogger<ClientAccredia> _log;

    public ClientAccredia(HttpClient http, ILogger<ClientAccredia> log)
    {
        _http = http;
        _log = log;
    }

    public async Task<UltimaRevisioneAccredia?> UltimaRevisioneAsync(
        string numeroAccreditamento, string sede, CancellationToken ct = default)
    {
        // Passo 1: la pagina "storico revisioni" reindirizza (lato server) a un URL che
        // contiene un codice interno specifico del laboratorio (parametro x3), necessario
        // per interrogare il feed dati. Basta seguire il redirect e leggere l'URL finale.
        var urlStorico = $"https://certificati.accredia.it/cert_pa/L/{numeroAccreditamento}/{sede}/A";
        using var rispStorico = await _http.GetAsync(urlStorico, ct);
        rispStorico.EnsureSuccessStatusCode();

        var urlFinale = rispStorico.RequestMessage?.RequestUri?.ToString() ?? "";
        var codiceInterno = EstraiParametro(urlFinale, "x3");
        if (codiceInterno is null)
        {
            _log.LogWarning("Accreditamento {Numero}/{Sede}: impossibile trovare il codice interno ACCREDIA (x3) da {Url}",
                numeroAccreditamento, sede, urlFinale);
            return null;
        }

        // Passo 2: il feed XML che alimenta la tabella delle revisioni sulla pagina.
        var urlFeed = $"https://certificati.accredia.it/cert_pa/grideleprovavita.php?f1={codiceInterno}";
        var xml = await _http.GetStringAsync(urlFeed, ct);

        var righe = XDocument.Parse(xml).Root?.Elements("row") ?? Enumerable.Empty<XElement>();

        UltimaRevisioneAccredia? migliore = null;
        foreach (var riga in righe)
        {
            var celle = riga.Elements("cell").ToList();
            if (celle.Count < 4) continue;
            if (!int.TryParse(celle[1].Value.Trim(), out var numeroRevisione)) continue;

            DateTime? dataEmissione = LeggiData(celle[3].Value);

            if (migliore is null || numeroRevisione > migliore.NumeroRevisione)
                migliore = new UltimaRevisioneAccredia(numeroRevisione, dataEmissione);
        }

        return migliore;
    }

    // Formati accettati per la data di emissione del feed ACCREDIA (corretto l'08/10/2026): prima si usava un
    // TryParse «invariante», che legge 05/10/2026 come 10 maggio (formato americano) e scarta 25/09/2026.
    // Prima i formati italiani, poi quelli ISO; nessun formato mese/giorno.
    private static readonly string[] FormatiData =
    {
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd-MM-yyyy", "dd.MM.yyyy",
        "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy/MM/dd"
    };

    /// <summary>Data di emissione della revisione dal testo del feed; null se non riconosciuta.</summary>
    internal static DateTime? LeggiData(string? testo)
    {
        var t = (testo ?? "").Trim();
        return DateTime.TryParseExact(t, FormatiData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.Date : null;
    }

    public async Task<byte[]> ScaricaPdfRevisioneAsync(
        string numeroAccreditamento, string sede, int numeroRevisione, CancellationToken ct = default)
    {
        var url = $"https://certificati.accredia.it/cert_pa/L/{numeroAccreditamento}/{sede}/C{numeroRevisione}";
        return await _http.GetByteArrayAsync(url, ct);
    }

    private static string? EstraiParametro(string url, string nome)
    {
        var idx = url.IndexOf('?');
        if (idx < 0) return null;
        var query = url[(idx + 1)..];
        foreach (var coppia in query.Split('&'))
        {
            var parti = coppia.Split('=', 2);
            if (parti.Length == 2 && parti[0] == nome)
                return Uri.UnescapeDataString(parti[1]);
        }
        return null;
    }
}
