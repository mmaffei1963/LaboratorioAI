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

    /// <summary>
    /// Ultima operazione registrata (qualsiasi azione) per ciascuna delle entità indicate, per
    /// mostrare "chi ha fatto l'ultima modifica e quando" negli elenchi — senza aggiungere colonne
    /// alle tabelle applicative: il dato è già tutto nell'audit trail esistente.
    /// Le entità senza alcuna voce d'audit non compaiono nel risultato.
    /// </summary>
    Task<Dictionary<string, VoceAuditRiepilogo>> UltimeOperazioniAsync(
        string tipoEntita, IEnumerable<string> idEntita, CancellationToken ct = default);
}

/// <summary>Riepilogo minimo dell'ultima voce d'audit di un'entità.</summary>
public record VoceAuditRiepilogo(string? EmailUtente, DateTime DataOra, string Azione);
