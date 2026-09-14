/*───────────────────────────────────────────────────────────────────────────
  Import nativo in ArcLab dei Certificati Esterni di LaboratorioAI.
  Da eseguire su LabAnalisiDati, DOPO ImportArcLab_Controlli.sql (registro/log).

  Per ogni certificato esterno "da importare":
    - crea un record in dbo.certificato (IdTipoCertificato=12, NCertificato, DataCertificato, DataSped);
    - collega il campione: Campione.IdCertificato = nuovo id (SOLO se il campione non è già certificato);
    - registra l'import (idempotenza sul CodiceLocale).
  Se il campione è già certificato: NON tocca nulla, logga e segna come processato (niente ritentativi).

  ⚠ NCertificato è UNIQUE in ArcLab: un numero già esistente fa fallire l'insert → finisce nel log.
───────────────────────────────────────────────────────────────────────────*/
USE LabAnalisiDati;
GO

/*── Tipo certificato 12 = "Certificato esterno" (IdTipoCertificato è IDENTITY) ──*/
IF NOT EXISTS (SELECT 1 FROM dbo.TipoCertificato WHERE IdTipoCertificato = 12)
BEGIN
    SET IDENTITY_INSERT dbo.TipoCertificato ON;
    INSERT INTO dbo.TipoCertificato (IdTipoCertificato, TipoCertificato, TipoReport)
    VALUES (12, N'Certificato esterno', N'certificato2');
    SET IDENTITY_INSERT dbo.TipoCertificato OFF;
END
GO

/*── Sinonimo verso la staging (adattare il DB in prod) ──────────────────────*/
IF OBJECT_ID('dbo.syn_LabAI_CertEsterni') IS NOT NULL DROP SYNONYM dbo.syn_LabAI_CertEsterni;
CREATE SYNONYM dbo.syn_LabAI_CertEsterni FOR LaboratorioAI_Test.certificati.EsterniDaImportare;
GO

/*── Stored procedure di import ──────────────────────────────────────────────*/
CREATE OR ALTER PROCEDURE dbo.Importa_CertificatiEsterni
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Codice VARCHAR(40), @IdCampione INT, @Numero NVARCHAR(50),
            @DataCert DATETIME2(6), @DataSped DATETIME2(6),
            @IdCertificato INT, @Esistente INT;

    DECLARE @pending TABLE (
        Id INT PRIMARY KEY, CodiceLocale VARCHAR(40), IdCampione INT,
        Numero NVARCHAR(50), DataCert DATETIME2(6), DataSped DATETIME2(6));

    INSERT INTO @pending (Id, CodiceLocale, IdCampione, Numero, DataCert, DataSped)
    SELECT s.IdCertificatoEsterno, s.CodiceLocale, s.IdCampioneArcLab,
           LEFT(s.NumeroCertificato, 50), s.DataCertificato, s.DataSped
    FROM dbo.syn_LabAI_CertEsterni s
    WHERE s.Attivo = 1 AND s.DataCancellazione IS NULL AND s.Stato = 1
      AND NOT EXISTS (SELECT 1 FROM dbo.ImportDaLaboratorioAI l
                      WHERE l.TipoEntita = 'CertificatoEsterno' AND l.CodiceLocale = s.CodiceLocale);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT CodiceLocale, IdCampione, Numero, DataCert, DataSped FROM @pending;
    OPEN cur; FETCH NEXT FROM cur INTO @Codice, @IdCampione, @Numero, @DataCert, @DataSped;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRAN;
                SELECT @Esistente = IdCertificato
                FROM dbo.Campione WITH (UPDLOCK, HOLDLOCK)
                WHERE IdCampione = @IdCampione;

                IF @Esistente IS NOT NULL AND @Esistente <> 0
                BEGIN
                    -- Campione già certificato: non creo nulla; segno come processato per non ritentare.
                    INSERT INTO dbo.ImportDaLaboratorioAI (TipoEntita, CodiceLocale, IdEntitaArcLab)
                    VALUES ('CertificatoEsterno', @Codice, @Esistente);
                    INSERT INTO dbo.ImportDaLaboratorioAILog (TipoEntita, CodiceLocale, Messaggio)
                    VALUES ('CertificatoEsterno', @Codice,
                            CONCAT('Campione ', @IdCampione, ' già certificato (IdCertificato=', @Esistente, '): import saltato.'));
                END
                ELSE
                BEGIN
                    INSERT INTO dbo.certificato (IdTipoCertificato, NCertificato, DataCertificato, DataSped,
                                                 StampaCertificato, EscludiLimiti, EscludiIncertezza)
                    VALUES (12, @Numero, @DataCert, @DataSped, 0, 0, 0);
                    SET @IdCertificato = SCOPE_IDENTITY();

                    UPDATE dbo.Campione SET IdCertificato = @IdCertificato WHERE IdCampione = @IdCampione;

                    INSERT INTO dbo.ImportDaLaboratorioAI (TipoEntita, CodiceLocale, IdEntitaArcLab)
                    VALUES ('CertificatoEsterno', @Codice, @IdCertificato);
                END
            COMMIT;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
            INSERT INTO dbo.ImportDaLaboratorioAILog (TipoEntita, CodiceLocale, Messaggio)
            VALUES ('CertificatoEsterno', @Codice, LEFT(ERROR_MESSAGE(), 1000));
        END CATCH
        FETCH NEXT FROM cur INTO @Codice, @IdCampione, @Numero, @DataCert, @DataSped;
    END
    CLOSE cur; DEALLOCATE cur;
END
GO

/*── Il wrapper richiama tutti i moduli ──────────────────────────────────────*/
CREATE OR ALTER PROCEDURE dbo.Importa_Tutto
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Importa_ProposteControlli;
    EXEC dbo.Importa_Accettazioni;
    EXEC dbo.Importa_CertificatiEsterni;
END
GO

/*───────────────────────────────────────────────────────────────────────────
  TEST MANUALE:
    EXEC dbo.Importa_CertificatiEsterni;
    SELECT * FROM dbo.ImportDaLaboratorioAI    WHERE TipoEntita='CertificatoEsterno' ORDER BY IdImport DESC;
    SELECT * FROM dbo.ImportDaLaboratorioAILog ORDER BY IdLog DESC;
    -- verifica il campione collegato:
    --   SELECT IdCampione, IdCertificato FROM dbo.Campione WHERE IdCampione = <idcampione>;

  PULIZIA di un test (sostituire il CodiceLocale):
    DECLARE @c VARCHAR(40) = 'INCOLLA_CODICELOCALE';
    DECLARE @idc INT = (SELECT IdEntitaArcLab FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='CertificatoEsterno' AND CodiceLocale=@c);
    UPDATE dbo.Campione SET IdCertificato = NULL WHERE IdCertificato = @idc;
    DELETE FROM dbo.certificato WHERE IdCertificato = @idc;
    DELETE FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='CertificatoEsterno' AND CodiceLocale=@c;
───────────────────────────────────────────────────────────────────────────*/
