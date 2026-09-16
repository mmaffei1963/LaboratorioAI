# Ordine di Accettazione — piano e note

Documento generato da **LaboratorioAI** leggendo **ArcLab (`LabAnalisiDati`) in sola lettura**,
sui lotti già ricevuti. Modellato sull'Ordine di Accettazione Sicural. **Nessuna scrittura su ArcLab.**

## Cosa contiene il documento
- **Testata**: intestazione MARE.A, "ORDINE DI ACCETTAZIONE", riferimento = `CodLotto`, data di stampa,
  destinatario (cliente della commessa: ragione sociale, indirizzo di fatturazione, P.IVA, insediamento).
- **Riepilogo campioni**: una riga per campione (riferimento `CodLotto-CodCampione`, denominazione + matrice,
  prezzo). Prezzo per campione = `commessa.PrezzoControllo`. Imponibile + IVA 22% + Totale.
- **Riquadro condizioni**: testo Allegato A delle Condizioni Generali Rev. 19 (VERIFICA / PRELIEVO E
  RAPPRESENTATIVITÀ / CONDIZIONI DI FORNITURA), pensato per il LIMS, senza firma.
- **Allegato tecnico** (un blocco per campione): riferimento, descrizione, matrice, imballaggio,
  punto di prelievo, data accettazione (`Lotto.DataRic`), data prelievo (`Lotto.DataPrelievo`),
  T° di ricevimento (`Campione.TempRic`), priorità (`Campione.Urgente`), controllo con `IdControllo`,
  elenco analisi (parametro + metodo). Per le prove in subappalto è riportata la frase del laboratorio
  esterno (`LabEsterno.Frase`, segnata con `*`).

Non riportati (per scelta): peso netto, riferimento normativo, scadenza.

## Mappatura ArcLab
- Lotto: `IdLotto` (chiave stabile — il `CodLotto` si ripete negli anni), `CodLotto`, `DataRic`,
  `DataPrelievo`, `OraRic`, `IdCommessa`.
- commessa → `Clienti` (join `CODContabilita` = `Cod_Cliente`) e `Insediamento`; `IDControllo`,
  `PrezzoControllo`; `Controllo.Controllo`.
- Campione: `CodCampione`, `DenomCampione`, `IdMatrice`→`Matrice.Matrice`,
  `IdImballaggio`→`Imballaggio.Imballaggio`, `PuntoPrelievo`, `TempRic`, `Urgente`.
- Analisi: `prove` → `rigametodo` (`IdLabEsterno`→`LabEsterno`) → `Metodo` (`RifBiblio`) + `Parametro`.

## Come funziona
- **Pagina** `/OrdiniAccettazione`: filtro per date di ricezione, elenco lotti, per ogni lotto
  anteprima **PDF**, download **Word** (carta intestata), **Segna emesso**.
- **Schedulazione**: `POST /api/ordini-accettazione/genera-schedulati?dal=&al=` registra come emessi
  i lotti del periodo non ancora emessi. Script: `scripts/genera-ordini-accettazione.ps1` (Task Scheduler).
- **Guardia anti-doppione**: tabella `accettazione.OrdiniEmessi` con **indice univoco su `IdLotto`**.
  Vale sia per il bottone sia per la schedulazione (gestita anche la corsa concorrente).

## A valle (Palomar / Brevo)
Il PDF va pubblicato sul **portale Palomar**, che fa partire le **mail massive via Brevo**
(assistenza Palomar). L'integrazione di pubblicazione/invio è esterna a questo modulo.

## Da completare in produzione
- **Autenticazione della chiamata schedulata**: l'app è protetta da Entra ID; la chiamata non
  interattiva richiede Windows Integrated Auth sull'endpoint o una API key. Finché non configurata,
  lo script va eseguito in un contesto autenticato.
- Verifica su modelli/lotti reali e allineamento estetico su carta intestata.

## Comandi
```
# Migrazione (crea accettazione.OrdiniEmessi)
dotnet ef migrations add OrdiniAccettazioneEmessi --project src/LaboratorioAI.Dati --startup-project src/LaboratorioAI.Web
dotnet ef database update --project src/LaboratorioAI.Dati --startup-project src/LaboratorioAI.Web
```
