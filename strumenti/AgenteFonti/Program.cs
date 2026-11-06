using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

// Agente fonti normative (17/10/2026). Gira sul PC dell'ufficio come l'agente ACCREDIA:
// 1) chiede al server le fonti attive con un canale automatico;
// 2) Cellar (EUR-Lex): cerca con SPARQL l'ultimo testo consolidato e lo scarica in XHTML italiano;
//    Normattiva: legge con le API Open Data tutti gli articoli e gli allegati del testo vigente;
// 3) invia il testo al server, che lo salva come nuova versione solo se è cambiato (impronta).
// Il server di produzione non può farlo da solo: EUR-Lex lo blocca e Normattiva via web dà solo il primo articolo.

var config = LeggiConfigurazione(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));
var urlServer = (Opzione(args, "--server") ?? config.GetValueOrDefault("ServerUrl"))?.TrimEnd('/');
var segreto = Opzione(args, "--segreto") ?? config.GetValueOrDefault("Segreto");
var soloFonte = int.TryParse(Opzione(args, "--fonte"), out var idf) ? idf : (int?)null;
var cartellaCopie = Opzione(args, "--salva");   // facoltativo: salva anche in locale una copia di ciò che invia

if (string.IsNullOrWhiteSpace(urlServer))
{
    Console.Error.WriteLine("Configurazione mancante: impostare \"ServerUrl\" (e \"Segreto\") in appsettings.json.");
    return 1;
}

using var httpServer = new HttpClient { BaseAddress = new Uri(urlServer + "/"), Timeout = TimeSpan.FromMinutes(5) };
if (!string.IsNullOrWhiteSpace(segreto)) httpServer.DefaultRequestHeaders.Add("X-Agente-Segreto", segreto);
httpServer.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

using var httpFonti = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
httpFonti.DefaultRequestHeaders.UserAgent.ParseAdd("LaboratorioAI-AgenteFonti/1.0 (MARE.A srl, laboratorio di prova)");

Log($"Recupero le fonti da {urlServer}...");
List<Fonte>? fonti;
try { fonti = await httpServer.GetFromJsonAsync<List<Fonte>>("api/fonti-agente/fonti", Json.Opzioni); }
catch (Exception ex) { Console.Error.WriteLine($"Impossibile contattare il server ({urlServer}): {ex.Message}"); return 1; }
if (fonti is null || fonti.Count == 0) { Log("Nessuna fonte con canale automatico. Niente da fare."); return 0; }
if (soloFonte is int sf) fonti = fonti.Where(f => f.Id == sf).ToList();
Log($"{fonti.Count} fonti da aggiornare.");

var errori = 0;
foreach (var f in fonti)
{
    Console.Write($"  [{f.Id}] {f.Titolo} ({f.Canale} {f.Codice})... ");
    try
    {
        var t = f.Canale switch
        {
            "Cellar" => await Cellar.ScaricaAsync(httpFonti, f.Codice),
            "Normattiva" => await Normattiva.ScaricaAsync(httpFonti, f.Codice),
            _ => throw new InvalidOperationException("canale sconosciuto")
        };
        Console.Write($"{t.Contenuto.Length / 1024:N0} KB ({t.Nota})... ");
        if (!string.IsNullOrWhiteSpace(cartellaCopie))
        {
            Directory.CreateDirectory(cartellaCopie);
            await File.WriteAllBytesAsync(Path.Combine(cartellaCopie, $"fonte{f.Id}_{t.NomeFile}"), t.Contenuto);
        }
        var risposta = await httpServer.PostAsJsonAsync($"api/fonti-agente/fonti/{f.Id}/testo", new
        {
            nomeFile = t.NomeFile, tipoContenuto = t.Tipo, contenutoBase64 = Convert.ToBase64String(t.Contenuto),
            dataAggiornamento = t.DataAggiornamento, nota = t.Nota
        });
        var corpo = await risposta.Content.ReadAsStringAsync();
        if (!risposta.IsSuccessStatusCode) { errori++; Console.WriteLine($"il server ha risposto {(int)risposta.StatusCode}: {corpo}"); continue; }
        var esito = JsonSerializer.Deserialize<EsitoServer>(corpo, Json.Opzioni);
        if (esito is { Riuscita: false }) errori++;
        Console.WriteLine(esito?.Messaggio ?? corpo);
    }
    catch (Exception ex) { errori++; Console.WriteLine($"ERRORE: {ex.Message}"); }
}
Log(errori == 0 ? "Fatto." : $"Fatto, con {errori} errori.");
return errori == 0 ? 0 : 2;

static void Log(string m) => Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {m}");

static string? Opzione(string[] a, string nome)
{
    var i = Array.IndexOf(a, nome);
    return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
}

static Dictionary<string, string> LeggiConfigurazione(string file)
{
    var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    if (!File.Exists(file)) return d;
    using var doc = JsonDocument.Parse(File.ReadAllText(file));
    foreach (var p in doc.RootElement.EnumerateObject())
        if (p.Value.ValueKind == JsonValueKind.String) d[p.Name] = p.Value.GetString()!;
    return d;
}

record Fonte(int Id, string Titolo, string Canale, string Codice);
record EsitoServer(bool Riuscita, string? Messaggio, bool Nuova);
record TestoScaricato(byte[] Contenuto, string Tipo, string NomeFile, DateTime? DataAggiornamento, string Nota);

static class Json
{
    public static readonly JsonSerializerOptions Opzioni = new(JsonSerializerDefaults.Web);
}

/// <summary>Cellar, l'archivio dell'Ufficio delle pubblicazioni dell'UE da cui EUR-Lex prende i testi.</summary>
static class Cellar
{
    private const string Sparql = "https://publications.europa.eu/webapi/rdf/sparql";

    public static async Task<TestoScaricato> ScaricaAsync(HttpClient http, string celexBase)
    {
        // Testi consolidati: CELEX «0» + numero + «-AAAAMMGG» (es. 02005R2073-20260701). Se non ce ne sono, l'originale.
        var prefisso = "0" + celexBase[1..] + "-";
        var q = "PREFIX cdm: <http://publications.europa.eu/ontology/cdm#> SELECT DISTINCT ?celex WHERE { ?w cdm:resource_legal_id_celex ?celex . "
              + $"FILTER(STRSTARTS(STR(?celex), \"{prefisso}\")) }}";
        var url = $"{Sparql}?query={Uri.EscapeDataString(q)}&format={Uri.EscapeDataString("application/sparql-results+json")}";
        using var doc = JsonDocument.Parse(await http.GetStringAsync(url));
        var consolidati = doc.RootElement.GetProperty("results").GetProperty("bindings").EnumerateArray()
            .Select(b => b.GetProperty("celex").GetProperty("value").GetString()!)
            .Where(c => Regex.IsMatch(c, @"^0\d{4}[A-Z]\d{4}-\d{8}$")).OrderBy(c => c).ToList();
        var celex = consolidati.LastOrDefault() ?? celexBase;
        DateTime? data = consolidati.Count > 0
            ? DateTime.ParseExact(celex[^8..], "yyyyMMdd", CultureInfo.InvariantCulture)
            : null;

        using var req = new HttpRequestMessage(HttpMethod.Get, $"http://publications.europa.eu/resource/celex/{celex}");
        req.Headers.Accept.ParseAdd("application/xhtml+xml");
        req.Headers.AcceptLanguage.ParseAdd("ita");
        using var r = await http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        var dati = await r.Content.ReadAsByteArrayAsync();
        return new TestoScaricato(dati, "text/html", $"{celex}.xhtml", data,
            consolidati.Count > 0 ? $"consolidato {celex}" : $"testo originale {celex} (nessun consolidato)");
    }
}

/// <summary>API Open Data di Normattiva: un articolo (o un allegato) per richiesta, testo vigente.</summary>
static class Normattiva
{
    private const string Api = "https://api.normattiva.it/t/normattiva.api/bff-opendata/v1/api/v1/atto/dettaglio-atto-urn";
    private const int ArticoliMassimi = 400, AllegatiMassimi = 30;

    private static async Task<(string Titolo, string Html, string? Vigenza)?> PezzoAsync(HttpClient http, string urn)
    {
        using var r = await http.PostAsJsonAsync(Api, new { urn });
        if (!r.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object
            || !data.TryGetProperty("atto", out var atto) || atto.ValueKind != JsonValueKind.Object) return null;
        var html = atto.TryGetProperty("articoloHtml", out var h) ? h.GetString() : null;
        if (string.IsNullOrWhiteSpace(html)) return null;
        var titolo = atto.TryGetProperty("titolo", out var t) ? t.GetString() ?? "" : "";
        string? vig = atto.TryGetProperty("articoloDataInizioVigenza", out var v) ? v.GetString() : null;
        return (titolo, html, vig);
    }

    public static async Task<TestoScaricato> ScaricaAsync(HttpClient http, string urn)
    {
        var sb = new StringBuilder();
        string titolo = urn; string? ultimaVigenza = null; int articoli = 0, allegati = 0;
        var visti = new HashSet<string>();

        void Aggiungi(string html, string? vig)
        {
            sb.Append("<section>").Append(html).Append("</section>\n");
            if (vig is { Length: 8 } && string.CompareOrdinal(vig, ultimaVigenza ?? "") > 0 && vig != "99999999") ultimaVigenza = vig;
        }

        // Articoli: ~art1, ~art2, ... fino al primo che manca o che ripete il precedente.
        for (var n = 1; n <= ArticoliMassimi; n++)
        {
            var p = await PezzoAsync(http, $"{urn}~art{n}");
            if (p is null || !visti.Add(p.Value.Html)) break;
            if (n == 1) titolo = p.Value.Titolo;
            Aggiungi(p.Value.Html, p.Value.Vigenza);
            articoli++;
            // articoli «bis», «ter»...: si prova solo dove esistono
            foreach (var suff in new[] { "bis", "ter", "quater", "quinquies", "sexies" })
            {
                var pb = await PezzoAsync(http, $"{urn}~art{n}{suff}");
                if (pb is null || !visti.Add(pb.Value.Html)) break;
                Aggiungi(pb.Value.Html, pb.Value.Vigenza);
                articoli++;
            }
        }
        // Allegati: :1~art1, :2~art1, ...
        for (var a = 1; a <= AllegatiMassimi; a++)
        {
            var p = await PezzoAsync(http, $"{urn}:{a}~art1");
            if (p is null || !visti.Add(p.Value.Html)) break;
            Aggiungi(p.Value.Html, p.Value.Vigenza);
            allegati++;
        }
        if (articoli == 0) throw new InvalidOperationException("Normattiva non ha restituito articoli per " + urn);

        var pagina = $"<!doctype html><html><head><meta charset=\"utf-8\"><title>{System.Net.WebUtility.HtmlEncode(titolo)}</title></head><body>"
                   + $"<h1>{System.Net.WebUtility.HtmlEncode(titolo)}</h1>\n<p>Testo vigente da Normattiva (API Open Data), {urn}</p>\n{sb}</body></html>";
        DateTime? data = ultimaVigenza is null ? null : DateTime.ParseExact(ultimaVigenza, "yyyyMMdd", CultureInfo.InvariantCulture);
        var nome = Regex.Replace(urn, @"[^A-Za-z0-9.;-]", "_") + ".html";
        return new TestoScaricato(Encoding.UTF8.GetBytes(pagina), "text/html", nome, data,
            $"{articoli} articoli, {allegati} allegati" + (data is DateTime d ? $", ultima modifica in vigore dal {d:dd/MM/yyyy}" : ""));
    }
}
