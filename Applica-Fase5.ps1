# Presenze, fase 5: registra le due guide in Guide.cs e il contatore "da controllare" in ContatoriHome.cs.
# Ogni modifica deve trovare il punto UNA volta sola, altrimenti lo script si ferma senza toccare quel file e dice cosa non ha trovato.
# Uso (dalla cartella C:\Sviluppo\LaboratorioAI):   powershell -ExecutionPolicy Bypass -File .\Applica-Fase5.ps1
$ErrorActionPreference = "Stop"
$radice = (Get-Location).Path

function Modifica-File {
    param([string]$Percorso, [object[]]$Sostituzioni)
    $completo = Join-Path $radice $Percorso
    if (-not (Test-Path $completo)) { throw "File non trovato: $Percorso" }
    $byte = [System.IO.File]::ReadAllBytes($completo)
    $haBom = ($byte.Length -ge 3 -and $byte[0] -eq 0xEF -and $byte[1] -eq 0xBB -and $byte[2] -eq 0xBF)
    $testo = [System.IO.File]::ReadAllText($completo, [System.Text.Encoding]::UTF8)
    $a = if ($testo.Contains("`r`n")) { "`r`n" } else { "`n" }
    foreach ($s in $Sostituzioni) {
        $cerca = $s.Cerca -replace "`r?`n", $a
        $nuovo = $s.Nuovo -replace "`r?`n", $a
        if ($testo.Contains($s.Gia)) { Write-Host "  gia presente, salto: $($s.Nome)"; continue }
        $n = ([regex]::Matches($testo, [regex]::Escape($cerca))).Count
        if ($n -ne 1) { throw "In $Percorso il punto '$($s.Nome)' compare $n volte (deve essere 1). Nessuna modifica fatta a questo file." }
        $testo = $testo.Replace($cerca, $nuovo)
        Write-Host "  fatto: $($s.Nome)"
    }
    [System.IO.File]::WriteAllText($completo, $testo, (New-Object System.Text.UTF8Encoding($haBom)))
}

Write-Host "Guide.cs"
Modifica-File "src\LaboratorioAI.Web\Navigazione\Guide.cs" @(
    @{ Nome = "guide Presenze"; Gia = 'new("PresenzeGestione"'
       Cerca = '        new("ManualisticaModelli", "Manualistica: modelli",'
       Nuovo = @'
        // 19/11/2026: la guida di gestione va prima, cosi' chi gestisce la trova sulle pagine riservate (/Presenze/Lavoratori, /Presenze/Riepilogo).
        new("PresenzeGestione", "Presenze: gestione",
            "Registrare chi timbra, controllare i totali del mese e chi \u00e8 in sede, correggere le presenze dei colleghi, tenere il calendario aziendale, esportare in Excel.",
            new[] { "/Presenze/Lavoratori", "/Presenze/ModificaLavoratore", "/Presenze/Riepilogo" }, Permessi.PresenzeGestisci, new DateOnly(2026, 11, 19)),
        new("Presenze", "Presenze",
            "Timbrare entrata e uscita, correggere le proprie timbrature, inserire ferie e permessi, vedere il prospetto del mese e il calendario ferie.",
            new[] { "/Presenze" }, Permessi.PresenzeTimbra, new DateOnly(2026, 11, 19)),
        new("ManualisticaModelli", "Manualistica: modelli",
'@ }
)

Write-Host "ContatoriHome.cs"
Modifica-File "src\LaboratorioAI.Web\Navigazione\ContatoriHome.cs" @(
    @{ Nome = "using Presenze"; Gia = 'using LaboratorioAI.Dati.Presenze;'
       Cerca = 'using LaboratorioAI.Dati.MonitorTemperature;'
       Nuovo = @'
using LaboratorioAI.Dati.MonitorTemperature;
using LaboratorioAI.Dati.Presenze;
'@ },
    @{ Nome = "using Core.Presenze"; Gia = 'using LaboratorioAI.Core.Presenze;'
       Cerca = 'using LaboratorioAI.Core.Entita;'
       Nuovo = @'
using LaboratorioAI.Core.Entita;
using LaboratorioAI.Core.Presenze;
'@ },
    @{ Nome = "contatore Presenze"; Gia = 'new("/Presenze/Riepilogo"'
       Cerca = '        new("/LaboratoriEsterni", "da sistemare",'
       Nuovo = @'
        // 19/11/2026: presenze. La voce di menu Riepilogo e' solo per chi gestisce le presenze, quindi il contatore e' solo per loro.
        new("/Presenze/Riepilogo", "da controllare", "giorni con timbrature da controllare (entrata o uscita mancante) nel mese e in quello precedente, su tutti i lavoratori", false,
            PresenzeDaControllareAsync),

        new("/LaboratoriEsterni", "da sistemare",
'@ },
    @{ Nome = "calcolo Presenze"; Gia = 'Task<int> PresenzeDaControllareAsync('
       Cerca = '    /// <summary>PDF presenti nella cartella di arrivo dei certificati esterni'
       Nuovo = @'
    /// <summary>
    /// Giorni con timbrature che non tornano (entrata senza uscita, uscita senza entrata) nel mese corrente e in quello precedente,
    /// sommati su tutti i lavoratori. Il giorno in corso non conta. Stesso calcolo del Riepilogo presenze.
    /// </summary>
    private static async Task<int> PresenzeDaControllareAsync(IServiceProvider sp, IContestoUtente _, CancellationToken ct)
    {
        var servizio = sp.GetRequiredService<ServizioPresenze>();
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var precedente = oggi.AddMonths(-1);
        var totale = 0;
        foreach (var (anno, mese) in new[] { (precedente.Year, precedente.Month), (oggi.Year, oggi.Month) })
            totale += (await servizio.ProspettoMensileAsync(anno, mese, null, ct)).Sum(r => r.Prospetto.Totali.GiorniConAnomalie);
        return totale;
    }

    /// <summary>PDF presenti nella cartella di arrivo dei certificati esterni
'@ }
)
Write-Host "Fatto."
