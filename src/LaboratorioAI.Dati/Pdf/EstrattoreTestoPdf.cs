using System.Diagnostics;
using System.Text;
using LaboratorioAI.Core.Servizi;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;

namespace LaboratorioAI.Dati.Pdf;

/// <inheritdoc cref="IEstrattoreTestoPdf" />
public class EstrattoreTestoPdf : IEstrattoreTestoPdf
{
    private readonly IConfiguration _config;
    private readonly ILogger<EstrattoreTestoPdf> _log;

    public EstrattoreTestoPdf(IConfiguration config, ILogger<EstrattoreTestoPdf> log)
    {
        _config = config;
        _log = log;
    }

    public string EstraiTesto(byte[] pdf)
    {
        var testo = EstraiConPdfPig(pdf);
        if (!string.IsNullOrWhiteSpace(testo))
            return testo;

        // PdfPig non ha estratto nulla (PDF scansionato, oppure — caso osservato sui PDF ACCREDIA —
        // un content stream con un operatore "cm" in un formato che PdfPig non sa interpretare e che
        // manda in eccezione ogni pagina). Ripiego su pdftotext (Poppler), molto più tollerante ai
        // PDF "sporchi": se non è configurato o fallisce, resta il risultato di PdfPig (stringa vuota).
        return EstraiConPdfToText(pdf) ?? testo;
    }

    /// <summary>Prova con PdfPig, una pagina per volta: una pagina che fallisce non deve far perdere le altre.</summary>
    private string EstraiConPdfPig(byte[] pdf)
    {
        try
        {
            using var documento = PdfDocument.Open(pdf);
            var sb = new StringBuilder();
            var paginaFallite = 0;

            for (var numeroPagina = 1; numeroPagina <= documento.NumberOfPages; numeroPagina++)
            {
                try
                {
                    sb.AppendLine(documento.GetPage(numeroPagina).Text);
                }
                catch (Exception ex)
                {
                    paginaFallite++;
                    _log.LogWarning(ex, "PdfPig: estrazione pagina {Pagina} non riuscita, pagina saltata.", numeroPagina);
                }
            }

            if (paginaFallite > 0)
                _log.LogWarning("PdfPig: {Fallite} pagine su {Totale} non lette.", paginaFallite, documento.NumberOfPages);

            return sb.ToString();
        }
        catch (Exception ex)
        {
            // Un PDF non apribile (scansione senza testo, formato inatteso, file corrotto) non deve
            // bloccare il rilevamento della revisione: si perde solo il suggerimento di abbinamento
            // (o si tenta il fallback pdftotext, se configurato).
            _log.LogWarning(ex, "PdfPig: apertura del PDF non riuscita.");
            return "";
        }
    }

    /// <summary>
    /// Ripiego su pdftotext (Poppler, eseguibile esterno) quando PdfPig non estrae nulla.
    /// Restituisce null — non stringa vuota — se il fallback non è utilizzabile (percorso non
    /// configurato, binario non trovato, errore di esecuzione), per non confondere "non ho potuto
    /// provare" con "ho provato e il PDF non ha testo".
    /// </summary>
    private string? EstraiConPdfToText(byte[] pdf)
    {
        var percorsoEseguibile = _config["AccreditamentiEsterni:PercorsoPdfToText"];
        if (string.IsNullOrWhiteSpace(percorsoEseguibile))
            return null;

        if (!File.Exists(percorsoEseguibile))
        {
            _log.LogWarning("pdftotext configurato ({Percorso}) ma il file non esiste: fallback non disponibile.", percorsoEseguibile);
            return null;
        }

        var fileTemporaneo = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        try
        {
            File.WriteAllBytes(fileTemporaneo, pdf);

            using var processo = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = percorsoEseguibile,
                    // -layout mantiene l'impaginazione a colonne (più leggibile per il confronto
                    // testuale dei suggerimenti); "-" scrive il testo su stdout invece che su file.
                    ArgumentList = { "-layout", fileTemporaneo, "-" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            processo.Start();

            // Leggere stdout e stderr in sequenza con ReadToEnd() può causare un deadlock: se il
            // processo figlio scrive abbastanza su stderr da riempire il buffer del pipe mentre noi
            // siamo ancora bloccati a leggere lo stdout, lui si blocca in scrittura e noi in lettura,
            // e nessuno dei due si sblocca più (osservato in pratica con pdftotext -layout, che scrive
            // warning su stderr per molti PDF). Le due letture vanno avviate insieme, non in sequenza.
            var letturaTesto = processo.StandardOutput.ReadToEndAsync();
            var letturaErrore = processo.StandardError.ReadToEndAsync();

            var uscitoInTempo = processo.WaitForExit(30_000);

            if (!uscitoInTempo)
            {
                _log.LogWarning("pdftotext: timeout, il processo non è terminato entro 30s.");
                try { processo.Kill(entireProcessTree: true); } catch { /* già terminato o non killabile */ }
                return null;
            }

            // A questo punto il processo è terminato, quindi le pipe sono chiuse e le letture
            // completano rapidamente: sicuro attendere in modo sincrono.
            var testo = letturaTesto.GetAwaiter().GetResult();
            var errore = letturaErrore.GetAwaiter().GetResult();

            if (processo.ExitCode != 0)
            {
                _log.LogWarning("pdftotext: uscito con codice {Codice}. {Errore}", processo.ExitCode, errore);
                return null;
            }

            _log.LogInformation(string.IsNullOrWhiteSpace(testo)
                ? "pdftotext: eseguito correttamente ma nessun testo estratto."
                : "pdftotext: testo estratto dove PdfPig aveva fallito.");

            return testo;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "pdftotext: esecuzione non riuscita.");
            return null;
        }
        finally
        {
            try { File.Delete(fileTemporaneo); } catch { /* pulizia best-effort, non bloccante */ }
        }
    }
}
