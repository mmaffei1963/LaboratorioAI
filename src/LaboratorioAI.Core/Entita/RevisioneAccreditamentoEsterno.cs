namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Una revisione dell'elenco prove ACCREDIA rilevata per un laboratorio (dal controllo
/// notturno automatico, o da una verifica manuale). Resta "da confermare" finché un
/// operatore non verifica che sia effettivamente quella corretta e la conferma: il
/// controllo automatico segnala soltanto, non decide mai da solo (coinvolgimento umano
/// richiesto — vedi SPEC_VerificaAccreditamentoLaboratoriEsterni.md).
/// </summary>
public class RevisioneAccreditamentoEsterno
{
    public int IdRevisione { get; set; }

    public int IdAccreditamento { get; set; }

    public int NumeroRevisione { get; set; }
    public DateTime? DataEmissione { get; set; }

    /// <summary>Percorso del PDF scaricato, relativo alla cartella archivio configurata.</summary>
    public string? PercorsoFile { get; set; }

    /// <summary>Impronta SHA-256 del PDF scaricato, per verificarne l'integrità nel tempo.</summary>
    public string? HashFileSha256 { get; set; }

    /// <summary>Quando il controllo notturno (o una verifica manuale) ha rilevato questa revisione.</summary>
    public DateTime DataRilevazione { get; set; }

    public bool Confermata { get; set; }
    public DateTime? DataConferma { get; set; }
    public int? IdUtenteConferma { get; set; }
    public string? NoteConferma { get; set; }

    /// <summary>
    /// Testo estratto dal PDF al momento del rilevamento (libreria di lettura PDF, nessun
    /// OCR). Usato solo per suggerire un abbinamento fra i metodi affidati in ArcLab e le
    /// righe dell'elenco prove ACCREDIA: è un aiuto per l'operatore, mai una fonte di verità —
    /// la qualità dipende da come il laboratorio ha impaginato il proprio PDF.
    /// </summary>
    public string? TestoEstratto { get; set; }
}
