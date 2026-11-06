# Ripristino manuale della produzione Web dal backup fatto prima della pubblicazione del 13/10/2026.
# Ferma l'app, aspetta che IIS liberi i file, ricopia il backup (MAI gli appsettings*.json), riaccende l'app.
# Poi copia il log di oggi in C:\Sviluppo\LaboratorioAI\obj per capire l'errore 500.
$ErrorActionPreference = "Stop"
$P = "\\SRVMAREA\marea\LaboratorioAI\Produzione\Web"
$B = "\\SRVMAREA\marea\LaboratorioAI\Produzione\Backup\BUP 13-10-26 (2)"

if (-not (Test-Path $B)) { throw "Backup non trovato: $B" }
if (Test-Path "$P\_app_offline.htm") { Rename-Item -LiteralPath "$P\_app_offline.htm" -NewName "app_offline.htm" -Force }
Write-Host "App ferma. Attendo 20 secondi che IIS liberi i file..."
Start-Sleep -Seconds 20

robocopy $B $P /E /XF "appsettings*.json" "app_offline.htm" "_app_offline.htm" /XD "$B\log" /R:10 /W:5 /NP /NFL /NDL
if ($LASTEXITCODE -ge 8) { Write-Warning "Robocopy ha segnalato errori (codice $LASTEXITCODE): l'app resta FERMA. Avvisa Claude."; exit 1 }

Rename-Item -LiteralPath "$P\app_offline.htm" -NewName "_app_offline.htm" -Force
Write-Host "Produzione ripristinata e riaccesa." -ForegroundColor Green

$log = Get-ChildItem "$P\log" -Filter "laboratorioai-*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($log) { Copy-Item $log.FullName "C:\Sviluppo\LaboratorioAI\obj\log-produzione.txt" -Force; Write-Host "Log copiato in C:\Sviluppo\LaboratorioAI\obj\log-produzione.txt" }
else { Write-Warning "Log non trovato in $P\log" }
