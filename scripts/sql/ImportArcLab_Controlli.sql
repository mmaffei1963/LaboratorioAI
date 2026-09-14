/*───────────────────────────────────────────────────────────────────────────
  Import nativo in ArcLab delle Proposte Controllo di LaboratorioAI.
  Da eseguire sul database ArcLab: LabAnalisiDati.

  Principio: è ArcLab che LEGGE la staging di LaboratorioAI e SCRIVE se stesso.
  LaboratorioAI non scrive mai su LabAnalisiDati.

  Idempotenza: registro dbo.ImportDaLaboratorioAI con UNIQUE(TipoEntita, CodiceLocale)
  + inserimento in transazione atomica (Controllo + rigametodo + registro).
  Una proposta già importata NON rientra mai.

  ATTENZIONE: scrive nel DB di produzione ArcLab. Testare prima su copia/backup
  o con una singola proposta, tenendo pronto lo script di pulizia in fondo.

  Nota ambiente: i SINONIMI puntano a LaboratorioAI_Test. In produzione
  ricrearli puntando al DB di produzione (es. LaboratorioAI).
───────────────────────────────────────────────────────────────────────────*/
USE LabAnalisiDati;
GO

/*── 1. Registro degli import riusciti (garanzia anti-doppione) ──────────────*/
IF OBJECT_ID('dbo.ImportDaLaboratorioAI') IS NULL
BEGIN
    CREATE TABLE dbo.ImportDaLaboratorioAI (
        IdImport        INT IDENTITY(1,1) CONSTRAINT PK_ImportLabAI PRIMARY KEY,
        TipoEntita      VARCHAR(40)    NOT NULL,
        CodiceLocale    VARCHAR(40)    NOT NULL,
        IdEntitaArcLab  INT            NULL,
        DataImport      DATETIME2(0)   NOT NULL CONSTRAINT DF_ImportLabAI_Data  DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_ImportLabAI UNIQUE (TipoEntita, CodiceLocale)
    );
END
GO

/*── 2. Log degli import falliti (non blocca i ritentativi) ──────────────────*/
IF OBJECT_ID('dbo.ImportDaLaboratorioAILog') IS NULL
BEGIN
    CREATE TABLE dbo.ImportDaLaboratorioAILog (
        IdLog        INT IDENTITY(1,1) CONSTRAINT PK_ImportLabAILog PRIMARY KEY,
        TipoEntita   VARCHAR(40)   NOT NULL,
        CodiceLocale VARCHAR(40)   NULL,
        Data         DATETIME2(0)  NOT NULL CONSTRAINT DF_ImportLabAILog_Data DEFAULT SYSUTCDATETIME(),
        Messaggio    NVARCHAR(1000) NULL
    );
END
GO

/*── 2-bis. Provenienza sul Controllo: codice locale della proposta LaboratorioAI ──
   Colonna nullable + indice unico filtrato (solo per i controlli nati da LaboratorioAI):
   distingue i controlli e rafforza l'anti-doppione a livello di tabella.                     */
IF COL_LENGTH('dbo.Controllo', 'CodiceLocaleLabAI') IS NULL
    ALTER TABLE dbo.Controllo ADD CodiceLocaleLabAI VARCHAR(40) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Controllo_CodiceLocaleLabAI' AND object_id = OBJECT_ID('dbo.Controllo'))
    CREATE UNIQUE INDEX UX_Controllo_CodiceLocaleLabAI
        ON dbo.Controllo (CodiceLocaleLabAI) WHERE CodiceLocaleLabAI IS NOT NULL;
GO

/*── 3. Sinonimi verso la staging di LaboratorioAI (adattare il DB in prod) ──*/
IF OBJECT_ID('dbo.syn_LabAI_Proposte') IS NOT NULL DROP SYNONYM dbo.syn_LabAI_Proposte;
CREATE SYNONYM dbo.syn_LabAI_Proposte      FOR LaboratorioAI_Test.controlli.Proposte;
GO
IF OBJECT_ID('dbo.syn_LabAI_ProposteRighe') IS NOT NULL DROP SYNONYM dbo.syn_LabAI_ProposteRighe;
CREATE SYNONYM dbo.syn_LabAI_ProposteRighe FOR LaboratorioAI_Test.controlli.RigheMetodo;
GO

/*── 4. Stored procedure di import (solo NUOVI controlli, Stato = Approvato=2) ─*/
CREATE OR ALTER PROCEDURE dbo.Importa_ProposteControlli
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Id INT, @Codice VARCHAR(40), @Nome NVARCHAR(255), @IdControllo INT;

    DECLARE @pending TABLE (IdPropostaControllo INT PRIMARY KEY, CodiceLocale VARCHAR(40), Controllo NVARCHAR(255));

    INSERT INTO @pending (IdPropostaControllo, CodiceLocale, Controllo)
    SELECT p.IdPropostaControllo, p.CodiceLocale, p.Controllo
    FROM dbo.syn_LabAI_Proposte p
    WHERE p.Attivo = 1
      AND p.DataCancellazione IS NULL
      AND p.Stato = 2                         -- Approvato
      AND p.IdControlloEsistenteArcLab IS NULL -- solo nuovi controlli (la modifica sarà una fase a parte)
      AND NOT EXISTS (SELECT 1 FROM dbo.ImportDaLaboratorioAI l
                      WHERE l.TipoEntita = 'PropostaControllo' AND l.CodiceLocale = p.CodiceLocale);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT IdPropostaControllo, CodiceLocale, Controllo FROM @pending;
    OPEN cur;
    FETCH NEXT FROM cur INTO @Id, @Codice, @Nome;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRAN;
                INSERT INTO dbo.Controllo (Controllo, ScontoControllo, Attivo, Listino, CodiceLocaleLabAI)
                VALUES (@Nome, 0, 1, 0, @Codice);
                SET @IdControllo = SCOPE_IDENTITY();

                INSERT INTO dbo.rigametodo
                    (IdMetodo, DenominazioneRiga, IdControllo, IdLabEsterno, IdUnita, ScontoRiga, Arrotonda, MaxDecimali)
                SELECT
                    r.IdMetodoArcLab,
                    LEFT(COALESCE(NULLIF(LTRIM(RTRIM(r.DenominazioneRiga)), ''),
                                  NULLIF(LTRIM(RTRIM(r.Metodo)), ''), N'—'), 100),
                    @IdControllo,
                    r.IdLabEsternoArcLab,
                    COALESCE(r.IdUnitaArcLab, 1),
                    COALESCE(r.ScontoRiga, 0),
                    r.CifreSignificative,
                    COALESCE(r.MaxDecimali, 0)
                FROM dbo.syn_LabAI_ProposteRighe r
                WHERE r.IdPropostaControllo = @Id;

                INSERT INTO dbo.ImportDaLaboratorioAI (TipoEntita, CodiceLocale, IdEntitaArcLab)
                VALUES ('PropostaControllo', @Codice, @IdControllo);
            COMMIT;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
            INSERT INTO dbo.ImportDaLaboratorioAILog (TipoEntita, CodiceLocale, Messaggio)
            VALUES ('PropostaControllo', @Codice, LEFT(ERROR_MESSAGE(), 1000));
        END CATCH

        FETCH NEXT FROM cur INTO @Id, @Codice, @Nome;
    END
    CLOSE cur;
    DEALLOCATE cur;
END
GO

/*── 5. Wrapper (in futuro chiamerà anche l'import accettazione) ─────────────*/
CREATE OR ALTER PROCEDURE dbo.Importa_Tutto
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Importa_ProposteControlli;
END
GO

/*───────────────────────────────────────────────────────────────────────────
  TEST MANUALE (eseguire a mano, NON schedulare finché non validato):

    -- Quante proposte verrebbero importate ora?
    SELECT * FROM dbo.syn_LabAI_Proposte
    WHERE Attivo = 1 AND DataCancellazione IS NULL AND Stato = 2 AND IdControlloEsistenteArcLab IS NULL;

    -- Esegui l'import
    EXEC dbo.Importa_ProposteControlli;

    -- Verifica risultato
    SELECT * FROM dbo.ImportDaLaboratorioAI    ORDER BY IdImport DESC;
    SELECT * FROM dbo.ImportDaLaboratorioAILog ORDER BY IdLog DESC;

    -- Ri-eseguendo NON deve creare doppioni (il registro blocca)
    EXEC dbo.Importa_ProposteControlli;

  PULIZIA di un controllo creato per test (sostituire il CodiceLocale):
    DECLARE @c VARCHAR(40) = 'INCOLLA_CODICELOCALE';
    DECLARE @idc INT = (SELECT IdEntitaArcLab FROM dbo.ImportDaLaboratorioAI
                        WHERE TipoEntita='PropostaControllo' AND CodiceLocale=@c);
    DELETE FROM dbo.rigametodo WHERE IdControllo = @idc;
    DELETE FROM dbo.Controllo  WHERE IdControllo = @idc;
    DELETE FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='PropostaControllo' AND CodiceLocale=@c;
───────────────────────────────────────────────────────────────────────────*/
