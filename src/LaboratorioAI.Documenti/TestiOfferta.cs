using System.Text.RegularExpressions;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Testi fissi dell'Offerta, condivisi tra Word (OffertaDocx) e PDF (GeneratoreOffertaPreventivo)
/// perché siano identici. Riscritti il 30/09/2026 per allinearli alle Condizioni Generali Rev. 19:
/// l'Offerta prevale sulle Condizioni (Art. 2.4), quindi qui non va scritto nulla che prometta
/// tempi, reperibilità, validità legale dei risultati o accreditamento dei laboratori terzi.
/// </summary>
internal static class TestiOfferta
{
    /// <summary>
    /// Più modalità di pagamento nella stessa offerta (12/10/2026): nel campo «Condizioni di pagamento» le modalità sono
    /// separate da una riga vuota; ogni modalità può avere più righe (es. banca e IBAN). Con una sola modalità il cliente
    /// non deve scegliere; con due o più indica quella scelta.
    /// </summary>
    public const string SceltaPagamento = "Condizioni di pagamento – indicare con una X la modalità scelta:";

    /// <summary>Modalità di pagamento dell'offerta: blocchi separati da una riga vuota, ognuno con le sue righe.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ModalitaPagamento(string? testo)
    {
        var blocchi = new List<IReadOnlyList<string>>();
        var corrente = new List<string>();
        foreach (var riga in (testo ?? "").Replace("\r", "").Split('\n'))
        {
            if (riga.Trim().Length == 0)
            {
                if (corrente.Count > 0) { blocchi.Add(corrente); corrente = new List<string>(); }
                continue;
            }
            corrente.Add(riga.Trim());
        }
        if (corrente.Count > 0) blocchi.Add(corrente);
        return blocchi.DistinctBy(b => string.Join("\n", b).ToLowerInvariant()).ToList();
    }

    private static readonly System.Globalization.CultureInfo ItPrezzi = System.Globalization.CultureInfo.GetCultureInfo("it-IT");

    /// <summary>
    /// Righe del prezzo sotto un controllo o una voce accessoria (12/10/2026): solo IVA esclusa; con lo sconto si
    /// riportano prezzo pieno, percentuale e prezzo scontato.
    /// </summary>
    public static IReadOnlyList<string> RighePrezzo(decimal prezzoBase, decimal sconto, int quantita, decimal prezzoFinale, bool accessorio)
    {
        string E(decimal v) => "€ " + v.ToString("N2", ItPrezzi);
        string Riga(string etichetta, decimal pieno, decimal scontato) => sconto > 0
            ? $"{etichetta}: {E(pieno)} – sconto {sconto.ToString("0.##", ItPrezzi)}% = {E(scontato)} (IVA escl.)"
            : $"{etichetta}: {E(scontato)} (IVA escl.)";
        if (quantita <= 1) return [Riga("Prezzo", prezzoBase, prezzoFinale)];
        var unitario = Math.Round(prezzoBase * (1 - sconto / 100m), 2, MidpointRounding.AwayFromZero);
        return
        [
            Riga(accessorio ? "Prezzo unitario" : "Prezzo per ciascun campione analizzato", prezzoBase, unitario),
            accessorio ? $"Totale per quantità {quantita}: {E(prezzoFinale)} (IVA escl.)" : $"Totale per {quantita} campioni: {E(prezzoFinale)} (IVA escl.)"
        ];
    }

    /// <summary>Paragrafi del testo descrittivo di una voce accessoria (separati da righe vuote; a capo singoli uniti).</summary>
    public static IReadOnlyList<string> Paragrafi(string? testo) =>
        System.Text.RegularExpressions.Regex.Split((testo ?? "").Replace("\r", ""), @"\n\s*\n")
            .Select(p => System.Text.RegularExpressions.Regex.Replace(p.Trim(), @"\s*\n\s*", " "))
            .Where(p => p.Length > 0).ToList();

    public const string TitoloIlNostroLavoro = "IL NOSTRO LAVORO";

    /// <summary>Presentazione descrittiva (nessun impegno di prestazione).</summary>
    public const string IlNostroLavoro =
        "Abbiamo costruito la nostra attività su un'esperienza trentennale nella microbiologia alimentare, maturata " +
        "affrontando problematiche, innovazioni tecnologiche e normative in continua evoluzione e nel confronto con gli " +
        "organi di controllo provinciali, regionali e nazionali, anche partecipando a tavoli di discussione europei sulle " +
        "problematiche igienico-sanitarie del settore alimentare. Il Laboratorio è sottoposto ogni anno alle visite di " +
        "sorveglianza di ACCREDIA per il mantenimento dell'accreditamento delle prove e partecipa a Proficiency Tests. " +
        "Il nostro personale di laboratorio lavora con noi da molti anni e conosce le aziende " +
        "dei clienti e la storia dei loro campioni: per questo definiamo il nostro lavoro «artigianale». Ogni cliente ha un " +
        "referente di laboratorio a cui rivolgersi per informazioni e assistenza. Per le prove che non eseguiamo in sede ci " +
        "avvaliamo di laboratori terzi qualificati secondo le nostre procedure.";

    public const string TaglineIlNostroLavoro = "I vostri buoni motivi, la nostra eccellenza.";

    public const string TitoloLaboratorioAnalisi = "LABORATORIO ANALISI";

    /// <summary>Dati di accreditamento e riconoscimenti (n. ACCREDIA 00729 L come da certificato).</summary>
    public const string LaboratorioAnalisi =
        "Il Laboratorio Analisi di MARE.A s.r.l. è accreditato ACCREDIA n. 00729 L in conformità alla norma UNI CEI EN " +
        "ISO/IEC 17025:2018; l'elenco delle prove accreditate è consultabile sul sito www.accredia.it. È iscritto al n. 008/RN/003 " +
        "nell'elenco della Regione Emilia-Romagna dei laboratori che effettuano analisi nell'ambito delle procedure di " +
        "autocontrollo delle imprese alimentari. Collabora con aziende della produzione e della distribuzione " +
        "alimentare, anche nell'ambito dei loro programmi di qualificazione dei fornitori.";

    /// <summary>Durata standard dell'Offerta in giorni (Art. 3.3 Rev. 19: 12 mesi con rinnovo tacito annuale).</summary>
    public const int DurataStandardGiorni = 365;

    /// <summary>
    /// Durata in testata. Decisione di Mirko (08/10/2026): ogni Offerta ha durata annuale con rinnovo tacito, quindi il
    /// numero di giorni salvato sull'Offerta (le vecchie hanno 60) non conta più.
    /// </summary>
    public static string DurataBreve(int _, bool rinnovoTacito = true) => rinnovoTacito ? "12 mesi, rinnovo tacito" : "12 mesi, senza rinnovo";

    /// <summary>21/10/2026: cliente generico («Spett.le cliente»): in prima pagina tutti i dati, anche vuoti, da compilare a mano.</summary>
    public static bool ClienteGenerico(string? ragioneSociale) =>
        string.IsNullOrWhiteSpace(ragioneSociale) || Regex.IsMatch(ragioneSociale, @"^\s*spett", RegexOptions.IgnoreCase);

    /// <summary>Spazio da compilare a mano quando il dato manca.</summary>
    public static string DaCompilare(string? valore) => string.IsNullOrWhiteSpace(valore) ? "______________________________" : valore.Trim();

    /// <summary>
    /// Durata dell'Offerta, Art. 3.3 delle Condizioni Rev. 19: 12 mesi con rinnovo tacito di anno in anno e revisione con
    /// preavviso di 30 giorni. Sempre questa, per tutte le Offerte (decisione dell'08/10/2026).
    /// </summary>
    public static string Validita(int _, bool rinnovoTacito = true) => rinnovoTacito ? ValiditaConRinnovo : ValiditaSenzaRinnovo;

    /// <summary>21/10/2026: offerta senza rinnovo tacito (scelta nel preventivo), in deroga all'Art. 3.3 delle Condizioni.</summary>
    private const string ValiditaSenzaRinnovo =
        "La presente Offerta ha durata di 12 mesi dalla data di emissione e, in deroga all'Art. 3.3 delle Condizioni Generali " +
        "di Fornitura, non si rinnova tacitamente: alla scadenza il Laboratorio invierà una nuova Offerta. Il Laboratorio " +
        "può rivederla con preavviso scritto di 30 giorni. Le prove non quotate nella presente Offerta sono eseguite a " +
        "prezzo di listino. I prezzi si intendono IVA esclusa.";

    private const string ValiditaConRinnovo =
        "La presente Offerta ha durata di 12 mesi dalla data di emissione e si rinnova tacitamente di anno in anno; " +
        "il Laboratorio può rivederla con preavviso scritto di 30 giorni (Condizioni Generali di Fornitura). Le prove non " +
        "quotate nella presente Offerta sono eseguite a prezzo di listino. I prezzi si intendono IVA esclusa.";

    public const string TitoloCondizioni = "CONDIZIONI GENERALI DI FORNITURA";

    public static string Condizioni(string revisione) =>
        $"La presente Offerta è integrata dalle Condizioni Generali di Fornitura di MARE.A s.r.l. ({revisione}), che ne " +
        "costituiscono parte integrante e sostanziale, pubblicate su www.mare-a.com/condizioni e nel Portale clienti e " +
        "disponibili su richiesta. Il Cliente dichiara di conoscerle e di accettarle integralmente. In caso di contrasto, " +
        "per le sole condizioni economiche e tecniche specifiche prevale quanto indicato nella presente Offerta.";

    /// <summary>Perfezionamento (Art. 3.1) e conoscibilità tramite Portale, con avvisi e-mail di cortesia (Art. 2.2).</summary>
    public const string Accettazione =
        "La presente Offerta è pubblicata nel Portale clienti insieme alle Condizioni Generali di Fornitura; gli avvisi " +
        "via e-mail che il Laboratorio può inviare hanno funzione di cortesia. L'Offerta e le Condizioni si " +
        "intendono integralmente accettate dal Cliente con l'accettazione dal Portale clienti, con la sottoscrizione o " +
        "con la prima consegna di Campioni successiva alla pubblicazione dell'Offerta nel Portale. " +
        "L'accettazione dal Portale è richiesta per ogni nuova Offerta e per le revisioni delle Condizioni che modificano " +
        "le clausole approvate specificamente; il rinnovo tacito non richiede una nuova accettazione.";

    /// <summary>
    /// Firme sull'Offerta (09/10/2026): con l'accettazione dal Portale clienti la firma non serve; gli spazi restano per
    /// chi preferisce firmare, a mano con timbro (c'è ancora chi lo fa, decisione di Mirko) o con firma digitale.
    /// </summary>
    public const string NotaFirme =
        "L'accettazione dal Portale clienti non richiede alcuna firma. Chi preferisce può firmare la presente Offerta " +
        "negli spazi che seguono, a mano con timbro oppure con firma digitale, e caricarla nel Portale clienti.";

    public const string FirmaAccettazione = "Per accettazione dell'Offerta e delle Condizioni Generali (timbro e firma, facoltativa)";

    public const string TitoloApprovazioneSpecifica = "APPROVAZIONE SPECIFICA (artt. 1341 e 1342 c.c.)";

    /// <summary>
    /// Clausole da approvare specificamente: Allegato C delle Condizioni Rev. 19 BOZZA 18 (08/10/2026: aggiunti Artt. 3.3
    /// e 7.3). Deve coincidere con la Casella 2 della pagina di accettazione e con la specifica del Portale.
    /// </summary>
    public const string ApprovazioneSpecifica =
        "Il Cliente dichiara di approvare specificamente le seguenti clausole delle Condizioni Generali di Fornitura: " +
        "Art. 3.3 (rinnovo tacito annuale dell'Offerta e sua revisione con preavviso di 30 giorni), Art. 4.2 (conferma " +
        "dei dati dell'Ordine di Accettazione in assenza di segnalazione entro il termine), Artt. 5.4 e 13.2 (esclusioni " +
        "di responsabilità), Art. 7.3 (esclusione di responsabilità per il mancato ricevimento della comunicazione " +
        "anticipata), Art. 8.2 (limitazione di responsabilità per le prove in subappalto), Art. 9.1 (pubblicazione nel " +
        "Portale come consegna del Rapporto di Prova), Art. 9.4 (tempi indicativi), Art. 9.8 (Portale come servizio di " +
        "cortesia, onere di scaricare i Rapporti di Prova entro 30 giorni, esclusione di responsabilità per indisponibilità " +
        "dello storico), Art. 11.2 (termine di decadenza per le contestazioni), Art. 13.3 (limitazione del risarcimento), " +
        "Art. 13.4 (manleva), Art. 14.2 (sospensione del servizio e del Portale per ritardato pagamento), Art. 18.1 (foro " +
        "esclusivo di Rimini).";

    public const string FirmaApprovazioneSpecifica = "Per approvazione specifica (timbro e firma, facoltativa)";

    /// <summary>
    /// True se la revisione richiamata è la 19 o successiva: solo da lì le Condizioni hanno articoli numerati,
    /// quindi il riquadro di approvazione specifica ha senso. Con la Rev. 18 non viene stampato.
    /// </summary>
    public static bool HaArticoliNumerati(string? revisione)
    {
        var m = Regex.Match(revisione ?? "", @"Rev\.?\s*(\d+)", RegexOptions.IgnoreCase);
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) && n >= 19;
    }

    /// <summary>
    /// Frase del laboratorio esterno in subappalto: la frase di ArcLab (LabEsterno.Frase) o, in mancanza, il nome;
    /// mai l'id interno di ArcLab. Sempre con la dicitura "non accreditata dal Laboratorio" (Art. 8.2).
    /// </summary>
    public static string FraseSubappalto(string? fraseArcLab, string? nomeLaboratorio)
    {
        var base_ = !string.IsNullOrWhiteSpace(fraseArcLab) ? fraseArcLab!.Trim().TrimEnd('.', ';')
            : !string.IsNullOrWhiteSpace(nomeLaboratorio) ? $"prova eseguita presso {nomeLaboratorio!.Trim()}"
            : "prova eseguita presso laboratorio esterno";
        return base_ + " – prova in subappalto, non accreditata dal nostro Laboratorio (Condizioni Generali di Fornitura).";
    }

    /// <summary>
    /// Nota sotto le prove in subappalto (§), regola del 05/10/2026 (09/10/2026): in subappalto vanno solo le prove per
    /// cui il Laboratorio non è accreditato; una prova accreditata va ad altro laboratorio accreditato solo in via
    /// eccezionale e con l'assenso del Cliente, anche via e-mail. Nessun numero di articolo (la numerazione delle
    /// Condizioni cambia tra revisioni). Al plurale se i laboratori esterni sono più d'uno (simboli §1, §2, …).
    /// Usata da Offerta, Ordine di Accettazione e contratto di consulenza.
    /// </summary>
    /// <param name="cliente">Come è chiamato il cliente nel documento: «Cliente» (Offerta, Ordine) o «Committente» (contratto di consulenza).</param>
    public static string NotaSubappalto(int numeroLaboratori, string cliente = "Cliente") =>
        (numeroLaboratori > 1
            ? "Le prove contrassegnate con i simboli (§1, §2, ecc.) "
            : "Le prove contrassegnate con il simbolo (§) ") +
        "sono affidate in subappalto a laboratori terzi qualificati dal Laboratorio. Il Laboratorio affida in subappalto " +
        "solo prove per le quali non è accreditato; una prova accreditata è affidata ad altro laboratorio accreditato " +
        $"solo in via eccezionale, informandone prima il {cliente} e con il suo assenso, che può essere dato anche via " +
        "e-mail. Le prove in subappalto sono riportate come non accreditate dal nostro Laboratorio, anche se possono " +
        "esserlo presso il laboratorio terzo; lo stato di accreditamento del laboratorio terzo è consultabile nella banca " +
        "dati ACCREDIA (www.accredia.it). Si applicano le Condizioni Generali di Fornitura.";

    /// <summary>Numero offerta per testata e piè di pagina.</summary>
    public static string NumeroOfferta(int? anno, int? numero) => numero is int n ? $"{anno}/{n:000}" : "—";
}
