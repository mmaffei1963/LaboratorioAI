# Avvia Tracciato (sviluppo) e apre il browser, come Avvia_Portale.
# Se il sito e' gia' in esecuzione, apre solo il browser.

$url      = 'http://localhost:5082'
$progetto = 'C:\Sviluppo\LaboratorioAI'

function ServerSu {
    # /stato risponde 503 se il database non c'e' ancora: il sito e' comunque acceso.
    try { Invoke-WebRequest -UseBasicParsing "$url/" -TimeoutSec 2 | Out-Null; return $true }
    catch { return $false }
}

if (-not (ServerSu)) {
    Write-Host "Avvio di Tracciato..."
    Start-Process cmd -ArgumentList '/k', "cd /d `"$progetto`" && dotnet run --project src\LaboratorioAI.Tracciabilita.Web"

    for ($i = 0; $i -lt 90; $i++) {
        if (ServerSu) { break }
        Start-Sleep -Seconds 1
    }
}

Start-Process $url
