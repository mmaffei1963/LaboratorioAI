# Crea sul Desktop tre collegamenti per Tracciato:
#   Tracciato (sviluppo)     -> avvia il sito locale su http://localhost:5082 e apre il browser
#   Tracciato (produzione)   -> apre https://tracciato.mare-a.com
#   Pubblica Tracciato       -> pubblica in produzione (doppio click, come per il Portale)
$cartella = 'C:\Sviluppo\LaboratorioAI'
$desktop  = [Environment]::GetFolderPath('Desktop')
$shell    = New-Object -ComObject WScript.Shell

function Collegamento($nome, $destinazione, $descrizione) {
    $lnk = $shell.CreateShortcut((Join-Path $desktop "$nome.lnk"))
    $lnk.TargetPath       = $destinazione
    $lnk.WorkingDirectory = $cartella
    $lnk.Description      = $descrizione
    $lnk.Save()
    Write-Host "Creato: $nome"
}

Collegamento 'Tracciato (sviluppo)'   (Join-Path $cartella 'Avvia_Tracciato.bat')             'Avvia Tracciato in sviluppo (localhost:5082)'
Collegamento 'Tracciato (produzione)' (Join-Path $cartella 'Avvia_Tracciato_Produzione.bat')  'Apre Tracciato in produzione'
Collegamento 'Pubblica Tracciato'     (Join-Path $cartella 'Pubblica-Tracciato.bat')           'Pubblica Tracciato in produzione'
