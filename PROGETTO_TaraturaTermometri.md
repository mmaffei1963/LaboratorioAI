# Taratura termometri — progetto (calcolo + modello dati)

## Modulo interno a Laboratorio AI

| | |
|---|---|
| **Data** | Agosto 2026 |
| **Committente** | MARE.A srl — Mirko Maffei |
| **Natura** | Documento di progetto (non implementativo). Mappa del calcolo e del modello dati, da rivedere prima di scrivere codice. |
| **Origine** | Foglio Excel `ModuloStatistico41TaraturaTermometri.xlsm` (Modulo Statistico 04-01 — Rapporto di Taratura). |

---

## 1. Scopo

Portare in Laboratorio AI il **Rapporto di Taratura dei termometri**: taratura per confronto di uno strumento misuratore di temperatura (UUT) contro un campione di riferimento (CDR) calibrato LAT, con budget d'incertezza GUM e rapporto su carta MARE.A. Rispetto al foglio attuale si aggiunge l'identificazione del **cliente dall'anagrafica ArcLab**.

Contesto: laboratorio accreditato ISO/IEC 17025 — lo strumento di calcolo va **validato e documentato**. Vincolo invariante invariato: nessuna scrittura su ArcLab.

---

## 2. Come funziona la taratura (metodo)

Si misurano più **punti di temperatura** (es. −20, 0, 20, 44 °C). Su ogni punto si eseguono **n letture ripetute** registrando, per ciascuna: la temperatura di riferimento nominale, la lettura dello strumento in prova (Tw).

Per ogni lettura:

- **Correzione del CDR** interpolata dal certificato: `corr(T) = m·T + q`, dove `m, q` sono pendenza e intercetta di una **regressione lineare** (LINEST) delle correzioni del certificato CDR rispetto alla temperatura (5 punti del certificato).
- **Temperatura reale di riferimento**: `tr = T_nominale + corr(T_nominale)`.
- **Errore dello strumento**: `Dw = Tw − tr`.

Per ogni **punto** (sui suoi n ripetuti) si calcolano `media(tr)`, `media(Tw)`, `media(Dw)`.

### 2.1 Budget d'incertezza (GUM, per punto)

Incertezza combinata come radice della somma dei quadrati di quattro contributi:

`u_c = √(u_rip² + u_lab² + u_ris² + u_stab²)`

| Contributo | Formula | Tipo | Note |
|---|---|---|---|
| `u_rip` — ripetibilità | `s(Tw)/√n` | A | scarto tipo delle letture Tw diviso √n |
| `u_lab` — riferimento | `u(CR)` interpolata: `m'·media(Tw) + q'` | B | regressione (LINEST) dell'incertezza tipo del CDR vs temperatura (≈ 0,025 °C) |
| `u_ris` — risoluzione UUT | `risoluzione / (2·√3)` | B | distribuzione rettangolare (es. 0,1 → 0,02887) |
| `u_stab` — stabilità rif. | `(tr_primo − tr_ultimo) / (2·√3)` | B | deriva del riferimento durante le letture, rettangolare |

**Incertezza estesa**: `U = k · u_c` con `k = 2` → livello di confidenza ~95%.
**Fattore di correzione** dichiarato: `−media(Dw)`.

### 2.2 Verifica numerica (dal foglio di validazione)

Sui casi `VAL_Caso1/2/3` il foglio riporta i valori attesi; ricostruendo le formule si ottengono gli stessi numeri (es. Caso 1, punto A: `u_c = 0,038188`, `U = 0,076376`, correzione `−0,017831`). Questi casi diventano i **test automatici** del motore.

---

## 3. Modello dati (schema proposto `taratura`)

### 3.1 `CampioniRiferimento` (CDR) — anagrafica riutilizzabile

Il CDR e il suo certificato oggi si ricopiano a ogni taratura: qui si inseriscono una volta.

| Campo | Note |
|---|---|
| `IdCampione` | PK |
| `Identificazione`, `TipoTermometro`, `CodiceInterno` | es. «Termometro Digitale VWR TD 121» |
| `CentroLat`, `NumeroCertificato`, `DataTaratura`, `DataScadenza` | tracciabilità LAT + scadenzario |
| `Risoluzione`, `FattoreCoperturaK` | dal certificato |
| `Attivo`, audit | |
| **Punti certificato** (tabella figlia `PuntiCertificatoCdr`) | per punto: `TIndicata`, `TRiferimento`, `Differenza`, `IncertezzaEstesa` — da cui si ricavano i fit di correzione e di u(CR) |

### 3.2 `Tarature` — record di taratura (UUT)

| Campo | Note |
|---|---|
| `IdTaratura` | PK |
| `IdCampioneRiferimento` | CDR usato (FK) |
| `StrumentoIdentificazione`, `StrumentoCodice` | UUT |
| `RisoluzioneStrumento` | per `u_ris` |
| `IdClienteArcLab` | **cliente dall'anagrafica ArcLab** (proprietario dello strumento) |
| `DataTaratura`, `DataScadenza` (= +365 gg), `NumeroRapporto` | numerazione automatica univoca |
| `RiferimentoIstruzione` | es. «IO 4-01» |
| `Note`, `Attivo`, audit | |

### 3.3 `PuntiTaratura` e `LettureTaratura`

| `PuntiTaratura` | `TemperaturaNominale`, ordine |
|---|---|
| `LettureTaratura` | per punto: `TemperaturaRiferimento` (nominale della lettura), `LetturaStrumento` (Tw) |

### 3.4 `RapportiTaratura` — snapshot (tracciabilità 17025)

Sullo stampo di `AnalisiAi`/rapporti nutrizionali: congela in JSON input + risultati per punto + versione del motore, con `NumeroRapporto`, `PercorsoDocumento`, utente. Riproducibile e auditabile.

---

## 4. Come si colloca in Laboratorio AI

- **Schema** `taratura`; migrazione EF Core dedicata.
- **Motore di calcolo** deterministico in `Core` (budget GUM), **validato con test** sui casi VAL (validazione dello strumento più solida del foglio auto-validante).
- **Generatore** del Rapporto di Taratura (PDF/Word) su carta MARE.A, riusando la macchina MigraDoc.
- **Registro tarature + scadenzario**: promemoria automatici (task schedulati) per tarature UUT e CDR in scadenza.
- **Cliente da ArcLab**: identico al committente delle ricette (ricerca su anagrafica clienti).
- **Permessi**: `taratura.esegui`, `taratura.registro`; area di navigazione dedicata.

---

## 5. Vantaggi e svantaggi (sintesi)

**Vantaggi.** Validazione con test automatici (forte per l'accreditamento); CDR e cliente riutilizzati senza ricopiare; numerazione rapporto e scadenzario automatici; snapshot tracciabile; fine dei file .xlsm con macro; report su carta intestata con lo stesso generatore.

**Svantaggi/rischi.** Il budget d'incertezza va riprodotto identico (fatto: mappa completa qui sopra) e ri-validato/documentato; il modello dati deve reggere la variabilità reale (numero di punti e di ripetizioni variabile); costo iniziale a fronte di un Excel funzionante.

---

## 6. Decisioni confermate (agosto 2026)

1. **Punti/ripetizioni**: fino a **4 punti × 10 letture** (limiti del foglio).
2. **Correzione CDR fuori range**: **estrapolazione lineare** (regressione LINEST estesa oltre i punti di certificato).
3. **Strumento in taratura**: sia **del cliente** (servizio) sia **interno** MARE.A → `IdClienteArcLab` opzionale.
4. **VBA**: verificato — **nessuna logica di calcolo**. Le macro fanno solo: interpolazione via celle (vecchia `CercaVicino`, superata dal LINEST), salvataggio in archivio, export PDF, stampa, azzeramento form. Tutta la matematica è nelle formule delle celle (ricostruite al §2).

---

## 7. Prossimi passi

1. Conferma di questo documento (metodo + modello dati + decisioni aperte).
2. Motore di calcolo GUM in `Core`, **validato** sui casi VAL_Caso1/2/3.
3. Persistenza (`taratura`) + anagrafica CDR + cliente ArcLab.
4. Generatore del Rapporto di Taratura (PDF/Word) + registro e scadenzario.

---

*MARE.A srl — Cattolica (RN) — Agosto 2026*
