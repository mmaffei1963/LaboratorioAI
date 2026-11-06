namespace LaboratorioAI.Core.Servizi;

/// <summary>
/// Estrae il testo grezzo da un PDF (nessun OCR: funziona solo se il PDF contiene testo
/// selezionabile, come i PDF generati da ACCREDIA). Usato per suggerire un abbinamento fra
/// i metodi affidati in ArcLab e le righe dell'elenco prove ACCREDIA — vedi
/// <see cref="IServizioVerificaMetodiEsterni"/>. Non è mai una fonte di verità: solo un aiuto.
/// </summary>
public interface IEstrattoreTestoPdf
{
    /// <summary>Estrae il testo di tutte le pagine, concatenato. Stringa vuota se il PDF non ha testo estraibile.</summary>
    string EstraiTesto(byte[] pdf);
}
