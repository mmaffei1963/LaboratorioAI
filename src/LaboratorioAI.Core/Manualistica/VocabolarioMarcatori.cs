namespace LaboratorioAI.Core.Manualistica;

/// <summary>Come è andato il controllo dei marcatori di un modello.</summary>
/// <param name="NomeFile">Il file esaminato.</param>
/// <param name="NumeroControlli">Quanti controlli contenuto marcati sono stati trovati.</param>
/// <param name="Errori">Problemi che impediscono di mandare il modello in approvazione.</param>
/// <param name="Avvisi">Segnalazioni che non bloccano: tipicamente campi nuovi.</param>
/// <param name="TagPerPrefisso">Quanti marcatori distinti per tipo, per il riepilogo a schermo.</param>
public record EsitoControlloModello(
    string NomeFile,
    int NumeroControlli,
    IReadOnlyList<string> Errori,
    IReadOnlyList<string> Avvisi,
    IReadOnlyDictionary<string, int> TagPerPrefisso)
{
    /// <summary>Nessun errore: il modello può andare in approvazione.</summary>
    public bool Ok => Errori.Count == 0;
}

/// <summary>
/// I marcatori che il motore sa interpretare (versione 23 del motore, 03/11/2026).
///
/// Verificati riga per riga sul <c>motore.js</c> consegnato il 04/11/2026
/// (copia in docs\Privacy\motore_v23.js): condizioni = chiavi di
/// <c>contesto()</c>, testi = rami di <c>testoAlt()</c>, liste = chiavi di
/// <c>liste()</c>. Non è più una ricostruzione. Il dettaglio di ogni regola è in
/// docs\Privacy\INVENTARIO_Motore_Manualistica.md.
///
/// Un modello che contiene un marcatore sconosciuto non si può generare:
/// il motore non saprebbe cosa farne. Per questo il controllo lo segnala
/// come errore e il modello non passa in approvazione. Aggiungere una
/// regola al motore significa aggiungere la voce anche qui.
/// </summary>
public static class VocabolarioMarcatori
{
    /// <summary>Un valore da scrivere.</summary>
    public const string Campo = "CAMPO";
    /// <summary>Blocco tenuto o tolto secondo una condizione.</summary>
    public const string Se = "SE";
    /// <summary>Testo scelto fra alternative, o costruito dal motore.</summary>
    public const string Testo = "TESTO";
    /// <summary>Elenco costruito da una selezione multipla.</summary>
    public const string Lista = "LISTA";
    /// <summary>Blocco duplicato una volta per elemento.</summary>
    public const string Ripeti = "RIPETI";
    /// <summary>Ambito di una funzione aziendale, nelle lettere di nomina.</summary>
    public const string Sezione = "SEZIONE";
    /// <summary>Punto in cui va inserita l'immagine di una firma.</summary>
    public const string Firma = "FIRMA";
    /// <summary>Trattamenti particolari: tabella delle revisioni, organigramma.</summary>
    public const string Speciale = "SPECIALE";

    public static readonly IReadOnlySet<string> Prefissi =
        new HashSet<string>(StringComparer.Ordinal) { Campo, Se, Testo, Lista, Ripeti, Sezione, Firma, Speciale };

    /// <summary>Condizioni vero/falso calcolate dalla check list.</summary>
    public static readonly IReadOnlySet<string> Condizioni =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "VIDEO_PRESENTE", "PISCINA_PRESENTE", "VIDEO_REGISTRAZIONE", "PISCINA_REGISTRAZIONE",
            "VIDEO_O_PISCINA", "QUALSIASI_REGISTRAZIONE", "VIDEO_NON_REGISTRA", "PISCINA_NON_REGISTRA",
            "FINALITA_STATISTICHE", "GESTIONALE_CLIENTI", "FINALITA_COMUNICAZIONE_PRESENZA",
            "FINALITA_FOTO_VIDEO", "FINALITA_MARKETING",
            "FUNZIONE_CAPO_CHEF", "GUARDIANO_PRESENTE", "MANUTENTORI_INTERNI",
            "CRM_PRESENTE", "WIFI_PRESENTE",
            "BACKUP_SERVER", "BACKUP_DISPOSITIVI_ESTERNI", "BACKUP_CLOUD", "NESSUN_BACKUP",
            "SOSTITUTO_LR_DATA_BREACH", "CONTRATTO_FORNITURA", "ASSISTENZA_ANNUALE",
            // Versione 23 del motore (03/11/2026): le dodici combinazioni di
            // videosorveglianza e telecamera piscina. Quando i due impianti si
            // comportano in modo diverso la DPIA scrive una frase per impianto.
            "VIDEO_VISIONE", "PISCINA_VISIONE", "VISIONE_DIRETTA",
            "IMPIANTI_MISTI_REGISTRAZIONE", "IMPIANTI_MISTI_VISIONE",
            // Valutate dal motore dentro un blocco, non dalla check list.
            "RIGA_COMPILATA", "VIDEO_ACCESSO_FUNZIONE"
        };

    /// <summary>Condizioni a valore, nella forma CHIAVE=VALORE, con i valori ammessi.</summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> CondizioniAValore =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["SALA"] = new HashSet<string>(StringComparer.Ordinal) { "CAMERIERI_SALA", "CAPO_SALA", "NESSUNO" },
            ["PIANI"] = new HashSet<string>(StringComparer.Ordinal) { "INTERNE_NOMINATE", "INTERNE_NON_NOMINATE", "DITTA_ESTERNA" },
            ["GUARDIANO"] = new HashSet<string>(StringComparer.Ordinal) { "CON_SEGRETERIA", "SENZA_SEGRETERIA", "NESSUNO" }
        };

    public static readonly IReadOnlySet<string> Testi =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "CONSERVAZIONE_VIDEO", "CONSERVAZIONE_PISCINA",
            "REGISTRAZIONI_AUTORITA_VIDEO", "REGISTRAZIONI_AUTORITA_PISCINA",
            "CATEGORIE_IMMAGINI_VIDEO", "DPIA_RESPONSABILITA",
            "FUNZIONI_VISIONE_ELENCO", "FUNZIONI_CONSULTAZIONE_ELENCO",
            "DPIA_PERIODO_CONSERVAZIONE", "BACKUP_INTRO",
            "PAGAMENTO_BONIFICO", "PAGAMENTO_RIBA",
            "NESSUNA_VIDEOSORVEGLIANZA", "VIDEO_ACCESSO",
            // Versione 23: frasi scritte per impianto quando video e piscina differiscono.
            "DPIA_CONSERVAZIONE_IMPIANTO", "DPIA_IMPIANTO_SOLA_VISIONE", "DPIA_VISIONE_IMPIANTO"
        };

    /// <summary>I consensi dell'informativa nel contratto: CONSENSO_PROFILAZIONE, CONSENSO_ART9…</summary>
    public const string PrefissoTestoConsenso = "CONSENSO_";

    public static readonly IReadOnlySet<string> Liste =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "FUNZIONI_VISIONE_DIRETTA", "FUNZIONI_CONSULTAZIONE_REGISTRAZIONI",
            "BACKUP_SERVER_FUNZIONI", "BACKUP_CLOUD_FUNZIONI", "EMAIL_CASELLE",
            "MISURE_MINACCE_UMANE", "MISURE_ANTINCENDIO", "MISURE_EVENTI_NATURALI"
        };

    public static readonly IReadOnlySet<string> Ripetizioni =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "SEDI_TRATTAMENTO", "FUNZIONI_VISIONE_DIRETTA", "AUTORIZZATI",
            "ARCHIVIO_CARTACEO", "ARCHIVIO_INFORMATICO",
            "LETTERE_RESPONSABILE", "MANUTENTORI_INTERNI"
        };

    /// <summary>Una lettera di nomina per funzione: RIPETI:AUTORIZZATI_FRONT_OFFICE…</summary>
    public const string PrefissoRipetiAutorizzati = "AUTORIZZATI_";

    /// <summary>Ambito di una funzione: SEZIONE:AMBITO_FRONT_OFFICE…</summary>
    public const string PrefissoSezioneAmbito = "AMBITO_";

    public static readonly IReadOnlySet<string> Firme =
        new HashSet<string>(StringComparer.Ordinal) { "CONSULENTE", "LEGALE_RAPPRESENTANTE", "LR" };

    public static readonly IReadOnlySet<string> Speciali =
        new HashSet<string>(StringComparer.Ordinal) { "TABELLA_REVISIONI", "ORGANIGRAMMA" };

    /// <summary>
    /// Esamina i marcatori trovati in un modello e dice se il motore li
    /// sa interpretare tutti.
    /// </summary>
    /// <param name="nomeFile">Nome del file, per il riepilogo.</param>
    /// <param name="tag">I valori dei tag trovati, uno per controllo (ripetizioni comprese).</param>
    /// <param name="codiciFunzione">Codici delle funzioni aziendali del tipo di manuale (AMMINISTRAZIONE, FRONT_OFFICE…).</param>
    /// <param name="campiNoti">Campi già visti negli altri modelli: quelli nuovi diventano un avviso, non un errore.</param>
    /// <param name="richiedeMarcatori">Falso per i modelli di servizio, che di marcatori non ne hanno.</param>
    public static EsitoControlloModello Verifica(
        string nomeFile,
        IEnumerable<string> tag,
        IReadOnlyCollection<string> codiciFunzione,
        IReadOnlySet<string>? campiNoti = null,
        bool richiedeMarcatori = true)
    {
        var errori = new List<string>();
        var avvisi = new List<string>();
        var perPrefisso = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var totale = 0;

        foreach (var grezzo in tag)
        {
            var t = (grezzo ?? "").Trim();
            if (t.Length == 0) continue;
            totale++;

            var taglio = t.IndexOf(':');
            if (taglio <= 0)
            {
                Aggiungi(errori, $"Marcatore senza prefisso: «{t}».");
                continue;
            }

            var prefisso = t[..taglio];
            var nome = t[(taglio + 1)..];

            if (!Prefissi.Contains(prefisso))
            {
                Aggiungi(errori, $"Prefisso sconosciuto «{prefisso}:» (in «{t}»).");
                continue;
            }

            if (!perPrefisso.TryGetValue(prefisso, out var insieme))
                perPrefisso[prefisso] = insieme = new HashSet<string>(StringComparer.Ordinal);
            insieme.Add(nome);

            if (nome.Length == 0)
            {
                Aggiungi(errori, $"Marcatore «{prefisso}:» senza nome.");
                continue;
            }

            switch (prefisso)
            {
                case Campo:
                    if (campiNoti is not null && !campiNoti.Contains(nome))
                        Aggiungi(avvisi, $"Campo nuovo «{nome}»: va aggiunto alla check list, altrimenti resta vuoto.");
                    break;

                case Se:
                    VerificaCondizione(nome, errori);
                    break;

                case Testo:
                    if (!Testi.Contains(nome) && !nome.StartsWith(PrefissoTestoConsenso, StringComparison.Ordinal))
                        Aggiungi(errori, $"Testo alternativo sconosciuto «{nome}»: il motore non sa quale testo scrivere.");
                    break;

                case Lista:
                    if (!Liste.Contains(nome))
                        Aggiungi(errori, $"Elenco sconosciuto «{nome}»: il motore non sa da dove prendere le voci.");
                    break;

                case Ripeti:
                    if (!Ripetizioni.Contains(nome) && !FunzioneValida(nome, PrefissoRipetiAutorizzati, codiciFunzione))
                        Aggiungi(errori, $"Ripetizione sconosciuta «{nome}»: il motore non sa cosa duplicare.");
                    break;

                case Sezione:
                    if (!FunzioneValida(nome, PrefissoSezioneAmbito, codiciFunzione))
                        Aggiungi(errori, $"Sezione sconosciuta «{nome}»: attesa «{PrefissoSezioneAmbito}» seguita da una funzione aziendale.");
                    break;

                case Firma:
                    if (!Firme.Contains(nome))
                        Aggiungi(errori, $"Firma sconosciuta «{nome}».");
                    break;

                case Speciale:
                    if (!Speciali.Contains(nome))
                        Aggiungi(errori, $"Trattamento speciale sconosciuto «{nome}».");
                    break;
            }
        }

        if (totale == 0 && richiedeMarcatori)
            Aggiungi(errori, "Nessun marcatore trovato: il file non è un modello marcato, o i controlli contenuto sono stati persi salvandolo.");

        return new EsitoControlloModello(
            nomeFile,
            totale,
            errori,
            avvisi,
            perPrefisso.ToDictionary(x => x.Key, x => x.Value.Count, StringComparer.Ordinal));
    }

    private static void VerificaCondizione(string nome, List<string> errori)
    {
        var uguale = nome.IndexOf('=');
        if (uguale < 0)
        {
            if (!Condizioni.Contains(nome))
                Aggiungi(errori, $"Condizione sconosciuta «{nome}»: il motore non sa come calcolarla, e nel dubbio terrebbe il blocco.");
            return;
        }

        var chiave = nome[..uguale];
        var valore = nome[(uguale + 1)..];
        if (!CondizioniAValore.TryGetValue(chiave, out var ammessi))
        {
            Aggiungi(errori, $"Condizione a valore sconosciuta «{chiave}» (in «{nome}»).");
            return;
        }
        if (!ammessi.Contains(valore))
            Aggiungi(errori, $"Valore «{valore}» non ammesso per «{chiave}»: previsti {string.Join(", ", ammessi)}.");
    }

    private static bool FunzioneValida(string nome, string prefisso, IReadOnlyCollection<string> codiciFunzione) =>
        nome.StartsWith(prefisso, StringComparison.Ordinal)
        && codiciFunzione.Contains(nome[prefisso.Length..], StringComparer.Ordinal);

    /// <summary>Lo stesso problema su più controlli si scrive una volta sola.</summary>
    private static void Aggiungi(List<string> elenco, string messaggio)
    {
        if (!elenco.Contains(messaggio)) elenco.Add(messaggio);
    }
}
