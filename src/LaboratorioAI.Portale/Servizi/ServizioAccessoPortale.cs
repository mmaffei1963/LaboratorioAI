using System.Security.Cryptography;
using System.Text;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Servizi;

/// <summary>Verifica delle credenziali e gestione delle sessioni del portale.</summary>
public interface IServizioAccessoPortale
{
    /// <summary>
    /// Verifica le credenziali. Se il cliente entra con la partita IVA da un browser non riconosciuto
    /// (<paramref name="tokenDispositivo"/> assente o non valido) non apre la sessione: invia un codice all'email del
    /// cliente e restituisce <c>TokenVerifica</c>, da usare con <see cref="ConfermaVerificaAsync"/> (18/11/2026).
    /// </summary>
    Task<(EsitoAccesso Esito, SessioneAttiva? Sessione, string? TokenVerifica)> AccediAsync(
        string codiceContabilita, string credenziale, string? tokenDispositivo,
        string? indirizzoIp, string? userAgent, CancellationToken ct = default);

    /// <summary>Conferma il codice ricevuto per email: apre la sessione e, se richiesto, riconosce il browser.</summary>
    Task<(EsitoAccesso Esito, SessioneAttiva? Sessione, string? TokenDispositivo, string? Messaggio)> ConfermaVerificaAsync(
        string tokenVerifica, string codice, bool ricordaBrowser,
        string? indirizzoIp, string? userAgent, CancellationToken ct = default);

    /// <summary>
    /// Accesso operatore con Microsoft 365 (19/11/2026): apre una sessione operatore se la persona è un utente attivo di
    /// LaboratorioAI con almeno un ruolo attivo (cercata per identificativo Entra, poi per e-mail). Null se non abilitata.
    /// </summary>
    Task<SessioneAttiva?> AccediOperatore365Async(Guid? objectId, string? email, string? nome,
        string? indirizzoIp, string? userAgent, CancellationToken ct = default);

    Task<SessioneAttiva?> VerificaTokenAsync(string token, CancellationToken ct = default);

    Task RevocaAsync(string token, CancellationToken ct = default);

    /// <summary>Rimuove le sessioni scadute. Invocata periodicamente.</summary>
    Task<int> PuliziaAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public class ServizioAccessoPortale : IServizioAccessoPortale
{
    private readonly IRepositoryPortale _repository;
    private readonly ContestoLaboratorioAI _ctx;
    private readonly OpzioniPortale _opzioni;
    private readonly ILogger<ServizioAccessoPortale> _log;
    private readonly IRepositoryCredenzialiClienti _credenziali;
    private readonly IServizioEmail _email;

    // Verifica del browser nuovo (18/11/2026)
    private const int GiorniBrowserRiconosciuto = 180;
    private const int MinutiValiditaVerifica = 15;
    private const int TentativiMassimiVerifica = 5;
    private const int VerificheMassimeOraPerCliente = 6;

    public ServizioAccessoPortale(
        IRepositoryPortale repository,
        ContestoLaboratorioAI ctx,
        Microsoft.Extensions.Options.IOptions<OpzioniPortale> opzioni,
        ILogger<ServizioAccessoPortale> log,
        IRepositoryCredenzialiClienti credenziali,
        IServizioEmail email)
    {
        _repository = repository;
        _ctx = ctx;
        _opzioni = opzioni.Value;
        _log = log;
        _credenziali = credenziali;
        _email = email;
    }

    // ═══════════════════════════════════════════════════════════════
    // ACCESSO
    // ═══════════════════════════════════════════════════════════════

    public async Task<(EsitoAccesso, SessioneAttiva?, string?)> AccediAsync(
        string codiceContabilita, string credenziale, string? tokenDispositivo,
        string? indirizzoIp, string? userAgent, CancellationToken ct = default)
    {
        var codice = codiceContabilita.Trim().ToUpperInvariant();

        if (await TroppiTentativiAsync(codice, indirizzoIp, ct))
        {
            await RegistraTentativoAsync(codice, indirizzoIp, false, "limite tentativi", ct);
            return (EsitoAccesso.TroppiTentativi, null, null);
        }

        // ── Accesso operatore MARE.A ───────────────────────────────
        if (codice == _opzioni.CodiceOperatore.ToUpperInvariant())
        {
            if (!_opzioni.OperatoreAbilitato)
            {
                _log.LogWarning("Tentativo di accesso operatore con credenziale non configurata.");
                await RegistraTentativoAsync(codice, indirizzoIp, false, "operatore non configurato", ct);
                return (EsitoAccesso.CredenzialiNonValide, null, null);
            }

            if (!ConfrontoSicuro(credenziale, _opzioni.CredenzialeOperatore!))
            {
                await RegistraTentativoAsync(codice, indirizzoIp, false, "credenziale errata", ct);
                return (EsitoAccesso.CredenzialiNonValide, null, null);
            }

            var sessioneOperatore = await CreaSessioneAsync(
                null, codice, "MARE.A srl — accesso operatore", true, indirizzoIp, userAgent, ct);

            await RegistraTentativoAsync(codice, indirizzoIp, true, null, ct);
            return (EsitoAccesso.Riuscito, sessioneOperatore, null);
        }

        // ── Accesso cliente ────────────────────────────────────────
        var (cliente, credenzialeRegistrata) = await _repository.CercaPerAccessoAsync(codice, ct);

        // 17/11/2026: se il cliente ha scelto una password personale vale solo quella (la partita IVA non più).
        var improntaPersonale = cliente is null ? null
            : await ServizioPasswordPortale.ImprontaAttivaAsync(_ctx, cliente.IdCliente, ct);

        // Il confronto viene eseguito anche quando il cliente non esiste,
        // così il tempo di risposta non rivela quali codici siano validi.
        var atteso = credenzialeRegistrata ?? Guid.NewGuid().ToString();
        var corrisponde = improntaPersonale is not null
            ? ServizioPasswordPortale.Verifica(improntaPersonale, credenziale)
            : ConfrontoSicuro(credenziale, atteso);

        if (cliente is null || !corrisponde)
        {
            await RegistraTentativoAsync(codice, indirizzoIp, false, "credenziale errata", ct);
            return (EsitoAccesso.CredenzialiNonValide, null, null);
        }

        // 18/11/2026: con la partita IVA (dato pubblico) da un browser non riconosciuto serve il codice via email.
        // Con una password personale non serve. Senza email registrata in ArcLab l'accesso resta come prima.
        if (improntaPersonale is null && !await BrowserRiconosciutoAsync(cliente.IdCliente, tokenDispositivo, ct))
        {
            string? tokenVerifica;
            try { tokenVerifica = await AvviaVerificaAsync(cliente, indirizzoIp, ct); }
            catch (LimiteVerificheException)
            {
                // Troppe verifiche: si blocca (non si salta la verifica).
                await RegistraTentativoAsync(codice, indirizzoIp, false, "limite verifiche email", ct);
                return (EsitoAccesso.TroppiTentativi, null, null);
            }
            if (tokenVerifica is not null)
            {
                await RegistraTentativoAsync(codice, indirizzoIp, true, "credenziali corrette, verifica email inviata", ct);
                return (EsitoAccesso.Riuscito, null, tokenVerifica);
            }
        }

        var sessione = await CreaSessioneAsync(
            cliente.IdCliente, cliente.CodiceContabilita, cliente.RagioneSociale,
            false, indirizzoIp, userAgent, ct);

        await RegistraTentativoAsync(codice, indirizzoIp, true, null, ct);
        return (EsitoAccesso.Riuscito, sessione, null);
    }

    // ═══════════════════════════════════════════════════════════════
    // VERIFICA DEL BROWSER NUOVO (18/11/2026)
    // ═══════════════════════════════════════════════════════════════

    private static string ImprontaToken(string token) => CatenaEvidenze.Sha256("browser|" + token);

    private async Task<bool> BrowserRiconosciutoAsync(int idCliente, string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 200) return false;
        var impronta = ImprontaToken(token);
        var d = await _ctx.Set<DispositivoFidatoPortale>().FirstOrDefaultAsync(x => x.ImprontaToken == impronta, ct);
        if (d is null || d.Revocato || d.IdClienteArcLab != idCliente || d.ScadenzaUtc < DateTime.UtcNow) return false;
        d.UltimoUsoUtc = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Invia il codice all'email del cliente; null se non c'è email (accesso come prima) o se l'invio fallisce.</summary>
    private async Task<string?> AvviaVerificaAsync(ClientePortale cliente, string? ip, CancellationToken ct)
    {
        var dati = await _credenziali.DatiAsync(cliente.IdCliente, ct);
        var indirizzi = dati?.IndirizziEmail ?? [];
        if (indirizzi.Count == 0)
        {
            _log.LogWarning("Accesso senza verifica email: il cliente {IdCliente} non ha un'email in ArcLab", cliente.IdCliente);
            return null;
        }

        var adesso = DateTime.UtcNow;
        var recenti = await _ctx.Set<VerificaAccessoPortale>()
            .CountAsync(v => v.IdClienteArcLab == cliente.IdCliente && v.DataCreazioneUtc > adesso.AddHours(-1), ct);
        if (recenti >= VerificheMassimeOraPerCliente)
            throw new LimiteVerificheException();

        var token = GeneraToken();
        var otp = CodiceOtp.Genera();
        var sale = CodiceOtp.Sale();
        var v = new VerificaAccessoPortale
        {
            IdClienteArcLab = cliente.IdCliente,
            CodiceContabilita = cliente.CodiceContabilita,
            RagioneSociale = cliente.RagioneSociale,
            ImprontaToken = ImprontaToken(token),
            Sale = sale,
            ImprontaCodice = CodiceOtp.Impronta(otp, sale, cliente.IdCliente),
            DataCreazioneUtc = adesso,
            ScadenzaUtc = adesso.AddMinutes(MinutiValiditaVerifica),
            IndirizzoIp = ip
        };
        _ctx.Set<VerificaAccessoPortale>().Add(v);
        await _ctx.SaveChangesAsync(ct);

        var corpo = $"""
            <p>Gentile cliente,</p>
            <p>è stato chiesto l'accesso all'Area Clienti MARE.A per il codice cliente <strong>{System.Net.WebUtility.HtmlEncode(cliente.CodiceContabilita)}</strong>
               da un browser non ancora riconosciuto.</p>
            <p>Il codice da inserire è: <strong style="font-size:1.3em;letter-spacing:.15em">{otp}</strong></p>
            <p>Il codice vale {MinutiValiditaVerifica} minuti. Se non sei stato tu, non inserirlo e avvisa MARE.A al numero 0541 830442:
               qualcuno conosce le tue credenziali. Puoi anche scegliere una password personale dall'Area Clienti (pulsante «Password»).</p>
            <p>MARE.A srl — Via E. Toti, 2 — 47841 Cattolica (RN)</p>
            """;
        try
        {
            await _email.InviaAsync(indirizzi, "Area Clienti MARE.A — codice di accesso", corpo, ct);
            return token;
        }
        catch (Exception ex)
        {
            // Senza email non si blocca il cliente: l'accesso prosegue come prima e resta traccia nel log.
            _log.LogError(ex, "Invio del codice di verifica accesso non riuscito per il cliente {IdCliente}", cliente.IdCliente);
            v.Usata = true;
            await _ctx.SaveChangesAsync(ct);
            return null;
        }
    }

    public async Task<(EsitoAccesso, SessioneAttiva?, string?, string?)> ConfermaVerificaAsync(
        string tokenVerifica, string codice, bool ricordaBrowser, string? indirizzoIp, string? userAgent, CancellationToken ct = default)
    {
        const string NonValido = "Codice non valido o scaduto: rifai l'accesso per riceverne uno nuovo.";
        if (string.IsNullOrWhiteSpace(tokenVerifica) || tokenVerifica.Length > 200)
            return (EsitoAccesso.CredenzialiNonValide, null, null, NonValido);

        var impronta = ImprontaToken(tokenVerifica);
        var v = await _ctx.Set<VerificaAccessoPortale>().FirstOrDefaultAsync(x => x.ImprontaToken == impronta, ct);
        if (v is null || v.Usata || v.ScadenzaUtc < DateTime.UtcNow || v.Tentativi >= TentativiMassimiVerifica)
            return (EsitoAccesso.CredenzialiNonValide, null, null, NonValido);

        if (!CodiceOtp.Corrisponde(codice, v.Sale, v.IdClienteArcLab, v.ImprontaCodice))
        {
            v.Tentativi++;
            await _ctx.SaveChangesAsync(ct);
            await RegistraTentativoAsync(v.CodiceContabilita, indirizzoIp, false, "codice di verifica errato", ct);
            return (EsitoAccesso.CredenzialiNonValide, null, null,
                v.Tentativi >= TentativiMassimiVerifica ? NonValido : "Codice non corretto.");
        }

        v.Usata = true;
        string? tokenDispositivo = null;
        if (ricordaBrowser)
        {
            tokenDispositivo = GeneraToken();
            _ctx.Set<DispositivoFidatoPortale>().Add(new DispositivoFidatoPortale
            {
                IdClienteArcLab = v.IdClienteArcLab,
                ImprontaToken = ImprontaToken(tokenDispositivo),
                DataCreazioneUtc = DateTime.UtcNow,
                ScadenzaUtc = DateTime.UtcNow.AddDays(GiorniBrowserRiconosciuto),
                IndirizzoIp = indirizzoIp,
                UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent
            });
        }
        await _ctx.SaveChangesAsync(ct);

        var sessione = await CreaSessioneAsync(v.IdClienteArcLab, v.CodiceContabilita, v.RagioneSociale, false, indirizzoIp, userAgent, ct);
        await RegistraTentativoAsync(v.CodiceContabilita, indirizzoIp, true, ricordaBrowser ? "verifica email, browser riconosciuto" : "verifica email", ct);
        return (EsitoAccesso.Riuscito, sessione, tokenDispositivo, null);
    }

    /// <summary>
    /// Confronto a tempo costante.
    ///
    /// I due valori vengono ridotti a impronte di lunghezza fissa prima del
    /// raffronto: in questo modo nemmeno la lunghezza della credenziale
    /// attesa è deducibile dai tempi di risposta.
    /// </summary>
    private static bool ConfrontoSicuro(string fornito, string atteso)
    {
        Span<byte> improntaFornita = stackalloc byte[32];
        Span<byte> improntaAttesa = stackalloc byte[32];

        SHA256.HashData(Encoding.UTF8.GetBytes(fornito.Trim()), improntaFornita);
        SHA256.HashData(Encoding.UTF8.GetBytes(atteso.Trim()), improntaAttesa);

        return CryptographicOperations.FixedTimeEquals(improntaFornita, improntaAttesa);
    }

    // ═══════════════════════════════════════════════════════════════
    // PROTEZIONE DA TENTATIVI RIPETUTI
    // ═══════════════════════════════════════════════════════════════

    private async Task<bool> TroppiTentativiAsync(
        string codice, string? indirizzoIp, CancellationToken ct)
    {
        var soglia = DateTime.UtcNow.AddMinutes(-_opzioni.FinestraTentativiMinuti);

        var perCodice = await _ctx.TentativiAccesso
            .CountAsync(t => t.CodiceContabilita == codice
                          && !t.Riuscito
                          && t.DataOra >= soglia, ct);

        if (perCodice >= _opzioni.TentativiMassimiPerCodice) return true;

        if (string.IsNullOrEmpty(indirizzoIp)) return false;

        var perIndirizzo = await _ctx.TentativiAccesso
            .CountAsync(t => t.IndirizzoIp == indirizzoIp
                          && !t.Riuscito
                          && t.DataOra >= soglia, ct);

        return perIndirizzo >= _opzioni.TentativiMassimiPerIndirizzo;
    }

    /// <remarks>La credenziale non compare in alcun campo di questa registrazione.</remarks>
    private async Task RegistraTentativoAsync(
        string codice, string? indirizzoIp, bool riuscito, string? motivo, CancellationToken ct)
    {
        _ctx.TentativiAccesso.Add(new TentativoAccesso
        {
            DataOra = DateTime.UtcNow,
            CodiceContabilita = codice,
            IndirizzoIp = indirizzoIp,
            Riuscito = riuscito,
            Motivo = motivo
        });

        await _ctx.SaveChangesAsync(ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // SESSIONI
    // ═══════════════════════════════════════════════════════════════

    private async Task<SessioneAttiva> CreaSessioneAsync(
        int? idCliente, string codice, string ragioneSociale, bool operatore,
        string? indirizzoIp, string? userAgent, CancellationToken ct, string? emailOperatore = null)
    {
        var token = GeneraToken();
        var scadenza = DateTime.UtcNow.AddHours(_opzioni.DurataSessioneOre);

        _ctx.SessioniPortale.Add(new SessionePortale
        {
            Token = token,
            IdCliente = idCliente,
            CodiceContabilita = codice,
            RagioneSociale = ragioneSociale,
            Operatore = operatore,
            EmailOperatore = emailOperatore,
            DataCreazione = DateTime.UtcNow,
            Scadenza = scadenza,
            IndirizzoIp = indirizzoIp,
            UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent
        });

        await _ctx.SaveChangesAsync(ct);

        return new SessioneAttiva
        {
            Token = token,
            IdCliente = idCliente,
            RagioneSociale = ragioneSociale,
            Operatore = operatore,
            EmailOperatore = emailOperatore,
            Scadenza = scadenza
        };
    }

    /// <summary>Token opaco da generatore crittograficamente sicuro.</summary>
    private static string GeneraToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public async Task<SessioneAttiva?> AccediOperatore365Async(Guid? objectId, string? email, string? nome,
        string? indirizzoIp, string? userAgent, CancellationToken ct = default)
    {
        var mail = email?.Trim().ToLowerInvariant();
        if (objectId is null && string.IsNullOrEmpty(mail)) return null;

        // Utente di LaboratorioAI: prima per identificativo Entra, poi per e-mail (persona registrata in anticipo).
        var utente = objectId is Guid oid
            ? await _ctx.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.ObjectIdEntra == oid, ct)
            : null;
        if (utente is null && !string.IsNullOrEmpty(mail))
            utente = await _ctx.Utenti.AsNoTracking().FirstOrDefaultAsync(u => u.Email.ToLower() == mail, ct);

        // Abilitato = utente attivo con almeno un ruolo attivo (decisione del 19/11/2026: tutti i colleghi, il portale
        // per l'operatore è di sola consultazione più il ripristino delle credenziali).
        var abilitato = utente is { Attivo: true }
            && await _ctx.UtentiRuoli.AsNoTracking().AnyAsync(ur => ur.IdUtente == utente.IdUtente && ur.Ruolo.Attivo, ct);

        var codice = "365:" + (mail ?? objectId?.ToString());
        if (codice.Length > 50) codice = codice[..50];
        if (!abilitato)
        {
            _log.LogWarning("Accesso operatore 365 rifiutato per {Email} ({ObjectId}): non è un utente attivo di LaboratorioAI.", mail, objectId);
            await RegistraTentativoAsync(codice, indirizzoIp, false, "operatore 365 non abilitato", ct);
            return null;
        }

        var nomeVisibile = string.IsNullOrWhiteSpace(utente!.NomeCompleto) ? (nome ?? mail ?? "operatore") : utente.NomeCompleto;
        var sessione = await CreaSessioneAsync(null, _opzioni.CodiceOperatore.ToUpperInvariant(),
            $"MARE.A srl — {nomeVisibile}", true, indirizzoIp, userAgent, ct, mail ?? utente.Email);
        await RegistraTentativoAsync(codice, indirizzoIp, true, null, ct);
        _log.LogInformation("Accesso operatore 365: {Email}", mail ?? utente.Email);
        return sessione;
    }

    public async Task<SessioneAttiva?> VerificaTokenAsync(
        string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        var sessione = await _ctx.SessioniPortale
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Token == token
                                   && s.DataRevoca == null
                                   && s.Scadenza > DateTime.UtcNow, ct);

        if (sessione is null) return null;

        return new SessioneAttiva
        {
            Token = sessione.Token,
            IdCliente = sessione.IdCliente,
            RagioneSociale = sessione.RagioneSociale,
            Operatore = sessione.Operatore,
            EmailOperatore = sessione.EmailOperatore,
            Scadenza = sessione.Scadenza
        };
    }

    public async Task RevocaAsync(string token, CancellationToken ct = default)
    {
        var sessione = await _ctx.SessioniPortale
            .FirstOrDefaultAsync(s => s.Token == token, ct);

        if (sessione is null) return;

        sessione.DataRevoca = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<int> PuliziaAsync(CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow.AddDays(-_opzioni.GiorniConservazioneSessioni);

        return await _ctx.SessioniPortale
            .Where(s => s.Scadenza < limite)
            .ExecuteDeleteAsync(ct);
    }
}

/// <summary>Troppe verifiche via email per lo stesso cliente nell'ultima ora (18/11/2026).</summary>
internal sealed class LimiteVerificheException : Exception { }
