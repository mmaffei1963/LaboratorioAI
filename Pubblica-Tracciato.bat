@echo off
REM ============================================================
REM  Pubblica Tracciato (LaboratorioAI.Tracciabilita.Web) in
REM  produzione con un doppio click. Stessa procedura del Portale:
REM    1) dotnet publish (compila e prepara i file in locale)
REM    2) Pubblica-Produzione.ps1 con i percorsi di Tracciato
REM       (stop, backup, copia esclusi i .json, riavvio,
REM       verifica su https://tracciato.mare-a.com/stato e
REM       ripristino automatico se qualcosa va storto)
REM  ATTENZIONE: non tocca il database. Se ci sono nuove migrazioni
REM  eseguire PRIMA Script-Database-Tracciato.bat e far applicare lo script.
REM ============================================================

cd /d C:\Sviluppo\LaboratorioAI

set PRODUZIONE=\\SRVMAREA\marea\LaboratorioAI\Produzione\Tracciato
set PRODUZIONE_WEB=\\SRVMAREA\marea\LaboratorioAI\Produzione\Web
set STAGING=C:\Sviluppo\Publish\Tracciato
set BACKUP=\\SRVMAREA\marea\LaboratorioAI\Produzione\Backup\Tracciato
set VERIFICA=https://tracciato.mare-a.com/stato

echo ============================================================
echo   Pubblicazione Tracciato in produzione
echo ============================================================
echo.

if not exist "%PRODUZIONE%\" (
    echo ERRORE: non trovo la cartella di produzione di Tracciato:
    echo   %PRODUZIONE%
    echo Il primo impianto va fatto con Gen: vedi DEPLOY_Tracciato.md. Niente e' stato toccato.
    echo.
    pause
    exit /b 1
)

REM Il fermo dell'app usa il file _app_offline.htm: se in Tracciato manca, lo copio da quello della Web.
if not exist "%PRODUZIONE%\_app_offline.htm" if not exist "%PRODUZIONE%\app_offline.htm" (
    if exist "%PRODUZIONE_WEB%\_app_offline.htm" (
        echo In Tracciato manca _app_offline.htm: lo copio da quello della Web.
        copy /y "%PRODUZIONE_WEB%\_app_offline.htm" "%PRODUZIONE%\_app_offline.htm" >nul
    ) else (
        echo ATTENZIONE: manca _app_offline.htm sia in Tracciato sia nella Web.
        echo.
        pause
        exit /b 1
    )
)

if not exist "%BACKUP%\" mkdir "%BACKUP%"

echo Passo 1: pubblicazione locale (dotnet publish)...
echo.

dotnet publish src\LaboratorioAI.Tracciabilita.Web -c Release -o "%STAGING%"

if errorlevel 1 (
    echo.
    echo ============================================================
    echo   ERRORE durante la pubblicazione locale. Interrotto:
    echo   la produzione NON e' stata toccata.
    echo ============================================================
    echo.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   Passo 2: pubblicazione in produzione
echo ============================================================
echo.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Sviluppo\LaboratorioAI\Pubblica-Produzione.ps1" -Staging "%STAGING%" -Produzione "%PRODUZIONE%" -CartellaBackup "%BACKUP%" -UrlVerifica "%VERIFICA%"

echo.
echo ============================================================
echo   Finito. Controlla sopra se ci sono stati errori.
echo ============================================================
echo.
pause
