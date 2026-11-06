using System.Security.Cryptography;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <summary>
/// Inviti al cliente a indicare il Firmatario (11/10/2026). Ai clienti con campioni negli ultimi 12 mesi che non hanno un
/// Firmatario e non hanno ancora approvato la revisione in vigore si scrive all'indirizzo aziendale dell'anagrafica ArcLab
/// (Clienti.Email, sola lettura); il link porta a una pagina del Portale dove si indicano nome, cognome, qualifica ed
/// e-mail nominativa. Alla risposta (vale la prima) LaboratorioAI designa il Firmatario e gli invia il link di
/// approvazione. Promemoria automatici a 7 e 14 giorni. Ogni passo va nel registro delle evidenze.
/// </summary>
public class ServizioInvitiFirmatario
{
    public const int GiorniValiditaLink = 30;

    private readonly ContestoLaboratorioAI _ctx;
    private readonly IRepositoryAnagrafiche _anagrafiche;
    private readonly IRegistroEvidenzeApprovazioni _registro;
    private readonly IServizioEmail _email;
    private readonly ServizioApprovazioni _approvazioni;
    private readonly IConfiguration _config;

    public ServizioInvitiFirmatario(ContestoLaboratorioAI ctx, IRepositoryAnagrafiche anagrafiche, IRegistroEvidenzeApprovazioni registro,
        IServizioEmail email, ServizioApprovazioni approvazioni, IConfiguration config)
    {
        _ctx = ctx; _anagrafiche = anagrafiche; _registro = registro; _email = email; _approvazioni = approvazioni; _config = config;
    }

    /// <summary>Stati in cui l'invito aspetta ancora la risposta del cliente.</summary>
    public static readonly StatoInvitoFirmatario[] Aperti =
        { StatoInvitoFirmatario.DaInviare, StatoInvitoFirmatario.Inviato, StatoInvitoFirmatario.Aperto };

    /// <summary>Giorni dopo l'ultimo invio a cui parte un promemoria (Condizioni:PromemoriaGiorni, predefinito «7,14»).</summary>
    public IReadOnlyList<int> GiorniPromemoria =>
        (_config["Condizioni:PromemoriaGiorni"] ?? "7,14").Split(',', ';').Select(x => int.TryParse(x.Trim(), out var n) ? n : 0)
            .Where(n => n > 0).OrderBy(n => n).ToList();

    private Task<EventoApprovazione> RegistraAsync(OperatoreApprovazioni op, string tipo, int idCliente, object? dati, CancellationToken ct) =>
        _registro.AggiungiAsync(tipo, null, idCliente, dati, op.Nome, op.IndirizzoIp, op.UserAgent, ct);

    // ═══ Situazione ═══

    public sealed record RigaSituazione(int IdCliente, string? RagioneSociale, string? PartitaIva, string? EmailAziendale,
        int NumeroCampioni, DateTime? UltimoCampione, string Situazione, string? Dettaglio);

    /// <summary>
    /// Situazione di ogni cliente con campioni negli ultimi 12 mesi rispetto alla revisione in vigore: già approvato,
    /// richiesta in corso, Firmatario designato senza richiesta, invito in corso o risposto, da invitare, senza e-mail.
    /// </summary>
    public async Task<(RevisioneCondizioni? Revisione, List<RigaSituazione> Righe)> SituazioneAsync(CancellationToken ct)
    {
        var revisione = await _approvazioni.RevisioneCorrenteAsync(ct);
        var clienti = await _anagrafiche.ClientiConCampioniAsync(DateTime.Today.AddMonths(-12), ct);
        var firmatari = (await _ctx.FirmatariClienti.AsNoTracking().Where(x => x.Attivo).ToListAsync(ct))
            .GroupBy(x => x.IdClienteArcLab).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataDesignazione).First());
        var richieste = revisione is null ? new() : (await _ctx.RichiesteApprovazione.AsNoTracking()
                .Where(x => x.IdRevisioneCondizioni == revisione.IdRevisioneCondizioni
                    && x.Stato != StatoRichiestaApprovazione.Annullata && x.Stato != StatoRichiestaApprovazione.Rifiutata)
                .Select(x => new { x.IdClienteArcLab, x.Stato }).ToListAsync(ct))
            .GroupBy(x => x.IdClienteArcLab).ToDictionary(g => g.Key, g => g.Min(x => x.Stato == StatoRichiestaApprovazione.Approvata ? 0 : 1));
        var inviti = (await _ctx.InvitiFirmatario.AsNoTracking().Where(x => x.Stato != StatoInvitoFirmatario.Annullato).ToListAsync(ct))
            .GroupBy(x => x.IdClienteArcLab).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.IdInvitoFirmatario).First());

        var righe = new List<RigaSituazione>();
        foreach (var c in clienti)
        {
            RigaSituazione R(string s, string? d = null) =>
                new(c.IdCliente, c.RagioneSociale, c.PartitaIva, c.Email, c.NumeroCampioni, c.UltimoCampione, s, d);
            if (richieste.TryGetValue(c.IdCliente, out var r) && r == 0) { righe.Add(R("approvato")); continue; }
            if (richieste.ContainsKey(c.IdCliente)) { righe.Add(R("richiesta in corso", Firmatario(firmatari, c.IdCliente))); continue; }
            if (firmatari.ContainsKey(c.IdCliente)) { righe.Add(R("firmatario senza richiesta", Firmatario(firmatari, c.IdCliente))); continue; }
            if (inviti.TryGetValue(c.IdCliente, out var i))
            {
                righe.Add(R(i.Stato switch
                {
                    StatoInvitoFirmatario.DaInviare => i.InvioAccodato is null ? "invito da inviare" : "invito in coda",
                    StatoInvitoFirmatario.Inviato => "invito inviato",
                    StatoInvitoFirmatario.Aperto => "invito aperto",
                    _ => "firmatario indicato"
                }, i.Stato >= StatoInvitoFirmatario.Indicato ? $"{i.NomeIndicato} {i.CognomeIndicato} <{i.EmailIndicata}>"
                    : i.ErroreInvio ?? (i.NumeroPromemoria > 0 ? $"{i.NumeroPromemoria} promemoria" : null)));
                continue;
            }
            righe.Add(IndirizziEmail.Elenco(c.Email).Count == 0 ? R("senza e-mail aziendale", c.Email) : R("da invitare"));
        }
        return (revisione, righe);
    }

    private static string Firmatario(Dictionary<int, FirmatarioCliente> f, int id) =>
        f.TryGetValue(id, out var x) ? $"{x.Nome} {x.Cognome} <{x.Email}>" : "";

    // ═══ Preparazione ═══

    /// <summary>Crea gli inviti «da inviare» per i clienti da invitare. Non invia nulla.</summary>
    public async Task<(int Creati, int SenzaEmail, string? Errore)> PreparaAsync(OperatoreApprovazioni op, CancellationToken ct)
    {
        var (revisione, righe) = await SituazioneAsync(ct);
        if (revisione is null) return (0, 0, "Non c'è una revisione delle Condizioni pubblicata: va pubblicata prima di invitare i clienti.");
        var ora = DateTime.UtcNow;
        var creati = 0;
        foreach (var r in righe.Where(x => x.Situazione == "da invitare"))
        {
            _ctx.InvitiFirmatario.Add(new InvitoFirmatario
            {
                IdClienteArcLab = r.IdCliente, ClienteRagioneSociale = r.RagioneSociale?.Trim(), ClientePartitaIva = r.PartitaIva?.Trim(),
                EmailDestinatari = string.Join("; ", IndirizziEmail.Elenco(r.EmailAziendale)),
                IdRevisioneCondizioni = revisione.IdRevisioneCondizioni, Stato = StatoInvitoFirmatario.DaInviare,
                IdUtenteCreazione = op.IdUtente, NomeUtenteCreazione = op.Nome, DataCreazione = ora, DataUltimaModifica = ora
            });
            creati++;
        }
        await _ctx.SaveChangesAsync(ct);
        return (creati, righe.Count(x => x.Situazione == "senza e-mail aziendale"), null);
    }

    // ═══ Invio ═══

    /// <summary>Invia (o rinnova con un promemoria) l'invito: nuovo codice, il link precedente non vale più.</summary>
    public async Task<EsitoApprovazioni> InviaAsync(int idInvito, bool promemoria, OperatoreApprovazioni op, CancellationToken ct)
    {
        var i = await _ctx.InvitiFirmatario.FirstOrDefaultAsync(x => x.IdInvitoFirmatario == idInvito, ct);
        if (i is null) return EsitoApprovazioni.Errore("Invito non trovato.");
        if (!Aperti.Contains(i.Stato)) return EsitoApprovazioni.Errore($"L'invito è «{i.Stato}»: non si invia.");
        var destinatari = IndirizziEmail.Elenco(i.EmailDestinatari);
        if (destinatari.Count == 0) return EsitoApprovazioni.Errore("Nessun indirizzo e-mail valido.");

        var codice = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var link = $"{_approvazioni.UrlPortale}/Indica/{codice}";
        var scadenza = DateTime.UtcNow.AddDays(GiorniValiditaLink);
        var oggetto = (promemoria ? "Promemoria – " : "") + "MARE.A – Condizioni Generali di Fornitura: indicateci a chi inviarle per l'approvazione";

        // Prova (11/10/2026): con Condizioni:EmailDiProva impostato (solo in sviluppo) gli inviti vanno a quell'indirizzo,
        // non ai clienti; l'oggetto dice a chi sarebbero andati.
        var prova = IndirizziEmail.Elenco(_config["Condizioni:EmailDiProva"]);
        var effettivi = prova.Count > 0 ? prova : destinatari;
        if (prova.Count > 0) oggetto = $"[PROVA – per {string.Join(", ", destinatari)}] " + oggetto;

        string? messageId;
        try
        {
            messageId = await _email.InviaConAllegatiAsync(effettivi, oggetto, Corpo(i, link, scadenza, promemoria),
                Array.Empty<AllegatoEmail>(), ct, ServizioApprovazioni.RispondiA(_config));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EsitoApprovazioni.Errore("E-mail non inviata: " + ex.Message);
        }

        var ora = DateTime.UtcNow;
        i.ImprontaCodiceLink = CatenaEvidenze.Sha256(codice);
        i.ScadenzaLink = scadenza;
        if (i.Stato == StatoInvitoFirmatario.DaInviare) { i.Stato = StatoInvitoFirmatario.Inviato; i.DataInvio = ora; }
        if (promemoria) { i.NumeroPromemoria++; i.DataUltimoPromemoria = ora; }
        i.InvioAccodato = null; i.ErroreInvio = null; i.DataUltimaModifica = ora;
        await RegistraAsync(op, promemoria ? "invito.promemoria" : "invito.inviato", i.IdClienteArcLab, new
        {
            i.IdInvitoFirmatario, i.ClienteRagioneSociale, Destinatari = destinatari, InviatoA = effettivi, Oggetto = oggetto, MessageId = messageId,
            ScadenzaLink = scadenza.ToString("yyyy-MM-ddTHH:mm:ssZ"), i.NumeroPromemoria
        }, ct);
        return new EsitoApprovazioni(true, prova.Count > 0
            ? $"PROVA: invito per {string.Join(", ", destinatari)} inviato a {string.Join(", ", effettivi)}."
            : $"Invito inviato a {string.Join(", ", destinatari)}.", null);
    }

    // ═══ Elaborazione delle risposte ═══

    /// <summary>
    /// Risposta del cliente: designa il Firmatario indicato, crea la richiesta sulla revisione in vigore e gli invia subito
    /// il link di approvazione (non conta nel tetto giornaliero: chi ha appena risposto lo aspetta).
    /// </summary>
    public async Task<EsitoApprovazioni> ElaboraAsync(int idInvito, OperatoreApprovazioni op, CancellationToken ct)
    {
        var i = await _ctx.InvitiFirmatario.FirstOrDefaultAsync(x => x.IdInvitoFirmatario == idInvito, ct);
        if (i is null || i.Stato != StatoInvitoFirmatario.Indicato) return EsitoApprovazioni.Errore("Niente da elaborare.");

        var (f, _) = await _approvazioni.DesignaFirmatarioAsync(i.IdClienteArcLab, i.ClienteRagioneSociale, i.ClientePartitaIva,
            i.NomeIndicato!, i.CognomeIndicato!, i.QualificaIndicata, i.EmailIndicata!, null,
            $"Indicato dal cliente (invito n. {i.IdInvitoFirmatario})", op, ct, idInvitoDaNonAnnullare: i.IdInvitoFirmatario);
        await _ctx.SaveChangesAsync(ct);

        var crea = await _approvazioni.CreaRichiestaAsync(i.IdClienteArcLab, null, null, "Firmatario indicato dal cliente", op, ct);
        var idRichiesta = crea.Id;
        if (idRichiesta is null) return EsitoApprovazioni.Errore(crea.Messaggio);

        var stato = await _ctx.RichiesteApprovazione.Where(x => x.IdRichiestaApprovazione == idRichiesta).Select(x => x.Stato).FirstAsync(ct);
        var invio = stato == StatoRichiestaApprovazione.Approvata
            ? new EsitoApprovazioni(true, "Il cliente aveva già approvato.", idRichiesta)
            : await _approvazioni.InviaLinkAsync(idRichiesta.Value, op, ct);

        i.IdFirmatarioCliente = f.IdFirmatarioCliente;
        i.IdRichiestaApprovazione = idRichiesta;
        i.DataUltimaModifica = DateTime.UtcNow;
        if (invio.Riuscito) { i.Stato = StatoInvitoFirmatario.Elaborato; i.DataElaborazione = i.DataUltimaModifica; i.ErroreElaborazione = null; }
        else i.ErroreElaborazione = invio.Messaggio.Length > 1000 ? invio.Messaggio[..1000] : invio.Messaggio;
        await _ctx.SaveChangesAsync(ct);
        return invio;
    }

    // ═══ Testo ═══

    private static string Html(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    private static string Corpo(InvitoFirmatario i, string link, DateTime scadenza, bool promemoria) => $"""
        <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1a1a1a;max-width:640px">
        <p>Gentile Cliente,</p>
        {(promemoria ? "<p><em>le scriviamo di nuovo perché non abbiamo ancora ricevuto la vostra indicazione.</em></p>" : "")}
        <p>è in vigore la nuova revisione delle nostre <strong>Condizioni Generali di Fornitura</strong> per i servizi di laboratorio.
        Per alcune clausole la legge (artt. 1341 e 1342 del Codice Civile) richiede un'approvazione specifica da parte di chi
        può impegnare la vostra azienda.</p>
        <p>Per questo vi chiediamo di indicarci <strong>la persona che approverà le Condizioni per {Html(i.ClienteRagioneSociale)}</strong>:
        il legale rappresentante o una persona con i poteri per farlo.</p>
        <p style="margin:24px 0"><a href="{link}" style="background:#C00000;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;font-weight:bold">Indica il Firmatario</a></p>
        <p>Bastano un paio di minuti: vi chiediamo nome, cognome, ruolo in azienda e un indirizzo e-mail personale della persona
        indicata (non una casella generica). A quell'indirizzo invieremo il link per leggere e approvare le Condizioni online:
        non servono registrazioni né password, e alla fine si riceve una ricevuta in PDF.</p>
        <ul>
        <li>Se la persona indicata è chi legge questa e-mail, basta inserire i propri dati.</li>
        <li>Basta una sola risposta per azienda: la prima indicazione ricevuta vale per tutte le vostre sedi.</li>
        <li>Il link vale fino al {scadenza.ToLocalTime():dd/MM/yyyy}.</li>
        </ul>
        <p style="background:#F4F4F4;border-radius:6px;padding:10px 12px">Se il pulsante non si apre, cliccate su questo indirizzo (o copiatelo nel browser):<br>
        <span style="font-size:14px;color:#1a3e8c;word-break:break-all">{link}</span></p>
        <p>Per qualsiasi dubbio potete rispondere a questa e-mail o chiamarci allo 0541 830442.</p>
        <p>Grazie per la collaborazione.<br>Cordiali saluti<br>
        <strong>MARE.A s.r.l. – Laboratorio di analisi</strong><br>Laboratorio accreditato ACCREDIA n. 00729<br>Via E. Toti, 2 – 47841 Cattolica (RN)</p>
        </div>
        """;
}
