# INIZIO LAVORO: da lanciare appena ti siedi a un PC (ufficio o portatile).
# Scarica da GitHub il lavoro fatto sull'altro PC, avvisa se qualcosa non va e, se sono
# arrivate nuove migrazioni del database, propone di applicarle al database LOCALE.
# Non cancella e non sovrascrive mai nulla: in caso di dubbio si ferma e lo dice.

. (Join-Path $PSScriptRoot 'PostazioneComune.ps1')
$radice = Split-Path -Parent $PSScriptRoot
Set-Location $radice
$git = Find-Git

function Esci-Con-Errore([string]$testo) {
    Write-Host ""
    Write-Host $testo -ForegroundColor Red
    exit 1
}

function Get-AvantiDietro {
    $conta = @(& $git rev-list --left-right --count 'HEAD...@{u}')
    if ($LASTEXITCODE -ne 0 -or $conta.Count -eq 0) { return $null }
    $n = ("" + $conta[0]) -split '\s+'
    return @([int]$n[0], [int]$n[1])
}

Write-Host ""
Write-Host ("=== INIZIO LAVORO su " + (Get-NomePostazione) + " ===") -ForegroundColor Cyan

if (-not $git) { Esci-Con-Errore "git non trovato: installalo o apri il terminale di Visual Studio." }

$ramo = ("" + (& $git rev-parse --abbrev-ref HEAD)).Trim()
if ($ramo -ne 'main') {
    Write-Host "ATTENZIONE: sei sul ramo '$ramo', non su 'main'." -ForegroundColor Yellow
}

Write-Host "Controllo GitHub..."
& $git fetch origin
if ($LASTEXITCODE -ne 0) { Esci-Con-Errore "Non riesco a contattare GitHub (rete, VPN o credenziali?). Non ho scaricato niente." }

$ad = Get-AvantiDietro
if ($null -eq $ad) { Esci-Con-Errore "Il ramo '$ramo' non e' collegato a GitHub. Chiedi aiuto a Claude." }
$avanti = $ad[0]
$dietro = $ad[1]
$modificati = @(& $git status --porcelain --untracked-files=no)

if ($avanti -gt 0 -and $dietro -gt 0) {
    Esci-Con-Errore ("DIVERGENTE: qui hai $avanti commit non spediti e su GitHub ce ne sono $dietro nuovi.`n" +
        "NON forzare niente. Incolla a Claude il risultato di:  git log --oneline --graph -10 --all")
}

$cambiati = @()
if ($dietro -gt 0) {
    if ($modificati.Count -gt 0) {
        Esci-Con-Errore ("Su GitHub ci sono $dietro commit nuovi, ma qui hai " + $modificati.Count + " file modificati e non salvati.`n" +
            "Non scarico per non creare conflitti. Lancia prima FINE-LAVORO (o annulla le modifiche) e poi rifai INIZIO-LAVORO.")
    }
    $prima = ("" + (& $git rev-parse HEAD)).Trim()
    Write-Host "Scarico $dietro commit nuovi da GitHub..."
    & $git pull --ff-only
    if ($LASTEXITCODE -ne 0) { Esci-Con-Errore "Il pull non e' riuscito. Non e' stato cambiato nulla di tuo. Incolla il messaggio a Claude." }
    $cambiati = @(& $git diff --name-only $prima HEAD)
    Write-Host ("Scaricati: " + $cambiati.Count + " file cambiati.") -ForegroundColor Green
}
elseif ($avanti -gt 0) {
    Write-Host "Hai $avanti commit non ancora spediti a GitHub: ricordati di lanciare FINE-LAVORO." -ForegroundColor Yellow
}
else {
    Write-Host "Sei gia' allineato a GitHub: niente da scaricare." -ForegroundColor Green
}

if ($modificati.Count -gt 0 -and $dietro -eq 0) {
    Write-Host ("Hai " + $modificati.Count + " file modificati e non ancora salvati in git (lavoro di prima?):") -ForegroundColor Yellow
    foreach ($m in ($modificati | Select-Object -First 10)) { Write-Host "  $m" }
}

# Nuove migrazioni arrivate con il pull?
$migrLab = @($cambiati | Where-Object { $_ -match '^src/LaboratorioAI\.Dati/Migrations/' })
$migrTrac = @($cambiati | Where-Object { $_ -match '^src/LaboratorioAI\.Tracciabilita\.Dati/Migrations/' })

if ($migrLab.Count -gt 0) {
    Write-Host ""
    Write-Host "Sono arrivate nuove migrazioni del database di LaboratorioAI." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'Controlla-Ambiente.ps1') -Progetto Web
    if ($LASTEXITCODE -ne 0) {
        Write-Host "NON aggiorno il database: la configurazione non e' sicura (vedi sopra)." -ForegroundColor Red
    }
    else {
        $r = Read-Host "Aggiorno ora il database LOCALE di sviluppo? Scrivi S e premi Invio (altro = no)"
        if ($r -eq 'S') {
            $env:ASPNETCORE_ENVIRONMENT = 'Development'
            & dotnet ef database update --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Aggiornamento non riuscito. Se manca lo strumento: dotnet tool install --global dotnet-ef" -ForegroundColor Red
            }
            else {
                Write-Host "Database locale aggiornato." -ForegroundColor Green
            }
        }
        else {
            Write-Host "Database NON aggiornato: ricordati di farlo prima di provare l'app." -ForegroundColor Yellow
        }
    }
}
if ($migrTrac.Count -gt 0) {
    Write-Host ""
    Write-Host "Sono arrivate nuove migrazioni del database di Tracciato: chiedi a Claude il comando per applicarle." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Ultimi salvataggi:"
& $git log --oneline -3
Write-Host ""
& (Join-Path $PSScriptRoot 'Controlla-Ambiente.ps1') -Progetto Web
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host ""
Write-Host "Puoi iniziare a lavorare." -ForegroundColor Green
exit 0
