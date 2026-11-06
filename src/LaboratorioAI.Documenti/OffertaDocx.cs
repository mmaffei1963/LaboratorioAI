using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Genera l'offerta scrivendola dentro il modello Word di carta intestata (header/footer reali).
/// Usa solo OpenXml (nessun MigraDoc) per evitare conflitti di tipo. Lavora su una copia in memoria:
/// il modello non viene mai modificato.
/// </summary>
internal static class OffertaDocx
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");

    /// <summary>Rientro laterale aggiuntivo del contenuto (twips). 0 = usa direttamente i margini di pagina.</summary>
    private const int IndentLato = 0;

    /// <summary>Carattere del documento.</summary>
    private const string FontNome = "Montserrat";

    /// <summary>Margine sinistro/destro della pagina in twips (2 cm = 1134).</summary>
    private const uint MargineLatoTwips = 1134;

    /// <summary>
    /// Frase di verifica ACCREDIA per le prove in subappalto, al singolare se il laboratorio esterno
    /// coinvolto nell'offerta è uno solo (simbolo "§"), al plurale se sono più d'uno (simboli "§1",
    /// "§2", ecc., necessari per distinguerli).
    /// </summary>
    private static string TestoVerificaAccredia(int numeroLaboratori) =>
        TestiOfferta.NotaSubappalto(numeroLaboratori);

    /// <summary>Simbolo del laboratorio esterno: "§" da solo se in offerta ce n'è uno solo, "§N" se ce ne sono più d'uno.</summary>
    private static string Simbolo(int numeroLaboratoriTotali, int numero) =>
        numeroLaboratoriTotali > 1 ? "§" + numero : "§";

    // Testi fissi dell'offerta (presentazione, condizioni, firme): vedi TestiOfferta.cs (30/09/2026).

    public static byte[] Genera(Preventivo p, string percorsoModello)
    {
        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(percorsoModello)) fs.CopyTo(ms);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var sectPr = body.Elements<SectionProperties>().LastOrDefault();

            // Margini pagina: 2 cm a sinistra e destra (l'intestazione/piè della carta restano invariati).
            var pm = sectPr?.Elements<PageMargin>().FirstOrDefault();
            if (pm is not null) { pm.Left = MargineLatoTwips; pm.Right = MargineLatoTwips; }

            void Aggiungi(OpenXmlElement e)
            {
                if (sectPr is not null) body.InsertBefore(e, sectPr);
                else body.AppendChild(e);
            }

            var num = p.Numero is int n ? $"{p.Anno}/{n:000}" : "—";
            Aggiungi(Para($"OFFERTA N. {num}", bold: true, size: 28, align: JustificationValues.Center, spazioPrima: 120, spazioDopo: 120));

            // Testata cliente / offerta
            var data = (p.DataOfferta ?? DateTime.Today).ToString("dd/MM/yyyy", It);
            Aggiungi(Riga2("Data offerta", data, "Durata", TestiOfferta.DurataBreve(p.ValiditaGiorni, !p.SenzaRinnovoTacito)));
            if (TestiOfferta.ClienteGenerico(p.ClienteRagioneSociale))
            {
                // 21/10/2026: «Spett.le cliente»: tutti i dati, anche quelli vuoti, da compilare a mano.
                var dc = TestiOfferta.DaCompilare;
                Aggiungi(Riga1("Cliente", string.IsNullOrWhiteSpace(p.ClienteRagioneSociale) ? "Spett.le Cliente" : p.ClienteRagioneSociale!.Trim()));
                Aggiungi(Riga1("Nome cliente", dc(p.ClienteNome)));
                Aggiungi(Riga2("Partita IVA / C.F.", dc(p.ClientePartitaIva), "Telefono", dc(p.ClienteTelefono)));
                Aggiungi(Riga1("Indirizzo", dc(p.ClienteIndirizzo)));
                Aggiungi(Riga2("CAP", dc(p.ClienteCap), "Città (Prov.)", dc(string.Join(" ", new[] { p.ClienteCitta, p.ClienteProvincia }.Where(s => !string.IsNullOrWhiteSpace(s))))));
                // 21/10/2026: e-mail sempre da scrivere a mano (non quella del cliente o dell'insediamento);
                // pagamento con due caselle: «Pagato» spuntata se segnato in maschera, altrimenti entrambe vuote.
                Aggiungi(Riga1("E-mail / PEC", dc(null)));
                Aggiungi(RigaCaselle("Pagamento", ("Pagato", p.GiaPagato), ("Non pagato", false)));
            }
            else
            {
                Aggiungi(Riga2("Cliente", p.ClienteRagioneSociale ?? "—", "Partita IVA", Vuoto(p.ClientePartitaIva)));
                var indirizzo = string.Join(" ", new[] { p.ClienteIndirizzo, p.ClienteCap, p.ClienteCitta, p.ClienteProvincia }
                    .Where(s => !string.IsNullOrWhiteSpace(s)));
                Aggiungi(Riga1("Indirizzo", Vuoto(indirizzo)));
                if (!string.IsNullOrWhiteSpace(p.ClienteTelefono)) Aggiungi(Riga1("Telefono", p.ClienteTelefono!.Trim()));
            }
            Aggiungi(Riga1("Oggetto", Vuoto(p.Oggetto)));

            // Copertina: solo i dati identificativi sopra. L'allegato tecnico (analisi, prezzi,
            // condizioni economiche) parte sempre su una pagina nuova.
            Aggiungi(new Paragraph(new Run(new Break { Type = BreakValues.Page })));

            var righe = p.Righe.OrderBy(r => r.Progressivo).ToList();
            var controlli = righe.Where(r => r.Tipo != TipoRigaPreventivo.Accessorio).ToList();
            var accessori = righe.Where(r => r.Tipo == TipoRigaPreventivo.Accessorio).ToList();

            // Blocchi analisi
            var haSubappalto = controlli.Any(r => r.Metodi.Any(m => m.IdLabEsternoArcLab != null) || r.Tipo == TipoRigaPreventivo.ControlloLibero);

            // Numerazione dei laboratori esterni distinti (§1, §2, ...): un controllo raggruppato può
            // riunire metodi in subappalto presso laboratori diversi, quindi il simbolo § da solo non
            // basta più a distinguerli. La numerazione è unica per l'intera offerta (non per riga), nel
            // primo ordine in cui i laboratori compaiono, e le frasi corrispondenti si riportano una sola
            // volta ciascuna, dopo tutti i blocchi di analisi.
            var numeroLab = new Dictionary<int, int>();
            var fraseLab = new List<string>();
            foreach (var r in controlli)
                foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
                {
                    if (m.IdLabEsternoArcLab is not int idLabNum || numeroLab.ContainsKey(idLabNum)) continue;
                    numeroLab[idLabNum] = numeroLab.Count + 1;
                    fraseLab.Add(TestiOfferta.FraseSubappalto(m.LabEsternoFrase, m.LabEsterno));
                }

            foreach (var r in controlli)
            {
                var idPart = !string.IsNullOrWhiteSpace(r.IdControlliOrigine) ? $" (ID {r.IdControlliOrigine})"
                    : r.IdControlloArcLab is int idc ? $" (ID {idc})"
                    : !string.IsNullOrWhiteSpace(r.RifLocale) ? $" (ID {r.RifLocale})" : "";
                Aggiungi(Titolo("ANALISI DI LABORATORIO — " + (r.Descrizione ?? "").ToUpperInvariant() + idPart));
                if (p.MostraInsediamenti && !string.IsNullOrWhiteSpace(r.Insediamento))   // 05/11/2026; solo se spuntato (06/11/2026)
                    Aggiungi(Para("Insediamento: " + r.Insediamento, size: 17, italic: true, spazioDopo: 40));

                if (r.Metodi.Count > 0)
                {
                    // Colonna "Parametro" rimossa su richiesta (29/09/2026): lo spazio liberato va
                    // quasi tutto alla colonna "Metodo" (riferimento normativo, spesso un testo
                    // lungo), che in precedenza era la più compressa delle quattro. "Accr." resta la
                    // più stretta possibile: contiene solo "Sì", "—" o un simbolo "§N".
                    // 20/10/2026: colonna «U.M.» (unità di misura del metodo, da rigametodo/unità ArcLab).
                    var t = Tabella(2400, 5348, 1140, 750);
                    t.Append(RigaTab(true, "F3F3F3", ("Denominazione", 2400, false, null), ("Metodo", 5348, false, null), ("U.M.", 1140, false, null), ("Accr.", 750, false, null)));
                    foreach (var m in r.Metodi.OrderBy(x => x.Progressivo))
                        t.Append(RigaTab(false, null,
                            (m.Denominazione ?? m.Descrizione ?? m.DenominazioneRiga ?? "—", 2400, false, null),
                            (m.RiferimentoNormativo ?? "—", 5348, false, DimensioneMetodo),
                            (string.IsNullOrWhiteSpace(m.UnitaMisura) ? "—" : m.UnitaMisura.Trim(), 1140, false, DimensioneMetodo),
                            (m.IdLabEsternoArcLab is int idLabRiga ? Simbolo(numeroLab.Count, numeroLab[idLabRiga]) : (m.Accreditato ? "Sì" : "—"), 750, false, null)));
                    Aggiungi(t);
                }

                // Prezzo (12/10/2026): solo IVA esclusa; con lo sconto prezzo pieno, percentuale e prezzo scontato.
                var righePrezzo = TestiOfferta.RighePrezzo(r.PrezzoBase, r.ScontoPercentuale, r.Quantita, r.PrezzoFinale, accessorio: false);
                for (var k = 0; k < righePrezzo.Count; k++)
                    Aggiungi(Para(righePrezzo[k], bold: true, align: JustificationValues.Right, spazioDopo: k == righePrezzo.Count - 1 ? 60 : 0));
            }

            // Frasi dei laboratori esterni (subappalto): una sola volta per laboratorio, con il simbolo
            // corrispondente a quello riportato in tabella (semplice "§" se il laboratorio è uno solo).
            for (var i = 0; i < fraseLab.Count; i++)
                Aggiungi(Para($"{Simbolo(numeroLab.Count, i + 1)} {fraseLab[i]}", size: 17));

            // Avviso ACCREDIA: una sola volta per offerta (non ripetuto per ogni blocco/laboratorio),
            // evidenziato perché vale per tutte le prove in subappalto marcate con §.
            if (haSubappalto)
                Aggiungi(Para(TestoVerificaAccredia(numeroLab.Count), bold: true, size: 17, shadingFill: "FFF3D6", spazioPrima: 120, spazioDopo: 120));

            // Servizi accessori (12/10/2026): per ogni voce il titolo e il testo descrittivo occupano tutta la larghezza,
            // il prezzo va sotto a destra come per i controlli.
            if (accessori.Count > 0)
            {
                Aggiungi(Titolo("SERVIZI ACCESSORI"));
                foreach (var a in accessori)
                {
                    var t = Tabella(9638);
                    t.Append(RigaTab(true, "F3F3F3", (a.Descrizione ?? "—", 9638, false, null)));
                    var paragrafi = TestiOfferta.Paragrafi(a.Note);
                    if (paragrafi.Count > 0)
                    {
                        var tc = new TableCell(new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "9638" }));
                        for (var k = 0; k < paragrafi.Count; k++)
                            tc.Append(Para(paragrafi[k], size: 18, cella: true, spazioDopo: k < paragrafi.Count - 1 ? 100 : 0));
                        t.Append(new TableRow(tc));
                    }
                    Aggiungi(t);
                    var righeAcc = TestiOfferta.RighePrezzo(a.PrezzoBase, a.ScontoPercentuale, a.Quantita, a.PrezzoFinale, accessorio: true);
                    for (var k = 0; k < righeAcc.Count; k++)
                        Aggiungi(Para(righeAcc[k], bold: true, align: JustificationValues.Right, spazioDopo: k == righeAcc.Count - 1 ? 60 : 0));
                }
            }

            // Condizioni economiche
            var iva = Math.Round(p.TotaleImponibile * p.AliquotaIva / 100m, 2, MidpointRounding.AwayFromZero);
            Aggiungi(Titolo("CONDIZIONI ECONOMICHE"));
            var te = Tabella(false, 7200, 2000);
            te.Append(RigaImporto("Imponibile", p.TotaleImponibile, false));
            te.Append(RigaImporto($"IVA {Fmt(p.AliquotaIva)}%", iva, false));
            te.Append(RigaImporto("TOTALE", p.TotaleImponibile + iva, true));
            Aggiungi(te);

            foreach (var par in ParagrafiPagamento(p.CondizioniPagamento)) Aggiungi(par);
            Aggiungi(Para(TestiOfferta.Validita(p.ValiditaGiorni, !p.SenzaRinnovoTacito), size: 17, spazioPrima: 120));
            if (!string.IsNullOrWhiteSpace(p.Note))
                Aggiungi(Para(p.Note!.Trim(), size: 17));

            // Condizioni generali di fornitura
            var rev = string.IsNullOrWhiteSpace(p.RevisioneCondizioni) ? "Mod. Com. 05" : p.RevisioneCondizioni!.Trim();
            Aggiungi(Titolo(TestiOfferta.TitoloCondizioni));
            Aggiungi(Para(TestiOfferta.Condizioni(rev), size: 17));
            Aggiungi(Para(TestiOfferta.Accettazione, size: 17, bold: true, spazioPrima: 120));

            // Riepilogo promozionale "Il nostro lavoro" (opzionale, a scelta del commerciale): in pagine
            // a parte, oltre la fine delle condizioni, per non mescolarlo al contenuto contrattuale/operativo.
            if (p.RiepilogoPromozionale)
            {
                Aggiungi(new Paragraph(new Run(new Break { Type = BreakValues.Page })));
                Aggiungi(Titolo(TestiOfferta.TitoloIlNostroLavoro));
                Aggiungi(Para(TestiOfferta.IlNostroLavoro, size: 17, spazioDopo: 60));
                Aggiungi(Para(TestiOfferta.TaglineIlNostroLavoro, size: 17, bold: true, italic: true, spazioDopo: 120));
                Aggiungi(Para(TestiOfferta.TitoloLaboratorioAnalisi, size: 17, bold: true, spazioDopo: 30));
                Aggiungi(Para(TestiOfferta.LaboratorioAnalisi, size: 17, spazioDopo: 120));
            }

            // Informativa privacy (fissa, uguale per ogni offerta)
            AggiungiInformativaPrivacy(Aggiungi);

            // Firma facoltativa (09/10/2026): l'accettazione dal Portale non richiede firme.
            Aggiungi(Para(TestiOfferta.NotaFirme, size: 17, italic: true, spazioPrima: 240));
            // Firma (tabella a due colonne: allineamento indipendente dal carattere usato)
            var tFirma = Tabella(false, 4600, 4600);
            tFirma.Append(RigaTab(false, null, (TestiOfferta.FirmaAccettazione, 4600, false, null), ("MARE.A s.r.l.", 4600, false, null)));
            tFirma.Append(RigaTab(false, null, ("______________________________", 4600, false, null), ("______________________________", 4600, false, null)));
            Aggiungi(Para("", spazioPrima: 240));
            Aggiungi(tFirma);

            // Approvazione specifica delle clausole vessatorie (artt. 1341-1342 c.c.): firma SEPARATA,
            // una firma sola "per accettazione" non basta. Solo con Condizioni ad articoli numerati (Rev. 19+).
            if (TestiOfferta.HaArticoliNumerati(rev))
            {
                Aggiungi(Para(TestiOfferta.TitoloApprovazioneSpecifica, size: 17, bold: true, spazioPrima: 240));
                Aggiungi(Para(TestiOfferta.ApprovazioneSpecifica, size: 17));
                var tSpec = Tabella(false, 4600, 4600);
                tSpec.Append(RigaTab(false, null, (TestiOfferta.FirmaApprovazioneSpecifica, 4600, false, null), ("", 4600, false, null)));
                tSpec.Append(RigaTab(false, null, ("______________________________", 4600, false, null), ("", 4600, false, null)));
                Aggiungi(Para("", spazioPrima: 120));
                Aggiungi(tSpec);
            }

            doc.MainDocumentPart.Document.Save();

            // Numerazione di pagina (29/09/2026): aggiunta in fondo a ogni piè di pagina del modello
            // (di norma "Primo" per la copertina e "Predefinito" per le pagine successive, secondo
            // come è impostata la carta intestata) così compare su tutte le pagine senza toccare il
            // contenuto grafico esistente del piè di pagina (loghi/dati societari della carta intestata).
            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                footerPart.Footer.Append(ParagrafoNumeroPagina($"Offerta N. {TestiOfferta.NumeroOfferta(p.Anno, p.Numero)} – "));
                footerPart.Footer.Save();
            }
        }

        return ms.ToArray();
    }

    /// <summary>Paragrafo "Pagina X di Y" con campi Word dinamici (PAGE / NUMPAGES), aggiornati automaticamente da Word all'apertura/stampa.</summary>
    internal static Paragraph ParagrafoNumeroPagina(string prefisso = "")
    {
        var pPr = new ParagraphProperties(new Justification { Val = JustificationValues.Center });
        var para = new Paragraph(pPr);

        var campoPagina = new SimpleField { Instruction = "PAGE" };
        campoPagina.Append(RunTesto("1", false));
        var campoTotale = new SimpleField { Instruction = "NUMPAGES" };
        campoTotale.Append(RunTesto("1", false));

        para.Append(RunTesto(prefisso + "Pagina ", false));
        para.Append(campoPagina);
        para.Append(RunTesto(" di ", false));
        para.Append(campoTotale);
        return para;
    }

    // ── Helper testo ───────────────────────────────────────────────
    internal static string Vuoto(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s!.Trim();
    internal static string Fmt(decimal v) => v.ToString("N2", It);

    // ── Informativa privacy (art. 13 Reg. UE 2016/679) — testo fisso MARE.A ──
    private static void Sotto(Action<OpenXmlElement> agg, string t) => agg(Para(t, bold: true, size: 17, spazioPrima: 80));
    private static void Testo(Action<OpenXmlElement> agg, string t) => agg(Para(t, size: 16));

    private static void AggiungiInformativaPrivacy(Action<OpenXmlElement> agg)
    {
        agg(Para("", size: 8, spazioPrima: 220));   // un po' di spazio dopo le Condizioni Generali
        agg(Titolo("INFORMATIVA PRIVACY"));
        agg(Para("Ai sensi dell'art. 13 Regolamento UE 2016/679", size: 16, italic: true, spazioDopo: 60));

        Sotto(agg, "Identità e dati di contatto del titolare del trattamento");
        Testo(agg, "Il titolare del trattamento è la ditta MARE.A s.r.l. con sede legale in Via E. Toti, 2 - 47841, Cattolica (RN) tel. 0541 830442, fax 0541 830460 e-mail: info@mare-a.com – CF/P.IVA 03670240401.");

        Sotto(agg, "Finalità e base giuridica del trattamento");
        Testo(agg, "I suoi dati personali sono trattati nel rispetto dei principi di correttezza, liceità e trasparenza, per le seguenti finalità:");
        Testo(agg, "1) Fornitura del servizio ed adempimenti amministrativi e fiscali connessi al rapporto contrattuale;");
        Testo(agg, "2) Servizio di assistenza post-vendita, ivi compresi eventuali contatti a mezzo posta cartacea e/o telefonici e/o tramite e-mail e/o sms per comunicazioni inerenti lo svolgimento dei servizi da lei affidatici;");
        Testo(agg, "3) Newsletter: previo ottenimento del suo consenso, il Titolare e gli incaricati al trattamento potranno inoltre inviarle, a mezzo posta cartacea, via e-mail, sms o App di messaggistica, occasionali aggiornamenti circa nuovi servizi obbligatori e/o funzionali alla sua attività;");
        Testo(agg, "4) Profilazione: previo ottenimento del suo consenso, il Titolare e gli incaricati al trattamento potranno categorizzare la sua attività (es. Hotel, Ristorante, Ingrosso ecc.) al fine di poter selezionare più accuratamente le comunicazioni a lei destinate.");

        Sotto(agg, "Destinatari dei dati personali");
        Testo(agg, "Nell'ambito dei trattamenti di cui alla presente informativa, i suoi dati, anche appartenenti alle categorie particolari di cui all'art. 9 del Regolamento UE 2016/679, saranno trattati da nostri incaricati interni e potranno essere comunicati anche a soggetti terzi, formalmente nominati responsabili del trattamento.");
        Testo(agg, "Nell'ambito del trattamento di cui al punto 1), in particolare, i suoi dati saranno comunicati ai consulenti amministrativi e fiscali del Titolare ed alle Autorità pubbliche competenti in materia.");

        Sotto(agg, "Obbligo di fornirci i suoi dati");
        Testo(agg, "La fornitura dei suoi dati personali per le finalità 1) e 2), anche appartenenti alle categorie particolari di cui all'art. 9 del Regolamento UE 2016/679, è necessaria ai fini dell'esecuzione del servizio contrattuale e conseguentemente la loro mancata o incompleta fornitura renderà impossibile assicurare tale servizio.");
        Testo(agg, "Il conferimento dei dati per le finalità di cui ai punti 3) e 4), invece, non è necessario ed il soggetto interessato ha il diritto di opporsi in qualsiasi momento a tale trattamento, anche in parte, inviando la richiesta all'indirizzo del Titolare del trattamento indicato sopra. Per quanto riguarda le finalità di cui al punto 4), infine, il conferimento dei suoi dati non è obbligatorio, ma il mancato consenso alla profilazione ci impedirà di inviarle comunicazioni personalizzate.");

        Sotto(agg, "Periodo di conservazione dei dati");
        Testo(agg, "I suoi dati personali saranno conservati per tutto il tempo imposto in base a disposizioni normative inerenti i servizi da lei affidatici e la disciplina amministrativa e fiscale.");
        Testo(agg, "Per finalità informative e per l'invio di aggiornamenti normativi, news relative ad iniziative commerciali, nonché per comunicare iniziative ed altri eventi, saranno conservati per un periodo di 24 mesi, fatta salva l'ipotesi di ulteriori vendite e/o sue richieste che dovessero avvenire in tale arco di tempo e/o la necessità di conservare i dati per le altre finalità di cui alla presente informativa.");

        Sotto(agg, "Diritti dell'interessato");
        Testo(agg, "Lei ha diritto di revocare il consenso prestato in calce alla presente informativa in qualsiasi momento tramite comunicazione all'indirizzo del Titolare del trattamento. Allo stesso modo lei potrà chiedere l'accesso ai suoi dati personali, la rettifica, la cancellazione, la limitazione o l'opposizione del trattamento oltre che la portabilità dei dati (art. 20 del Regolamento UE 2016/679). La richiesta di cancellazione dei dati è subordinata agli obblighi di conservazione dei documenti imposti da norme di legge.");

        Sotto(agg, "Diritto di proporre reclamo all'Autorità di controllo");
        Testo(agg, "Lei potrà, nel caso ritenga che il trattamento di dati che la riguardano violi il Regolamento UE 2016/679, proporre reclamo all'Autorità per la protezione dei dati personali.");

        Sotto(agg, "Consensi");
        Testo(agg, "Acconsento al trattamento dei dati di natura particolare ai sensi dell'art. 9 del Reg. UE 2016/679.");
        Testo(agg, "[  ] SI        [  ] NO");
        Testo(agg, "Acconsento ad essere contattato a mezzo posta cartacea, via e-mail, sms o App di messaggistica, per l'invio di occasionali aggiornamenti circa nuovi servizi obbligatori e/o funzionali alla sua attività.");
        Testo(agg, "[  ] SI        [  ] NO");
        Testo(agg, "Acconsento al trattamento dei miei dati personali per l'attività di profilazione al fine di poter selezionare più accuratamente le comunicazioni a lei destinate.");
        Testo(agg, "[  ] SI        [  ] NO");
    }

    internal static Paragraph Para(string testo, bool bold = false, int size = 20, bool italic = false,
        JustificationValues? align = null, string? shadingFill = null, int spazioPrima = 0, int spazioDopo = 0,
        bool cella = false)
    {
        var pPr = new ParagraphProperties();
        if (shadingFill is not null)
            pPr.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = shadingFill });
        if (spazioPrima > 0 || spazioDopo > 0)
            pPr.Append(new SpacingBetweenLines { Before = spazioPrima.ToString(), After = spazioDopo.ToString() });
        if (!cella)
            pPr.Append(new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
        if (align is not null)
            pPr.Append(new Justification { Val = align.Value });

        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome });
        if (bold) rPr.Append(new Bold());
        if (italic) rPr.Append(new Italic());
        rPr.Append(new FontSize { Val = size.ToString() });

        var run = new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(rPr);

        var para = new Paragraph();
        para.Append(pPr);
        para.Append(run);
        return para;
    }

    /// <summary>
    /// Condizioni di pagamento (12/10/2026): con una modalità titolo e righe; con due o più la frase di scelta e,
    /// per ognuna, la casella ☐ con la prima riga in grassetto e le altre rientrate (come nel modello di Mirko).
    /// </summary>
    internal static IEnumerable<Paragraph> ParagrafiPagamento(string? testo, int dimensione = 17)
    {
        var modalita = TestiOfferta.ModalitaPagamento(testo);
        if (modalita.Count == 0) yield break;
        yield return Para(modalita.Count == 1 ? "Condizioni di pagamento:" : TestiOfferta.SceltaPagamento, size: dimensione, bold: true, spazioPrima: 120);
        foreach (var m in modalita)
            for (var k = 0; k < m.Count; k++)
                yield return RigaPagamento(m[k], prima: k == 0, casella: modalita.Count > 1, dimensione, spazioPrima: k == 0 ? 80 : 0);
    }

    private static Paragraph RigaPagamento(string testo, bool prima, bool casella, int dimensione, int spazioPrima)
    {
        var rientro = casella ? 680 : 340;
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { Before = spazioPrima.ToString(), After = "0" },
            new Indentation { Left = rientro.ToString(), Hanging = prima && casella ? "340" : "0" });
        var para = new Paragraph(pPr);
        if (prima && casella)
        {
            var rCasella = new Run(new RunProperties(
                new RunFonts { Ascii = "Segoe UI Symbol", HighAnsi = "Segoe UI Symbol", ComplexScript = "Segoe UI Symbol" },
                new FontSize { Val = (dimensione + 2).ToString() }), new Text("☐"), new TabChar());
            para.Append(rCasella);
        }
        var rPr = new RunProperties(new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome });
        if (prima) rPr.Append(new Bold());
        rPr.Append(new FontSize { Val = dimensione.ToString() });
        para.Append(new Run(rPr, new Text(testo) { Space = SpaceProcessingModeValues.Preserve }));
        return para;
    }

    internal static Paragraph Titolo(string testo) =>
        Para(testo, bold: true, size: 22, shadingFill: "EEEEEE", spazioPrima: 160, spazioDopo: 60);

    /// <summary>Etichetta seguita da caselle ☐/☒ (21/10/2026, pagamento dello «Spett.le cliente»).</summary>
    internal static Paragraph RigaCaselle(string label, params (string Testo, bool Spuntata)[] voci)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { After = "20" },
            new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
        var para = new Paragraph(pPr);
        para.Append(RunTesto(label + ": ", true));
        foreach (var (testo, spuntata) in voci)
        {
            para.Append(new Run(new RunProperties(
                new RunFonts { Ascii = "Segoe UI Symbol", HighAnsi = "Segoe UI Symbol", ComplexScript = "Segoe UI Symbol" }),
                new Text(spuntata ? "☒" : "☐")));
            para.Append(RunTesto(" " + testo + "      ", false));
        }
        return para;
    }

    internal static Paragraph Riga1(string label, string valore)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { After = "20" },
            new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
        var para = new Paragraph(pPr);
        para.Append(RunTesto(label + ": ", true));
        para.Append(RunTesto(valore, false));
        return para;
    }

    internal static Paragraph Riga2(string l1, string v1, string l2, string v2)
    {
        var pPr = new ParagraphProperties(
            new SpacingBetweenLines { After = "20" },
            new Indentation { Left = IndentLato.ToString(), Right = IndentLato.ToString() });
        var para = new Paragraph(pPr);
        para.Append(RunTesto(l1 + ": ", true));
        para.Append(RunTesto(v1 + "        ", false));
        para.Append(RunTesto(l2 + ": ", true));
        para.Append(RunTesto(v2, false));
        return para;
    }

    internal static Run RunTesto(string testo, bool bold)
    {
        var rPr = new RunProperties();
        rPr.Append(new RunFonts { Ascii = FontNome, HighAnsi = FontNome, ComplexScript = FontNome });
        if (bold) rPr.Append(new Bold());
        rPr.Append(new FontSize { Val = "20" });
        var run = new Run(new Text(testo) { Space = SpaceProcessingModeValues.Preserve });
        run.PrependChild(rPr);
        return run;
    }

    // ── Helper tabelle ─────────────────────────────────────────────
    internal static Table Tabella(params int[] larghezze) => Tabella(true, larghezze);

    internal static Table Tabella(bool bordi, params int[] larghezze)
    {
        var props = new TableProperties(
            new TableWidth { Type = TableWidthUnitValues.Dxa, Width = larghezze.Sum().ToString() },
            new TableIndentation { Type = TableWidthUnitValues.Dxa, Width = IndentLato });
        if (bordi)
        {
            EnumValue<BorderValues> s = BorderValues.Single;
            props.Append(new TableBorders(
                new TopBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new LeftBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new BottomBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new RightBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new InsideHorizontalBorder { Val = s, Size = 4, Color = "BBBBBB" },
                new InsideVerticalBorder { Val = s, Size = 4, Color = "BBBBBB" }));
        }
        var t = new Table();
        t.Append(props);
        var grid = new TableGrid();
        foreach (var w in larghezze) grid.Append(new GridColumn { Width = w.ToString() });
        t.Append(grid);
        return t;
    }

    // Il param bool per cella indica l'allineamento a destra. Dimensione (facoltativa, in
    // mezzi-punti come FontSize di OpenXml) sovrascrive la dimensione di default (18 = 9pt) solo
    // per quella cella — usata per un carattere più piccolo nella colonna "Metodo", spesso lunga.
    private const int DimensioneMetodo = 16; // 8pt, contro il default di 9pt delle altre colonne.

    internal static TableRow RigaTab(bool grassetto, string? shadingFill, params (string Testo, int Larghezza, bool Destra, int? Dimensione)[] celle)
    {
        var tr = new TableRow();
        foreach (var (testo, larghezza, destra, dimensione) in celle)
        {
            var tcPr = new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = larghezza.ToString() });
            if (shadingFill is not null)
                tcPr.Append(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = shadingFill });
            var para = Para(testo, bold: grassetto, size: dimensione ?? 18, align: destra ? JustificationValues.Right : null, cella: true);
            tr.Append(new TableCell(tcPr, para));
        }
        return tr;
    }

    internal static TableRow RigaImporto(string label, decimal importo, bool grassetto)
    {
        var tr = new TableRow();
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "7200" }),
            Para(label, bold: grassetto, align: JustificationValues.Right, cella: true)));
        tr.Append(new TableCell(
            new TableCellProperties(new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = "2000" }),
            Para($"€ {Fmt(importo)}", bold: grassetto, align: JustificationValues.Right, cella: true)));
        return tr;
    }
}
