# FINE LAVORO: da lanciare prima di lasciare un PC (ufficio o portatile).
# Salva tutto su GitHub (commit + push) cosi' l'altro PC puo' scaricarlo.
# Prima di salvare controlla che non ci siano password/chiavi o file che non devono andare
# su GitHub; se ne trova, NON salva e dice quali file guardare.

. (Join-Path $PSScriptRoot 'PostazioneComune.ps1')
$radice = Split-Path -Parent $PSScriptRoot
Set-Location $radice
$git = Find-Git

function Esci-Con-Errore([string]$testo) {
    Write-Host ""
    Write-Host $testo -ForegroundColor Red
    exit 1
}

function Get-AvantiDietro {
    $conta = @(& $git rev-list --left-right --count 'HEAD...@{u}')
    if ($LASTEXITCODE -ne 0 -or $conta.Count -eq 0) { return $null }
    $n = ("" + $conta[0]) -split '\s+'
    return @([int]$n[0], [int]$n[1])
}

Write-Host ""
Write-Host ("=== FINE LAVORO su " + (Get-NomePostazione) + " ===") -ForegroundColor Cyan

if (-not $git) { Esci-Con-Errore "git non trovato: installalo o apri il terminale di Visual Studio." }

$righe = @(& $git status --porcelain)

if ($righe.Count -gt 0) {
    Write-Host ("File cambiati: " + $righe.Count)
    foreach ($r in ($righe | Select-Object -First 40)) { Write-Host "  $r" }
    if ($righe.Count -gt 40) { Write-Host "  ... e altri" }

    # 1) File che non devono mai andare su GitHub
    $vietati = '(?i)(appsettings\.Development\.json|appsettings\.Local\.json|appsettings\.secrets\.json|\.bak$|\.pfx$|\.key$|\.accdb$|\.mdf$|\.ldf$|\.env$)'
    $trovati = @()
    foreach ($r in $righe) {
        $percorso = $r.Substring(3)
        if ($percorso -match ' -> ') { $percorso = ($percorso -split ' -> ')[-1] }
        if ($percorso -match $vietati) { $trovati += $percorso }
    }
    if ($trovati.Count -gt 0) {
        Write-Host ""
        Write-Host "NON SALVO: tra i file ce ne sono che non devono andare su GitHub (configurazioni locali, copie, database):" -ForegroundColor Red
        foreach ($t in $trovati) { Write-Host "  $t" -ForegroundColor Red }
        Write-Host "Cancellali o spostali fuori dal progetto, poi rilancia FINE-LAVORO."
        exit 1
    }

    # 2) File troppo grandi (GitHub rifiuta oltre 100 MB)
    $grandi = @()
    foreach ($p in @(& $git ls-files --others --exclude-standard)) {
        $info = Get-Item -LiteralPath $p -ErrorAction SilentlyContinue
        if ($info -and $info.Length -gt 50MB) { $grandi += $p }
    }
    if ($grandi.Count -gt 0) {
        Write-Host ""
        Write-Host "NON SALVO: file troppo grandi per GitHub (oltre 50 MB):" -ForegroundColor Red
        foreach ($g in $grandi) { Write-Host "  $g" -ForegroundColor Red }
        exit 1
    }

    # 3) Password o chiavi scritte nei file di configurazione (mostro solo i nomi dei file, mai i valori)
    $estensioni = '\.(json|config|xml|ini|env|yml|yaml|txt|properties)$'
    $modello = '(Password|Pwd|ChiaveApi|ApiKey|Segreto|Secret|Token)["'']?\s*[:=]\s*["'']?[^"''\s;,{}$<>*]{6,}'
    $sospetti = New-Object System.Collections.Generic.List[string]
    $file = $null
    foreach ($riga in @(& $git diff HEAD -U0 --no-color)) {
        if ($riga -match '^\+\+\+ b/(.*)$') { $file = $Matches[1]; continue }
        if ($riga.StartsWith('+') -and $file -and ($file -match $estensioni) -and ($riga -match $modello)) {
            if (-not $sospetti.Contains($file)) { $sospetti.Add($file) }
        }
    }
    foreach ($p in @(& $git ls-files --others --exclude-standard)) {
        if ($p -notmatch $estensioni) { continue }
        $info = Get-Item -LiteralPath $p -ErrorAction SilentlyContinue
        if (-not $info -or $info.Length -gt 1MB) { continue }
        if (Select-String -LiteralPath $p -Pattern $modello -Quiet) {
            if (-not $sospetti.Contains($p)) { $sospetti.Add($p) }
        }
    }
    if ($sospetti.Count -gt 0) {
        Write-Host ""
        Write-Host "NON SALVO: in questi file sembra esserci una password o una chiave scritta in chiaro:" -ForegroundColor Red
        foreach ($s in $sospetti) { Write-Host "  $s" -ForegroundColor Red }
        Write-Host "Toglila (i segreti vanno nei user-secrets, non nei file) e rilancia FINE-LAVORO."
        Write-Host "Se e' un falso allarme, salva a mano con git add / git commit / git push."
        exit 1
    }

    # 4) Commit
    Write-Host ""
    $msg = Read-Host "Cosa hai fatto? Scrivi poche parole (Invio = lavoro in corso)"
    if ([string]::IsNullOrWhiteSpace($msg)) { $msg = "Lavoro in corso" }
    $msg = ($msg -replace '"', "'").Trim() + " [" + $env:COMPUTERNAME + "]"
    & $git add -A
    & $git commit -m $msg
    if ($LASTEXITCODE -ne 0) { Esci-Con-Errore "Il commit non e' riuscito (vedi sopra). Non ho spedito niente." }
}
else {
    Write-Host "Nessuna modifica da salvare."
}

# 5) Allineamento con GitHub e invio
Write-Host ""
Write-Host "Controllo GitHub..."
& $git fetch origin
if ($LASTEXITCODE -ne 0) { Esci-Con-Errore "Non riesco a contattare GitHub (rete, VPN o credenziali?). Il lavoro e' salvato qui in locale ma NON e' su GitHub: rilancia FINE-LAVORO appena la rete torna." }

$ad = Get-AvantiDietro
if ($null -eq $ad) { Esci-Con-Errore "Il ramo non e' collegato a GitHub. Chiedi aiuto a Claude." }

if ($ad[1] -gt 0) {
    Write-Host ("Su GitHub ci sono " + $ad[1] + " commit nuovi (fatti dall'altro PC): provo a unirli ai tuoi.") -ForegroundColor Yellow
    & $git pull --no-rebase --no-edit
    if ($LASTEXITCODE -ne 0) {
        & $git merge --abort 2>$null
        Esci-Con-Errore ("Conflitto: le due postazioni hanno cambiato gli stessi file. Non ho forzato niente e il tuo lavoro e' al sicuro nel commit locale.`n" +
            "Incolla a Claude il risultato di:  git status")
    }
}

& $git push
if ($LASTEXITCODE -ne 0) { Esci-Con-Errore "Il push non e' riuscito (vedi sopra). Il lavoro e' salvato in locale ma NON e' su GitHub." }

& $git fetch origin
$ad = Get-AvantiDietro
Write-Host ""
Write-Host "Ultimi salvataggi:"
& $git log --oneline -3
Write-Host ""
if ($null -ne $ad -and $ad[0] -eq 0 -and $ad[1] -eq 0) {
    Write-Host "Tutto salvato su GitHub. Puoi cambiare postazione." -ForegroundColor Green
    exit 0
}
Esci-Con-Errore "Qualcosa non torna: questo PC e GitHub non sono ancora allineati. NON cambiare postazione e incolla a Claude:  git status"
