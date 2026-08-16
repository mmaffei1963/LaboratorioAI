# Laboratorio AI — MARE.A srl

Applicativo interno di supporto alle attività di laboratorio.

## Struttura

```
LaboratorioAI.sln
├── src/
│   ├── LaboratorioAI.Core/    Dominio: entità, enumerazioni, interfacce dei servizi
│   ├── LaboratorioAI.Dati/    Accesso dati: EF Core (dati propri) e Dapper (ArcLab)
│   └── LaboratorioAI.Web/     Presentazione: ASP.NET Core MVC
└── docs/
    ├── Analisi_Sistema_Laboratorio_MAREA.md
    └── Messa_in_esercizio.md
```

## Principio invariante

> Il sistema **non scrive mai** su ArcLab.

I dati gestionali sono di proprietà di ArcLab e vengono letti in sola lettura.
Il database `LaboratorioAI` contiene esclusivamente i dati di cui questo
sistema è proprietario: analisi AI, utenti applicativi, registro DVR, audit.

Il vincolo è applicato tecnicamente: l'account di servizio ha solo il ruolo
`db_datareader` su `LabAnalisiDati`.

## Ambiente di sviluppo

Richiede .NET 8 SDK e un'istanza SQL Server (anche LocalDB).

```powershell
dotnet restore
dotnet build
dotnet run --project src\LaboratorioAI.Web
```

All'avvio l'applicazione applica le migrazioni e popola ruoli e permessi.

### Migrazioni

```powershell
# Nuova migrazione
dotnet ef migrations add <NomeMigrazione> `
    --project src\LaboratorioAI.Dati `
    --startup-project src\LaboratorioAI.Web `
    --context ContestoLaboratorioAI

# Applicazione manuale (normalmente automatica all'avvio)
dotnet ef database update `
    --project src\LaboratorioAI.Dati `
    --startup-project src\LaboratorioAI.Web
```

## Versionamento

Versione semantica in `Directory.Build.props`, propagata a tutti i progetti.
Viene registrata su ogni analisi archiviata e su ogni voce di audit.

| Cifra | Quando incrementarla |
|---|---|
| MAJOR | Modifiche che richiedono intervento su dati o configurazioni |
| MINOR | Nuove funzionalità retrocompatibili |
| PATCH | Correzioni |

## Rami

| Ramo | Contenuto |
|---|---|
| `main` | Codice in produzione, taggato a ogni rilascio |
| `develop` | Integrazione |
| `feature/*` | Singola funzionalità |

## Documentazione

- [Analisi funzionale e architetturale](docs/Analisi_Sistema_Laboratorio_MAREA.md)
- [Messa in esercizio](docs/Messa_in_esercizio.md)
