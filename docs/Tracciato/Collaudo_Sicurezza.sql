/* ============================================================================
   Tracciato — collaudo delle protezioni di SQL Server
   Da eseguire in SSMS sul database  Tracciabilita_Test  (MAI sulla produzione).
   Prima: avviare il sito con  Tracciabilita:ApplicaVincoliSql = true  (Avvia_Tracciato.bat)
   Ogni blocco (separato da GO) dice cosa ci si aspetta.
   ============================================================================ */
USE Tracciabilita_Test;
GO

/* 1) Oggetti creati dal sito. Atteso: 2 trigger (non disabilitati), 1 politica, 3 predicati per tabella. */
SELECT t.name AS Trigger_, OBJECT_NAME(t.parent_id) AS Tabella, t.is_disabled AS Disabilitato
FROM sys.triggers t WHERE t.parent_id IN (OBJECT_ID(N'trac.MovimentiLotto'), OBJECT_ID(N'trac.Audit'));
SELECT name AS Politica, is_enabled AS Accesa FROM sys.security_policies;
SELECT COUNT(DISTINCT target_object_id) AS TabelleProtette, COUNT(*) AS Predicati FROM sys.security_predicates;
GO

/* 2) Dati di prova, scritti come gestore (la politica è ancora spenta). */
EXEC sp_set_session_context N'Gestore', 1;
EXEC sp_set_session_context N'IdAzienda', NULL;
EXEC sp_set_session_context N'ConsentiEliminazione', 0;

IF NOT EXISTS (SELECT 1 FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-A')
    INSERT trac.Aziende (RagioneSociale, Settore, Stato, MaxInsediamenti, MaxUtenti, DataCreazione)
    VALUES (N'TEST-Azienda-A', 1, 1, 1, 6, SYSUTCDATETIME());
IF NOT EXISTS (SELECT 1 FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-B')
    INSERT trac.Aziende (RagioneSociale, Settore, Stato, MaxInsediamenti, MaxUtenti, DataCreazione)
    VALUES (N'TEST-Azienda-B', 1, 1, 1, 6, SYSUTCDATETIME());

DECLARE @A int = (SELECT IdAzienda FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-A');
DECLARE @B int = (SELECT IdAzienda FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-B');
IF NOT EXISTS (SELECT 1 FROM trac.Fornitori WHERE RagioneSociale = N'TEST-Fornitore-di-A')
    INSERT trac.Fornitori (IdAzienda, RagioneSociale, Attivo) VALUES (@A, N'TEST-Fornitore-di-A', 1);
IF NOT EXISTS (SELECT 1 FROM trac.Fornitori WHERE RagioneSociale = N'TEST-Fornitore-di-B')
    INSERT trac.Fornitori (IdAzienda, RagioneSociale, Attivo) VALUES (@B, N'TEST-Fornitore-di-B', 1);
INSERT trac.Audit (IdAzienda, Azione, Entita, DataOra) VALUES (NULL, N'TEST', N'Collaudo', SYSUTCDATETIME());

SELECT N'Atteso: 2 aziende e 2 fornitori di prova' AS Nota;
SELECT IdAzienda, RagioneSociale FROM trac.Aziende WHERE RagioneSociale LIKE N'TEST-%';
SELECT IdAzienda, RagioneSociale FROM trac.Fornitori WHERE RagioneSociale LIKE N'TEST-%';
GO

/* 3) Trigger di sola aggiunta sul registro Audit.
      Atteso: UPDATE e DELETE falliscono con errore 51000 ("registro in sola aggiunta"). */
EXEC sp_set_session_context N'Gestore', 1;
DECLARE @id bigint = (SELECT MAX(IdAudit) FROM trac.Audit WHERE Azione = N'TEST');
BEGIN TRY
    UPDATE trac.Audit SET Azione = N'ALTERATO' WHERE IdAudit = @id;
    PRINT N'ERRORE DI COLLAUDO: la modifica dell''audit è riuscita!';
END TRY
BEGIN CATCH
    PRINT N'OK  modifica bloccata: ' + ERROR_MESSAGE();
END CATCH;
BEGIN TRY
    DELETE trac.Audit WHERE IdAudit = @id;
    PRINT N'ERRORE DI COLLAUDO: la cancellazione dell''audit è riuscita!';
END TRY
BEGIN CATCH
    PRINT N'OK  cancellazione bloccata: ' + ERROR_MESSAGE();
END CATCH;
SELECT N'Atteso: la riga TEST c''è ancora e dice TEST (non ALTERATO)' AS Nota, IdAudit, Azione FROM trac.Audit WHERE IdAudit = @id;
GO

/* 4) Accendo la sicurezza a livello di riga. */
ALTER SECURITY POLICY trac.PoliticaAzienda WITH (STATE = ON);
GO

/* 5) Come lavora l'Azienda A (non gestore).
      Atteso: vede SOLO TEST-Azienda-A e TEST-Fornitore-di-A. */
EXEC sp_set_session_context N'Gestore', 1;
DECLARE @A int = (SELECT IdAzienda FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-A');
EXEC sp_set_session_context N'Gestore', 0;
EXEC sp_set_session_context N'IdAzienda', @A;
SELECT N'Come Azienda A: atteso 1 azienda e 1 fornitore' AS Nota;
SELECT IdAzienda, RagioneSociale FROM trac.Aziende WHERE RagioneSociale LIKE N'TEST-%';
SELECT IdAzienda, RagioneSociale FROM trac.Fornitori WHERE RagioneSociale LIKE N'TEST-%';
GO

/* 6) Scrittura per un'altra azienda (A prova a scrivere per B). Atteso: errore di violazione della politica. */
EXEC sp_set_session_context N'Gestore', 1;
DECLARE @A int = (SELECT IdAzienda FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-A');
DECLARE @B int = (SELECT IdAzienda FROM trac.Aziende WHERE RagioneSociale = N'TEST-Azienda-B');
EXEC sp_set_session_context N'Gestore', 0;
EXEC sp_set_session_context N'IdAzienda', @A;
BEGIN TRY
    INSERT trac.Fornitori (IdAzienda, RagioneSociale, Attivo) VALUES (@B, N'TEST-INTRUSO', 1);
    PRINT N'ERRORE DI COLLAUDO: A ha scritto una riga per B!';
END TRY
BEGIN CATCH
    PRINT N'OK  scrittura per altra azienda bloccata: ' + ERROR_MESSAGE();
END CATCH;
-- A prova anche a spostare una propria riga su B
BEGIN TRY
    UPDATE trac.Fornitori SET IdAzienda = @B WHERE RagioneSociale = N'TEST-Fornitore-di-A';
    PRINT N'ERRORE DI COLLAUDO: A ha spostato una riga su B!';
END TRY
BEGIN CATCH
    PRINT N'OK  spostamento su altra azienda bloccato: ' + ERROR_MESSAGE();
END CATCH;
GO

/* 7) Sessione senza azienda e senza gestore. Atteso: 0 righe ovunque. */
EXEC sp_set_session_context N'Gestore', 0;
EXEC sp_set_session_context N'IdAzienda', NULL;
SELECT N'Senza azienda: atteso 0 aziende e 0 fornitori' AS Nota,
       (SELECT COUNT(*) FROM trac.Aziende) AS Aziende, (SELECT COUNT(*) FROM trac.Fornitori) AS Fornitori;
GO

/* 8) Il gestore vede tutto. Atteso: 2 aziende e 2 fornitori di prova. */
EXEC sp_set_session_context N'Gestore', 1;
SELECT N'Come gestore: atteso 2 aziende e 2 fornitori di prova' AS Nota,
       (SELECT COUNT(*) FROM trac.Aziende WHERE RagioneSociale LIKE N'TEST-%') AS Aziende,
       (SELECT COUNT(*) FROM trac.Fornitori WHERE RagioneSociale LIKE N'TEST-%') AS Fornitori;
GO

/* 9) Pulizia dei dati di prova (la riga di Audit resta: è un registro in sola aggiunta, con azienda vuota). */
EXEC sp_set_session_context N'Gestore', 1;
DELETE trac.Fornitori WHERE RagioneSociale LIKE N'TEST-%';
DELETE trac.Aziende WHERE RagioneSociale LIKE N'TEST-%';
SELECT N'Pulizia fatta: atteso 0 e 0' AS Nota,
       (SELECT COUNT(*) FROM trac.Aziende) AS Aziende, (SELECT COUNT(*) FROM trac.Fornitori) AS Fornitori;
GO
/* Se vuoi rispegnere la sicurezza a livello di riga:
   ALTER SECURITY POLICY trac.PoliticaAzienda WITH (STATE = OFF);                                  */
