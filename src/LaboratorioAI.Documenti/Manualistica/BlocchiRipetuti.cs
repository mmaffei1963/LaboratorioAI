using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Documenti.Dvr;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// I blocchi ripetuti dei modelli (11/11/2026).
///
/// Per ora uno solo: <c>RIPETI:LETTERE_RESPONSABILE</c>, che avvolge la
/// lettera di nomina a responsabile esterno. In tredici modelli su
/// quattordici il blocco non contiene campi — serve solo a dire «questa
/// e' una lettera» — e l'unica cosa da fare e' aprirlo. Nel modello
/// della software house contiene invece la tabella dei trattamenti.
///
/// Dentro il blocco i campi si contano **per posizione**: lo stesso nome
/// ripetuto e' un campo diverso, e la risposta si cerca come
/// <c>NOME#n</c>. E' la stessa regola con cui le domande sono state
/// estratte dai modelli, ed e' <c>fillPos()</c> nel motore. Chi cambiasse
/// una delle due senza l'altra farebbe uscire lettere con i campi
/// sbagliati, senza che nessuno se ne accorga.
/// </summary>
internal static class BlocchiRipetuti
{
    /// <summary>La lettera di nomina a responsabile esterno.</summary>
    public const string Lettere = "RIPETI:LETTERE_RESPONSABILE";

    /// <summary>Le disposizioni ai manutentori interni, una per persona.</summary>
    public const string Manutentori = "RIPETI:MANUTENTORI_INTERNI";

    private const string Campo = "CAMPO:";
    private const string RigaCompilata = "SE:RIGA_COMPILATA";
    private const string Trattamento = "CAMPO:RESP_TRATTAMENTO";

    /// <summary>
    /// Apre il blocco delle lettere, una volta per copia richiesta.
    /// </summary>
    /// <param name="documento">Il documento in lavorazione.</param>
    /// <param name="copie">Quante lettere: una, salvo l'impegno alla
    /// riservatezza della ditta esterna, che se ne stampa una a persona.</param>
    /// <param name="contesto">Da cui si prendono i valori dei campi.</param>
    public static void ApriLettere(
        WordprocessingDocument documento, string marcatore, int copie,
        ContestoGenerazione contesto, bool nuovaPagina = false)
    {
        var quante = Math.Max(1, copie);

        foreach (var blocco in ControlliContenuto.Tutti(documento)
                     .Where(s => ControlliContenuto.Tag(s) == marcatore).ToList())
        {
            var genitore = blocco.Parent;
            if (genitore is null) continue;

            for (var n = 0; n < quante; n++)
            {
                var copia = (SdtElement)blocco.CloneNode(true);
                genitore.InsertBefore(copia, blocco);

                Riempi(copia, contesto);

                // Ogni copia dopo la prima comincia su una pagina sua:
                // sono fogli da firmare uno per persona.
                if (nuovaPagina && n > 0) ACapoPagina(copia);

                ControlliContenuto.Sciogli(copia);
            }

            blocco.Remove();
        }
    }

    /// <summary>Fa cominciare la copia su una pagina nuova.</summary>
    private static void ACapoPagina(SdtElement copia)
    {
        var primo = copia.Descendants<Paragraph>().FirstOrDefault();
        if (primo is null) return;

        var proprieta = primo.GetFirstChild<ParagraphProperties>();
        if (proprieta is null)
        {
            proprieta = new ParagraphProperties();
            primo.InsertAt(proprieta, 0);
        }

        if (proprieta.GetFirstChild<PageBreakBefore>() is not null) return;

        // Dopo lo stile, se c'e': Word vuole gli elementi in quest'ordine.
        var stile = proprieta.GetFirstChild<ParagraphStyleId>();
        if (stile is null) proprieta.InsertAt(new PageBreakBefore(), 0);
        else proprieta.InsertAfter(new PageBreakBefore(), stile);
    }

    /// <summary>Scrive i campi di una copia e decide quali righe restano.</summary>
    private static void Riempi(SdtElement copia, ContestoGenerazione contesto)
    {
        var occorrenze = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var sdt in CampiDi(copia))
        {
            var nome = ControlliContenuto.Tag(sdt)![Campo.Length..];
            occorrenze[nome] = occorrenze.TryGetValue(nome, out var n) ? n + 1 : 1;

            var valore = contesto.Campo($"{nome}#{occorrenze[nome]}", out _);
            ControlliContenuto.Scrivi(sdt, valore ?? "");
        }

        // Le righe della tabella dei trattamenti: resta solo quella in cui
        // il trattamento e' stato scritto, altrimenti la lettera uscirebbe
        // con cinque righe vuote.
        foreach (var riga in copia.Descendants<SdtElement>()
                     .Where(s => ControlliContenuto.Tag(s) == RigaCompilata).ToList())
        {
            if (riga.Parent is null) continue;

            var vuota = riga.Descendants<SdtElement>()
                .Where(s => (ControlliContenuto.Tag(s) ?? "").StartsWith(Trattamento, StringComparison.Ordinal))
                .All(s => string.IsNullOrWhiteSpace(ControlliContenuto.Leggi(s)));

            if (vuota) riga.Remove(); else ControlliContenuto.Sciogli(riga);
        }

        // I campi gia' scritti escono di scena subito: il passaggio
        // generale che viene dopo li cercherebbe col nome senza numero,
        // non li troverebbe e li svuoterebbe.
        foreach (var sdt in CampiDi(copia))
            if (sdt.Parent is not null) ControlliContenuto.Sciogli(sdt);
    }

    /// <summary>L'altro blocco ripetuto: le sedi del trattamento.</summary>
    public const string Sedi = "RIPETI:SEDI_TRATTAMENTO";

    /// <summary>
    /// Ripete il blocco delle sedi, una volta per sede.
    ///
    /// Il blocco compare in due forme, e il motore le tratta in modo
    /// diverso. Dentro una **tabella** ogni sede e' una riga, e dalla
    /// seconda in poi si svuota la prima cella, che e' l'etichetta e
    /// non va ripetuta. Dentro una **frase** le sedi si incolonnano una
    /// dopo l'altra separate da «; », e quando sono piu' d'una ciascuna
    /// porta davanti il nome dell'attivita' («Hotel Graziana – Via …»),
    /// altrimenti non si capirebbe di quale sede si parla.
    /// </summary>
    public static void ApriSedi(WordprocessingDocument documento, ContestoGenerazione contesto)
    {
        var sedi = contesto.Sedi;

        foreach (var blocco in ControlliContenuto.Tutti(documento)
                     .Where(s => ControlliContenuto.Tag(s) == Sedi).ToList())
        {
            var genitore = blocco.Parent;
            if (genitore is null) continue;

            // Nessuna sede: il blocco sparisce invece di lasciare i
            // segnaposto del modello in un documento da firmare.
            if (sedi.Count == 0) { blocco.Remove(); continue; }

            var inTabella = ContenutoDi(blocco)?.ChildElements.FirstOrDefault() is TableRow;
            var modelloRiga = blocco.Descendants<Run>().FirstOrDefault();

            for (var k = 0; k < sedi.Count; k++)
            {
                var sede = sedi[k];
                var copia = (SdtElement)blocco.CloneNode(true);
                genitore.InsertBefore(copia, blocco);

                ScriviSede(copia, sede);

                if (inTabella && k > 0) SvuotaEtichetta(copia);

                if (!inTabella && sedi.Count > 1 && !string.IsNullOrWhiteSpace(sede.Nome))
                    PremettiTesto(copia, modelloRiga, sede.Nome!.Trim() + " – ");

                if (!inTabella && k > 0)
                    InserisciPrima(genitore, copia, modelloRiga, "; ");

                ControlliContenuto.Sciogli(copia);
            }

            blocco.Remove();
        }
    }

    /// <summary>Il terzo blocco ripetuto: gli autorizzati al trattamento.</summary>
    public const string Autorizzati = "RIPETI:AUTORIZZATI";

    /// <summary>
    /// Ripete il blocco degli autorizzati, una volta per persona.
    ///
    /// Chi fa parte di un gruppo senza nominativo (i Lavoratori) occupa
    /// tante righe quanti sono: nei documenti ciascuno firma la sua, e
    /// il nome lo scrive a mano. Chi ha un nominativo ne occupa una.
    /// </summary>
    public static void ApriAutorizzati(WordprocessingDocument documento, ContestoGenerazione contesto)
    {
        foreach (var blocco in ControlliContenuto.Tutti(documento)
                     .Where(s => ControlliContenuto.Tag(s) == Autorizzati).ToList())
        {
            var genitore = blocco.Parent;
            if (genitore is null) continue;

            if (contesto.Autorizzati.Count == 0) { blocco.Remove(); continue; }

            foreach (var persona in contesto.Autorizzati)
            {
                var righe = persona.ConNominativo ? 1 : Math.Max(1, persona.Quantita);

                for (var k = 0; k < righe; k++)
                {
                    var copia = (SdtElement)blocco.CloneNode(true);
                    genitore.InsertBefore(copia, blocco);
                    ScriviPersona(copia, persona);
                    ControlliContenuto.Sciogli(copia);
                }
            }

            blocco.Remove();
        }
    }

    /// <summary>I campi di un autorizzato, cercati per nome.</summary>
    private static void ScriviPersona(SdtElement copia, AutorizzatoDocumento persona)
    {
        foreach (var sdt in CampiDi(copia))
        {
            var nome = ControlliContenuto.Tag(sdt)![Campo.Length..];
            var valore = nome switch
            {
                "AUTORIZZATO_NOME" => persona.ConNominativo ? persona.Nome ?? "" : "",
                "AUTORIZZATO_COGNOME" => persona.ConNominativo ? persona.Cognome ?? "" : "",
                "AUTORIZZATO_QUALIFICA" => persona.Qualifica,
                // Nelle nomine le funzioni vanno una per riga: quando
                // arriveremo a quel modello va verificato che il ritorno
                // a capo diventi un a capo di Word e non un quadratino.
                "AUTORIZZATO_FUNZIONE" => string.Join("\n", persona.Funzioni),
                _ => null
            };

            if (valore is null) continue;

            ControlliContenuto.Scrivi(sdt, valore);
            if (sdt.Parent is not null) ControlliContenuto.Sciogli(sdt);
        }
    }

    /// <summary>I tre campi di una sede, cercati per nome.</summary>
    private static void ScriviSede(SdtElement copia, SedeDocumento sede)
    {
        foreach (var sdt in CampiDi(copia))
        {
            var nome = ControlliContenuto.Tag(sdt)![Campo.Length..];
            var valore = nome switch
            {
                "SEDE_TRATTAMENTO_INDIRIZZO" => sede.Indirizzo ?? "",
                "SEDE_TRATTAMENTO_CAP_COMUNE" => ContestoGenerazione.CapComune(sede.Cap, sede.Comune, sede.Provincia),
                "SEDE_TRATTAMENTO_NOME" => sede.Nome ?? "",
                _ => null
            };

            if (valore is null) continue;

            ControlliContenuto.Scrivi(sdt, valore);
            if (sdt.Parent is not null) ControlliContenuto.Sciogli(sdt);
        }
    }

    /// <summary>
    /// Svuota la prima cella della riga: dalla seconda sede in poi
    /// l'etichetta non va ripetuta. Si tocca solo se la cella non
    /// contiene campi, per non cancellare un dato.
    /// </summary>
    private static void SvuotaEtichetta(SdtElement copia)
    {
        var cella = copia.Descendants<TableCell>().FirstOrDefault();
        if (cella is null || cella.Descendants<SdtElement>().Any()) return;

        foreach (var testo in cella.Descendants<Text>()) testo.Text = "";
    }

    /// <summary>Mette un pezzo di testo in testa alla copia.</summary>
    private static void PremettiTesto(SdtElement copia, Run? modello, string testo)
    {
        var contenuto = ContenutoDi(copia);
        if (contenuto is null) return;

        var riga = RigaConTesto(modello, testo);
        var primo = contenuto.ChildElements.FirstOrDefault();

        // Dentro un paragrafo il testo va nel paragrafo, non prima.
        if (primo is Paragraph paragrafo) paragrafo.InsertAt(riga, 0);
        else contenuto.InsertAt(riga, 0);
    }

    /// <summary>Mette un separatore fra una sede e la successiva.</summary>
    private static void InserisciPrima(
        OpenXmlElement genitore, SdtElement copia, Run? modello, string testo)
    {
        if (genitore is Paragraph) genitore.InsertBefore(RigaConTesto(modello, testo), copia);
    }

    /// <summary>
    /// Una riga di testo con la stessa formattazione del modello: senza
    /// copiarne le proprieta' il separatore uscirebbe con il carattere
    /// predefinito di Word, diverso dal resto della frase.
    /// </summary>
    private static Run RigaConTesto(Run? modello, string testo)
    {
        var riga = modello is null ? new Run() : (Run)modello.CloneNode(true);

        foreach (var figlio in riga.ChildElements.ToList())
            if (figlio is not RunProperties) figlio.Remove();

        riga.AppendChild(new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        return riga;
    }

    /// <summary>Il contenuto di un controllo, qualunque forma abbia.</summary>
    private static OpenXmlElement? ContenutoDi(SdtElement sdt) =>
        (OpenXmlElement?)sdt.GetFirstChild<SdtContentBlock>()
        ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentRun>()
        ?? (OpenXmlElement?)sdt.GetFirstChild<SdtContentCell>()
        ?? sdt.GetFirstChild<SdtContentRow>();

    private static List<SdtElement> CampiDi(SdtElement copia) =>
        copia.Descendants<SdtElement>()
            .Where(s => (ControlliContenuto.Tag(s) ?? "").StartsWith(Campo, StringComparison.Ordinal))
            .ToList();
}
