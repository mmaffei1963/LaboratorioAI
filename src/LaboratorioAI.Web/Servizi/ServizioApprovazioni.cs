using System.Security.Cryptography;
using System.Text.RegularExpressions;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using LaboratorioAI.Documenti;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>Chi esegue l'operazione (utente della Web app oppure il servizio automatico).</summary>
public sealed record OperatoreApprovazioni(int? IdUtente, string? Nome, string? IndirizzoIp, string? UserAgent);

/// <summary>Esito di un'operazione: riuscita o no, con il messaggio per l'utente.</summary>
public sealed record EsitoApprovazioni(bool Riuscito, string Messaggio, int? Id = null)
{
    public static EsitoApprovazioni Errore(string messaggio) => new(false, messaggio);
}

/// <summary>
/// Operazioni sulle approvazioni delle Condizioni condivise tra la pagina «Condizioni e firme», l'invio massivo e il
/// servizio di invio in coda (10/10/2026): designazione del Firmatario, creazione della richiesta, invio del link.
/// </summary>
public class ServizioApprovazioni
{
    /// <summary>Parti iniziali delle e-mail generiche da non usare per il Firmatario (Specifica §2).</summary>
    public static HashSet<string> EmailGeneriche => IndirizziEmail.Generiche;

    public static Regex FormatoEmail => IndirizziEmail.Formato;

    public static bool EmailGenerica(string email) => IndirizziEmail.Generica(email);

    /// <summary>Giorni di validità del link personale (Specifica §4.1).</summary>
    public const int GiorniValiditaLink = 30;

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRegistroEvidenzeApprovazioni _registro;
    private readonly IServizioEmail _email;
    private readonly IGeneratoreOffertaPreventivo _offerta;
    private readonly IWebHostEnvironment _ambiente;
    private readonly IConfiguration _config;

    public ServizioApprovazioni(ContestoLaboratorioAI ctx, IRegistroEvidenzeApprovazioni registro, IServizioEmail email,
        IGeneratoreOffertaPreventivo offerta, IWebHostEnvironment ambiente, IConfiguration config)
    {
        _ctx = ctx; _registro = registro; _email = email; _offerta = offerta; _ambiente = ambiente; _config = config;
    }

    /// <summary>Indirizzo pubblico del Portale clienti per il link personale (configurabile: Condizioni:UrlPortale).</summary>
    /// <summary>
    /// Indirizzi a cui arrivano le risposte dei clienti alle e-mail delle Condizioni (Reply-To), separati da «;»:
    /// Condizioni:RispondiA in appsettings, altrimenti quelli decisi il 10/10/2026.
    /// </summary>
    public static IReadOnlyList<string> RispondiA(IConfiguration config) =>
        (config["Condizioni:RispondiA"] is { Length: > 0 } v ? v : "refertazione@mare-a.com")
            .Split(';', ',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();

    public string UrlPortale => (_config["Condizioni:UrlPortale"] is { Length: > 0 } u ? u
        : _ambiente.IsDevelopment() ? "http://localhost:5081" : "https://portale.mare-a.com").TrimEnd('/');

    private Task<EventoApprovazione> RegistraAsync(OperatoreApprovazioni op, string tipo, int? idRichiesta, int? idCliente, object? dati, CancellationToken ct) =>
        _registro.AggiungiAsync(tipo, idRichiesta, idCliente, dati, op.Nome, op.IndirizzoIp, op.UserAgent, ct);

    /// <summary>Revisione pubblicata più recente (quella su cui si chiedono le approvazioni).</summary>
    public Task<RevisioneCondizioni?> RevisioneCorrenteAsync(CancellationToken ct) =>
        _ctx.RevisioniCondizioni.AsNoTracking().Where(x => x.Attivo && x.Stato == StatoRevisioneCondizioni.Pubblicata)
            .OrderByDescending(x => x.NumeroRevisione).FirstOrDefaultAsync(ct);

    // ── Firmatario ────────────────────────────────────────────────

    /// <summary>
    /// Designa il Firmatario del cliente: il precedente diventa non attivo (resta nel registro); le richieste aperte passano
    /// al nuovo e, se il link era già partito, tornano «da inviare» (il vecchio link non vale più). Validazioni a carico del chiamante.
    /// </summary>
    public async Task<(FirmatarioCliente Firmatario, int Riassegnate)> DesignaFirmatarioAsync(int idCliente, string? ragioneSociale, string? partitaIva,
        string nome, string cognome, string? qualifica, string email, string? codiceFiscale, string? note, OperatoreApprovazioni op, CancellationToken ct,
        int? idInvitoDaNonAnnullare = null)
    {
        var ora = DateTime.UtcNow;
        var precedenti = await _ctx.FirmatariClienti.Where(x => x.IdClienteArcLab == idCliente && x.Attivo).ToListAsync(ct);
        foreach (var p in precedenti) { p.Attivo = false; p.DataCancellazione = ora; }

        var f = new FirmatarioCliente
        {
            IdClienteArcLab = idCliente,
            ClienteRagioneSociale = ragioneSociale?.Trim(),
            ClientePartitaIva = partitaIva?.Trim(),
            Nome = nome.Trim(), Cognome = cognome.Trim(),
            Qualifica = string.IsNullOrWhiteSpace(qualifica) ? null : qualifica.Trim(),
            Email = email.Trim(),
            CodiceFiscale = string.IsNullOrWhiteSpace(codiceFiscale) ? null : codiceFiscale.Trim().ToUpperInvariant(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            DataDesignazione = ora,
            IdUtenteDesignazione = op.IdUtente,
            NomeUtenteDesignazione = op.Nome
        };
        _ctx.FirmatariClienti.Add(f);

        var aperte = await _ctx.RichiesteApprovazione.Where(x => x.IdClienteArcLab == idCliente
            && (x.Stato == StatoRichiestaApprovazione.DaInviare || x.Stato == StatoRichiestaApprovazione.Inviata || x.Stato == StatoRichiestaApprovazione.Aperta))
            .ToListAsync(ct);
        foreach (var a in aperte)
        {
            a.Firmatario = f;
            if (a.Stato != StatoRichiestaApprovazione.DaInviare)
            {
                a.Stato = StatoRichiestaApprovazione.DaInviare;
                a.ImprontaCodiceLink = null;
                a.ScadenzaLink = null;
            }
            a.DataUltimaModifica = ora;
        }

        // Inviti a indicare il Firmatario ancora aperti per questo cliente (11/10/2026): non servono più.
        var inviti = await _ctx.InvitiFirmatario.Where(x => x.IdClienteArcLab == idCliente && x.IdInvitoFirmatario != idInvitoDaNonAnnullare
                && (x.Stato == StatoInvitoFirmatario.DaInviare || x.Stato == StatoInvitoFirmatario.Inviato || x.Stato == StatoInvitoFirmatario.Aperto))
            .ToListAsync(ct);
        foreach (var i in inviti)
        {
            i.Stato = StatoInvitoFirmatario.Annullato; i.InvioAccodato = null; i.ImprontaCodiceLink = null; i.ScadenzaLink = null;
            i.Note = "Annullato: Firmatario designato da " + (op.Nome ?? "LaboratorioAI"); i.DataUltimaModifica = ora;
        }

        await RegistraAsync(op, "firmatario.designato", null, idCliente, new
        {
            f.ClienteRagioneSociale, f.ClientePartitaIva, f.Nome, f.Cognome, f.Qualifica, f.Email, f.CodiceFiscale,
            Sostituisce = precedenti.Select(p => new { p.IdFirmatarioCliente, p.Nome, p.Cognome, p.Email }),
            RichiesteRiassegnate = aperte.Select(a => a.IdRichiestaApprovazione),
            InvitiAnnullati = inviti.Select(i => i.IdInvitoFirmatario)
        }, ct);
        return (f, aperte.Count);
    }

    // ── Richiesta ─────────────────────────────────────────────────

    public async Task<EsitoApprovazioni> CreaRichiestaAsync(int idCliente, int? idRevisione, int? idPreventivo, string? note,
        OperatoreApprovazioni op, CancellationToken ct)
    {
        var revisione = idRevisione is int idr
            ? await _ctx.RevisioniCondizioni.AsNoTracking().FirstOrDefaultAsync(x => x.IdRevisioneCondizioni == idr && x.Attivo, ct)
            : await RevisioneCorrenteAsync(ct);
        if (revisione is null || revisione.Stato != StatoRevisioneCondizioni.Pubblicata)
            return EsitoApprovazioni.Errore("Non c'è una revisione delle Condizioni pubblicata: va pubblicata prima di chiedere le approvazioni.");

        var firmatario = await _ctx.FirmatariClienti.AsNoTracking().FirstOrDefaultAsync(x => x.IdClienteArcLab == idCliente && x.Attivo, ct);
        if (firmatario is null) return EsitoApprovazioni.Errore("Designa prima il Firmatario del cliente.");

        var esistente = await _ctx.RichiesteApprovazione.AsNoTracking().Where(x => x.IdClienteArcLab == idCliente
                && x.IdRevisioneCondizioni == revisione.IdRevisioneCondizioni
                && x.Stato != StatoRichiestaApprovazione.Annullata && x.Stato != StatoRichiestaApprovazione.Rifiutata)
            .Select(x => new { x.IdRichiestaApprovazione, x.Stato }).FirstOrDefaultAsync(ct);
        if (esistente is not null)
            return new EsitoApprovazioni(false, esistente.Stato == StatoRichiestaApprovazione.Approvata
                ? $"Il cliente ha già approvato {revisione.Codice}."
                : $"C'è già una richiesta aperta per {revisione.Codice} (stato: {esistente.Stato}).", esistente.IdRichiestaApprovazione);

        if (idPreventivo is int idp && !await _ctx.Preventivi.AnyAsync(p => p.IdPreventivo == idp && p.IdClienteArcLab == idCliente && p.Attivo, ct))
            return EsitoApprovazioni.Errore("L'Offerta indicata non è di questo cliente.");

        var ora = DateTime.UtcNow;
        var r = new RichiestaApprovazione
        {
            IdClienteArcLab = idCliente,
            ClienteRagioneSociale = firmatario.ClienteRagioneSociale,
            ClientePartitaIva = firmatario.ClientePartitaIva,
            IdRevisioneCondizioni = revisione.IdRevisioneCondizioni,
            IdPreventivo = idPreventivo,
            IdFirmatarioCliente = firmatario.IdFirmatarioCliente,
            Stato = StatoRichiestaApprovazione.DaInviare,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            IdUtenteCreazione = op.IdUtente, NomeUtenteCreazione = op.Nome,
            DataCreazione = ora, DataUltimaModifica = ora
        };
        _ctx.RichiesteApprovazione.Add(r);
        await _ctx.SaveChangesAsync(ct);   // serve l'Id per il registro
        await RegistraAsync(op, "richiesta.creata", r.IdRichiestaApprovazione, r.IdClienteArcLab, new
        {
            Revisione = revisione.Codice, revisione.ImprontaPdf, r.IdPreventivo,
            Firmatario = new { firmatario.IdFirmatarioCliente, firmatario.Nome, firmatario.Cognome, firmatario.Email }
        }, ct);
        return new EsitoApprovazioni(true, $"Richiesta creata per {r.ClienteRagioneSociale}: {revisione.Codice}.", r.IdRichiestaApprovazione);
    }

    // ── Invio del link ────────────────────────────────────────────

    /// <summary>
    /// Invia (o reinvia) al Firmatario il link personale alla pagina di approvazione. Il codice è casuale (256 bit) e compare
    /// solo nell'e-mail; nel database resta l'impronta. Un nuovo invio annulla il link precedente. Al primo invio con Offerta
    /// se ne congela il PDF.
    /// </summary>
    public async Task<EsitoApprovazioni> InviaLinkAsync(int idRichiesta, OperatoreApprovazioni op, CancellationToken ct, bool promemoria = false)
    {
        var r = await _ctx.RichiesteApprovazione.Include(x => x.Revisione).Include(x => x.Firmatario)
            .FirstOrDefaultAsync(x => x.IdRichiestaApprovazione == idRichiesta, ct);
        if (r is null) return EsitoApprovazioni.Errore("Richiesta non trovata.");
        if (r.Stato is not (StatoRichiestaApprovazione.DaInviare or StatoRichiestaApprovazione.Inviata or StatoRichiestaApprovazione.Aperta))
            return EsitoApprovazioni.Errore($"La richiesta è «{r.Stato}»: il link non si invia.");
        if (r.Firmatario is null || !r.Firmatario.Attivo)
            return EsitoApprovazioni.Errore("Il Firmatario della richiesta non è più attivo: designa il Firmatario del cliente e riprova.");
        if (r.Revisione is null || r.Revisione.Stato != StatoRevisioneCondizioni.Pubblicata || !r.Revisione.Attivo)
            return EsitoApprovazioni.Errore("La revisione delle Condizioni della richiesta non è pubblicata.");

        if (r.IdPreventivo is int idp && r.OffertaPdf is null)
        {
            var p = await _ctx.Preventivi.AsNoTracking().Include(x => x.Righe).ThenInclude(x => x.Metodi)
                .FirstOrDefaultAsync(x => x.IdPreventivo == idp, ct);
            if (p is null) return EsitoApprovazioni.Errore("L'Offerta collegata non esiste più.");
            r.OffertaPdf = _offerta.GeneraPdf(p, Path.Combine(_ambiente.WebRootPath, "images/preventivo"));
            r.OffertaImpronta = CatenaEvidenze.Sha256(r.OffertaPdf);
            r.OffertaRiferimento = (p.Numero is int n ? $"{p.Anno}/{n:000}" : $"bozza {p.IdPreventivo}")
                + (p.DataOfferta is DateTime d ? $" del {d:dd/MM/yyyy}" : "");
        }

        var codice = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var link = $"{UrlPortale}/Firma/{codice}";
        var scadenza = DateTime.UtcNow.AddDays(GiorniValiditaLink);
        var f = r.Firmatario;
        var oggetto = (promemoria ? "Promemoria – " : "") + $"MARE.A – Condizioni Generali di Fornitura {r.Revisione.Codice}: approvazione richiesta"
            + (r.OffertaRiferimento is null ? "" : $" (e Offerta n. {r.OffertaRiferimento})");

        string? messageId;
        try
        {
            messageId = await _email.InviaConAllegatiAsync(new[] { f.Email }, oggetto, CorpoRichiesta(f, r, link, scadenza), Array.Empty<AllegatoEmail>(), ct, RispondiA(_config));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EsitoApprovazioni.Errore("E-mail non inviata: " + ex.Message);
        }

        var reinvio = r.Stato != StatoRichiestaApprovazione.DaInviare;
        r.ImprontaCodiceLink = CatenaEvidenze.Sha256(codice);
        r.ScadenzaLink = scadenza;
        r.Stato = StatoRichiestaApprovazione.Inviata;
        var ora = DateTime.UtcNow;
        if (promemoria) { r.NumeroPromemoria++; r.DataUltimoPromemoria = ora; }   // 11/10/2026: promemoria automatici
        else r.DataInvio = ora;
        r.InvioAccodato = null;
        r.DataUltimaModifica = ora;
        await RegistraAsync(op, promemoria ? "richiesta.promemoria" : reinvio ? "richiesta.reinviata" : "richiesta.inviata", r.IdRichiestaApprovazione, r.IdClienteArcLab, new
        {
            r.NumeroPromemoria,
            Destinatario = f.Email, Oggetto = oggetto, MessageId = messageId,
            ScadenzaLink = scadenza.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Offerta = r.OffertaRiferimento, r.OffertaImpronta
        }, ct);
        return new EsitoApprovazioni(true,
            $"Link {(reinvio ? "reinviato" : "inviato")} a {f.Nome} {f.Cognome} ({f.Email}); valido fino al {scadenza.ToLocalTime():dd/MM/yyyy}.",
            r.IdRichiestaApprovazione);
    }

    private static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string CorpoRichiesta(FirmatarioCliente f, RichiestaApprovazione r, string link, DateTime scadenza) => $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1a1a1a;max-width:640px">
        <p>Gentile {Html(f.Nome)} {Html(f.Cognome)},</p>
        <p>le chiediamo di approvare, per <strong>{Html(r.ClienteRagioneSociale)}</strong>, le Condizioni Generali di Fornitura
        del Laboratorio MARE.A <strong>{Html(r.Revisione!.Codice)}</strong>{(r.OffertaRiferimento is null ? "" : $" e di accettare l'Offerta n. <strong>{Html(r.OffertaRiferimento)}</strong>")}.
        Si fa una volta per revisione, in pochi minuti, senza stampare né firmare a mano.</p>
        <p style="margin:24px 0"><a href="{link}" style="background:#C00000;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:bold">Apri la pagina di approvazione</a></p>
        <p style="background:#F4F4F4;border-radius:6px;padding:10px 12px">Se il pulsante non si apre, clicchi su questo indirizzo (o lo copi nel browser):<br>
        <span style="font-size:14px;color:#1a3e8c;word-break:break-all">{link}</span></p>
        <p>Il link è personale e vale fino al {scadenza.ToLocalTime():dd/MM/yyyy}: non lo inoltri ad altri.</p>
        <p>Per qualsiasi dubbio può rispondere a questa e-mail o chiamarci allo 0541 830442.</p>
        <p>Cordiali saluti<br>MARE.A s.r.l. – Laboratorio di analisi<br>Via E. Toti, 2 – 47841 Cattolica (RN)</p>
        </div>
        """;
}
