using LaboratorioAI.Core.Servizi;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Testi dell'Ordine di Accettazione per i clienti privati (06/11/2026), adattati alle Condizioni di Fornitura per i
/// clienti privati (Mod. Com. 05A) e all'informativa privacy (Mod. Com. 05C), che si consegnano stampate insieme
/// all'Ordine (Mod. Com. 05B). Gli articoli citati sono quelli delle Condizioni per i privati.
/// </summary>
public static class TestiOrdinePrivato
{
    private static readonly string Condizioni = RegoleRichiestePrivati.CondizioniPrivati;
    private static readonly string Informativa = RegoleRichiestePrivati.InformativaPrivacy;

    public const string TitoloRiquadro = "CONDIZIONI DELL'ORDINE DI ACCETTAZIONE";

    public const string TitoloVerifica = "VERIFICA DELL'ORDINE DI ACCETTAZIONE";
    public const string Verifica =
        "I dati del Campione (descrizione, punto, data e ora del prelievo), le prove richieste, i metodi e l'eventuale " +
        "richiesta di giudizio di conformità riportati nel presente Ordine sono registrati dal Laboratorio sulla base di " +
        "quanto dichiarato dal Cliente, che li controlla e li conferma con la firma; nel Rapporto di Prova sono riportati " +
        "come informazioni fornite dal Cliente (UNI CEI EN ISO/IEC 17025:2018, p.to 7.8.2; Art. 3.2). Il Cliente può " +
        "correggere i dati e modificare le prove fino all'emissione del Rapporto di Prova, scrivendo a " +
        "refertazione@mare-a.com o rivolgendosi al Laboratorio; dopo l'emissione non sono emessi Rapporti di Prova " +
        "sostitutivi per errori o modifiche dei dati forniti dal Cliente (Art. 3.4). Prima dell'inizio delle prove il " +
        "Cliente può rinunciarvi e ritirare il Campione senza costi; le prove già avviate sono dovute (Art. 2.2).";

    public const string TitoloPrelievo = "PRELIEVO E RAPPRESENTATIVITÀ";
    public const string Prelievo =
        "Il Campione è stato prelevato dal Cliente, che ha scelto in autonomia che cosa prelevare, da quale punto, in quale " +
        "quantità e in quale momento, ed è responsabile della sua integrità, conservazione e trasporto fino alla consegna " +
        "(Art. 3.1). Il prelievo non è un campionamento ai sensi della UNI CEI EN ISO/IEC 17025: il Laboratorio non " +
        "predispone piani di campionamento e non svolge attività di campionamento accreditata. I risultati si riferiscono " +
        "esclusivamente al Campione così come ricevuto, nel punto e nel momento in cui è stato prelevato, e ai soli " +
        "parametri analizzati (Art. 3.5).";

    public const string TitoloRapporto = "RAPPORTO DI PROVA, GIUDIZI E TEMPI";
    public const string Rapporto =
        "Senza richiesta di giudizio di conformità il Rapporto di Prova riporta i soli risultati e il confronto con i " +
        "limiti spetta al Cliente; il giudizio, se richiesto, confronta il risultato con il valore di riferimento senza " +
        "considerare l'incertezza di misura (Art. 5.2). Pareri e interpretazioni non sono attività accreditate e sono " +
        "forniti solo se richiesti (Art. 5.3). I tempi di consegna indicati sono indicativi e decorrono dalla consegna " +
        "del Campione (Art. 6.1). Le prove indicate con (§) sono eseguite dal laboratorio terzo indicato (Art. 4.2).";

    public const string TitoloCondizioni = "CONDIZIONI DI FORNITURA";
    public static string TestoCondizioni =>
        "La consegna del Campione e la sottoscrizione del presente Ordine concludono il contratto e costituiscono " +
        $"accettazione delle Condizioni di Fornitura per i clienti privati ({Condizioni}) e dei prezzi qui indicati, " +
        "IVA inclusa (Artt. 2.1 e 8.1). Fino al pagamento il Laboratorio può non iniziare le prove e non rilasciare il " +
        $"Rapporto di Prova (Art. 8.3). Al Cliente sono consegnate copia del presente Ordine, delle Condizioni ({Condizioni}) " +
        $"e dell'informativa sul trattamento dei dati personali ({Informativa}).";

    public static string Firma =>
        "Il Cliente dichiara di aver controllato i dati del presente Ordine e di confermarli, di aver ricevuto copia delle " +
        $"Condizioni di Fornitura per i clienti privati ({Condizioni}), di averle lette e di accettarle, di sapere che la " +
        "scelta di che cosa prelevare, dove e quando spetta a lui e che i risultati si riferiscono esclusivamente al " +
        "Campione così come consegnato, e autorizza l'esecuzione delle prove indicate con (§) presso il laboratorio terzo " +
        "indicato. Dichiara di aver ricevuto l'informativa sul trattamento dei dati personali clienti privati.";

    public const string TitoloApprovazione = "APPROVAZIONE SPECIFICA (artt. 1341 e 1342 c.c.)";
    public static string Approvazione =>
        "Il Cliente dichiara di aver letto attentamente e di approvare specificamente, ai sensi degli artt. 1341 e 1342 del " +
        $"codice civile, le seguenti clausole delle Condizioni di Fornitura per i clienti privati ({Condizioni}): " +
        "Art. 3.3 – il Laboratorio non risponde dei risultati influenzati da prelievo, conservazione, trasporto o dati " +
        "forniti dal Cliente; Art. 3.4 – nessun Rapporto di Prova sostitutivo per errori nei dati forniti dal Cliente dopo " +
        "l'emissione; Art. 4.2 – esecuzione di prove presso laboratori terzi; Art. 8.3 – prove e Rapporto di Prova non " +
        "rilasciati fino al pagamento; Art. 9.2 – esclusioni di responsabilità.";

    public const string TitoloConsenso = "CONSENSO FACOLTATIVO (informativa privacy, art. 13 GDPR)";
    public static string Consenso =>
        $"Il consenso qui sotto è facoltativo, non è necessario per l'esecuzione delle prove e può essere revocato in " +
        "qualsiasi momento scrivendo a info@mare-a.com.";
    public const string DomandaConsenso =
        "Acconsento a essere contattato a mezzo e-mail, sms o app di messaggistica per l'invio di aggiornamenti sui servizi del Laboratorio.";
}
