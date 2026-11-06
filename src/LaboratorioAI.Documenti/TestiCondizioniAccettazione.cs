namespace LaboratorioAI.Documenti;

/// <summary>
/// Testi fissi da riportare in calce all'Ordine di Accettazione, prima dell'allegato tecnico
/// (Allegato A delle Condizioni Generali di Fornitura MARE.A, Mod. Com. 05 Rev. 19).
/// Condivisi tra la generazione PDF (MigraDoc) e Word (OpenXml) perché il testo sia identico.
/// </summary>
public static class TestiCondizioniAccettazione
{
    public const string TitoloRiquadro = "CONDIZIONI DELL'ORDINE DI ACCETTAZIONE";

    public const string TitoloVerifica = "VERIFICA DELL'ORDINE DI ACCETTAZIONE";
    public const string Verifica =
        "I dati identificativi dei Campioni, le prove richieste, " +
        "i metodi, gli eventuali riferimenti normativi e le eventuali richieste di giudizio di conformità o di parere " +
        "riportati nel presente Ordine sono stati registrati sulla base delle informazioni fornite dal Cliente al " +
        "momento della consegna. Il Cliente è tenuto a verificarli e a segnalare qualsiasi difformità a " +
        "refertazione@mare-a.com prima dell'inizio delle prove e comunque entro 24 ore dalla pubblicazione del presente " +
        "documento nel Portale clienti; fanno fede la data e l'ora di pubblicazione registrate dal Portale. In assenza di segnalazione entro tale termine i dati si intendono confermati dal " +
        "Cliente e sono riportati nel Rapporto di Prova come informazioni fornite dal Cliente (UNI CEI EN ISO/IEC " +
        "17025:2018, p.to 7.8.2). Dopo l'emissione del Rapporto di Prova non sono possibili correzioni delle informazioni " +
        "fornite dal Cliente né riemissioni (Condizioni Generali di Fornitura). Le prove già avviate sono fatturate; aggiunte o sostituzioni di " +
        "prove sono possibili solo se il residuo lo consente, al prezzo comunicato dal Laboratorio. Per i Campioni " +
        "indicati come «Autocontrollo: non dichiarato dal Cliente» il Cliente non ha comunicato che rientrano nelle " +
        "procedure di autocontrollo: l'indicazione non esclude che vi rientrino e non incide sull'esecuzione " +
        "delle prove; se un Campione rientra nell'autocontrollo, il Cliente lo segnala entro il termine di verifica del " +
        "presente Ordine. Il presente Ordine è disponibile nel Portale clienti; l'avviso e-mail ha funzione di cortesia.";

    public const string TitoloPrelievo = "PRELIEVO E RAPPRESENTATIVITÀ";
    public const string Prelievo =
        "I Campioni sono stati prelevati dal Cliente o, su sua richiesta, da un operatore MARE.A secondo le Istruzioni " +
        "operative di prelievo; in entrambi i casi unità, punti, quantità e momento del prelievo sono stati scelti dal " +
        "Cliente. Quando il prelievo è eseguito da un operatore MARE.A, i relativi dati sono riportati nell'allegato " +
        "tecnico e il presente Ordine sostituisce il verbale di prelievo. MARE.A s.r.l. non predispone piani " +
        "di campionamento e non svolge attività di campionamento accreditata. I risultati si riferiranno esclusivamente " +
        "ai Campioni così come ricevuti e non a lotti, partite, impianti o produzioni da cui provengono.";

    public const string TitoloCondizioni = "CONDIZIONI DI FORNITURA";

    /// <summary>Testo delle Condizioni di Fornitura con la revisione richiamata dinamicamente.</summary>
    public static string Condizioni(string revisione) =>
        $"La consegna dei Campioni costituisce accettazione integrale delle Condizioni Generali di Fornitura MARE.A " +
        $"{revisione}, disponibili su www.mare-a.com/condizioni e nel Portale clienti insieme all'Offerta, e dei " +
        "corrispettivi qui indicati. Il Cliente dichiara di averne preso visione. Se i Campioni sono riferiti a " +
        "procedimenti, controlli ufficiali, contraddittorio o controversie con terzi, il Cliente è tenuto a " +
        "dichiararlo immediatamente.";
    // ── Versione da firmare per i clienti privati (14/10/2026) ─────────────────
    // Predefiniti: si cambiano in Impostazioni documenti quando arrivano le Condizioni per i privati.

    public const string VerificaPrivati =
        "I dati identificativi dei Campioni, le prove richieste, i metodi, gli eventuali riferimenti normativi e le eventuali " +
        "richieste di giudizio di conformità o di parere riportati nel presente Ordine sono stati registrati sulla base delle " +
        "informazioni fornite dal Cliente al momento della consegna. Il Cliente è tenuto a verificarli e a segnalare qualsiasi " +
        "difformità a refertazione@mare-a.com prima dell'inizio delle prove e comunque entro 24 ore dalla sottoscrizione del " +
        "presente Ordine; fanno fede la data e l'ora indicate accanto alla firma. In assenza di segnalazione entro tale termine " +
        "i dati si intendono confermati dal Cliente e sono riportati nel Rapporto di Prova come informazioni fornite dal Cliente " +
        "(UNI CEI EN ISO/IEC 17025:2018, p.to 7.8.2). Dopo l'emissione del Rapporto di Prova non sono possibili correzioni delle " +
        "informazioni fornite dal Cliente né riemissioni. Le prove già avviate sono fatturate; aggiunte o sostituzioni di prove " +
        "sono possibili solo se il residuo lo consente, al prezzo comunicato dal Laboratorio. Il Cliente riceve copia del " +
        "presente Ordine al momento della sottoscrizione.";

    public const string CondizioniPrivati =
        "La consegna dei Campioni e la sottoscrizione del presente Ordine costituiscono accettazione delle Condizioni Generali " +
        "di Fornitura MARE.A [revisione], disponibili su www.mare-a.com/condizioni e in copia presso l'accettazione, e dei " +
        "corrispettivi qui indicati. Il Cliente dichiara di averne preso visione. Se i Campioni sono riferiti a procedimenti, " +
        "controlli ufficiali, contraddittorio o controversie con terzi, il Cliente è tenuto a dichiararlo immediatamente.";

    public const string FirmaPrivati =
        "Il Cliente dichiara di aver verificato i dati del presente Ordine e di accettarlo, insieme alle Condizioni Generali di " +
        "Fornitura sottoscritte contestualmente.";

    public const string TitoloConsegnaRapporto = "CONSEGNA DEL RAPPORTO DI PROVA";
}
