# Laboratorio AI — MARE.A srl
## Documento di Analisi Funzionale e Architetturale

| | |
|---|---|
| **Versione** | 1.0 — approvata |
| **Data** | Luglio 2026 |
| **Redatto da** | Analisi condotta con Mirko Maffei, Responsabile di Laboratorio |
| **Stato** | Approvata — punti aperti chiusi, sviluppo autorizzato |

---

## 1. Scopo del documento

Definire perimetro, architettura e requisiti del sistema che sostituirà il prototipo *Laboratorio AI* attualmente in esercizio su PCMAREA01.

Il prototipo ha assolto il proprio compito: ha permesso di scoprire quali funzioni servono davvero e come devono comportarsi. Il sistema nuovo parte da quella conoscenza ma è progettato per la manutenzione a lungo termine, la gestione delle release e l'esercizio su infrastruttura aziendale.

**Questo documento va approvato prima di scrivere codice.** Le decisioni prese qui condizionano tutto il resto; cambiarle a sviluppo iniziato costa molto più che discuterle adesso.

---

## 2. Posizionamento

### 2.1 Natura del sistema

Applicativo **interno** a MARE.A srl. Non destinato alla commercializzazione, non multi-tenant, non soggetto a licensing verso terzi.

Questa scelta semplifica sensibilmente l'architettura: nessun isolamento dati fra organizzazioni, nessuna gestione abbonamenti, nessun canale di distribuzione. Se in futuro il posizionamento cambiasse, sarebbe una riscrittura sostanziale — è bene esserne consapevoli ora.

### 2.2 Ruolo rispetto ad ArcLab

ArcLab resta il **sistema gestionale di riferimento** del laboratorio. Il sistema nuovo non lo sostituisce e non ne duplica le funzioni: lo affianca fornendo strumenti di consultazione, analisi e supporto decisionale che ArcLab non offre.

```
┌─────────────────────────┐         ┌──────────────────────────┐
│        ArcLab           │         │   Sistema MARE.A Lab     │
│   (gestionale, master)  │◄────────│   (consultazione,        │
│                         │  lettura│    analisi, supporto)    │
│  · Campioni             │         │                          │
│  · Prove e risultati    │         │  · Analisi AI            │
│  · Commesse             │         │  · Ricerche trasversali  │
│  · Clienti              │         │  · Statistiche           │
│  · Certificati          │         │  · Generazione DVR       │
└─────────────────────────┘         │  · Portale clienti       │
                                    └──────────────────────────┘
                                                │
                                                ▼
                                    ┌──────────────────────────┐
                                    │  Database LaboratorioAI  │
                                    │   (dati propri)          │
                                    └──────────────────────────┘
```

### 2.3 Confine dei dati

| Dato | Proprietario | Accesso del sistema |
|---|---|---|
| Campioni, lotti, prove, risultati | ArcLab | **sola lettura** |
| Clienti, insediamenti, contatti | ArcLab | **sola lettura** |
| Commesse, controlli, metodi, prezzi | ArcLab | **sola lettura** |
| Certificati | ArcLab | **sola lettura** |
| Analisi AI | **Sistema** | lettura/scrittura |
| Utenti applicativi, ruoli, permessi | **Sistema** | lettura/scrittura |
| Registro DVR generati | **Sistema** | lettura/scrittura |
| Audit trail | **Sistema** | scrittura (append-only) |
| Configurazioni applicative | **Sistema** | lettura/scrittura |
| Sessioni portale clienti | **Sistema** | lettura/scrittura |

> **Regola invariante:** il sistema non scrive mai su ArcLab. Nessuna eccezione. Questo protegge il gestionale accreditato e rende il sistema sostituibile senza impatti sul processo certificativo.

---

## 3. Utenti e ruoli

### 3.1 Categorie di utenza

| Categoria | Chi | Accesso |
|---|---|---|
| **Operatore interno** | Personale MARE.A | Applicazione completa |
| **Cliente** | Clienti del laboratorio | Solo portale, solo propri dati |

### 3.2 Modello dei ruoli

Allo stato attuale **tutti gli operatori interni hanno lo stesso livello di autorizzazione**. Il sistema viene però progettato con un modello a ruoli e permessi granulari, in modo che una futura differenziazione sia una configurazione e non una modifica al codice.

Ruoli previsti dal modello (inizialmente tutti gli utenti sono assegnati ad *Amministratore*):

| Ruolo | Descrizione | Assegnato ora |
|---|---|---|
| `Amministratore` | Accesso completo, gestione utenti e configurazioni | ✓ tutti |
| `Responsabile` | Tutte le funzioni operative, esclusa gestione utenti | — |
| `Operatore` | Consultazione e analisi, no configurazioni | — |
| `Consultazione` | Sola lettura, nessuna azione | — |

I permessi sono definiti per **funzione** (es. `analisi.esegui`, `commesse.visualizza`, `dvr.genera`, `utenti.gestisci`) e associati ai ruoli. Aggiungere una differenziazione futura significherà togliere un permesso da un ruolo, non riscrivere controlli.

**Punto aperto 5.1** — Definire l'elenco completo delle funzioni permissionabili in fase di disegno di dettaglio.

---

## 4. Perimetro funzionale

### 4.1 In perimetro

Riepilogo delle funzioni del prototipo che entrano nel sistema, riorganizzate per area:

**Area Validazione**
- Elenco campioni pronti per validazione, con indicatore di anzianità
- Dettaglio prove del campione con valori, limiti, unità di misura
- Analisi AI singola del campione
- Analisi AI massiva (batch) con esito sintetico
- Archivio delle analisi effettuate, con recupero senza nuovo costo

**Area Consultazione**
- Ricerca campioni con filtri multipli (date, cliente, insediamento, matrice, controllo, prelevatore, luogo, laboratorio esterno)
- Vista campioni per cliente con statistiche di periodo
- Ricerca certificati emessi e analisi AI a posteriori
- Lookup identificativi tecnici di un campione

**Area Metodi e Controlli**
- Ricerca controlli a partire da uno o più metodi (logiche: tutti, almeno uno, esattamente)
- Esplosione di un controllo nei metodi che lo compongono
- Indicazione accreditamento MARE.A e accreditamento del laboratorio in subappalto
- Informazioni di prezzo del controllo sulle commesse attive (minimo, massimo, numero clienti)

**Area Commesse**
- Consultazione commesse attive per tipologia, cliente, insediamento
- Esplosione campioni per le commesse di laboratorio
- Esplosione prestazioni per le commesse di consulenza, con responsabile e scadenza calcolata

**Area Laboratori Esterni**
- Prove affidate in outsourcing nel periodo, per laboratorio e matrice
- Riepilogo metodi per laboratorio con conteggi
- Esportazione in Excel

**Area DVR Legionella**
- Generazione singola con recupero dati cliente da ArcLab
- Generazione massiva da elenco
- Registro dei documenti prodotti

**Portale Clienti**
- Autenticazione cliente
- Consultazione propri campioni con filtri
- Accesso operatore con visibilità su tutti i clienti
- Dettaglio risultati analitici (riservato operatore)

### 4.2 Fuori perimetro

- Scrittura su ArcLab in qualsiasi forma
- Emissione di rapporti di prova (resta funzione di ArcLab)
- Gestione contabile e fatturazione
- Sostituzione del LIMS

### 4.3 Evoluzioni previste ma non nella prima release

Da considerare nel modello dati fin da subito, per non doverlo rifare:

- Calcolo provvigioni agenti (progetto già analizzato separatamente)
- Statistiche e cruscotti direzionali
- Integrazione con Power BI sui dati propri del sistema

---

## 5. Architettura tecnica

### 5.1 Stack proposto

| Livello | Tecnologia | Motivazione |
|---|---|---|
| **Runtime** | .NET 8 (LTS) | Supporto Microsoft fino a novembre 2026, poi migrazione a LTS successiva |
| **Framework web** | ASP.NET Core MVC | Hosting nativo in IIS, standard di mercato per applicativi gestionali su Windows |
| **Accesso dati** | Entity Framework Core (dati propri) + Dapper (query su ArcLab) | EF per il modello proprio con migrazioni versionate; Dapper per le query di sola lettura su ArcLab, che sono complesse e ottimizzate a mano |
| **Database proprio** | SQL Server — istanza 192.168.23.1 | Infrastruttura esistente, backup già strutturato |
| **Frontend** | Razor + JavaScript modulare (ES modules) | Nessuna pipeline di build, manutenibile da qualsiasi sviluppatore .NET |
| **Documenti Word** | DocumentFormat.OpenXml (Microsoft) | Libreria ufficiale, gestione nativa dei Content Control SDT |
| **Fogli Excel** | ClosedXML | Licenza MIT, matura |
| **Logging** | Serilog | Standard di fatto, scrittura su file e su database |
| **Autenticazione** | Microsoft.Identity.Web (Entra ID) | Account M365 aziendali già esistenti |

### 5.2 Perché .NET e non Python

Il prototipo è in Python e funziona. La scelta di cambiare linguaggio risponde al criterio di manutenibilità espresso: *«strumenti e linguaggi di uso comune tra i professionisti, in modo che possa essere mantenuto da chi fa questo di mestiere»*.

Su infrastruttura Windows Server + IIS + SQL Server, il profilo professionale che si trova sul mercato italiano per applicativi gestionali è lo sviluppatore .NET. Un'applicazione Python ospitata in IIS è una configurazione insolita che richiede competenze più specifiche.

In aggiunta:

- **Hosting realmente nativo.** ASP.NET Core gira *dentro* il processo di IIS (in-process, modulo ANCM v2). Non serve alcun gestore di processo esterno, nessuna porta interna, nessun componente aggiuntivo da installare.
- **Configurazione e segreti gestiti dalla piattaforma.** Connection string in `appsettings.json` o nella sezione dedicata di IIS, autenticazione integrata funzionante con l'identità dell'Application Pool, cifratura nativa delle sezioni di configurazione.
- **Schema del database versionato.** Le migrazioni EF Core rendono l'evoluzione dello schema parte del codice sorgente e del processo di rilascio, non un'operazione manuale.
- **Generazione DOCX più solida.** La libreria OpenXml di Microsoft gestisce i Content Control strutturati meglio di quanto faccia python-docx, che ha richiesto diversi aggiramenti nel prototipo.

L'unico punto di attrito è l'assenza di un SDK .NET ufficiale Anthropic: l'API è però REST/JSON e l'integrazione via `HttpClient` è di poche decine di righe.

### 5.3 Struttura della soluzione

```
LaboratorioAI.sln
│
├── src/
│   ├── LaboratorioAI.Web/              ← ASP.NET Core MVC — presentazione
│   │   ├── Controllers/
│   │   ├── Views/
│   │   ├── wwwroot/js/moduli/     ← un modulo JS per area funzionale
│   │   └── Program.cs
│   │
│   ├── LaboratorioAI.Portale/          ← portale clienti (progetto separato)
│   │
│   ├── LaboratorioAI.Core/             ← logica di dominio, indipendente da web e DB
│   │   ├── Entita/
│   │   ├── Servizi/
│   │   └── Interfacce/
│   │
│   ├── LaboratorioAI.Dati/             ← accesso dati
│   │   ├── LaboratorioAI/        ← EF Core, database proprio
│   │   └── ArcLab/                ← Dapper, sola lettura
│   │
│   ├── LaboratorioAI.Documenti/        ← generazione DOCX e XLSX
│   └── LaboratorioAI.IA/  ← integrazione API Claude
│
├── tests/
│   ├── LaboratorioAI.Core.Tests/
│   └── LaboratorioAI.Dati.Tests/
│
└── docs/
    ├── analisi/
    ├── rilasci/
    └── validazione/
```

La separazione fra `Web` e `Portale` come progetti distinti permette di rilasciarli in modo indipendente e di applicare politiche di sicurezza diverse — il portale è esposto su internet, l'applicazione interna no.

**Punto aperto 5.2** — Valutare se mantenere il portale come progetto separato o come area della stessa applicazione. La separazione è più sicura, l'unificazione riduce la duplicazione del codice di accesso ai dati.

---

## 6. Modello dati — database proprio

Database `LaboratorioAI` su istanza 192.168.23.1.

### 6.1 Entità principali

**Analisi AI**
```
AnalisiAI
  IdAnalisi          PK
  IdCampione         → riferimento logico ad ArcLab
  IdCertificato      → riferimento logico, nullable
  Modalita           validazione | certificato
  DataAnalisi
  IdUtente           FK Utenti
  RagioneSociale     ┐
  DenomCampione      │ snapshot al momento dell'analisi:
  Matrice            │ i dati ArcLab possono cambiare,
  DataRicezione      │ l'analisi deve restare interpretabile
  NProve             ┘
  Giudizio           CONFORME | NON CONFORME | BORDERLINE
  TestoAnalisi
  ModelloAI
  TokenInput / TokenOutput / CostoStimato
```

Lo *snapshot* dei dati descrittivi è una scelta deliberata: un'analisi archiviata deve poter essere riletta anche se il campione in ArcLab viene modificato o archiviato.

**Utenti e autorizzazioni**
```
Utenti            IdUtente, ObjectIdEntra, Email, NomeCompleto, Attivo, DataCreazione
Ruoli             IdRuolo, Nome, Descrizione
Permessi          IdPermesso, Codice, Descrizione, Area
UtentiRuoli       IdUtente, IdRuolo
RuoliPermessi     IdRuolo, IdPermesso
```

L'identità viene da Entra ID; la tabella `Utenti` mantiene il collegamento e i dati applicativi. Nessuna password gestita dal sistema.

**Registro DVR**
```
RegistroDVR
  IdDvr, IdCliente, IdInsediamento, Regione, DataGenerazione,
  IdUtente, NomeFile, PercorsoArchivio, Esito, Note
```

**Audit trail**
```
AuditLog
  IdLog, DataOra, IdUtente, IndirizzoIP, Area, Azione,
  EntitaTipo, EntitaId, Esito, Dettaglio
```

Tabella in sola aggiunta, senza aggiornamenti né cancellazioni. Registra le operazioni significative: accessi, analisi eseguite, documenti generati, modifiche alle configurazioni.

**Configurazioni e sessioni**
```
Configurazioni    Chiave, Valore, Descrizione, DataModifica, IdUtente
SessioniPortale   Token, IdCliente, Scadenza, IndirizzoIP, DataCreazione
```

### 6.2 Convenzioni

- Nomi tabella al plurale, in italiano, coerenti col dominio
- Chiavi primarie `Id<Entità>`, sempre `INT IDENTITY`
- Date in UTC nel database, conversione a ora locale in presentazione
- Nessuna cancellazione fisica: campo `Attivo` o `DataCancellazione`
- Ogni tabella con `DataCreazione` e `DataModifica`

### 6.3 Versionamento dello schema

Lo schema è gestito tramite **migrazioni EF Core**: ogni modifica è un file versionato nel repository, applicabile e reversibile. Non si modifica il database a mano.

---

## 7. Sicurezza

### 7.1 Autenticazione operatori

**Entra ID** (Microsoft 365 aziendale). L'utente accede con le proprie credenziali aziendali; il sistema non gestisce password.

Vantaggi diretti: nessuna password da custodire, disattivazione automatica dell'accesso alla cessazione del rapporto di lavoro, MFA se attivo sul tenant, un unico punto di gestione delle identità.

### 7.2 Autenticazione clienti (portale)

**Decisione.** Le credenziali restano quelle presenti in ArcLab: codice contabilità come identificativo e partita IVA come password. Nessuna anagrafica di autenticazione duplicata nel sistema.

Misure tecniche adottate perché la credenziale non transiti né venga conservata in chiaro:

| Misura | Attuazione |
|---|---|
| Cifratura del trasporto | HTTPS obbligatorio end-to-end. Cloudflare in modalità *Full (strict)*, HSTS attivo, redirect automatico da HTTP |
| Credenziale nel corpo della richiesta | La password viaggia nel body della POST, mai in query string, mai in URL |
| Assenza dai log | Filtro Serilog che esclude i campi credenziale da qualunque livello di log, compresi gli errori |
| Assenza dalle risposte | Nessun endpoint restituisce la credenziale, nemmeno in forma parziale |
| Confronto a tempo costante | Verifica con `CryptographicOperations.FixedTimeEquals` per non esporre informazioni tramite timing |
| Nessuna persistenza | La credenziale non viene scritta in sessione, cache o cookie: dopo la verifica si conserva solo il token |
| Protezione forza bruta | Limitazione dei tentativi per codice cliente e per indirizzo IP |

**Rischio residuo accettato.** La partita IVA è un dato pubblico, reperibile su fatture, siti aziendali e visure camerali. Le misure sopra proteggono la credenziale in transito e a riposo, ma non ne aumentano l'entropia: chi conosce codice contabilità e partita IVA di un cliente accede ai suoi dati. Il rischio è invariato rispetto alla situazione attuale ed è accettato consapevolmente.

L'introduzione di una password dedicata per cliente resta possibile in una release successiva senza modifiche architetturali: cambierebbe solo la sorgente della credenziale.

### 7.3 Autorizzazione

Controllo per permesso, non per ruolo, direttamente sugli endpoint. Aggiungere o togliere un permesso a un ruolo è una modifica di configurazione.

### 7.4 Protezione dell'applicazione interna

L'applicazione operatori **non viene pubblicata su internet**. Resta raggiungibile dalla rete aziendale e, per l'accesso da remoto, tramite VPN.

Il portale clienti resta pubblico, dietro Cloudflare in modalità proxy, con la 443 filtrata sui soli intervalli IP di Cloudflare.

---

## 8. Requisiti non funzionali

| Ambito | Requisito |
|---|---|
| **Prestazioni** | Ricerche entro 3 secondi su archivio storico completo; analisi AI entro 30 secondi |
| **Disponibilità** | Orario lavorativo; nessun requisito di alta disponibilità |
| **Utenza concorrente** | Fino a 15 operatori interni, fino a 50 sessioni cliente |
| **Volume dati** | Circa 15.000 analisi AI/anno, archivio ArcLab esistente |
| **Conservazione** | Analisi AI e audit trail: conservazione illimitata |
| **Backup** | Database nel piano di manutenzione SQL Server; applicazione nel backup del server |
| **Ripristino** | RPO 24 ore, RTO 4 ore |
| **Browser** | Chrome, Edge, Firefox — versioni correnti. Portale utilizzabile da mobile |
| **Lingua** | Italiano, interfaccia e documentazione |

### 8.1 Tracciabilità

Il sistema partecipa al processo di validazione dei rapporti di prova. Sono quindi previsti fin dalla prima release:

- Registrazione di chi ha eseguito ogni analisi e quando
- Conservazione integrale del testo delle analisi archiviate
- Audit trail delle operazioni significative
- Versione dell'applicazione registrata su ogni analisi archiviata

Sono elementi poco costosi se progettati da subito e molto onerosi se aggiunti dopo.

---

## 9. Gestione delle release

### 9.1 Versionamento

Versionamento semantico `MAJOR.MINOR.PATCH`:

- **MAJOR** — modifiche che richiedono intervento su dati o configurazioni
- **MINOR** — nuove funzionalità retrocompatibili
- **PATCH** — correzioni

La versione è visibile nell'interfaccia e registrata nell'audit trail.

### 9.2 Repository e controllo del codice

Repository **Git bare** su share dedicata del server aziendale.

```
\\<server>\Sviluppo\LaboratorioAI.git      ← repository condiviso (bare)
D:\LaboratorioAI\Produzione                 ← deployment, non è un clone
D:\LaboratorioAI\Collaudo                   ← deployment, non è un clone
```

Struttura dei rami:

| Ramo | Contenuto |
|---|---|
| `main` | Codice rilasciato in produzione. Ogni commit è taggato con la versione |
| `develop` | Integrazione delle funzionalità in corso |
| `feature/*` | Singola funzionalità o correzione |

La share deve essere accessibile in scrittura ai soli sviluppatori e **esclusa dall'indicizzazione e dalla sincronizzazione OneDrive**, che corromperebbe gli oggetti Git.

> Un repository bare su share SMB è adeguato per un team ristretto ma non offre code review, cronologia web né controllo degli accessi granulare. Se in futuro entrassero più sviluppatori, la migrazione verso Azure DevOps o un Gitea interno è un'operazione di pochi minuti e non impatta il codice.

### 9.3 Ambienti

Entrambi gli ambienti risiedono sul server aziendale.

| Ambiente | Percorso | Database proprio | ArcLab |
|---|---|---|---|
| **Collaudo** | `D:\LaboratorioAI\Collaudo` | `LaboratorioAI_Test` | produzione, sola lettura |
| **Produzione** | `D:\LaboratorioAI\Produzione` | `LaboratorioAI` | produzione, sola lettura |

Il collaudo può leggere direttamente ArcLab di produzione senza alcun rischio: la regola invariante di §2.3 stabilisce che il sistema non scrive mai su ArcLab. Non serve quindi mantenere una copia dei dati gestionali, il che semplifica sensibilmente la gestione degli ambienti.

Ogni ambiente ha il proprio sito IIS e il proprio Application Pool.

### 9.4 Procedura di rilascio

1. Verifica in collaudo delle funzioni modificate
2. Backup database `LaboratorioAI`
3. Applicazione delle migrazioni schema
4. Pubblicazione su IIS (`app_offline.htm` durante l'operazione)
5. Verifica post-rilascio
6. Aggiornamento del changelog

### 9.5 Documentazione mantenuta

| Documento | Aggiornamento |
|---|---|
| Analisi funzionale | A ogni modifica di perimetro |
| Manuale operatore | A ogni release MINOR |
| Manuale portale | A ogni release MINOR |
| Changelog | A ogni release |
| Documentazione di validazione | A ogni release MAJOR |

---

## 10. Migrazione dal prototipo

### 10.1 Fasi

| Fase | Contenuto | Esito |
|---|---|---|
| **0** | Approvazione di questo documento | Perimetro congelato |
| **1** | Impianto: soluzione, database, autenticazione, layout | Applicazione vuota funzionante e pubblicata |
| **2** | Aree Validazione e Consultazione | Sostituisce l'uso quotidiano del prototipo |
| **3** | Aree Metodi, Commesse, Laboratori Esterni | Copertura funzionale completa |
| **4** | Portale clienti | Sostituzione del portale attuale |
| **5** | DVR Legionella | Ultimo per complessità e minore frequenza d'uso |
| **6** | Dismissione prototipo | PCMAREA01 liberato |

Il prototipo resta in esercizio in parallelo fino alla fase 6. Nessuna interruzione di servizio.

### 10.2 Dati da migrare

- Analisi AI presenti nell'archivio attuale
- Nessun altro dato: tutto il resto vive in ArcLab

---

## 11. Registro delle decisioni

Tutti i punti aperti della versione 0.1 sono stati chiusi. Le decisioni sono vincolanti per lo sviluppo; una loro revisione richiede una nuova versione di questo documento.

| Rif. | Questione | Decisione |
|---|---|---|
| 5.1 | Funzioni permissionabili | Modello a permessi granulari implementato, ma **tutti gli operatori assegnati al ruolo Amministratore**. Nessuna differenziazione nella prima release |
| 5.2 | Portale separato o unificato | **Progetto separato**, rilasciabile e proteggibile in modo indipendente |
| 7.1 | Password clienti portale | **Credenziali ArcLab** (codice contabilità + partita IVA). Misure di protezione in §7.2, rischio residuo accettato |
| 9.1 | Repository Git | **Share dedicata sul server aziendale**, repository bare |
| 9.2 | Ambiente di collaudo | **Sul server**, `D:\LaboratorioAI\Collaudo` con database `LaboratorioAI_Test` |
| 11.1 | Denominazione | **Laboratorio AI** confermato |
| 11.2 | Account SQL per Application Pool | Definito in **fase di deploy** |
| 11.3 | Registrazione su Entra ID | Definita in **fase di deploy** |
| 11.4 | Collocazione applicazione | **`D:\LaboratorioAI`** |

### 11.1 Nomenclatura consolidata

| Elemento | Nome |
|---|---|
| Soluzione | `LaboratorioAI.sln` |
| Database proprio | `LaboratorioAI` (produzione), `LaboratorioAI_Test` (collaudo) |
| Radice applicazione | `D:\LaboratorioAI\Produzione`, `D:\LaboratorioAI\Collaudo` |
| Repository | `\\<server>\Sviluppo\LaboratorioAI.git` |
| Siti IIS | `Laboratorio AI`, `Laboratorio AI (Collaudo)`, `Portale Clienti`, `Portale Clienti (Collaudo)` |

## 12. Stato dei lavori

| Fase | Contenuto | Stato |
|---|---|---|
| **0** | Analisi e approvazione | ✅ Completata |
| **1** | Impianto: soluzione, database, autenticazione, layout | ✅ Completata |
| **2** | Aree Validazione e Consultazione | ✅ Completata |
| **3** | Aree Metodi, Commesse, Laboratori Esterni | ✅ Completata |
| **4** | Portale clienti | ✅ Completata |
| **5** | DVR Legionella | ✅ Completata |
| **6** | Verifica, collaudo e dismissione prototipo | 🔄 In corso |

### 12.1 Attività residue prima dell'esercizio

| Attività | Riferimento |
|---|---|
| Prima compilazione della soluzione e correzione degli errori | — |
| Registrazione applicazione su Entra ID | §11.3, guida di messa in esercizio §3 |
| Creazione account di dominio per l'Application Pool | §11.2, guida §4.1 |
| Predisposizione dei modelli DVR con i controlli attesi | guida DVR |
| Verifica della corrispondenza fra modelli e scheda | funzione «Verifica modelli» |
| Confronto degli esiti con il prototipo su campioni noti | — |
| Collaudo con utenti reali | — |
| Dismissione del prototipo su PCMAREA01 | Fase 6 |

Il prototipo resta in esercizio senza interruzioni fino al completamento della fase 6.

---

*MARE.A srl — Cattolica (RN)*
