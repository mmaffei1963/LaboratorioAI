using System.Globalization;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Modelli;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;

namespace LaboratorioAI.Documenti;

/// <summary>
/// Documenti PDF dell'accettazione, ricostruiti dai dati di staging (nessuna dipendenza
/// dai codici ArcLab, che verranno assegnati solo dopo l'import Palomar):
/// richiesta analisi e catena di custodia per il laboratorio esterno, etichette del
/// campione/lotto e scheda di ricezione (Campione3).
/// </summary>
public interface IGeneratoreDocumentiAccettazione
{
    byte[] RichiestaAnalisi(AccettazioneLotto lotto, AccettazioneCampione campione, IntestazioneMarea m, LabEsternoInfo? lab);
    byte[] CatenaCustodia(AccettazioneLotto lotto, AccettazioneCampione campione, IntestazioneMarea m, LabEsternoInfo? lab, IndirizzoDestinatario? dest);
    byte[] Etichette(AccettazioneLotto lotto, IReadOnlyList<AccettazioneCampione> campioni);
    byte[] SchedaRicezione(AccettazioneLotto lotto, IReadOnlyList<AccettazioneCampione> campioni);
    string NomeFile(string tipo, AccettazioneLotto lotto, int? progressivo);
}

/// <inheritdoc />
public class GeneratoreDocumentiAccettazione : IGeneratoreDocumentiAccettazione
{
    private static readonly CultureInfo It = CultureInfo.GetCultureInfo("it-IT");
    private const string Riga1 = "Laboratorio Analisi inserito nell'Elenco dei Laboratori Accreditati dalla Regione Emilia-Romagna al n. 008/RN/003";
    private const string Riga2 = "Sede Operativa - Via Enrico Toti, 2 - Cattolica (RN) - Partita IVA 03670240401 - Tel. 0541 830442 - Fax 0541 830460 - www.mare-a.com - info@mare-a.com";

    // ── API ──────────────────────────────────────────────────────────
    public byte[] RichiestaAnalisi(AccettazioneLotto l, AccettazioneCampione c, IntestazioneMarea m, LabEsternoInfo? lab)
    {
        var (doc, sec) = NuovoDoc();
        Testata(sec);
        Data(sec);
        Destinatario(sec, "Spettabile:", lab?.Nome, lab?.Indirizzo);
        Oggetto(sec, $"Oggetto: richiesta esecuzione analisi   —   data ricevimento campione: {Data(l.DataConferimento)}   —   Codice accettazione: {Codice(l, c)}");
        Testo(sec, $"La presente per richiederVi l'esecuzione delle seguenti prove sul campione {Codice(l, c)} di {c.Matrice} ({c.Descrizione}):");
        foreach (var p in ProveEsterne(c)) Prova(sec, p);
        Testo(sec, $"La consegna dei risultati dovrà avvenire entro {(c.ConsegnaPrevistaLabEstGiorni?.ToString() ?? "—")} giorni lavorativi, all'indirizzo mail {m.Mail}. In caso non vi sia possibile rispettare i tempi, Vi invitiamo a segnalarci tale difficoltà e le relative motivazioni, prendendo contatto con il nostro laboratorio. Grazie");
        Spazio(sec);
        Testo(sec, "cordiali saluti");
        Firma(sec, m);
        return Rendi(doc);
    }

    public byte[] CatenaCustodia(AccettazioneLotto l, AccettazioneCampione c, IntestazioneMarea m, LabEsternoInfo? lab, IndirizzoDestinatario? dest)
    {
        var (doc, sec) = NuovoDoc();
        Testata(sec);
        Data(sec);
        Destinatario(sec, "Spett.le:", dest?.RagioneSociale ?? l.Cliente, dest?.IndirizzoCompleto);
        Oggetto(sec, "Oggetto: Affidamento Campione a Laboratorio in Subappalto");
        Testo(sec, $"Il campione identificato dal ns. sistema di accettazione con il codice: {Codice(l, c)} è stato prelevato il: {Data(c.DataPrelievo)} alle ore: {c.OraPrelievo ?? "—"}");
        Testo(sec, $"Presso il Vs. insediamento: {dest?.Insediamento ?? l.Insediamento} {dest?.IndirizzoCompleto}");
        Testo(sec, $"punto di prelievo: {c.PuntoPrelievo} — è stato ricevuto presso il ns. laboratorio in data: {Data(l.DataConferimento)} — temp. ricevimento: {c.TempRicezione}");
        Testo(sec, $"note: {c.Note}");
        Testo(sec, $"Il campione {Codice(l, c)} di {c.Matrice} ({c.Descrizione}) è stato affidato al laboratorio {lab?.Nome} localizzato in {lab?.Indirizzo} per l'esecuzione delle seguenti prove:");
        foreach (var p in ProveEsterne(c)) Prova(sec, p);
        Testo(sec, $"Il presente documento accompagna il Rapporto di Prova del laboratorio {lab?.Nome} relativo al campione corrispondente al nostro codice di accettazione {Codice(l, c)} del {Data(l.DataConferimento)} e permette di ricostruire la catena di custodia e l'origine del campione.");
        Firma(sec, m);
        var f = sec.AddParagraph("Documento con firma digitale ai sensi della normativa vigente");
        f.Format.Font.Size = 7; f.Format.Font.Italic = true; f.Format.SpaceBefore = "0.3cm";
        return Rendi(doc);
    }

    public byte[] Etichette(AccettazioneLotto l, IReadOnlyList<AccettazioneCampione> campioni)
    {
        var (doc, sec) = NuovoDoc(compatta: true);
        var tit = sec.AddParagraph($"Etichette campioni — {l.Cliente}");
        tit.Format.Font.Bold = true; tit.Format.Font.Size = 11; tit.Format.SpaceAfter = "0.2cm";
        foreach (var c in campioni.OrderBy(x => x.Progressivo))
        {
            var t = sec.AddTable(); t.Borders.Width = 0.75; t.Borders.Color = Colors.Black;
            t.AddColumn(Unit.FromCentimeter(4.2)); t.AddColumn(Unit.FromCentimeter(12.5));
            void R(string k, string? v) { var r = t.AddRow(); r.Cells[0].AddParagraph(k).Format.Font.Bold = true; r.Cells[0].Format.Font.Size = 8; r.Cells[1].AddParagraph(v ?? "").Format.Font.Size = 9; }
            var cod = t.AddRow(); cod.Cells[0].AddParagraph("CODICE").Format.Font.Bold = true;
            var pc = cod.Cells[1].AddParagraph(Codice(l, c)); pc.Format.Font.Bold = true; pc.Format.Font.Size = 12;
            R("Ricevuto il", Data(l.DataConferimento));
            R("Matrice", c.Matrice);
            R("Denominazione", c.Descrizione);
            R("Commessa", l.Commessa);
            R("Parametri", Parametri(c));
            R("Verbale prelievo", l.VerbalePrelievo);
            R("Imballaggio", c.Imballaggio);
            R("Analizzare entro", DataPiuGiorni(l.DataConferimento, c.RitardoAnalisiGiorni));
            sec.AddParagraph().Format.SpaceAfter = "0.35cm";
        }
        return Rendi(doc);
    }

    public byte[] SchedaRicezione(AccettazioneLotto l, IReadOnlyList<AccettazioneCampione> campioni)
    {
        var (doc, sec) = NuovoDoc(compatta: true);
        bool primo = true;
        foreach (var c in campioni.OrderBy(x => x.Progressivo))
        {
            var tit = sec.AddParagraph("SCHEDA DI RICEZIONE CAMPIONE");
            tit.Format.Font.Bold = true; tit.Format.Font.Size = 13; tit.Format.Alignment = ParagraphAlignment.Center;
            if (!primo) tit.Format.PageBreakBefore = true;
            primo = false;
            var sub = sec.AddParagraph($"Codice accettazione {Codice(l, c)}");
            sub.Format.Alignment = ParagraphAlignment.Center; sub.Format.Font.Size = 9; sub.Format.SpaceAfter = "0.3cm";

            var t = sec.AddTable(); t.Borders.Width = 0.5; t.Borders.Color = Colors.Gray;
            t.AddColumn(Unit.FromCentimeter(4.5)); t.AddColumn(Unit.FromCentimeter(12.2));
            void R(string k, string? v) { var r = t.AddRow(); r.Cells[0].AddParagraph(k).Format.Font.Bold = true; r.Cells[0].Shading.Color = Colors.WhiteSmoke; r.Cells[0].Format.Font.Size = 9; r.Cells[1].AddParagraph(v ?? "").Format.Font.Size = 9; }
            R("Cliente", l.Cliente);
            R("Insediamento", l.Insediamento);
            R("Commessa (controllo)", l.Commessa);
            R("Denominazione", c.Descrizione);
            R("Matrice", c.Matrice);
            R("Etichetta / sigla cliente", string.Join(" / ", new[] { c.EtichettaCliente, c.SiglaCliente }.Where(s => !string.IsNullOrWhiteSpace(s))));
            R("Prelevatore", l.Prelevatore);
            R("Procedura di prelievo", c.ProceduraPrelievo);
            R("Imballaggio", c.Imballaggio);
            R("Data/ora prelievo", $"{Data(c.DataPrelievo)} {c.OraPrelievo}");
            R("Temp. prelievo (°C)", c.Temperatura?.ToString("0.#", It));
            R("Luogo / punto prelievo", string.Join(" — ", new[] { c.LuogoPrelievo, c.PuntoPrelievo }.Where(s => !string.IsNullOrWhiteSpace(s))));
            R("Ricevuto il", Data(l.DataConferimento));
            R("Temp. ricezione", c.TempRicezione);
            R("Termometro", c.Termometro);
            R("Condizioni trasporto", c.CondizioniTrasporto);
            R("Analizzare entro", DataPiuGiorni(l.DataConferimento, c.RitardoAnalisiGiorni));
            R("Note", c.Note);

            sec.AddParagraph().Format.SpaceAfter = "0.2cm";
            var pt = sec.AddParagraph("Prove richieste"); pt.Format.Font.Bold = true; pt.Format.SpaceAfter = "0.1cm";
            var tp = sec.AddTable(); tp.Borders.Width = 0.5; tp.Borders.Color = Colors.Gray;
            tp.AddColumn(Unit.FromCentimeter(6)); tp.AddColumn(Unit.FromCentimeter(6.2)); tp.AddColumn(Unit.FromCentimeter(4.5));
            var h = tp.AddRow(); h.Shading.Color = Colors.WhiteSmoke;
            h.Cells[0].AddParagraph("Parametro").Format.Font.Bold = true;
            h.Cells[1].AddParagraph("Metodo").Format.Font.Bold = true;
            h.Cells[2].AddParagraph("Lab esterno").Format.Font.Bold = true;
            foreach (var p in c.Prove)
            {
                var r = tp.AddRow();
                r.Cells[0].AddParagraph(p.Parametro ?? "");
                r.Cells[1].AddParagraph(p.Metodo ?? "");
                r.Cells[2].AddParagraph(p.LaboratorioEsterno ?? "");
            }

            // Clausola di accettazione + spazio firma cliente
            var cl = sec.AddParagraph(ClausolaCliente);
            cl.Format.Font.Size = 7.5; cl.Format.Font.Italic = true;
            cl.Format.SpaceBefore = "0.5cm"; cl.Format.Alignment = ParagraphAlignment.Justify;
            var firma = sec.AddParagraph("Il Cliente ______________________________________");
            firma.Format.SpaceBefore = "1.2cm";
        }
        return Rendi(doc);
    }

    private const string ClausolaCliente =
        "IL CLIENTE RICHIEDE LA DETERMINAZIONE DEI PARAMETRI SOPRA ELENCATI OVVERO; IL PREZZO, SE NON ALTRIMENTI STABILITO " +
        "(ABBONAMENTO, PREVENTIVO, ECC.) È QUELLO STABILITO NEL LISTINO MARE.A srl, RICHIEDIBILE IN VISIONE ALLA CONSEGNA DEL " +
        "CAMPIONE. IL CLIENTE SI IMPEGNA A PAGARE IL COSTO DELL'ANALISI ALLA CONSEGNA DEL CAMPIONE, SE NON ALTRIMENTI CONCORDATO.";

    public string NomeFile(string tipo, AccettazioneLotto l, int? prog)
    {
        var t = tipo switch
        {
            "richiesta" => "Richiesta_analisi", "catena" => "Catena_custodia",
            "etichetta" => "Etichetta", "etichette" => "Etichette_lotto",
            "scheda" => "Scheda_ricezione", "schede" => "Schede_ricezione_lotto", _ => "Documento"
        };
        var suffisso = prog.HasValue ? $"_{l.IdAccettazioneLotto}-{prog}" : $"_{l.IdAccettazioneLotto}";
        return $"{t}{suffisso}.pdf";
    }

    // ── Helper di impaginazione ──────────────────────────────────────
    private static (Document, Section) NuovoDoc(bool compatta = false)
    {
        var doc = new Document();
        var n = doc.Styles["Normal"]!; n.Font.Name = "Arial"; n.Font.Size = compatta ? 9 : 10;
        var sec = doc.AddSection();
        sec.PageSetup.PageFormat = PageFormat.A4;
        sec.PageSetup.TopMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.BottomMargin = Unit.FromCentimeter(1.2);
        sec.PageSetup.LeftMargin = Unit.FromCentimeter(2);
        sec.PageSetup.RightMargin = Unit.FromCentimeter(2);
        return (doc, sec);
    }

    private static void Testata(Section sec)
    {
        var r1 = sec.AddParagraph(Riga1); r1.Format.Font.Size = 7.5; r1.Format.Font.Bold = true;
        var r2 = sec.AddParagraph(Riga2); r2.Format.Font.Size = 7; r2.Format.SpaceAfter = "0.5cm";
        var line = sec.AddParagraph(); line.Format.Borders.Bottom.Width = 0.5; line.Format.SpaceAfter = "0.3cm";
    }

    private static void Data(Section sec)
    {
        var p = sec.AddParagraph($"Cattolica, {DateTime.Now.ToString("d MMMM yyyy", It)}");
        p.Format.SpaceBefore = "0.3cm"; p.Format.SpaceAfter = "0.3cm";
    }

    private static void Destinatario(Section sec, string etichetta, string? nome, string? indirizzo)
    {
        var p = sec.AddParagraph(); p.AddFormattedText(etichetta + " ", TextFormat.Bold);
        p.AddFormattedText(nome ?? "", TextFormat.Bold);
        if (!string.IsNullOrWhiteSpace(indirizzo)) sec.AddParagraph(indirizzo);
        sec.AddParagraph().Format.SpaceAfter = "0.2cm";
    }

    private static void Oggetto(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo); p.Format.Font.Bold = true; p.Format.SpaceAfter = "0.3cm";
    }

    private static void Testo(Section sec, string testo)
    {
        var p = sec.AddParagraph(testo); p.Format.SpaceAfter = "0.2cm"; p.Format.Alignment = ParagraphAlignment.Justify;
    }

    private static void Prova(Section sec, AccettazioneProva p)
    {
        var par = sec.AddParagraph();
        par.Format.LeftIndent = "0.6cm";
        par.AddFormattedText("parametro: ", TextFormat.Bold);
        par.AddText(string.Join(" ", new[] { p.Parametro, p.Metodo }.Where(s => !string.IsNullOrWhiteSpace(s))));
    }

    private static void Firma(Section sec, IntestazioneMarea m)
    {
        var p = sec.AddParagraph("il Direttore del Laboratorio:"); p.Format.SpaceBefore = "0.5cm";
        var d = sec.AddParagraph(m.Direttore ?? ""); d.Format.Font.Bold = true;
    }

    private static void Spazio(Section sec) => sec.AddParagraph().Format.SpaceAfter = "0.15cm";

    private static IEnumerable<AccettazioneProva> ProveEsterne(AccettazioneCampione c)
    {
        var est = c.Prove.Where(p => !string.IsNullOrWhiteSpace(p.LaboratorioEsterno)).ToList();
        return est.Count > 0 ? est : c.Prove;
    }

    private static string Parametri(AccettazioneCampione c) =>
        string.Join(", ", c.Prove.Select(p => p.Parametro).Where(s => !string.IsNullOrWhiteSpace(s)));
    private static string Codice(AccettazioneLotto l, AccettazioneCampione c)
    {
        // Il CodLotto definitivo lo assegna Palomar all'import; finché non c'è usiamo il
        // numero di verbale interno, altrimenti un riferimento provvisorio.
        var rif = !string.IsNullOrWhiteSpace(l.CodLotto) ? l.CodLotto
                : !string.IsNullOrWhiteSpace(l.VerbalePrelievo) ? l.VerbalePrelievo
                : $"PROVV-{l.IdAccettazioneLotto}";
        return $"{rif}/{c.Progressivo}";
    }
    private static string Data(DateTime? d) => d?.ToString("dd/MM/yy", It) ?? "—";
    private static string DataPiuGiorni(DateTime d, int? gg) => d.AddDays(gg ?? 0).ToString("dd/MM/yy", It);

    private static byte[] Rendi(Document doc)
    {
        var r = new PdfDocumentRenderer { Document = doc };
        r.RenderDocument();
        using var ms = new MemoryStream();
        r.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }
}
