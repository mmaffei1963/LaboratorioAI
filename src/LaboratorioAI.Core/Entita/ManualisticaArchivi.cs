namespace LaboratorioAI.Core.Entita;

/// <summary>Carta o computer: i primi quattordici campi sono gli stessi.</summary>
public enum TipoArchivio
{
    Cartaceo = 1,
    Informatico = 2
}

/// <summary>
/// Un archivio del cliente, cartaceo o informatico (10/11/2026).
///
/// Una tabella sola per i due tipi: le domande che ci si fa sopra —
/// dove tiene i dati questo cliente, quali archivi sono accessibili al
/// Front Office — non distinguono fra carta e computer, e con due
/// tabelle ogni ricerca andrebbe scritta due volte. I campi del solo
/// informatico restano vuoti sui cartacei.
///
/// Chi vi accede, le categorie di interessati, le categorie di dati e i
/// gestionali installati stanno in tabelle figlie, con una riga per
/// elemento. Nel modello Word le caselle sono tre, tre, sei e due, ma
/// quello e' lo spazio nella tabella Word, non una regola: un albergo
/// con quattro gestionali li ha davvero. Il generatore avvisa quando il
/// documento ne stampa meno di quanti ce ne sono — senza quell'avviso
/// questa scelta perderebbe dati in silenzio, che e' peggio del limite.
/// </summary>
public class ArchivioCompilazione
{
    public int IdArchivioCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public TipoArchivio Tipo { get; set; }

    /// <summary>Dove si trova l'archivio. Voce del dizionario, o scritta a mano.</summary>
    public string? Luogo { get; set; }

    // ── Solo per gli archivi informatici ───────────────────────────

    public string? SistemaOperativo { get; set; }
    public string? Antivirus { get; set; }

    /// <summary>Il backup dei dati che stanno sul PC, non quello dei gestionali.</summary>
    public string? Backup { get; set; }
    public string? FrequenzaBackup { get; set; }
    public string? LuogoConservazione { get; set; }

    // ───────────────────────────────────────────────────────────────

    /// <summary>Numero dell'archivio nel documento: il motore lo ricava da qui.</summary>
    public int Ordine { get; set; }

    /// <summary>Niente si cancella: un archivio tolto si spegne.</summary>
    public bool Attivo { get; set; } = true;

    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }

    public List<ArchivioAccesso> Accessi { get; set; } = new();
    public List<ArchivioInteressati> Interessati { get; set; } = new();
    public List<ArchivioCategoriaDati> CategorieDati { get; set; } = new();
    public List<ArchivioGestionale> Gestionali { get; set; } = new();
}

/// <summary>Chi accede a un archivio. Nel Word sono tre caselle; qui quante servono.</summary>
public class ArchivioAccesso
{
    public int IdArchivioAccesso { get; set; }
    public int IdArchivioCompilazione { get; set; }
    public ArchivioCompilazione? Archivio { get; set; }

    public string Valore { get; set; } = null!;
    public int Ordine { get; set; }
}

/// <summary>Una categoria di interessati i cui dati stanno nell'archivio.</summary>
public class ArchivioInteressati
{
    public int IdArchivioInteressati { get; set; }
    public int IdArchivioCompilazione { get; set; }
    public ArchivioCompilazione? Archivio { get; set; }

    public string Valore { get; set; } = null!;
    public int Ordine { get; set; }
}

/// <summary>Dati comuni, particolari ex art. 9, particolari ex art. 10.</summary>
public class ArchivioCategoriaDati
{
    public int IdArchivioCategoriaDati { get; set; }
    public int IdArchivioCompilazione { get; set; }
    public ArchivioCompilazione? Archivio { get; set; }

    public string Valore { get; set; } = null!;
    public int Ordine { get; set; }
}

/// <summary>
/// Un gestionale del cliente (10/11/2026).
///
/// Sta qui e non dentro il PC perche' lo stesso gestionale e' spesso
/// installato su piu' postazioni, e nessuno dei suoi dati dipende dal
/// PC: prodotto, misure di sicurezza, chi fa la manutenzione, come e
/// quando si fa il backup, dove stanno i dati sono proprieta' del
/// software. Il PC aggiunge una sola informazione, che e' installato li'.
///
/// Cosi' il consulente scrive PASSEPARTOUT una volta e lo spunta sulle
/// tre postazioni.
/// </summary>
public class GestionaleCompilazione
{
    public int IdGestionaleCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    /// <summary>PASSEPARTOUT, BEDDY, WUBOOK… Voce del dizionario, o scritta a mano.</summary>
    public string Prodotto { get; set; } = null!;

    public string? MisureSicurezza { get; set; }

    /// <summary>Chi fa la manutenzione: e' il fornitore, con ragione sociale e indirizzo.</summary>
    public string? Manutenzione { get; set; }

    public string? Backup { get; set; }
    public string? Frequenza { get; set; }
    public string? LuogoConservazione { get; set; }

    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;

    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }

    public List<ArchivioGestionale> Installazioni { get; set; } = new();
}

/// <summary>Un gestionale installato su un archivio informatico.</summary>
public class ArchivioGestionale
{
    public int IdArchivioGestionale { get; set; }

    public int IdArchivioCompilazione { get; set; }
    public ArchivioCompilazione? Archivio { get; set; }

    public int IdGestionaleCompilazione { get; set; }
    public GestionaleCompilazione? Gestionale { get; set; }

    public int Ordine { get; set; }
}

/// <summary>
/// Una casella di posta del cliente. Nel Registro l'elenco compare in
/// ogni sezione di funzione, filtrato sulle funzioni che vi accedono.
/// </summary>
public class CasellaEmailCompilazione
{
    public int IdCasellaEmailCompilazione { get; set; }
    public int IdCompilazione { get; set; }
    public Compilazione? Compilazione { get; set; }

    public string Indirizzo { get; set; } = null!;
    public int Ordine { get; set; }
    public bool Attivo { get; set; } = true;

    public DateTime CreatoIlUtc { get; set; }
    public string? CreatoDa { get; set; }

    public List<CasellaEmailFunzione> Funzioni { get; set; } = new();
}

/// <summary>Una funzione aziendale che accede a una casella.</summary>
public class CasellaEmailFunzione
{
    public int IdCasellaEmailFunzione { get; set; }
    public int IdCasellaEmailCompilazione { get; set; }
    public CasellaEmailCompilazione? Casella { get; set; }

    public int IdFunzioneAziendale { get; set; }
    public FunzioneAziendale? Funzione { get; set; }
}
