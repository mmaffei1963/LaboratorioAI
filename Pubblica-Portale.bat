@echo off
REM ============================================================
REM  Pubblica il Portale clienti (LaboratorioAI.Portale) in
REM  produzione con un doppio click. Stessa procedura della Web:
REM    1) dotnet publish (compila e prepara i file in locale)
REM    2) Pubblica-Produzione.ps1 con i percorsi del Portale
REM       (stop, backup, copia esclusi i .json, riavvio,
REM       verifica su https://portale.mare-a.com/stato e
REM       ripristino automatico se qualcosa va storto)
REM ============================================================

cd /d C:\Sviluppo\LaboratorioAI

set PRODUZIONE=\\SRVMAREA\marea\LaboratorioAI\Produzione\Portale
set PRODUZIONE_WEB=\\SRVMAREA\marea\LaboratorioAI\Produzione\Web
set STAGING=C:\Sviluppo\Publish\Portale
set BACKUP=\\SRVMAREA\marea\LaboratorioAI\Produzione\Backup\Portale
set VERIFICA=https://portale.mare-a.com/stato

echo ============================================================
echo   Pubblicazione Portale clienti in produzione
echo ============================================================
echo.

if not exist "%PRODUZIONE%\" (
    echo ERRORE: non trovo la cartella di produzione del Portale:
    echo   %PRODUZIONE%
    echo Controlla il percorso in questo file. Niente e' stato toccato.
    echo.
    pause
    exit /b 1
)

REM Il fermo dell'app usa il file _app_offline.htm: se nel Portale manca, lo copio da quello della Web.
if not exist "%PRODUZIONE%\_app_offline.htm" if not exist "%PRODUZIONE%\app_offline.htm" (
    if exist "%PRODUZIONE_WEB%\_app_offline.htm" (
        echo Nel Portale manca _app_offline.htm: lo copio da quello della Web.
        copy /y "%PRODUZIONE_WEB%\_app_offline.htm" "%PRODUZIONE%\_app_offline.htm" >nul
    ) else (
        echo ATTENZIONE: manca _app_offline.htm sia nel Portale sia nella Web.
        echo Senza quel file il Portale non si ferma e la copia dei file puo' fallire.
        echo.
        pause
        exit /b 1
    )
)

echo Passo 1: pubblicazione locale (dotnet publish)...
echo.

dotnet publish src\LaboratorioAI.Portale -c Release -o "%STAGING%"

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
