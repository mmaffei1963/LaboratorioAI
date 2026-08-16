# Report nutrizionali — progetto (modello dati)

## Modulo interno a Laboratorio AI

| | |
|---|---|
| **Data** | Agosto 2026 |
| **Committente** | MARE.A srl — Mirko Maffei |
| **Natura** | Documento di progetto (non implementativo). Abbozzo del modello dati per la revisione, prima di scrivere codice. |
| **Base di partenza** | Foglio Excel `Torta Mimosa.xls` (fogli *Ricetta*, *tabella nutrizionale*, *Nutrizionale*, *Rapporto*). |

---

## 1. Scopo

Sostituire il foglio Excel con un modulo interno a Laboratorio AI che:

- mantiene un **archivio dei contenuti nutrizionali delle materie prime**, alimentato sia dalle **schede tecniche** (cliente/fornitore) sia dai **database in rete** (CREA, ex-INRAN; eventualmente EuroFIR/USDA);
- calcola i valori nutrizionali di un prodotto a partire dalla **ricetta**, conformemente al Reg. (UE) 1169/2011;
- produce il **Rapporto di stima dei valori nutrizionali** e la dichiarazione pronta per l'etichetta, con piena tracciabilità.

I calcoli del foglio attuale sono corretti e conformi: vengono portati nel motore così come sono, con `Torta Mimosa.xls` come test di riferimento.

**Dove si colloca.** Dentro Laboratorio AI, come nuova area funzionale, riusando autenticazione, permessi, generatore documenti e convenzioni SQL. Nuovo schema dedicato **`nutri`** (accanto a `sec`, `app`, `portale`, `log`). Vincolo invariante invariato: nessuna scrittura su ArcLab.

---

## 2. La scelta di fondo: un solo catalogo, due livelli di fonte

La domanda «un archivio dalle schede tecniche e uno dai DB in rete» si risolve **non** con due archivi separati, ma con **un unico catalogo** in cui ogni materia prima può avere più *set di valori*, ciascuno con la propria fonte, data e priorità. Motivo: una ricetta reale mescola i due mondi (nella Torta Mimosa la panna vegetale viene da scheda tecnica, la farina da tabella pubblica) e il calcolo li consuma insieme.

I due "archivi" diventano quindi **due livelli di fonte** dentro lo stesso catalogo, con la stessa priorità già dichiarata nel rapporto attuale:

1. valori **specifici** — scheda tecnica cliente/fornitore (legati a marca/fornitore, prioritari);
2. valori **generici di riferimento** — CREA / ex-INRAN / IEO, poi EuroFIR, poi altre fonti.

Quando arriva o si aggiorna una scheda tecnica si aggiunge un **nuovo set di valori**, conservando lo storico: i rapporti già emessi restano riproducibili perché ognuno registra *quale* set ha usato per ogni ingrediente.

---

## 3. Modello dati (schema `nutri`)

### 3.1 `MateriePrime` — identità dell'ingrediente

| Campo | Note |
|---|---|
| `IdMateriaPrima` | PK, INT IDENTITY |
| `Denominazione` | es. «whippak panna vegetale», «Farina di frumento» |
| `Tipo` | enum: `Specifica` (scheda tecnica) / `Generica` (riferimento CREA) |
| `Marca`, `Fornitore` | valorizzati per le specifiche (nullable) |
| `IdClienteArcLab` | opzionale, se l'ingrediente è legato a un committente (sola lettura) |
| `Categoria` | opzionale, per raggruppamento / classificazione CREA |
| `CodiceInterno` | opzionale |
| `Note` | |
| `Attivo`, `DataCancellazione` | nessuna cancellazione fisica |
| campi audit | come da convenzioni |

### 3.2 `ValoriNutrizionali` — i set di valori (uno-a-molti su MateriePrime)

Valori **per 100 g di parte edibile**. Una materia prima ne ha uno o più nel tempo/per fonte.

| Campo | Note |
|---|---|
| `IdValoreNutrizionale` | PK |
| `IdMateriaPrima` | FK |
| `Grassi`, `GrassiSaturi` | g/100 g |
| `Carboidrati`, `Zuccheri` | g/100 g |
| `Proteine` | g/100 g |
| `Fibre` | g/100 g |
| `Sale` | g/100 g (se la fonte dà il **Sodio**, si memorizza e si converte: Sale = Sodio × 2,5) |
| `Acqua` | g/100 g |
| `EnergiaKj`, `EnergiaKcal` | opzionali: se assenti si ricavano dai macro (Allegato XIV) |
| `IdFonte` | FK → `Fonti` |
| `Priorita` | 1 scheda tecnica · 2 CREA/IEO · 3 EuroFIR · 4 altro |
| `Corrente` | bit: il set attivo per quell'ingrediente (o risolto per priorità+data) |
| `DataValidita`, `DataAcquisizione` | |
| `RiferimentoDocumento` | n° referto, file scheda tecnica, URL |
| `Note`, audit | |

> **Deciso**: si parte con gli **8 nutrienti attuali** (grassi, saturi, carboidrati, zuccheri, proteine, sale, fibre, acqua) **più l'energia**. L'**acqua** resta sempre memorizzata perché serve al ricalcolo del calo peso.
>
> **Campi opzionali futuri** (nutrienti facoltativi del 1169): acidi grassi mono/polinsaturi, polioli, amido, sali minerali/vitamine. Il modello li accoglie senza migrazioni pesanti.

### 3.3 `Fonti` — provenienza riutilizzabile

| Campo | Note |
|---|---|
| `IdFonte` | PK |
| `Tipo` | enum: `SchedaTecnica` / `DatabaseCrea` / `DatabaseEuroFir` / `Referto` / `Bibliografia` / `Calcolo` |
| `Descrizione` | es. «CREA — Tabelle di composizione degli alimenti», «Scheda tecnica Eurovo misto d'uovo rev.3» |
| `Riferimento`, `Url`, `Data` | |
| `PrioritaDefault` | usata se non specificata sul set |

> **Import CREA.** Non serve una tabella separata: l'import popola `MateriePrime` (Tipo = `Generica`) + `ValoriNutrizionali` legati a una `Fonte` di tipo `DatabaseCrea`. È la conferma pratica del "catalogo unico".

### 3.4 `Ricette` — prodotto/ricetta

| Campo | Note |
|---|---|
| `IdRicetta` | PK |
| `Denominazione` | es. «Torta Mimosa» |
| `IdClienteArcLab` | **committente da anagrafica ArcLab** (sola lettura); `IdInsediamentoArcLab`, `Lotto` opzionali |
| `UnitaConsumoG` | es. 100 |
| `UnitaConsumoPezzi` | opzionale |
| `CaloPesoPercentuale` | **canonico**: calo peso in cottura come % del peso crudo (tutto assunto acqua evaporata) |
| `PesoCottoKg` | opzionale: se inserito, calcola `CaloPesoPercentuale` e l'acqua evaporata |
| `Note`, `Attivo`, `DataCancellazione`, audit | |

### 3.5 `RigheRicetta` — ingredienti della ricetta

| Campo | Note |
|---|---|
| `IdRigaRicetta` | PK |
| `IdRicetta` | FK |
| `IdMateriaPrima` | FK |
| `Quantita` | **kg** |
| `Ordine` | |

### 3.6 `Rapporti` — rapporti generati (tracciabilità)

Sullo stile di `AnalisiAi`: ogni rapporto è un record che **congela** i dati usati, così resta riproducibile anche se il catalogo cambia.

| Campo | Note |
|---|---|
| `IdRapporto` | PK |
| `IdRicetta` | FK |
| `DataRapporto`, `IdUtente`, `RedattoDa` | |
| `Committente`, `Denominazione`, `Lotto` | snapshot |
| `Risultati` | JSON: per 100 g + per unità di consumo, %RI, valori grezzi e arrotondati, energia |
| `RicettaUsata` | JSON: righe + **quale set di valori** per ogni ingrediente |
| `VersioneCalcolo`, `VersioneApplicazione` | |
| `PercorsoDocumento` | file generato |
| audit | |

---

## 4. Motore di calcolo (regole, non codice)

Implementa esattamente ciò che fa oggi il foglio:

- **Somma pesata** dei nutrienti per frazione di ingrediente sulla massa totale.
- **Calo peso in cottura**: dalla % di calo (o dal peso cotto) si ricava l'acqua evaporata; i nutrienti restano invariati, l'acqua diminuisce, si ricalcola tutto per 100 g del prodotto cotto.
- **Energia**: coefficienti dell'Allegato XIV del Reg. 1169/2011 (grassi 37 kJ/9 kcal; carboidrati 17/4; proteine 17/4; fibre 8/2; sale 0), calcolata **prima** dell'arrotondamento, poi arrotondata.
- **Arrotondamenti**: tabella delle Linee guida Ministero della Salute (16/6/2016).
- **% di assunzione di riferimento**: Allegato XIII, adulto medio 8 400 kJ / 2 000 kcal.
- **Sale/Sodio**: conversione Sale = Sodio × 2,5 quando la fonte fornisce il sodio.

**Validazione dati** all'inserimento: somma macro + acqua per 100 g ≈ 100; segnalare gli scostamenti (nell'archivio attuale «farina di frumento» somma 105 e «farina integrale» 107 — errori da intercettare).

---

## 5. Rapporto e etichetta

Il *Rapporto di stima dei valori nutrizionali* è un documento a segnaposto (carta MARE.A, dichiarazione per 100 g e per unità di consumo, %RI, fonti, note legali): **stessa macchina a token dei manuali**, quindi si riusa il generatore. In uscita: il rapporto (Word/PDF) e la tabella di dichiarazione pronta per l'etichetta.

---

## 6. Cablaggio in Laboratorio AI

- **Schema** `nutri` con le tabelle sopra; migrazione EF Core dedicata.
- **Permessi**: `nutrizionale.materieprime`, `nutrizionale.ricette`, `nutrizionale.rapporti`.
- **Area funzionale** `Nutrizionale` con voce di navigazione, controller e viste Razor + moduli ES.
- **Convenzioni rispettate**: nomi tabella italiani al plurale, chiavi `Id<Entità>` INT IDENTITY, date UTC, nessuna cancellazione fisica, schema versionato con migrazioni, sola lettura su ArcLab.

---

## 7. Decisioni confermate (agosto 2026)

- Quantità di ricetta in **kg**.
- **Committente** legato all'anagrafica **ArcLab** (sola lettura).
- **8 nutrienti attuali + energia**; **acqua** sempre memorizzata (serve al calo peso).
- **Calo peso** espresso come **percentuale** (canonica), inseribile anche come **peso cotto**; l'acqua evaporata si ricava.

## 8. Prossimi passi

1. **Motore di calcolo** validato contro `Torta Mimosa.xls` (stessi numeri).
2. **Import CREA** come fonte di riferimento + gestione a video dell'archivio materie prime.
3. **Generatore del rapporto** (riuso della macchina a token).

---

*MARE.A srl — Cattolica (RN) — Agosto 2026*
