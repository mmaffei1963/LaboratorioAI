using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using LaboratorioAI.Core.Modelli;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaboratorioAI.Documenti.Dvr;

/// <summary>Produzione del documento di valutazione del rischio legionellosi.</summary>
public interface IGeneratoreDvr
{
    Task<RisultatoGenerazioneDvr> GeneraAsync(SchedaDvr scheda, CancellationToken ct = default);

    /// <summary>
    /// Elenca i controlli presenti in un modello.
    /// Serve a verificare la corrispondenza fra modello e scheda quando
    /// il modello viene aggiornato.
    /// </summary>
    Task<IReadOnlyList<string>> IspezionaModelloAsync(
        RegioneDvr regione, CancellationToken ct = default);
}

/// <inheritdoc />
public class GeneratoreDvr : IGeneratoreDvr
{
    private readonly OpzioniDvr _opzioni;
    private readonly ILogger<GeneratoreDvr> _log;

    public GeneratoreDvr(IOptions<OpzioniDvr> opzioni, ILogger<GeneratoreDvr> log)
    {
        _opzioni = opzioni.Value;
        _log = log;
    }

    // ═══════════════════════════════════════════════════════════════
    // GENERAZIONE
    // ═══════════════════════════════════════════════════════════════

    public async Task<RisultatoGenerazioneDvr> GeneraAsync(
        SchedaDvr scheda, CancellationToken ct = default)
    {
        var modello = _opzioni.ModelloPer(scheda.Regione);

        if (!File.Exists(modello))
            return RisultatoGenerazioneDvr.Errore($"Modello non trovato: {modello}");

        var nomeFile = ComponiNomeFile(scheda);
        var destinazione = Path.Combine(_opzioni.CartellaArchivio, nomeFile);

        Directory.CreateDirectory(_opzioni.CartellaArchivio);

        try
        {
            // Si lavora su una copia: il modello non va mai modificato
            File.Copy(modello, destinazione, overwrite: true);

            var nonPopolati = await Task.Run(() => Compila(destinazione, scheda), ct);

            _log.LogInformation(
                "DVR prodotto per {Cliente}: {File}. Campi non popolati: {Numero}",
                scheda.RagioneSociale, nomeFile, nonPopolati.Count);

            return new RisultatoGenerazioneDvr
            {
                Riuscito = true,
                NomeFile = nomeFile,
                PercorsoCompleto = destinazione,
                CampiNonPopolati = nonPopolati
            };
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Generazione DVR non riuscita per {Cliente}", scheda.RagioneSociale);

            // Il file parziale non deve restare in archivio
            if (File.Exists(destinazione))
            {
                try { File.Delete(destinazione); } catch { /* ignorato */ }
            }

            return RisultatoGenerazioneDvr.Errore(ex.Message);
        }
    }

    /// <summary>Compila il documento e restituisce i controlli rimasti senza valore.</summary>
    private List<string> Compila(string percorso, SchedaDvr scheda)
    {
        var valori = MappaValori.Componi(scheda);
        var nonPopolati = new List<string>();

        using var documento = WordprocessingDocument.Open(percorso, isEditable: true);

        // 1. Sezioni condizionate, prima della compilazione: i controlli
        //    contenuti in sezioni rimosse non vanno nemmeno cercati
        RifinituraDocumento.ApplicaSezioni(documento, scheda.SezioniAttive);

        // 2. Compilazione dei controlli rimasti
        var controlli = ControlliContenuto.Tutti(documento).ToList();

        foreach (var sdt in controlli)
        {
            var identificativo = ControlliContenuto.Identificativo(sdt);
            if (identificativo is null) continue;

            if (valori.TryGetValue(identificativo, out var valore))
            {
                ControlliContenuto.Scrivi(sdt, valore);
            }
            else
            {
                nonPopolati.Add(identificativo);
            }
        }

        // 3. Tabella dei punti di prelievo, se il modello la prevede
        CompilaPuntiPrelievo(documento, scheda);

        // 4. Rifinitura
        if (_opzioni.SciogliControlli)
        {
            foreach (var sdt in ControlliContenuto.Tutti(documento).ToList())
                ControlliContenuto.Sciogli(sdt);
        }

        RifinituraDocumento.RimuoviPagineVuote(documento);
        RifinituraDocumento.RichiediAggiornamentoIndice(documento);

        RifinituraDocumento.ImpostaProprieta(
            documento,
            $"DVR Legionellosi — {scheda.RagioneSociale}",
            "Documento di valutazione del rischio legionellosi");

        documento.MainDocumentPart?.Document.Save();

        return nonPopolati;
    }

    /// <summary>
    /// Popola la tabella del piano di campionamento.
    ///
    /// La tabella è individuata dal tag <c>tab_punti_prelievo</c>: la prima
    /// riga dopo l'intestazione fa da modello e viene replicata per ogni
    /// punto, conservando la formattazione.
    /// </summary>
    private static void CompilaPuntiPrelievo(WordprocessingDocument documento, SchedaDvr scheda)
    {
        if (scheda.PuntiPrelievo.Count == 0) return;

        var corpo = documento.MainDocumentPart?.Document?.Body;
        if (corpo is null) return;

        var tabella = corpo.Descendants<Table>().FirstOrDefault(t =>
            t.Descendants<Text>().Any(x =>
                NormalizzazioneTesto.Chiave(x.Text) == "tab_punti_prelievo"));

        if (tabella is null) return;

        var righe = tabella.Elements<TableRow>().ToList();
        if (righe.Count < 2) return;

        var modelloRiga = righe[1];

        foreach (var punto in scheda.PuntiPrelievo)
        {
            var nuova = (TableRow)modelloRiga.CloneNode(true);
            var celle = nuova.Elements<TableCell>().ToList();

            ScriviCella(celle, 0, punto.Descrizione);
            ScriviCella(celle, 1, punto.Ubicazione);
            ScriviCella(celle, 2, punto.Tipologia);

            tabella.AppendChild(nuova);
        }

        // La riga usata come modello contiene i segnaposto
        modelloRiga.Remove();

        // Rimuove il segnaposto del tag dalla cella che lo conteneva
        foreach (var testo in tabella.Descendants<Text>()
                     .Where(t => NormalizzazioneTesto.Chiave(t.Text) == "tab_punti_prelievo"))
        {
            testo.Text = "";
        }
    }

    private static void ScriviCella(List<TableCell> celle, int indice, string? valore)
    {
        if (indice >= celle.Count) return;

        var paragrafo = celle[indice].Elements<Paragraph>().FirstOrDefault();
        if (paragrafo is null) return;

        var esecuzioni = paragrafo.Elements<Run>().ToList();

        if (esecuzioni.Count == 0)
        {
            paragrafo.AppendChild(new Run(new Text(valore ?? "")));
            return;
        }

        foreach (var testo in esecuzioni[0].Elements<Text>().ToList()) testo.Remove();
        esecuzioni[0].AppendChild(new Text(valore ?? ""));

        foreach (var eccedente in esecuzioni.Skip(1)) eccedente.Remove();
    }

    // ═══════════════════════════════════════════════════════════════
    // ISPEZIONE
    // ═══════════════════════════════════════════════════════════════

    public Task<IReadOnlyList<string>> IspezionaModelloAsync(
        RegioneDvr regione, CancellationToken ct = default)
    {
        var modello = _opzioni.ModelloPer(regione);

        if (!File.Exists(modello))
            throw new FileNotFoundException($"Modello non trovato: {modello}");

        return Task.Run<IReadOnlyList<string>>(() =>
        {
            using var documento = WordprocessingDocument.Open(modello, isEditable: false);

            return ControlliContenuto.Tutti(documento)
                .Select(sdt => new
                {
                    Identificativo = ControlliContenuto.Identificativo(sdt),
                    Tag = ControlliContenuto.Tag(sdt),
                    Etichetta = ControlliContenuto.Etichetta(sdt),
                    Opzioni = ControlliContenuto.Opzioni(sdt).Count
                })
                .Where(x => x.Identificativo is not null)
                .Select(x => x.Opzioni > 0
                    ? $"{x.Identificativo}  (elenco di {x.Opzioni} voci — tag: {x.Tag ?? "assente"}, etichetta: {x.Etichetta ?? "assente"})"
                    : $"{x.Identificativo}  (tag: {x.Tag ?? "assente"}, etichetta: {x.Etichetta ?? "assente"})")
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }, ct);
    }

    // ═══════════════════════════════════════════════════════════════
    // NOME FILE
    // ═══════════════════════════════════════════════════════════════

    private static string ComponiNomeFile(SchedaDvr scheda)
    {
        var parti = new[] { scheda.RagioneSociale, scheda.NomeInsediamento }
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(Ripulisci);

        var nome = string.Join("_", parti);
        if (nome.Length > 90) nome = nome[..90];

        return $"DVR_Legionellosi_{nome}_{DateTime.Now:yyyyMMdd}.docx";
    }

    /// <summary>Rende una stringa utilizzabile come nome di file.</summary>
    private static string Ripulisci(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return "";

        var proibiti = Path.GetInvalidFileNameChars();
        var pulito = new string(NormalizzazioneTesto.Normalizza(valore)
            .Select(c => proibiti.Contains(c) ? '_' : c)
            .ToArray());

        return pulito.Replace(' ', '_').Replace("__", "_").Trim('_');
    }
}
