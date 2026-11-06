namespace LaboratorioAI.Core.Entita;

// Invio ai laboratori esterni dell'elenco dei campioni spediti (06/11/2026): Excel con i dati dei campioni (letti da
// ArcLab in sola lettura) inviato per e-mail al laboratorio, con copia a refertazione@mare-a.com. Gli indirizzi dei
// laboratori non sono in ArcLab: si scrivono in ContattiLaboratoriEsterni.

/// <summary>Indirizzi e-mail di un laboratorio esterno di ArcLab (LabEsterno), per l'invio dell'elenco campioni.</summary>
public class ContattoLaboratorioEsterno
{
    public int IdContattoLaboratorioEsterno { get; set; }
    /// <summary>LabEsterno.IdLabEsterno in ArcLab (sola lettura, nessuna vera FK). Unico.</summary>
    public int IdLabEsternoArcLab { get; set; }
    /// <summary>Nome del laboratorio al momento dell'inserimento (ArcLab resta la fonte).</summary>
    public string NomeLaboratorio { get; set; } = "";
    /// <summary>Destinatari, separati da punto e virgola.</summary>
    public string? EmailDestinatari { get; set; }
    /// <summary>Altri indirizzi in copia, separati da punto e virgola (refertazione@mare-a.com è sempre in copia).</summary>
    public string? EmailCopia { get; set; }
    public string? Note { get; set; }
    public bool Attivo { get; set; } = true;
    public DateTime? DataCancellazione { get; set; }
    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
    public int? IdUtente { get; set; }
}

/// <summary>Un invio dell'elenco campioni a un laboratorio esterno (registro ed evidenza).</summary>
public class InvioLaboratorioEsterno
{
    public int IdInvioLaboratorioEsterno { get; set; }
    public int IdLabEsternoArcLab { get; set; }
    public string NomeLaboratorio { get; set; } = "";
    /// <summary>Periodo della data analisi (data ricevimento + ritardo analisi) usato per la selezione.</summary>
    public DateTime Dal { get; set; }
    public DateTime Al { get; set; }
    public string Destinatari { get; set; } = "";
    public string? Copia { get; set; }
    public string Oggetto { get; set; } = "";
    public string? Testo { get; set; }
    public string NomeFile { get; set; } = "";
    /// <summary>L'Excel inviato, conservato come evidenza.</summary>
    public byte[]? FileExcel { get; set; }
    public int NumeroCampioni { get; set; }
    public int NumeroProve { get; set; }
    public string? MessageId { get; set; }
    public bool Riuscito { get; set; }
    public string? Errore { get; set; }
    public DateTime DataInvio { get; set; }
    public int? IdUtente { get; set; }
    public string? NomeUtente { get; set; }
    public List<RigaInvioLaboratorioEsterno> Righe { get; set; } = new();
}

/// <summary>Prova di un campione compresa in un invio: serve a segnalare «già inviato» negli invii successivi.</summary>
public class RigaInvioLaboratorioEsterno
{
    public int IdRigaInvioLaboratorioEsterno { get; set; }
    public int IdInvioLaboratorioEsterno { get; set; }
    public InvioLaboratorioEsterno? Invio { get; set; }
    public int IdCampioneArcLab { get; set; }
    public int IdProvaArcLab { get; set; }
    public string NumeroCampione { get; set; } = "";
}
