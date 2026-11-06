using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Classificazione di un metodo affidato a un laboratorio esterno (ArcLab, LabEstMetodo)
/// rispetto all'elenco prove ACCREDIA confermato per quel laboratorio: "Accreditato",
/// "Non accreditato" o "Da verificare" (stato iniziale, e stato a cui la riga torna
/// automaticamente se cambia la revisione confermata dell'elenco).
///
/// Non tocca mai ArcLab: <see cref="LaboratorioAI.Core.Servizi.IServizioVerificaMetodiEsterni"/>
/// confronta questa classificazione con il flag LabEstMetodo.AccrLabEsterno di ArcLab solo per
/// mostrarlo come contesto all'operatore, mai per scriverci sopra.
/// </summary>
public class VerificaMetodoAccreditamento
{
    public int IdVerifica { get; set; }

    /// <summary>Laboratorio esterno monitorato (nostro <see cref="AccreditamentoLabEsterno"/>).</summary>
    public int IdAccreditamento { get; set; }

    /// <summary>LabEstMetodo.IdLabEstMetodo in ArcLab (sola lettura, nessuna vera FK).</summary>
    public int IdLabEstMetodoArcLab { get; set; }

    public StatoVerificaMetodo Stato { get; set; } = StatoVerificaMetodo.DaVerificare;

    /// <summary>
    /// Quale revisione confermata dell'elenco prove è stata usata per questa classificazione.
    /// Se la revisione confermata del laboratorio cambia, la classificazione è considerata
    /// superata (va mostrata di nuovo come "da verificare") anche se questo campo non cambia.
    /// </summary>
    public int? IdRevisioneVerificata { get; set; }

    public string? Note { get; set; }
    public DateTime? DataVerifica { get; set; }
    public int? IdUtenteVerifica { get; set; }

    public DateTime DataCreazione { get; set; }
    public DateTime DataUltimaModifica { get; set; }
}
