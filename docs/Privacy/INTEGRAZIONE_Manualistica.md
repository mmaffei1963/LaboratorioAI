# INTEGRAZIONE_Manualistica.md — Modulo Manualistica / Privacy («Privacy Slug»)

> Stato: **PROPOSTA — nessun codice scritto, in attesa di conferma.**
> Aggiornato: 21/10/2026 · Fonte di verità per l'architettura: HANDOFF Laboratorio AI (13/10/2026).
> Destinatario finale: Mirko Maffei, per l'inserimento nella soluzione `C:\Sviluppo\LaboratorioAI\LaboratorioAI.sln`.

---

## 1. Scopo

Generare in automatico il pacchetto di documentazione privacy per strutture ricettive (hotel) partendo da una
check list a tendine compilata dal consulente. Il modulo nasce dentro Laboratorio AI (stesso stack, stesse
convenzioni) come primo tipo del modulo **Manualistica**; i tipi successivi (Legionella, Piscine, HACCP,
Sicurezza) si aggiungono come dati e modelli, senza riscrivere il motore.

Regole invarianti rispettate:
- **ArcLab (LabAnalisiDati) solo in lettura**: anagrafica cliente e insediamenti letti via Dapper con l'account
  `db_datareader`. Nessuna scrittura.
- Tabelle proprie nel database di Laboratorio AI, schema nuovo `man`.
- Nessuna cancellazione fisica (campo `Attivo`), date UTC, schema solo con migrazioni EF Core.

---

## 2. Documenti del pacchetto privacy (modelli in knowledge)

| Codice proposto | File modello | Inclusione nel pacchetto (proposta, da confermare) |
|---|---|---|
| REGISTRO | REGISTRO DELLE ATTIVITA DI TRATTAMENTO.docx | sempre |
| NOMINE | AUTORIZZATI_AL_TRATTAMENTO_Nomine.docx | sempre (lettere duplicate per autorizzato) |
| FORMAZIONE | AUTORIZZATI_AL_TRATTAMENTO_Formazione.docx | sempre |
| DISCIPLINARE | Procedure_di_gestione__Discipinare_autorizzati_al_trattamento.docx | sempre (tabella firme per autorizzato) |
| INF_CLIENTI | INFORMATIVA_clienti.docx | sempre |
| INF_LAVORATORI | INFORMATIVA_lavoratori.docx | sempre |
| CONSENSI | MODULO_CONSENSI.docx | sempre |
| INF_CV | Informativa_per_acquisizione_curriculum_vitae.docx | sempre |
| RESP_BACKUP | Responsabile_backup_e_credenziali.docx | sempre |
| ISTRUZIONI | ISTRUZIONI_DOCUMENTAZIONE.docx | sempre (documento interno per il cliente) |
| DISP_CAMERIERE | Disposizioni_interne_cameriere_ai_piani.docx | solo se le cameriere ai piani **non** sono nominate autorizzate; se sono nominate, il testo è già integrato nella loro lettera di nomina (decisione 21/10/2026) |
| IMPEGNO_DITTA_PIANI (nuovo) | Impegno_riservatezza_-_DITTA_ESTERNA_SERVIZI_AI_PIANI_No piano camere.docx | se servizi ai piani affidati a ditta esterna |
| RESP_DITTA_PIANI (nuovo) | Responsabile_del_trattamento_-_DITTA_ESTERNA_SERVIZI_AI_PIANI_Uso piano camere.docx | se ditta esterna **e** riceve il piano camere con i nominativi |
| DISP_MANUTENTORE | Disposizioni_interne_manutentore.docx | se presente manutentore interno |
| CARTELLO_VIDEO | CARTELLO_VIDEOSORVEGLIANZA.docx | se videosorveglianza generale ≠ nessuna |
| CARTELLO_PISCINA | CARTELLO_TELECAMERA Piscina.docx | se telecamera piscina ≠ nessuna |
| DPIA_VIDEO | DPIA_Videosorveglianza.docx | se c'è videosorveglianza **o** telecamera piscina (22/10/2026: il Registro dichiara «è stata effettuata la DPIA», quindi la DPIA va sempre consegnata insieme) |
| RESP_* (11 modelli, compreso il nuovo RESP_EXPERIENCE) | Responsabile_del_trattamento_-_*.docx | scelta multipla in check list (commercialista, paghe, paghe e commercialista, web agency, software house, tecnico computer, chiavi elettroniche, impianto videosorveglianza, stabilimento balneare, experience, consulente, consulente MARE.A) |

---

## 3. Le «discriminazioni» nei modelli Word (come marcare i documenti)

Il motore deve sapere quali parti togliere, quali duplicare e dove scrivere i dati. Proposta: **marcatori di
testo visibili** scritti direttamente in Word, comprensibili anche a chi non programma.
Nota (22/10/2026): il documento da generare è il **Registro delle attività di trattamento** (e gli altri modelli
privacy). Il generatore del DVR Legionellosi riguarda un'altra materia e non è un riferimento per i contenuti; resta
solo il precedente tecnico indicato nelle istruzioni (Word + OpenXml + mappa dei valori), non vincolante per la marcatura.

| Scopo | Marcatore | Esempio |
|---|---|---|
| Inserire un dato | `{{NomeCampo}}` dentro il testo | `TITOLARE: {{RagioneSociale}}` |
| Tenere un blocco solo se vale una condizione | paragrafo `{{SE CONDIZIONE}}` … paragrafo `{{/SE}}` | `{{SE VIDEO_REGISTRAZIONE}}` … `{{/SE}}` |
| Tenere un blocco se la condizione NON vale | `{{SENON CONDIZIONE}}` … `{{/SENON}}` | testo «dati non conservati» per piscina senza registrazione |
| Duplicare un blocco per ogni autorizzato di una funzione | `{{RIPETI AUTORIZZATI FUNZIONE}}` … `{{/RIPETI}}` | lettera di nomina Front Office ripetuta per ogni addetto |
| Duplicare una riga di tabella | marcatore nella prima cella della riga: `{{RIGA AUTORIZZATI}}` | tabella firme del Disciplinare |

Regole di scrittura: i marcatori di blocco stanno **da soli nel proprio paragrafo**; un blocco può contenere
paragrafi, tabelle e interruzioni di pagina; i blocchi si possono annidare. Il motore controlla i modelli al
caricamento e segnala marcatori aperti e non chiusi o nomi sconosciuti (il modello non passa in approvazione).

Esistono già nel Registro dei campi «Email_titolareamministrazione», «Sito», «CRM», «Wi_Fi»: verranno
gestiti come campi (sia nella forma «…» sia convertiti in `{{…}}`, da decidere).

### Flusso di marcatura concordato (22/10/2026)
1. [nome collega] seleziona in Word le parti interessate e scrive **commenti in linguaggio descrittivo** (es. «togliere se non c'è
   videosorveglianza», «qui il nome del legale rappresentante», «una lettera per ogni addetto al Front Office»).
2. Claude traduce ogni commento in marcatore tecnico — **metodo definitivo (22/10/2026): controlli contenuto di Word con
   etichetta** (`CAMPO:` `TESTO:` `LISTA:` `FIRMA:` `SE:` `RIPETI:` `SEZIONE:` `SPECIALE:`), dettaglio in `claude/MARCATURA_Registro.md` — e
   restituisce il file marcato + una tabella «commento originale → regola applicata» da verificare.
3. I dubbi di interpretazione vengono chiesti prima di marcare; i commenti descrittivi originali restano nello storico.
4. Il modello marcato passa poi nel flusso di approvazione (due approvatori).

### Condizioni disponibili (prima versione)
Derivano dalla check list:
- `VIDEO_GENERALE` = NESSUNA | VISIONE_REG | SOLO_VISIONE | SOLO_REG
- `PISCINA` = NESSUNA | VISIONE_REG | SOLO_VISIONE
- derivate: `VIDEO_PRESENTE`, `VIDEO_VISIONE`, `VIDEO_REGISTRAZIONE`, `PISCINA_PRESENTE`,
  `PISCINA_REGISTRAZIONE`, `QUALSIASI_REGISTRAZIONE`, `QUALSIASI_VISIONE`
- `PIANI` = INTERNE_NOMINATE | INTERNE_NON_NOMINATE | DITTA_ESTERNA (decisione 21/10/2026):
  INTERNE_NOMINATE → sezione Registro + lettera Cameriera ai piani (con Disposizioni integrate), niente DISP_CAMERIERE;
  INTERNE_NON_NOMINATE → solo DISP_CAMERIERE; DITTA_ESTERNA → IMPEGNO_DITTA_PIANI e, se `DITTA_PIANI_RICEVE_NOMINATIVI`,
  anche RESP_DITTA_PIANI
- `SALA` = CAMERIERI_SALA | CAPO_SALA | NESSUNO — alternative tra loro (decisione 21/10/2026): il **Capo sala**
  sostituisce i camerieri di sala quando questi non sono nominati autorizzati. Se SALA = CAMERIERI_SALA si tiene la
  sezione/lettera «Cameriere di sala» e si toglie «Capo sala»; se CAPO_SALA il contrario; se NESSUNO si tolgono entrambe.
  Organigramma del Registro: il riquadro «CAMERIERI DI SALA» riporta «CAPO SALA» quando SALA = CAPO_SALA
  (campo nel riquadro), e va tolto quando SALA = NESSUNO (da risolvere in fase di marcatura: è un disegno con connettori).
- presenza di ciascuna funzione facoltativa: `FUNZIONE_GUARDIANO_NOTTURNO`, `FUNZIONE_CAPO_CHEF`, `CAMERIERE_PIANI_AUTORIZZATE`, `SALA`
  (Amministrazione e Front Office sempre presenti)

Le 8 combinazioni ammesse della struttura base sono validate dal programma (le altre non sono selezionabili):

| # | Videosorveglianza generale | Telecamera piscina |
|---|---|---|
| 1 | visione + registrazione | nessuna |
| 2 | solo visione | nessuna |
| 3 | solo registrazione | nessuna |
| 4 | visione + registrazione | visione + registrazione |
| 5 | solo visione | solo visione |
| 6 | nessuna | visione + registrazione |
| 7 | nessuna | solo visione |
| 8 | nessuna | nessuna |

(Interpretazione dei casi 4 e 5: la modalità tra parentesi vale per entrambi gli impianti — **da confermare**.)

---

## 4. Modello dati proposto (schema `man`, database LaboratorioAI)

Tutte le tabelle: chiave `Id<Entità>` INT IDENTITY, `Attivo` bit, `CreatoIlUtc`, `CreatoDa` (UPN Entra ID).

**Modelli controllati (comuni a tutti i tipi di manuale)**
- `man.TipiManuale` — IdTipoManuale, Codice (PRIVACY, LEGIONELLA…), Nome.
- `man.ModelliDocumento` — IdModelloDocumento, IdTipoManuale, Codice (REGISTRO, NOMINE…), Nome,
  CondizioneInclusione (es. `VIDEO_PRESENTE`), Ordine.
- `man.RevisioniModello` — IdRevisioneModello, IdModelloDocumento, NumeroRevisione, Stato
  (Bozza / InApprovazione / Approvato / Obsoleto), ContenutoDocx (varbinary), ImprontaSha256, NoteRevisione.
- `man.ApprovazioniRevisione` — IdApprovazioneRevisione, IdRevisioneModello, Approvatore (UPN), Esito
  (Approvata / Respinta), DataUtc, Note. Una revisione diventa Approvata solo con **due** approvazioni
  positive di persone diverse; la revisione approvata precedente passa a Obsoleto in automatico.

**Check list configurabile (pannello di controllo)**
- `man.VociChecklist` — IdVoceChecklist, IdTipoManuale, Codice, Etichetta, Gruppo, TipoCampo
  (Testo / Data / Tendina / SiNo / Numero / SceltaMultipla), Obbligatoria, Ordine.
- `man.OpzioniVoce` — IdOpzioneVoce, IdVoceChecklist, Valore, Etichetta, **Predefinita**, Ordine.
- `man.FunzioniAziendali` — IdFunzioneAziendale, IdTipoManuale, Codice, Nome (Amministrazione, Front Office,
  Capo chef, Guardiano notturno, Cameriera ai piani, Cameriere di sala…), QualifichePossibili.
- `man.ResponsabiliCompilazione` — IdResponsabileCompilazione, IdCompilazione, CodiceModello (WEB_AGENCY…), Ordine.
  Una riga = una lettera di nomina (più righe dello stesso modello = più lettere, es. due web agency; predefinito 1).
  I dati del Responsabile non si salvano: li compila a mano il Responsabile (decisione 23/10/2026).
- `man.ScelteResponsabile` — IdSceltaResponsabile, IdResponsabileCompilazione, CodiceCampo (RESP_TRATTAMENTO_1…),
  Valore: scelte delle tendine per lettera.
- `man.ImpostazioniModulo` — IdImpostazioneModulo, Chiave, Valore (es. e-mail consulente predefinita,
  COMPILAZIONE MANUALE attiva sì/no). In alternativa riuso di `app.Configurazioni`: **da concordare con Mirko**.

**Lavoro sul cliente**
- `man.Compilazioni` — IdCompilazione, IdTipoManuale, CodiceClienteArcLab (null se inserito a mano),
  RagioneSociale, SedeLegale, PartitaIva, CodiceFiscale, Email, Pec, Telefono, DataDocumenti,
  NumeroRevisioneDocumenti, CompilazioneManuale (bit), EmailCliente, EmailConsulente, Stato
  (Bozza / Generata / Inviata / Annullata).
- `man.SediCompilazione` — IdSedeCompilazione, IdCompilazione, CodiceInsediamentoArcLab (null se manuale),
  Indirizzo, Cap, Comune, Provincia.
- `man.ValoriCompilazione` — IdValoreCompilazione, IdCompilazione, CodiceVoce, Valore, ScrittoAMano (bit).
- `man.AutorizzatiCompilazione` — IdAutorizzatoCompilazione, IdCompilazione, IdQualifica, Nome (null), Cognome (null),
  **Quantita** (int, ≥ 1), Ordine. Una riga = un autorizzato (o `Quantita` autorizzati identici: stessa qualifica e
  stesse funzioni, tipicamente lavoratori). Ogni unità genera **una lettera di nomina**.
  Nome e Cognome valorizzati **solo** se la qualifica li richiede (allora Quantita = 1); per i Lavoratori sempre null.
- `man.AutorizzatiFunzioni` — IdAutorizzatoFunzione, IdAutorizzatoCompilazione, IdFunzioneAziendale. Una o più
  funzioni per autorizzato (decisione 22/10/2026: chi ha più funzioni riceve **una sola lettera** con tutte).
- `man.Qualifiche` — IdQualifica, Nome, **RichiedeNominativo** (bit). Valori iniziali: Legale Rappresentante (sì),
  Socio (sì), Coadiuvante familiare (sì), Lavoratore (**no**).

**Nominativi degli autorizzati (decisione 22/10/2026, sostituisce la regola di anonimizzazione del 21/10/2026)**
- La check list **non chiede** nome e cognome dei Lavoratori, in nessun documento (Nomine, Formazione, Disciplinare):
  si indica solo quanti sono e la qualifica; i nominativi si scrivono a mano sui fogli stampati.
- Nome e cognome si chiedono solo per Legale rappresentante, Soci e Coadiuvanti familiari (confermato 22/10/2026).
- Conseguenza: il programma non salva dati personali dei lavoratori, quindi **la regola di anonimizzazione a 2 anni
  e l'attività notturna collegata non servono più** (tolte; colonna `AnonimizzatoIlUtc` e flag `Anonimizzabile` eliminati
  dal modello dati). I nominativi di LR e soci sono dati societari pubblici (visura) e restano con la compilazione.

**Firme (decisione 22/10/2026)**
- `man.FirmeCompilazione` — IdFirmaCompilazione, IdCompilazione, Ruolo (Consulente / LegaleRappresentante),
  NomeFirmatario, ImmaginePng (varbinary), ImprontaSha256, AcquisitaIlUtc, AcquisitaDa (UPN), Attivo.
- Acquisizione nella check list con **riquadro di firma a disegno** (mouse, dito o penna su tablet).
- Si salva solo l'immagine della firma, **nessun dato dinamico** (pressione, velocità, tempi): così non è firma
  grafometrica e non si trattano dati biometrici. È una firma elettronica semplice (immagine), non una firma qualificata.
- Accesso alle immagini: solo chi ha `manualistica.genera` sulla compilazione e gli amministratori; ogni uso in un
  documento è registrato in `log.Audit`.
- **Deciso (22/10/2026):** si riusa il componente di «firma grafica» già presente nel Portale
  (`/Firma/{codice}`, Condizioni Generali). **Punto da concordare con Mirko Maffei:** come renderlo disponibile anche
  nella Web (es. spostarlo in una vista parziale / modulo JS condiviso) senza toccare il funzionamento del Portale.
- **Dove si inseriscono le firme (22/10/2026):** solo nel **Registro delle attività di trattamento**:
  firma del consulente nella colonna «Firma revisore» della riga dell'ultima revisione; firma del legale rappresentante
  in «Figure responsabili» e nella colonna «Firma legale rappresentante» della riga dell'ultima revisione.
  Negli altri documenti le firme restano a mano.

**Tracciabilità e consegna**
- `man.ManualiGenerati` — IdManualeGenerato, IdCompilazione, GeneratoDa, GeneratoIlUtc, NomeFile, Formato
  (DOCX / PDF), ImprontaSha256, Contenuto o PercorsoArchivio.
- `man.ManualiGeneratiRevisioni` — legame manuale ↔ IdRevisioneModello di ogni documento usato.
- `man.InviiDocumenti` — IdInvioDocumenti, IdCompilazione, Modalita (Cliente / Consulente), Destinatari,
  Formati, Esito, IdMessaggio, InviatoIlUtc.
- `man.PubblicazioniCloud` — IdPubblicazioneCloud, IdManualeGenerato, Destinazione (Logi), Stato, Esito,
  DataUtc. Vuota finché il collegamento con marea.palomar.srl/logi non esiste.

Migrazione prevista (unica, dopo conferma): `ManualisticaPrivacy`.

---

## 5. Schermate proposte (area Web, colleghi)

**Proposta grafica (23/10/2026, da confermare)**: tela «Check list Privacy – proposta schermate» (artifact Design) con 7
schermate: 1 Cliente, sedi, data e firme · 2 Struttura e finalità · 3 Autorizzati (tabella qualifica / funzioni / nome
solo LR-soci-coadiuvanti / quantità; accesso alle immagini per funzione; incarichi backup e credenziali) · 4 Responsabili
(presente + numero lettere + tendine per lettera) · 5 Sistemi, archivi e misure · 6 Riepilogo, generazione e invio ·
Pannello di controllo (predefiniti, e-mail, COMPILAZIONE MANUALE, modelli e approvazioni). I controlli di coerenza (es.
Sala ↔ Capo sala) bloccano «Genera» finché non sono risolti.

**Correzioni alla proposta (23/10/2026)**
- Sede legale e sedi: CAP, Comune e Provincia in campi separati (`man.Compilazioni`: SedeLegaleIndirizzo,
  SedeLegaleCap, SedeLegaleComune, SedeLegaleProvincia; `man.SediCompilazione` già separata).
- Codice fiscale vuoto → nei documenti si usa la Partita IVA.
- Numero di revisione proposto = ultima revisione riportata nel Registro del cliente + 1 (00 per un cliente nuovo),
  modificabile. Serve lo storico per cliente: lettura dall'ultima compilazione salvata in `man.Compilazioni`.
- Firme (consulente e LR) raccolte alla fine della check list, nel passo 6.
- Tutte le tendine accettano anche testo scritto a mano (campo con elenco di suggerimenti); dove la scelta è multipla
  (archivi, accessi, gestionali, backup, trattamenti e interessati dei Responsabili) caselle con tutte le voci + «altro».
- Autorizzati: a ogni riga (qualifica) si possono aggiungere più funzioni; numero di persone per riga.
- Responsabili con più lettere dello stesso tipo: ogni lettera apre la stessa maschera completa (tutte le voci).
- Backup aziendale: aggiunta l'opzione «NESSUN BACKUP».
- Autorizzati, incarichi particolari: «Sostituto del LR per i data breach» (autorizzati + «Nessun sostituto», predefinito
  nessuno) — 27/10/2026.

1. **Manualistica › Privacy › Nuova compilazione** — scelta del cliente (ricerca da ArcLab, sola lettura) oppure
   «Inserisci a mano»; scelta delle sedi del trattamento (insediamenti ArcLab o manuali).
2. **Check list** — a sezioni: Struttura (videosorveglianza / piscina / camerieri), Dati generali, Autorizzati
   (tabella: funzione, n°, nome, cognome, qualifica), Responsabili del trattamento (scelta multipla), Sistemi e
   archivi, Data e revisione unica. Tendine già precompilate con i valori predefiniti del pannello.
3. **Riepilogo** — elenco dei documenti che verranno prodotti e dei blocchi tolti/tenuti, con le revisioni di
   modello usate. Pulsante «Genera».
4. **COMPILAZIONE MANUALE** (se attiva nel pannello) — testo scritto a mano nelle tendine quando nessuna voce va bene
   (decisione 23/10/2026, vedi §10 punto 3).
5. **Invio** — «INVIO CLIENTE» (PDF al cliente; PDF + Word al consulente) oppure «INVIO CONSULENTE» (solo Word).
6. **Pannello di controllo** — valori predefiniti delle tendine, funzioni aziendali, e-mail predefinite, attivazione
   COMPILAZIONE MANUALE.
7. **Modelli** — caricamento revisioni, controllo marcatori, invio in approvazione, approvazione dei due
   approvatori, storico revisioni consultabile.

---

## 6. Componenti previsti nella soluzione (nessuno ancora creato)

| Progetto | Elementi |
|---|---|
| LaboratorioAI.Core | `Manualistica/` entità, enum Stati, `MotoreCondizioni` (funzione pura: valuta le condizioni dalla check list), `ValidatoreStruttura` (8 combinazioni) |
| LaboratorioAI.Dati | configurazioni EF schema `man`, migrazione `ManualisticaPrivacy`, `ServizioCompilazioni`, `ServizioModelli` (approvazioni), `LettoreClientiArcLab` (Dapper, sola lettura — riuso delle query esistenti se ci sono) |
| LaboratorioAI.Documenti | `Manualistica/GeneratoreManuale` (OpenXml: campi, blocchi SE/SENON, RIPETI, RIGA), `ControlloMarcatori`; PDF tramite `ConvertitoreWordPdf` comune già esistente |
| LaboratorioAI.Web | `ManualisticaController`, `ModelliManualisticaController`, viste `Views/Manualistica/*`, ES module `wwwroot/js/manualistica/checklist.js`, guida `_ManualisticaPrivacy.cshtml` |
| tests/LaboratorioAI.Core.Tests | test del motore condizioni e della validazione delle 8 combinazioni; test del generatore su modelli di prova |

Interfaccia predisposta per il cloud del cliente: `IArchivioDocumentiCliente` con implementazione
`ArchivioNessuno` ora e `ArchivioLogi` in futuro.

## 7. Dipendenze (NuGet)
Nessun pacchetto nuovo previsto: DocumentFormat.OpenXml, Dapper, EF Core, Serilog, client e-mail Brevo e
convertitore PDF sono già nella soluzione.

## 8. Ruoli e permessi (da aggiungere a `Permessi.Tutti` e `DatiIniziali.Catalogo()`)
- `manualistica.consulta` — vedere compilazioni e manuali generati
- `manualistica.genera` — compilare check list, generare, inviare (ruolo Consulente)
- `manualistica.modelli.gestisci` — caricare revisioni dei modelli
- `manualistica.modelli.approva` — approvare (assegnato solo ai due approvatori)
- `manualistica.impostazioni` — pannello di controllo

## 9. Punti di contatto con Laboratorio AI (file esistenti toccati in sola aggiunta)
- `Web\Navigazione\MenuApplicazione.cs` — nuova voce (cartella Consulenza o nuova cartella «Manualistica»: **da concordare con Mirko**)
- `Web\Navigazione\Guide.cs` — nuova guida
- `Core\Costanti\Permessi.cs` e `DatiIniziali.Catalogo()` — nuovi permessi
- `ContestoLaboratorioAI.cs` — nuovi DbSet e configurazioni schema `man`
- `IServizioAudit.RegistraAsync` — generazione, invio, approvazioni
- Servizio e-mail Brevo esistente — invii (limite piano Starter 20.000/mese)

## 10. Punti aperti
1. ~~Check list attuale~~ → ricevuta, mappata in §12; restano 3 domande (§12, fine).
2. ~~Codice del generatore DVR Legionellosi~~ → non necessario (22/10/2026): la marcatura dei modelli si decide sui
   modelli privacy stessi.
3. ~~COMPILAZIONE MANUALE~~ → deciso (23/10/2026): riguarda le **voci delle tendine**. Quando nessuna voce va bene, il
   consulente scrive il testo a mano direttamente nel campo (ogni tendina = campo con elenco di suggerimenti). Attiva/
   disattiva dal pannello; le voci scritte a mano sono salvate con la compilazione (`man.ValoriCompilazione`, flag
   `ScrittoAMano`), elencate nel riepilogo prima di generare e, se l'opzione è attiva, proposte per l'aggiunta
   all'elenco del pannello. Non si modificano i Word dopo la generazione.
4. PDF: richiesto dalla consegna per «INVIO CLIENTE»; confermare l'uso del `ConvertitoreWordPdf` esistente.
5. Inclusione di DISP_CAMERIERE, DPIA_VIDEO e RESP_IMPIANTO_VIDEOSORVEGLIANZA (vedi §2).
6. ~~Conservazione dei nominativi degli autorizzati~~ → superato (22/10/2026): i nominativi dei lavoratori non si
   chiedono né si salvano; niente anonimizzazione (vedi §4).
7. Dove salvare i file generati (database o cartella su server) — **da concordare con Mirko**.
8. Integrazione futura con marea.palomar.srl/logi: modalità tecnica (API? cartella?) da chiedere a Palomar.
9. Modelli: i dati d'esempio («PINCO PALLINO», «Via Carducci, 22», «Rev. 00 del 27/03/2026»,
   «www.standard.com») **restano per ora** (decisione 21/10/2026); si trasformano in campi nella fase di marcatura.
10. Correzioni ai contenuti decise il 21/10/2026 → applicate (§11); il Registro corretto va ricaricato in knowledge.

---

## 11. Registro delle modifiche ai modelli

Ogni modifica a un modello è una nuova revisione del documento controllato. Finché il flusso di approvazione
del modulo non esiste, le modifiche sono annotate qui e confluiranno nella prima revisione caricata.

| Data | Modello | Modifica | Deciso da | Stato |
|---|---|---|---|---|
| 21/10/2026 | REGISTRO | Funzione FRONT OFFICE, tabella «Tipologie di trattamenti»: MODIFICA da SVOLTO a PRECLUSO (coerente con la lettera di nomina Front Office) | [nome collega] | **applicata** 21/10/2026 — da ricaricare in knowledge |
| 21/10/2026 | REGISTRO | Aggiunta sezione «Funzione aziendale: CAPO SALA» (10 pagine, dopo Camerieri di sala): copia integrale della sezione Camerieri di sala con solo il titolo cambiato — stessi dati (nome, cognome, n. camera, art. 9) come da colonna 4 della check list. Indice da aggiornare in Word | [nome collega] | **applicata** 21/10/2026 — contenuto da verificare dagli approvatori |
| 21/10/2026 | NOMINE | Aggiunta lettera «Capo sala – AUTORIZZAZIONE AL TRATTAMENTO» in fondo al file (pag. 9): copia della lettera Cameriere di sala, ambito «FUNZIONE CAPO SALA», tendina funzione = «Capo sala» | [nome collega] | **applicata** 21/10/2026 — contenuto da verificare dagli approvatori |
| 21/10/2026 | NOMINE | Tolta la voce «Back office» dalle 9 tendine «Funzione aziendale» (una per lettera); testo del documento invariato | [nome collega] | **applicata** 21/10/2026 |
| 21/10/2026 | FORMAZIONE | Tolta la voce «Back office» dalle 23 tendine «Funzione aziendale» della tabella firme; testo invariato | [nome collega] | **applicata** 21/10/2026 |
| 21/10/2026 | DISCIPLINARE | Tolta la voce «Back office» dalle 9 tendine «Funzione aziendale» della tabella firme; testo invariato | [nome collega] | **applicata** 21/10/2026 |
| 21/10/2026 | RESP_WEB_AGENCY | Aggiunta voce «GESTIONE MAILING SU PROSPECT / CAMPAGNE PUBBLICITARIE» alle 5 tendine «Descrizione del/i trattamento/i», dopo «GESTIONE NEWSLETTER»; testo visibile invariato | [nome collega] | **applicata** 21/10/2026 |
| 21/10/2026 | RESP_EXPERIENCE (nuovo) | Nuovo modello di nomina per fornitori Experience, stessa struttura dello Stabilimento balneare. «Descrizione del trattamento»: 1 riga con tendina (Public relations · Organizzazione e gestione eventi · Parchi tematici / divertimento · Strutture termali · Noleggi mezzi e veicoli) + 1 riga vuota a compilazione libera. Interessati: Clienti aziendali, dati comuni; trattamento: Utilizzo | [nome collega] | **creato** 21/10/2026 (Responsabile_del_trattamento_-_EXPERIENCE.docx) — da caricare in knowledge e approvare |
| 21/10/2026 | RESP_WEB_AGENCY | Aggiunta voce «PROSPECT» alle 3 tendine «Soggetti interessati», dopo «FORNITORI DI BENI E SERVIZI»; testo visibile invariato | [nome collega] | **applicata** 21/10/2026 |
| 21/10/2026 | RESP_STABILIMENTO_BALNEARE | Tolta la voce «RISTORANTE CONVENZIONATO» dalla tendina «Descrizione del trattamento» (residuo); testo visibile invariato | [nome collega] | **applicata** 21/10/2026 |
| 21/10/2026 | NOMINE | Lettera «Cameriera ai piani»: integrato il testo delle Disposizioni interne (dati dei clienti anche particolari; chiavi elettroniche/passe-partout) dopo l'ambito del trattamento, su una seconda pagina insieme alla tabella firme. Esclusi intestazione, «Qualsiasi altra informazione…» e firma, già presenti nella lettera | [nome collega] | **applicata** 21/10/2026 — superata il 23/10/2026: ogni lettera deve stare in una pagina (compattazione automatica) |
| 21/10/2026 | RESP_DITTA_PIANI (nuovo) | Nuova nomina Responsabile per ditta esterna servizi ai piani: struttura Stabilimento balneare; descrizione = tendina «SERVIZI DI PULIZIA E RIASSETTO CAMERE» + riga libera; interessati Clienti aziendali, dati comuni; trattamenti Utilizzo, Consultazione | [nome collega] | **creato** 21/10/2026 — da approvare |
| 21/10/2026 | IMPEGNO_DITTA_PIANI (nuovo) | Impegno di riservatezza per ditta esterna servizi ai piani: testo concordato (chiavi restituite al Titolare a fine turno; aggiunta procedura oggetti smarriti; mantenuta la frase sulla responsabilità della Ditta) — testo in `claude/bozza_Impegno_riservatezza_DITTA_SERVIZI_AI_PIANI.md` | [nome collega] | **creato** 21/10/2026 in Word con il layout delle nomine Responsabile (Impegno_riservatezza_-_DITTA_SERVIZI_AI_PIANI.docx, 1 pagina) — da approvare |
| 22/10/2026 | NOMINE | Marcatura applicata (vedi `claude/MARCATURA_Nomine.md`): una lettera modello per funzione con `RIPETI:AUTORIZZATI_<FUNZIONE>` (copie = numero di autorizzati in check list); tolte le lettere doppie del modello (2ª Amministrazione «Socio», 2ª Front Office); interruzione di pagina spostata sul titolo di ogni lettera; tabella Titolare in intestazione resa compilabile; riga «Videosorveglianza» a tendina per funzione (predefinita dalle liste del Registro); tutte le sedi operative nella riga dell'intestazione; data e firma a mano; nome e cognome compilati solo per LR/soci/coadiuvanti, a mano per i lavoratori; ambito di ogni funzione marcato `SEZIONE:AMBITO_<FUNZIONE>` per le lettere di chi ha più funzioni | [nome collega] | **applicata** 22/10/2026 (AUTORIZZATI_AL_TRATTAMENTO_Nomine_marcato.docx) — da approvare |
| 23/10/2026 | FORMAZIONE | Marcatura applicata (vedi `claude/MARCATURA_Formazione_Disciplinare.md`): tabella firme ridotta a una coppia di righe `RIPETI:AUTORIZZATI` (tolte le 22 righe d'esempio, due tabelle unificate, intestazione ripetuta); docente = Legale rappresentante; durata = 2 ore (predefinita); «In data» e firme a mano; intestazione Titolare compilabile | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | DISCIPLINARE | Marcatura applicata: tabella firme finale come nel Verbale di formazione (stessi autorizzati, stesso ordine); tolta l'evidenziazione gialla della tabella e quella nel testo (Diritti dell'interessato, punto f) | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | DPIA_VIDEO | Marcatura applicata (vedi `claude/MARCATURA_DPIA_Informative.md`): dati generali dal Registro, sedi ripetute, funzioni che vedono/consultano le immagini dalla check list, parti DVR condizionate a `QUALSIASI_REGISTRAZIONE`, data documenti e riesame (+2 anni) | [nome collega] | **applicata** 23/10/2026 — 2 interpretazioni da confermare |
| 23/10/2026 | INFORMATIVA_CLIENTI | Marcatura applicata (IT ed EN): blocchi videosorveglianza/piscina condizionati, frasi registrazione e conservazione automatiche, finalità facoltative (statistica, profilazione, presenza, foto/video, promozionali) con condizioni | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | INFORMATIVA_LAVORATORI | Marcatura applicata: blocchi videosorveglianza/piscina condizionati, frasi registrazione e conservazione automatiche; data e firme a mano | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | CARTELLI | Cartello videosorveglianza e cartello telecamera piscina: nessuna modifica (ok così) | [nome collega] | confermati |
| 23/10/2026 | REGISTRO | Finalità facoltative allineate all'Informativa clienti: righe statistiche, profilazione (gestionale clienti), presenza in hotel, foto e video (anche categoria di dati e minori), messaggi promozionali rese condizionate (33 righe); testo invariato | [nome collega] | **applicata** 23/10/2026 — Registro marcato da ricaricare in knowledge |
| 23/10/2026 | MODULO_CONSENSI | Marcatura applicata (4 copie IT/EN): consensi profilazione, presenza, foto/video (adulti e minori), promozionali condizionati come in Informativa e Registro; revisione e ragione sociale compilabili (tolto il campo REF «. XX») | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | RESP_* (13 modelli) | Marcatura applicata (vedi `claude/MARCATURA_Responsabili.md`): lettera ripetuta N volte (N in check list, predefinito 1), dati del Responsabile sempre a mano, revisione e intestazione; tendine scelte in check list per Consulente, Consulente MARE.A, Software house, Web agency, Experience (righe vuote tolte); riga DVR condizionata nell'Impianto videosorveglianza; interruzioni di pagina sistemate | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | IMPEGNO_DITTA_PIANI | Confermato invariato; marcate solo revisione, intestazione e numero di copie (predefinito 1); dati della ditta e firme a mano | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | RESP_BACKUP_CREDENZIALI | Autorizzati alla gestione dei backup e alla custodia delle credenziali: predefinito il Legale rappresentante, modificabili in check list (separatamente); frequenza delle copie a tendina con le stesse voci del Registro; revisione e intestazione compilabili | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | REGISTRO | Correzione marcatura: etichettate 10 tendine della tabella «Archivio informatico» rimaste senza etichetta (backup, frequenze, gestionali) | [nome collega] | **applicata** 23/10/2026 — Registro marcato da ricaricare in knowledge |
| 23/10/2026 | INFORMATIVA_CV, DISP_MANUTENTORE, DISP_CAMERIERE | Contenuto invariato; marcate intestazione Titolare e revisione (numero predefinito 00, data = data documenti); data e firme a mano (vedi `claude/MARCATURA_Altri.md`) | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | IMPEGNO_DITTA_PIANI | Rimarcato sulla versione in knowledge inviata il 23/10/2026 (stesse regole) | [nome collega] | **applicata** 23/10/2026 |
| 23/10/2026 | ISTRUZIONI | Aggiunte intestazione Titolare e riga di revisione (non presenti); blocchi Disposizioni cameriere (`PIANI=INTERNE_NON_NOMINATE`) e manutentore (`MANUTENTORI_INTERNI`) condizionati | [nome collega] | **applicata** 23/10/2026 — da approvare |
| 23/10/2026 | DISP_MANUTENTORE | Incluso solo se ci sono manutentori interni; una copia per manutentore (numero in check list, predefinito 0) | [nome collega] | **applicata** 23/10/2026 |
| 27/10/2026 | PROC_DATA_BREACH (nuovo) | Procedura di gestione delle violazioni segnalate dai Responsabili aggiunta al pacchetto da rilasciare al cliente (versione inviata il 27/10/2026); marcate intestazione, revisione, dati del LR; tabella e frase del sostituto del LR tolte se in check list non c'è il sostituto (vedi `claude/MARCATURA_DataBreach.md`); inclusa sempre; tendine dell'Allegato 1 in bianco se non scelte | [nome collega] | **applicata** 27/10/2026 — da approvare |
| 22/10/2026 | REGISTRO | Capitolo DPIA: frase riformulata in «Poiché è presente un impianto di videosorveglianza, è stata effettuata la DPIA del relativo trattamento mediante software rilasciato dalla CNIL (…)» e resa condizionata alla presenza di telecamere | [nome collega] | **applicata** nel Registro marcato |
| 21/10/2026 | REGISTRO | Finalità «imposta di soggiorno», Clienti e Minori, nelle 3 sezioni (Titolare, Amministrazione, Front Office) = 6 celle: periodo di conservazione = «Periodo previsto dal Regolamento comunale» (tolti «(30 giorni)» e «[DA VERIFICARE]») | [nome collega] | **applicata** 21/10/2026 — da ricaricare in knowledge |

Nota: INFORMATIVA_clienti riporta già «period laid down by the municipal regulation», coerente con la decisione.
Verifica eseguita: confronto testuale tra file originale e corretto — differiscono solo le 7 celle indicate;
impaginazione, stili, tabelle e immagini invariati (XML modificato solo in quei punti).
Osservazione residua per gli approvatori: per il Front Office ADATTAMENTO («variazione sostanziale») resta SVOLTO
mentre MODIFICA («variazione minima») è ora PRECLUSO.

---

### Promemoria per la chiusura dei modelli
- [ ] **REGISTRO: aggiornare l'indice** (clic destro sull'indice → «Aggiorna campo» → «Aggiorna intero sommario»),
  una sola volta alla fine di tutte le modifiche (decisione 21/10/2026). Oggi manca la voce «CAPO SALA».

### Nota per la marcatura (§3)
Le Nomine usano già **controlli contenuto a tendina** di Word (funzione aziendale: Amministrazione, Front Office,
Capo sala, Capo chef, Guardiano notturno, Back office, Cameriera ai piani, Cameriere di sala; qualifica: Legale
Rappresentante, Coadiuvante familiare, Socio, Lavoratore). Il generatore può compilarli direttamente via OpenXml:
argomento a favore dei controlli contenuto rispetto ai marcatori `{{…}}` — decisione in corso (22/10/2026).
«Back office» tolto dalle tendine di Nomine, Formazione e Disciplinare (decisione 21/10/2026).
Le nomine Responsabile usano anch'esse tendine (trattamenti, interessati: Clienti aziendali / Personale lavoratore /
Fornitori; tipologia dati: comuni / art. 9 / art. 10).
Nota tecnica: il modello FORMAZIONE originale contiene 2 anomalie di schema già presenti (elemento `sdtEndPr` in una
tendina); Word lo apre comunque, ma il `ControlloMarcatori` dovrà tollerarle o il modello va ripulito in fase di marcatura.

---

## 12. Check list in uso (Check_list_privacy_HOTEL_rev_02.docx, ricevuta 21/10/2026)

Struttura del modulo cartaceo e corrispondenza proposta con la schermata. Il file è di 2 pagine.

**Testata** — TIMBRO (ragione sociale, indirizzo, P.IVA: da ArcLab o a mano), Pec, Legale Rappresentante,
Codice Univoco. Mancano nella check list e vanno aggiunti a schermo: **data documenti** e **numero di revisione**
(unici per tutti i documenti), sedi del trattamento, codice ATECO, attività esercitata, e-mail, sito web.

**Responsabili del trattamento** (ogni riga: casella + nome fornitore; «Stesso» = un unico fornitore per il gruppo)

| Riga check list | Modello Responsabile proposto |
|---|---|
| Ufficio paghe | RESP_PAGHE |
| Commercialista | RESP_COMMERCIALISTA |
| Paghe + Commercialista «Stesso» | RESP_PAGHE_E_COMMERCIALISTA (al posto dei due singoli) |
| Gestionale clienti / CRM sito / Fatturazione elettronica / Channel Manager / Preventivatore (con nome prodotto) | RESP_SOFTWARE_HOUSE (uno per fornitore) |
| Gestione sito / casella mail / newsletter | RESP_WEB_AGENCY |
| Gestione mailing su prospect / Campagne pubblicitarie | RESP_WEB_AGENCY — voce aggiunta alle tendine «Descrizione del/i trattamento/i» (decisione 21/10/2026) |
| Responsabile IT computer | RESP_TECNICO_COMPUTER |
| Chiavi elettroniche | RESP_CHIAVI_ELETTRONICHE |
| Azienda assistenza videosorveglianza | RESP_IMPIANTO_VIDEOSORVEGLIANZA |
| Studio consulenza Privacy / Haccp / 81/08 / Legionella / Formazione | RESP_CONSULENTE_MAREA se MARE.A, altrimenti RESP_CONSULENTE |
| Stabilimento balneare convenzionato | RESP_STABILIMENTO_BALNEARE |
| Experience (PR, eventi, parchi, terme, noleggi) | **RESP_EXPERIENCE** — modello nuovo (decisione 21/10/2026), costruito sulla struttura di RESP_STABILIMENTO_BALNEARE |

**Funzioni aziendali** (colonne 1–6) — per ciascuna: qualifica [LR / Soci / CF / Lav] e voci di trattamento a
casella. 1 Amministrazione (Prospect, Imm. promo) · 2 Front Office (Clienti, Fornitori, Lavoratori, Dati standard C,
Videosorv., Gestionale Clienti/Fatt., Prospect, Imm. promo) · 3 Guardiano notturno (Solo carte identità, Come Front
Office, Videosorv., Gestionale Clienti/Fatt.) · 4 Capo sala (Nome e cognome, N. camera, ex Art. 9) · 5 Capo chef
(Fornitori) · 6 libera. Colonna 6 contiene anche INCARICO DONNE AI PIANI n.__ e INCARICO CAMERIERI SALA n.__.
Le qualifiche coincidono con `man.Qualifiche` (CF = coadiuvante familiare).

**Caselle mail 1–5** — indirizzo + «accessibile a» funzioni 1/2/3/tutte → alimentano il campo
«Email_titolareamministrazione» del Registro.

**Archivi 1–6 e PC 1–6** — luogo (Uff. 1, Uff. 2, Desk 2, Magazzino, Camera gestore), sotto chiave (porta,
armadio, cassetto), accessibile a, dati (Clienti/Lavoratori/Fornitori: comuni, art. 9), sistema operativo
(W7/W10/W11/iOS), antivirus, gestionali, caselle mail → tabelle «Descrizione archivi cartacei/informatici» del
Registro, ripetute per ogni archivio/PC compilato.

**Misure di sicurezza** — vetrate, allarme, antincendio, estintori e naspi, rilevatori fumo, aspersori.
**Videosorveglianza** — autorizzazione n./del, Visione diretta / Registrazione / Piscina, schermo installato in,
visibile a, assistenza con accesso al DVR / alla visione diretta → struttura base (§3).
**Computer e backup** — LAN, server centrale (luogo, accessibile a), backup PC (nessuno, chiavetta, HD, ogni n.
giorni, NAS, cloud; conservato in), backup gestionale clienti e fatturazione (cloud / server / internet;
giornaliero / istantaneo), CRM sito (Iperbooking, Slope, Hotel CC, Qualitando, Hotel door, altro).
**Buste paga** — modalità consegna, conservazione. **Wi-Fi**, **Menu elettronico**, **Chiavi elettroniche**,
**Piano camere donne ai piani**, **Prenotazioni SPA** — fornitore e dati trattati.

Le liste chiuse (luoghi, sistemi operativi, CRM, Wi-Fi, menu) diventano tendine con valori predefiniti
modificabili dal pannello di controllo; le voci «Altro ___» restano campo libero.

### Revisione dei documenti
Numero di revisione predefinito **00** (commento Disposizioni, 23/10/2026), modificabile in check list; la data di
revisione coincide con la data dei documenti. Stesso numero e stessa data in tutti i documenti del pacchetto.

### Autorizzati (decisioni 22/10/2026)
La schermata ha una tabella «Autorizzati al trattamento»; ogni riga è un autorizzato (o un gruppo di autorizzati
uguali) con:
- **Qualifica** (tendina `man.Qualifiche`);
- **Funzioni** (scelta multipla tra le funzioni presenti): chi ha più funzioni — es. Legale rappresentante in
  Amministrazione e Capo chef o Guardiano notturno — riceve **una sola lettera di nomina** con tutte le funzioni: in linea nel titolo, una sotto
  l'altra nella tabella firme; la lettera resta **tassativamente in una pagina** (compattazione automatica, vedi
  `claude/MARCATURA_Nomine.md`) (decisioni 23/10/2026);
- **Nome e cognome**: solo per Legale rappresentante, Soci e Coadiuvanti familiari; per i Lavoratori non si chiedono
  (si scrivono a mano dopo la stampa, in tutti i documenti);
- **Quantità**: quanti autorizzati uguali (es. «3 Lavoratori – Front Office» in una riga sola). Per LR/soci/coadiuvanti = 1.

Il programma non salva nominativi di lavoratori: anonimizzazione non più necessaria (§4).
Da queste righe derivano: le lettere di nomina (NOMINE, una per autorizzato), le righe delle tabelle di FORMAZIONE e
DISCIPLINARE, l'organigramma del Registro (qualifiche presenti per funzione), la presenza delle funzioni facoltative
(una funzione senza autorizzati è assente). Controlli: Amministrazione e Front Office con almeno un autorizzato;
SALA e PIANI coerenti con le funzioni scelte (es. niente «Capo sala» se SALA = CAMERIERI_SALA).
Per ogni funzione c'è anche la tendina «Accesso alle immagini» (visione+registrazioni / solo visione / solo visione,
registrazioni non consentite / solo registrazioni / nessuno), predefinita dalle liste del Registro.
Il numero «INCARICO DONNE AI PIANI n.__ / CAMERIERI SALA n.__» della colonna 6 della check list cartacea confluisce qui.

### Punti da chiarire sulla check list
1. ~~Funzione «Capo sala»~~ → deciso: sostituisce i camerieri di sala quando non autorizzati; aggiunta ai modelli
   (§11). Formazione e Disciplinare avevano già la riga «Capo sala».
2. ~~«Gestione mailing su prospect» ed «Experience»~~ → deciso: mailing nella Web Agency; Experience con modello proprio.
3. ~~Funzioni facoltative~~ → deciso 21/10/2026:
   - **permanenti**: Amministrazione e Front Office (sempre presenti nei documenti);
   - **facoltative** (tolte da Registro, Nomine, Formazione e Disciplinare se assenti): Guardiano notturno, Capo chef,
     Cameriere di sala, Capo sala (alternativo ai Camerieri di sala), Cameriera ai piani;

## Versione di prova nel browser (27/10/2026)
Pagina di prova (artifact «Privacy Slug – prova»): check list in 6 passi + pannello di controllo; genera i Word dai 28 modelli
marcati e li scarica in uno zip con `compilazione.json` (scelte, revisione, voci scritte a mano). Il motore (`motore.js`) applica le
stesse regole dei prototipi Python ed è il riferimento per la riscrittura in C# (OpenXml) in Laboratorio AI.
Aggiunte rispetto ai prototipi, da riportare in C#:
- togliendo una parte `SE:` che contiene un'interruzione di sezione, l'interruzione si conserva (altrimenti nel Registro si perde
  l'intestazione: la prima sezione con intestazione è dentro `SE:BACKUP_SERVER`);
- commenti dei modelli ed evidenziazioni gialle delle intestazioni tolti dai documenti consegnati; indice del Registro aggiornato
  all'apertura in Word (`updateFields`);
- «HOTEL» nell'intestazione del Registro (non marcato) sostituito con il nome dell'insediamento — **confermato 27/10/2026**;
- tabella revisioni: dati e firme sulla riga della revisione corrente; righe precedenti da riportare dallo storico.
Limiti della prova: niente PDF (lo farà il convertitore del server), invio e-mail e caricamento su Logi simulati, compattazione
delle nomine stimata dal numero di funzioni (da controllare in Word), cartelli non inclusi, impostazioni salvate solo nel browser.

| 27/10/2026 | REGISTRO | Tolta la nota «[DA VERIFICARE]» dopo il riferimento al Libro Unico del Lavoro (D.M. 9 luglio 2008), 2 occorrenze; testo per il resto invariato | [nome collega] | **applicata** 27/10/2026 |

## Modifiche del 27/10/2026 (sera) – applicate nella versione di prova, da riportare nel codice C#
| Documento | Regola |
|---|---|
| Check list | «Funzioni che consultano le registrazioni»: voce «RLS – rappresentante dei lavoratori per la Sicurezza» + voci scritte a mano; nel Registro la riga fissa «- Rappresentante dei lavoratori per la sicurezza (RLS), se nominato» si toglie e compare solo se scelta; nella DPIA diventa «…e al Rappresentante dei lavoratori per la sicurezza (RLS)» |
| Check list | Caselle e-mail: Capo chef e Cameriere di sala non selezionabili |
| Check list | Registrazioni: «DVR fisico» oppure «Cloud del fornitore» (campo `video.archiviazione`) |
| Tutti (intestazione) | Con più sedi operative, prima di ogni indirizzo il nome dell'attività («Hotel Mare Blu – Viale Dante 10 – 47841 Cattolica (RN); Residence Sole – …») |
| Pacchetto | Nome dello zip: `Privacy_<insediamento>_<comune>.zip` (senza revisione, 27/10/2026); ogni lettera di nomina Responsabile è un file separato («lettera 1 di 2») |
| Registro | Righe vuote sotto l'organigramma tolte (spingevano una pagina bianca in più); e-mail senza funzioni tra parentesi; nella sezione di una funzione solo le caselle di quella funzione (riga E-MAIL tolta se nessuna); «SITO WEB AZIENDALE» senza righe vuote sopra; tabella CRS/CRO tolta se il nome è vuoto (senza entrambi si toglie anche «Sistemi di gestione in background…»); con cloud: «Le registrazioni sono conservate in:» e frase del DVR adattata; «24h» del paragrafo DVR sostituito dalla durata della check list; una pagina per ogni archivio informatico |
| Nomine | Righe vuote in coda a ogni lettera tolte (in Word producevano pagine bianche) |
| Disciplinare | Tabella firme ridotta alla larghezza della pagina e centrata |
| Nomi | Nome e cognome di LR, soci, coadiuvanti (e incaricati backup/credenziali, sostituto data breach): MAIUSCOLO, grassetto, centrati nella casella |
| Data breach | «Attenzione al calcolo dei tempi» sempre a inizio pagina |
| Istruzioni | Aggiunta la voce «Procedure di gestione data breach – Leggere e firmare Legale rappresentante e/o suo sostituto autorizzato al trattamento» (modello aggiornato) |
| DPIA | Evidenziazioni gialle tolte; luogo del DVR dalla check list al posto di «nell'ufficio Front Office»; con cloud riscritte 5 frasi sul DVR — **testo proposto dal programma, da rileggere e approvare** |
| Firma LR | La firma disegnata nel Registro va anche in: lettere dei Responsabili e Impegno ditta piani (colonna «Il Titolare del trattamento»), DPIA («Il Titolare del trattamento (firma)»), Formazione («Firma del Titolare o del Legale Rappresentante»), Procedura data breach (presa visione del LR), Responsabile backup e credenziali (se l'incaricato è il LR), nomina autorizzato del LR («Firma dell'Autorizzato per presa visione», firma più bassa e lettera compattata di un livello per restare in una pagina). Le nomine degli altri autorizzati restano a mano |
| Modulo consensi | Il campo «Titolare del trattamento» riporta già la ragione sociale: nell'esempio insediamento e ragione sociale avevano quasi lo stesso nome; ora l'esempio usa «Hotel Mare Blu» |

## Invio, cloud del cliente e anagrafica da P.IVA (27/10/2026)
**Invio (confermato)**: «INVIO CONSULENTE» → solo al consulente, Word. «INVIO CLIENTE» → al cliente solo PDF; al consulente
Word + PDF. Ogni invio registrato in `man.InviiDocumenti` (modalità, destinatari, formati, esito).

**Caricamento su marea.palomar.srl (predisposto, non attivo)**: dopo la generazione i file da pubblicare (PDF se invio cliente,
altrimenti Word) vanno in `man.PubblicazioniCloud` con stato «in attesa»; lo spazio del cliente si individua dalla
**ragione sociale**, con la **partita IVA** come chiave di controllo contro le omonimie. Servizio previsto
`IServizioPubblicazioneCloud` (implementazione vuota finché non c'è il collegamento). Nella prova la destinazione è scritta in
`compilazione.json` (`pubblicazioneCloud`). **Da chiedere a Palomar**: modalità tecnica (API o cartella), come si identifica lo
spazio del cliente, credenziali. **Punto da concordare con Mirko Maffei**: riuso del registro `log.RegistroDocumenti` già usato
per i documenti consegnati a Palomar.

**Anagrafica da P.IVA**: nella check list, «Cerca cliente» con la partita IVA (controllo della cifra di controllo già nella
prova). In Laboratorio AI la ricerca legge **in sola lettura** le copie dei clienti già importate ogni notte dal file Access AZW
(schema `provv`, copie AZW clienti) e gli insediamenti ArcLab (schema `clienti`, copie ArcLab insediamenti), compilando
ragione sociale, sede legale, P.IVA/C.F., e-mail/PEC e sedi del trattamento; tutto resta modificabile. Nessuna scrittura su
ArcLab né sul file AZW. **Punto da concordare con Mirko Maffei**: tabelle e colonne esatte da usare e se leggere le copie in
LaboratorioAI o ArcLab direttamente.

## Contratto di fornitura servizi e nuovo cliente (28/10/2026)
**Contratto di fornitura servizi**: nuova voce della check list (passo 1), per clienti esistenti e nuovi. **Modello Word non
ancora caricato**: nella prova si registra solo la richiesta e l'intestazione (`compilazione.json` → `contrattoFornitura`);
la marcatura si farà con le stesse etichette degli altri documenti quando arriva il modello.
- Cliente già in anagrafica: intestazione «come i documenti privacy» oppure altro intestatario cercato per **partita IVA**
  (stessa ricerca in sola lettura dell'intestazione dei documenti).
- Nuovo cliente: intestazione dai dati del Titolare inseriti a mano.

**Nuovo cliente**: campi aggiuntivi codice univoco fatturazione (7 caratteri) e referente (predefinito il Legale
Rappresentante, modificabile). Alla generazione parte un'e-mail con oggetto **«NUOVO CLIENTE DA INSERIRE»** alla casella del
pannello di controllo (predefinita `segreteria@mare-a.com`, modificabile anche sulla singola compilazione). Testo: nome
insediamento, ragione sociale, sede legale, partita IVA, codice fiscale, e-mail, codice univoco fatturazione, nome e cognome
del referente. In Laboratorio AI: invio con il servizio e-mail Brevo esistente, registrato in `man.InviiDocumenti`
(modalità «NuovoCliente»). L'applicativo **non crea** il cliente in ArcLab né in AZW: lo inserisce la segreteria.

Modello dati (proposta, da confermare prima del codice): in `man.Compilazioni` le colonne CodiceUnivocoFatturazione,
ReferenteNome, ReferenteCognome, ReferenteComeLegaleRappresentante (bit), NuovoCliente (bit), ContrattoFornitura (bit);
nuova tabella `man.IntestatariContratto` (IdIntestatarioContratto, IdCompilazione, PartitaIva, RagioneSociale, SedeLegale…,
CodiceClienteAzw null se inserito a mano). Impostazione `EmailNuoviClienti` tra le impostazioni del modulo.

## Invio al cliente ritardato (28/10/2026)
Con «INVIO CLIENTE» il consulente riceve **subito** Word + PDF; il cliente riceve i PDF **dopo N ore dal clic** (campo nella
check list, predefinito dal pannello di controllo: **6 ore**; 0 = subito). In quell'intervallo il consulente può controllare i
documenti e **annullare** l'invio al cliente dalla compilazione (proposta). Con «INVIO CONSULENTE» nessun ritardo.
- `man.InviiDocumenti`: una riga per destinatario, con `InviareDopoUtc`, `Stato` (Programmato / Inviato / Annullato / Errore),
  `InviatoIlUtc`, `AnnullatoDa`, `AnnullatoIlUtc`. Impostazione `OreRitardoInvioCliente` (predefinita 6) tra le impostazioni
  del modulo.
- Un servizio in background controlla ogni pochi minuti gli invii programmati scaduti e li spedisce con il servizio e-mail
  Brevo esistente; esito in `log.Audit`. **Punto da concordare con Mirko Maffei**: usare il meccanismo già presente in
  Laboratorio AI per le attività pianificate (come l'importazione AZW notturna) o un servizio ospitato dedicato; l'invio deve
  sopravvivere a un riavvio dell'applicazione (lo stato è nel database, non in memoria).
- Nella prova l'invio è simulato: l'ora programmata compare nel riepilogo e in `compilazione.json` (`invio.cliente`).

## Riepilogo dei dati raccolti per il cliente (28/10/2026 – bozza in revisione)
Nuovo documento generato dal programma (non è un modello marcato): «Riepilogo dei dati raccolti – documentazione privacy»,
in Word con OpenXml e inviato al cliente in **PDF** insieme agli altri documenti (stessa regola e stesso ritardo dell'invio
cliente). Bozza: `Riepilogo_dati_privacy_BOZZA.docx` (prototipo `riepilogo_prototipo.js`, dati delle prove).
Sezioni: 1 Titolare, sedi, revisione · 2 Videosorveglianza · 3 Organizzazione e finalità facoltative · 4 Autorizzati (nome e
cognome solo LR/soci/coadiuvanti) e incarichi particolari · 5 Responsabili (trattamenti e interessati per lettera) · 6 Origine
dei dati (caselle e-mail, CRM/CRS/CRO, Wi-Fi, buste paga) · 7 Backup e archivi · 8 Misure di sicurezza · 9 Documenti
consegnati e cosa farne (dalle Istruzioni documentazione) · 10 Riferimenti.
Regole (28/10/2026): i dati richiesti ma non inseriti compaiono come «[da indicare …]» **evidenziati in giallo** (es. istanza
all'Ispettorato del Lavoro); per i Responsabili con lettera a testo fisso si scrive «Trattamenti canonici propri della
professione»; nessun riferimento al contratto di fornitura servizi; consulente = **nome e cognome dell'utente collegato**
(Entra ID, es. Daniele Vaccarini) con la sua e-mail. Termine per le correzioni del cliente: **72 ore** (in grassetto nel testo, 28/10/2026). Rischio: il riepilogo contiene dati del cliente e nomi di LR/soci → stessa riservatezza degli altri documenti.
Carta intestata (28/10/2026): il riepilogo si stampa su **«Carta_intestata_NO_ACCREDIA»** (versione senza marchio ACCREDIA,
corretta perché il riepilogo non è un rapporto di prova). Intestazione e piè di pagina della carta, margini 2 cm, numero di
pagina «Pag. X di Y» sopra il piè di pagina. La carta va trattata come modello controllato del modulo (codice
`CARTA_INTESTATA`). Nota tecnica: le immagini della carta sono ancorate alla colonna; con margini diversi da quelli originali
vanno ancorate alla pagina (prototipo `su_carta_intestata.py`). La carta è un .doc: va salvata in .docx prima del caricamento.

## Cartelli videosorveglianza e telecamera piscina (28/10/2026)
Marcati e inseriti nel pacchetto (dettaglio in `claude/MARCATURA_Cartelli.md`): intestazione del Titolare al posto dello
spazio per il timbro; paragrafo sulla conservazione sempre presente: con registrazione la durata del Registro, senza registrazione «le immagini non sono conservate» (nuove condizioni `VIDEO_NON_REGISTRA`, `PISCINA_NON_REGISTRA`); cartello
videosorveglianza solo con videosorveglianza, cartello piscina solo con telecamera piscina. Nel riepilogo per il cliente:
«Esporre prima di entrare nelle aree riprese dalle telecamere».

## Preventivo / contratto di fornitura servizi privacy (28/10/2026)
Ricevuto `Preventivo_PRIVACY.docx` (è il «contratto di fornitura servizi» previsto nella check list). Revisione delle
condizioni in `Preventivo_PRIVACY_corretto.docx`, **parti nuove o modificate evidenziate in giallo**, da approvare (e da
far verificare al legale per le clausole contrattuali, come per le Condizioni Generali di Laboratorio AI). Principali
interventi: elenco servizi allineato ai documenti prodotti dal modulo; prezzi con virgola decimale; PEC corretta
(marea@spaziopec.it, nel testo era «mare@»); disdetta 90 giorni prima della scadenza; informativa con finalità numerate e
basi giuridiche; MARE.A Responsabile del trattamento (art. 28); interessi di mora (D.Lgs. 231/2002); foro competente
**Rimini**; assistenza annuale € 200 non dovuta il primo anno e dovuta **solo negli anni in cui si aggiorna la documentazione**;
informativa fornitori tolta dai servizi (l'applicativo non la genera) — decisioni del 28/10/2026; approvazione specifica delle clausole (artt. 1341-1342 c.c.) con firma separata.
**Versione di riferimento (28/10/2026): quella rivista da [nome collega]** (impaginazione dell'ultima pagina e firma MARE.A sistemate, evidenziazione tolta dalla parte finale); è la base per la marcatura.
Commenti già presenti nel file (da marcare quando il modello sarà approvato): riga videosorveglianza solo se presente;
prezzi € 300 e € 200 predefiniti ma modificabili; tabella assistenza annuale inseribile o no.
**Punto da concordare con Mirko Maffei**: rapporto con il modulo «Condizioni Generali» (schema `contratti`) già presente
in Laboratorio AI.
**29/10/2026 – sezione aggiunta da [nome collega]**: «Assunzione dell'incarico di Responsabile del trattamento dei dati
personali» (art. 28 GDPR, premesse + art. 1 oggetto + art. 2 garanzie), inserita prima delle condizioni economiche nella
versione corretta → `Preventivo_PRIVACY_corretto_v2.docx` (sezione in giallo; il file ricevuto partiva dalla versione non
corretta, le correzioni del 28/10 sono state mantenute). La frase in «Riservatezza» rimanda ora a questa sezione invece che
alla «lettera di nomina consegnata con la documentazione». Osservazioni in attesa di risposta: attività elencate non
riferite alla consulenza privacy; mancano durata, tipo di dati e categorie di interessati (art. 28 par. 3) e l'avviso
al Titolare in caso di violazione; doppione con la lettera «Responsabile del trattamento – CONSULENTE MARE.A» del pacchetto.
**Decisioni 29/10/2026**: aggiunti nella sezione (evidenziati in **verde**, da approvare) durata, tipo di dati e categorie
di interessati (art. 28 par. 3) e la voce «Violazioni dei dati personali» (avviso al Titolare senza ingiustificato ritardo,
art. 33 par. 2); l'elenco delle attività resta quello scritto da [nome collega]. **Regola del pacchetto**: se nella check
list è richiesto il contratto di fornitura servizi, la lettera «Responsabile del trattamento – CONSULENTE MARE.A» **non si
genera** (la nomina è nel contratto); nel Registro MARE.A resta tra i Responsabili. Applicata nella prova (versione 16,
`pacchetto()` in `motore.js`); in C#: condizione di inclusione del modello `RESP_CONSULENTE_MAREA` = `!CONTRATTO_FORNITURA`.

**Nuovo modello del contratto (29/10/2026)**: `MODELLO_PRIVACY_Contratto.docx` di [nome collega] è la base dei preventivi
(sostituisce `Preventivo_PRIVACY_corretto_v2`). Marcatura in `claude/MARCATURA_Contratto.md`, modello `CONTRATTO` incluso nel
pacchetto quando è richiesto il contratto (condizione `CONTRATTO_FORNITURA`). Regole dai commenti: righe videosorveglianza/DPIA
con `SE:VIDEO_O_PISCINA`; prezzi `PREZZO_DOCUMENTAZIONE` (predefinito 300) e `PREZZO_ASSISTENZA` (200) modificabili; tabella
assistenza con `SE:ASSISTENZA_ANNUALE`. Check list (passo 1): prezzi, assistenza sì/no, pagamento (bonifico / ricevuta
bancaria con IBAN controllato, ABI e CAB ricavati / scelto dal cliente), consensi dell'informativa (SI / NO / li sceglie il
cliente), SDI, telefono, e-mail per la documentazione. Pannello: prezzi predefiniti, assistenza predefinita, cellulare del
consulente. Impostazioni del modulo da aggiungere: `PrezzoDocumentazione`, `PrezzoAssistenza`, `AssistenzaPredefinita`; in
`man.Compilazioni` (o tabella `man.ContrattiFornitura`, proposta): prezzi, assistenza, pagamento, IBAN, consensi. **Rischio**: l'IBAN
del cliente è un dato riservato → stessa protezione degli altri dati della compilazione. Applicato nella prova (versione 17).
**Riferimento del contratto (29/10/2026, deciso)**: «Riferimento – Tel. / Whatsapp – E-mail» e il firmatario «Per MARE.A
s.r.l. – Dott. …» sono **sempre il consulente collegato** (Entra ID): nome ed e-mail dall'utente, cellulare dal profilo
(`man.FirmeConsulenti` o profilo utente, proposta) o dal pannello. Non si modificano sulla singola compilazione, così nome e
firma coincidono. Nella compilazione si registrano nome, cellulare ed e-mail usati (tracciabilità). Prova: versione 19.

## Firme: consulente predefinito e firma OTP (28/10/2026)
**Firma del consulente revisore**: proposta di default in base all'**utente collegato** (Entra ID). Per Daniele Vaccarini è la
firma già presente nel Registro, ricostruita ad alta risoluzione (`firma_daniele_vaccarini_HR.png`, 1445×430, PNG
trasparente; ricavata da un originale di 185×56 punti: per la massima qualità serve una scansione a 300 dpi). Il consulente può
cancellarla e firmare a mano nel riquadro. Archivio previsto: `man.FirmeConsulenti` (IdFirmaConsulente, UpnUtente, Immagine,
CaricataIlUtc, CaricataDa, Attiva) — accesso solo all'utente stesso e agli amministratori, ogni uso registrato in `log.Audit`.
**Rischio**: l'immagine della firma è un dato sensibile per l'azienda; nella versione di prova (pagina web privata) è inclusa
nella pagina: non condividere la prova all'esterno.

**Firma del Legale Rappresentante: autografa oppure OTP** (scelta al passo 6, predefinita autografa).
- Autografa: firma disegnata nel riquadro (inserita nei documenti che richiedono la firma del Titolare) o a mano sui
  documenti stampati.
- OTP: nei documenti non si inserisce la firma; il LR riceve un codice OTP all'e-mail del cliente e firma dopo l'invio. In
  `compilazione.json` → `firme.legaleRappresentante` con modo, firmatario, e-mail, stato. **Servizio OTP da realizzare in
  Laboratorio AI (Mirko Maffei) — punto da concordare**: come il modulo chiede la firma e riceve l'esito; collegamento con la catena di impronte già usata dalle
  Condizioni Generali (schema `contratti`). Le firme di autorizzati e Responsabili restano autografe.
- **Deciso (28/10/2026)**: la modalità scelta (autografa o OTP) vale sia per la documentazione sia per il contratto di
  fornitura servizi (preventivo); `compilazione.json` → `contrattoFornitura.firma`.
- **Deciso (28/10/2026)**: con la firma OTP i documenti al cliente **partono dopo la conferma OTP**, con la firma inserita
  (PDF); il ritardo in ore non si applica. Il consulente riceve subito Word e PDF (senza firma del LR). Stato in
  `man.InviiDocumenti`: «In attesa OTP» → «Inviato».
- **Deciso (28/10/2026)**: se il cliente non conferma l'OTP riceve un **promemoria automatico al giorno per 7 giorni**
  (impostazione `GiorniPromemoriaOtp`, predefinita 7, nel pannello di controllo); i promemoria si fermano alla conferma.
  Ogni promemoria registrato in `man.InviiDocumenti` (tipo «PromemoriaOtp»). **Deciso (28/10/2026)**: se alla scadenza dei giorni
  impostati nel pannello il documento non è ancora firmato, parte un'e-mail di avviso al consulente che ha generato la
  compilazione (tipo «AvvisoOtpScaduto»); i documenti al cliente restano in attesa della firma.

## Note della check list per sezione (29/10/2026)
In ogni sezione della check list (esclusi «Invio» e «Voci scritte a mano») c'è un campo **Note** a testo libero. Alla
generazione, se c'è almeno una nota, si crea un file Word a parte **«Note_check_list_<insediamento>_<comune>.docx»**: dati
del Titolare, insediamento, data, revisione, consulente, poi ogni nota con il riferimento «passo › sezione».
- Al **consulente** va sempre (Word), sia con «INVIO CONSULENTE» sia con «INVIO CLIENTE».
- Al **cliente** solo se spuntato «Invia le note anche al cliente (in PDF)» (predefinito: no), con gli altri PDF e lo stesso
  ritardo/OTP.
- Non si carica nello spazio cloud del cliente salvo la stessa spunta (proposta, da confermare).
Modello dati (proposta): `man.NoteCompilazione` (IdNotaCompilazione, IdCompilazione, Sezione nvarchar(200), Testo
nvarchar(max), CreatoIlUtc, CreatoDa, Attivo) e in `man.Compilazioni` la colonna `NoteAlCliente` bit. **Rischio**: le note
possono contenere dati personali o giudizi interni → trattarle come dati della compilazione e controllarle prima di spuntare
l'invio al cliente. Prova: versione 20 (`compilazione.json` → `note`).

## Comune → CAP e provincia negli indirizzi (29/10/2026)
In tutti gli indirizzi della check list (sede legale del Titolare, sedi del trattamento, intestatario del contratto) il campo
**Comune** propone l'elenco dei comuni («Nome (PR)»); scelto il comune, la **provincia** e il **CAP** si compilano da soli.
Se il comune ha più CAP (es. Rimini 4, Pesaro 2, Milano, Roma) il CAP diventa un elenco da cui scegliere; se il nome è di
più comuni (es. Calliano TN/AT) si sceglie quello con la provincia giusta; se il comune non è nell'elenco CAP e provincia si
scrivono a mano. Tutto resta modificabile.
Fonte nella prova: elenco pubblico «comuni-json» (matteocontrini, dati ISTAT con CAP; 7.904 comuni) in `comuni.js`.
**Punto da concordare con Mirko Maffei**: in Laboratorio AI usare una tabella di riferimento (proposta `man.Comuni`: Codice
ISTAT, Nome, SiglaProvincia, Cap, Attivo – una riga per CAP) caricata con migrazione/seed e aggiornata periodicamente, oppure
una tabella comuni già presente in Laboratorio AI o ArcLab (sola lettura). Verificare licenza e aggiornamento della fonte
(fusioni di comuni, nuovi CAP). Prova: versione 21.

## Correzioni dopo l'uso (03/11/2026) – applicate nella prova (versione 22), da riportare nel codice C#
| Documento | Regola |
|---|---|
| DPIA | Tolti tutti gli sfondi gialli (celle FFFF00) e le evidenziazioni rimaste; righe «SEDE LEGALE» e «SEDI DEL TRATTAMENTO»: via e città nella stessa cella (unite le due colonne, stesso carattere; prima la città usciva in Times) |
| Registro – dati generali | Come la DPIA: via e città nella stessa cella, separate da « - » |
| Registro – impaginazione | Tolte le pagine bianche del modello (interruzioni di pagina senza contenuto in mezzo). Le copertine di macro sezione (Analisi delle funzioni aziendali, Titolare, Autorizzati, «Funzione aziendale: …», Valutazione dei rischi, Misure di sicurezza, Sistemi di archiviazione, Misure organizzative) diventano sezioni che iniziano su **pagina dispari** (interruzione di sezione «pagina dispari») e il testo che le segue riparte da pagina dispari: **retro della copertina sempre bianco**; la pagina bianca prima di una copertina compare solo se serve a portarla sul fronte. Le interruzioni di pagina rimaste diventano «nuova pagina prima» del paragrafo seguente e si tolgono le righe vuote prima di una nuova pagina (non lasciano più pagine bianche quando il testo riempie la pagina). Formato e margini di tutte le sezioni uguali alla prima (il modello mescolava Letter/A4 e margini diversi: un'interruzione «continua» diventava una pagina nuova). Intestazione e piè di pagina scritti in ogni sezione; solo la prima ha la prima pagina diversa. Tabella «mobile» del riquadro MARE.A: il paragrafo a cui è ancorata non si tocca |
| Registro – elenco destinatari | Ogni elenco numerato delle tabelle «ELENCO DESTINATARI» riparte da 1: nuova istanza di numerazione (w:num) per tabella con «riparti da 1» (startOverride) su tutti i livelli. In C#: `NumberingInstance` + `LevelOverride/StartOverrideNumberingValue` |
| Registro – titoli chiusi | Tolto `w15:collapsed` dai titoli (i paragrafi «Backup su SERVER / DISPOSITIVI ESTERNI / CLOUD» si aprivano chiusi in Word): ora sono sempre aperti |
| Registro – videosorveglianza | Nuove condizioni nel modello: istanza INL e tabella numero/data solo con `VIDEO_O_PISCINA`; «La consultazione in tempo reale… schermo… è consentita al Legale Rappresentante ed agli Autorizzati…» con l'elenco solo con `VISIONE_DIRETTA` (telecamere presenti e non «solo registrazione»; nuova condizione nel motore). Senza telecamere restano solo il titolo e «La struttura NON è dotata…». Le funzioni elencate (visione e consultazione) sono quelle del passo 2 **corrette dalle scelte del passo 3** «Accesso alle immagini nelle nomine» (stessa regola in Registro, DPIA e nomine). Se l'elenco resta vuoto la frase diventa «…è consentita esclusivamente al Legale Rappresentante.» con avviso al consulente |
| Registro – archivi informatici | Riga «BACK UP» uniformata alla check list: se c'è un backup, riporta i tipi scelti (es. «Server (Ufficio Amministrazione); Memoria esterna (HARD DISK); Cloud (GOOGLE DRIVE)») al posto di «Nessuno…»; «FREQUENZA BACK UP» = frequenza della check list al posto di «Nessuna». Resta la scelta del consulente se ha indicato per quel PC un tipo diverso da «Nessuno» |
| Data breach | Tabella del sostituto del LR con il carattere e la dimensione della tabella del LR: nel modello Montserrat 8 pt dove mancava; in generazione ogni testo della tabella del sostituto prende la formattazione del testo corrispondente della tabella del LR |
| Cartelli | Tabella del Titolare: colonna dell'etichetta più stretta (3600 twip) e colonna dei dati più larga; in generazione ogni riga dei dati che non sta su una riga viene ridotta di carattere (fino a 7 pt) |
| Informativa lavoratori | Sempre in una pagina: righe vuote ridotte a 6 pt, nessuna interruzione di pagina. **Da controllare in Word** sui casi con più sedi e con videosorveglianza e piscina (il browser non impagina) |
| Riepilogo per il cliente | Ora **generato** dall'applicativo (prima era solo la bozza): `Riepilogo_dati_raccolti_<insediamento>_<comune>.docx` su Carta_intestata_NO_ACCREDIA, nello zip insieme agli altri documenti; al cliente in PDF con «INVIO CLIENTE», al consulente Word (e PDF). Codice in `riepilogo.js` (OOXML senza librerie esterne, funziona anche offline); carta intestata come modello `CARTA_RIEPILOGO` (già con margini 2 cm, immagini ancorate alla pagina e numero di pagina) |

Script di correzione dei modelli: `fix_modelli_0311.py` (DPIA, Registro, data breach, cartelli). **Versione offline**: va
aggiornata con `motore.js`, `modelli.js` e `riepilogo.js` di questa versione (più l'aggancio del riepilogo nella generazione).

## Approvazioni dei modelli
| Data | Modello | Revisione | Approvatore | Esito | Note |
|---|---|---|---|---|---|
| 04/11/2026 | DPIA_VIDEO | rev03 – marcatura impianti misti (`claude/MARCATURA_DPIA_Impianti_misti.md`, prova versione 23) | Daniele Vaccarini | Approvata | Manca la seconda approvazione (Mirko Maffei). In Laboratorio AI andrà registrata in `man.ApprovazioniRevisione`; usare solo con il motore della versione 23 o successiva |
