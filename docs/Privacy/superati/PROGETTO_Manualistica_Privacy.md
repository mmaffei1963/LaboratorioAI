# Manualistica Privacy — integrazione in Laboratorio AI

## Specifica di integrazione — versione 2

| | |
|---|---|
| **Data** | 31 ottobre 2026 (versione 1: 29 ottobre) |
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

---

## 6. Fasi aggiornate

| Fase | Contenuto | Blocco |
|---|---|---|
| **0** | Decisioni del §4 e dodici punti del §3 | **Chiusa il 31/10**, salvo il secondo approvatore |
| **1** | Inventario delle regole di `motore.js`, con la traduzione in C# e OpenXml | In corso |
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
| Regole del motore, versione buona | `versione_prova_browser/motore.js` |
| Marcatura dei modelli, per documento | i nove file `MARCATURA_*.md` |
| Guida del prossimo progetto | `GUIDA_POOL_SLUG.md` — da congelare se vale la decisione A1+A3 |
| 31 modelli marcati, versione 21 del 30/10 | `Modelli_marcati\` |
| Prototipi Python | `generazione\`, `marcatura\`, `utilita\` — utili per organigramma e carta intestata |
| Check list di prova | `esempi_checklist\` (dati inventati) |

---

*MARE.A srl — Cattolica (RN) — ottobre 2026*
