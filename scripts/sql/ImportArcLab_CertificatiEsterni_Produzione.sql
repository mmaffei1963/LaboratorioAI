/*───────────────────────────────────────────────────────────────────────────
  MARE.A — Import in ArcLab dei CERTIFICATI ESTERNI registrati da LaboratorioAI
  Versione PRODUZIONE, autonoma (30/09/2026). Da eseguire in SSMS sull'istanza
  che ospita sia LabAnalisiDati (ArcLab) sia LaboratorioAI (192.168.23.1).

  Principio: LaboratorioAI non scrive mai su ArcLab. È questa procedura, lato
  ArcLab, a LEGGERE la tabella di appoggio LaboratorioAI.certificati.EsterniDaImportare
  e a scrivere in ArcLab.

  Per ogni certificato esterno confermato dall'operatore in LaboratorioAI:
    1. crea una riga in dbo.certificato
         IdTipoCertificato = 12 ("Certificato esterno")
         NCertificato      = numero del Rapporto di Prova del laboratorio esterno (originale, es. 2604327-001)
         DataCertificato   = data di emissione del certificato esterno
         DataSped          = data/ora di conferma in LaboratorioAI
    2. collega il campione: Campione.IdCertificato = nuovo IdCertificato,
       SOLO se il campione non ha già un certificato (in quel caso non tocca nulla e lo annota nel log);
    3. registra l'import in dbo.ImportDaLaboratorioAI (UNIQUE su TipoEntita+CodiceLocale):
       una riga già importata non rientra mai, anche con esecuzioni ripetute o concorrenti.
  Ogni certificato è in una transazione separata: un errore su uno non blocca gli altri
  e finisce in dbo.ImportDaLaboratorioAILog.

  Lo script è idempotente: si può rieseguire.
  PRIMA DI ESEGUIRE: backup di LabAnalisiDati e verifica del passo 0.
───────────────────────────────────────────────────────────────────────────*/
USE LabAnalisiDati;
GO

/*── 0. Verifica preliminare: IdTipoCertificato 12 deve essere libero o già un tipo "esterno" ──
   (in produzione MARE.A il 12 è già stato creato a mano come "certificato_esterno": il controllo lo accetta)
   Se in questa installazione ArcLab il 12 è già usato per altro, lo script si ferma:
   scegliere un altro id libero e sostituire 12 in tutto lo script.                               */
IF EXISTS (SELECT 1 FROM dbo.TipoCertificato
           WHERE IdTipoCertificato = 12 AND TipoCertificato NOT LIKE N'%estern%')
BEGIN
    RAISERROR(N'IdTipoCertificato 12 è già usato per un altro tipo: script interrotto, scegliere un id libero.', 16, 1);
    SET NOEXEC ON;
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.TipoCertificato WHERE IdTipoCertificato = 12)
BEGIN
    SET IDENTITY_INSERT dbo.TipoCertificato ON;
    INSERT INTO dbo.TipoCertificato (IdTipoCertificato, TipoCertificato, TipoReport)
    VALUES (12, N'Certificato esterno', N'certificato2');
    SET IDENTITY_INSERT dbo.TipoCertificato OFF;
END
GO

/*── 1. Registro degli import riusciti (anti-doppione) e log degli errori ────*/
IF OBJECT_ID('dbo.ImportDaLaboratorioAI') IS NULL
    CREATE TABLE dbo.ImportDaLaboratorioAI (
        IdImport        INT IDENTITY(1,1) CONSTRAINT PK_ImportLabAI PRIMARY KEY,
        TipoEntita      VARCHAR(40)   NOT NULL,
        CodiceLocale    VARCHAR(40)   NOT NULL,
        IdEntitaArcLab  INT           NULL,
        DataImport      DATETIME2(0)  NOT NULL CONSTRAINT DF_ImportLabAI_Data DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ImportLabAI UNIQUE (TipoEntita, CodiceLocale)
    );
GO
IF OBJECT_ID('dbo.ImportDaLaboratorioAILog') IS NULL
    CREATE TABLE dbo.ImportDaLaboratorioAILog (
        IdLog        INT IDENTITY(1,1) CONSTRAINT PK_ImportLabAILog PRIMARY KEY,
        TipoEntita   VARCHAR(40)    NOT NULL,
        CodiceLocale VARCHAR(40)    NULL,
        Data         DATETIME2(0)   NOT NULL CONSTRAINT DF_ImportLabAILog_Data DEFAULT SYSUTCDATETIME(),
        Messaggio    NVARCHAR(1000) NULL
    );
GO

/*── 2. Sinonimo verso la tabella di appoggio di LaboratorioAI (PRODUZIONE) ──*/
IF OBJECT_ID('dbo.syn_LabAI_CertEsterni') IS NOT NULL DROP SYNONYM dbo.syn_LabAI_CertEsterni;
CREATE SYNONYM dbo.syn_LabAI_CertEsterni FOR LaboratorioAI.certificati.EsterniDaImportare;
GO

/*── 3. Procedura di import ─────────────────────────────────────────────────*/
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

    -- Stato 1 = "da importare"; righe disattivate escluse; già importate escluse.
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
                SET @Esistente = NULL;
                SELECT @Esistente = IdCertificato
                FROM dbo.Campione WITH (UPDLOCK, HOLDLOCK)
                WHERE IdCampione = @IdCampione;

                IF @@ROWCOUNT = 0
                BEGIN
                    -- Campione inesistente: niente da collegare, lo segno come processato.
                    INSERT INTO dbo.ImportDaLaboratorioAI (TipoEntita, CodiceLocale, IdEntitaArcLab)
                    VALUES ('CertificatoEsterno', @Codice, NULL);
                    INSERT INTO dbo.ImportDaLaboratorioAILog (TipoEntita, CodiceLocale, Messaggio)
                    VALUES ('CertificatoEsterno', @Codice, CONCAT('Campione ', @IdCampione, ' non trovato: import saltato.'));
                END
                ELSE IF @Esistente IS NOT NULL AND @Esistente <> 0
                BEGIN
                    -- Campione già certificato: non creo nulla, segno come processato (niente ritentativi).
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
            -- Es. NCertificato già esistente (vincolo UNIQUE): rollback e log; la riga resta da importare.
            IF @@TRANCOUNT > 0 ROLLBACK;
            INSERT INTO dbo.ImportDaLaboratorioAILog (TipoEntita, CodiceLocale, Messaggio)
            VALUES ('CertificatoEsterno', @Codice, LEFT(ERROR_MESSAGE(), 1000));
        END CATCH
        FETCH NEXT FROM cur INTO @Codice, @IdCampione, @Numero, @DataCert, @DataSped;
    END
    CLOSE cur; DEALLOCATE cur;
END
GO

SET NOEXEC OFF;
GO

/*── 4. (FACOLTATIVO) Job SQL Server Agent ogni 5 minuti ─────────────────────
   Richiede SQL Server Agent attivo (non disponibile nell'edizione Express).
   Per crearlo togliere le due righe di commento che racchiudono questo blocco. Job separato, non tocca eventuali altri job.

USE msdb;
GO
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Import_LaboratorioAI_CertificatiEsterni')
    EXEC msdb.dbo.sp_delete_job @job_name = N'Import_LaboratorioAI_CertificatiEsterni', @delete_unused_schedule = 1;
GO
EXEC msdb.dbo.sp_add_job      @job_name = N'Import_LaboratorioAI_CertificatiEsterni', @enabled = 1,
     @description = N'Importa in ArcLab i certificati esterni confermati in LaboratorioAI.';
EXEC msdb.dbo.sp_add_jobstep  @job_name = N'Import_LaboratorioAI_CertificatiEsterni', @step_name = N'Importa',
     @subsystem = N'TSQL', @database_name = N'LabAnalisiDati', @command = N'EXEC dbo.Importa_CertificatiEsterni;';
EXEC msdb.dbo.sp_add_schedule @schedule_name = N'Import_CertEsterni_Ogni5Min',
     @freq_type = 4, @freq_interval = 1, @freq_subday_type = 4, @freq_subday_interval = 5, @active_start_time = 000000;
EXEC msdb.dbo.sp_attach_schedule @job_name = N'Import_LaboratorioAI_CertificatiEsterni', @schedule_name = N'Import_CertEsterni_Ogni5Min';
EXEC msdb.dbo.sp_add_jobserver   @job_name = N'Import_LaboratorioAI_CertificatiEsterni', @server_name = N'(LOCAL)';
GO
───────────────────────────────────────────────────────────────────────────*/

/*── 5. Verifiche ────────────────────────────────────────────────────────────
  -- Cosa c'è da importare (lato LaboratorioAI):
     SELECT * FROM LaboratorioAI.certificati.EsterniDaImportare WHERE Stato = 1 AND Attivo = 1;
  -- Esecuzione manuale:
     EXEC LabAnalisiDati.dbo.Importa_CertificatiEsterni;
  -- Esito:
     SELECT TOP 50 * FROM LabAnalisiDati.dbo.ImportDaLaboratorioAI WHERE TipoEntita = 'CertificatoEsterno' ORDER BY IdImport DESC;
     SELECT TOP 50 * FROM LabAnalisiDati.dbo.ImportDaLaboratorioAILog WHERE TipoEntita = 'CertificatoEsterno' ORDER BY IdLog DESC;
  -- Campione collegato:
     SELECT IdCampione, IdCertificato FROM LabAnalisiDati.dbo.Campione WHERE IdCampione = <id>;

  -- Annullare UN import di prova (sostituire il CodiceLocale):
     DECLARE @c VARCHAR(40) = 'INCOLLA_CODICELOCALE';
     DECLARE @idc INT = (SELECT IdEntitaArcLab FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='CertificatoEsterno' AND CodiceLocale=@c);
     UPDATE dbo.Campione SET IdCertificato = NULL WHERE IdCertificato = @idc;
     DELETE FROM dbo.certificato WHERE IdCertificato = @idc AND IdTipoCertificato = 12;
     DELETE FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='CertificatoEsterno' AND CodiceLocale=@c;
───────────────────────────────────────────────────────────────────────────*/
