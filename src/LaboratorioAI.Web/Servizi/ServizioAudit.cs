using System.Text.Json;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Enumerazioni;
using LaboratorioAI.Core.Servizi;
using LaboratorioAI.Dati.LaboratorioAI;

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
        try
        {
            _ctx.Audit.Add(new VoceAudit
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
        }
    }
}
