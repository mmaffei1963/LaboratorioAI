namespace LaboratorioAI.Core.Entita;

/// <summary>Stato dell'invio al portale Palomar di un Ordine di Accettazione.</summary>
public enum StatoInvioOrdine
{
    /// <summary>Generato e archiviato, in attesa di pubblicazione sul portale.</summary>
    DaInviare = 1,
    /// <summary>Pubblicato/consegnato al portale.</summary>
    Inviato = 2,
    /// <summary>Ultimo tentativo di invio fallito (vedi UltimoErrore/Tentativi).</summary>
    Errore = 3
}

/// <summary>
/// Registro degli Ordini di Accettazione già generati/inviati, per evitare doppioni sia
/// dalla generazione manuale (bottone) sia da quella schedulata. La chiave logica è l'IdLotto
/// di ArcLab (univoco; il CodLotto no). Tabella in LaboratorioAI: nessuna scrittura su ArcLab.
/// </summary>
public class OrdineAccettazioneEmesso
{
    public int IdOrdineAccettazione { get; set; }

    /// <summary>Lotto ArcLab per cui è stato emesso il documento (Lotto.IdLotto). Univoco.</summary>
    public int IdLotto { get; set; }

    /// <summary>Codice lotto stampato (Lotto.CodLotto), solo per tracciabilità/ricerca.</summary>
    public string? CodLotto { get; set; }

    /// <summary>Cliente al momento dell'emissione, per tracciabilità.</summary>
    public string? Cliente { get; set; }

    /// <summary>Insediamento (IDInsediamento) di riferimento, per tracciabilità/naming.</summary>
    public int? IdInsediamento { get; set; }

    /// <summary>Numero di campioni inclusi nel documento.</summary>
    public int NumeroCampioni { get; set; }

    /// <summary>Data/ora di prima emissione (UTC).</summary>
    public DateTime DataEmissione { get; set; }

    /// <summary>Origine dell'emissione: "manuale" o "schedulato".</summary>
    public string? Origine { get; set; }

    public int? IdUtente { get; set; }

    // ── Archiviazione file + invio al portale ──────────────────────
    /// <summary>Nome del file PDF archiviato.</summary>
    public string? NomeFile { get; set; }

    /// <summary>Percorso completo del PDF archiviato su disco.</summary>
    public string? PercorsoFile { get; set; }

    /// <summary>Impronta SHA-256 del PDF (per verifica integrità / de-dup).</summary>
    public string? Hash { get; set; }

    /// <summary>Stato dell'invio al portale Palomar.</summary>
    public StatoInvioOrdine StatoInvio { get; set; } = StatoInvioOrdine.DaInviare;

    /// <summary>Data/ora dell'invio al portale (UTC), se avvenuto.</summary>
    public DateTime? DataInvio { get; set; }

    /// <summary>Numero di tentativi di invio effettuati.</summary>
    public int Tentativi { get; set; }

    /// <summary>Messaggio dell'ultimo errore di invio, se presente.</summary>
    public string? UltimoErrore { get; set; }
}
