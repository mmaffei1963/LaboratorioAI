using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Servizi;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Dati.LaboratorioAI;

/// <summary>
/// Scrittura e verifica del registro delle evidenze delle approvazioni (contratti.EventiApprovazione, 09/10/2026).
/// Ogni aggiunta legge l'impronta dell'ultima riga e calcola la propria (<see cref="CatenaEvidenze"/>) sotto un lock
/// applicativo esclusivo (sp_getapplock), così due richieste contemporanee non possono agganciarsi alla stessa riga.
/// </summary>
public interface IRegistroEvidenzeApprovazioni
{
    /// <summary>
    /// Aggiunge una riga al registro e salva anche le modifiche già in sospeso nel contesto, nella stessa transazione:
    /// l'evento e il cambio di stato che lo accompagna si salvano insieme o per niente.
    /// </summary>
    Task<EventoApprovazione> AggiungiAsync(string tipo, int? idRichiesta, int? idCliente, object? dati,
        string? nomeUtente, string? indirizzoIp, string? userAgent, CancellationToken ct = default);

    /// <summary>Ricalcola l'intera catena.</summary>
    Task<CatenaEvidenze.EsitoVerifica> VerificaAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public class RegistroEvidenzeApprovazioni : IRegistroEvidenzeApprovazioni
{
    private static readonly JsonSerializerOptions OpzioniJson = new() { WriteIndented = false };

    private readonly ContestoLaboratorioAI _ctx;

    public RegistroEvidenzeApprovazioni(ContestoLaboratorioAI ctx) => _ctx = ctx;

    public async Task<EventoApprovazione> AggiungiAsync(string tipo, int? idRichiesta, int? idCliente, object? dati,
        string? nomeUtente, string? indirizzoIp, string? userAgent, CancellationToken ct = default)
    {
        var strategia = _ctx.Database.CreateExecutionStrategy();
        return await strategia.ExecuteAsync(async () =>
        {
            await using var tx = await _ctx.Database.BeginTransactionAsync(ct);
            await _ctx.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = 'contratti.EventiApprovazione', @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000",
                ct);

            var precedente = await _ctx.EventiApprovazione.AsNoTracking()
                .OrderByDescending(x => x.IdEventoApprovazione)
                .Select(x => x.Impronta)
                .FirstOrDefaultAsync(ct) ?? CatenaEvidenze.Origine;

            var evento = new EventoApprovazione
            {
                IdRichiestaApprovazione = idRichiesta,
                IdClienteArcLab = idCliente,
                Tipo = tipo,
                DataUtc = DateTime.UtcNow,
                Dati = dati is null ? null : dati as string ?? JsonSerializer.Serialize(dati, OpzioniJson),
                NomeUtente = Taglia(nomeUtente, 200),
                IndirizzoIp = Taglia(indirizzoIp, 64),
                UserAgent = Taglia(userAgent, 500),
                ImprontaPrecedente = precedente,
            };
            evento.Impronta = CatenaEvidenze.Calcola(evento);
            _ctx.EventiApprovazione.Add(evento);
            await _ctx.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return evento;
        });
    }

    public async Task<CatenaEvidenze.EsitoVerifica> VerificaAsync(CancellationToken ct = default)
    {
        var righe = await _ctx.EventiApprovazione.AsNoTracking()
            .OrderBy(x => x.IdEventoApprovazione).ToListAsync(ct);
        return CatenaEvidenze.Verifica(righe);
    }

    private static string? Taglia(string? s, int max) => s is null ? null : s.Length <= max ? s : s[..max];
}
