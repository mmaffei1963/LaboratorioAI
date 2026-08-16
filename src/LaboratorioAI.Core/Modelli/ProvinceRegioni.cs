// LaboratorioAI.Core/Modelli/ProvinceRegioni.cs
//
// Resolver provincia -> regione, usato per valorizzare il tag `regione` del
// modello a partire dal campo ProvinciaInsed dell'insediamento (ArcLab).
//
// Perché qui e non su ArcLab: la regione non è un dato del gestionale ma una
// derivazione stabile dalla provincia; tenerla come funzione pura evita di
// dover leggere/mantenere un campo aggiuntivo e rispetta la regola di sola
// lettura su ArcLab.
//
// I nomi di regione coincidono esattamente con le voci dell'elenco a discesa
// `regione` del modello Word, così il valore risolto è già un'opzione valida.

namespace LaboratorioAI.Core.Modelli;

/// <summary>Corrispondenza fra provincia italiana (sigla o nome) e regione.</summary>
public static class ProvinceRegioni
{
    // Fonte unica: sigla, nome esteso, regione. Da qui si costruiscono i due
    // dizionari di lookup, evitando disallineamenti.
    private static readonly (string Sigla, string Nome, string Regione)[] Elenco =
    [
        ("AQ", "L'Aquila", "Abruzzo"), ("CH", "Chieti", "Abruzzo"),
        ("PE", "Pescara", "Abruzzo"), ("TE", "Teramo", "Abruzzo"),

        ("MT", "Matera", "Basilicata"), ("PZ", "Potenza", "Basilicata"),

        ("CZ", "Catanzaro", "Calabria"), ("CS", "Cosenza", "Calabria"),
        ("KR", "Crotone", "Calabria"), ("RC", "Reggio Calabria", "Calabria"),
        ("VV", "Vibo Valentia", "Calabria"),

        ("AV", "Avellino", "Campania"), ("BN", "Benevento", "Campania"),
        ("CE", "Caserta", "Campania"), ("NA", "Napoli", "Campania"),
        ("SA", "Salerno", "Campania"),

        ("BO", "Bologna", "Emilia-Romagna"), ("FC", "Forlì-Cesena", "Emilia-Romagna"),
        ("FE", "Ferrara", "Emilia-Romagna"), ("MO", "Modena", "Emilia-Romagna"),
        ("PR", "Parma", "Emilia-Romagna"), ("PC", "Piacenza", "Emilia-Romagna"),
        ("RA", "Ravenna", "Emilia-Romagna"), ("RE", "Reggio Emilia", "Emilia-Romagna"),
        ("RN", "Rimini", "Emilia-Romagna"),

        ("GO", "Gorizia", "Friuli-Venezia Giulia"), ("PN", "Pordenone", "Friuli-Venezia Giulia"),
        ("TS", "Trieste", "Friuli-Venezia Giulia"), ("UD", "Udine", "Friuli-Venezia Giulia"),

        ("FR", "Frosinone", "Lazio"), ("LT", "Latina", "Lazio"),
        ("RI", "Rieti", "Lazio"), ("RM", "Roma", "Lazio"), ("VT", "Viterbo", "Lazio"),

        ("GE", "Genova", "Liguria"), ("IM", "Imperia", "Liguria"),
        ("SP", "La Spezia", "Liguria"), ("SV", "Savona", "Liguria"),

        ("BG", "Bergamo", "Lombardia"), ("BS", "Brescia", "Lombardia"),
        ("CO", "Como", "Lombardia"), ("CR", "Cremona", "Lombardia"),
        ("LC", "Lecco", "Lombardia"), ("LO", "Lodi", "Lombardia"),
        ("MN", "Mantova", "Lombardia"), ("MI", "Milano", "Lombardia"),
        ("MB", "Monza e della Brianza", "Lombardia"), ("PV", "Pavia", "Lombardia"),
        ("SO", "Sondrio", "Lombardia"), ("VA", "Varese", "Lombardia"),

        ("AN", "Ancona", "Marche"), ("AP", "Ascoli Piceno", "Marche"),
        ("FM", "Fermo", "Marche"), ("MC", "Macerata", "Marche"),
        ("PU", "Pesaro e Urbino", "Marche"),

        ("CB", "Campobasso", "Molise"), ("IS", "Isernia", "Molise"),

        ("AL", "Alessandria", "Piemonte"), ("AT", "Asti", "Piemonte"),
        ("BI", "Biella", "Piemonte"), ("CN", "Cuneo", "Piemonte"),
        ("NO", "Novara", "Piemonte"), ("TO", "Torino", "Piemonte"),
        ("VB", "Verbano-Cusio-Ossola", "Piemonte"), ("VC", "Vercelli", "Piemonte"),

        ("BA", "Bari", "Puglia"), ("BT", "Barletta-Andria-Trani", "Puglia"),
        ("BR", "Brindisi", "Puglia"), ("FG", "Foggia", "Puglia"),
        ("LE", "Lecce", "Puglia"), ("TA", "Taranto", "Puglia"),

        ("CA", "Cagliari", "Sardegna"), ("NU", "Nuoro", "Sardegna"),
        ("OR", "Oristano", "Sardegna"), ("SS", "Sassari", "Sardegna"),
        ("SU", "Sud Sardegna", "Sardegna"),

        ("AG", "Agrigento", "Sicilia"), ("CL", "Caltanissetta", "Sicilia"),
        ("CT", "Catania", "Sicilia"), ("EN", "Enna", "Sicilia"),
        ("ME", "Messina", "Sicilia"), ("PA", "Palermo", "Sicilia"),
        ("RG", "Ragusa", "Sicilia"), ("SR", "Siracusa", "Sicilia"),
        ("TP", "Trapani", "Sicilia"),

        ("AR", "Arezzo", "Toscana"), ("FI", "Firenze", "Toscana"),
        ("GR", "Grosseto", "Toscana"), ("LI", "Livorno", "Toscana"),
        ("LU", "Lucca", "Toscana"), ("MS", "Massa-Carrara", "Toscana"),
        ("PI", "Pisa", "Toscana"), ("PT", "Pistoia", "Toscana"),
        ("PO", "Prato", "Toscana"), ("SI", "Siena", "Toscana"),

        ("BZ", "Bolzano", "Trentino-Alto Adige"), ("TN", "Trento", "Trentino-Alto Adige"),

        ("PG", "Perugia", "Umbria"), ("TR", "Terni", "Umbria"),

        ("AO", "Aosta", "Valle d'Aosta"),

        ("BL", "Belluno", "Veneto"), ("PD", "Padova", "Veneto"),
        ("RO", "Rovigo", "Veneto"), ("TV", "Treviso", "Veneto"),
        ("VE", "Venezia", "Veneto"), ("VR", "Verona", "Veneto"),
        ("VI", "Vicenza", "Veneto"),
    ];

    private static readonly Dictionary<string, string> PerSigla =
        Elenco.ToDictionary(p => p.Sigla, p => p.Regione, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> PerNome =
        Elenco.ToDictionary(p => p.Nome, p => p.Regione, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Restituisce la regione a partire dalla provincia (sigla, es. "RN", o nome
    /// esteso, es. "Rimini"). Restituisce <c>null</c> se la provincia è vuota o
    /// non riconosciuta.
    /// </summary>
    public static string? RegioneDa(string? provincia)
    {
        if (string.IsNullOrWhiteSpace(provincia))
            return null;

        var chiave = provincia.Trim();
        if (PerSigla.TryGetValue(chiave, out var regione))
            return regione;
        if (PerNome.TryGetValue(chiave, out regione))
            return regione;

        return null;
    }
}
