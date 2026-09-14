/*───────────────────────────────────────────────────────────────────────────
  Job SQL Server Agent: importa in ArcLab, ogni 5 minuti, le proposte approvate
  di LaboratorioAI (chiama dbo.Importa_Tutto su LabAnalisiDati).

  Prerequisiti:
   - Aver già eseguito ImportArcLab_Controlli.sql (tabelle, sinonimi, proc).
   - SQL Server Agent avviato (Standard/Developer/Enterprise; NON disponibile in Express).

  Da eseguire in SSMS. Ricrea il job se già presente (idempotente).
───────────────────────────────────────────────────────────────────────────*/
USE msdb;
GO

IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Import_LaboratorioAI')
    EXEC msdb.dbo.sp_delete_job @job_name = N'Import_LaboratorioAI', @delete_unused_schedule = 1;
GO

EXEC msdb.dbo.sp_add_job
    @job_name    = N'Import_LaboratorioAI',
    @description = N'Importa in ArcLab le proposte controllo (e in futuro le accettazioni) approvate da LaboratorioAI.',
    @enabled     = 1;

EXEC msdb.dbo.sp_add_jobstep
    @job_name      = N'Import_LaboratorioAI',
    @step_name     = N'Importa',
    @subsystem     = N'TSQL',
    @database_name = N'LabAnalisiDati',
    @command       = N'EXEC dbo.Importa_Tutto;',
    @retry_attempts = 0;

EXEC msdb.dbo.sp_add_schedule
    @schedule_name        = N'Import_LaboratorioAI_Ogni5Min',
    @freq_type            = 4,          -- giornaliero
    @freq_interval        = 1,
    @freq_subday_type     = 4,          -- ogni N minuti
    @freq_subday_interval = 5,          -- 5 minuti
    @active_start_time    = 000000;     -- da mezzanotte

EXEC msdb.dbo.sp_attach_schedule
    @job_name      = N'Import_LaboratorioAI',
    @schedule_name = N'Import_LaboratorioAI_Ogni5Min';

EXEC msdb.dbo.sp_add_jobserver
    @job_name    = N'Import_LaboratorioAI',
    @server_name = N'(LOCAL)';
GO

/*── Utility ─────────────────────────────────────────────────────────────────
  -- Lancia il job subito (equivalente del futuro bottone "Importa ora"):
      EXEC msdb.dbo.sp_start_job @job_name = N'Import_LaboratorioAI';

  -- Stato/esito ultime esecuzioni:
      SELECT TOP 20 j.name, h.run_date, h.run_time, h.run_status, h.message
      FROM msdb.dbo.sysjobhistory h
      JOIN msdb.dbo.sysjobs j ON j.job_id = h.job_id
      WHERE j.name = N'Import_LaboratorioAI'
      ORDER BY h.instance_id DESC;
───────────────────────────────────────────────────────────────────────────*/
