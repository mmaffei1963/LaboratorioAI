# Manualistica Privacy — integrazione in Laboratorio AI

## Documento di specifica (da approvare prima di scrivere codice)

| | |
|---|---|
| **Data** | 29 ottobre 2026 |
| **Committente** | MARE.A srl — Mirko Maffei, Responsabile di Laboratorio |
| **Autore del prototipo** | Daniele Vaccarini (artifact «Privacy Slug – prova», versione 1793270246-954c) |
| **Natura** | Specifica di integrazione. Non è codice e non è una decisione presa: va letta, corretta e approvata. |
| **Stato** | Bozza 1 — da rivedere con Mirko e con Vaccarini |

---

## 1. Che cos'è il lavoro di Vaccarini

Un prototipo **funzionante nel browser** della futura sezione «Manualistica › Privacy» di Laboratorio AI. Non è una presentazione: genera davvero i documenti Word.

È composto da quattro file:

| File | Dimensione | Contenuto |
|---|---|---|
| `index.html` | 87 KB | Interfaccia: check list in 6 passi, pannello impostazioni, pacchetto documenti |
| `motore.js` | 69 KB | Motore di compilazione dei modelli Word (OpenXml lato browser) |
| `modelli.js` | 5,2 MB | **31 modelli Word** marcati, codificati in base64 dentro il file |
| `firme.js` | 123 KB | Firma grafica del consulente e firma del Legale Rappresentante |

### 1.1 I sei passi della check list

1. Cliente, sedi, data e revisione
2. Struttura e finalità
3. Autorizzati al trattamento
4. Responsabili (esterni)
5. Sistemi, archivi e misure di sicurezza
6. Firme, generazione e invio

### 1.2 I 31 modelli

Registro delle attività di trattamento · Nomine degli autorizzati · Formazione degli autorizzati · Disciplinare · Informativa clienti · Informativa lavoratori · Modulo consensi · Informativa curriculum vitae · Responsabile backup e credenziali · Procedura violazioni (data breach) · Istruzioni · DPIA videosorveglianza · Cartello videosorveglianza · Cartello telecamera piscina · Disposizioni cameriere · Disposizioni manutentore · Impegno ditta ai piani · 13 lettere di nomina a Responsabile esterno (commercialista, paghe, software house, web agency, tecnico computer, consulente, consulente MARE.A, impianto videosorveglianza, chiavi elettroniche, stabilimento balneare, experience, ditta esterna servizi ai piani, backup e credenziali) · Contratto di fornitura servizi.

Il pacchetto non è fisso: undici documenti escono sempre, gli altri entrano o escono in base alle risposte della check list (videosorveglianza, piscina, figure presenti, responsabili esterni indicati).

### 1.3 I tag dei modelli — il punto tecnicamente importante

I modelli sono marcati con i **controlli contenuto di Word** (`w:sdt` + `w:tag`), la stessa tecnica già usata in Laboratorio AI per il DVR Legionellosi e per le Piscine. Dall'estrazione dei 31 file risultano **230 tag distinti**, con una convenzione a prefisso:

| Prefisso | Quanti | Funzione |
|---|---|---|
| `CAMPO:` | 148 | Valore semplice da sostituire (es. `CAMPO:CODICE_ATECO`) |
| `SE:` | 31 | Blocco condizionale: resta o sparisce (es. `SE:VIDEO_PRESENTE`) |
| `TESTO:` | 17 | Testo alternativo scelto fra più varianti |
| `RIPETI:` | 14 | Blocco ripetuto (sedi, autorizzati per funzione, archivi, lettere) |
| `LISTA:` | 8 | Elenco puntato generato da una selezione multipla |
| `SEZIONE:` | 7 | Sezione di ambito per funzione aziendale |
| `FIRMA:` | 3 | Punto di inserimento di un'immagine di firma |
| `SPECIALE:` | 2 | Trattamenti particolari (organigramma) |

L'elenco completo è nel file `docs/Privacy/tutti_i_tag.txt`; la ripartizione per modello in `docs/Privacy/tag_per_modello.json`.

**Conseguenza pratica:** la convenzione è già compatibile con l'impostazione di Laboratorio AI. Il motore attuale (`Documenti/Dvr/ControlliContenuto.cs`) riconosce già i controlli per tag; manca la gestione dei blocchi ripetuti e delle liste, che nel DVR non servivano.

---

## 2. Dove si colloca rispetto alla nota di indirizzo

`VISIONE_Manualistica.md` (agosto 2026) prevede una piattaforma a tre strati — **dato / layout / mappatura** — e un percorso in tre tappe: prima estrarre la piattaforma comune da legionella e piscine, poi autocontrollo alimentare, **infine DVR 81 e privacy**.

Il prototipo di Vaccarini arriva quindi fuori ordine. È un fatto, non un problema: la sua struttura (check list → dato unico → modelli marcati → pacchetto condizionale) è esattamente quella della nota di indirizzo.

**Proposta.** Integrare la Privacy **adesso**, come modulo a sé, costruito secondo lo schema che DVR e Piscine già usano: scheda in JSON sul database, modelli Word sul server, mappa dei valori nel codice. Non è lavoro buttato: quando si estrarrà la piattaforma comune, la Privacy sarà il **terzo** dominio da confrontare, con il vantaggio di essere il più ricco dei tre (ripetizioni, condizioni, lettere per persona). La regola di prudenza della nota di indirizzo — astrarre solo ciò che è davvero duplicato — ne esce rafforzata, non indebolita.

*Da confermare da Mirko: se invece si preferisce aspettare la piattaforma, il prototipo resta in attesa e questa specifica va rifatta.*

---

## 3. Come si traduce in Laboratorio AI

| Parte del prototipo | Destinazione in LabAI | Riuso |
|---|---|---|
| `motore.js` | Nuovo `src\LaboratorioAI.Documenti\Privacy\` (generatore + mappa valori + motore condizionale) | Si riusa `Dvr\ControlliContenuto.cs` e `NormalizzazioneTesto.cs`; da scrivere la parte ripetizioni/liste |
| 31 modelli dentro `modelli.js` | File `.docx` in una cartella sul server, come i modelli piscine; percorso in Impostazioni documenti | Stesso schema di `OpzioniDvr.ModelloPer(...)` |
| Check list compilata | Tabella `privacy.CompilazioniPrivacy`, dati in JSON, sullo stampo di `app.DvrPiscine` | Stesso schema: colonne strutturate per elenco e ricerca, JSON per il resto |
| Pannello impostazioni (oggi nel browser) | `app.Configurazioni`, pagina Amministrazione, permesso dedicato | Pattern `ConfigurazioniPreventiviConsulenza.cs` |
| Ricerca cliente per P.IVA | `RepositoryAnagrafiche` (ArcLab) + `AnagraficaAzw.TrovaAsync` (copia AZW) | **Già esistente**, sola lettura |
| Firma del consulente | Dall'utente collegato, archivio protetto | **Da creare** (vedi §5) |
| Firma del Legale Rappresentante con «OTP» | Infrastruttura «Condizioni e firme» già in esercizio | **Già esistente**, vedi §4 |
| Invio e-mail con allegati, ritardo, promemoria | `ServizioEmailSmtp` (Brevo) + pattern inviti/promemoria delle Condizioni | **Già esistente** |
| Interfaccia a 6 passi | Razor + ES module, come le altre pagine | Gran parte del JavaScript è riusabile così com'è |
| JSZip da CDN | Copiato in `wwwroot` (in produzione non si dipende da una CDN) | — |

### 3.1 Permessi da aggiungere

```
privacy.compila        compilare la check list e generare il pacchetto
privacy.registro       vedere il registro delle compilazioni di tutti
privacy.impostazioni   modificare i predefiniti e i prezzi del contratto
```

Da inserire **sia** in `Permessi.Tutti` **sia** in `DatiIniziali.Catalogo()`, secondo la regola dell'handoff. Assegnazione ai ruoli Consulente e Amministratore (da confermare: anche Commerciale, per il contratto?).

### 3.2 Migrazione

Una sola migrazione, nome proposto `ManualisticaPrivacy`, con la tabella `privacy.CompilazioniPrivacy`. Comando che eseguirà Mirko, a Laboratorio AI chiuso:

```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet ef migrations add ManualisticaPrivacy --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
dotnet ef database update --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
dotnet build
```

---

## 4. La firma «OTP»: non va costruita da zero

Nel prototipo la firma con codice OTP è **simulata** e il commento dice «servizio di Laboratorio AI (in sviluppo)». In realtà Laboratorio AI ha già in esercizio, per le Condizioni Generali, tutto ciò che serve:

- pagina `/Firma/{codice}` sul Portale clienti, con apertura obbligatoria dei PDF e firma grafica;
- codici casuali a 256 bit, nel database solo l'impronta, percorsi mascherati nei log;
- ricevuta di approvazione, catena di impronte sugli eventi, trigger contro `UPDATE`/`DELETE`;
- inviti al cliente e promemoria automatici, invio massivo con Brevo.

**Proposta:** la firma del Legale Rappresentante sulla manualistica privacy passa dalla stessa strada, con un tipo di richiesta nuovo nello schema `contratti`, invece di un meccanismo OTP separato. Si eredita il livello di tracciabilità già concordato per le Condizioni e non si mantengono due sistemi di firma diversi.

**Avvertenza da non sottovalutare.** Che valore si attribuisce a questa firma non è una domanda tecnica. Le Condizioni Generali sono passate dal parere legale di Giovanna prima del lancio; per la documentazione privacy, che è il documento con cui il cliente dimostra di aver adempiuto, la stessa verifica va fatta **prima** di mettere la funzione in mano ai consulenti. Da mettere in conto nei tempi.

---

## 5. Punti di attenzione

### 5.1 Dato personale nel prototipo condiviso

`firme.js` contiene l'immagine della firma autografa di Daniele Vaccarini e il suo numero di cellulare, dentro un artifact condiviso. In Laboratorio AI la firma va in un archivio protetto, legata all'utente collegato. **Nel frattempo conviene limitare la condivisione di quell'artifact.**

### 5.2 Il prototipo è tarato su una struttura ricettiva

Funzioni (`Front Office`, `Capo chef`, `Guardiano notturno`, `Cameriera ai piani`, `Capo sala`), ambiti, cartello piscina, stabilimento balneare, ditta esterna servizi ai piani: l'impianto è quello di un hotel. Va deciso se la prima release copre **solo** le strutture ricettive — scelta legittima e più veloce — o se le funzioni diventano una tabella configurabile. Le due strade divergono presto, quindi la decisione va presa adesso.

### 5.3 Riferimenti normativi

Il prototipo produce documentazione privacy. Nel passaggio in Laboratorio AI i riferimenti normativi dei modelli **non vanno toccati dallo sviluppo**: restano responsabilità di chi mantiene i modelli (Qualità e consulenti), come previsto dalla nota di indirizzo per lo strato *layout*. Va però registrata **la versione del modello** usata per ogni documento prodotto, come già si fa per il DVR, altrimenti non si è in grado di dire con quale testo è stato generato un documento consegnato l'anno prima.

### 5.4 Fascicolo unico

La nota di indirizzo chiede un fascicolo unico per insediamento. Anagrafica, sedi e figure che la check list privacy raccoglie sono in larga parte gli stessi dati di legionella e piscine. In questa prima release **non** si unifica (sarebbe la piattaforma, cioè un altro progetto), ma la tabella va pensata in modo da non impedirlo: riferimento a `IdInsediamentoArcLab` come in `DvrPiscina`, snapshot anagrafici congelati, dati nel JSON.

---

## 6. Fasi proposte

| Fase | Contenuto | Esito |
|---|---|---|
| **0 — Decisioni** | §2 (adesso o dopo la piattaforma), §5.2 (solo ricettivo?), §4 (verifica legale della firma) | Questa specifica approvata |
| **1 — Impianto** | Modelli sul server, entità e migrazione, permessi, voce di menu, pagina con i 6 passi, salvataggio della compilazione | Si compila e si salva, non si genera ancora |
| **2 — Motore** | Generatore C# dei 31 modelli: campi, condizioni, testi alternativi, liste, ripetizioni, firme, organigramma | Pacchetto Word scaricabile in ZIP |
| **3 — Collaudo** | Confronto documento per documento con l'uscita del prototipo, su un cliente reale, con Vaccarini | Uscite identiche |
| **4 — Firma e invio** | Richiesta di firma sul Portale, invio al cliente e al consulente, promemoria, registro | Ciclo completo |
| **5 — Contratto** | Contratto di fornitura servizi, prezzi, assistenza annuale, comunicazione nuovi clienti | — |

La fase 2 è la più lunga: è la riscrittura di 69 KB di JavaScript in C#, con un collaudo che deve dimostrare l'identità delle uscite, non la loro somiglianza.

---

## 7. Domande aperte

**Per Mirko**

1. Si procede adesso, prima della piattaforma comune (§2)?
2. Prima release solo per strutture ricettive, o funzioni configurabili (§5.2)?
3. Chi può usare la funzione: solo Consulente e Amministratore, o anche Commerciale per la parte contratto?
4. La verifica legale della firma: la si chiede a Giovanna insieme a qualcos'altro o a parte (§4)?
5. Dove mettere la voce di menu: cartella nuova «Manualistica» (che poi accoglierà DVR 81 e HACCP) o dentro «Consulenza»?

**Per Vaccarini**

6. Esistono ancora i prototipi Python citati in testa a `motore.js` (`gen_generico`, `gen_nomine`, `gen_tabella`, `gen_responsabili`, `organigramma`)? Leggere quelli evita di dedurre le regole dal JavaScript.
7. I 31 modelli `.docx` originali, non quelli estratti dal prototipo: ci sono versioni più recenti?
8. I modelli hanno un numero di revisione e una data di efficacia? Senza, il requisito §5.3 non si può soddisfare.
9. Le voci «scritte a mano» previste dal pannello: vanno proposte per l'aggiunta agli elenchi (quindi una coda da approvare) o restano sul singolo documento?
10. Il ritardo di invio al cliente (6 ore predefinite) e i promemoria giornalieri: sono requisiti confermati o ipotesi del prototipo?

---

## 8. Materiale estratto e dove si trova

| Elemento | Percorso sul PC |
|---|---|
| Questa specifica | `C:\Sviluppo\LaboratorioAI\docs\PROGETTO_Manualistica_Privacy.md` |
| 31 modelli Word estratti dal prototipo | `C:\Sviluppo\LaboratorioAI\docs\Privacy\Modelli\` |
| Elenco dei 230 tag | `C:\Sviluppo\LaboratorioAI\docs\Privacy\tutti_i_tag.txt` |
| Tag per modello | `C:\Sviluppo\LaboratorioAI\docs\Privacy\tag_per_modello.json` |
| Prototipo originale | artifact «Privacy Slug – prova», condiviso da Vaccarini |

I modelli estratti servono all'analisi. **Non sono gli originali di Vaccarini**: prima di costruirci sopra, farseli dare (domanda 7).

---

*MARE.A srl — Cattolica (RN) — ottobre 2026*
