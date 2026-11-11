namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// I nomi delle domande che dal modello non si ricavano (11/11/2026).
///
/// L'etichetta di una domanda viene letta dalla prima cella della sua
/// riga nel modello Word. Quasi sempre funziona, ma in alcune tabelle
/// quella cella dice un'altra cosa: nel modulo degli autorizzati al
/// backup c'e' scritto «NOME» sopra quattro campi diversi, e nella
/// tabella dei gestionali la prima cella contiene l'indirizzo di un
/// fornitore d'esempio. Risultato: sette domande che si chiamano tutte
/// «NOME» e tre che si chiamano «GRUPPO ZUCCHETTI - Via Solferino…».
///
/// Qui stanno i nomi decisi a mano per quelle. Il resto continua a
/// venire dai modelli: correggere la cella nel Word resta la strada
/// giusta, questa tabella serve finche' non la si corregge.
///
/// A differenza dei passi, si applica **a ogni rilettura dei modelli**:
/// il nome di una domanda appartiene al codice, come il tipo e le voci
/// della tendina, mentre alla persona appartengono il passo e
/// l'acceso/spento. Rinominare una domanda dalla pagina non serve:
/// alla prima rilettura torna quella di qui. Per cambiarla davvero si
/// cambia questa tabella, o si corregge la cella nel modello Word.
/// </summary>
internal static class EtichetteDecise
{
    private static readonly IReadOnlyDictionary<string, string> Nomi =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Modulo «autorizzato alla gestione delle copie di sicurezza»:
            // nel Word le quattro celle dicono tutte «NOME».
            ["BACKUP_AUTORIZZATO_NOME"] = "Backup — nome di chi lo gestisce",
            ["BACKUP_AUTORIZZATO_COGNOME"] = "Backup — cognome di chi lo gestisce",
            ["BACKUP_AUTORIZZATO_FUNZIONE"] = "Backup — funzione aziendale di chi lo gestisce",
            ["BACKUP_AUTORIZZATO_QUALIFICA"] = "Backup — qualifica di chi lo gestisce",

            // Stesso modulo, parte delle credenziali.
            ["CREDENZIALI_AUTORIZZATO_NOME"] = "Credenziali — nome di chi le custodisce",
            ["CREDENZIALI_AUTORIZZATO_COGNOME"] = "Credenziali — cognome di chi le custodisce",
            ["CREDENZIALI_AUTORIZZATO_FUNZIONE"] = "Credenziali — funzione aziendale di chi le custodisce",
            ["CREDENZIALI_AUTORIZZATO_QUALIFICA"] = "Credenziali — qualifica di chi le custodisce",

            // Chi sostituisce il legale rappresentante in caso di
            // violazione dei dati.
            ["SOSTITUTO_LR_NOME"] = "Sostituto del legale rappresentante — nome",
            ["SOSTITUTO_LR_COGNOME"] = "Sostituto del legale rappresentante — cognome",
            ["SOSTITUTO_LR_FUNZIONE"] = "Sostituto del legale rappresentante — funzione aziendale",
            ["SOSTITUTO_LR_QUALIFICA"] = "Sostituto del legale rappresentante — qualifica",

            // Il consulente MARE.A che segue il cliente: nel contratto le
            // tre celle dicono tutte «Nome del consulente».
            ["CONSULENTE_NOME"] = "Consulente MARE.A — nome e cognome",
            ["CONSULENTE_CELLULARE"] = "Consulente MARE.A — cellulare",
            ["CONSULENTE_EMAIL"] = "Consulente MARE.A — e-mail",

            // I tre sistemi di prenotazione del Registro. Nel Word la
            // prima cella dice «STANDARD» per i nomi e l'indirizzo di un
            // fornitore d'esempio per i fornitori. I nomi per esteso li
            // scrive il modello stesso, al paragrafo «Sistemi di gestione
            // in background interfacciati con il CRM».
            ["CRM_NOME"] = "CRM — nome del prodotto",
            ["CRM_FORNITORE"] = "CRM — fornitore",
            ["CRS_NOME"] = "CRS, sistema centrale di prenotazione — nome del prodotto",
            ["CRS_FORNITORE"] = "CRS, sistema centrale di prenotazione — fornitore",
            ["CRO_NOME"] = "CRO, ufficio centrale prenotazioni — nome del prodotto",
            ["CRO_FORNITORE"] = "CRO, ufficio centrale prenotazioni — fornitore",

            // Buste paga: le due celle dicono tutte e due «RICEZIONE BUSTE PAGA».
            ["BUSTE_PAGA_RICEZIONE"] = "Come si ricevono le buste paga",
            ["BUSTE_PAGA_MAIL"] = "Casella a cui arrivano le buste paga"
        };

    /// <summary>Il nome deciso per questa domanda, se ce n'e' uno.</summary>
    public static string? Per(string codice) =>
        Nomi.TryGetValue(codice, out var nome) ? nome : null;
}
