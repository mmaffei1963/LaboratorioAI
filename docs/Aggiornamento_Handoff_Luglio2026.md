# Aggiornamento handoff — Luglio 2026 (post-collaudo)

Da recepire nel file `HANDOFF_LaboratorioAI.md` (knowledge del progetto),
sezioni §7.2 "Aperti" e §9 "Prossimi passi".

## Punti chiusi

| Problema (era in §7.2) | Esito |
|---|---|
| **Da Validare — errore 500** | **Risolto.** Causa reale: query EF Core non traducibile in `ServizioAnalisi.ArchivioAsync` (`GroupBy(...).Select(g => g.First())` con proprietà di navigazione), non `STRING_AGG` né la versione SQL. SQL Server risultato: **2022 (16.0)**, supporta `STRING_AGG`. |
| **Ricerca campioni — errore 500** | **Risolto.** Stessa causa: entrambe le schermate chiamano `ArchivioAsync` per il badge archivio analisi. |
| **Chiave API Anthropic** | **Configurata** via user-secrets (`Anthropic:ChiaveApi`). Analisi AI funzionante. |

## Correzioni funzionali aggiuntive

- **Da Validare** ora esclude i campioni con `AnalisiChiusa` impostata
  (`ISNULL(ca.AnalisiChiusa, 0) = 0`): l'elenco mostra solo i campioni realmente da validare.
- **Consultazione**: etichette di stato con icona (✓ Certificato, ⏳ In corso).
- **Legionella nei certificati.** Scoperta importante: la costante
  `IdRigaMetodoDaEscludere = 18963` in `CostantiArcLab.cs`, documentata come
  "riga tecnica presente su ogni campione", **è in realtà la determinazione di
  legionella** (IdMetodo 5381; valore in `ValoreCalcolato`, con `MaggioreDi`/`MinoreDi`).
  Escludendola ovunque, i valori di legionella sparivano dal certificato.
  `IRepositoryCampioni.CompletoAsync` accetta ora `includiProveEscluse`: il
  certificato la richiede a `true`, mentre analisi AI e "Da Validare" mantengono
  l'esclusione (le legionelle non devono comparire tra i campioni da validare).

## Ancora aperti

- **DVR — cerca struttura** (`RepositoryDvr.StruttureAsync`, `LIKE '%legionell%'`):
  non affrontato in questa sessione.
- Cosmetico: correggere il commento fuorviante su `IdRigaMetodoDaEscludere`
  in `CostantiArcLab.cs` (è la legionella, non una "riga tecnica").
- Regressione da verificare: comportamento delle analisi AI massive con le
  modifiche a `CompletoAsync` e `ArchivioAsync`.
- Restano i punti §7.3: Entra ID, account `MAREA\svc_labai`, deploy IIS,
  primo avvio portale, test DVR su modelli reali.
