# Messa in produzione di Tracciato — periodo di prova con Riccione srl

Tracciato è un sito separato, con il suo database (`Tracciabilita`, schema `trac`), pubblicato come il Portale clienti: sito IIS dedicato, Cloudflare davanti, segreti nelle variabili d'ambiente del server. **Non ha nessun accesso a ArcLab né al database di Laboratorio AI.**

## Chi fa cosa
- **Gen (IT):** account di servizio, database SQL, cartelle e sito IIS, record DNS/Cloudflare, variabili d'ambiente.
- **Mirko:** pubblica (doppio click), crea l'azienda Riccione dal sito, importa i dati, controlla.

## Primo impianto (una volta sola)

1. **Account di servizio dedicato** `MAREA\svc_tracciato` (non riusare `svc_labai`, che vede ArcLab).
2. **Database:** in SSMS come amministratore SQL, eseguire `deploy_database_tracciato.sql` (crea il database vuoto `Tracciabilita` e i permessi solo su di esso). Poi, sul PC di Mirko, doppio click su `Script-Database-Tracciato.bat`: produce `C:\Sviluppo\Publish\Tracciato_database.sql`; eseguirlo in SSMS sul database `Tracciabilita`.
3. **Cartelle sul server:** `D:\LaboratorioAI\Produzione\Tracciato` (cioè `\\SRVMAREA\marea\LaboratorioAI\Produzione\Tracciato`) e `...\Produzione\Backup\Tracciato`. Permessi di lettura/scrittura a `svc_tracciato` sulla cartella del sito.
4. **Prima copia dei file:** sul PC di Mirko `dotnet publish src\LaboratorioAI.Tracciabilita.Web -c Release -o C:\Sviluppo\Publish\Tracciato`; copiare **tutta** la cartella (compreso `appsettings.Production.json`, che le pubblicazioni successive non sovrascrivono) in `Produzione\Tracciato`, più `_app_offline.htm` (da quella della Web).
5. **Segreti:** compilare `deploy_variabili_ambiente_tracciato.ps1` (SMTP Brevo, utente gestore iniziale) ed eseguirlo sul server come amministratore, poi `iisreset`.
6. **IIS:** Application Pool dedicato, «No Managed Code», identità `MAREA\svc_tracciato`; sito `tracciato.mare-a.com` sulla cartella di produzione, HTTPS con il certificato come per il Portale.
7. **Cloudflare:** record DNS `tracciato` come quello del Portale (proxy attivo).
8. **Controllo:** `https://tracciato.mare-a.com/stato` deve rispondere `Healthy`; poi login con l'utente gestore.

## Dati di Riccione srl
Dal sito (login gestore) creare l'azienda «Riccione srl», la sede e l'amministratore (invito da Gestione). I dati storici si importano dal file Access con l'importatore (database pulito + reimportazione, come in prova), poi le ricette (`ricette`).

## Aggiornamenti successivi
1. Se ci sono nuove migrazioni: `Script-Database-Tracciato.bat` e far eseguire lo script in SSMS **prima**.
2. Doppio click su `Pubblica-Tracciato.bat`: pubblica, fa il backup, riavvia, verifica `/stato` e **ripristina da solo** se il sito non risponde. I file `.json` di produzione non si toccano mai.

## Collegamenti sul desktop
Doppio click una volta su `Crea-Collegamenti-Desktop-Tracciato.bat`: crea «Tracciato (sviluppo)» (avvia il sito locale su `localhost:5082`), «Tracciato (produzione)» (apre `https://tracciato.mare-a.com`) e «Pubblica Tracciato».

## Da sapere
- `appsettings.Production.json` contiene la stringa di connessione, l'indirizzo pubblico (`UrlPubblico`, usato per i QR delle etichette e per i link degli inviti) e `ApplicaVincoliSql`/`AttivaSicurezzaRiga` **spenti**: si accendono solo dopo il collaudo su `Tracciabilita_Test`.
- L'indirizzo `tracciato.mare-a.com` è provvisorio: il dominio col marchio si sceglie dopo e si cambia senza toccare i dati (basta cambiare `UrlPubblico` e il binding IIS).
- Periodo di prova senza corrispettivo: prima di caricare dati reali di Riccione serve almeno un accordo scritto breve (anche una mail) su prova, durata, trattamento dei dati e restituzione/cancellazione a fine prova. Il contratto GDPR art. 28 completo è ancora da fare.
