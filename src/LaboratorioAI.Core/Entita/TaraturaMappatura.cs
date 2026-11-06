namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Mappatura di un'apparecchiatura termostatica (Modulo 04-03): rilievo della
/// temperatura del CDR in più ripiani/posizioni, alla temperatura di esercizio.
/// L'apparecchiatura può essere interna MARE.A (ArcLab Strumenti) o del cliente.
/// Le letture (posizione, t rif) sono in JSON.
/// </summary>
public class TaraturaMappatura
{
    public int IdMappatura { get; set; }

    public int IdCampioneRiferimento { get; set; }
    public CampioneRiferimento? CampioneRiferimento { get; set; }

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

    public double ValoreNominale { get; set; }
    public double Tolleranza { get; set; }
    public double RisoluzioneCdr { get; set; }

    public string? NumeroRapporto { get; set; }
    public DateTime DataMappatura { get; set; }
    public DateTime? DataScadenza { get; set; }
    public string? RiferimentoIstruzione { get; set; }
    public string? Responsabile { get; set; }

    /// <summary>Letture (posizione, t rif) serializzate in JSON.</summary>
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

/// <summary>Esito archiviato di una validazione del motore di calcolo della mappatura.</summary>
public class EsitoValidazioneMappatura
{
    public int IdEsito { get; set; }
    public DateTime DataEsecuzione { get; set; }
    public bool Validato { get; set; }
    public int NumeroOk { get; set; }
    public int NumeroKo { get; set; }
    public double Tolleranza { get; set; }
    public string VersioneSet { get; set; } = null!;
    public string? VersioneApplicazione { get; set; }
    public string Dettaglio { get; set; } = null!;
    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
    public string? Operatore { get; set; }
}

/// <summary>Analisi anti-errore AI di una mappatura (archivio-first).</summary>
public class AnalisiMappatura
{
    public int IdAnalisi { get; set; }
    public int IdMappatura { get; set; }
    public TaraturaMappatura? Mappatura { get; set; }
    public DateTime DataAnalisi { get; set; }
    public string Firma { get; set; } = null!;
    public bool AnomalieDeterministiche { get; set; }
    public string? Controlli { get; set; }
    public string? Esito { get; set; }
    public string Testo { get; set; } = null!;
    public string? ModelloAi { get; set; }
    public int TokenInput { get; set; }
    public int TokenOutput { get; set; }
    public string? VersioneApplicazione { get; set; }
    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
}
