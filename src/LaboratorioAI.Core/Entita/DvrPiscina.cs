using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>
/// DVR piscina salvato nel database del sistema. I dati della scheda
/// (<c>SchedaPiscina</c>) sono serializzati in JSON nella colonna
/// <see cref="DatiScheda"/>, sullo stampo di <c>AnalisiAi.Testo</c>; le colonne
/// strutturate servono a elenco, ricerca e snapshot anagrafico congelato.
/// La scrittura avviene solo su questo database, mai su ArcLab.
/// </summary>
public class DvrPiscina
{
    public int IdDvrPiscina { get; set; }

    // ── Riferimenti logici ad ArcLab (nessun vincolo referenziale) ──
    public int IdInsediamentoArcLab { get; set; }
    public int? IdClienteArcLab { get; set; }

    // ── Snapshot anagrafici, congelati alla compilazione ────────────
    public string RagioneSociale { get; set; } = null!;
    public string? NomeInsediamento { get; set; }
    public string? Comune { get; set; }
    public string? Provincia { get; set; }
    public string? Regione { get; set; }

    // ── Stato del documento ─────────────────────────────────────────
    public StatoDvrPiscina Stato { get; set; }

    /// <summary>SchedaPiscina serializzata in JSON. Fonte per rigenerare il Word.</summary>
    public string DatiScheda { get; set; } = null!;

    // ── Tracciabilità ───────────────────────────────────────────────
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }

    public string VersioneApplicazione { get; set; } = null!;

    // ── Ultima generazione del documento ────────────────────────────
    public DateTime? DataUltimaGenerazione { get; set; }
    public string? NomeFileGenerato { get; set; }
    public string? PercorsoArchivio { get; set; }
}
