using System.Net;
using System.Text.RegularExpressions;

namespace LaboratorioAI.Dati.Accredia;

/// <summary>
/// Laboratori (numero + sede) letti dalla banca dati ACCREDIA: tutti gli accreditati e quelli con sospensione attiva.
/// <see cref="ProveSospese"/>: per i laboratori sospesi richiesti, le prove con asterisco (assente = dettaglio non letto).
/// </summary>
public sealed record StatiAccredia(
    IReadOnlySet<(string Numero, string Sede)> Accreditati,
    IReadOnlySet<(string Numero, string Sede)> Sospesi,
    IReadOnlyDictionary<(string Numero, string Sede), IReadOnlyList<ProvaSospesaLetta>>? ProveSospese = null)
{
    /// <summary>
    /// Prove sospese da inviare per un laboratorio: lista vuota se accreditato senza sospensioni, le righe lette se
    /// sospeso, null se sospeso ma il dettaglio non è stato letto o se non trovato (le prove salvate non cambiano).
    /// </summary>
    public IReadOnlyList<ProvaSospesaLetta>? Prove(string numero, string sede)
    {
        var chiave = (numero.Trim().PadLeft(5, '0'), sede.Trim().ToUpperInvariant());
        return Stato(numero, sede) switch
        {
            "Accreditato" => Array.Empty<ProvaSospesaLetta>(),
            "Sospeso" when ProveSospese is not null && ProveSospese.TryGetValue(chiave, out var p) => p,
            _ => null
        };
    }

    /// <summary>«Accreditato», «Sospeso» o «NonTrovato» per numero e sede (numero a 5 cifre, sede una lettera).</summary>
    public string Stato(string numero, string sede)
    {
        var chiave = (numero.Trim().PadLeft(5, '0'), sede.Trim().ToUpperInvariant());
        if (Sospesi.Contains(chiave)) return "Sospeso";
        return Accreditati.Contains(chiave) ? "Accreditato" : "NonTrovato";
    }
}

/// <summary>
/// Legge lo stato degli accreditamenti dalla banca dati pubblica dei laboratori di prova ACCREDIA
/// (services.accredia.it, pagina «Ricerca laboratori di prova»), con le stesse due ricerche che si fanno a mano
/// (08/10/2026, punto 1 del documento sui subappalti):
/// 1. pulsante «Tutti» (GET): tutti i laboratori di prova accreditati, con numero e sede;
/// 2. ricerca con «Sospensione Attiva: Sì» (POST del modulo): solo quelli con sospensione temporanea in corso.
/// Un laboratorio monitorato assente dall'elenco 1 non è più accreditato (revoca, ritiro, rinuncia).
/// Usato dall'Agente ACCREDIA (il server di produzione non raggiunge ACCREDIA). Nessuna API ufficiale: se la pagina
/// cambia o non risponde come previsto, <see cref="LeggiAsync"/> solleva un'eccezione e lo stato non viene inviato
/// (meglio «non verificato» che un falso allarme su tutti i laboratori).
/// </summary>
public sealed class ClientStatoAccredia
{
    private const string Indirizzo = "https://services.accredia.it/accredia_labsearch.jsp?ID_LINK=1734&area=310&dipartimento=L";

    /// <summary>Sotto questa soglia l'elenco «Tutti» è considerato incompleto (l'08/10/2026 erano 1.927 laboratori).</summary>
    public const int MinimoLaboratoriAttesi = 500;

    private readonly HttpClient _http;

    public ClientStatoAccredia(HttpClient http) => _http = http;

    /// <param name="dettaglioPer">Laboratori (numero, sede) di cui leggere le prove sospese, se risultano sospesi
    /// (di norma i laboratori monitorati). Un errore sul dettaglio di un laboratorio non blocca gli altri.</param>
    /// <param name="avviso">Facoltativo: riceve un messaggio per ogni dettaglio non letto (per il log dell'agente).</param>
    public async Task<StatiAccredia> LeggiAsync(
        IEnumerable<(string Numero, string Sede)>? dettaglioPer = null, Action<string>? avviso = null, CancellationToken ct = default)
    {
        // Pagina di ricerca prima delle due richieste: eventuali cookie di sessione restano nel HttpClient.
        await _http.GetStringAsync(Indirizzo + "&desc=Laboratori", ct);

        var tutti = await _http.GetStringAsync(Indirizzo + "&submit1=Tutti", ct);
        var accreditati = EstraiLaboratori(tutti, out var totaleTutti);
        if (totaleTutti is null || accreditati.Count < MinimoLaboratoriAttesi)
            throw new InvalidOperationException(
                $"Elenco ACCREDIA dei laboratori non riconosciuto ({accreditati.Count} laboratori letti): stato non aggiornato.");

        var campi = new Dictionary<string, string>
        {
            ["ID_LINK"] = "1734", ["area"] = "310", ["dipartimento"] = "L", ["cmbCriterioRicerca"] = "AND",
            ["txtNumeroAccr"] = "", ["txtLabor"] = "", ["cmbLabor"] = "AND", ["txtLabor2"] = "",
            ["txtProva"] = "", ["cmbProva"] = "AND", ["txtProva2"] = "", ["txtMater"] = "", ["cmbMater"] = "AND", ["txtMater2"] = "",
            ["txtNorma"] = "", ["cmbNorma"] = "AND", ["txtNorma2"] = "", ["txtTecnica"] = "", ["cmbTecnica"] = "AND", ["txtTecnica2"] = "",
            ["txtCategoria"] = "", ["sosp"] = "true", ["submit2"] = "Cerca"
        };
        using var risposta = await _http.PostAsync(Indirizzo + "&desc=Laboratori", new FormUrlEncodedContent(campi), ct);
        risposta.EnsureSuccessStatusCode();
        var sospesiHtml = await risposta.Content.ReadAsStringAsync(ct);
        var sospesi = EstraiLaboratori(sospesiHtml, out var totaleSospesi);
        // Nessun sospeso: la pagina potrebbe non riportare «Risultati: 0» ma un messaggio di nessun risultato.
        if (totaleSospesi is null && Regex.IsMatch(sospesiHtml, @"nessun\s+(risultato|laboratorio)", RegexOptions.IgnoreCase))
            totaleSospesi = 0;
        if (totaleSospesi is null)
            throw new InvalidOperationException("Ricerca ACCREDIA dei laboratori sospesi non riconosciuta: stato non aggiornato.");
        // Controllo di buon senso: i sospesi sono pochi; se la ricerca restituisce quasi tutti, il filtro non è stato applicato.
        if (sospesi.Count > accreditati.Count / 2)
            throw new InvalidOperationException($"Ricerca dei sospesi non filtrata ({sospesi.Count} risultati): stato non aggiornato.");

        // Prove sospese: ogni risultato della ricerca ha un modulo nascosto (numeroaccr, classification, TL = codici
        // delle prove sospese, ...) che apre l'«Elenco ristretto alle prove» con l'asterisco. Lo si invia così com'è.
        var prove = new Dictionary<(string, string), IReadOnlyList<ProvaSospesaLetta>>();
        var richiesti = (dettaglioPer ?? Array.Empty<(string, string)>())
            .Select(x => (x.Item1.Trim().PadLeft(5, '0'), x.Item2.Trim().ToUpperInvariant()))
            .Where(sospesi.Contains).ToHashSet();
        if (richiesti.Count > 0)
        {
            foreach (var modulo in EstraiModuli(sospesiHtml))
            {
                if (!richiesti.Contains(modulo.Chiave) || prove.ContainsKey(modulo.Chiave)) continue;
                try
                {
                    using var r = await _http.PostAsync(modulo.Indirizzo, new FormUrlEncodedContent(modulo.Campi), ct);
                    r.EnsureSuccessStatusCode();
                    var dettaglio = await r.Content.ReadAsStringAsync(ct);
                    var righe = EstraiProveSospese(dettaglio);
                    // Se la tabella c'è ma nessuna riga ha l'asterisco, la pagina non è quella attesa: meglio non dire nulla.
                    if (righe is null || righe.Count == 0)
                        avviso?.Invoke($"Prove sospese di {modulo.Chiave.Item1}/{modulo.Chiave.Item2} non riconosciute: non aggiornate.");
                    else
                        prove[modulo.Chiave] = righe;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    avviso?.Invoke($"Prove sospese di {modulo.Chiave.Item1}/{modulo.Chiave.Item2} non lette: {ex.Message}");
                }
            }
        }

        return new StatiAccredia(accreditati, sospesi, prove);
    }

    /// <summary>Un modulo nascosto «frm…» della pagina dei risultati: indirizzo, campi e laboratorio (numero, sede).</summary>
    public sealed record ModuloDettaglio(string Indirizzo, IReadOnlyList<KeyValuePair<string, string>> Campi, (string Numero, string Sede) Chiave);

    private static readonly Regex Modulo = new(
        "<form\\s+method=\"POST\"\\s+name=\"frm[^\"]*\"\\s+action=\"([^\"]*)\"\\s*>(.*?)</form>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex CampoNascosto = new(
        "<input\\s+name=\"([^\"]+)\"\\s+type=\"hidden\"\\s+value=\"([^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<ModuloDettaglio> EstraiModuli(string html)
    {
        var moduli = new List<ModuloDettaglio>();
        foreach (Match m in Modulo.Matches(html))
        {
            var indirizzo = WebUtility.HtmlDecode(m.Groups[1].Value);
            if (!indirizzo.StartsWith("https://services.accredia.it/", StringComparison.OrdinalIgnoreCase)) continue;
            // I valori sono passati come li invierebbe il browser (testo della pagina decodificato, poi codificato dal modulo).
            var campi = CampoNascosto.Matches(m.Groups[2].Value)
                .Select(c => new KeyValuePair<string, string>(c.Groups[1].Value, WebUtility.HtmlDecode(c.Groups[2].Value)))
                .ToList();
            var numero = campi.FirstOrDefault(c => c.Key == "numeroaccr").Value;
            var sede = campi.FirstOrDefault(c => c.Key == "classification").Value;
            if (string.IsNullOrWhiteSpace(numero) || string.IsNullOrWhiteSpace(sede)) continue;
            moduli.Add(new ModuloDettaglio(indirizzo, campi, (numero.Trim().PadLeft(5, '0'), sede.Trim().ToUpperInvariant())));
        }
        return moduli;
    }

    private static readonly Regex TabellaProve = new(
        "<table[^>]*class=\"proveflex_table\"[^>]*>(.*?)</table>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex Riga = new("<tr[^>]*>(.*?)</tr>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private static readonly Regex Cella = new("<td[^>]*/>|<td[^>]*>(.*?)</td>", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    /// <summary>
    /// Righe con asterisco nella prima colonna della tabella «Elenco ristretto alle prove» (colonne: S., Cat., -, Matrice,
    /// Prova, Norma, Tecnica, Campo). Null se la tabella non c'è.
    /// </summary>
    public static IReadOnlyList<ProvaSospesaLetta>? EstraiProveSospese(string html)
    {
        var tabella = TabellaProve.Match(html);
        if (!tabella.Success) return null;
        var righe = new List<ProvaSospesaLetta>();
        foreach (Match r in Riga.Matches(tabella.Groups[1].Value))
        {
            var celle = Cella.Matches(r.Groups[1].Value).Select(c => Testo(c.Groups[1].Value)).ToList();
            if (celle.Count < 7 || celle[0] != "*") continue;
            righe.Add(new ProvaSospesaLetta(Vuoto(celle[3]), Vuoto(celle[4]), Vuoto(celle[5]), Vuoto(celle[6])));
        }
        return righe;
    }

    private static string Testo(string html) =>
        Regex.Replace(WebUtility.HtmlDecode(Regex.Replace(html, "<[^>]+>", " ")), @"\s+", " ").Trim();

    private static string? Vuoto(string s) => s.Length == 0 ? null : s;

    // Ogni risultato ha un modulo nascosto con numero e sede: <input name="numeroaccr" ... value="00253"><input name="classification" ... value="A">
    // (alcune sedi non di prova hanno un nome al posto della lettera, es. «Uffici»: si tengono così come sono).
    private static readonly Regex RigaLaboratorio = new(
        "name=\"numeroaccr\"\\s+type=\"hidden\"\\s+value=\"(\\d{1,5})\"\\s*>\\s*<input\\s+name=\"classification\"\\s+type=\"hidden\"\\s+value=\"([^\"]{1,60})\"",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Risultati = new(@"Risultati:\s*(?:<span>)?\s*(\d+)", RegexOptions.Compiled);

    /// <summary>Numero e sede di ogni laboratorio nella pagina dei risultati; <paramref name="totale"/> = «Risultati: N» (null se assente).</summary>
    public static HashSet<(string Numero, string Sede)> EstraiLaboratori(string html, out int? totale)
    {
        var m = Risultati.Match(html);
        totale = m.Success ? int.Parse(m.Groups[1].Value) : null;
        return RigaLaboratorio.Matches(html)
            .Select(x => (x.Groups[1].Value.PadLeft(5, '0'), x.Groups[2].Value.Trim().ToUpperInvariant()))
            .ToHashSet();
    }
}
