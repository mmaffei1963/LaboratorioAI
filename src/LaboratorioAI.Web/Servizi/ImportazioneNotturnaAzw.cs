using LaboratorioAI.Dati.Provvigioni;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Importa ogni notte i dati di AZW nelle tabelle di lavoro delle provvigioni.
/// Attiva solo se <c>Provvigioni:ImportazioneNotturna</c> è true; l'ora (locale del server) è
/// <c>Provvigioni:OraImportazione</c> (formato HH:mm, predefinita 03:30).
/// L'importazione è la stessa del pulsante "Importa da AZW": legge AZW solo in lettura e scrive solo su labai.
/// Un errore viene registrato nel log e nella tabella delle importazioni, e non ferma l'applicazione.
/// </summary>
public sealed class ImportazioneNotturnaAzw : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ImportazioneNotturnaAzw> _log;

    public ImportazioneNotturnaAzw(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<ImportazioneNotturnaAzw> log)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue<bool>("Provvigioni:ImportazioneNotturna"))
        {
            _log.LogInformation("Importazione notturna AZW non attiva (Provvigioni:ImportazioneNotturna).");
            return;
        }

        var ora = TimeSpan.TryParse(_config["Provvigioni:OraImportazione"], out var o) ? o : new TimeSpan(3, 30, 0);

        while (!stoppingToken.IsCancellationRequested)
        {
            var adesso = DateTime.Now;
            var prossima = adesso.Date + ora;
            if (prossima <= adesso) prossima = prossima.AddDays(1);

            try { await Task.Delay(prossima - adesso, stoppingToken); }
            catch (OperationCanceledException) { return; }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var importatore = scope.ServiceProvider.GetRequiredService<ImportatoreAzw>();
                var esito = await importatore.ImportaAsync(stoppingToken);
                if (esito.Riuscita)
                {
                    _log.LogInformation("Importazione notturna AZW riuscita: {Fatture} fatture, {Incassi} incassi, {Note} note di credito.", esito.Fatture, esito.Incassi, esito.NoteCredito);
                    var servizio = scope.ServiceProvider.GetRequiredService<ServizioProvvigioniLab>();
                    var nuove = await servizio.SincronizzaConvenzioniAsync(stoppingToken);
                    if (nuove > 0) _log.LogInformation("Convenzioni create dalle commesse continuative di ArcLab: {Nuove}.", nuove);
                }
                else
                    _log.LogWarning("Importazione notturna AZW non riuscita: {Messaggio}", esito.Messaggio);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _log.LogError(ex, "Importazione notturna AZW fallita.");
            }
        }
    }
}
