# Piano — Importazione nativa in ArcLab ("ArcLab tira da LaboratorioAI")

> Stato: **in definizione** (13/09/2026). Da concordare prima di realizzare (regola: prima definiamo, poi si realizza).
> Obiettivo: abbattere la **latenza** dell'attuale flusso "genera XML → import manuale Palomar", mantenendo ArcLab
> come unica fonte di verità e senza violare l'invariante.

## 1. Idea

Invertire la direzione della scrittura. Non è LaboratorioAI che spinge in ArcLab: è un **componente lato ArcLab**
("Ponte/Importatore") che **legge** le entità approvate dalle tabelle di staging di LaboratorioAI e le **scrive
nativamente** nelle tabelle ArcLab, segnandole come importate.

- **Invariante preservato**: a scrivere su `LabAnalisiDati` è un componente/account di ArcLab con diritti di scrittura;
  l'account di LaboratorioAI resta `db_datareader`. LaboratorioAI continua a non scrivere mai su ArcLab.
- **Fonte di verità unica**: i controlli e i campioni restano nativi in ArcLab (veri `IdControllo`, `IdCampione`),
  quindi prove / commesse / fatturazione / rapporti funzionano senza modifiche.
- **Niente più formato XML da mantenere**: l'interfaccia diventa direttamente la tabella di staging (tipizzata).

## 2. Idempotenza (nessun doppione) — requisito centrale

Meccanismo a **tre livelli**, così una riga importata non rientra mai:

1. **Chiave locale stabile**: ogni staging ha già `CodiceLocale` (GUID). È l'identità univoca della cosa da importare.
2. **Registro import in ArcLab** (`LabAnalisiDati`): tabella nuova, es. `ImportDaLaboratorioAI`
   (`IdImport`, `TipoEntita`, `CodiceLocale`, `IdEntitaArcLabCreato`, `DataImport`, `Esito`),
   con **UNIQUE (TipoEntita, CodiceLocale)**. È il libro mastro di ciò che è già entrato.
3. **Transazione atomica**: per ogni riga, in **un'unica transazione**: inserimento nativo (Controllo+rigametodo /
   Lotto+Campione+prove) **+** inserimento della riga nel registro. Se la riga esiste già nel registro → skip.
   Il vincolo UNIQUE garantisce l'esattamente-una-volta anche in caso di esecuzioni concorrenti o ritentativi.

Solo le righe in stato **Approvato/DaTrasmettere** vengono prese (le bozze sono escluse). Dopo l'import la staging
diventa non modificabile (già previsto: "già trasmessa: non modificabile").

## 3. Cosa viene scritto

- **Proposta controllo (nuovo)**: 1 riga in `Controllo` + N righe in `rigametodo` (i `metodo` esistono già,
  si referenziano per `IdMetodo`). Campi riga: IdMetodo, DenominazioneRiga, IdUnita, IdLabEsterno, Arrotonda
  (=CifreSignificative), MaxDecimali, ScontoRiga.
- **Proposta controllo (modifica di esistente)**: aggiornamento dell'insieme di `rigametodo` del controllo indicato
  da `IdControlloEsistenteArcLab` (aggiungi/rimuovi righe). Da trattare in una fase successiva.
- **Accettazione**: `Lotto` + `Campione` + `prove` (per `IdRigaMetodo`). Riusa le anagrafiche ArcLab già snapshottate.

## 4. Visibilità dello stato

LaboratorioAI **legge** il registro `ImportDaLaboratorioAI` (sola lettura) per mostrare "Importato il …, IdControllo …".
In più può aggiornare il proprio `Stato = Importato` sulla staging (scrittura sul proprio DB, consentita).

## 5. Realizzazione — opzioni da decidere

- **Chi scrive**:
  - (A) **Stored procedure T-SQL** in `LabAnalisiDati` + **SQL Server Agent job** schedulato. Tutto lato DB ArcLab,
    zero coinvolgimento di LaboratorioAI nel percorso di scrittura. Richiede cross-DB query (stesso server) o linked server.
  - (B) **Piccolo worker .NET** dedicato ("Importatore ArcLab"), con connessione in scrittura ad ArcLab e in lettura a
    LaboratorioAI. Più manutenibile in C#, ma è un nuovo deployable.
- **Cadenza**: job ogni N minuti (near-real-time), oppure on-demand (bottone "importa ora"), oppure entrambi.
- **Ordine**: prima Proposta controlli (nuovo), poi modifica controllo, poi Accettazione.

## 6. Decisioni prese (13/09/2026)

- **Topologia**: `LaboratorioAI` e `LabAnalisiDati` sullo **stesso server/istanza SQL** → query cross-database con nomi
  a tre parti (`LaboratorioAI_Test.controlli.Proposte`), nessun linked server.
- **Meccanismo**: **stored procedure T-SQL in `LabAnalisiDati` + SQL Server Agent job**. Tutta la scrittura sta lato ArcLab.
- **Cadenza**: **on-demand + schedulato**. Il job Agent gira ogni N minuti; l'on-demand fa partire lo **stesso job**
  con `msdb.dbo.sp_start_job` (LaboratorioAI non scrive su `LabAnalisiDati`: al massimo avvia un job in `msdb`; la
  scrittura la fa l'Agent col suo account). **Da confermare** che questo rientri nell'interpretazione dell'invariante;
  in alternativa l'on-demand lo lancia un operatore da SSMS / da una pagina lato ArcLab.

## 7. Prossimi passi

1. **Serve la struttura reale delle tabelle ArcLab di destinazione** per scrivere insert corretti:
   `Controllo` e `rigametodo` (poi `Lotto`, `Campione`, `prove` per l'accettazione). Da SSMS: `sp_help 'Controllo'`,
   `sp_help 'rigametodo'` (o "Script Table as → CREATE").
2. Script SQL (io li scrivo, tu li esegui): tabella registro `dbo.ImportDaLaboratorioAI` (UNIQUE su TipoEntita+CodiceLocale)
   + stored proc `dbo.Importa_ProposteControlli` (transazione: Controllo + rigametodo + riga registro; skip se già presente)
   + wrapper `dbo.Importa_Tutto` + job Agent `Import_LaboratorioAI`.
3. Lato LaboratorioAI: lettura del registro per mostrare lo stato "Importato / IdControllo", ed eventuale bottone
   "Importa ora". Poi si estende all'accettazione.
