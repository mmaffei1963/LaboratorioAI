# Presenze, fase 5b: pagina "Chi c'e' adesso". Aggiunge menu, metodo del servizio, azione del controller e contatore della home.
# Ogni modifica deve trovare il punto UNA volta sola, altrimenti lo script si ferma senza toccare quel file e dice cosa non ha trovato.
# Uso (dalla cartella C:\Sviluppo\LaboratorioAI):   powershell -ExecutionPolicy Bypass -File .\Applica-Fase5b.ps1
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

Write-Host "MenuApplicazione.cs"
Modifica-File "src\LaboratorioAI.Web\Navigazione\MenuApplicazione.cs" @(
    @{ Nome = "voce Chi c'e' adesso nel menu Presenze"; Gia = '"/Presenze/ChiCe"'
       Cerca = '            new("Le mie presenze", "/Presenze/Mese", Permessi.PresenzeTimbra,'
       Nuovo = @'
            new("Chi c'e' adesso", "/Presenze/ChiCe", Permessi.PresenzeTimbra, "Chi ha timbrato, chi e' uscito e chi e' assente oggi, per sapere subito chi trovi."),
            new("Le mie presenze", "/Presenze/Mese", Permessi.PresenzeTimbra,
'@ }
)

Write-Host "ServizioPresenze.cs"
Modifica-File "src\LaboratorioAI.Dati\Presenze\ServizioPresenze.cs" @(
    @{ Nome = "metodo DisponibilitaOggiAsync"; Gia = 'DisponibilitaOggiAsync('
       Cerca = '    // ------------------------------------------------------------------ calendario aziendale (modifica)'
       Nuovo = @'
    // ------------------------------------------------------------------ chi c'e' adesso

    /// <summary>Stato di oggi di ogni lavoratore attivo (per «Chi c'è adesso»): stesso calcolo del prospetto, nell'ordine dell'elenco.</summary>
    public async Task<List<RigaDisponibilita>> DisponibilitaOggiAsync(CancellationToken ct)
    {
        var oggi = FusoPresenze.GiornoLocale(DateTime.UtcNow);
        var risultato = new List<RigaDisponibilita>();
        foreach (var l in await _ctx.Lavoratori.AsNoTracking().Include(x => x.Sede)
                     .Where(x => x.Attivo && (x.AttivoDal == null || x.AttivoDal <= oggi))
                     .OrderBy(x => x.Ordine).ThenBy(x => x.Cognome).ToListAsync(ct))
        {
            var g = (await CalcolaAsync(l, oggi, oggi, ct)).Giorni[0];
            risultato.Add(new RigaDisponibilita(l.IdLavoratore, l.Cognome, l.Colore, l.IdSede, l.Sede?.Nome ?? "", DisponibilitaPresenze.Valuta(g)));
        }
        return risultato;
    }

    // ------------------------------------------------------------------ calendario aziendale (modifica)
'@ }
)

Write-Host "PresenzeController.cs"
Modifica-File "src\LaboratorioAI.Web\Controllers\PresenzeController.cs" @(
    @{ Nome = "azione ChiCe"; Gia = 'IActionResult> ChiCe('
       Cerca = '    private bool PuoGestire => _utente.HaPermesso(Permessi.PresenzeGestisci);'
       Nuovo = @'
    private bool PuoGestire => _utente.HaPermesso(Permessi.PresenzeGestisci);

    /// <summary>
    /// Chi c'è adesso (20/11/2026): lo stato di oggi di tutti, per tutti. Dell'assenza si mostra solo che c'è, mai la causale.
    /// Il calcolo si tiene in memoria 30 secondi (uguale per tutti), così molte pagine aperte non pesano sul database.
    /// </summary>
    public async Task<IActionResult> ChiCe(int? sede, CancellationToken ct)
    {
        var memoria = HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        var tutte = await Microsoft.Extensions.Caching.Memory.CacheExtensions.GetOrCreateAsync(memoria, "presenze-chi-ce", async voce =>
        {
            voce.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30);
            return await _servizio.DisponibilitaOggiAsync(ct);
        }) ?? new List<RigaDisponibilita>();
        var righe = sede is int s ? tutte.Where(r => r.IdSede == s).ToList() : tutte;
        ViewData["Titolo"] = "Chi c'è adesso";
        return View(new ModelloChiCe(righe, await _servizio.SediAsync(ct), sede, FusoPresenze.Locale(DateTime.UtcNow)));
    }
'@ }
)

Write-Host "ContatoriHome.cs"
Modifica-File "src\LaboratorioAI.Web\Navigazione\ContatoriHome.cs" @(
    @{ Nome = "contatore Chi c'e' adesso"; Gia = 'new("/Presenze/ChiCe"'
       Cerca = '        new("/LaboratoriEsterni", "da sistemare",'
       Nuovo = @'
        new("/Presenze/ChiCe", "in servizio", "colleghi che hanno timbrato l'entrata e non sono ancora usciti", false,
            PresenzeInServizioAsync),

        new("/LaboratoriEsterni", "da sistemare",
'@ },
    @{ Nome = "calcolo in servizio"; Gia = 'Task<int> PresenzeInServizioAsync('
       Cerca = '    /// <summary>PDF presenti nella cartella di arrivo dei certificati esterni'
       Nuovo = @'
    /// <summary>Colleghi in servizio adesso (ultima timbratura di oggi = entrata). Stesso calcolo della pagina «Chi c'è adesso».</summary>
    private static async Task<int> PresenzeInServizioAsync(IServiceProvider sp, IContestoUtente _, CancellationToken ct) =>
        DisponibilitaPresenze.InServizio(await sp.GetRequiredService<ServizioPresenze>().DisponibilitaOggiAsync(ct));

    /// <summary>PDF presenti nella cartella di arrivo dei certificati esterni
'@ }
)
Write-Host "Fatto."
