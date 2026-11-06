namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Taratura del misuratore di temperatura di un incubatore/cella (Modulo 04-02),
/// alla temperatura di esercizio, contro un campione di riferimento (CDR).
/// Compaiono due strumenti: il Misuratore (l'oggetto tarato) e l'Apparecchiatura
/// (la cella/incubatore, contesto). Entrambi possono essere interni MARE.A
/// (anagrafica ArcLab Strumenti) o del cliente. Le misure sono in JSON.
/// </summary>
public class TaraturaIncubatore
{
    public int IdTaratura { get; set; }

    public int IdCampioneRiferimento { get; set; }
    public CampioneRiferimento? CampioneRiferimento { get; set; }

    // ── Misuratore in taratura (oggetto tarato) ─────────────────────
    public string MisuratoreIdentificazione { get; set; } = null!;
    public string? MisuratoreCodice { get; set; }
    /// <summary>Vero se strumento interno MARE.A (da ArcLab Strumenti); falso se del cliente.</summary>
    public bool MisuratoreInterno { get; set; }
    public int? MisuratoreIdStrumentoArcLab { get; set; }
    public int? MisuratoreIdClienteArcLab { get; set; }

    // ── Apparecchiatura (cella/incubatore, contesto) ────────────────
    public string ApparecchiaturaIdentificazione { get; set; } = null!;
    public string? ApparecchiaturaCodice { get; set; }
    public bool ApparecchiaturaInterna { get; set; }
    public int? ApparecchiaturaIdStrumentoArcLab { get; set; }
    public int? ApparecchiaturaIdClienteArcLab { get; set; }

    // ── Committente (03/11/2026): cliente e insediamento ArcLab, separati dallo strumento. Vuoto = interno MARE.A ──
    public int? IdClienteArcLab { get; set; }
    public string? Committente { get; set; }
    public int? IdInsediamentoArcLab { get; set; }
    public string? Insediamento { get; set; }

    // ── Condizioni di esercizio ─────────────────────────────────────
    public double ValoreNominale { get; set; }
    public double Tolleranza { get; set; }
    public double RisoluzioneStrumento { get; set; }
    public double DispersioneMappatura { get; set; }

    // ── Rapporto ────────────────────────────────────────────────────
    public string? NumeroRapporto { get; set; }
    public DateTime DataTaratura { get; set; }
    public DateTime? DataScadenza { get; set; }
    public string? RiferimentoIstruzione { get; set; }
    public string? Responsabile { get; set; }

    /// <summary>Letture (posizione, t rif, tw) serializzate in JSON.</summary>
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
