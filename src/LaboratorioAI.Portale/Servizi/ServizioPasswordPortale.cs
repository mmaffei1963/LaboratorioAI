using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Servizi;

/// <summary>
/// Password del portale clienti (17/11/2026, claude/definizione-password-portale.md): cambio dall'area riservata,
/// recupero con codice inviato all'email del cliente in ArcLab, ripristino della partita IVA da parte dell'operatore.
/// Le password personali e i codici si conservano solo come impronte nel database LaboratorioAI; ArcLab in sola lettura.
/// </summary>
public class ServizioPasswordPortale
{
    /// <summary>Esito delle operazioni: messaggio d'errore per l'utente, null se riuscita.</summary>
    public record Esito(bool Riuscito, string? Messaggio);

    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object Soggetto = new();

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryCredenzialiClienti _credenziali;
    private readonly IRepositoryPortale _portale;
    private readonly IServizioEmail _email;
    private readonly ILogger<ServizioPasswordPortale> _log;

    public ServizioPasswordPortale(ContestoLaboratorioAI ctx, IRepositoryCredenzialiClienti credenziali, IRepositoryPortale portale,
        IServizioEmail email, ILogger<ServizioPasswordPortale> log)
    {
        _ctx = ctx;
        _credenziali = credenziali;
        _portale = portale;
        _email = email;
        _log = log;
    }

    // ── Impronte ───────────────────────────────────────────────

    public static string Impronta(string password) => Hasher.HashPassword(Soggetto, password);

    public static bool Verifica(string impronta, string password) =>
        Hasher.VerifyHashedPassword(Soggetto, impronta, password) != PasswordVerificationResult.Failed;

    /// <summary>Impronta della password personale attiva del cliente, o null se entra ancora con la partita IVA.</summary>
    public static Task<string?> ImprontaAttivaAsync(ContestoLaboratorioAI ctx, int idCliente, CancellationToken ct) =>
        ctx.Set<PasswordCliente>().Where(p => p.IdClienteArcLab == idCliente && !p.Annullata)
            .OrderByDescending(p => p.IdPasswordCliente).Select(p => (string?)p.ImprontaPassword).FirstOrDefaultAsync(ct);

    // ── Stato (operatore) ──────────────────────────────────────

    public async Task<(bool Personale, DateTime? Dal)> StatoAsync(int idCliente, CancellationToken ct)
    {
        var p = await _ctx.Set<PasswordCliente>().Where(x => x.IdClienteArcLab == idCliente && !x.Annullata)
            .OrderByDescending(x => x.IdPasswordCliente).FirstOrDefaultAsync(ct);
        return (p is not null, p?.DataImpostazioneUtc);
    }

    // ── Cambio dall'area riservata ─────────────────────────────

    public async Task<Esito> CambiaAsync(int idCliente, string? attuale, string? nuova, string? ripetuta, string? ip, string? ua, CancellationToken ct)
    {
        var dati = await _credenziali.DatiAsync(idCliente, ct);
        if (dati is null) return new(false, "Cliente non trovato.");

        var impronta = await ImprontaAttivaAsync(_ctx, idCliente, ct);
        var attualeGiusta = !string.IsNullOrEmpty(attuale) && (impronta is not null
            ? Verifica(impronta, attuale)
            : !string.IsNullOrWhiteSpace(dati.PartitaIva) && string.Equals(attuale.Trim(), dati.PartitaIva.Trim(), StringComparison.OrdinalIgnoreCase));
        if (!attualeGiusta)
        {
            await EventoAsync(idCliente, dati.CodiceContabilita, "cambio.attuale-errata", ip, ua, null, ct);
            return new(false, "La password attuale non è corretta.");
        }

        if (RegolePasswordPortale.Valida(nuova, ripetuta, dati.PartitaIva, dati.CodiceContabilita) is string errore)
            return new(false, errore);

        await ImpostaAsync(idCliente, nuova!, "cambio", ct);
        await EventoAsync(idCliente, dati.CodiceContabilita, "cambio", ip, ua, null, ct);
        return new(true, "Password cambiata. Dal prossimo accesso usa la password nuova.");
    }

    // ── Recupero con codice via email ──────────────────────────

    /// <summary>
    /// Invia il codice all'email del cliente in ArcLab. La risposta all'utente è sempre la stessa (anche per codici
    /// inesistenti o senza email), per non rivelare quali codici cliente sono validi.
    /// </summary>
    public async Task RichiediCodiceAsync(string? codiceCliente, string? ip, string? ua, CancellationToken ct)
    {
        var codice = (codiceCliente ?? "").Trim().ToUpperInvariant();
        if (codice.Length == 0) return;
        var adesso = DateTime.UtcNow;

        if (!string.IsNullOrEmpty(ip) &&
            await _ctx.Set<CodiceRecuperoPassword>().CountAsync(c => c.IndirizzoIp == ip && c.DataCreazioneUtc > adesso.AddHours(-1), ct)
                >= RegolePasswordPortale.InviiMassimiOraPerIndirizzo)
        {
            await EventoAsync(null, codice, "recupero.limite-indirizzo", ip, ua, null, ct);
            return;
        }

        var (cliente, _) = await _portale.CercaPerAccessoAsync(codice, ct);
        if (cliente is null) { await EventoAsync(null, codice, "recupero.codice-inesistente", ip, ua, null, ct); return; }

        var precedenti = await _ctx.Set<CodiceRecuperoPassword>()
            .Where(c => c.IdClienteArcLab == cliente.IdCliente && c.DataCreazioneUtc > adesso.AddHours(-1))
            .Select(c => c.DataCreazioneUtc).ToListAsync(ct);
        if (precedenti.Count >= RegolePasswordPortale.InviiMassimiOraPerCliente ||
            precedenti.Any(d => (adesso - d).TotalSeconds < RegolePasswordPortale.AttesaReinvioSecondi))
        {
            await EventoAsync(cliente.IdCliente, codice, "recupero.limite-cliente", ip, ua, null, ct);
            return;
        }

        var dati = await _credenziali.DatiAsync(cliente.IdCliente, ct);
        var indirizzi = dati?.IndirizziEmail ?? [];
        if (indirizzi.Count == 0) { await EventoAsync(cliente.IdCliente, codice, "recupero.senza-email", ip, ua, null, ct); return; }

        var otp = CodiceOtp.Genera();
        var sale = CodiceOtp.Sale();
        _ctx.Set<CodiceRecuperoPassword>().Add(new CodiceRecuperoPassword
        {
            IdClienteArcLab = cliente.IdCliente,
            Sale = sale,
            ImprontaCodice = CodiceOtp.Impronta(otp, sale, cliente.IdCliente),
            DataCreazioneUtc = adesso,
            ScadenzaUtc = adesso.AddMinutes(RegolePasswordPortale.ValiditaCodiceMinuti),
            IndirizzoIp = ip
        });
        await _ctx.SaveChangesAsync(ct);

        var corpo = $"""
            <p>Gentile cliente,</p>
            <p>è stato richiesto il recupero della password dell'Area Clienti MARE.A per il codice cliente <strong>{System.Net.WebUtility.HtmlEncode(codice)}</strong>.</p>
            <p>Il codice da inserire è: <strong style="font-size:1.3em;letter-spacing:.15em">{otp}</strong></p>
            <p>Il codice vale {RegolePasswordPortale.ValiditaCodiceMinuti} minuti. Se non sei stato tu a chiederlo, ignora questo messaggio: la password non cambia.</p>
            <p>MARE.A srl — Via E. Toti, 2 — 47841 Cattolica (RN)</p>
            """;
        try
        {
            await _email.InviaAsync(indirizzi, "Area Clienti MARE.A — codice per il recupero della password", corpo, ct);
            await EventoAsync(cliente.IdCliente, codice, "recupero.codice-inviato", ip, ua,
                string.Join(", ", indirizzi.Select(CodiceOtp.Maschera)), ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Invio del codice di recupero password non riuscito per il cliente {IdCliente}", cliente.IdCliente);
            await EventoAsync(cliente.IdCliente, codice, "recupero.invio-fallito", ip, ua, ex.GetType().Name, ct);
        }
    }

    public async Task<Esito> ConfermaRecuperoAsync(string? codiceCliente, string? codiceOtp, string? nuova, string? ripetuta,
        string? ip, string? ua, CancellationToken ct)
    {
        const string NonValido = "Codice non valido o scaduto: chiedine uno nuovo.";
        var codice = (codiceCliente ?? "").Trim().ToUpperInvariant();
        var (cliente, _) = await _portale.CercaPerAccessoAsync(codice, ct);
        if (cliente is null) return new(false, NonValido);

        var adesso = DateTime.UtcNow;
        var r = await _ctx.Set<CodiceRecuperoPassword>()
            .Where(c => c.IdClienteArcLab == cliente.IdCliente && !c.Usato)
            .OrderByDescending(c => c.IdCodiceRecuperoPassword).FirstOrDefaultAsync(ct);
        if (r is null || r.ScadenzaUtc < adesso || r.Tentativi >= RegolePasswordPortale.TentativiMassimiCodice)
            return new(false, NonValido);

        if (!CodiceOtp.Corrisponde(codiceOtp ?? "", r.Sale, cliente.IdCliente, r.ImprontaCodice))
        {
            r.Tentativi++;
            await _ctx.SaveChangesAsync(ct);
            await EventoAsync(cliente.IdCliente, codice, "recupero.codice-errato", ip, ua, $"tentativo {r.Tentativi}", ct);
            return new(false, r.Tentativi >= RegolePasswordPortale.TentativiMassimiCodice ? NonValido : "Codice non corretto.");
        }

        var dati = await _credenziali.DatiAsync(cliente.IdCliente, ct);
        if (RegolePasswordPortale.Valida(nuova, ripetuta, dati?.PartitaIva, dati?.CodiceContabilita ?? codice) is string errore)
            return new(false, errore);   // il codice resta valido: si può correggere la password

        r.Usato = true;
        await ImpostaAsync(cliente.IdCliente, nuova!, "recupero", ct);
        await EventoAsync(cliente.IdCliente, codice, "recupero", ip, ua, null, ct);
        return new(true, "Password impostata. Ora puoi accedere con la password nuova.");
    }

    // ── Ripristino da parte dell'operatore ─────────────────────

    public async Task<Esito> RipristinaAsync(int idCliente, string? ip, string? ua, CancellationToken ct)
    {
        var attive = await _ctx.Set<PasswordCliente>().Where(p => p.IdClienteArcLab == idCliente && !p.Annullata).ToListAsync(ct);
        if (attive.Count == 0) return new(false, "Il cliente entra già con la partita IVA: non c'è niente da ripristinare.");
        foreach (var p in attive) { p.Annullata = true; p.DataAnnullamentoUtc = DateTime.UtcNow; p.MotivoAnnullamento = "ripristino operatore"; }
        // 18/11/2026: con la partita IVA tornano a valere le verifiche: i browser riconosciuti si revocano.
        foreach (var d in await _ctx.Set<DispositivoFidatoPortale>().Where(d => d.IdClienteArcLab == idCliente && !d.Revocato).ToListAsync(ct))
        { d.Revocato = true; d.DataRevocaUtc = DateTime.UtcNow; }
        await _ctx.SaveChangesAsync(ct);
        var dati = await _credenziali.DatiAsync(idCliente, ct);
        await EventoAsync(idCliente, dati?.CodiceContabilita, "ripristino-operatore", ip, ua, null, ct);
        return new(true, "Credenziali originali ripristinate: il cliente entra di nuovo con la partita IVA.");
    }

    // ── Interni ────────────────────────────────────────────────

    private async Task ImpostaAsync(int idCliente, string password, string origine, CancellationToken ct)
    {
        var adesso = DateTime.UtcNow;
        foreach (var p in await _ctx.Set<PasswordCliente>().Where(p => p.IdClienteArcLab == idCliente && !p.Annullata).ToListAsync(ct))
        { p.Annullata = true; p.DataAnnullamentoUtc = adesso; p.MotivoAnnullamento = "sostituita"; }
        _ctx.Set<PasswordCliente>().Add(new PasswordCliente
        {
            IdClienteArcLab = idCliente, ImprontaPassword = Impronta(password), Origine = origine, DataImpostazioneUtc = adesso
        });
        await _ctx.SaveChangesAsync(ct);
    }

    private async Task EventoAsync(int? idCliente, string? codice, string evento, string? ip, string? ua, string? dettaglio, CancellationToken ct)
    {
        _ctx.Set<EventoPasswordCliente>().Add(new EventoPasswordCliente
        {
            IdClienteArcLab = idCliente, CodiceContabilita = codice?.Length > 50 ? codice[..50] : codice, Evento = evento,
            DataUtc = DateTime.UtcNow, IndirizzoIp = ip, UserAgent = ua?.Length > 500 ? ua[..500] : ua,
            Dettaglio = dettaglio?.Length > 500 ? dettaglio[..500] : dettaglio
        });
        await _ctx.SaveChangesAsync(ct);
    }
}
