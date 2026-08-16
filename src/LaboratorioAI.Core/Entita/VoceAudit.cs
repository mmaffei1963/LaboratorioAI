using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Voce dell'audit trail.
///
/// Tabella in sola aggiunta: nessun aggiornamento, nessuna cancellazione.
/// Registra le operazioni significative ai fini della tracciabilità
/// richiesta dal sistema di gestione qualità.
/// </summary>
public class VoceAudit
{
    public long IdVoce { get; set; }

    public DateTime DataOra { get; set; }

    public int? IdUtente { get; set; }

    /// <summary>Email dell'utente al momento dell'operazione, conservata per leggibilità.</summary>
    public string? EmailUtente { get; set; }

    public string? IndirizzoIp { get; set; }

    public AreaFunzionale Area { get; set; }

    /// <summary>Azione compiuta, es. <c>analisi.eseguita</c>, <c>dvr.generato</c>.</summary>
    public string Azione { get; set; } = null!;

    /// <summary>Tipo di entità coinvolta, es. <c>Campione</c>, <c>Utente</c>.</summary>
    public string? TipoEntita { get; set; }

    public string? IdEntita { get; set; }

    public EsitoOperazione Esito { get; set; }

    /// <summary>Dettaglio in formato JSON. Non deve mai contenere credenziali.</summary>
    public string? Dettaglio { get; set; }

    public string VersioneApplicazione { get; set; } = null!;
}
