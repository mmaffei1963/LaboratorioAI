using System.Globalization;

using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Documenti.Dvr;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>Com'e' andata la generazione di un documento.</summary>
/// <param name="Codice">Il modello generato.</param>
/// <param name="NomeFile">Nome proposto per il file.</param>
/// <param name="Contenuto">Il .docx, o null se non si e' generato.</param>
/// <param name="Avvisi">Cose da rileggere prima della firma.</param>
/// <param name="Errore">Perche' non si e' generato.</param>
public sealed record EsitoGenerazione(
    string Codice, string NomeFile, byte[]? Contenuto,
    IReadOnlyList<string> Avvisi, string? Errore);

/// <summary>
/// Genera un documento dal suo modello approvato (11/11/2026).
///
/// Tre passaggi, nell'ordine in cui li fa il motore e per lo stesso
/// motivo:
///
/// 1. **I blocchi condizionati.** Si tolgono per primi: un campo dentro
///    un blocco che sparisce non va compilato, e compilarlo prima
///    sarebbe lavoro buttato — ma soprattutto, togliere il blocco dopo
///    cancellerebbe un valore gia' scritto senza che si veda.
/// 2. **I campi.** Si scrive nel controllo conservando la formattazione
///    del modello, non sostituendo il contenuto.
/// 3. **Si sciolgono i controlli.** Nel documento consegnato non devono
///    restare: si vedrebbero i bordi grigi e si potrebbero modificare.
///
/// Dei blocchi ripetuti sa fare le lettere di nomina a responsabile
/// esterno (<c>RIPETI:LETTERE_RESPONSABILE</c>, quattordici modelli) e
/// le sedi del trattamento (<c>RIPETI:SEDI_TRATTAMENTO</c>, sette).
///
/// Dei testi alternativi sa i cinque delle due informative.
///
/// Quello che questa versione **non** fa: gli altri testi alternativi,
/// gli elenchi (<c>LISTA:</c>), gli altri blocchi
/// ripetuti, le sezioni, le firme e i trattamenti speciali. Un modello
/// che li usa viene rifiutato, invece di uscire incompleto senza dirlo.
/// </summary>
public static class GeneratoreManualistica
{
    private const string Campo = "CAMPO:";
    private const string Se = "SE:";

    /// <summary>I prefissi che questa versione sa trattare.</summary>
    private static readonly IReadOnlySet<string> Trattati =
        new HashSet<string>(StringComparer.Ordinal) { "CAMPO", "SE", "FIRMA" };

    /// <summary>
    /// I marcatori interi che si sanno trattare pur avendo un prefisso
    /// non ancora coperto. Si elencano uno per uno, non per prefisso:
    /// «RIPETI:» comprende anche blocchi che non sappiamo fare, e
    /// lasciarli passare darebbe documenti incompleti senza dirlo.
    /// </summary>
    private static readonly IReadOnlySet<string> TrattatiPerNome =
        new HashSet<string>(StringComparer.Ordinal) {
            BlocchiRipetuti.Lettere, BlocchiRipetuti.Manutentori,
            BlocchiRipetuti.Sedi, BlocchiRipetuti.Autorizzati
        };

    /// <summary>
    /// Genera un documento.
    /// </summary>
    /// <param name="codiceModello">Il modello, per il riepilogo.</param>
    /// <param name="nomeFile">Nome proposto per il file prodotto.</param>
    /// <param name="modelloDocx">La revisione approvata del modello.</param>
    /// <param name="contesto">Condizioni e valori della compilazione.</param>
    /// <param name="firmaLegaleRappresentante">PNG, se raccolta.</param>
    /// <param name="firmaConsulente">PNG, se raccolta.</param>
    public static EsitoGenerazione Genera(
        string codiceModello, string nomeFile, byte[] modelloDocx, ContestoGenerazione contesto,
        byte[]? firmaLegaleRappresentante = null, byte[]? firmaConsulente = null)
    {
        var avvisi = new List<string>();

        try
        {
            // Si lavora su una copia: il modello approvato non si tocca mai.
            using var flusso = new MemoryStream();
            flusso.Write(modelloDocx, 0, modelloDocx.Length);
            flusso.Position = 0;

            using (var documento = WordprocessingDocument.Open(flusso, isEditable: true))
            {
                var nonTrattati = ControlliContenuto.Tutti(documento)
                    .Select(ControlliContenuto.Tag)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Where(t => !SaTrattare(t!))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                if (nonTrattati.Count > 0)
                    return new EsitoGenerazione(codiceModello, nomeFile, null, avvisi,
                        "Questo modello usa marcatori che il generatore non sa ancora trattare: "
                        + string.Join(", ", nonTrattati.Take(8)) + (nonTrattati.Count > 8 ? ", …" : "") + ".");

                // Prima i blocchi ripetuti: aprono pezzi di documento che
                // i due passaggi dopo devono poter vedere.
                BlocchiRipetuti.ApriLettere(
                    documento, BlocchiRipetuti.Lettere, Copie(codiceModello, contesto), contesto);

                // I manutentori interni: un foglio per persona, quante
                // dice la check list.
                BlocchiRipetuti.ApriLettere(
                    documento, BlocchiRipetuti.Manutentori, Quante(contesto, "MANUTENTORI"), contesto,
                    nuovaPagina: true);
                BlocchiRipetuti.ApriSedi(documento, contesto);
                BlocchiRipetuti.ApriAutorizzati(documento, contesto);

                Condizioni(documento, contesto, avvisi);
                TestiAlternativi.Applica(documento, contesto, avvisi);
                Campi(documento, contesto, avvisi);

                // Le firme per ultime: vanno in un riquadro che i
                // passaggi prima potrebbero aver tolto.
                FirmeDocumento.Applica(documento, firmaLegaleRappresentante, firmaConsulente);
                AvvisaFirmeMancanti(documento, firmaLegaleRappresentante, firmaConsulente, avvisi);

                Sciogli(documento);

                documento.MainDocumentPart!.Document.Save();
            }

            return new EsitoGenerazione(codiceModello, nomeFile, flusso.ToArray(), avvisi, null);
        }
        catch (Exception ex)
        {
            return new EsitoGenerazione(codiceModello, nomeFile, null, avvisi,
                $"Il documento non si e' generato: {ex.Message}");
        }
    }

    /// <summary>
    /// Dice quali firme il documento chiedeva e non ha avuto: un
    /// documento che esce senza la firma che prevedeva non deve
    /// sembrare completo.
    /// </summary>
    private static void AvvisaFirmeMancanti(
        WordprocessingDocument documento, byte[]? lr, byte[]? consulente, List<string> avvisi)
    {
        var chieste = ControlliContenuto.Tutti(documento)
            .Select(ControlliContenuto.Tag)
            .Where(t => t is not null && t.StartsWith("FIRMA:", StringComparison.Ordinal))
            .Select(t => t!["FIRMA:".Length..])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var chi in chieste)
        {
            var perConsulente = string.Equals(chi, "CONSULENTE", StringComparison.Ordinal);
            var immagine = perConsulente ? consulente : lr;
            if (immagine is not null && immagine.Length > 0) continue;

            avvisi.Add(perConsulente
                ? "La firma del consulente non c'e': il riquadro e' stato lasciato vuoto."
                : "La firma del legale rappresentante non c'e': il documento va firmato a mano.");
        }
    }

    /// <summary>
    /// Vero se il marcatore si sa trattare. Si guarda il marcatore
    /// intero e non il solo prefisso: di <c>RIPETI:</c> e <c>TESTO:</c>
    /// se ne sa fare una parte, e lasciar passare il resto darebbe
    /// documenti incompleti senza dirlo.
    /// </summary>
    private static bool SaTrattare(string marcatore)
    {
        if (TrattatiPerNome.Contains(marcatore)) return true;

        var prefisso = marcatore.Split(':')[0];
        if (Trattati.Contains(prefisso)) return true;

        return prefisso == "TESTO" && TestiAlternativi.Conosce(marcatore["TESTO:".Length..]);
    }

    /// <summary>
    /// Quante volte si ripete la lettera.
    ///
    /// Una sola, tranne l'impegno alla riservatezza della ditta esterna
    /// che fa i servizi ai piani: li' se ne stampa una per persona, e
    /// quante siano lo dice la check list.
    /// </summary>
    private static int Copie(string codiceModello, ContestoGenerazione contesto) =>
        codiceModello == "IMPEGNO_DITTA_PIANI" ? Quante(contesto, "IMPEGNO_DITTA_PIANI") : 1;

    /// <summary>Il numero scritto in una risposta della check list, almeno uno.</summary>
    private static int Quante(ContestoGenerazione contesto, string codice)
    {
        var risposta = contesto.Campo(codice, out _);
        return int.TryParse(risposta, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n : 1;
    }

    /// <summary>
    /// Toglie i blocchi la cui condizione e' falsa.
    ///
    /// Si parte dai piu' esterni: togliendo un blocco se ne va anche
    /// quello che contiene, e ripassarci sopra lavorerebbe su elementi
    /// non piu' nel documento.
    /// </summary>
    private static void Condizioni(WordprocessingDocument documento, ContestoGenerazione contesto, List<string> avvisi)
    {
        var daTogliere = new List<SdtElement>();

        foreach (var sdt in ControlliContenuto.Tutti(documento).ToList())
        {
            var tag = ControlliContenuto.Tag(sdt);
            if (tag is null || !tag.StartsWith(Se, StringComparison.Ordinal)) continue;

            var nome = tag[Se.Length..];
            var vale = contesto.Vale(nome, out var conosciuta);

            if (!conosciuta)
            {
                // Come il motore: nel dubbio il blocco resta. Ma qui lo
                // si dice, perche' un avviso che nessuno legge e' peggio
                // di niente.
                avvisi.Add($"Condizione sconosciuta «{nome}»: il blocco e' stato tenuto, va riletto.");
                continue;
            }

            if (!vale) daTogliere.Add(sdt);
        }

        foreach (var sdt in daTogliere)
        {
            // Saltato se un blocco esterno lo ha gia' portato via.
            if (sdt.Parent is not null) sdt.Remove();
        }
    }

    /// <summary>Scrive i valori nei campi rimasti.</summary>
    private static void Campi(WordprocessingDocument documento, ContestoGenerazione contesto, List<string> avvisi)
    {
        var senzaValore = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sdt in ControlliContenuto.Tutti(documento).ToList())
        {
            var tag = ControlliContenuto.Tag(sdt);
            if (tag is null || !tag.StartsWith(Campo, StringComparison.Ordinal)) continue;

            var codice = tag[Campo.Length..];
            var valore = contesto.Campo(codice, out var conosciuto);

            if (!conosciuto) senzaValore.Add(codice);

            ControlliContenuto.Scrivi(sdt, valore ?? "");
        }

        foreach (var codice in senzaValore.OrderBy(x => x, StringComparer.Ordinal))
            avvisi.Add($"Il campo «{codice}» e' rimasto vuoto: nella check list non c'e' una risposta per lui.");
    }

    /// <summary>
    /// Toglie i controlli contenuto lasciando quello che contengono.
    /// Nel documento consegnato non devono restare.
    /// </summary>
    private static void Sciogli(WordprocessingDocument documento)
    {
        foreach (var sdt in ControlliContenuto.Tutti(documento).ToList())
        {
            if (sdt.Parent is null) continue;
            if (ControlliContenuto.Tag(sdt) is null) continue;
            ControlliContenuto.Sciogli(sdt);
        }
    }

    /// <summary>«Cartello videosorveglianza - Hotel Graziana - 11-11-2026.docx».</summary>
    public static string NomeFile(string nomeModello, Compilazione compilazione)
    {
        var pezzi = new[]
        {
            nomeModello,
            compilazione.RagioneSociale,
            compilazione.DataDocumenti.ToString("dd-MM-yyyy")
        };

        var nome = string.Join(" - ", pezzi.Where(p => !string.IsNullOrWhiteSpace(p)));
        foreach (var vietato in Path.GetInvalidFileNameChars()) nome = nome.Replace(vietato, ' ');

        return nome.Trim() + ".docx";
    }
}
