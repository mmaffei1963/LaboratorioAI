# Avvia LaboratorioAI (sviluppo) e apre il browser.
# Se il server è già in esecuzione, apre solo il browser.

$url      = 'http://localhost:5080'
$progetto = 'C:\Sviluppo\LaboratorioAI'

function ServerSu {
    try { Invoke-WebRequest -UseBasicParsing "$url/stato" -TimeoutSec 2 | Out-Null; return $true }
    catch { return $false }
}

if (-not (ServerSu)) {
    Write-Host "Avvio del server..."
    # Il server gira in una finestra dedicata: chiuderla (o Ctrl+C) lo ferma.
    Start-Process cmd -ArgumentList '/k', "cd /d `"$progetto`" && dotnet run --project src\LaboratorioAI.Web"

    for ($i = 0; $i -lt 90; $i++) {
        if (ServerSu) { break }
        Start-Sleep -Seconds 1
    }
}

Start-Process $url
