using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Documenti.Dvr;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// Una tendina trovata in un modello.
/// </summary>
/// <param name="Tag">Il tag senza prefisso: ARCHIVIO_INFORMATICO_LUOGO_DI_CONSERVAZIONE_1.</param>
/// <param name="Occorrenza">Quante volte quel tag e' gia' comparso nello stesso blocco: 1, 2…</param>
/// <param name="Codice">Identita' del campo: «TAG» alla prima occorrenza, «TAG#n» dalla seconda.</param>
/// <param name="Blocco">Il blocco ripetuto che lo contiene (RIPETI:ARCHIVIO_INFORMATICO), oppure null.</param>
/// <param name="Etichetta">Come il campo si chiama a schermo: la prima cella della riga, o l'etichetta del controllo.</param>
/// <param name="Chiusa">Elenco a discesa (si sceglie e basta) invece di casella combinata.</param>
/// <param name="Opzioni">Le voci, nell'ordine del modello.</param>
public sealed record TendinaTrovata(
    string Tag,
    int Occorrenza,
    string Codice,
    string? Blocco,
    string? Etichetta,
    bool Chiusa,
    IReadOnlyList<string> Opzioni);

/// <summary>
/// Legge le tendine da un modello Word (05/11/2026).
///
/// Le voci delle tendine stanno dentro i controlli contenuto dei modelli
/// approvati, ed e' da li' che si costruisce il dizionario della check
/// list: cosi' non si digita a mano e non puo' divergere dai documenti
/// che genera.
///
/// **Come si identifica un campo** dipende da come lo riempie il motore,
/// e il motore ha due modi.
///
/// Dentro i blocchi delle lettere e degli archivi riempie **per
/// posizione** (<c>fillPos</c>, chiave «ETICHETTA#n»): li' lo stesso tag
/// ripetuto e' un campo diverso a ogni occorrenza. Nella scheda
/// dell'archivio informatico «LUOGO DI CONSERVAZIONE» compare due volte,
/// una per i dati sul PC — dove fra le voci c'e' «Cloud» — e una per
/// quelli del gestionale, dove c'e' «Server farm del gestionale».
/// Unirle vorrebbe dire proporre il cloud dove va la server farm.
///
/// Dappertutto altrove riempie **per tag** (<c>fill</c>), e tutte le
/// occorrenze dello stesso tag sono lo stesso campo: prendono lo stesso
/// valore. Vale anche dentro le sedi del trattamento e dentro gli
/// elenchi delle misure di sicurezza, dove le righe del modello sono un
/// prototipo che il motore duplica, non domande distinte.
///
/// Per questo i blocchi a posizione sono elencati qui sotto per nome:
/// sono quelli, e si sanno solo leggendo il motore. Se il motore ne
/// aggiunge uno, va aggiunto anche qui — come per il vocabolario dei
/// marcatori.
/// </summary>
public static class TendineModello
{
    private const string Prefisso = "CAMPO:";

    /// <summary>
    /// I blocchi che il motore riempie per posizione (<c>fillPos</c>,
    /// motore.js: lettere alle righe 665 e archivi alla 703). Tutti gli
    /// altri blocchi — <c>RIPETI:SEDI_TRATTAMENTO</c> compreso, che usa
    /// <c>fill</c> alla riga 417 — riempiono per tag.
    /// </summary>
    private static readonly IReadOnlySet<string> BlocchiPerPosizione =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "RIPETI:LETTERE_RESPONSABILE",
            "RIPETI:ARCHIVIO_CARTACEO",
            "RIPETI:ARCHIVIO_INFORMATICO"
        };

    /// <summary>«Dato: archivio informatico – luogo di conservazione (col. 1)» → «archivio informatico – luogo di conservazione».</summary>
    private static readonly Regex Colonna = new(@"\s*\((?:col\.|colonna)\s*\d+\)\s*$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Tutte le tendine del modello. Un controllo senza voci non compare:
    /// li' il consulente scrive e basta.
    /// </summary>
    public static IReadOnlyList<TendinaTrovata> Leggi(byte[] contenutoDocx)
    {
        var trovate = new List<TendinaTrovata>();
        var conteggi = new Dictionary<(object Blocco, string Tag), int>();

        using var flusso = new MemoryStream(contenutoDocx, writable: false);
        using var documento = WordprocessingDocument.Open(flusso, isEditable: false);

        foreach (var sdt in ControlliContenuto.Tutti(documento))
        {
            var tag = ControlliContenuto.Tag(sdt);
            if (string.IsNullOrEmpty(tag) || !tag.StartsWith(Prefisso, StringComparison.Ordinal)) continue;

            var opzioni = Opzioni(sdt);
            if (opzioni.Count == 0) continue;

            var nome = tag[Prefisso.Length..];
            var contenitore = BloccoPerPosizione(sdt);

            // Fuori dai blocchi a posizione il motore riempie per tag:
            // tutte le occorrenze sono lo stesso campo.
            var occorrenza = 1;
            if (contenitore is not null)
            {
                var chiave = ((object)contenitore, nome);
                conteggi.TryGetValue(chiave, out occorrenza);
                conteggi[chiave] = ++occorrenza;
            }

            trovate.Add(new TendinaTrovata(
                nome,
                occorrenza,
                occorrenza == 1 ? nome : $"{nome}#{occorrenza}",
                contenitore is null ? null : ControlliContenuto.Tag(contenitore),
                Etichetta(sdt),
                sdt.SdtProperties?.GetFirstChild<SdtContentDropDownList>() is not null,
                opzioni));
        }

        return trovate;
    }

    /// <summary>Il blocco a posizione piu' vicino che contiene il controllo, se c'e'.</summary>
    private static SdtElement? BloccoPerPosizione(SdtElement sdt) =>
        sdt.Ancestors<SdtElement>()
            .FirstOrDefault(a => ControlliContenuto.Tag(a) is { } t && BlocchiPerPosizione.Contains(t));

    /// <summary>
    /// Le voci della tendina. Come il motore: prima il testo mostrato,
    /// poi il valore. Leggere solo il valore farebbe sparire in silenzio
    /// le voci di un modello scritto diversamente.
    /// </summary>
    private static IReadOnlyList<string> Opzioni(SdtElement sdt)
    {
        var proprieta = sdt.SdtProperties;
        if (proprieta is null) return [];

        var voci =
            (IEnumerable<ListItem>?)proprieta.GetFirstChild<SdtContentDropDownList>()?.Elements<ListItem>()
            ?? proprieta.GetFirstChild<SdtContentComboBox>()?.Elements<ListItem>();

        if (voci is null) return [];

        return voci
            .Select(v => (v.DisplayText?.Value ?? v.Value?.Value ?? "").Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Come il campo si chiama a schermo. Il motore usa il testo della
    /// prima cella della riga — «LUOGO DI CONSERVAZIONE» — perche' e'
    /// quello che il consulente legge nel documento; l'etichetta del
    /// controllo e' il ripiego.
    /// </summary>
    private static string? Etichetta(SdtElement sdt)
    {
        var riga = sdt.Ancestors<TableRow>().FirstOrDefault();
        if (riga is not null)
        {
            var prima = riga.Elements<TableCell>().FirstOrDefault();
            if (prima is not null && !prima.Descendants<SdtElement>().Contains(sdt))
            {
                var testo = string.Concat(prima.Descendants<Text>().Select(t => t.Text)).Trim();
                if (testo.Length > 0) return testo;
            }
        }

        var etichetta = ControlliContenuto.Etichetta(sdt);
        if (string.IsNullOrWhiteSpace(etichetta)) return null;

        etichetta = Colonna.Replace(etichetta.Trim(), "");
        if (etichetta.StartsWith("Dato:", StringComparison.OrdinalIgnoreCase))
            etichetta = etichetta[5..].TrimStart();

        return etichetta.Length == 0 ? null : etichetta;
    }
}
