<#
    pubblica_web.ps1 — Pubblica la Web in Release e la copia sul server (config esclusa).

    USO (dalla root del repo, PC di sviluppo con SDK .NET 8):
        .\pubblica_web.ps1                      # publish + copia il pacchetto in $Destinazione
        .\pubblica_web.ps1 -SoloPublish         # solo publish (nessuna copia)
        .\pubblica_web.ps1 -Destinazione '\\SERVER\D$\LaboratorioAI\Produzione\Web'

    Cosa fa:
      1. dotnet publish del solo progetto Web in Release -> cartella locale .\publish\Web
      2. mette l'app offline, copia sul server TUTTO il pacchetto pubblicato
         (DLL + dipendenze + wwwroot), poi rimette l'app online.

    NB IMPORTANTE: la copia ESCLUDE sempre i file di configurazione del server, che
        NON vengono mai toccati:
        - appsettings.json, appsettings.Production.json, appsettings.Development.json
        - appsettings.secrets.json  (chiave Anthropic e segreti)
        - web.config
        - app_offline.htm / _app_offline.htm
    Copiando tutto il resto in blocco non si rischia di dimenticare una DLL cambiata.
#>

param(
    [string]$Destinazione = '\\SRVMAREA\marea\LaboratorioAI\Produzione\Web',
    [switch]$SoloPublish
)

$ErrorActionPreference = 'Stop'

# --- Protezione aggiunta il 06/11/2026 ---
# Script superato da Pubblica-Produzione.bat (che fa anche backup, verifica e ripristino automatico).
# In produzione si pubblica solo dal PC dell'ufficio.
. (Join-Path $PSScriptRoot 'scripts\PostazioneComune.ps1')
if (-not (Test-PostazioneUfficio)) { throw "Si pubblica solo dal PC dell'ufficio (questo e' il $(Get-NomePostazione))." }
Write-Host "NOTA: questo script e' superato: meglio Pubblica-Produzione.bat (backup, verifica e ripristino automatico)." -ForegroundColor Yellow
$root   = Split-Path -Parent $MyInvocation.MyCommand.Path
$outDir = Join-Path $root 'publish\Web'

Write-Host "== 1/5  Publish Release ==" -ForegroundColor Cyan
dotnet publish (Join-Path $root 'src\LaboratorioAI.Web\LaboratorioAI.Web.csproj') `
    -c Release -o $outDir
if ($LASTEXITCODE -ne 0) { throw "Publish fallita (exit $LASTEXITCODE)." }
Write-Host "   OK -> $outDir" -ForegroundColor Green

# File di configurazione del server da NON sovrascrivere mai
$fileEsclusi = @(
    'appsettings.json',
    'appsettings.Production.json',
    'appsettings.Development.json',
    'appsettings.secrets.json',
    'web.config',
    'app_offline.htm',
    '_app_offline.htm'
)

Write-Host "`n== 2/5  Verifica pacchetto pubblicato ==" -ForegroundColor Cyan
if (-not (Test-Path (Join-Path $outDir 'LaboratorioAI.Web.dll'))) {
    throw "Publish incompleta: manca LaboratorioAI.Web.dll in $outDir"
}
Write-Host ("   {0} file pronti in {1}" -f (Get-ChildItem $outDir -Recurse -File).Count, $outDir) -ForegroundColor Green

if ($SoloPublish) {
    Write-Host "`n-SoloPublish: nessuna copia. Copia manualmente il contenuto di $outDir in $Destinazione (esclusi i file di config)." -ForegroundColor Yellow
    return
}

Write-Host "`n== 3/5  Metti l'app OFFLINE (app_offline.htm) ==" -ForegroundColor Cyan
if (-not (Test-Path $Destinazione)) { throw "Destinazione non raggiungibile: $Destinazione" }
$offlineAttivo   = Join-Path $Destinazione 'app_offline.htm'    # nome che IIS riconosce -> app offline
$offlineDisatt   = Join-Path $Destinazione '_app_offline.htm'   # nome "parcheggiato" -> app online
if (Test-Path $offlineDisatt) {
    Rename-Item $offlineDisatt 'app_offline.htm' -Force
    Write-Host "   _app_offline.htm -> app_offline.htm (app offline)" -ForegroundColor Green
} elseif (Test-Path $offlineAttivo) {
    Write-Host "   app_offline.htm gia' presente: app gia' offline" -ForegroundColor Yellow
} else {
    throw "Non trovo ne' _app_offline.htm ne' app_offline.htm in $Destinazione. Crea prima il file _app_offline.htm sul server."
}

Write-Host "`n== 4/5  Attendo 10s l'arresto dell'app pool ==" -ForegroundColor Cyan
Start-Sleep -Seconds 10

Write-Host "`n== Copia pacchetto sul server (config esclusa) ==" -ForegroundColor Cyan
try {
    # robocopy /E copia tutto (sottocartelle incluse) SENZA cancellare gli extra sul server;
    # /XF esclude i file di configurazione; /XO salta i file gia' identici o piu' recenti.
    robocopy $outDir $Destinazione /E /XO /R:2 /W:2 /NFL /NDL /NP /XF $fileEsclusi | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy ha restituito codice $LASTEXITCODE (errore di copia)." }
    Write-Host "   pacchetto copiato in $Destinazione (esclusi: $($fileEsclusi -join ', '))" -ForegroundColor Green
    $global:LASTEXITCODE = 0
}
finally {
    Write-Host "`n== 5/5  Rimetti l'app ONLINE ==" -ForegroundColor Cyan
    if (Test-Path $offlineAttivo) {
        Rename-Item $offlineAttivo '_app_offline.htm' -Force
        Write-Host "   app_offline.htm -> _app_offline.htm (app online)" -ForegroundColor Green
    }
}

Write-Host "`nFatto. Riprova la generazione PDF dell'OA sul portale." -ForegroundColor Cyan
