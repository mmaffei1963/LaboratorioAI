namespace LaboratorioAI.Documenti;

/// <summary>
/// Testi fissi da riportare in calce all'Ordine di Accettazione, prima dell'allegato tecnico
/// (Allegato A delle Condizioni Generali di Fornitura MARE.A, Mod. Com. 05 Rev. 19).
/// Condivisi tra la generazione PDF (MigraDoc) e Word (OpenXml) perché il testo sia identico.
/// </summary>
internal static class TestiCondizioniAccettazione
{
    public const string TitoloRiquadro = "CONDIZIONI DELL'ORDINE DI ACCETTAZIONE";

    public const string TitoloVerifica = "VERIFICA DELL'ORDINE DI ACCETTAZIONE";
    public const string Verifica =
        "I dati identificativi dei Campioni (descrizione, lotto, data di prelievo, produttore), le prove richieste, " +
        "i metodi e i riferimenti normativi riportati nel presente Ordine sono stati registrati sulla base delle " +
        "informazioni fornite dal Cliente al momento della consegna. Il Cliente è tenuto a verificarli e a segnalare " +
        "qualsiasi difformità a refertazione@mare-a.com prima dell'inizio delle prove e comunque entro 24 ore " +
        "dall'invio del presente documento. In assenza di segnalazione entro tale termine i dati si intendono " +
        "confermati dal Cliente e sono riportati nel Rapporto di Prova come informazioni fornite dal Cliente " +
        "(UNI CEI EN ISO/IEC 17025:2018, p.to 7.8.2). Dopo l'emissione del Rapporto di Prova non sono possibili " +
        "correzioni delle informazioni fornite dal Cliente né riemissioni (Art. 9.7). Le prove già avviate sono " +
        "fatturate; aggiunte o sostituzioni di prove sono possibili solo se il residuo lo consente, a listino.";

    public const string TitoloPrelievo = "PRELIEVO E RAPPRESENTATIVITÀ";
    public const string Prelievo =
        "I Campioni sono stati prelevati dal Cliente o, su sua indicazione, da un operatore MARE.A, secondo scelte del " +
        "Cliente in ordine a unità, quantità, punto e momento del prelievo. MARE.A s.r.l. non esegue attività di " +
        "campionamento e non la ha in scopo di accreditamento. I risultati si riferiranno esclusivamente ai Campioni " +
        "così come ricevuti e non a lotti, partite o produzioni da cui provengono.";

    public const string TitoloCondizioni = "CONDIZIONI DI FORNITURA";

    /// <summary>Testo delle Condizioni di Fornitura con la revisione richiamata dinamicamente.</summary>
    public static string Condizioni(string revisione) =>
        $"La consegna dei Campioni costituisce accettazione integrale delle Condizioni Generali di Fornitura MARE.A " +
        $"{revisione}, disponibili su www.mare-a.com/condizioni e nel Portale clienti insieme all'Offerta, e dei " +
        "corrispettivi qui indicati. Il Cliente dichiara di averne preso visione. Se i Campioni sono riferiti a " +
        "procedimenti, controlli ufficiali, contraddittorio o controversie con terzi, il Cliente è tenuto a " +
        "dichiararlo immediatamente (Art. 12).";
}
