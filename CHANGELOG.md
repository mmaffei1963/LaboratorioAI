# Changelog

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/).

## [1.0.0] — non ancora rilasciata

### Fase 1 — Impianto

- Modello di dominio: utenti, ruoli, permessi, analisi AI, registro DVR,
  audit trail, sessioni portale, tentativi di accesso
- Contesto EF Core con schemi separati `sec`, `app`, `portale`, `log`
- Popolamento iniziale idempotente di ruoli e permessi
- Autenticazione tramite Entra ID
- Autorizzazione a permessi granulari con generazione automatica delle policy
- Provisioning automatico dell'utente al primo accesso
- Servizio di audit trail resiliente agli errori
- Layout applicativo e navigazione filtrata per permessi
- Sonda di stato su `/stato`
- Configurazione IIS con hosting in-process

### Fase 2 — Validazione e Consultazione

- Accesso in sola lettura ad ArcLab tramite Dapper, con query parametrizzate
- Elenco campioni da validare con semaforo di giacenza
- Dettaglio campione con prove, limiti, unità di misura e laboratori esterni
- Integrazione API Anthropic tramite HttpClient, senza dipendenze di terze parti
- Archiviazione delle analisi: ogni esecuzione è un record, nessuna sovrascrittura
- Recupero dell'analisi archiviata senza costo aggiuntivo
- Analisi massiva sequenziale con riuso dell'archivio e semaforo di sintesi
- Ricerca campioni con quattordici criteri combinabili
- Stato campione calcolato: in corso, certificato, spedito originale
- Ricerca certificati emessi con analisi a posteriori
- Pannello identificativi tecnici per diagnosi
- Frontend a moduli ES, senza pipeline di compilazione

### Fase 3 — Metodi, Commesse, Laboratori Esterni

- Ricerca bidirezionale metodi ↔ controlli con tre logiche di confronto
- Esplosione del controllo con accreditamento MARE.A, subappalto e stato del metodo
- Quadro dei prezzi sulle commesse in vigore, riservato agli utenti abilitati
- Ricerca commesse per tipologia, con filtri adattati al laboratorio o alla consulenza
- Esplosione delle commesse in campioni collegati o prestazioni erogate
- Scadenza delle prestazioni calcolata dalla durata di commessa, con evidenza dei ritardi
- Interrogazione dei laboratori esterni sulla data di analisi effettiva
- Riepilogo per laboratorio, matrice e metodo con conteggio delle certificate
- Esportazione in Excel su due fogli con formattazione e filtri automatici
- Gli importi commerciali sono filtrati lato server in base ai permessi

### Fase 4 — Portale Clienti

- Progetto separato, rilasciabile e proteggibile in modo indipendente
- Credenziali lette da ArcLab, mai duplicate nel sistema
- Confronto a tempo costante su impronte di lunghezza fissa
- Confronto eseguito anche per codici inesistenti, per non rivelare quali siano validi
- Risposta indistinguibile fra codice inesistente e credenziale errata
- Protezione da tentativi ripetuti, per codice cliente e per indirizzo
- Filtro Serilog che esclude le credenziali da ogni livello di log
- Sessioni su database con token opaco, revocabili singolarmente
- Pulizia periodica delle sessioni scadute
- Vincolo sul cliente applicato nelle query, non nell'interfaccia
- Dettaglio dei risultati analitici riservato all'accesso operatore, con avvertenza
- Lettura dell'indirizzo reale del visitatore dietro Cloudflare

### Fase 5 — DVR Legionellosi

- Compilazione dei controlli contenuto tramite DocumentFormat.OpenXml
- Riconoscimento per tag, etichetta o impronta delle opzioni di elenco
- Normalizzazione dei caratteri tipografici introdotti dalla correzione di Word
- Sezioni condizionate rimosse o sciolte secondo le caratteristiche della struttura
- Rimozione delle pagine rimaste vuote dopo l'esclusione delle sezioni
- Richiesta di ricostruzione dell'indice all'apertura del documento
- Scioglimento dei controlli nel documento consegnato
- Tabella del piano di campionamento popolata dai punti indicati
- Precaricamento dei dati anagrafici e dei punti di prelievo storici da ArcLab
- Generazione massiva in secondo piano con avanzamento interrogabile
- Registro dei documenti prodotti, con esito e operatore
- Funzione di ispezione dei modelli per verificare la corrispondenza dei campi

### Correzioni post-collaudo — Luglio 2026

- Risolto l'errore 500 su «Da Validare» e «Ricerca campioni»: la causa era
  una query EF Core non traducibile in `ServizioAnalisi.ArchivioAsync`
  (`GroupBy(...).Select(g => g.First())` con proprietà di navigazione).
  Riscritta selezionando la riga con `IdAnalisi` massimo per campione.
  Non era, come ipotizzato, un problema di versione SQL Server o di `STRING_AGG`.
- Sostituiti i due usi di `STRING_AGG` in `RepositoryCampioni.cs` con
  `FOR XML PATH`: uno per indipendenza dalla versione, l'altro perché
  `STRING_AGG(DISTINCT ...)` è sintassi non valida su ogni versione.
- «Da Validare» ora esclude anche i campioni con `AnalisiChiusa` impostata:
  aggiunto `ISNULL(ca.AnalisiChiusa, 0) = 0` alla query.
- Etichette di stato in Consultazione arricchite con icona (Certificato / In corso).
- I valori di legionella ora compaiono nei certificati. Scoperto che la costante
  `IdRigaMetodoDaEscludere = 18963`, documentata come «riga tecnica», corrisponde
  in realtà alla determinazione di legionella (IdMetodo 5381). `CompletoAsync`
  accetta ora `includiProveEscluse`: il certificato la richiede, mentre analisi AI
  e «Da Validare» mantengono l'esclusione. Formattazione con `>`/`<` già gestita
  da `ProvaCampione.ValoreFormattato`.

### Correzioni — Agosto 2026

- Taratura incubatori/celle/frigoriferi (Modulo 04-02): l'incertezza del campione
  di riferimento u(CR) è ora **interpolata** linearmente tra i due punti di
  certificato che racchiudono la temperatura di esercizio (prima: valore del punto
  più vicino), coerente con la correzione già interpolata. Rilevato confrontando la
  taratura di un frigorifero (esercizio 5 °C) con il foglio storico: al punto più
  vicino il motore usava l'incertezza del solo punto a 0 °C. Il set di riferimento
  di validazione resta uniforme a 0,06 come il foglio storico, quindi i casi VAL e
  i valori attesi non cambiano (validazione superata). Il Piano di Validazione
  Calcolo Incubatori va aggiornato (u(CR) interpolata) e ri-approvato dal RSQ.

### Fase 6 — DVR Piscine (integrazione componente)

- Componente DVR Piscine integrato nella soluzione: precompilazione da ArcLab
  (sola lettura), salvataggio della scheda sul database proprio con payload JSON
  (stile `AnalisiAi`), generazione del documento Word dal record salvato.
- Base normativa reale (in vigore): Accordo Stato-Regioni 16/1/2003, Tabella A;
  frequenze UNI 10637; metodi ISTISAN 07/11 (A.C. 2576 = aggiornamento futuro).
- Aggancio ArcLab alle analisi piscina già svolte tramite le matrici 213/214
  (`acqua di piscina - in vasca` / `- immissione in vasca`), con join
  `Campione → Lotto → commessa → Insediamento`, cliente via `Cod_Cliente`.
- `RepositoryPiscine` su Dapper e schema reale; entità `DvrPiscina` con migrazione
  `AggiuntaDvrPiscine`; permessi `piscine.genera` / `piscine.registro`;
  `AreaFunzionale.Piscine`; controller, vista, modulo ES, navigazione, registro.
- Scope del DVR: tecnico-sanitario sulla piscina, con rimando al DVR aziendale
  generale ex art. 28 d.lgs. 81/2008.
- Prossime estensioni: tabelle editabili parametri/punti/scadenzario con standard
  Tabella A riutilizzabile; rischio residuo calcolato dalle risposte.

### Manuale di autocontrollo piscine (in corso — dato unico, output multipli)

- Impostazione "documenti separati, dato unico": la stessa `SchedaPiscina`
  alimenterà DVR, Manuale di autocontrollo (ER) / Registro tecnico (Marche) e
  Regolamento interno. Estensione dei campi via payload JSON, senza migrazioni DB.
- Grado 1 — anagrafica estesa e classificazione: codice fiscale, sede legale e
  stagionalità (da ArcLab), ATECO, attività, periodo, gestore, anno di costruzione,
  categoria A/B/C, gruppo, autorizzazioni sanitaria/scarico, fonte di
  approvvigionamento; figure responsabile sicurezza bagnanti e assistente ai bagnanti.
- Grado 2 — impianti tecnologici per vasca: `VascaPiscina` estesa (forma, struttura,
  rivestimento, pompe, filtri/prefiltri, immissione/ripresa, ricircolo, riscaldamento,
  ricambio, sistema di dosaggio, principio attivo, sistema automatico di analisi);
  UI vasche riorganizzata a schede (card) per vasca.
- Prossimo passo: modelli Word tag-based (Manuale ER, Registro Marche, Regolamento)
  ottenuti "tag-izzando" i modelli reali esistenti, e pulsanti di generazione
  dedicati con gli stessi criteri di salvataggio (download + archivio + percorso).
- Variabili individuate e classificate in `docs/../Dizionario_Variabili_Manuale_Piscine.md`
  (nel repo di lavoro Piscine).

### Regolamento interno piscina (completato)

- Modello Word ottenuto "tag-izzando" il documento reale di MARE.A: loghi e
  formattazione preservati, valori sostituiti da segnaposto testuali.
- Generatore a sostituzione token (robusto ai run di Word), con archiviazione e
  nome file coerenti col DVR (download + copia in archivio + percorso mostrato).
- Conversione automatica della profondità in piedi (ft) a partire dai metri/cm.
- Righe dell'idromassaggio condizionate: compaiono solo se esiste una vasca di
  tipo "Idromassaggio" (campo Tipo reso menu a tendina nella card vasca).
- Endpoint `genera-regolamento`, pulsante dedicato, campi orario e ubicazione servizi.

### Manuale di autocontrollo — infrastruttura + strato 1 (intestazione ER)

- `OpzioniPiscine` estesa con i modelli del manuale per regione
  (`ManualeModelloEmiliaRomagna` / `ManualeModelloMarche`); il generatore sceglie
  il modello dalla regione dell'insediamento, preferendo quello presente su disco
  (fallback su ER finché il modello Marche non è pronto).
- `IGeneratorePiscine.GeneraManuale` + `GeneratorePiscine.GeneraManuale`
  (sostituzione token come il Regolamento) + `MappaManuale` (strato 1).
- Endpoint `api/piscine/genera-manuale/{id}`, pulsante "Genera Manuale" nella UI.
- Modello ER `Manuale_ER_Modello.docx` ottenuto tag-izzando il manuale reale
  (Hotel Love Boat): intestazione/dati generali, figure di responsabilità,
  classificazione e autorizzazioni, più il nome nell'intestazione di pagina.
  Verificato: zero residui del cliente, tutti i token si popolano.
- Strati successivi: blocco impianti per vasca, tabelle parametri/punti/
  scadenzario, variante Marche (Registro tecnico) e piano di autocontrollo.

### Manuale ER — correzioni da revisione (copertina, header, figure)

- Il generatore ora sostituisce i token anche in intestazioni e piè di pagina
  (prima solo nel corpo): la ragione sociale compare nell'intestazione di pagina.
- Copertina: tag-izzate le caselle di testo (nome + indirizzo + comune) che
  python-docx non vede; il corpo (comprese le textbox) è già coperto dal generatore.
- Figure di responsabilità su una sola riga con il nome intero, senza distinzione
  Nome/Cognome (coerente con la raccolta dati). «Datore di lavoro» è un campo
  dedicato (non più derivato dal gestore).
- Tolta l'«Edizione» (resta la revisione), sia in tabella sia nell'intestazione.
- Nuovo campo «Data dichiarazione responsabile» riportato nella dichiarazione.
- Corretto il rientro dal Registro: le analisi degli ultimi 5 anni vengono
  rilette da ArcLab all'apertura (non erano salvate nella scheda).

### Manuale ER — strato 2a: sezione vasche (DESCRIZIONE)

- Meccanismo per vasca: token distinti per vasca principale (`vp_`) e
  idromassaggio (`vi_`); il generatore assegna la prima vasca non-idro a `vp_` e
  la vasca idromassaggio a `vi_`.
- I campi tecnici del manuale erano menu a tendina di Word (content control a
  livello di cella): portati nella raccolta dati con le identiche opzioni.
  Card vasca estesa con Forma, Superficie, Variazione batimetrica, Attrezzature
  accessorie (a tendina), oltre a volume/profondità già presenti.
- Modello ER: blocchi DESCRIZIONE di vasca principale e idromassaggio tag-izzati
  (forma, superficie, volume, profondità, variazione, attrezzature).
- Corretta la tabella autorizzazione in «Caratteristiche tecniche»: la data ora
  è accanto a «del» (prima era in alto e restava «N.D.» letterale).
- Restano per lo strato 2b: impianto tecnico per vasca (pompe, filtri,
  riscaldamento, °T, ricircolo, ricambio…), circolazione (bocchette, sfioratoi,
  vasca di compenso), capienza, e la rimozione del blocco idromassaggio quando
  la vasca non è presente.

### Manuale ER — strato 2b (1/n): caratteristiche tecniche e vigilanza (pag. 31-32)

- Criterio confermato: ogni menù a tendina di Word diventa un campo della raccolta
  dati con le stesse opzioni (campo libero se non c'è tendina); i content control
  vengono scartati, il manuale resta solo testo.
- Nuovi campi (fieldset «Caratteristiche tecniche funzionali e vigilanza»):
  tipologia strutturale (1-4), tipo utilizzazione (A-F), contatore immissione,
  registrazione ingressi, caratteristica vasca (superficie e profondità),
  lati del bordo liberi, vigilanza bagnanti. Fonte di approvvigionamento ora
  riportata nel manuale.
- Rimossa la riga «N° di assistenti bagnanti» dalla capienza (ridondante con la
  sezione dedicata).
- Restano su pag. 32: sezione «Assistente bagnanti» (togliere il primo punto,
  scelte a tendina) e «Istruttori di nuoto» (aggiungere l'opzione «Sono presenti»);
  poi impianto tecnico vasca (37), circolazione (38) con cubatura vasca di
  compenso, idromassaggio (39) con la regola < 4 m³, e HACCP pH (44).

### Manuale ER — tag-izzazione completa (strati 2-3)

- Tutte le scelte a tendina di Word del manuale ER sono state spostate nella
  raccolta dati con le stesse opzioni; il documento generato contiene solo testo
  (nessun content control residuo). Coperto: caratteristiche tecniche e vigilanza,
  assistente bagnanti/istruttori, sezione vasche (descrizione, impianto tecnico,
  circolazione) per vasca principale e idromassaggio, HACCP (pericolo/azione/
  monitoraggio), sostanze chimiche, servizi igienici, docce, vaschette lavapiedi,
  pronto soccorso, accessi.
- Regola UNI 10637 sull'idromassaggio (< 4 m³ → sezione rimossa + frase rossa
  Montserrat 10 con spaziatura; ≥ 4 m³ → premessa rossa; assente → rimossa).
- Matrice pulizie: operazioni rese testo fisso, «addetto» come campo unico.
- Rimossi ~240 paragrafi vuoti che generavano pagine bianche.
- Correzioni codifica (`&lt;` → `<`) e grassetto sui valori di caratteristica vasca.
- Resta la variante Marche (Registro tecnico) sullo stesso impianto a token.

### Manuale ER — correzione «il file non si apre in Word»

- Causa: sostituendo alcuni content control **inline** era stato inserito un
  `<w:p>` dentro un altro `<w:p>` (paragrafi annidati, invalidi in OOXML). Word
  rifiuta di aprire; LibreOffice li tollera, per questo i controlli strutturali
  standard non lo evidenziavano.
- Modello ripulito (0 paragrafi annidati). Aggiunta rete di sicurezza nel
  generatore (`CorreggiParagrafiAnnidati`) che li corregge a ogni generazione.
- Rimozione sezione idromassaggio: eliminate anche le voci d'indice (PAGEREF)
  che restavano orfane (`RimuoviVociIndice`), altra causa di rifiuto da Word.
- Nome file download reso ASCII pulito (niente punti di «S.R.L.» che
  storpiavano il Content-Disposition).

### Variante Marche — Registro tecnico funzionale (DGR 1431/2013)

- Modello Sporting tag-izzato con gli stessi criteri dell'ER: intestazione,
  anagrafica, gestore/figure, classificazione, capienza, caratteristiche e
  impianto vasca, sostanze chimiche, ditte di manutenzione. Zero dati del
  cliente originale residui; documento solo testo.
- Campi condivisi con l'ER riusano gli stessi token (si compilano dalla scheda
  esistente). Campi specifici marchigiani (soglie 240 m²/160 cm, posti letto,
  immissione/ripresa, curva caratteristica, neutralizzante, sistema automatico,
  ditte manutenzione) hanno token dedicati `mar_`/`ditta_`, nuovi campi nel
  modello e nel form (fieldset «Dati specifici Marche»), e mappatura dedicata.
- Categoria in versione marchigiana; il generatore sceglie il modello dalla
  regione dell'insediamento. Tutte le tendine Word portate nella raccolta dati.

### Fase 7 — Prefatturazione (Tamponi in contratto, Non fatturare)

- **Tamponi in contratto**: traduzione in T-SQL (sola lettura) della logica di
  prefatturazione dei tamponi. Individua i tamponi fatturabili nel periodo con gli
  stessi criteri di ArcLab (scadenza incasso, non-fatturare, certificato non
  fatturato, vettore presente, campione non già fatturato) e aggancia, per
  insediamento, il contratto di consulenza HACCP con il n° tamponi inclusi.
  IdControllo 180/203/2513 con filtro controlli componibile (spunte + campo
  opzionale). Riepilogo per insediamento + dettaglio; Excel su due fogli.
- **Non fatturare**: campioni con `commessa.NonFatturare` attivo nel periodo.
  Doppio riepilogo per prelevatore e per ricevitore, ciascuno con riga «Totale
  generale»; entrambe le tabelle cliccabili per filtrare il dettaglio. Dettaglio
  con `commessa.PrezzoControllo` e flag contratto HACCP. Excel con colonna e foglio
  ricevitore, oltre a quello prelevatore.
- Permessi dedicati `prefatturato.tamponi` e `prefatturato.nonfatturare`; nuove
  aree di navigazione; controller, viste e moduli ES separati.

### Fase 7 — Estensioni su schede esistenti

- **Certificati emessi**: sotto ogni prova ora compaiono esecutore, numero del
  laboratorio esterno (`LabEsterno.NumeroAccredia`) e accreditamento del metodo
  (MARE.A `Metodo.Accreditato` / esterno via `labEstMetodo.AccrLabEsterno`,
  letto con `EXISTS`).
- **Laboratori Esterni**: aggiunto il filtro per numero lotto e l'indicazione
  «accreditato esterno» a video (badge) e in Excel (colonna dedicata).
- **Consultazione**: filtri prelevatore e ricevitore; colonne prezzo, IdControllo
  e prelevatore; tabella per singola prova negli identificativi tecnici
  (accreditamento interno/esterno, IdProva, IdRigaMetodo), attivabile sia dalla
  maschera sia cliccando il campione nell'elenco.
- **Totali delle card sempre completi** su tutte le schede: dove il dettaglio a
  video è limitato, i totali sono calcolati lato server sull'intera selezione
  (Consultazione: `TotaliRicercaAsync`) o sull'estrazione completa.
- **IdControllo** mostrato accanto al nome del controllo (Tamponi, Non fatturare)
  e messaggio «Elenco troncato» arricchito con il numero di righe mostrate.
- **Launcher desktop** (`Avvia_LaboratorioAI.bat` / `.ps1`): avvia il server se
  non è già attivo (sonda `/stato`) e apre `http://localhost:5080` nel browser.

### Fase 8 — Strumenti (tarature) e Monitoraggio TempLog (agosto 2026)

- **Nutrizionali**: archivio materie prime (import USDA/CREA/CNF, import AI da
  scheda tecnica PDF), Ricette con calcolo in tempo reale, rapporto, verifica AI.
- **Taratura termometri / incubatori / mappatura / bilance** (moduli 04-01,
  04-02, 04-03, 45): motori di calcolo validati contro i fogli storici con test
  xUnit, rapporti PDF/Word, validazione del calcolo con registro e avviso di
  scadenza, analisi AI anti-errore archivio-first, funzione «duplica».
- **Bilance**: budget EURAMET cg-18, pesiere condivise, rapporto a due pagine con
  grafico e note esplicative; termometro di riferimento selezionabile da ArcLab.
- **Piani di validazione delle formule** (docx) per bilance e mappatura, in
  `docs/Validazione`, con formule allineate al codice.
- **Monitoraggio TempLog Econorma**: lettura del file notturno `.xls`
  (ExcelDataReader), limiti di taratura letti da ArcLab in sola lettura tramite
  mappatura canale→sonda→`PianoInterventi` TARATURA; evidenziazione escursioni
  rosso/blu; tabella «valori per orario»; monitor con grafici interattivi per
  canale (tooltip con ora, valore e limite superato); report escursioni Excel/PDF;
  export sonde ArcLab con data ultima sincronizzazione; **validazione umana**
  (Conforme/Dubbio/Non conforme) singola e multipla con registro. I dati non
  vengono mai alterati (a differenza della macro Econorma originale).
- **Menù per macroaree**: Laboratorio, Manualistica, Nutrizionali, Strumenti.
- Nuovi schemi SQL: `nutrizionale`, `taratura`, `incubatori`, `mappatura`,
  `bilance`, `templog`. Nuovi permessi: `templog.esegui`, `templog.mappatura`,
  `templog.validazione` (oltre a quelli dei moduli strumenti).

### Fase 9 — Scadenziario tarature ed etichette (agosto 2026)

- **Scadenziario tarature strumenti interni** (sola lettura ArcLab): piani
  `IdAttività` 25/26, ultima taratura da `MAX(SchedeIntervento.DataInt)` per piano,
  scadenza = ultima + `FreqGiorni`, apparecchiature in uso; raggruppamento per tipo
  (Termometri; Incubatori/Termostati/Stufe; Frigoriferi/Congelatori; Altro).
- **Archivio termometri clienti (esterni)**: import dal foglio «2011-2026»
  (ExcelDataReader), solo tarature correnti (righe RINNOVATO/DISMESSO escluse);
  flag `Fatturato` dalla colonna «Fatt», filtro «da fatturare», selezione cliente
  da ArcLab, pulsante «svuota archivio importato». Le tarature dei termometri
  cliente eseguite nel modulo Taratura confluiscono nell'elenco esterni.
- **Etichette termometro** generate dal modulo Taratura: Excel nel formato del
  blocchetto storico (foglio 2020), con sfondo colorato ciclico per anno di
  taratura (rosso mattone, azzurro, blu, verde, arancione, viola) e testo a
  contrasto; date scritte come testo per evitare il troncamento.
- Rinominato il modulo TempLog in **«Monitoraggio Econorma»** (intestazione
  «Visore Dati Econorma»); allineati i nomi dei file esportati.
- Nuovo schema SQL `tarature` (tabella `TermometriEsterni`). Nuovi permessi:
  `scadenziario.visualizza`, `scadenziario.gestione`.

### Fase 10 — Accettazione, Proposta controlli, esterni non ricertificati (agosto 2026)

- **Accettazione** (schema `app`, staging): maschera ottimizzata per smartphone con
  sezioni «accessorie» apribili (registro accettazioni, verbale, dati facoltativi);
  insediamento con città, commesse per insediamento con prezzo, prelevatori/ricevitori
  attivi, tecniche/imballaggi, esploso del controllo, procedura automatica dalla
  matrice, laboratorio esterno automatico, luogo prelievo. **Verbale di prelievo
  con numero interno `VP-NNNNNN`** (contatore atomico `UPDLOCK, HOLDLOCK`,
  modificabile). Data/ora ricezione attribuita alla sincronizzazione (non
  compilabile). PDF: richiesta analisi e catena di custodia (lab esterno),
  etichetta campione/lotto e scheda ricezione (campione). Verifica di importabilità.
  La generazione del `CodLotto` e l'import in ArcLab **restano a Palomar**: nota di
  mappatura XML→tabelle consegnata (`docs/Accettazione/`).
- **Certificati esterni Non Ricertificati** («Solo esterni»): confronto tra i
  certificati esterni presenti sul portale (`[Marea].dbo.Entries` via `IDCliente`
  → `Clienti`) e i certificati interni; elenca i campioni con certificato esterno
  ma senza ricertificazione MARE.A (originale spedito), con stato di fatturazione;
  export Excel. Schema `app`, `AreaFunzionale.SoloEsterni = 18`,
  permesso `soloesterni.visualizza`.
- **Proposta controlli** (schema `app`, staging): catalogo dei metodi attivi
  (`ProceduraMetodo.Attivo` ↔ `metodo`), prezzo sommato automaticamente, regola del
  **lab esterno unico**, verifica **duplicati** (stesso insieme distinto di metodi)
  con popup, **proposta del nome via AI** con le sigle di `Sigla_Metodo`, **modifica
  di un controllo esistente** (ricerca per nome o `IdControllo`, precarica le righe)
  con guardia server-side sul riutilizzo del nome, validazione di `Arrotonda`
  (cifre significative) e `MaxDecimali`. Genera **XML per import supervisionato**:
  nessuna scrittura diretta in ArcLab. `AreaFunzionale.Controlli = 19`, permessi
  `controlli.proponi` / `controlli.approva`.
- **Stato «Spedito originale»** in Consultazione e Laboratori esterni: quando il
  certificato esterno è sul portale (Marea `Entries`) ma manca la ricertificazione
  interna, lo stato diventa *Spedito originale*, con filtro dedicato. Distingue il
  ritardo (IdCertificato nullo, nessun esterno) dal spedito-l'originale (esterno
  presente e non ricertificato). Realizzato con una **CTE su Marea + LEFT JOIN**
  (`CteEstPortale`/`JoinEstPortale` in `CostantiArcLab`) per evitare il timeout
  (error −2) delle EXISTS correlate per riga; confronto cross-collation con
  `COLLATE DATABASE_DEFAULT`.
- **Schede di variabilità dei manuali** (docx): metodologia di apprendimento delle
  parti fisse/variabili/condizionali applicata a DVR Sicurezza e Manuale HACCP
  (`docs/Validazione/Modelli_Studio_Manuali/`), inviate ai consulenti per la
  calibrazione. Base per il futuro modello dati + template.
- **Contatore interno riusato** per il numero di verbale (rinuncia alla generazione
  del `CodLotto`, lasciata a Palomar).
- **Ricerca errori** (nuova pagina in area Laboratorio, sola lettura ArcLab): quattro
  verifiche diagnostiche con export Excel — doppioni di parametro sui rapporti di
  prova (selezione anni, esclusi microbiologici, eccezione valore energetico 236),
  metodi attivi senza `Cifre_significative`/`Decimali_Max`, metodi con margine
  `CostoMetodoEuro`/`CostoAnoi` sotto soglia % impostabile (o negativo), metodi doppi
  per stesso `IdParametro` + `DettagliMetodo` + `RifMetodo` (al netto di spazi e
  maiuscole/minuscole); **lotti senza ricevitore** e **senza vettore** (per anno di
  ricezione); **coerenza di categoria prelevatore/vettore** (decisa dai nomi:
  interno vs cliente/terza parte devono coincidere; se il prelevatore è cliente/terza
  parte il verbale `Lotto.BuonoPrelievo` non deve esserci); **campioni «chiusi» non
  pronti alla stampa** (`Campione.AnalisiChiusa` = Sì ma con prove senza
  `VerificaValore`, senza `DataInizioProva`/`DataFineProva`, o rigametodo senza
  `Arrotonda`/`MaxDecimali`), con ricerca per data (Lotto.DataRic) o per singolo
  lotto/campione. In cima alla pagina un **cruscotto** con spunte per eseguire i
  controlli selezionati (riepilogo con semafori e «Apri» che apre e lancia la sezione
  di dettaglio) e **«Esporta tutto»** in un unico Excel con un foglio per controllo.
  Permesso `ricercaerrori.visualizza`, area 21; nessuna nuova tabella (permesso
  popolato all'avvio).
- **Audit trail esteso a tutti i moduli operatore**: le chiamate `IServizioAudit.
  RegistraAsync` sono state aggiunte (oltre a DVR/Laboratori esterni/Tamponi già
  coperti) ad Accettazione (salva/elimina/tracciato/PDF), Proposta controlli
  (salva/elimina/XML), Certificati esterni Non Ricertificati e Non fatturare
  (export), Piscine (salva/DVR/regolamento/manuale), Taratura/Incubatori/Mappatura/
  Bilance (salva/validazione/analisi AI), Monitoraggio Econorma (validazioni/
  mappatura), Da Validare (analisi AI singola e massiva), Scadenziario (salva/
  elimina/svuota/import), Materie prime (import/estrazione AI/salva/elimina) e
  Ricette (salva/verifica AI). Ogni voce registra utente, IP, area, azione, esito,
  dettaglio JSON e versione dell'app. Aggiunta l'area funzionale `Mappatura = 20`.

### Consultazione — Esporta risultati (RDP) (agosto 2026)

- Nuovo export **«Esporta risultati (RDP)»** in Ricerca campioni: i risultati completi
  come sul rapporto di prova, **una riga per prova** (con eventuali sotto-righe
  analita), sui campioni filtrati e **solo certificati**. Traduzione in T-SQL della
  storica query Access del rapporto (`IIf`→`CASE`, `&`→`CONCAT`, `maggioredi=-1`→`<>0`),
  con `certificato` in INNER JOIN; riusa gli stessi filtri di ricerca (`ComponiFiltri`)
  e la CTE portale. Passthrough dinamico: le colonne del SELECT diventano le colonne
  del foglio (`IEsportatoreRisultatiRdp`). Colonne: n° certificato/data, cliente,
  insediamento, n° ricezione, denominazione/matrice, **punto campionato**, date e
  temperature, prelevatore/verbale; per prova: parametro/denominazione, **risultato**
  con segno, unità, limiti di fiducia, analita e relativi limiti, metodo/rif. biblio,
  **accreditamento** (asterisco), responsabile, date prova, laboratorio esterno.

### Nutrizionali — Scheda di verifica per il committente (agosto 2026)

- Il **rapporto nutrizionale** diventa anche **scheda di verifica dati**: include la
  tabella **ingredienti** (denominazione, quantità kg, %, **fonte dei valori** con
  riferimento/URL) e una **clausola di responsabilità** (`TestiRapportoNutrizionale.
  ClausolaResponsabilita`) con cui il committente, usando i dati per l'etichettatura,
  si assume la verifica della corretta trascrizione di ricetta/quantità e dell'idoneità
  delle fonti; per dubbi deve contattare il laboratorio. Obiettivo: condividere il
  rischio di errore con il cliente.
- Il rapporto è ora esportabile anche in **Excel** (oltre a PDF/Word): stessa scheda
  con ingredienti/fonti/clausola. Nuovo pulsante «Rapporto Excel» in Ricette.

### Note di schema ArcLab consolidate

- `CodLotto`/`CodCampione` sono testo: confronto con interi via `TRY_CONVERT(int, …)`.
- **Scadenziario**: «UltimiInterventi» (query Access) = `MAX(SchedeIntervento.DataInt)`
  per `IdPianoInt`; attività di taratura = `IdAttività` 25/26.
- **TempLog / Econorma**: la sonda ha in `apparecchiature.Associato1` il testo
  dell'ambiente con «Cod: <IdApparecchio>»; i limiti stanno nel `PianoInterventi`
  dell'ambiente con `NomePiano` «TARATURA» (mai «MAPPATURA»); `NonInUso` è `bit`.
- Prelevatore: `Campione.IdPrelevatore → mansione (IdAttività = 1) → Organico`.
  Ricevitore: `Lotto.IdRicevitore → mansione (IdAttività = 2) → Organico`.
- Contratto HACCP: commessa `IdTipoCommessa = 5`, `DescrIncarico LIKE
  'Gestione ed assistenza autocontrol%HACCP%'` (una «l» per il refuso
  «autocontrolo»), con prestazioni presenti; n° tamponi = `DurataPres`.
