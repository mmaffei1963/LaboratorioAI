using System.Text.RegularExpressions;
using LaboratorioAI.Core.Costanti;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Web.Autorizzazione;
using LaboratorioAI.Web.Servizi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Controllers;

/// <summary>
/// Amministrazione → Utenti e ruoli (11/10/2026). Chi può entrare in LaboratorioAI e cosa può fare: ruoli delle
/// persone (si sommano), persone registrate in anticipo per e-mail, disattivazione (nessuna cancellazione), permessi
/// di ogni ruolo. Ogni modifica va nell'audit e vale alla richiesta successiva.
///
/// Regole: il ruolo Amministratore lo assegna o lo toglie solo un Amministratore, e solo un Amministratore cambia i
/// permessi dei ruoli; deve restare almeno un Amministratore attivo; nessuno può disattivare sé stesso o togliersi
/// il ruolo di Amministratore.
/// </summary>
[RichiedePermesso(Permessi.AmministrazioneUtenti)]
public class UtentiController : Controller
{
    private static readonly Regex FormatoEmail = new(@"^[^@\s;,]+@[^@\s;,]+\.[^@\s;,]+$", RegexOptions.Compiled);

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioAudit _audit;

    public UtentiController(ContestoLaboratorioAI ctx, IContestoUtente utente, IServizioAudit audit)
    {
        _ctx = ctx; _utente = utente; _audit = audit;
    }

    public IActionResult Index()
    {
        ViewData["Titolo"] = "Utenti e ruoli";
        return View();
    }

    private async Task<bool> SonoAmministratoreAsync(CancellationToken ct) =>
        _utente.IdUtente is int id && await _ctx.UtentiRuoli.AnyAsync(x => x.IdUtente == id && x.Ruolo.Codice == Ruoli.Amministratore, ct);

    // ═══ Persone ═══

    [HttpGet("api/utenti")]
    public async Task<IActionResult> Elenco(CancellationToken ct)
    {
        var ruoli = await _ctx.Ruoli.AsNoTracking().OrderBy(r => r.Ordine).ThenBy(r => r.Nome)
            .Select(r => new { r.IdRuolo, r.Codice, r.Nome, r.Descrizione, r.Attivo, Permessi = r.Permessi.Count, Persone = r.Utenti.Count })
            .ToListAsync(ct);
        var utenti = await _ctx.Utenti.AsNoTracking().OrderBy(u => u.NomeCompleto)
            .Select(u => new
            {
                u.IdUtente, u.Email, u.NomeCompleto, u.Note, u.Attivo,
                Preregistrato = u.ObjectIdEntra == null,
                u.DataCreazione, u.DataUltimoAccesso,
                Ruoli = u.Ruoli.Select(r => r.Ruolo.Codice).ToList()
            })
            .ToListAsync(ct);
        return Json(new { utenti, ruoli, sonoAmministratore = await SonoAmministratoreAsync(ct), idUtenteCorrente = _utente.IdUtente });
    }

    public sealed record RichiestaUtente(string? Email, string? NomeCompleto, string? Note, List<string>? Ruoli, bool? Attivo);

    /// <summary>Registra in anticipo una persona per e-mail: al primo accesso con quell'account trova già i suoi ruoli.</summary>
    [HttpPost("api/utenti")]
    public async Task<IActionResult> Nuovo([FromBody] RichiestaUtente req, CancellationToken ct)
    {
        var email = (req.Email ?? "").Trim();
        var nome = (req.NomeCompleto ?? "").Trim();
        if (!FormatoEmail.IsMatch(email)) return BadRequest(new { messaggio = "Indica un indirizzo e-mail valido (l'account Microsoft con cui la persona entra)." });
        if (nome.Length == 0) return BadRequest(new { messaggio = "Indica il nome." });
        if (await _ctx.Utenti.AnyAsync(u => u.Email == email, ct))
            return BadRequest(new { messaggio = "Esiste già una persona con questa e-mail: cercala nell'elenco." });

        var u = new Utente
        {
            Email = email, NomeCompleto = nome, Attivo = true, DataCreazione = DateTime.UtcNow,
            Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim()
        };
        var errore = await ApplicaRuoliAsync(u, req.Ruoli ?? new() { Ruoli.Consultazione }, nuovo: true, ct);
        if (errore is not null) return BadRequest(new { messaggio = errore });
        _ctx.Utenti.Add(u);
        await _ctx.SaveChangesAsync(ct);
        ServizioPermessiUtente.Invalida();

        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "utente.registrato", EsitoOperazione.Successo, "Utente",
            u.IdUtente.ToString(), new { u.Email, u.NomeCompleto, u.Note, Ruoli = req.Ruoli }, ct);
        return Json(new { messaggio = $"{nome} registrato: al primo accesso con {email} troverà i suoi ruoli.", u.IdUtente });
    }

    [HttpPost("api/utenti/{id:int}")]
    public async Task<IActionResult> Modifica(int id, [FromBody] RichiestaUtente req, CancellationToken ct)
    {
        var u = await _ctx.Utenti.Include(x => x.Ruoli).ThenInclude(x => x.Ruolo).FirstOrDefaultAsync(x => x.IdUtente == id, ct);
        if (u is null) return NotFound(new { messaggio = "Utente non trovato." });

        var prima = new { u.NomeCompleto, u.Note, u.Attivo, Ruoli = u.Ruoli.Select(r => r.Ruolo.Codice).OrderBy(x => x).ToList() };

        if (req.Attivo is bool attivo && attivo != u.Attivo)
        {
            if (!attivo && id == _utente.IdUtente) return BadRequest(new { messaggio = "Non puoi disattivare te stesso." });
            u.Attivo = attivo;
        }
        if (!string.IsNullOrWhiteSpace(req.NomeCompleto)) u.NomeCompleto = req.NomeCompleto.Trim();
        u.Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim();

        if (req.Ruoli is not null)
        {
            var errore = await ApplicaRuoliAsync(u, req.Ruoli, nuovo: false, ct);
            if (errore is not null) return BadRequest(new { messaggio = errore });
        }

        // Deve restare almeno un Amministratore attivo.
        var ammAttivi = await _ctx.UtentiRuoli.Where(x => x.Ruolo.Codice == Ruoli.Amministratore && x.Utente.Attivo && x.IdUtente != id)
            .Select(x => x.IdUtente).Distinct().CountAsync(ct);
        var idAmm = await _ctx.Ruoli.Where(r => r.Codice == Ruoli.Amministratore).Select(r => r.IdRuolo).FirstOrDefaultAsync(ct);
        var restaAmm = u.Attivo && u.Ruoli.Any(r => r.IdRuolo == idAmm);
        if (ammAttivi == 0 && !restaAmm) return BadRequest(new { messaggio = "Deve restare almeno un Amministratore attivo." });

        await _ctx.SaveChangesAsync(ct);
        ServizioPermessiUtente.Invalida();

        var dopoRuoli = await _ctx.UtentiRuoli.Where(x => x.IdUtente == id).Select(x => x.Ruolo.Codice).OrderBy(x => x).ToListAsync(ct);
        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "utente.modificato", EsitoOperazione.Successo, "Utente",
            id.ToString(), new { u.Email, Prima = prima, Dopo = new { u.NomeCompleto, u.Note, u.Attivo, Ruoli = dopoRuoli } }, ct);
        return Json(new { messaggio = $"{u.NomeCompleto}: modifiche salvate." });
    }

    /// <summary>Imposta i ruoli dell'utente; restituisce un messaggio d'errore se la richiesta non è ammessa.</summary>
    private async Task<string?> ApplicaRuoliAsync(Utente u, List<string> codici, bool nuovo, CancellationToken ct)
    {
        var ruoli = await _ctx.Ruoli.ToDictionaryAsync(r => r.Codice, ct);
        var idAmm = ruoli.TryGetValue(Ruoli.Amministratore, out var amm) ? amm.IdRuolo : 0;
        var voluti = codici.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct().ToList();
        foreach (var c in voluti)
        {
            if (!ruoli.TryGetValue(c, out var r)) return $"Ruolo sconosciuto: {c}.";
            if (!r.Attivo && u.Ruoli.All(x => x.IdRuolo != r.IdRuolo)) return $"Il ruolo «{r.Nome}» non è più in uso.";
        }
        if (voluti.Count == 0) return "Assegna almeno un ruolo (per la sola lettura: Consultazione). Per togliere l'accesso usa «Disattiva».";

        var aveva = u.Ruoli.Any(x => x.IdRuolo == idAmm);
        var avra = voluti.Contains(Ruoli.Amministratore);
        if (aveva != avra)
        {
            if (!await SonoAmministratoreAsync(ct)) return "Solo un Amministratore può assegnare o togliere il ruolo di Amministratore.";
            if (!nuovo && aveva && u.IdUtente == _utente.IdUtente) return "Non puoi toglierti da solo il ruolo di Amministratore.";
        }

        var ora = DateTime.UtcNow;
        var idVoluti = voluti.Select(c => ruoli[c].IdRuolo).ToHashSet();
        foreach (var ur in u.Ruoli.Where(x => !idVoluti.Contains(x.IdRuolo)).ToList()) { u.Ruoli.Remove(ur); _ctx.UtentiRuoli.Remove(ur); }
        foreach (var idR in idVoluti.Where(idR => u.Ruoli.All(x => x.IdRuolo != idR)))
            u.Ruoli.Add(new UtenteRuolo { Utente = u, IdRuolo = idR, DataAssegnazione = ora, IdUtenteAssegnante = _utente.IdUtente });
        return null;
    }

    // ═══ Ruoli e permessi ═══

    private static string NomeArea(AreaFunzionale a) => a switch
    {
        AreaFunzionale.LaboratoriEsterni => "Laboratori esterni",
        AreaFunzionale.Dvr => "DVR Legionella",
        AreaFunzionale.Piscine => "DVR Piscine",
        AreaFunzionale.Taratura => "Tarature termometri",
        AreaFunzionale.Incubatori => "Incubatori e mappature",
        AreaFunzionale.Bilance => "Bilance",
        AreaFunzionale.SoloEsterni => "Certificati esterni non ricertificati",
        AreaFunzionale.CertificatiEsterni => "Certificati esterni",
        AreaFunzionale.RicercaErrori => "Ricerca errori",
        AreaFunzionale.MonitorTemperature => "Monitor temperature",
        AreaFunzionale.Contratti => "Condizioni e firme",
        AreaFunzionale.Clienti => "Clienti da seguire",
        AreaFunzionale.LimitiNormativi => "Limiti normativi",
        _ => a.ToString()
    };

    [HttpGet("api/utenti/ruoli")]
    public async Task<IActionResult> ElencoRuoli(CancellationToken ct)
    {
        var permessi = await _ctx.Permessi.AsNoTracking().OrderBy(p => p.Area).ThenBy(p => p.IdPermesso)
            .Select(p => new { p.Codice, p.Descrizione, p.Area }).ToListAsync(ct);
        var aree = permessi.GroupBy(p => p.Area)
            .Select(g => new { Area = NomeArea(g.Key), Permessi = g.Select(p => new { p.Codice, p.Descrizione }).ToList() })
            .ToList();
        var ruoli = await _ctx.Ruoli.AsNoTracking().Where(r => r.Attivo).OrderBy(r => r.Ordine).ThenBy(r => r.Nome)
            .Select(r => new { r.Codice, r.Nome, r.Descrizione, Permessi = r.Permessi.Select(p => p.Permesso.Codice).ToList() })
            .ToListAsync(ct);
        return Json(new { aree, ruoli, modificabile = await SonoAmministratoreAsync(ct) });
    }

    public sealed record RichiestaPermessiRuolo(List<string>? Permessi);

    [HttpPost("api/utenti/ruoli/{codice}/permessi")]
    public async Task<IActionResult> SalvaPermessiRuolo(string codice, [FromBody] RichiestaPermessiRuolo req, CancellationToken ct)
    {
        if (!await SonoAmministratoreAsync(ct)) return BadRequest(new { messaggio = "Solo un Amministratore può cambiare i permessi dei ruoli." });
        if (codice == Ruoli.Amministratore) return BadRequest(new { messaggio = "L'Amministratore ha sempre tutti i permessi." });
        var ruolo = await _ctx.Ruoli.Include(r => r.Permessi).ThenInclude(p => p.Permesso).FirstOrDefaultAsync(r => r.Codice == codice, ct);
        if (ruolo is null) return NotFound(new { messaggio = "Ruolo non trovato." });

        var tutti = await _ctx.Permessi.ToDictionaryAsync(p => p.Codice, ct);
        var voluti = (req.Permessi ?? new()).Where(tutti.ContainsKey).ToHashSet();
        var prima = ruolo.Permessi.Select(p => p.Permesso.Codice).ToHashSet();
        var tolti = prima.Except(voluti).OrderBy(x => x).ToList();
        var aggiunti = voluti.Except(prima).OrderBy(x => x).ToList();
        if (tolti.Count == 0 && aggiunti.Count == 0) return Json(new { messaggio = "Nessuna modifica." });

        foreach (var rp in ruolo.Permessi.Where(p => tolti.Contains(p.Permesso.Codice)).ToList()) _ctx.RuoliPermessi.Remove(rp);
        foreach (var c in aggiunti) _ctx.RuoliPermessi.Add(new RuoloPermesso { IdRuolo = ruolo.IdRuolo, IdPermesso = tutti[c].IdPermesso });
        await _ctx.SaveChangesAsync(ct);
        ServizioPermessiUtente.Invalida();

        await _audit.RegistraAsync(AreaFunzionale.Amministrazione, "ruolo.permessi.modificati", EsitoOperazione.Successo, "Ruolo",
            ruolo.IdRuolo.ToString(), new { ruolo.Codice, ruolo.Nome, Aggiunti = aggiunti, Tolti = tolti }, ct);
        return Json(new { messaggio = $"{ruolo.Nome}: {aggiunti.Count} permessi aggiunti, {tolti.Count} tolti." });
    }
}
