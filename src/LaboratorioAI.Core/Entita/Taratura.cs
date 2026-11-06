namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Record di una taratura di uno strumento misuratore di temperatura (UUT)
/// contro un campione di riferimento. Le misure (punti + letture) sono in JSON
/// (<see cref="DatiMisure"/>), sullo stampo di <c>DvrPiscina.DatiScheda</c>.
/// Lo strumento può essere del cliente (identificato da ArcLab) o interno MARE.A.
/// </summary>
public class Taratura
{
    public int IdTaratura { get; set; }

    public int IdCampioneRiferimento { get; set; }
    public CampioneRiferimento? CampioneRiferimento { get; set; }

    // ── Strumento in taratura (UUT) ─────────────────────────────────
    public string StrumentoIdentificazione { get; set; } = null!;
    public string? StrumentoCodice { get; set; }
    public double RisoluzioneStrumento { get; set; }
    public double FattoreCopertura { get; set; } = 2.0;

    // ── Cliente (anagrafica ArcLab, opzionale: interno se assente) ──
    public int? IdClienteArcLab { get; set; }
    public string? Committente { get; set; }
    /// <summary>03/11/2026: insediamento del cliente (ArcLab), stampato in testata solo se indicato.</summary>
    public int? IdInsediamentoArcLab { get; set; }
    public string? Insediamento { get; set; }
    /// <summary>03/11/2026: per i termometri dei clienti, tipo e servizio (passano allo scadenziario).</summary>
    public Tarature.TipoTermometro TipoTermometro { get; set; }
    public Tarature.ServizioTaraturaTermometro Servizio { get; set; } = Tarature.ServizioTaraturaTermometro.SoloTaratura;

    // ── Rapporto ────────────────────────────────────────────────────
    public string? NumeroRapporto { get; set; }
    public DateTime DataTaratura { get; set; }
    public DateTime? DataScadenza { get; set; }
    public string? RiferimentoIstruzione { get; set; }

    /// <summary>Responsabile del rapporto (può differire dall'utente che redige).</summary>
    public string? Responsabile { get; set; }

    /// <summary>Punti e letture serializzati in JSON (fonte per calcolo e rigenerazione).</summary>
    public string DatiMisure { get; set; } = null!;

    public string? Note { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }

    public string? VersioneApplicazione { get; set; }
}
