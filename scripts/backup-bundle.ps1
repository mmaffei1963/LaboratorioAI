# Backup del repository LaboratorioAI in un unico file .bundle su OneDrive.
# Eseguito automaticamente dall'Utilità di pianificazione di Windows.
$ErrorActionPreference = 'Stop'

$repo   = 'C:\Sviluppo\LaboratorioAI'
$backup = 'C:\Users\mirko.maffei\OneDrive - MAREA S.R.L\Backup'
$gitDir = 'C:\Program Files\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd'

$log = Join-Path $backup 'backup-bundle.log'
$dest = Join-Path $backup 'LaboratorioAI.bundle'

try {
    $env:Path += ";$gitDir"
    New-Item -ItemType Directory -Force -Path $backup | Out-Null
    Set-Location $repo

    # Il bundle contiene tutta la storia committata (tutti i rami).
    git bundle create "$dest" --all

    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  OK  bundle aggiornato" | Add-Content -Path $log
}
catch {
    "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  ERRORE  $($_.Exception.Message)" | Add-Content -Path $log
}
