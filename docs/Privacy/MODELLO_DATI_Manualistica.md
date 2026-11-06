# Modello dati del modulo Manualistica

## Schema `man` — da approvare prima di scrivere le entità

| | |
|---|---|
| **Data** | 1 novembre 2026 |
| **Base** | §4 di `INTEGRAZIONE_Manualistica.md` di Daniele Vaccarini, con le dodici decisioni del 31/10 e le decisioni del §4 della specifica |
| **Migrazione** | Una sola, nome proposto `Manualistica` |
| **Stato** | Proposta. Approvata questa, si scrivono le entità e la migrazione. |

Convenzioni rispettate: tabelle in italiano al plurale, chiavi `Id<Entità>` `INT IDENTITY`, date in UTC, nessuna cancellazione fisica (`Attivo` bit), schema solo con migrazioni EF Core. Tutte le tabelle hanno `CreatoIlUtc` e `CreatoDa` (UPN Entra ID); non li ripeto nelle colonne qui sotto.

**Il modulo nasce generico.** Ogni tabella che riguarda un manuale porta `IdTipoManuale`: la privacy è il primo tipo, piscine e legionella seguono senza toccare lo schema.

---

## 1. Cosa cambia rispetto alla proposta di Vaccarini

| Sua proposta | Qui | Perché |
|---|---|---|
| `man.ImpostazioniModulo` | **Tolta** → `app.Configurazioni` | Le impostazioni di tutti i moduli stanno lì, con la pagina in Amministrazione |
| `man.PubblicazioniCloud` | **Tolta** → `log.RegistroDocumenti` | Esiste già per i documenti consegnati a Palomar, con la verifica che il file ci sia |
| `man.ManualiGenerati.Contenuto` | **Solo percorso e impronta** | I documenti generati stanno sul server, come DVR e piscine |
| `man.Comuni` | **Sospesa** | Prima si verifica se una tabella comuni esista già in ArcLab |
| — | **+ `man.ContrattiFornitura`** | I dati del contratto sono molti, facoltativi e comprendono l'IBAN: tabella a parte, più facile da proteggere |
| — | **+ dimensione attività e regione** | Risposta 5 di Vaccarini: la divisione futura è per macro attività, con differenze regionali |

---

## 2. Modelli controllati

### `man.TipiManuale`
Il tipo di manuale. Righe iniziali: PRIVACY, PISCINE, LEGIONELLA, HACCP, SICUREZZA.

| Colonna | Tipo | Note |
|---|---|---|
| IdTipoManuale | int | chiave |
| Codice | nvarchar(30) | univoco |
| Nome | nvarchar(100) | |
| Ordine | int | |
| Attivo | bit | |

### `man.ModelliDocumento`
Un documento del pacchetto. Non contiene il file: contiene la sua identità e la regola con cui entra nel pacchetto.

| Colonna | Tipo | Note |
|---|---|---|
| IdModelloDocumento | int | chiave |
| IdTipoManuale | int | |
| Codice | nvarchar(60) | REGISTRO, NOMINE, RESP_WEB_AGENCY… |
| Nome | nvarchar(200) | come compare nel pacchetto |
| CondizioneInclusione | nvarchar(200) | null = sempre; es. `VIDEO_O_PISCINA`, `!CONTRATTO_FORNITURA` |
| CodiceAttivita | nvarchar(30) | **null = vale per tutte le attività** |
| SiglaRegione | char(2) | **null = vale per tutte le regioni** |
| Ordine | int | |
| Attivo | bit | |

**Le due colonne nuove** sono il modo meno invasivo di reggere quello che Vaccarini annuncia nella risposta 5. La scelta del modello per una compilazione cerca prima la riga specifica per attività e regione, poi quella generica: è lo stesso meccanismo che oggi distingue Emilia-Romagna e Marche nelle piscine, scritto una volta sola.

### `man.RevisioniModello`
Qui vive il file `.docx`.

| Colonna | Tipo | Note |
|---|---|---|
| IdRevisioneModello | int | chiave |
| IdModelloDocumento | int | |
| NumeroRevisione | int | |
| Stato | tinyint | Bozza · InApprovazione · Approvato · Obsoleto |
| ContenutoDocx | varbinary(max) | il modello marcato |
| ImprontaSha256 | char(64) | |
| NomeFileOriginale | nvarchar(260) | |
| DataEfficacia | date | da quando vale |
| NoteRevisione | nvarchar(max) | cosa è cambiato |
| Attivo | bit | |

Una revisione diventa Approvato quando riceve l'approvazione; l'approvata precedente passa a Obsoleto in automatico. **Una compilazione usa sempre e solo revisioni approvate ed efficaci alla data.**

### `man.ApprovazioniRevisione`

| Colonna | Tipo | Note |
|---|---|---|
| IdApprovazioneRevisione | int | chiave |
| IdRevisioneModello | int | |
| Approvatore | nvarchar(200) | UPN |
| Esito | tinyint | Approvata · Respinta |
| DataUtc | datetime2 | |
| Note | nvarchar(max) | |

Approva **Mirko Maffei**, con una firma sola (decisione del 2/11/2026, che semplifica quella del giorno prima). Vincolo che resta nel database: la stessa persona non esamina due volte la stessa revisione. Chi ha caricato e chi ha approvato restano registrati, anche quando coincidono.

---

## 3. Check list configurabile

### `man.VociChecklist`

| Colonna | Tipo | Note |
|---|---|---|
| IdVoceChecklist | int | chiave |
| IdTipoManuale | int | |
| Codice | nvarchar(60) | corrisponde al tag del modello |
| Etichetta | nvarchar(200) | come si legge a schermo |
| Gruppo | nvarchar(60) | il passo della check list |
| TipoCampo | tinyint | Testo · Data · Tendina · SiNo · Numero · SceltaMultipla |
| Obbligatoria | bit | |
| Ordine | int | |
| Attivo | bit | |

### `man.OpzioniVoce`
Le voci delle tendine, **compresa la gestione delle voci scritte a mano** (risposta 6).

| Colonna | Tipo | Note |
|---|---|---|
| IdOpzioneVoce | int | chiave |
| IdVoceChecklist | int | |
| Valore | nvarchar(400) | |
| Etichetta | nvarchar(400) | |
| Predefinita | bit | proposta all'apertura |
| DaCompilazione | bit | **nata da una voce scritta a mano** |
| IdCompilazioneOrigine | int | da quale compilazione, se `DaCompilazione` |
| ApprovataDa | nvarchar(200) | chi l'ha ammessa nell'elenco |
| ApprovataIlUtc | datetime2 | |
| Ordine | int | |
| Attivo | bit | **false = obsoleta**: resta nei documenti già prodotti, sparisce dalle tendine |

Il giro è questo: il consulente scrive una voce a mano, la compilazione la salva con il contrassegno, il riepilogo prima di generare la elenca, e una pagina di gestione la ammette nell'elenco o la lascia dov'è. Dichiararla obsoleta è un `Attivo = false`, non una cancellazione: i documenti prodotti l'anno scorso devono restare spiegabili.

### `man.FunzioniAziendali`

| Colonna | Tipo | Note |
|---|---|---|
| IdFunzioneAziendale | int | chiave |
| IdTipoManuale | int | |
| Codice | nvarchar(60) | AMMINISTRAZIONE, FRONT_OFFICE… |
| Nome | nvarchar(100) | |
| Facoltativa | bit | Amministrazione e Front Office no |
| Ordine | int | **l'ordine delle lettere di nomina** |
| Attivo | bit | |

### `man.Qualifiche`

| Colonna | Tipo | Note |
|---|---|---|
| IdQualifica | int | chiave |
| Nome | nvarchar(100) | |
| RichiedeNominativo | bit | |
| Ordine | int | |
| Attivo | bit | |

Righe iniziali: Legale Rappresentante (sì), Socio (sì), Coadiuvante familiare (sì), Lavoratore (**no**).

---

## 4. Il lavoro sul cliente

### `man.Compilazioni`
La testata.

| Colonna | Tipo | Note |
|---|---|---|
| IdCompilazione | int | chiave |
| IdTipoManuale | int | |
| CodiceClienteArcLab | nvarchar(30) | null se inserito a mano |
| RagioneSociale, PartitaIva, CodiceFiscale | nvarchar | congelati alla compilazione |
| SedeLegaleIndirizzo, SedeLegaleCap, SedeLegaleComune, SedeLegaleProvincia | nvarchar | separati |
| Email, Pec, Telefono | nvarchar(200) | |
| CodiceAteco, AttivitaEsercitata, SitiWeb | nvarchar | |
| CodiceUnivocoFatturazione | nvarchar(7) | |
| ReferenteNome, ReferenteCognome, ReferenteComeLegaleRappresentante | | |
| NuovoCliente | bit | fa partire l'e-mail alla segreteria |
| DataDocumenti | date | |
| NumeroRevisioneDocumenti | int | |
| CompilazioneManuale | bit | tendine aperte al testo libero |
| EmailCliente, EmailConsulente | nvarchar(200) | |
| NoteAlCliente | bit | le note vanno anche al cliente |
| Stato | tinyint | Bozza · Generata · InAttesaFirma · Inviata · Annullata |
| Attivo | bit | |

### Le tabelle di dettaglio

| Tabella | Una riga per | Colonne principali |
|---|---|---|
| `man.SediCompilazione` | sede del trattamento | CodiceInsediamentoArcLab (null se a mano), Nome, Indirizzo, Cap, Comune, Provincia, Ordine |
| `man.ValoriCompilazione` | risposta della check list | CodiceVoce, Valore, **ScrittoAMano** bit |
| `man.AutorizzatiCompilazione` | autorizzato o gruppo di uguali | IdQualifica, Nome, Cognome (null per i lavoratori), **Quantita** ≥ 1, AccessoImmagini, Ordine |
| `man.AutorizzatiFunzioni` | funzione di un autorizzato | IdAutorizzatoCompilazione, IdFunzioneAziendale |
| `man.ResponsabiliCompilazione` | lettera di nomina a Responsabile | CodiceModello, Ordine |
| `man.ScelteResponsabile` | scelta dentro una lettera | IdResponsabileCompilazione, CodiceCampo, Valore |
| `man.NoteCompilazione` | nota di una sezione | Sezione, Testo |

**Nome e cognome dei lavoratori non si chiedono e non si salvano** (decisione del 22/10 di Vaccarini): si indicano quantità e qualifica, i nominativi si scrivono a mano sui fogli stampati. Per questo non serve nessuna anonimizzazione programmata. I nomi di legale rappresentante, soci e coadiuvanti sono dati societari pubblici e restano.

### `man.ContrattiFornitura`
Separata perché facoltativa e perché contiene l'IBAN del cliente.

| Colonna | Tipo | Note |
|---|---|---|
| IdContrattoFornitura | int | chiave |
| IdCompilazione | int | |
| PrezzoDocumentazione, PrezzoAssistenza | decimal(10,2) | predefiniti 300 e 200, modificabili |
| AssistenzaAnnuale | bit | |
| Pagamento | tinyint | Bonifico · RicevutaBancaria · SceltoDalCliente |
| RibaIstituto, RibaAgenzia, RibaIban | nvarchar | ABI e CAB si ricavano dall'IBAN |
| Sdi, Telefono, EmailDocumentazione | nvarchar | |
| ConsensiProfilazione, ConsensiPresenza, ConsensiFotoVideo, ConsensiPromozionali | tinyint | Si · No · SceglieIlCliente |
| ConsulenteNome, ConsulenteCellulare, ConsulenteEmail | nvarchar | congelati: devono coincidere con la firma |

### `man.IntestatariContratto`
Quando il contratto è intestato a un soggetto diverso dal titolare della documentazione.

IdCompilazione, PartitaIva, RagioneSociale, Indirizzo, Cap, Comune, Provincia, CodiceFiscale, CodiceClienteAzw (null se a mano).

---

## 5. Firme

### `man.FirmeConsulenti`
L'archivio protetto delle firme dei consulenti, legate all'utente.

IdFirmaConsulente, UpnUtente, ImmaginePng varbinary(max), Cellulare, CaricataIlUtc, CaricataDa, Attiva.

Accesso alla sola persona e agli amministratori; ogni uso in un documento registrato in `log.Audit`. È qui che finisce la firma che oggi sta dentro `firme.js`.

### `man.FirmeCompilazione`
La firma usata in una compilazione: l'immagine si congela, perché il documento prodotto deve restare uguale a sé stesso anche se l'archivio cambia.

IdCompilazione, Ruolo (Consulente · LegaleRappresentante), NomeFirmatario, ImmaginePng, ImprontaSha256, Modo (Autografa · Codice), AcquisitaIlUtc, AcquisitaDa.

Si salva solo l'immagine: nessun dato di pressione, velocità o tempi. Non è firma grafometrica e non si trattano dati biometrici.

---

## 6. Produzione e consegna

### `man.ManualiGenerati`

| Colonna | Tipo | Note |
|---|---|---|
| IdManualeGenerato | int | chiave |
| IdCompilazione | int | |
| CodiceModello | nvarchar(60) | |
| NumeroLettera | int | per le lettere ripetute |
| NomeFile | nvarchar(260) | |
| Formato | tinyint | Docx · Pdf |
| PercorsoArchivio | nvarchar(500) | il file sta sul server |
| ImprontaSha256 | char(64) | |
| GeneratoIlUtc, GeneratoDa | | |

### `man.ManualiGeneratiRevisioni`
Il legame fra documento prodotto e revisione del modello usata. **È la tabella che risponde alla domanda «con quale versione del modello è stato fatto questo documento?»**, e quindi la ragione per cui i modelli stanno nel database.

IdManualeGenerato, IdRevisioneModello.

### `man.InviiDocumenti`

| Colonna | Tipo | Note |
|---|---|---|
| IdInvioDocumenti | int | chiave |
| IdCompilazione | int | |
| Tipo | tinyint | Cliente · Consulente · NuovoCliente · PromemoriaCodice · AvvisoCodiceScaduto |
| Destinatari | nvarchar(1000) | |
| Formati | nvarchar(50) | |
| **InviareDopoUtc** | datetime2 | il ritardo di sei ore |
| Stato | tinyint | Programmato · InAttesaFirma · Inviato · Annullato · Errore |
| InviatoIlUtc, AnnullatoDa, AnnullatoIlUtc | | |
| IdMessaggio | nvarchar(200) | identificativo Brevo |
| Esito | nvarchar(max) | |

Lo stato sta nel database, non in memoria: l'invio programmato deve sopravvivere a un riavvio dell'applicazione.

---

## 7. Permessi

Da aggiungere **sia** a `Permessi.Tutti` **sia** a `DatiIniziali.Catalogo()`:

```
manualistica.consulta           vedere compilazioni e documenti prodotti
manualistica.genera             compilare, generare, inviare            → ruolo Consulente
manualistica.modelli.gestisci   caricare revisioni dei modelli
manualistica.modelli.approva    approvare                               → Maffei
manualistica.impostazioni       pannello di controllo e voci delle tendine
```

---

## 8. Quello che non è ancora deciso

1. **Tabella dei comuni.** Da verificare se ArcLab ne abbia già una utilizzabile. Se no, seed con migrazione da fonte ISTAT, verificando licenza e aggiornamento.
2. **Attività e regione** (§2): la proposta delle due colonne va confermata da Vaccarini, che sa come si divideranno davvero le macro attività.
3. **Migrazione dei dati esistenti**: le schede piscina in `app.DvrPiscine` e il registro dei DVR prodotti. Da decidere prima del rilascio del tipo PISCINE, non adesso.

---

## 9. Il comando, quando si approva

Con Laboratorio AI chiuso:

```powershell
cd C:\Sviluppo\LaboratorioAI
dotnet ef migrations add Manualistica --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
dotnet ef database update --context ContestoLaboratorioAI --project src\LaboratorioAI.Dati --startup-project src\LaboratorioAI.Web
dotnet build
```

---

*MARE.A srl — Cattolica (RN) — novembre 2026*
