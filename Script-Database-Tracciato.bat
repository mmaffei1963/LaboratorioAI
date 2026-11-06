@echo off
REM ============================================================
REM  Prepara lo script SQL con TUTTE le migrazioni di Tracciato
REM  (idempotente: si puo' rieseguire, applica solo quelle mancanti).
REM  NON tocca nessun database: scrive solo il file
REM    C:\Sviluppo\Publish\Tracciato_database.sql
REM  Il file va eseguito in SSMS sul database "Tracciabilita" del server
REM  (da Gen o da un amministratore SQL) PRIMA di pubblicare una versione
REM  che contiene nuove migrazioni.
REM ============================================================

cd /d C:\Sviluppo\LaboratorioAI
if not exist "C:\Sviluppo\Publish\" mkdir "C:\Sviluppo\Publish"

dotnet ef migrations script --idempotent --context ContestoTracciabilita --project src\LaboratorioAI.Tracciabilita.Dati --startup-project src\LaboratorioAI.Tracciabilita.Web --output "C:\Sviluppo\Publish\Tracciato_database.sql"

if errorlevel 1 (
    echo.
    echo ERRORE nella preparazione dello script.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   Script pronto: C:\Sviluppo\Publish\Tracciato_database.sql
echo   Eseguilo in SSMS sul database "Tracciabilita" del server.
echo ============================================================
echo.
pause
