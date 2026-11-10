using LaboratorioAI.Core.Costanti;

namespace LaboratorioAI.Web.Navigazione;

/// <summary>Una voce di menu / scheda della pagina principale. <c>Permesso</c> può elencarne più d'uno separati da «|»: basta uno.</summary>
public record VoceMenu(string Titolo, string Url, string Permesso, string Descrizione);

/// <summary>
/// Una cartella della pagina principale e un gruppo del menu in testata (08/10/2026: cartelle per mestiere, pensate
/// per i gruppi 365). <paramref name="Titolo"/> è il nome breve del menu, <paramref name="TitoloHome"/> quello della
/// cartella; <paramref name="Icona"/> è il tracciato SVG (24×24, solo contorno) disegnato sulla home.
/// </summary>
public record GruppoMenu(string Titolo, string TitoloHome, string Descrizione, string Icona, IReadOnlyList<VoceMenu> Voci)
{
    /// <summary>Identificativo per la pagina (finestra della cartella), ricavato dal titolo.</summary>
    public string Codice => new string(TitoloHome.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}

/// <summary>Icone delle cartelle: tracciati SVG semplici, a solo contorno (nessuna libreria esterna).</summary>
public static class Icone
{
    public const string Flask = "M9 3h6M10 3v6l-5 9a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-9V3M7.5 15h9";
    public const string Search = "M11 4a7 7 0 1 0 0 14a7 7 0 1 0 0-14M20 20l-4-4";
    public const string Brief = "M4 8h16v11H4zM9 8V5h6v3M4 13h16";
    public const string Clip = "M9 4h6v3H9zM7 5H5v16h14V5h-2M9 12h6M9 16h4";
    public const string Drop = "M12 3c-4 5-6 8-6 11a6 6 0 0 0 12 0c0-3-2-6-6-11";
    public const string Leaf = "M5 19c0-8 6-14 14-14c0 8-6 14-14 14M5 19l7-7";
    public const string Scale = "M12 4v16M7 20h10M5 7h14M5 7l-2 6h4zM19 7l-2 6h4z";
    public const string Thermo = "M10 5a2 2 0 0 1 4 0v9a4 4 0 1 1-4 0zM12 10v6";
    public const string Euro = "M16 6.5a6.5 6.5 0 1 0 0 11M5 10h8M5 14h8";
    public const string Gear = "M4 6h16M4 12h16M4 18h16M9 4v4M15 10v4M8 16v4";
}

/// <summary>
/// Definizione UNICA della navigazione (01/10/2026; riorganizzata in dieci cartelle l'08/10/2026, vedi
/// claude/proposta-home-menu-registro-attivita.md): da qui sono generati sia il menu in testata
/// (_Layout) sia le schede della pagina principale (Home/Index), così aggiungere una pagina aggiorna
/// entrambi. Una voce compare solo se l'utente ha il permesso indicato.
/// Per aggiungere una pagina: una riga nel gruppo giusto, nient'altro.
/// </summary>
public static class MenuApplicazione
{
    public static readonly IReadOnlyList<GruppoMenu> Gruppi = new List<GruppoMenu>
    {
        new("Ciclo analitico", "Ciclo analitico", "Dall'accettazione al certificato", Icone.Flask, new List<VoceMenu>
        {
            new("Accettazione campioni", "/Accettazione", Permessi.AccettazioneEsegui, "Accettazione fuori sede e generazione del tracciato per Palomar."),
            new("Prelievo vocale", "/Prelievo", Permessi.AccettazioneEsegui, "Raccolta a voce dei dati del prelievo, anche offline."),
            new("Da Validare", "/Validazione", Permessi.ValidazioneVisualizza, "Campioni pronti per la validazione e analisi AI, singola o massiva."),
            new("Campioni in ritardo", "/RitardiCampioni", Permessi.ValidazioneVisualizza, "Campioni senza rapporto di prova oltre la soglia del loro tipo (dalla ricezione più il ritardo programmato), con l'avviso nel giorno scelto."),
            new("Limiti normativi", "/LimitiNormativi", Permessi.LimitiConsulta, "Schede limite ricavate dalle fonti ufficiali con l'AI e validate dal laboratorio: base delle segnalazioni in «Da Validare»."),
            new("Richieste analisi privati", "/RichiestePrivati", Permessi.RichiestePrivatiGestione, "Modulo da firmare al banco per i clienti privati (Mod. Com. 05B): dati del cliente e del campione, prove e prezzo IVA inclusa, compilato o in bianco."),
            new("Ordini di Accettazione", "/OrdiniAccettazione", Permessi.OrdiniAccettazioneGestione, "Ordini di Accettazione da firmare per i clienti privati (pulsante «Stampa da firmare» di ArcLab), registro dei consegnati e anteprima."),
            new("Certificati esterni · rinomina", "/CertificatiEsterni", Permessi.CertificatiEsterniGestione, "Rinomina dei certificati dei laboratori esterni, consegna a Palomar e registro di controllo."),
            new("Certificati esterni non ricertificati", "/SoloEsterni", Permessi.SoloEsterniVisualizza, "Confronto tra certificati esterni e interni per la fatturazione."),
            new("Laboratori Esterni", "/LaboratoriEsterni", Permessi.LaboratoriEsterniConsulta, "Analisi affidate all'esterno, accreditamenti ACCREDIA e verifica dei metodi."),
            new("Invio campioni ai lab esterni", "/InviiLaboratoriEsterni", Permessi.LaboratoriEsterniInvio, "Excel dei campioni spediti a un laboratorio esterno, inviato per e-mail con copia a refertazione@mare-a.com."),
            new("Ricerca errori", "/RicercaErrori", Permessi.RicercaErroriVisualizza, "Verifiche diagnostiche su rapporti, metodi, denominazioni e lotti (sola lettura)."),
        }),
        new("Ricerche", "Ricerche e statistiche", "Interrogare l'archivio", Icone.Search, new List<VoceMenu>
        {
            new("Consultazione", "/Consultazione", Permessi.ConsultazioneCampioni, "Ricerca campioni nell'archivio storico con filtri combinabili."),
            new("Certificati emessi", "/Consultazione/Certificati", Permessi.ConsultazioneCertificati, "Ricerca dei rapporti di prova e analisi a posteriori."),
            new("Metodi e Controlli", "/Metodi", Permessi.MetodiConsulta, "Ricerca bidirezionale, accreditamenti, subappalti e prezzi."),
            new("Commesse", "/Commesse", Permessi.CommesseConsulta, "Commesse in vigore, campioni collegati e prestazioni erogate."),
            new("Statistiche", "/Statistiche", Permessi.StatisticheVisualizza, "Ricerca trasversale per prova, con filtri ed esportazione Excel."),
        }),
        new("Commerciale", "Commerciale", "Clienti, offerte, risultati", Icone.Brief, new List<VoceMenu>
        {
            new("Clienti da seguire", "/ClientiDaSeguire", Permessi.ClientiSeguire, "Clienti in calo, Legionella da ricordare, registro delle azioni del commerciale."),
            new("Cambi di gestione", "/ClientiDaSeguire/Subentri", Permessi.ClientiSeguire + "|" + Permessi.ClientiSubentri, "Sedi perse (Legionella, analisi, consulenza per area): nuovo gestore trovato nello stesso posto, da confermare; quelle da ricontattare."),
            new("Preventivi analisi", "/Preventivi", Permessi.PreventiviGestione, "Componi le offerte dai controlli e metodi in archivio, su carta intestata."),
            // 12/10/2026: anche qui per i commerciali (Ludo, Andrea); resta pure in Consulenza per i consulenti.
            new("Preventivi consulenza", "/PreventiviConsulenza", Permessi.PreventiviConsulenza, "Contratti di consulenza e formazione (serie C) dalle attività di ArcLab o dai pacchetti di settore."),
            new("Condizioni e firme", "/Condizioni", Permessi.CondizioniFirmatari, "Revisioni delle Condizioni Generali, Firmatari dei clienti e richieste di approvazione, con registro delle evidenze."),
            new("Proposta controlli", "/Controlli", Permessi.ControlliProponi, "Proposta e approvazione di nuovi controlli con import supervisionato."),
            new("Le mie provvigioni", "/MieProvvigioni", Permessi.ProvvigioniProprie, "Il proprio lavoro: clienti, incassi, provvigioni e obiettivi."),
        }),
        new("Consulenza", "Consulenza", "Preventivi e rapportini", Icone.Clip, new List<VoceMenu>
        {
            new("Preventivi consulenza", "/PreventiviConsulenza", Permessi.PreventiviConsulenza, "Contratti di consulenza e formazione (serie C) dalle attività di ArcLab o dai pacchetti di settore."),
            new("Rapportini di consulenza", "/Rapportini", Permessi.RapportiniCompila, "Rapportino del sopralluogo: cosa è stato fatto e cosa sarà fatturato, inviato al cliente prima della fattura."),
            new("Attività e pacchetti consulenza", "/PreventiviConsulenza/Attivita", Permessi.PreventiviConsulenza, "Attività dei preventivi di consulenza (da ArcLab), testi, prezzi proposti e pacchetti di settore."),
            new("Le mie spettanze", "/MieSpettanze", Permessi.SpettanzeProprie, "I tuoi compensi a percentuale: maturato sugli incassi mese per mese, clienti, fatture emesse e conguaglio di fine anno."),
        }),
        new("Manualistica", "Manualistica", "Documenti per i clienti", Icone.Clip, new List<VoceMenu>
        {
            new("Modelli", "/Manualistica/Modelli", Permessi.ManualisticaConsulta, "I modelli Word da cui si generano i documenti per i clienti: caricamento, controllo dei marcatori e approvazione."),
            new("Chi approva", "/Manualistica/Approvatori", Permessi.ManualisticaApprovatori, "Per ogni tipo di manuale, chi può approvarne i modelli."),
            new("Voci della check list", "/Manualistica/Voci", Permessi.ManualisticaImpostazioni, "Le domande della check list e le voci delle tendine, lette dai modelli approvati."),
        }),
        new("Legionella e piscine", "Legionella e piscine", "DVR in Word", Icone.Drop, new List<VoceMenu>
        {
            new("DVR Legionella", "/Dvr", Permessi.DvrGenera, "Generazione dei documenti di valutazione del rischio, singola o massiva."),
            new("DVR Piscine", "/Piscine", Permessi.PiscineGenera, "Valutazione del rischio per le piscine e registro dei documenti."),
        }),
        new("Nutrizionale", "Nutrizionale", "Etichette e valori nutrizionali", Icone.Leaf, new List<VoceMenu>
        {
            new("Materie prime", "/MateriePrime", Permessi.NutrizionaleMateriePrime, "Catalogo delle materie prime e dei valori nutrizionali."),
            new("Ricette nutrizionali", "/Ricette", Permessi.NutrizionaleRicette, "Composizione delle ricette e rapporti nutrizionali (USA/Canada)."),
        }),
        new("Strumenti", "Strumenti e tarature", "Tarature e scadenze", Icone.Scale, new List<VoceMenu>
        {
            new("Taratura termometri", "/Taratura", Permessi.TaraturaEsegui, "Taratura, validazione e registro dei termometri."),
            new("Taratura incubatori", "/Incubatori", Permessi.IncubatoriEsegui, "Taratura e validazione degli incubatori."),
            new("Mappatura incubatori", "/Mappatura", Permessi.MappaturaEsegui, "Mappatura termica e validazione."),
            new("Taratura bilance", "/Bilance", Permessi.BilanceEsegui, "Taratura e validazione delle bilance."),
            new("Scadenziario tarature", "/Scadenziario", Permessi.ScadenziarioVisualizza, "Scadenze delle tarature e pianificazione."),
        }),
        new("Monitor Econorma", "Monitor Econorma", "Temperature delle sonde", Icone.Thermo, new List<VoceMenu>
        {
            new("Monitoraggio Econorma", "/TempLog", Permessi.TempLogEsegui, "Andamento temperature da file o database, con limiti."),
            new("Monitor generale", "/MonitorTemperature/Monitor/Monitor", Permessi.MonitorTemperatureVisualizza, "Stato attuale di tutte le sonde."),
            new("Monitor Controllo", "/MonitorTemperature/MonitorControllo", Permessi.MonitorTemperatureVisualizza, "Controllo delle sonde e dei limiti."),
            new("Monitor Differenze", "/MonitorTemperature/Differenze", Permessi.MonitorTemperatureVisualizza, "Differenze tra sonde e riferimenti."),
            new("Monitor Grafici", "/MonitorTemperature/Grafici", Permessi.MonitorTemperatureVisualizza, "Grafici storici delle temperature."),
            new("Sonde e sincronizzazione", "/MonitorTemperature/Sonde", Permessi.MonitorTemperatureVisualizza, "Sonde attive in ArcLab, codice Econorma e confronto dei seriali con LabAI; sincronizzazione della mappatura."),
        }),
        new("Amministrazione", "Amministrazione e fatturazione", "Prefatturato e provvigioni", Icone.Euro, new List<VoceMenu>
        {
            new("Tamponi in contratto", "/TamponiContratto", Permessi.PrefatturatoTamponi, "Prefatturato dei tamponi previsti dai contratti."),
            new("Non fatturare", "/NonFatturare", Permessi.PrefatturatoNonFatturare, "Prestazioni escluse dalla fatturazione."),
            new("Provvigioni", "/Provvigioni", Permessi.ProvvigioniGestisci + "|" + Permessi.ProvvigioniConsulta, "Provvigioni dei commerciali: importazione da AZW, calcolo, chiusura trimestri ed elenco «in sviluppo»."),
            new("Accordi provvigioni", "/AccordiProvvigioni", Permessi.ProvvigioniGestisci + "|" + Permessi.ProvvigioniConsulta, "Accordi dei commerciali: percentuali, basi di calcolo e soglie del premio, con versioni e date di validità."),
            new("Segnalatori", "/Segnalatori", Permessi.ProvvigioniGestisci + "|" + Permessi.ProvvigioniConsulta, "Chi ci porta clienti (enti, studi, consulenti, MARE.A), con le condizioni e i clienti segnalati."),
            new("Compensi segnalatori", "/Segnalatori/Compensi", Permessi.ProvvigioniGestisci + "|" + Permessi.ProvvigioniConsulta, "Compensi dovuti ai segnalatori, calcolati sugli incassi, per trimestre e per cliente."),
            new("Spettanze consulenti", "/SpettanzeConsulenti", Permessi.ProvvigioniGestisci + "|" + Permessi.ProvvigioniConsulta, "Compensi dei consulenti a percentuale (De Angelis, Bacchiani, Remedia): maturato sugli incassi, acconti e conguaglio annuale."),
            new("Ricerca fatture per descrizione", "/RicercaFatture", Permessi.RicercaFattureConsulta, "Fatture di vendita AZW cercate per testo della fattura (es. corsi e-learning) in un periodo, con totali ed Excel."),
            new("Consuntivo per area", "/Consuntivi", Permessi.ConsuntiviConsulta, "Costi e ricavi per area da AZW, con contributo dell'amministrazione, confronto con l'anno prima e andamento mensile (riservato ai soci)."),
        }),
        // 13/10/2026: riservato al ruolo Direzione (soci e direzione di laboratorio).
        new("Direzione", "Direzione", "Riservato: indicatori economici, territorio e commerciali", Icone.Euro, new List<VoceMenu>
        {
            new("Indicatori economici", "/Direzione", Permessi.DirezioneConsulta, "Fatturato, costi, margini, EBITDA e pareggio di MAREA e delle aree, dalla contabilità analitica di AZW (come Power BI)."),
            new("Andamento territorio", "/Direzione/Territorio", Permessi.DirezioneConsulta, "Fatturato per regione, provincia e comune della sede del cliente; clienti nuovi e persi; confronto sede di Pesaro."),
            new("Rendimento commerciali", "/Direzione/Commerciali", Permessi.DirezioneConsulta, "Costo di ogni commerciale (stipendio AZW + provvigioni) contro fatturato portato e seguito, margine e contributo."),
            new("Compensi soci", "/CompensiSoci", Permessi.CompensiSociConsulta, "Conguaglio annuale dei compensi dei soci: base, incarichi, RSPP e indennità laboratorio da AZW, rimborsi, percepiti e recupero dall'anno prima (riservato ai soci)."),
            new("Rimborsi spese soci", "/CompensiSoci/Rimborsi", Permessi.CompensiSociConsulta, "Rimborsi spese di ogni socio, con ricevuta facoltativa: ognuno inserisce i propri, tutti i soci li vedono."),
            new("Classificazione voci", "/Direzione/Voci", Permessi.DirezioneConsulta, "Voci di costo AZW: fisse o variabili, macrocategoria, materie prime."),
        }),
        new("Impostazioni", "Impostazioni e controllo", "Configurazioni e registro", Icone.Gear, new List<VoceMenu>
        {
            new("Impostazioni documenti", "/Impostazioni", Permessi.AmministrazioneConfigurazioni, "Revisioni delle condizioni, moduli e impostazioni dei documenti."),
            new("Voci preventivi (accessori, pagamenti)", "/VociPreventivo", Permessi.AmministrazioneConfigurazioni, "Voci accessorie e condizioni di pagamento delle offerte."),
            new("Sigle laboratori esterni", "/SigleLaboratori", Permessi.AmministrazioneConfigurazioni, "Sigle e parole chiave dei laboratori esterni per i nomi dei certificati."),
            new("Responsabili clienti", "/PreventiviConsulenza/Responsabili", Permessi.PreventiviConsulenza, "Sigle di zona dell'anagrafica AZW e nome del responsabile cliente MARE.A, riportato nei contratti di consulenza."),
            new("Consulenze: documenti e deleghe", "/ConsulenzeAmministrazione", Permessi.RapportiniGestione, "Documenti tipo dei rapportini di consulenza e deleghe operative dei clienti."),
            new("Clienti da seguire: soglie e assegnazioni", "/ClientiDaSeguire/Assegnazioni", Permessi.ClientiGestisci, "Approvazione dei clienti assegnati ai commerciali e soglie del semaforo."),
            new("Utenti e ruoli", "/Utenti", Permessi.AmministrazioneUtenti, "Chi può entrare in LaboratorioAI e cosa può fare: ruoli delle persone e permessi di ogni ruolo."),
            new("Registro attività (audit)", "/Audit", Permessi.AmministrazioneAudit, "Chi ha fatto cosa e quando."),
            new("Domande alla guida", "/Guide/Domande", Permessi.GuideDomande, "Domande fatte a «Chiedi alla guida»: risposte, voti, domande più frequenti e costo."),
        }),
    };

    /// <summary>Gruppi con le sole voci visibili all'utente (gruppi vuoti esclusi).</summary>
    public static IEnumerable<GruppoMenu> Visibili(Func<string, bool> haPermesso) =>
        Gruppi.Select(g => g with { Voci = g.Voci.Where(v => v.Permesso.Split('|').Any(haPermesso)).ToList() })
              .Where(g => g.Voci.Count > 0);
}
