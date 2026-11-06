# Changelog

Il formato segue [Keep a Changelog](https://keepachangelog.com/it/1.1.0/).

## Script per due postazioni, ufficio e portatile (06/11/2026)
- Il codice e' su GitHub (repository privato): si lavora con `git pull` / `git push`; vedi `LAVORARE_DA_DUE_POSTAZIONI.md`.
- Nuovi `scripts\PostazioneComune.ps1` (riconosce il PC dell'ufficio `PCMAREA01`), `scripts\Controlla-Ambiente.ps1` e `scripts\Controlla-Pubblicazione.ps1`.
- `Avvia_LaboratorioAI`, `Avvia_Portale`, `Avvia_Tracciato` (.ps1): usano la cartella in cui si trovano, impostano `ASPNETCORE_ENVIRONMENT=Development` e **non partono** se manca `appsettings.Development.json` o se `LaboratorioAI`, `ArcLab` o `Tracciabilita` puntano alla produzione. Sul portatile preimpostano la cartella Econorma locale e spengono il controllo ACCREDIA notturno.
- `Pubblica-Produzione`, `Pubblica-Portale`, `Pubblica-Tracciato` (.bat): si pubblica solo dal PC dell'ufficio e dopo aver controllato che il codice sia allineato a GitHub (altrimenti conferma scritta).
- `Ripristina-Produzione-13-10.bat` e `datapump_dati_locale_to_prod.ps1`: aggiunta una conferma scritta (possono rovinare la produzione); `pubblica_web.ps1` solo dal PC dell'ufficio.
- `Backup_Su_D.bat`, `Script-Database-Tracciato.bat`, `Crea-Collegamenti-Desktop-Tracciato.ps1`: percorso del progetto preso dalla posizione dello script; sul portatile non si crea il collegamento di pubblicazione.
- `scripts\estrai-andamento-clienti.ps1`: istanza locale `.\SQLEXPRESS` (vale su entrambi i PC).

## Manualistica - dizionario della check list dai modelli (05/11/2026)
- Nuovi `TendineModello` e `ServizioVociChecklist`: le voci delle tendine si leggono dalle revisioni approvate e popolano `man.VociChecklist` e `man.OpzioniVoce`. Pagina «Manualistica → Voci della check list»: si guarda l'anteprima, poi si scrive. Le voci scritte a mano dai consulenti non si toccano; quelle sparite dai modelli si spengono senza cancellarsi.
- **L'identita' di un campo segue il modo in cui il motore lo riempie.** Nelle lettere e negli archivi riempie per posizione (`fillPos`, chiave «TAG#n») e li' lo stesso tag ripetuto e' un campo diverso: nella scheda dell'archivio informatico «LUOGO DI CONSERVAZIONE» compare due volte, una per i dati sul PC — con «Cloud» — e una per quelli del gestionale — con «Server farm del gestionale». Dappertutto altrove riempie per tag (`fill`) e le occorrenze sono lo stesso campo, anche dentro le sedi del trattamento e gli elenchi delle misure di sicurezza, dove le righe del modello sono un prototipo che il motore duplica.
- I tre blocchi a posizione sono elencati per nome in `TendineModello`: si sanno solo leggendo il motore, e se il motore ne aggiunge uno va aggiunto li', come per il vocabolario dei marcatori. Sui 32 modelli: 85 domande con tendina e 618 voci, di cui 213 legate a un solo documento.
- Elenco delle domande nella stessa pagina: si vedono le voci di ciascuna, si assegna il passo della check list — l'unica cosa che dai modelli non si ricava — anche a piu' domande insieme, e un pulsante propone i passi mancanti dal nome del campo, da rivedere.
- `man.VociChecklist.Codice` da 60 a 80 caratteri (migrazione `CodiceVoceChecklist80`): il codice porta anche l'occorrenza.

## Manualistica - le tendine sono per modello (04/11/2026)
- `man.OpzioniVoce.IdModelloDocumento` (nullable; migrazione `OpzioniVocePerModello`): una voce di tendina vale per tutti i modelli o per uno solo.
- Motivo, verificato sui 32 modelli approvati: otto campi delle lettere di nomina hanno tendine diverse **per progetto**. Nella lettera alla web agency fra gli interessati c'e' «PROSPECT», in quella all'Experience fra i trattamenti ci sono «Parchi tematici» e «Strutture termali», nella Software house quelli informatici. Un dizionario unico avrebbe offerto a ogni lettera i trattamenti delle altre.
- Dei 148 campi dei modelli, 77 hanno una tendina e 71 sono testo libero.

## Anagrafica dei comuni (04/11/2026)
- Nuova tabella `app.Comuni` con l'elenco ISTAT: 7.904 comuni con provincia, regione, codice catastale e CAP. Serve a scrivere i luoghi sempre allo stesso modo nei documenti generati, invece di «Cattolica (RN)», «CATTOLICA» e «Cattolica (Rimini)» nello stesso pacchetto.
- I CAP stanno in `app.ComuniCap`, una riga per CAP: 8.570 righe. Con un solo CAP il campo si compila da se', con piu' di uno diventa una tendina (Rimini 4, Milano 42, Roma 82). Tabella figlia e non colonna, altrimenti Roma comparirebbe 82 volte nell'elenco dei comuni.
- Chiave vera: il codice ISTAT. Sei comuni hanno lo stesso nome in province diverse (Calliano, Castro, Peglio, Samone, San Teodoro), quindi negli elenchi il comune si mostra sempre con la sigla della provincia.
- L'elenco e' una risorsa incorporata (`Anagrafiche\comuni-istat.csv`): per aggiornarlo si sostituisce il file e si ricompila. Il popolamento si puo' rieseguire — aggiunge, corregge e spegne i soppressi, senza cancellare niente.
- Migrazione `Comuni` da creare e applicare.

## Manualistica - allineamento alla versione 23 del motore (04/11/2026)
- Dodici combinazioni di videosorveglianza e telecamera piscina, tutte ammesse (erano otto, con le altre impedite). Quando i due impianti si comportano in modo diverso la DPIA scrive una frase per impianto.
- `VocabolarioMarcatori`: nuove condizioni `VIDEO_VISIONE`, `PISCINA_VISIONE`, `VISIONE_DIRETTA`, `IMPIANTI_MISTI_REGISTRAZIONE`, `IMPIANTI_MISTI_VISIONE`; nuovi testi `DPIA_CONSERVAZIONE_IMPIANTO`, `DPIA_IMPIANTO_SOLA_VISIONE`, `DPIA_VISIONE_IMPIANTO`. Senza queste voci il controllo dei marcatori avrebbe rifiutato i modelli del 03/11.
- Trentaduesimo modello controllato `CARTA_RIEPILOGO`, con la condizione di servizio `NON_NEL_PACCHETTO`: versionato e approvato come gli altri, ma non consegnato come documento. Gli alias di riconoscimento dal nome del file vanno confrontati con il nome vero quando il file arriva.
- Confronto con il `motore.js` consegnato da Vaccarini il 04/11 (copia in `docs\Privacy\motore_v23.js`): il vocabolario è ora verificato sulla sorgente, non ricostruito. Tolta `DITTA_PIANI_RICEVE_NOMINATIVI`, che il motore non conosce: è una regola di inclusione del pacchetto, non una condizione dei modelli.
- `IMPEGNO_DITTA_PIANI` esce solo se è stata chiesta almeno una copia, come in `pacchetto()`.
- `ModelloDocumento.RichiedeMarcatori` (migrazione `ModelloRichiedeMarcatori`): distingue «questo file non ha marcatori perché non ne ha bisogno» da «questo file ha perso i controlli contenuto salvandolo». Falso per i modelli di servizio; il popolamento corregge anche le righe già inserite.
- Scelta del manuale in testa alla pagina dei modelli: la pagina non conosce più «PRIVACY» scritto nel codice, usa il primo tipo attivo e accetta `?tipo=`. Il caricamento entra nel manuale che si sta guardando, la selezione si azzera cambiando manuale, e piscine, legionellosi, HACCP e sicurezza compariranno da soli quando saranno a catalogo.
- Riconoscimento dal nome del file verificato sui 32 modelli del 04/11 (`docs\Privacy\FILE PRIVACY SLUG`): 32 su 32, nessuna ambiguità.

## Manualistica - impianto del modulo (01/11/2026)
- Nuovo schema `man`: 23 tabelle per la manualistica che i consulenti consegnano ai clienti (privacy come primo tipo; piscine, legionellosi, HACCP e sicurezza si aggiungono come dati, senza toccare lo schema).
- Modelli Word versionati nel database (`man.RevisioniModello`) con approvazione (02/11/2026: una firma, di chi ha il permesso di approvare; resta registrato chi ha caricato e chi ha approvato). Ogni documento prodotto resta legato alla revisione del modello con cui e' stato generato (`man.ManualiGeneratiRevisioni`).
- Scelta del modello per tipo, macro attivita' e regione (`CodiceAttivita`, `SiglaRegione`: null = vale per tutte).
- Check list configurabile con voci e opzioni; le voci scritte a mano possono entrare nelle tendine per le compilazioni successive e si disattivano quando sono superate, senza cancellarle.
- Nominativi dei lavoratori non richiesti e non salvati: solo qualifica e quantita'.
- Nuovi permessi `manualistica.consulta`, `manualistica.genera`, `manualistica.modelli.gestisci`, `manualistica.modelli.approva`, `manualistica.impostazioni`; nuova `AreaFunzionale.Manualistica`.
- Impostazioni in `app.Configurazioni`, documenti generati su cartella del server, pubblicazione al cliente tramite `log.RegistroDocumenti`: nessuna tabella nuova per cose che esistono gia'.
- Documenti di riferimento: `docs\Privacy\PROGETTO_Manualistica_Privacy.md`, `MODELLO_DATI_Manualistica.md`, `INVENTARIO_Motore_Manualistica.md`.
- Migrazione `Manualistica` da creare e applicare.

## Tracciato - ricette e lotti per data (25/10/2026)
- Nuove ricette con ingredienti che coprono piu categorie (matrici), con numero di lotti per fornitore.
- Pagina «Lotti da usare in un giorno»: per ogni fornitore attivo (consegne negli ultimi 90 giorni) gli ultimi lotti impegnati fino a quel giorno; esclusione manuale dei fornitori.
- Migrazione `RicetteIngredienti`: RicetteRighe.Etichetta e LottiPerFornitore, nuova tabella RicetteRigheCategorie.

## 2026-10-25 — Tracciato: matrice sul lotto
- Il lotto ha ora la propria matrice (`Lotto.IdCategoria`, nuova migrazione `LottoCategoria`): lo stesso articolo può arrivare in matrici diverse (es. farina 00 in silos o in sacchi) e la ricetta cerca i lotti per matrice. L'importazione la ricava dalla riga di Ordine; la scheda lotto la mostra.

## 2026-10-25 — Tracciato: importazione da Ordine, categorie e profili di controllo
- L'importazione legge la tabella Ordine (righe con lotto) invece di MAGAZZINO: categoria esatta da IDMatrice, scadenza dei lotti, giudizio e note di accettazione, controlli registrati (confezione, temperatura, prelievo, organolettico, certificati), unità di misura per riga.
- Le 114 matrici del vecchio programma diventano categorie; quelle con gli stessi controlli condividono un profilo di controllo (48 profili). I controlli «non richiesto» non entrano nel profilo.
- Nuovi comandi di sola lettura: `struttura`, `valori`, `controlli`. `azzera` cancella anche i profili.

## 2026-10-25 — Tracciato: consultazione lotti
- Nuove pagine per gli utenti d'azienda: elenco lotti con ricerca e filtro «solo in magazzino», scheda del lotto (arrivi e movimenti, giacenza = somma dei movimenti), rintracciabilità a monte e a valle (motore esistente). Solo lettura. Il gestore MARE.A vede un avviso, non i dati.

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

### Certificati esterni — nome per Palomar (30/09/2026)

- Nuovo formato del nome, concordato con Palomar:
  `GG-MM-AA-lotto-campione-idInsediamento-cliente-prodotto-matrice-idMatrice-EST-LAB-idLabEsterno-RdP-(AVV|STD)`.
  `idMatrice` (`Campione.IdMatrice`) e `idLabEsterno` (`rigametodo.IdLabEsterno` delle prove del
  campione, scelto per sigla del PDF o perché unico) servono a Palomar per il caricamento sul portale.
  Se non determinabili il nome contiene `IDMATRICE`/`IDLAB`: il salvataggio viene rifiutato finché
  non si sostituiscono.
- La data del nome (e della sottocartella di archivio) è la **data di ricezione** del campione,
  non più quella di emissione: l'anno coincide con quello del lotto (aggancio portale a cavallo d'anno).
  La data di emissione resta quella registrata come `DataCertificato` per l'import in ArcLab.
- Aggancio del campione ArcLab per lotto/campione vincolato al lotto ricevuto **entro la data di
  emissione** (il più vicino): evita di agganciare lo stesso numero di lotto dell'anno successivo
  (cliente sbagliato → portale sbagliato). Se la data non c'è o non torna, ripiego sul più recente con avviso.
- **Ogni `-` nel nome è un separatore** (15 campi fissi): nel testo libero (cliente, prodotto,
  matrice) trattini e barre diventano spazi, nell'RdP diventano `_` (`2604327-001` → `2604327_001`,
  solo nel nome file). Confermato con Palomar.
- **Sigle laboratori esterni in Amministrazione** (tabella `certificati.SigleLaboratoriEsterni`,
  migrazione `SigleLaboratoriEsterni`): una riga per `LabEsterno` ArcLab con sigla e parole chiave
  per riconoscerlo nel PDF. La sigla nel nome ora si prende dalla tabella per l'`idLabEsterno`
  agganciato; se manca, sigla provvisoria + avviso. Il dizionario nel codice resta solo come proposta.
- `Pubblica-Produzione.ps1`: esclusi dalla copia (e dal ripristino) solo gli `appsettings*.json`;
  prima erano esclusi tutti i `.json`, compresi `deps.json`/`runtimeconfig.json` generati dalla
  compilazione, che in produzione restavano vecchi.

### Offerte e Ordini di Accettazione allineati alle Condizioni Rev. 19 (30/09/2026)

Principio: nei documenti per il cliente nessun impegno non richiesto da legge, norma o accreditamento
(l'Offerta prevale sulle Condizioni, Art. 2.4).
- **Offerta** (Word e PDF, testi condivisi in `TestiOfferta.cs`): "Il nostro lavoro" riscritto senza
  promesse (tempi, reperibilità festiva, "valide in sede giuridica", "partner accreditati"); n. ACCREDIA
  corretto in **00729 L** (era 0947L); richiamo alle Condizioni senza "tempi di consegna"/"campionamento";
  validità coerente con l'Art. 3.3; seconda firma di **approvazione specifica** (artt. 1341-1342 c.c.)
  stampata solo con Condizioni ad articoli numerati (Rev. 19+); piè di pagina "Offerta N. … – Pagina X di Y".
- **Subappalto** (Offerta e Ordine): mai più l'id interno ArcLab del laboratorio (es. "n° 1403269539");
  frase ArcLab o nome del laboratorio + "prova in subappalto, non accreditata dal Laboratorio" (Art. 8.2).
- **Ordine di Accettazione** (Word e PDF): "Accettazione n." al posto di "Riferimento lotto n."; recapiti
  come destinatari di avvisi di cortesia; riquadri allineati al nuovo Allegato A (24 ore dalla
  pubblicazione, prezzo comunicato, autocontrollo non dichiarato, verbale di prelievo sostituito);
  allegato tecnico con **Autocontrollo** (`Campione.Escludi_statistica`, query tollerante) e **Prelievo
  eseguito da** (prelevatore ArcLab), priorità solo se urgente; prove identiche raggruppate
  ("Salmonella spp. (5 aliquote)"); piè di pagina "Mod. Acc. … – Accettazione n. … – Pagina X di Y".

### Navigazione, ricerca e verifica metodi (01/10/2026)

- **Menu e pagina principale da un'unica definizione** (`Navigazione/MenuApplicazione.cs`): il menu in
  testata e le schede della Home sono generati dalla stessa lista, quindi una pagina nuova compare in
  entrambi. Gruppi: **Laboratorio Operatori**, **Laboratorio Servizi** (Prelievo vocale, Proposta controlli,
  Preventivi), **Laboratorio Ricerche**, Manualistica, Nutrizionali,
  Strumenti, **Monitor Econorma** (con "Monitoraggio Econorma", spostato da Strumenti), Amministrazione.
  Pulsante **Home** in testata.
- **Caselle azzerabili** (`js/campi-azzerabili.js`, caricato da `_Layout`): × per svuotare il singolo
  campo nei filtri (`.griglia-filtri`, `input[type=search]`, `data-azzerabile`).
- **Consultazione**: filtro **Id controllo** (prevale sulla scelta per nome); l'elenco dei controlli mostra
  anche l'Id.
- **Verifica metodi laboratori esterni**: il nome del parametro è `Metodo.DettagliMetodo` (come nel
  Rapporto di Prova; ripiego su `Parametro.NomeParametro`), il riferimento resta `Metodo.RifBiblio`; il
  suggerimento della riga ACCREDIA usa anche il nome del parametro (peso 25%).

### Emissione massiva Ordini di Accettazione e registro di controllo Palomar (01/10/2026)

- **Registro di controllo** `log.RegistroDocumenti` (append-only, migrazione `RegistroDocumenti`): una riga
  per ogni Ordine di Accettazione emesso e ogni certificato esterno salvato, con esito
  **OK / Parziale / Errore / Non pronto / Già emesso**, messaggio, file d'origine, archivio e Palomar, e
  verifica che il file sia davvero nella cartella Palomar (esistenza + dimensione).
  Servizio `ServizioRegistroDocumenti`.
- **Ordini di Accettazione**: emissione **massiva** (lotti spuntati, oppure tutti i pronti non emessi del
  periodo); ogni PDF è archiviato e **copiato nella cartella di prelievo Palomar** (nuova impostazione
  `OrdineAccettazione.CartellaPalomar`), con stato di consegna sul lotto (Inviato / NON in Palomar) e
  pulsante **Riprova Palomar**. Riquadro con l'esito dell'ultima emissione e sezione **Registro** con filtro
  "solo problemi". Il vecchio "Genera schedulati" è sostituito (endpoint mantenuto, stessa logica).
- **Certificati esterni**: ogni salvataggio è registrato; se il file non risulta in Palomar l'originale
  resta nella cartella di arrivo (si può ritentare) e l'esito è "Parziale"; "Parziale" anche se l'originale
  non si sposta in `processati`. Sezione **Registro** con il conteggio dei PDF ancora in arrivo.
- **Mail di riepilogo** agli indirizzi di `OrdineAccettazione.EmailErrori` (ora usati anche per i certificati
  esterni) quando un'esecuzione ha errori o file non consegnati: a fine emissione massiva / "Salva tutti",
  subito per il salvataggio singolo.
- **Correzione PDF Ordine di Accettazione** (errore 500 "'Rectangle' must not be null here"): la doppia riga
  tra un campione e l'altro era un paragrafo vuoto con bordi, che PDFsharp/MigraDoc 6.1 non sa impaginare
  quando cade in fondo alla pagina. Ora è una tabella di una riga; il riquadro delle condizioni è diviso in
  una riga per blocco, così può proseguire sulla pagina successiva.

### Rapportini di consulenza — fase 1 (04/10/2026)

- Nuova pagina **Laboratorio Servizi → Rapportini di consulenza** (`/Rapportini`): il consulente compila al
  sopralluogo cliente, insediamento, data, consulente (Organico ArcLab, ricordato per utente), commessa di
  consulenza facoltativa (tutte, attive e non attive), voce di fattura (proposta dalla commessa o scelta),
  descrizione per la fattura (max 250 caratteri, con le frasi già usate), ore, luogo, importo a corpo o
  tariffa × ore, fatturazione a ore / a consuntivo / convenzione, contratto prepagato (ultimo scalo e ore
  residue).
- PDF "Rapporto di intervento di consulenza" su carta intestata; invio = copia verificata nella cartella
  Palomar (`Rapportini.CartellaPalomar`) + archivio (`Rapportini.CartellaOutput`) + registro di controllo e
  mail di riepilogo se non arriva.
- Gestione (permesso `rapportini.gestione`): contestazione, correzione dell'importo con motivo, conferma
  singola o multipla (convenzione e prepagato diventano "Rendicontato"), annullamento senza cancellazione.
- Schema `cons` (migrazione `Rapportini`): `Rapportini`, `ConsulentiUtenti`, `VociIncarico` (fasce
  ID_Tipo_Incarico → voce, con 159–183 = FON corretto e 118–119 senza voce).
- Permessi nuovi: `rapportini.compila`, `rapportini.gestione` (l'amministratore li riceve in automatico).

### Statistiche — temperatura di prelievo (05/10/2026)

- Nuova colonna **Temp. prelievo** (`Campione.TempPrelievo`) nella tabella a video e nell'Excel, subito dopo
  la data di prelievo (nell'Excel dopo il punto di prelievo). Nell'Excel il valore è un numero quando è
  leggibile come tale (anche con la virgola), altrimenti resta il testo registrato in ArcLab.

### Rapportini di consulenza — modello MOD. CF 05 e documentazione di riferimento (05/10/2026)

- PDF allineato al modello approvato: Rif. offerta/contratto (dalla commessa ArcLab se c'è, altrimenti scritto
  dal consulente), Rif. delega operativa (solo dal registro per cliente), **Attività svolta** puntuale (campo
  nuovo, distinto dalla descrizione breve per la fattura; minimo 80 caratteri per l'invio), **Documentazione di
  riferimento** (documenti esaminati, modificati/emessi con consegna in bozza o definitiva, fonte delle
  informazioni, piano di autocontrollo analitico, modalità di consegna con la data di pubblicazione),
  **Esito e responsabilità**, follow-up facoltativo, richiamo alle Condizioni MOD. CF 01. Luogo "da remoto".
- Controllo della nota d'uso 3: se un documento che "può richiamare il Laboratorio" è modificato o emesso,
  l'invio richiede la verifica esplicita del consulente.
- Nuova pagina **Amministrazione → Consulenze: documenti e deleghe**: elenco dei documenti tipo (con i flag
  "può richiamare il Laboratorio" e "piano analitico") e registro delle deleghe operative per cliente.
- Testi fissi del rapporto modificabili in **Impostazioni documenti** (campi su più righe).
- Migrazione `RapportiniDocumentazione`: tabelle `cons.RapportiniDocumenti`, `cons.DocumentiTipo` (6 documenti
  iniziali), `cons.DelegheClienti` e nuovi campi in `cons.Rapportini`. Le righe documenti sostituite da una
  modifica restano disattivate (nessuna cancellazione fisica).

### Deleghe operative per i consulenti (06/10/2026)

- Generazione della **delega operativa** dal modello Word (`wwwroot/docx/Delega_operativa_modello.docx`,
  rev. 2 del modello MARE.A, con segnaposto `{{…}}`; note d'uso e nota legale tolte dal documento per il
  cliente) in **Word e PDF**. Il testo vive solo nel modello: per cambiarlo si modifica il file (percorso
  alternativo in Impostazioni → `Deleghe.ModelloWord`).
- Dal rapportino ("Genera delega" quando manca) oppure da **Amministrazione → Consulenze: documenti e
  deleghe** scegliendo il cliente. Solo clienti con una **commessa di consulenza attiva** in ArcLab.
- Dati da ArcLab: ragione sociale, P.IVA, codice fiscale, sede legale, sede operativa (insediamento),
  consulente. Legale rappresentante scritto a mano e salvato sulla delega (riproposto alla successiva).
  Qualifica e iscrizione all'Albo del consulente salvate una volta in amministrazione (`cons.ConsulentiDati`).
- Registro per **cliente e consulente**: la delega nasce "Da firmare"; con "Registra firma" (data e copia
  firmata facoltativa) diventa in essere. Nei rapportini compare solo la delega in essere del consulente del
  rapportino. Firma per MARE.A da Impostazioni (`Deleghe.LegaleRappresentanteMarea`).
- Migrazione `DelegheOperative`.
- **Deleghe su più sedi:** la delega vale per le sedi operative scelte (una riga "Sede operativa" per
  ciascuna nel documento) oppure per **tutte le sedi del cliente**, anche future. Nei rapportini compare solo
  se vale per la sede del rapportino. Tabella `cons.DelegheInsediamenti` (migrazione `DelegheSedi`).
- **Carta intestata** MARE.A sulla delega: nel modello Word (intestazione e piè dalla carta intestata) e nel
  PDF (come Ordini e rapportini). Tolto il colore verde (e il rosso residuo) dal testo del modello.


### Preventivi consulenza — analisi di laboratorio nei pacchetti (06/10/2026)

- **Pacchetti con controlli ArcLab:** ogni pacchetto di settore ha ora una sezione «Analisi di laboratorio» con
  controlli ArcLab ben definiti (non più «analisi alimento» generiche), cercati per nome o ID, con parametri,
  metodi, accreditamento e listino visibili. Più controlli si possono raggruppare in una riga con nome
  commerciale e prezzo unico (ID sempre tracciati) e poi separare di nuovo. Tabella `cons.AnalisiPacchettiConsulenza`.
- Per ogni analisi: **compresa nel canone** (prezzo non stampato) oppure **a pagamento** (prezzo indicato,
  fatturata a esecuzione, vale anche come «per conoscenza»); **analisi all'anno** facoltative; prezzo
  facoltativo, altrimenti come nei preventivi analisi (prezzo di commessa del cliente, poi listino).
- **Duplica pacchetto**, ultima modifica e autore nell'elenco; le vecchie righe di analisi generiche sono
  segnalate «da sostituire».
- **Nel preventivo:** il pacchetto porta anche le analisi, con copia dei metodi da ArcLab (servizio dei
  preventivi analisi riusato). Si aggiungono, tolgono e raggruppano controlli; scegliendo il cliente i prezzi
  si aggiornano a quelli di commessa; pulsante «Ricalcola i prezzi da ArcLab». Tabelle
  `cons.AnalisiPreventiviConsulenza` e `cons.MetodiAnalisiConsulenza`.
- **Nel contratto Word:** sezione «Analisi di laboratorio» con tabella denominazione / metodo / accreditamento
  come nell'offerta analisi, simboli e frase ACCREDIA per il subappalto, prezzo solo per quelle a pagamento e
  stima annua nelle condizioni economiche.
- **Dati per ArcLab:** tabella delle commesse di laboratorio (IDControllo, PrezzoControllo; 0 se compresa nel canone).
- **Excel di revisione per la riunione:** dall'elenco pacchetti si scarica un Excel con le analisi generiche e i
  controlli ArcLab possibili; si scrive l'ID scelto e si reimporta (niente doppioni, riga generica tolta).
- Tasto Invio nei moduli di preventivi e pacchetti: salva (o aggiunge il controllo cercato) invece di premere
  il primo pulsante del modulo.
- Migrazione `AnalisiPacchettiConsulenza`.

### Rapporto di intervento — correzioni dopo la prova (06/10/2026)

- Il rapporto sta in **una pagina**: caratteri e spazi più compatti, «Voce in fattura» su tutta la larghezza del
  riepilogo (con la modalità), riepilogo che non si spezza fra due pagine.
- **Rif. delega operativa** sempre presente nel rapporto; senza delega: «nessuna delega operativa in essere».
- Testo «Decisioni del Cliente»: aggiunto «salvo specifico incarico o autorizzazione scritta del Cliente»
  (coerenza con le trasmissioni all'ULSS su autorizzazione, es. clienti Chioggia).
- Nel modulo: indicatore **Delega operativa firmata SÌ/NO** per cliente e consulente; promemoria sotto la voce
  in fattura (deve corrispondere all'attività svolta; le frasi proposte vengono dai rapportini precedenti).
- Corretto errore 500 sulle frasi proposte per la voce in fattura quando cliente o consulente non sono ancora
  scelti (COUNT su NULL, errore SQL 8117).

### Preventivi consulenza — riferimento commerciale e responsabile cliente (06/10/2026)

- Nel contratto, sotto il consulente di riferimento: **Riferimento commerciale** (solo se il commerciale è indicato)
  e **Responsabile cliente** (solo se presente).
- Il responsabile cliente si ricava dalla **zona dell'anagrafica AZW** (ANACF.ZONA, ora letta dall'importazione
  AZW) tramite la nuova tabella delle sigle in **Amministrazione → Responsabili clienti** (`cons.ResponsabiliCliente`:
  sigla, nome, e-mail, attiva), con il pulsante «Aggiungi le sigle presenti in AZW». Nel preventivo resta modificabile.
- Migrazione `ResponsabiliClienteConsulenza` (nuova tabella, colonna `Zona` in `provv.AzwClienti`, colonne
  `CommercialeNome`, `ResponsabileCliente`, `ZonaCliente` in `cons.PreventiviConsulenza`).

### Preventivi consulenza — contratto in PDF (06/10/2026)

- Pulsante «Scarica contratto (PDF)» accanto al Word: stesso contenuto, su carta intestata MARE.A.
- La conversione Word → PDF delle deleghe è ora un componente comune (`ConvertitoreWordPdf`), con sfondi di
  paragrafi e celle; usato da deleghe e contratti di consulenza.

### Provvigioni Carloni — accordo dell'08/01/2025 (07/10/2026)

- Nuove voci per gli accordi a convenzioni: «Contratti occasionali (spot), 1° anno» e «Contratti di analisi, 1° anno».
  Il primo anno parte dalla prima fattura del cliente (con importi nella base della voce) emessa dalla data della commessa.
- Occasionali = commesse di consulenza (tipo 5) con rapporto occasionale e il riferimento del commerciale; non si
  applicano se la fattura rientra in una convenzione.
- Analisi = commesse di laboratorio (tipo 3) con il riferimento del commerciale: l'importazione ora legge il
  riferimento anche sulle commesse di analisi. Non entrano nelle convenzioni né nella voce extra laboratorio di Ludo.
- Le due voci si aggiungono allo 0% agli accordi esistenti; la percentuale si imposta dalla pagina dell'accordo.
- Test del motore aggiornati.

### Clienti da seguire (07/10/2026)

- Nuova pagina «Clienti da seguire» (Laboratorio · Ricerche): clienti con fatturato di analisi in calo (ultimi 12 mesi interi
  contro i 12 precedenti) con semaforo e motivi, sezione Legionella (perso, in ritardo, in calo, da ricordare, nuovo),
  clienti che comprano solo consulenza, elenco «da chiamare». Esportazione Excel.
- Scheda cliente: grafici mensili (SVG, senza librerie) di fatturato e campioni, analisi per controllo con quelle perse,
  registro delle azioni del commerciale (con prossimo contatto e annullamento), proposta di assegnazione, spunta «intermediario».
- Assegnazioni (proposte dal commerciale, approvate da chi ha `clienti.gestisci`) e soglie modificabili in Amministrazione.
- L'importazione AZW copia anche il riepilogo mensile dei campioni ArcLab (sola lettura) in `clienti.CampioniMensiliArcLab`.
- Schema nuovo `clienti` (migrazione `ClientiDaSeguire`); permessi `clienti.seguire` e `clienti.gestisci`.
- Motore `MotoreAndamentoClienti` (Core) con test; verificato sui dati reali: stessi risultati del prototipo Excel.
- (stessa sera) Periodi selezionabili: ultimi 12, 6 o 3 mesi o da inizio anno, confrontati con lo stesso periodo dell'anno
  prima o di due anni prima; soglie in proporzione alla durata. Scheda cliente su 36 mesi con i mesi confrontati in evidenza.
  Pulsante «Importa AZW ora» nella pagina (con `clienti.gestisci`) e avviso se mancano i campioni ArcLab.

### Monitor Econorma nel registro attività (08/10/2026)

- I controller del Monitor (area MonitorTemperature) ora scrivono nel registro attività (`log.Audit`, area 24):
  `monitortemperature.esportazione.dashboard|storico|monitor|differenze|grafici|controllo` (con filtri e righe esportate)
  e `monitortemperature.sonde.sincronizza` (sonde lette da ArcLab, inserite, aggiornate). Le consultazioni non si registrano.
- Area «Sicurezza» per accessi e uscite riservata al numero 29 (il 28 è «Clienti»).
- «Sincronizza mappatura da ArcLab» (Sonde) è ora un pulsante con conferma (POST) invece di un collegamento: non parte più per errore.
- Pagina Sonde: colonne «Seriale in LabAI» e «Codice Econorma» accanto al seriale ArcLab; in rosso i seriali diversi, in giallo
  i seriali usati da più sonde in ArcLab, avviso con il riepilogo e le sonde non ancora mappate.
- Menu Monitor Econorma: nuova voce «Sonde e sincronizzazione» (/MonitorTemperature/Sonde).

### Nuova home a cartelle (08/10/2026)

- Menu e pagina principale riorganizzati in dieci cartelle per mestiere (Ciclo analitico, Ricerche e statistiche,
  Commerciale, Consulenza, Legionella e piscine, Nutrizionale, Strumenti e tarature, Monitor Econorma,
  Amministrazione e fatturazione, Impostazioni e controllo). Tutte le 48 funzioni restano, con gli stessi permessi e indirizzi.
- Home: cartelle con icona; il clic apre una finestra con le funzioni (collegamenti normali). «Preventivi» → «Preventivi analisi».
- Contatori, ricerca delle funzioni e preferiti: passo successivo.

## 08/10/2026 — Home: contatori «cose da fare» (passo 2a)
- Nuovo `Web\Navigazione\ContatoriHome.cs` (ServizioContatoriHome) e `GET api/home/contatori` (HomeController): numeri calcolati solo per le voci visibili all'utente, in parallelo, max 10 s ciascuno, tenuti in memoria 2 minuti; se una fonte non risponde quel numero non compare (avviso nel log).
- Contatori: Da validare (campioni da validare), Ordini di accettazione (lotti pronti ultimi 7 giorni con ordine da emettere o non consegnato), Certificati esterni (PDF nella cartella di arrivo), Clienti da seguire (promemoria di oggi o in ritardo), Proposta controlli (da approvare), Rapportini (inviati o contestati; solo i propri per chi non li gestisce), Scadenziario (tarature scadute o entro 30 giorni), Sonde (seriali da verificare), Assegnazioni clienti (da approvare).
- Query di solo conteggio aggiunte: `IRepositoryCampioni.ContaDaValidareAsync`, `IRepositoryOrdineAccettazione.LottiProntiAsync` (ArcLab sola lettura).
- Home: bollino rosso sulla cartella (somma) ed etichetta sulla funzione; `home.js` chiede i numeri dopo l'apertura della pagina. `AddMemoryCache` in Program.cs.

## 08/10/2026 — Home: preferiti (stellina) e soglia Legionella «in calo» separata
- Preferiti: stellina accanto a ogni funzione nelle finestre della home; riga «Preferiti» in cima alla home, con i contatori. Nuova tabella `app.PreferitiUtenti` (IdUtente, Url, Ordine, Attivo; togliere la stella disattiva la riga, nessuna cancellazione). `POST api/home/preferiti` accetta solo funzioni che l'utente vede.
- Clienti da seguire: nuova soglia `LegionellaSogliaCalo` (default −20%, come finora la soglia del rosso) in «Soglie»; il motore la usa per lo stato Legionella «in calo». Test `Legionella_InCalo_UsaLaSogliaPropria`.
- Serve la migrazione `PreferitiESogliaLegionella` (comando in chat).

## 08/10/2026 — Accreditamento esterno per laboratorio esecutore
- «Accreditato esterno» ora vale solo se il metodo è accreditato presso il laboratorio che ha eseguito la prova (`labEstMetodo.IdLabEsterno = rigametodo.IdLabEsterno`), non presso un qualsiasi laboratorio a cui è affidato lo stesso metodo. Le prove interne non risultano più «accreditato lab esterno».
- Interessa: Certificati emessi (pagina ed Excel), Laboratori Esterni (elenco ed Excel), Metodi → metodi del controllo (laboratorio e accreditamento della riga del controllo). Ricerca metodi: laboratorio scelto sempre nello stesso ordine.
- File: RepositoryCampioni.cs, RepositoryLaboratoriEsterni.cs, RepositoryMetodi.cs (sola lettura ArcLab).

## 08/10/2026 — Ordine di Accettazione: data di emissione e decorrenza delle 24 ore
- Testata: «Emesso il gg/mm/aaaa» al posto di «Stampato in data» (solo data, senza ora: scelta di Mirko).
- Verifica dell'Ordine: le 24 ore decorrono dalla pubblicazione nel Portale clienti; aggiunto «fanno fede la data e l'ora di pubblicazione registrate dal Portale».
- La revisione citata viene da Impostazioni documenti (Preventivi.RevisioneCondizioni, condivisa con le offerte): va impostata a Rev. 19 perché coincida con gli articoli richiamati.

## 08/10/2026 — Accreditamenti da sistemare (subappalti)
- Nuovo `Dati\LaboratorioAI\Accreditamenti\ServizioAnomalieAccreditamenti.cs` e `GET api/laboratori-esterni/accreditamenti/da-sistemare` (sola lettura):
  1. metodi classificati in LabAI (sulla revisione ACCREDIA confermata) in modo diverso dal flag `labEstMetodo.AccrLabEsterno` di ArcLab;
  2. laboratori con `LabEsterno.NumeroAccredia` di ArcLab diverso (o mancante) rispetto al numero monitorato (confronto sulle sole cifre);
  3. controllo ACCREDIA fermo: nessun invio dell'agente né controllo manuale nel registro da oltre 3 giorni.
- Pagina Laboratori Esterni: riquadro «Accreditamenti da sistemare»; si aggiorna dopo ogni classificazione o conferma.
- Home: contatore «da sistemare» sulla voce Laboratori Esterni (cartella Ciclo analitico). In sviluppo il controllo risulta fermo perché l'agente invia alla produzione.
- `IRepositoryMetodiLabEsterno.LaboratoriAsync` (nome e numero ACCREDIA da ArcLab, sola lettura).

- Correzione: il controllo «da sistemare» andava in errore 500 (array.Contains in una query EF, compilato come Contains su ReadOnlySpan non traducibile da EF Core 8): sostituito con un confronto esplicito. Stessa precauzione sul contatore «da spedire».

## 08/10/2026 — Prove in subappalto: mai «accreditato MARE.A»
- Per le prove eseguite da un laboratorio esterno conta solo l'accreditamento del laboratorio esterno (Condizioni Generali, Art. 8.2):
  - Certificati emessi: pagina (etichette) ed Excel (colonna 9);
  - Consultazione: colonna accreditamento («esterno» / «no»);
  - Statistiche: colonna e filtro «Accreditato» = accreditato MARE.A solo per prove interne;
  - Portale clienti: badge «A» solo per prove interne (RepositoryPortale, condiviso: va ripubblicato anche il Portale).

## 08/10/2026 — Home: ricerca delle funzioni
- Casella «Cerca una funzione» sotto il benvenuto: filtra nel browser le funzioni visibili all'utente (nome, descrizione, cartella), senza accenti né maiuscole; ogni parola deve comparire. Mentre si scrive i risultati prendono il posto delle cartelle, con i contatori. Invio apre il primo risultato, Esc svuota, «/» porta alla casella.

## 08/10/2026 — Laboratori monitorati e date ACCREDIA (proposte 5 e 9)
- Laboratori Esterni → «Laboratori esterni monitorati»: con il permesso `labesterni.accreditamento` pulsanti «Aggiungi laboratorio» e «Modifica», casella «Mostra anche i disattivati». Numero a 5 cifre, sede una lettera; nessuna cancellazione (disattiva/riattiva); cambiando numero o sede la revisione confermata si azzera. Nuovo `ServizioLaboratoriMonitorati`, API `accreditamenti/tutti` e `accreditamenti/laboratorio`; audit `accreditamento.laboratorio.aggiunto|modificato`.
- Data di emissione delle revisioni ACCREDIA letta in formato italiano (prima: 05/10 letto come 10 maggio, 25/09 scartata). Al prossimo controllo le revisioni già registrate con data diversa vengono corrette (log informativo). Va ripubblicato anche l'Agente ACCREDIA (usa ClientAccredia).

## 08/10/2026 — Stato ACCREDIA dei laboratori in subappalto (sospensione/revoca)
- L'agente AgenteAccredia legge dalla banca dati ACCREDIA l'elenco dei laboratori accreditati e di quelli con sospensione attiva e invia lo stato (Accreditato / Sospeso / NonTrovato) insieme alle revisioni.
- Nuove colonne `StatoAccredia`, `DataStatoAccredia` in `labesterni.Accreditamenti` (migrazione StatoAccreditamentiEsterni).
- Laboratori Esterni: colonna «Stato ACCREDIA»; i laboratori sospesi o non trovati compaiono in «Da sistemare» e nel contatore della home; e-mail a ogni cambio di stato.
- Se la lettura dello stato non riesce o il risultato non è plausibile, l'agente non invia nulla (nessun falso allarme).
- 08/10/2026 (correzione): la sospensione ACCREDIA riguarda singole prove (asterisco sulla riga della prova), non tutto il laboratorio. Etichetta «sospensione su alcune prove» (arancione) al posto di «SOSPESO»; testi del riquadro «da sistemare» e dell'e-mail aggiornati. Primo rilevamento: ALS Italia 00141/A e IZS delle Venezie 00139/A.

## 08/10/2026 — Prove sospese dei laboratori in subappalto
- L'Agente ACCREDIA, per i laboratori monitorati con sospensione attiva, legge dalla banca dati ACCREDIA le prove con asterisco (matrice, prova, norma, tecnica) e le invia a LabAI.
- Nuova tabella `labesterni.ProveSospese` (migrazione ProveSospeseLaboratoriEsterni): una riga per prova, con data di rilevazione, ultima lettura e fine sospensione; nessuna cancellazione.
- Riquadro «Accreditamenti da sistemare»: una riga per prova sospesa, con i metodi che affidiamo al laboratorio che citano la stessa norma («possibile metodo nostro coinvolto», da verificare).
- E-mail: elenco delle prove sospese nuove e di quelle non più sospese.

## 08/10/2026 — Offerta allineata alle Condizioni Rev. 19 (BOZZA 18)
- Durata: di norma 12 mesi con rinnovo tacito annuale e revisione con preavviso di 30 giorni (Art. 3.3); campo «Durata (giorni)» con 365 predefinito; una durata più breve vale senza rinnovo tacito. In testata «Durata: 12 mesi, rinnovo tacito».
- Accettazione: pubblicazione nel Portale, avvisi e-mail di cortesia (Art. 2.2), perfezionamento come Art. 3.1; accettazione dal Portale per ogni nuova Offerta e per le revisioni che toccano le clausole approvate.
- Approvazione specifica allineata all'Allegato C: aggiunti Art. 3.3 e Art. 7.3.
- 08/10/2026: Ordine di Accettazione, prelievo: «non predispone piani di campionamento e non svolge attività di campionamento accreditata». Nota sulle prove in subappalto (§) di Offerta e Ordine di Accettazione riallineata agli Artt. 7.2 e 8.2 Rev. 19 (verifica del Laboratorio all'affidamento per autocontrollo o su richiesta in Offerta, senza garanzia), un solo testo condiviso (TestiOfferta.NotaSubappalto). Il contratto di consulenza mantiene la sua nota.
- 08/10/2026: contratto di consulenza, sezione «Analisi di laboratorio»: nota sulle prove in subappalto (§) uguale a Offerta e Ordine di Accettazione (Artt. 7.2 e 8.2 Rev. 19), con «Committente» al posto di «Cliente».
- 08/10/2026: Offerta, durata sempre 12 mesi con rinnovo tacito (Art. 3.3), anche per le offerte salvate con 60 giorni; tolto il campo «Durata» dalla maschera dei preventivi.

## 09/10/2026 — Ricerca errori: denominazioni dei metodi
- Nuovo controllo 9 «Denominazioni dei metodi da controllare» (nato da «Manganese» stampato sul metodo del mercurio UNI EN ISO 12846): metodi attivi la cui denominazione (Metodo.DettagliMetodo) non cita il parametro, o è usata per parametri diversi. Per ognuno i rapporti di prova emessi nel periodo.
- Falsi allarmi: pulsante «È corretta» con nota, salvato in `app.DenominazioniMetodoVerificate` (migrazione DenominazioniMetodoVerificate); vale finché in ArcLab non cambiano denominazione o parametro; «Annulla» senza cancellare. Registro attività e Excel.
- Logica in Core (ControlloDenominazioniMetodi), ArcLab in sola lettura.
- 09/10/2026: «Ricerca errori» spostata dalla cartella Ricerche a Ciclo analitico (stesso indirizzo e permesso).
- 09/10/2026: Offerta e Ordine di Accettazione senza numeri di articolo nel testo («Condizioni Generali di Fornitura»), tranne l'elenco dell'approvazione specifica (Allegato C). Firme dell'Offerta facoltative, solo per chi firma digitalmente (l'accettazione dal Portale non richiede firme). Nota sul subappalto riallineata alla regola del 05/10 (solo prove non accreditate; prova accreditata solo in via eccezionale con assenso del Cliente anche via e-mail).
- 09/10/2026: Ordine di Accettazione, allegato tecnico: riga «Comunicazione anticipata dei fuori limite» (RT-08 rev. 06) da Campione.RichiesteCliente: «Richiesta dal Cliente» se contiene «Richiesta Comunicazione anticipata dei fuori limite» (con l'eventuale testo che segue), altrimenti «Non richiesta dal Cliente».
- 09/10/2026: firme dell'Offerta facoltative, a mano con timbro o con firma digitale.
- 09/10/2026: Ordine di Accettazione, allegato tecnico: colonna delle etichette più larga (5,2 cm) e tabella analisi con denominazione più larga e «Accr.» più stretta, meno righe a capo (PDF e Word).
- 09/10/2026: allegato tecnico, colonna etichette a 7,2 cm (Word 4080 twip): «Comunicazione anticipata dei fuori limite» su una riga; a capo eventualmente nei valori.

## 2026-10-09 — Tarature: numerazione, letture a gruppi, eliminazione dal registro
- Incubatori: colonna «N.» delle letture (riparte a ogni cambio di posizione). Bilance, linearità: colonna «N.».
- Termometri e incubatori: pulsante «Aggiungi 10 letture» (completa il gruppo a 10; se completo, 10 righe con la lettera successiva: 10 A, 10 B, …).
- Bilance, linearità: pulsante «Aggiungi 5 punti» (porta i punti a 5; con la portata propone 0/25/50/75/100%).
- Termometri, incubatori, bilance, mappature: «Elimina dal registro» (solo permesso di validazione, motivo obbligatorio, Attivo = false + registro operazioni), «Mostra eliminate» e «Ripristina». Nessuna migrazione.
- Nuovo modulo comune wwwroot/js/registro-strumenti.js.

## 2026-10-09 — Condizioni e firme, fase 1 (Specifica v0.4)
- Nuovo schema `contratti`: RevisioniCondizioni (PDF nel database, impronta SHA-256 congelata alla pubblicazione), ClausoleCondizioni, FirmatariClienti, RichiesteApprovazione, EventiApprovazione (registro a sola aggiunta con impronte concatenate, trigger che vieta UPDATE/DELETE).
- Pagina Commerciale → «Condizioni e firme»: revisioni e clausole (precompilazione Rev. 19, pubblicazione), Firmatario per cliente (blocco caselle generiche con conferma), richieste di approvazione, registro con verifica integrità.
- Permessi nuovi: condizioni.gestione, condizioni.firmatari (area Contratti).
- Pubblica-Portale.bat: pubblicazione del Portale clienti con Pubblica-Produzione.ps1 (backup in Produzione\Backup\Portale, verifica /stato).
- 10/10/2026: riferimento della revisione composto in automatico da numero e data con il prefisso di Impostazioni documenti (niente testo libero); avviso se Impostazioni cita una revisione diversa; «Ritira» per una revisione pubblicata per errore e senza richieste.

## 2026-10-10 — Condizioni e firme, fase 2
- Portale: pagina /Firma/{codice} (link personale, codice casuale 256 bit, nel db solo l'impronta; percorso mascherato nei log). Apertura obbligatoria dei PDF, caselle 1-3, doppia conferma; modalità D firma grafica con mouse/dito (due riquadri, controllo minimo, immagine + tracciato in contratti.FirmeGraficheApprovazione). Ogni passo nel registro; al perfezionamento il server verifica i passi sul registro.
- Web: «Invia link» / «Reinvia» (e-mail al Firmatario, PDF dell'Offerta congelato al primo invio), download Ricevuta; servizio ServizioRicevuteApprovazione: genera la Ricevuta PDF (RA-AAAA-NNNN) e la invia a Firmatario + e-mail cliente ArcLab con Message-ID nel registro.
- IServizioEmail.InviaConAllegatiAsync (allegati + Message-ID). Avvia_Portale.bat per lo sviluppo.

## 2026-10-10 — Condizioni e firme: invio massivo
- Excel dei clienti con campioni negli ultimi 12 mesi (ArcLab, sola lettura), precompilato con i Firmatari già designati; ricaricamento con anteprima riga per riga (nuovo/cambia/invariato, salta, errori, e-mail generiche escluse salvo conferma).
- «Prepara»: designa i Firmatari e crea le richieste sulla revisione pubblicata, senza inviare. «Metti in coda»: ServizioInvioLinkMassivo invia un link ogni 3 s, al massimo Condizioni:InviiMassimiGiorno (250, poi 1000) al giorno; errori ritentati dopo 30 min; «Ferma l'invio».
- Logica comune in ServizioApprovazioni (designazione, richiesta, invio link). Nuovi campi RichiestaApprovazione.InvioAccodato ed ErroreInvioLink (migrazione da creare).

## 2026-10-10 — Invio massivo: limite giornaliero 1000
- Condizioni:InviiMassimiGiorno predefinito portato da 250 a 1000 (piano Brevo Starter, 20.000 e-mail/mese, nessun limite giornaliero). Evitare campagne massive da aprile a luglio.

## 2026-10-10 — Condizioni e firme: «Rispondi a»
- Le e-mail del link e della Ricevuta hanno il Reply-To verso mmaffei@, commercialelab@, gsimoncelli@mare-a.com (Condizioni:RispondiA, separati da «;», per cambiarli senza ricompilare). IServizioEmail.InviaConAllegatiAsync: nuovo parametro facoltativo rispondiA.
- Brevo: dominio mare-a.com autenticato (DKIM brevo1/brevo2 su Cloudflare).
- Rispondi a cambiato in refertazione@mare-a.com (casella condivisa); filtro sulle risposte: oggetto contiene «Condizioni Generali».
- «Metti in coda» e «Ferma l'invio» del massivo riservati a condizioni.gestione (Responsabile); chi ha solo condizioni.firmatari prepara (Excel, Prepara) e vede una nota al posto dei pulsanti.

## 2026-10-10 sera — Pubblicazione in produzione
- Web pubblicata con Pubblica-Produzione.bat (tarature, Ordine di Accettazione, Condizioni e firme, invio massivo 1000/giorno, Rispondi a refertazione@). Migrazioni fino a CondizioniInvioMassivo applicate all'avvio. Controllati Strumenti e Commerciale: OK.

## 2026-10-11 — Utenti e ruoli; cartella Palomar certificati esterni
- Ruoli per mestiere (tabella «Ruoli e permessi di LaboratorioAI» di Mirko): Amministratore, Responsabile, Responsabile Qualità, Qualità, Tecnico, Campionatore, Accettazione, Commerciale, Consulente, Socio, Consultazione; Operatore disattivato. Permessi e assegnazioni iniziali in DatiIniziali/ProfiliRuoli.cs, applicati UNA volta al primo avvio (quando manca il ruolo «tecnico»): chi è in elenco riceve i suoi ruoli (registrato in anticipo se non è ancora entrato), gli altri passano a Consultazione; in sviluppo l'utente di sviluppo resta Amministratore.
- Nuovo permesso provvigioni.consulta (soci): Provvigioni e Accordi in sola lettura ([RichiedeUnoDeiPermessi] + [SolaLetturaSenza]; il layout disattiva i moduli POST).
- Chi entra per la prima volta riceve Consultazione (non più Amministratore). Persone registrabili in anticipo per e-mail (Utenti.ObjectIdEntra nullable, Utenti.Note; Ruoli.Attivo, Ruoli.Ordine).
- Permessi riletti dal database a ogni richiesta (TrasformazionePermessi + ServizioPermessiUtente, memoria 1 min azzerata dalle modifiche).
- Pagina Impostazioni → Utenti e ruoli (/Utenti, amministrazione.utenti): persone, ruoli, disattivazione, tabella permessi per ruolo; ruolo Amministratore e permessi dei ruoli modificabili solo da un Amministratore; almeno un Amministratore attivo; audit utente.registrato/utente.modificato/ruolo.permessi.modificati.
- Impostazioni documenti: «Cartella di prelievo Palomar per i Certificati Esterni rinominati» (CertificatiEsterni.CartellaPalomar, predefinita \\SRVMAREA\marea\ArcLab\dati\Caricamento_Certificati_Esterni); CertificatiEsterniController la usa prima del file di configurazione.
- Migrazione da creare: UtentiERuoli.
- Ruolo Qualità: aggiunto «Firmatari e richieste» (condizioni.firmatari) per l'Excel delle firme.

## 2026-10-11 — Richiesta del Firmatario ai clienti (inviti) e promemoria
- Nuova tabella contratti.InvitiFirmatario (migrazione da creare: InvitiFirmatario). Invito all'e-mail aziendale ArcLab (Clienti.Email, sola lettura, più indirizzi ammessi) con link personale (256 bit, solo impronta nel db) alla pagina Portale /Indica/{codice}: nome, cognome, ruolo, e-mail nominativa (doppia), casella generica sconsigliata ma ammessa; vale la prima risposta.
- ServizioInvitiFirmatario: situazione dei clienti con campioni negli ultimi 12 mesi (approvato / richiesta in corso / firmatario senza richiesta / invito / da invitare / senza e-mail), Excel, preparazione, invio, elaborazione della risposta (designa Firmatario, crea richiesta, invia subito il link).
- ServizioInvioLinkMassivo: elabora le risposte, invia richieste e inviti in coda, promemoria a 7 e 14 giorni (Condizioni:PromemoriaGiorni) per inviti e richieste non approvate; inviti e promemoria solo feriali 8-18; tetto giornaliero comune (InviatiOggiAsync).
- La designazione manuale di un Firmatario annulla gli inviti aperti del cliente. «Ferma l'invio» ferma anche gli inviti.
- Pagina Condizioni: sezione «Richiesta del Firmatario ai clienti» (situazione, Excel, Prepara, Metti in coda solo gestione) e, nella scheda cliente, «Invia la richiesta al cliente».
- Sviluppo: Condizioni:EmailDiProva in appsettings.Development.json devia gli inviti a mmaffei@mare-a.com (oggetto «[PROVA – per …]»).
- Core: IndirizziEmail (formato, generiche, elenco) condiviso da Web e Portale. Portale: log mascherati anche per /Indica.

## 2026-10-11 — Guide d'uso
- Pulsante «? Guida» in testata: sulla pagina di una funzione apre la sua guida in un pannello laterale (Esc chiude); altrove «? Guide» porta all'elenco /Guide. Guida a pagina intera stampabile /Guide/{codice}. Registro delle guide in Navigazione/Guide.cs (permessi come il menu); contenuti in Views/Guide/_{Codice}.cshtml.
- Prima guida: Condizioni e firme (situazioni per commerciali/qualità/accettazione, sezione per il Responsabile, cosa vede il cliente, automatismi, glossario).
- Solo sviluppo: POST /api/guide/schermata salva le schermate delle guide in wwwroot/img/guide.

## 2026-10-11 — Link «di riserva» nelle e-mail
- Brevo (piano Starter) riscrive i link dei pulsanti per il tracciamento dei clic e non lo si può disattivare (solo Enterprise, su richiesta); l'indirizzo scritto come testo non viene riscritto (verificato su e-mail reale). E-mail del link di approvazione e dell'invito: indirizzo diretto in evidenza in un riquadro grigio «Se il pulsante non si apre, clicchi su questo indirizzo». Guida aggiornata.

## 11/10/2026 — Guide della parte Commerciale
- Nuove guide «? Guida» per: Clienti da seguire, Preventivi analisi, Proposta controlli, Le mie provvigioni (registrate in Navigazione/Guide.cs, contenuti in Views/Guide/_*.cshtml).
- Schermate con dati anonimizzati in wwwroot/img/guide: clienti-riquadri, clienti-elenco, clienti-azione, preventivi-registro, preventivi-righe, controlli-proposta. Le mie provvigioni senza schermate (dati personali).
- Le sezioni «Solo per chi approva / per il Responsabile» compaiono solo a chi ha il permesso.

## 11/10/2026 — Guide di Strumenti e tarature
- Nuove guide «? Guida» per: Taratura termometri (con gestione CDR ed etichetta), Taratura incubatori, Mappatura incubatori, Taratura bilance (con pesiera), Scadenziario tarature.
- Schermate in wwwroot/img/guide: taratura-misure, incubatori-risultati, mappatura-risultati, bilance-prove, scadenziario-esterni (clienti anonimizzati).
- Le sezioni «Solo per Responsabile e Qualità» (validazione del calcolo, elimina dal registro) e «Per chi gestisce lo scadenziario» compaiono solo a chi ha il permesso.

## 11/10/2026 — Guide di Ricerche e statistiche
- Nuove guide «? Guida» per: Ricerca campioni (Consultazione), Certificati emessi, Metodi e controlli, Commesse, Statistiche.
- Schermate in wwwroot/img/guide: consultazione-risultati, certificati-dettaglio, metodi-ricerca, commesse-elenco, statistiche-ricerca (clienti anonimizzati).
- La guida «Certificati emessi» è registrata prima di «Ricerca campioni» perché il suo percorso (/Consultazione/Certificati) inizia come quello della Consultazione.

## 11/10/2026 — Guide del Ciclo analitico
- Nuove guide «? Guida» per: Accettazione campioni, Prelievo vocale, Da Validare, Certificati esterni · rinomina, Certificati esterni non ricertificati, Laboratori Esterni, Ricerca errori.
- Ordini di Accettazione senza guida: sviluppo sospeso (gli OdA partiranno probabilmente da ArcLab).
- Schermate: validazione-dettaglio, prelievo-campione (formato telefono), accettazione-lotto, certificati-esterni-esito (esempio costruito con dati di prova), ricerca-errori-cruscotto.

## 11/10/2026 — Guide del Nutrizionale
- Nuove guide «? Guida» per: Materie prime (scheda PDF con AI, inserimento a mano, somma componenti, import CREA/USDA/CNF) e Ricette nutrizionali (dichiarazione, calo peso, verifica AI, Nutrition Facts USA/Canada).
- Schermate: materie-prime-scheda, ricette-dichiarazione (committente anonimizzato).

## 11/10/2026 — Guide della Consulenza
- Nuove guide «? Guida» per: Preventivi consulenza (nuovo contratto, pacchetto, analisi, stati, dati per le commesse ArcLab), Attività e pacchetti consulenza (allineamento con ArcLab, modalità di prezzo, contenuti, pacchetti, revisione Excel), Rapportini di consulenza (compilazione, casella di verifica, delega operativa, prepagato; per chi gestisce: conferma, contestazione, registro invii).
- La guida «Attività e pacchetti» copre anche /PreventiviConsulenza/AttivitaModifica, /Pacchetti e /Pacchetto (registrata prima di quella dei Preventivi consulenza).
- Schermate: preventivi-consulenza-attivita, pacchetto-consulenza, rapportino-intervento (cliente e consulente anonimizzati).

## 11/10/2026 — Guide di Amministrazione e fatturazione
- Nuove guide «? Guida» per: Tamponi in contratto, Non fatturare, Provvigioni (incluse Convenzioni ed elenco «in sviluppo»), Accordi provvigioni (incluse Basi di calcolo), Segnalatori e compensi.
- Nessuna schermata: le pagine contengono importi e dati commerciali riservati.
- Chi ha solo provvigioni.consulta (soci) vede la guida in versione sola lettura, senza le sezioni operative.

## 11/10/2026 — Guide di Impostazioni e controllo
- Nuove guide «? Guida» per: Impostazioni documenti, Voci preventivi, Sigle laboratori esterni, Responsabili clienti, Consulenze: documenti e deleghe, Utenti e ruoli, Registro attività.
- «Clienti da seguire: soglie e assegnazioni» usa la guida di Clienti da seguire (sezione per il Responsabile).
- Nessuna schermata (pagine di amministrazione).

## 11/10/2026 — «Chiedi alla guida»
- Casella «Chiedi alla guida» nel pannello giallo, nell'elenco Guide e nelle guide a pagina intera: l'AI risponde usando solo le guide e il menu che la persona può vedere (nessun dato ArcLab o clienti), con prompt caching.
- Ogni domanda registrata in log.DomandeGuida (persona, pagina, domanda, risposta, 👍/👎, token, costo); dopo 12 mesi testo e persona svuotati in automatico (ServizioScadenzaDomandeGuida).
- Nuova pagina Impostazioni → Domande alla guida (/Guide/Domande, permesso guide.domande): filtri, 👎 in evidenza, domande frequenti, costo mese, Excel. Con relativa guida.
- Migrazione da creare: DomandeGuida.

## 11/10/2026 — Caselle info@ e segreteria@
- ProfiliRuoli: info@ è di Silvia Migani, segreteria@ di Federica Volponi (non più caselle condivise). stafflab@ resta condivisa (Angelica Teodorani e Giulia Simoncelli L).

## 12/10/2026 — Cambi di gestione (subentri) — fase 1, Legionella
- Nuova pagina Commerciale → «Cambi di gestione (Legionella)» (/ClientiDaSeguire/Subentri): sedi con Legionella 12-24 mesi fa e nessun campione negli ultimi 12; cerca nello stesso posto (stesso nome o indirizzo) un cliente arrivato dopo. Esiti A nuovo gestore nostro, B anagrafica doppia, C nuovo gestore non attivo, D nessun subentro, E cliente attivo da altra sede; affidabilità alta/media/bassa (bassa = da verificare).
- Conferma / Scarta / abbinamento manuale con ricerca / «Nessun subentro», con nome e data; annullabili. Tabella clienti.SubentriInsediamenti.
- Verifica P.IVA sul servizio europeo VIES (singola o in blocco, 60 per volta), tabella clienti.VerifichePartitaIva.
- L'importazione AZW copia anche insediamenti e campioni per insediamento da ArcLab (sola lettura): clienti.InsediamentiArcLab, clienti.CampioniInsediamentiArcLab.
- Sezione Legionella di Clienti da seguire: riquadro riassuntivo ed esito del cambio di gestione sotto lo stato del cliente.
- Permesso nuovo clienti.subentri (commerciali, consulenti, accettazione); gli importi li vede solo chi ha clienti.seguire. Guida «Cambi di gestione».
- Motore in Core/Andamento/MotoreSubentri.cs con test (MotoreSubentriTest, 8 casi dalle prove in SSMS).
- Migrazione da creare: Subentri.

## 12/10/2026 — Più modalità di pagamento nelle offerte (analisi e consulenza)
- Campo «Condizioni di pagamento»: una modalità per riga; scegliendo dall'elenco si aggiungono (senza doppioni), con avviso sotto il campo.
- Con una sola riga il documento resta com'era; con due o più: «Condizioni di pagamento – indicare con una X la modalità scelta:» e una riga «[  ]» per ciascuna (offerta analisi Word e PDF, contratto di consulenza Word).
- Nessuna migrazione: si usa il campo esistente.

## 12/10/2026 — Correzione importazione AZW
- Lettura insediamenti ArcLab: tolta la colonna Telefono (non esiste in Insediamento) che faceva fallire l importazione.

## 12/10/2026 — Cambi di gestione: scelta del periodo
- Filtri Periodo (ultimi 12/6/3 mesi o da inizio anno) e confronto (stessi mesi di 1 o 2 anni prima), come in Clienti da seguire; predefinito 12 mesi contro l anno prima. Il testo in alto riporta i mesi esatti; periodo mantenuto dopo Conferma/Scarta/Annulla/VIES ed Excel; la sezione Legionella passa il proprio periodo.
- Test: FinestraSeiMesi (9 casi in totale).

## 12/10/2026 — Offerta analisi: voci accessorie, prezzi, sconti, modalità di pagamento
- Voci accessorie: titolo e testo descrittivo (nuovo, facoltativo, nel campo Note della riga) su tutta la larghezza; prezzo sotto a destra come per i controlli (Word e PDF). Nel modulo: titolo modificabile e testo descrittivo per ogni voce; testo anche per l'accessorio libero.
- Prezzi solo IVA esclusa (tolto «IVA incl.»); con lo sconto: «Prezzo: € 142,00 – sconto 10% = € 127,80 (IVA escl.)».
- Modalità di pagamento separate da una riga vuota (ognuna può avere più righe, es. banca e IBAN); con due o più: frase di scelta e casella per ciascuna, prima riga in grassetto e le altre rientrate (offerta Word/PDF e contratto di consulenza).
- Righe.Note portato a 4000 caratteri: migrazione da creare (NoteRighePreventivo).

## 12/10/2026 — Cambi di gestione estesi ad analisi e consulenza
- Filtro «Servizio»: Legionella (predefinito), tutte le analisi, analisi esclusa la Legionella; consulenza: tutta (esclusa formazione), sicurezza, igiene alimentare (HACCP), ambiente e rifiuti, privacy, formazione. Testi, colonne ed Excel seguono il servizio (campioni o prestazioni).
- Aree della consulenza dalla voce del tipo di incarico (cons.VociIncarico): 626/RSP sicurezza, HAC/AT igiene alimentare, RIF ambiente, PRY privacy, F62/FHA/FON e commesse tipo 4 formazione.
- L'importazione AZW copia anche le prestazioni di consulenza e formazione per sede e mese (clienti.PrestazioniInsediamentiArcLab, sola lettura ArcLab).
- Motore generalizzato (AttivitaInsediamentoMese: presenza e ambito); voce di menu ora «Cambi di gestione». Test: 10 casi.
- Migrazione da creare (insieme a NoteRighePreventivo se non ancora fatta).

## 2026-10-12 — Certificati esterni: solo esterni / misti, annullamento, stato ArcLab
- Analisi: dalle prove del campione in ArcLab (sola lettura) il certificato è classificato "Solo esterni" o "Misto" (campione con anche prove nostre); correggibile a mano; due tabelle separate.
- Misti: salvati nella cartella Palomar INTERNI_ESTERNI (nuova impostazione CertificatiEsterni.CartellaPalomarMisti), nessun import ArcLab.
- Registro di controllo: colonna Tipo / ArcLab; per i misti controllo live dei valori inseriti in ArcLab ("da inserire" / "inserito") e filtro "solo misti da inserire".
- Annulla con motivo: riga "Annullato" collegata (nessuna cancellazione), file tolto da Palomar se non prelevato (Arrivo\annullati), originale rimesso in arrivo, uscita dalla coda di import ArcLab.
- Migrazione da creare: CertificatiMistiEAnnulli (4 colonne in log.RegistroDocumenti).
- Originali già lavorati: cartella dei processati separata per solo esterni e misti (Impostazioni documenti CertificatiEsterni.CartellaProcessatiEsterni / CartellaProcessatiMisti; se vuote resta Arrivo\processati). L'annullamento cerca l'originale in tutte e tre.
- Audit: IdEntita troncata a 100 caratteri e voce non salvata staccata dal contesto (nomi lunghi dei certificati esterni).
- Certificati esterni: cartella di arrivo impostabile in Impostazioni documenti (CertificatiEsterni.CartellaArrivo).
- Taratura bilance: carico consigliato per la ripetibilità = massimo del campo di taratura (se compilato), altrimenti portata.
- Menu: «Preventivi consulenza» anche in Commerciale (resta in Consulenza).
- Portale: filtro Stato non dava più errore 400 (stato inviato come testo, ora accettato).
- Prelievo vocale: tendine (clienti, insediamenti del cliente, controlli, matrici, imballaggi, luoghi) da ArcLab, tenute sul dispositivo per l'offline; resta l'inserimento libero.
- Portale: in sviluppo i log anche nella finestra (Now listening…).
- Prelievo vocale: tendina punti di prelievo già usati nell'insediamento (25 più frequenti, ultimi 2 anni).
- Menu in alto: apertura al tocco su iPhone/iPad (prima solo al passaggio del mouse).
- Prelievo vocale: corretta la tendina insediamenti/punti (leggeva il cliente prima dell'aggiornamento).
- Prelievo vocale: luogo di prelievo proposto per insediamento (luoghi già usati lì), sempre con inserimento libero.

## 2026-10-13 — Cruscotto Direzione, fase 1
- Nuovo gruppo di menu «Direzione» (ruolo Direzione: soci + Bertozzi, creato e assegnato al primo avvio): Indicatori economici e Classificazione voci.
- Importazione notturna AZW estesa: contabilità analitica (dati_importati_preconsuntivo) per mese/centro/voce/commessa/conto, voci, centri, commesse (schema dir).
- Classificazione voci come Power BI (Mapping_Costi), in tabella modificabile; voci nuove proposte e «da verificare».
- Indicatori ricalcolati e riscontrati con Power BI al centesimo (2020-2025, MAREA e Laboratorio). Correzioni di definizione: VA/EBITDA/EBIT senza ammortamenti e imposte; quota 1/5 Amministrazione solo sulle aree.
- Sviluppo: PercorsoAzw sulla copia D:\LabAnalisi\mirko.maffei\AZW_07.accdb (non il db di lavoro di Access).
- Migrazione da creare: Direzione.

## 2026-10-13 — Cruscotto Direzione, fase 2
- «Aggiorna da AZW» anche in Direzione (permesso direzione.gestisci).
- Andamento territorio: fatturato per regione/provincia/comune della sede del cliente AZW (DETTANA ricavi su TESTANA.CODCF, note di credito sottratte), clienti attivi/nuovi/persi, filtri area, voce, commessa, zona, commerciale; confronto sede di Pesaro (PU, Marche sud vs RN, FC) con crescita annua prima/dopo il 2022; export Excel.
- Nuove tabelle dir.FatturatoClienti, colonna provv.AzwClienti.CodiceVenditore. Migrazione da creare: DirezioneTerritorio.

## 13/10/2026 - Pubblicazione Direzione fase 1 in produzione
- Primo tentativo fallito (500 all avvio, causa non registrata: il ripristino aveva sovrascritto il log). Secondo tentativo riuscito.
- Pubblica-Produzione.ps1: il ripristino non tocca piu la cartella log; corretto TrimStart nel ripristino automatico.
- DatiIniziali: l assegnazione iniziale del ruolo Direzione non blocca piu l avvio (errore riportato nel log come avviso).
- Direzione: Commento AI su Indicatori economici e Andamento territorio (numeri calcolati dal programma, nomi dei primi 30 clienti per elenco), archivio dir.CommentiAi, guida aggiornata. Migrazione da creare: DirezioneCommentiAi.
- Direzione fase 3: Rendimento commerciali (/Direzione/Commerciali): costo = voce personale AZW riconosciuta dal nome + provvigioni; portato = base provvigioni; seguito = clienti con codice venditore; margine con MdC% aree pesate sul costo; contributo e cumulato; Excel; Commento AI. Nessuna migrazione nuova.

## 14/10/2026 - Clienti da seguire: togli dalla lista
- Scheda cliente: «Togli dalla lista» con motivo (acquisito da un altro nostro cliente, cessata attivita, non recuperabile, altro con nota), «Rimetti in lista». Tabella clienti.ClientiTolti (nessuna cancellazione).
- I clienti tolti spariscono da rossi/gialli, Legionella, solo consulenza, da chiamare e Cambi di gestione; nuova sezione «Tolti dalla lista». Se fatturano o mandano campioni dopo il mese di uscita ricompaiono con «tornato attivo».
- Acquisiti: lo storico si somma al cliente che li ha acquisiti (anche a catena); nella sua scheda «Comprende anche lo storico di...».
- Direzione: i clienti acquisiti contano a parte («acq.»), non fra i persi (territorio, rendimento commerciali, commento AI).
- Scheda cliente: commerciale proposto per l assegnazione = commerciale dell utente, altrimenti quello con accordo di laboratorio (Ludovico).
- Test UsciteClientiTest. Migrazione da creare: ClientiTolti.
- 14/10: corretto timeout di Clienti da seguire (filtro clienti nella query solo per la scheda, niente «lista nulla oppure contiene»).
- 14/10: «Acquisito da»: ricerca del cliente ordinata (codice, nome che comincia, nome che contiene, poi citta), fino a 40 risultati; con piu parole devono esserci tutte.

## 14/10/2026 — Ordini di Accettazione: ordine da firmare per i clienti privati
- Gli Ordini per il Portale li genera Palomar da ArcLab: tolti «Emetti», l'emissione massiva/schedulata e la copia nella cartella Palomar (voce OrdineAccettazione.CartellaPalomar tolta da Impostazioni documenti).
- Nuova pagina /OrdiniAccettazione/Privato/{IdLotto} (pulsante «Stampa da firmare» di ArcLab): e-mail per il Rapporto di Prova (ricordata per cliente), consegna e-mail / a mano / entrambi, firmato oggi; Stampa = PDF archiviato (_privato.pdf) + registro + audit.
- PDF privati: riquadro «Consegna del Rapporto di Prova», testi senza rimandi al Portale, riquadro firme Cliente / MARE.A. Testi modificabili in Impostazioni documenti (TestoVerificaPrivati, TestoCondizioniPrivati, TestoFirmaPrivati).
- Pagina Ordini di Accettazione: sezione «Consegnati ai privati» con ricerca per lotto/cliente/e-mail, divisa fra «da inviare per e-mail» e «da consegnare a mano», modifica di e-mail, modalita, firmato il, note. Lotti: anteprima e «Stampa da firmare».
- Home: tolto il contatore «da spedire» degli Ordini di Accettazione. Guida «Ordini di Accettazione».
- Nuova tabella accettazione.OrdiniPrivati. Migrazione da creare: OrdiniPrivati.
- 14/10: Ordini di Accettazione, Lotti ricevuti: filtri per cliente e insediamento (nome o indirizzo), colonna Insediamento; senza date cerca in tutto l'archivio (max 2000 lotti).
- 14/10: Lotti ricevuti: cliente da elenco ArcLab (scrivi e scegli) e insediamento da tendina dei suoi insediamenti; testo libero come ripiego per il cliente.
- 14/10: Ordine da firmare: messaggio distinto per lotto inesistente o lotto senza campioni salvati.

## 15/10/2026 — Condizioni Generali: firma con codice OTP via e-mail
- Dopo Cass. Sez. III ord. 20945/2026 (la sola spunta non basta per le clausole vessatorie): nel Portale l'approvazione si firma con un codice di 6 cifre inviato per e-mail al Firmatario (10 minuti, 5 tentativi, 5 codici l'ora, reinvio dopo 60 s). Nel registro solo l'impronta del codice; codice legato all'impronta dei testi dichiarati. Modalita CaselleOtp.
- Tolta la firma grafica (mouse/dito) e la finestra di doppia conferma dalla pagina del Portale.
- Ricevuta: modalita e riga «firmata elettronicamente con codice OTP inviato a … il … e inserito il …».
- ServizioEmailSmtp spostato da Web a Dati (LaboratorioAI.Dati.Email), MailKit nel progetto Dati: lo usa anche il Portale. Portale: sezione Smtp in appsettings (password da user-secrets / appsettings.Production.json), in sviluppo Condizioni:EmailDiProva.
- Guida Condizioni aggiornata (le schermate della pagina di firma andranno rifatte).
- 15/10: ServizioEmailSmtp in Dati senza GetValue (pacchetto Binder assente).
- 15/10: ricerca clienti tollerante (RicercaNomi: ignora punteggiatura/spazi, ordina per pertinenza) in Condizioni e Consulenze amministrazione; Condizioni usa un elenco di suggerimenti proprio (suggerimenti.js) al posto del datalist.
- 15/10: Da Validare, analisi massiva a blocchi di 2 campioni con avanzamento (errore 524 Cloudflare oltre 100 s).
- 15/10: Ordine di Accettazione, allegato tecnico: nuova riga «Luogo di prelievo» (Campione.LuogoPrelievo); imballaggio, punto e luogo di prelievo, data prelievo, T° di ricevimento e controllo stampati solo se compilati (PDF e Word).

## 17/10/2026 — Limiti normativi, fase 1a: schede limite, fonti ufficiali, estrazione con l'AI
- Nuova pagina /LimitiNormativi (menu Ciclo analitico, dopo «Da Validare»): schede limite e fonti, con guida. Procedura «Gestione dei limiti normativi» Rev. 0.
- Schema `limiti`: FontiNormative, VersioniFonti (copia originale, testo letto, impronta SHA-256, data «vigente al»), LimitiNormativi (scheda per versione: codice LEG-/ACQ-/MOL-…, operatore ≤ < ≥ intervallo «assente in», valore, unità, n/c/m/M, tipo di limite, citazione, testo della segnalazione, validità, stato Proposta/Validata/Da riverificare/Respinta/Superata), AbbinamentiLimiti (parametri ArcLab). Nessuna cancellazione fisica. Migrazione da creare: LimitiNormativi.
- Fonti: «Aggiungi le fonti della fase 1» (Linee guida Legionella 2015, D.Lgs. 18/2023, Reg. 2073/2005, 2019/627, 853/2004, 2023/915), «Scarica» dall'indirizzo ufficiale o «Carica file» (PDF, HTML salvato, testo). Testo invariato = nessuna nuova versione.
- «Estrai con AI» in background (ServizioEstrazioneLimiti, fino a 10 minuti, niente errore 524): l'AI propone le schede con i parametri ArcLab abbinati; scartate le proposte la cui citazione non si trova parola per parola nel testo salvato e quelle già presenti.
- Validazione: Valida / Correggi / Respingi (motivo obbligatorio). Non valida chi ha proposto o corretto. Correggere una scheda validata crea una nuova versione «Proposta»; alla validazione la precedente diventa «Superata».
- Permessi limiti.consulta e limiti.valida (area Limiti normativi): da spuntare in «Utenti e ruoli». Home: contatore «da validare» solo per i validatori.
- ClientAnthropic: tempo massimo per chiamata (TimeoutSecondi per le chiamate normali, dedicato per i lavori lunghi); HttpClient con margine di 15 minuti.
- Test LimitiNormativiTest. Le segnalazioni in «Da Validare» arrivano con la fase 1b.
- 17/10: Scarica rifiuta le pagine di controllo anti-bot (salute.gov.it «Pagina di convalida del browser») e le pagine web al posto di un PDF; le versioni già salvate con quella pagina sono segnate «non è la norma» e non si possono mandare all'AI.
- 17/10: Agente fonti (strumenti/AgenteFonti), come l'agente ACCREDIA: sul PC dell'ufficio scarica le fonti EUR-Lex da Cellar (ultimo testo consolidato trovato con SPARQL) e Normattiva dalle API Open Data (tutti gli articoli e gli allegati vigenti) e le invia al server (api/fonti-agente, segreto LimitiNormativi:AgenteSegreto o quello dell'agente ACCREDIA; in sviluppo basta localhost). Sul server «Scarica» resta solo per le altre fonti.
- 17/10: Nuova versione di una fonte: le schede validate tratte da quella fonte passano a «Da riverificare». Fonti fase 1: aggiunta la Direttiva (UE) 2020/2184 (valori dell'Allegato I del D.Lgs. 18/2023, che in Gazzetta è un'immagine); indirizzi EUR-Lex sul numero CELEX di base.
- 17/10: Testo delle fonti HTML: una riga di tabella = una riga di testo con le celle separate da « | » (prima ogni cella andava a capo), tolti i segni del consolidato EUR-Lex (►M9, ◄) e i richiami di nota «( 7 )»; citazioni controllate anche a tratti nell'ordine (riga di tabella, parti omesse con «…»). Prima prova sul Reg. 2073: le 2 citazioni erano state scartate per questo.
- 17/10: Estrazione AI a più giri (fino a 4) quando la risposta supera i token, con l'elenco dei limiti già estratti; doppioni saltati solo nella stessa fonte, stesso valore in un'altra fonte = scheda creata con nota per chi valida; niente schede per parametri senza valore numerico.
- 18/10: Estrazione AI: parametri ArcLab tenuti in memoria 6 ore (timeout SQL su D.Lgs. e 2073), se ArcLab non risponde si prosegue senza abbinamenti; nell'esito i primi esempi di citazioni non trovate. Agente fonti: opzione --salva <cartella> per tenere una copia locale dei testi inviati.
- 18/10: Citazioni valide anche se brevi (righe di tabella come «Antimonio | 10 | μg/l»): almeno 10 caratteri utili con una cifra e il nome del parametro (prima ne servivano 15: scartati i parametri chimici della Direttiva 2020/2184).
- 18/10: Citazioni di 7-9 caratteri utili valide solo con l'unità dopo il valore («Rame | 2,0 | mg/l» sì, «Nitrati | 15» no); all'AI detto di non estrarre le tabelle di prestazione dei metodi (incertezza, LOQ, % del valore di parametro) né frequenze di campionamento.
- 18/10: Schede limite: ordinamento per parametro (schede dello stesso parametro vicine, segno «×2/×3» sui possibili doppioni), colonna Fonte, selezione multipla e «Respingi le selezionate» con un motivo unico (validazione sempre una per una).
- 18/10: Estrai con AI: si scelgono le famiglie da estrarre e l'AI assegna la famiglia a ogni scheda (una fonte come il Reg. 2073 produce insieme schede Molluschi e Alimenti); parametri ArcLab elencati per famiglia; doppioni per famiglia. Le famiglie scelte viaggiano nell'esito «In coda (famiglie: …)», nessuna modifica al database.
- 18/10: Schede limite, «validazione rapida»: sotto ogni scheda citazione, condizioni e parametri ArcLab; «Valida le selezionate» con nota facoltativa (stesse regole della validazione singola, una riga di registro per scheda; saltate quelle proposte o corrette da chi valida; avviso per le schede senza parametri ArcLab).
- 18/10: Schede limite: le spunte restano quando la tabella si ricarica (apertura di una scheda, aggiornamento automatico durante le estrazioni, cambio filtro); Valida/Respingi le selezionate agiscono solo sulle schede visibili con il filtro attuale; conferma anche per Respingi.
- 18/10: Verifica delle schede validate («quattro occhi»): controlli automatici (valore nella citazione, unità rispetto ad ArcLab, nome del parametro ArcLab, tipo di limite/famiglia, schede senza ArcLab, contraddizioni) e revisione con l'AI per fonte in background (conforme/dubbio/errato con citazione controllata; modello Anthropic:ModelloRevisione, vuoto = lo stesso). Rilievi chiusi con nota, Excel per giro. Nuove tabelle limiti.GiriVerifica e limiti.RilieviVerifica: migrazione da creare VerificaLimiti.
- 18/10: Fase 1b passo 1 — Categorie normative (dal testo «Matrice / categoria» delle schede degli Alimenti) e abbinamento delle matrici ArcLab alle categorie: proposta con l'AI in background, conferma in tabella (✓/✕, conferma in blocco, «nessuna categoria»). Nuova scheda «Categorie e matrici». Tabelle limiti.CategorieNormative e limiti.MatriciCategorie: migrazione da creare CategorieLimiti.
- 18/10: Fase 1b passo 2 — «Da Validare»: colori dei limiti normativi per campione (viola da correggere, rosso limite di legge superato, arancione da segnalare/dipende dalla fase, grigio non confrontabile, verde) calcolati sulle schede limite validate e sulle categorie confermate; controlli tecnici (unità, arrotondamento, parametro ripetuto, subappalto senza frase, prelievo dopo ricezione, tempi massimi ricezione→inizio prova, E. coli > coliformi, LOQ sopra il limite). Filtro per colore, ordinamento per gravità, riquadro rilievi nel dettaglio.
- 18/10: Limiti normativi, nuova scheda «Tempi massimi»: ore massime tra ricezione e inizio prova per famiglia o per singolo parametro ArcLab (il parametro prevale), modificabili da chi valida, righe disattivabili (non cancellabili); valori iniziali Acque 24, Legionella 48, Molluschi 24, Alimenti 48, Piscine 24. Nuova tabella limiti.TempiMassimiProva: migrazione da creare TempiMassimiProva.
- 18/10: Proposta abbinamenti con l'AI: se non ci sono categorie (nessuna scheda degli Alimenti) lo dice subito invece di accodare un giro destinato a fallire; messaggi «Proposta in corso/non riuscita» invece di «Revisione».
- 18/10: «Da Validare» — validazione del campione da LaboratorioAI: riquadro «Validazione» con stato ArcLab (giudizio in GiudCamp, analisi chiusa, richiesta giudizio del cliente), scelta del giudizio tra quelli della matrice (GiudizioMatrice, non sospesi) o 212 automatico per i clienti senza GiudizioConf, conferma in più per campioni rossi/viola, annullamento finché non è in ArcLab, apertura diretta con /Validazione?campione=<IdCampione>. LaboratorioAI scrive solo in scambio.GiudiziDaRiportare (una validazione attiva per campione, indice univoco); Palomar importa in ArcLab. Nuovo permesso validazione.conferma. Migrazione da creare: GiudiziDaRiportare.
- 19/10: «Da Validare», controllo tempi ricezione → inizio prova: si conta dalla data prevista di inizio = ricezione + Campione.RitardoAnalisi (ritardo programmato, es. shelf life); prima le shelf life risultavano tutte viola.
- 19/10: Spettanze dei consulenti a percentuale (De Angelis, Bacchiani, Remedia): Amministrazione → Spettanze consulenti e «Le mie spettanze». Calcolo sugli incassi AZW con ZONA del cliente (suoi clienti) e COMMESSA della riga (suo lavoro): a) consulenze 50%, b) analisi portate 10%, c) analisi interne dei suoi clienti 10%, d) primo anno di servizi di altri 10%, e) formazione di altri 10%, f) docenze 50% del fatturato, g) tarature 50%; contratti a versioni con date; gruppi di voci VOCICOST modificabili; acconti = sue fatture fornitore (centro FOR = docenze a ore, a parte); conguaglio annuale e chiusura che congela; Excel. Importazione AZW: COMMESSA sulle righe di fattura e note, fatture dei consulenti. Nuovo permesso spettanze.proprie. Migrazione da creare: SpettanzeConsulenti.
- 19/10: Spettanze consulenti: pulsante «Importa AZW ora» anche nella pagina (stessa importazione di Provvigioni); corretto il controllo dei permessi della pagina (403).
- 19/10: «Le mie spettanze» spostata nel menu Consulenza.
- 19/10: «Da Validare»: all'apertura i filtri (testo e colore) ripartono sempre vuoti (il browser li ripristinava e l'elenco risultava vuoto); il messaggio dice quale filtro è attivo, con il pulsante «Togli i filtri».
- 19/10: «Da Validare», controllo dei tempi massimi ricezione → inizio prova solo sulle prove eseguite internamente (escluse quelle in subappalto a laboratori esterni).

- 2026-10-19: pulsante «Aggiorna dati da AZW» in «Le mie provvigioni» e «Le mie spettanze» (max ogni 15 minuti, una sola importazione alla volta). Nessuna migrazione.
- 2026-10-20: Amministrazione → «Consuntivo per area» (costi e ricavi per area da AZW, contributo AMM in parti uguali, confronto anno prima, andamento mensile, dettaglio registrazioni, Excel, stampa). Nuova tabella dir.RegistrazioniAnalitiche (migrazione ConsuntiviPerArea), permesso consuntivi.consulta al ruolo Socio.
- 2026-10-20: Da Validare: parametro ripetuto ammesso 5 volte (unità campionarie) o 9 per l'istamina, viola solo se diverso; parametri indicatori acque/piscine superati (es. coliformi) ora rossi = non conforme. Nessuna migrazione.
- 2026-10-20: Consuntivo per area: sezione Grafici (utile per area con contributo vs anno prima, utile MARE.A progressivo, ricavi e costi per area, 10 voci di costo più alte), SVG senza librerie. Nessuna migrazione.
- 2026-10-20: Offerte di laboratorio (PDF e Word): colonna «U.M.» (unità di misura del metodo) nella tabella dei metodi. Nessuna migrazione.
- 2026-10-20: Limiti normativi: unità con specificazione della matrice («MPN/100 g di carne e liquido intervalvare») confrontabili con «MPN/100g»; pulsante «Ritira» per le schede validate (passano a Respinta con motivo, nessuna cancellazione). Nessuna migrazione.
- 2026-10-20: Analisi AI dei risultati: temperatura 0 (stesso dato, stesso giudizio), regole precise per BORDERLINE (non per «< valore» né per parametri senza limite di legge), riga finale «ESITO FINALE:» letta per il giudizio. Nessuna migrazione.
- 2026-10-20: Revisione AI dei limiti: corretto l'errore «Members 'm' and 'M' … cannot both bind» (campi m/M rinominati m_piccola/M_grande nel JSON inviato all'AI). Nessuna migrazione.
- 2026-10-21: Preventivi: rinnovo tacito facoltativo (spunta, predefinito sì; senza rinnovo testo in deroga all'Art. 3.3), campo Telefono del cliente, «Spett.le cliente» con tutti i dati da compilare a mano in prima pagina. Migrazione PreventiviRinnovoTelefono (SenzaRinnovoTacito, ClienteTelefono).
- 2026-10-21: Nuovo ruolo «Amministrazione contabile» (Silvia Migani, Federica Volponi), sola lettura: prefatturato, provvigioni, accordi, spettanze, segnalatori e compensi, consuntivo per area. Segnalatori visibili in sola lettura con provvigioni.consulta. Nessuna migrazione.
- 2026-10-21: Preventivi: spunta «Già pagato» per «Spett.le cliente» (riga Pagamento in prima pagina: GIÀ EFFETTUATO / da effettuare). Migrazione PreventiviGiaPagato.
- 2026-10-21: Offerta «Spett.le cliente»: e-mail sempre vuota da compilare a mano; pagamento con due caselle «Pagato» / «Non pagato» (Pagato spuntata se segnato in maschera). Utenti e ruoli: intestazione dei ruoli e colonna dei permessi fisse durante lo scorrimento. Nessuna migrazione.

## 21/10/2026 — Preventivi: «Nome cliente» e ricerca nel registro
- Nuovo campo `ClienteNome` (solo «Spett.le cliente»): in maschera «Nome cliente», in Word/PDF riga «Nome cliente» (vuota da compilare se non indicato). Intestazione invariata «Spett.le cliente». Migrazione: PreventiviNomeCliente.
- Registro preventivi: ricerca per numero (2026/012 o 12) o cliente (ragione sociale o nome) e intervallo di date offerta.

## 21/10/2026 — Pulsante «⟳ AZW» nella barra in alto
- Nuovo AzwController (`GET api/azw/stato`, `POST api/azw/aggiorna`), per tutti gli utenti autenticati, senza permesso dedicato. Stessa regola delle pagine personali: al massimo ogni 15 minuti, una importazione alla volta; dopo l importazione sincronizza le convenzioni. Audit `azw.aggiorna`.
- _Layout: pulsante accanto al nome utente, suggerimento con l ora dell ultimo aggiornamento, avviso con l esito e link «Ricarica la pagina». File wwwroot/js/aggiorna-azw.js, stili in sito.css.
- Promemoria all apertura: avviso «aggiorna AZW se ti servono calcoli contabili» con pulsante «Aggiorna ora»; una volta per sessione del browser e solo se i dati hanno più di 15 minuti.

## 21/10/2026 — Compensi soci (Direzione)
- Nuova sezione Direzione → «Compensi soci» e «Rimborsi spese soci». Definizione: claude/definizione-compensi-soci.md.
- Schema `soci`: Soci, AnniCompensi, PercentualiVociLaboratorio, CompensiSociAnno, PremiAreaLaboratorio, CategorieRimborsi, RimborsiSpese, RicevuteRimborsi. Migrazione: CompensiSoci.
- Permessi `compensisoci.consulta` (al ruolo Socio alla creazione) e `compensisoci.gestisci` (Amministratori); in più la pagina richiede un socio attivo collegato all'e-mail e, per modificare, il socio «gestore».
- Calcolo come il foglio Excel (MotoreCompensiSoci + test); RSPP, base laboratorio e percepiti da dir.RegistrazioniAnalitiche; recupero automatico dall'anno prima; chiusura/riapertura dell'anno con cifre AZW fissate.
- Importazione del foglio «2026x2025» del vecchio Excel con confronto dei totali; esportazione Excel; rimborsi con ricevuta facoltativa e maggiorazione per categoria.

## 22/10/2026 — Spettanze consulenti: percentuale del campionamento per le analisi esterne
- Contratto consulente: nuovo campo `PercCampionamentoEsterne` («b) esterne %»). Le analisi esterne (gruppo Analisi esterne, ANE) portate dal consulente usano questa percentuale; vuoto = come b). Test aggiunti.

## 22/10/2026 — Spettanze consulenti: filtro per date fatture e incassi
- Gestione e «Le mie spettanze»: filtro fatture dal/al e incassi dal/al, scorciatoie «Solo fatture dell anno (incassi fino a oggi)» e «Incassi dell anno di fatture degli anni prima»; vista filtrata ricalcolata da AZW, senza acconti e conguaglio.
- Nuova tabella «Per anno della fattura» (anche nella vista dell anno) e colonna «Data fattura» nei movimenti; Excel con foglio «Per anno fattura» e filtri. Nessuna migrazione.

## 22/10/2026 — Ciclo analitico: campioni in ritardo con avviso settimanale
- Nuova pagina «Campioni in ritardo» (permesso validazione.visualizza; impostazioni e «non mostrare più» con validazione.conferma). Campioni senza rapporto di prova oltre la soglia del tipo (famiglia della matrice; soglia a parte per il subappalto), dall inizio = ricezione + RitardoAnalisi. Gruppi: parametri mancanti, rapporto da emettere, subappalto. Excel.
- Avviso all apertura nei giorni impostati (impostazioni in app.Configurazioni, chiave CicloAnalitico.AvvisoRitardi). Tabella app.CampioniNascostiRitardi. Migrazione da creare.

## 22/10/2026 — Compensi soci: rimborsi trasferte da AZW
- Importazione AZW: tabella Viaggi (con MEZZI e Operatori_commerciale, collegate a AZW_07_Dati.accdb) in soci.TrasferteAzw. Rimborso = km x costo al km + autostrada + vitto e alloggio + costi diversi; solo mezzo privato (TipoMezzo 2); per data della trasferta.
- Prospetto: riga «Rimborsi trasferte» separata da «Altri rimborsi spese»; pagina «Rimborsi trasferte» per socio e mese, Excel. Soci.OperatoreTrasferteAzw (vuoto = nome del socio), AnniCompensi.TrasferteDaAzw (falso per l anno caricato dal vecchio Excel), CompensiSociAnno.TrasferteAzwChiusura. Migrazione da creare.
- 22/10/2026: in sviluppo (accesso di sviluppo attivo) l utente di sviluppo vede i compensi soci come il socio gestore, con un avviso in pagina.

## 22/10/2026 — Compensi soci: accessori e maturato non pagato per area
- In fondo al prospetto: accessori dei soci (indennità lab, premi area lab, RSPP, responsabile d area, premio, assicurazione, amministratore) per area, con fatturato e margine del consuntivo prima e dopo AMM e incidenza %; rimborsi a parte. Specchietto dei compensi maturati e non pagati (differenza − recupero) per area con margine rettificato. Excel: foglio «Accessori per area».
- Ripartizione per socio (CompensiSociAnno.QuotaFissaAmministrazione, RipartizioneAree): Maffei 16.000 AMM resto Lab; Vaccarini 6.000 AMM resto Lab; Sertori 50% Sicurezza 50% Formazione (RSPP tutta Sicurezza); Scaramucci Sicurezza; Giulini Igiene. Migrazione da creare.
- 22/10/2026: per area conta tutta la differenza ancora da pagare (non più differenza − recupero); ripartizione apposita della differenza (CompensiSociAnno.RipartizioneDifferenza; Vaccarini → Igiene). Migrazione da creare.

## 22/10/2026 — Consuntivo per area: rettifica con i compensi soci da pagare
- Spunta «Rettifica compensi soci» (solo soci, solo anno intero 1/1–31/12): righe «Compensi soci ancora da pagare», «Utile rettificato», contributo AMM dei compensi da pagare e «Utile rettificato con contributo AMM», con l'anno prima se presente nei compensi soci; anche nell'Excel.
- Da pagare = differenza del prospetto compensi (anche il debito degli anni prima), divisa per area come nel prospetto per area (Vaccarini → Igiene).
- Nessuna migrazione.

## 22/10/2026 — Importazione AZW: protezione della contabilità analitica
- La contabilità analitica (consuntivo, cruscotto Direzione, compensi soci) non si riscrive se il file AZW è più vecchio di quanto già importato: ultima registrazione indietro di più di 7 giorni, file senza registrazioni o meno del 90% delle righe. Il resto dell'importazione procede.
- Avviso nel messaggio di «⟳ AZW» (in rosso), nella pagina Provvigioni e sotto i filtri del Consuntivo. Nuovo `Core/Direzione/ControlloContabilitaAnalitica.cs` con test. Nessuna migrazione.

## 23/10/2026 — Tracciato (tracciabilità per produttori alimentari): base del progetto
- Nuovi progetti nella soluzione: `LaboratorioAI.Tracciabilita.Core` (entità, regole, motori), `.Dati` (contesto EF Core), `.Web` (sito su http://localhost:5082) e `LaboratorioAI.Tracciabilita.Tests`. Servizio a canone «by MARE.A» per produttori (prima piadine/pinsa, poi ittico e altri settori); progetto documentato in `claude/analisi-tracciabilita.md`.
- Database separato `Tracciabilita` (schema `trac`): i dati dei clienti del servizio non stanno mai in `LaboratorioAI`. Nessuna scrittura su ArcLab né AZW, nessun collegamento a ArcLab in questa fase.
- Modello a eventi: lotti, movimenti in sola aggiunta (giacenza = somma, correzione = storno), lotti in uso, produzioni con consumi (proposto/confermato/modificato), spedizioni, trasferimenti tra insediamenti, richiami e simulazioni, allergeni (Reg. UE 1169/2011).
- Isolamento tra aziende su più livelli: filtro automatico per azienda, controllo in salvataggio, nessuna cancellazione fisica (solo alla chiusura del servizio, solo gestore); trigger e sicurezza a livello di riga di SQL Server in `VincoliSicurezzaSql` (sicurezza a livello di riga spenta finché non collaudata).
- Motori con test: codice lotto (giorno dell'anno, es. `296`, `296-CAT-2`), proposta lotti per ricetta, rintracciabilità a monte e a valle su più livelli, giacenze e storni, controlli di coerenza.
- Sito minimale (pagina iniziale e `/stato`); accesso con utenti e ruoli per azienda, schermate e importazione dal vecchio Access nelle consegne successive. Avvio: `Avvia_Tracciato.bat`.
- Migrazione `Iniziale` da creare (contesto `ContestoTracciabilita`), poi `database update` su `Tracciabilita_Test`.

## 24/10/2026 — Tracciato: accesso con e-mail e password
- Pagina di accesso (`/Accesso`) con e-mail e password, uscita (`/Esci`), sessione in cookie cifrato (8 ore, scorrevole). Utenti personali per azienda: servono al registro attività. Password di almeno 12 caratteri, impronta con l'hasher standard di ASP.NET Core.
- Sicurezza: stesso messaggio per e-mail sconosciuta e password sbagliata, tempi simili, blocco di 15 minuti dopo 5 tentativi falliti, esiti scritti nel registro attività, controllo a ogni richiesta che utente e servizio dell'azienda siano ancora attivi (sospendendo un utente la sessione cade subito).
- Primo utente gestore creato all'avvio da `Tracciabilita:GestoreIniziale:Email` / `:Password` (user-secrets, mai nel repository). `Utenti`: nuove colonne `TentativiFalliti` e `BloccatoFinoUtc`. **Migrazione da creare** (`AccessoUtenti`).
- Il sito ora ricava azienda e gestore dalla sessione; senza accesso il database non mostra nessun dato. Ruoli per insediamento e scelta dell'insediamento: consegna successiva.

## 24/10/2026 — Tracciato: pagine del gestore e inviti
- Area riservata al personale MARE.A (`/Gestione`): elenco aziende, creazione azienda (con prima sede e impostazioni), sedi aggiuntive (rispetto del limite contrattuale), utenti con ruolo e sede, stato del servizio (attivo, sospeso, in chiusura), registro attività consultabile e filtrabile per azienda.
- Invito: alla creazione di un utente si genera un link monouso valido 7 giorni con cui la persona sceglie la password (`/Invito/...`). Nel database resta solo l'impronta del codice. Lo stesso meccanismo serve da «Reimposta password». Per ora il link lo copia e lo manda il gestore; l'invio automatico via e-mail è la consegna successiva.
- Disattivando un utente la sua sessione cade alla richiesta successiva. Ogni azione del gestore è scritta nel registro.
- `Utenti`: nuove colonne `ImprontaInvito` e `ScadenzaInvitoUtc`. **Migrazione da creare** (`InvitiUtenti`).

## 24/10/2026 — Tracciato: importazione dal vecchio programma Access
- Nuovo progetto `LaboratorioAI.Tracciabilita.Importazione` (console, da lanciare dalla cartella principale). Legge il file Access in sola lettura con il driver ODBC a 64 bit: non scrive mai nel file Access né in ArcLab/AZW.
- `prova`: rapporto sul contenuto, proposte di abbinamento di fornitori e articoli in due file CSV da compilare in Excel (`Documenti\ImportazioneTracciato`, fuori dal repository perché contengono dati reali), righe sospette con il numero di riga.
- `importa`: scrive solo in un database di prova (nome che finisce per `_Test`), in una transazione, rilanciabile senza duplicati. Fornitori (per codice del vecchio programma), categorie (matrici), articoli, legami articolo-fornitore, lotti, accettazioni e movimenti. Storico: i lotti con data di impegno entrano chiusi (carico e uscita di chiusura «storico importato»), restano in magazzino solo quelli senza impegno.
- Nessuna nuova migrazione.

## 26/10/2026 — Da Validare: correzioni ai limiti; Preventivi: «Duplica»
- Limiti: attività dell'acqua (aw) adimensionale (niente più «unità di misura mancante»); negli alimenti e molluschi MPN e ufc non si confrontano mai (la scheda non si applica, nessun rilievo); nuova etichetta azzurra «Nessun limite di legge» quando nessuna scheda si applica al campione («Nei limiti» solo se c'è stato un confronto). Filtro colori aggiornato.
- AI: regola per l'acqua di rete (conta colonie a 22/36 °C senza valore di legge: mai BORDERLINE né NON CONFORME). Le analisi archiviate vanno rieseguite.
- Preventivi: pulsante «Duplica» (registro e scheda): nuova bozza con le stesse righe e prezzi, data di oggi, numero al salvataggio, revisione Condizioni in vigore; righe con prezzo di oggi diverso segnalate («oggi: … € usa»), ricalcolate se si cambia cliente. Nuovo campo `Preventivi.IdPreventivoOrigine` → migrazione `PreventiviDuplica`.

## 27/10/2026 — Preventivi: ricerca controllo per ID e prezzi delle commesse attive
- Casella «cerca solo per ID»: mostra gli ID che iniziano con le cifre digitate; l'ID esatto compare sempre per primo (anche nella ricerca per nome).
- Accanto a ogni controllo (suggerimenti e righe del preventivo): «commesse attive: min–max € (n)», dalle commesse di laboratorio non scadute di tutti i clienti (sola lettura ArcLab). Nessuna migrazione.

## 28/10/2026 — Ricerca errori: prezzi commesse ArcLab ↔ listini Marea
- Nuova sezione 10: commesse di laboratorio attive di ArcLab (per insediamento e controllo) confrontate con `Marea.dbo.Listini` (cliente via `Marea.Clienti.IDArcLab`, clienti non obsoleti). Fa fede ArcLab. Casi: prezzo diverso (con differenza), manca in Marea, solo in Marea, prezzi diversi tra insediamenti. Filtri per caso e cliente/controllo, esportazione Excel. Sola lettura. `Core/Repository/ConfrontoPrezziMarea.cs` con test. Nessuna migrazione.
- 28/10/2026: conteggio «Commesse con prezzo diverso da Marea» nel Cruscotto controlli (casella 10, «Apri» porta alla sezione già filtrata) e in evidenza in cima ai risultati della sezione 10.
- 28/10/2026: Ricerca errori, nuova sezione 11 «Commesse: altre differenze ArcLab ↔ Marea» (anche nel cruscotto): nome controllo diverso nei listini, commessa mancante in Marea, non attiva in ArcLab, insediamento diverso, controllo non riconoscibile nella descrizione, parametri diversi (per nome, unità campionarie accorpate). Fa fede ArcLab. `Core/Repository/ConfrontoCommesseMarea.cs` con test. Nessuna migrazione.
- 28/10/2026: confronto ArcLab ↔ Marea per contratto: Listini.IDContratto = commessa.NContratto (+ controllo); senza contratto, per cliente e controllo. Sezione 11: la descrizione del listino si confronta con commessa.DescrIncarico (da cui Marea la copia), non più con il nome del controllo.
- 28/10/2026: sezioni 10 e 11 con le date della commessa ArcLab (DataCommessa, DataUltimoRinnovo) e l'ultima modifica del listino nel portale, anche nell'Excel.
- 28/10/2026: sezioni 10 e 11 limitate ai clienti attivi in ArcLab (Clienti.Attivo = 1) e con codice contabilità, sia per le commesse ArcLab sia per listini e commesse di Marea.
- 28/10/2026: sezione 11, nuovo caso «Cliente attivo nel portale ma non in ArcLab»: clienti non obsoleti in Marea che in ArcLab non sono attivi, non hanno codice contabilità, non esistono o non sono collegati; con righe di listino, commesse e ultimo accesso al portale.
- 28/10/2026: il portale prende solo le commesse con numero di contratto (verificato sui dati): «Manca in Marea» (sezioni 10 e 11) solo per commesse con NContratto. Nuova sezione 12 «Commesse attive senza numero di contratto» (errore da sistemare in ArcLab), con filtro per uso, cruscotto ed Excel. Colonna «Ultimo campione» (ultimo lotto ricevuto sulla commessa) nelle sezioni 10, 11 e 12.

## 29/10/2026 — Telefono del cliente dall'anagrafica AZW
- Preventivi analisi: scegliendo un cliente ArcLab il telefono si precompila dall'anagrafica AZW (ANACF), come P.IVA, indirizzo ed e-mail; resta modificabile ed è stampato in prima pagina.
- Contratti di consulenza: nuovo campo «Telefono» del committente (da AZW al caricamento del cliente, modificabile), stampato accanto all'e-mail. Migrazione `PreventiviConsulenzaTelefono`.
- Ricerca del cliente AZW estratta in `Dati/Consulenza/AnagraficaAzw.cs` (stessa regola: codice cliente, codici delle commesse, P.IVA, C.F.).
- 29/10/2026: Da Validare, corretto: se durante un'analisi AI si apriva un altro campione, il risultato (testo ed etichetta nell'elenco) finiva sul campione aperto invece che su quello analizzato. Ora va sempre al campione analizzato; se non è aperto compare un avviso. I pulsanti di analisi mostrano lo stato del campione aperto. Solo JavaScript.
- 29/10/2026: colonna «Redattore» (chi ha creato il preventivo) negli elenchi dei preventivi analisi (IdUtenteCreatore) e consulenza (utente di creazione); nelle consulenze resta anche «Consulente». Nessuna migrazione.
- 29/10/2026: preventivi consulenza in due fasi (Fase 1 obbligatoria, Fase 2 opzionale, testi liberi: titolo, descrizione, condizione di attivazione); ogni attività assegnata a una fase; totali separati (Fase 2 fuori dal corrispettivo); nel contratto le due sezioni e le righe economiche separate; nelle commesse da compilare nota «Fase 2». Attività «personalizzata» (solo Laboratorio AI, tipo commessa 5, IdTipoIncarico −5, creata in automatico e mai disattivata da «Allinea»): nel preventivo titolo, unità, testo, modalità e prezzo liberi. Migrazione `PreventiviConsulenzaFasiPersonalizzate`.

## 29/10/2026 sera – Preventivi consulenza: blocchi di quotazione (osservazioni di Ludo)
- Le «due fasi» sono sostituite dai **blocchi di quotazione**, quanti se ne vuole: titolo e descrizione liberi, totale proprio, i totali non si sommano. Nuova tabella `cons.BlocchiPreventivoConsulenza`; nelle righe `Fase` → `Blocco`; tolti `DueFasi`, `Fase1/2*`, `TotaleFase2*` (solo dati di prova).
- Elenco: il totale è quello del primo blocco, con «+ altri N blocchi».
- Contratto: fascia per il blocco, attività come sottotitolo (omesso se ripete il blocco), righe «per conoscenza» al loro posto, «IVA esclusa» su ogni prezzo, totale per blocco, tabella riassuntiva dei blocchi, linee divisorie, titoli tenuti col testo, nuova pagina per Condizioni economiche e per Condizioni generali e firme. Le analisi restano una sezione a sé.
- Migrazione da creare: `PreventiviConsulenzaBlocchi`.

## 03/11/2026 — Tarature: carta intestata e committente; Monitor: seriale attivo e limiti; Scadenziario clienti: stati e filtri
- Rapporti di taratura (termometri, incubatori, mappatura, bilance): stessa carta intestata (intestazione e piè di pagina) per tutti (`Documenti/CartaIntestataRapporti.cs`). Cliente e insediamento ArcLab in campi propri («Committente», modulo JS `committente-taratura.js`), stampati in testata; l'insediamento solo se indicato. Nome file con il cliente.
- Identificazione dello strumento del cliente libera (marca, modello, matricola): non propone più l'anagrafica clienti ma gli strumenti di quel cliente già tarati. Incubatori: il misuratore è sempre interno (nessuna scelta di origine). Rapporti salvati prima: il cliente si ricava dallo strumento («Origine: Cliente»).
- Monitor Econorma → Sonde: il seriale confrontato con ArcLab è quello attivo per EconormaAcquisizione (`temperature.StoricoSerialiSonde`), non quello di `MappaSonde` (scritto alla prima sincronizzazione e mai aggiornato). Colonna «Limiti monitoraggio» e pagina «Allinea limiti da ArcLab…»: anteprima delle differenze, copia dei limiti di ArcLab in `temperature.Apparecchiature` solo per le apparecchiature spuntate (nuovo permesso `monitortemperature.limiti`). Gli allarmi già registrati non cambiano. La vecchia pagina del sito MonitorTemperature scriveva in `dbo.Apparecchiature`, che non esiste più.
- Scadenziario, termometri clienti: stato «in uso / rinnovata / dismessa» (come le colonne Rinnovato e Dismesso del vecchio Excel), pulsanti sulla riga, proposta di rinnovo dopo una taratura nuova (stesso cliente e tipologia, da spuntare; `Core/Tarature/RinnoviTermometri.cs` con test). Filtri per cliente, riferimento, stato e preavviso in giorni; nascoste le «in uso» scadute da oltre 12 mesi (conta la data di scadenza).
- Migrazione da creare: `TaratureCommittenteRinnovi`.
- 03/11/2026: Metodi e Controlli, «Dai metodi ai controlli»: casella «Prendi i metodi da un controllo» (nome o ID). I metodi del controllo entrano tra quelli scelti; aggiunte le altre prove, «Cerca controlli» con «tutti i metodi» trova i controlli che li contengono (richiesta di Zatterin). Solo JavaScript e vista, nessuna migrazione.
- 03/11/2026: Taratura incubatori, misuratore: testo «misuratore associato alla cella o all'apparecchiatura sulla quale si legge la temperatura da confrontare con il CDR». Il misuratore segue l'origine dell'apparecchiatura (interno: elenco ArcLab; del cliente: testo libero con i misuratori del cliente già tarati, nuovo `api/incubatori/misuratori-cliente`). Nessuna migrazione.
- 03/11/2026: Termometri clienti, tipo e servizio. Tipo: a sonda, a gabbia, pirometro (anche «a infrarossi»), datalogger, altro; servizio: solo taratura oppure termometro nuovo fornito da noi + taratura. Si scelgono nella taratura termometri (passano allo scadenziario) e nella scheda dello scadenziario; colonne e filtri nello scadenziario. All'avvio il tipo delle righe esistenti si ricava dalla tipologia (SONDA → a sonda, PIROMETRO/INFRAROSSI → pirometro…), il servizio dell'archivio è «solo taratura». La proposta di rinnovo confronta il tipo. Migrazione da creare: `TermometriTipoServizio`.

## 05/11/2026 — Tabelle ordinabili; ID controllo in Commesse (richieste di Zatterin)
- In tutto il sito un clic sull'intestazione di una colonna ordina la tabella (crescente/decrescente, ▲▼): numeri all'italiana, date, testo; valore esplicito con `data-ordina`. `wwwroot/js/tabelle-ordinabili.js` nel layout. Clienti da seguire: valori grezzi per semaforo, importi, variazione, perdita, campioni e ultimo campione.
- Ricerche → Commesse (laboratorio): colonna «ID controllo».
- 05/11/2026: Da Validare, limiti: nei piani a 3 classi (n, c, m, M) un risultato «< x» con x tra m e M non è più viola (osservazione di Bertozzi, 9713/01 Enterobatteriacee 5 UC); resta viola se x supera M. Rilievi identici (stesso testo, più unità campionarie) in una riga sola con «(N prove)».
- 05/11/2026: Clienti da seguire, colonna «In corso»: campioni ricevuti dall'ultimo mese intero in poi, che si fatturano il mese dopo (valore a listino, fuori dal semaforo).
- 05/11/2026: Preventivi analisi, insediamento facoltativo per riga di controllo: il prezzo di commessa si prende da quella sede (poi dal cliente, poi listino), stampato nell'offerta sotto il controllo. Migrazione da creare: `PreventiviRigaInsediamento`.
- 05/11/2026: Metodi e Controlli, «Dai metodi ai controlli»: nei suggerimenti, sotto norma e parametro, ID del metodo, denominazione della riga (se diversa), sigla, riferimento del metodo, unità di misura e dettagli; la ricerca trova anche questi (Zatterin: prove su superfici che sembravano uguali). I metodi scelti mostrano l'ID. Nessuna migrazione.

## 05/11/2026 — Analisi AI con i limiti di legge (piscina 9379/01)
- Il prospetto dell'AI riporta, per ogni prova, i limiti di legge validati in Limiti normativi che si applicano al campione («[limite di legge: … (scheda)]»), con la stessa regola del controllo automatico (MotoreSegnalazioni.LimitiPerProva).
- Nuova query ArcLab in sola lettura: RepositoryLimitiArcLab.ProveCampioneAsync (prove di un campione qualsiasi).
- Istruzioni all'AI: fuori dal limite di legge = NON CONFORME; vietato citare intervalli «configurati» non presenti; nel dubbio DA VERIFICARE (mai CONFORME per mancanza di limite).
- Nuovo esito finale «DA VERIFICARE» (giudizio NonDeterminato, mostrato come «Da verificare»). Nessuna migrazione.
- Test: MotoreSegnalazioniTest +2.

## 05/11/2026 — Richieste analisi privati (Mod. Com. 05A)
- Nuova pagina Ciclo analitico → «Richieste analisi privati» (/RichiestePrivati), permesso `richiesteprivati.gestione` (Permessi.Tutti + DatiIniziali).
- Dati cliente (nome, CF, indirizzo, telefono, e-mail, ritiro RdP, pagamento alla consegna del campione o del RdP), campione (matrice ArcLab, punto, data/ora, contenitore Imballaggio ArcLab + fornito da, temperatura), prove dalle commesse in vigore del cliente MAF2 con prezzo IVA inclusa, tempi proposti 10/20 gg lavorativi (Legionella ed esterne 20) modificabili, giudizio di conformità con norma (tendina dalle norme con limiti validati per la matrice) e supplemento 50 € IVA inclusa.
- Nuove tabelle preventivi.RichiesteAnalisiPrivati e preventivi.RigheRichiestaAnalisiPrivati → MIGRAZIONE DA CREARE (RichiesteAnalisiPrivati).
- Modulo Word dal modello wwwroot/docx/Modulo_Richiesta_Analisi_Privati.docx (segnaposto {{…}}), compilato o in bianco. Dalla bozza: Mod. Com. 06 → 05A, Art. 8.2 corretto, tolti allegato interno e segni di revisione.
- ArcLab solo in lettura: RepositoryCommesse.ControlliCommesseClienteAsync; ServizioSegnalazioniLimiti.NormePerMatriceAsync.
- Test: RegoleRichiestePrivatiTest (6).
- (stessa sera) Il modulo diventa **Mod. Com. 05B** e non contiene più le Condizioni: le richiama (Mod. Com. 05A Rev. 0) insieme all'informativa privacy (**Mod. Com. 05C Rev. 0**), da consegnare stampate al cliente. Condizioni separate in docs/Condizioni_Fornitura_Clienti_Privati_Mod_Com_05A_Rev0_BOZZA.docx (8.2 corretto, art. 11.1 richiama il 05C).
- (20:20) Richieste privati: ricerca dei controlli MAF2 in una tabella larga (nome, ID o prova) con «Prove» per vedere il dettaglio prima di aggiungere; nei controlli scelti l'elenco prove con metodo. Modulo 05B e Condizioni 05A su carta intestata MARE.A (stessi loghi di offerte e ordini). Messaggio chiaro se manca la migrazione (prima: «Errore 500»).

## 06/11/2026 — Ordine di Accettazione per i privati
- Dalla maschera «Richieste analisi privati» si genera ora l'**Ordine di Accettazione per i privati** (Mod. Com. 05B) con lo stesso aspetto degli Ordini di Accettazione: carta intestata, riepilogo con prezzi IVA inclusa (totale, di cui imponibile, di cui IVA), pagamento, consegna del RdP, riquadro condizioni adattato alle Condizioni per i privati (Mod. Com. 05A), allegato tecnico (prelievo: Cliente; fuori limite; giudizio e pareri; tempi; prove per controllo con §), sottoscrizione, approvazione specifica 1341-1342 e consenso facoltativo. Anche in bianco. Nuovi file: OrdinePrivatoDocx.cs, TestiOrdinePrivato.cs; in OrdineAccettazioneDocx solo helper resi internal.
- Maschera: comunicazione anticipata dei fuori limite (predefinito no), giudizio e pareri (predefinito no), consenso comunicazioni commerciali (facoltativo), imponibile e IVA sotto il totale.
- Nuovi campi RichiesteAnalisiPrivati.ComunicazioneFuoriLimite e ConsensoComunicazioni → MIGRAZIONE DA CREARE (RichiestePrivatiOrdine). Il totale imponibile ora comprende il supplemento scorporato.
- Il modello Modulo_Richiesta_Analisi_Privati.docx non è più usato (si può eliminare).
- docs: Condizioni 05A rifatte dalla nuova bozza (8.2 corretto, 11.1 richiama 05C, su carta intestata); Informativa privacy con codice Mod. Com. 05C Rev. 0 in intestazione.

## 06/11/2026 — Preventivi: insediamento in offerta solo se spuntato (richiesta Zatterin)
- Nuova casella «Riporta nell'offerta l'insediamento scelto sulle righe» (Preventivo.MostraInsediamenti, predefinito no). Senza spunta l'offerta è come prima; l'insediamento sulla riga continua a servire per il prezzo. → MIGRAZIONE DA CREARE (PreventiviMostraInsediamenti).
- (13:45) Ordine privati, Sottoscrizione: la frase finisce con «l'informativa sul trattamento dei dati personali clienti privati» (senza codice).
- (14:10) Ordine privati in bianco: «Allegato 1» con i controlli delle commesse MAF2 (prezzo IVA inclusa, tempi, prove come nell'allegato tecnico, § e frasi dei laboratori terzi).

## 06/11/2026 — Ordini di Accettazione dei lotti MAF2 con il modello dei privati
- In «Ordini di Accettazione» i lotti del cliente con codice contabilità MAF2 (Spettabile cliente) escono, in Word e PDF (anche portale e «Stampa da firmare»), con l'Ordine di Accettazione per i privati (Mod. Com. 05B): destinatario «Spettabile cliente – <Campione.LuogoPrelievo>», prezzi IVA inclusa con imponibile e IVA, condizioni 05A, prelievo del Cliente, fuori limite e giudizio da ArcLab (senza indicazione: no), pagamento e consenso da spuntare a mano. Tutti gli altri clienti invariati.
- OrdineAccettazione.ClienteCodContabilita (da Clienti.CodContabilita, sola lettura). OrdinePrivatoDocx riscritto su un modello unico (bianco, maschera, lotto ArcLab); PDF dei privati per conversione del Word.
- Ordine in bianco: casella da spuntare accanto a ogni controllo dell'Allegato 1.

## 06/11/2026 — Invio campioni ai laboratori esterni
- Nuova pagina Ciclo analitico → «Invio campioni ai lab esterni» (/InviiLaboratoriEsterni), permesso `labesterni.invio` (Permessi.Tutti + DatiIniziali).
- Dalla query Access di MARE.A: campioni non certificati, data analisi (DataRic + RitardoAnalisi) nel periodo, prove del laboratorio non esitate (nessun valore, non verificate) e non escluse/annullate. Laboratorio preselezionato: Centro Ricerche Marine (-867474552).
- Excel (ClosedXML) con le colonne della query, una riga per campione e prova; e-mail ai destinatari del laboratorio, Cc e Reply-To refertazione@mare-a.com. IServizioEmail.InviaConAllegatiAsync: nuovo parametro opzionale «copia» (Cc).
- Nuove tabelle accettazione.ContattiLaboratoriEsterni, InviiLaboratoriEsterni (con l'Excel inviato), RigheInvioLaboratorioEsterno («già inviato») → MIGRAZIONE DA CREARE (InviiLaboratoriEsterni).
