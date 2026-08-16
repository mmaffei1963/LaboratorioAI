# Manualistica — nota di indirizzo

## Dove vogliamo arrivare con i manuali

| | |
|---|---|
| **Data** | Agosto 2026 |
| **Committente** | MARE.A srl — Mirko Maffei |
| **Natura** | Nota strategica (non implementativa): l'indirizzo di lavoro per la produzione organica di manualistica. Da rivedere prima di scrivere codice. |

---

## 1. Obiettivo

Dare al consulente uno strumento con cui, dal cliente, con **tablet o telefono** (anche senza rete), compila delle **checklist** e da quei dati genera tutta la manualistica:

- DVR Legionellosi
- Autocontrollo piscine
- Autocontrollo alimentare
- DVR ex d.lgs. 81/2008
- Documento privacy

Ciascun documento con i suoi allegati, moduli e registri. **Un dato raccolto una volta, più documenti in uscita.**

---

## 2. Il problema da risolvere

- I documenti cambiano **spesso**, sia nell'impaginazione sia per adeguamento alla **normativa corrente**.
- Ogni documento ha **parti fisse** e **parti variabili**, dove inserire i dati raccolti.
- Alcune **sezioni entrano o escono** a seconda di cosa viene compilato nella checklist.

Oggi ogni manuale è un lavoro a sé: questo approccio non regge su cinque tipi di documento.

---

## 3. La strategia — una piattaforma a tre strati

Tre strati **separati e indipendenti**, così un cambiamento in uno non costringe a rimettere mano agli altri.

| Strato | Cosa contiene | Chi lo mantiene |
|---|---|---|
| **Dato** | Checklist guidata dallo schema, offline, che alimenta un **fascicolo unico per insediamento** condiviso tra tutti i documenti | Sistema + consulente |
| **Layout** | Modelli Word **versionati** (riferimento normativo + data di efficacia), aggiornabili senza toccare il codice | Qualità |
| **Mappatura** | **Dizionario** (dato → segnaposto), **motore condizionale** (sezioni che entrano/escono), **regole normative** complesse concentrate in un solo posto | Sviluppo + qualità |

Un **generatore unico** combina i tre strati e produce il **dossier** completo (manuale + registri + moduli), con traccia di cosa è stato prodotto e con quale versione di modello (riproducibile e auditabile).

Vincolo invariante confermato: il sistema legge ArcLab, **non ci scrive mai**.

---

## 4. Il vantaggio pratico

- Se cambia solo l'**impaginazione o il testo** → si sostituisce un modello, **zero codice**.
- Solo se serve un **dato nuovo** → si estende la checklist e il dizionario.
- Il **motore resta fermo** nella grande maggioranza dei cambiamenti.
- Ogni nuovo manuale **non è sviluppo da zero**: è un pacchetto di configurazione = *checklist + dizionario + modelli + regole*.
- Il **fascicolo unico** fa sì che il dato inserito per un documento (es. anagrafica e figure durante il sopralluogo legionella) sia già disponibile per gli altri (DVR 81, privacy).

---

## 5. Il percorso

1. **Estrarre la piattaforma comune** dai due domini che già funzionano (legionella e piscine): sono il banco di prova che l'astrazione regge.
2. Aggiungere l'**autocontrollo alimentare** come prima prova su un caso nuovo.
3. Infine **DVR 81** e **privacy**, come ulteriori pacchetti di configurazione.

Regola di prudenza: astrarre solo ciò che è **davvero duplicato** tra i domini noti. Evitare di costruire un motore super-generico prima di aver capito il terzo dominio.

---

## 6. Prossimi passi di dettaglio (aperti)

Da definire, come documenti prima che come codice:

- Lo **schema della checklist** (formato unico che genera i form e guida le condizioni di visibilità e le sezioni condizionali).
- Il **registro modelli** con versioni e selezione del modello corrente per tipo / regione / regime e data di efficacia.
- Il **dizionario delle variabili** condiviso, a partire da quelli già esistenti (legionella, piscine).

Requisiti già confermati: **offline** per la raccolta dati sul campo; **fascicolo unico per insediamento**.

---

*MARE.A srl — Cattolica (RN) — Agosto 2026*
