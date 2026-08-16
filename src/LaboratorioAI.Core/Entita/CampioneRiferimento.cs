namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Campione di riferimento (CDR) per la taratura dei termometri: anagrafica
/// riutilizzabile con i dati del certificato LAT. I valori metrologici sono in
/// double per coerenza col motore di calcolo. Solo database proprio.
/// </summary>
public class CampioneRiferimento
{
    public int IdCampione { get; set; }

    public string Identificazione { get; set; } = null!;
    public string? TipoTermometro { get; set; }
    public string? CodiceInterno { get; set; }

    // ── Certificato LAT ─────────────────────────────────────────────
    public string? CentroLat { get; set; }
    public string? NumeroCertificato { get; set; }
    public DateTime? DataTaratura { get; set; }
    public DateTime? DataScadenza { get; set; }

    public double Risoluzione { get; set; }
    public double FattoreCoperturaK { get; set; } = 2.0;

    /// <summary>
    /// Incertezza estesa "di testata" del certificato UE(CR) (°C), usata nel budget
    /// della mappatura come u(CR) = UE/k. Opzionale: se 0 si usa la massima incertezza
    /// estesa tra i punti del certificato.
    /// </summary>
    public double IncertezzaEstesaCertificato { get; set; }

    /// <summary>
    /// Incertezza tipo di instabilità/deriva del CDR u(stCR) (°C), ricavata dalla
    /// storia di taratura del riferimento. Usata nel budget delle tarature di
    /// incubatori/celle. Opzionale (default 0).
    /// </summary>
    public double IncertezzaInstabilita { get; set; }

    public string? Note { get; set; }

    // ── Nessuna cancellazione fisica ────────────────────────────────
    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public ICollection<PuntoCertificato> Punti { get; set; } = new List<PuntoCertificato>();
}

/// <summary>Un punto del certificato del CDR (per l'interpolazione di correzione e u(CR)).</summary>
public class PuntoCertificato
{
    public int IdPunto { get; set; }

    public int IdCampione { get; set; }
    public CampioneRiferimento? Campione { get; set; }

    public double TIndicata { get; set; }
    public double TRiferimento { get; set; }
    public double Differenza { get; set; }
    public double IncertezzaEstesa { get; set; }   // UE(CR)
    public double IncertezzaTipo { get; set; }     // u(CR) usata nel budget
    public int Ordine { get; set; }

    /// <summary>
    /// Vero se il punto appartiene alla taratura secondaria/interna del CDR (usata
    /// per calcolare la deriva u(stCR) nelle tarature di incubatori). Falso = certificato primario.
    /// </summary>
    public bool Secondario { get; set; }
}
