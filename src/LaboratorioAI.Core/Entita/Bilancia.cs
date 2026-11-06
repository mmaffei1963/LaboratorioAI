namespace LaboratorioAI.Core.Entita;

/// <summary>Pesiera: set di masse campione riutilizzabile per le tarature di bilance.</summary>
public class Pesiera
{
    public int IdPesiera { get; set; }
    public string Identificazione { get; set; } = null!;
    public string? Note { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public ICollection<MassaCampione> Masse { get; set; } = new List<MassaCampione>();
}

/// <summary>Singola massa campione della pesiera (con dati del certificato LAT).</summary>
public class MassaCampione
{
    public int IdMassa { get; set; }
    public int IdPesiera { get; set; }
    public Pesiera? Pesiera { get; set; }

    public double ValoreNominale { get; set; }
    public double ValoreConvenzionale { get; set; }
    /// <summary>Incertezza estesa U (k=2) in mg.</summary>
    public double IncertezzaEstesa { get; set; }
    /// <summary>Incertezza tipo u in mg.</summary>
    public double IncertezzaTipo { get; set; }

    /// <summary>Errore massimo ammesso mpe della massa (mg), da classe OIML.</summary>
    public double Mpe { get; set; }
    /// <summary>Classe OIML della massa (es. E2, F1).</summary>
    public string? ClasseOiml { get; set; }

    public string? CertificatoLat { get; set; }
    public DateTime? DataCertificato { get; set; }
    public DateTime? DataScadenza { get; set; }
    public int? CentroLat { get; set; }
    public int Ordine { get; set; }
}

/// <summary>
/// Taratura di una bilancia (Modulo 45, EURAMET cg-18). La bilancia può essere
/// interna MARE.A (ArcLab Strumenti) o del cliente. Le prove (eccentricità,
/// ripetibilità, linearità) sono serializzate in JSON.
/// </summary>
public class TaraturaBilancia
{
    public int IdTaratura { get; set; }

    public int? IdPesiera { get; set; }
    public Pesiera? Pesiera { get; set; }

    public string BilanciaIdentificazione { get; set; } = null!;
    public string? BilanciaModello { get; set; }
    public bool BilanciaInterna { get; set; }
    public int? BilanciaIdStrumentoArcLab { get; set; }
    public int? BilanciaIdClienteArcLab { get; set; }

    // ── Committente (03/11/2026): cliente e insediamento ArcLab, separati dallo strumento. Vuoto = interno MARE.A ──
    public int? IdClienteArcLab { get; set; }
    public string? Committente { get; set; }
    public int? IdInsediamentoArcLab { get; set; }
    public string? Insediamento { get; set; }

    public double Portata { get; set; }
    public double UnitaFormato { get; set; }
    public double CoefficienteSensibilitaTermica { get; set; }
    public double MaxIncertezzaAmmessa { get; set; } = 0.02;
    public double TemperaturaTaratura { get; set; }
    public double TemperaturaUtilizzo { get; set; }

    // ── Metadati per il rapporto (come nel foglio) ─────────────────
    public double CampoTaratura { get; set; }
    public double TempoStabilizzazione { get; set; }
    public double TempoIntervalloLettura { get; set; }
    public double MaxVariazioneTemperatura { get; set; }
    public string? Tipo { get; set; }
    public string? Sda { get; set; }
    public string? TermometroUtilizzato { get; set; }

    public string? NumeroRapporto { get; set; }
    public DateTime DataTaratura { get; set; }
    public DateTime? DataScadenza { get; set; }
    public string? RiferimentoIstruzione { get; set; }
    public string? Responsabile { get; set; }

    /// <summary>Prove (eccentricità, ripetibilità, linearità) serializzate in JSON.</summary>
    public string DatiProve { get; set; } = null!;

    public string? Note { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }

    public int? IdUtente { get; set; }
    public Utente? Utente { get; set; }
    public string? VersioneApplicazione { get; set; }
}

/// <summary>Esito archiviato di una validazione del motore di calcolo delle bilance.</summary>
public class EsitoValidazioneBilancia
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

/// <summary>Analisi anti-errore AI di una taratura di bilancia (archivio-first).</summary>
public class AnalisiBilancia
{
    public int IdAnalisi { get; set; }
    public int IdTaratura { get; set; }
    public TaraturaBilancia? Taratura { get; set; }
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
