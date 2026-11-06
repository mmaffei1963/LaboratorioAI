using LaboratorioAI.Core.Enumerazioni;

namespace LaboratorioAI.Core.Servizi;

/// <summary>Un metodo affidato al laboratorio, con la classificazione attuale e un eventuale suggerimento.</summary>
public record MetodoDaVerificare(
    int IdLabEstMetodo,
    int IdMetodo,
    string? CodMetodo,
    string? RiferimentoNormativo,
    string? NomeParametro,
    bool AccreditatoArcLab,
    StatoVerificaMetodo Stato,
    string? Note,
    /// <summary>Frammento del PDF dove il suggerimento pensa di aver trovato una corrispondenza (contesto per l'operatore).</summary>
    string? SuggerimentoTesto,
    /// <summary>Quanto il suggerimento è sicuro (0-1): quota di token del riferimento normativo trovati nel PDF.</summary>
    double? PunteggioSuggerimento);

/// <summary>
/// Confronta i metodi affidati a un laboratorio esterno (ArcLab, LabEstMetodo — sola lettura)
/// con l'elenco prove ACCREDIA confermato per quel laboratorio, e tiene la classificazione
/// (Accreditato/Non accreditato/Da verificare) decisa da un operatore dopo aver controllato il PDF.
///
/// Il confronto testuale automatico è solo un suggerimento: non decide mai da solo se un metodo
/// è accreditato, esattamente come <see cref="IServizioAccreditamentiEsterni"/> non conferma mai
/// da solo una revisione. Se cambia la revisione confermata dell'elenco, le classificazioni fatte
/// sulla revisione precedente tornano "da verificare".
/// </summary>
public interface IServizioVerificaMetodiEsterni
{
    /// <summary>Metodi affidati al laboratorio con classificazione e suggerimento, più recenti/incerti prima.</summary>
    Task<IReadOnlyList<MetodoDaVerificare>> ElencoAsync(int idAccreditamento, CancellationToken ct = default);

    /// <summary>Salva la classificazione di un metodo decisa da un operatore dopo aver controllato il PDF.</summary>
    Task SalvaVerificaAsync(
        int idAccreditamento, int idLabEstMetodo, StatoVerificaMetodo stato,
        int idUtente, string? note, CancellationToken ct = default);
}
