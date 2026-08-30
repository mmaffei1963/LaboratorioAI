# Progetto "Conformità Normativa" — Documento di progettazione

**Stato:** bozza per revisione · **Data:** 2026-08-30 · **Autore:** assistente AI su indicazioni di M. Maffei
**Ambito:** LaboratorioAI (applicativo interno MARE.A srl, ACCREDIA 00729, ISO/IEC 17025:2018)

> Documento di *definizione*, non di realizzazione. Coerente con la regola di progetto
> "prima definiamo bene, poi realizzi". Nessun codice viene scritto finché questo schema
> non è approvato. Rispetta l'invariante: **il sistema non scrive mai su ArcLab**
> (`LabAnalisiDati`, sola lettura).

---

## 1. Scopo e principio guida

Due esigenze distinte del sistema qualità che poggiano sulla **stessa ossatura**:

1. **Sorveglianza documentale** — verificare che i documenti del SGQ citino riferimenti
   normativi *vigenti* (norme nazionali, regolamenti UE, documenti ACCREDIA) e non
   contengano norme superate.
2. **Catalogo limiti** — disporre di limiti chimici e microbiologici (alimenti, acque,
   ambientali) sempre aggiornati e verificabili, per attribuire giudizi di conformità.

**Principio invariante del progetto:**

> L'AI *sorveglia*, non è *fonte di verità*.
> Ogni riferimento e ogni limite che produce un effetto (giudizio di conformità,
> segnalazione di norma superata) proviene da una **riga di tabella curata, versionata
> e con la fonte citata**. L'AI assiste in tre punti — estrazione dalle fonti ufficiali,
> monitoraggio delle variazioni, interrogazione in linguaggio naturale — ma non sostituisce
> mai il dato validato. Nessuna sovrascrittura automatica: ogni modifica passa da revisione
> umana e genera una nuova versione, mantenendo lo storico.

Entrambi i sottosistemi condividono:

- una tabella **Fonti** (le norme/documenti citabili, con edizione e stato di vigenza);
- il **versioning** (nessuna cancellazione fisica; nuova versione a ogni variazione);
- il **motore di sorveglianza** (task programmato + workflow di validazione);
- le convenzioni dati del progetto.

---

## 2. Convenzioni dati (invarianti di progetto)

- Nomi tabella in **italiano al plurale**.
- Chiave primaria `Id<Entità>` **INT IDENTITY**.
- **Date in UTC** sul database.
- **Nessuna cancellazione fisica**: campi `DataCancellazione`/`Attivo` o riga di versione.
- Schema **versionato con migrazioni EF Core** (mai modifiche manuali al db).
- Persistenza sul db applicativo `LaboratorioAI_Test` (sviluppo) / produzione.
- ArcLab in **sola lettura** via Dapper; l'aggancio è solo `IdParametro` (int) letto.

---

## 3. Modello dati

### 3.1 Ossatura condivisa

#### `RiferimentiNormativi` (o `Fonti`) — registro maestro delle norme
La tabella pivot condivisa: ogni norma/documento che il laboratorio cita o applica.

| Campo | Tipo | Note |
|---|---|---|
| `IdRiferimento` | INT IDENTITY PK | |
| `Tipo` | int (enum) | UNI, EN, ISO, IEC, CEI, RegolamentoUE, DecretoLgs, DM, DPR, Legge, ISTISAN, ACCREDIA, LineaGuida, Altro |
| `Sigla` | nvarchar | es. "UNI EN ISO/IEC 17025", "Reg. (UE) 2023/915", "D.Lgs 18/2023" |
| `Edizione` | nvarchar? | anno/edizione es. "2018", "2023" |
| `Titolo` | nvarchar? | descrizione estesa |
| `Stato` | int (enum) | Vigente, Superata, Ritirata, DaVerificare |
| `SostituitaDa` | INT? (FK self) | punta all'edizione/norma che la sostituisce |
| `DataEntrataVigore` | datetime2? | UTC |
| `DataFineValidita` | datetime2? | UTC |
| `Url` | nvarchar? | EUR-Lex / Normattiva / ACCREDIA |
| `UrlSnapshot` | nvarchar? | copia archiviata (opzionale) |
| `Note` | nvarchar? | |
| `DataUltimaVerifica` | datetime2? | ultimo controllo di vigenza |
| campi audit | | `IdUtente`, `DataCreazione`, `DataUltimaModifica`, `Attivo` |

> Questa tabella serve **sia** all'audit documentale (contro cosa confronto le citazioni)
> **sia** al catalogo limiti (la fonte di ogni limite è un `IdRiferimento`).

#### `EventiSorveglianza` — traccia dei controlli di vigenza (condiviso)
Ogni run di verifica (manuale o programmata) su un riferimento o su un limite.

| Campo | Tipo | Note |
|---|---|---|
| `IdEvento` | INT IDENTITY PK | |
| `TipoOggetto` | int (enum) | Riferimento, Limite, Documento |
| `IdOggetto` | int | id dell'entità controllata |
| `Esito` | int (enum) | Confermato, Discordanza, DaRivedere |
| `Dettaglio` | nvarchar | cosa ha trovato la sorveglianza |
| `Fonte` | nvarchar? | url consultato |
| `Origine` | int (enum) | Manuale, Programmata, AI |
| campi audit | | data, utente/servizio |

---

### 3.2 Sottosistema A — Sorveglianza documentale

#### `DocumentiQualita` — inventario dei documenti del SGQ
| Campo | Tipo | Note |
|---|---|---|
| `IdDocumento` | INT IDENTITY PK | |
| `Codice` | nvarchar | es. "MQ", "PG-05", "IO 7/01", "MOD-12" |
| `Titolo` | nvarchar | |
| `Categoria` | int (enum) | Manuale, Procedura, IstruzioneOperativa, Modulo, Altro |
| `RevisioneDoc` | nvarchar? | revisione dichiarata nel documento |
| `PercorsoFile` | nvarchar | posizione del Word |
| `HashContenuto` | nvarchar? | per rilevare modifiche tra scansioni |
| `DataScansione` | datetime2 | UTC |
| campi audit | | |

#### `CitazioniDocumento` — ogni riferimento normativo trovato in un documento
| Campo | Tipo | Note |
|---|---|---|
| `IdCitazione` | INT IDENTITY PK | |
| `IdDocumento` | INT FK | |
| `IdRiferimento` | INT? FK | match col registro (null se non riconosciuto) |
| `TestoGrezzo` | nvarchar | come appare nel documento |
| `EdizioneCitata` | nvarchar? | anno citato nel testo (se presente) |
| `Contesto` | nvarchar? | snippet / paragrafo |
| `Posizione` | nvarchar? | pagina/paragrafo |
| `Segnalazione` | int (enum) | Ok, Superata, EdizioneMancante, Incoerente, DaVerificare |
| campi audit | | data scansione |

**Logica di confronto (triage):**
- *Superata*: l'edizione citata è marcata `Superata`/`SostituitaDa` nel registro.
- *EdizioneMancante*: citazione senza anno/edizione dove l'edizione è rilevante.
- *Incoerente*: stessa norma citata con edizioni diverse in documenti diversi.
- *DaVerificare*: norma non presente nel registro o vigenza non confermabile via web.

> **Limite dichiarato:** la vigenza assoluta dell'intero catalogo UNI non è certificabile
> senza l'abbonamento UNI. Lo strumento garantisce inventario completo + segnalazioni certe
> (supersessioni note, incoerenze, edizioni mancanti) + lista "da verificare" che l'operatore
> conferma. Per ACCREDIA e legislazione (Normattiva/EUR-Lex) la verifica web è affidabile.

---

### 3.3 Sottosistema B — Catalogo limiti

#### `Matrici` — tipo di matrice analitica
| Campo | Tipo | Note |
|---|---|---|
| `IdMatrice` | INT IDENTITY PK | |
| `Nome` | nvarchar | Alimenti, AcquePotabili, AcqueReflue, Superfici, Aria, … |
| `Dominio` | int (enum) | Alimentare, Acque, Ambientale |

#### `CategorieProdotto` — dettaglio entro la matrice
| Campo | Tipo | Note |
|---|---|---|
| `IdCategoria` | INT IDENTITY PK | |
| `IdMatrice` | INT FK | |
| `Nome` | nvarchar | es. "Carni macinate", "Latte crudo", "Acqua consumo umano" |
| `Codice` | nvarchar? | eventuale codifica normativa |
| `IdCategoriaPadre` | INT? FK self | gerarchia opzionale |

#### `Limiti` — la riga di limite (cuore del catalogo, versionata)
| Campo | Tipo | Note |
|---|---|---|
| `IdLimite` | INT IDENTITY PK | |
| `IdMatrice` | INT FK | |
| `IdCategoria` | INT? FK | null = vale per tutta la matrice |
| `IdParametroArcLab` | INT? | aggancio a `Parametro.IdParametro` (sola lettura) |
| `NomeParametro` | nvarchar | denominazione del parametro/analita |
| `TipoLimite` | int (enum) | Chimico, MicrobiologicoSicurezza, MicrobiologicoIgieneProcesso |
| `Operatore` | int (enum) | Max (≤), Min (≥), Intervallo, Assenza |
| `Valore` | decimal? | limite (per chimici e micro semplici) |
| `ValoreMin` / `ValoreMax` | decimal? | per intervalli (es. pH acque) |
| `CriterioM` | decimal? | criterio microbiologico "M" (Reg. 2073) |
| `Criterio_m` | decimal? | criterio microbiologico "m" |
| `NumeroUnita_n` | int? | n. unità campionarie |
| `NumeroTollerati_c` | int? | c del piano di campionamento |
| `Unita` | nvarchar | mg/kg, µg/L, UFC/g, /25g, … |
| `FaseApplicazione` | nvarchar? | es. "prodotti immessi sul mercato" |
| `IdRiferimento` | INT FK | **fonte del limite** (norma + edizione) |
| `RiferimentoPuntuale` | nvarchar? | articolo/allegato/tabella |
| `DataEntrataVigore` | datetime2? | UTC |
| `DataFineValidita` | datetime2? | UTC (null = vigente) |
| `Stato` | int (enum) | Vigente, Superato, Bozza, DaVerificare |
| `IdVersionePrecedente` | INT? FK self | catena di versioni |
| `Note` | nvarchar? | |
| campi audit | | validatore, date, `Attivo` |

> Il **giudizio di conformità** confronta il risultato analitico del campione con la riga
> `Limiti` selezionata per (matrice, categoria, parametro) *vigente alla data del campione*,
> e mostra sempre valore + operatore + **fonte citata**. AI mai nel calcolo del giudizio.

**Fonti ufficiali gratuite di partenza (scelta approvata):**
- Microbiologico alimenti: **Reg. (CE) 2073/2005** e s.m.i.
- Contaminanti chimici alimenti: **Reg. (UE) 2023/915** (ha abrogato il 1881/2006).
- Residui fitosanitari: Reg. (CE) 396/2005 + EU Pesticides Database (MRL).
- Acque potabili: **D.Lgs 18/2023** (Dir. UE 2020/2184; ha sostituito il D.Lgs 31/2001).
- Ambientali/scarichi: D.Lgs 152/2006; Legionella e superfici da linee guida dedicate.

---

## 4. Sorveglianza e aggiornamento

- **Task programmato** (infrastruttura scheduling già disponibile) che periodicamente
  interroga le fonti chiave (EUR-Lex, Normattiva, ACCREDIA) e, per ogni `Riferimento`/`Limite`,
  registra un `EventoSorveglianza` con esito Confermato / Discordanza / DaRivedere.
- **Nessuna modifica automatica**: le discordanze mettono la voce in stato `DaVerificare`
  e compaiono in una coda di revisione.
- **Validazione umana**: l'operatore conferma o aggiorna; l'aggiornamento crea una **nuova
  versione** (la precedente resta con `DataFineValidita` valorizzata), mai sovrascrittura.
- **Verifica puntuale AI on-demand**: "questo limite/norma è ancora vigente?" esegue un
  controllo web e riporta la fonte, senza toccare il dato.

---

## 5. Web, permessi, audit

- Nuovo modulo Web **"Conformità normativa"** (sezione Laboratorio), con due schede:
  *Sorveglianza documentale* e *Catalogo limiti*, più la coda *Revisioni sorveglianza*.
- Nuovi permessi (catalogo `Permessi`): `ConformitaVisualizza`, `ConformitaGestione`
  (validazione/aggiornamento voci).
- Nuova `AreaFunzionale` per l'audit trail (es. `Conformita = 22`), con `RegistraAsync`
  sui punti sensibili (import documenti, validazione limite, aggiornamento riferimento).
- Esportazioni: report audit documentale (Excel/PDF) e catalogo limiti (Excel) coi generatori
  già in casa (ClosedXML / MigraDoc).

---

## 6. Fasi realizzative (dopo approvazione)

**Ossatura condivisa**
1. Entità + migrazione EF Core: `RiferimentiNormativi`, `EventiSorveglianza`, enum e permessi.
2. Servizio di sorveglianza (interfaccia) + task programmato + coda revisioni.

**Sottosistema A — documentale (in parallelo)**
3. Estrattore citazioni da Word (riconoscimento pattern norme + contesto).
4. Entità `DocumentiQualita` / `CitazioniDocumento` + logica di triage.
5. UI scansione cartella SGQ, report segnalazioni, esportazioni.

**Sottosistema B — limiti (in parallelo)**
6. Entità `Matrici` / `CategorieProdotto` / `Limiti` + migrazione.
7. **Pilota: acque potabili (D.Lgs 18/2023)** — popolamento assistito + validazione umana,
   per collaudare il modello end-to-end.
8. Estensione a contaminanti alimenti (Reg. 2023/915) e microbiologico (Reg. 2073/2005).
9. Aggancio `IdParametro` ArcLab e calcolo/controllo del giudizio di conformità.

---

## 7. Punti aperti / decisioni da confermare

1. **Posizione dei Word del SGQ**: `docs\SGQ` o cartella dedicata montata? (attesa copia)
2. **Granularità categorie prodotto**: quanto scendere di dettaglio nel Reg. 2073/2005
   (piani n/c/m/M per singola categoria) fin dalla prima versione?
3. **Aggancio parametri**: mappare `IdLimite ↔ IdParametro` ArcLab da subito o dopo il pilota?
4. **Snapshot fonti**: archiviare copia locale dei testi normativi (per verificabilità offline)
   o tenere solo URL?
5. **Cadenza sorveglianza**: settimanale/mensile per il task programmato?

---

*Fine documento — in attesa di revisione prima della fase realizzativa.*
