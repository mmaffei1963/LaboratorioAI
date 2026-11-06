# Lavorare da due postazioni (ufficio e portatile)

Il repository GitHub e' la fonte di verita' del codice.

## Prima di cominciare
    cd C:\Sviluppo\LaboratorioAI
    git pull

## Quando finisci (prima di cambiare postazione)
    git status --short
    git add <file modificati>
    git commit -m "cosa hai fatto"
    git push

## Regole
- Non lavorare sulle due postazioni senza fare pull e push in mezzo.
- Le migrazioni EF Core si creano su una postazione sola per volta.
  Dopo un pull che ne porta di nuove, sull'altra postazione:
    dotnet ef database update --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
- Pubblicare in produzione solo dal PC dell'ufficio.
- Prima di ogni commit controllare con git status che non ci siano file di configurazione con segreti.

## Cosa NON viaggia con git (da tenere allineato a mano)
- appsettings.Development.json (Web, Portale, Tracciabilita.Web)
- Segreti (dotnet user-secrets): chiave Anthropic, password SMTP
- Modelli Word in D:\LaboratorioAI\Piscine\Modello e D:\LaboratorioAI\Modelli\Dvr
- File contabile AZW_07.accdb
- Database di test (si copiano con backup e ripristino)

## Come si cambia postazione (i due .bat)
- Quando ti siedi a un PC: doppio clic su INIZIO-LAVORO.bat. Scarica il lavoro dall'altro PC,
  propone di aggiornare il database locale se sono arrivate migrazioni e controlla la configurazione.
- Prima di alzarti: doppio clic su FINE-LAVORO.bat. Controlla che non ci siano password o file
  riservati, chiede due parole su cosa hai fatto e salva tutto su GitHub.
- Sono gli stessi file su entrambi i PC: riconoscono da soli dove si trovano.
- Se un messaggio e' rosso, lo script si e' fermato senza cambiare niente: leggi cosa dice e, se non basta,
  incollalo a Claude. Non forzare mai (niente git push --force, niente git reset --hard).

## Script (.bat / .ps1): cosa fanno su ciascuna postazione
- Avvia_LaboratorioAI.bat, Avvia_Portale.bat, Avvia_Tracciato.bat: funzionano su entrambe le postazioni.
  Prima di partire controllano (scripts\Controlla-Ambiente.ps1): se manca appsettings.Development.json
  o se i database principali puntano alla produzione (192.168.23.1 / SRVMAREA), NON partono.
  Sul portatile impostano da soli la cartella Econorma locale (D:\LaboratorioAI\TempLogScarico),
  spengono il controllo ACCREDIA notturno e avvisano se c'e' una password SMTP.
- Pubblica-Produzione.bat, Pubblica-Portale.bat, Pubblica-Tracciato.bat: partono SOLO dal PC dell'ufficio
  (PCMAREA01). Prima controllano che il codice sia su GitHub (nessuna modifica non salvata, nessun commit
  da spedire o da scaricare); se non e' cosi' chiedono una conferma scritta.
- Backup_Su_D.bat: serve soprattutto per i file che in git non ci sono (appsettings.Development.json, ecc.).
- Ripristina-Produzione-13-10.bat e datapump_dati_locale_to_prod.ps1: script una tantum, ora con conferma
  scritta, perche' in produzione possono fare danni. pubblica_web.ps1 e' superato: usare Pubblica-Produzione.bat.
- Tutti gli script usano la cartella in cui si trovano: non dipendono piu' da C:\Sviluppo\LaboratorioAI.

## Sul portatile
- Nessuna password SMTP: il database di test ha indirizzi veri.
- Database solo locali (.\SQLEXPRESS), mai il server di produzione 192.168.23.1.
