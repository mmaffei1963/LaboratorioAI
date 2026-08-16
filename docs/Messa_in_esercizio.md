# Laboratorio AI — Messa in esercizio

Procedura per la prima installazione su server aziendale. Ambiente **Collaudo**, poi **Produzione**.

---

## 1. Prerequisiti sul server

| Componente | Note |
|---|---|
| Ruolo **Web Server (IIS)** | Con *Application Development* → *.NET Extensibility* |
| **ASP.NET Core 8 Hosting Bundle** | [download](https://dotnet.microsoft.com/download/dotnet/8.0) — sezione *Hosting Bundle*. Installare **dopo** IIS |
| **SQL Server** raggiungibile | Istanza 192.168.23.1 |
| **Git for Windows** | Solo se il server ospita anche il repository |

Dopo l'installazione dell'Hosting Bundle, riavviare IIS:

```powershell
net stop was /y
net start w3svc
```

Verifica che il modulo sia registrato:

```powershell
Get-WebGlobalModule | Where-Object Name -like "*AspNetCore*"
```

Deve comparire `AspNetCoreModuleV2`.

---

## 2. Repository Git

Repository *bare* sulla share dedicata.

```powershell
# Sul server, nella share condivisa
New-Item -ItemType Directory -Path "D:\Sviluppo" -Force
cd D:\Sviluppo
git init --bare LaboratorioAI.git
```

Condivisione con accesso in scrittura ai soli sviluppatori:

```powershell
New-SmbShare -Name "Sviluppo" -Path "D:\Sviluppo" -FullAccess "MAREA\Sviluppatori"
```

> **Importante.** La cartella `D:\Sviluppo` deve essere esclusa da OneDrive e da qualunque sincronizzazione automatica: la replica parziale degli oggetti Git corrompe il repository.

Dalla postazione di sviluppo:

```powershell
git clone \\<server>\Sviluppo\LaboratorioAI.git
```

---

## 3. Registrazione su Entra ID

Serve un account con ruolo *Application Administrator* o *Global Administrator* sul tenant M365.

### 3.1 Creazione dell'applicazione

1. [portal.azure.com](https://portal.azure.com) → **Microsoft Entra ID** → *Registrazioni app* → **Nuova registrazione**
2. Nome: `Laboratorio AI`
3. Tipi di account supportati: **Solo account in questa directory organizzativa**
4. URI di reindirizzamento: tipo **Web**, valore:
   - Collaudo: `https://laboratorio-collaudo.mare-a.local/signin-oidc`
   - Produzione: `https://laboratorio.mare-a.local/signin-oidc`
5. **Registra**

### 3.2 Configurazione

Nella pagina *Autenticazione*:
- URL di disconnessione del front-channel: `https://<host>/signout-callback-oidc`
- Concessione implicita: spuntare **Token ID**

Nella pagina *Autorizzazioni API*:
- `User.Read` (Microsoft Graph) è sufficiente

### 3.3 Valori da riportare nella configurazione

Dalla pagina *Panoramica*:

| Voce del portale | Chiave in `appsettings` |
|---|---|
| ID applicazione (client) | `EntraId:ClientId` |
| ID directory (tenant) | `EntraId:TenantId` |

> Non serve alcun *client secret*: l'applicazione usa il flusso implicito con token ID, senza chiamate a Graph per conto dell'applicazione.

### 3.4 Limitare l'accesso a un gruppo

Facoltativo ma consigliato. Nella pagina *Applicazioni aziendali* → `Laboratorio AI` → *Proprietà*:
- **Assegnazione richiesta**: Sì

Poi in *Utenti e gruppi*, assegnare il gruppo di sicurezza abilitato. Chi non è nel gruppo riceve un rifiuto da Entra ID prima ancora di raggiungere l'applicazione.

---

## 4. Database

L'applicazione crea e aggiorna da sé lo schema all'avvio, tramite migrazioni EF Core. Vanno però creati i database vuoti e concessi i permessi.

```sql
CREATE DATABASE LaboratorioAI;
CREATE DATABASE LaboratorioAI_Test;
```

### 4.1 Account di servizio

L'Application Pool deve girare con un account di dominio: l'identità virtuale predefinita non può autenticarsi su un SQL Server remoto.

1. Creare in Active Directory `MAREA\svc_labai`, password che non scade, nessun logon interattivo
2. Concedere i permessi:

```sql
CREATE LOGIN [MAREA\svc_labai] FROM WINDOWS;

USE LaboratorioAI;
CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
ALTER ROLE db_owner ADD MEMBER [MAREA\svc_labai];   -- necessario per le migrazioni

USE LaboratorioAI_Test;
CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
ALTER ROLE db_owner ADD MEMBER [MAREA\svc_labai];

USE LabAnalisiDati;
CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
ALTER ROLE db_datareader ADD MEMBER [MAREA\svc_labai];   -- sola lettura, mai scrittura
```

> Su `LabAnalisiDati` viene concessa **esclusivamente** la lettura. È l'attuazione tecnica della regola invariante di §2.3 dell'analisi: il sistema non scrive mai su ArcLab. Anche un errore di programmazione verrebbe rifiutato dal database.

---

## 5. Pubblicazione

### 5.1 Compilazione

Dalla postazione di sviluppo:

```powershell
cd LaboratorioAI
dotnet publish src\LaboratorioAI.Web\LaboratorioAI.Web.csproj `
        -c Release `
        -o publish\web
```

Copiare il contenuto di `publish\web` in:
- Collaudo: `D:\LaboratorioAI\Collaudo`
- Produzione: `D:\LaboratorioAI\Produzione`

### 5.2 Configurazione dell'ambiente

Creare `appsettings.Production.json` nella cartella di destinazione — **non** nel repository:

```json
{
  "ConnectionStrings": {
    "LaboratorioAI": "Server=192.168.23.1;Database=LaboratorioAI;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;",
    "ArcLab": "Server=192.168.23.1;Database=LabAnalisiDati;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
  },
  "EntraId": {
    "TenantId": "<ID directory>",
    "ClientId": "<ID applicazione>"
  }
}
```

Per il collaudo il file si chiama `appsettings.Staging.json` e punta a `LaboratorioAI_Test`.

Restringere l'accesso al file:

```powershell
icacls "D:\LaboratorioAI\Produzione\appsettings.Production.json" /inheritance:r
icacls "D:\LaboratorioAI\Produzione\appsettings.Production.json" /grant "MAREA\svc_labai:R" "Administrators:F"
```

### 5.3 Application Pool

```powershell
Import-Module WebAdministration

New-WebAppPool -Name "LaboratorioAI"
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name managedRuntimeVersion  -Value ""
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name startMode              -Value "AlwaysRunning"
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name processModel.idleTimeout -Value "00:00:00"
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name recycling.periodicRestart.time -Value "00:00:00"

# Identità di dominio
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name processModel.identityType -Value 3
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name processModel.userName -Value "MAREA\svc_labai"
Set-ItemProperty IIS:\AppPools\LaboratorioAI -Name processModel.password -Value "<password>"
```

Ripetere con nome `LaboratorioAI_Collaudo` per l'ambiente di collaudo.

**Perché queste impostazioni.** `managedRuntimeVersion` vuoto perché non si usa .NET Framework. `AlwaysRunning` fa partire l'applicazione al boot invece che alla prima richiesta. I due timeout azzerati evitano che l'applicazione venga spenta per inattività o riciclata a metà giornata.

### 5.4 Siti

```powershell
New-Website -Name "Laboratorio AI" `
            -PhysicalPath "D:\LaboratorioAI\Produzione" `
            -ApplicationPool "LaboratorioAI" `
            -HostHeader "laboratorio.mare-a.local" `
            -Port 80

New-Website -Name "Laboratorio AI (Collaudo)" `
            -PhysicalPath "D:\LaboratorioAI\Collaudo" `
            -ApplicationPool "LaboratorioAI_Collaudo" `
            -HostHeader "laboratorio-collaudo.mare-a.local" `
            -Port 80
```

L'ambiente si seleziona con la variabile nel `web.config` di ciascuna cartella: `Production` oppure `Staging`.

### 5.5 Permessi sulle cartelle

```powershell
$percorso = "D:\LaboratorioAI\Produzione"
$account  = "MAREA\svc_labai"

icacls $percorso /grant "${account}:(OI)(CI)RX" /T
New-Item -ItemType Directory -Path "$percorso\log" -Force
icacls "$percorso\log" /grant "${account}:(OI)(CI)M"
```

---

## 6. Verifica

```powershell
# Stato dei servizi
Get-IISAppPool | Select-Object Name, State
Get-Website | Select-Object Name, State

# Sonda di stato — non richiede autenticazione
Invoke-WebRequest "http://localhost/stato" -Headers @{Host="laboratorio.mare-a.local"} -UseBasicParsing
```

La sonda risponde `Healthy` se l'applicazione è avviata e il database raggiungibile.

Al primo avvio l'applicazione:
1. applica le migrazioni creando le tabelle nei quattro schemi `sec`, `app`, `portale`, `log`
2. popola ruoli e permessi
3. registra l'esito nel log

Verificare in `D:\LaboratorioAI\Produzione\log\laboratorioai-<data>.log` la riga *«Database allineato e popolamento iniziale completato»*.

Aprendo il sito da browser si viene reindirizzati all'accesso Microsoft. Al primo accesso l'utente viene creato automaticamente e assegnato al ruolo Amministratore.

---

## 7. Rilascio di una nuova versione

```powershell
# 1. Backup del database
sqlcmd -S 192.168.23.1 -Q "BACKUP DATABASE LaboratorioAI TO DISK='...' WITH INIT"

# 2. Sospensione del sito
New-Item "D:\LaboratorioAI\Produzione\app_offline.htm" -Value "Aggiornamento in corso"

# 3. Copia dei file (appsettings.Production.json non va sovrascritto)
robocopy publish\web D:\LaboratorioAI\Produzione /E /XF appsettings.Production.json web.config

# 4. Riattivazione
Remove-Item "D:\LaboratorioAI\Produzione\app_offline.htm"
```

Le migrazioni dello schema vengono applicate automaticamente all'avvio. Non serve intervenire sul database a mano.

---

## 8. Problemi frequenti

| Sintomo | Causa |
|---|---|
| **HTTP 500.30** — avvio non riuscito | Leggere `log\laboratorioai-*.log`. Quasi sempre: connection string errata o database non raggiungibile |
| **HTTP 500.19** | Hosting Bundle non installato, o installato prima di IIS. Reinstallarlo |
| **Login failed for user 'NT AUTHORITY\ANONYMOUS LOGON'** | L'Application Pool gira con identità locale. Rivedere §5.3 |
| **Reindirizzamento a Microsoft che torna con errore AADSTS50011** | L'URI di reindirizzamento su Entra ID non coincide con l'indirizzo del sito |
| **L'utente accede ma non vede le voci di menu** | Popolamento dei permessi non eseguito: verificare il log di avvio |
| **Modifica ai permessi senza effetto** | I permessi sono nei claim del cookie: hanno effetto al successivo accesso |

---

*MARE.A srl — Cattolica (RN)*
