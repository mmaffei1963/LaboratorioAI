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

## Sul portatile
- Nessuna password SMTP: il database di test ha indirizzi veri.
- Database solo locali (.\SQLEXPRESS), mai il server di produzione 192.168.23.1.
