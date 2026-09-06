# Progetto "Rinomina e triage certificati esterni" — Nota di progetto

**Stato:** bozza per revisione · **Data:** 2026-09-05 · **Autore:** assistente AI su indicazioni di M. Maffei
**Ambito:** modulo LabAI per i Rapporti di Prova (RdP) dei laboratori esterni

> Documento di *definizione*, non di realizzazione ("prima definiamo, poi realizzi").
> Invariante di progetto: **il sistema non scrive mai su ArcLab** (`LabAnalisiDati`, sola lettura).

---

## 1. Scopo

Automatizzare la gestione dei certificati PDF ricevuti dai laboratori esterni:

1. **leggere** il PDF ed estrarre il **nostro codice di accettazione** (lotto/campione) e il **numero RdP** del laboratorio;
2. **segnalare le criticità** (risultati fuori specifica, non conformità, valori anomali);
3. **rinominare** il file con una convenzione fissa che permetta il **caricamento automatico sul portale cliente da parte di Palomar**;
4. prima di rinominare, **chiedere all'operatore** se il certificato va inviato con **lettera di avviso** al cliente e codificarlo nel nome.

Il nome ricostruisce lo standard dei rapporti interni (dati presi da ArcLab tramite lotto/campione), così Palomar tratta interni ed esterni con lo stesso parser.

---

## 2. Convenzione del nome file

```
GG-MM-AA-<lotto>-<campione>-<idInsediamento>-<accettazione>-<cliente>-<prodotto>-<matrice>-EST-<LAB>-<RdP_esterno>-<AVV|STD>
```

- **Blocco chiave** `GG-MM-AA-<lotto>-<campione>-<idInsediamento>-<accettazione>-<cliente>-<prodotto>-<matrice>`: identico ai certificati interni; è la parte su cui **Palomar aggancia** cliente e dati. I campi provengono da **ArcLab** (vedi §5).
- **`<idInsediamento>`**: id insediamento dal DB, così **Palomar carica direttamente nel giusto insediamento**. Posizione fissa (subito dopo lotto/campione).
- **`EST`**: marca "certificato esterno". Posizionato **prima del codice esterno**, così il blocco iniziale resta identico agli interni.
- **`<LAB>`**: acronimo del laboratorio esterno, per capire subito di chi è (vedi tabella acronimi). Es. `SIC` (Sicural), `EP` (Epta Nord), `CRM` (Centro Ricerche Marine), `CSA`.
- **`<RdP_esterno>`**: numero di Rapporto di Prova del laboratorio (sua identità documentale). Es. `26LA17574`, `26LB0112156`.
- **Flag avviso** (terminale, **sempre presente**): `-AVV` se il certificato richiede **lettera di avviso** al cliente, `-STD` altrimenti. Stato doppio → ogni file ha un flag esplicito (più robusto per il parser di Palomar).
- **Caratteri vietati** nei nomi file (`/ \ : * ? " < > |`) → sostituiti con `-`. Es. "7113/02" → "7113-02".

**Acronimi laboratorio** (configurabili): Sicural = `SIC`, Epta Nord = `EP`, Centro Ricerche Marine = `CRM`, C.S.A. = `CSA`, IZS Venezie = `IZSVE`, IZS Umbria = `IZSUM`, ARPAM = `ARPAM`, BioAgriFood = `BAF`, Agribiosearch = `ABS`.

**Esempi (dai PDF reali):**

| Laboratorio | Nostro codice | RdP esterno | Criticità | Nome proposto (coda EST) |
|---|---|---|---|---|
| Sicural | 7183/02 | 26LA17574 | nessuna | `…-7183-02-<idIns>-…-EST-SIC-26LA17574-STD` |
| Epta Nord | 7113/02 | 26LB0112156 | **non conforme** (alcol etilico su s.s. oltre limite DM 312/1998) | `…-7113-02-<idIns>-…-EST-EP-26LB0112156-AVV` |

---

## 3. Rilevamento del laboratorio

Il laboratorio si riconosce da marcatori nell'intestazione/piè di pagina del PDF
(ragione sociale, P.IVA, n° LAB). Per ciascun laboratorio noto si applica la
regola di estrazione dedicata (§4). Laboratori riconosciuti nei campioni forniti:
Sicural, Epta Nord, C.S.A., Centro Ricerche Marine, IZS Venezie (CSI Ittiopatologia),
IZS Umbria (Togo Rosati), ARPAM, BioAgriFood, Agribiosearch, Lab 0447, Lab 0157.

---

## 4. Estrazione del nostro lotto/campione (mappa per laboratorio)

Formato del nostro codice: `NNNN/NN` (lotto / campione).

| Laboratorio | Posizione del nostro codice |
|---|---|
| Sicural, Centro Ricerche Marine, IZS Venezie, ARPA-lab (5548), Lab 24SA, BioAgriFood, Agribiosearch | **Descrizione campione**: "CAMPIONE `7183/02` …" |
| Epta Nord | "**Vs. Rif.** `7113/02`" |
| C.S.A. | "**cod.** `294/03`" / "vs. rif. protocollo" |
| Lab 0447 | `9551/01` (in descrizione/riferimenti) |

**Casi senza il nostro codice** (rari): ARPAM, IZS Umbria, Lab 0157 attualmente non
lo riportano. Il modulo **non blocca**: segnala "codice non trovato" e chiede
l'inserimento manuale. Azione di processo parallela: chiedere ai laboratori di
riportare sempre il nostro n° campione in descrizione.

**Dosimetri/Radon (Laboratorio di Fisica):** trattati come i casi senza codice —
il modulo li segnala e chiede inserimento manuale (non esclusi a priori).

---

## 5. Aggancio ArcLab (sola lettura)

Dal `lotto/campione` estratto si recuperano da ArcLab i campi per comporre il nome,
riusando le query già esistenti (sezione Lab esterno / Consultazione):
data accettazione, codice accettazione/commessa, cliente, descrizione prodotto, matrice.
Se il codice non trova riscontro in ArcLab → criticità "campione non trovato in archivio".

---

## 6. Rilevamento criticità

Definizione (da RSQ): **qualunque cosa fuori specifica**, non solo il giudizio di conformità.

**Deterministico** (da testo del PDF):
- dichiarazione esplicita "NON CONFORME" / "superiore al limite di legge";
- parametri contrassegnati come fuori limite (simboli/asterischi di superamento);
- parametro non accreditato dove atteso; certificato non firmato;
- coerenza matrice/parametri con l'ordine.

**Assistito da AI** (già integrata):
- valori **anomali per il tipo di prodotto** anche se non regolamentati da norme
  specifiche (confronto con intervalli tipici/attese di prodotto).
- L'AI è **ausilio**: segnala, non decide; il giudizio resta all'operatore/RSQ.

L'esito criticità è mostrato all'operatore prima della rinomina.

---

## 7. Flusso operativo

1. L'operatore carica uno o più PDF (o si punta a una cartella di arrivo).
2. Il modulo: riconosce il laboratorio → estrae lotto/campione + RdP → aggancia ArcLab
   → rileva criticità.
3. Mostra il riepilogo (dati estratti, criticità) e il **nome proposto**.
4. Chiede: **"inviare con avviso al cliente?"** (sì/no) → aggiunge o meno `-AVV`.
5. Rinomina/copia il file nella **cartella di prelievo di Palomar** (percorso configurabile).
6. **Smistamento/archiviazione**: una routine sposta i file rinominati in sottocartelle
   dedicate `<base>\<Laboratorio>\<data>\` (cartella per laboratorio, poi per data),
   creandole se mancano. Percorso base configurabile.
7. Registra l'operazione nell'audit trail (area dedicata), con esito e criticità.

Oggi i certificati arrivano via **e-mail** e vengono poi posizionati a mano nelle cartelle
dei vari laboratori: la routine §7.6 automatizza proprio questo passaggio.

---

## 8. Architettura (schema consueto LabAI)

- **Core**: interfaccia `IServizioCertificatiEsterni` + modelli
  (`CertificatoEsterno`: laboratorio, lotto, campione, RdPEsterno, criticità[],
  avviso, nomeProposto, campiArcLab).
- **Dati**: repository ArcLab (Dapper, sola lettura) per lotto/campione → dati campione;
  parser PDF per-laboratorio (estrazione testo + regole §4). Verificare la libreria di
  estrazione testo PDF già in uso (import scheda tecnica) e riusarla.
- **Documenti/AI**: servizio criticità (deterministico) + chiamata AI per anomalie.
- **Web**: controller + vista di caricamento/anteprima/conferma avviso + rinomina;
  permesso dedicato; voce di menu in area Laboratorio.
- **Config**: `CertificatiEsterni:CartellaPalomar` (prelievo),
  `CertificatiEsterni:CartellaArchivio` (base per smistamento `<Lab>\<data>\`),
  mappa **acronimi laboratorio** (SIC, EP, CRM, CSA, …), eventuale cartella di arrivo e-mail.

---

## 9. Punti aperti / decisioni

1. **Posizione di `EST`**: prima del codice esterno (consigliato) o in testa `EST-…`?
2. **Flag avviso**: DECISO → **doppio** `-AVV`/`-STD` (flag sempre presente).
3. **Percorsi**: nessuno fissato ora → tutti **configurabili**, li scegliamo dopo.
   Servono: cartella di prelievo Palomar e cartella base per lo smistamento
   `<base>\<Laboratorio>\<data>\` (§7.6). Oggi arrivo via e-mail.
4. Campi ArcLab: **si riusano le query esistenti** (Lab esterno / Consultazione) per
   `<idInsediamento>` / `<accettazione>` / `<cliente>` / `<prodotto>` / `<matrice>`.
5. **Lotti senza campione** e **certificati multi-campione** (un PDF con più campioni):
   rinomina **manuale assistita** — il modulo **pre-compila le parti fisse** (data,
   idInsediamento, ragione sociale, ecc.) e propone la coda già pronta
   (es. `…-EST-EP-26LB0112156-AVV`), l'operatore completa solo la parte variabile.

---

*Fine nota — in attesa di revisione prima della fase realizzativa.*
