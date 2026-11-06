using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.Clienti;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Dati.LaboratorioAI.Accreditamenti;
using LaboratorioAI.Dati.MonitorTemperature;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LaboratorioAI.Web.Navigazione;

/// <summary>Numero di «cose da fare» di una funzione, con la frase che lo spiega (suggerimento sulla home).</summary>
public sealed record ContatoreHome(int Numero, string Breve, string Testo);

/// <summary>
/// Contatori della pagina principale (08/10/2026, vedi claude/proposta-home-menu-registro-attivita.md §3).
/// Ogni contatore è legato a una voce del menu (stesso indirizzo) e si calcola solo se l'utente vede quella voce.
/// Regole:
/// - solo conteggi leggeri, ognuno con un tempo massimo (<see cref="TempoMassimo"/>): se una fonte è lenta o
///   irraggiungibile quel numero semplicemente non compare, la home non si blocca e non va in errore;
/// - risultati tenuti in memoria per <see cref="Durata"/>, così aprire la home più volte non ripete le query;
/// - tutto in sola lettura (ArcLab, Monitor Econorma e database di LabAI).
/// La home si apre subito; i numeri arrivano dopo, con una chiamata separata (Home/Contatori, js/moduli/home.js).
/// Per aggiungere un contatore: una riga in <see cref="Definizioni"/> con l'indirizzo della voce di menu.
/// </summary>
public sealed class ServizioContatoriHome
{
    public static readonly TimeSpan Durata = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan TempoMassimo = TimeSpan.FromSeconds(10);

    /// <summary>Preavviso per le tarature in scadenza (come il valore predefinito dello Scadenziario).</summary>
    public const int GiorniTarature = 30;

    private sealed record Definizione(
        string Url, string Breve, string Testo, bool PerUtente,
        Func<IServiceProvider, IContestoUtente, CancellationToken, Task<int>> Calcola,
        string? Permesso = null);   // 17/10/2026: permesso in più rispetto alla voce di menu (es. solo i validatori)

    private static readonly IReadOnlyList<Definizione> Definizioni = new List<Definizione>
    {
        // ── Ciclo analitico ──
        new("/Validazione", "da validare", "campioni con tutte le prove verificate, senza certificato", false,
            (sp, _, ct) => sp.GetRequiredService<IRepositoryCampioni>().ContaDaValidareAsync(ct)),
        new("/LimitiNormativi", "da validare", "schede limite proposte o da riverificare, in attesa di un validatore", false,
            (sp, _, ct) => sp.GetRequiredService<ContestoLaboratorioAI>().LimitiNormativi
                .CountAsync(x => x.Stato == StatoSchedaLimite.Proposta || x.Stato == StatoSchedaLimite.DaRiverificare, ct),
            Permessi.LimitiValida),
        new("/CertificatiEsterni", "da rinominare", "certificati PDF nella cartella di arrivo, da rinominare", false,
            (sp, _, ct) => Task.Run(() => CertificatiInArrivo(sp.GetRequiredService<IConfiguration>()), ct)),

        // ── Commerciale ──
        new("/ClientiDaSeguire", "da richiamare", "clienti da richiamare oggi o già in ritardo (promemoria)", false,
            async (sp, _, ct) => (await sp.GetRequiredService<ServizioClientiDaSeguire>().PromemoriaAsync(DateTime.Today, ct)).Count),
        new("/Controlli", "in attesa", "proposte di nuovi controlli in attesa di approvazione", false,
            (sp, _, ct) => sp.GetRequiredService<ContestoLaboratorioAI>().ProposteControllo
                .CountAsync(p => p.Attivo && p.DataCancellazione == null && p.Stato == StatoProposta.DaApprovare, ct)),

        // ── Consulenza ── (chi non gestisce i rapportini vede solo i propri, come nella pagina)
        new("/Rapportini", "da confermare", "rapportini inviati o contestati, da confermare", true,
            RapportiniDaConfermareAsync),

        new("/LaboratoriEsterni", "da sistemare", "accreditamenti dei laboratori in subappalto da sistemare: verifiche diverse dal flag di ArcLab, numeri ACCREDIA diversi, controllo ACCREDIA fermo", false,
            async (sp, _, ct) => (await sp.GetRequiredService<ServizioAnomalieAccreditamenti>().CalcolaAsync(ct)).Totale),

        // ── Strumenti e tarature ──
        new("/Scadenziario", "in scadenza", $"tarature scadute o in scadenza entro {GiorniTarature} giorni", false,
            async (sp, _, ct) => (await sp.GetRequiredService<IRepositoryScadenziario>().ScadenziarioInterniAsync(GiorniTarature, ct)).Count),

        // ── Monitor Econorma ──
        new("/MonitorTemperature/Sonde", "da verificare", "sonde con il seriale da verificare (diverso da ArcLab, doppio o non mappato)", false,
            SerialiDaVerificareAsync),

        // ── Impostazioni e controllo ──
        new("/ClientiDaSeguire/Assegnazioni", "da approvare", "assegnazioni di clienti proposte, da approvare", false,
            (sp, _, ct) => sp.GetRequiredService<ContestoLaboratorioAI>().AssegnazioniClienti
                .CountAsync(a => a.Stato == StatoAssegnazioneCliente.Proposta, ct)),
    };

    private readonly IServiceScopeFactory _scope;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ServizioContatoriHome> _log;

    public ServizioContatoriHome(IServiceScopeFactory scope, IMemoryCache cache, ILogger<ServizioContatoriHome> log)
    {
        _scope = scope;
        _cache = cache;
        _log = log;
    }

    /// <summary>
    /// Contatori delle voci visibili all'utente, per indirizzo della voce. Calcolati in parallelo (ognuno con il
    /// proprio contesto dati); quelli non riusciti o oltre il tempo massimo sono omessi.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, ContatoreHome>> CalcolaAsync(IContestoUtente utente, CancellationToken ct)
    {
        var visibili = MenuApplicazione.Visibili(utente.HaPermesso).SelectMany(g => g.Voci)
            .Select(v => v.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var daCalcolare = Definizioni.Where(d => visibili.Contains(d.Url) && (d.Permesso is null || utente.HaPermesso(d.Permesso))).ToList();

        var risultati = await Task.WhenAll(daCalcolare.Select(d => UnoAsync(d, utente, ct)));
        return daCalcolare.Zip(risultati)
            .Where(x => x.Second is not null)
            .ToDictionary(x => x.First.Url, x => new ContatoreHome(x.Second!.Value, x.First.Breve, x.First.Testo), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<int?> UnoAsync(Definizione d, IContestoUtente utente, CancellationToken ct)
    {
        // Chi gestisce i rapportini vede il totale: stessa chiave per tutti i gestori.
        var perUtente = d.PerUtente && !utente.HaPermesso(Permessi.RapportiniGestione);
        var chiave = $"contatore-home|{d.Url}|{(perUtente ? utente.IdUtente?.ToString() ?? "-" : "tutti")}";
        if (_cache.TryGetValue(chiave, out int memorizzato)) return memorizzato;

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(TempoMassimo);
        try
        {
            using var scope = _scope.CreateScope();
            var calcolo = d.Calcola(scope.ServiceProvider, utente, limite.Token);
            // WaitAsync: anche una fonte che ignora l'annullamento (es. cartella di rete) non trattiene la home.
            var n = await calcolo.WaitAsync(TempoMassimo, ct);
            _cache.Set(chiave, n, Durata);
            return n;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _log.LogWarning(ex, "Contatore della home non calcolato: {Url}", d.Url);
            return null;
        }
    }

    // ── Calcoli più articolati ────────────────────────────────────────

    /// <summary>PDF presenti nella cartella di arrivo dei certificati esterni (quelli già trattati vengono spostati).</summary>
    private static int CertificatiInArrivo(IConfiguration config)
    {
        var cartella = config["CertificatiEsterni:CartellaArrivo"];
        if (string.IsNullOrWhiteSpace(cartella) || !Directory.Exists(cartella))
            throw new DirectoryNotFoundException($"Cartella di arrivo dei certificati esterni non raggiungibile: {cartella}");
        return Directory.EnumerateFiles(cartella, "*.pdf", SearchOption.TopDirectoryOnly).Count();
    }

    private static Task<int> RapportiniDaConfermareAsync(IServiceProvider sp, IContestoUtente utente, CancellationToken ct)
    {
        var q = sp.GetRequiredService<ContestoLaboratorioAI>().Rapportini.AsNoTracking()
            .Where(r => r.Stato == StatoRapportino.Inviato || r.Stato == StatoRapportino.Contestato);
        if (!utente.HaPermesso(Permessi.RapportiniGestione))
        {
            var idu = utente.IdUtente ?? -1;
            q = q.Where(r => r.IdUtenteCreazione == idu);
        }
        return q.CountAsync(ct);
    }

    /// <summary>
    /// Sonde attive in ArcLab con il seriale da verificare: seriale in LabAI diverso da ArcLab, seriale usato da più
    /// sonde in ArcLab, oppure sonda non ancora nella mappa di LabAI. Stessa regola della pagina Sonde
    /// (Areas/MonitorTemperature/Views/Sonde/Index.cshtml): se si cambia lì, va cambiata anche qui.
    /// </summary>
    private static async Task<int> SerialiDaVerificareAsync(IServiceProvider sp, IContestoUtente _, CancellationToken ct)
    {
        var sonde = (await sp.GetRequiredService<IRepositoryArcLabSonde>().GetSondeAttiveAsync(ct)).ToList();
        var mappa = (await sp.GetRequiredService<ContestoMonitorTemperature>().MappaSonde.AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.IdApparecchioSonda)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IdMappa).First());
        static string Norm(string? s) => (s ?? "").Trim().ToUpperInvariant();
        var doppi = sonde.Where(x => !string.IsNullOrWhiteSpace(x.SerialeSonda))
            .GroupBy(x => Norm(x.SerialeSonda)).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        return sonde.Count(s =>
            !mappa.TryGetValue(s.IdApparecchioSonda, out var m)
            || (!string.IsNullOrWhiteSpace(m.SerialeSonda) && Norm(m.SerialeSonda) != Norm(s.SerialeSonda))
            || doppi.Contains(Norm(s.SerialeSonda)));
    }
}
