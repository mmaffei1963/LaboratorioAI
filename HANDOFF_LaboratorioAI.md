# Laboratorio AI — Documento di handoff
## Per la continuazione del progetto in una nuova sessione

| | |
|---|---|
| **Data** | Agosto 2026 (aggiornato: aggiunte le funzioni di prefatturazione — Tamponi in contratto e Non fatturare — accreditamento esterno nei certificati, filtri prelevatore/ricevitore, launcher desktop) |
| **Committente** | MARE.A srl — Mirko Maffei, Responsabile di Laboratorio |
| **Scopo** | Consentire la ripresa del lavoro senza aver letto la conversazione precedente |

> **Stato sintetico:** lo sviluppo e il collaudo funzionale sono **completi**. Resta la **messa in produzione** (deploy sul server), descritta nel runbook `DEPLOY_LaboratorioAI.md`. Dopo le ultime modifiche la produzione va ripubblicata (vedi §9).

---

## 1. Nome e scopo del sistema

**Laboratorio AI** è un applicativo **interno** a MARE.A srl, laboratorio accreditato ACCREDIA n° 00729 (ISO/IEC 17025:2018), sede a Cattolica (RN).

Affianca il gestionale ArcLab fornendo strumenti di consultazione, analisi e supporto decisionale che ArcLab non offre: analisi AI dei risultati analitici, ricerche trasversali sull'archivio, generazione automatica di documenti di valutazione del rischio, portale per i clienti.

**Non è destinato alla vendita.** Non è multi-tenant. Non sostituisce ArcLab.

**Regola invariante, mai da violare:** il sistema non scrive mai su ArcLab. Nessuna eccezione. Il vincolo è applicato tecnicamente: l'account di servizio ha solo `db_datareader` su `LabAnalisiDati`.

---

## 2. Funzionalità principali

### 2.1 Aree dell'applicazione operatori

| Area | Funzione | Stato |
|---|---|---|
| **Da Validare** | Campioni con tutte le prove verificate ma senza certificato; analisi AI singola e massiva con archivio-first | Funzionante (errore 500 risolto) |
| **Consultazione** | Ricerca campioni con filtri combinabili (inclusi prelevatore e ricevitore), colonne prezzo/idcontrollo/prelevatore, totali card calcolati lato server sull'intera selezione; identificativi tecnici con tabella per singola prova (accreditamento interno/esterno, IdProva, IdRigaMetodo) attivabile dall'elenco | Funzionante (errore 500 risolto) |
| **Certificati emessi** | Ricerca storico certificati, analisi AI a posteriori; sotto ogni prova esecutore, numero laboratorio esterno e accreditamento (MARE.A / esterno) | Funzionante |
| **Metodi e Controlli** | Ricerca bidirezionale metodi ↔ controlli, tre logiche di confronto, prezzi sulle commesse in vigore | Funzionante |
| **Commesse** | Commesse attive per tipo/cliente/insediamento, esplosione in campioni o prestazioni | Funzionante |
| **Laboratori Esterni** | Prove in outsourcing, riepilogo per metodo, filtro numero lotto, indicazione «accreditato esterno» (a video e in Excel), esportazione Excel | Funzionante |
| **Tamponi in contratto** | Prefatturazione tamponi (IdControllo 180/203/2513, filtro controlli componibile) con riconoscimento del contratto di consulenza HACCP e n° tamponi inclusi; filtri prelevatore e ricevitore; riepilogo per insediamento + dettaglio; Excel | Funzionante |
| **Non fatturare** | Campioni con flag «non fatturare» nel periodo; doppio riepilogo per prelevatore e per ricevitore (con totale generale), dettaglio con prezzo e contratto HACCP; filtri prelevatore/ricevitore/controlli; Excel | Funzionante |
| **DVR Legionellosi** | Generazione DOCX singola e massiva, registro, ispezione modelli | Funzionante — collaudato su dati reali |
| **Piscine** | DVR piscine, Regolamento interno, Manuale/Registro autocontrollo (Emilia-Romagna e Marche), tabelle parametri/punti/scadenzario, dati salvati su SQL | Funzionante — collaudato su dati reali |
| **Nutrizionali** | Archivio materie prime (import USDA/CREA/CNF, import AI da scheda tecnica PDF), Ricette con calcolo nutrizionale in tempo reale, rapporto, verifica AI archivio-first | Funzionante |
| **Strumenti · Taratura termometri** | Modulo 04-01: taratura per confronto con CDR, budget GUM, rapporto, validazione calcolo, analisi AI, duplica | Funzionante |
| **Strumenti · Taratura incubatori** | Modulo 04-02: correzione + deriva CDR, budget, rapporto, validazione, AI, duplica | Funzionante |
| **Strumenti · Mappatura incubatori** | Modulo 04-03: correzione, budget, intervallo per ripiano, test F/Cochran/Kruskal-Wallis, rapporto, validazione, AI | Funzionante |
| **Strumenti · Taratura bilance** | Modulo 45 (EURAMET cg-18): eccentricità/ripetibilità/linearità, budget d'incertezza, pesiere condivise, rapporto PDF/Word a due pagine con grafico, validazione, AI | Funzionante |
| **Strumenti · Monitoraggio TempLog** | Carica il file notturno Econorma (.xls), applica i limiti di taratura letti da ArcLab (mappatura canale→sonda→PianoInterventi TARATURA), evidenzia escursioni rosso/blu, tabella valori per orario, monitor con grafici interattivi per canale, report escursioni Excel/PDF, export sonde ArcLab con data ultima sincronizzazione, validazione umana (conforme/dubbio/non conforme) singola e multipla con registro. **Non altera mai i dati.** | Funzionante — validato sul motore, in collaudo web |

> **Menù per macroaree** (agosto 2026): la barra superiore è organizzata in tendine — **Laboratorio** (Da Validare, Consultazione, Certificati, Metodi e Controlli, Commesse, Laboratori Esterni, Tamponi, Non fatturare), **Manualistica** (DVR Legionella, DVR Piscine), **Nutrizionali**, **Strumenti** (le quattro tarature + Monitoraggio TempLog).

### 2.2 Portale clienti

Progetto ASP.NET Core separato (`LaboratorioAI.Portale`), porta 5081. **Avviato e collaudato.**
- Autenticazione con codice contabilità + P.IVA (credenziali da ArcLab)
- Sessioni su database con token opaco (header `X-Sessione-Portale`, non cookie), revocabili singolarmente
- Protezione da tentativi ripetuti per codice e per IP
- Dettaglio prove riservato all'accesso operatore
- Filtro Serilog che esclude credenziali da ogni livello di log
- Accesso operatore (codice `MAREA`) con password da variabile d'ambiente `Portale__CredenzialeOperatore`, mai nel codice; se assente l'accesso operatore è disabilitato

### 2.3 Analisi AI

- Integrazione API Anthropic (claude-sonnet-4-6) via HttpClient
- Archivio-first: recupera analisi esistente prima di chiamare il modello
- Ogni esecuzione è un record distinto — nessuna sovrascrittura
- Giudizio (CONFORME / NON CONFORME / BORDERLINE) dedotto dal testo
- Snapshot dei dati descrittivi nella riga: l'analisi resta leggibile anche se il campione in ArcLab viene modificato
- Tracciabilità: versione applicazione e modello AI registrati su ogni analisi

---

## 3. Architettura tecnica

### 3.1 Stack

| Livello | Tecnologia | Motivazione |
|---|---|---|
| Runtime | .NET 8 LTS | Standard di mercato su Windows/IIS; manutenibilità da sviluppatori .NET |
| Framework web | ASP.NET Core MVC | Hosting in-process nativo su IIS (ANCM v2), nessun processo esterno |
| Accesso dati | EF Core (db proprio) + Dapper (ArcLab) | EF per schema versionato con migrazioni; Dapper per query complesse su ArcLab |
| Database | SQL Server 192.168.23.1 | Infrastruttura esistente, backup già strutturato |
| Frontend | Razor + ES modules | Nessuna pipeline di build (no npm, no webpack); manutenibile da qualsiasi sviluppatore .NET |
| Documenti Word | DocumentFormat.OpenXml (Microsoft) | Libreria ufficiale; gestisce i Content Control SDT meglio di alternative |
| Fogli Excel | ClosedXML | Licenza MIT, matura |
| Logging | Serilog | Standard di fatto; file + console |
| Autenticazione | Microsoft.Identity.Web (Entra ID) | Account M365 aziendali già esistenti |

### 3.2 Struttura della soluzione

```
LaboratorioAI.sln
├── src/LaboratorioAI.Core/          Entità, modelli, interfacce repository e servizi
├── src/LaboratorioAI.Dati/          EF Core (db proprio) + Dapper (ArcLab sola lettura)
├── src/LaboratorioAI.Web/           ASP.NET Core MVC — applicazione operatori (porta 5080)
├── src/LaboratorioAI.Portale/       Portale clienti (progetto separato, porta 5081)
├── src/LaboratorioAI.IA/            Integrazione API Anthropic
└── src/LaboratorioAI.Documenti/     ClosedXML (Excel) + OpenXml (DVR e Manuali Word)
```

### 3.3 IIS — hosting

- **In-process** (ANCM v2): l'applicazione gira dentro w3wp.exe, nessun processo Python o gestore esterno
- Application Pool senza runtime .NET gestito (`managedRuntimeVersion = ""`)
- `startMode = AlwaysRunning`, `idleTimeout = 0`, nessun riciclo automatico
- Cloudflare in modalità proxy (orange cloud) davanti all'IP pubblico per il portale
- Laboratorio AI non viene pubblicato su internet: accesso solo da LAN o VPN
- Dettaglio operativo completo nel runbook `DEPLOY_LaboratorioAI.md`

---

## 4. Modello dati

### 4.1 Due database

| Database | Proprietario | Accesso del sistema |
|---|---|---|
| `LabAnalisiDati` (ArcLab) | Fornitore ArcLab | Sola lettura — mai scrittura |
| `LaboratorioAI` | Sistema | Lettura/scrittura piena |
| `LaboratorioAI_Test` | Sistema | Collaudo |

### 4.2 Schema del database proprio — quattro schemi SQL

| Schema | Contenuto |
|---|---|
| `sec` | Utenti, Ruoli, Permessi, UtentiRuoli, RuoliPermessi |
| `app` | Analisi (AI), RegistroDvr, DvrPiscine (dati scheda in JSON), Configurazioni |
| `nutrizionale` | Materie prime, Ricette, VerificheNutrizionali |
| `taratura` / `incubatori` / `mappatura` | Tarature, ValidazioniCalcolo, Analisi dei moduli 04-01/04-02/04-03 |
| `bilance` | Pesiere, MasseCampione, TaratureBilance, ValidazioniCalcoloBilance, AnalisiBilance |
| `templog` | MappatureCanali, Caricamenti (con validazione umana), Sincronizzazioni |
| `portale` | SessioniPortale, TentativiAccesso |
| `log` | Audit (append-only) |

### 4.3 Entità principali

**AnalisiAi** — ogni esecuzione è un record distinto
- `IdAnalisi`, `IdCampione`, `IdCertificato?`, `Modalita`
- `DataAnalisi`, `IdUtente`
- Snapshot: `RagioneSociale`, `DenominazioneCampione`, `Matrice`, `DataRicezione`, `NumeroProve`
- `Giudizio` (enum: NonDeterminato / Conforme / Borderline / NonConforme)
- `Testo` (testo integrale), `ModelloAI`, `VersioneApplicazione`
- `TokenInput`, `TokenOutput`, `CostoStimato`

**DvrPiscina** — scheda piscina persistita
- Dati anagrafici, vasche e impianti serializzati in `DatiScheda` (JSON) nello schema `app`
- Fonte unica dei dati fisici delle vasche, riusata per DVR, Manuale ER e Registro Marche
- Migrazione `20260726102836_AggiuntaDvrPiscine`

**Monitoraggio TempLog** (schema `templog`)
- `MappaturaCanaleTempLog` — legame stabile Canale (T01…) ↔ IdStrumento ArcLab; i limiti NON sono qui, si rileggono da ArcLab a ogni caricamento; soft-delete + validità per il turnover sonde
- `CaricamentoTempLog` — storico dei file analizzati con esiti (escursioni), cartella d'origine (per riproporre l'ultima), e **validazione umana**: `EsitoValidazione` (Conforme/Dubbio/NonConforme), `Validatore`, `DataValidazione`, `NoteValidazione`
- `SincronizzazioneSondeTempLog` — registra ogni sincronizzazione delle sonde da ArcLab (data «ultima sincronizzazione»)
- Risoluzione limiti (sola lettura ArcLab): sonda → `apparecchiature.Associato1` (testo con «Cod: <IdApparecchio ambiente>») → `PianoInterventi` con `NomePiano` contenente «TARATURA» (mai «MAPPATURA»), match `CodPiano` tollerante ai separatori → `ValTeorico`, `LimInfInt`, `LimSupInt`. Lettura file .xls via ExcelDataReader.

**VoceAudit** — append-only, mai aggiornata né cancellata
- `DataOra`, `IdUtente`, `EmailUtente`, `IndirizzoIp`
- `Area` (enum), `Azione`, `TipoEntita`, `IdEntita`
- `Esito`, `Dettaglio` (JSON), `VersioneApplicazione`

**SessionePortale** — sessioni del portale su database (sopravvivono ai riavvii)
- Token opaco generato con `RandomNumberGenerator`
- `Scadenza`, `DataRevoca` (revoca puntuale)
- La credenziale non è mai memorizzata

### 4.4 Convenzioni del modello dati

- Nomi tabella in italiano, plurale
- Chiavi primarie `Id<Entità>`, sempre `INT IDENTITY`
- Date in UTC nel database, conversione a ora locale in presentazione
- Nessuna cancellazione fisica: campo `Attivo` o `DataCancellazione`
- Schema versionato con migrazioni EF Core — mai modificare il database a mano
- Migrazioni presenti: `20260724102024_Iniziale`, `20260726102836_AggiuntaDvrPiscine`

### 4.5 Note tecniche ArcLab (agganci di schema da ricordare)

Facts scoperti durante lo sviluppo dei report, causa di errori 500 se ignorati:

- **CodLotto / CodCampione sono colonne testo** con valori non numerici (es. `\`): confrontandoli con interi usare sempre `TRY_CONVERT(int, ...)`, altrimenti errore 245.
- **Prelevatore**: `Campione.IdPrelevatore → mansione (IdMansione, IdAttività = 1) → Organico`. Nome = `LTRIM(RTRIM(ISNULL(CognomeOrg,'') + ' ' + ISNULL(NomeOrg,'')))`.
- **Ricevitore**: `Lotto.IdRicevitore → mansione (IdMansione, IdAttività = 2) → Organico`, stesso schema di nome.
- **Contratto di consulenza HACCP** = commessa `IdTipoCommessa = 5` con `DescrIncarico LIKE 'Gestione ed assistenza autocontrol%HACCP%'` (una sola «l» per intercettare il refuso «autocontrolo» in ArcLab) **e** con prestazioni presenti; il n° tamponi inclusi = `DurataPres`.
- **Accreditamento**: `Metodo.Accreditato` (MARE.A) e `Metodo.Accreditato_Esterno`; per il lab esterno usare `labEstMetodo.AccrLabEsterno` con `EXISTS` (non `MAX(bit)`, non valido). Numero laboratorio = `LabEsterno.NumeroAccredia`.
- **Prefatturazione**: `commessa.NonFatturare` (bit), `commessa.PrezzoControllo`, `commessa.DataScadenzaInc`; certificato `Certificato.NCertificato/DataCertificato/DataFattura`. In SSMS le query vanno prefissate con `USE LabAnalisiDati;`.

---

## 5. Ruoli e permessi

### 5.1 Modello

Autorizzazione a permessi granulari, non a ruoli. I permessi hanno forma `area.funzione`:

```
validazione.visualizza          validazione.analizza        validazione.analizza_lotto
consultazione.campioni          consultazione.certificati   consultazione.clienti
consultazione.risultati         metodi.consulta             metodi.prezzi
commesse.consulta               commesse.prezzi             labesterni.consulta
labesterni.esporta              prefatturato.tamponi        prefatturato.nonfatturare
dvr.genera                      dvr.lotto                   dvr.registro
piscine.genera                  amministrazione.utenti      amministrazione.configurazioni
amministrazione.audit
```

> I permessi effettivi sono elencati in `src\LaboratorioAI.Core\Costanti\Permessi.cs`: questa lista è indicativa. Le aree «Tamponi in contratto» e «Non fatturare» usano i permessi di prefatturazione.

### 5.2 Stato attuale

**Tutti gli utenti sono assegnati al ruolo Amministratore.** Tutti vedono tutto (scelta confermata: «per ora tutti vedono tutto»).

Il modello a ruoli (Amministratore, Responsabile, Operatore, Consultazione) è definito e i ruoli sono in database, ma solo Amministratore ha permessi assegnati nella prima release.

**Differenziazione futura = configurazione, non codice.** Togliere un permesso a un ruolo è una riga in una tabella.

### 5.3 Implementazione tecnica

- `FornitorePolicyPermessi`: genera policy al volo da qualunque codice permesso
- `[RichiedePermesso(Permessi.ValidazioneAnalizza)]`: attributo su controller/azione
- I permessi viaggiano come claim nel cookie di sessione
- Modifica ai permessi: effetto al successivo accesso dell'utente

---

## 6. Vincoli e requisiti confermati

| Requisito | Dettaglio |
|---|---|
| **Credenziale portale** | Letta da ArcLab (P.IVA). Non passa mai in chiaro: HTTPS obbligatorio, solo nel body della POST, esclusa dai log con filtro Serilog, confronto a tempo costante su hash SHA-256, confronto eseguito anche per codici inesistenti (timing attack) |
| **Repository Git** | Bare su share dedicata: `\\<server>\Sviluppo\LaboratorioAI.git`. Escludere da OneDrive e da sincronizzazioni automatiche |
| **Ambiente collaudo** | `D:\LaboratorioAI\Collaudo`, database `LaboratorioAI_Test`. Legge ArcLab di produzione in sola lettura — nessun rischio |
| **Deploy produzione** | `D:\LaboratorioAI\Produzione`, database `LaboratorioAI`. Vedi `DEPLOY_LaboratorioAI.md` |
| **Entra ID** | Da configurare in fase di deploy: TenantId e ClientId da inserire come variabili d'ambiente o `appsettings.Production.json` (fuori dal repository). Richiede admin M365 |
| **Application Pool** | Account di dominio `MAREA\svc_labai`: `db_datareader` su ArcLab, su `LaboratorioAI` datareader+datawriter+ddladmin per le migrazioni |
| **Rischio residuo portale** | La P.IVA è dato pubblico: le misure proteggono il transito, non aumentano l'entropia. Accettato consapevolmente |

---

## 7. Stato attuale

### 7.1 Completato

- [x] Documento di analisi funzionale approvato
- [x] Soluzione .NET 8 con sei progetti — **compila con 0 errori**
- [x] Migrazioni `Iniziale` e `AggiuntaDvrPiscine` generate e applicate
- [x] Accesso di sviluppo (senza Entra ID) attivo in `Development`
- [x] Web app su `http://localhost:5080` con `LaboratorioAI_Test` su `PCMAREA01\SQLEXPRESS`
- [x] Tutte le aree operatori funzionanti (errori 500 «Da Validare» e «Ricerca campioni» risolti)
- [x] **Piscine**: DVR, Regolamento, Manuale ER e Registro Marche completi e collaudati su dati reali; dati scheda salvati su SQL (schema `app`, `DvrPiscine`)
- [x] Raccolta dati piscine resa responsive per smartphone (CSS)
- [x] Chiave API Anthropic impostata via user-secrets (`Anthropic:ChiaveApi`)
- [x] DVR Legionellosi collaudato su modelli reali
- [x] **Portale clienti avviato e testato**: login cliente (codice + P.IVA) e `/stato` verificati; corretta gestione del messaggio d'errore in accesso
- [x] **Tamponi in contratto** (prefatturazione): riproduce in sola lettura i criteri di prefatturazione ArcLab; IdControllo 180/203/2513 con filtro controlli componibile; aggancia il contratto di consulenza HACCP (commessa tipo 5) con n° tamponi inclusi; filtri prelevatore e ricevitore; Excel su due fogli
- [x] **Non fatturare** (prefatturazione): campioni con flag NonFatturare nel periodo; doppio riepilogo per prelevatore e per ricevitore con totale generale; dettaglio con prezzo e contratto HACCP; filtri e Excel (colonna + foglio ricevitore)
- [x] **Certificati emessi**: sotto ogni prova, esecutore, numero laboratorio esterno (`LabEsterno.NumeroAccredia`) e accreditamento (MARE.A `Metodo.Accreditato` / esterno via `labEstMetodo.AccrLabEsterno`)
- [x] **Laboratori Esterni**: filtro numero lotto e indicazione «accreditato esterno» (a video e in Excel)
- [x] **Consultazione**: filtri prelevatore/ricevitore, colonne prezzo/idcontrollo/prelevatore, totali card completi calcolati lato server; tabella per singola prova negli identificativi tecnici (accreditamento interno/esterno, IdProva, IdRigaMetodo)
- [x] **Totali delle card sempre completi** su tutte le schede (calcolo lato server o caricamento completo con dettaglio a video limitato)
- [x] **Launcher desktop** (`Avvia_LaboratorioAI.bat` / `.ps1`): avvia il server se non attivo e apre `http://localhost:5080`
- [x] Etichetta «Elenco troncato» arricchita con il numero di righe mostrate; IdControllo mostrato accanto al nome del controllo (Tamponi e Non fatturare)
- [x] **Nutrizionali**: archivio materie prime + Ricette con calcolo in tempo reale, import AI da scheda tecnica, verifica AI
- [x] **Tarature strumenti** (04-01, 04-02, 04-03, bilance 45): motori validati con test xUnit, rapporti PDF/Word, validazione del calcolo con registro/avviso, analisi AI, duplica; piani di validazione formule (docx) per bilance e mappatura
- [x] **Monitoraggio TempLog Econorma**: motore validato sul file reale; lettura `.xls`, limiti da ArcLab (mappatura canale→sonda→PianoInterventi TARATURA), evidenziazione escursioni, valori per orario, monitor interattivo, report Excel/PDF, export sonde, validazione umana singola/multipla con registro (**in collaudo web**)
- [x] **Menù riorganizzato per macroaree** (Laboratorio, Manualistica, Nutrizionali, Strumenti)

### 7.2 Aperti (sviluppo/collaudo)

Nessuno bloccante. Lo sviluppo è concluso.

### 7.3 Da fare — messa in produzione (con il sistemista)

Tutti i dettagli operativi sono nel runbook **`DEPLOY_LaboratorioAI.md`**. In sintesi:

- Creazione database `LaboratorioAI` sul server e applicazione migrazioni (automatiche al primo avvio della Web app)
- Creazione account `MAREA\svc_labai` e assegnazione permessi (invariante: `db_datareader` su ArcLab)
- Registrazione app su Entra ID (TenantId/ClientId) — richiede admin M365
- Pubblicazione e configurazione IIS di Web (5080) e Portale (5081)
- Variabili d'ambiente di produzione: `Anthropic__ChiaveApi`, `EntraId__*`, `Portale__CredenzialeOperatore`, percorsi modelli piscine
- Esposizione esterna del portale (reverse proxy / Cloudflare)
- Collaudo post-deploy secondo la checklist del runbook

---

## 8. Percorsi chiave

| Elemento | Percorso |
|---|---|
| Soluzione | `C:\Sviluppo\LaboratorioAI\LaboratorioAI.sln` |
| Runbook di deploy | `C:\Sviluppo\LaboratorioAI\DEPLOY_LaboratorioAI.md` |
| Configurazione sviluppo Web | `src\LaboratorioAI.Web\appsettings.Development.json` |
| Configurazione sviluppo Portale | `src\LaboratorioAI.Portale\appsettings.Development.json` |
| Program.cs Web | `src\LaboratorioAI.Web\Program.cs` |
| Program.cs Portale | `src\LaboratorioAI.Portale\Program.cs` |
| Migrazioni EF | `src\LaboratorioAI.Dati\Migrations\` |
| Mappa valori DVR | `src\LaboratorioAI.Documenti\Dvr\MappaValori.cs` |
| Generatore piscine | `src\LaboratorioAI.Documenti\Piscine\GeneratorePiscine.cs` |
| Repository campioni | `src\LaboratorioAI.Dati\ArcLab\Repository\RepositoryCampioni.cs` |
| Repository portale | `src\LaboratorioAI.Dati\ArcLab\Repository\RepositoryPortale.cs` |
| Repository tamponi in contratto | `src\LaboratorioAI.Dati\ArcLab\Repository\RepositoryTamponiContratto.cs` |
| Repository non fatturare | `src\LaboratorioAI.Dati\ArcLab\Repository\RepositoryNonFatturare.cs` |
| Esportatori Excel prefatturazione | `src\LaboratorioAI.Documenti\EsportatoreTamponiContratto.cs`, `EsportatoreNonFatturare.cs` |
| Catalogo permessi | `src\LaboratorioAI.Core\Costanti\Permessi.cs` |
| Launcher desktop | `Avvia_LaboratorioAI.bat` / `Avvia_LaboratorioAI.ps1` (radice soluzione) |
| Modelli Word piscine | `C:\Sviluppo\Piscine\Modello\` |

**Comandi di avvio (sviluppo)**:
```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet run --project src\LaboratorioAI.Web      # porta 5080
dotnet run --project src\LaboratorioAI.Portale  # porta 5081
```

> Nota operativa: Web (5080) e Portale (5081) condividono i progetti `Core`/`Dati`/`Documenti`. Se una è in esecuzione mentre ricompili l'altra dopo aver toccato quei progetti, la build resta bloccata sui DLL bloccati. In quel caso: `Ctrl+C`, `dotnet build-server shutdown`, chiudi i processi, e usa `dotnet build` seguito da `dotnet run --no-build`. Il Serilog del portale scrive solo su file (`log\portale-*.log`): la console vuota all'avvio è normale, l'app è comunque in ascolto.

---

## 9. Prossimi passi

Un solo blocco residuo: **la messa in produzione**, da eseguire seguendo `DEPLOY_LaboratorioAI.md`.

> **Ripubblicazione (dopo le ultime modifiche).** Le funzioni recenti (prefatturazione, accreditamento certificati, filtri, ecc.) non sono ancora nella cartella di produzione. Al momento del passaggio su server, pubblicare le versioni Release aggiornate:
> ```powershell
> dotnet publish src\LaboratorioAI.Web     -c Release -o D:\LaboratorioAI\Produzione\Web
> dotnet publish src\LaboratorioAI.Portale -c Release -o D:\LaboratorioAI\Produzione\Portale
> ```
> Dopo la publish, rimuovere gli `appsettings.Development.json` dalle cartelle di produzione (la configurazione di produzione arriva da `appsettings.Production.json` e dalle variabili d'ambiente).

Ordine consigliato:

1. Preparare il database e l'account di servizio (sezione 3 del runbook)
2. Registrare l'app su Entra ID (sezione 7)
3. Pubblicare e configurare IIS per Web e Portale (sezioni 4, 6, 8)
4. Predisporre cartelle e modelli documentali sul server (sezione 5)
5. Eseguire la checklist di verifica e sicurezza post-deploy (sezioni 9 e 10)

---

## 10. Progetto Piscine — integrato

Il componente Piscine, previsto inizialmente come sviluppo separato, è stato **realizzato e integrato** in Laboratorio AI seguendo le convenzioni della sezione 4: permessi, area funzionale, controller `PiscineController`, voce di navigazione, persistenza su SQL (schema `app`). Genera DVR piscine, Regolamento interno e Manuale/Registro di autocontrollo per Emilia-Romagna e Marche, con i dati fisici delle vasche come fonte unica condivisa fra i documenti e le soglie normative regionali gestite separatamente.

---

*MARE.A srl — Cattolica (RN) — Luglio 2026*
