# Funzioni comuni agli script di Laboratorio AI.
# Servono a distinguere il PC dell'ufficio (dove si pubblica in produzione)
# dal portatile (solo sviluppo, solo database locali).
# Uso:  . (Join-Path $PSScriptRoot 'PostazioneComune.ps1')

function Test-PostazioneUfficio {
    return ($env:COMPUTERNAME -eq 'PCMAREA01')
}

function Get-NomePostazione {
    if (Test-PostazioneUfficio) { return 'UFFICIO' }
    return "PORTATILE ($env:COMPUTERNAME)"
}

# Trova git.exe: prima nel PATH, poi in Git for Windows, poi in quello di Visual Studio.
function Find-Git {
    $c = Get-Command git -ErrorAction SilentlyContinue
    if ($c) { return $c.Source }
    $candidati = @('C:\Program Files\Git\cmd\git.exe')
    $candidati += @(Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\*\*\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName })
    foreach ($p in $candidati) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}
