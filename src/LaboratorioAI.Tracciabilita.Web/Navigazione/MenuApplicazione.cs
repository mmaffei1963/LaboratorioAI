namespace LaboratorioAI.Tracciabilita.Web.Navigazione;

/// <summary>Una funzione del menu / scheda della pagina principale.</summary>
public record VoceMenu(string Titolo, string Url, string Descrizione);

/// <summary>
/// Una cartella della pagina principale e un gruppo del menu in testata (stesso schema di Laboratorio AI).
/// <paramref name="Titolo"/> è il nome breve del menu, <paramref name="TitoloHome"/> quello della cartella;
/// <paramref name="Icona"/> è un tracciato SVG 24×24 a solo contorno.
/// </summary>
public record GruppoMenu(string Titolo, string TitoloHome, string Descrizione, string Icona, IReadOnlyList<VoceMenu> Voci)
{
    /// <summary>Identificativo per la finestra della cartella, ricavato dal titolo.</summary>
    public string Codice => new string(TitoloHome.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

/// <summary>Icone delle cartelle: tracciati SVG semplici, a solo contorno (nessuna libreria esterna).</summary>
public static class Icone
{
    public const string Ordini = "M9 4h6v3H9zM7 5H5v16h14V5h-2M9 12h6M9 16h4";
    public const string Magazzino = "M3 8l9-5 9 5v9l-9 5-9-5zM3 8l9 5 9-5M12 13v9";
    public const string Produzione = "M4 20V10l5 3V10l5 3V6h6v14zM8 20v-3M13 20v-3";
    public const string Spedizioni = "M3 6h11v10H3zM14 9h4l3 3v4h-7M5 18a2 2 0 1 0 4 0a2 2 0 1 0-4 0M15 18a2 2 0 1 0 4 0a2 2 0 1 0-4 0";
    public const string Richiamo = "M12 4l9 16H3zM12 10v4M12 17v.5";
    public const string Anagrafiche = "M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 1-3-3zM5 17a3 3 0 0 1 3-3h11";
    public const string Impostazioni = "M4 6h16M4 12h16M4 18h16M9 4v4M15 10v4M8 16v4";
    public const string Gestione = "M4 8h16v11H4zM9 8V5h6v3M4 13h16";
}

/// <summary>
/// Definizione UNICA della navigazione: da qui sono generati sia il menu in testata (_Layout) sia le schede della
/// pagina principale (Home/Index), così aggiungere una pagina aggiorna entrambi.
/// Per aggiungere una pagina: una riga nel gruppo giusto, nient'altro.
/// </summary>
public static class MenuApplicazione
{
    /// <summary>Cartelle per gli utenti delle aziende clienti.</summary>
    public static readonly IReadOnlyList<GruppoMenu> Azienda = new List<GruppoMenu>
    {
        new("Ordini e arrivi", "Ordini e arrivi", "Dall'ordine al fornitore all'accettazione", Icone.Ordini, new List<VoceMenu>
        {
            new("Ordini ai fornitori", "/Ordini", "Quadro delle consegne, nuovo ordine, invio per e-mail e stampa."),
            new("Accettazioni", "/Accettazioni", "Controlli alla consegna, etichetta con QR e carico in magazzino; correzione di un'accettazione già registrata."),
        }),
        new("Magazzino", "Magazzino e lotti", "Cosa c'è, cosa si usa, cosa è arrivato", Icone.Magazzino, new List<VoceMenu>
        {
            new("Magazzino", "/Magazzino", "Lotti da impegnare, in uso, rimanenze per articolo."),
            new("Materie prime", "/Lotti?tipo=mp", "Elenco dei lotti di materie prime, scheda del lotto, sblocco."),
            new("Prodotti finiti", "/Lotti?tipo=pf", "Elenco dei lotti di prodotto finito e dove sono finiti."),
        }),
        new("Produzione", "Produzione", "Ricette e produzioni del giorno", Icone.Produzione, new List<VoceMenu>
        {
            new("Ricette e «Oggi ho prodotto»", "/Ricette", "Ricette, lotti proposti per giorno e registrazione della produzione."),
            new("Produzioni", "/Produzioni", "Elenco delle produzioni fatte, con i lotti usati e i kg."),
        }),
        new("Spedizioni", "Spedizioni e clienti", "Cosa è partito e per chi", Icone.Spedizioni, new List<VoceMenu>
        {
            new("Spedizioni", "/Spedizioni", "Nuova spedizione ai clienti, elenco e annullamento."),
            new("Clienti", "/Clienti", "Agenda dei clienti, con l'e-mail per gli avvisi di richiamo."),
        }),
        new("Richiamo", "Rintracciabilità e richiamo", "Dove è finito un lotto, simulazione e richiamo", Icone.Richiamo, new List<VoceMenu>
        {
            new("Richiamo e simulazione", "/Richiamo", "Dal lotto sospetto ai lotti e ai clienti coinvolti; simulazione o richiamo reale con rapporto."),
            new("Cerca un lotto", "/Lotti", "Trova un lotto e apri la sua rintracciabilità a monte e a valle."),
        }),
        new("Anagrafiche", "Anagrafiche", "Fornitori e prodotti", Icone.Anagrafiche, new List<VoceMenu>
        {
            new("Fornitori", "/Anagrafiche/Fornitori", "Agenda dei fornitori, con l'e-mail per gli ordini."),
            new("Prodotti e materie prime", "/Anagrafiche/Articoli", "Articoli, matrice abituale, unità, bio e fornitori abbinati."),
            new("Categorie e controlli", "/Anagrafiche/Categorie", "Le matrici delle materie prime e i profili dei controlli da fare all'arrivo (Qualità e Amministratore)."),
        }),
        new("Impostazioni", "Impostazioni", "Come l'azienda lavora", Icone.Impostazioni, new List<VoceMenu>
        {
            new("Kg consumati per ingrediente", "/Impostazioni", "Scegli se e come registrare i kg usati in ogni produzione."),
        }),
    };

    /// <summary>Cartelle per il personale MARE.A (gestore del servizio).</summary>
    public static readonly IReadOnlyList<GruppoMenu> Gestore = new List<GruppoMenu>
    {
        new("Gestione", "Gestione del servizio", "Aziende, utenti e registro attività", Icone.Gestione, new List<VoceMenu>
        {
            new("Aziende e utenti", "/Gestione", "Aziende clienti, sedi, utenti con ruolo, inviti e stato del servizio."),
            new("Registro attività", "/Gestione/Registro", "Chi ha fatto cosa e quando, su tutte le aziende."),
        }),
    };

    public static IReadOnlyList<GruppoMenu> Per(bool gestore) => gestore ? Gestore : Azienda;
}
