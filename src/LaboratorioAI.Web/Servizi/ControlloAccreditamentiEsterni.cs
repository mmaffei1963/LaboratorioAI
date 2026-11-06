using System.Text;
using LaboratorioAI.Core.Servizi;

namespace LaboratorioAI.Web.Servizi;

/// <summary>Esito di un'esecuzione (notturna o manuale) del controllo accreditamenti.</summary>
public record RiepilogoControlloAccreditamenti(
    int TotaleLaboratori, int NuoveRevisioni, int Errori, bool EmailInviata);

/// <summary>Stato di avanzamento dell'ultimo controllo avviato a mano dal pulsante "Esegui controllo ora".</summary>
public enum StatoEsecuzioneControlloManuale { MaiEseguito, InCorso, Completato, Errore }

/// <summary>Fotografia dello stato corrente del controllo manuale, per il pulsante che lo interroga.</summary>
public record StatoControlloManuale(
    StatoEsecuzioneControlloManuale Stato,
    RiepilogoControlloAccreditamenti? UltimoRiepilogo,
    DateTime? UltimaEsecuzioneUtc,
    string? UltimoErrore,
    DateTime? AvviatoAlleUtc);

/// <summary>
/// Job notturno: controlla se ACCREDIA ha pubblicato una nuova revisione dell'elenco prove
/// accreditate per uno dei laboratori esterni attivi monitorati. Le nuove revisioni trovate
/// vengono scaricate e salvate come "da confermare" (mai applicate automaticamente: la conferma
/// resta sempre un'azione umana, vedi <see cref="IServizioAccreditamentiEsterni.ConfermaRevisioneAsync"/>).
/// Invia un'email di riepilogo quando trova almeno una novità o un errore.
///
/// Registrato in DI sia come singleton sia come <see cref="IHostedService"/> (stessa istanza),
/// così <see cref="EseguiControlloManualeAsync"/> può essere richiamato anche a mano (pulsante
/// "Esegui controllo ora" nella pagina Laboratori Esterni) con la stessa logica del job notturno.
///
/// Il pulsante NON aspetta più il completamento sulla stessa richiesta HTTP: ACCREDIA può essere
/// lenta (osservato più volte), e con più laboratori da controllare in sequenza il tempo totale
/// può superare il timeout dell'edge Cloudflare davanti al sito (errore 524), anche se il server
/// dietro sta ancora lavorando o fallirebbe comunque sul singolo laboratorio lento. Il pulsante
/// avvia il controllo in background (<see cref="AvviaControlloManuale"/>) e la pagina interroga
/// <see cref="OttieniStatoControlloManuale"/> a intervalli finché non risulta concluso.
/// Un semaforo (<see cref="_semaforoEsecuzione"/>) impedisce che il job notturno e un avvio
/// manuale (o due avvii manuali) girino in contemporanea sulla stessa logica.
///
/// Ogni esecuzione (notturna o manuale) ha anche un tetto massimo di durata
/// (<see cref="DurataMassima"/>): se qualcosa si blocca davvero (una chiamata di rete che non
/// rispetta il proprio timeout, un lock sul database, l'estrazione del testo da un PDF
/// particolarmente malformato) l'esecuzione viene comunque interrotta invece di restare bloccata
/// per sempre — cosa che, senza questo tetto, terrebbe occupato il semaforo a tempo indeterminato
/// e impedirebbe qualunque controllo successivo (notturno o dal pulsante) finché l'applicazione
/// non viene riavviata. Osservato in pratica: un controllo lasciato "in corso" da una sera
/// all'altra senza mai concludersi.
///
/// Dal 29/09/2026 il controllo notturno diretto (chiamata a certificati.accredia.it dal server
/// stesso) resta presente ma può essere disattivato con "AccreditamentiEsterni:ControlloAutomaticoNotturnoAttivo"
/// = false: il server di produzione non riesce a raggiungere ACCREDIA (probabile blocco lato
/// ACCREDIA sull'IP del server, ospitato fuori dalla rete aziendale), quindi lasciarlo attivo
/// produrrebbe solo un errore ogni notte. Il canale effettivo diventa l'importazione da
/// <c>strumenti/AgenteAccredia</c> (vedi <c>AccrediaAgenteController</c>), che chiama
/// <see cref="ElaboraRisultatiAsync"/> con i risultati già raccolti. Il pulsante manuale "Esegui
/// controllo ora" resta sempre attivo (utile per verificare se ACCREDIA è tornato raggiungibile).
/// </summary>
public class ControlloAccreditamentiEsterni : BackgroundService
{
    /// <summary>Tetto massimo per una singola esecuzione (notturna o manuale) del controllo.</summary>
    private static readonly TimeSpan DurataMassima = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _servizi;
    private readonly IConfiguration _config;
    private readonly ILogger<ControlloAccreditamentiEsterni> _log;

    private readonly SemaphoreSlim _semaforoEsecuzione = new(1, 1);
    private readonly object _lockStatoManuale = new();
    private StatoEsecuzioneControlloManuale _statoManuale = StatoEsecuzioneControlloManuale.MaiEseguito;
    private RiepilogoControlloAccreditamenti? _ultimoRiepilogoManuale;
    private DateTime? _ultimaEsecuzioneManualeUtc;
    private string? _ultimoErroreManuale;
    private DateTime? _avvioManualeUtc;
    private bool _completamentoManualeDaNotificare;

    public ControlloAccreditamentiEsterni(
        IServiceProvider servizi, IConfiguration config, ILogger<ControlloAccreditamentiEsterni> log)
    {
        _servizi = servizi;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(RitardoFinoAllaProssimaEsecuzione(), ct);

                if (!ControlloAutomaticoNotturnoAttivo())
                {
                    _log.LogInformation(
                        "Controllo notturno diretto degli accreditamenti ACCREDIA disattivato " +
                        "(AccreditamentiEsterni:ControlloAutomaticoNotturnoAttivo = false) — salto.");
                    continue;
                }

                await _semaforoEsecuzione.WaitAsync(ct);
                try
                {
                    // Tetto massimo anche di notte: se non fosse mai completato entro
                    // DurataMassima, terrebbe bloccato il semaforo fino al prossimo riavvio,
                    // impedendo anche il pulsante manuale per tutto il giorno successivo.
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(DurataMassima);

                    _log.LogInformation("Avvio controllo notturno degli accreditamenti ACCREDIA.");
                    await EseguiControlloManualeAsync(timeoutCts.Token);
                }
                finally
                {
                    _semaforoEsecuzione.Release();
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Arresto dell'applicazione: non un errore da loggare.
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Controllo notturno degli accreditamenti ACCREDIA non riuscito (o interrotto per timeout dopo {Minuti} minuti).",
                    DurataMassima.TotalMinutes);
            }
        }
    }

    /// <summary>
    /// Di default il controllo notturno diretto è attivo; impostare
    /// "AccreditamentiEsterni:ControlloAutomaticoNotturnoAttivo" = false per disattivarlo (senza
    /// toccare il pulsante manuale) quando si sa già che ACCREDIA non è raggiungibile dal server.
    /// </summary>
    private bool ControlloAutomaticoNotturnoAttivo() =>
        _config.GetValue("AccreditamentiEsterni:ControlloAutomaticoNotturnoAttivo", true);

    /// <summary>
    /// Avvia in background un controllo manuale (pulsante "Esegui controllo ora") senza attendere
    /// il completamento: restituisce subito <c>true</c> se l'ha avviato, <c>false</c> se un
    /// controllo (notturno o manuale) era già in corso — in quel caso il chiamante può comunque
    /// interrogare <see cref="OttieniStatoControlloManuale"/> per seguirne l'esito.
    /// </summary>
    public bool AvviaControlloManuale()
    {
        if (!_semaforoEsecuzione.Wait(0))
            return false;

        lock (_lockStatoManuale)
        {
            _statoManuale = StatoEsecuzioneControlloManuale.InCorso;
            _avvioManualeUtc = DateTime.UtcNow;
        }

        _log.LogInformation("Avvio controllo manuale degli accreditamenti ACCREDIA (pulsante \"Esegui controllo ora\").");

        _ = Task.Run(async () =>
        {
            // Tetto massimo: se qualcosa si blocca (rete, database, estrazione testo di un PDF
            // malformato) l'esecuzione viene comunque interrotta, così il semaforo si libera
            // sempre entro DurataMassima invece di restare occupato a tempo indeterminato.
            using var timeoutCts = new CancellationTokenSource(DurataMassima);

            try
            {
                var riepilogo = await EseguiControlloManualeAsync(timeoutCts.Token);
                lock (_lockStatoManuale)
                {
                    _statoManuale = StatoEsecuzioneControlloManuale.Completato;
                    _ultimoRiepilogoManuale = riepilogo;
                    _ultimoErroreManuale = null;
                    _ultimaEsecuzioneManualeUtc = DateTime.UtcNow;
                    _completamentoManualeDaNotificare = true;
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                var messaggio =
                    $"Timeout complessivo ({DurataMassima.TotalMinutes:0} minuti) superato: il controllo è stato " +
                    "interrotto perché qualcosa si è bloccato (rete verso ACCREDIA, database o estrazione di un " +
                    "PDF). Controllare i log applicativi per capire su quale laboratorio si è fermato.";
                _log.LogError(messaggio);
                lock (_lockStatoManuale)
                {
                    _statoManuale = StatoEsecuzioneControlloManuale.Errore;
                    _ultimoErroreManuale = messaggio;
                    _ultimaEsecuzioneManualeUtc = DateTime.UtcNow;
                    _completamentoManualeDaNotificare = true;
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Controllo manuale (pulsante \"Esegui controllo ora\") non riuscito.");
                lock (_lockStatoManuale)
                {
                    _statoManuale = StatoEsecuzioneControlloManuale.Errore;
                    _ultimoErroreManuale = ex.Message;
                    _ultimaEsecuzioneManualeUtc = DateTime.UtcNow;
                    _completamentoManualeDaNotificare = true;
                }
            }
            finally
            {
                _semaforoEsecuzione.Release();
            }
        });

        return true;
    }

    /// <summary>Stato corrente (e ultimo esito) del controllo manuale, per il polling del pulsante.</summary>
    public StatoControlloManuale OttieniStatoControlloManuale()
    {
        lock (_lockStatoManuale)
        {
            return new StatoControlloManuale(
                _statoManuale, _ultimoRiepilogoManuale, _ultimaEsecuzioneManualeUtc, _ultimoErroreManuale,
                _statoManuale == StatoEsecuzioneControlloManuale.InCorso ? _avvioManualeUtc : null);
        }
    }

    /// <summary>
    /// Se l'ultimo controllo manuale si è appena concluso (successo o errore) e nessuno lo ha
    /// ancora notificato, lo marca come notificato e restituisce <c>true</c>: il chiamante (sempre
    /// l'endpoint di stato, dentro una richiesta autenticata) è così responsabile di registrare
    /// l'esito nell'audit trail con l'utente della richiesta corrente. Evita doppie registrazioni
    /// se più schede del browser interrogano lo stato in parallelo.
    /// </summary>
    public bool ProvaConsumaCompletamentoManualeDaNotificare()
    {
        lock (_lockStatoManuale)
        {
            if (!_completamentoManualeDaNotificare)
                return false;

            _completamentoManualeDaNotificare = false;
            return true;
        }
    }

    /// <summary>
    /// Esegue subito un controllo completo (stessa logica del job notturno) e restituisce un
    /// riepilogo. Usato sia dal ciclo notturno sia dall'avvio manuale — il chiamante deve già
    /// possedere <see cref="_semaforoEsecuzione"/> quando serve evitare sovrapposizioni.
    /// </summary>
    public async Task<RiepilogoControlloAccreditamenti> EseguiControlloManualeAsync(CancellationToken ct)
    {
        using var ambito = _servizi.CreateScope();
        var servizio = ambito.ServiceProvider.GetRequiredService<IServizioAccreditamentiEsterni>();

        var risultati = (await servizio.ControllaTuttiAsync(ct)).ToList();
        return await ElaboraRisultatiAsync(risultati, ct);
    }

    /// <summary>
    /// Riepiloga (conteggi, log, email) un elenco di risultati già ottenuto altrove — usato sia da
    /// <see cref="EseguiControlloManualeAsync"/> (che li ottiene chiamando ACCREDIA direttamente)
    /// sia da <c>AccrediaAgenteController</c> (che li ottiene dai dati importati dall'agente
    /// esterno): stessa logica di riepilogo ed email in entrambi i casi, nessuna duplicazione.
    /// </summary>
    public async Task<RiepilogoControlloAccreditamenti> ElaboraRisultatiAsync(
        IReadOnlyList<RisultatoControlloAccreditamento> risultati, CancellationToken ct)
    {
        var novita = risultati.Where(r => r.Esito == EsitoControlloAccreditamento.NuovaRevisioneTrovata).ToList();
        var errori = risultati.Where(r => r.Esito == EsitoControlloAccreditamento.Errore).ToList();

        _log.LogInformation(
            "Controllo accreditamenti ACCREDIA completato: {Totale} laboratori, {Novita} nuove revisioni, {Errori} errori.",
            risultati.Count, novita.Count, errori.Count);

        var emailInviata = false;
        if (novita.Count > 0 || errori.Count > 0)
        {
            // L'invio dell'email è solo una notifica: se fallisce (es. SMTP non ancora
            // configurato in produzione) non deve far fallire l'intera richiesta — i risultati
            // (nuove revisioni salvate, PDF scaricati) sono già stati persistiti da
            // ControllaTuttiAsync prima di arrivare qui, e non vanno persi solo perché la mail
            // di cortesia non parte. Osservato in pratica il 29/09/2026 (primo test dell'agente
            // push): l'eccezione SMTP faceva rispondere 500 al chiamante (agente o pulsante
            // manuale) nonostante il controllo fosse di fatto riuscito.
            try
            {
                emailInviata = await InviaEmailRiepilogoAsync(novita, errori, ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Invio dell'email di riepilogo accreditamenti non riuscito (il controllo stesso è comunque andato a buon fine).");
            }
        }

        return new RiepilogoControlloAccreditamenti(risultati.Count, novita.Count, errori.Count, emailInviata);
    }

    private async Task<bool> InviaEmailRiepilogoAsync(
        List<RisultatoControlloAccreditamento> novita, List<RisultatoControlloAccreditamento> errori,
        CancellationToken ct)
    {
        var destinatari = _config.GetSection("AccreditamentiEsterni:EmailDestinatari").Get<string[]>();
        if (destinatari is null || destinatari.Length == 0)
        {
            _log.LogWarning("Nessun destinatario configurato per l'email di riepilogo accreditamenti (AccreditamentiEsterni:EmailDestinatari).");
            return false;
        }

        var corpo = new StringBuilder();
        corpo.Append("<p>Controllo degli accreditamenti ACCREDIA dei laboratori esterni.</p>");

        if (novita.Count > 0)
        {
            corpo.Append("<p><b>Nuove revisioni trovate (da confermare in Laboratorio AI):</b></p><ul>");
            foreach (var r in novita)
                corpo.Append($"<li>{System.Net.WebUtility.HtmlEncode(r.NomeLaboratorio)} — revisione n. {r.NumeroRevisioneTrovata}</li>");
            corpo.Append("</ul>");
        }

        if (errori.Count > 0)
        {
            corpo.Append("<p><b>Errori durante il controllo:</b></p><ul>");
            foreach (var r in errori)
                corpo.Append($"<li>{System.Net.WebUtility.HtmlEncode(r.NomeLaboratorio)} — {System.Net.WebUtility.HtmlEncode(r.MessaggioErrore ?? "errore sconosciuto")}</li>");
            corpo.Append("</ul>");
        }

        using var ambito = _servizi.CreateScope();
        var email = ambito.ServiceProvider.GetRequiredService<IServizioEmail>();
        await email.InviaAsync(destinatari, "Laboratorio AI - Accreditamenti ACCREDIA: aggiornamenti", corpo.ToString(), ct);
        return true;
    }

    /// <summary>
    /// E-mail sui cambi di stato ACCREDIA dei laboratori monitorati e sulle prove sospese comparse o terminate
    /// (08/10/2026). Non segnala il primo stato «accreditato» (prima lettura). Restituisce true se l'e-mail è partita.
    /// </summary>
    public async Task<bool> InviaEmailVariazioniStatoAsync(
        LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.EsitoStatoAccredia esito, CancellationToken ct)
    {
        var daSegnalare = esito.Variazioni
            .Where(v => !(v.Prima is null && v.Ora == LaboratorioAI.Core.Enumerazioni.StatoAccreditamentoAccredia.Accreditato))
            .ToList();
        if (daSegnalare.Count == 0 && esito.VariazioniProve.Count == 0) return false;
        try
        {
            var destinatari = _config.GetSection("AccreditamentiEsterni:EmailDestinatari").Get<string[]>();
            if (destinatari is null || destinatari.Length == 0) return false;
            static string H(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
            var corpo = new StringBuilder();
            if (daSegnalare.Count > 0)
            {
                corpo.Append("<p>Cambio di stato dell'accreditamento ACCREDIA di laboratori in subappalto (banca dati dei laboratori di prova):</p><ul>");
                foreach (var v in daSegnalare)
                    corpo.Append($"<li><b>{H(v.Laboratorio)}</b> ({v.Numero}/{v.Sede}): " +
                        $"{H(LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioStatoAccreditamenti.Etichetta(v.Prima))} → " +
                        $"<b>{H(LaboratorioAI.Dati.LaboratorioAI.Accreditamenti.ServizioStatoAccreditamenti.Etichetta(v.Ora))}</b></li>");
                corpo.Append("</ul>");
            }
            foreach (var (titolo, nuove) in new[] { ("Prove con sospensione attiva (nuove)", true), ("Prove non più sospese", false) })
            {
                var righe = esito.VariazioniProve.Where(v => v.Nuova == nuove).ToList();
                if (righe.Count == 0) continue;
                corpo.Append($"<p><b>{titolo}</b>:</p><ul>");
                foreach (var v in righe)
                    corpo.Append($"<li><b>{H(v.Laboratorio)}</b> ({v.Numero}/{v.Sede}): {H(v.Prova)} — {H(v.Matrice)} — {H(v.Norma)}</li>");
                corpo.Append("</ul>");
            }
            corpo.Append("<p>La sospensione riguarda solo le prove elencate (asterisco nell'elenco prove ACCREDIA), non tutto l'elenco. " +
                "In Laboratorio AI → Laboratori Esterni, riquadro «Accreditamenti da sistemare», si vede se fra i metodi che affidiamo " +
                "al laboratorio ce n'è qualcuno con la stessa norma.</p>");
            using var ambito = _servizi.CreateScope();
            var email = ambito.ServiceProvider.GetRequiredService<IServizioEmail>();
            await email.InviaAsync(destinatari, "Laboratorio AI - Accreditamenti ACCREDIA: CAMBIO DI STATO", corpo.ToString(), ct);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invio dell'email sul cambio di stato degli accreditamenti non riuscito.");
            return false;
        }
    }

    /// <summary>Calcola l'attesa fino alle 03:00 del prossimo giorno utile (oggi se non ancora passate).</summary>
    private static TimeSpan RitardoFinoAllaProssimaEsecuzione()
    {
        var adesso = DateTime.Now;
        var prossima = adesso.Date.AddHours(3);
        if (prossima <= adesso)
            prossima = prossima.AddDays(1);

        return prossima - adesso;
    }
}
