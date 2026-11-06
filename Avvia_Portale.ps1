# Avvia il Portale clienti (sviluppo) e apre il browser, come Avvia_LaboratorioAI.
# Se il Portale è già in esecuzione, apre solo il browser.

$url      = 'http://localhost:5081'
$progetto = 'C:\Sviluppo\LaboratorioAI'

function ServerSu {
    try { Invoke-WebRequest -UseBasicParsing "$url/stato" -TimeoutSec 2 | Out-Null; return $true }
    catch { return $false }
}

if (-not (ServerSu)) {
    Write-Host "Avvio del Portale..."
    # Il Portale gira in una finestra dedicata: chiuderla (o Ctrl+C) lo ferma.
    Start-Process cmd -ArgumentList '/k', "cd /d `"$progetto`" && dotnet run --project src\LaboratorioAI.Portale"

    for ($i = 0; $i -lt 90; $i++) {
        if (ServerSu) { break }
        Start-Sleep -Seconds 1
    }
}

Start-Process $url
