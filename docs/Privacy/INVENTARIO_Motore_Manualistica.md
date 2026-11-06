# Inventario delle regole del motore

## Da `motore.js` (987 righe nella versione del 30/10) alla riscrittura in C# con OpenXml

| | |
|---|---|
| **Data** | 31 ottobre 2026 — aggiornato da Daniele Vaccarini il 3 novembre 2026 |
| **Fonte** | `versione_prova_browser/motore.js`, versione del 30/10/2026 consegnata da Daniele Vaccarini. **[DV 03/11]** Le regole della versione 22 del 03/11 sono nel §13, nuovo; dove cambiano una regola dei paragrafi precedenti, il paragrafo lo dice |
| **Scopo** | Elencare tutto quello che il motore fa, nell'ordine in cui lo fa, con accanto come si traduce in C#. È il documento su cui si scrive il codice della fase 3. |
| **Avvertenza** | I prototipi Python (`gen_*.py`) sono fermi al 22–23 ottobre. Le regole aggiunte dopo stanno **solo** qui. Dove i due divergono, vale questo documento. |

---

## 1. Come è fatto un modello

Un modello è un `.docx` normale in cui le parti da trattare sono racchiuse in **controlli contenuto** di Word (`w:sdt`), ciascuno con un **tag** che dice al motore cosa farne. Il tag ha la forma `PREFISSO:NOME`.

| Prefisso | Quanti | Cosa fa il motore |
|---|---|---|
| `CAMPO:` | 148 | Scrive un valore al posto del contenuto |
| `SE:` | 31 | Tiene o toglie il blocco secondo una condizione |
| `TESTO:` | 17 | Sceglie fra testi alternativi, o ne costruisce uno |
| `RIPETI:` | 14 | Duplica il blocco una volta per elemento |
| `LISTA:` | 8 | Costruisce un elenco da una selezione multipla |
| `SEZIONE:` | 7 | Ambito di una funzione aziendale, usato nelle nomine |
| `FIRMA:` | 3 | Inserisce l'immagine di una firma |
| `SPECIALE:` | 2 | Tabella delle revisioni e organigramma |

Il motore lavora su `word/document.xml` e su tutti gli `header*.xml` e `footer*.xml`, in ordine alfabetico.

---

## 2. L'ordine delle operazioni

**L'ordine non è negoziabile: cambiarlo rompe i documenti.** Per ogni parte del file, nell'ordine:

| # | Passo | Dove si applica |
|---|---|---|
| 1 | Toglie i commenti XML (`commentRangeStart/End`, `commentReference`) | tutte le parti |
| 2 | Trattamento speciale secondo il documento: nomine, tabelle autorizzati, registro, lettere ripetute | solo `document.xml` |
| 3 | Rifiniture specifiche del documento (§7) | solo `document.xml` |
| 4 | «HOTEL» evidenziato nell'intestazione del Registro → nome dell'insediamento | header del Registro |
| 5 | Toglie le evidenziazioni gialle | tutti gli header |
| 6 | Ripete le sedi di trattamento | tutte le parti |
| 7 | Ripete le funzioni che vedono in diretta | tutte le parti |
| 8 | Applica gli elenchi `LISTA:` | tutte le parti |
| 9 | Applica le condizioni `SE:` | tutte le parti |
| 10 | Applica i testi alternativi `TESTO:` | tutte le parti |
| 11 | Scrive i campi `CAMPO:` | tutte le parti |
| 12 | Inserisce le firme `FIRMA:` | solo Registro e Contratto |
| 13 | Firma del Legale Rappresentante negli altri punti (§8) | solo `document.xml` |
| 14 | Toglie le evidenziazioni | solo DPIA |
| 15 | Toglie i `lastRenderedPageBreak` | tutte le parti |
| 16 | Rifiniture finali | solo `document.xml` |
| 17 | Scioglie i controlli rimasti | tutte le parti |
| 18 | Sistema i vuoti (§4.5) | tutte le parti |

Dopo il ciclo, sul pacchetto: si aggiungono le relazioni delle immagini di firma, si eliminano `word/comments*.xml` con le voci corrispondenti in `[Content_Types].xml` e nelle relazioni, si aggiunge il `Default` per l'estensione `png` se manca, e per il Registro si scrive `updateFields` in `settings.xml`.

**Perché i campi si scrivono per ultimi.** Un campo appena compilato viene rinominato da `CAMPO:X` a `CAMPO:_X`, così i passi successivi non lo riscrivono. Alla fine `pulisciControlli` rimette il nome originale. Se si scrivessero i campi prima delle condizioni, le copie generate dalle ripetizioni risulterebbero già compilate con i valori sbagliati.

---

## 3. Le condizioni

Si calcolano una volta sola dalla check list. Due forme: `NOME` (vero/falso) e `NOME=VALORE`.

**Dalle due tendine della videosorveglianza** (`VIDEO` e `PISCINA`, valori `NESSUNA` · `VISIONE_REG` · `SOLO_VISIONE` · `SOLO_REG`) si derivano: `VIDEO_PRESENTE`, `PISCINA_PRESENTE`, `VIDEO_REGISTRAZIONE`, `PISCINA_REGISTRAZIONE`, `VIDEO_O_PISCINA`, `QUALSIASI_REGISTRAZIONE`, `VIDEO_NON_REGISTRA`, `PISCINA_NON_REGISTRA`.

**Dal resto della check list**: le cinque finalità facoltative, `FUNZIONE_CAPO_CHEF`, `GUARDIANO_PRESENTE`, `MANUTENTORI_INTERNI`, `CRM_PRESENTE`, `WIFI_PRESENTE`, `BACKUP_SERVER`, `BACKUP_DISPOSITIVI_ESTERNI`, `BACKUP_CLOUD`, `NESSUN_BACKUP`, `SOSTITUTO_LR_DATA_BREACH`, `CONTRATTO_FORNITURA`, `ASSISTENZA_ANNUALE`.

**A valore**: `SALA` (`CAMERIERI_SALA` · `CAPO_SALA` · `NESSUNO`), `PIANI` (`INTERNE_NOMINATE` · `INTERNE_NON_NOMINATE` · `DITTA_ESTERNA`), `GUARDIANO`.

**[DV 03/11] Condizioni da aggiungere all'elenco:**
- `VISIONE_DIRETTA` — nuova nella versione 22: telecamere presenti e non «solo registrazione». Governa nel Registro la frase sulla consultazione in tempo reale (§13). Con le combinazioni nuove deve significare «almeno un impianto con visione». **Nella versione 22 non era così** (guardava solo la videosorveglianza): corretta nella **versione 23** del 03/11, che è il nuovo riferimento.
- `IMPIANTI_MISTI_REGISTRAZIONE` e `IMPIANTI_MISTI_VISIONE` — nuove, decise il 03/11 per la DPIA con i due impianti in modalità diverse (`MARCATURA_DPIA_Impianti_misti.md`). Nel prototipo dalla versione 23.
- `VIDEO_ACCESSO_FUNZIONE` — già usata nelle nomine (§6.1), calcolata per ciascuna funzione dalla tendina «Accesso alle immagini».
- `DITTA_PIANI_RICEVE_NOMINATIVI` — decide la lettera Responsabile della ditta esterna (21/10). È una regola di inclusione del pacchetto; da verificare nella versione 22 se è scritta in `pacchetto()` o lasciata alla scelta del passo 4.
- `RIGA_COMPILATA` — caso speciale delle lettere dei Responsabili (§5), valutato dopo i campi.

**Condizione sconosciuta: il blocco resta** e si scrive un avviso. È la scelta giusta — meglio un pezzo di troppo che un documento mutilato — e va mantenuta in C#.

---

## 4. Le utilità trasversali

Sono cinque, e sono il punto dove un porting superficiale si rompe.

### 4.1 Scrittura di un valore (`setText`)

1. Se il controllo è una tendina e il valore corrisponde a una voce — confronto **normalizzato**: via il trattino iniziale, apostrofi tipografici uniformati, spazi compattati, minuscole — si scrive la voce **nella forma del modello** (`- Amministrazione`, non `Amministrazione`).
2. Se non corrisponde e il controllo è una `dropDownList`, viene **convertito in `comboBox`**: è così che il testo scritto a mano può entrare in un campo che nel modello era a scelta chiusa.
3. Toglie `showingPlcHdr`, lo stile «segnaposto» e le evidenziazioni.
4. Se il controllo è di tipo data e il valore è `gg/mm/aaaa`, imposta anche `fullDate`.
5. Se il contenuto non ha nodi di testo ne crea uno.
6. Il testo su più righe diventa una sequenza di `w:br` e `w:t`, con `xml:space="preserve"`.

### 4.2 Rimozione che conserva l'interruzione di sezione (`togliParte`)

Togliendo un blocco `SE:` che contiene un `sectPr` si perderebbero intestazione e piè di pagina di quella sezione. Succede davvero: nel Registro la prima sezione con intestazione sta dentro `SE:BACKUP_SERVER`.

Il motore quindi, prima di rimuovere, inserisce un paragrafo vuoto che porta l'ultimo `sectPr` del blocco, dentro il quale copia i riferimenti a intestazione e piè di pagina delle sezioni precedenti che mancano. **Questa è la regola che un porting dimentica e che si scopre solo guardando il Registro stampato.**

### 4.3 Clonazione (`clona` / `rinumera`)

Ogni copia di un blocco deve avere identificatori nuovi, altrimenti Word segnala il file come danneggiato o duplica i segnalibri:

- via gli attributi `w14:paraId` e `w14:textId` (li rigenera Word);
- nuovo `w:id` casuale per ogni controllo contenuto;
- identificatori dei segnalibri spostati di `1000 × n` e nome con suffisso;
- `wp:docPr` con identificatore progressivo.

### 4.4 Sostituzione dentro un paragrafo (`sostituisci`)

Word spezza il testo in più `w:r` anche a metà parola. La funzione ricompone il testo del paragrafo, trova la posizione, e scrive la sostituzione nel primo pezzo coinvolto svuotando gli altri, mantenendo la formattazione del primo. Serve per le frasi fisse non marcate (il DVR, il «24h», le linee di firma).

### 4.5 Sistemazione dei vuoti (`sistemaVuoti`)

Quattro regole, tutte per difetti che si vedono solo in Word:

1. tabella senza righe → si toglie;
2. cella senza paragrafo → se ne aggiunge uno vuoto (una cella vuota senza `w:p` rende il file non valido);
3. cella che finisce con una tabella → paragrafo in coda;
4. due tabelle consecutive → paragrafo vuoto in mezzo, altrimenti Word le fonde.

---

## 5. I sette tipi di marcatore, in dettaglio

### `CAMPO:`
Scrive il valore. Due casi particolari: `REVISIONE_NUMERO` viene prefissato con `Rev. ` se il contenuto del modello inizia con «Rev»; nome e cognome di legale rappresentante, soci, coadiuvanti e incaricati diventano **maiuscolo, grassetto e centrati** se il campo è da solo nella cella di una tabella.

Esiste anche una scrittura **per posizione** (`fillPos`): quando la stessa etichetta compare più volte nello stesso blocco ripetuto, le occorrenze si numerano `ETICHETTA#1`, `ETICHETTA#2`. È così che si compilano le righe degli archivi e le lettere dei Responsabili.

### `SE:`
Condizione vera → il blocco si scioglie e il contenuto resta. Falsa → si rimuove con `togliParte`. Dentro le lettere dei Responsabili c'è il caso speciale `SE:RIGA_COMPILATA`, valutato dopo: la riga resta solo se almeno un campo `RESP_TRATTAMENTO` di quel blocco è stato compilato.

### `TESTO:`
Quattordici casi. Alcuni scelgono fra le voci della tendina del controllo stesso, cercandole per espressione regolare; altri costruiscono il testo.

| Etichetta | Regola |
|---|---|
| `CONSERVAZIONE_VIDEO` / `_PISCINA` | Con registrazione: durata dalla check list al posto del valore nel modello. Senza: «non sono conservati» |
| `REGISTRAZIONI_AUTORITA_*` | Frase sulle autorità solo se c'è registrazione |
| `CATEGORIE_IMMAGINI_VIDEO` | «, immagini di videosorveglianza.» oppure «.» |
| `DPIA_RESPONSABILITA` | Elenco: titolare più le funzioni che vedono |
| `FUNZIONI_VISIONE_ELENCO` | Elenco in prosa, con «e» prima dell'ultimo |
| `FUNZIONI_CONSULTAZIONE_ELENCO` | Distingue le funzioni aziendali dalle voci libere; «RLS» diventa «al Rappresentante dei lavoratori per la sicurezza (RLS)» |
| `DPIA_PERIODO_CONSERVAZIONE` | Durata con riferimento alla Circolare INL, oppure «solo visione in tempo reale» |
| `BACKUP_INTRO` | Voce «non effettua» o «Al fine di garantire» |
| `CONSENSO_*` | Nel contratto: `[X] SI`, `[X] NO`, o entrambi vuoti se sceglie il cliente |
| `PAGAMENTO_BONIFICO` / `_RIBA` | Croce sulla modalità scelta |
| `NESSUNA_VIDEOSORVEGLIANZA` | Voce «NON è dotata» se non ci sono telecamere |
| `VIDEO_ACCESSO` | Frase sull'accesso alle immagini, per funzione (§6.1) |

**Italiano e inglese.** Nelle informative il testo esiste in due lingue nello stesso file. Il motore individua il paragrafo che inizia con `INFORMATION ON THE PROCESSING` e considera inglese tutto ciò che viene dopo. In C# serve lo stesso confronto di posizione nel documento.

### `LISTA:`
Otto elenchi. Sette sono liste di valori; `EMAIL_CASELLE` è una funzione: restituisce le caselle accessibili alla funzione aziendale **della sezione in cui si trova il controllo**, individuata risalendo ai titoli del Registro nella forma «FRONT OFFICE Autorizzati al trattamento».

Tre forme di resa, scelte da cosa contiene il controllo:

1. **tabella** → una riga per valore, clonando la prima riga;
2. **run o controllo dentro un paragrafo condiviso** → valori uniti da virgola;
3. **blocco** → si duplica una volta per valore.

Nel terzo caso i controlli consecutivi vengono raggruppati: tre `LISTA:` di fila nello stesso punto producono **un solo elenco**, non tre. Se la lista-funzione non restituisce nulla, la riga di tabella che la contiene si toglie.

Se le voci della tendina iniziano tutte con `- `, il trattino viene aggiunto anche ai valori.

### `RIPETI:`
| Etichetta | Cosa duplica |
|---|---|
| `SEDI_TRATTAMENTO` | Le sedi. In tabella: una riga per sede, con la prima cella svuotata dalla seconda in poi. In linea: separate da `; `, e con più sedi ogni indirizzo è preceduto dal nome dell'attività |
| `FUNZIONI_VISIONE_DIRETTA` | Un blocco per funzione che vede in diretta |
| `AUTORIZZATI_<FUNZIONE>` | Le lettere di nomina (§6.1) |
| `AUTORIZZATI` | Le righe delle tabelle firme di Formazione e Disciplinare |
| `ARCHIVIO_CARTACEO` / `_INFORMATICO` | Una tabella per archivio, numerata. Gli archivi informatici vanno **ognuno su una pagina** |
| `LETTERE_RESPONSABILE` | Le lettere dei Responsabili e l'impegno della ditta |
| `MANUTENTORI_INTERNI` | Una copia per manutentore, ognuna su pagina nuova |

### `SEZIONE:`
`SEZIONE:AMBITO_<FUNZIONE>` delimita l'ambito di una funzione dentro una lettera di nomina. Serve per chi ha più funzioni: si prendono gli ambiti dalle altre lettere modello e si incollano in coda al primo.

### `FIRMA:` e `SPECIALE:`
Vedi §8 e §6.3.

---

## 6. I documenti con trattamento proprio

### 6.1 Nomine — il documento più complicato

Il modello contiene una lettera per funzione, dentro `RIPETI:AUTORIZZATI_<FUNZIONE>`. Il motore le estrae tutte, svuota il corpo, e ricostruisce una lettera per autorizzato:

1. **Ordinamento**: gli autorizzati si ordinano per la posizione della loro prima funzione nell'ordine fisso Amministrazione · Front Office · Capo chef · Guardiano notturno · Cameriera ai piani · Cameriere di sala · Capo sala.
2. **Quantità**: un lavoratore con quantità 3 produce 3 lettere identiche. Legale rappresentante, soci e coadiuvanti sempre 1.
3. **Più funzioni, una lettera sola**: si parte dalla lettera della prima funzione e si accodano gli ambiti delle altre.
4. Per ogni ambito si scrive la frase sull'accesso alle immagini e si valutano le condizioni con `VIDEO_ACCESSO_FUNZIONE` calcolata per quella funzione.
5. Nome e cognome solo per chi li richiede; le funzioni vanno nel titolo separate da virgola e nella tabella firme una per riga.
6. **Compattazione**: la lettera deve stare tassativamente in una pagina. Il motore riduce i corpi (16→15 o 14, 14→13) e, al livello massimo, l'interlinea a 228. Il livello si stima dal numero di funzioni: 2 funzioni → livello 2, 3 o più → livello 3; per il legale rappresentante un livello in più, per lasciare spazio alla firma. Con 4 o più funzioni scrive un avviso: «controllarla in Word».
7. Paragrafi vuoti in coda rimossi, altrimenti producono pagine bianche.

**Qui il porting in C# deve fare meglio del prototipo.** La compattazione stimata dal numero di funzioni è un ripiego del browser, che non sa impaginare. Sul server la via pulita è convertire in PDF con il convertitore già presente, contare le pagine, e aumentare il livello finché la lettera sta in una. È più lento ma è l'unico modo di rispettare una regola dichiarata tassativa.

**[DV 03/11] È già la regola decisa il 23/10** (`MARCATURA_Nomine.md` §A): impaginare con il convertitore, contare le pagine, compattare a livelli solo la lettera che sfora. La stima nel browser è un ripiego della prova, non una scelta. I livelli, in punti, sono gli stessi qui sopra in mezzi punti:

| Livello | Intervento |
|---|---|
| 0 | layout del modello (corpo 8 pt, titoli 7 pt) |
| 1 | corpo 7,5 pt, titoletti 6,5 pt |
| 2 | corpo 7 pt, titoletti 6,5 pt |
| 3 | come 2 + interlinea al 95% (il 228 su 240) |

Se il livello 3 non basta, la generazione si ferma con un avviso al consulente. Rischio dichiarato allora: Word e il convertitore impaginano in modo leggermente diverso; se in Word una lettera sfora e nel PDF no, si aggiunge un livello di sicurezza. Da ricontrollare sui primi casi reali.

### 6.2 Formazione e Disciplinare
Una riga di tabella per autorizzato, stessa lista e stesso ordine delle nomine.

### 6.3 Registro
- **Archivi**: una tabella per archivio cartaceo e una per informatico, numerate, con i valori scritti per posizione. Gli informatici separati da un'interruzione di pagina. Nessun archivio indicato → tabella tolta e avviso.
- **Tabella delle revisioni**: i dati e le firme vanno sulla riga della revisione corrente, cercata come `Rev. NN`. La riga `Rev. 00` conserva il numero ma perde data e firme, che vanno riportate dallo storico — e il motore lo dice con un avviso. Se la riga non esiste, compila la prima e avvisa.
- **Organigramma**: lo SmartArt del modello viene **sostituito** da un disegno costruito dal programma. Si individua cercando, entro i quattro elementi successivi al controllo, un `graphicData` il cui uri contiene `diagram`. Il disegno è un gruppo di riquadri e linee con coordinate assolute in EMU: titolare in alto, Amministrazione sotto, Front Office e, se presente, Capo chef affiancati, e sotto Front Office la colonna di Camerieri di sala o Capo sala, Cameriera ai piani, Guardiano notturno e «Responsabili del trattamento (Esterni)». In ogni riquadro le qualifiche presenti in quella funzione. Altezze calcolate dal numero di righe. I paragrafi vuoti sotto allo SmartArt si tolgono, altrimenti spingono una pagina in più.
- Si imposta `updateFields` perché Word aggiorni l'indice all'apertura.

### 6.4 Lettere dei Responsabili, manutentore, impegno ditta
Una lettera per occorrenza; dalla seconda in poi con interruzione di pagina per il manutentore, senza per le altre. I valori si scrivono per posizione, perché la stessa etichetta si ripete dentro la lettera.

---

## 7. Le rifiniture per documento

Sono le correzioni decise il 27 e 28 ottobre, applicate a testi **non marcati**: in C# diventano sostituzioni su frasi fisse, ed è la parte più fragile del motore, perché una virgola cambiata nel modello la fa fallire in silenzio.

**Registro** — toglie la riga fissa dell'RLS, che ora arriva dall'elenco; sostituisce `24h` con la durata della check list; con registrazioni su cloud riscrive la frase del DVR; toglie le tabelle CRS e CRO se il nome è vuoto, e con entrambi vuoti toglie anche l'introduzione; toglie i vuoti sopra «SITO WEB AZIENDALE» e gli mette un'interruzione di pagina davanti.

**DPIA** — con il DVR fisico sostituisce «nell'ufficio Front Office» con il luogo indicato; con il cloud riscrive **cinque frasi intere**, e segnala un avviso: «rileggerle prima della firma». È giusto che resti un avviso: sono testi prodotti dal programma su un documento che il cliente firma.

**Procedura data breach** — «Attenzione al calcolo dei tempi» sempre a inizio pagina.

**Disciplinare** — la tabella firme viene ristretta alla larghezza utile della pagina, calcolata dal `sectPr`, scalando proporzionalmente colonne e celle, e centrata.

> **Raccomandazione.** Ogni sostituzione su testo fisso va scritta in C# con un controllo esplicito: se la frase non si trova, un avviso nel registro di generazione. Un fallimento silenzioso qui produce un documento che sembra giusto e non lo è.

---

## 8. Le firme

Due immagini: consulente e legale rappresentante. L'inserimento aggiunge il PNG al pacchetto, crea la relazione, e costruisce un `w:drawing` in linea largo 1.440.000 EMU con altezza proporzionale; dove lo spazio è contato si può limitare l'altezza.

I controlli `FIRMA:` esistono solo nel Registro e nel Contratto. La firma del legale rappresentante va però anche in altri sei punti, trovati **per testo**:

| Documento | Dove |
|---|---|
| Lettere Responsabili e Impegno ditta | Colonna «IL TITOLARE DEL TRATTAMENTO» della tabella firme, sulla cella con la sola linea di sottolineatura |
| DPIA | Paragrafo «Il Titolare del trattamento (firma):» |
| Procedura data breach | Prima riga «Firma dell'autorizzato per presa visione» dopo la tabella del legale rappresentante |
| Formazione | «Firma del Titolare o del Legale Rappresentante», prima linea (la seconda è del docente e resta vuota) |
| Nomine | Nella lettera del legale rappresentante, «Firma dell'Autorizzato per presa visione», con altezza ridotta |
| Responsabile backup e credenziali | Solo se l'incaricato è il legale rappresentante |

Se la firma del consulente non c'è, il contenuto del controllo viene svuotato, non lasciato con il segnaposto.

---

## 9. Quali documenti entrano nel pacchetto

Undici sempre: Registro, Nomine, Formazione, Disciplinare, Informativa clienti, Informativa lavoratori, Modulo consensi, Informativa CV, Responsabile backup e credenziali, Procedura data breach, Istruzioni.

A condizione: DPIA se ci sono telecamere di qualunque tipo; cartello videosorveglianza se c'è la videosorveglianza; cartello piscina se c'è la telecamera piscina; disposizioni cameriere se le cameriere interne non sono nominate; disposizioni manutentore se ci sono manutentori interni, una copia ciascuno; impegno ditta piani se i servizi sono affidati a ditta esterna.

Le tredici lettere dei Responsabili secondo le scelte della check list, ciascuna ripetibile. **Eccezione:** se è richiesto il contratto di fornitura, la lettera «Consulente MARE.A» non si genera, perché la nomina è dentro il contratto.

Il contratto di fornitura se richiesto.

I file escono numerati `01_`, `02_`… con il nome del documento ripulito dagli accenti.

**[DV 03/11] Cosa si aggiunge al pacchetto:**
- **Riepilogo dei dati raccolti** — dalla versione 22 lo genera il programma (`riepilogo.js`), sulla carta intestata `CARTA_RIEPILOGO` (senza marchio ACCREDIA, margini 2 cm, «Pag. X di Y»). Nome: `Riepilogo_dati_raccolti_<insediamento>_<comune>.docx`.
- **Note della check list** — un Word a parte solo se c'è almeno una nota (29/10).
- **Ditta esterna con piano camere** — la lettera Responsabile per la ditta dei servizi ai piani esce con la ditta esterna che riceve i nominativi (21/10).
- Ogni lettera di nomina a Responsabile è un **file separato** («lettera 1 di 2»), e il pacchetto si chiama `Privacy_<insediamento>_<comune>.zip` (27/10).

---

## 10. Cosa il prototipo non fa e in Laboratorio AI va fatto

| Cosa | Come |
|---|---|
| PDF | `ConvertitoreWordPdf` già nella soluzione, con il font resolver aggiunto il 29/10. **[DV 03/11]** La versione offline non fa PDF (decisione del 03/11): la conversione nel dispositivo non era fedele. Il PDF resta compito del server |
| Invio e-mail e promemoria | Servizio Brevo esistente, stato nel database |
| Riepilogo dei dati per il cliente | **[DV 03/11]** Dalla versione 22 sta in `riepilogo.js`, scritto in OOXML diretto senza librerie esterne (non più `report.js` con docx-js). In C# si riscrive con OpenXml, su `CARTA_RIEPILOGO`. Regole: dati mancanti come «[da indicare …]» evidenziati in giallo; termine di 72 ore per le correzioni, in grassetto; consulente = nome dell'utente collegato; nessun riferimento al contratto |
| Comuni e CAP | Oggi 7.904 comuni in un file JavaScript: in C# una tabella di riferimento |
| Persistenza | Schema `man` |
| Caricamento sul cloud del cliente | Da concordare con Palomar |

---

## 11. Le trappole del porting (dieci, più quattro dal 03/11)

In ordine di quanto costa accorgersene tardi.

1. **Interruzioni di sezione dentro i blocchi condizionali** (§4.2). Sintomo: il Registro perde l'intestazione.
2. **Compattazione delle nomine** (§6.1). Oggi è una stima. Sul server va misurata.
3. **Identificatori nelle copie** (§4.3). Sintomo: Word dice che il file è danneggiato, oppure i segnalibri si accavallano.
4. **Commenti**: vanno tolti dal documento, dai file `comments*.xml`, da `[Content_Types].xml` e dalle relazioni. Toglierne solo una parte lascia il file non valido.
5. **`Default` per l'estensione png** in `[Content_Types].xml`: senza, il documento con la firma non si apre.
6. **Celle vuote senza paragrafo** e **tabelle consecutive** (§4.5).
7. **Doppia scrittura dei campi**: senza la rinomina in `CAMPO:_`, le copie si riempiono due volte.
8. **Confronto con le voci delle tendine** (§4.1): la normalizzazione del trattino e degli apostrofi non è un dettaglio estetico, decide se il valore corrisponde.
9. **Conversione da tendina a casella combinata**: senza, le voci scritte a mano non entrano.
10. **Sostituzioni su testo fisso** (§7): vanno segnalate quando falliscono.

**[DV 03/11] Quattro trappole in più, dalla versione 22:**

11. **Sezioni su pagina dispari nel Registro** (§13). Sintomo: copertine sul retro, pagine bianche al posto sbagliato. Va controllato contando le pagine del PDF.
12. **Formato e margini di sezione diversi** nel modello (Letter e A4 mescolati): un'interruzione «continua» fra sezioni con formati diversi diventa una pagina nuova. Il motore deve uniformarli alla prima sezione.
13. **Elenchi numerati delle tabelle «ELENCO DESTINATARI»**: senza una nuova istanza di numerazione per tabella, la numerazione prosegue da una tabella all'altra.
14. **Carattere di ripiego**: una cella unita o una tabella clonata senza la formattazione esplicita esce in Times (DPIA, tabella del sostituto nella data breach). Ogni testo scritto in una cella nuova prende la formattazione di una cella di riferimento.

---

## 12. Come si collauda

Il criterio è l'identità, non la somiglianza. Per lo stesso contenuto di check list, i documenti prodotti dal C# e quelli prodotti dal prototipo devono coincidere.

- Nel materiale di Vaccarini ci sono otto check list di prova (`esempi_checklist/`, dati inventati) che coprono i casi tematici: backup, videosorveglianza, responsabili, modulo consensi, istruzioni, data breach presente e assente.
- Il confronto si fa sul testo estratto e sulla struttura, non sui byte: i due pacchetti differiscono per identificatori casuali e ordine degli attributi.
- Le otto combinazioni ammesse di videosorveglianza e telecamera piscina vanno coperte tutte: è il punto dove le condizioni si intrecciano. **[DV 03/11]** Dal 03/11 le combinazioni ammesse sono **tutte e 12**: il collaudo le copre tutte. Per le quattro nuove, con i due impianti in modalità diverse, il prototipo non può fare da riferimento, perché le impedisce e non ha le frasi per impianto della DPIA: il riferimento sono i testi approvati di `MARCATURA_DPIA_Impianti_misti.md`. Il validatore delle combinazioni in `LaboratorioAI.Core` non scarta più nulla.
- Per i documenti con impaginazione critica — nomine, Registro — il confronto va fatto anche sul PDF, contando le pagine.
- **[DV 03/11]** Il riferimento del collaudo è la **versione 22**, non quella del 30/10. Un dato di partenza: con la nuova impaginazione il Registro d'esempio è passato da 157 a 138 pagine. Aggiungere al numero di pagine dell'Informativa lavoratori (deve restare una) i casi con più sedi e con videosorveglianza e piscina insieme: nel browser non si è potuto verificare.

---

## 13. [DV 03/11] Le regole della versione 22 (correzioni del 03/11/2026)

Correzioni fatte dopo l'uso della prova, applicate in `motore.js` e in parte nei modelli (script `fix_modelli_0311.py`). Modelli cambiati: REGISTRO, DPIA_VIDEO, CARTELLO_VIDEO, CARTELLO_PISCINA, PROC_DATA_BREACH; nuovo CARTA_RIEPILOGO.

**Prima di scrivere il codice:** il numero di righe, la posizione di queste regole nel ciclo del §2 e l'elenco esatto dei tag vanno riletti nella versione 22 di `motore.js`. Qui sotto la colonna «Dove nel ciclo» è la mia lettura, da verificare.

| Documento | Regola | Nel modello o nel motore | Dove nel ciclo (da verificare) | Traduzione in C# |
|---|---|---|---|---|
| DPIA | Tolti tutti gli sfondi gialli delle celle (`FFFF00`) e le evidenziazioni rimaste | entrambi | passo 14 | togliere `w:shd` con fill FFFF00 oltre a `w:highlight` |
| DPIA, Registro | «Sede legale» e «Sedi del trattamento»: via e città nella stessa cella, separate da « - »; le due colonne unite nel modello | modello + motore | passi 6 e 11 | il valore si compone prima di scriverlo; carattere della cella di riferimento (trappola 14) |
| Registro | **Impaginazione**: tolte le pagine bianche fisse; le copertine di macro sezione diventano sezioni che iniziano su **pagina dispari**, e il testo che segue riparte da pagina dispari, così il retro della copertina è sempre bianco | motore | passo 16 | `SectionType` = `OddPage` sulle interruzioni delle copertine |
| Registro | Le interruzioni di pagina rimaste diventano «nuova pagina prima» del paragrafo seguente; via le righe vuote prima di una nuova pagina | motore | passo 16 | `PageBreakBefore` sul paragrafo successivo al posto del `w:br type="page"` |
| Registro | Formato e margini di tutte le sezioni uguali alla prima; intestazione e piè di pagina scritti in ogni sezione; solo la prima con prima pagina diversa | motore | passo 16 | copiare `PageSize`, `PageMargin` e i riferimenti header/footer della prima `SectionProperties` |
| Registro | La tabella «mobile» del riquadro MARE.A: il paragrafo a cui è ancorata **non si tocca** | motore | passo 16 | escluderlo dalla pulizia dei vuoti |
| Registro | Ogni elenco numerato delle tabelle «ELENCO DESTINATARI» riparte da 1 | motore | passo 16 | nuova `NumberingInstance` per tabella con `LevelOverride` / `StartOverrideNumberingValue` su tutti i livelli |
| Registro | Titoli «Backup su SERVER / DISPOSITIVI ESTERNI / CLOUD» sempre aperti | motore | passo 16 | togliere `w15:collapsed` |
| Registro | Istanza INL e tabella numero/data solo con `VIDEO_O_PISCINA`; frase «La consultazione in tempo reale… è consentita al Legale Rappresentante ed agli Autorizzati…» solo con `VISIONE_DIRETTA`. Senza telecamere restano titolo e «La struttura NON è dotata…» | modello (condizioni nuove) | passo 9 | condizione nuova nel calcolo del §3 |
| Registro, DPIA, Nomine | Le funzioni che vedono e consultano sono quelle del passo 2 **corrette dalla scelta «Accesso alle immagini» del passo 3**. Stessa regola nei tre documenti | motore | prima del ciclo, nel calcolo degli elenchi | un solo metodo che produce i due elenchi, usato da tutti |
| Registro | Se l'elenco di chi vede resta vuoto: «…è consentita esclusivamente al Legale Rappresentante.» e avviso al consulente | motore | passo 10 | avviso nel registro di generazione |
| Registro | Archivi informatici, riga «BACK UP»: con un backup riporta i tipi scelti (es. «Server (Ufficio Amministrazione); Memoria esterna (HARD DISK); Cloud (GOOGLE DRIVE)») al posto di «Nessuno…»; «FREQUENZA BACK UP» = frequenza della check list. Resta la scelta del consulente se per quel PC ha indicato un tipo diverso | motore | passo 2 (archivi) | il valore si propone già nella check list (specifica §5) |
| Data breach | La tabella del sostituto del LR prende carattere e dimensione della tabella del LR, testo per testo; nel modello Montserrat 8 pt dove mancava | modello + motore | passo 3 | copiare `RunProperties` dalla cella corrispondente |
| Cartelli | Tabella del Titolare: colonna etichetta 3600 twip, dati più larga; ogni riga di dati che non sta su una riga si riduce di carattere, fino a 7 pt | modello + motore | passo 16 | **sul server va misurato**, come le nomine: il browser stima |
| Informativa lavoratori | Sempre in una pagina: righe vuote a 6 pt, nessuna interruzione di pagina | modello | — | controllo del numero di pagine sul PDF (§12) |
| Riepilogo | Generato dal programma su CARTA_RIEPILOGO (§9, §10) | motore | dopo il ciclo | generatore a parte in `LaboratorioAI.Documenti` |

---

*MARE.A srl — Cattolica (RN) — ottobre 2026, aggiornato novembre 2026*
