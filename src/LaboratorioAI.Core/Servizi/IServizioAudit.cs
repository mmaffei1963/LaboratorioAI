using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Registrazione delle operazioni significative nell'audit trail.
///
/// Le scritture non devono mai far fallire l'operazione principale:
/// un errore di audit viene registrato nei log ma non propagato.
/// </summary>
public interface IServizioAudit
{
    Task RegistraAsync(
        AreaFunzionale area,
        string azione,
        EsitoOperazione esito = EsitoOperazione.Successo,
        string? tipoEntita = null,
        string? idEntita = null,
        object? dettaglio = null,
        CancellationToken ct = default);
}
