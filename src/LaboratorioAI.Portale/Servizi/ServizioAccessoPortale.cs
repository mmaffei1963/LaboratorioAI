using System.Security.Cryptography;
using System.Text;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using LaboratorioAI.Core.Repository;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Portale.Servizi;

/// <summary>Verifica delle credenziali e gestione delle sessioni del portale.</summary>
public interface IServizioAccessoPortale
{
    Task<(EsitoAccesso Esito, SessioneAttiva? Sessione)> AccediAsync(
        string codiceContabilita, string credenziale,
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

    public ServizioAccessoPortale(
        IRepositoryPortale repository,
        ContestoLaboratorioAI ctx,
        Microsoft.Extensions.Options.IOptions<OpzioniPortale> opzioni,
        ILogger<ServizioAccessoPortale> log)
    {
        _repository = repository;
        _ctx = ctx;
        _opzioni = opzioni.Value;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════
    // ACCESSO
    // ═══════════════════════════════════════════════════════════════

    public async Task<(EsitoAccesso, SessioneAttiva?)> AccediAsync(
        string codiceContabilita, string credenziale,
        string? indirizzoIp, string? userAgent, CancellationToken ct = default)
    {
        var codice = codiceContabilita.Trim().ToUpperInvariant();

        if (await TroppiTentativiAsync(codice, indirizzoIp, ct))
        {
            await RegistraTentativoAsync(codice, indirizzoIp, false, "limite tentativi", ct);
            return (EsitoAccesso.TroppiTentativi, null);
        }

        // ── Accesso operatore MARE.A ───────────────────────────────
        if (codice == _opzioni.CodiceOperatore.ToUpperInvariant())
        {
            if (!_opzioni.OperatoreAbilitato)
            {
                _log.LogWarning("Tentativo di accesso operatore con credenziale non configurata.");
                await RegistraTentativoAsync(codice, indirizzoIp, false, "operatore non configurato", ct);
                return (EsitoAccesso.CredenzialiNonValide, null);
            }

            if (!ConfrontoSicuro(credenziale, _opzioni.CredenzialeOperatore!))
            {
                await RegistraTentativoAsync(codice, indirizzoIp, false, "credenziale errata", ct);
                return (EsitoAccesso.CredenzialiNonValide, null);
            }

            var sessioneOperatore = await CreaSessioneAsync(
                null, codice, "MARE.A srl — accesso operatore", true, indirizzoIp, userAgent, ct);

            await RegistraTentativoAsync(codice, indirizzoIp, true, null, ct);
            return (EsitoAccesso.Riuscito, sessioneOperatore);
        }

        // ── Accesso cliente ────────────────────────────────────────
        var (cliente, credenzialeRegistrata) = await _repository.CercaPerAccessoAsync(codice, ct);

        // Il confronto viene eseguito anche quando il cliente non esiste,
        // così il tempo di risposta non rivela quali codici siano validi.
        var atteso = credenzialeRegistrata ?? Guid.NewGuid().ToString();
        var corrisponde = ConfrontoSicuro(credenziale, atteso);

        if (cliente is null || !corrisponde)
        {
            await RegistraTentativoAsync(codice, indirizzoIp, false, "credenziale errata", ct);
            return (EsitoAccesso.CredenzialiNonValide, null);
        }

        var sessione = await CreaSessioneAsync(
            cliente.IdCliente, cliente.CodiceContabilita, cliente.RagioneSociale,
            false, indirizzoIp, userAgent, ct);

        await RegistraTentativoAsync(codice, indirizzoIp, true, null, ct);
        return (EsitoAccesso.Riuscito, sessione);
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
        string? indirizzoIp, string? userAgent, CancellationToken ct)
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
            Scadenza = scadenza
        };
    }

    /// <summary>Token opaco da generatore crittograficamente sicuro.</summary>
    private static string GeneraToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

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
