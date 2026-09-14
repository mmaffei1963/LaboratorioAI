/*───────────────────────────────────────────────────────────────────────────
  Import nativo in ArcLab delle Accettazioni di LaboratorioAI (Lotto + Campione + prove).
  Da eseguire sul database ArcLab (LabAnalisiDati), DOPO ImportArcLab_Controlli.sql
  (che crea il registro e i sinonimi di base).

  Stesso principio: ArcLab LEGGE la staging e SCRIVE se stesso. Idempotenza sul
  CodiceLocale del lotto (registro UNIQUE + transazione atomica).

  ⚠ DELICATO: Lotto/Campione/prove sono il cuore operativo di ArcLab. Questa è una v1
  con mappature prudenti e default ArcLab dove il dato non c'è. VALIDARE aprendo il lotto
  importato nelle schermate di ArcLab (accettazione/campioni) prima di fidarsi.

  Assunzioni (da verificare in fase di test):
   - Trigger di import: lotti in stato 1 (DaTrasmettere), non bozze.
   - CodCampione = progressivo del campione nel lotto (1,2,3…).
   - IdCommessa e IdMatrice devono essere valorizzati nella staging (altrimenti la riga
     finisce nel log errori e non viene importata).
   - Campi non presenti nella staging → lasciati ai default di ArcLab (IdVettore, IdCondTras,
     DataFinePrevista, ecc.).
───────────────────────────────────────────────────────────────────────────*/
USE LabAnalisiDati;
GO

/*── Provenienza sul Lotto ───────────────────────────────────────────────────*/
IF COL_LENGTH('dbo.Lotto', 'CodiceLocaleLabAI') IS NULL
    ALTER TABLE dbo.Lotto ADD CodiceLocaleLabAI VARCHAR(40) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Lotto_CodiceLocaleLabAI' AND object_id = OBJECT_ID('dbo.Lotto'))
    CREATE UNIQUE INDEX UX_Lotto_CodiceLocaleLabAI
        ON dbo.Lotto (CodiceLocaleLabAI) WHERE CodiceLocaleLabAI IS NOT NULL;
GO

/*── Sinonimi verso la staging accettazione (adattare il DB in prod) ─────────*/
IF OBJECT_ID('dbo.syn_LabAI_AccLotti')    IS NOT NULL DROP SYNONYM dbo.syn_LabAI_AccLotti;
CREATE SYNONYM dbo.syn_LabAI_AccLotti    FOR LaboratorioAI_Test.accettazione.Lotti;
GO
IF OBJECT_ID('dbo.syn_LabAI_AccCampioni') IS NOT NULL DROP SYNONYM dbo.syn_LabAI_AccCampioni;
CREATE SYNONYM dbo.syn_LabAI_AccCampioni FOR LaboratorioAI_Test.accettazione.Campioni;
GO
IF OBJECT_ID('dbo.syn_LabAI_AccProve')    IS NOT NULL DROP SYNONYM dbo.syn_LabAI_AccProve;
CREATE SYNONYM dbo.syn_LabAI_AccProve    FOR LaboratorioAI_Test.accettazione.Prove;
GO

/*── Stored procedure di import accettazioni ─────────────────────────────────*/
CREATE OR ALTER PROCEDURE dbo.Importa_Accettazioni
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @IdLottoStaging INT, @Codice VARCHAR(40), @IdLotto INT,
            @IdCampStaging INT, @IdCampNew INT, @NextCod INT;

    DECLARE @pending TABLE (IdAccettazioneLotto INT PRIMARY KEY, CodiceLocale VARCHAR(40));
    INSERT INTO @pending (IdAccettazioneLotto, CodiceLocale)
    SELECT p.IdAccettazioneLotto, p.CodiceLocale
    FROM dbo.syn_LabAI_AccLotti p
    WHERE p.Attivo = 1 AND p.DataCancellazione IS NULL
      AND p.Stato = 1                          -- DaTrasmettere
      AND NOT EXISTS (SELECT 1 FROM dbo.ImportDaLaboratorioAI l
                      WHERE l.TipoEntita = 'Accettazione' AND l.CodiceLocale = p.CodiceLocale);

    IF OBJECT_ID('tempdb..#mapCamp') IS NOT NULL DROP TABLE #mapCamp;
    CREATE TABLE #mapCamp (IdAccStaging INT PRIMARY KEY, IdCampioneNew INT);

    DECLARE curL CURSOR LOCAL FAST_FORWARD FOR SELECT IdAccettazioneLotto, CodiceLocale FROM @pending;
    OPEN curL; FETCH NEXT FROM curL INTO @IdLottoStaging, @Codice;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            DELETE FROM #mapCamp;
            BEGIN TRAN;

                -- 1) Lotto. CodLotto = progressivo in un RANGE DEDICATO a LaboratorioAI (da 100000000),
                --    così i lotti importati sono riconoscibili dal codice e non collidono con la
                --    numerazione normale di ArcLab. Il lock serializza l'assegnazione.
                --    Il codice della staging viene conservato in EtichettaLotto per tracciabilità.
                SELECT @NextCod = ISNULL(MAX(TRY_CONVERT(int, CodLotto)), 100000000 - 1) + 1
                FROM dbo.Lotto WITH (UPDLOCK, HOLDLOCK)
                WHERE TRY_CONVERT(int, CodLotto) >= 100000000;

                INSERT INTO dbo.Lotto (IdCommessa, CodLotto, EtichettaLotto, DataRic, OraRic, BuonoPrelievo, IdRicevitore, CodiceLocaleLabAI)
                SELECT l.IdCommessaArcLab,
                       CAST(@NextCod AS NVARCHAR(10)),
                       LEFT(l.CodLotto, 50),
                       CONVERT(date, l.DataConferimento),
                       CONVERT(datetime2(0), CONVERT(time(0), l.DataConferimento)),
                       l.VerbalePrelievo,
                       COALESCE(l.IdRicevitore, 1870751998),
                       l.CodiceLocale
                FROM dbo.syn_LabAI_AccLotti l
                WHERE l.IdAccettazioneLotto = @IdLottoStaging;
                SET @IdLotto = SCOPE_IDENTITY();

                -- 2) Campioni (uno per volta, per catturare il nuovo IdCampione)
                DECLARE curC CURSOR LOCAL FAST_FORWARD FOR
                    SELECT IdAccettazioneCampione
                    FROM dbo.syn_LabAI_AccCampioni
                    WHERE IdAccettazioneLotto = @IdLottoStaging
                    ORDER BY Progressivo;
                OPEN curC; FETCH NEXT FROM curC INTO @IdCampStaging;
                WHILE @@FETCH_STATUS = 0
                BEGIN
                    INSERT INTO dbo.Campione
                        (IdLotto, CodCampione, IdMatrice, DenomCampione, EtichettaCliente, LuogoPrelievo,
                         PuntoPrelievo, OraPrelievo, TempPrelievo, TempRic, IdLabEsterno, DataInvioLab,
                         NoteLabEsterno, ConsegnaPrevistaLabEst, TermometroAccettazione, RitardoAnalisi,
                         N_Camera, CertificatoNonRichiesto, NoteCampione)
                    SELECT @IdLotto,
                           CAST(c.Progressivo AS NVARCHAR(20)),
                           c.IdMatriceArcLab,
                           c.Descrizione,
                           COALESCE(c.EtichettaCliente, '--'),
                           COALESCE(c.LuogoPrelievo, '--'),
                           COALESCE(c.PuntoPrelievo, 'sconosciuto'),
                           c.OraPrelievo,
                           CONVERT(NVARCHAR(100), c.Temperatura),
                           COALESCE(c.TempRicezione, 'ambiente'),
                           c.IdLabEsternoArcLab,
                           c.DataInvioLab,
                           c.NoteLabEsterno,
                           COALESCE(c.ConsegnaPrevistaLabEstGiorni, 10),
                           COALESCE(c.IdTermometroArcLab, 0),
                           COALESCE(c.RitardoAnalisiGiorni, 0),
                           c.CameraLegionella,
                           CASE WHEN c.CertificatoNonRichiesto = 1 THEN 1 ELSE 0 END,
                           c.Note
                    FROM dbo.syn_LabAI_AccCampioni c
                    WHERE c.IdAccettazioneCampione = @IdCampStaging;

                    SET @IdCampNew = SCOPE_IDENTITY();
                    INSERT INTO #mapCamp (IdAccStaging, IdCampioneNew) VALUES (@IdCampStaging, @IdCampNew);

                    FETCH NEXT FROM curC INTO @IdCampStaging;
                END
                CLOSE curC; DEALLOCATE curC;

                -- 3) Prove di tutti i campioni del lotto
                INSERT INTO dbo.prove (IdCampione, IdRigaMetodo)
                SELECT m.IdCampioneNew, pr.IdRigaMetodoArcLab
                FROM dbo.syn_LabAI_AccProve pr
                JOIN #mapCamp m ON m.IdAccStaging = pr.IdAccettazioneCampione
                WHERE pr.IdRigaMetodoArcLab IS NOT NULL;

                -- 4) Registro (chiave anti-doppione: CodiceLocale del lotto)
                INSERT INTO dbo.ImportDaLaboratorioAI (TipoEntita, CodiceLocale, IdEntitaArcLab)
                VALUES ('Accettazione', @Codice, @IdLotto);
            COMMIT;
        END TRY
        BEGIN CATCH
            IF CURSOR_STATUS('local', 'curC') >= 0 BEGIN CLOSE curC; DEALLOCATE curC; END
            IF @@TRANCOUNT > 0 ROLLBACK;
            INSERT INTO dbo.ImportDaLaboratorioAILog (TipoEntita, CodiceLocale, Messaggio)
            VALUES ('Accettazione', @Codice, LEFT(ERROR_MESSAGE(), 1000));
        END CATCH

        FETCH NEXT FROM curL INTO @IdLottoStaging, @Codice;
    END
    CLOSE curL; DEALLOCATE curL;
    DROP TABLE #mapCamp;
END
GO

/*── Il wrapper richiama anche l'accettazione ────────────────────────────────*/
CREATE OR ALTER PROCEDURE dbo.Importa_Tutto
AS
BEGIN
    SET NOCOUNT ON;
    EXEC dbo.Importa_ProposteControlli;
    EXEC dbo.Importa_Accettazioni;
END
GO

/*───────────────────────────────────────────────────────────────────────────
  TEST MANUALE (su una singola accettazione in stato DaTrasmettere):

    EXEC dbo.Importa_Accettazioni;
    SELECT * FROM dbo.ImportDaLaboratorioAI    WHERE TipoEntita='Accettazione' ORDER BY IdImport DESC;
    SELECT * FROM dbo.ImportDaLaboratorioAILog ORDER BY IdLog DESC;
    SELECT * FROM dbo.Lotto    WHERE CodiceLocaleLabAI IS NOT NULL ORDER BY IdLotto DESC;
    -- poi apri il lotto in ArcLab e verifica campioni/prove/date.

  PULIZIA di un lotto di test (sostituire il CodiceLocale):
    DECLARE @c VARCHAR(40) = 'INCOLLA_CODICELOCALE';
    DECLARE @idl INT = (SELECT IdEntitaArcLab FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='Accettazione' AND CodiceLocale=@c);
    DELETE pr FROM dbo.prove pr JOIN dbo.Campione ca ON ca.IdCampione=pr.IdCampione WHERE ca.IdLotto=@idl;
    DELETE FROM dbo.Campione WHERE IdLotto=@idl;
    DELETE FROM dbo.Lotto    WHERE IdLotto=@idl;
    DELETE FROM dbo.ImportDaLaboratorioAI WHERE TipoEntita='Accettazione' AND CodiceLocale=@c;
───────────────────────────────────────────────────────────────────────────*/
