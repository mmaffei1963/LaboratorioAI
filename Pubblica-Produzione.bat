@echo off
REM ============================================================
REM  Pubblica LaboratorioAI.Web in produzione con un doppio click.
REM  Fa tutto in sequenza:
REM    1) dotnet publish (compila e prepara i file in locale)
REM    2) Pubblica-Produzione.ps1 (stop, backup, copia, riavvio,
REM       verifica automatica e ripristino se qualcosa va storto)
REM ============================================================

cd /d "%~dp0"

echo Controlli (postazione e sincronizzazione con GitHub)...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Controlla-Pubblicazione.ps1" -Nome "LaboratorioAI.Web"
if errorlevel 1 (
    echo.
    echo   Pubblicazione interrotta: la produzione NON e' stata toccata.
    echo.
    pause
    exit /b 1
)

echo ============================================================
echo   Pubblicazione LaboratorioAI.Web in produzione
echo ============================================================
echo.
echo Passo 1: pubblicazione locale (dotnet publish)...
echo.

dotnet publish src\LaboratorioAI.Web -c Release -o C:\Sviluppo\Publish\Web

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

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Pubblica-Produzione.ps1"

echo.
echo ============================================================
echo   Finito. Controlla sopra se ci sono stati errori.
echo ============================================================
echo.
pause
