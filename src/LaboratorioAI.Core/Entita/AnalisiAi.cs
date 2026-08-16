using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Analisi AI archiviata.
///
/// Ogni esecuzione produce un nuovo record: le analisi precedenti non vengono
/// sovrascritte. Questo conserva lo storico e permette di ricostruire su quale
/// base è stata presa una decisione di validazione.
/// </summary>
public class AnalisiAi
{
    public int IdAnalisi { get; set; }

    // ── Riferimenti ad ArcLab ──────────────────────────────────────
    // Riferimenti logici: ArcLab è un database distinto, nessun vincolo
    // di integrità referenziale è possibile né desiderabile.

    public int IdCampione { get; set; }
    public int? IdCertificato { get; set; }

    // ── Contesto dell'esecuzione ───────────────────────────────────

    public ModalitaAnalisi Modalita { get; set; }
    public DateTime DataAnalisi { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }

    // ── Fotografia dei dati descrittivi ────────────────────────────
    // Copiati da ArcLab al momento dell'analisi. Scelta deliberata:
    // un'analisi archiviata deve restare leggibile e interpretabile
    // anche se il campione viene successivamente modificato o archiviato.

    public string? RagioneSociale { get; set; }
    public string? DenominazioneCampione { get; set; }
    public string? Matrice { get; set; }
    public DateOnly? DataRicezione { get; set; }
    public int? NumeroProve { get; set; }

    // ── Risultato ──────────────────────────────────────────────────

    /// <summary>Giudizio dedotto dal testo prodotto dal modello.</summary>
    public GiudizioAnalisi Giudizio { get; set; }

    /// <summary>Testo integrale restituito dal modello, conservato senza rielaborazioni.</summary>
    public string Testo { get; set; } = null!;

    // ── Tracciabilità tecnica ──────────────────────────────────────
    // Necessaria per ricostruire le condizioni in cui l'analisi è stata
    // prodotta: modello e versione applicativa possono cambiare nel tempo.

    public string ModelloAi { get; set; } = null!;
    public string VersioneApplicazione { get; set; } = null!;

    public int? TokenInput { get; set; }
    public int? TokenOutput { get; set; }
    public decimal? CostoStimato { get; set; }
}
