using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI.DatiIniziali;

/// <summary>
/// Popolamento iniziale di ruoli e permessi.
///
/// Eseguito all'avvio dell'applicazione: è idempotente, aggiunge solo
/// ciò che manca. Questo permette di introdurre nuovi permessi con una
/// release senza interventi manuali sul database.
/// </summary>
public static class Popolamento
{
    /// <summary>
    /// Allinea permessi e ruoli. Al primo avvio con i ruoli per mestiere (11/10/2026) applica anche i profili e le
    /// assegnazioni iniziali di <see cref="ProfiliRuoli"/>; <paramref name="amministratoriAggiuntivi"/> sono e-mail che
    /// restano Amministratori in quel passaggio (in sviluppo: l'utente di sviluppo). Restituisce un riepilogo da
    /// registrare nel log quando quel passaggio è stato eseguito, altrimenti null.
    /// </summary>
    public static async Task<string?> EseguiAsync(ContestoLaboratorioAI ctx, IEnumerable<string>? amministratoriAggiuntivi = null,
        CancellationToken ct = default)
    {
        var primaVolta = !await ctx.Ruoli.AnyAsync(r => r.Codice == Ruoli.Tecnico, ct);
        var nuovaDirezione = !await ctx.Ruoli.AnyAsync(r => r.Codice == Ruoli.Direzione, ct);
        var nuovaContabile = !await ctx.Ruoli.AnyAsync(r => r.Codice == Ruoli.AmministrazioneContabile, ct);
        var nuovoConsuntivo = !await ctx.Permessi.AnyAsync(p => p.Codice == Permessi.ConsuntiviConsulta, ct);
        var nuoviCompensiSoci = !await ctx.Permessi.AnyAsync(p => p.Codice == Permessi.CompensiSociConsulta, ct);
        var nuovaRicercaFatture = !await ctx.Permessi.AnyAsync(p => p.Codice == Permessi.RicercaFattureConsulta, ct);
        await PopolaPermessiAsync(ctx, ct);
        await PopolaRuoliAsync(ctx, primaVolta, ct);
        await AssegnaPermessiAmministratoreAsync(ctx, ct);
        await ctx.SaveChangesAsync(ct);
        string? riepilogo = null;
        if (nuovoConsuntivo && !primaVolta)
        {
            // 20/10/2026: il permesso del consuntivo per area va al ruolo Socio una volta sola, quando nasce;
            // poi si gestisce da «Utenti e ruoli». Gli Amministratori lo ricevono come tutti i permessi.
            var socio = await ctx.Ruoli.FirstOrDefaultAsync(r => r.Codice == Ruoli.Socio, ct);
            var permesso = await ctx.Permessi.FirstOrDefaultAsync(p => p.Codice == Permessi.ConsuntiviConsulta, ct);
            if (socio is not null && permesso is not null
                && !await ctx.RuoliPermessi.AnyAsync(rp => rp.IdRuolo == socio.IdRuolo && rp.IdPermesso == permesso.IdPermesso, ct))
            {
                ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = socio.IdRuolo, IdPermesso = permesso.IdPermesso });
                await ctx.SaveChangesAsync(ct);
            }
        }
        if (nuoviCompensiSoci && !primaVolta)
        {
            // 21/10/2026: la consultazione dei compensi soci va al ruolo Socio una volta sola, quando nasce il permesso.
            // La gestione la ricevono solo gli Amministratori (come tutti i permessi); in più la pagina controlla che
            // l'utente sia un socio attivo e, per modificare, il socio «gestore».
            var socio = await ctx.Ruoli.FirstOrDefaultAsync(r => r.Codice == Ruoli.Socio, ct);
            var permesso = await ctx.Permessi.FirstOrDefaultAsync(p => p.Codice == Permessi.CompensiSociConsulta, ct);
            if (socio is not null && permesso is not null
                && !await ctx.RuoliPermessi.AnyAsync(rp => rp.IdRuolo == socio.IdRuolo && rp.IdPermesso == permesso.IdPermesso, ct))
            {
                ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = socio.IdRuolo, IdPermesso = permesso.IdPermesso });
                await ctx.SaveChangesAsync(ct);
            }
        }
        if (nuovaRicercaFatture && !primaVolta)
        {
            // 10/11/2026: la ricerca fatture va una volta sola, quando nasce il permesso, ai ruoli Socio e Amministrazione
            // contabile (Migani, Volponi); poi si gestisce da «Utenti e ruoli». Gli Amministratori la ricevono come tutti i permessi.
            var permesso = await ctx.Permessi.FirstOrDefaultAsync(p => p.Codice == Permessi.RicercaFattureConsulta, ct);
            foreach (var codice in new[] { Ruoli.Socio, Ruoli.AmministrazioneContabile })
            {
                var ruolo = await ctx.Ruoli.FirstOrDefaultAsync(r => r.Codice == codice, ct);
                if (ruolo is not null && permesso is not null
                    && !await ctx.RuoliPermessi.AnyAsync(rp => rp.IdRuolo == ruolo.IdRuolo && rp.IdPermesso == permesso.IdPermesso, ct))
                    ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = ruolo.IdRuolo, IdPermesso = permesso.IdPermesso });
            }
            await ctx.SaveChangesAsync(ct);
        }
        if (primaVolta)
        {
            riepilogo = await ApplicaProfiliInizialiAsync(ctx, amministratoriAggiuntivi ?? Array.Empty<string>(), ct);
            await ctx.SaveChangesAsync(ct);
        }
        if (nuovaContabile && !primaVolta)
        {
            // 21/10/2026: come per la Direzione, l'assegnazione iniziale non deve mai impedire l'avvio del sito.
            string d;
            try
            {
                d = await ApplicaRuoloAsync(ctx, Ruoli.AmministrazioneContabile, ProfiliRuoli.PermessiAmministrazioneContabile,
                    ProfiliRuoli.PersoneAmministrazioneContabile, ct);
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.ChangeTracker.Clear();
                d = "ATTENZIONE: ruolo Amministrazione contabile creato ma non assegnato (assegnarlo da «Utenti e ruoli»). Motivo: "
                    + (ex.InnerException?.Message ?? ex.Message);
            }
            riepilogo = riepilogo is null ? d : riepilogo + " " + d;
        }
        if (nuovaDirezione)
        {
            // 13/10/2026: l'assegnazione iniziale del ruolo non deve mai impedire l'avvio del sito.
            // Se non riesce, il ruolo resta creato senza persone (si assegnano da «Utenti e ruoli») e il motivo va nel log.
            string d;
            try
            {
                d = await ApplicaRuoloDirezioneAsync(ctx, ct);
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ctx.ChangeTracker.Clear();
                d = "ATTENZIONE: ruolo Direzione creato ma non assegnato (assegnarlo da «Utenti e ruoli»). Motivo: "
                    + (ex.InnerException?.Message ?? ex.Message);
            }
            riepilogo = riepilogo is null ? d : riepilogo + " " + d;
        }
        return riepilogo;
    }

    // ── Permessi ───────────────────────────────────────────────────
    private static async Task PopolaPermessiAsync(ContestoLaboratorioAI ctx, CancellationToken ct)
    {
        var esistenti = await ctx.Permessi.Select(p => p.Codice).ToListAsync(ct);

        foreach (var (codice, descrizione, area) in Catalogo())
        {
            if (esistenti.Contains(codice)) continue;

            ctx.Permessi.Add(new Permesso
            {
                Codice = codice,
                Descrizione = descrizione,
                Area = area
            });
        }
    }

    private static IEnumerable<(string Codice, string Descrizione, AreaFunzionale Area)> Catalogo() =>
    [
        (Permessi.ValidazioneVisualizza,    "Visualizzare i campioni da validare",              AreaFunzionale.Validazione),
        (Permessi.ValidazioneAnalizza,      "Eseguire l'analisi AI su un campione",             AreaFunzionale.Validazione),
        (Permessi.ValidazioneAnalizzaLotto, "Eseguire l'analisi AI massiva",                    AreaFunzionale.Validazione),
        (Permessi.ValidazioneConferma,      "Validare il campione: giudizio e chiusura analisi da riportare in ArcLab", AreaFunzionale.Validazione),

        (Permessi.ConsultazioneCampioni,    "Ricercare campioni nell'archivio",                 AreaFunzionale.Consultazione),
        (Permessi.ConsultazioneCertificati, "Ricercare certificati emessi",                     AreaFunzionale.Consultazione),
        (Permessi.ConsultazioneClienti,     "Consultare i campioni per cliente",                AreaFunzionale.Consultazione),
        (Permessi.ConsultazioneRisultati,   "Visualizzare i risultati analitici di dettaglio",  AreaFunzionale.Consultazione),

        (Permessi.MetodiConsulta,           "Consultare metodi e controlli",                    AreaFunzionale.Metodi),
        (Permessi.MetodiPrezzi,             "Visualizzare i prezzi dei controlli",              AreaFunzionale.Metodi),

        (Permessi.CommesseConsulta,         "Consultare le commesse",                           AreaFunzionale.Commesse),
        (Permessi.CommessePrezzi,           "Visualizzare gli importi di commessa",             AreaFunzionale.Commesse),

        (Permessi.LaboratoriEsterniConsulta,"Consultare le analisi in outsourcing",             AreaFunzionale.LaboratoriEsterni),
        (Permessi.LaboratoriEsterniEsporta, "Esportare i dati dei laboratori esterni",          AreaFunzionale.LaboratoriEsterni),
        (Permessi.LaboratoriEsterniAccreditamento, "Confermare le revisioni ACCREDIA rilevate dal controllo notturno", AreaFunzionale.LaboratoriEsterni),
        (Permessi.LaboratoriEsterniInvio, "Inviare ai laboratori esterni l'elenco dei campioni spediti (Excel per e-mail) e gestirne gli indirizzi", AreaFunzionale.LaboratoriEsterni),

        (Permessi.DvrGenera,                "Generare un DVR Legionellosi",                     AreaFunzionale.Dvr),
        (Permessi.DvrLotto,                 "Generare DVR in modalità massiva",                 AreaFunzionale.Dvr),
        (Permessi.DvrRegistro,              "Consultare il registro dei DVR prodotti",          AreaFunzionale.Dvr),

        (Permessi.PiscineGenera,            "Generare un DVR Piscine",                          AreaFunzionale.Piscine),
        (Permessi.PiscineRegistro,          "Consultare il registro dei DVR piscine",           AreaFunzionale.Piscine),

        (Permessi.PrefatturatoTamponi,      "Consultare i tamponi in contratto (prefatturato)", AreaFunzionale.Prefatturato),
        (Permessi.PrefatturatoNonFatturare, "Consultare i campioni non-fatturare (prefatturato)", AreaFunzionale.Prefatturato),

        (Permessi.NutrizionaleMateriePrime, "Gestire l'archivio delle materie prime nutrizionali", AreaFunzionale.Nutrizionale),
        (Permessi.NutrizionaleRicette,      "Gestire ricette e calcolo nutrizionale",             AreaFunzionale.Nutrizionale),

        (Permessi.TaraturaEsegui,     "Eseguire tarature dei termometri",              AreaFunzionale.Taratura),
        (Permessi.TaraturaRegistro,   "Consultare il registro delle tarature",         AreaFunzionale.Taratura),
        (Permessi.TaraturaValidazione,"Validare il motore di calcolo della taratura",  AreaFunzionale.Taratura),
        (Permessi.IncubatoriEsegui,     "Eseguire tarature di incubatori/celle",        AreaFunzionale.Incubatori),
        (Permessi.IncubatoriValidazione,"Validare il calcolo delle tarature incubatori",AreaFunzionale.Incubatori),
        (Permessi.MappaturaEsegui,      "Eseguire mappature di apparecchiature",        AreaFunzionale.Incubatori),
        (Permessi.MappaturaValidazione, "Validare il calcolo delle mappature",          AreaFunzionale.Incubatori),
        (Permessi.BilanceEsegui,        "Eseguire tarature di bilance",                 AreaFunzionale.Bilance),
        (Permessi.BilanceValidazione,   "Validare il calcolo delle tarature bilance",   AreaFunzionale.Bilance),
        (Permessi.TempLogEsegui,        "Caricare e monitorare i file TempLog Econorma", AreaFunzionale.TempLog),
        (Permessi.TempLogMappatura,     "Gestire la mappatura canali TempLog",           AreaFunzionale.TempLog),
        (Permessi.TempLogValidazione,   "Validare i file TempLog (conforme/dubbio/non conforme)", AreaFunzionale.TempLog),
        (Permessi.ScadenziarioVisualizza, "Consultare lo scadenziario delle tarature",   AreaFunzionale.Scadenziario),
        (Permessi.ScadenziarioGestione,   "Gestire termometri esterni e generare etichette", AreaFunzionale.Scadenziario),
        (Permessi.AccettazioneEsegui,   "Accettare campioni fuori sede (bozze)",         AreaFunzionale.Accettazione),
        (Permessi.AccettazioneTrasmetti, "Generare il tracciato e trasmettere l'accettazione", AreaFunzionale.Accettazione),
        (Permessi.OrdiniAccettazioneGestione, "Generare gli Ordini di Accettazione dai lotti ArcLab", AreaFunzionale.Accettazione),

        (Permessi.SoloEsterniVisualizza, "Confrontare i certificati esterni con gli interni", AreaFunzionale.SoloEsterni),

        (Permessi.CertificatiEsterniGestione, "Rinominare e smistare i certificati dei laboratori esterni", AreaFunzionale.CertificatiEsterni),

        (Permessi.ControlliProponi, "Proporre nuovi controlli (bozze)",                    AreaFunzionale.Controlli),
        (Permessi.ControlliApprova, "Approvare e generare il tracciato dei controlli",     AreaFunzionale.Controlli),

        (Permessi.PreventiviGestione, "Comporre e gestire i preventivi/offerte",           AreaFunzionale.Preventivi),
        (Permessi.PreventiviApprova,  "Approvare i preventivi che applicano uno sconto",   AreaFunzionale.Preventivi),
        (Permessi.RichiestePrivatiGestione, "Richieste di analisi dei clienti privati: compilare e stampare il modulo da firmare (Mod. Com. 05B)", AreaFunzionale.Preventivi),
        (Permessi.PreventiviConsulenza, "Preventivi di consulenza e formazione: comporre, gestire attività e pacchetti", AreaFunzionale.Consulenze),

        (Permessi.RicercaErroriVisualizza, "Eseguire le verifiche diagnostiche (ricerca errori)", AreaFunzionale.RicercaErrori),

        (Permessi.MonitorTemperatureVisualizza, "Consultare il monitoraggio temperature (Econorma)", AreaFunzionale.MonitorTemperature),
        (Permessi.MonitorTemperatureLimiti, "Allineare i limiti delle apparecchiature del monitoraggio a quelli di ArcLab", AreaFunzionale.MonitorTemperature),

        (Permessi.StatisticheVisualizza, "Ricerca trasversale statistiche (per singola prova)", AreaFunzionale.Statistiche),
        (Permessi.ProvvigioniGestisci,   "Provvigioni commerciali: importazione, calcolo, elenco sviluppo", AreaFunzionale.Provvigioni),
        (Permessi.ProvvigioniProprie,    "Provvigioni: il commerciale vede solo i propri dati",            AreaFunzionale.Provvigioni),
        (Permessi.ProvvigioniChiudi,     "Provvigioni: chiusura del trimestre",                            AreaFunzionale.Provvigioni),
        (Permessi.ProvvigioniConsulta,   "Provvigioni e accordi: consultazione in sola lettura (soci)",    AreaFunzionale.Provvigioni),
        (Permessi.SpettanzeProprie,      "Spettanze: il consulente a percentuale vede solo le proprie",   AreaFunzionale.Provvigioni),

        (Permessi.RapportiniCompila,  "Rapportini di consulenza: compilare e inviare i propri",                    AreaFunzionale.Consulenze),
        (Permessi.RapportiniGestione, "Rapportini di consulenza: gestire tutti (correzioni, conferma, annullamento)", AreaFunzionale.Consulenze),

        (Permessi.ClientiSeguire,  "Clienti da seguire: andamento, registro azioni, proposta di assegnazione", AreaFunzionale.Clienti),
        (Permessi.ClientiGestisci, "Clienti da seguire: soglie e approvazione delle assegnazioni",             AreaFunzionale.Clienti),
        (Permessi.ClientiSubentri, "Clienti da seguire: vedere e confermare i cambi di gestione (subentri)",   AreaFunzionale.Clienti),

        (Permessi.DirezioneConsulta, "Direzione: indicatori economici, rendimento commerciali e territorio (riservato)", AreaFunzionale.Direzione),
        (Permessi.DirezioneGestisci, "Direzione: modificare la classificazione delle voci AZW",                       AreaFunzionale.Direzione),
        (Permessi.ConsuntiviConsulta, "Consuntivo costi e ricavi per area, con gli stipendi (riservato ai soci)",     AreaFunzionale.Direzione),
        (Permessi.RicercaFattureConsulta, "Ricerca delle fatture di vendita per testo della fattura (AZW)",            AreaFunzionale.Amministrazione),
        (Permessi.CompensiSociConsulta, "Compensi soci e rimborsi spese (riservato ai soci: serve anche essere un socio attivo)", AreaFunzionale.Direzione),
        (Permessi.CompensiSociGestisci, "Compensi soci: parametri, dati dei soci, premi area laboratorio, chiusura dell'anno (solo il socio gestore)", AreaFunzionale.Direzione),
        (Permessi.LimitiConsulta, "Limiti normativi: consultare schede limite e fonti", AreaFunzionale.LimitiNormativi),
        (Permessi.LimitiValida,   "Limiti normativi: fonti, estrazione AI, proposta, validazione e correzione delle schede", AreaFunzionale.LimitiNormativi),

        (Permessi.CondizioniGestione,  "Condizioni Generali: revisioni e clausole da approvare (pubblicazione)", AreaFunzionale.Contratti),
        (Permessi.CondizioniFirmatari, "Condizioni Generali: firmatari dei clienti e richieste di approvazione", AreaFunzionale.Contratti),

        (Permessi.AmministrazioneUtenti,         "Gestire utenti e ruoli",                      AreaFunzionale.Amministrazione),
        (Permessi.AmministrazioneConfigurazioni, "Modificare le configurazioni applicative",    AreaFunzionale.Amministrazione),
        (Permessi.AmministrazioneAudit,          "Consultare l'audit trail",                    AreaFunzionale.Amministrazione),
        (Permessi.GuideDomande,                  "Vedere le domande fatte a «Chiedi alla guida»", AreaFunzionale.Amministrazione),

        (Permessi.ManualisticaConsulta,        "Manualistica: vedere compilazioni e documenti prodotti",        AreaFunzionale.Manualistica),
        (Permessi.ManualisticaGenera,          "Manualistica: compilare la check list, generare e inviare",     AreaFunzionale.Manualistica),
        (Permessi.ManualisticaModelliGestisci, "Manualistica: caricare le revisioni dei modelli",               AreaFunzionale.Manualistica),
        (Permessi.ManualisticaModelliApprova,  "Manualistica: approvare le revisioni dei modelli",              AreaFunzionale.Manualistica),
        (Permessi.ManualisticaImpostazioni,    "Manualistica: pannello di controllo e voci delle tendine",      AreaFunzionale.Manualistica),
        (Permessi.ManualisticaApprovatori,     "Manualistica: indicare chi approva i modelli di ogni tipo",     AreaFunzionale.Manualistica),
    ];

    // ── Ruoli ──────────────────────────────────────────────────────
    private static async Task PopolaRuoliAsync(ContestoLaboratorioAI ctx, bool primaVolta, CancellationToken ct)
    {
        var esistenti = await ctx.Ruoli.ToDictionaryAsync(r => r.Codice, ct);

        foreach (var (codice, nome, descrizione, ordine) in ProfiliRuoli.Ruoli)
        {
            if (esistenti.TryGetValue(codice, out var r))
            {
                // Al passaggio ai ruoli per mestiere si aggiornano nome, descrizione e ordine dei ruoli già presenti
                // (la migrazione li ha creati con Attivo = false); dopo, li gestisce la pagina Utenti e ruoli.
                if (!primaVolta) continue;
                r.Nome = nome; r.Descrizione = descrizione; r.Ordine = ordine;
                r.Attivo = codice != Ruoli.Operatore;
                continue;
            }

            ctx.Ruoli.Add(new Ruolo
            {
                Codice = codice,
                Nome = nome,
                Descrizione = descrizione,
                Ordine = ordine,
                Attivo = codice != Ruoli.Operatore,
                DiSistema = true
            });
        }
    }

    // ── Profili e assegnazioni iniziali (una sola volta, 11/10/2026) ──
    private static async Task<string> ApplicaProfiliInizialiAsync(ContestoLaboratorioAI ctx, IEnumerable<string> amministratoriAggiuntivi,
        CancellationToken ct)
    {
        var ruoli = await ctx.Ruoli.ToDictionaryAsync(r => r.Codice, ct);
        var permessi = await ctx.Permessi.ToDictionaryAsync(p => p.Codice, p => p.IdPermesso, ct);

        // 1) Permessi dei ruoli (Amministratore escluso: li ha tutti).
        foreach (var (codice, elenco) in ProfiliRuoli.Permessi)
        {
            var ruolo = ruoli[codice];
            var attuali = await ctx.RuoliPermessi.Where(x => x.IdRuolo == ruolo.IdRuolo).ToListAsync(ct);
            ctx.RuoliPermessi.RemoveRange(attuali);
            foreach (var p in elenco.Distinct())
                ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = ruolo.IdRuolo, IdPermesso = permessi[p] });
        }

        // 2) Persone: chi è in elenco riceve i suoi ruoli (registrato in anticipo se non è ancora entrato); tutti gli
        //    altri, che finora erano Amministratori, passano a Consultazione.
        var ora = DateTime.UtcNow;
        var utenti = await ctx.Utenti.Include(u => u.Ruoli).ToListAsync(ct);
        var perEmail = utenti.GroupBy(u => u.Email.Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
        var amministratori = amministratoriAggiuntivi.Select(e => e.Trim().ToLowerInvariant()).ToHashSet();
        int assegnati = 0, preregistrati = 0, base_ = 0;

        void ImpostaRuoli(Utente u, IEnumerable<string> codici)
        {
            var voluti = codici.Select(c => ruoli[c].IdRuolo).ToHashSet();
            foreach (var ur in u.Ruoli.Where(x => !voluti.Contains(x.IdRuolo)).ToList()) { u.Ruoli.Remove(ur); ctx.UtentiRuoli.Remove(ur); }
            foreach (var id in voluti.Where(id => u.Ruoli.All(x => x.IdRuolo != id)))
                u.Ruoli.Add(new UtenteRuolo { Utente = u, IdRuolo = id, DataAssegnazione = ora });
        }

        var inElenco = new HashSet<string>();
        foreach (var (email, nome, codici, note) in ProfiliRuoli.Persone)
        {
            var chiave = email.ToLowerInvariant();
            inElenco.Add(chiave);
            if (!perEmail.TryGetValue(chiave, out var u))
            {
                u = new Utente { Email = email, NomeCompleto = nome, Attivo = true, DataCreazione = ora, Note = note };
                ctx.Utenti.Add(u);
                perEmail[chiave] = u;
                preregistrati++;
            }
            else u.Note ??= note;
            ImpostaRuoli(u, codici);
            assegnati++;
        }

        foreach (var u in utenti.Where(u => !inElenco.Contains(u.Email.Trim().ToLowerInvariant())))
        {
            if (amministratori.Contains(u.Email.Trim().ToLowerInvariant())) { ImpostaRuoli(u, [Ruoli.Amministratore]); continue; }
            ImpostaRuoli(u, [Ruoli.Consultazione]);
            base_++;
        }

        return $"Ruoli per mestiere applicati: {assegnati} persone dall'elenco iniziale ({preregistrati} registrate in anticipo), " +
               $"{base_} altri utenti portati a Consultazione.";
    }

    // ── Ruolo nuovo con permessi e persone (21/10/2026, una sola volta alla creazione del ruolo) ──
    private static async Task<string> ApplicaRuoloAsync(ContestoLaboratorioAI ctx, string codiceRuolo, string[] codiciPermessi,
        (string Email, string Nome)[] persone, CancellationToken ct)
    {
        var ruolo = await ctx.Ruoli.FirstAsync(r => r.Codice == codiceRuolo, ct);
        var permessi = await ctx.Permessi.Where(p => codiciPermessi.Contains(p.Codice)).ToListAsync(ct);
        var gia = await ctx.RuoliPermessi.Where(rp => rp.IdRuolo == ruolo.IdRuolo).Select(rp => rp.IdPermesso).ToListAsync(ct);
        foreach (var p in permessi.Where(p => !gia.Contains(p.IdPermesso)))
            ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = ruolo.IdRuolo, IdPermesso = p.IdPermesso });

        var ora = DateTime.UtcNow;
        var utenti = await ctx.Utenti.Include(u => u.Ruoli).ToListAsync(ct);
        int assegnati = 0;
        foreach (var (email, nome) in persone)
        {
            var u = utenti.FirstOrDefault(x => string.Equals(x.Email.Trim(), email, StringComparison.OrdinalIgnoreCase));
            if (u is null)
            {
                u = new Utente { Email = email, NomeCompleto = nome, Attivo = true, DataCreazione = ora };
                ctx.Utenti.Add(u);
            }
            if (u.Ruoli.Any(x => x.IdRuolo == ruolo.IdRuolo)) continue;
            u.Ruoli.Add(new UtenteRuolo { Utente = u, IdRuolo = ruolo.IdRuolo, DataAssegnazione = ora });
            assegnati++;
        }
        return $"Ruolo {ruolo.Nome} creato: {permessi.Count} permessi, assegnato a {assegnati} persone.";
    }

    // ── Ruolo Direzione (13/10/2026, una sola volta alla creazione del ruolo) ──
    private static async Task<string> ApplicaRuoloDirezioneAsync(ContestoLaboratorioAI ctx, CancellationToken ct)
    {
        var ruolo = await ctx.Ruoli.FirstAsync(r => r.Codice == Ruoli.Direzione, ct);
        var permessi = await ctx.Permessi.Where(p => ProfiliRuoli.PermessiDirezione.Contains(p.Codice)).ToListAsync(ct);
        var gia = await ctx.RuoliPermessi.Where(rp => rp.IdRuolo == ruolo.IdRuolo).Select(rp => rp.IdPermesso).ToListAsync(ct);
        foreach (var p in permessi.Where(p => !gia.Contains(p.IdPermesso)))
            ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = ruolo.IdRuolo, IdPermesso = p.IdPermesso });

        var ora = DateTime.UtcNow;
        var utenti = await ctx.Utenti.Include(u => u.Ruoli).ToListAsync(ct);
        int assegnati = 0;
        foreach (var (email, nome) in ProfiliRuoli.PersoneDirezione)
        {
            var u = utenti.FirstOrDefault(x => string.Equals(x.Email.Trim(), email, StringComparison.OrdinalIgnoreCase));
            if (u is null)
            {
                u = new Utente { Email = email, NomeCompleto = nome, Attivo = true, DataCreazione = ora };
                ctx.Utenti.Add(u);
            }
            if (u.Ruoli.Any(x => x.IdRuolo == ruolo.IdRuolo)) continue;
            u.Ruoli.Add(new UtenteRuolo { Utente = u, IdRuolo = ruolo.IdRuolo, DataAssegnazione = ora });
            assegnati++;
        }
        return $"Ruolo Direzione creato e assegnato a {assegnati} persone.";
    }

    // ── Assegnazione permessi ──────────────────────────────────────
    /// <summary>
    /// Il ruolo Amministratore riceve sempre tutti i permessi, compresi
    /// quelli introdotti da release successive. Gli altri ruoli ricevono i permessi una sola volta da
    /// <see cref="ProfiliRuoli"/>; i permessi nuovi di release successive si assegnano dalla pagina Utenti e ruoli.
    /// </summary>
    private static async Task AssegnaPermessiAmministratoreAsync(ContestoLaboratorioAI ctx, CancellationToken ct)
    {
        await ctx.SaveChangesAsync(ct);   // rende disponibili gli Id appena generati

        var amministratore = await ctx.Ruoli
            .FirstOrDefaultAsync(r => r.Codice == Ruoli.Amministratore, ct);

        if (amministratore is null) return;

        var giaAssegnati = await ctx.RuoliPermessi
            .Where(rp => rp.IdRuolo == amministratore.IdRuolo)
            .Select(rp => rp.IdPermesso)
            .ToListAsync(ct);

        var daAssegnare = await ctx.Permessi
            .Where(p => !giaAssegnati.Contains(p.IdPermesso))
            .Select(p => p.IdPermesso)
            .ToListAsync(ct);

        foreach (var idPermesso in daAssegnare)
        {
            ctx.RuoliPermessi.Add(new RuoloPermesso
            {
                IdRuolo = amministratore.IdRuolo,
                IdPermesso = idPermesso
            });
        }
    }
}
