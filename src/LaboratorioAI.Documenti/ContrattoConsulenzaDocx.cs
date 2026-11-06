using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Dati.Consulenza;
using static LaboratorioAI.Documenti.OffertaDocx;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Contratto/offerta di consulenza (serie C) sulla carta intestata Word, con gli stessi helper dell'offerta di laboratorio.
/// Rinvia alle Condizioni Generali di Fornitura – Servizi di Consulenza e Formazione (Mod. Com. 05-Cons) e contiene il
/// riquadro di approvazione specifica (artt. 1341-1342 c.c.) delle clausole del loro Allegato A.
/// </summary>
public static class ContrattoConsulenzaDocx
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private const int Larghezza = 9638;

    public static string NomeFile(PreventivoConsulenza p) =>
        $"Contratto_{p.NumeroCompleto.Replace('/', '-')}_{Pulisci(p.RagioneSociale)}.docx";

    public static string NomeFilePdf(PreventivoConsulenza p) => Path.ChangeExtension(NomeFile(p), ".pdf");

    /// <summary>PDF del contratto ricavato dal Word (stesso contenuto), su carta intestata MARE.A.</summary>
    public static byte[] GeneraPdf(PreventivoConsulenza p, string percorsoModello, string? percorsoWwwRoot)
    {
        var rev = string.IsNullOrWhiteSpace(p.RevisioneCondizioni) ? "Mod. Com. 05-Cons" : p.RevisioneCondizioni!;
        return ConvertitoreWordPdf.Converti(Genera(p, percorsoModello), percorsoWwwRoot, $"Contratto {p.NumeroCompleto} – {rev} – Pagina ");
    }

    public static byte[] Genera(PreventivoConsulenza p, string percorsoModello)
    {
        using var ms = new MemoryStream();
        using (var fs = File.OpenRead(percorsoModello)) fs.CopyTo(ms);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var sectPr = body.Elements<SectionProperties>().LastOrDefault();
            var pm = sectPr?.Elements<PageMargin>().FirstOrDefault();
            if (pm is not null) { pm.Left = 1134; pm.Right = 1134; }
            void A(DocumentFormat.OpenXml.OpenXmlElement e)
            {
                if (sectPr is not null) body.InsertBefore(e, sectPr); else body.AppendChild(e);
            }

            var rev = string.IsNullOrWhiteSpace(p.RevisioneCondizioni) ? "Mod. Com. 05-Cons" : p.RevisioneCondizioni!;
            A(Para("CONTRATTO DI PRESTAZIONE DI SERVIZI", bold: true, size: 26, align: JustificationValues.Center, spazioPrima: 60));
            A(Para($"Offerta n. {p.NumeroCompleto}", bold: true, size: 22, align: JustificationValues.Center, spazioDopo: 160));
            A(Riga2("Data", (p.DataOfferta ?? DateTime.Today).ToString("dd/MM/yyyy", It), "Validità dell'offerta", $"{p.ValiditaGiorni} giorni"));

            // Committente
            A(Titolo("COMMITTENTE"));
            A(Riga1("Ragione sociale", Vuoto(p.RagioneSociale)));
            A(Riga2("Partita IVA", Vuoto(p.PartitaIva), "Codice fiscale", Vuoto(p.CodiceFiscale)));
            A(Riga1("Sede legale", Vuoto(Unisci(p.Indirizzo, p.Cap, p.Citta, string.IsNullOrWhiteSpace(p.Provincia) ? null : $"({p.Provincia})"))));
            A(Riga2("Codice destinatario SDI", Vuoto(p.CodiceSdi), "PEC", Vuoto(p.Pec)));
            A(Riga2("E-mail", Vuoto(p.Email), "Telefono", Vuoto(p.Telefono)));   // 29/10/2026
            A(Riga1("Sede del servizio", Vuoto(Unisci(p.SedeNome, string.IsNullOrWhiteSpace(p.SedeIndirizzo) ? null : "– " + p.SedeIndirizzo))));
            A(Riga1("Referente", Vuoto(Unisci(p.ReferenteNome, string.IsNullOrWhiteSpace(p.ReferenteMansione) ? null : $"({p.ReferenteMansione})",
                string.IsNullOrWhiteSpace(p.ReferenteTelefono) ? null : "tel. " + p.ReferenteTelefono, p.ReferenteEmail))));
            if (!string.IsNullOrWhiteSpace(p.SettoreAttivita)) A(Riga1("Settore di attività", p.SettoreAttivita!));
            A(Para("di seguito «Committente».", size: 18, italic: true, spazioDopo: 120));

            if (!string.IsNullOrWhiteSpace(p.Oggetto)) A(Riga1("Oggetto", p.Oggetto!));
            A(Para("MARE.A s.r.l. si impegna a prestare al Committente i servizi descritti di seguito, alle condizioni della presente Offerta " +
                   "e delle Condizioni Generali richiamate in fondo.", size: 18, spazioPrima: 80, spazioDopo: 80));

            // Servizi. 29/10/2026 (osservazioni di Ludo): blocchi di quotazione con titolo e totale propri, senza
            // etichette imposte; gerarchia blocco → attività; righe «per conoscenza» al loro posto; «IVA esclusa» su ogni prezzo.
            var blocchi = ServizioPreventiviConsulenza.BlocchiOrdinati(p);
            var servizi = p.Righe.Where(r => !r.Informativa).ToList();
            if (blocchi.Count == 0)
            {
                A(ConSuccessivo(Titolo("SERVIZI OFFERTI")));
                AttivitaBlocco(null);
            }
            foreach (var b in blocchi)
            {
                if (!string.IsNullOrWhiteSpace(b.Titolo))
                    A(ConSuccessivo(Para(b.Titolo!.Trim().ToUpper(It), bold: true, size: 24, shadingFill: "D9D9D9", spazioPrima: 280, spazioDopo: 80)));
                else
                    A(Para("", size: 6, spazioPrima: 200));
                if (!string.IsNullOrWhiteSpace(b.Descrizione))
                    foreach (var par in Righe(b.Descrizione)) A(ConSuccessivo(Para(par, size: 18, italic: true, spazioDopo: 60)));
                AttivitaBlocco(b);
                var (primo, succ, can) = ServizioPreventiviConsulenza.TotaliBlocco(p, b);
                if (ServizioPreventiviConsulenza.RigheBlocco(p, b).Any(r => !r.Informativa))
                {
                    var tb = Tabella(false, 7200, 2438);
                    tb.Append(RigaImporto("Totale del blocco" + (can ? " – primo anno" : "") + " (IVA esclusa)", primo, true));
                    if (can) tb.Append(RigaImporto("Totale del blocco – dal secondo anno, all'anno (IVA esclusa)", succ, true));
                    A(Para("", size: 6, spazioPrima: 60));
                    A(tb);
                }
            }

            void AttivitaBlocco(BloccoPreventivoConsulenza? b)
            {
                var righe = ServizioPreventiviConsulenza.RigheBlocco(p, b);
                foreach (var r in righe)
                {
                    // Il titolo dell'attività si omette se ripete quello del blocco
                    var ripete = b is not null && string.Equals((b.Titolo ?? "").Trim(), r.DescrizioneAttivita.Trim(), StringComparison.OrdinalIgnoreCase);
                    if (!ripete)
                        A(ConSuccessivo(BordoSotto(Para(r.DescrizioneAttivita, bold: true, size: 20, spazioPrima: 220, spazioDopo: 60))));
                    if (r.Informativa)
                        A(ConSuccessivo(Para("Per conoscenza – non compreso nel totale", italic: true, size: 17, spazioDopo: 40)));
                    if (!string.IsNullOrWhiteSpace(r.DescrizioneLibera)) A(ConSuccessivo(Para(r.DescrizioneLibera!, size: 18, bold: true, spazioDopo: 40)));
                    if (!string.IsNullOrWhiteSpace(r.TestoOfferta)) A(Corto(Para(r.TestoOfferta!, size: 18, spazioDopo: 40)));
                    foreach (var c in Righe(r.Contenuti)) A(ConSuccessivo(Para("– " + c, size: 17)));
                    if (r.ModalitaPrezzo == ModalitaPrezzoConsulenza.Annuale && !string.IsNullOrWhiteSpace(r.TestoSuccessivi))
                    {
                        A(ConSuccessivo(Para("Secondo anno e successivi", size: 18, bold: true, spazioPrima: 60)));
                        A(Corto(Para(r.TestoSuccessivi!, size: 17)));
                    }
                    var t = Tabella(7200, 2438);
                    foreach (var (etichetta, importo) in Prezzi(r))
                        t.Append(RigaTab(false, r.Informativa ? "F7F7F7" : null, (etichetta, 7200, false, null), ($"€ {Fmt(importo)}", 2438, true, null)));
                    A(ConSuccessivo(Para("", size: 6, spazioPrima: 40)));
                    A(t);
                }
            }

            // Analisi di laboratorio (controlli ArcLab, come nell'offerta analisi): sezione a sé, dopo i blocchi
            if (p.Analisi.Count > 0) AggiungiAnalisi(A, p);

            // Condizioni economiche (nuova pagina)
            A(NuovaPagina(ConSuccessivo(Titolo("CONDIZIONI ECONOMICHE"))));
            var tot = Tabella(7200, 2438);
            var canoni = servizi.Any(r => r.ModalitaPrezzo == ModalitaPrezzoConsulenza.Annuale);
            var conPrezzo = blocchi.Where(b => ServizioPreventiviConsulenza.RigheBlocco(p, b).Any(r => !r.Informativa)).ToList();
            if (conPrezzo.Count > 0)
            {
                // Una riga per blocco: i corrispettivi non si sommano
                var tbl = Tabella(5638, 2000, 2000);
                tbl.Append(RigaTab(true, "F3F3F3", ("Blocco", 5638, false, null), ("Primo anno / prestazione", 2000, true, null), ("Dal secondo anno, all'anno", 2000, true, null)));
                foreach (var b in conPrezzo)
                {
                    var (primo, succ, can) = ServizioPreventiviConsulenza.TotaliBlocco(p, b);
                    tbl.Append(RigaTab(false, null, (ServizioPreventiviConsulenza.NomeBlocco(b), 5638, false, null),
                        ($"€ {Fmt(primo)}", 2000, true, null), (can ? $"€ {Fmt(succ)}" : "—", 2000, true, null)));
                }
                A(tbl);
                A(Para("Importi IVA esclusa." + (conPrezzo.Count > 1 ? " Ogni blocco è quotato separatamente: i corrispettivi dei blocchi non si sommano tra loro." : ""),
                    size: 17, bold: true, spazioPrima: 40, spazioDopo: 60));
            }
            if (blocchi.Count == 0)
            {
                var (primo, succ, can) = ServizioPreventiviConsulenza.TotaliBlocco(p, null);
                tot.Append(RigaImporto((can ? "Corrispettivo per il primo anno" : "Corrispettivo complessivo") + " (IVA esclusa)", primo, true));
                if (can) tot.Append(RigaImporto("Corrispettivo annuo dal secondo anno (IVA esclusa)", succ, true));
            }
            var stima = p.Analisi.Where(a => a.Modalita == ModalitaAnalisiConsulenza.APagamento && a.QuantitaAnnua is > 0)
                .Sum(a => Math.Round(a.Prezzo * a.QuantitaAnnua!.Value, 2));
            if (stima > 0) tot.Append(RigaImporto("Analisi di laboratorio a pagamento: stima annua, IVA esclusa (fatturate a esecuzione, non comprese nei corrispettivi)", stima, false));
            if (tot.Elements<TableRow>().Any()) A(tot);
            A(Para("Tutti i prezzi sono IVA esclusa. Eventuali spese documentate sostenute in nome e per conto del Committente sono addebitate a parte." +
                   (canoni ? " I canoni sono aggiornati annualmente secondo l'indice ISTAT (Condizioni Generali, art. 12.1)." : ""), size: 17, spazioPrima: 60));
            if (canoni)
                A(Para($"Durata: {p.DurataAnni} anni" + (p.DataInizio is DateTime di ? $" dal {di:dd/MM/yyyy}" : " dalla sottoscrizione") +
                       ", con rinnovo tacito per uguale periodo salvo disdetta comunicata con almeno 90 giorni di preavviso (Condizioni Generali, art. 3.2)." +
                       (p.NumeroRate > 1 ? $" Fatturazione dei canoni in {p.NumeroRate} rate annuali." : ""), size: 17, spazioPrima: 40));
            foreach (var par in ParagrafiPagamento(p.CondizioniPagamento)) A(par);
            A(Para("", size: 6, spazioPrima: 120));
            A(Riga1("Consulente MARE.A di riferimento", Vuoto(p.ConsulenteNome)));
            if (!string.IsNullOrWhiteSpace(p.CommercialeNome)) A(Riga1("Riferimento commerciale", p.CommercialeNome!));
            if (!string.IsNullOrWhiteSpace(p.ResponsabileCliente)) A(Riga1("Responsabile cliente", p.ResponsabileCliente!));

            // Condizioni generali
            A(NuovaPagina(ConSuccessivo(Titolo("CONDIZIONI GENERALI E FIRME"))));
            A(Para($"Il presente contratto è regolato dalle Condizioni Generali di Fornitura – Servizi di Consulenza e Formazione di MARE.A s.r.l. ({rev}), " +
                   "pubblicate su www.mare-a.com e nel Portale clienti e disponibili su richiesta, che ne costituiscono parte integrante e che il Committente " +
                   "dichiara di conoscere e di accettare. In caso di contrasto, per le sole condizioni economiche e tecniche specifiche prevale la presente Offerta. " +
                   $"La presente Offerta, se non sottoscritta per accettazione, è valida {p.ValiditaGiorni} giorni dalla data di emissione.", size: 17));

            var firma = Tabella(false, 4819, 4819);
            firma.Append(RigaTab(false, null, ("Per accettazione – il Committente (timbro e firma)", 4819, false, null), ("MARE.A s.r.l.", 4819, false, null)));
            firma.Append(RigaTab(false, null, ("______________________________", 4819, false, null), ("______________________________", 4819, false, null)));
            A(Para("Cattolica (RN), ____/____/________", size: 18, spazioPrima: 200));
            A(firma);

            A(Para("APPROVAZIONE SPECIFICA (artt. 1341 e 1342 c.c.)", bold: true, size: 18, spazioPrima: 240));
            var rspp = servizi.Any(r => r.IdTipoIncarico == 95);
            A(Para("Il Committente dichiara di approvare specificamente le seguenti clausole delle Condizioni Generali: art. 3.2 (rinnovo tacito e " +
                   "preavviso di disdetta), art. 4.5 (termini indicativi), art. 7.3 (accettazione del Rapporto di intervento in assenza di osservazioni " +
                   "entro 3 giorni lavorativi), art. 10.3 (Portale come servizio di cortesia e onere di scaricare i documenti entro 30 giorni), art. 12.3 " +
                   "(sospensione per ritardato pagamento), art. 13.1 (esclusioni di responsabilità), art. 13.2 (limitazione del risarcimento), art. 13.3 " +
                   "(manleva), art. 13.4 (termine di decadenza per le contestazioni), art. 14.1 (recesso), art. 16.2 (foro esclusivo di Rimini)" +
                   (rspp ? ", Allegato C punto C.3 (cessazione dell'incarico di RSPP esterno)." : "."), size: 17));
            var spec = Tabella(false, 4819, 4819);
            spec.Append(RigaTab(false, null, ("Per approvazione specifica (timbro e firma)", 4819, false, null), ("", 4819, false, null)));
            spec.Append(RigaTab(false, null, ("______________________________", 4819, false, null), ("", 4819, false, null)));
            A(spec);

            A(Para("TRATTAMENTO DEI DATI PERSONALI", bold: true, size: 18, spazioPrima: 240));
            A(Para("L'informativa ai sensi dell'art. 13 del Reg. (UE) 2016/679 è disponibile su www.mare-a.com e nel Portale clienti. " +
                   "I consensi che seguono sono facoltativi e il loro diniego non incide sul contratto (Condizioni Generali, art. 11.2).", size: 17));
            A(Para("Comunicazioni su nuovi servizi e aggiornamenti normativi (posta, e-mail, SMS, app di messaggistica):   [  ] SÌ   [  ] NO", size: 17, spazioPrima: 60));
            A(Para("Profilazione per categoria di attività, per comunicazioni più mirate:   [  ] SÌ   [  ] NO", size: 17));
            A(Para("Firma ______________________________", size: 18, spazioPrima: 120));

            doc.MainDocumentPart.Document.Save();
            foreach (var footerPart in doc.MainDocumentPart.FooterParts)
            {
                footerPart.Footer.Append(ParagrafoNumeroPagina($"Contratto {p.NumeroCompleto} – {rev} – "));
                footerPart.Footer.Save();
            }
        }
        return ms.ToArray();
    }

    /// <summary>
    /// Sezione «Analisi di laboratorio»: per ogni controllo (o gruppo) la tabella di denominazione, metodo e accreditamento
    /// come nell'offerta analisi; prezzo per analisi solo per quelle a pagamento; simboli e frasi dei laboratori in subappalto.
    /// </summary>
    private static void AggiungiAnalisi(Action<DocumentFormat.OpenXml.OpenXmlElement> A, PreventivoConsulenza p)
    {
        A(ConSuccessivo(Titolo("ANALISI DI LABORATORIO")));
        A(Para("Le prove sono eseguite dal Laboratorio MARE.A (accreditato ACCREDIA n. 00729) e sono regolate dalle Condizioni Generali di " +
               "Fornitura del Laboratorio (Mod. Com. 05). Le prove accreditate sono indicate nella colonna «Accr.».", size: 17, spazioDopo: 60));
        var analisi = p.Analisi.OrderBy(a => a.Progressivo).ToList();
        var numeroLab = new Dictionary<int, int>();
        var fraseLab = new List<string>();
        foreach (var m in analisi.SelectMany(a => a.Metodi.OrderBy(x => x.Progressivo)))
        {
            if (m.IdLabEsternoArcLab is not int idl || numeroLab.ContainsKey(idl)) continue;
            numeroLab[idl] = numeroLab.Count + 1;
            fraseLab.Add(TestiOfferta.FraseSubappalto(m.LabEsternoFrase, m.LabEsterno));
        }
        string Simbolo(int n) => numeroLab.Count > 1 ? "§" + n : "§";

        foreach (var a in analisi)
        {
            var rif = AnalisiConsulenza.Riferimento(a.IdControlloArcLab, a.IdControlliOrigine);
            A(ConSuccessivo(Para(a.Descrizione + (rif.Length > 0 ? $" ({rif})" : ""), bold: true, size: 18, spazioPrima: 100, spazioDopo: 40)));
            if (a.Metodi.Count > 0)
            {
                var t = Tabella(2600, 6288, 750);
                t.Append(RigaTab(true, "F3F3F3", ("Denominazione", 2600, false, null), ("Metodo", 6288, false, null), ("Accr.", 750, false, null)));
                foreach (var m in a.Metodi.OrderBy(x => x.Progressivo))
                    t.Append(RigaTab(false, null,
                        (m.Denominazione ?? m.Descrizione ?? "—", 2600, false, null),
                        (m.RiferimentoNormativo ?? "—", 6288, false, 16),
                        (m.IdLabEsternoArcLab is int idl ? Simbolo(numeroLab[idl]) : (m.Accreditato ? "Sì" : "—"), 750, false, null)));
                A(t);
            }
            var q = a.QuantitaAnnua is decimal qa ? qa.ToString("0.##", It) : null;
            string riga;
            if (a.Modalita == ModalitaAnalisiConsulenza.CompresaNelCanone)
                riga = "Compresa nel corrispettivo del servizio" + (q is null ? "." : $": {q} analisi all'anno.");
            else
                riga = $"Prezzo per analisi: € {Fmt(a.Prezzo)} (IVA esclusa), fatturata a esecuzione" +
                       (q is null ? "." : $" – previste {q} analisi all'anno, stima € {Fmt(Math.Round(a.Prezzo * a.QuantitaAnnua!.Value, 2))}.");
            A(Para(riga, bold: true, size: 17, align: JustificationValues.Right, spazioPrima: 40, spazioDopo: 40));
            A(Divisore());
            if (!string.IsNullOrWhiteSpace(a.Note)) A(Para(a.Note!, size: 17, italic: true));
        }
        for (var i = 0; i < fraseLab.Count; i++)
            A(Para($"{Simbolo(i + 1)} {fraseLab[i]}", size: 17));
        if (fraseLab.Count > 0)
            // Stessa nota di Offerta e Ordine di Accettazione (Condizioni del Laboratorio Rev. 19, Artt. 7.2 e 8.2).
            A(Para(TestiOfferta.NotaSubappalto(fraseLab.Count, "Committente"),
                   bold: true, size: 17, shadingFill: "FFF3D6", spazioPrima: 120, spazioDopo: 120));
    }

    private static IEnumerable<(string, decimal)> Prezzi(RigaPreventivoConsulenza r)
    {
        const string iva = " – IVA esclusa";
        var q = r.Quantita.ToString("0.##", It);
        switch (r.ModalitaPrezzo)
        {
            case ModalitaPrezzoConsulenza.Annuale:
                yield return ("Primo anno" + iva, r.Prezzo);
                yield return ("Secondo anno e successivi, all'anno" + iva, r.PrezzoSuccessivi ?? r.Prezzo);
                break;
            case ModalitaPrezzoConsulenza.Oraria:
                yield return ($"Tariffa oraria € {Fmt(r.Prezzo)} × ore stimate {q}{iva} (si fattura il tempo effettivo, rendicontato nel Rapporto di intervento)",
                    Math.Round(r.Prezzo * r.Quantita, 2));
                break;
            case ModalitaPrezzoConsulenza.PacchettoOre:
                yield return ($"Pacchetto di {q} ore" + iva, r.Prezzo);
                break;
            case ModalitaPrezzoConsulenza.PerUnita:
                yield return (r.Quantita == 1m ? $"Prezzo per {r.UnitaMisura ?? "unità"}{iva}" : $"€ {Fmt(r.Prezzo)} per {r.UnitaMisura ?? "unità"} × {q}{iva}",
                    Math.Round(r.Prezzo * r.Quantita, 2));
                break;
            default:
                yield return ((r.Quantita == 1m ? "Prezzo" : $"Prezzo € {Fmt(r.Prezzo)} × {q}") + iva, Math.Round(r.Prezzo * r.Quantita, 2));
                break;
        }
    }

    // ── Impaginazione (29/10/2026): tenere insieme titolo e testo, linee divisorie, salti pagina ──
    // L'ordine degli elementi di w:pPr è fissato dallo schema: keepNext, pageBreakBefore, pBdr, poi shd/spacing/ind/jc
    // (che Para aggiunge): per questo si inseriscono in testa nell'ordine inverso.

    private static ParagraphProperties Proprieta(Paragraph par) =>
        par.ParagraphProperties ?? par.PrependChild(new ParagraphProperties());

    /// <summary>Tiene il paragrafo sulla stessa pagina del successivo.</summary>
    private static Paragraph ConSuccessivo(Paragraph par)
    {
        var pPr = Proprieta(par);
        if (pPr.GetFirstChild<KeepNext>() is null) pPr.InsertAt(new KeepNext(), 0);
        return par;
    }

    /// <summary>Tiene col successivo solo i testi brevi (quelli lunghi possono andare a capo pagina).</summary>
    private static Paragraph Corto(Paragraph par) => par.InnerText.Length <= 600 ? ConSuccessivo(par) : par;

    private static Paragraph NuovaPagina(Paragraph par)
    {
        var pPr = Proprieta(par);
        var keep = pPr.GetFirstChild<KeepNext>();
        var pb = new PageBreakBefore();
        if (keep is not null) pPr.InsertAfter(pb, keep); else pPr.InsertAt(pb, 0);
        return par;
    }

    private static Paragraph BordoSotto(Paragraph par, string colore = "999999")
    {
        var pPr = Proprieta(par);
        var bordo = new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 6, Space = 1, Color = colore });
        var prima = (DocumentFormat.OpenXml.OpenXmlElement?)pPr.GetFirstChild<PageBreakBefore>() ?? pPr.GetFirstChild<KeepNext>();
        if (prima is not null) pPr.InsertAfter(bordo, prima); else pPr.InsertAt(bordo, 0);
        return par;
    }

    /// <summary>Linea divisoria sottile tra le analisi.</summary>
    private static Paragraph Divisore() => BordoSotto(Para("", size: 6, spazioDopo: 60), "DDDDDD");

    private static IEnumerable<string> Righe(string? testo) =>
        (testo ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string? Unisci(params string?[] parti)
    {
        var s = string.Join(" ", parti.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        return s.Length == 0 ? null : s;
    }

    private static string Pulisci(string? s)
    {
        var t = new string((s ?? "cliente").Where(ch => char.IsLetterOrDigit(ch) || ch == ' ').ToArray()).Trim().Replace(' ', '_');
        return t.Length == 0 ? "cliente" : (t.Length > 40 ? t[..40] : t);
    }
}
