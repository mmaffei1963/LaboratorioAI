using LaboratorioAI.Dati.Pdf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

// Strumento usa e getta: verifica l'estrazione testo (EstrattoreTestoPdf, la stessa classe
// usata dall'app, con la stessa configurazione) su un PDF già scaricato, senza dover
// ripassare dal sito ACCREDIA.
//
// Uso:
//   dotnet run --project strumenti\VerificaEstrazionePdf
//   dotnet run --project strumenti\VerificaEstrazionePdf -- "C:\percorso\altro.pdf"
//
// Legge PercorsoPdfToText da appsettings.json/appsettings.Development.json del progetto Web
// (stessa configurazione usata in esecuzione dall'app), niente da duplicare qui.

var percorso = args.Length > 0
    ? args[0]
    : @"D:\LaboratorioAI\Archivio\AccreditamentiEsterni\00112_A\Revisione_85.pdf";

if (!File.Exists(percorso))
{
    Console.WriteLine($"File non trovato: {percorso}");
    return;
}

var cartellaWeb = Path.GetFullPath(Path.Combine(
    AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "LaboratorioAI.Web"));

var config = new ConfigurationBuilder()
    .SetBasePath(cartellaWeb)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build();

var percorsoPdfToText = config["AccreditamentiEsterni:PercorsoPdfToText"];
Console.WriteLine($"Configurazione letta da: {cartellaWeb}");
Console.WriteLine(string.IsNullOrWhiteSpace(percorsoPdfToText)
    ? "PercorsoPdfToText: non configurato (fallback pdftotext disattivato, si userà solo PdfPig)."
    : $"PercorsoPdfToText: {percorsoPdfToText}");

using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
var logger = loggerFactory.CreateLogger<EstrattoreTestoPdf>();
var estrattore = new EstrattoreTestoPdf(config, logger);

var pdf = File.ReadAllBytes(percorso);
var testo = estrattore.EstraiTesto(pdf);

Console.WriteLine();
Console.WriteLine($"--- File: {percorso} ---");
Console.WriteLine($"Caratteri estratti: {testo.Length}");
Console.WriteLine();

if (testo.Length > 0)
{
    Console.WriteLine("--- Primi 500 caratteri ---");
    Console.WriteLine(testo.Length > 500 ? testo[..500] : testo);
}
else
{
    Console.WriteLine("Nessun testo estratto (vedi eventuali warning sopra per il dettaglio).");
}
