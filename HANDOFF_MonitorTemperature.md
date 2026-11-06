# HANDOFF — Integrazione modulo MonitorTemperature in LaboratorioAI

> Nota di consegna per una **nuova chat** (stesso progetto claude.ai "Laboratorio AI").
> Come iniziare: apri una nuova conversazione nel progetto e scrivi
> *"Leggi HANDOFF_MonitorTemperature.md e procedi con l'integrazione"*.
> Leggere PRIMA anche `HANDOFF_LaboratorioAI.md` (architettura, convenzioni, regola read-only).

## Obiettivo

Integrare le funzionalità **web** del progetto `MonitorTemperature` dentro `LaboratorioAI`,
così l'utente usa **un'unica applicazione** e non serve più avviare un sito separato.
Piano già **analizzato e approvato** con l'utente (18-20/09/2026). Nessuna modifica ancora effettuata.

## Regole invarianti (NON violare)

- `LabAnalisiDati` (ArcLab) è **sempre e solo READ ONLY**: nessuna INSERT/UPDATE/DELETE/DDL.
- **NON** integrare `EconormaAcquisizione`, programmi di acquisizione, task schedulati o accesso diretto
  al SQLite Econorma. Quel software resta separato e continua a scrivere nel DB SQL `MonitorTemperature`.
- **NON** modificare struttura/dati/logica del DB `MonitorTemperature` se non strettamente necessario.
- Le misure corrette già presenti in `Misure1` **non** vanno ricalcolate dalla web app.
  (`Misure`=originali, `Misure1`=controllo/corrette, `Misure3`=regole correzione, `StoricoSerialiSonde`=storico seriali).

## Percorsi

- Sorgente da cui attingere: `C:\Users\mirko.maffei\source\repos\MonitorTemperature` (progetto `MonitorTemperature`, ASP.NET Core MVC net8.0).
- Destinazione (SORGENTE vero di LaboratorioAI): `C:\Sviluppo\LaboratorioAI`, web in `src\LaboratorioAI.Web`.
- ATTENZIONE: `C:\Sviluppo\Publish\Web` è **solo una pubblicazione** (DLL/exe) — non lavorare lì.

## Decisioni approvate dall'utente

1. **Approccio**: modulo come **Area** dentro `LaboratorioAI.Web` (`Areas/MonitorTemperature`),
   accesso dati in `LaboratorioAI.Dati` (secondo DbContext read-only). Unica app, modulo modulare e riconoscibile.
2. **Funzioni incluse**: Monitor generale, Monitor, Monitor Controllo, Grafici (originali),
   Grafici1 (controllo), Differenze + **Sincronizzazione sonde da ArcLab** (lettura sola LabAnalisiDati).
3. **Escluse**: configurazione/storico seriali sonde (non ora), tutto ciò che è acquisizione/correzione/diagnostica.

## Connection string (già centralizzata — NON hardcodare)

Già presente in `src\LaboratorioAI.Web\appsettings.json` e `appsettings.Development.json`:
```
"MonitorTemperature": "Server=PCMAREA01\\SQLEXPRESS;Database=MonitorTemperature;Trusted_Connection=True;TrustServerCertificate=True;Encrypt=False;"
```
Futuro passaggio a produzione = **solo** cambiare `PCMAREA01\SQLEXPRESS` → `SRVMAREA\SQLEXPRESS` in appsettings.
La connstring ArcLab read-only esiste già come `"ArcLab"` (riusare quella per la sync sonde, invece di
`"ArcLabReadOnly"` del progetto originale).

## Struttura target

```
src/LaboratorioAI.Web/Areas/MonitorTemperature/
  Controllers/
    MonitorController.cs          (Index=Monitor generale/dashboard, Monitor, Storico — ESTRATTI da HomeController)
    MonitorControlloController.cs (da Monitor1Controller)
    GraficiController.cs          (grafici originali / Misure)
    Grafici1Controller.cs        (grafici controllo / Misure1)
    DifferenzeController.cs       (differenze + soglia + logica congelatori)
    SondeController.cs           (sync sonde da ArcLab, read-only)
  Views/
    Monitor/{Index,Monitor,Storico}.cshtml
    MonitorControllo/Index.cshtml
    Grafici/Index.cshtml  Grafici1/Index.cshtml  Differenze/Index.cshtml
    Sonde/{Index,...}.cshtml
    _ViewImports.cshtml  _ViewStart.cshtml   (usare il _Layout di LaboratorioAI)
src/LaboratorioAI.Dati/MonitorTemperature/
  ContestoMonitorTemperature.cs   (era MonitorDbContext; read-only; DbSet Misure, MappaSonde, viste)
  Models/  Misura, MappaSonda, StatoAttualeDashboard, StoricoTemperatura, DifferenzaTemperatura, SondaArcLab
  RepositoryArcLabSonde.cs        (era ArcLabReadRepository; usa connstring "ArcLab")
```
Namespace dedicato `LaboratorioAI.*.MonitorTemperature` per evitare qualsiasi collisione.
Rotte risultanti: `/MonitorTemperature/Monitor`, `/MonitorTemperature/Grafici`, `/MonitorTemperature/Grafici1`,
`/MonitorTemperature/Differenze`, `/MonitorTemperature/MonitorControllo`, `/MonitorTemperature/Sonde`.

## File da PORTARE (dal sorgente MonitorTemperature)

Controller (adattare, NON copiare l'HomeController in blocco):
- `Controllers/GraficiController.cs`, `Grafici1Controller.cs`, `DifferenzeController.cs`, `Monitor1Controller.cs`
- da `Controllers/HomeController.cs` solo le azioni: `Index` (dashboard), `Monitor(...)`, `Storico(...)`
  → nel nuovo `MonitorController`. (Per la sync sonde: `SondeArcLab`, `TestArcLab`, `SincronizzaSonde` → `SondeController`.)

Modelli (`Models/`): `Misura.cs`, `MappaSonda.cs`, `StatoAttualeDashboard.cs`, `StoricoTemperatura.cs`,
`DifferenzaTemperatura.cs`, `SondaArcLab.cs`.

Dati: `Data/MonitorDbContext.cs` (→ `ContestoMonitorTemperature`), `Repositories/ArcLabReadRepository.cs`
(→ `RepositoryArcLabSonde`, connstring "ArcLab").

Viste: `Views/Grafici/Index.cshtml`, `Views/Grafici1/Index.cshtml`, `Views/Differenze/Index.cshtml`,
`Views/Monitor1/Index.cshtml`, `Views/Home/{Index,Monitor,Storico,SondeArcLab,SincronizzaSonde}.cshtml`
→ ri-vestite col `_Layout` di LaboratorioAI. La logica JS dei grafici (Chart.js) va mantenuta invariata:
filtri per sonda, intervallo Da/A, pulsanti 24h/48h/7g/30g/Tutto, limiti min/max, tooltip data+ora,
stampa/PDF A4 orizzontale, soglia differenze, logica congelatori.

## File/parti da NON portare

Progetto `EconormaAcquisizione`; controller `Importazione`, `MigrazioneStorico`, `Pulizia`,
`CorrezioneMisure1` (niente ricalcolo Misure1), tutti i `Configurazione*`, i `Diagnostica*`,
`ControlloMisure`, `DiagnosticaMisure1`, e `UltimeTemperatureEconorma` (legge SQLite Econorma).
Non portare bootstrap/jquery/validation di MonitorTemperature (LaboratorioAI ha già il suo front-end).

## Conflitti / attenzioni

- **Nessun conflitto** di nomi model verificato (Misura/MappaSonda/… non esistono in LaboratorioAI);
  isolare comunque nel namespace MonitorTemperature.
- **Nessuna Area** esistente: spazio pulito. Va abilitato il routing delle Aree in `Program.cs`.
- Sovrapposizione solo **funzionale** (non di codice) con la feature esistente "TempLog / Monitoraggio Econorma"
  (`TempLogController`, `LettoreDbTempLog`): sono cose diverse, convivono. Da NON confondere/unire.
- LaboratorioAI legge già dal DB `MonitorTemperature` via Dapper (LettoreDbTempLog): il nuovo
  `ContestoMonitorTemperature` (EF, sole viste/SELECT) è indipendente e non interferisce.

## Dipendenze NuGet

**Nessuna nuova**: LaboratorioAI ha già `Dapper` ed EF Core SqlServer. `Microsoft.Data.Sqlite` NON serve.
Unica libreria front-end necessaria: **Chart.js** (aggiungere via CDN nelle viste dell'Area, o un singolo file
in `wwwroot`). Verificare in fase di porting come le viste originali caricano Chart.js.

## Cablaggio in LaboratorioAI (riferimenti esatti)

- `Program.cs` (`src\LaboratorioAI.Web`):
  - registrare il 2° DbContext: `builder.Services.AddDbContext<ContestoMonitorTemperature>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("MonitorTemperature")))` — **solo lettura**, MAI `MigrateAsync` su questo contesto.
  - registrare `RepositoryArcLabSonde` (Scoped).
  - abilitare le Aree nel routing, prima della route default (riga ~293):
    `app.MapControllerRoute(name:"aree", pattern:"{area:exists}/{controller=Monitor}/{action=Index}/{id?}");`
- Autorizzazione: attributo esistente `RichiedePermessoAttribute`
  (`src\LaboratorioAI.Web\Autorizzazione\RichiedePermessoAttribute.cs`). Usare sui controller dell'Area:
  `[Area("MonitorTemperature")] [RichiedePermesso(Permessi.MonitorTemperature)]`.
- Enumerazioni (`LaboratorioAI.Core/Enumerazioni/Enumerazioni.cs`): aggiungere
  `MonitorTemperature = 24` sia in `enum Permessi` (ultimo attuale: Preventivi=23) sia in `enum AreaFunzionale`
  (ultimo attuale: Preventivi=23). Assegnare il permesso ai ruoli in `DatiIniziali.cs` (seed RBAC).
- Menu: `src\LaboratorioAI.Web\Views\Shared\_Layout.cshtml` — aggiungere una sezione/voce
  "Monitor Temperature" con `@if (Utente.HaPermesso(Permessi.MonitorTemperature)) { ... }`, sul modello
  della voce esistente "Monitoraggio Econorma" (riga ~103) e degli altri gruppi di menu.

## Verifica finale attesa

In sviluppo, aprire LaboratorioAI e vedere i dati reali di `PCMAREA01\SQLEXPRESS\MonitorTemperature`
alle rotte `/MonitorTemperature/...`. Il passaggio in produzione dovrà richiedere essenzialmente solo il
cambio connstring a `SRVMAREA\SQLEXPRESS`. Build sul PC dell'utente (nel sandbox non c'è l'SDK .NET);
commit a fine lavoro.

## Domande aperte (da confermare a inizio nuova chat)

- Mapping etichette menu: "Monitor generale" = dashboard `Index`, "Monitor" = `Monitor`,
  "Monitor Controllo" = `Monitor1`. Confermare i nomi da mostrare a menu.
- La sync sonde ArcLab va esposta a tutti o solo ad Amministrazione? (permesso separato?).

---

## Stato: integrazione completata (20/09/2026)

Portata come Area `MonitorTemperature` dentro `LaboratorioAI.Web`. File creati/modificati:

- `src/LaboratorioAI.Web/Areas/MonitorTemperature/Controllers/`: MonitorController (Index/Monitor/Storico),
  MonitorControlloController (da Monitor1), GraficiController, Grafici1Controller, DifferenzeController,
  SondeController (SincronizzaSonde da ArcLab).
- `src/LaboratorioAI.Web/Areas/MonitorTemperature/Views/`: Monitor, MonitorControllo, Grafici, Grafici1,
  Differenze, Sonde + `_ViewImports.cshtml`/`_ViewStart.cshtml`.
- `src/LaboratorioAI.Dati/MonitorTemperature/`: `ContestoMonitorTemperature.cs` (EF, sola lettura, MAI
  migrato), `Models/` (Misura, MappaSonda, StatoAttualeDashboard, StoricoTemperatura, DifferenzaTemperatura,
  SondaArcLab), `RepositoryArcLabSonde.cs` (usa `IFabbricaConnessioniArcLab`, connstring "ArcLab").
- `Program.cs`: registrato `ContestoMonitorTemperature` (no MigrateAsync) e `IRepositoryArcLabSonde`;
  abilitato il routing delle Aree.
- `Permessi.cs` / `Enumerazioni.cs` / `DatiIniziali.cs`: aggiunto un solo permesso
  `Permessi.MonitorTemperatureVisualizza` (`monitortemperature.visualizza`), area funzionale
  `AreaFunzionale.MonitorTemperature = 24`. Nota: `Permessi` è una classe di costanti stringa, non un enum
  int come indicato nell'handoff originale — l'enum int è stato aggiunto solo per `AreaFunzionale`.
- `_Layout.cshtml`: voce di menu "Monitor Temperature" nel gruppo Strumenti.

Scelte fatte per le domande aperte del punto 11:
- Un solo permesso per tutto il modulo, inclusa la sincronizzazione sonde (non separato per Amministrazione).
  Se serve differenziare, si assegna in seguito ai ruoli come per gli altri moduli.
- Etichette di menu: un solo link "Monitor Temperature" (verso la dashboard); la navigazione fra
  Grafici/Grafici1/Differenze/MonitorControllo resta interna alle pagine, come nell'app originale.

Esclusioni rispetto al codice sorgente (confermate, non ancora fatte / scartate volutamente):
- Nessun accesso diretto al SQLite Econorma (`C:\Econorma\Temp\database.db`): la parte di
  `SincronizzaSonde` che leggeva i seriali da Econorma NON è stata portata.
  `UltimeTemperatureEconorma`, `TestEconormaDb`, `TestArcLab`, `TestVistaStorico`, `TestSondeMonitor`
  (azioni diagnostiche) non sono state portate.
- `EconormaAcquisizione` e tutti i controller di configurazione/diagnostica/correzione non toccati.

Da fare (sul PC dell'utente, qui non c'è l'SDK .NET):
- `dotnet build` dalla cartella `C:\Sviluppo\LaboratorioAI` e correggere eventuali errori di compilazione
  residui (revisione statica fatta, ma senza build reale).
- Verificare a video le rotte `/MonitorTemperature/Monitor`, `/Grafici`, `/Grafici1`, `/Differenze`,
  `/MonitorControllo`, `/Sonde` con dati reali di sviluppo.
- Decidere se assegnare `Permessi.MonitorTemperatureVisualizza` anche a ruoli diversi da Amministratore.
- Commit a fine lavoro.

## Sessione 2 — redesign multi-monitor (2026-09-20)

Su richiesta dell'utente (confronto con l'app standalone originale su
localhost:7239, 7 screenshot allegati), il modulo è stato riorganizzato in
4 pagine sotto un nuovo gruppo di menu **"Monitor Econorma"**:

1. **Monitor generale** (`MonitorController.Monitor`, tabella `Misure`) —
   grafico combinato di tutte le sonde + selettore singola sonda. Invariato
   nella logica, solo aggiunto pulsante Esporta Excel.
2. **Monitor Controllo** (ex `Grafici1Controller`, tabella `Misure1`) —
   **rinominato/unificato** in `MonitorControlloController` + vista
   `Views/MonitorControllo/Index.cshtml`. Riusa lo stile a mosaico colorato
   (linee blu/rosso/arancio, limiti tratteggiati) della pagina "Monitor
   Grafici". **Nuova funzione**: cliccando una card si ricarica la pagina
   (postback server-side, GET con `sondaSelezionata=<codice>`) mostrando
   sotto la griglia la tabella storico di dettaglio (Data/Ora, Sonda, Temp.
   grezza, Offset, Temp. corretta, Limiti, Stato) per quella sola sonda,
   sempre su `Misure1`. Il vecchio `Grafici1Controller`/`Views/Grafici1` è
   stato rimosso: le sue funzionalità sono confluite qui.
3. **Monitor Differenze** (`DifferenzeController`, confronto `Misure` vs
   `Misure1`) — già colorato correttamente (non era necessario ricolorare
   come temuto dal confronto con l'app legacy grigia); aggiunta solo
   l'esportazione Excel. Query di filtro estratta in un metodo privato
   condiviso `InterrogaAsync` per evitare duplicazione tra `Index` e
   `EsportaExcel`. Soglia differenza °C invariata.
4. **Monitor Grafici** (`GraficiController`, tabella `Misure`) — invariato
   nella grafica, aggiunto pulsante Esporta Excel e titolo pagina
   allineato ("Monitor Grafici").

La dashboard (`MonitorController.Index`, stat cards + tabella) e lo
storico di dettaglio per apparecchiatura (`MonitorController.Storico`)
restano raggiungibili dal pulsante "Dashboard"/click sull'apparecchiatura
presenti in tutte le pagine, ma non hanno una voce propria nel menu
principale (non richiesta esplicitamente).

**Esportazione Excel** (nuova, non esisteva né nell'app originale né nella
prima integrazione): aggiunta una classe condivisa
`LaboratorioAI.Documenti.EsportatoreMonitorTemperature` (interfaccia
`IEsportatoreMonitorTemperature`, registrata singleton in `Program.cs`),
sul modello di `EsportatoreNonFatturare`/`StileFoglio` già in uso nel
progetto. Espone `Genera(...)` per le tabelle storico (Misure/Misure1) e
`GeneraDifferenze(...)` per la tabella differenze. Per usarla,
`LaboratorioAI.Documenti` referenzia ora anche `LaboratorioAI.Dati`
(nessun ciclo: `Dati` non referenzia `Documenti`). Pulsante "Esporta
Excel" aggiunto accanto a "Stampa/PDF" su tutte le pagine del modulo, con
azioni dedicate: `MonitorController.EsportaExcelDashboard/EsportaExcelStorico/EsportaExcelMonitor`,
`GraficiController.EsportaExcel`, `MonitorControlloController.EsportaExcel`,
`DifferenzeController.EsportaExcel`.

**Menu `_Layout.cshtml`**: la vecchia voce singola "Monitor Temperature"
sotto "Strumenti" è stata sostituita da un gruppo dedicato "Monitor
Econorma" con le 4 sottovoci richieste (Monitor generale, Monitor
Controllo, Monitor Differenze, Monitor Grafici).

**Da fare sul PC (nessun SDK .NET disponibile in questa sessione):**
```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet build
```
Revisione statica completa eseguita (parentesi bilanciate, using coerenti,
nessun riferimento residuo a `Grafici1`, DI aggiornata), ma va comunque
verificata con una build reale e un giro sul browser (in particolare il
click sulle card di Monitor Controllo e i pulsanti Esporta Excel).

## Sessione 3 — causa radice della "formattazione brutta" (2026-09-20)

L'utente ha segnalato che bottoni, card, tabelle e badge non avevano
alcuno stile (testi nudi, nessun colore, nessun bordo) nonostante il
codice Razor usasse ovunque classi Bootstrap (`btn`, `card`, `table`,
`badge`, `alert`, `row`/`col-*`, `form-*`). **Causa**: il progetto
LaboratorioAI non include affatto Bootstrap — `_Layout.cshtml` carica
solo `wwwroot/css/sito.css` (design system proprietario dell'app), non
esiste alcuna cartella `wwwroot/lib/bootstrap`. L'app standalone
originale (Bootstrap incluso) mascherava il problema nei suoi screenshot.

**Fix**: aggiunta di Bootstrap 5.3.3 via CDN (jsdelivr, stesso dominio
già usato per Chart.js), ma **scoperto solo per il modulo Econorma**, per
non toccare lo stile delle altre pagine del gestionale:
- `Views/Shared/_Layout.cshtml`: nel `<head>`, se `ViewData["UsaBootstrap"]`
  è `true`, viene incluso il link al CSS di Bootstrap (dopo `sito.css`).
- `Areas/MonitorTemperature/Views/_ViewStart.cshtml`: imposta
  `ViewData["UsaBootstrap"] = true;` per tutte le viste dell'area.

Effetto collaterale atteso (minore, solo estetico): quando si naviga in
una pagina del modulo Monitor Temperature, il reset CSS di Bootstrap
(Reboot) si applica anche a header/nav/footer condivisi in quella
pagina — possono avere un aspetto leggermente diverso (font/margini)
rispetto alle altre pagine dell'app, dove Bootstrap non viene caricato.
Da verificare a video; se disturba, si può sostituire con una copia
locale di Bootstrap "scoped" (namespace CSS) invece del CDN — non fatto
in questa sessione per restare nei tempi.

Con questo fix, badge di stato (OK verde / ALLARME rosso / FERMA
giallo), tabelle con bordi (`table-bordered`), card con bordi colorati
e bottoni tornano visibili come da codice già scritto — non erano
necessarie modifiche funzionali a gran parte delle pagine.

**Altre modifiche mirate di questa sessione:**
- Monitor generale (`Monitor.cshtml`): quando si seleziona "Tutte le
  sonde", ogni linea ora ha un colore diverso da una palette di 12
  colori (prima erano tutte blu) e la legenda è sempre visibile (prima
  disattivata in modalità "tutte le sonde").
- Monitor Grafici (`GraficiController`/`Grafici/Index.cshtml`) e Monitor
  Controllo (`MonitorControlloController`/`MonitorControllo/Index.cshtml`):
  la linea "Temperatura" di ogni grafico ora cambia colore in base allo
  stato della sonda — verde se OK, giallo se FERMA, rosso se ALLARME
  (prima sempre blu fisso `#0047AB`).
- Tabella Differenze (vista "Tabella"): aggiunta classe `table-bordered`.

**Da fare sul PC:**
```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet build
```
poi verificare a video tutte le pagine del modulo (in particolare che
l'header/nav non risultino alterati in modo fastidioso dal CSS di
Bootstrap).

## Sessione 3 — verifica utente (2026-09-20)

Confermato dall'utente via screenshot dopo `dotnet build` sul PC:
- Monitor generale: bottoni, legenda e colori per sonda OK.
- Monitor Controllo: click su una card → storico dettagliato sotto via
  postback (URL `.../MonitorControllo?...&sondaSelezionata=T5#tabella-storico`),
  tabella con bordi e badge OK verde visibili correttamente.
- Bootstrap (CDN, scoped al modulo via `ViewData["UsaBootstrap"]`) non ha
  creato problemi visibili all'header/nav condiviso.

Redesign multi-monitor del modulo Econorma considerato **completo e
verificato** dall'utente ("direi perfetto"). Punti aperti residui del
modulo: nessuno segnalato. Prossimi lavori sul progetto LaboratorioAI
vanno ripresi dalle priorità generali in cima a questo documento (§7.2/§9:
errori 500 su "Da Validare"/"Ricerca campioni", chiave API Anthropic,
primo avvio portale clienti, test DVR, Entra ID/deploy IIS).
