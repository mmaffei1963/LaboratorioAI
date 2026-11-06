# Controlli di sicurezza prima di avviare un'app in SVILUPPO.
# Chiamato da Avvia_LaboratorioAI.ps1, Avvia_Portale.ps1 e Avvia_Tracciato.ps1.
# Esce con codice 0 se si puo' partire, con 1 se NO (e dice perche').
#
#  1) deve esistere appsettings.Development.json (non e' in git: va copiato dall'altra postazione);
#  2) i database principali non devono essere quelli di PRODUZIONE (192.168.23.1 / SRVMAREA):
#     se il file manca o e' sbagliato, l'app userebbe la configurazione di produzione e
#     all'avvio applicherebbe migrazioni e popolamento sui dati veri;
#  3) sul portatile: cartella Econorma locale e controllo ACCREDIA notturno spento (se non
#     sono gia' impostati a mano) e avviso se c'e' una password SMTP (i database di test
#     contengono e-mail di clienti VERI).

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Web', 'Portale', 'Tracciato')]
    [string]$Progetto
)

. (Join-Path $PSScriptRoot 'PostazioneComune.ps1')

$radice = Split-Path -Parent $PSScriptRoot
$cartella = switch ($Progetto) {
    'Web'       { 'src\LaboratorioAI.Web' }
    'Portale'   { 'src\LaboratorioAI.Portale' }
    'Tracciato' { 'src\LaboratorioAI.Tracciabilita.Web' }
}
$file    = Join-Path $radice "$cartella\appsettings.Development.json"
$ufficio = Test-PostazioneUfficio
$errori  = New-Object System.Collections.Generic.List[string]
$avvisi  = New-Object System.Collections.Generic.List[string]
$elenco  = @()

if (-not (Test-Path -LiteralPath $file)) {
    Write-Host ""
    Write-Host "NON AVVIO: manca il file $file" -ForegroundColor Red
    Write-Host "Senza quel file l'app userebbe la configurazione di PRODUZIONE." -ForegroundColor Red
    Write-Host "Copialo dall'altra postazione (non e' in git, vedi LAVORARE_DA_DUE_POSTAZIONI.md)"
    Write-Host "e fai puntare i database a quelli locali (.\SQLEXPRESS)."
    exit 1
}

try {
    $j = Get-Content -LiteralPath $file -Raw | ConvertFrom-Json
}
catch {
    Write-Host ""
    Write-Host "NON AVVIO: $file non e' un JSON valido." -ForegroundColor Red
    exit 1
}

$produzione = '192\.168\.23\.1|SRVMAREA'
$principali = @('LaboratorioAI', 'ArcLab', 'Tracciabilita')

if ($j.ConnectionStrings) {
    foreach ($c in $j.ConnectionStrings.PSObject.Properties) {
        if ($c.Value -isnot [string]) { continue }
        if ($c.Name.StartsWith('_')) { continue }
        $parti = $c.Value -split ';'
        $srv = $parti | Where-Object { $_ -match '^\s*(Server|Data Source)\s*=' } | Select-Object -First 1
        $db  = $parti | Where-Object { $_ -match '^\s*(Database|Initial Catalog)\s*=' } | Select-Object -First 1
        $srv = "$srv" -replace '^[^=]*=\s*', ""
        $db  = "$db" -replace '^[^=]*=\s*', ""
        $elenco += ("{0}: {1} / {2}" -f $c.Name, $srv, $db)
        if ($srv -match $produzione) {
            if ($principali -contains $c.Name) {
                $errori.Add("$($c.Name) punta alla PRODUZIONE ($srv / $db)")
            }
            else {
                $avvisi.Add("$($c.Name) punta al server di produzione ($srv / $db)")
            }
        }
    }
}

if (-not $ufficio) {
    if ($Progetto -eq 'Web') {
        if (-not $env:TempLog__CartellaScarico) {
            if (Test-Path 'D:\') { $scarico = 'D:\LaboratorioAI\TempLogScarico' }
            else { $scarico = Join-Path $env:LOCALAPPDATA 'LaboratorioAI\TempLogScarico' }
            New-Item -ItemType Directory -Force -Path $scarico | Out-Null
            $env:TempLog__CartellaScarico = $scarico
        }
        if (-not $env:AccreditamentiEsterni__ControlloAutomaticoNotturnoAttivo) {
            $env:AccreditamentiEsterni__ControlloAutomaticoNotturnoAttivo = 'false'
        }
    }
    $smtp = [bool]($env:Smtp__Password -or $env:Posta__Password)
    try {
        $segreti = @(& dotnet user-secrets list --project (Join-Path $radice $cartella) 2>$null)
        if (@($segreti | Where-Object { $_ -match '^(Smtp|Posta):Password\s*=' }).Count -gt 0) { $smtp = $true }
    }
    catch { }
    if ($smtp) {
        $avvisi.Add("sul portatile c'e' una password SMTP: i database di test hanno e-mail di clienti VERI e un test potrebbe spedirne di vere")
    }
}

Write-Host ""
Write-Host ("Postazione: " + (Get-NomePostazione)) -ForegroundColor Cyan
foreach ($riga in $elenco) { Write-Host "  $riga" }
foreach ($avviso in $avvisi) { Write-Host "ATTENZIONE: $avviso" -ForegroundColor Yellow }

if ($errori.Count -gt 0) {
    foreach ($errore in $errori) { Write-Host "NON AVVIO: $errore" -ForegroundColor Red }
    Write-Host "Correggi ${file}: i database principali devono essere locali (.\SQLEXPRESS)." -ForegroundColor Red
    exit 1
}

Write-Host "Controlli superati." -ForegroundColor Green
exit 0
