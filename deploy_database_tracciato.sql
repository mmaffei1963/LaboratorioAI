/* ============================================================
   Tracciato — database di PRODUZIONE
   Da eseguire in SSMS con un account AMMINISTRATORE SQL, una volta sola.

   1) crea il database vuoto "Tracciabilita";
   2) crea l'accesso dell'account di servizio che fa girare il sito
      (account SEPARATO da quello di Laboratorio AI) e gli dà i permessi
      SOLO su questo database. Nessun accesso a ArcLab né a LaboratorioAI;
   3) poi si applica Tracciato_database.sql (migrazioni) su questo database.

   >>> Sostituire [MAREA\svc_tracciato] con l'account vero. <<<
   ============================================================ */
CREATE DATABASE Tracciabilita;
GO

USE master;
GO
CREATE LOGIN [MAREA\svc_tracciato] FROM WINDOWS;   -- account dedicato a Tracciato
GO

USE Tracciabilita;
GO
CREATE USER [MAREA\svc_tracciato] FOR LOGIN [MAREA\svc_tracciato];
ALTER ROLE db_datareader ADD MEMBER [MAREA\svc_tracciato];
ALTER ROLE db_datawriter ADD MEMBER [MAREA\svc_tracciato];
ALTER ROLE db_ddladmin   ADD MEMBER [MAREA\svc_tracciato];   -- trigger e vincoli creati all'avvio (se attivati)
GO

-- Verifica: l'account deve avere accesso SOLO a Tracciabilita.
SELECT dp.name AS Utente, r.name AS Ruolo
FROM sys.database_role_members m
JOIN sys.database_principals r  ON r.principal_id  = m.role_principal_id
JOIN sys.database_principals dp ON dp.principal_id = m.member_principal_id
WHERE dp.name = N'MAREA\svc_tracciato';
