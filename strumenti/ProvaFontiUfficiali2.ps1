# Seconda prova (17/10/2026): allegati di Normattiva e testi consolidati di Cellar. Solo lettura.
$ErrorActionPreference = "Continue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$dir = "C:\Sviluppo\LaboratorioAI\obj\prova-fonti"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$base = "https://api.normattiva.it/t/normattiva.api/bff-opendata/v1/api/v1"

# ── Normattiva: articoli e Allegato I del D.Lgs. 18/2023 ──
$urn = "urn:nir:stato:decreto.legislativo:2023-02-23;18"
$varianti = @("~art1", "~art4", ":1~art1", ":1~art1!vig=2026-10-17", ":1", ":2~art1", ":3~art1")
$i = 0
foreach ($v in $varianti) {
    $i++
    $file = Join-Path $dir ("nt_{0:D2}.json" -f $i)
    $corpo = [Text.Encoding]::UTF8.GetBytes((@{ urn = "$urn$v" } | ConvertTo-Json))
    try {
        Invoke-WebRequest -Uri "$base/atto/dettaglio-atto-urn" -Method Post -Body $corpo -ContentType "application/json; charset=utf-8" -OutFile $file -UseBasicParsing -TimeoutSec 60
        Write-Host ("{0,-28} OK  {1:N0} byte -> {2}" -f $v, (Get-Item $file).Length, (Split-Path $file -Leaf))
    } catch { Write-Host ("{0,-28} ERRORE {1}" -f $v, $_.Exception.Message) }
}
try {
    Invoke-WebRequest -Uri "$base/tipologiche/estensioni" -OutFile (Join-Path $dir "nt_estensioni.json") -UseBasicParsing -TimeoutSec 60
    Write-Host "estensioni OK"
} catch { Write-Host "estensioni ERRORE $($_.Exception.Message)" }

# ── Cellar: elenco delle versioni consolidate (SPARQL) ──
$q = @"
PREFIX cdm: <http://publications.europa.eu/ontology/cdm#>
SELECT DISTINCT ?celex WHERE {
  ?w cdm:resource_legal_id_celex ?celex .
  FILTER(STRSTARTS(STR(?celex), "02005R2073") || STRSTARTS(STR(?celex), "02019R0627") || STRSTARTS(STR(?celex), "02004R0853") || STRSTARTS(STR(?celex), "02023R0915"))
} ORDER BY ?celex
"@
$url = "https://publications.europa.eu/webapi/rdf/sparql?query=" + [Uri]::EscapeDataString($q) + "&format=" + [Uri]::EscapeDataString("application/sparql-results+json")
try {
    Invoke-WebRequest -Uri $url -OutFile (Join-Path $dir "cellar_consolidati.json") -UseBasicParsing -TimeoutSec 120
    $j = Get-Content (Join-Path $dir "cellar_consolidati.json") -Raw -Encoding UTF8 | ConvertFrom-Json
    $elenco = $j.results.bindings | ForEach-Object { $_.celex.value }
    Write-Host "Versioni consolidate trovate: $($elenco.Count)"
    $elenco | Group-Object { $_.Substring(0, 10) } | ForEach-Object { Write-Host ("  {0}: ultima {1}" -f $_.Name, ($_.Group | Sort-Object | Select-Object -Last 1)) }
    # scarica l'ultima consolidata del 2073
    $ultima = $elenco | Where-Object { $_ -like "02005R2073*" } | Sort-Object | Select-Object -Last 1
    if ($ultima) {
        $file = Join-Path $dir "cellar_$ultima.xhtml"
        Invoke-WebRequest -Uri "http://publications.europa.eu/resource/celex/$ultima" -Headers @{ "Accept" = "application/xhtml+xml"; "Accept-Language" = "ita" } -OutFile $file -UseBasicParsing -TimeoutSec 90
        Write-Host ("Consolidato {0}: {1:N0} byte" -f $ultima, (Get-Item $file).Length)
    }
} catch { Write-Host "SPARQL/consolidato ERRORE $($_.Exception.Message)" }
Write-Host "File in $dir"
