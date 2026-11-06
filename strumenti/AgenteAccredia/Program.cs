using System.Net.Http.Headers;
using System.Net.Http.Json;
using LaboratorioAI.Dati.Accredia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

// Agente ACCREDIA: gira su questo PC (accesso diretto a certificati.accredia.it, verificato
// funzionante), scarica l'ultima revisione (ed eventuale PDF) per ogni laboratorio esterno attivo
// e invia il risultato al server di produzione, che applica la stessa logica del controllo
// diretto (confronto con l'ultima revisione nota, salvataggio, estrazione testo). Pensato per
// essere lanciato una volta al giorno da un'attività pianificata di Windows.

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var urlServer = config["ServerUrl"]?.TrimEnd('/');
var segreto = config["Segreto"];

if (string.IsNullOrWhiteSpace(urlServer) || string.IsNullOrWhiteSpace(segreto))
{
    Console.Error.WriteLine("Configurazione mancante: impostare \"ServerUrl\" e \"Segreto\" in appsettings.json.");
    return 1;
}

using var servizi = new ServiceCollection()
    .AddLogging(l => l.AddConsole())
    .AddHttpClient<IClientAccredia, ClientAccredia>(http => http.Timeout = TimeSpan.FromSeconds(30))
    .Services
    .BuildServiceProvider();

var client = servizi.GetRequiredService<IClientAccredia>();

using var httpServer = new HttpClient { BaseAddress = new Uri(urlServer + "/"), Timeout = TimeSpan.FromMinutes(5) };
httpServer.DefaultRequestHeaders.Add("X-Agente-Segreto", segreto);
httpServer.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Recupero l'elenco dei laboratori attivi da {urlServer}...");

List<LaboratorioAttivo>? laboratori;
try
{
    laboratori = await httpServer.GetFromJsonAsync<List<LaboratorioAttivo>>("api/accredia-agente/laboratori");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Impossibile contattare il server ({urlServer}): {ex.Message}");
    return 1;
}

if (laboratori is null || laboratori.Count == 0)
{
    Console.WriteLine("Nessun laboratorio attivo restituito dal server. Niente da fare.");
    return 0;
}

Console.WriteLine($"{laboratori.Count} laboratori da controllare.");

// Stato nella banca dati ACCREDIA (accreditato / sospeso / non trovato), dall'08/10/2026: due ricerche per tutti i
// laboratori. Se non riesce, lo stato non viene inviato (il server lascia quello precedente) e le revisioni proseguono.
StatiAccredia? stati = null;
try
{
    using var httpStato = new HttpClient(new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() })
        { Timeout = TimeSpan.FromMinutes(2) };
    httpStato.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) LaboratorioAI-AgenteAccredia");
    // Per i laboratori monitorati con sospensione attiva si leggono anche le prove sospese (righe con asterisco).
    stati = await new ClientStatoAccredia(httpStato).LeggiAsync(
        laboratori.Select(l => (l.NumeroAccreditamento, l.Sede)),
        avviso: m => Console.WriteLine($"ATTENZIONE: {m}"));
    Console.WriteLine($"Banca dati ACCREDIA: {stati.Accreditati.Count} laboratori accreditati, {stati.Sospesi.Count} con sospensione attiva.");
    foreach (var lab in laboratori)
        if (stati.Prove(lab.NumeroAccreditamento, lab.Sede) is { Count: > 0 } prove)
            Console.WriteLine($"  {lab.NomeLaboratorio}: {prove.Count} prove sospese.");
}
catch (Exception ex)
{
    Console.WriteLine($"ATTENZIONE: stato degli accreditamenti non letto ({ex.Message}). Proseguo con le revisioni.");
}

var voci = new List<VoceImportazioneAccredia>();
foreach (var lab in laboratori)
{
    var stato = stati?.Stato(lab.NumeroAccreditamento, lab.Sede);
    var proveSospese = stati?.Prove(lab.NumeroAccreditamento, lab.Sede);
    Console.Write($"  {lab.NomeLaboratorio} ({lab.NumeroAccreditamento}/{lab.Sede}){(stato is null ? "" : $" [{stato}]")}... ");
    try
    {
        var ultima = await client.UltimaRevisioneAsync(lab.NumeroAccreditamento, lab.Sede);
        if (ultima is null)
        {
            Console.WriteLine("nessuna revisione trovata.");
            voci.Add(new VoceImportazioneAccredia(lab.NumeroAccreditamento, lab.Sede, null, null, null, stato, proveSospese));
            continue;
        }

        var pdf = await client.ScaricaPdfRevisioneAsync(lab.NumeroAccreditamento, lab.Sede, ultima.NumeroRevisione);
        Console.WriteLine($"revisione n. {ultima.NumeroRevisione} (PDF {pdf.Length / 1024} KB).");

        voci.Add(new VoceImportazioneAccredia(
            lab.NumeroAccreditamento, lab.Sede, ultima.NumeroRevisione, ultima.DataEmissione,
            Convert.ToBase64String(pdf), stato, proveSospese));
    }
    catch (Exception ex)
    {
        // Non blocca gli altri laboratori: questo semplicemente non compare nel payload e il
        // server lo tratterà come "nessuna revisione trovata" (non distingue un errore
        // dell'agente da un vero "non trovato" — accettabile per uno strumento interno).
        Console.WriteLine($"ERRORE: {ex.Message}");
        // Lo stato ACCREDIA, se letto, si invia comunque (la revisione risulterà «non trovata» come prima).
        if (stato is not null)
            voci.Add(new VoceImportazioneAccredia(lab.NumeroAccreditamento, lab.Sede, null, null, null, stato, proveSospese));
    }
}

Console.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Invio {voci.Count} risultati al server...");

try
{
    var risposta = await httpServer.PostAsJsonAsync("api/accredia-agente/importa", voci);
    var corpo = await risposta.Content.ReadAsStringAsync();

    if (!risposta.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"Il server ha risposto {(int)risposta.StatusCode}: {corpo}");
        return 1;
    }

    Console.WriteLine($"Fatto. Risposta del server: {corpo}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Invio al server fallito: {ex.Message}");
    return 1;
}

record LaboratorioAttivo(int IdAccreditamento, string NumeroAccreditamento, string Sede, string NomeLaboratorio);
