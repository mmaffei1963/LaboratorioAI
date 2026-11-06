<#
    datapump_dati_locale_to_prod.ps1
    Travaso DATI (solo dati, non lo schema) dal DB locale di sviluppo al DB di produzione.

    Sorgente : PCMAREA01\SQLEXPRESS  ->  LaboratorioAI_Test
    Destin.  : 192.168.23.1          ->  LaboratorioAI   (schema gia' allineato via migrazioni)

    Le tabelle sono su schemi multipli (app, sec, log, portale, accettazione, tarature, bilance, ...):
    lo script legge schema+tabella reali e li usa in bcp, quindi non assume 'dbo'.

    Cosa fa (con bcp nativo, preservando gli IDENTITY):
      1. Esporta ogni tabella della sorgente in file .dat nativi (cartella temporanea).
      2. [solo con -Conferma] Sul DB di produzione: disabilita i vincoli FK, SVUOTA tutte le
         tabelle (tranne __EFMigrationsHistory), poi reimporta i dati mantenendo le chiavi.
      3. Riabilita i vincoli FK con ricontrollo (WITH CHECK) -> se un dato viola una FK, lo segnala.
      4. Confronta i conteggi righe sorgente vs destinazione.

    IMPORTANTE:
      - Va lanciato dal PC che raggiunge SIA la sorgente SIA la produzione (autenticazione Windows).
      - Senza -Conferma fa solo l'ESPORTAZIONE e mostra cosa farebbe (nessuna scrittura in prod).
      - La produzione viene SVUOTATA e riempita: usare solo perche' prod e' vuota/appena inizializzata.
      - __EFMigrationsHistory NON viene toccata (schema resta quello gia' migrato in prod).

    USO:
      .\datapump_dati_locale_to_prod.ps1                 # prova: esporta soltanto, nessuna scrittura
      .\datapump_dati_locale_to_prod.ps1 -Conferma       # esegue davvero il travaso in produzione
#>

param(
    [string]$Sorgente       = 'PCMAREA01\SQLEXPRESS',
    [string]$DbSorgente     = 'LaboratorioAI_Test',
    [string]$Destinazione   = '192.168.23.1',
    [string]$DbDestinazione = 'LaboratorioAI',
    [string]$Cartella       = (Join-Path $env:TEMP 'datapump_labai'),
    [switch]$Conferma
)

$ErrorActionPreference = 'Stop'

function Esegui-Sql([string]$server, [string]$db, [string]$query) {
    $r = sqlcmd -S $server -d $db -h -1 -W -b -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw "sqlcmd fallito su $server/${db}: $r" }
    return $r
}

Write-Host "== DataPump dati  $Sorgente\$DbSorgente  ->  $Destinazione\$DbDestinazione ==" -ForegroundColor Cyan
if (-not $Conferma) {
    Write-Host "   MODALITA' PROVA: esporto soltanto, NON scrivo in produzione. Usa -Conferma per eseguire." -ForegroundColor Yellow
}

# --- Cartella temporanea pulita
if (Test-Path $Cartella) { Remove-Item $Cartella -Recurse -Force }
New-Item -ItemType Directory -Path $Cartella | Out-Null

# --- Elenco tabelle (schema + nome) dalla sorgente, escluso lo storico migrazioni
Write-Host "`n== 1/5  Elenco tabelle sorgente (schema.tabella) ==" -ForegroundColor Cyan
$tabelle = (Esegui-Sql $Sorgente $DbSorgente @"
SELECT TABLE_SCHEMA + '|' + TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory'
ORDER BY TABLE_SCHEMA, TABLE_NAME;
"@) | Where-Object { $_ -and $_.Trim() -ne '' } | ForEach-Object {
    $p = $_.Trim() -split '\|', 2
    [pscustomobject]@{ Schema = $p[0]; Nome = $p[1] }
}

if (-not $tabelle) { throw "Nessuna tabella trovata nella sorgente." }
Write-Host ("   {0} tabelle da travasare." -f $tabelle.Count) -ForegroundColor Green

# --- Esportazione (bcp OUT nativo)
Write-Host "`n== 2/5  Esportazione dati (bcp out) ==" -ForegroundColor Cyan
foreach ($tb in $tabelle) {
    $file = Join-Path $Cartella ("{0}.{1}.dat" -f $tb.Schema, $tb.Nome)
    $out = & bcp "[$($tb.Schema)].[$($tb.Nome)]" out $file -S $Sorgente -d $DbSorgente -T -n -k 2>&1
    if ($LASTEXITCODE -ne 0) { throw "bcp OUT fallito su $($tb.Schema).$($tb.Nome)`n$($out -join "`n")" }
}
Write-Host ("   esportate {0} tabelle in {1}" -f $tabelle.Count, $Cartella) -ForegroundColor Green

if (-not $Conferma) {
    Write-Host "`nPROVA completata. Verrebbero svuotate e ricaricate queste $($tabelle.Count) tabelle in produzione:" -ForegroundColor Yellow
    ($tabelle | ForEach-Object { "$($_.Schema).$($_.Nome)" }) -join ', ' | Write-Host
    Write-Host "`nRilancia con -Conferma per eseguire il travaso reale." -ForegroundColor Yellow
    return
}

# --- Produzione: disabilita FK e svuota (tranne storico migrazioni)
Write-Host "`n== 3/5  Produzione: disabilito vincoli FK e svuoto le tabelle ==" -ForegroundColor Cyan
Esegui-Sql $Destinazione $DbDestinazione @"
DECLARE @t nvarchar(400);
-- Passo 1: disabilita i vincoli FK su TUTTE le tabelle (qualificate schema.tabella)
DECLARE cur CURSOR FOR
  SELECT '[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'
  FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE';
OPEN cur; FETCH NEXT FROM cur INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN EXEC('ALTER TABLE ' + @t + ' NOCHECK CONSTRAINT ALL'); FETCH NEXT FROM cur INTO @t; END
CLOSE cur; DEALLOCATE cur;
-- Passo 2: svuota tutte le tabelle (tranne lo storico migrazioni)
DECLARE cur2 CURSOR FOR
  SELECT '[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'
  FROM INFORMATION_SCHEMA.TABLES
  WHERE TABLE_TYPE='BASE TABLE' AND TABLE_NAME <> '__EFMigrationsHistory';
OPEN cur2; FETCH NEXT FROM cur2 INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN EXEC('DELETE FROM ' + @t); FETCH NEXT FROM cur2 INTO @t; END
CLOSE cur2; DEALLOCATE cur2;
"@ | Out-Null
Write-Host "   fatto." -ForegroundColor Green

# --- Importazione (bcp IN nativo, -E mantiene gli IDENTITY)
Write-Host "`n== 4/5  Importazione dati in produzione (bcp in, IDENTITY preservati) ==" -ForegroundColor Cyan
foreach ($tb in $tabelle) {
    $file = Join-Path $Cartella ("{0}.{1}.dat" -f $tb.Schema, $tb.Nome)
    $out = & bcp "[$($tb.Schema)].[$($tb.Nome)]" in $file -S $Destinazione -d $DbDestinazione -T -n -E -k -b 5000 2>&1
    if ($LASTEXITCODE -ne 0) { throw "bcp IN fallito su $($tb.Schema).$($tb.Nome) (vincoli FK gia' disabilitati)`n$($out -join "`n")" }
}
Write-Host ("   importate {0} tabelle." -f $tabelle.Count) -ForegroundColor Green

# --- Riabilita i vincoli FK con ricontrollo
Write-Host "`n== 5/5  Riabilito i vincoli FK (con ricontrollo) e confronto i conteggi ==" -ForegroundColor Cyan
Esegui-Sql $Destinazione $DbDestinazione @"
DECLARE @t nvarchar(400);
DECLARE cur CURSOR FOR
  SELECT '[' + TABLE_SCHEMA + '].[' + TABLE_NAME + ']'
  FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE';
OPEN cur; FETCH NEXT FROM cur INTO @t;
WHILE @@FETCH_STATUS = 0
BEGIN EXEC('ALTER TABLE ' + @t + ' WITH CHECK CHECK CONSTRAINT ALL'); FETCH NEXT FROM cur INTO @t; END
CLOSE cur; DEALLOCATE cur;
"@ | Out-Null
Write-Host "   vincoli FK riabilitati e validati." -ForegroundColor Green

# --- Confronto righe sorgente/destinazione
Write-Host "`n   Tabella                                            Sorgente   Produzione" -ForegroundColor Cyan
$diff = 0
foreach ($tb in $tabelle) {
    $obj = "[$($tb.Schema)].[$($tb.Nome)]"
    $cs = [int](Esegui-Sql $Sorgente     $DbSorgente     "SELECT COUNT(*) FROM $obj;").Trim()
    $cd = [int](Esegui-Sql $Destinazione $DbDestinazione "SELECT COUNT(*) FROM $obj;").Trim()
    $flag = if ($cs -eq $cd) { ' ' } else { $diff++; '  <-- DIVERSO' }
    "{0,-50} {1,9} {2,11}{3}" -f "$($tb.Schema).$($tb.Nome)", $cs, $cd, $flag | Write-Host
}

if ($diff -eq 0) {
    Write-Host "`nTravaso completato: tutti i conteggi coincidono." -ForegroundColor Green
} else {
    Write-Host "`nATTENZIONE: $diff tabelle con conteggi diversi. Verificare sopra le righe '<-- DIVERSO'." -ForegroundColor Red
}
