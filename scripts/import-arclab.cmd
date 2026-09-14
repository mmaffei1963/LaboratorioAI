@echo off
REM ─────────────────────────────────────────────────────────────────────────
REM Import delle proposte/accettazioni approvate di LaboratorioAI in ArcLab.
REM Alternativa al SQL Agent per le edizioni SQL Express (che non hanno l'Agent).
REM Lanciato periodicamente dall'Utilita di pianificazione di Windows.
REM La scrittura la fa SQL Server (proc dbo.Importa_Tutto); questo script la avvia soltanto.
REM ─────────────────────────────────────────────────────────────────────────
set "LOG=%~dp0import-arclab.log"
echo [%date% %time%] avvio >> "%LOG%"
sqlcmd -S PCMAREA01\SQLEXPRESS -d LabAnalisiDati -E -b -Q "EXEC dbo.Importa_Tutto;" >> "%LOG%" 2>&1
if %ERRORLEVEL%==0 (echo [%date% %time%] OK >> "%LOG%") else (echo [%date% %time%] ERRORE %ERRORLEVEL% >> "%LOG%")
