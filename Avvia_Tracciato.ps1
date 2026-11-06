# Avvia Tracciato (sviluppo) e apre il browser.
# Se il sito e' gia' in esecuzione, apre solo il browser.
# Funziona su entrambe le postazioni (ufficio e portatile): la cartella del progetto
# e' quella in cui si trova questo file.
# Prima di partire controlla la configurazione (scripts\Controlla-Ambiente.ps1):
# se l'app puntasse ai database di PRODUZIONE, NON parte.

$url      = 'http://localhost:5082'
$progetto = $PSScriptRoot

function ServerSu {
    # /stato risponde 503 se il database non c'e' ancora: il sito e' comunque acceso, per questo si interroga la home.
    try { Invoke-WebRequest -UseBasicParsing "$url/" -TimeoutSec 2 | Out-Null; return $true }
    catch { return $false }
}

if (-not (ServerSu)) {
    & (Join-Path $progetto 'scripts\Controlla-Ambiente.ps1') -Progetto Tracciato
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Read-Host "Premi Invio per chiudere"
        exit 1
    }

    Write-Host "Avvio di Tracciato..."
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    # il sito gira in una finestra dedicata: chiuderla (o Ctrl+C) lo ferma.
    Start-Process cmd -ArgumentList '/k', "cd /d `"$progetto`" && dotnet run --project src\LaboratorioAI.Tracciabilita.Web"

    for ($i = 0; $i -lt 90; $i++) {
        if (ServerSu) { break }
        Start-Sleep -Seconds 1
    }
}

Start-Process $url
