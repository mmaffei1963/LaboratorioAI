using LaboratorioAI.Tracciabilita.Core.Entita;
using LaboratorioAI.Tracciabilita.Core.Enumerazioni;
using LaboratorioAI.Tracciabilita.Core.Motori;
using LaboratorioAI.Tracciabilita.Core.Sicurezza;
using LaboratorioAI.Tracciabilita.Dati;
using LaboratorioAI.Tracciabilita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Tracciabilita.Web.Controllers;

/// <summary>Area riservata al personale MARE.A: aziende clienti, sedi, utenti, inviti, registro attività.</summary>
[Authorize(Policy = "Gestore")]
[Route("Gestione")]
public class GestioneController : Controller
{
    private readonly ContestoTracciabilita _db;

    public GestioneController(ContestoTracciabilita db) => _db = db;

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

    // ── Elenco aziende ─────────────────────────────────────────────
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var aziende = await _db.Aziende.AsNoTracking().OrderBy(a => a.RagioneSociale).ToListAsync(ct);
        var sedi = await _db.Insediamenti.AsNoTracking().Where(i => i.Attivo)
            .GroupBy(i => i.IdAzienda).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N, ct);
        var utenti = await _db.Utenti.AsNoTracking().Where(u => u.Attivo && u.IdAzienda != null)
            .GroupBy(u => u.IdAzienda!.Value).Select(g => new { Id = g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.N, ct);

        var righe = aziende.Select(a => new RigaAzienda
        {
            IdAzienda = a.IdAzienda,
            RagioneSociale = a.RagioneSociale,
            Settore = a.Settore,
            Stato = a.Stato,
            Sedi = sedi.GetValueOrDefault(a.IdAzienda),
            Utenti = utenti.GetValueOrDefault(a.IdAzienda)
        }).ToList();
        return View(righe);
    }

    // ── Nuova azienda ──────────────────────────────────────────────
    [HttpGet("NuovaAzienda")]
    public IActionResult NuovaAzienda() => View(new ModelloNuovaAzienda());

    [HttpPost("NuovaAzienda")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuovaAzienda(ModelloNuovaAzienda m, CancellationToken ct)
    {
        var sigla = GeneratoreCodiceLotto.NormalizzaSigla(m.SiglaSede);
        if (sigla is null) ModelState.AddModelError(nameof(m.SiglaSede), "La sigla deve contenere lettere o cifre.");
        if (!ModelState.IsValid) return View(m);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var azienda = new Azienda
        {
            RagioneSociale = m.RagioneSociale!.Trim(),
            PartitaIva = Pulisci(m.PartitaIva),
            Email = Pulisci(m.Email),
            Settore = m.Settore,
            Stato = StatoServizio.Attivo,
            DataAttivazione = DateTime.UtcNow,
            MaxInsediamenti = m.MaxInsediamenti,
            MaxUtenti = m.MaxUtenti
        };
        _db.Aziende.Add(azienda);
        await _db.SaveChangesAsync(ct);

        _db.ImpostazioniAziende.Add(new ImpostazioniAzienda { IdAzienda = azienda.IdAzienda });
        _db.Insediamenti.Add(new Insediamento { IdAzienda = azienda.IdAzienda, Nome = m.NomeSede!.Trim(), Sigla = sigla! });
        Registra(azienda.IdAzienda, "AziendaCreata", "Azienda", azienda.IdAzienda, azienda.RagioneSociale);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        TempData["Messaggio"] = $"Azienda «{azienda.RagioneSociale}» creata. Ora aggiungi il primo utente.";
        return RedirectToAction(nameof(Azienda), new { id = azienda.IdAzienda });
    }

    // ── Scheda azienda ─────────────────────────────────────────────
    [HttpGet("Azienda/{id:int}")]
    public async Task<IActionResult> Azienda(int id, CancellationToken ct)
    {
        var m = await CaricaScheda(id, ct);
        if (m is null) return NotFound();
        if (TempData["LinkInvito"] is string link)
        {
            m.LinkInvito = link;
            m.LinkInvitoPer = TempData["LinkInvitoPer"] as string;
        }
        return View(m);
    }

    private async Task<ModelloAziendaDettaglio?> CaricaScheda(int id, CancellationToken ct)
    {
        var azienda = await _db.Aziende.AsNoTracking().FirstOrDefaultAsync(a => a.IdAzienda == id, ct);
        if (azienda is null) return null;

        var sedi = await _db.Insediamenti.AsNoTracking().Where(i => i.IdAzienda == id).OrderBy(i => i.Nome).ToListAsync(ct);
        var utenti = await _db.Utenti.AsNoTracking().Where(u => u.IdAzienda == id).OrderBy(u => u.Nome).ToListAsync(ct);
        var ruoli = await _db.UtentiInsediamenti.AsNoTracking().Where(r => r.IdAzienda == id).ToListAsync(ct);
        var adesso = DateTime.UtcNow;

        string Ruoli(int idUtente) => string.Join(", ", ruoli.Where(r => r.IdUtente == idUtente).Select(r =>
            $"{NomeRuolo(r.Ruolo)} ({(r.IdInsediamento is null ? "tutte le sedi" : sedi.FirstOrDefault(s => s.IdInsediamento == r.IdInsediamento)?.Nome ?? "?")})"));

        return new ModelloAziendaDettaglio
        {
            Azienda = azienda,
            Insediamenti = sedi,
            Utenti = utenti.Select(u => new RigaUtente
            {
                IdUtente = u.IdUtente,
                Nome = u.Nome,
                Email = u.Email,
                Attivo = u.Attivo,
                UltimoAccesso = u.UltimoAccesso,
                PasswordImpostata = !string.IsNullOrEmpty(u.ImprontaPassword),
                InvitoPendente = u.ImprontaInvito != null && u.ScadenzaInvitoUtc > adesso,
                Ruoli = Ruoli(u.IdUtente)
            }).ToList()
        };
    }

    public static string NomeRuolo(RuoloAzienda r) => r switch
    {
        RuoloAzienda.Consultazione => "Consultazione",
        RuoloAzienda.Ricevimento => "Ricevimento merci",
        RuoloAzienda.Produzione => "Produzione",
        RuoloAzienda.Qualita => "Qualità",
        RuoloAzienda.AmministratoreAzienda => "Amministratore azienda",
        _ => r.ToString()
    };

    // ── Sedi ───────────────────────────────────────────────────────
    [HttpPost("Azienda/{id:int}/Sede")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuovaSede(int id, [Bind(Prefix = "NuovaSede")] ModelloNuovoInsediamento m, CancellationToken ct)
    {
        var azienda = await _db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == id, ct);
        if (azienda is null) return NotFound();

        var sigla = GeneratoreCodiceLotto.NormalizzaSigla(m.Sigla);
        if (!ModelState.IsValid || sigla is null) return Errore(id, PrimoErrore() ?? "Controlla nome e sigla della sede.");

        var attive = await _db.Insediamenti.CountAsync(i => i.IdAzienda == id && i.Attivo, ct);
        if (attive >= azienda.MaxInsediamenti)
            return Errore(id, $"Questa azienda può avere al massimo {azienda.MaxInsediamenti} sedi. Aumenta il limite se il contratto lo prevede.");
        if (await _db.Insediamenti.AnyAsync(i => i.IdAzienda == id && i.Sigla == sigla, ct))
            return Errore(id, $"La sigla «{sigla}» è già usata da un'altra sede.");

        var sede = new Insediamento { IdAzienda = id, Nome = m.Nome!.Trim(), Sigla = sigla, Indirizzo = Pulisci(m.Indirizzo) };
        _db.Insediamenti.Add(sede);
        await _db.SaveChangesAsync(ct);
        Registra(id, "SedeCreata", "Insediamento", sede.IdInsediamento, $"{sede.Nome} ({sede.Sigla})");
        await _db.SaveChangesAsync(ct);

        TempData["Messaggio"] = $"Sede «{sede.Nome}» aggiunta.";
        return RedirectToAction(nameof(Azienda), new { id });
    }

    [HttpPost("Azienda/{id:int}/Sede/{idSede:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModificaSede(int id, int idSede, [Bind(Prefix = "Sede")] ModelloNuovoInsediamento m, CancellationToken ct)
    {
        var sede = await _db.Insediamenti.FirstOrDefaultAsync(i => i.IdInsediamento == idSede && i.IdAzienda == id, ct);
        if (sede is null) return NotFound();

        var sigla = GeneratoreCodiceLotto.NormalizzaSigla(m.Sigla);
        if (!ModelState.IsValid || sigla is null) return Errore(id, PrimoErrore() ?? "Controlla nome e sigla della sede.");
        if (await _db.Insediamenti.AnyAsync(i => i.IdAzienda == id && i.IdInsediamento != idSede && i.Sigla == sigla, ct))
            return Errore(id, $"La sigla «{sigla}» è già usata da un'altra sede.");

        var prima = $"{sede.Nome} ({sede.Sigla}) {sede.Indirizzo}";
        sede.Nome = m.Nome!.Trim();
        sede.Sigla = sigla;
        sede.Indirizzo = Pulisci(m.Indirizzo);
        Registra(id, "SedeModificata", "Insediamento", sede.IdInsediamento, $"{prima} → {sede.Nome} ({sede.Sigla}) {sede.Indirizzo}");
        await _db.SaveChangesAsync(ct);

        TempData["Messaggio"] = $"Sede «{sede.Nome}» aggiornata. I lotti già creati mantengono la sigla di allora.";
        return RedirectToAction(nameof(Azienda), new { id });
    }

    [HttpPost("Azienda/{id:int}/Dati")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModificaAzienda(int id, string? ragioneSociale, string? partitaIva, string? email, CancellationToken ct)
    {
        var azienda = await _db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == id, ct);
        if (azienda is null) return NotFound();
        if (string.IsNullOrWhiteSpace(ragioneSociale)) return Errore(id, "La ragione sociale non può essere vuota.");

        var prima = $"{azienda.RagioneSociale}; P.IVA {azienda.PartitaIva}; {azienda.Email}";
        azienda.RagioneSociale = ragioneSociale.Trim();
        azienda.PartitaIva = Pulisci(partitaIva);
        azienda.Email = Pulisci(email);
        Registra(id, "AziendaModificata", "Azienda", id, $"{prima} → {azienda.RagioneSociale}; P.IVA {azienda.PartitaIva}; {azienda.Email}");
        await _db.SaveChangesAsync(ct);

        TempData["Messaggio"] = "Dati dell'azienda aggiornati.";
        return RedirectToAction(nameof(Azienda), new { id });
    }

    // ── Utenti ─────────────────────────────────────────────────────
    [HttpPost("Azienda/{id:int}/Utente")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuovoUtente(int id, [Bind(Prefix = "NuovoUtente")] ModelloNuovoUtente m, CancellationToken ct)
    {
        var azienda = await _db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == id, ct);
        if (azienda is null) return NotFound();
        if (!ModelState.IsValid) return Errore(id, PrimoErrore() ?? "Controlla i dati dell'utente.");

        var email = m.Email!.Trim();
        if (await _db.Utenti.AnyAsync(u => u.Email == email, ct))
            return Errore(id, "Esiste già un utente con questa e-mail.");

        var attivi = await _db.Utenti.CountAsync(u => u.IdAzienda == id && u.Attivo, ct);
        if (attivi >= azienda.MaxUtenti)
            return Errore(id, $"Questa azienda può avere al massimo {azienda.MaxUtenti} utenti attivi.");

        if (m.IdInsediamento is { } idSede &&
            !await _db.Insediamenti.AnyAsync(i => i.IdAzienda == id && i.IdInsediamento == idSede && i.Attivo, ct))
            return Errore(id, "La sede scelta non appartiene a questa azienda.");

        var adesso = DateTime.UtcNow;
        var invito = InvitoUtente.Crea(adesso);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var utente = new Utente
        {
            IdAzienda = id,
            Email = email,
            Nome = m.Nome!.Trim(),
            Attivo = true,
            ImprontaInvito = invito.Impronta,
            ScadenzaInvitoUtc = invito.ScadenzaUtc
        };
        _db.Utenti.Add(utente);
        await _db.SaveChangesAsync(ct);

        _db.UtentiInsediamenti.Add(new UtenteInsediamento
        {
            IdAzienda = id,
            IdUtente = utente.IdUtente,
            IdInsediamento = m.IdInsediamento,
            Ruolo = m.Ruolo
        });
        Registra(id, "UtenteCreato", "Utente", utente.IdUtente, $"{utente.Email} — {NomeRuolo(m.Ruolo)}");
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return RiceviLink(id, utente, invito.Codice);
    }

    [HttpPost("Utente/{idUtente:int}/NuovoInvito")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NuovoInvito(int idUtente, CancellationToken ct)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.IdUtente == idUtente && u.IdAzienda != null && !u.EGestore, ct);
        if (utente is null) return NotFound();
        if (!utente.Attivo) return Errore(utente.IdAzienda!.Value, "L'utente è disattivato: riattivalo prima di inviargli un invito.");

        var invito = InvitoUtente.Crea(DateTime.UtcNow);
        utente.ImprontaInvito = invito.Impronta;
        utente.ScadenzaInvitoUtc = invito.ScadenzaUtc;
        Registra(utente.IdAzienda, "InvitoRigenerato", "Utente", utente.IdUtente, utente.Email);
        await _db.SaveChangesAsync(ct);
        return RiceviLink(utente.IdAzienda!.Value, utente, invito.Codice);
    }

    [HttpPost("Utente/{idUtente:int}/Ruolo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiaRuolo(int idUtente, RuoloAzienda ruolo, CancellationToken ct)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.IdUtente == idUtente && u.IdAzienda != null && !u.EGestore, ct);
        if (utente is null) return NotFound();
        var id = utente.IdAzienda!.Value;
        if (!Enum.IsDefined(ruolo)) return Errore(id, "Scegli un ruolo valido.");

        var righe = await _db.UtentiInsediamenti.Where(r => r.IdUtente == idUtente).ToListAsync(ct);
        var prima = righe.Count == 0 ? "nessun ruolo" : string.Join(", ", righe.Select(r => NomeRuolo(r.Ruolo)));
        if (righe.Count == 0)
            _db.UtentiInsediamenti.Add(new UtenteInsediamento { IdAzienda = id, IdUtente = idUtente, IdInsediamento = null, Ruolo = ruolo });
        else
            foreach (var r in righe) r.Ruolo = ruolo;
        Registra(id, "RuoloCambiato", "Utente", utente.IdUtente, $"{utente.Email}: {prima} → {NomeRuolo(ruolo)}");
        await _db.SaveChangesAsync(ct);   // il ruolo si rilegge a ogni richiesta: vale subito
        TempData["Messaggio"] = $"{utente.Nome}: ruolo cambiato in {NomeRuolo(ruolo)}.";
        return RedirectToAction(nameof(Azienda), new { id });
    }

    [HttpPost("Utente/{idUtente:int}/Attivo")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImpostaAttivo(int idUtente, bool attivo, CancellationToken ct)
    {
        var utente = await _db.Utenti.FirstOrDefaultAsync(u => u.IdUtente == idUtente && u.IdAzienda != null && !u.EGestore, ct);
        if (utente is null) return NotFound();
        var id = utente.IdAzienda!.Value;

        if (attivo && !utente.Attivo)
        {
            var azienda = await _db.Aziende.AsNoTracking().FirstAsync(a => a.IdAzienda == id, ct);
            if (await _db.Utenti.CountAsync(u => u.IdAzienda == id && u.Attivo, ct) >= azienda.MaxUtenti)
                return Errore(id, $"Questa azienda può avere al massimo {azienda.MaxUtenti} utenti attivi.");
        }

        utente.Attivo = attivo;
        Registra(id, attivo ? "UtenteRiattivato" : "UtenteDisattivato", "Utente", utente.IdUtente, utente.Email);
        await _db.SaveChangesAsync(ct);   // la sessione dell'utente smette di funzionare alla richiesta successiva
        TempData["Messaggio"] = attivo ? $"{utente.Nome} riattivato." : $"{utente.Nome} disattivato: non può più entrare.";
        return RedirectToAction(nameof(Azienda), new { id });
    }

    // ── Stato del servizio ─────────────────────────────────────────
    [HttpPost("Azienda/{id:int}/Stato")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CambiaStato(int id, StatoServizio stato, CancellationToken ct)
    {
        var azienda = await _db.Aziende.FirstOrDefaultAsync(a => a.IdAzienda == id, ct);
        if (azienda is null) return NotFound();

        // «Chiuso» e l'eliminazione dei dati richiedono una procedura a parte (con scarico dati e conferma).
        if (stato is not (StatoServizio.Attivo or StatoServizio.Sospeso or StatoServizio.InChiusura))
            return Errore(id, "Questo stato non si imposta da qui.");

        var prima = azienda.Stato;
        if (prima == stato) return RedirectToAction(nameof(Azienda), new { id });

        azienda.Stato = stato;
        if (stato == StatoServizio.InChiusura) azienda.DataInizioChiusura ??= DateTime.UtcNow;
        if (stato == StatoServizio.Attivo) azienda.DataInizioChiusura = null;
        Registra(id, "StatoServizioCambiato", "Azienda", id, $"{prima} → {stato}");
        await _db.SaveChangesAsync(ct);

        TempData["Messaggio"] = $"Stato del servizio: {stato}.";
        return RedirectToAction(nameof(Azienda), new { id });
    }

    // ── Registro attività ──────────────────────────────────────────
    [HttpGet("Registro")]
    public async Task<IActionResult> Registro(int? idAzienda, CancellationToken ct)
    {
        var q = _db.Audit.AsNoTracking().AsQueryable();
        if (idAzienda is { } a) q = q.Where(x => x.IdAzienda == a);

        var voci = await q.OrderByDescending(x => x.IdAudit).Take(300).ToListAsync(ct);
        var nomiAziende = await _db.Aziende.AsNoTracking().ToDictionaryAsync(x => x.IdAzienda, x => x.RagioneSociale, ct);
        var nomiUtenti = await _db.Utenti.AsNoTracking().ToDictionaryAsync(x => x.IdUtente, x => x.Nome, ct);

        ViewBag.Aziende = nomiAziende.OrderBy(x => x.Value).ToList();
        ViewBag.IdAzienda = idAzienda;
        return View(voci.Select(v => new RigaRegistro
        {
            DataOra = v.DataOra,
            Azienda = v.IdAzienda is { } ia ? nomiAziende.GetValueOrDefault(ia) : null,
            Utente = v.IdUtente is { } iu ? nomiUtenti.GetValueOrDefault(iu) : null,
            Azione = v.Azione,
            Dettaglio = v.Dopo,
            IndirizzoIp = v.IndirizzoIp
        }).ToList());
    }

    // ── Strumenti ──────────────────────────────────────────────────
    private IActionResult RiceviLink(int idAzienda, Utente utente, string codice)
    {
        TempData["LinkInvito"] = $"{Servizi.UrlPubblico.Base(HttpContext)}/Invito/{codice}";
        TempData["LinkInvitoPer"] = $"{utente.Nome} ({utente.Email})";
        return RedirectToAction(nameof(Azienda), new { id = idAzienda });
    }

    private IActionResult Errore(int idAzienda, string testo)
    {
        TempData["Errore"] = testo;
        return RedirectToAction(nameof(Azienda), new { id = idAzienda });
    }

    private string? PrimoErrore() =>
        ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));

    private void Registra(int? idAzienda, string azione, string entita, int idEntita, string? dettaglio) =>
        _db.Audit.Add(new VoceAudit
        {
            IdAzienda = idAzienda,
            IdUtente = _db.IdUtenteCorrente,
            Azione = azione,
            Entita = entita,
            IdEntita = idEntita.ToString(),
            Dopo = dettaglio,
            IndirizzoIp = Ip
        });

    private static string? Pulisci(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();
}
