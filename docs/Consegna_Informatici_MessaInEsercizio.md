# Laboratorio AI — Scheda di consegna per la messa in esercizio

**Destinatari:** sistemista / informatici che curano il passaggio in produzione.
**Data:** agosto 2026 · **Versione target:** 1.0.0 · **Stato:** pronto al collaudo su server.

Questa scheda è un **indice operativo**. I due runbook dettagliati restano la fonte di verità:

- **`DEPLOY_LaboratorioAI.md`** (radice) — runbook completo passo-passo.
- **`docs/Messa_in_esercizio.md`** — prima installazione, Entra ID, IIS, rilascio nuove versioni.
- **`HANDOFF_LaboratorioAI.md`** — architettura, modello dati, stato dei moduli.

---

## 1. In una riga

Due app ASP.NET Core 8 (`Web` porta 5080 interna con Entra ID; `Portale` porta 5081 su Internet) ricavate da una soluzione a sei progetti. Database applicativo **`LaboratorioAI`** (lettura/scrittura). Gestionale **`LabAnalisiDati` (ArcLab)** in **SOLA LETTURA — invariante assoluto**.

## 2. Cosa consegniamo

| Elemento | Da dove | Note |
|---|---|---|
| Cartella pubblicata **Web** | `dotnet publish src\LaboratorioAI.Web -c Release` | Copiare così com'è in `D:\LaboratorioAI\Produzione\Web` |
| Cartella pubblicata **Portale** | `dotnet publish src\LaboratorioAI.Portale -c Release` | In `D:\LaboratorioAI\Produzione\Portale` |
| Script SQL | `deploy_database.sql` (radice) | Crea DB, login e permessi, con verifica finale |
| Script segreti | `deploy_variabili_ambiente.ps1` (radice) | Variabili d'ambiente di sistema, poi `iisreset` |
| Modelli documentali | `.docx` DVR e Piscine | In `D:\LaboratorioAI\Modelli\...` (sez. 5 del DEPLOY) |

> La compilazione avviene **una sola volta** sulla macchina di sviluppo (ha l'SDK). Il server esegue soltanto: gli serve **solo** l'**ASP.NET Core 8 Hosting Bundle**, non l'SDK.

## 3. Checklist go-live (in ordine)

1. **Backup** completo di `LaboratorioAI` (se già esiste) e verifica backup di `LabAnalisiDati` — *non* lo tocchiamo, ma per prudenza.
2. **Prerequisiti server**: IIS + ASP.NET Core 8 Hosting Bundle (`dotnet --list-runtimes` deve elencare `Microsoft.AspNetCore.App 8.x`); raggiungibilità SQL `192.168.23.1`; account `MAREA\svc_labai` creato.
3. **Database e permessi**: eseguire `deploy_database.sql` con account admin SQL.
   - Verifica invariante: su `LabAnalisiDati` l'utente `MAREA\svc_labai` deve avere **`db_datareader` e nient'altro**.
   - Su `LaboratorioAI` servono `db_datareader`, `db_datawriter`, `db_ddladmin` (le migrazioni creano le tabelle).
4. **Pubblicazione**: ripubblicare le due cartelle Release dallo sviluppo (contengono tutti i moduli fino ad agosto 2026) e copiarle sul server. Rimuovere ogni `appsettings.Development.json` residuo dalle cartelle pubblicate.
5. **Segreti**: `deploy_variabili_ambiente.ps1` (Anthropic__ChiaveApi, EntraId__TenantId, EntraId__ClientId, Portale__CredenzialeOperatore), poi `iisreset`.
6. **Entra ID**: registrare l'app (redirect `/signin-oidc`, logout `/signout-callback-oidc`), copiare Tenant/Client ID nelle variabili. Richiede admin M365. Dominio `mare-a.com`.
7. **IIS**: due siti/app pool con identità `MAREA\svc_labai`, hosting in-process (già nei `web.config`). Permessi cartelle: lettura sui Modelli, scrittura sugli Archivi; lettura sullo share Econorma se si vuole il caricamento da rete.
8. **Primo avvio Web**: applica automaticamente le **31 migrazioni EF** e popola ruoli/permessi. In alternativa far rivedere lo script al DBA: `dotnet ef migrations script --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web -o migrazioni.sql`.
9. **Verifica post-deploy**: sonda `/stato` (no auth) su entrambe; login Entra ID sul Web; login cliente sul Portale; una ricerca campioni e un'estrazione Excel.
10. **Esposizione portale**: reverse proxy / Cloudflare per l'accesso esterno del solo Portale (5081).

## 4. Controlli di sicurezza (invarianti, da non violare)

- ArcLab **sola lettura**: `db_datareader` unico ruolo; le connessioni ArcLab usano `ApplicationIntent=ReadOnly`.
- I tre moduli di staging (**Accettazione**, **Proposta controlli**, **Certificati esterni Non Ricertificati**) **non scrivono** su ArcLab: producono **XML/staging** per l'import supervisionato di **Palomar**. La generazione del `CodLotto` e l'import restano a Palomar.
- Segreti **mai** nel codice o nel repository: solo variabili d'ambiente / user-secrets.
- Schema DB **solo** via migrazioni EF Core, mai a mano.
- L'accesso di sviluppo senza credenziali esiste solo in `appsettings.Development.json`, non caricato in Production: verificare che nessun `appsettings.Production.json` lo reintroduca.

## 5. Audit trail (tracciabilità operatori)

Esiste ed è attivo. Tabella **`log.Audit`** in sola aggiunta (nessun update/delete), scritta da `IServizioAudit`: registra data/ora UTC, utente (Id + email), IP, area funzionale, azione, esito, dettaglio JSON e **versione dell'applicazione**. La scrittura non fa mai fallire l'operazione principale.

**Copertura (agosto 2026):** estesa a tutti i moduli con azioni operatore — DVR, Laboratori esterni, Tamponi, Accettazione (salva/elimina/tracciato/PDF), Proposta controlli (salva/elimina/XML), Certificati esterni Non Ricertificati e Non fatturare (export), Piscine (salva/documenti), Taratura/Incubatori/Mappatura/Bilance (salva/validazione/analisi AI), Monitoraggio Econorma (validazioni/mappatura), Da Validare (analisi AI singola/massiva), Scadenziario (salva/elimina/svuota/import), Materie prime (import/estrazione AI/salva/elimina), Ricette (salva/verifica AI). Restano fuori solo le schermate di sola lettura (Consultazione, Metodi, Commesse). Nessuna nuova tabella: usa la `log.Audit` esistente. **Va ricompilato** con l'ultima publish.

## 6. Versionamento (git)

Il codice è versionato con **git** nel repository locale (`C:\Sviluppo\LaboratorioAI`). Alla messa in esercizio conviene **fissare uno snapshot etichettato** della versione pubblicata. Comandi pronti (dalla radice del progetto):

```powershell
cd C:\Sviluppo\LaboratorioAI
git add -A
git commit -m "Go-live 1.0.0: Accettazione, Proposta controlli, Certificati esterni non ricertificati, stato Spedito originale; documentazione aggiornata"
git tag -a v1.0.0 -m "Prima messa in esercizio con Palomar e autenticazione Office365"
```

> Backup fisico complementare: `Backup_Su_D.bat` (robocopy verso `D:\BACKUP`), utile come copia off-repo prima di ogni migrazione/rilascio.

Per i **rilasci successivi** (dopo modifiche): backup DB → `git commit`/`tag` della nuova versione → `dotnet publish` Release → copia sul server senza sovrascrivere `appsettings.Production.json` → `iisreset`. Dettaglio in `docs/Messa_in_esercizio.md` §7.
