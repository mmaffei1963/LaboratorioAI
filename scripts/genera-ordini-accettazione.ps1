<#
    genera-ordini-accettazione.ps1
    ------------------------------------------------------------------
    Registra come "emessi" gli Ordini di Accettazione dei lotti ricevuti
    in un intervallo di date, richiamando l'endpoint di LaboratorioAI.
    La guardia anti-doppione lato server evita di ri-emettere lotti già emessi.

    Pensato per Windows Task Scheduler (esecuzione schedulata). La pubblicazione
    del PDF sul portale Palomar e l'invio mail Brevo restano a valle (assistenza Palomar).

    ATTENZIONE (produzione): l'app è protetta da Entra ID. Una chiamata schedulata
    non interattiva richiede un metodo di autenticazione dedicato configurato in fase
    di deploy (uno tra: Windows Integrated Authentication sull'endpoint, oppure una
    API key/segreto). Finché non è configurato, questo script va eseguito con un
    contesto autenticato. Vedi docs/Piano_OrdineAccettazione.md.

    Esempi:
      # Ultimi 2 giorni (default), ambiente di produzione
      powershell -ExecutionPolicy Bypass -File genera-ordini-accettazione.ps1 -BaseUrl "https://labai.marea.local"

      # Intervallo esplicito
      powershell -ExecutionPolicy Bypass -File genera-ordini-accettazione.ps1 -BaseUrl "http://localhost:5080" -Dal 2026-09-01 -Al 2026-09-15
#>

param(
    [string]$BaseUrl = "http://localhost:5080",
    [string]$Dal = "",
    [string]$Al  = "",
    [int]$GiorniIndietro = 0
)

# Uso previsto: run NOTTURNO (es. 22:00 o 23:00 via Task Scheduler) che genera le schede
# ricevute OGGI e non ancora inviate. Solo lotti "pronti" (tutti i campioni con parametri);
# i non pronti sono esclusi e riproposti al run successivo. Guardia anti-doppione lato server.

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Dal)) { $Dal = (Get-Date).AddDays(-$GiorniIndietro).ToString("yyyy-MM-dd") }
if ([string]::IsNullOrWhiteSpace($Al))  { $Al  = (Get-Date).ToString("yyyy-MM-dd") }

$uri = "$BaseUrl/api/ordini-accettazione/genera-schedulati?dal=$Dal&al=$Al"
Write-Host "[$(Get-Date -Format s)] POST $uri"

try {
    # -UseDefaultCredentials: usa le credenziali Windows correnti se l'endpoint le accetta.
    $resp = Invoke-RestMethod -Method Post -Uri $uri -UseDefaultCredentials -TimeoutSec 300
    Write-Host "[$(Get-Date -Format s)] OK — nuovi:$($resp.nuovi) giàEmessi:$($resp.giaEmessi) errori:$($resp.errori) totale:$($resp.totale)"
    exit 0
}
catch {
    Write-Error "[$(Get-Date -Format s)] Errore: $($_.Exception.Message)"
    exit 1
}
