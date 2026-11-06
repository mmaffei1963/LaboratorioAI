namespace LaboratorioAI.Core.Direzione;

/// <summary>Sigle delle province italiane con nome e regione (cruscotto Direzione → Andamento territorio, 13/10/2026).</summary>
public static class ProvinceItaliane
{
    public const string Estero = "Estero o non indicata";

    private static readonly Dictionary<string, (string Nome, string Regione)> Elenco = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AQ"] = ("L'Aquila", "Abruzzo"),
        ["CH"] = ("Chieti", "Abruzzo"),
        ["PE"] = ("Pescara", "Abruzzo"),
        ["TE"] = ("Teramo", "Abruzzo"),
        ["MT"] = ("Matera", "Basilicata"),
        ["PZ"] = ("Potenza", "Basilicata"),
        ["CZ"] = ("Catanzaro", "Calabria"),
        ["CS"] = ("Cosenza", "Calabria"),
        ["KR"] = ("Crotone", "Calabria"),
        ["RC"] = ("Reggio Calabria", "Calabria"),
        ["VV"] = ("Vibo Valentia", "Calabria"),
        ["AV"] = ("Avellino", "Campania"),
        ["BN"] = ("Benevento", "Campania"),
        ["CE"] = ("Caserta", "Campania"),
        ["NA"] = ("Napoli", "Campania"),
        ["SA"] = ("Salerno", "Campania"),
        ["BO"] = ("Bologna", "Emilia-Romagna"),
        ["FE"] = ("Ferrara", "Emilia-Romagna"),
        ["FC"] = ("Forlì-Cesena", "Emilia-Romagna"),
        ["MO"] = ("Modena", "Emilia-Romagna"),
        ["PR"] = ("Parma", "Emilia-Romagna"),
        ["PC"] = ("Piacenza", "Emilia-Romagna"),
        ["RA"] = ("Ravenna", "Emilia-Romagna"),
        ["RE"] = ("Reggio Emilia", "Emilia-Romagna"),
        ["RN"] = ("Rimini", "Emilia-Romagna"),
        ["GO"] = ("Gorizia", "Friuli-Venezia Giulia"),
        ["PN"] = ("Pordenone", "Friuli-Venezia Giulia"),
        ["TS"] = ("Trieste", "Friuli-Venezia Giulia"),
        ["UD"] = ("Udine", "Friuli-Venezia Giulia"),
        ["FR"] = ("Frosinone", "Lazio"),
        ["LT"] = ("Latina", "Lazio"),
        ["RI"] = ("Rieti", "Lazio"),
        ["RM"] = ("Roma", "Lazio"),
        ["VT"] = ("Viterbo", "Lazio"),
        ["GE"] = ("Genova", "Liguria"),
        ["IM"] = ("Imperia", "Liguria"),
        ["SP"] = ("La Spezia", "Liguria"),
        ["SV"] = ("Savona", "Liguria"),
        ["BG"] = ("Bergamo", "Lombardia"),
        ["BS"] = ("Brescia", "Lombardia"),
        ["CO"] = ("Como", "Lombardia"),
        ["CR"] = ("Cremona", "Lombardia"),
        ["LC"] = ("Lecco", "Lombardia"),
        ["LO"] = ("Lodi", "Lombardia"),
        ["MN"] = ("Mantova", "Lombardia"),
        ["MI"] = ("Milano", "Lombardia"),
        ["MB"] = ("Monza e Brianza", "Lombardia"),
        ["PV"] = ("Pavia", "Lombardia"),
        ["SO"] = ("Sondrio", "Lombardia"),
        ["VA"] = ("Varese", "Lombardia"),
        ["AN"] = ("Ancona", "Marche"),
        ["AP"] = ("Ascoli Piceno", "Marche"),
        ["FM"] = ("Fermo", "Marche"),
        ["MC"] = ("Macerata", "Marche"),
        ["PU"] = ("Pesaro e Urbino", "Marche"),
        ["CB"] = ("Campobasso", "Molise"),
        ["IS"] = ("Isernia", "Molise"),
        ["AL"] = ("Alessandria", "Piemonte"),
        ["AT"] = ("Asti", "Piemonte"),
        ["BI"] = ("Biella", "Piemonte"),
        ["CN"] = ("Cuneo", "Piemonte"),
        ["NO"] = ("Novara", "Piemonte"),
        ["TO"] = ("Torino", "Piemonte"),
        ["VB"] = ("Verbano-Cusio-Ossola", "Piemonte"),
        ["VC"] = ("Vercelli", "Piemonte"),
        ["BA"] = ("Bari", "Puglia"),
        ["BT"] = ("Barletta-Andria-Trani", "Puglia"),
        ["BR"] = ("Brindisi", "Puglia"),
        ["FG"] = ("Foggia", "Puglia"),
        ["LE"] = ("Lecce", "Puglia"),
        ["TA"] = ("Taranto", "Puglia"),
        ["CA"] = ("Cagliari", "Sardegna"),
        ["NU"] = ("Nuoro", "Sardegna"),
        ["OR"] = ("Oristano", "Sardegna"),
        ["SS"] = ("Sassari", "Sardegna"),
        ["SU"] = ("Sud Sardegna", "Sardegna"),
        ["CI"] = ("Carbonia-Iglesias", "Sardegna"),
        ["VS"] = ("Medio Campidano", "Sardegna"),
        ["OG"] = ("Ogliastra", "Sardegna"),
        ["OT"] = ("Olbia-Tempio", "Sardegna"),
        ["AG"] = ("Agrigento", "Sicilia"),
        ["CL"] = ("Caltanissetta", "Sicilia"),
        ["CT"] = ("Catania", "Sicilia"),
        ["EN"] = ("Enna", "Sicilia"),
        ["ME"] = ("Messina", "Sicilia"),
        ["PA"] = ("Palermo", "Sicilia"),
        ["RG"] = ("Ragusa", "Sicilia"),
        ["SR"] = ("Siracusa", "Sicilia"),
        ["TP"] = ("Trapani", "Sicilia"),
        ["AR"] = ("Arezzo", "Toscana"),
        ["FI"] = ("Firenze", "Toscana"),
        ["GR"] = ("Grosseto", "Toscana"),
        ["LI"] = ("Livorno", "Toscana"),
        ["LU"] = ("Lucca", "Toscana"),
        ["MS"] = ("Massa-Carrara", "Toscana"),
        ["PI"] = ("Pisa", "Toscana"),
        ["PT"] = ("Pistoia", "Toscana"),
        ["PO"] = ("Prato", "Toscana"),
        ["SI"] = ("Siena", "Toscana"),
        ["BZ"] = ("Bolzano", "Trentino-Alto Adige"),
        ["TN"] = ("Trento", "Trentino-Alto Adige"),
        ["PG"] = ("Perugia", "Umbria"),
        ["TR"] = ("Terni", "Umbria"),
        ["AO"] = ("Aosta", "Valle d'Aosta"),
        ["BL"] = ("Belluno", "Veneto"),
        ["PD"] = ("Padova", "Veneto"),
        ["RO"] = ("Rovigo", "Veneto"),
        ["TV"] = ("Treviso", "Veneto"),
        ["VE"] = ("Venezia", "Veneto"),
        ["VR"] = ("Verona", "Veneto"),
        ["VI"] = ("Vicenza", "Veneto"),
    };

    /// <summary>Sigla normalizzata (maiuscola, senza spazi); vuota se non indicata.</summary>
    public static string Sigla(string? provincia) => (provincia ?? "").Trim().ToUpperInvariant();

    public static string Regione(string? provincia) =>
        Elenco.TryGetValue(Sigla(provincia), out var p) ? p.Regione : Estero;

    public static string NomeProvincia(string? provincia)
    {
        var s = Sigla(provincia);
        return Elenco.TryGetValue(s, out var p) ? $"{p.Nome} ({s})" : s.Length == 0 ? Estero : $"{s} (non riconosciuta)";
    }

    public static bool Esiste(string? provincia) => Elenco.ContainsKey(Sigla(provincia));

    /// <summary>Regioni con le sigle delle loro province, in ordine alfabetico.</summary>
    public static IReadOnlyList<(string Regione, IReadOnlyList<string> Province)> Regioni() =>
        Elenco.GroupBy(p => p.Value.Regione).OrderBy(g => g.Key)
            .Select(g => (g.Key, (IReadOnlyList<string>)g.Select(x => x.Key).OrderBy(x => x).ToList())).ToList();
}
