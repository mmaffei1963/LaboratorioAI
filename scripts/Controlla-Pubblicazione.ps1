# Controlli prima di pubblicare in PRODUZIONE. Chiamato da Pubblica-*.bat.
# Esce con codice 0 se si puo' procedere, con 1 se NO.
#
#  1) si pubblica SOLO dal PC dell'ufficio (il portatile serve per sviluppare);
#  2) in produzione deve andare codice che e' anche su GitHub: se ci sono modifiche non
#     salvate in git, commit non ancora spediti (push) o commit su GitHub che qui non ci
#     sono (pull), lo dice e chiede una conferma esplicita.

param([string]$Nome = 'LaboratorioAI')

. (Join-Path $PSScriptRoot 'PostazioneComune.ps1')
$radice = Split-Path -Parent $PSScriptRoot

if (-not (Test-PostazioneUfficio)) {
    Write-Host ""
    Write-Host ("NON PUBBLICO: questo e' il " + (Get-NomePostazione) + ", non il PC dell'ufficio.") -ForegroundColor Red
    Write-Host "In produzione si pubblica solo dal PC dell'ufficio (o collegandosi ad esso in Desktop Remoto)."
    Write-Host "Dal portatile: git push, poi sul PC dell'ufficio: git pull e pubblica."
    exit 1
}

$problemi = New-Object System.Collections.Generic.List[string]
$git = Find-Git

if (-not $git) {
    $problemi.Add("git non trovato: non posso controllare che il codice sia su GitHub")
}
else {
    Push-Location $radice
    try {
        $modificati = @(& $git status --porcelain --untracked-files=no 2>$null)
        if ($modificati.Count -gt 0) {
            $problemi.Add("ci sono " + $modificati.Count + " file modificati e non salvati in git (commit)")
        }

        & $git fetch origin 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $problemi.Add("non riesco a contattare GitHub (fetch): non posso controllare la sincronizzazione")
        }
        else {
            $conta = @(& $git rev-list --left-right --count 'HEAD...@{u}' 2>$null)
            if ($LASTEXITCODE -eq 0 -and $conta.Count -gt 0) {
                $numeri = ("" + $conta[0]) -split '\s+'
                $avanti = [int]$numeri[0]
                $dietro = [int]$numeri[1]
                if ($avanti -gt 0) {
                    $problemi.Add("ci sono $avanti commit non ancora spediti a GitHub (git push)")
                }
                if ($dietro -gt 0) {
                    $problemi.Add("su GitHub ci sono $dietro commit che qui non hai (git pull): pubblicheresti codice piu' vecchio")
                }
            }
            else {
                $problemi.Add("il ramo non e' collegato a GitHub (upstream)")
            }
        }
    }
    finally { Pop-Location }
}

if ($problemi.Count -eq 0) {
    Write-Host ""
    Write-Host "Controlli superati: PC dell'ufficio, codice allineato a GitHub." -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "ATTENZIONE prima di pubblicare ${Nome}:" -ForegroundColor Yellow
foreach ($riga in $problemi) { Write-Host "  - $riga" -ForegroundColor Yellow }
Write-Host ""
Write-Host "In produzione dovrebbe andare solo codice che e' anche su GitHub."
$risposta = Read-Host "Per pubblicare comunque scrivi S e premi Invio (qualsiasi altra cosa annulla)"
if ($risposta -eq 'S') { exit 0 }

Write-Host "Pubblicazione annullata: la produzione non e' stata toccata." -ForegroundColor Red
exit 1
