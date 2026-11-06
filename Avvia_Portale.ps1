# Avvia il Portale clienti (sviluppo) e apre il browser.
# Se il Portale e' gia' in esecuzione, apre solo il browser.
# Funziona su entrambe le postazioni (ufficio e portatile): la cartella del progetto
# e' quella in cui si trova questo file.
# Prima di partire controlla la configurazione (scripts\Controlla-Ambiente.ps1):
# se l'app puntasse ai database di PRODUZIONE, NON parte.

$url      = 'http://localhost:5081'
$progetto = $PSScriptRoot

function ServerSu {
    try { Invoke-WebRequest -UseBasicParsing "$url/stato" -TimeoutSec 2 | Out-Null; return $true }
    catch { return $false }
}

if (-not (ServerSu)) {
    & (Join-Path $progetto 'scripts\Controlla-Ambiente.ps1') -Progetto Portale
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Read-Host "Premi Invio per chiudere"
        exit 1
    }

    Write-Host "Avvio del Portale..."
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    # il Portale gira in una finestra dedicata: chiuderla (o Ctrl+C) lo ferma.
    Start-Process cmd -ArgumentList '/k', "cd /d `"$progetto`" && dotnet run --project src\LaboratorioAI.Portale"

    for ($i = 0; $i -lt 90; $i++) {
        if (ServerSu) { break }
        Start-Sleep -Seconds 1
    }
}

Start-Process $url
