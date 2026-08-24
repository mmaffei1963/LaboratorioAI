@echo off
REM ============================================================
REM  Backup del progetto Laboratorio AI su D:\BACKUP
REM  Da lanciare DOPO aver fatto delle modifiche al programma.
REM  Usa robocopy (gia' incluso in Windows): copia tutto il
REM  progetto in D:\BACKUP\LaboratorioAI, esclusi i file di build.
REM ============================================================

if not exist "D:\BACKUP" mkdir "D:\BACKUP"

echo.
echo Copio il progetto su D:\BACKUP\LaboratorioAI ...
echo.

robocopy "C:\Sviluppo\LaboratorioAI" "D:\BACKUP\LaboratorioAI" /MIR /XD bin obj .vs /NFL /NDL /NP /R:1 /W:1

if errorlevel 8 (
    echo.
    echo  ERRORE nel backup. Controlla che il disco D: sia collegato.
) else (
    echo.
    echo  Backup completato: D:\BACKUP\LaboratorioAI
)

echo.
pause
