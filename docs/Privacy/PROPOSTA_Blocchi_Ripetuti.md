# I blocchi ripetuti della check list

## Proposta — da approvare prima di scrivere le tabelle

| | |
|---|---|
| **Data** | 10 novembre 2026 |
| **Fonte** | I 32 modelli approvati, `motore.js` versione 23, e le tabelle già esistenti nello schema `man` |
| **Stato** | **Decisa da Mirko Maffei il 10/11/2026**: una tabella sola (§4), righe libere (§5), anagrafica dei gestionali del cliente (§6). |

---

## 1. Tre blocchi su cinque esistono già

Dal modello dati del 1 novembre:

| Blocco | Tabella | Stato |
|---|---|---|
| Sedi del trattamento | `man.SediCompilazione` | **c'è** |
| Autorizzati | `man.AutorizzatiCompilazione` + `man.AutorizzatiFunzione` | **c'è** |
| Responsabili | `man.ResponsabiliCompilazione` + `man.ScelteResponsabile` | **c'è** |
| Archivi cartacei e informatici | — | **manca** |
| Caselle e-mail | — | **manca** |

Quindi il lavoro è su due blocchi, non su cinque. E il più grosso è l'archivio informatico.

Una nota sulle lettere ai Responsabili, perché il modo in cui sono fatte è il precedente a cui questa proposta si confronta: `ScelteResponsabile` non ha una colonna per campo, ha righe `CodiceCampo` + `Valore`. È chiave-valore, ma dentro una tabella dedicata che sa a quale lettera appartiene. Funziona perché i campi di una lettera cambiano da modello a modello — la web agency ha «PROSPECT», l'Experience ha «Parchi tematici» — e una colonna per ciascuno vorrebbe dire una colonna per ogni variante.

Gli archivi sono il caso opposto: la struttura è la stessa per tutti i clienti. Per questo lì conviene un'altra forma.

---

## 2. Cos'è davvero un archivio, letto dal modello

### Archivio cartaceo — 14 campi

| Parte | Campi nel modello |
|---|---|
| Numero | lo mette il motore contando |
| Luogo | 1 |
| Chi vi accede | **3 celle** affiancate |
| Categorie di interessati | **3 celle** affiancate |
| Categorie di dati | **2 righe × 3 celle** = 6 celle, tutte con le stesse 3 voci (dati comuni, art. 9, art. 10) |

### Archivio informatico — 31 campi

Gli stessi 14, più:

| Parte | Campi |
|---|---|
| Il PC | sistema operativo, antivirus, backup, frequenza del backup, luogo di conservazione — 5 campi |
| I gestionali | **2 blocchetti**, ciascuno con prodotto, misure di sicurezza, manutenzione, backup, frequenza, luogo di conservazione — 6 campi × 2 = 12 |

Nel documento ogni archivio informatico occupa una pagina.

### Il punto che conta

**Il modello ha un numero fisso di caselle.** Tre persone che accedono, tre categorie di interessati, sei caselle di categorie di dati, due gestionali. Non è una scelta di progetto: è lo spazio che c'è nella tabella Word.

Un albergo con quattro gestionali su un PC, o con cinque funzioni che accedono all'archivio, oggi non si può descrivere. Il consulente ne scrive quanti ci stanno e il resto si perde.

---

## 3. Un'incoerenza nei nomi, da segnalare a Vaccarini

Il prodotto del **secondo** gestionale non si chiama `GESTIONALI_INSTALLATI_PRODOTTO_2` come ci si aspetterebbe: è marcato `ARCHIVIO_INFORMATICO_RIGA_2`, terza occorrenza. Il motore non se ne accorge perché dentro gli archivi riempie per posizione, e infatti la prova funziona.

Per noi però è un problema di leggibilità: nella pagina delle voci quel campo compare come «archivio informatico – riga» con undici nomi di software dentro, e chi lo guarda fra un anno non capisce cosa sia.

Non blocca niente e non va corretto di fretta. Vale la pena che Vaccarini lo rinomini alla prossima revisione del Registro, insieme al refuso «conservai».

---

## 4. Prima decisione: una tabella o due?

I due archivi condividono 14 campi su 14 e 31. La differenza è che l'informatico ne ha altri 17.

**Proposta: una tabella sola**, `man.ArchiviCompilazione`, con una colonna che dice se è cartaceo o informatico. I campi del solo informatico restano vuoti sui cartacei.

Il motivo è che un archivio è un archivio: luogo, chi vi accede, cosa contiene. Le domande che ci si farà sopra — «dove tiene i dati questo cliente», «quali archivi sono accessibili al Front Office» — non distinguono fra carta e computer. Con due tabelle ogni ricerca andrebbe scritta due volte e unita.

Il prezzo è qualche colonna vuota sui cartacei. Mi sembra il prezzo giusto.

---

## 5. Seconda decisione, la più importante: caselle fisse o righe?

Qui ci sono due strade e portano lontano.

**Strada A — ricalcare il documento.** Tre colonne `AccessibileA1`, `AccessibileA2`, `AccessibileA3`, e così via. Il database somiglia alla pagina Word, il generatore copia una casella alla volta.

**Strada B — descrivere la realtà.** Tabelle figlie con una riga per ogni persona che accede, per ogni categoria, per ogni gestionale. Quanti ne servono.

Propongo la **strada B**, per tre ragioni concrete.

La prima è che il limite di tre e di due non è una regola della privacy, è lo spazio in una tabella Word. Scrivendolo nel database lo rendiamo permanente: il giorno che Vaccarini allarga la tabella del Registro a quattro gestionali, serve una migrazione per seguirlo.

La seconda è che si perderebbero dati veri. Un albergo con quattro gestionali li ha, e il consulente li ha visti in sopralluogo. Meglio registrarli tutti e far avvisare il programma che il documento ne stampa due, piuttosto che non poterli scrivere.

La terza è che le ricerche diventano possibili. «Quali clienti hanno un archivio accessibile al Front Office» con tre colonne è una condizione in tre pezzi; con le righe è una riga sola.

Il prezzo da accettare: **il generatore dovrà avvisare** quando un archivio ha più elementi di quanti il modello ne stampi. Non è un di più, è parte della decisione: senza quell'avviso la strada B perde dati in silenzio, che è peggio della strada A.

---

## 6. Terza decisione: i gestionali — **anagrafica del cliente**

Avevo proposto di lasciarli dentro la scheda del PC. **Deciso il contrario**, perché nella pratica lo stesso gestionale sta su più postazioni e il consulente lo riscriverebbe ogni volta.

Guardando i modelli la decisione si giustifica da sé: dei sei dati di un gestionale — prodotto, misure di sicurezza, manutenzione, backup, frequenza, luogo di conservazione — **nessuno dipende dal PC**. Sono tutti proprietà del software. Il PC aggiunge una sola informazione: che quel gestionale è installato lì.

Quindi: una tabella dei gestionali del cliente, e una tabella di collegamento fra archivio e gestionale. Il consulente scrive PASSEPARTOUT una volta e lo spunta sui tre PC.

### Una duplicazione che esiste già nei modelli

I **17 fornitori** delle tendine `CRM_FORNITORE`, `CRS_FORNITORE`, `CRO_FORNITORE` e i **15** della tendina `MANUTENZIONE_GESTIONALI` sono la stessa cosa: le stesse aziende con lo stesso indirizzo. E sono già divergenti — la manutenzione ha LASERSOFT che le altre non hanno, le altre hanno GUEST.IT e TITANKA! che la manutenzione non ha.

Non la risolviamo adesso: Vaccarini il 03/11 ha deciso che i fornitori restano un elenco chiuso dentro le tendine, e quell'elenco il programma lo legge già dai modelli. Ma vale la pena che lui sappia che le due liste vanno riallineate, altrimenti il consulente vede fornitori diversi a seconda del campo che sta compilando.

### Un punto che resta aperto

Nel Registro i gestionali compaiono **in due posti**: dentro gli archivi (quelli installati su ogni PC) e in un blocco a parte, `CRM` / `CRS` / `CRO`, con nome e fornitore. Spesso sono gli stessi software.

Con l'anagrafica dei gestionali, la cosa pulita sarebbe che anche CRM, CRS e CRO si scelgano da lì invece di riscriverli. Non lo faccio adesso perché tocca come la check list pone la domanda, che è roba di Vaccarini. Lo segnalo per quando arriveremo alla schermata del passo 5.

## 7. Le tabelle

| Tabella | Una riga per | Contenuto |
|---|---|---|
| `man.ArchiviCompilazione` | archivio | tipo (cartaceo/informatico), luogo, ordine; e per i soli informatici sistema operativo, antivirus, backup, frequenza del backup, luogo di conservazione |
| `man.ArchiviAccessi` | chi vi accede | la voce scelta |
| `man.ArchiviInteressati` | categoria di interessati | la voce scelta |
| `man.ArchiviCategorieDati` | categoria di dati contenuta | dati comuni · art. 9 · art. 10 |
| `man.GestionaliCompilazione` | gestionale del cliente | prodotto, misure di sicurezza, manutenzione, backup, frequenza, luogo di conservazione |
| `man.ArchiviGestionali` | gestionale installato su un PC | il collegamento, e basta |
| `man.CaselleEmailCompilazione` | casella | indirizzo |
| `man.CaselleEmailFunzioni` | funzione che accede alla casella | funzione aziendale |

Otto tabelle. Tutte figlie della compilazione, nessuna cancellazione fisica, date UTC, chiavi `Id<Entità>`, come le altre.

Le voci delle tendine di questi campi sono già nel dizionario letto dai modelli: non si ridigitano, si leggono da `man.OpzioniVoce`.

## 8. Le decisioni prese (10/11/2026)

1. **Una tabella sola** per i due archivi, con il tipo come colonna.
2. **Righe libere invece di caselle fisse**, e il generatore avvisa quando il documento non ci sta. Senza quell'avviso questa scelta perderebbe dati in silenzio, che è peggio del limite.
3. **Anagrafica dei gestionali del cliente**, con il collegamento ai PC su cui sono installati.

Resta da segnalare a Vaccarini, senza fretta: le due liste dei fornitori da riallineare, il campo del secondo gestionale marcato `ARCHIVIO_INFORMATICO_RIGA_2` invece che `..._PRODOTTO_2` (§3), e il refuso «conservai».

---

*MARE.A srl — Cattolica (RN) — novembre 2026*
