# Cosa deve contenere una compilazione

## Specifica — dove va a finire ogni dato della check list

| | |
|---|---|
| **Data** | 10 novembre 2026 |
| **Fonte** | L'oggetto `cl` di `motore.js` versione 23: è l'elenco autorevole, perché è esattamente ciò che il motore legge per generare i documenti |
| **Stato** | Proposta. Serve una risposta sul §4. |

---

## 1. Perché questo documento

Finora abbiamo costruito dal basso: i modelli, il dizionario delle tendine, i passi, le tabelle dei blocchi ripetuti. Prima di disegnare le sei schermate serve il quadro: **tutto quello che il consulente deve raccogliere, e dove ciascun pezzo si ferma.**

L'elenco non me lo invento. Il motore legge un oggetto solo, `cl`, e quello che non c'è dentro non finisce in nessun documento. Sono 36 voci di primo livello, e sono queste.

---

## 2. Quello che ha già una casa

| Dato | Dove sta |
|---|---|
| `cliente`, `codiceUnivoco`, `referente`, `nuovoCliente` | colonne di `man.Compilazioni`, da ArcLab o da partita IVA |
| `dataDocumenti`, `revisione` | colonne di `man.Compilazioni` |
| `consulente` | l'utente che compila |
| `sedi` | `man.SediCompilazione` |
| `autorizzati`, `incaricoBackup`, `incaricoCredenziali`, `sostituto` | `man.AutorizzatiCompilazione` |
| `responsabili` | `man.ResponsabiliCompilazione` + `man.ScelteResponsabile` |
| `archivi` | `man.ArchiviCompilazione` e figlie **(dal 10/11)** |
| `caselle` | `man.CaselleEmailCompilazione` **(dal 10/11)** |
| `contratto` | `man.ContrattiFornitura` + `man.IntestatariContratto` |

Niente da fare: esistono.

---

## 3. Quello che è una risposta e basta

Va in `man.ValoriCompilazione`, una riga per risposta, con il codice della voce. Le voci con una tendina sono le **85 già lette dai modelli**; le altre sono il §4.

| Dato | Voci |
|---|---|
| `video` | luogo del DVR, luogo dello schermo, numero e data dell'istanza INL, archiviazione su DVR o cloud |
| `videoAccesso`, `durataOre` | chi vede le immagini, per quante ore si conservano |
| `crm`, `wifi`, `bustePaga`, `misure` | nomi, fornitori, modalità, presidi |
| `backup` | tipi, luoghi, dispositivo, custodia, fornitore, frequenza, ore di ripristino |
| `informazioniFornite`, `formazioneOre` | chi ha fornito le informazioni, ore di formazione |

---

## 4. Quello che manca, ed è il punto

Ventitré domande **non esistono da nessuna parte**. Non sono nei modelli come campi — non scrivono niente — ma il motore le legge per decidere quali documenti escono e quali parti restano. Sono le più importanti della check list: da loro dipende tutto il resto.

| Passo | Domanda | Tipo |
|---|---|---|
| 1 | contratto di fornitura richiesto · assistenza annuale | 2 sì/no |
| 2 | **videosorveglianza** (nessuna · visione e registrazione · sola visione · sola registrazione) | tendina |
| 2 | **telecamera piscina** (nessuna · visione e registrazione · sola visione) | tendina |
| 2 | archiviazione delle immagini (DVR fisico · cloud del fornitore) | tendina |
| 2 | ore di conservazione delle immagini | numero, 24 |
| 2 | sala (camerieri · capo sala · nessuno) | tendina |
| 2 | piani (interne nominate · interne non nominate · ditta esterna) | tendina |
| 2 | guardiano (con segreteria · senza · nessuno) | tendina |
| 2 | capo chef | sì/no |
| 2 | cinque finalità facoltative | 5 sì/no |
| 2 | manutentori interni · lettere alla ditta dei piani | 2 numeri |
| 3 | ore di formazione | numero, 2 |
| 5 | CRM presente · wifi presente | 2 sì/no |
| 5 | tipi di backup (server · dispositivi esterni · cloud) | scelta multipla |
| 5 | ore di ripristino | numero, 48 |

Sono derivate dal motore, non inventate: i valori sono quelli che `contesto()` confronta. Propongo di scriverle come dato iniziale, allo stesso modo dei passi, e di non farle rivedere a mano: cambiarle vorrebbe dire rompere la generazione.

**Un nome da concordare con Vaccarini.** Il motore riconosce `NESSUNA`, `VISIONE_REG` e `SOLO_REG`; la quarta modalità della videosorveglianza — sola visione — non la nomina mai, la riconosce per esclusione. Io la chiamo `SOLO_VISIONE`. Se nella sua schermata si chiama diversamente non cambia niente per la generazione, ma è bene che i due programmi dicano la stessa parola.

---

## 5. Il secondo buco: i campi di testo

Dei 148 campi dei modelli, 77 hanno una tendina e sono diventati voci. Gli altri **71 sono testo libero e non hanno una voce**.

Non sono però 71 domande. Quasi tutti prendono il valore dalle strutture del §2 — il nome di un autorizzato, l'indirizzo di una sede, il prezzo nel contratto. Le domande vere sono una ventina: il nome del CRM, il numero dell'istanza INL, l'indirizzo della casella delle buste paga.

Distinguere le une dalle altre è un lavoro di giudizio, come l'assegnazione dei passi. Propongo lo stesso metodo che ha funzionato: il programma crea una voce per ciascuno dei 71, e nella pagina delle voci si spengono quelle che vengono da altrove. Si fa una volta e viaggia con il codice.

---

## 6. Le tre tappe

1. **Le 23 domande che governano** (§4), scritte come dato iniziale.
2. **I 71 campi di testo** (§5), creati e poi scremati a mano una volta.
3. **Le sei schermate**, che a quel punto si costruiscono su un elenco completo.

Senza la prima non si può generare niente. Senza la seconda si genera con dei buchi.

---

## 7. Cosa chiedo

Il §4: le 23 domande come le ho scritte, con i valori presi dal motore.

---

*MARE.A srl — Cattolica (RN) — novembre 2026*
