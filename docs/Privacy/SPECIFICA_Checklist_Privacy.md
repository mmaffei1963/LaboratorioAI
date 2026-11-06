# La check list privacy

## Specifica — da approvare prima di scrivere la schermata

| | |
|---|---|
| **Data** | 3 novembre 2026 |
| **Fonte** | I 31 modelli marcati del 30/10/2026, il motore della versione di prova e il §12 di `INTEGRAZIONE_Manualistica.md` |
| **Stato** | Aggiornata il 04/11/2026 con le risposte di Vaccarini del 03/11. Restano da approvare i punti 1, 2 e 3 del §7. |

---

## 1. Quante domande sono davvero

I modelli contengono **148 campi**, ma non sono 148 domande. Quasi tutti prendono il valore da strutture che il modulo ha già:

| Quanti | Dove sta il dato | Esempi |
|---|---|---|
| 37 | **Archivi** — blocco ripetuto, una scheda per archivio | `ARCHIVIO_CARTACEO_LUOGO_1`, `ARCHIVIO_INFORMATICO_ANTIVIRUS_1` |
| 29 | **Contratto** — `man.ContrattiFornitura` | `CONTRATTO_RIBA_IBAN`, `PREZZO_DOCUMENTAZIONE` |
| 18 | **Autorizzati** — `man.AutorizzatiCompilazione` | `AUTORIZZATO_NOME`, `BACKUP_AUTORIZZATO_QUALIFICA` |
| 17 | **Responsabili** — una scheda per lettera | `RESP_TRATTAMENTO_1`, `RESP_DATI_2_3` |
| 11 | **Anagrafica** — colonne di `man.Compilazioni`, lette da ArcLab/AZW | `RAGIONE_SOCIALE`, `PARTITA_IVA`, `CODICE_ATECO` |
| 5 | **Testata** — data e numero di revisione | `REVISIONE_NUMERO`, `DATA_DOCUMENTI` |
| 3 | **Sedi** — `man.SediCompilazione` | `SEDE_TRATTAMENTO_INDIRIZZO` |
| 1 | **Calcolato** | `DATA_RIESAME_DPIA` = data documenti + 2 anni |
| **27** | **Domande vere della check list** | vedi §3 |

A queste 27 se ne aggiungono una ventina che **non compaiono come campi nei modelli** ma servono al motore per decidere cosa tenere e cosa togliere: videosorveglianza, piscina, figure presenti, finalità facoltative. Sono domande a tutti gli effetti, e sono quelle del §4.

**Il totale da costruire è quindi di una cinquantina di voci**, non 148. È una differenza che cambia il lavoro: la check list si scrive in un passaggio, non in un mese.

---

## 2. I sei passi

Sono quelli del prototipo, confermati.

| # | Passo | Cosa si compila |
|---|---|---|
| 1 | Cliente, sedi, data e revisione | Anagrafica da P.IVA, sedi del trattamento, data e revisione, contratto di fornitura |
| 2 | Struttura e finalità | Videosorveglianza, piscina, figure presenti, finalità facoltative |
| 3 | Autorizzati | Tabella qualifica / funzioni / nome / quantità, incarichi particolari |
| 4 | Responsabili | Quali lettere, quante copie, scelte dentro ciascuna |
| 5 | Sistemi, archivi e misure | Caselle e-mail, gestionali, backup, archivi, misure di sicurezza |
| 6 | Firme, generazione e invio | Firma del consulente e del legale rappresentante, invio |

---

## 3. Le 27 domande che riempiono i campi dei modelli

Le tendine hanno già le loro voci dentro i modelli: si leggono da lì, non si riscrivono. Dove la voce manca, il consulente scrive a mano e quella voce può entrare nell'elenco per le volte successive.

### Passo 2 — Videosorveglianza

| Voce | Tipo | Voci della tendina |
|---|---|---|
| `INL_NUMERO` | testo | autorizzazione Ispettorato del Lavoro, numero |
| `INL_DATA` | data | — |
| `DVR_LUOGO` | tendina | 3 voci (Desk Front Office, Ufficio Front Office, Ufficio Amministrazione) |
| `SCHERMO_LUOGO` | tendina | le stesse 3 |
| `DURATA_CONSERVAZIONE_IMMAGINI` | numero | ore; predefinito 24. Il motore lo scrive in Registro, DPIA, informative e cartelli |

### Passo 5 — Gestionali

| Voce | Tipo | Voci |
|---|---|---|
| `CRM_NOME`, `CRS_NOME`, `CRO_NOME` | testo | nome del prodotto |
| `CRM_FORNITORE`, `CRS_FORNITORE`, `CRO_FORNITORE` | tendina | **17 fornitori** già censiti nei modelli, con ragione sociale e indirizzo |
| `WIFI_NOME` | testo | nome della rete |

### Passo 5 — Backup

| Voce | Tipo | Voci |
|---|---|---|
| `BACKUP_SERVER_LUOGO` | tendina | 7 luoghi |
| `BACKUP_DISPOSITIVO` | tendina | 3 (hard disk, chiavetta, NAS) |
| `BACKUP_DISPOSITIVO_CUSTODIA` | tendina | 6 (cassetto, armadio, cassetta ignifuga, cassaforte, camera del gestore, ufficio) |
| `BACKUP_CLOUD_FORNITORE` | tendina | gli stessi 17 fornitori |
| `BACKUP_FREQUENZA` | tendina | 3 (giornaliera, automatica e immediata, automatica e giornaliera) |
| `TEMPO_RIPRISTINO_ORE` | numero | predefinito 48 |

### Passo 5 — Buste paga

| Voce | Tipo | Voci |
|---|---|---|
| `BUSTE_PAGA_RICEZIONE` | tendina | 4 modalità, a frase intera |
| `BUSTE_PAGA_MAIL` | testo | casella |
| `BUSTE_PAGA_CONSERVAZIONE` | tendina | 6 luoghi |

### Passo 5 — Misure di sicurezza

| Voce | Tipo | Voci |
|---|---|---|
| `MISURE_MINACCE_UMANE` | scelta multipla | 6 (vetrate antisfondamento, porte blindate, locali chiudibili, allarme, videosorveglianza) |
| `MISURE_ANTINCENDIO` | scelta multipla | 5 (estintori, naspi, rilevatori di fumo, aspersori) |
| `MISURE_EVENTI_NATURALI` | scelta multipla | 4 (salvavita, edificio antisismico, assicurazione) |

### Altre

| Voce | Passo | Tipo | Note |
|---|---|---|---|
| `FORMAZIONE_DURATA_ORE` | 3 | numero | predefinito 2 |
| `INFORMAZIONI_FORNITE_DA` | 1 | tendina | «da me» · «dal personale tecnico competente in materia» |
| `FUNZIONE_VISIONE` | — | — | **non è una domanda**: lo riempie il motore ripetendo le funzioni che vedono le immagini |

---

## 4. Le domande che governano i documenti

Non riempiono un campo: decidono quali documenti escono e quali parti restano. Sono quelle da cui dipende tutto il resto.

| Voce | Tipo | Valori | Cosa comanda |
|---|---|---|---|
| `VIDEO` | tendina | nessuna · visione+registrazione · solo visione · solo registrazione | DPIA, cartello, blocchi in 6 documenti |
| `PISCINA` | tendina | nessuna · visione+registrazione · solo visione | cartello piscina, DPIA |
| `VIDEO_ARCHIVIAZIONE` | tendina | DVR fisico · cloud del fornitore | riscrive 5 frasi della DPIA |
| `SALA` | tendina | camerieri di sala · capo sala · nessuno | sezioni e lettere, organigramma |
| `PIANI` | tendina | interne nominate · interne non nominate · ditta esterna | disposizioni cameriere, impegno ditta, lettera responsabile |
| `GUARDIANO` | tendina | con segreteria · senza segreteria · nessuno | sezione e lettera |
| `CAPO_CHEF` | sì/no | | sezione, lettera, organigramma |
| 5 finalità facoltative | sì/no | statistiche, gestionale clienti, comunicazione presenza, foto e video, marketing | 33 righe del Registro, informativa, consensi |
| `MANUTENTORI` | numero | 0 = nessuno | disposizioni manutentore, una copia per persona |
| `IMPEGNO_DITTA_PIANI` | numero | copie | impegno di riservatezza |
| `CRM_PRESENTE`, `WIFI_PRESENTE` | sì/no | | tabelle del Registro |
| `BACKUP_TIPI` | scelta multipla | server · dispositivi esterni · cloud · nessuno | blocchi del Registro |
| `SOSTITUTO_LR` | scelta | fra gli autorizzati, o nessuno | tabella e frase della procedura data breach |
| `CONTRATTO_FORNITURA` | sì/no | | contratto, e toglie la lettera «Consulente MARE.A» |
| `ASSISTENZA_ANNUALE` | sì/no | | tabella del contratto |

**Le combinazioni di videosorveglianza e telecamera piscina sono dodici, e sono tutte ammesse** (decisione di Vaccarini del 03/11/2026, versione 23 del motore). Erano otto, con le altre impedite dal programma; la regola nuova è che il cliente ha quello che ha, e la documentazione si adatta.

I quattro modi della videosorveglianza (nessuna · visione e registrazione · sola visione · sola registrazione) per i tre della piscina (nessuna · visione e registrazione · sola visione) danno dodici casi. Quando i due impianti si comportano in modo diverso, la DPIA non può più scrivere una frase sola: ne scrive una per impianto. Da qui le condizioni nuove `IMPIANTI_MISTI_REGISTRAZIONE` e `IMPIANTI_MISTI_VISIONE` e i tre testi `DPIA_CONSERVAZIONE_IMPIANTO`, `DPIA_IMPIANTO_SOLA_VISIONE`, `DPIA_VISIONE_IMPIANTO`.

**Conseguenza per la check list:** le due tendine `VIDEO` e `PISCINA` sono indipendenti e non si controllano a vicenda. Non va scritto alcun controllo di combinazione: scriverlo sarebbe un errore, perché rifiuterebbe casi veri.

---

## 5. I blocchi ripetuti

Non sono voci di check list: sono schede che si aggiungono quante servono.

| Blocco | Una scheda per | Campi |
|---|---|---|
| **Sedi del trattamento** | sede | nome, indirizzo, CAP, comune, provincia |
| **Autorizzati** | persona o gruppo di uguali | qualifica, funzioni, nome e cognome (solo LR/soci/coadiuvanti), quantità, accesso alle immagini |
| **Responsabili** | lettera | tipo di lettera, e le scelte dentro (trattamenti, interessati, tipi di dato) |
| **Archivi cartacei** | archivio | luogo, chi vi accede (3 righe), categorie di interessati (3 righe), contenuto (3 righe) |
| **Archivi informatici** | PC o server | luogo, sistema operativo, antivirus, gestionali installati, backup e frequenza, manutenzione, misure di sicurezza, chi vi accede, categorie di interessati |
| **Caselle e-mail** | casella | indirizzo, quali funzioni vi accedono |

Gli archivi informatici sono la scheda più pesante: **26 campi**. Nel documento ognuno occupa una pagina.

---

## 6. Come si popolano le voci

Le tendine non si scrivono a mano: le voci stanno già nei controlli contenuto dei modelli approvati, ed è da lì che il programma le legge la prima volta, scrivendole in `man.VociChecklist` e `man.OpzioniVoce`. Da quel momento si gestiscono dal pannello, e le voci scritte a mano dai consulenti possono entrare nell'elenco.

Vantaggio: **il dizionario non si digita e non diverge dai modelli**. Se Vaccarini aggiunge un fornitore alla tendina di un modello, al caricamento della nuova revisione la voce si propone da sola.

---

## 7. Cosa chiedo di approvare

1. **I sei passi** del §2.
2. **La ripartizione** del §1: 27 domande vere, il resto da strutture esistenti.
3. **Il popolamento automatico** delle tendine dai modelli approvati (§6), invece di scriverle a mano.
4. ~~Due cose da decidere con Vaccarini.~~ **Decise da Vaccarini il 03/11/2026:**
   - i **17 fornitori** restano un **elenco chiuso** dentro le tendine, non diventano un'anagrafica a parte;
   - la scheda **archivio informatico** resta **completa**, tutti i campi, nessuna semplificazione. Per accorciare il sopralluogo si aggiunge invece un comando **«copia la scheda»**, che duplica un archivio già compilato lasciando ogni campo modificabile. È la risposta giusta: più PC uguali si compilano una volta sola, ma due PC diversi restano descritti per quello che sono.

---

*MARE.A srl — Cattolica (RN) — novembre 2026*
