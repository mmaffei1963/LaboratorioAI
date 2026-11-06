using System.Net;
using System.Text;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Registro di controllo dei documenti destinati a Palomar (Ordini di Accettazione emessi e
/// certificati esterni rinominati). Scrive righe append-only in log.RegistroDocumenti, verifica
/// che i file siano davvero presenti nella cartella di prelievo e, a fine esecuzione, manda una
/// mail di riepilogo se qualcosa non è andato a buon fine.
/// Le scritture nel registro non interrompono mai l'operazione principale: un errore del registro
/// finisce nel log applicativo (Serilog).
/// </summary>
public class ServizioRegistroDocumenti
{
    public const string AmbitoOrdine = "OrdineAccettazione";
    public const string AmbitoCertificato = "CertificatoEsterno";
    public const string AmbitoRapportino = "Rapportino";

    /// <summary>Chiave di configurazione (Impostazioni) con i destinatari delle mail di errore.</summary>
    public const string ChiaveEmailErrori = "OrdineAccettazione.EmailErrori";
    public const string EmailErroriDefault = "mmaffei@mare-a.com; dbertozzi@mare-a.com; refertazione@mare-a.com";

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioEmail _email;
    private readonly ILogger<ServizioRegistroDocumenti> _log;

    public ServizioRegistroDocumenti(ContestoLaboratorioAI ctx, IContestoUtente utente, IServizioEmail email,
        ILogger<ServizioRegistroDocumenti> log)
    {
        _ctx = ctx; _utente = utente; _email = email; _log = log;
    }

    /// <summary>Nuovo identificativo di esecuzione, leggibile (data-ora locale + suffisso casuale).</summary>
    public static string NuovaEsecuzione() =>
        DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];

    /// <summary>
    /// Scrive <paramref name="contenuto"/> in <paramref name="cartella"/>/<paramref name="nomeFile"/> e
    /// verifica che il file esista con la dimensione attesa. Non lancia eccezioni: ritorna l'esito.
    /// </summary>
    public static (bool Ok, string? Percorso, string? Errore) ScriviVerificato(string? cartella, string nomeFile, byte[] contenuto)
    {
        if (string.IsNullOrWhiteSpace(cartella))
            return (false, null, "Cartella Palomar non impostata.");
        string? percorso = null;
        try
        {
            Directory.CreateDirectory(cartella);
            percorso = Path.Combine(cartella, nomeFile);
            File.WriteAllBytes(percorso, contenuto);
            return Verifica(percorso, contenuto.LongLength);
        }
        catch (Exception ex)
        {
            return (false, percorso, "Scrittura nella cartella Palomar fallita: " + ex.Message);
        }
    }

    /// <summary>Controlla che il file esista e abbia la dimensione attesa (se indicata).</summary>
    public static (bool Ok, string? Percorso, string? Errore) Verifica(string? percorso, long? dimensioneAttesa = null)
    {
        if (string.IsNullOrWhiteSpace(percorso))
            return (false, percorso, "Percorso Palomar assente.");
        try
        {
            var fi = new FileInfo(percorso);
            if (!fi.Exists)
                return (false, percorso, "File non presente nella cartella Palomar.");
            if (dimensioneAttesa is long d && fi.Length != d)
                return (false, percorso, $"Dimensione del file in Palomar diversa dall'atteso ({fi.Length} invece di {d} byte).");
            if (fi.Length == 0)
                return (false, percorso, "File vuoto nella cartella Palomar.");
            return (true, percorso, null);
        }
        catch (Exception ex)
        {
            return (false, percorso, "Verifica del file Palomar fallita: " + ex.Message);
        }
    }

    /// <summary>Aggiunge una riga al registro (data/ora UTC e utente sono impostati qui).</summary>
    public async Task RegistraAsync(RegistroDocumento riga, CancellationToken ct = default)
    {
        riga.DataOra = DateTime.UtcNow;
        riga.IdUtente ??= _utente.IdUtente;
        riga.Origine = Taglia(riga.Origine, 30);
        riga.Riferimento = Taglia(riga.Riferimento, 300);
        riga.Messaggio = Taglia(riga.Messaggio, 2000);
        riga.FileOrigine = Taglia(riga.FileOrigine, 500);
        riga.FileArchivio = Taglia(riga.FileArchivio, 500);
        riga.FilePalomar = Taglia(riga.FilePalomar, 500);
        _ctx.RegistroDocumenti.Add(riga);
        try
        {
            await _ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _ctx.Entry(riga).State = EntityState.Detached;
            _log.LogError(ex, "Registro documenti: impossibile scrivere la riga {Ambito} {Riferimento} ({Esito})",
                riga.Ambito, riga.Riferimento, riga.Esito);
        }
    }

    /// <summary>Righe del registro degli ultimi <paramref name="giorni"/> giorni, più recenti prima.</summary>
    public async Task<IReadOnlyList<RegistroDocumento>> ElencoAsync(string ambito, int giorni, bool soloProblemi, CancellationToken ct = default)
    {
        var dal = DateTime.UtcNow.AddDays(-Math.Clamp(giorni, 1, 366));
        var q = _ctx.RegistroDocumenti.AsNoTracking().Where(r => r.Ambito == ambito && r.DataOra >= dal);
        if (soloProblemi)
            q = q.Where(r => r.Esito == EsitoDocumento.Errore || r.Esito == EsitoDocumento.Parziale);
        return await q.OrderByDescending(r => r.DataOra).ThenByDescending(r => r.IdRegistroDocumento)
            .Take(2000).ToListAsync(ct);
    }

    /// <summary>Righe di una singola esecuzione.</summary>
    public async Task<IReadOnlyList<RegistroDocumento>> EsecuzioneAsync(string idEsecuzione, CancellationToken ct = default) =>
        await _ctx.RegistroDocumenti.AsNoTracking().Where(r => r.IdEsecuzione == idEsecuzione)
            .OrderBy(r => r.IdRegistroDocumento).ToListAsync(ct);

    /// <summary>
    /// Se l'esecuzione contiene righe in Errore o Parziale, manda una mail di riepilogo ai destinatari
    /// configurati. Ritorna true se la mail è partita. Non lancia eccezioni.
    /// </summary>
    public async Task<bool> NotificaProblemiAsync(string ambito, string idEsecuzione, CancellationToken ct = default)
    {
        try
        {
            var righe = await EsecuzioneAsync(idEsecuzione, ct);
            var problemi = righe.Where(r => r.Esito is EsitoDocumento.Errore or EsitoDocumento.Parziale).ToList();
            if (problemi.Count == 0) return false;

            var cfg = await _ctx.Configurazioni.AsNoTracking().FirstOrDefaultAsync(c => c.Chiave == ChiaveEmailErrori, ct);
            var elenco = string.IsNullOrWhiteSpace(cfg?.Valore) ? EmailErroriDefault : cfg!.Valore!;
            var destinatari = elenco.Split(new[] { ';', ',', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(e => e.Contains('@')).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (destinatari.Count == 0) return false;

            var titolo = ambito switch
            {
                AmbitoOrdine => "Ordini di Accettazione",
                AmbitoRapportino => "Rapportini di consulenza",
                _ => "Certificati esterni"
            };
            var oggetto = $"[LaboratorioAI] {titolo}: {problemi.Count} documento/i non arrivati correttamente in Palomar";
            await _email.InviaAsync(destinatari, oggetto, CorpoMail(titolo, idEsecuzione, righe, problemi), ct);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Registro documenti: invio mail di riepilogo fallito per l'esecuzione {IdEsecuzione}", idEsecuzione);
            return false;
        }
    }

    private static string CorpoMail(string titolo, string idEsecuzione, IReadOnlyList<RegistroDocumento> righe,
        IReadOnlyList<RegistroDocumento> problemi)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        var ora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, FusoItalia());
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px\">");
        sb.Append($"<p><strong>{H(titolo)}</strong> — esecuzione {H(idEsecuzione)} del {ora:dd/MM/yyyy HH:mm}</p>");
        sb.Append("<p>Riepilogo: ");
        sb.Append($"{righe.Count(r => r.Esito == EsitoDocumento.Ok)} ok, ");
        sb.Append($"<strong style=\"color:#b00\">{problemi.Count} con problemi</strong>");
        var np = righe.Count(r => r.Esito == EsitoDocumento.NonPronto);
        if (np > 0) sb.Append($", {np} non pronti");
        var ge = righe.Count(r => r.Esito == EsitoDocumento.GiaEmesso);
        if (ge > 0) sb.Append($", {ge} già emessi");
        sb.Append(".</p>");
        sb.Append("<table cellpadding=\"5\" cellspacing=\"0\" border=\"1\" style=\"border-collapse:collapse;font-size:13px\">");
        sb.Append("<tr style=\"background:#eee\"><th>Esito</th><th>Riferimento</th><th>Messaggio</th><th>File</th></tr>");
        foreach (var r in problemi)
        {
            sb.Append("<tr>");
            sb.Append($"<td>{H(r.Esito.ToString())}</td>");
            sb.Append($"<td>{H(r.Riferimento)}</td>");
            sb.Append($"<td>{H(r.Messaggio)}</td>");
            sb.Append($"<td>{H(r.FilePalomar ?? r.FileArchivio ?? r.FileOrigine)}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</table>");
        sb.Append("<p>Il dettaglio è nel Registro della pagina corrispondente di LaboratorioAI, dove si può ritentare la consegna.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static TimeZoneInfo FusoItalia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Rome"); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
            catch { return TimeZoneInfo.Local; }
        }
    }

    private static string? Taglia(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
