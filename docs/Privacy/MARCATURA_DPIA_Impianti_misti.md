# MARCATURA_DPIA_Impianti_misti.md — DPIA con videosorveglianza e telecamera piscina in modalità diverse

| | |
|---|---|
| **Data** | 3 novembre 2026 |
| **Decisione** | Daniele Vaccarini, 03/11/2026: **tutte le combinazioni** di videosorveglianza (4 modalità) e telecamera piscina (3 modalità) sono ammesse, 12 in tutto; nella DPIA una frase per ciascun impianto, che cambia secondo le scelte della check list |
| **Modello** | `DPIA_VIDEO` — marcatura **applicata il 03/11** sulla versione corretta da `fix_modelli_0311.py` (§3.4). La copia del 23/10 nei File del progetto è superata |
| **Stato** | **Approvato da Daniele Vaccarini il 04/11/2026** (primo approvatore; file consegnato: `DPIA_Videosorveglianza_marcato_rev03.docx`, modello della prova versione 23). **Manca la seconda approvazione di Mirko Maffei** prima dell'uso con i clienti |

---

## 1. Il problema

La DPIA tratta i due impianti come uno solo. Le parti su conservazione e DVR dipendono da `QUALSIASI_REGISTRAZIONE`: basta che registri un impianto perché il testo dica che le immagini sono conservate per {durata} ore. Con la videosorveglianza che registra e la telecamera piscina in sola visione, il documento direbbe che anche le immagini della piscina sono conservate, e non è vero.

Registro, informative e cartelli non hanno il problema: lì il motore distingue già i due impianti (`VIDEO_REGISTRAZIONE`, `PISCINA_REGISTRAZIONE`, `VIDEO_NON_REGISTRA`, `PISCINA_NON_REGISTRA`).

## 2. La regola

**Un solo impianto, oppure entrambi nella stessa modalità** → i testi restano quelli già approvati, senza nessun cambiamento. Così i documenti delle combinazioni 1–8 restano identici a quelli della prova, e il collaudo già fatto vale ancora.

**Entrambi gli impianti in modalità diverse** → nei punti del §3 il testo unico è sostituito da **una frase per impianto**, ciascuna scelta in base alla modalità di quell'impianto.

Due impianti possono essere diversi in due modi, e ognuno tocca frasi diverse: per questo le condizioni nuove sono due.

| Condizione | Vera quando | Esempi | Tocca |
|---|---|---|---|
| `IMPIANTI_MISTI_REGISTRAZIONE` | entrambi presenti, e uno registra mentre l'altro no | videosorveglianza visione+registrazione o solo registrazione, piscina solo visione; videosorveglianza solo visione, piscina visione+registrazione | conservazione (§3.1, §3.2) |
| `IMPIANTI_MISTI_VISIONE` | entrambi presenti, e uno si vede in diretta mentre l'altro no | videosorveglianza solo registrazione con qualunque telecamera piscina (la piscina ha sempre la visione) | visione in tempo reale (§3.3) |

Videosorveglianza «solo registrazione» con piscina «solo visione» rende vere tutte e due.

### Le 12 combinazioni e cosa cambia nella DPIA

| Videosorveglianza | Piscina | DPIA | Testi |
|---|---|---|---|
| nessuna | nessuna | non esce | — |
| qualunque | nessuna | esce | come oggi |
| nessuna | qualunque | esce | come oggi |
| visione+registrazione | visione+registrazione | esce | come oggi |
| solo visione | solo visione | esce | come oggi |
| visione+registrazione | solo visione | esce | §3.1, §3.2 |
| solo visione | visione+registrazione | esce | §3.1, §3.2 |
| solo registrazione | visione+registrazione | esce | §3.3 |
| solo registrazione | solo visione | esce | §3.1, §3.2, §3.3 |

(Le righe «qualunque» raccolgono 3 + 2 combinazioni: in tutto 12.)

## 3. Dove e con quali testi

Le etichette sono `TESTO:`, perché il testo si costruisce come per `DPIA_PERIODO_CONSERVAZIONE`. Con la loro condizione falsa non fanno nulla e il testo del modello resta.

### 3.1 `TESTO:DPIA_PERIODO_CONSERVAZIONE` — «Qual è il periodo di conservazione dei dati?»

Etichetta già esistente: si aggiunge solo il caso misto.

| Impianto | Registra | Non registra (sola visione) |
|---|---|---|
| Videosorveglianza | «Videosorveglianza: massimo {durata} ore, come da Circolare INL n.5 del 19 Febbraio 2018.» | «Videosorveglianza: le immagini non sono conservate; è prevista la sola visione in tempo reale.» |
| Telecamera piscina | «Telecamera piscina: massimo {durata} ore, come da Circolare INL n.5 del 19 Febbraio 2018.» | «Telecamera piscina: le immagini non sono conservate; è prevista la sola visione in tempo reale.» |

Le due frasi vanno una sotto l'altra, prima la videosorveglianza. Sono le frasi già approvate il 23/10, con davanti il nome dell'impianto; il «:» della frase senza registrazione diventa «;» per non avere due due punti nella stessa riga.

### 3.2 Conservazione — «Qual è il ciclo di vita del trattamento dei dati?»

Condizione: `IMPIANTI_MISTI_REGISTRAZIONE`, la stessa del §3.1. Due interventi, entrambi dentro il blocco `SE:QUALSIASI_REGISTRAZIONE` già esistente.

| Etichetta | Dove | Contenuto nel modello | Cosa scrive il motore |
|---|---|---|---|
| `TESTO:DPIA_CONSERVAZIONE_IMPIANTO` (nuova, in linea) | nel punto «Conservazione delle immagini **·** su Digital Video Recorder (DVR)…», subito dopo «delle immagini» | **vuoto** | caso misto: « della videosorveglianza» oppure « della telecamera piscina», secondo l'impianto che registra; altrimenti nulla |
| `SE:IMPIANTI_MISTI_REGISTRAZIONE` (nuova, blocco) | punto elenco aggiunto **dopo** «Trascorso il tempo massimo di conservazione…» | «Le immagini della ‹impianto› non sono conservate: è prevista la sola visione in tempo reale.» | tiene il punto solo nel caso misto |
| `TESTO:DPIA_IMPIANTO_SOLA_VISIONE` (nuova, in linea, dentro il punto sopra) | al posto di ‹impianto› | «telecamera piscina» | «videosorveglianza» oppure «telecamera piscina», l'impianto che non registra |

Il punto aggiunto sta **dopo** «Trascorso il tempo massimo…», non prima: così quella frase resta attaccata alle immagini conservate di cui parla. Il resto del punto del DVR (luogo, chiave, chi consulta, durata, eccezioni) non cambia, e con le registrazioni su cloud vale la riscrittura già prevista.

### 3.3 Visione in tempo reale — «Qual è il ciclo di vita del trattamento dei dati?»

Condizione: `IMPIANTI_MISTI_VISIONE`, cioè videosorveglianza «solo registrazione» con la telecamera piscina.

| Etichetta | Dove | Contenuto nel modello | Cosa scrive il motore |
|---|---|---|---|
| `TESTO:DPIA_VISIONE_IMPIANTO` (nuova, in linea) | nel punto «Consultazione in tempo reale delle immagini **·** mediante apposito schermo…», subito dopo «delle immagini» | **vuoto** | caso misto: « della telecamera piscina»; altrimenti nulla |
| `SE:IMPIANTI_MISTI_VISIONE` (nuova, blocco) | punto elenco aggiunto subito dopo | «Le immagini della videosorveglianza non sono visibili in tempo reale: sono soltanto registrate.» | tiene il punto solo nel caso misto |

L'elenco delle funzioni che vedono non cambia (passo 2 corretto dal passo 3). Lo schermo resta nelle «Risorse di supporto» e nel «Controllo degli accessi logici»: c'è, ed è quello della piscina.

### 3.4 Marcatura applicata — 03/11/2026

Applicata su `DPIA_Videosorveglianza_marcato.docx` del 03/11 (quello già corretto da `fix_modelli_0311.py`: nessuno sfondo giallo rimasto). Cinque controlli contenuto nuovi, con etichetta e descrizione come gli altri; in `TESTO:DPIA_PERIODO_CONSERVAZIONE` aggiornata solo la descrizione con il caso misto. Nient'altro toccato.

Verifiche: convalida dello schema superata; 345 paragrafi contro 343 (i due punti nuovi); convertito in PDF, stesse 10 pagine dell'originale. Nel modello compaiono tutte le varianti insieme, come in ogni modello marcato: è il motore che sceglie.

## 4. Cosa resta com'è, e perché

- **Le parti sul DVR** in «Risorse di supporto», «Controllo degli accessi logici», «Tracciabilità», «Protezione contro fonti di rischio non umane» e «Controllo degli accessi fisici» restano sotto `SE:QUALSIASI_REGISTRAZIONE` e invariate: descrivono il DVR, che c'è. Le due frasi del §3 bastano a dire quali immagini vi finiscono.
- **«Quale è il trattamento in considerazione? VIDEOSORVEGLIANZA»** e **«Immagini di videosorveglianza»** non distinguono la piscina neppure oggi, con nessuna combinazione. Non è un effetto della decisione di oggi e non lo tocco: lo segnalo per la revisione del modello.

## 5. Fuori dalla DPIA: un controllo da fare nel motore

`VISIONE_DIRETTA` (versione 22, Registro) è definita come «telecamere presenti e non solo registrazione». Con la videosorveglianza «solo registrazione» e la piscina visibile deve risultare **vera**, perché lo schermo della piscina c'è. Va verificato che il motore la calcoli come «almeno un impianto con visione», non guardando la sola videosorveglianza.

Registro, informative e cartelli trattano già i due impianti separatamente: le combinazioni nuove vanno solo collaudate.

## 6. Per il codice C# e per la prova

- `IMPIANTI_MISTI_REGISTRAZIONE` e `IMPIANTI_MISTI_VISIONE` nel calcolo delle condizioni (inventario §3).
- Le quattro etichette `TESTO:` (§3.1–§3.3) nel trattamento dei testi (inventario §5), passo 10 del ciclo; i due blocchi `SE:` al passo 9.
- Il validatore delle combinazioni non scarta più nulla: tutte e 12 sono ammesse.
- Nel registro di generazione un avviso «DPIA con impianti in modalità diverse: rileggere conservazione e ciclo di vita prima della firma», come per le frasi del cloud.
- Collaudo: i quattro casi misti della tabella del §2, con DVR fisico e con cloud.

### Prova aggiornata — versione 23 (03/11/2026)

`motore.js`, `modelli.js` e la pagina della prova sono stati aggiornati e pubblicati come **versione 23**:
- `contesto()`: nuove `VIDEO_VISIONE`, `PISCINA_VISIONE`, `IMPIANTI_MISTI_REGISTRAZIONE`, `IMPIANTI_MISTI_VISIONE`; `VISIONE_DIRETTA` corretta in «almeno un impianto si vede in diretta» (nella 22 era falsa con videosorveglianza «solo registrazione» e piscina visibile, e il Registro perdeva la frase sulla consultazione in tempo reale); l'elenco delle funzioni che vedono usa la stessa condizione;
- `testoAlt()`: il caso misto di `DPIA_PERIODO_CONSERVAZIONE` e le tre etichette nuove;
- avviso «DPIA con impianti in modalità diverse: rileggere…» nel riepilogo della generazione;
- schermata: le 4 combinazioni nuove in coda all'elenco (posizioni 9–12, le 1–8 non cambiano, così le compilazioni salvate restano valide); luogo dello schermo e funzioni che vedono compaiono quando almeno un impianto si vede in diretta;
- `modelli.js`: la DPIA marcata al posto della precedente, gli altri 31 modelli invariati.

**Collaudo** (pacchetto completo generato in Chrome, confronto sul testo di tutti i documenti):
- combinazioni 1–8: **documenti identici** a quelli della versione 22, avvisi compresi;
- combinazioni 9–12: frasi per impianto come da §3, una sola volta e nel punto giusto; Registro con la frase sulla consultazione in tempo reale anche nella 11 e nella 12;
- combinazione 9 con registrazioni su **cloud**: la riscrittura del DVR funziona («Conservazione delle immagini della videosorveglianza sul cloud del fornitore…»), le altre quattro frasi riscritte invariate.

**Resta da fare: la versione offline.** Va ricostruita con questi `motore.js`, `modelli.js` e `index.html`: con il motore vecchio e il modello nuovo le frasi nuove uscirebbero in ogni DPIA (vedi sotto).

**Attenzione prima di usare il modello nella prova o nella versione offline.** Il motore attuale (versione 22) non conosce le due condizioni nuove, e la regola è «condizione sconosciuta: il blocco resta». Con questo modello e il motore di oggi, **i due punti nuovi comparirebbero in ogni DPIA**, anche con un solo impianto. Il modello va caricato nella prova e nell'offline **insieme** all'aggiornamento di `motore.js`, mai da solo.

Due cose da verificare nell'aggiornamento — **entrambe verificate nella versione 23**:
1. la riscrittura delle frasi del DVR con le registrazioni su cloud cerca la frase «Conservazione delle immagini su Digital Video Recorder…» per testo. Ora in mezzo c'è un controllo contenuto vuoto: il testo letto è lo stesso, ma va provato che la sostituzione lo trovi ancora e non lo cancelli (trappola 10 dell'inventario);
2. le due etichette in linea con contenuto vuoto non devono lasciare uno spazio doppio quando restano vuote.
