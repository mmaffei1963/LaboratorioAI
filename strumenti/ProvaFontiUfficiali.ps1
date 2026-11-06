# Prova dei canali ufficiali per programmi (17/10/2026): Normattiva Open Data API e Cellar (EUR-Lex).
# Solo lettura: scarica in C:\Sviluppo\LaboratorioAI\obj\prova-fonti e stampa un riepilogo.
$ErrorActionPreference = "Continue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$dir = "C:\Sviluppo\LaboratorioAI\obj\prova-fonti"
New-Item -ItemType Directory -Force -Path $dir | Out-Null

function Riepilogo($nome, $file) {
    if (-not (Test-Path $file)) { Write-Host "$nome : nessun file"; return }
    $len = (Get-Item $file).Length
    $testa = (Get-Content $file -TotalCount 3 -Encoding UTF8) -join " "
    if ($testa.Length -gt 160) { $testa = $testa.Substring(0, 160) }
    Write-Host ("{0} : {1:N0} byte | {2}" -f $nome, $len, $testa)
}

# ── 1) Normattiva Open Data ─────────────────────────────────────
$base = "https://api.normattiva.it/t/normattiva.api/bff-opendata/v1/api/v1"
$prove = @(
    @{ n = "normattiva-atto";      urn = "urn:nir:stato:decreto.legislativo:2023-02-23;18" },
    @{ n = "normattiva-allegato1"; urn = "urn:nir:stato:decreto.legislativo:2023-02-23;18:1" }
)
foreach ($p in $prove) {
    foreach ($percorso in @("atto/dettaglio-atto-urn", "atto/dettaglioatto-urn")) {
        $file = Join-Path $dir ("{0}_{1}.json" -f $p.n, ($percorso -replace '[/]', '_'))
        $corpo = [Text.Encoding]::UTF8.GetBytes((@{ urn = $p.urn } | ConvertTo-Json))
        try {
            Invoke-WebRequest -Uri "$base/$percorso" -Method Post -Body $corpo -ContentType "application/json; charset=utf-8" `
                -OutFile $file -UseBasicParsing -TimeoutSec 60
            Riepilogo "$($p.n) ($percorso)" $file
        } catch { Write-Host "$($p.n) ($percorso) : ERRORE $($_.Exception.Message)" }
    }
}

# ── 2) Cellar (archivio dell'Ufficio pubblicazioni UE, testi EUR-Lex) ──
$celex = @("32019R0627", "32005R2073", "02005R2073-20240225")
foreach ($c in $celex) {
    foreach ($fmt in @(@{ e = "xhtml"; a = "application/xhtml+xml" }, @{ e = "pdf"; a = "application/pdf" })) {
        $file = Join-Path $dir ("cellar_{0}.{1}" -f $c, $fmt.e)
        try {
            Invoke-WebRequest -Uri "http://publications.europa.eu/resource/celex/$c" `
                -Headers @{ "Accept" = $fmt.a; "Accept-Language" = "ita" } -OutFile $file -UseBasicParsing -TimeoutSec 90
            Riepilogo "cellar $c $($fmt.e)" $file
        } catch { Write-Host "cellar $c $($fmt.e) : ERRORE $($_.Exception.Message)" }
    }
}
Write-Host ""
Write-Host "File salvati in $dir"
