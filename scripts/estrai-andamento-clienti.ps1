# Estrae i dati per il prototipo «Clienti da seguire» (andamento fatturato e campioni per cliente).
# SOLA LETTURA: legge ArcLab (LabAnalisiDati) e i dati AZW già importati in LaboratorioAI_Test.
# Non scrive su nessun database. Produce file CSV in docs\AndamentoClienti.
# Uso:  powershell -ExecutionPolicy Bypass -File scripts\estrai-andamento-clienti.ps1

$ErrorActionPreference = 'Stop'
[System.Threading.Thread]::CurrentThread.CurrentCulture = [System.Globalization.CultureInfo]::InvariantCulture

$dest = Join-Path $PSScriptRoot '..\docs\AndamentoClienti'
New-Item -ItemType Directory -Force -Path $dest | Out-Null

$csLabAI   = 'Server=PCMAREA01\SQLEXPRESS;Database=LaboratorioAI_Test;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;'
$csArcProd = 'Server=192.168.23.1;Database=LabAnalisiDati;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;ApplicationIntent=ReadOnly;'
$csArcLoc  = 'Server=PCMAREA01\SQLEXPRESS;Database=LabAnalisiDati;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;'

function Esporta($cs, $sql, $file) {
    $cn = New-Object System.Data.SqlClient.SqlConnection $cs
    $cn.Open()
    try {
        $cmd = $cn.CreateCommand(); $cmd.CommandText = $sql; $cmd.CommandTimeout = 600
        $t = New-Object System.Data.DataTable
        $t.Load($cmd.ExecuteReader())
        $t | Select-Object * -ExcludeProperty RowError, RowState, Table, ItemArray, HasErrors |
            Export-Csv -Path (Join-Path $dest $file) -Delimiter ';' -NoTypeInformation -Encoding UTF8
        Write-Host ("{0}: {1} righe" -f $file, $t.Rows.Count)
    } finally { $cn.Close() }
}

# 1) Fatturato AZW per cliente, mese, centro e voce (note di credito in negativo), dal 2022.
Esporta $csLabAI @"
SELECT f.CodiceCliente, YEAR(f.DataFattura) AS Anno, MONTH(f.DataFattura) AS Mese, r.Centro, r.Voce, SUM(r.Netto) AS Netto
FROM provv.AzwRigheFattura r JOIN provv.AzwFatture f ON f.Chiave = r.ChiaveFattura
WHERE f.DataFattura >= '20220101'
GROUP BY f.CodiceCliente, YEAR(f.DataFattura), MONTH(f.DataFattura), r.Centro, r.Voce
UNION ALL
SELECT n.CodiceCliente, YEAR(n.Data), MONTH(n.Data), n.Centro, n.Voce, -SUM(ABS(n.Netto))
FROM provv.AzwRigheNoteCredito n
WHERE n.Data >= '20220101'
GROUP BY n.CodiceCliente, YEAR(n.Data), MONTH(n.Data), n.Centro, n.Voce
"@ 'fatturato.csv'

# 2) Anagrafica AZW (venditore attuale dalle fatture, zona = responsabile cliente).
Esporta $csLabAI @"
SELECT c.CodiceCliente, c.RagioneSociale, c.Citta, c.Provincia, c.Zona,
       (SELECT MAX(f.CodiceVenditore) FROM provv.AzwFatture f WHERE f.CodiceCliente = c.CodiceCliente) AS CodiceVenditore
FROM provv.AzwClienti c
"@ 'clienti.csv'

# 3) Campioni ArcLab di laboratorio (commesse tipo 3) per cliente, mese e controllo, dal 2022.
$sqlCampioni = @"
SELECT LTRIM(RTRIM(CAST(cl.CODContabilita AS varchar(50)))) AS CodiceCliente,
       YEAR(lo.DataRic) AS Anno, MONTH(lo.DataRic) AS Mese,
       cm.IDControllo AS IdControllo, MAX(CAST(co.Controllo AS nvarchar(200))) AS Controllo,
       COUNT(*) AS Campioni, SUM(ISNULL(cm.PrezzoControllo, 0)) AS ValoreListino,
       MAX(lo.DataRic) AS UltimoCampione
FROM Campione ca
JOIN Lotto lo           ON lo.IdLotto = ca.IdLotto
JOIN commessa cm        ON cm.IdCommessa = lo.IdCommessa
LEFT JOIN Controllo co  ON co.IDControllo = cm.IDControllo
JOIN ContattoCliente cc ON cc.IDContatto = cm.IDContatto
JOIN Insediamento i     ON i.IDInsediamento = cc.IDInsediamento
JOIN Clienti cl         ON cl.IDCliente = i.IDCliente
WHERE cm.IdTipoCommessa = 3 AND lo.DataRic >= '20220101' AND cl.CODContabilita IS NOT NULL
GROUP BY LTRIM(RTRIM(CAST(cl.CODContabilita AS varchar(50)))), YEAR(lo.DataRic), MONTH(lo.DataRic), cm.IDControllo
"@
try { Esporta $csArcProd $sqlCampioni 'campioni.csv' }
catch {
    Write-Host "ArcLab di produzione non raggiungibile ($($_.Exception.Message)): uso la copia locale."
    Esporta $csArcLoc $sqlCampioni 'campioni.csv'
}
Write-Host "File in $dest"
