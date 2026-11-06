@echo off
REM ============================================================
REM  Backup del progetto Laboratorio AI su D:\BACKUP
REM  Da lanciare DOPO aver fatto delle modifiche al programma.
REM  Ogni backup va in una cartella nuova con data e ora
REM  (es. D:\BACKUP\LaboratorioAI_2026-10-10_0845): si tengono
REM  le ultime 10 copie, le piu' vecchie vengono eliminate.
REM  Copia il progetto esclusi i file di build (bin, obj, .vs, publish)
REM  e il materiale di riferimento che non cambia (docs\Validazione\
REM  Modelli_Studio_Manuali e i database Access .accdb, circa 1 GB):
REM  di quello resta la copia completa del 10/10/2026 su D:\BACKUP.
REM  Ogni copia pesa cosi' poche decine di MB.
REM  La vecchia cartella D:\BACKUP\LaboratorioAI non viene toccata.
REM ============================================================

set COPIE_DA_TENERE=10

if not exist "D:\BACKUP" mkdir "D:\BACKUP"

for /f %%d in ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HHmm"') do set ORA=%%d
set DEST=D:\BACKUP\LaboratorioAI_%ORA%

echo.
echo Copio il progetto in %DEST% ...
echo.

robocopy "C:\Sviluppo\LaboratorioAI" "%DEST%" /E /XD bin obj .vs publish "C:\Sviluppo\LaboratorioAI\docs\Validazione\Modelli_Studio_Manuali" /XF *.accdb /NFL /NDL /NP /R:1 /W:1

if errorlevel 8 (
    echo.
    echo  ERRORE nel backup. Controlla che il disco D: sia collegato.
    echo  Le copie precedenti NON sono state toccate.
    echo.
    pause
    exit /b 1
)

echo.
echo Tengo le ultime %COPIE_DA_TENERE% copie, elimino le piu' vecchie...
powershell -NoProfile -Command "Get-ChildItem 'D:\BACKUP' -Directory -Filter 'LaboratorioAI_*' | Sort-Object Name -Descending | Select-Object -Skip %COPIE_DA_TENERE% | ForEach-Object { Write-Host ('  elimino ' + $_.Name); Remove-Item $_.FullName -Recurse -Force }"

echo.
echo  Backup completato: %DEST%
echo.
pause
