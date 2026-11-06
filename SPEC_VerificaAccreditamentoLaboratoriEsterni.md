# SPEC — Verifica accreditamento metodi dei laboratori esterni (subappalto)

> Stato: **definita, in backlog** (da realizzare **dopo** l'integrazione MonitorTemperature).
> Approccio scelto con l'utente: **A — import dell'elenco prove ACCREDIA** con confronto automatico.

## Obiettivo

Aiutare a verificare che i laboratori a cui affidiamo prove in subappalto (es. Eptanord, Sicural,
Chelab) siano **accreditati per i metodi** che affidiamo loro, e che il dato registrato in ArcLab
(flag `labEstMetodo.AccrLabEsterno`) sia **allineato** alla situazione attuale di ACCREDIA.
Serve a supportare il requisito ISO/IEC 17025:2018 §6.6 (servizi forniti dall'esterno).

## Invariante

LaboratorioAI legge ArcLab (`LabAnalisiDati`) in **SOLA LETTURA**. L'app **segnala** le discrepanze;
l'eventuale aggiornamento del flag/metodo in ArcLab lo fa l'operatore nel gestionale. Nessuna
scrittura su ArcLab.

## Dati già disponibili in ArcLab (sola lettura)

- `LabEsterno (IdLabEsterno, LabEsterno, IndirizzoLabEsterno, Frase)` — anagrafica laboratori esterni.
- `rigametodo (IdRigametodo, IdMetodo, IdLabEsterno, DenominazioneRiga)` — associazione metodo→lab esterno.
- `Metodo (IdMetodo, RifBiblio)` — `RifBiblio` = **riferimento normativo** (la norma, es. "UNI EN ISO 6579-1").
- `labEstMetodo (IdLabEsterno, IdMetodo, AccrLabEsterno)` — **flag accreditamento** per coppia lab+metodo.

Query di partenza (già componibile riusando `RepositoryLaboratoriEsterni` /`RepositoryMetodi`):
per ogni laboratorio esterno → elenco metodi affidati (`RifBiblio` + `DenominazioneRiga`) + valore
attuale del flag `AccrLabEsterno`.

## Lato ACCREDIA

Nessuna API pubblica: solo il portale `services.accredia.it` (ricerca laboratori di prova, con
`Download` dell'elenco prove per laboratorio). Lo scraping automatico del sito è fragile e poco
difendibile in audit → **NON** usarlo. Si usa il **file ufficiale scaricato** dall'operatore.
Nota: da inizio 2025 ACCREDIA ha rinumerato gli accreditamenti (registro unico per ente giuridico);
un laboratorio può avere più numeri per sedi/entità diverse (es. Chelab: 00051, 00090, 00258).

## Flusso funzionale (Opzione A)

1. **Anagrafica di raccordo**: per ogni `LabEsterno` usato, l'operatore registra in LaboratorioAI
   il/i **numero/i di accreditamento ACCREDIA** e la data dell'ultima verifica (tabella nuova nel DB
   `LaboratorioAI`, NON in ArcLab).
2. **Import elenco prove**: l'operatore scarica da ACCREDIA il file "elenco prove accreditate" del
   laboratorio (una volta per verifica) e lo carica nell'app. Salvare il file come evidenza + data.
3. **Matching automatico**: per ogni metodo affidato al lab (da ArcLab), confronto della norma
   (`RifBiblio`) con le norme presenti nell'elenco prove importato. Normalizzare le stringhe
   (spazi, "UNI EN ISO" vs "EN ISO", anni/revisioni, parti) — matching su norma + eventuale matrice.
   Esito per metodo: **Accreditato / Non trovato / Da verificare** (match parziale/ambiguo).
4. **Confronto col dato ArcLab**: evidenziare le **discrepanze** con `AccrLabEsterno`:
   - ArcLab = accreditato MA non nello scope corrente → **da correggere in ArcLab**;
   - ArcLab = non accreditato MA presente nello scope → possibile aggiornamento;
   - concordanti → ok.
5. **Report/cruscotto**: per laboratorio, tabella metodo | norma | esito ACCREDIA | flag ArcLab |
   discrepanza | note. Esportabile (Excel/PDF) e con storico verifiche (chi, quando, con quale file).

## Modello dati nuovo (solo nel DB LaboratorioAI, via migrazione EF)

- `AccreditamentoLabEsterno` (IdLabEsternoArcLab, NumeroAccredia, Ente, DataVerifica, Note).
- `ImportElencoProveAccredia` (Id, IdLabEsternoArcLab, DataImport, NomeFile, HashFile).
- `RigaElencoProveAccredia` (IdImport, NormaGrezza, NormaNormalizzata, Matrice, Denominazione, …).
- `EsitoVerificaMetodoEsterno` (IdImport, IdMetodoArcLab, Esito, Discrepanza, Note) — opzionale/derivabile.

## Cablaggio previsto (coerente col resto)

- Nuova voce menu in area **Laboratori esterni** / **Amministrazione**; permesso dedicato
  (`Permessi`/`AreaFunzionale` — prossimo valore libero dopo 23, coordinare con MonitorTemperature=24).
- Controller + viste in LaboratorioAI.Web; lettura ArcLab via i repository esistenti; scrittura solo
  sul DB `LaboratorioAI` (anagrafica accreditamenti, import, esiti).
- Nessuna dipendenza NuGet nuova (import Excel/CSV con ClosedXML/ExcelDataReader già presenti).

## Domande aperte (da chiudere alla realizzazione)

- Formato esatto del file scaricabile da ACCREDIA (Excel? CSV? PDF?) → determina il parser.
  Verificare con un file reale di uno dei tre laboratori.
- Regole di normalizzazione norme (come trattare anno/revisione e le "parti" delle norme).
- Se gestire il match anche per **matrice** oltre che per norma (per scope molto ampi).
- Periodicità del promemoria di ri-verifica (es. annuale) e collegamento allo scadenziario.
