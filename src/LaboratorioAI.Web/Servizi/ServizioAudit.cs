using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;
using Microsoft.EntityFrameworkCore;

namespace LaboratorioAI.Web.Servizi;

/// <inheritdoc />
public class ServizioAudit : IServizioAudit
{
    private readonly ContestoLaboratorioAI _ctx;
    private readonly IContestoUtente _utente;
    private readonly IServizioVersione _versione;
    private readonly ILogger<ServizioAudit> _log;

    private static readonly JsonSerializerOptions OpzioniJson = new()
    {
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ServizioAudit(
        ContestoLaboratorioAI ctx,
        IContestoUtente utente,
        IServizioVersione versione,
        ILogger<ServizioAudit> log)
    {
        _ctx = ctx;
        _utente = utente;
        _versione = versione;
        _log = log;
    }

    public async Task RegistraAsync(
        AreaFunzionale area,
        string azione,
        EsitoOperazione esito = EsitoOperazione.Successo,
        string? tipoEntita = null,
        string? idEntita = null,
        object? dettaglio = null,
        CancellationToken ct = default)
    {
        // 12/10/2026: IdEntita è lunga al massimo 100 caratteri (i nomi dei certificati esterni la superano).
        if (idEntita is { Length: > 100 }) idEntita = idEntita[..100];
        VoceAudit? voce = null;
        try
        {
            _ctx.Audit.Add(voce = new VoceAudit
            {
                DataOra = DateTime.UtcNow,
                IdUtente = _utente.IdUtente,
                EmailUtente = _utente.Email,
                IndirizzoIp = _utente.IndirizzoIp,
                Area = area,
                Azione = azione,
                TipoEntita = tipoEntita,
                IdEntita = idEntita,
                Esito = esito,
                Dettaglio = dettaglio is null ? null : JsonSerializer.Serialize(dettaglio, OpzioniJson),
                VersioneApplicazione = _versione.Versione
            });

            await _ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Un errore nella scrittura dell'audit non deve mai far fallire
            // l'operazione applicativa che lo ha originato.
            _log.LogError(ex, "Scrittura audit non riuscita: {Area}/{Azione}", area, azione);
            // La voce non salvata non deve restare in sospeso nel contesto (farebbe fallire i salvataggi successivi).
            if (voce is not null) _ctx.Entry(voce).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
        }
    }

    public async Task<Dictionary<string, VoceAuditRiepilogo>> UltimeOperazioniAsync(
        string tipoEntita, IEnumerable<string> idEntita, CancellationToken ct = default)
    {
        var ids = idEntita.Distinct().ToList();
        if (ids.Count == 0) return new();

        // Si filtra sul server e si raggruppa in memoria (i risultati per un elenco sono comunque
        // pochi): evita di dover tradurre in SQL un GroupBy+First per riga, meno prevedibile.
        var righe = await _ctx.Audit.AsNoTracking()
            .Where(v => v.TipoEntita == tipoEntita && v.IdEntita != null && ids.Contains(v.IdEntita))
            .Select(v => new { v.IdEntita, v.EmailUtente, v.DataOra, v.Azione })
            .ToListAsync(ct);

        return righe
            .GroupBy(v => v.IdEntita!)
            .Select(g => g.OrderByDescending(v => v.DataOra).First())
            .ToDictionary(v => v.IdEntita!, v => new VoceAuditRiepilogo(v.EmailUtente, v.DataOra, v.Azione));
    }
}
