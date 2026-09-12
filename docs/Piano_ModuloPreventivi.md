# Piano operativo — Modulo Preventivi (LaboratorioAI)

> Stato: **proposta approvata da Mirko** (11/09/2026), da sviluppare per fasi.
> Invariante rispettata: **nessuna scrittura su ArcLab** (sola lettura `db_datareader`).

## 1. Obiettivo

Consentire ai commerciali di comporre i preventivi/offerte direttamente in LaboratorioAI,
caricando i dati dai **controlli** e **metodi** già esistenti in ArcLab (con relativi costi),
con possibilità di proporre controlli non ancora presenti, applicare sconti soggetti ad
approvazione, e generare l'offerta finale in Word.

## 2. Sorgente dati e prezzi (ArcLab, sola lettura — già mappato)

La catena prezzi è: `Controllo → rigametodo (IdControllo) → metodo (IdMetodo, CostoMetodoEuro)`.
Il prezzo base di un controllo = **somma dei `CostoMetodoEuro`** dei suoi metodi.

Infrastruttura già disponibile in `RepositoryControlli` (riutilizzata, non riscritta):

- `ControlliAttiviAsync()` — elenco controlli attivi.
- `RigheDiControlloAsync(idControllo)` — esplode il controllo nei metodi con `CostoMetodoEuro`
  → da cui si calcola la somma = prezzo base.
- `MetodiCatalogoAsync(filtro)` — catalogo metodi attivi (per comporre controlli nuovi).
- `PrezziControllo` / `ControlloEsploso` — quadro prezzi min/max realmente applicato ad altri
  clienti sulle commesse in vigore → usato come **riferimento/controllo di sanità** accanto al prezzo base.

**Prezzo di commessa per-cliente** (base preferenziale): la tabella `commessa` contiene
`PrezzoControllo` per `IDControllo`, legata al cliente via `Cod_Cliente`/`Clienti.CodContabilita`,
attiva quando `DataScadenzaInc IS NULL`. È già raggiungibile con `RepositoryCommesse.CercaAsync`
(filtri `IdCliente` + `IdControllo`); si aggiungerà un metodo dedicato
`PrezzoCommessaClienteAsync(idCliente, idControllo)` che restituisce `commessa.PrezzoControllo`
della commessa attiva, se esiste.

Controlli non ancora presenti: si usa il meccanismo **già esistente** `PropostaControllo` +
`PropostaRigaMetodo` (staging locale, prezzo sommato dai metodi scelti, XML per import
supervisionato Palomar — nessuna scrittura diretta su ArcLab).

### Terzo caso: controlli/metodi in subappalto a campo libero

Spesso Ludo inserisce in offerta controlli con metodi **non presenti in ArcLab**, comunicati
dai laboratori in subappalto, con prezzo concordato in base al costo del lab esterno. Requisiti:

- Il commerciale deve poter **verificare** che il metodo non sia già tra i nostri
  (ricerca su `RepositoryControlli.MetodiCatalogoAsync`) prima di crearlo a mano.
- Se non c'è, compone la riga a **campo libero** indicando controllo e metodo, con **gli stessi
  campi dei nostri metodi** (denominazione, riferimento, unità, costo, accreditamento, lab
  esterno), così in offerta sono **indistinguibili** dai metodi interni.
- Per le prove in subappalto l'offerta riporta **sempre** il riferimento al laboratorio esterno,
  tramite il campo `LabEsterno.Frase` (già disponibile in ArcLab).
- Il **laboratorio esterno NON è libero**: si sceglie sempre dall'anagrafica dei lab esterni ArcLab
  (`LaboratoriConFraseAsync`), perché le offerte arrivano solo dai laboratori con cui si lavora già.
  Libero è solo il metodo/controllo.

Modellato con `TipoRigaPreventivo.ControlloLibero` e con il dettaglio metodi per riga
`RigaMetodoPreventivo` (usato per tutte le righe: snapshot dei metodi ArcLab per i controlli
esistenti, inserimento manuale per quelli liberi). `RigaMetodoPreventivo.LabEsternoFrase`
conserva la frase del lab esterno da stampare in offerta.

## 3. Decisioni funzionali (definite con Mirko)

- **Prezzo di partenza (base)**: risoluzione a cascata per ogni controllo/cliente:
  1. se per quel **cliente** esiste una commessa attiva sul controllo → base = `commessa.PrezzoControllo`;
  2. altrimenti → base = **somma dei `CostoMetodoEuro`** dei metodi del controllo.
- **Sconto**: campo **percentuale** sul prezzo base (opzionale). Pensato soprattutto per il caso 2
  (listino grezzo), ma disponibile in generale. Valore assente/0 = prezzo pieno.
- **Approvazione**: richiesta **solo** quando è stato inserito uno sconto. Senza sconto il
  preventivo può procedere all'invio senza passaggi. Con sconto → stato `DaApprovare` e blocco
  invio finché un utente con ruolo approvatore non conferma.
- **Utenti**: più commerciali; un ruolo aggiuntivo *approvatore*.
- **Anagrafica clienti**: **mista** — se il cliente è già in ArcLab si usa quello; altrimenti si
  inserisce un cliente/prospect ex novo (tipico in fase di offerta), salvato lato LaboratorioAI.
- **Offerta Word**: **layout nuovo** (non vincolato al modello CELACK), con richiamo alle
  Condizioni Generali di Fornitura.

## 4. Modello dati (nuovo schema `preventivi` nel DB LaboratorioAI)

Chiavi `Id<Entità>` INT IDENTITY, date UTC, nessuna cancellazione fisica, migrazioni EF Core.

- **Preventivi** (testata): `IdPreventivo`, `Numero` (progressivo), `IdClienteArcLab?` /
  dati cliente ex novo (denominazione, P.IVA, indirizzo), `Oggetto`, `DataCreazione`,
  `ValiditaGiorni` / `DataScadenza`, `Stato`, `RevisioneCondizioni` (es. "Mod. Com. 05 Rev. 18
  del 16/04/25"), `TotaleImponibile`, `IdUtenteCreatore`, `IdUtenteApprovatore?`, `DataApprovazione?`,
  `Note`, flag `Attivo` + `DataCancellazione`.
- **RighePreventivo**: `IdRigaPreventivo`, `IdPreventivo`, tipo riga (controllo esistente / proposta
  nuova / **controllo libero-subappalto** / accessorio), `IdControlloArcLab?`, `IdPropostaControllo?`,
  `IdVoceAccessorio?`, `Descrizione`, `OriginePrezzo`, `PrezzoBase`, `ScontoPercentuale`, `Quantita`,
  `PrezzoFinale`, `Progressivo`.
- **RigheMetodo** (`RigaMetodoPreventivo`): dettaglio metodi/parametri di ogni riga (snapshot).
  Campi allineati ai metodi ArcLab + `Libero`, `IdLabEsternoArcLab?`, `LabEsterno`, `LabEsternoFrase`.
- **ListinoAccessori** (precaricato dalle Condizioni Generali): giudizio di conformità €10,
  modifica RdP €20, conservazione €0,50/gg, campionamento. Modificabile.

Stati preventivo: `Bozza → (se sconto) DaApprovare → Approvato → Inviato → Accettato | Rifiutato`.

## 5. Fasi di sviluppo

1. **Persistenza**: entità + `ConfigurazioniPreventivi` + migrazione EF Core (schema `preventivi`),
   listino accessori seed.
2. **Servizi/logica**: calcolo prezzo base (somma `CostoMetodoEuro`), applicazione sconto,
   determinazione se serve approvazione, numerazione progressiva, gestione anagrafica mista.
3. **Repository ArcLab**: riuso di `RepositoryControlli` (esistente) + eventuale lookup anagrafica
   clienti ArcLab (sola lettura).
4. **Interfaccia** (Razor + JS): scelta cliente (ArcLab o nuovo) → aggiunta controlli (catalogo
   esistente + creazione proposta) → prezzi/sconti → accessori → validità/pagamento → stati →
   pulsante "Richiedi approvazione" quando serve.
5. **Generazione Word** (OpenXml): nuovo layout offerta con tabelle parametro/metodo/prezzo,
   condizioni economiche, pagamento, e **clausola di richiamo alle Condizioni Generali** inclusa.
6. **Tracciabilità/ruoli**: ruolo approvatore, log approvazioni, revisione condizioni applicata.

## 6. Da chiarire in corso d'opera

- Sconto per riga o solo sul totale (default proposto: entrambi possibili, ma l'approvazione
  scatta se esiste qualunque sconto > 0).
- Anagrafica clienti ArcLab: tabella/campi da leggere (da definire con lo schema ArcLab).
- Soglia di sconto oltre la quale l'approvazione è obbligatoria (per ora: qualunque sconto).
