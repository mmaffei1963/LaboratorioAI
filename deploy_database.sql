/* ============================================================
   Laboratorio AI — Preparazione database e account di servizio
   Da eseguire in SSMS con un account amministratore SQL.

   REGOLA INVARIANTE: su ArcLab (LabAnalisiDati) l'account ha
   SOLO db_datareader. Nessun permesso di scrittura, mai.

   Nota: le TABELLE del database LaboratorioAI vengono create
   automaticamente dalle migrazioni EF Core al primo avvio
   della Web app. Questo script prepara solo il database vuoto
   e i permessi.
   ============================================================ */

/* 1. Database applicativo (vuoto) --------------------------- */
IF DB_ID('LaboratorioAI') IS NULL
    CREATE DATABASE LaboratorioAI;
GO

/* 2. Login di dominio per l'account di servizio ------------- */
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'MAREA\svc_labai')
    CREATE LOGIN [MAREA\svc_labai] FROM WINDOWS;
GO

/* 3. Permessi sul database applicativo ----------------------
   Lettura/scrittura + creazione tabelle (per le migrazioni).  */
USE LaboratorioAI;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'MAREA\svc_labai')
    CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
GO
ALTER ROLE db_datareader ADD MEMBER [MAREA\svc_labai];
ALTER ROLE db_datawriter ADD MEMBER [MAREA\svc_labai];
ALTER ROLE db_ddladmin   ADD MEMBER [MAREA\svc_labai];  -- necessario alle migrazioni EF
GO

/* 4. Permessi su ArcLab: SOLA LETTURA (invariante) ---------- */
USE LabAnalisiDati;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'MAREA\svc_labai')
    CREATE USER [MAREA\svc_labai] FOR LOGIN [MAREA\svc_labai];
GO
ALTER ROLE db_datareader ADD MEMBER [MAREA\svc_labai];
GO

/* 5. Verifica finale ----------------------------------------
   Su LabAnalisiDati deve comparire SOLO db_datareader.        */
USE LabAnalisiDati;
GO
SELECT r.name AS Ruolo
FROM sys.database_role_members m
JOIN sys.database_principals r ON r.principal_id = m.role_principal_id
JOIN sys.database_principals u ON u.principal_id = m.member_principal_id
WHERE u.name = 'MAREA\svc_labai';
GO
