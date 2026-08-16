namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Mappatura stabile tra un canale del file Econorma (T01, T02, …) e lo strumento/sonda
/// in ArcLab (IdStrumento). I limiti NON sono memorizzati qui: vengono riletti da ArcLab
/// a ogni caricamento. Gestione soft-delete per il turnover delle sonde.
/// </summary>
public class MappaturaCanaleTempLog
{
    public int IdMappatura { get; set; }

    /// <summary>Etichetta canale nel file (es. "T01").</summary>
    public string Canale { get; set; } = null!;

    /// <summary>IdStrumento ArcLab della sonda Econorma.</summary>
    public int IdStrumento { get; set; }

    /// <summary>Nome della sonda al momento della mappatura (solo visualizzazione).</summary>
    public string? Sonda { get; set; }

    public string? Note { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? ValidaDal { get; set; }
    public DateTime? ValidaAl { get; set; }

    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }
}

/// <summary>Storico di un caricamento del file notturno (per tracciabilità/audit).</summary>
public class CaricamentoTempLog
{
    public int IdCaricamento { get; set; }

    public string? NomeFile { get; set; }
    /// <summary>Cartella da cui è stato caricato il file (per riproporla al caricamento successivo).</summary>
    public string? CartellaOrigine { get; set; }
    public DateTime? DataDal { get; set; }
    public DateTime? DataAl { get; set; }

    public int NumeroCanaliMonitorati { get; set; }
    public int NumeroCanaliConEscursioni { get; set; }
    public int NumeroEscursioni { get; set; }
    public bool TuttoConforme { get; set; }

    /// <summary>Elenco delle escursioni serializzato in JSON (per il report/registro).</summary>
    public string? EscursioniJson { get; set; }
    /// <summary>Canali senza limiti risolvibili al momento del caricamento.</summary>
    public string? DaSistemare { get; set; }

    // ── Validazione (revisione umana del file) ─────────────────────
    /// <summary>Esito della validazione: "Conforme" | "Dubbio" | "NonConforme" (null = non validato).</summary>
    public string? EsitoValidazione { get; set; }
    public string? Validatore { get; set; }
    public DateTime? DataValidazione { get; set; }
    public string? NoteValidazione { get; set; }

    public DateTime DataCreazione { get; set; }
    public int? IdUtente { get; set; }
    public string? VersioneApplicazione { get; set; }
}

/// <summary>Registra una sincronizzazione delle sonde Econorma da ArcLab (per la data "ultima sincronizzazione").</summary>
public class SincronizzazioneSondeTempLog
{
    public int IdSincronizzazione { get; set; }
    public DateTime DataSincronizzazione { get; set; }
    public int NumeroSonde { get; set; }
    public int? IdUtente { get; set; }
}
