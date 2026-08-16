# Runbook di messa in produzione — Laboratorio AI

Documento operativo per il passaggio in produzione di **Laboratorio AI**. Pensato per essere eseguito dal sistemista sul server. Ogni passo riporta i comandi esatti e i controlli di verifica.

> **Regola invariante (non violabile):** il sistema **non scrive mai** su ArcLab (`LabAnalisiDati`). L'account di servizio ha **solo `db_datareader`** su ArcLab e la stringa di connessione usa `ApplicationIntent=ReadOnly`. Nessuna eccezione.

---

## 1. Panoramica

L'applicativo è composto da **due applicazioni web** ricavate da una soluzione a sei progetti:

| Applicazione | Progetto | Porta (dev) | Esposizione | Autenticazione |
|---|---|---|---|---|
| Gestionale interno | `LaboratorioAI.Web` | 5080 | Rete interna | Entra ID (Microsoft) |
| Portale clienti | `LaboratorioAI.Portale` | 5081 | Internet | Codice contabilità + P.IVA (cliente) / credenziale operatore |

Due database:

- **`LaboratorioAI`** — database dedicato dell'applicativo (lettura/scrittura). Contiene utenti, permessi, DVR piscine, sessioni portale, log tentativi. Le tabelle vengono create automaticamente dalle migrazioni EF Core al primo avvio della Web app.
- **`LabAnalisiDati` (ArcLab)** — gestionale esistente, **sola lettura**.

---

## 2. Prerequisiti sul server

- Windows Server con **IIS** attivo.
- **.NET 8 ASP.NET Core Hosting Bundle** installato (fornisce il modulo `AspNetCoreModuleV2` per IIS). Verifica: `dotnet --list-runtimes` deve elencare `Microsoft.AspNetCore.App 8.x`.
- Raggiungibilità di SQL Server (`192.168.23.1`) dal server web.
- Account di servizio di dominio **`MAREA\svc_labai`** creato.
- Cartella base di produzione: **`D:\LaboratorioAI\Produzione`**.

---

## 3. SQL Server — database e permessi

> **Script pronto:** questi passaggi sono già raccolti nel file **`deploy_database.sql`** (nella radice del progetto). Aprirlo in SSMS con un account **amministratore SQL** ed eseguirlo: crea il database, l'account e i permessi, con verifica finale. I comandi qui sotto sono per riferimento/lettura.

Da eseguire in SSMS con un account amministratore SQL.

**3.1 Creare il database dedicato (vuoto):**

```sql
CREATE DATABASE LaboratorioAI;
```

**3.2 Login dell'account di servizio e permessi:**

```sql
-- Login di dominio per l'account di servizio
CREATE LOGIN [MAREA\svc_labai] FROM WINDOWS;

-- Database applicativo: lettura/scrittura + creazione tabelle (migrazioni)
USE LaboratorioAI;
CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
ALTER ROLE db_datareader  ADD MEMBER [MAREA\svc_labai];
ALTER ROLE db_datawriter  ADD MEMBER [MAREA\svc_labai];
ALTER ROLE db_ddladmin    ADD MEMBER [MAREA\svc_labai];  -- necessario alle migrazioni EF

-- ArcLab: SOLA LETTURA (invariante)
USE LabAnalisiDati;
CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
ALTER ROLE db_datareader ADD MEMBER [MAREA\svc_labai];
```

> Non concedere `db_owner` su ArcLab né alcun ruolo di scrittura. Verifica finale: `db_datareader` deve essere l'**unico** ruolo dell'utente su `LabAnalisiDati`.

**3.3 Migrazioni.** La Web app esegue `Database.Migrate()` **automaticamente al primo avvio**, creando le tabelle su `LaboratorioAI`. In alternativa, per far rivedere lo script al DBA prima:

```powershell
dotnet ef migrations script --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web --output migrazioni.sql
```

---

## 4. Pubblicazione degli applicativi

> **Chiarimento sul ruolo del server.** Il server di produzione **non** richiede l'SDK .NET (il compilatore) e **non** compila nulla. La compilazione avviene **una sola volta** sulla macchina di sviluppo, che ha già l'SDK, tramite `dotnet publish`. Il risultato sono due cartelle già eseguibili. Al server serve **solo** il runtime, cioè l'**ASP.NET Core 8 Hosting Bundle** (vedi sezione 2): è il pacchetto standard per eseguire siti .NET su IIS, non è il compilatore.
>
> **Da rifare prima del passaggio su server (agosto 2026).** Dopo la prima
> pubblicazione sono state aggiunte le funzioni di prefatturazione (Tamponi in
> contratto, Non fatturare), l'accreditamento esterno nei certificati, i filtri
> prelevatore/ricevitore e il launcher desktop. Le cartelle di produzione vanno
> quindi **ripubblicate** con i due comandi qui sotto, sulla macchina di sviluppo,
> prima di consegnare le cartelle al sistemista.

Comando (eseguire sulla macchina di sviluppo, **non** sul server):

```powershell
dotnet publish src\LaboratorioAI.Web     -c Release -o D:\LaboratorioAI\Produzione\Web
dotnet publish src\LaboratorioAI.Portale -c Release -o D:\LaboratorioAI\Produzione\Portale
```

Dopo la publish, verificare che nelle due cartelle di produzione **non** resti
alcun `appsettings.Development.json` (rimuoverlo se presente): in produzione la
configurazione arriva da `appsettings.Production.json` e dalle variabili d'ambiente.

**Cosa consegnare al sistemista:** le due cartelle pubblicate qui sopra, da **copiare così come sono** sul server. Nessun altro passo di build. Il codice sorgente (la solution) resta nel repository per la manutenzione e **non** va installato sul server di produzione.

Riassunto dei due componenti .NET, per evitare equivoci:

| Componente | Dove | A cosa serve |
|---|---|---|
| **.NET SDK** (compilatore) | Solo macchina di sviluppo | Costruire la build (`dotnet publish`). Già fatto. |
| **ASP.NET Core Hosting Bundle** (runtime + modulo IIS) | Solo server | Eseguire le due app pubblicate sotto IIS |

---

## 5. Cartelle e modelli documentali

Creare sul server e popolare con i modelli `.docx` definitivi:

```
D:\LaboratorioAI\Modelli\Dvr\      -> DVR_Legionellosi_EmiliaRomagna.docx, DVR_Legionellosi_Marche.docx
D:\LaboratorioAI\Archivio\Dvr\     -> (vuota, archiviazione generati)
D:\LaboratorioAI\Modelli\Piscine\  -> DVR_Piscine_Modello.docx, Regolamento_Piscine_Modello.docx,
                                       Manuale_ER_Modello.docx, Manuale_Marche_Modello.docx
D:\LaboratorioAI\Archivio\Piscine\ -> (vuota)
```

L'account `MAREA\svc_labai` deve avere permessi di **lettura** sui Modelli e **scrittura** sugli Archivi.

---

## 6. Configurazione di produzione (segreti e parametri)

I segreti **non vanno nel codice né nel repository**. Vanno impostati come **variabili d'ambiente di sistema** sul server. I parametri non segreti (percorsi modelli) sono già in `appsettings.Production.json`.

> **Ambiente già impostato:** i `web.config` di entrambe le app fissano già `ASPNETCORE_ENVIRONMENT=Production` e l'hosting in-process. Nessuna modifica necessaria per l'ambiente.

**6.1 Parametri non segreti — già pronti.** Il file **`appsettings.Production.json`** (Web) contiene già i percorsi dei modelli piscine puntati a `D:\LaboratorioAI\Modelli\Piscine`. Va semplicemente presente accanto ad `appsettings.json` nella cartella pubblicata della Web (già incluso nelle pubblicazioni). Le stringhe di connessione in `appsettings.json` puntano già a `192.168.23.1` (`LaboratorioAI` e `LabAnalisiDati` con `ApplicationIntent=ReadOnly`); i percorsi DVR Legionellosi (`Dvr`) sono già su `D:\LaboratorioAI\...`.

**6.2 Segreti — script pronto.** Impostarli con lo script **`deploy_variabili_ambiente.ps1`** (nella radice del progetto): sostituire i valori segnaposto ed eseguirlo **sul server, come amministratore**, poi `iisreset`. Variabili impostate:

| Impostazione | Variabile d'ambiente | Fornita da |
|---|---|---|
| Chiave API Anthropic | `Anthropic__ChiaveApi` | Già in possesso di MARE.A |
| Tenant Entra ID | `EntraId__TenantId` | Dalla registrazione Entra ID (sez. 7) |
| Client/App ID Entra ID | `EntraId__ClientId` | Dalla registrazione Entra ID (sez. 7) |
| Credenziale operatore portale | `Portale__CredenzialeOperatore` | Password scelta da MARE.A (senza, accesso operatore disabilitato) |

> **Accesso di sviluppo:** la sezione `AutenticazioneSviluppo` (login senza credenziali) esiste **solo** in `appsettings.Development.json`, che in produzione non viene caricato (ambiente Production). Verificare che nessun `appsettings.Production.json` la reintroduca.

---

## 7. Entra ID (autenticazione Web)

Nel portale Azure / Entra ID, registrare l'applicazione:

- **Redirect URI (web):** `https://<host-interno-web>/signin-oidc`
- **Front-channel logout URL:** `https://<host-interno-web>/signout-callback-oidc`
- Copiare **Directory (tenant) ID** e **Application (client) ID** nelle variabili `EntraId__TenantId` e `EntraId__ClientId`.

Dominio già previsto: `mare-a.com`.

---

## 8. IIS

Per **ciascuna** delle due app:

1. Creare un **Application Pool** dedicato: **.NET CLR = "No Managed Code"**, Identity = **`MAREA\svc_labai`**.
2. Creare il **sito/applicazione** puntando alla cartella pubblicata (`D:\LaboratorioAI\Produzione\Web` e `...\Portale`).
3. **Segreti:** impostarli con `deploy_variabili_ambiente.ps1` (sez. 6.2), non nel `web.config`. Il `web.config` è già configurato (ambiente Production, hosting in-process, header di sicurezza): nessuna modifica necessaria.
4. **HTTPS:** certificato valido. La Web app forza HSTS fuori sviluppo; il Portale forza HSTS + redirect HTTPS fuori sviluppo.
5. **Portale su internet:** esposizione tramite reverse proxy / Cloudflare. Il portale legge già `X-Forwarded-For`/`X-Forwarded-Proto` (necessario perché la protezione da tentativi ripetuti veda l'IP reale del visitatore).

---

## 9. Verifica post-deploy

- [ ] **Web `/stato`** risponde `Healthy` (`https://<host-web>/stato`).
- [ ] **Portale `/stato`** risponde `Healthy` (`https://<host-portale>/stato`).
- [ ] Login **Web via Entra ID** riuscito; l'utente riceve i permessi corretti.
- [ ] Generazione di un **DVR Legionellosi** e di un **Manuale/Registro piscine** (ER e Marche) con dati reali; i file si aprono correttamente in Word.
- [ ] **Analisi AI** funzionante (conferma che `Anthropic__ChiaveApi` è valida).
- [ ] Portale: **login cliente** (codice contabilità + P.IVA) → vede solo i propri campioni.
- [ ] Portale: **login operatore** (codice `MAREA` + credenziale) → vede tutti i clienti e il dettaglio prove.
- [ ] **Log** scritti in `.\log\` di ciascuna app senza errori critici.

## 10. Controlli di sicurezza (checklist invarianti)

- [ ] `MAREA\svc_labai` ha **solo `db_datareader`** su `LabAnalisiDati` (nessuna scrittura su ArcLab).
- [ ] Stringa ArcLab con `ApplicationIntent=ReadOnly`.
- [ ] Nessun segreto (chiave API, credenziale operatore) nel codice o nel repository.
- [ ] Accesso di sviluppo (`AutenticazioneSviluppo`) **disattivato** in produzione.
- [ ] HTTPS attivo su entrambe le app; portale esposto solo via reverse proxy/Cloudflare.
