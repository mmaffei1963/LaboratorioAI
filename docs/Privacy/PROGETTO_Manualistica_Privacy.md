# Manualistica Privacy — integrazione in Laboratorio AI

## Specifica di integrazione — versione 2

| | |
|---|---|
| **Data** | 31 ottobre 2026 (versione 1: 29 ottobre) — risposte e aggiornamento di Daniele Vaccarini il 3 novembre 2026, segnati **[DV 03/11]** |
| **Committente** | MARE.A srl — Mirko Maffei, Responsabile di Laboratorio |
| **Documento gemello** | `INTEGRAZIONE_Manualistica.md` di Daniele Vaccarini (21–29/10/2026, 650 righe) |
| **Natura** | Parte architetturale: come il modulo si innesta in Laboratorio AI. La parte funzionale sta nel documento di Vaccarini. |
| **Stato** | Decisioni del §4 prese il 31/10/2026, approvatori il 01-02/11/2026; dodici punti del §3 approvati. Modello dati in collaudo, pagina dei modelli realizzata. |

---

## 1. Cosa è cambiato dalla versione 1

Vaccarini ha risposto a tutte e dieci le domande e ha consegnato due archivi: i prototipi Python e i 31 modelli nella versione 21 del 30/10, con quindici documenti di progetto.

**Tre cose rovesciano quello che sapevamo.**

**I prototipi Python non sono il riferimento migliore.** Nella versione 1 avevo consigliato di leggerli invece del JavaScript. Sbagliato: Vaccarini scrive che i `gen_*.py` sono i primi tentativi del 22–23 ottobre e che le regole aggiunte dopo stanno **solo in `motore.js`**, che è il riferimento per la riscrittura in C#. I Python restano utili per capire l'impianto dell'organigramma e della carta intestata, non per le regole.

**Esiste già una specifica funzionale completa.** `INTEGRAZIONE_Manualistica.md` non è un appunto: propone lo schema database `man` con venti tabelle, i permessi, le schermate, il flusso di approvazione dei modelli a due approvatori, e tiene un registro di cinquanta decisioni datate. È più avanti del documento che avevo scritto io, sul piano funzionale. Dentro ci sono **dodici punti marcati «da concordare con Mirko Maffei»**: sono il §3 qui sotto.

**Il seguito del lavoro va a sbattere contro Laboratorio AI.** Alla domanda sui prossimi manuali, Vaccarini risponde: «il prossimo sarà POOL SLUG (manuale piscina), poi LEGIO SLUG (DVR legionellosi)». Sono due documenti che Laboratorio AI **genera già, in produzione, collaudati su dati reali**. La guida di POOL SLUG è già scritta e pronta per aprire il progetto. Questa è la decisione A del §4, ed è urgente: ogni settimana che passa è lavoro che si duplica.

---

## 2. Le risposte, in breve

| # | Domanda | Risposta |
|---|---|---|
| 1 | Prototipi Python | Consegnati. Ma il riferimento è `motore.js` |
| 2 | Modelli originali | Consegnati (versione 21, 30/10). Li carica lui nei File del progetto a ogni modifica |
| 3 | Revisione ed efficacia | Le tabelle di revisione **ci sono già** nei modelli, vuote perché è una versione di prova |
| 4 | Chi aggiorna i modelli | Vaccarini, per legionella, privacy e piscine: è revisore e aggiornatore |
| 5 | Solo strutture ricettive? | Sì per ora. **Ma in futuro la suddivisione sarà per macro attività lavorative, con differenze anche regionali** |
| 6 | Voci scritte a mano | Devono entrare nelle tendine per le compilazioni successive, con possibilità di eliminarle se obsolete |
| 7 | Prezzi 300/200 | Veri, modificabili a mano |
| 8 | Firma OTP | Unificare il sistema. Serve ad **avallare un contratto e la manualistica, con assunzione di responsabilità** |
| 9 | Ritardo e promemoria | Decisioni da mantenere |
| 10 | Prossimi manuali | POOL SLUG, poi LEGIO SLUG |
| — | Convenzione dei tag | «Userò sempre gli stessi tag» |

Due risposte hanno conseguenze che vanno oltre la domanda.

La **5** dice che l'impianto «solo hotel» è temporaneo e che la divisione vera sarà per macro attività, con norme diverse anche da regione a regione. Quindi le funzioni aziendali non vanno scritte nel codice: il modello dati deve reggere fin da subito la terna **tipo di manuale × attività × regione**. Nello schema `man` di Vaccarini c'è `TipiManuale` e c'è `FunzioniAziendali` legata al tipo: manca la dimensione regionale, che invece nelle piscine di Laboratorio AI esiste già (Emilia-Romagna e Marche).

La **8** dice che la firma serve ad avallare un contratto. Non è una conferma di ricezione: è una sottoscrizione. Vale quindi lo stesso percorso delle Condizioni Generali, parere legale compreso (decisione C del §4).

---

## 3. I dodici punti «da concordare con Mirko»

Per ciascuno: cosa chiede Vaccarini, cosa c'è già in Laboratorio AI, cosa propongo. La colonna che conta è l'ultima.

### 3.1 Impostazioni del modulo
Chiede se fare una tabella `man.ImpostazioniModulo` o riusare `app.Configurazioni`.
→ **Riusare `app.Configurazioni`**, con una classe `ConfigurazioniManualistica` sullo stampo di `ConfigurazioniPreventiviConsulenza.cs`, e la pagina dentro Amministrazione → Impostazioni documenti, dove stanno già cartelle, testi e revisioni. Una tabella in più non aggiunge niente e sposta le impostazioni in un posto dove nessuno le cerca.

### 3.2 Firma grafica disponibile anche nella Web
Il riquadro di firma vive oggi nel Portale (`/Firma/{codice}`), la check list sta nella Web: sono due progetti separati.
→ Estrarre il modulo ES in un file condiviso incluso da entrambi i progetti, senza toccare il comportamento del Portale. È l'unico punto dove i due progetti condividono codice di interfaccia: va fatto con attenzione al publish, perché le due cartelle di produzione sono distinte.

### 3.3 Dove salvare i file generati
→ **Documenti generati: sul server**, nella cartella configurata in Impostazioni documenti, con impronta e percorso nel database. È quello che fanno già DVR e piscine, e `log.RegistroDocumenti` verifica pure che il file ci sia davvero.
→ **Modelli: nel database**, come propone lui (`man.RevisioniModello` con contenuto, impronta, stato e due approvazioni). Qui la ragione è diversa: serve sapere con quale revisione è stato generato un documento consegnato due anni prima, e un file in cartella lo può sostituire chiunque senza lasciare traccia. La cartella sul server (`\\SRVMAREA\marea\Manualistica\Privacy\Modelli\`) resta il posto da cui si caricano le revisioni, non la fonte da cui il programma legge.

Questo però **cambia il modo in cui funzionano oggi DVR e piscine**, che leggono i modelli da cartella. Vedi decisione B.

### 3.4 Dove mettere la voce di menu
→ **Cartella nuova «Manualistica»**. Se poi il modulo assorbe DVR Legionella e piscine (decisione A), la cartella «Legionella e piscine» confluisce lì e il menu resta pulito.

### 3.5 Riuso di `log.RegistroDocumenti` per il cloud del cliente
→ Sì. Esiste già per i documenti consegnati a Palomar e fa esattamente quel mestiere. `man.PubblicazioniCloud` diventa superflua: basta una riga nel registro esistente con destinazione «Logi».

### 3.6 Anagrafica da partita IVA
→ **Esiste già il codice che fa questa cosa**: `AnagraficaAzw.TrovaAsync` in `Dati\Consulenza\AnagraficaAzw.cs` cerca il cliente partendo dal codice ArcLab, poi dai codici delle commesse, poi dalla partita IVA, poi dal codice fiscale. È stato estratto il 29/10 proprio per essere riusato. Si legge dalle copie notturne (`provv.AzwClienti`, insediamenti in `clienti`), non da ArcLab in diretta: più veloce e nessun rischio sul gestionale.

### 3.7 Invio ritardato e promemoria
→ Riusare il meccanismo già in esercizio per l'importazione AZW notturna e per il run notturno degli Ordini di Accettazione, con lo stato nel database come lui propone. Attenzione al limite Brevo: piano Starter, 20.000 messaggi al mese, e per gli invii massivi delle Condizioni c'è già un tetto di 1.000 al giorno. I promemoria OTP quotidiani per sette giorni vanno contati dentro quel budget.

### 3.8 Rapporto fra il contratto privacy e le Condizioni Generali
Qui serve una decisione tua, perché i documenti contrattuali in Laboratorio AI stanno già in **tre posti**: le Condizioni Generali di fornitura (schema `contratti`), i contratti di consulenza dei preventivi (schema `cons`), e ora il contratto di fornitura servizi privacy.
→ Propongo che il contratto privacy resti un documento del modulo Manualistica (lo genera la check list), ma che **la sua firma passi dall'infrastruttura delle Condizioni**. Diverso è il problema di merito: il contratto privacy contiene una nomina di MARE.A a Responsabile del trattamento ex art. 28, e Vaccarini stesso segnala che fa doppione con la lettera «Responsabile del trattamento – CONSULENTE MARE.A» del pacchetto. Ha già deciso che quando c'è il contratto quella lettera non si genera. Da verificare con Giovanna che la nomina dentro il contratto basti.

### 3.9 Firma OTP: come funziona davvero
→ L'infrastruttura esiste: codici casuali a 256 bit di cui resta in archivio solo l'impronta, catena di impronte sugli eventi con trigger contro modifica e cancellazione, ricevuta, inviti e promemoria. La differenza da colmare è che per le Condizioni il cliente riceve un **link personale**, mentre il prototipo privacy prevede un **codice OTP via e-mail**. Propongo il link personale, che è già collaudato, chiamandolo «firma con codice» nei testi per il cliente. Se invece si vuole il codice numerico, è uno sviluppo nuovo sul Portale: va messo in conto.

**[DV 03/11]** D'accordo sul link personale: è quello che intendevo con «unificare il sistema» (risposta 8). Restano le regole già decise per questa firma: documenti al cliente solo dopo la conferma, promemoria uno al giorno per sette giorni, avviso al consulente alla scadenza. Nella versione offline la firma con codice non c'è.

### 3.10 Tabella dei comuni
→ Prima di creare `man.Comuni` con 7.904 righe da fonte esterna, va verificato se una tabella comuni esista già in ArcLab: gli insediamenti hanno comune, provincia e CAP, quindi qualcosa c'è. Se non basta, va bene il seed con migrazione, verificando licenza e aggiornamento della fonte.

### 3.11 Conversione in PDF
→ Confermato: `ConvertitoreWordPdf` è in `LaboratorioAI.Documenti` ed è comune a tutti i generatori. Dal 29/10 ha anche il font resolver con Liberation Sans incorporato, che serve proprio a generare PDF sul server dove i font di sistema non ci sono.

### 3.12 Chi sono i due approvatori
→ **Deciso il 01/11/2026, semplificato il 02/11/2026: approva Mirko Maffei, una firma sola.** Vedi §4 B.

---

## 4. Le decisioni prese (31/10/2026)

### A. I documenti di Vaccarini sostituiscono quelli di Laboratorio AI — **deciso**

**Decisione di Mirko Maffei, 31/10/2026:** quello che produce Vaccarini va a sostituire i generatori esistenti, e così sarà per tutto il resto. POOL SLUG prosegue: il manuale piscine che ne esce prende il posto di quello che Laboratorio AI genera oggi; lo stesso per LEGIO SLUG e il DVR Legionellosi.

Conseguenze da tenere presenti, perché cambiano il modo di lavorare:

- **Il motore nasce generico, non per la privacy.** Non si scrive un generatore privacy da estendere dopo: si scrive il motore della manualistica e la privacy è il primo tipo. Il modello dati regge fin da subito la terna tipo di manuale × attività × regione (risposta 5 di Vaccarini, e le piscine hanno già Emilia-Romagna e Marche).
- **I moduli attuali non si spengono il giorno del rilascio.** DVR Legionella e Piscine restano in esercizio finché il nuovo non ha prodotto gli stessi documenti su casi reali. Il doppio regime è temporaneo e va dichiarato: con una data, non «finché non siamo pronti».
- **I dati già salvati vanno decisi.** Le schede piscina salvate in `app.DvrPiscine` e il registro dei DVR prodotti sono storia che non si butta: o migrano nelle tabelle nuove, o restano consultabili in sola lettura. Da stabilire prima del rilascio, non dopo.
- **Le check list esistenti sono materiale da riusare.** Le domande che i moduli piscine e legionella già pongono sono il punto di partenza delle nuove check list: non si riparte dal foglio bianco.

### B. Modelli nel database con controllo delle revisioni — **deciso**

**Decisione di Mirko Maffei, 31/10/2026:** si adotta `man.RevisioniModello` — contenuto, impronta, stato e approvazione nel database. Dato che i generatori attuali verranno sostituiti (decisione A), il regime a cartella sparisce da sé: non restano due modi diversi di gestire i modelli.

La cartella sul server resta il posto da cui si caricano le revisioni e dove i modelli si lavorano, non la fonte da cui il programma legge.

**Approvatori (deciso il 01/11/2026, semplificato il 02/11/2026):** approva **Mirko Maffei**, con una sola firma, anche sui modelli che ha caricato lui. La doppia approvazione proposta da Vaccarini è stata tolta: nella pratica i modelli li prepara il consulente e li approva il responsabile, quindi autore e approvatore restano persone diverse senza bisogno di imporlo per regola, e il caso in cui coincidono è quello dei caricamenti di prova. Di ogni revisione resta registrato chi l'ha caricata e chi l'ha approvata. La Qualità del laboratorio non entra nel giro: la manualistica è consulenza, non laboratorio.

### C. Il valore legale della firma

Vaccarini ha risposto che la firma serve ad avallare un contratto e la manualistica, con le assunzioni di responsabilità. È una sottoscrizione, non una presa visione. Nel suo documento è scritto correttamente che si tratta di firma elettronica semplice — immagine, senza dati biometrici — ma il punto non è la tecnica: è se quella firma regge come sottoscrizione di un contratto e di nomine ex art. 28.

Serve il passaggio da Giovanna, sugli stessi punti già affrontati per la Rev. 19 delle Condizioni. Da fare **prima** che la funzione arrivi ai consulenti, non dopo.

---

## 5. Cosa resta da chiarire con Vaccarini

1. **Dimensione regionale.** La risposta 5 dice che le norme cambiano anche per regione. Nello schema `man` non c'è. Dove entra: nel tipo di manuale, nel modello, o nella condizione di inclusione?
2. **Voci scritte a mano (risposta 6).** Chi approva l'ingresso di una voce nelle tendine, e chi la dichiara obsoleta? Serve una pagina di gestione e un permesso.
3. **I modelli nella sezione File del progetto Claude.** Va bene per lavorarci, ma la fonte da cui Laboratorio AI genera deve essere la revisione approvata nel sistema. Vanno tenuti allineati, e il punto di verità dev'essere uno solo.
4. **Compattazione delle nomine.** Nel prototipo è stimata dal numero di funzioni e va controllata in Word: in C# va risolta davvero, perché la regola «ogni lettera in una pagina» è tassativa.
5. **Le due interpretazioni ancora aperte** sulla DPIA (marcatura del 23/10) e le combinazioni 4 e 5 della struttura base.

### [DV 03/11] Risposte

| # | Risposta | Stato |
|---|---|---|
| 1 | La proposta del modello dati (due colonne `CodiceAttivita` e `SiglaRegione` su `man.ModelliDocumento`, con ricerca prima della riga specifica e poi della generica) mi sembra il posto giusto. Su **come si divideranno le macro attività** non ho ancora una risposta: la do quando arriva la seconda attività | **aperto** |
| 2 | Ammette e dichiara obsolete le voci chi ha `manualistica.impostazioni`, dalla pagina di gestione; obsoleta = `Attivo = false`. Coerente con la decisione del 23/10 e con la risposta 4. Proposta di assegnazione: Vaccarini | **confermato da Maffei 04/11** |
| 3 | D'accordo: il punto di verità è la **revisione approvata in `man.RevisioniModello`**. I File del progetto Claude sono l'area dove si marcano i modelli. Regola: un modello cambiato nei File non vale finché non è caricato e approvato in Laboratorio AI; l'impronta SHA-256 permette di vedere se le due copie coincidono | **confermato da Maffei 04/11** |
| 4 | **Già deciso il 23/10** (`MARCATURA_Nomine.md` §A): impaginare con il convertitore, contare le pagine, compattare a livelli solo la lettera che sfora; se il livello 3 non basta, avviso e stop. La stima della prova è un limite del browser. Dettaglio nell'inventario §6.1 | **chiuso** |
| 5a | **DPIA: chiuse il 23/10** (`MARCATURA_DPIA_Informative.md` §D, punti 1 e 2): confermato cosa togliere nella c4 e confermate le parti DVR condizionate; approvata la frase senza registrazione | **chiuso** |
| 5b | **Combinazioni**: **deciso il 03/11** — valgono tutte, 12 in tutto; nella DPIA una frase per ciascun impianto quando i due hanno modalità diverse (`MARCATURA_DPIA_Impianti_misti.md`, bozza da approvare). Cambia la regola della prova delle otto combinazioni | **chiuso** |

---

## 5 bis. [DV 03/11] Cosa è successo dal 31/10

**Versione 22 della prova (03/11).** Correzioni dopo l'uso: impaginazione del Registro (copertine su pagina dispari, niente pagine bianche fisse; l'esempio passa da 157 a 138 pagine), elenchi «Destinatari» numerati da 1, accesso alle immagini del passo 3 che corregge gli elenchi di Registro, DPIA e nomine, riga backup degli archivi dalla check list, cartelli, data breach, informativa lavoratori in una pagina. Sono tutte nel §13 dell'inventario, da portare nel C#.

**Riepilogo dei dati raccolti generato dal programma**, su una nuova carta intestata senza marchio ACCREDIA: il 32° modello controllato (`CARTA_RIEPILOGO`).

**Versione offline «Privacy Slug offline» 1.0 e 1.1 (03/11).** Pagina installabile su PC, tablet e telefono, senza collegamento a Laboratorio AI: anagrafiche a mano, solo Word (i PDF si salvano da Word), niente ricerca per P.IVA, firma con codice, invio ritardato né promemoria. È un ponte per lavorare subito, non un'alternativa: il riferimento resta il C#. Due punti:
- **da concordare con Mirko Maffei**: dove pubblicarla in https per installarla su tablet e telefono. Proposta: cartella protetta sul server MARE.A, non un hosting pubblico, perché i modelli sono documenti interni;
- **rischio**: dati dei clienti (anche IBAN e nomi di LR e soci) e firma del consulente restano nella memoria del browser del dispositivo; i modelli che usa **non sono ancora approvati**. Solo dispositivi aziendali protetti, e i documenti che produce vanno riletti come bozze.

---

## 6. Fasi aggiornate

| Fase | Contenuto | Blocco |
|---|---|---|
| **0** | Decisioni del §4 e dodici punti del §3 | **Chiusa** — approvatore deciso il 02/11 |
| **1** | Inventario delle regole di `motore.js`, con la traduzione in C# e OpenXml | **[DV 03/11]** Inventario del 31/10 aggiornato alla versione 22. Resta da rileggere la versione 22 di `motore.js` per l'ordine esatto delle regole nuove |
| **2** | Schema `man`, migrazione, permessi `manualistica.*`, menu, check list a 6 passi, salvataggio | — |
| **3** | Motore C# sui 31 modelli | — |
| **4** | Collaudo: uscite identiche al prototipo, su cliente reale, con Vaccarini | — |
| **5** | Firma, invio, promemoria, registro | Dipende da C |
| **6** | Contratto di fornitura servizi | Dipende da C e da §3.8 |
| **7** | Piscine e legionella con lo stesso motore, e spegnimento dei moduli attuali | Dopo la 4 |

I permessi sono quelli proposti da Vaccarini — `manualistica.consulta`, `manualistica.genera`, `manualistica.modelli.gestisci`, `manualistica.modelli.approva`, `manualistica.impostazioni` — e sostituiscono i `privacy.*` della versione 1: sono migliori, perché valgono per tutta la manualistica e non solo per la privacy.

---

## 7. Materiale

| Elemento | Dove |
|---|---|
| Specifica funzionale di Vaccarini | `INTEGRAZIONE_Manualistica.md` (nei suoi documenti di progetto) |
| Regole del motore, versione buona | `versione_prova_browser/motore.js` — **[DV 03/11]** versione 23 del 03/11 (12 combinazioni e DPIA per impianto), con `modelli.js` e `riepilogo.js`; la 22 resta il riferimento per tutto il resto, invariato |
| Marcatura dei modelli, per documento | i nove file `MARCATURA_*.md` |
| Guida del prossimo progetto | `GUIDA_POOL_SLUG.md` — da congelare se vale la decisione A1+A3 |
| 31 modelli marcati, versione 21 del 30/10 | `Modelli_marcati\` — **[DV 03/11]** cinque cambiati il 03/11 (REGISTRO, DPIA_VIDEO, CARTELLO_VIDEO, CARTELLO_PISCINA, PROC_DATA_BREACH) più il nuovo CARTA_RIEPILOGO; script `fix_modelli_0311.py` |
| Versione offline | «Privacy Slug offline» 1.1 del 03/11; note in `INTEGRAZIONE_Privacy_offline.md` |
| Lavoro di completamento del 03/11 | `SPECIFICA_Checklist_Privacy.md`, `MODELLO_DATI_Manualistica.md`, `INVENTARIO_Motore_Manualistica.md`, con le aggiunte segnate [DV 03/11] |
| Prototipi Python | `generazione\`, `marcatura\`, `utilita\` — utili per organigramma e carta intestata |
| Check list di prova | `esempi_checklist\` (dati inventati) |

---

## 8. [DV 03/11] Punti aperti, tutti in un posto

| # | Punto | Chi decide | Dove è descritto |
|---|---|---|---|
| 1 | Divisione per macro attività e regione | Vaccarini | §5 punto 1 |
| 2 | ~~Combinazioni di videosorveglianza e piscina~~ — **deciso 03/11: tutte e 12 ammesse**. **[MM 04/11] Ancora aperto**: la DPIA nella cartella «FILE PRIVACY SLUG» è quella della versione 22 (lo dice il suo LEGGIMI) e non contiene i cinque controlli contenuto nuovi del §3.4. Caricata e approvata come Rev. 02, ma va sostituita con quella della versione 23 | Vaccarini | §5 punto 5b; `MARCATURA_DPIA_Impianti_misti.md` |
| 3 | ~~I 17 fornitori: elenco chiuso o anagrafica~~ — **deciso 03/11: elenco chiuso nelle tendine** | Vaccarini | specifica §8 |
| 4 | ~~Semplificazione della scheda archivio informatico~~ — **deciso 03/11: scheda completa, con «copia la scheda» e ogni campo modificabile** | Vaccarini | specifica §8 |
| 5 | Blocchi ripetuti: colonne in `ValoriCompilazione` o tabelle dedicate | Maffei | modello dati §4 |
| 6 | Cosa vede un consulente con `manualistica.consulta` | Maffei | modello dati §7 |
| 7 | Valori iniziali di `AssistenzaPredefinita` e `CompilazioneManualeAttiva` | Vaccarini | modello dati §8 |
| 8 | Righe iniziali con `Popolamento.EseguiAsync` | Maffei | modello dati §10 |
| 9 | Dove pubblicare la versione offline | Maffei | §5 bis |
| 10 | Valore legale della firma: parere di Giovanna | Maffei | §4 C |
| 11 | Nomina art. 28 dentro il contratto basta al posto della lettera Consulente MARE.A | Giovanna | §3.8 |
| 12 | Caricamento sul cloud del cliente: modalità tecnica | Palomar | §3.5 |
| 13 | Tabella dei comuni | Maffei | §3.10 |

---

*MARE.A srl — Cattolica (RN) — ottobre 2026, aggiornato novembre 2026*
