# Manualistica MARE.A — documento di contesto

## Da leggere prima di lavorare in questo progetto

| | |
|---|---|
| **Data** | 29 ottobre 2026 |
| **A cura di** | Mirko Maffei, Responsabile di Laboratorio |
| **Destinatari** | Chi lavora alla manualistica: Mirko, Daniele Vaccarini, chi verrà dopo |
| **Scopo** | Dare in una pagina il quadro: cosa stiamo costruendo, con quali regole, a che punto siamo |

---

## 1. Di cosa si occupa questo progetto

Della **manualistica che MARE.A produce per i clienti**: i documenti che il consulente compila in sopralluogo e consegna. Oggi sono cinque famiglie:

| Documento | Stato |
|---|---|
| DVR Legionellosi | **In esercizio** in Laboratorio AI, collaudato su dati reali |
| Autocontrollo piscine (DVR, regolamento, manuale ER, registro Marche) | **In esercizio** in Laboratorio AI |
| Documento privacy | **Prototipo** di Daniele Vaccarini, specifica di integrazione pronta |
| Autocontrollo alimentare (HACCP) | Da fare |
| DVR ex d.lgs. 81/2008 | Da fare |

Questo progetto **non** è il posto dove si parla di provvigioni, preventivi, Direzione o certificati: quelle cose stanno nel progetto Laboratorio AI.

---

## 2. L'obiettivo, in una frase

Il consulente, dal cliente, compila **una check list** su tablet o telefono — anche senza rete — e da quei dati esce **tutta la documentazione**: manuale, allegati, moduli e registri. **Un dato raccolto una volta, più documenti in uscita.**

Il problema che risolve: oggi ogni manuale è un lavoro a sé, i documenti cambiano spesso (impaginazione e normativa) e alcune sezioni entrano o escono a seconda di cosa si è compilato. Su cinque famiglie di documenti questo approccio non regge.

---

## 3. Come è impostato: tre strati separati

| Strato | Cosa contiene | Chi lo mantiene |
|---|---|---|
| **Dato** | La check list e il fascicolo del cliente | Sistema + consulente |
| **Layout** | I **modelli Word versionati**, aggiornabili senza toccare il codice | Qualità e consulenti |
| **Mappatura** | Il dizionario dato → segnaposto, le condizioni, le regole | Sviluppo + qualità |

Il vantaggio: se cambia **solo testo o impaginazione**, si sostituisce un modello e **non si tocca il codice**. Solo se serve **un dato nuovo** si estende la check list.

Il percorso previsto dalla nota di indirizzo di agosto: prima estrarre la piattaforma comune da legionella e piscine, poi l'alimentare, infine DVR 81 e privacy. La privacy è arrivata prima perché Vaccarini ne aveva già fatto il prototipo: è in discussione (vedi la specifica) se procedere subito o rispettare l'ordine.

---

## 4. Come si marcano i modelli Word — la convenzione da seguire

**Questa è la parte operativa più importante per chi prepara i modelli.**

I modelli si marcano con i **controlli contenuto di Word** (scheda Sviluppo → Controllo contenuto), assegnando a ciascuno un **tag**. Il tag ha un prefisso che dice al programma cosa farne:

| Prefisso | A cosa serve | Esempio |
|---|---|---|
| `CAMPO:` | Un valore da sostituire | `CAMPO:CODICE_ATECO` |
| `SE:` | Un blocco che resta o sparisce secondo una risposta | `SE:VIDEO_PRESENTE` |
| `TESTO:` | Un testo scelto fra più varianti | `TESTO:ACCESSO_VIDEO` |
| `RIPETI:` | Un blocco ripetuto una volta per elemento | `RIPETI:SEDI_TRATTAMENTO` |
| `LISTA:` | Un elenco puntato da una selezione multipla | `LISTA:MISURE_ANTINCENDIO` |
| `SEZIONE:` | Una sezione di ambito per funzione aziendale | `SEZIONE:AMBITO_CAPO_CHEF` |
| `FIRMA:` | Dove va inserita un'immagine di firma | `FIRMA:CONSULENTE` |

Convenzioni: **maiuscole**, parole separate da `_`, niente accenti né spazi. Per i blocchi ripetuti numerare con `_1`, `_2`, `_3` (es. `CAMPO:ARCHIVIO_CARTACEO_RIGA_1`).

**Se i prossimi manuali vengono marcati con questa stessa convenzione, l'integrazione in Laboratorio AI è molto più breve.** I 31 modelli privacy la usano già: sono il riferimento da guardare. L'elenco completo dei 230 tag esistenti è nel file `tutti_i_tag.txt` allegato a questo progetto — riusare un tag già esistente, quando il dato è lo stesso, evita di duplicare il dizionario.

Due cose da non dimenticare nei modelli:
- **numero di revisione e data di efficacia**, perché il sistema deve poter dire con quale versione del modello è stato generato un documento consegnato un anno prima;
- i **riferimenti normativi** sono responsabilità di chi mantiene i modelli, non dello sviluppo.

---

## 5. Regole che valgono sempre

- **Laboratorio AI non scrive mai su ArcLab** (database `LabAnalisiDati`) né su AZW: sola lettura, nessuna eccezione. I dati anagrafici del cliente si leggono, non si modificano.
- **Prima si definisce bene, poi si realizza.** Per una funzione nuova si propone, si fanno le domande, Mirko approva, poi si sviluppa.
- **Italiano semplice.** Mirko non è uno sviluppatore: spiegazioni brevi e comandi pronti da copiare.
- **Niente dati personali sparsi.** Firme autografe, numeri di cellulare e dati dei clienti stanno nel sistema, protetti, non dentro prototipi o file condivisi.
- I documenti prodotti sono **bozze**: la revisione e la firma spettano alla persona responsabile.

---

## 6. Dove si trova il materiale

| Elemento | Dove |
|---|---|
| Specifica di integrazione della privacy | In questo progetto, `PROGETTO_Manualistica_Privacy.md` |
| Nota di indirizzo sulla manualistica | In questo progetto, `VISIONE_Manualistica.md` |
| Elenco dei 230 tag esistenti | In questo progetto, `tutti_i_tag.txt` |
| Tag suddivisi per modello | In questo progetto, `tag_per_modello.json` |
| I 31 modelli Word estratti dal prototipo | `C:\Sviluppo\LaboratorioAI\docs\Privacy\Modelli\` (PC di Mirko) |
| Prototipo privacy funzionante | Artifact «Privacy Slug – prova» di Vaccarini |
| Codice di Laboratorio AI | `C:\Sviluppo\LaboratorioAI` (PC di Mirko) |

---

## 7. A che punto siamo e cosa serve

**Privacy:** la specifica di integrazione è scritta e aspetta cinque decisioni da Mirko e cinque risposte da Vaccarini (sono elencate in fondo alla specifica). Tra queste, le più urgenti: se procedere adesso o dopo la piattaforma comune, se la prima versione copre solo le strutture ricettive, e se esistono ancora i prototipi Python da cui è nato il motore.

**Altri manuali:** man mano che Vaccarini li prepara, vanno marcati con la convenzione del §4. Per ciascuno servirà poi la stessa analisi fatta per la privacy: quali documenti escono sempre e quali a condizione, quali dati servono, quali sono già in ArcLab.

---

*MARE.A srl — Cattolica (RN) — ottobre 2026*
