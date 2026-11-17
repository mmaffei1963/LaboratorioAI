namespace LaboratorioAI.Core.Offerte;

/// <summary>
/// Testi fissi dell'offerta di laboratorio sul portale (17/11/2026, dettati da Mirko Maffei), uguali a video e nel PDF.
/// </summary>
public static class TestiOffertaCliente
{
    /// <summary>Pagina del sito con le Condizioni generali di fornitura (link cliccabile a video e nel PDF).</summary>
    public const string IndirizzoCondizioni = "https://www.mare-a.com/laboratorio-analisi/";

    public const string Oggetto = "OGGETTO: Analisi di laboratorio su Vs. prodotti";

    public static readonly string[] Presentazione =
    [
        "A seguito della Vs. gentile richiesta Vi sottoponiamo la nostra migliore offerta.",
        "Per ogni contatto relativo alle offerte il rif. è l'Amministratore Unico: Dott. Daniele Vaccarini, email: dvaccarini@mare-a.com",
        "Per ogni contatto relativo ad aspetti tecnici il rif. è il Direttore del Laboratorio: Dott. Mirko Maffei, email: mmaffei@mare-a.com"
    ];

    public const string TitoloAllegato = "ALL.1 OFFERTA ANALISI DI LABORATORIO";
    public const string TitoloDettaglio = "DETTAGLIO INCARICO";

    /// <summary>Sezioni finali: titolo e paragrafi.</summary>
    public static readonly (string Titolo, string[] Paragrafi)[] Condizioni =
    [
        ("Modalità e termini di accettazione offerta:", ["Si fa riferimento alle condizioni generali di fornitura vigenti."]),
        ("Condizioni economiche:",
        [
            "I prezzi si intendono al netto di IVA.",
            "L'offerta è valida fino alla scadenza dell'anno solare. All'inizio di ogni anno solare, se non interviene una nuova offerta, " +
            "questa si intende tacitamente rinnovata e i prezzi vengono adeguati secondo l'indice nazionale ISTAT dei prezzi al consumo.",
            "I campioni si intendono franco ns. laboratorio."
        ]),
        ("Modalità e termini di pagamento:", ["Rimessa diretta o RiBA."]),
        ("Condizioni generali di fornitura:", ["Vedi «Condizioni generali di fornitura» all'indirizzo " + IndirizzoCondizioni])
    ];
}
