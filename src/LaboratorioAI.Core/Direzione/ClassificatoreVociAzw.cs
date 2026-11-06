namespace LaboratorioAI.Core.Direzione;

/// <summary>
/// Proposta di classificazione di una voce AZW: le stesse regole della query «Mapping_Costi» del report Power BI
/// AZW_TS_VERSION (letto il 13/10/2026). Serve per popolare la tabella delle classificazioni e per proporre la
/// classificazione delle voci nuove; da lì in poi decide la tabella.
/// </summary>
public static class ClassificatoreVociAzw
{
    public const string Fisso = "Fisso";
    public const string Variabile = "Variabile";

    public const string MacroRicavo = "Ricavo";
    public const string MacroPersonale = "Personale";
    public const string MacroAltri = "Altri costi";

    public static readonly string[] Macrocategorie =
        [MacroRicavo, MacroPersonale, "Formazione", "Produzione", "Commerciali", "Amministrative", "Generali", MacroAltri];

    private static readonly string[] VariabiliEsatti =
    [
        "Affitti uffici corso base", "Cancelleria corso base", "Energia ill. corso base", "Compensi per docenti c. base",
        "Energia ill. corso avanzato", "Affitti uffici corso avanzato", "cancelleria corso avanzato",
        "Compensi per docenti c. avanza", "Scherpiani Lorenzo", "Remedia Gianluca", "Spese rifiuti"
    ];

    private static readonly string[] FissiEsatti =
    [
        "Carloni Andrea", "Spese sede Pesaro", "Pivetta Elena", "Bonci Laura", "Tamburini Selene", "Gelli Maria Camilla",
        "Simoncelli Giulia 1995", "Simoncelli Giulia 2001", "Maffei Enea", "Teodorani Angelica", "Zatterin Ludovico"
    ];

    private static readonly string[] FissiContiene =
    [
        "Personale", "Aggiornamento", "Consulenze tecniche", "costi di produzione", "Manutenzioni", "Spese generali",
        "Affitti", "Ammortamenti", "Perdite su crediti", "Accreditamento", "Paolo Fagioli", "Stefano Scaramucci",
        "Giuliana Giulini", "Daniele Vaccarini", "Mirko Maffei", "Claudio Micci", "Silvia Migani", "Federica Volponi",
        "Nicoletta Magi", "Marco Sertori", "Chiara Prioli", "Bertozzi Davide", "Pesaresi Martina", "Mussoni Sabrina",
        "Buscaglia Matteo", "automezzi", "Ergascov Lara"
    ];

    private static readonly string[] VariabiliContiene =
    [
        "Esterne", "Rimborsi", "Attrezzature", "Consulenze tecniche", "automezzi", "compensi a terzi",
        "Materiali consumo laboratorio", "Acquisto termometri", "Costi commerciali", "Acquisto filtri", "De Angelis Valerio",
        "Acquisto data logger", "Bacchiani Matteo", "Rimborsi trasferte CIA", "Guidi Gioele", "Costo consulenti CIA"
    ];

    private static readonly Dictionary<string, string> MacroPerCodice = Costruisci(
        (MacroPersonale, "DIP COM PAO STE GIU DAN MIR CLA SIL FED NIC MAR CHI ALE BER PES SAB GUI BUS LAR CAR PIV BLA TAM CAM SIM SI2 TEO MEN VAL BAC ZAT REM SCH"),
        ("Formazione", "AGG DOC CDA SSP SAU SPE SPA ENE ECA"),
        ("Produzione", "LAB EST CON MAN SPO ATT TER FIL ADL ACC RV"),
        ("Commerciali", "COC RTC CCC"),
        ("Amministrative", "INS"),
        ("Generali", "SPG AFF AMM AUT SGP SRI"));

    private static readonly HashSet<string> MateriePrime = new(StringComparer.OrdinalIgnoreCase) { "EST", "LAB", "CON", "MAN", "SPO", "COM" };

    private static Dictionary<string, string> Costruisci(params (string Macro, string Codici)[] gruppi)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (macro, codici) in gruppi)
            foreach (var c in codici.Split(' ', StringSplitOptions.RemoveEmptyEntries)) d[c] = macro;
        return d;
    }

    /// <summary>Classificazione proposta per una voce (codice, descrizione AZW, ricavo sì/no).</summary>
    public static (string? TipoCosto, string Macrocategoria, bool MateriaPrima) Proponi(string codice, string? descrizione, bool ricavo)
    {
        if (ricavo) return (null, MacroRicavo, false);
        var d = (descrizione ?? "").Trim();
        string? tipo =
            FissiEsatti.Any(x => string.Equals(x, d, StringComparison.OrdinalIgnoreCase)) ? Fisso
            : VariabiliEsatti.Any(x => string.Equals(x, d, StringComparison.OrdinalIgnoreCase)) ? Variabile
            : FissiContiene.Any(x => d.Contains(x, StringComparison.OrdinalIgnoreCase)) ? Fisso
            : VariabiliContiene.Any(x => d.Contains(x, StringComparison.OrdinalIgnoreCase)) ? Variabile
            : null;
        var macro = MacroPerCodice.TryGetValue(codice.Trim(), out var m) ? m : MacroAltri;
        return (tipo, macro, MateriePrime.Contains(codice.Trim()));
    }
}
