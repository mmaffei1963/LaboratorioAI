# Agente fonti normative (18/10/2026): pubblica l'agente e crea l'attività pianificata settimanale di Windows.
# Uso (PowerShell, dalla cartella dell'agente):
#   powershell -ExecutionPolicy Bypass -File .\Installa-AttivitaPianificata.ps1
# Rilanciarlo dopo ogni modifica all'agente: ripubblica e sostituisce l'attività.
$ErrorActionPreference = "Stop"
$cartella = Split-Path -Parent $MyInvocation.MyCommand.Path
$pubblicato = Join-Path $cartella "pubblicato"
$nome = "LaboratorioAI - Agente fonti"

Write-Host "1/3 Pubblicazione dell'agente in $pubblicato ..."
dotnet publish "$cartella\AgenteFonti.csproj" -c Release -o $pubblicato --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw "Pubblicazione non riuscita." }
if (-not (Test-Path (Join-Path $pubblicato "appsettings.json"))) { throw "Manca appsettings.json nella cartella pubblicata." }

# L'attività lancia questo file, che scrive l'esito in agente.log (accodato, con data e ora di ogni riga).
$esegui = Join-Path $pubblicato "esegui.cmd"
@"
@echo off
cd /d "%~dp0"
echo ================================================== >> agente.log
AgenteFonti.exe >> agente.log 2>&1
"@ | Set-Content -Path $esegui -Encoding ASCII

Write-Host "2/3 Attività pianificata «$nome»: ogni lunedì alle 7:30 (se il PC è spento, appena si riaccende) ..."
$azione = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c `"$esegui`"" -WorkingDirectory $pubblicato
$quando = New-ScheduledTaskTrigger -Weekly -DaysOfWeek Monday -At 7:30
$impostazioni = New-ScheduledTaskSettingsSet -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 1)
Register-ScheduledTask -TaskName $nome -Action $azione -Trigger $quando -Settings $impostazioni -Description "Scarica le fonti normative (EUR-Lex/Cellar, Normattiva) e le invia a LaboratorioAI" -Force | Out-Null

Write-Host "3/3 Fatto."
Write-Host ""
Write-Host "Per lanciarla subito:   Start-ScheduledTask -TaskName `"$nome`""
Write-Host "Esito delle esecuzioni: $pubblicato\agente.log"
