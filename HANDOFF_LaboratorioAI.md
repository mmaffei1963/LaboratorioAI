# Laboratorio AI — Documento di handoff
## Per riprendere il lavoro in una nuova sessione

| | |
|---|---|
| **Aggiornato** | 13 ottobre 2026 (riscrittura completa; sostituisce la versione di agosto e `claude/ripartenza-2026-10-11.md`) |
| **Committente** | MARE.A srl — Mirko Maffei, Responsabile di Laboratorio |
| **Scopo** | Riprendere il lavoro senza aver letto le conversazioni precedenti |

> **Stato sintetico (13/10/2026).** Il sistema è **in produzione** dal 10/10/2026 (https://labai.mare-a.com per i colleghi, https://portale.mare-a.com per i clienti) e viene ripubblicato a ogni blocco di funzioni. In produzione c'è tutto fino al Cruscotto Direzione fase 1. Sono **pronti in sviluppo, da collaudare e pubblicare** insieme: Direzione fase 2 (Andamento territorio), Commento AI e fase 3 (Rendimento commerciali). Lo storico dettagliato di ogni consegna è in `CHANGELOG.md` sul PC.

---

## 1. Nome, scopo e regole invarianti

**Laboratorio AI** (LabAI) è un applicativo **interno** di MARE.A srl, laboratorio accreditato ACCREDIA n° 00729 L (ISO/IEC 17025:2018), Cattolica (RN), con sede anche a Pesaro dal 2022. Affianca il gestionale **ArcLab** e la contabilità **AZW** con consultazioni, controlli, documenti, analisi AI, strumenti per commerciali e consulenti, cruscotto per la direzione e un portale per i clienti. Non è in vendita, non è multi-tenant, non sostituisce ArcLab.

**Regole che non si violano mai**
- **Mai scrivere su ArcLab** (`LabAnalisiDati`) né su AZW: sola lettura, nessuna eccezione. L'account di servizio ha solo `db_datareader` su ArcLab. Quello che va in ArcLab ci arriva tramite **Palomar** (cartelle di prelievo, XML per import supervisionato) o lo inseriscono le persone.
- **Nessuna cancellazione fisica**: campo `Attivo = false`, righe «Annullato» collegate, svuotamento/anonimizzazione dopo i termini.
- **Schema solo con migrazioni EF Core**, che **crea e applica Mirko** con i comandi che gli si danno. Mai toccare il database a mano.
- Tabelle in italiano al plurale, chiavi `Id<Entità>` `INT IDENTITY`, date **UTC** nel database.
- **Segreti** (password SMTP Brevo, `AgenteSegreto`, chiave Anthropic, credenziali `svc_labai` e SUPERUSER del portale): solo in `appsettings.Production.json` di produzione, negli user-secrets di sviluppo o nell'appsettings dell'agente. Mai nel repository, in chat o in memoria; chiedere a Mirko di non incollarli.

## 2. Come lavorare con Mirko

- Mirko non è uno sviluppatore: **italiano semplice**, spiegazioni brevi, **comandi pronti da copiare** (sempre con il `cd C:\Sviluppo\LaboratorioAI` davanti). Rispondere sempre in italiano.
- **«Prima definiamo bene, poi realizzi»**: per una funzione nuova si propone, si fanno le domande che servono, Mirko approva, poi si sviluppa.
- Priorità alla manutenibilità: strumenti .NET comuni, niente soluzioni esotiche.
- Colleghi poco informatizzati: testi delle pagine e delle guide chiari, brevi, «per situazioni».
- Persone di riferimento: Mirko Maffei (socio, responsabile di laboratorio, amministratore di LabAI); Davide Bertozzi (dirige il laboratorio con Mirko, non socio); soci Giuliana Giulini, Marco Sertori, Daniele Vaccarini, Stefano Scaramucci; commerciali Ludovico Zatterin «Ludo» (laboratorio, dal 2025) e Andrea Carloni (sicurezza, HACCP, formazione); **Gen** è il responsabile IT (server, IIS, firewall, permessi).

---

## 3. Funzioni (menu e home «a cartelle»)

Menu in testata e home sono generati dalla stessa lista (`Web\Navigazione\MenuApplicazione.cs`): una pagina nuova compare in entrambi. La home ha cartelle con icona, **contatori «cose da fare»** (`ContatoriHome.cs`), **preferiti** (stellina, `app.PreferitiUtenti`) e **ricerca delle funzioni**. Ogni pagina ha la sua **guida** («? Guida») e la casella **«Chiedi alla guida»** (§5).

| Cartella | Funzioni | Note |
|---|---|---|
| **Ciclo analitico** | Accettazione campioni · Prelievo vocale · Da Validare · Ordini di Accettazione · Certificati esterni · rinomina · Certificati esterni non ricertificati · Laboratori Esterni · Ricerca errori | Accettazione e Prelievo vocale pensati per telefono (Prelievo funziona anche offline, con tendine da ArcLab e inserimento libero). Ordini di Accettazione: **sviluppo sospeso** (probabilmente partiranno da ArcLab). Certificati esterni: nome concordato con Palomar, «solo esterni» e «misti» in cartelle Palomar diverse, annullamento con motivo, registro di controllo. Laboratori esterni: accreditamenti monitorati con l'Agente ACCREDIA (§8), «da sistemare», prove sospese. Ricerca errori: 9 controlli diagnostici su ArcLab. |
| **Ricerche e statistiche** | Consultazione · Certificati emessi · Metodi e Controlli · Commesse · Statistiche | Export «risultati come sul rapporto di prova». Prove in subappalto: conta solo l'accreditamento del laboratorio esterno (mai «accreditato MARE.A»). |
| **Commerciale** | Clienti da seguire · Cambi di gestione · Preventivi analisi · Preventivi consulenza · Condizioni e firme · Proposta controlli · Le mie provvigioni | Clienti da seguire: fatturato di analisi in calo, Legionella, registro azioni, assegnazioni. Cambi di gestione (subentri): sedi perse e nuovo gestore, verifica P.IVA su VIES. Condizioni e firme: revisioni delle Condizioni Generali, Firmatario per cliente, link personale di approvazione sul Portale, ricevuta, invio massivo (max 1000/giorno, Brevo), inviti al cliente e promemoria. |
| **Consulenza** | Preventivi consulenza · Rapportini di consulenza · Attività e pacchetti consulenza | Contratti Word/PDF con analisi di laboratorio nei pacchetti; rapporto di intervento (MOD. CF 05) inviato a Palomar; deleghe operative (Word/PDF, per sede). |
| **Legionella e piscine** | DVR Legionella · DVR Piscine | DVR Legionellosi DOCX singolo e massivo; piscine: DVR, regolamento, manuale ER e registro Marche. |
| **Nutrizionale** | Materie prime · Ricette nutrizionali | Import USDA/CREA/CNF e da scheda tecnica PDF con AI; rapporto anche come scheda di verifica per il committente; Nutrition Facts USA/Canada. |
| **Strumenti e tarature** | Taratura termometri · Taratura incubatori · Mappatura incubatori · Taratura bilance · Scadenziario tarature | Motori validati con test sui fogli storici; validazione del calcolo; eliminazione dal registro con motivo; etichette termometri. |
| **Monitor Econorma** | Monitoraggio Econorma · Monitor generale / controllo / differenze / grafici · Sonde e sincronizzazione | Area MVC `MonitorTemperature` con database proprio `MonitorTemperature` (vedi `HANDOFF_MonitorTemperature.md`); limiti di taratura da ArcLab. |
| **Amministrazione e fatturazione** | Tamponi in contratto · Non fatturare · Provvigioni · Accordi provvigioni · Segnalatori · Compensi segnalatori | Provvigioni calcolate sugli **incassi** AZW con accordi, basi di calcolo, convenzioni, trimestri chiusi e archiviati; vedi `claude/analisi-modulo-provvigioni.md` e `claude/regole-provvigioni-2027.md`. |
| **Direzione** (solo ruolo Direzione) | Indicatori economici · Andamento territorio · Rendimento commerciali · Classificazione voci | Vedi `claude/analisi-cruscotto-direzione.md`. Indicatori come Power BI, riscontrati al centesimo; territorio per sede del cliente e caso Pesaro; costo e rendimento dei commerciali; **Commento AI** su tutte e tre le pagine, con archivio. |
| **Impostazioni e controllo** | Impostazioni documenti · Voci preventivi · Sigle laboratori esterni · Responsabili clienti · Consulenze: documenti e deleghe · Clienti da seguire: soglie e assegnazioni · Utenti e ruoli · Registro attività (audit) · Domande alla guida | Impostazioni documenti contiene cartelle (Palomar, arrivo, processati, archivi), testi e revisione delle Condizioni. |

## 4. Portale clienti

Progetto separato `LaboratorioAI.Portale` (sviluppo `localhost:5081`, produzione https://portale.mare-a.com dietro Cloudflare).
- Accesso con codice contabilità + P.IVA (da ArcLab); sessioni su database con token opaco (header `X-Sessione-Portale`), revocabili; protezione da tentativi ripetuti; credenziale esclusa dai log. Accesso operatore `MAREA` con password da variabile d'ambiente.
- Consultazione di campioni e certificati, filtri (lo Stato viaggia come testo, `JsonStringEnumConverter`); badge «A» di accreditamento solo per le prove interne.
- **Condizioni e firme**: `/Firma/{codice}` (approvazione delle Condizioni Generali, apertura obbligatoria dei PDF, firma grafica facoltativa) e `/Indica/{codice}` (il cliente indica il Firmatario). Codici casuali a 256 bit, nel database solo l'impronta; percorsi mascherati nei log.
- In sviluppo i log compaiono anche nella finestra; `Avvia_Portale.bat` avvia e apre il browser.

## 5. L'AI nel sistema

API Anthropic via `HttpClient` (`IA\ClientAnthropic.cs`; modello predefinito in `OpzioniAnthropic`, configurabile; chiave in `Anthropic:ChiaveApi`). Principi comuni: **archivio prima di chiamare** dove ha senso; ogni chiamata registrata con modello, token e costo stimato; **l'AI commenta o estrae, i numeri li calcola il programma**.

| Dove | Cosa fa |
|---|---|
| Da Validare / Certificati emessi | Analisi dei risultati del campione (giudizio Conforme / Borderline / Non conforme), archivio in `app.Analisi` |
| Certificati esterni | Estrazione dei dati dal PDF del laboratorio esterno |
| Materie prime | Estrazione dei valori nutrizionali dalla scheda tecnica PDF |
| Ricette, tarature | Verifica anti-errore del calcolo |
| Proposta controlli | Proposta del nome del controllo con le sigle dei metodi |
| Prelievo vocale | Struttura il testo dettato in campi del prelievo |
| «Chiedi alla guida» | Risponde solo con il testo delle guide che la persona può vedere (prompt caching), registro `log.DomandeGuida` svuotato dopo 12 mesi |
| Direzione · Commento AI | Commenta i numeri della pagina (con i nomi dei primi 30 clienti per elenco), archivio `dir.CommentiAi` con i dati inviati |

---

## 6. Architettura tecnica

| Livello | Tecnologia |
|---|---|
| Runtime | .NET 8 LTS |
| Web | ASP.NET Core MVC, Razor + ES modules (nessuna pipeline di build, niente npm) |
| Dati | EF Core per il database proprio (migrazioni) + Dapper per ArcLab (sola lettura) |
| Database | SQL Server (produzione su 192.168.23.1 / SRVMAREA; sviluppo `PCMAREA01\SQLEXPRESS`) |
| Documenti | DocumentFormat.OpenXml (Word), PDFsharp/MigraDoc (PDF), ClosedXML (Excel), ExcelDataReader (.xls Econorma), `ConvertitoreWordPdf` comune |
| Log | Serilog su file (`log\laboratorioai-AAAAMMGG.log`) |
| Autenticazione | Entra ID (Microsoft.Identity.Web); in sviluppo «accesso di sviluppo» senza credenziali |
| E-mail | Brevo (SMTP, piano Starter 20.000/mese; dominio mare-a.com autenticato; Reply-To refertazione@) |

**Soluzione** (`C:\Sviluppo\LaboratorioAI\LaboratorioAI.sln`):
```
src/LaboratorioAI.Core/        Entità, modelli, motori di calcolo puri (testati), costanti, interfacce
src/LaboratorioAI.Dati/        EF Core (ContestoLaboratorioAI, migrazioni), Dapper ArcLab, importatore AZW, servizi
src/LaboratorioAI.Web/         App per i colleghi (5080), Areas/MonitorTemperature
src/LaboratorioAI.Portale/     Portale clienti (5081)
src/LaboratorioAI.IA/          Client Anthropic ed estrattori
src/LaboratorioAI.Documenti/   Generatori Word/PDF/Excel (DVR, piscine, offerte, OdA, rapporti...)
tests/LaboratorioAI.Core.Tests/ Test xUnit dei motori
strumenti/AgenteAccredia/      Agente che legge la banca dati ACCREDIA e invia a LabAI (fuori dalla soluzione)
```

**Punti tecnici trasversali**
- Avvio della Web: `MigrateAsync` e poi `Popolamento.EseguiAsync` (permessi, ruoli, profili iniziali). Un errore qui ferma l'avvio (500.30), salvo le assegnazioni iniziali di ruolo che dal 13/10 finiscono solo come avviso nel log.
- **Permessi**: ogni permesso nuovo va aggiunto **sia** a `Permessi.Tutti` (`Core\Costanti\Permessi.cs`) **sia** a `DatiIniziali.Catalogo()`; l'Amministratore riceve in automatico tutto il catalogo. Attributi `[RichiedePermesso]`, `[RichiedeUnoDeiPermessi]`, `[SolaLetturaSenza]`. I permessi si rileggono dal database a ogni richiesta (cache 1 minuto).
- **Audit**: `IServizioAudit.RegistraAsync` in tutti i moduli operativi (`log.Audit`, append-only; IdEntita max 100 caratteri).
- **Registro di controllo** dei documenti consegnati a Palomar: `log.RegistroDocumenti` con verifica che il file sia davvero nella cartella.
- **Guide**: registro in `Web\Navigazione\Guide.cs` (le voci con percorso più specifico prima), testi in `Views\Guide\_<Codice>.cshtml`.
- Razor: una `@` nel testo va scritta `@@`. Nelle viste nuove il compilatore in sviluppo non richiede altro.
- EF Core 8: evitare `array.Contains` su `ReadOnlySpan` nelle query (errore 500 già visto); le liste `List<>.Contains` vanno bene.
- JSON: le tuple non si serializzano; proiettare su oggetti anonimi.

## 7. Modello dati

**Database**
| Database | Uso |
|---|---|
| `LabAnalisiDati` (ArcLab) | Sola lettura |
| `Marea` (portale storico) | Sola lettura (`Entries` dei certificati esterni pubblicati) |
| `LaboratorioAI` | Produzione, lettura e scrittura |
| `LaboratorioAI_Test` | Sviluppo e collaudo |
| `MonitorTemperature` | Monitor Econorma (mai `MigrateAsync` su quel contesto; si scrive solo la mappatura sonde) |
| File AZW (`AZW_07.accdb`) | Letto da copia temporanea dall'importatore (sola lettura) |

**Schemi del database proprio**
| Schema | Contenuto |
|---|---|
| `sec` | Utenti, Ruoli, Permessi, UtentiRuoli, RuoliPermessi |
| `app` | Analisi AI, DVR, piscine, Configurazioni (Impostazioni documenti), accettazione e proposte (staging), preferiti, denominazioni metodi verificate |
| `log` | Audit, RegistroDocumenti, DomandeGuida |
| `portale` | Sessioni e tentativi di accesso |
| `nutri` | Materie prime, ricette, verifiche nutrizionali |
| `taratura`, `bilance`, `tarature`, `templog` | Strumenti, scadenziario esterni, Monitoraggio Econorma |
| `accettazione`, `controlli` | Accettazione e proposta controlli |
| `certificati` | Certificati esterni da importare, sigle laboratori, archivio estrazioni |
| `labesterni` | Accreditamenti monitorati, revisioni, stato ACCREDIA, prove sospese, verifica metodi |
| `preventivi` | Preventivi analisi, voci accessorie e di pagamento |
| `cons` | Preventivi e pacchetti consulenza, rapportini, deleghe, documenti tipo, voci incarico, responsabili cliente |
| `provv` | Commerciali, codici venditore, copie AZW (clienti, fatture, righe, incassi, note), accordi, basi, convenzioni, trimestri, segnalatori |
| `clienti` | Clienti da seguire: azioni, assegnazioni, soglie, copie ArcLab (campioni mensili, insediamenti, prestazioni), subentri, verifiche P.IVA |
| `contratti` | Condizioni Generali: revisioni (PDF e impronta), clausole, firmatari, richieste, eventi (catena di impronte, trigger contro UPDATE/DELETE), inviti |
| `dir` | Direzione: movimenti analitici mensili, classificazione voci, centri, commesse, fatturato per cliente, commenti AI |

**Ultime migrazioni**: … `Subentri`, `PrestazioniENoteRighe`, `CertificatiMistiEAnnulli`, `Direzione` (in produzione), `DirezioneTerritorio`, `DirezioneCommentiAi` (create, da pubblicare).

**Note di schema ArcLab da ricordare** (cause di errori 500 se ignorate)
- `CodLotto` / `CodCampione` sono testo: confronto con interi con `TRY_CONVERT(int, …)`.
- Prelevatore: `Campione.IdPrelevatore → mansione (IdAttività = 1) → Organico`; ricevitore: `Lotto.IdRicevitore → mansione (IdAttività = 2) → Organico`.
- Contratto HACCP: commessa `IdTipoCommessa = 5`, `DescrIncarico LIKE 'Gestione ed assistenza autocontrol%HACCP%'`, con prestazioni; n° tamponi = `DurataPres`.
- Accreditamento esterno: vale solo se il metodo è accreditato presso il laboratorio che ha eseguito la prova (`labEstMetodo.IdLabEsterno = rigametodo.IdLabEsterno`), con `EXISTS`.
- Certificati esterni sul portale: CTE su `Marea` + LEFT JOIN (le EXISTS correlate vanno in timeout), `COLLATE DATABASE_DEFAULT`.
- Scadenziario: ultima taratura `MAX(SchedeIntervento.DataInt)` per piano, `IdAttività` 25/26. Econorma: limiti nel `PianoInterventi` «TARATURA».
- Tipi commessa: 3 analisi, 4 formazione, 5 consulenza; aree della consulenza dalla voce del tipo di incarico (`cons.VociIncarico`).
- In SSMS le query su ArcLab vanno precedute da `USE LabAnalisiDati;`, quelle su LabAI da `USE LaboratorioAI;` (le tabelle hanno lo schema davanti, es. `sec.Ruoli`).

## 8. Fonti e collegamenti esterni

- **AZW**: `Copia.bat` sul PC di Mirko, ogni notte prima delle copie notturne, aggiorna `AZW_07.accdb` (macro Access `mcrAggiornaDBF`) e lo copia in `\\SRVMAREA\marea\IT\MIRKO\Mirko\AZW_07.accdb` (produzione) e `D:\LabAnalisi\mirko.maffei\AZW_07.accdb` (sviluppo, `PercorsoAzw`). L'**importazione AZW** di LabAI (notturna alle 03:30, o con «Importa AZW ora» / «Aggiorna da AZW») copia: clienti con zona e codice venditore, fatture, righe, incassi, note di credito, commesse ArcLab, campioni mensili, insediamenti e prestazioni ArcLab, contabilità analitica e fatturato per cliente (Direzione). Se una parte non si legge, il resto procede e il messaggio lo dice.
- **Palomar**: cartelle di prelievo per certificati esterni (solo esterni e misti), Ordini di Accettazione, rapportini; il codice del lotto e l'import in ArcLab restano a Palomar.
- **Agente ACCREDIA** (`strumenti\AgenteAccredia`): legge la banca dati ACCREDIA (revisioni, stato, prove sospese) e invia a LabAI con `AgenteSegreto`. Da programmare come attività pianificata di Windows.
- **Power BI**: modello `AZW_TS_VERSION.pbit` sullo stesso file AZW; gli indicatori della Direzione ne riproducono le regole.

## 9. Ruoli e permessi

Ruoli **per mestiere** (dall'11/10/2026, gestiti da **Impostazioni → Utenti e ruoli**): Amministratore, Responsabile, Responsabile Qualità, Qualità, Tecnico, Campionatore, Accettazione, Commerciale, Consulente, Socio, Consultazione, **Direzione** (dal 13/10: soci + Bertozzi, permessi `direzione.consulta` e `direzione.gestisci`); Operatore disattivato. Chi entra la prima volta riceve Consultazione; le persone si possono registrare in anticipo per e-mail. Profili iniziali in `Dati\LaboratorioAI\DatiIniziali\ProfiliRuoli.cs` (applicati una sola volta). Almeno un Amministratore attivo; i permessi dei ruoli li cambia solo un Amministratore.

> In produzione il ruolo Direzione esiste ma l'assegnazione automatica alle 6 persone non è avvenuta: va fatta (o verificata) a mano da Utenti e ruoli.

## 10. Ambienti, avvio e pubblicazione

| | Sviluppo | Produzione |
|---|---|---|
| Web | `http://localhost:5080` (`Avvia_LaboratorioAI.bat`) | https://labai.mare-a.com |
| Portale | `http://localhost:5081` (`Avvia_Portale.bat`) | https://portale.mare-a.com |
| Database | `LaboratorioAI_Test` | `LaboratorioAI` |
| Cartella | `C:\Sviluppo\LaboratorioAI` | `\\SRVMAREA\marea\LaboratorioAI\Produzione\Web` e `\Portale` (D:\MAREA\… sul server) |
| Log | console + `log\` | `\\SRVMAREA\marea\LaboratorioAI\Produzione\Web\log\` |

**Pubblicare** (da `C:\Sviluppo\LaboratorioAI`, con LaboratorioAI di sviluppo chiuso):
```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet publish src\LaboratorioAI.Web -c Release -o C:\Sviluppo\Publish\Web
.\Pubblica-Produzione.ps1 -TentativiVerifica 18
```
Oppure `Pubblica-Produzione.bat` (publish + script). Il Portale con `Pubblica-Portale.bat`. Lo script: mette offline l'app (`app_offline.htm`), aspetta che IIS rilasci i file, fa il backup datato in `Produzione\Backup\BUP gg-mm-aa (n)`, copia **esclusi gli `appsettings*.json`** (riprovando i file bloccati), riavvia, verifica il sito e, se non risponde, **ripristina da solo** il backup (senza toccare la cartella `log`). Dettagli in `claude/procedura-deploy-produzione.md`.

**Lezioni del 13/10/2026**: il primo avvio dopo una migrazione può fallire o essere lento; se capita, il log ora resta e si legge in `Produzione\Web\log\`. Per una diagnosi senza toccare la produzione: avviare il pacchetto pubblicato sul PC contro il database di test (`$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet LaboratorioAI.Web.dll --urls http://localhost:5090` dentro `C:\Sviluppo\Publish\Web`). Il registro `stdout` di IIS si attiva con `stdoutLogEnabled="true"` nel `web.config` del pacchetto (da rimettere a false).

## 11. Come si sviluppa e si consegna

- **Fonte di verità del codice**: il PC di Mirko, `C:\Sviluppo\LaboratorioAI` (in sessione collegata: `$HOME/mnt/Sviluppo--LaboratorioAI`). Nello spazio cloud si lavora su una copia per compilare e testare.
- Consegna:
  1. prima di sovrascrivere, confrontare i file del PC con quelli attesi (diff/md5);
  2. pacchetto `.tgz` con `md5.txt` in `C:\Sviluppo\LaboratorioAI\obj\`, estratto in `src\` (e `tests\`), verifica con `md5sum -c`;
  3. voce in `CHANGELOG.md`;
  4. se cambiano le entità, dare a Mirko il comando della migrazione.
- Comandi migrazione (con LaboratorioAI chiuso):
```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet ef migrations add <Nome> --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
dotnet ef database update --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
dotnet build
```
  (`--context` è obbligatorio: nella soluzione ci sono più DbContext.)
- I motori di calcolo stanno in Core come funzioni pure, con test in `tests\LaboratorioAI.Core.Tests`.

## 12. Stato e punti aperti (13/10/2026)

**Da fare subito**
1. Collaudare in sviluppo **Andamento territorio**, **Commento AI** (tutte e tre le pagine) e **Rendimento commerciali**; poi pubblicare insieme. In produzione premere «Aggiorna da AZW» nella Direzione.
2. Assegnare il ruolo **Direzione** alle 6 persone in produzione (se non già fatto).
3. Rendimento commerciali: verificare che le provvigioni non siano già comprese nelle voci di costo ZAT/CAR (doppio conteggio).
4. Classificare le voci AZW senza tipo **ALE** e **GAS** (Classificazione voci).
5. Impostazioni documenti in produzione: cartella di arrivo dei certificati esterni, cartelle Palomar solo esterni / misti, processati esterni / misti.
6. Ripubblicare anche il **Portale** (correzione del filtro Stato, badge accreditamento).

**Prima del lancio delle Condizioni Generali**: parere legale (Giovanna), Rev. 19 definitiva, marca temporale giornaliera; non mettere in coda inviti massivi prima. Evitare campagne massive da aprile a luglio.

**Altri aperti**
- Mail di presentazione ai colleghi (bozza in `claude/ripartenza-2026-10-11.md`, appendice).
- Ricerca errori: valutare le incoerenze prelevatore/vettore (~1.084) e i campioni non pronti (~131) emersi in sviluppo.
- Guide ancora da scrivere: DVR Legionella, Piscine, Monitor Econorma.
- Ordini di Accettazione: sviluppo sospeso.
- Manuali (DVR Sicurezza, Manuale HACCP): modello dati e template dopo il riscontro dei consulenti.
- Rimandati: approvazione obbligatoria delle offerte scontate, riconciliazione Palomar, metodo ArcLab 12846, rotazione `AgenteSegreto`, attività pianificata per l'Agente ACCREDIA, punti aperti delle provvigioni (vedi analisi), «clienti senza approvazione» in accettazione, chi ha accesso RDP al server IIS.
- Direzione: eventuale ripartizione del costo dei commerciali cliente per cliente.

## 13. Percorsi chiave

| Elemento | Percorso |
|---|---|
| Soluzione | `C:\Sviluppo\LaboratorioAI\LaboratorioAI.sln` |
| Storico consegne | `CHANGELOG.md` (radice) |
| Menu e home | `src\LaboratorioAI.Web\Navigazione\MenuApplicazione.cs`, `ContatoriHome.cs` |
| Guide | `src\LaboratorioAI.Web\Navigazione\Guide.cs`, `Views\Guide\_*.cshtml` |
| Permessi | `src\LaboratorioAI.Core\Costanti\Permessi.cs` + `DatiIniziali.Catalogo()` |
| Ruoli iniziali | `src\LaboratorioAI.Dati\LaboratorioAI\DatiIniziali\ProfiliRuoli.cs` |
| Contesto EF e migrazioni | `src\LaboratorioAI.Dati\LaboratorioAI\ContestoLaboratorioAI.cs`, `src\LaboratorioAI.Dati\Migrations\` |
| Importazione AZW | `src\LaboratorioAI.Dati\Provvigioni\ImportatoreAzw.cs` |
| Provvigioni | `Dati\Provvigioni\ServizioProvvigioniLab.cs`, `Core\Provvigioni\Motore*.cs` |
| Direzione | `Core\Direzione\*`, `Dati\Direzione\ServizioRendimentoCommerciali.cs`, `Web\Controllers\DirezioneController.cs` |
| Client AI | `src\LaboratorioAI.IA\ClientAnthropic.cs` |
| Pubblicazione | `Pubblica-Produzione.bat` / `.ps1`, `Pubblica-Portale.bat` |
| Avvio sviluppo | `Avvia_LaboratorioAI.bat`, `Avvia_Portale.bat` |
| Runbook messa in esercizio iniziale | `DEPLOY_LaboratorioAI.md` |
| Monitor Econorma | `HANDOFF_MonitorTemperature.md` |

## 14. Documenti di riferimento nel progetto

- `claude/procedura-deploy-produzione.md` — pubblicazione corrente.
- `claude/analisi-cruscotto-direzione.md` — Direzione (indicatori, territorio, commerciali, Commento AI).
- `claude/analisi-modulo-provvigioni.md`, `claude/regole-provvigioni-2027.md`, `Contesto_modulo_provvigioni.md` — provvigioni e segnalatori.
- `claude/analisi-clienti-da-seguire.md`, `claude/analisi-subentri-e-priorita-clienti.md` — clienti da seguire e cambi di gestione.
- `claude/condizioni-e-firme.md` — Condizioni Generali e firme.
- `claude/analisi-preventivi-consulenza.md`, `claude/analisi-rapportino-consulenze.md` — consulenza.
- `claude/proposta-home-menu-registro-attivita.md` — home e menu.
- `claude/ripartenza-2026-10-11.md` — superato da questo documento (resta per la bozza della mail ai colleghi).

---

*MARE.A srl — Cattolica (RN) — ottobre 2026*
