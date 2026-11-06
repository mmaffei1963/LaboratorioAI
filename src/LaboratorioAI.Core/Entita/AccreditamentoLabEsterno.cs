namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Anagrafica di raccordo fra un laboratorio esterno (ArcLab, LabEsterno) e il suo
/// accreditamento ACCREDIA: numero e sede correnti secondo la nuova anagrafica unica
/// (dal 2025 possono differire dal vecchio numero eventualmente scritto nel nome in
/// ArcLab, per via della rinumerazione), più l'ultima revisione dell'elenco prove
/// CONFERMATA come valida da un operatore.
///
/// Il controllo automatico notturno (<see cref="Servizi.IServizioAccreditamentiEsterni"/>)
/// confronta la revisione pubblicata su ACCREDIA con <see cref="UltimaRevisioneConfermata"/>;
/// se diversa crea una nuova <see cref="RevisioneAccreditamentoEsterno"/> da confermare e
/// avvisa via email — non aggiorna mai da solo la revisione confermata: serve sempre la
/// verifica di un operatore (vedi SPEC_VerificaAccreditamentoLaboratoriEsterni.md).
/// </summary>
public class AccreditamentoLabEsterno
{
    public int IdAccreditamento { get; set; }

    /// <summary>LabEsterno.IdLabEsterno in ArcLab (sola lettura, nessuna vera FK).</summary>
    public int IdLabEsternoArcLab { get; set; }

    /// <summary>Nome del laboratorio, per leggibilità (snapshot; ArcLab resta la fonte).</summary>
    public string NomeLaboratorio { get; set; } = "";

    /// <summary>Numero di accreditamento ACCREDIA corrente (es. "00253"), senza lettera schema.</summary>
    public string NumeroAccreditamento { get; set; } = "";

    /// <summary>Sede/classificazione ACCREDIA della sede monitorata (es. "A", "D").</summary>
    public string Sede { get; set; } = "";

    /// <summary>Numero dell'ultima revisione dell'elenco prove confermata come valida da un operatore.</summary>
    public int? UltimaRevisioneConfermata { get; set; }

    /// <summary>Note libere (es. indirizzo ACCREDIA diverso da quello in ArcLab, da verificare).</summary>
    public string? Note { get; set; }

    /// <summary>Stato dell'accreditamento secondo la banca dati ACCREDIA all'ultimo controllo (null = mai letto).</summary>
    public Enumerazioni.StatoAccreditamentoAccredia? StatoAccredia { get; set; }

    /// <summary>Quando è stato letto lo stato (UTC).</summary>
    public DateTime? DataStatoAccredia { get; set; }

    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }
}
