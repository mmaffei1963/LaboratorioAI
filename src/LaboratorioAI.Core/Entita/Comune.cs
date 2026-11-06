namespace LaboratorioAI.Core.Entita;

/// <summary>
/// Un comune italiano, dall'elenco ISTAT (04/11/2026).
///
/// Serve a scrivere sempre allo stesso modo i luoghi nei documenti:
/// senza questa tabella nello stesso pacchetto compaiono «Cattolica (RN)»,
/// «CATTOLICA» e «Cattolica (Rimini)» a seconda di chi ha compilato.
///
/// Il nome non basta a identificare un comune: ce ne sono sei che si
/// ripetono in province diverse (Calliano, Castro, Peglio, Samone,
/// San Teodoro). La chiave e' il codice ISTAT, e a schermo il comune si
/// mostra sempre con la sigla della provincia.
///
/// Sta nello schema <c>app</c> e non in <c>man</c> perche' gli indirizzi
/// servono a tutto l'applicativo — preventivi, ordini, rapportini — non
/// alla sola manualistica.
/// </summary>
public class Comune
{
    public int IdComune { get; set; }

    /// <summary>Codice ISTAT di sei cifre: e' l'identita' vera del comune.</summary>
    public string CodiceIstat { get; set; } = null!;

    public string Nome { get; set; } = null!;

    /// <summary>Sigla della provincia: RN, MI, BZ…</summary>
    public string SiglaProvincia { get; set; } = null!;

    public string Provincia { get; set; } = null!;

    /// <summary>Codice ISTAT della regione, due cifre.</summary>
    public string CodiceRegione { get; set; } = null!;

    /// <summary>
    /// La regione. Serve anche fuori dagli indirizzi: le varianti
    /// regionali dei modelli si sceglieranno dal comune del cliente,
    /// senza chiedere la regione a parte.
    /// </summary>
    public string Regione { get; set; } = null!;

    /// <summary>Codice catastale, quattro caratteri: serve ai codici fiscali.</summary>
    public string? CodiceCatastale { get; set; }

    /// <summary>I comuni soppressi restano, spenti: i documenti vecchi li citano ancora.</summary>
    public bool Attivo { get; set; } = true;

    /// <summary>
    /// I CAP del comune. Quasi sempre uno (7.860 comuni su 7.904), ma
    /// Rimini ne ha 4, Milano 42, Roma 82. Con uno solo si compila da
    /// se'; con piu' di uno si sceglie, invece di scriverlo a mano.
    /// </summary>
    public List<ComuneCap> Cap { get; set; } = new();

    /// <summary>Nome come va scritto nei documenti e mostrato negli elenchi.</summary>
    public string NomeConProvincia => $"{Nome} ({SiglaProvincia})";
}

/// <summary>
/// Un CAP di un comune. Tabella a parte e non una colonna, perche' il
/// rapporto e' uno-a-molti: tenere una riga per CAP nella tabella dei
/// comuni farebbe comparire Roma 82 volte nell'elenco dei comuni.
/// </summary>
public class ComuneCap
{
    public int IdComuneCap { get; set; }
    public int IdComune { get; set; }
    public Comune? Comune { get; set; }

    /// <summary>Cinque cifre.</summary>
    public string Cap { get; set; } = null!;

    /// <summary>Ordine di presentazione: il primo e' quello proposto.</summary>
    public int Ordine { get; set; }
}
