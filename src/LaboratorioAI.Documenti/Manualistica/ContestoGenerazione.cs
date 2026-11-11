using System.Globalization;
using LaboratorioAI.Core.Entita;

namespace LaboratorioAI.Documenti.Manualistica;

/// <summary>
/// Le condizioni e i valori con cui si genera il pacchetto di un
/// cliente (11/11/2026).
///
/// E' il porto in C# di <c>contesto()</c> di motore.js, riga per riga.
/// Le regole non sono state reinterpretate: dove il motore scrive
/// <c>V !== 'SOLO_REG'</c> qui c'e' la stessa cosa, perche' una
/// differenza anche piccola fa uscire o sparire un paragrafo da un
/// documento firmato, e non se ne accorgerebbe nessuno.
///
/// Riferimento: docs\Privacy\INVENTARIO_Motore_Manualistica.md §3.
/// </summary>
/// <summary>Una sede del trattamento, come esce nei documenti.</summary>
public sealed record SedeDocumento(
    string? Nome, string? Indirizzo, string? Cap, string? Comune, string? Provincia);

/// <summary>
/// Un autorizzato al trattamento, come esce nei documenti.
///
/// <paramref name="ConNominativo"/> distingue chi e' una persona con un
/// nome (Legale Rappresentante, Socio, Coadiuvante familiare) da chi e'
/// un gruppo di cui si sa solo quanti sono: per i Lavoratori il nome
/// non si chiede e si scrive a mano sul documento stampato.
/// </summary>
public sealed record AutorizzatoDocumento(
    string? Nome, string? Cognome, string Qualifica, bool ConNominativo,
    int Quantita, IReadOnlyList<string> Funzioni);

public sealed class ContestoGenerazione
{
    private readonly Dictionary<string, bool> _condizioni = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aValore = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, string?> _risposte;
    private readonly Compilazione _compilazione;

    /// <summary>Le sedi del trattamento, nell'ordine in cui vanno scritte.</summary>
    public IReadOnlyList<SedeDocumento> Sedi { get; }

    /// <summary>Gli autorizzati, nell'ordine delle funzioni aziendali.</summary>
    public IReadOnlyList<AutorizzatoDocumento> Autorizzati { get; }

    /// <summary>Costruisce il contesto dalle risposte della check list.</summary>
    public ContestoGenerazione(
        Compilazione compilazione,
        IReadOnlyDictionary<string, string?> risposte,
        IReadOnlyList<SedeDocumento>? sedi = null,
        IReadOnlyList<AutorizzatoDocumento>? autorizzati = null)
    {
        _compilazione = compilazione;
        _risposte = risposte;
        Sedi = sedi ?? [];
        Autorizzati = autorizzati ?? [];
        Calcola();
    }

    /// <summary>
    /// Per quante ore si conservano le immagini. Serve ai testi
    /// alternativi, che la scrivono dentro una frase: qui va il numero
    /// nudo, non «24 ore».
    /// </summary>
    public int DurataOre => Numero("DURATA_CONSERVAZIONE_IMMAGINI");

    private string? R(string codice) => _risposte.TryGetValue(codice, out var v) ? v : null;
    private bool Si(string codice) => string.Equals(R(codice), "SI", StringComparison.Ordinal);
    private int Numero(string codice) =>
        int.TryParse(R(codice), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private void Calcola()
    {
        var v = R("VIDEO") ?? "NESSUNA";
        var p = R("PISCINA") ?? "NESSUNA";

        var videoPresente = v != "NESSUNA";
        var piscinaPresente = p != "NESSUNA";
        var videoRegistrazione = v is "VISIONE_REG" or "SOLO_REG";
        var piscinaRegistrazione = p is "VISIONE_REG" or "SOLO_REG";

        // La piscina non ha «solo registrazione», ma la regola vale per entrambi.
        var videoVisione = videoPresente && v != "SOLO_REG";
        var piscinaVisione = piscinaPresente && p != "SOLO_REG";

        _condizioni["VIDEO_PRESENTE"] = videoPresente;
        _condizioni["PISCINA_PRESENTE"] = piscinaPresente;
        _condizioni["VIDEO_REGISTRAZIONE"] = videoRegistrazione;
        _condizioni["PISCINA_REGISTRAZIONE"] = piscinaRegistrazione;
        _condizioni["VIDEO_O_PISCINA"] = videoPresente || piscinaPresente;
        _condizioni["VIDEO_VISIONE"] = videoVisione;
        _condizioni["PISCINA_VISIONE"] = piscinaVisione;

        // Almeno un impianto si vede in diretta (corretta il 03/11/2026 per le 12 combinazioni).
        _condizioni["VISIONE_DIRETTA"] = videoVisione || piscinaVisione;
        _condizioni["QUALSIASI_REGISTRAZIONE"] = videoRegistrazione || piscinaRegistrazione;
        _condizioni["VIDEO_NON_REGISTRA"] = videoPresente && !videoRegistrazione;
        _condizioni["PISCINA_NON_REGISTRA"] = piscinaPresente && !piscinaRegistrazione;

        // Impianti in modalita' diverse: nella DPIA una frase per impianto.
        var entrambi = videoPresente && piscinaPresente;
        _condizioni["IMPIANTI_MISTI_REGISTRAZIONE"] = entrambi && videoRegistrazione != piscinaRegistrazione;
        _condizioni["IMPIANTI_MISTI_VISIONE"] = entrambi && videoVisione != piscinaVisione;

        foreach (var f in new[]
                 {
                     "FINALITA_STATISTICHE", "GESTIONALE_CLIENTI", "FINALITA_COMUNICAZIONE_PRESENZA",
                     "FINALITA_FOTO_VIDEO", "FINALITA_MARKETING"
                 })
            _condizioni[f] = Si(f);

        _condizioni["FUNZIONE_CAPO_CHEF"] = Si("CAPO_CHEF");

        var guardiano = R("GUARDIANO") ?? "NESSUNO";
        _condizioni["GUARDIANO_PRESENTE"] = guardiano != "NESSUNO";
        _condizioni["MANUTENTORI_INTERNI"] = Numero("MANUTENTORI") > 0;
        _condizioni["CRM_PRESENTE"] = Si("CRM_PRESENTE");
        _condizioni["WIFI_PRESENTE"] = Si("WIFI_PRESENTE");

        var tipi = (R("BACKUP_TIPI") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries);
        _condizioni["BACKUP_SERVER"] = tipi.Contains("SERVER");
        _condizioni["BACKUP_DISPOSITIVI_ESTERNI"] = tipi.Contains("DISPOSITIVI");
        _condizioni["BACKUP_CLOUD"] = tipi.Contains("CLOUD");
        _condizioni["NESSUN_BACKUP"] = tipi.Length == 0;

        _condizioni["SOSTITUTO_LR_DATA_BREACH"] = !string.IsNullOrWhiteSpace(R("SOSTITUTO_LR"));

        _condizioni["CONTRATTO_FORNITURA"] = Si("CONTRATTO_FORNITURA");
        // Nel motore l'assistenza e' vera finche' non la si toglie.
        _condizioni["ASSISTENZA_ANNUALE"] = R("ASSISTENZA_ANNUALE") != "NO";

        _aValore["SALA"] = R("SALA") ?? "NESSUNO";
        _aValore["PIANI"] = R("PIANI") ?? "";
        _aValore["GUARDIANO"] = guardiano;
    }

    /// <summary>
    /// Vero se la condizione e' soddisfatta. Accetta anche la forma
    /// <c>CHIAVE=VALORE</c>, come <c>cond()</c> nel motore.
    /// </summary>
    /// <param name="nome">Il nome scritto nel marcatore, senza prefisso.</param>
    /// <param name="conosciuta">Falso se il nome non esiste: chi genera decide cosa farne.</param>
    public bool Vale(string nome, out bool conosciuta)
    {
        var uguale = nome.IndexOf('=');
        if (uguale > 0)
        {
            var chiave = nome[..uguale];
            var atteso = nome[(uguale + 1)..];
            conosciuta = _aValore.ContainsKey(chiave);
            return conosciuta && string.Equals(_aValore[chiave], atteso, StringComparison.Ordinal);
        }

        conosciuta = _condizioni.ContainsKey(nome);
        return conosciuta && _condizioni[nome];
    }

    /// <summary>
    /// Il valore di un campo. Prima l'anagrafica della compilazione,
    /// che non si chiede al consulente perche' arriva da sola; poi le
    /// risposte della check list.
    /// </summary>
    /// <param name="codice">Il nome del campo, senza il prefisso CAMPO:.</param>
    /// <param name="conosciuto">Falso se nessuno sa cosa scriverci.</param>
    public string? Campo(string codice, out bool conosciuto)
    {
        conosciuto = true;
        var c = _compilazione;

        switch (codice)
        {
            case "RAGIONE_SOCIALE": return c.RagioneSociale;
            case "PARTITA_IVA": return c.PartitaIva;
            // Come il motore: dove il codice fiscale manca si scrive la
            // partita IVA. Nei documenti il campo e' «C.F. / P.IVA», e
            // lasciandolo vuoto resterebbe una barra sospesa.
            case "CODICE_FISCALE":
                return string.IsNullOrWhiteSpace(c.CodiceFiscale) ? c.PartitaIva : c.CodiceFiscale;
            case "EMAIL": return c.Email;
            case "PEC": return c.Pec;
            case "TELEFONO": return c.Telefono;
            case "CODICE_ATECO": return c.CodiceAteco;
            case "ATTIVITA_ESERCITATA": return c.AttivitaEsercitata;
            case "SITI_WEB": return Elenco(c.SitiWeb);
            case "SEDE_LEGALE_INDIRIZZO": return c.SedeLegaleIndirizzo;
            case "SEDE_LEGALE_CAP_COMUNE": return CapComune(c.SedeLegaleCap, c.SedeLegaleComune, c.SedeLegaleProvincia);
            case "DATA_DOCUMENTI": return c.DataDocumenti.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            case "REVISIONE_NUMERO": return c.NumeroRevisioneDocumenti.ToString("00", CultureInfo.InvariantCulture);
            case "REVISIONE_DATA": return c.DataDocumenti.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            // Il Legale Rappresentante firma quasi tutti i documenti: si
            // prende dalla scheda degli autorizzati, riconosciuto dalla
            // qualifica come fa il motore.
            case "LR_NOME": return LegaleRappresentante?.Nome ?? "";
            case "LR_COGNOME": return LegaleRappresentante?.Cognome ?? "";
            case "DATA_RIESAME_DPIA": return c.DataDocumenti.AddYears(2).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

            // ── Il contratto di fornitura ────────────────────
            //
            // Quasi tutto il contratto e' gia' nella check list: il
            // cliente, la sua sede legale, la prima sede operativa, il
            // legale rappresentante. Restano da chiedere solo la banca,
            // i prezzi e l'eventuale intestazione a un soggetto diverso,
            // che arriveranno dalla scheda del contratto.
            case "DATA_DOCUMENTO": return c.DataDocumenti.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            case "REV_CORRENTE": return c.NumeroRevisioneDocumenti.ToString("00", CultureInfo.InvariantCulture);

            case "CONTRATTO_RAGIONE_SOCIALE": return c.RagioneSociale;
            case "CONTRATTO_INDIRIZZO": return c.SedeLegaleIndirizzo;
            case "CONTRATTO_CAP_COMUNE":
                return CapComune(c.SedeLegaleCap, c.SedeLegaleComune, c.SedeLegaleProvincia);
            case "CONTRATTO_PIVA": return c.PartitaIva;
            case "CONTRATTO_CODFISC":
                return string.IsNullOrWhiteSpace(c.CodiceFiscale) ? c.PartitaIva : c.CodiceFiscale;
            case "CONTRATTO_SDI": return c.CodiceUnivocoFatturazione;
            case "CONTRATTO_TELEFONO": return c.Telefono;
            case "CONTRATTO_EMAIL": return c.Email;
            case "CONTRATTO_REFERENTE": return Referente();

            // Sede legale, divisa in via e numero civico.
            case "CONTRATTO_SL_VIA": return ViaCivico(c.SedeLegaleIndirizzo).Via;
            case "CONTRATTO_SL_CIVICO": return ViaCivico(c.SedeLegaleIndirizzo).Civico;
            case "CONTRATTO_SL_CITTA": return c.SedeLegaleComune;
            case "CONTRATTO_SL_CAP": return c.SedeLegaleCap;
            case "CONTRATTO_SL_PROVINCIA": return c.SedeLegaleProvincia;

            // Sede operativa: la prima della scheda delle sedi.
            case "CONTRATTO_SO_VIA": return ViaCivico(PrimaSede?.Indirizzo).Via;
            case "CONTRATTO_SO_CIVICO": return ViaCivico(PrimaSede?.Indirizzo).Civico;
            case "CONTRATTO_SO_CITTA": return PrimaSede?.Comune;
            case "CONTRATTO_SO_CAP": return PrimaSede?.Cap;
            case "CONTRATTO_SO_PROVINCIA": return PrimaSede?.Provincia;

            // Prezzi: quelli di listino finche' non si decide altro.
            case "PREZZO_DOCUMENTAZIONE": return Euro(R("PREZZO_DOCUMENTAZIONE"), 300);
            case "PREZZO_ASSISTENZA": return Euro(R("PREZZO_ASSISTENZA"), 200);
        }

        // Risposte della check list che il motore non scrive cosi' come sono.
        switch (codice)
        {
            // «24» da solo lascerebbe «per un massimo di 24 e poi cancellate».
            case "DURATA_CONSERVAZIONE_IMMAGINI":
                return Con(R(codice), " ore");

            // Nei documenti questi luoghi sono voci di un elenco puntato.
            case "DVR_LUOGO":
            case "SCHERMO_LUOGO":
                return Trattino(R(codice));

            case "INL_DATA":
                return DataItaliana(R(codice));
        }

        if (_risposte.TryGetValue(codice, out var risposta)) return risposta;

        conosciuto = false;
        return null;
    }

    /// <summary>Aggiunge il suffisso solo se c'e' qualcosa a cui aggiungerlo.</summary>
    private static string? Con(string? valore, string suffisso) =>
        string.IsNullOrWhiteSpace(valore) ? valore : valore.Trim() + suffisso;

    /// <summary>«Ufficio Front Office» → «- Ufficio Front Office», se non ce l'ha gia'.</summary>
    private static string? Trattino(string? valore)
    {
        var v = (valore ?? "").Trim();
        if (v.Length == 0) return v;
        return v.StartsWith("- ", StringComparison.Ordinal) || v.StartsWith("-\t", StringComparison.Ordinal)
            ? v : "- " + v;
    }

    /// <summary>Dalla data del modulo (2026-11-11) a come si scrive (11/11/2026).</summary>
    private static string? DataItaliana(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return valore;
        return DateTime.TryParse(valore, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : valore;
    }

    /// <summary>Piu' siti scritti a capo o separati da virgole diventano un elenco in riga.</summary>
    private static string? Elenco(string? valore)
    {
        if (string.IsNullOrWhiteSpace(valore)) return valore;
        var pezzi = valore.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim()).Where(x => x.Length > 0);
        return string.Join(", ", pezzi);
    }

    /// <summary>La prima sede del trattamento, la sede operativa del contratto.</summary>
    private SedeDocumento? PrimaSede => Sedi.Count > 0 ? Sedi[0] : null;

    /// <summary>
    /// Chi firma il contratto per il cliente: il referente se ne hanno
    /// indicato uno diverso, altrimenti il legale rappresentante.
    /// </summary>
    private string Referente()
    {
        var c = _compilazione;

        var pezzi = !c.ReferenteComeLegaleRappresentante
            ? new[] { c.ReferenteNome, c.ReferenteCognome }
            : [LegaleRappresentante?.Nome, LegaleRappresentante?.Cognome];

        return string.Join(" ", pezzi.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));
    }

    /// <summary>
    /// «Via Carducci, 22» → via e civico separati, come li vuole il
    /// modulo del contratto. Se il civico non si riconosce resta tutto
    /// nella via: meglio un indirizzo intero in un campo solo che un
    /// numero inventato.
    /// </summary>
    private static (string Via, string Civico) ViaCivico(string? indirizzo)
    {
        var testo = (indirizzo ?? "").Trim();
        if (testo.Length == 0) return ("", "");

        var m = System.Text.RegularExpressions.Regex.Match(
            testo, @"^(.*?)[,\s]+(\d+[A-Za-z]?(?:/\w+)?)$");

        return m.Success ? (m.Groups[1].Value.Trim(), m.Groups[2].Value) : (testo, "");
    }

    /// <summary>Un importo come si scrive in Italia: 300,00.</summary>
    private static string Euro(string? risposta, decimal predefinito)
    {
        var valore = decimal.TryParse(risposta, NumberStyles.Any, CultureInfo.InvariantCulture, out var n)
            ? n : predefinito;

        return valore.ToString("N2", CultureInfo.GetCultureInfo("it-IT"));
    }

    /// <summary>Chi ha la qualifica di Legale Rappresentante, se c'e'.</summary>
    private AutorizzatoDocumento? LegaleRappresentante =>
        Autorizzati.FirstOrDefault(a =>
            string.Equals(a.Qualifica, "Legale Rappresentante", StringComparison.Ordinal));

    /// <summary>«47841 Cattolica (RN)»: come si scrive nei documenti.</summary>
    public static string CapComune(string? cap, string? comune, string? provincia)
    {
        var pezzi = new List<string>();
        if (!string.IsNullOrWhiteSpace(cap)) pezzi.Add(cap.Trim());
        if (!string.IsNullOrWhiteSpace(comune)) pezzi.Add(comune.Trim());

        var testo = string.Join(' ', pezzi);
        if (!string.IsNullOrWhiteSpace(provincia)) testo = $"{testo} ({provincia.Trim()})".TrimStart();

        return testo;
    }
}
