# Prima compilazione — istruzioni passo passo

Guida per installare l'ambiente e compilare la soluzione per la prima volta.

Serve solo una macchina Windows con connessione a internet. Non occorre ancora
il server, né SQL Server, né la configurazione Entra ID: quelli servono per
*eseguire* l'applicazione, non per compilarla.

**Tempo previsto:** 15 minuti, di cui 10 di attesa per l'installazione.

---

## Parte 1 — Installare gli strumenti

### 1.1 Verificare se .NET è già presente

Apri **PowerShell**: tasto Windows, scrivi `powershell`, premi Invio.

Digita:

```powershell
dotnet --version
```

**Se compare un numero che inizia con 8** (esempio `8.0.404`), salta alla Parte 2.

**Se compare un errore** o un numero che inizia con 6 o 7, prosegui.

### 1.2 Scaricare .NET 8 SDK

Vai su:

```
https://dotnet.microsoft.com/download/dotnet/8.0
```

Nella colonna **SDK**, riga **Windows**, clicca su **x64**.

> Attenzione: la pagina propone anche il *Runtime*. Non è quello: il Runtime
> serve solo a far girare i programmi, l'**SDK** serve a compilarli.

### 1.3 Installare

Esegui il file scaricato (`dotnet-sdk-8.x.xxx-win-x64.exe`), accetta le
condizioni e attendi. L'installazione richiede qualche minuto.

### 1.4 Verificare

**Chiudi PowerShell e riaprilo** — è necessario perché legga i percorsi
aggiornati dall'installazione.

```powershell
dotnet --version
```

Deve comparire un numero che inizia con `8`.

---

## Parte 2 — Preparare i file

### 2.1 Scompattare

Crea una cartella di lavoro, per esempio `C:\Sviluppo`, e scompattaci dentro
`LaboratorioAI_Completo.zip`.

Il risultato deve essere:

```
C:\Sviluppo\LaboratorioAI\
├── LaboratorioAI.sln
├── README.md
├── CHANGELOG.md
├── docs\
└── src\
    ├── LaboratorioAI.Core\
    ├── LaboratorioAI.Dati\
    ├── LaboratorioAI.Documenti\
    ├── LaboratorioAI.IA\
    ├── LaboratorioAI.Portale\
    └── LaboratorioAI.Web\
```

Se dentro `LaboratorioAI` trovi un'altra cartella `LaboratorioAI`, sposta il
contenuto di un livello: il file `LaboratorioAI.sln` deve stare nella cartella
principale.

### 2.2 Posizionarsi

In PowerShell:

```powershell
cd C:\Sviluppo\LaboratorioAI
```

Verifica di essere nel posto giusto:

```powershell
dir *.sln
```

Deve comparire `LaboratorioAI.sln`.

---

## Parte 3 — Scaricare le dipendenze

La soluzione usa librerie esterne — Entity Framework, Dapper, ClosedXML e
altre — che vanno scaricate la prima volta.

```powershell
dotnet restore
```

Richiede uno o due minuti. Al termine deve comparire qualcosa come:

```
Determining projects to restore...
Restored C:\Sviluppo\LaboratorioAI\src\LaboratorioAI.Core\...
```

**Se compaiono errori di rete**, il computer è probabilmente dietro un proxy
aziendale che blocca `nuget.org`. In quel caso serve l'intervento di chi
gestisce la rete, oppure una macchina con accesso libero.

---

## Parte 4 — Compilare

```powershell
dotnet build
```

Questo è il momento della verifica. Il comando gira per mezzo minuto e produce
uno dei due esiti seguenti.

### Esito A — compilazione riuscita

```
Compilazione completata.
    0 Avvisi
    0 Errori
```

Improbabile al primo tentativo con settemila righe mai compilate, ma possibile.

### Esito B — errori (previsto)

```
src\LaboratorioAI.Web\Controllers\DvrController.cs(45,17): error CS0246:
Impossibile trovare il tipo o il nome dello spazio dei nomi 'IServizioLottiDvr'
```

Ogni riga dice:

| Parte | Significato |
|---|---|
| `src\...\DvrController.cs` | il file |
| `(45,17)` | riga 45, colonna 17 |
| `CS0246` | codice dell'errore |
| il testo | cosa non torna |

Non allarmarti dal numero: **gli errori si moltiplicano a cascata**. Un solo
nome sbagliato in una classe può generarne venti in tutti i file che la usano,
e correggendo l'originale spariscono tutti insieme.

---

## Parte 5 — Riportare gli errori

Salva l'esito in un file:

```powershell
dotnet build 2>&1 | Out-File -Encoding utf8 errori.txt
```

Il file `errori.txt` si trova nella cartella della soluzione. Aprilo con Blocco
note e incollami il contenuto.

Se è molto lungo, bastano le prime venti righe che contengono `error`:

```powershell
dotnet build 2>&1 | Select-String "error" | Select-Object -First 20
```

---

## Dopo la compilazione

Una volta che la soluzione compila, per **eseguirla** servono ancora due cose:

**Un database.** L'applicazione crea da sé le tabelle al primo avvio, ma il
database vuoto deve esistere e l'utente deve avervi accesso. In sviluppo si può
usare LocalDB, incluso in Visual Studio, senza toccare il server aziendale.

**L'autenticazione.** L'accesso passa da Entra ID, che va registrato sul tenant
M365 — richiede permessi di amministratore. Per provare l'applicazione prima di
quella configurazione si può predisporre un accesso di sviluppo, attivo solo in
locale e mai in produzione.

Affronteremo entrambe le cose quando la compilazione sarà pulita.

---

## Se preferisci Visual Studio

Visual Studio 2022 Community è gratuito e include già l'SDK, l'editor e
LocalDB. È la strada più comoda se poi vorrai leggere e modificare il codice.

```
https://visualstudio.microsoft.com/it/vs/community/
```

Durante l'installazione seleziona il carico di lavoro **Sviluppo ASP.NET e Web**.

Poi apri `LaboratorioAI.sln` con doppio clic e compila con **Ctrl+Maiusc+B**.
Gli errori compaiono nella finestra *Elenco errori*, con doppio clic che porta
direttamente alla riga.

L'installazione richiede però 10–15 GB e una mezz'ora. Se l'obiettivo immediato
è solo vedere se il codice compila, il solo SDK è più rapido.

---

*MARE.A srl — Cattolica (RN)*
