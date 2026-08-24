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
    public static async Task EseguiAsync(ContestoLaboratorioAI ctx, CancellationToken ct = default)
    {
        await PopolaPermessiAsync(ctx, ct);
        await PopolaRuoliAsync(ctx, ct);
        await AssegnaPermessiAmministratoreAsync(ctx, ct);
        await ctx.SaveChangesAsync(ct);
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

        (Permessi.SoloEsterniVisualizza, "Confrontare i certificati esterni con gli interni", AreaFunzionale.SoloEsterni),

        (Permessi.ControlliProponi, "Proporre nuovi controlli (bozze)",                    AreaFunzionale.Controlli),
        (Permessi.ControlliApprova, "Approvare e generare il tracciato dei controlli",     AreaFunzionale.Controlli),

        (Permessi.RicercaErroriVisualizza, "Eseguire le verifiche diagnostiche (ricerca errori)", AreaFunzionale.RicercaErrori),

        (Permessi.AmministrazioneUtenti,         "Gestire utenti e ruoli",                      AreaFunzionale.Amministrazione),
        (Permessi.AmministrazioneConfigurazioni, "Modificare le configurazioni applicative",    AreaFunzionale.Amministrazione),
        (Permessi.AmministrazioneAudit,          "Consultare l'audit trail",                    AreaFunzionale.Amministrazione),
    ];

    // ── Ruoli ──────────────────────────────────────────────────────
    private static async Task PopolaRuoliAsync(ContestoLaboratorioAI ctx, CancellationToken ct)
    {
        var esistenti = await ctx.Ruoli.Select(r => r.Codice).ToListAsync(ct);

        (string Codice, string Nome, string Descrizione)[] ruoli =
        [
            (Ruoli.Amministratore, "Amministratore", "Accesso completo, comprese gestione utenti e configurazioni"),
            (Ruoli.Responsabile,   "Responsabile",   "Tutte le funzioni operative, esclusa l'amministrazione"),
            (Ruoli.Operatore,      "Operatore",      "Consultazione e analisi, senza accesso ai dati commerciali"),
            (Ruoli.Consultazione,  "Consultazione",  "Sola lettura")
        ];

        foreach (var (codice, nome, descrizione) in ruoli)
        {
            if (esistenti.Contains(codice)) continue;

            ctx.Ruoli.Add(new Ruolo
            {
                Codice = codice,
                Nome = nome,
                Descrizione = descrizione,
                DiSistema = true
            });
        }
    }

    // ── Assegnazione permessi ──────────────────────────────────────
    /// <summary>
    /// Il ruolo Amministratore riceve sempre tutti i permessi, compresi
    /// quelli introdotti da release successive.
    ///
    /// Gli altri ruoli non ricevono permessi in questa release: la
    /// differenziazione è prevista dal modello ma non ancora attivata.
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
