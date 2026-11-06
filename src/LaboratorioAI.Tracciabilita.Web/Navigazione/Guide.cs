namespace LaboratorioAI.Tracciabilita.Web.Navigazione;

/// <summary>Una guida d'uso: codice (= nome della vista Views/Guide/_{Codice}.cshtml), pagine a cui si riferisce, destinatari.</summary>
public record Guida(string Codice, string Titolo, string Descrizione, string[] Percorsi, bool PerGestore, DateOnly Aggiornata);

/// <summary>
/// Guide d'uso delle funzioni (stesso schema di Laboratorio AI). Il pulsante «? Guida» in testata apre la guida della
/// pagina in un pannello laterale; altrove porta all'elenco /Guide.
/// Per aggiungere una guida: una riga qui e la vista parziale Views/Guide/_{Codice}.cshtml.
/// </summary>
public static class Guide
{
    private static readonly DateOnly Oggi = new(2026, 10, 27);

    public static readonly IReadOnlyList<Guida> Elenco = new List<Guida>
    {
        new("Panoramica", "Come funziona Tracciato",
            "Il giro completo, dall'ordine al fornitore fino al richiamo: dove si fa ogni cosa e in che ordine.",
            new[] { "/" }, false, Oggi),
        new("Anagrafiche", "Fornitori e prodotti",
            "Tenere in ordine l'agenda dei fornitori e l'elenco dei prodotti: matrice, unità, fornitori abbinati.",
            new[] { "/Anagrafiche" }, false, Oggi),
        new("Categorie", "Categorie e profili di controllo",
            "Gestire le matrici delle materie prime e i controlli da fare all'arrivo di ognuna; mettere fuori uso quelle che non servono.",
            new[] { "/Anagrafiche/Categorie", "/Anagrafiche/Profili" }, false, Oggi),
        new("Ordini", "Ordini ai fornitori",
            "Fare un ordine, mandarlo per e-mail o stamparlo, seguire il quadro delle consegne.",
            new[] { "/Ordini" }, false, Oggi),
        new("Accettazioni", "Accettazioni",
            "Registrare la merce che arriva: controlli, giudizio, etichetta con QR, carico in magazzino, correzioni.",
            new[] { "/Accettazioni" }, false, Oggi),
        new("Magazzino", "Magazzino",
            "Cosa c'è in magazzino, quali lotti mettere in uso e quando segnarli esauriti.",
            new[] { "/Magazzino" }, false, Oggi),
        new("Lotti", "Lotti e rintracciabilità",
            "Trovare un lotto, leggerne la scheda, vedere da dove viene e dove è finito, sbloccarlo.",
            new[] { "/Lotti" }, false, Oggi),
        new("Ricette", "Ricette e «Oggi ho prodotto»",
            "Preparare le ricette e registrare la produzione del giorno con i lotti realmente usati.",
            new[] { "/Ricette" }, false, Oggi),
        new("Produzioni", "Produzioni",
            "Ritrovare cosa è stato prodotto, con quali lotti e quanti kg.",
            new[] { "/Produzioni" }, false, Oggi),
        new("Spedizioni", "Spedizioni",
            "Registrare cosa parte per ogni cliente e correggere con un annullamento.",
            new[] { "/Spedizioni" }, false, Oggi),
        new("Clienti", "Clienti",
            "L'agenda dei clienti: serve per le spedizioni e per avvisarli in caso di richiamo.",
            new[] { "/Clienti" }, false, Oggi),
        new("Richiamo", "Richiamo e simulazione",
            "Dal lotto sospetto ai clienti da avvisare: provare la procedura e, se serve, avviare il richiamo.",
            new[] { "/Richiamo" }, false, Oggi),
        new("Impostazioni", "Impostazioni",
            "Scegliere come registrare i kg consumati per ingrediente in ogni produzione.",
            new[] { "/Impostazioni" }, false, Oggi),
        new("Gestione", "Gestione del servizio",
            "Per il personale MARE.A: aziende, sedi, utenti, inviti e registro attività.",
            new[] { "/Gestione" }, true, Oggi),
    };

    public static IEnumerable<Guida> Visibili(bool gestore) => Elenco.Where(g => g.PerGestore == gestore);

    public static Guida? PerCodice(string codice, bool gestore) =>
        Visibili(gestore).FirstOrDefault(g => string.Equals(g.Codice, codice, StringComparison.OrdinalIgnoreCase));

    /// <summary>La guida della pagina aperta: quella con il percorso più lungo che corrisponde.</summary>
    public static Guida? PerPercorso(string? percorso, bool gestore)
    {
        var p = string.IsNullOrEmpty(percorso) ? "/" : percorso;
        if (p.Length > 1) p = p.TrimEnd('/');
        Guida? migliore = null;
        var lunghezza = -1;
        foreach (var g in Visibili(gestore))
            foreach (var x in g.Percorsi)
            {
                var corrisponde = x == "/" ? p == "/" : p.Equals(x, StringComparison.OrdinalIgnoreCase) || p.StartsWith(x + "/", StringComparison.OrdinalIgnoreCase);
                if (corrisponde && x.Length > lunghezza) { migliore = g; lunghezza = x.Length; }
            }
        return migliore;
    }
}
