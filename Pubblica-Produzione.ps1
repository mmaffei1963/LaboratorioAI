<#
.SINOPSI
    Automatizza i passi 2-6 della procedura di ripubblicazione in produzione di LaboratorioAI
    (stop IIS via app_offline, backup datato, copia selettiva esclusi i .json, riavvio,
    verifica che il sito risponda davvero e ripristino automatico se non risponde).

    Il passo 1 (dotnet publish) NON è incluso qui: va lanciato prima, separatamente, es.:
        cd C:\Sviluppo\LaboratorioAI
        dotnet publish src\LaboratorioAI.Web -c Release -o C:\Sviluppo\Publish\Web

.DESCRIZIONE
    1. Verifica che la cartella di staging (pubblicazione locale) esista e non sia vuota.
    2. Ferma l'app in produzione rinominando _app_offline.htm -> app_offline.htm.
    3. Crea un backup datato dell'intera cartella di produzione (BUP gg-mm-aa; se esiste già
       per oggi, aggiunge un progressivo).
    4. Copia tutti i file dallo staging alla produzione, ESCLUDENDO i .json (che contengono
       la configurazione locale/di produzione e non vanno mai sovrascritti).
    5. Riavvia l'app rinominando app_offline.htm -> _app_offline.htm.
    6. VERIFICA che il sito risponda davvero (richieste di prova a -UrlVerifica, per un
       massimo di -TentativiVerifica tentativi ogni -IntervalloVerificaSecondi secondi:
       di default fino a ~2 minuti). Se il sito continua a non rispondere allo scadere del
       tempo, lo script RIPRISTINA AUTOMATICAMENTE il codice in produzione dal backup appena
       creato (i .json NON vengono mai toccati, né qui né al passo 4) e la rimette online, poi
       segnala l'errore: la pubblicazione non resta mai "a metà" con il sito rotto, nel
       peggiore dei casi si torna al codice precedente, e la configurazione locale non viene
       mai persa.

    In caso di errore nei passi 2-4, lo script si ferma e riporta subito l'app online
    (rinominando app_offline.htm -> _app_offline.htm) prima di uscire, così l'applicazione
    non resta mai offline per un errore imprevisto durante la copia.

.PARAMETRO Staging
    Cartella di pubblicazione locale (output di "dotnet publish"). Default: C:\Sviluppo\Publish\Web

.PARAMETRO Produzione
    Cartella di produzione da aggiornare. Default: \\SRVMAREA\marea\LaboratorioAI\Produzione\Web

.PARAMETRO CartellaBackup
    Cartella dove creare il backup datato. Default: la stessa cartella padre della Produzione
    (es. \\SRVMAREA\marea\LaboratorioAI\Produzione\Backup)

.PARAMETRO UrlVerifica
    Indirizzo da interrogare dopo il riavvio per accertarsi che il sito risponda davvero.
    Default: https://labai.mare-a.com/ (per il Portale, passare l'URL del Portale).

.PARAMETRO TentativiVerifica
    Numero massimo di tentativi di verifica dopo il riavvio. Default: 12.

.PARAMETRO IntervalloVerificaSecondi
    Secondi di attesa fra un tentativo di verifica e il successivo. Default: 10
    (12 tentativi x 10 secondi = fino a 2 minuti prima di ripristinare il backup).

.PARAMETRO WhatIf
    Se specificato, mostra solo cosa verrebbe fatto senza eseguire alcuna modifica reale
    (nessun rename, nessuna copia, nessun backup, nessuna verifica/ripristino).

.ESEMPIO
    .\Pubblica-Produzione.ps1
    Esegue la ripubblicazione con i percorsi di default (Web).

.ESEMPIO
    .\Pubblica-Produzione.ps1 -Staging "C:\Sviluppo\Publish\Portale" -Produzione "\\SRVMAREA\marea\LaboratorioAI\Produzione\Portale" -UrlVerifica "https://portale.mare-a.com/"
    Esegue la ripubblicazione per il Portale clienti, verificando l'URL corretto.

.ESEMPIO
    .\Pubblica-Produzione.ps1 -WhatIf
    Simula l'intera procedura senza modificare nulla, per verificare i percorsi prima di lanciarla sul serio.
#>

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Staging = "C:\Sviluppo\Publish\Web",
    [string]$Produzione = "\\SRVMAREA\marea\LaboratorioAI\Produzione\Web",
    [string]$CartellaBackup = "\\SRVMAREA\marea\LaboratorioAI\Produzione\Backup",
    [string]$UrlVerifica = "https://labai.mare-a.com/",
    [int]$TentativiVerifica = 12,
    [int]$IntervalloVerificaSecondi = 10
)

$ErrorActionPreference = "Stop"

function Scrivi-Passo([string]$Testo) {
    Write-Host ""
    Write-Host "==> $Testo" -ForegroundColor Cyan
}

function Assicura-AppOnline {
    # Rete di sicurezza: se qualcosa va storto nei passi 2-4, riporta comunque l'app online
    # (senza toccare i file: la copia potrebbe essere a metà, meglio non peggiorare le cose).
    $offline = Join-Path $Produzione "app_offline.htm"
    $online  = Join-Path $Produzione "_app_offline.htm"
    if (Test-Path $offline) {
        Write-Warning "Ripristino l'app online dopo un errore (app_offline.htm -> _app_offline.htm)..."
        Rename-Item -LiteralPath $offline -NewName "_app_offline.htm" -Force
    }
}

# 13/10/2026: dopo app_offline.htm IIS impiega qualche secondo a chiudere l'app e a liberare le DLL.
# Senza attesa la cancellazione/copia può fallire con «Accesso negato» (successo durante un ripristino reale).
function Attendi-RilascioFile([int]$Secondi = 15) {
    Write-Host "Attendo $Secondi secondi che IIS chiuda l'applicazione e liberi i file..."
    Start-Sleep -Seconds $Secondi
}

function Esegui-ConTentativi([scriptblock]$Azione, [string]$Descrizione, [int]$Tentativi = 10) {
    for ($i = 1; $i -le $Tentativi; $i++) {
        try { & $Azione; return }
        catch {
            if ($i -eq $Tentativi) { throw }
            Write-Host "  $Descrizione non riuscito (tentativo $i/$Tentativi): $($_.Exception.Message). Riprovo fra 5 secondi..." -ForegroundColor Yellow
            Start-Sleep -Seconds 5
        }
    }
}

function Ripristina-DaBackup([string]$PercorsoBackup, [string]$CartellaProduzione) {
    # Usata solo quando, dopo il riavvio, il sito non risponde entro i tentativi previsti:
    # rimette offline, svuota la produzione (lasciando i marcatori app_offline e i .json
    # ATTUALI intatti) e ripristina dal backup solo i file NON .json, poi rimette online.
    #
    # IMPORTANTE (corretto il 29/09/2026, dopo un incidente reale): i .json NON vengono mai
    # toccati anche in questo ripristino di emergenza, esattamente come al passo 4 in caso di
    # successo. Prima venivano ripristinati anche loro dal backup (che li contiene, essendo una
    # copia integrale fatta PRIMA del deploy): se un amministratore aveva modificato a mano
    # appsettings.Production.json poco prima di lanciare una pubblicazione — cosa più comune di
    # quanto sembri, es. per aggiungere una chiave o un segreto — e quella pubblicazione andava
    # in ripristino automatico (es. per una verifica lenta/instabile lato Cloudflare, non
    # necessariamente per un vero problema del deploy), quella modifica spariva silenziosamente
    # insieme al codice, senza che nessuno se ne accorgesse subito. I .json sono config gestita
    # a mano sul server: uno script di pubblicazione non deve mai poterli riscrivere, né in caso
    # di successo né in caso di ripristino.
    Write-Warning "Ripristino la produzione dal backup '$PercorsoBackup' (i file .json attuali restano intatti)..."

    $offline = Join-Path $CartellaProduzione "app_offline.htm"
    $online  = Join-Path $CartellaProduzione "_app_offline.htm"
    if (Test-Path $online) {
        Rename-Item -LiteralPath $online -NewName "app_offline.htm" -Force
    }

    Attendi-RilascioFile 20

    Esegui-ConTentativi -Descrizione "Svuotamento della produzione" -Azione {
        Get-ChildItem -LiteralPath $CartellaProduzione -Force |
            Where-Object { $_.Name -notin @('app_offline.htm', '_app_offline.htm', 'log') -and $_.Name -notlike 'appsettings*.json' } |
            Remove-Item -Recurse -Force
    }

    # Il backup è stato fatto con l'app ferma: contiene app_offline.htm, che non va ricopiato.
    # La cartella log NON si ripristina: conterrebbe la versione vecchia del log e cancellerebbe l'errore appena avvenuto.
    $fileBackup = Get-ChildItem -LiteralPath $PercorsoBackup -File -Recurse |
        Where-Object { $_.Name -notlike 'appsettings*.json' -and $_.Name -notin @('app_offline.htm', '_app_offline.htm') -and $_.FullName.Substring($PercorsoBackup.Length).TrimStart('\') -notlike 'log\*' }
    foreach ($file in $fileBackup) {
        $percorsoRelativo = $file.FullName.Substring($PercorsoBackup.Length).TrimStart('\')
        $destinazione = Join-Path $CartellaProduzione $percorsoRelativo
        $cartellaDestinazione = Split-Path $destinazione -Parent
        if (-not (Test-Path $cartellaDestinazione)) {
            New-Item -ItemType Directory -Path $cartellaDestinazione -Force | Out-Null
        }
        Esegui-ConTentativi -Descrizione "Copia di $percorsoRelativo" -Azione { Copy-Item -LiteralPath $file.FullName -Destination $destinazione -Force }
    }

    $offlineAdesso = Join-Path $CartellaProduzione "app_offline.htm"
    if (Test-Path $offlineAdesso) {
        Rename-Item -LiteralPath $offlineAdesso -NewName "_app_offline.htm" -Force
    }

    Write-Warning "Ripristino completato: codice tornato alla versione precedente al deploy; i .json non sono stati toccati."
}

try {
    # --- Controlli preliminari -------------------------------------------------
    Scrivi-Passo "Controllo cartelle"

    if (-not (Test-Path $Staging)) {
        throw "Cartella di staging non trovata: $Staging. Esegui prima 'dotnet publish'."
    }
    $fileStaging = Get-ChildItem -LiteralPath $Staging -File -Recurse
    if ($fileStaging.Count -eq 0) {
        throw "La cartella di staging è vuota: $Staging. Esegui prima 'dotnet publish'."
    }
    if (-not (Test-Path $Produzione)) {
        throw "Cartella di produzione non trovata: $Produzione. Controlla il percorso o la connessione alla share."
    }

    Write-Host "Staging:     $Staging  ($($fileStaging.Count) file)"
    Write-Host "Produzione:  $Produzione"
    Write-Host "Backup in:   $CartellaBackup"
    Write-Host "Verifica su: $UrlVerifica"

    # --- Passo 2: stop applicazione ---------------------------------------------
    Scrivi-Passo "Passo 2/6: fermo l'applicazione (_app_offline.htm -> app_offline.htm)"

    $percorsoOnline  = Join-Path $Produzione "_app_offline.htm"
    $percorsoOffline = Join-Path $Produzione "app_offline.htm"

    if (Test-Path $percorsoOffline) {
        Write-Host "L'app risulta già offline (app_offline.htm presente): procedo comunque."
    }
    elseif (Test-Path $percorsoOnline) {
        if ($PSCmdlet.ShouldProcess($percorsoOnline, "Rinomina in app_offline.htm (ferma l'app)")) {
            Rename-Item -LiteralPath $percorsoOnline -NewName "app_offline.htm" -Force
        }
    }
    if (-not $WhatIfPreference) { Attendi-RilascioFile 15 }
    else {
        Write-Warning "Non trovo né app_offline.htm né _app_offline.htm in ${Produzione}. Procedo comunque, ma verifica manualmente lo stato dell'app."
    }

    # --- Passo 3: backup datato --------------------------------------------------
    Scrivi-Passo "Passo 3/6: backup della produzione attuale"

    if (-not (Test-Path $CartellaBackup)) {
        if ($PSCmdlet.ShouldProcess($CartellaBackup, "Crea cartella di backup")) {
            New-Item -ItemType Directory -Path $CartellaBackup -Force | Out-Null
        }
    }

    $dataOggi = Get-Date -Format "dd-MM-yy"
    $nomeBackup = "BUP $dataOggi"
    $percorsoBackup = Join-Path $CartellaBackup $nomeBackup
    $progressivo = 1
    while (Test-Path $percorsoBackup) {
        $progressivo++
        $percorsoBackup = Join-Path $CartellaBackup "$nomeBackup ($progressivo)"
    }

    Write-Host "Backup in: $percorsoBackup"
    if ($PSCmdlet.ShouldProcess($Produzione, "Copia in $percorsoBackup")) {
        Copy-Item -LiteralPath $Produzione -Destination $percorsoBackup -Recurse -Force
    }

    # --- Passo 4: copia selettiva (esclusi SOLO gli appsettings*.json) -----------
    # Corretto il 30/09/2026: prima si escludevano TUTTI i .json, compresi quelli generati dalla
    # compilazione (LaboratorioAI.Web.deps.json, .runtimeconfig.json, .staticwebassets.endpoints.json),
    # che in produzione restavano fermi a una versione vecchia. Innocuo finché non si aggiunge un
    # pacchetto NuGet, poi l'app non lo troverebbe all'avvio. Ora si proteggono solo i file di
    # configurazione gestiti a mano (appsettings.json, appsettings.Production.json, appsettings.secrets.json…).
    Scrivi-Passo "Passo 4/6: copio i file dallo staging alla produzione (esclusi gli appsettings*.json)"

    $fileDaCopiare = $fileStaging | Where-Object { $_.Name -notlike "appsettings*.json" }
    $fileEsclusi   = $fileStaging | Where-Object { $_.Name -like "appsettings*.json" }

    Write-Host "File da copiare: $($fileDaCopiare.Count)"
    Write-Host "File appsettings*.json esclusi (config locale/produzione, non toccati): $($fileEsclusi.Count)"
    foreach ($f in $fileEsclusi) { Write-Host "   - $($f.FullName.Substring($Staging.Length).TrimStart('\'))" -ForegroundColor DarkGray }

    foreach ($file in $fileDaCopiare) {
        $percorsoRelativo = $file.FullName.Substring($Staging.Length).TrimStart('\')
        $destinazione = Join-Path $Produzione $percorsoRelativo
        $cartellaDestinazione = Split-Path $destinazione -Parent

        if ($PSCmdlet.ShouldProcess($destinazione, "Copia file")) {
            if (-not (Test-Path $cartellaDestinazione)) {
                New-Item -ItemType Directory -Path $cartellaDestinazione -Force | Out-Null
            }
            Esegui-ConTentativi -Descrizione "Copia di $percorsoRelativo" -Azione { Copy-Item -LiteralPath $file.FullName -Destination $destinazione -Force }
        }
    }

    # --- Passo 5: riavvio applicazione -------------------------------------------
    Scrivi-Passo "Passo 5/6: riavvio l'applicazione (app_offline.htm -> _app_offline.htm)"

    if (Test-Path $percorsoOffline) {
        if ($PSCmdlet.ShouldProcess($percorsoOffline, "Rinomina in _app_offline.htm (riavvia l'app)")) {
            Rename-Item -LiteralPath $percorsoOffline -NewName "_app_offline.htm" -Force
        }
    }
    else {
        Write-Warning "app_offline.htm non trovato in ${Produzione}: l'app potrebbe essere già online, verifica manualmente."
    }

    # --- Passo 6: verifica che il sito risponda davvero, altrimenti ripristino ---
    Scrivi-Passo "Passo 6/6: verifico che il sito risponda ($UrlVerifica)"

    $sitoOk = $false
    if ($PSCmdlet.ShouldProcess($UrlVerifica, "Verifica raggiungibilità dopo il riavvio")) {
        for ($tentativo = 1; $tentativo -le $TentativiVerifica; $tentativo++) {
            try {
                $risposta = Invoke-WebRequest -Uri $UrlVerifica -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
                Write-Host "Tentativo ${tentativo}/${TentativiVerifica}: il sito risponde (stato $($risposta.StatusCode))." -ForegroundColor Green
                $sitoOk = $true
                break
            }
            catch {
                Write-Host "Tentativo ${tentativo}/${TentativiVerifica}: non risponde ancora ($($_.Exception.Message))." -ForegroundColor Yellow
                if ($tentativo -lt $TentativiVerifica) {
                    Start-Sleep -Seconds $IntervalloVerificaSecondi
                }
            }
        }
    }
    else {
        # -WhatIf: non è stato davvero riavviato nulla, non ha senso verificarlo.
        $sitoOk = $true
    }

    if (-not $sitoOk) {
        $minuti = [math]::Round(($TentativiVerifica * $IntervalloVerificaSecondi) / 60, 1)
        Write-Warning "Il sito non ha risposto dopo $TentativiVerifica tentativi (~$minuti minuti)."
        Ripristina-DaBackup -PercorsoBackup $percorsoBackup -CartellaProduzione $Produzione
        throw "PUBBLICAZIONE ANNULLATA: '$UrlVerifica' non rispondeva dopo il riavvio. Produzione ripristinata alla versione precedente da '$percorsoBackup'. Controlla i log dell'app prima di ripubblicare."
    }

    Scrivi-Passo "Completato: produzione aggiornata e verificata raggiungibile, backup salvato in '$percorsoBackup'"
}
catch {
    Write-Host ""
    Write-Error "ERRORE: $($_.Exception.Message)"
    if (-not $WhatIfPreference) {
        Assicura-AppOnline
    }
    exit 1
}
