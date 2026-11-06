@echo off
echo ============================================================
echo   ATTENZIONE: script UNA TANTUM del 13/10/2026.
echo   Rimette in produzione il codice del backup "BUP 13-10-26 (2)":
echo   torna INDIETRO di settimane rispetto alla versione attuale.
echo   Da usare solo se sai esattamente cosa stai facendo.
echo ============================================================
set /p OK=Per continuare scrivi RIPRISTINA e premi Invio: 
if /i not "%OK%"=="RIPRISTINA" (
    echo Annullato: la produzione non e' stata toccata.
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Ripristina-Produzione-13-10.ps1"
pause
